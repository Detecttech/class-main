using QuizBattle.Arena;
using TMPro;
using UnityEngine;

namespace QuizBattle.Arena.Visuals
{
    /// Billboarded pop-up combat text that bursts above a character with an energetic
    /// spring scale punch, floats upward, and fades out. Used for damage, streaks,
    /// and status effects.
    public class FloatingCombatText : MonoBehaviour
    {
        private const float Duration = 0.85f;
        private const float FloatDistance = 1.1f;

        private TMP_Text _text;
        private Vector3 _startPos;
        private float _elapsed;
        private Color _baseColor;

        public static FloatingCombatText Spawn(Vector3 worldPos, string message, Color color, float scale = 1.0f)
        {
            var go = new GameObject($"CombatText_{message}");
            go.transform.position = worldPos;

            var billboard = go.AddComponent<BillboardLabel>();
            if (Camera.main != null) billboard.Align(Camera.main);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(go.transform, false);
            textObj.transform.localPosition = Vector3.zero;
            textObj.transform.localScale = Vector3.one * 0.22f * scale;

            var tmp = textObj.AddComponent<TextMeshPro>();
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = message;
            tmp.fontSize = 6.2f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.outlineWidth = 0.22f;
            tmp.outlineColor = Color.black;
            tmp.enableWordWrapping = false;
            tmp.ForceMeshUpdate();

            var comp = go.AddComponent<FloatingCombatText>();
            comp._text = tmp;
            comp._startPos = worldPos;
            comp._baseColor = color;
            return comp;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / Duration);

            // Upward drift
            float easeOut = 1f - Mathf.Pow(1f - t, 2.2f);
            transform.position = _startPos + Vector3.up * (easeOut * FloatDistance);

            // Bouncy spring scale punch: 0 -> 1.35 -> 1.0
            float punch;
            if (t < 0.2f)
            {
                punch = Mathf.Lerp(0.2f, 1.35f, t / 0.2f);
            }
            else if (t < 0.45f)
            {
                punch = Mathf.Lerp(1.35f, 1.0f, (t - 0.2f) / 0.25f);
            }
            else
            {
                punch = 1.0f;
            }
            transform.localScale = Vector3.one * punch;

            // Fade out towards end
            float alpha = t > 0.6f ? Mathf.Clamp01((1f - t) / 0.4f) : 1f;
            var c = _baseColor;
            c.a = alpha;
            _text.color = c;
            var oc = Color.black;
            oc.a = alpha;
            _text.outlineColor = oc;

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
