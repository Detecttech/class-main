using QuizBattle.Arena;
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
            // Match width and height evenly rather than width-only (the default) — the
            // reference is a fixed 16:9 landscape layout, and phone screens (even locked
            // to landscape, see AndroidManifest.xml's sensorLandscape) vary in aspect
            // ratio (e.g. 20:9). Width-only matching over-scales the canvas height on
            // wider-than-16:9 screens, shrinking every element relative to the visible area.
            scaler.matchWidthOrHeight = 0.5f;
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

        // Generated once, on first use, rather than relying on Resources.GetBuiltinResource
        // — the exact built-in resource path for a rounded-rect sprite isn't a stable
        // public API and repeatedly failed to resolve ("could not be loaded from the
        // resource file") across attempts at guessing it. Drawing it ourselves removes
        // that dependency entirely and gives exact control over the corner radius.
        private static Sprite _roundedSprite;
        private const int RoundedSpriteSize = 64;
        private const int RoundedSpriteCornerRadius = 16;

        public static Sprite RoundedSprite
        {
            get
            {
                if (_roundedSprite != null) return _roundedSprite;
                _roundedSprite = BuildRoundedRectSprite(RoundedSpriteSize, RoundedSpriteCornerRadius);
                return _roundedSprite;
            }
        }

        private static Sprite BuildRoundedRectSprite(int size, int cornerRadius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "QB_RoundedRect",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                // Same reasoning as ToonMaterialFactory's cached materials: this is a
                // runtime-only asset referenced solely from this static field, which a
                // scene load can otherwise reclaim as "unused".
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };

            var pixels = new Color32[size * size];
            float left = cornerRadius, right = size - cornerRadius, top = cornerRadius, bottom = size - cornerRadius;
            for (int y = 0; y < size; y++)
            {
                float py = y + 0.5f;
                float cy = Mathf.Clamp(py, top, bottom);
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float cx = Mathf.Clamp(px, left, right);
                    float dist = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                    // ~1px soft edge instead of a hard aliased corner.
                    byte alpha = (byte)(Mathf.Clamp01(cornerRadius - dist + 0.5f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            var border = new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = "QB_RoundedRect";
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }

        private const float BannerBorder = 5f;

        /// Chunky bordered "banner" look (a gold-trim frame around an inset colored fill,
        /// plus a faint top highlight strip for a glossy/beveled feel).
        public static (RectTransform frame, Image fill) CreateBannerPanel(Transform parent, string name, Vector2 anchor, Vector2 size, Color fillColor, Vector2 anchoredPos = default)
        {
            var frame = CreateRect(parent, name, anchor, size, anchoredPos);
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.sprite = RoundedSprite;
            frameImage.type = Image.Type.Sliced;
            frameImage.color = QuizBattlePalette.GoldTrim;

            var fillRect = CreateRect(frame, "Fill", Vector2.zero, Vector2.zero);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(BannerBorder, BannerBorder);
            fillRect.offsetMax = new Vector2(-BannerBorder, -BannerBorder);
            var fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.sprite = RoundedSprite;
            fillImage.type = Image.Type.Sliced;
            fillImage.color = fillColor;

            var highlightRect = CreateRect(fillRect, "Highlight", Vector2.zero, Vector2.zero);
            highlightRect.anchorMin = new Vector2(0f, 0.5f);
            highlightRect.anchorMax = new Vector2(1f, 1f);
            highlightRect.offsetMin = Vector2.zero;
            highlightRect.offsetMax = Vector2.zero;
            var highlightImage = highlightRect.gameObject.AddComponent<Image>();
            highlightImage.sprite = RoundedSprite;
            highlightImage.type = Image.Type.Sliced;
            highlightImage.color = new Color(1f, 1f, 1f, 0.18f);
            highlightImage.raycastTarget = false;

            return (frame, fillImage);
        }

        /// Medieval royal placard banner with a rich stone/wooden outer border, inset parchment/slate card,
        /// and corner golden rivets for the signature Clash Royale card appearance.
        public static (RectTransform placard, Image innerCard) CreatePlacardPanel(Transform parent, string name, Vector2 anchor, Vector2 size, Color innerFill, Vector2 anchoredPos = default)
        {
            var placard = CreateRect(parent, name, anchor, size, anchoredPos);

            // Dark drop shadow beneath the placard
            var shadow = CreateRect(placard, "PlacardShadow", new Vector2(0.5f, 0.5f), size + new Vector2(4, 4), new Vector2(0, -5));
            var shadowImg = shadow.gameObject.AddComponent<Image>();
            shadowImg.sprite = RoundedSprite;
            shadowImg.type = Image.Type.Sliced;
            shadowImg.color = new Color(0.04f, 0.04f, 0.08f, 0.65f);

            // Outer wooden/gold border frame
            var outerFrame = placard.gameObject.AddComponent<Image>();
            outerFrame.sprite = RoundedSprite;
            outerFrame.type = Image.Type.Sliced;
            outerFrame.color = QuizBattlePalette.GoldTrim;

            // Inset card body
            var innerRect = CreateRect(placard, "InnerCard", Vector2.zero, Vector2.zero);
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(6, 6);
            innerRect.offsetMax = new Vector2(-6, -6);
            var innerImg = innerRect.gameObject.AddComponent<Image>();
            innerImg.sprite = RoundedSprite;
            innerImg.type = Image.Type.Sliced;
            innerImg.color = innerFill;

            // Subtle top gloss sheen
            var sheen = CreateRect(innerRect, "GlossSheen", Vector2.zero, Vector2.zero);
            sheen.anchorMin = new Vector2(0f, 0.6f);
            sheen.anchorMax = new Vector2(1f, 1f);
            sheen.offsetMin = Vector2.zero;
            sheen.offsetMax = Vector2.zero;
            var sheenImg = sheen.gameObject.AddComponent<Image>();
            sheenImg.sprite = RoundedSprite;
            sheenImg.type = Image.Type.Sliced;
            sheenImg.color = new Color(1f, 1f, 1f, 0.12f);
            sheenImg.raycastTarget = false;

            return (placard, innerImg);
        }

        /// 3D-beveled cartoon button with deep bottom drop-shadow plate, gold/metal rim,
        /// vibrant gradient fill, top gloss highlight, and optional circular badge medallion (e.g. A, B, C, D).
        public static (Button button, TMP_Text label) CreateClashButton(Transform parent, string name, Vector2 anchor, Vector2 size, string labelText, Color mainColor, Color bevelShadowColor, string badgeText = null, Vector2 anchoredPos = default)
        {
            var container = CreateRect(parent, name, anchor, size, anchoredPos);

            // 3D bottom bevel shadow under-plate
            var bottomPlate = CreateRect(container, "BottomBevel", Vector2.zero, Vector2.zero);
            bottomPlate.anchorMin = Vector2.zero;
            bottomPlate.anchorMax = Vector2.one;
            bottomPlate.offsetMin = new Vector2(0, -6);
            bottomPlate.offsetMax = new Vector2(0, 0);
            var bottomImg = bottomPlate.gameObject.AddComponent<Image>();
            bottomImg.sprite = RoundedSprite;
            bottomImg.type = Image.Type.Sliced;
            bottomImg.color = bevelShadowColor;

            // Main golden rim
            var rimRect = CreateRect(container, "Rim", Vector2.zero, Vector2.zero);
            rimRect.anchorMin = Vector2.zero;
            rimRect.anchorMax = Vector2.one;
            rimRect.offsetMin = Vector2.zero;
            rimRect.offsetMax = Vector2.zero;
            var rimImg = rimRect.gameObject.AddComponent<Image>();
            rimImg.sprite = RoundedSprite;
            rimImg.type = Image.Type.Sliced;
            rimImg.color = QuizBattlePalette.GoldTrim;

            // Inset button surface
            var faceRect = CreateRect(rimRect, "Face", Vector2.zero, Vector2.zero);
            faceRect.anchorMin = Vector2.zero;
            faceRect.anchorMax = Vector2.one;
            faceRect.offsetMin = new Vector2(4, 5);
            faceRect.offsetMax = new Vector2(-4, -3);
            var faceImg = faceRect.gameObject.AddComponent<Image>();
            faceImg.sprite = RoundedSprite;
            faceImg.type = Image.Type.Sliced;
            faceImg.color = mainColor;

            // Top gloss reflection
            var glossRect = CreateRect(faceRect, "Gloss", Vector2.zero, Vector2.zero);
            glossRect.anchorMin = new Vector2(0f, 0.5f);
            glossRect.anchorMax = new Vector2(1f, 1f);
            glossRect.offsetMin = Vector2.zero;
            glossRect.offsetMax = Vector2.zero;
            var glossImg = glossRect.gameObject.AddComponent<Image>();
            glossImg.sprite = RoundedSprite;
            glossImg.type = Image.Type.Sliced;
            glossImg.color = new Color(1f, 1f, 1f, 0.22f);
            glossImg.raycastTarget = false;

            // Button interaction component
            var button = container.gameObject.AddComponent<Button>();
            button.targetGraphic = faceImg;
            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(mainColor, Color.white, 0.18f);
            colors.pressedColor = bevelShadowColor;
            button.colors = colors;

            // Optional circular badge medallion (e.g. A, B, C, D)
            float badgeSize = Mathf.Clamp(size.y - 12f, 22f, 34f);
            if (!string.IsNullOrEmpty(badgeText))
            {
                var badgeRect = CreateRect(faceRect, "Badge", new Vector2(0f, 0.5f), new Vector2(badgeSize, badgeSize), new Vector2(badgeSize * 0.65f + 4f, 0));
                var badgeBg = badgeRect.gameObject.AddComponent<Image>();
                badgeBg.sprite = RoundedSprite;
                badgeBg.type = Image.Type.Sliced;
                badgeBg.color = QuizBattlePalette.GoldTrim;

                var badgeInner = CreateRect(badgeRect, "BadgeInner", Vector2.zero, Vector2.zero);
                badgeInner.anchorMin = Vector2.zero;
                badgeInner.anchorMax = Vector2.one;
                badgeInner.offsetMin = new Vector2(2, 2);
                badgeInner.offsetMax = new Vector2(-2, -2);
                var badgeInnerImg = badgeInner.gameObject.AddComponent<Image>();
                badgeInnerImg.sprite = RoundedSprite;
                badgeInnerImg.type = Image.Type.Sliced;
                badgeInnerImg.color = bevelShadowColor;

                var badgeTxt = CreateText(badgeInner, "BadgeText", new Vector2(0.5f, 0.5f), new Vector2(badgeSize, badgeSize), Mathf.RoundToInt(badgeSize * 0.5f));
                badgeTxt.text = badgeText;
                badgeTxt.fontStyle = FontStyles.Bold;
                badgeTxt.color = QuizBattlePalette.GoldTrim;
                badgeTxt.alignment = TextAlignmentOptions.Center;
            }

            // Main Label
            float leftOffset = !string.IsNullOrEmpty(badgeText) ? (badgeSize + 8f) : 0f;
            var text = CreateText(faceRect, "Label", new Vector2(0.5f, 0.5f), new Vector2(size.x - 24f - leftOffset, size.y - 6f), 17, new Vector2(leftOffset * 0.4f, 0));
            text.text = labelText;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.outlineWidth = 0.2f;
            text.outlineColor = Color.black;

            return (button, text);
        }

        public static Button CreateButton(Transform parent, string name, Vector2 anchor, Vector2 size, string label, Vector2 anchoredPos = default)
        {
            var (button, _) = CreateClashButton(parent, name, anchor, size, label, QuizBattlePalette.PanelFill, QuizBattlePalette.GoldTrimDark, null, anchoredPos);
            return button;
        }

        public static TMP_InputField CreateInputField(Transform parent, string name, Vector2 anchor, Vector2 size, string placeholder = "", Vector2 anchoredPos = default)
        {
            var panel = CreatePanel(parent, name, anchor, size, QuizBattlePalette.ParchmentField, anchoredPos);
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
