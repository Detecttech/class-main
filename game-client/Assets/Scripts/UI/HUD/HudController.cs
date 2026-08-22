using System;
using System.Collections.Generic;
using System.Text;
using QuizBattle.Arena;
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
            // Question Placard Banner at the top
            var (placard, innerCard) = QuizBattle.UI.UiFactory.CreatePlacardPanel(
                parent, "QuestionPlacard", new Vector2(0.5f, 0.938f), new Vector2(860, 56), QuizBattlePalette.PanelDeep);

            // Question Sequence Ribbon Badge
            var (badge, _) = QuizBattle.UI.UiFactory.CreateBannerPanel(
                placard, "QuestionBadge", new Vector2(0.5f, 1f), new Vector2(170, 24), QuizBattlePalette.BannerBlue, new Vector2(0, 0));
            _roundText = QuizBattle.UI.UiFactory.CreateText(badge, "RoundText", new Vector2(0.5f, 0.5f), new Vector2(160, 22), 13);
            _roundText.fontStyle = FontStyles.Bold;
            _roundText.color = QuizBattlePalette.GoldTrim;

            // Question Text inside the placard
            _questionText = QuizBattle.UI.UiFactory.CreateText(
                innerCard.transform, "QuestionText", new Vector2(0.5f, 0.5f), new Vector2(830, 44), 16);
            _questionText.fontStyle = FontStyles.Bold;
            _questionText.color = Color.white;
            _questionText.outlineWidth = 0.18f;
            _questionText.outlineColor = Color.black;

            // 2x2 grid of 3D-beveled tactile Clash Royale choice buttons (compacted into top 24%)
            _choiceButtons = new Button[4];
            _choiceLabels = new TMP_Text[4];
            (float x, float y)[] gridPositions = { (0.285f, 0.842f), (0.715f, 0.842f), (0.285f, 0.762f), (0.715f, 0.762f) };
            string[] badges = { "A", "B", "C", "D" };
            Color[] colors = {
                new Color(0.16f, 0.44f, 0.88f), // Royal Blue
                new Color(0.18f, 0.68f, 0.28f), // Emerald Green
                new Color(0.88f, 0.56f, 0.12f), // Amber Gold
                new Color(0.82f, 0.22f, 0.22f), // Crimson
            };
            Color[] shadows = {
                new Color(0.10f, 0.28f, 0.60f),
                new Color(0.10f, 0.44f, 0.18f),
                new Color(0.58f, 0.35f, 0.08f),
                new Color(0.52f, 0.12f, 0.12f),
            };

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var (x, y) = gridPositions[i];
                var (button, label) = QuizBattle.UI.UiFactory.CreateClashButton(
                    parent, $"Choice_{i}", new Vector2(x, y), new Vector2(360, 42), "", colors[i], shadows[i], badges[i]);
                label.fontSize = 16;
                button.onClick.AddListener(() => ChoiceSelected?.Invoke(index));
                _choiceButtons[i] = button;
                _choiceLabels[i] = label;
            }

            // Bottom-left corner match log in a semi-transparent royal slate card (unobtrusive)
            const float margin = 12f;
            const float logWidth = 280f;
            const float logHeight = 90f;
            var (logPlacard, logInner) = QuizBattle.UI.UiFactory.CreatePlacardPanel(
                parent, "LogPanel", new Vector2(0f, 0f), new Vector2(logWidth, logHeight),
                new Color(0.08f, 0.09f, 0.14f, 0.80f), new Vector2(margin + logWidth / 2f, margin + logHeight / 2f));
            _logText = QuizBattle.UI.UiFactory.CreateText(
                logInner.transform, "LogText", new Vector2(0.5f, 0.5f), new Vector2(logWidth - 16f, logHeight - 12f), 11);
            _logText.alignment = TextAlignmentOptions.TopLeft;

            _celebrationOverlay = AnswerCelebrationOverlay.Create(parent);
        }

        private AnswerCelebrationOverlay _celebrationOverlay;

        public void ShowFeedback(bool correct, int streak, int chosenIndex = -1)
        {
            if (_celebrationOverlay != null)
            {
                _celebrationOverlay.ShowFeedback(correct, streak);
            }
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
