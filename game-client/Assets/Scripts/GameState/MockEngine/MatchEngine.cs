using System;
using System.Collections.Generic;
using System.Linq;

namespace QuizBattle.GameState.MockEngine
{
    /// Mirrors server/src/matchEngine/MatchEngine.ts — the single authoritative core the
    /// server also implements. This local copy exists to validate game feel on one
    /// device before networking is wired up (see project plan, Phase 1); once the Unity
    /// client talks to the real server, gameplay scripts should read state from
    /// MatchStateStore (server truth) instead of driving this engine directly.
    public static class MatchEngine
    {
        public static MatchState CreateMatch(int matchId, MatchMode mode, int maxRounds = 20, int? gridHeight = null) =>
            new MatchState(matchId, mode, maxRounds, gridHeight);

        public static PlayerState AddPlayer(MatchState state, int playerId, string name, string characterId, string team, GridPos startPos)
        {
            var character = CharacterCatalog.Get(characterId);
            var player = new PlayerState
            {
                playerId = playerId,
                name = name,
                characterId = characterId,
                team = team,
                hp = character.maxHp,
                maxHp = character.maxHp,
                pos = startPos,
                alive = true,
            };
            state.players[playerId] = player;
            return player;
        }

        public static void StartMatch(MatchState state)
        {
            state.status = MatchStatus.Active;
        }

        /// Moves a player toward the goal row (gridHeight - 1), clamped there, and flags
        /// goalReached once they hit it. The lane is a single dimension — x never changes
        /// after the player's starting position.
        private static void AdvancePlayer(PlayerState player, int gridHeight, int steps)
        {
            int goalY = gridHeight - 1;
            int nextY = Math.Min(goalY, player.pos.y + steps);
            player.pos = new GridPos(player.pos.x, nextY);
            if (nextY >= goalY) player.goalReached = true;
        }

        public static void PushQuestion(MatchState state, int playerId, int questionId, int correctIndex)
        {
            if (!state.players.TryGetValue(playerId, out var player)) return;
            player.currentQuestion = new ActiveQuestion { questionId = questionId, correctIndex = correctIndex, questionNumber = player.questionsAnswered + 1 };
        }

        public struct SubmitAnswerResult
        {
            public bool ok;
            public string error;
            public bool correct;
            public int streakCount;
            public PendingReward rewardOffered;
            public GridPos newPos;
            public bool goalReached;
            public int dotDamage;
            public MatchResult result;
        }

        public static SubmitAnswerResult SubmitAnswer(MatchState state, int playerId, int choiceIndex, Func<double> rng = null)
        {
            if (!state.players.TryGetValue(playerId, out var player) || !player.alive)
                return new SubmitAnswerResult { ok = false, error = "not_in_match" };
            if (player.currentQuestion == null)
                return new SubmitAnswerResult { ok = false, error = "no_active_question" };

            bool correct = choiceIndex == player.currentQuestion.correctIndex;
            player.currentQuestion = null;
            player.questionsAnswered += 1;

            player.consecutiveCorrect = correct ? player.consecutiveCorrect + 1 : 0;
            if (correct)
            {
                player.totalCorrectAnswers += 1;
                player.maxStreak = Math.Max(player.maxStreak, player.consecutiveCorrect);
            }

            PendingReward rewardOffered = null;
            if (correct && player.consecutiveCorrect >= 2 && player.pendingReward == null)
            {
                var reward = RewardResolver.RollReward(player, player.questionsAnswered, rng);
                player.pendingReward = reward;
                rewardOffered = reward;
            }
            RewardResolver.ExpireStaleRewards(new[] { player }, player.questionsAnswered);

            // A correct answer is what drives you toward the goal — this replaces the
            // old movement-budget/direction system entirely. A frozen player still
            // answers/builds streak normally; only this one advance is consumed/skipped.
            if (correct)
            {
                if (player.frozen)
                {
                    player.frozen = false;
                }
                else
                {
                    AdvancePlayer(player, state.gridHeight, 1);
                }
            }

            int dotDamage = CombatResolver.TickDot(player);

            var result = CheckWinCondition(state);
            state.result = result;
            if (result != null) state.status = MatchStatus.Completed;

            return new SubmitAnswerResult
            {
                ok = true,
                correct = correct,
                streakCount = player.consecutiveCorrect,
                rewardOffered = rewardOffered,
                newPos = player.pos,
                goalReached = player.goalReached,
                dotDamage = dotDamage,
                result = result,
            };
        }

        /// A player's personal question timer expired with no answer submitted — scored
        /// the same as a wrong answer (streak reset, no movement), but still ticks their
        /// own DoT and advances their personal question count.
        public static SubmitAnswerResult TimeoutAnswer(MatchState state, int playerId)
        {
            if (!state.players.TryGetValue(playerId, out var player) || !player.alive || player.currentQuestion == null)
                return new SubmitAnswerResult { ok = false, error = "no_active_question" };

            player.currentQuestion = null;
            player.questionsAnswered += 1;
            player.consecutiveCorrect = 0;

            // A reward the player never acted on (missed/ignored the popup) must not linger —
            // SubmitAnswer's pendingReward gate would otherwise silently block every future
            // streak reward until natural expiry, several questions later.
            if (player.pendingReward != null)
            {
                RewardResolver.ConsumeReward(player, player.pendingReward.rewardId);
            }
            RewardResolver.ExpireStaleRewards(new[] { player }, player.questionsAnswered);

            int dotDamage = CombatResolver.TickDot(player);

            var result = CheckWinCondition(state);
            state.result = result;
            if (result != null) state.status = MatchStatus.Completed;

            return new SubmitAnswerResult
            {
                ok = true,
                correct = false,
                streakCount = 0,
                rewardOffered = null,
                newPos = player.pos,
                goalReached = player.goalReached,
                dotDamage = dotDamage,
                result = result,
            };
        }

        /// Every living player attacker could legally target (alive, not self, not a
        /// teammate) other than excludeId — used both to enforce "can't target the same
        /// player twice in a row" and to waive that rule when there's genuinely no one
        /// else to pick (e.g. a 2-player match), rather than soft-locking the reward entirely.
        private static bool HasAlternativeTarget(MatchState state, PlayerState attacker, int excludeId)
        {
            foreach (var p in state.players.Values)
            {
                if (!p.alive || p.playerId == attacker.playerId || p.playerId == excludeId) continue;
                if (state.mode == MatchMode.Teams && attacker.team != null && attacker.team == p.team) continue;
                return true;
            }
            return false;
        }

        private static string ValidateTarget(MatchState state, PlayerState attacker, PlayerState target)
        {
            if (target == null || !target.alive) return "invalid_target";
            if (attacker.playerId == target.playerId) return "cannot_target_self";
            if (state.mode == MatchMode.Teams && attacker.team != null && attacker.team == target.team) return "friendly_fire_blocked";
            if (attacker.lastTargetedPlayerId == target.playerId && HasAlternativeTarget(state, attacker, target.playerId))
                return "repeat_target_blocked";
            return null;
        }

        public struct UseAttackResult
        {
            public bool ok;
            public string error;
            public CombatResolver.AttackOutcome outcome;
            public MatchResult result;
        }

        public static UseAttackResult UseAttack(MatchState state, int playerId, string rewardId, int targetId)
        {
            if (!state.players.TryGetValue(playerId, out var attacker) || !attacker.alive)
                return new UseAttackResult { ok = false, error = "not_in_match" };
            state.players.TryGetValue(targetId, out var target);
            var targetError = ValidateTarget(state, attacker, target);
            if (targetError != null) return new UseAttackResult { ok = false, error = targetError };
            if (attacker.pendingReward == null || attacker.pendingReward.rewardId != rewardId || attacker.pendingReward.type != RewardType.AttackChoice)
                return new UseAttackResult { ok = false, error = "no_such_reward" };

            var consumed = RewardResolver.ConsumeReward(attacker, rewardId);
            if (!consumed.ok) return new UseAttackResult { ok = false, error = consumed.error };
            attacker.lastTargetedPlayerId = targetId;

            var ability = CombatResolver.AttackFor(attacker.characterId);
            var outcome = CombatResolver.ResolveAttack(attacker, target, ability);

            var result = CheckWinCondition(state);
            state.result = result;
            if (result != null) state.status = MatchStatus.Completed;

            return new UseAttackResult { ok = true, outcome = outcome, result = result };
        }

        public struct UseFreezeResult
        {
            public bool ok;
            public string error;
        }

        /// Freezes the target: their next correct answer won't advance them. Doesn't
        /// touch HP/position itself, so — unlike attack/bonus_move — it can never itself
        /// produce a match result.
        public static UseFreezeResult UseFreeze(MatchState state, int playerId, string rewardId, int targetId)
        {
            if (!state.players.TryGetValue(playerId, out var attacker) || !attacker.alive)
                return new UseFreezeResult { ok = false, error = "not_in_match" };
            state.players.TryGetValue(targetId, out var target);
            var targetError = ValidateTarget(state, attacker, target);
            if (targetError != null) return new UseFreezeResult { ok = false, error = targetError };
            if (attacker.pendingReward == null || attacker.pendingReward.rewardId != rewardId || attacker.pendingReward.type != RewardType.Freeze)
                return new UseFreezeResult { ok = false, error = "no_such_reward" };

            var consumed = RewardResolver.ConsumeReward(attacker, rewardId);
            if (!consumed.ok) return new UseFreezeResult { ok = false, error = consumed.error };
            attacker.lastTargetedPlayerId = targetId;

            target.frozen = true;
            return new UseFreezeResult { ok = true };
        }

        public struct WaiveRewardResult
        {
            public bool ok;
            public string error;
        }

        /// Clears a pending reward with no other side effect — used when there's no
        /// valid target to act on it (e.g. attack_choice/freeze with no living
        /// opponent left). Mirrors server/src/matchEngine/MatchEngine.ts's
        /// waiveReward — without this, an un-consumed reward stays pending and blocks
        /// every future reward roll until it naturally expires.
        public static WaiveRewardResult WaiveReward(MatchState state, int playerId, string rewardId)
        {
            if (!state.players.TryGetValue(playerId, out var player))
                return new WaiveRewardResult { ok = false, error = "not_in_match" };
            if (player.pendingReward == null || player.pendingReward.rewardId != rewardId)
                return new WaiveRewardResult { ok = false, error = "no_such_reward" };

            var consumed = RewardResolver.ConsumeReward(player, rewardId);
            return new WaiveRewardResult { ok = consumed.ok, error = consumed.error };
        }

        public struct ConsumeBonusMoveResult
        {
            public bool ok;
            public string error;
            public GridPos newPos;
            public bool goalReached;
            public MatchResult result;
        }

        /// Consuming a bonus_move reward advances the player extra steps up their lane
        /// immediately — there's no separate directional "move" action anymore since a
        /// lane only has one direction to go.
        public static ConsumeBonusMoveResult ConsumeBonusMove(MatchState state, int playerId, string rewardId)
        {
            if (!state.players.TryGetValue(playerId, out var player) || !player.alive)
                return new ConsumeBonusMoveResult { ok = false, error = "not_in_match" };
            if (player.pendingReward == null || player.pendingReward.rewardId != rewardId || player.pendingReward.type != RewardType.BonusMove)
                return new ConsumeBonusMoveResult { ok = false, error = "no_such_reward" };

            var consumed = RewardResolver.ConsumeReward(player, rewardId);
            if (!consumed.ok) return new ConsumeBonusMoveResult { ok = false, error = consumed.error };

            var character = CharacterCatalog.Get(player.characterId);
            int bonus = character.abilityType == QuizBattle.Characters.AbilityType.Passive && character.bonusMoveSteps > 0
                ? character.bonusMoveSteps
                : MatchState.DefaultBonusMoveSteps;
            AdvancePlayer(player, state.gridHeight, bonus);

            var result = CheckWinCondition(state);
            state.result = result;
            if (result != null) state.status = MatchStatus.Completed;

            return new ConsumeBonusMoveResult { ok = true, newPos = player.pos, goalReached = player.goalReached, result = result };
        }

        private static Dictionary<object, List<PlayerState>> LivingGroups(MatchState state)
        {
            var groups = new Dictionary<object, List<PlayerState>>();
            foreach (var player in state.players.Values)
            {
                if (!player.alive) continue;
                object key = state.mode == MatchMode.Teams && player.team != null ? player.team : player.playerId;
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<PlayerState>();
                    groups[key] = list;
                }
                list.Add(player);
            }
            return groups;
        }

        public static MatchResult CheckWinCondition(MatchState state)
        {
            var groups = LivingGroups(state);

            // Race win takes priority: first living player (or, in teams mode, any
            // member of a team) to reach the goal row wins outright.
            foreach (var (key, players) in groups)
            {
                if (players.Any(p => p.goalReached))
                {
                    return new MatchResult { winnerId = key, reason = WinReason.Goal };
                }
            }

            if (groups.Count == 1)
            {
                return new MatchResult { winnerId = groups.Keys.First(), reason = WinReason.Hp };
            }
            if (groups.Count == 0)
            {
                return new MatchResult { winnerId = null, reason = WinReason.Hp };
            }

            // Forced decision once any living player has been asked enough questions —
            // replaces the old round-cap zone-control tiebreak with lane progress
            // (furthest up the board wins, tiebroken by total HP).
            bool anyoneAtCap = state.players.Values.Any(p => p.alive && p.questionsAnswered >= state.maxRounds);
            if (anyoneAtCap)
            {
                object bestKey = null;
                int bestProgress = -1;
                int bestHp = -1;
                foreach (var (key, players) in groups)
                {
                    int progress = players.Max(p => p.pos.y);
                    int hp = players.Sum(p => p.hp);
                    if (progress > bestProgress || (progress == bestProgress && hp > bestHp))
                    {
                        bestKey = key;
                        bestProgress = progress;
                        bestHp = hp;
                    }
                }
                return new MatchResult { winnerId = bestKey, reason = WinReason.Progress };
            }

            return null;
        }
    }
}
