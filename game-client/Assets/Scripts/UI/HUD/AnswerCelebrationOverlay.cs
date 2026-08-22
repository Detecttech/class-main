using System.Collections;
using QuizBattle.Arena;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuizBattle.UI.HUD
{
    /// Screen-space celebration overlay that triggers when the local player answers a question.
    /// Provides energetic feedback with spring scale-punch, celebratory ribbons, and streak multipliers.
    public class AnswerCelebrationOverlay : MonoBehaviour
    {
        private RectTransform _container;
        private Image _bannerBg;
        private Image _bannerBorder;
        private TMP_Text _mainText;
        private RectTransform _streakRibbon;
        private TMP_Text _streakText;
        private CanvasGroup _canvasGroup;

        private Coroutine _animCoroutine;

        public static AnswerCelebrationOverlay Create(Transform canvasTransform)
        {
            var go = new GameObject("AnswerCelebrationOverlay");
            go.transform.SetParent(canvasTransform, false);

            var comp = go.AddComponent<AnswerCelebrationOverlay>();
            comp.Build();
            go.SetActive(false);
            return comp;
        }

        private void Build()
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _container = UiFactory.CreateRect(transform, "Container", new Vector2(0.5f, 0.52f), new Vector2(520, 110));

            // Drop shadow
            var shadow = UiFactory.CreateRect(_container, "Shadow", new Vector2(0.5f, 0.5f), new Vector2(528, 118), new Vector2(0, -6));
            var shadowImg = shadow.gameObject.AddComponent<Image>();
            shadowImg.sprite = UiFactory.RoundedSprite;
            shadowImg.type = Image.Type.Sliced;
            shadowImg.color = new Color(0f, 0f, 0f, 0.6f);

            // Outer gold border
            _bannerBorder = _container.gameObject.AddComponent<Image>();
            _bannerBorder.sprite = UiFactory.RoundedSprite;
            _bannerBorder.type = Image.Type.Sliced;
            _bannerBorder.color = QuizBattlePalette.GoldTrim;

            // Inner banner fill
            var inner = UiFactory.CreateRect(_container, "Inner", Vector2.zero, Vector2.zero);
            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(6, 6);
            inner.offsetMax = new Vector2(-6, -6);
            _bannerBg = inner.gameObject.AddComponent<Image>();
            _bannerBg.sprite = UiFactory.RoundedSprite;
            _bannerBg.type = Image.Type.Sliced;
            _bannerBg.color = new Color(0.12f, 0.55f, 0.25f); // Vibrant Emerald Green

            // Top gloss sheen
            var gloss = UiFactory.CreateRect(inner, "Gloss", Vector2.zero, Vector2.zero);
            gloss.anchorMin = new Vector2(0f, 0.5f);
            gloss.anchorMax = new Vector2(1f, 1f);
            gloss.offsetMin = Vector2.zero;
            gloss.offsetMax = Vector2.zero;
            var glossImg = gloss.gameObject.AddComponent<Image>();
            glossImg.sprite = UiFactory.RoundedSprite;
            glossImg.type = Image.Type.Sliced;
            glossImg.color = new Color(1f, 1f, 1f, 0.25f);
            glossImg.raycastTarget = false;

            // Main Text (CORRECT! / INCORRECT)
            _mainText = UiFactory.CreateText(_container, "MainText", new Vector2(0.5f, 0.5f), new Vector2(500, 60), 38);
            _mainText.fontStyle = FontStyles.Bold;
            _mainText.color = Color.white;
            _mainText.outlineWidth = 0.28f;
            _mainText.outlineColor = Color.black;
            _mainText.alignment = TextAlignmentOptions.Center;

            // Streak Ribbon at the bottom
            var (ribbonRect, _) = UiFactory.CreateBannerPanel(
                _container, "StreakRibbon", new Vector2(0.5f, 0f), new Vector2(260, 32), QuizBattlePalette.FireGlow, new Vector2(0, -14));
            _streakRibbon = ribbonRect;
            _streakText = UiFactory.CreateText(_streakRibbon, "StreakText", new Vector2(0.5f, 0.5f), new Vector2(240, 26), 16);
            _streakText.fontStyle = FontStyles.Bold;
            _streakText.color = Color.white;
            _streakText.outlineWidth = 0.2f;
            _streakText.outlineColor = Color.black;
            _streakText.alignment = TextAlignmentOptions.Center;
        }

        public void ShowFeedback(bool correct, int streak)
        {
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            gameObject.SetActive(true);
            _animCoroutine = StartCoroutine(AnimateCelebration(correct, streak));
        }

        private IEnumerator AnimateCelebration(bool correct, int streak)
        {
            _canvasGroup.alpha = 1f;

            if (correct)
            {
                _bannerBorder.color = QuizBattlePalette.GoldTrim;
                _bannerBg.color = new Color(0.10f, 0.62f, 0.22f); // Vibrant Emerald
                _mainText.text = "CORRECT!";
                _mainText.color = new Color(1f, 0.95f, 0.45f);

                if (streak >= 2)
                {
                    _streakRibbon.gameObject.SetActive(true);
                    _streakText.text = $"STREAK x{streak}!";
                }
                else
                {
                    _streakRibbon.gameObject.SetActive(false);
                }
            }
            else
            {
                _bannerBorder.color = new Color(0.55f, 0.15f, 0.15f);
                _bannerBg.color = new Color(0.75f, 0.15f, 0.15f); // Crimson Red
                _mainText.text = "INCORRECT";
                _mainText.color = Color.white;
                _streakRibbon.gameObject.SetActive(false);
            }

            // Energetic Spring scale punch: 0 -> 1.32 -> 1.0
            float duration = 1.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;

                // Scale punch with spring overshoot in the first 0.35s
                float scale = 1f;
                if (elapsed < 0.35f)
                {
                    float p = elapsed / 0.35f;
                    scale = Mathf.Sin(p * Mathf.PI * 0.75f) * 1.30f;
                }
                else
                {
                    // Settle spring
                    float p = (elapsed - 0.35f) / 0.25f;
                    scale = Mathf.Lerp(1.30f, 1.0f, Mathf.Clamp01(p));
                }
                _container.localScale = Vector3.one * scale;

                // Float upward gently
                _container.anchoredPosition = new Vector2(0, Mathf.Lerp(0f, 35f, t));

                // Fade out near the end
                if (t > 0.75f)
                {
                    float fade = 1f - ((t - 0.75f) / 0.25f);
                    _canvasGroup.alpha = fade;
                }

                yield return null;
            }

            gameObject.SetActive(false);
            _animCoroutine = null;
        }
    }
}
