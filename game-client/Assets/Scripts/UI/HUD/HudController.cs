using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuizBattle.UI.HUD
{
    /// Question/streak/log HUD, built entirely from code (via UiFactory) so no
    /// Editor-authored prefab/scene wiring is required. Choice buttons are real,
    /// clickable Buttons — ArenaController subscribes to ChoiceSelected to submit answers.
    public class HudController : MonoBehaviour
    {
        private TMP_Text _roundText;
        private TMP_Text _questionText;
        private Button[] _choiceButtons;
        private TMP_Text[] _choiceLabels;
        private TMP_Text _logText;
        private readonly List<string> _logLines = new List<string>();

        public event Action<int> ChoiceSelected;

        public static HudController Create()
        {
            var canvas = QuizBattle.UI.UiFactory.CreateCanvas("HUD_Canvas");
            var hud = canvas.gameObject.AddComponent<HudController>();
            hud.Build(canvas.transform);
            return hud;
        }

        private void Build(Transform parent)
        {
            _roundText = QuizBattle.UI.UiFactory.CreateText(parent, "RoundText", new Vector2(0.5f, 0.94f), new Vector2(400, 40), 22);
            _questionText = QuizBattle.UI.UiFactory.CreateText(parent, "QuestionText", new Vector2(0.5f, 0.84f), new Vector2(900, 70), 26);

            // 2x2 grid, not a single row: at the 1280-wide reference resolution a 4-across
            // row of 210px-wide buttons only had ~154px between anchor points — less than
            // the button width, so they visually overlapped into one solid bar. This
            // layout leaves generous margin (512px between column anchors vs 380px wide
            // buttons) regardless of actual screen size, since CanvasScaler scales both
            // proportionally.
            _choiceButtons = new Button[4];
            _choiceLabels = new TMP_Text[4];
            (float x, float y)[] gridPositions = { (0.30f, 0.68f), (0.70f, 0.68f), (0.30f, 0.56f), (0.70f, 0.56f) };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var (x, y) = gridPositions[i];
                var button = QuizBattle.UI.UiFactory.CreateButton(parent, $"Choice_{i}", new Vector2(x, y), new Vector2(380, 70), "");
                var label = button.GetComponentInChildren<TMP_Text>();
                label.fontSize = 22;
                button.onClick.AddListener(() => ChoiceSelected?.Invoke(index));
                _choiceButtons[i] = button;
                _choiceLabels[i] = label;
            }

            // Bottom-left corner anchor with a fixed margin, not a fractional position —
            // the previous 0.15-anchored, 480-wide panel ran off the left edge of the
            // screen entirely on narrower/non-16:9 windows.
            const float margin = 20f;
            const float logWidth = 420f;
            const float logHeight = 220f;
            var logPanel = QuizBattle.UI.UiFactory.CreatePanel(
                parent, "LogPanel", new Vector2(0f, 0f), new Vector2(logWidth, logHeight),
                new Color(0, 0, 0, 0.6f), new Vector2(margin + logWidth / 2f, margin + logHeight / 2f));
            _logText = QuizBattle.UI.UiFactory.CreateText(logPanel.transform, "LogText", new Vector2(0.5f, 0.5f), new Vector2(logWidth - 20f, logHeight - 20f), 14);
            _logText.alignment = TextAlignmentOptions.TopLeft;
        }

        public void ShowQuestion(int questionNumber, string text, IReadOnlyList<string> choices)
        {
            // "Question N" not "Round N" — every player answers independently at their
            // own pace now, so there's no shared match-wide round to label.
            _roundText.text = $"Question {questionNumber}";
            _questionText.text = text;
            for (int i = 0; i < _choiceLabels.Length; i++)
            {
                _choiceLabels[i].text = i < choices.Count ? choices[i] : "";
                _choiceButtons[i].interactable = i < choices.Count;
            }
        }

        public void SetChoicesInteractable(bool interactable)
        {
            foreach (var b in _choiceButtons) b.interactable = interactable;
        }

        public void Log(string line)
        {
            _logLines.Add(line);
            if (_logLines.Count > 12) _logLines.RemoveAt(0);
            var sb = new StringBuilder();
            foreach (var l in _logLines) sb.AppendLine(l);
            _logText.text = sb.ToString();
        }
    }
}
