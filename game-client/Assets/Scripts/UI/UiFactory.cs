using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QuizBattle.UI
{
    /// Small shared helpers for building uGUI screens entirely from code — every screen
    /// controller in this project uses these instead of hand-wired scene prefabs, so
    /// screens stay easy to construct/verify from Editor tooling without manual drag-drop.
    /// Uses TextMeshPro (not legacy Text/InputField) for text rendering quality — run
    /// Tools > Scaffold > Import TMP Essential Resources once if TMP_Settings.defaultFontAsset
    /// is null.
    public static class UiFactory
    {
        public static Canvas CreateCanvas(string name = "Canvas")
        {
            var obj = new GameObject(name);
            var canvas = obj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = obj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            obj.AddComponent<GraphicRaycaster>();

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }
            return canvas;
        }

        public static RectTransform CreateRect(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 anchoredPos = default)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;
            return rect;
        }

        public static TMP_Text CreateText(Transform parent, string name, Vector2 anchor, Vector2 size, int fontSize, Vector2 anchoredPos = default)
        {
            var rect = CreateRect(parent, name, anchor, size, anchoredPos);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        public static Image CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 size, Color color, Vector2 anchoredPos = default)
        {
            var rect = CreateRect(parent, name, anchor, size, anchoredPos);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Button CreateButton(Transform parent, string name, Vector2 anchor, Vector2 size, string label, Vector2 anchoredPos = default)
        {
            var panel = CreatePanel(parent, name, anchor, size, new Color(0.28f, 0.38f, 0.58f, 1f), anchoredPos);
            var button = panel.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.38f, 0.5f, 0.75f, 1f);
            colors.pressedColor = new Color(0.2f, 0.28f, 0.45f, 1f);
            button.colors = colors;

            var text = CreateText(panel.transform, "Label", new Vector2(0.5f, 0.5f), size, 18);
            text.text = label;
            return button;
        }

        public static TMP_InputField CreateInputField(Transform parent, string name, Vector2 anchor, Vector2 size, string placeholder = "", Vector2 anchoredPos = default)
        {
            var panel = CreatePanel(parent, name, anchor, size, new Color(1f, 1f, 1f, 0.92f), anchoredPos);
            var inputField = panel.gameObject.AddComponent<TMP_InputField>();

            var textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(panel.transform, false);
            var textAreaRect = (RectTransform)textArea.transform;
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 6);
            textAreaRect.offsetMax = new Vector2(-10, -7);
            textArea.AddComponent<RectMask2D>();

            var textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(textArea.transform, false);
            var textRect = (RectTransform)textObj.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textObj.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 16;
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.MidlineLeft;

            var placeholderObj = new GameObject("Placeholder", typeof(RectTransform));
            placeholderObj.transform.SetParent(textArea.transform, false);
            var placeholderRect = (RectTransform)placeholderObj.transform;
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            var placeholderText = placeholderObj.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) placeholderText.font = TMP_Settings.defaultFontAsset;
            placeholderText.fontSize = 16;
            placeholderText.color = new Color(0, 0, 0, 0.4f);
            placeholderText.text = placeholder;
            placeholderText.fontStyle = FontStyles.Italic;
            placeholderText.alignment = TextAlignmentOptions.MidlineLeft;

            inputField.textViewport = textAreaRect;
            inputField.textComponent = text;
            inputField.placeholder = placeholderText;
            inputField.fontAsset = text.font;

            return inputField;
        }
    }
}
