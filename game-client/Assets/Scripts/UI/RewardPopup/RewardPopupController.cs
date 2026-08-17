using System;
using System.Collections.Generic;
using QuizBattle.Arena;
using QuizBattle.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuizBattle.UI.RewardPopup
{
    /// Shown whenever the server offers a streak reward (see RewardOfferedPayload):
    /// attack_choice/freeze both let the player pick a living opponent to hit; bonus_move
    /// is just an acknowledgement toast (the extra movement is already available once consumed).
    public class RewardPopupController : MonoBehaviour
    {
        private GameObject _backdrop;
        private GameObject _panel;
        private TMP_Text _titleText;
        private Transform _buttonContainer;
        private readonly List<GameObject> _spawnedButtons = new List<GameObject>();

        public static RewardPopupController Create(Transform canvasParent)
        {
            var obj = new GameObject("RewardPopup");
            obj.transform.SetParent(canvasParent, false);
            var controller = obj.AddComponent<RewardPopupController>();
            controller.Build();
            return controller;
        }

        private void Build()
        {
            // Full-screen dim behind the panel so a streak reward reads as a clear modal
            // interruption instead of a small box easy to miss against the already-visible
            // next question underneath it (see ArenaController.OnAnswerResult).
            var backdropRect = UiFactory.CreateRect(transform, "Backdrop", new Vector2(0.5f, 0.5f), Vector2.zero);
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            var backdropImage = backdropRect.gameObject.AddComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.6f);
            _backdrop = backdropRect.gameObject;

            _panel = UiFactory.CreatePanel(transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(420, 260), new Color(QuizBattlePalette.PanelDeep.r, QuizBattlePalette.PanelDeep.g, QuizBattlePalette.PanelDeep.b, 0.95f)).gameObject;
            _titleText = UiFactory.CreateText(_panel.transform, "Title", new Vector2(0.5f, 0.8f), new Vector2(380, 60), 22);

            var containerObj = new GameObject("Buttons");
            containerObj.transform.SetParent(_panel.transform, false);
            var rect = containerObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.35f);
            rect.anchorMax = new Vector2(0.5f, 0.35f);
            rect.sizeDelta = new Vector2(380, 160);
            _buttonContainer = containerObj.transform;

            _backdrop.SetActive(false);
            _panel.SetActive(false);
        }

        public void ShowAttackChoice(IReadOnlyList<(int playerId, string name)> opponents, Action<int> onTargetChosen) =>
            ShowTargetChoice("Streak reward: choose a target to attack!", opponents, onTargetChosen);

        public void ShowFreezeChoice(IReadOnlyList<(int playerId, string name)> opponents, Action<int> onTargetChosen) =>
            ShowTargetChoice("Streak reward: choose a target to freeze!", opponents, onTargetChosen);

        private void ShowTargetChoice(string title, IReadOnlyList<(int playerId, string name)> opponents, Action<int> onTargetChosen)
        {
            // Activate before spawning buttons, not after — a TMP_Text added under an
            // inactive-in-hierarchy GameObject never runs its Awake/OnEnable material
            // setup, so UiFactory.CreateButton's `text.outlineWidth = ...` throws a
            // NullReferenceException (confirmed via a Player.log crash trace) and aborts
            // before the popup ever becomes visible — this is what made every streak
            // reward (attack/freeze/bonus_move alike) silently do nothing.
            _backdrop.SetActive(true);
            _panel.SetActive(true);

            ClearButtons();
            _titleText.text = title;
            for (int i = 0; i < opponents.Count; i++)
            {
                var target = opponents[i];
                var button = UiFactory.CreateButton(_buttonContainer, $"Target_{target.playerId}", new Vector2(0.5f, 0.5f), new Vector2(300, 40), target.name, new Vector2(0, -i * 46));
                button.onClick.AddListener(() =>
                {
                    Hide();
                    onTargetChosen(target.playerId);
                });
                _spawnedButtons.Add(button.gameObject);
            }
        }

        public void ShowBonusMove(Action onAcknowledged)
        {
            _backdrop.SetActive(true);
            _panel.SetActive(true);

            ClearButtons();
            _titleText.text = "Streak reward: bonus movement!";
            var button = UiFactory.CreateButton(_buttonContainer, "Ack", new Vector2(0.5f, 0.5f), new Vector2(200, 40), "Nice!");
            button.onClick.AddListener(() =>
            {
                Hide();
                onAcknowledged();
            });
            _spawnedButtons.Add(button.gameObject);
        }

        public void Hide()
        {
            _backdrop.SetActive(false);
            _panel.SetActive(false);
        }

        private void ClearButtons()
        {
            foreach (var b in _spawnedButtons) Destroy(b);
            _spawnedButtons.Clear();
        }
    }
}
