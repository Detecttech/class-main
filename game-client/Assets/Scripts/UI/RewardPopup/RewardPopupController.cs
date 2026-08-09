using System;
using System.Collections.Generic;
using QuizBattle.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuizBattle.UI.RewardPopup
{
    /// Shown whenever the server offers a streak reward (see RewardOfferedPayload):
    /// attack_choice lets the player pick a living opponent to hit; bonus_move is just an
    /// acknowledgement toast (the extra movement is already available once consumed).
    public class RewardPopupController : MonoBehaviour
    {
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
            _panel = UiFactory.CreatePanel(transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(420, 260), new Color(0.1f, 0.1f, 0.18f, 0.95f)).gameObject;
            _titleText = UiFactory.CreateText(_panel.transform, "Title", new Vector2(0.5f, 0.8f), new Vector2(380, 60), 22);

            var containerObj = new GameObject("Buttons");
            containerObj.transform.SetParent(_panel.transform, false);
            var rect = containerObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.35f);
            rect.anchorMax = new Vector2(0.5f, 0.35f);
            rect.sizeDelta = new Vector2(380, 160);
            _buttonContainer = containerObj.transform;

            _panel.SetActive(false);
        }

        public void ShowAttackChoice(IReadOnlyList<(int playerId, string name)> opponents, Action<int> onTargetChosen)
        {
            ClearButtons();
            _titleText.text = "Streak reward: choose a target to attack!";
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
            _panel.SetActive(true);
        }

        public void ShowBonusMove(Action onAcknowledged)
        {
            ClearButtons();
            _titleText.text = "Streak reward: bonus movement!";
            var button = UiFactory.CreateButton(_buttonContainer, "Ack", new Vector2(0.5f, 0.5f), new Vector2(200, 40), "Nice!");
            button.onClick.AddListener(() =>
            {
                Hide();
                onAcknowledged();
            });
            _spawnedButtons.Add(button.gameObject);
            _panel.SetActive(true);
        }

        public void Hide() => _panel.SetActive(false);

        private void ClearButtons()
        {
            foreach (var b in _spawnedButtons) Destroy(b);
            _spawnedButtons.Clear();
        }
    }
}
