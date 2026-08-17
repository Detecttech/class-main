using System;
using System.Collections.Generic;
using System.Linq;
using QuizBattle.Arena;
using QuizBattle.Arena.Vfx;
using QuizBattle.Arena.Visuals;
using QuizBattle.Characters;
using QuizBattle.GameState.MockEngine;
using QuizBattle.UI.HUD;
using UnityEngine;

namespace QuizBattle.GameState
{
    /// Drives a local, no-networking "mock match" end to end using MockEngine — validates
    /// the race-to-the-goal/elimination win conditions and independent-per-player question
    /// flow before any server is involved. Auto-play only for now (dummy players answer
    /// with a biased coin flip); a human-controlled path arrives once this loop is proven out.
    public class GameManager : MonoBehaviour
    {
        private GridController _grid;
        private HudController _hud;
        private ArenaRig _rig;
        private List<CharacterDefinitionSO> _characterDefs;
        private MatchState _state;
        private readonly Dictionary<int, CharacterToken> _tokens = new Dictionary<int, CharacterToken>();

        public static GameManager Bootstrap(List<CharacterDefinitionSO> characterDefs)
        {
            CharacterCatalog.Clear();
            foreach (var def in characterDefs) CharacterCatalog.Register(CharacterConfig.FromDefinition(def));

            var gridObj = new GameObject("Grid");
            var grid = gridObj.AddComponent<GridController>();

            var hud = HudController.Create();
            var rig = ArenaEnvironment.Acquire(new Color(0.08f, 0.08f, 0.13f));

            var managerObj = new GameObject("GameManager");
            var manager = managerObj.AddComponent<GameManager>();
            manager._grid = grid;
            manager._hud = hud;
            manager._rig = rig;
            manager._characterDefs = characterDefs;
            return manager;
        }

        /// Spreads players evenly across the bottom row, one lane each — mirrors
        /// server/src/matchEngine/LiveMatchRegistry.ts's startPositions().
        private static GridPos[] StartPositions(int count, int gridWidth)
        {
            var positions = new GridPos[count];
            for (int i = 0; i < count; i++)
            {
                positions[i] = new GridPos(Mathf.FloorToInt((i + 0.5f) * gridWidth / count), 0);
            }
            return positions;
        }

        /// Runs a full match synchronously (no coroutines) so it can be driven from Editor
        /// tooling without needing Play mode's ticking player loop. Players answer
        /// independently — each gets their own random question in turn, rather than a
        /// shared/synced round — so this loops player-by-player instead of round-by-round.
        public MatchResult RunAutoPlayMatch(int seed, int maxRounds = 20)
        {
            var rng = new System.Random(seed);
            _state = MatchEngine.CreateMatch(1, MatchMode.Ffa, maxRounds);
            _grid.BuildGrid(_state.gridWidth, _state.gridHeight, _state.gridHeight - 1);
            PositionCamera();

            var positions = StartPositions(_characterDefs.Count, _state.gridWidth);
            for (int i = 0; i < _characterDefs.Count; i++)
            {
                var def = _characterDefs[i];
                var startPos = positions[i];
                var player = MatchEngine.AddPlayer(_state, i + 1, def.displayName, def.characterId, null, startPos);
                var token = CharacterToken.Create(def.displayName, CharacterVisual.From(def), _grid.TileToWorldPos(startPos.x, startPos.y));
                token.SetHp(player.hp, player.maxHp);
                _tokens[player.playerId] = token;
            }

            MatchEngine.StartMatch(_state);
            _hud.Log("Match started! Race to the top row.");

            int questionSeq = 1;
            int guard = 0;
            while (_state.result == null && guard < 400)
            {
                guard++;
                foreach (var player in _state.players.Values.Where(p => p.alive).ToList())
                {
                    if (_state.result != null) break;

                    int correctIndex = rng.Next(4);
                    var choices = new[] { "Option A", "Option B", "Option C", "Option D" };
                    int qid = questionSeq++;
                    MatchEngine.PushQuestion(_state, player.playerId, qid, correctIndex);
                    _hud.ShowQuestion(player.questionsAnswered + 1, $"Demo question #{qid}", choices);

                    // Biased toward correct (70%) so streaks and lane progress reliably occur in a short demo.
                    int chosen = rng.NextDouble() < 0.7 ? correctIndex : (correctIndex + 1) % 4;
                    var result = MatchEngine.SubmitAnswer(_state, player.playerId, chosen, () => rng.NextDouble());
                    UpdateVisuals();

                    if (result.ok && _tokens.TryGetValue(player.playerId, out var pt))
                    {
                        FloatingCombatText.Spawn(pt.transform.position + Vector3.up * 1.5f, player.consecutiveCorrect >= 2 ? $"STREAK x{player.consecutiveCorrect}!" : "CORRECT!", QuizBattlePalette.GoldTrim, 1.05f);
                    }

                    if (result.rewardOffered != null)
                    {
                        HandleReward(player, result.rewardOffered, rng);
                        UpdateVisuals();
                    }

                    if (_state.result != null)
                    {
                        _hud.Log($"MATCH OVER — winner: {_state.result.winnerId} ({_state.result.reason})");
                    }
                }
            }

            return _state.result;
        }

        private void HandleReward(PlayerState player, PendingReward reward, System.Random rng)
        {
            if (reward.type == RewardType.AttackChoice)
            {
                var target = PickRandomTarget(player, rng);
                if (target == null)
                {
                    MatchEngine.WaiveReward(_state, player.playerId, reward.rewardId);
                    return;
                }
                var atk = MatchEngine.UseAttack(_state, player.playerId, reward.rewardId, target.playerId);
                if (atk.ok)
                {
                    if (_tokens.TryGetValue(player.playerId, out var attackerToken) && _tokens.TryGetValue(target.playerId, out var targetToken))
                    {
                        AbilityVfxPlayer.Play(atk.outcome.vfxTag, attackerToken.transform.position, targetToken.transform.position, !target.alive);
                        FloatingCombatText.Spawn(targetToken.transform.position + Vector3.up * 1.5f, $"-{atk.outcome.damage} HP", QuizBattlePalette.RoofTilesRed, 1.25f);
                    }
                    _hud.Log($"{player.name} attacks {target.name} for {atk.outcome.damage} dmg!");
                }
            }
            else if (reward.type == RewardType.Freeze)
            {
                var target = PickRandomTarget(player, rng);
                if (target == null)
                {
                    MatchEngine.WaiveReward(_state, player.playerId, reward.rewardId);
                    return;
                }
                var frz = MatchEngine.UseFreeze(_state, player.playerId, reward.rewardId, target.playerId);
                if (frz.ok)
                {
                    if (_tokens.TryGetValue(player.playerId, out var casterToken) && _tokens.TryGetValue(target.playerId, out var targetToken))
                    {
                        AbilityVfxPlayer.Play("vfx_freeze", casterToken.transform.position, targetToken.transform.position, eliminated: false);
                        FloatingCombatText.Spawn(targetToken.transform.position + Vector3.up * 1.5f, "FROZEN!", QuizBattlePalette.WaterBlue, 1.15f);
                    }
                    _hud.Log($"{player.name} freezes {target.name}!");
                }
            }
            else
            {
                MatchEngine.ConsumeBonusMove(_state, player.playerId, reward.rewardId);
                _hud.Log($"{player.name} got a bonus move toward the goal!");
            }
        }

        private PlayerState PickRandomTarget(PlayerState attacker, System.Random rng)
        {
            var targets = _state.players.Values.Where(p => p.alive && p.playerId != attacker.playerId).ToList();
            return targets.Count == 0 ? null : targets[rng.Next(targets.Count)];
        }

        private void UpdateVisuals()
        {
            foreach (var player in _state.players.Values)
            {
                if (!_tokens.TryGetValue(player.playerId, out var token)) continue;
                token.MoveTo(_grid.TileToWorldPos(player.pos.x, player.pos.y));
                token.SetHp(player.hp, player.maxHp);
                if (player.consecutiveCorrect >= 2) token.SetStreak(player.consecutiveCorrect);
                if (!player.alive) token.SetEliminated();
                token.SetFrozen(player.frozen);
            }
        }

        private void PositionCamera()
        {
            ArenaEnvironment.FrameGrid(_rig, _grid, _state.gridWidth, _state.gridHeight);
        }
    }
}
