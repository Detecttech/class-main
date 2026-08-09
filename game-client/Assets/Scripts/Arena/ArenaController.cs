using System.Collections.Generic;
using System.Linq;
using QuizBattle.Bootstrap;
using QuizBattle.Characters;
using QuizBattle.GameState;
using QuizBattle.Networking;
using QuizBattle.Networking.Protocol;
using QuizBattle.UI.HUD;
using QuizBattle.UI.RewardPopup;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuizBattle.Arena
{
    /// The real, network-driven Arena scene controller — replaces the Phase 1 local
    /// GameManager/MockEngine demo. Every visible change here originates from a
    /// MatchStateStore event (server truth); this class only ever sends *intents*
    /// (submit_answer, use_attack, reward_consumed) and never decides outcomes.
    /// Movement is automatic (a correct answer advances the player one step toward the
    /// goal server-side) — there is no client-sent move intent anymore.
    public class ArenaController : MonoBehaviour
    {
        private GridController _grid;
        private HudController _hud;
        private ArenaRig _rig;
        private RewardPopupController _rewardPopup;
        private NetworkedArenaView _view;
        private MatchStateStore _store;
        private Dictionary<string, CharacterVisual> _characterVisuals;

        private void Start()
        {
            _store = AppRoot.Instance.Store;
            _characterVisuals = CharacterCatalogLoader.LoadAll().ToDictionary(d => d.characterId, CharacterVisual.From);

            var gridObj = new GameObject("Grid");
            _grid = gridObj.AddComponent<GridController>();
            _hud = HudController.Create();
            _rewardPopup = RewardPopupController.Create(_hud.transform);

            _rig = ArenaEnvironment.Acquire(new Color(0.08f, 0.08f, 0.13f));

            // NetworkedArenaView handles the case where match_start already arrived
            // before construction (see its constructor) — no separate fallback needed
            // here; an earlier version of this duplicated that logic with its own
            // untracked tokens, which silently broke every HP/attack update.
            _view = new NetworkedArenaView(_grid, _hud, _rig, _store, _characterVisuals);

            _hud.ChoiceSelected += OnChoiceSelected;
            _store.AnswerResultReceived += OnAnswerResult;
            _store.MatchEnded += OnMatchEnded;
        }

        private void OnDestroy()
        {
            if (_hud != null) _hud.ChoiceSelected -= OnChoiceSelected;
            if (_store != null)
            {
                _store.AnswerResultReceived -= OnAnswerResult;
                _store.MatchEnded -= OnMatchEnded;
            }
        }

        private void OnChoiceSelected(int choiceIndex)
        {
            _hud.SetChoicesInteractable(false);
            AppRoot.Instance.Client.Send("submit_answer", new { choiceIndex });
        }

        private void OnAnswerResult(AnswerResultPayload result)
        {
            if (!result.Ok || result.RewardOffered == null) return;

            if (result.RewardOffered.Type == "attack_choice")
            {
                var opponents = _store.Players.Values
                    .Where(p => p.alive && p.playerId != SessionManager.PlayerId)
                    .Select(p => (p.playerId, p.name))
                    .ToList();
                if (opponents.Count == 0) return;

                var rewardId = result.RewardOffered.RewardId;
                _rewardPopup.ShowAttackChoice(opponents, targetId =>
                {
                    AppRoot.Instance.Client.Send("use_attack", new { rewardId, targetPlayerId = targetId });
                });
            }
            else if (result.RewardOffered.Type == "bonus_move")
            {
                var rewardId = result.RewardOffered.RewardId;
                _rewardPopup.ShowBonusMove(() =>
                {
                    AppRoot.Instance.Client.Send("reward_consumed", new { rewardId, choice = "bonus_move" });
                });
            }
        }

        private void OnMatchEnded(MatchEndPayload payload)
        {
            _hud.Log($"Match over! Winner: {payload.WinnerId} ({payload.Reason})");
            SceneManager.LoadScene("PostMatch");
        }
    }
}
