using System;
using System.Collections.Generic;

namespace QuizBattle.GameState.MockEngine
{
    /// Mirrors server/src/matchEngine/RewardResolver.ts — keep the two in sync.
    public static class RewardResolver
    {
        private const double AttackChoiceWeight = 0.6;
        private static int _nextRewardSeq = 1;

        /// Anti-repeat rule: if the roll would grant another attack_choice immediately
        /// after the player's last *consumed* reward was also an attack, it is
        /// downgraded to bonus_move — "can't attack twice in a row" when every v1
        /// character has exactly one attack.
        public static PendingReward RollReward(PlayerState player, int currentQuestionCount, Func<double> rng = null)
        {
            rng ??= () => UnityEngine.Random.value;
            var type = rng() < AttackChoiceWeight ? RewardType.AttackChoice : RewardType.BonusMove;
            if (type == RewardType.AttackChoice && player.lastRewardType == RewardType.AttackChoice)
            {
                type = RewardType.BonusMove;
            }
            return new PendingReward
            {
                rewardId = $"reward-{_nextRewardSeq++}",
                type = type,
                expiresAtQuestion = currentQuestionCount + MatchState.RewardExpiryQuestions,
            };
        }

        /// Each player's reward expiry is evaluated against their own question count,
        /// not a shared round — call this for a single player right after their
        /// questionsAnswered count changes.
        public static void ExpireStaleRewards(IEnumerable<PlayerState> players, int currentQuestionCount)
        {
            foreach (var player in players)
            {
                if (player.pendingReward != null && player.pendingReward.expiresAtQuestion < currentQuestionCount)
                {
                    player.pendingReward = null;
                }
            }
        }

        public struct ConsumeResult
        {
            public bool ok;
            public string error;
        }

        public static ConsumeResult ConsumeReward(PlayerState player, string rewardId)
        {
            if (player.pendingReward == null || player.pendingReward.rewardId != rewardId)
            {
                return new ConsumeResult { ok = false, error = "no_such_reward" };
            }
            player.lastRewardType = player.pendingReward.type;
            player.pendingReward = null;
            return new ConsumeResult { ok = true };
        }
    }
}
