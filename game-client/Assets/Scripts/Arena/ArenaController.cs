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
    /// (submit_answer, use_attack, use_freeze, reward_consumed) and never decides outcomes.
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

        // Tracked client-side purely to filter the target popup so it doesn't even offer
        // the disallowed repeat target — the server remains the actual authority on the
        // "can't attack/freeze the same player twice in a row" rule and will reject it
        // regardless if this ever falls out of sync.
        private int? _lastTargetPlayerId;

        // The server pushes the player's next question in the same tick it offers a
        // streak reward, and that question's arrival (QuestionPushed -> HudController.
        // ShowQuestion) unconditionally re-enables the choice buttons — which would
        // silently undo the lock below the instant it's applied. This flag lets
        // OnQuestionPushedWhileLocked re-assert the lock after that happens, for as
        // long as the reward popup is still open.
        private bool _rewardPopupOpen;
        private int _lastChosenIndex = -1;

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
            // Subscribed after _view's own QuestionPushed subscription above, so this
            // handler always runs after NetworkedArenaView.OnQuestionPushed re-enables
            // the buttons via ShowQuestion — letting it re-lock them if a reward popup
            // is still open.
            _store.QuestionPushed += OnQuestionPushedWhileLocked;
        }

        private void OnDestroy()
        {
            if (_hud != null) _hud.ChoiceSelected -= OnChoiceSelected;
            if (_store != null)
            {
                _store.AnswerResultReceived -= OnAnswerResult;
                _store.MatchEnded -= OnMatchEnded;
                _store.QuestionPushed -= OnQuestionPushedWhileLocked;
            }
        }

        private void OnQuestionPushedWhileLocked(QuestionPushPayload q)
        {
            if (_rewardPopupOpen) _hud.SetChoicesInteractable(false);
        }

        private void OnChoiceSelected(int choiceIndex)
        {
            _lastChosenIndex = choiceIndex;
            _hud.SetChoicesInteractable(false);
            AppRoot.Instance.Client.Send("submit_answer", new { choiceIndex });
        }

        private void OnAnswerResult(AnswerResultPayload result)
        {
            if (result.Ok)
            {
                _hud.ShowFeedback(result.Correct, result.StreakCount, _lastChosenIndex);
            }

            if (!result.Ok || result.RewardOffered == null) return;

            var rewardId = result.RewardOffered.RewardId;

            if (result.RewardOffered.Type == "attack_choice")
            {
                var opponents = GetTargetableOpponents();
                if (opponents.Count == 0)
                {
                    // No living opponent to target — waive it rather than silently
                    // dropping it. An un-consumed reward stays "pending" server-side and
                    // blocks every future streak reward for several questions (see
                    // MatchEngine.submitAnswer's `!player.pendingReward` gate), which is
                    // exactly what made streaks look completely broken after this case
                    // was hit once.
                    _hud.Log("Streak reward offered, but no opponent left to target — skipped.");
                    AppRoot.Instance.Client.Send("reward_consumed", new { rewardId, choice = "waive" });
                    return;
                }

                // Lock the next question's answer buttons behind the reward popup — the
                // server already pushed that question in the same tick it offered this
                // reward, so without this the two compete for the player's attention and
                // it's easy to miss the popup entirely, letting the reward silently expire.
                _rewardPopupOpen = true;
                _hud.SetChoicesInteractable(false);
                _rewardPopup.ShowAttackChoice(opponents, targetId =>
                {
                    _lastTargetPlayerId = targetId;
                    AppRoot.Instance.Client.Send("use_attack", new { rewardId, targetPlayerId = targetId });
                    _rewardPopupOpen = false;
                    _hud.SetChoicesInteractable(true);
                });
            }
            else if (result.RewardOffered.Type == "freeze")
            {
                var opponents = GetTargetableOpponents();
                if (opponents.Count == 0)
                {
                    _hud.Log("Streak reward offered, but no opponent left to target — skipped.");
                    AppRoot.Instance.Client.Send("reward_consumed", new { rewardId, choice = "waive" });
                    return;
                }

                _rewardPopupOpen = true;
                _hud.SetChoicesInteractable(false);
                _rewardPopup.ShowFreezeChoice(opponents, targetId =>
                {
                    _lastTargetPlayerId = targetId;
                    AppRoot.Instance.Client.Send("use_freeze", new { rewardId, targetPlayerId = targetId });
                    _rewardPopupOpen = false;
                    _hud.SetChoicesInteractable(true);
                });
            }
            else if (result.RewardOffered.Type == "bonus_move")
            {
                _rewardPopupOpen = true;
                _hud.SetChoicesInteractable(false);
                _rewardPopup.ShowBonusMove(() =>
                {
                    AppRoot.Instance.Client.Send("reward_consumed", new { rewardId, choice = "bonus_move" });
                    _rewardPopupOpen = false;
                    _hud.SetChoicesInteractable(true);
                });
            }
        }

        /// Living opponents, excluding whoever we last targeted — unless that's the only
        /// opponent left, in which case there's no alternative and the server allows a
        /// repeat anyway (see MatchEngine.HasAlternativeTarget on the server/client engine).
        private List<(int playerId, string name)> GetTargetableOpponents()
        {
            var all = _store.Players.Values
                .Where(p => p.alive && p.playerId != SessionManager.PlayerId)
                .Select(p => (p.playerId, p.name))
                .ToList();

            if (_lastTargetPlayerId == null) return all;
            var filtered = all.Where(o => o.playerId != _lastTargetPlayerId).ToList();
            return filtered.Count > 0 ? filtered : all;
        }

        private void OnMatchEnded(MatchEndPayload payload)
        {
            _hud.Log($"Match over! Winner: {payload.WinnerId} ({payload.Reason})");
            SceneManager.LoadScene("PostMatch");
        }
    }
}
