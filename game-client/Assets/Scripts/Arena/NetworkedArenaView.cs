using System.Collections.Generic;
using QuizBattle.Arena.Vfx;
using QuizBattle.Arena.Visuals;
using QuizBattle.Characters;
using QuizBattle.GameState;
using QuizBattle.Networking.Protocol;
using QuizBattle.UI.HUD;
using UnityEngine;

namespace QuizBattle.Arena
{
    /// Renders a MatchStateStore (real server truth) using the same visual building
    /// blocks as the Phase 1 local mock demo (GridController/CharacterToken/HudController).
    /// This class only reacts to store events — it never calls into MockEngine or computes
    /// outcomes itself, per the "renderer of server state" rule from the project plan.
    public class NetworkedArenaView
    {
        private readonly GridController _grid;
        private readonly HudController _hud;
        private readonly ArenaRig _rig;
        private readonly MatchStateStore _store;
        private readonly Dictionary<string, CharacterVisual> _characterVisuals;
        private readonly Dictionary<int, CharacterToken> _tokens = new Dictionary<int, CharacterToken>();

        public NetworkedArenaView(GridController grid, HudController hud, ArenaRig rig, MatchStateStore store, Dictionary<string, CharacterVisual> characterVisuals)
        {
            _grid = grid;
            _hud = hud;
            _rig = rig;
            _store = store;
            _characterVisuals = characterVisuals;

            store.LobbyUpdated += s => _hud.Log($"Lobby: {s.Players.Count} player(s) joined");
            store.MatchStarted += OnMatchStarted;
            store.QuestionPushed += OnQuestionPushed;
            store.PlayerAdvanced += OnPlayerAdvanced;
            store.AttackResolved += OnAttackResolved;
            store.FreezeResolved += OnFreezeResolved;
            store.PlayerEliminated += OnPlayerEliminated;
            store.MatchEnded += OnMatchEnded;
            store.ServerError += e => _hud.Log($"[error] {e.Code}: {e.Message}");

            // match_start can arrive (and populate store.Players) before this view
            // finishes subscribing — e.g. during the Lobby→Arena scene transition. If we
            // missed the event, build the tokens from current store state now instead of
            // silently ending up with an empty _tokens map that every later update
            // (HP/attack/elimination) would then no-op against.
            if (store.Players.Count > 0)
            {
                BuildArenaFromCurrentState();
            }

            // The server pushes match_start immediately followed by each player's first
            // question, both synchronously — so the first question_push very often also
            // arrives (and gets consumed into store.CurrentQuestion) before this view
            // finishes subscribing, same race as above. Without this the HUD's question
            // panel just stays blank until the *second* question happens to arrive.
            if (store.CurrentQuestion != null)
            {
                OnQuestionPushed(store.CurrentQuestion);
            }
        }

        private void OnMatchStarted(MatchStartPayload start)
        {
            BuildArenaFromCurrentState();
            _hud.Log("Match started (server-driven)! Race to the top row.");
        }

        private void BuildArenaFromCurrentState()
        {
            _grid.BuildGrid(_store.GridWidth, _store.GridHeight, _store.GoalRow);
            PositionCamera(_store.GridWidth, _store.GridHeight);

            _tokens.Clear();
            foreach (var p in _store.Players.Values)
            {
                var visual = _characterVisuals.TryGetValue(p.characterId, out var v) ? v : CharacterVisual.Fallback(Color.gray);
                var token = CharacterToken.Create(p.name, visual, _grid.TileToWorldPos(p.pos.x, p.pos.y));
                token.SetHp(p.hp, p.maxHp);
                if (p.streak >= 2) token.SetStreak(p.streak);
                if (!p.alive) token.SetEliminated();
                _tokens[p.playerId] = token;
            }
        }

        private void OnQuestionPushed(QuestionPushPayload q)
        {
            _hud.ShowQuestion(q.QuestionNumber, q.Text, q.Choices);
        }

        /// Fires whenever any player's turn resolves — answered, timed out, or consumed
        /// a bonus_move — since every player now advances independently instead of in a
        /// synced batch round. Replaces both the old round_resolved and move_result handling.
        private void OnPlayerAdvanced(PlayerAdvancedPayload a)
        {
            if (!_tokens.TryGetValue(a.PlayerId, out var token)) return;

            token.MoveTo(_grid.TileToWorldPos(a.NewGridPos.X, a.NewGridPos.Y));
            token.SetHp(a.Hp, a.MaxHp);
            if (a.Streak >= 2)
            {
                token.SetStreak(a.Streak);
                FloatingCombatText.Spawn(token.transform.position + Vector3.up * 1.5f, $"STREAK x{a.Streak}!", QuizBattlePalette.FireGlow, 1.15f);
            }
            else
            {
                FloatingCombatText.Spawn(token.transform.position + Vector3.up * 1.5f, "CORRECT!", QuizBattlePalette.GoldTrim, 1.0f);
            }

            if (!a.Alive) token.SetEliminated();
            token.SetFrozen(a.Frozen);
        }

        private void OnAttackResolved(AttackResultPayload a)
        {
            // Update immediately rather than waiting for the target's next turn — the
            // attack already landed server-side the moment this event arrived.
            if (_tokens.TryGetValue(a.TargetId, out var token))
            {
                Vector3 from = _tokens.TryGetValue(a.AttackerId, out var attackerToken) ? attackerToken.transform.position : token.transform.position;
                AbilityVfxPlayer.Play(a.VfxTag, from, token.transform.position, a.Eliminated);

                token.SetHp(a.TargetHpAfter, _store.Players.TryGetValue(a.TargetId, out var p) ? p.maxHp : a.TargetHpAfter);
                FloatingCombatText.Spawn(token.transform.position + Vector3.up * 1.5f, $"-{a.Damage} HP", QuizBattlePalette.RoofTilesRed, 1.25f);
                if (a.Eliminated) token.SetEliminated();
            }
            _hud.Log($"Player {a.AttackerId} attacks player {a.TargetId} for {a.Damage} dmg!");
        }

        private void OnFreezeResolved(FreezeResultPayload f)
        {
            if (_tokens.TryGetValue(f.TargetId, out var token))
            {
                Vector3 from = _tokens.TryGetValue(f.CasterId, out var casterToken) ? casterToken.transform.position : token.transform.position;
                AbilityVfxPlayer.Play("vfx_freeze", from, token.transform.position, eliminated: false);
                token.SetFrozen(true);
                FloatingCombatText.Spawn(token.transform.position + Vector3.up * 1.5f, "FROZEN!", QuizBattlePalette.WaterBlue, 1.15f);
            }
            _hud.Log($"Player {f.CasterId} freezes player {f.TargetId}!");
        }

        private void OnPlayerEliminated(int playerId)
        {
            if (_tokens.TryGetValue(playerId, out var token)) token.SetEliminated();
        }

        private void OnMatchEnded(MatchEndPayload end)
        {
            _hud.Log($"MATCH OVER — winner: {end.WinnerId} ({end.Reason})");
        }

        private void PositionCamera(int width, int height)
        {
            ArenaEnvironment.FrameGrid(_rig, _grid, width, height);
        }
    }
}
