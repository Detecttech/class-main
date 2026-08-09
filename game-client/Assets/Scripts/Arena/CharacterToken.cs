using QuizBattle.Arena.Visuals;
using QuizBattle.Characters;
using TMPro;
using UnityEngine;

namespace QuizBattle.Arena
{
    /// A per-archetype stylized body (see CharacterVisualBuilder) plus a billboarded
    /// nameplate (name text + HP bar) above it.
    public class CharacterToken : MonoBehaviour
    {
        private const float BarWidth = 0.9f;
        private const float BarHeight = 0.1f;
        private const float NameplateHeight = 1.5f;
        private const float MoveDuration = 0.35f;

        private TMP_Text _nameLabel;
        private Renderer[] _bodyRenderers;
        private Transform _hpFillPivot;
        private Renderer _hpFillRenderer;
        private string _displayName;
        private int _hp;
        private int _maxHp;
        private int _streak;
        private bool _eliminated;

        private Vector3 _moveStart;
        private Vector3 _moveTarget;
        private float _moveElapsed;
        private bool _moving;

        /// Fallback overload for any call site that only has a bare color (e.g. a missed
        /// migration) — degrades to a generic capsule instead of failing to compile.
        public static CharacterToken Create(string displayName, Color color, Vector3 worldPos) =>
            Create(displayName, CharacterVisual.Fallback(color), worldPos);

        public static CharacterToken Create(string displayName, in CharacterVisual visual, Vector3 worldPos)
        {
            var root = new GameObject($"Token_{displayName}");
            root.transform.position = worldPos;

            var bodyContainer = new GameObject("Body");
            bodyContainer.transform.SetParent(root.transform, false);
            var visualResult = CharacterVisualBuilder.Build(visual, bodyContainer.transform);

            var nameplate = new GameObject("Nameplate");
            nameplate.transform.SetParent(root.transform, false);
            nameplate.transform.localPosition = new Vector3(0, NameplateHeight, 0);
            var billboard = nameplate.AddComponent<BillboardLabel>();

            var nameLabel = CreateNameLabel(nameplate.transform);
            var (fillPivot, fillRenderer) = CreateHpBar(nameplate.transform, visual.BaseColor);

            if (Camera.main != null) billboard.Align(Camera.main);

            var token = root.AddComponent<CharacterToken>();
            token._nameLabel = nameLabel;
            token._bodyRenderers = visualResult.Renderers;
            token._hpFillPivot = fillPivot;
            token._hpFillRenderer = fillRenderer;
            token._displayName = displayName;
            token.SetHp(0, 0);
            return token;
        }

        private static TMP_Text CreateNameLabel(Transform parent)
        {
            var labelObj = new GameObject("NameText");
            labelObj.transform.SetParent(parent, false);
            labelObj.transform.localPosition = new Vector3(0, 0.22f, 0);
            labelObj.transform.localScale = Vector3.one * 0.15f;

            var text = labelObj.AddComponent<TextMeshPro>();
            text.fontSize = 8f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.outlineWidth = 0.15f;
            text.outlineColor = Color.black;
            text.enableWordWrapping = false;
            return text;
        }

        private static readonly float FrameMargin = 0.06f;

        private static (Transform fillPivot, Renderer fillRenderer) CreateHpBar(Transform parent, Color fillColor)
        {
            // Gold frame, a hair larger than the bg and set further back (+z) so it peeks
            // out as a border — the chunky bordered-banner look used throughout the UI.
            var frame = GameObject.CreatePrimitive(PrimitiveType.Quad);
            frame.name = "HpBarFrame";
            frame.transform.SetParent(parent, false);
            frame.transform.localPosition = new Vector3(0f, 0f, 0.001f);
            frame.transform.localScale = new Vector3(BarWidth + FrameMargin, BarHeight + FrameMargin, 1f);
            Object.Destroy(frame.GetComponent<Collider>());
            var frameStyle = new ToonStyle
            {
                ShadowTint = QuizBattlePalette.GoldTrimDark,
                RimColor = Color.black,
                RimIntensity = 0f,
                EmissionColor = Color.black,
                EmissionIntensity = 0f,
                OutlineColor = Color.black,
                OutlineWidth = 0f,
                OutlineEnabled = false,
            };
            frame.GetComponent<Renderer>().sharedMaterial = ToonMaterialFactory.Toon(QuizBattlePalette.GoldTrim, frameStyle);

            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "HpBarBg";
            bg.transform.SetParent(parent, false);
            bg.transform.localPosition = Vector3.zero;
            bg.transform.localScale = new Vector3(BarWidth, BarHeight, 1f);
            Object.Destroy(bg.GetComponent<Collider>());
            // Opaque flat plate (not the additive glow shader — additive can't render a
            // dark backing that actually occludes what's behind it), outline/rim off so
            // it reads as a flat card rather than a shaded object.
            var barBgStyle = new ToonStyle
            {
                ShadowTint = new Color(0.05f, 0.05f, 0.08f),
                RimColor = Color.black,
                RimIntensity = 0f,
                EmissionColor = Color.black,
                EmissionIntensity = 0f,
                OutlineColor = Color.black,
                OutlineWidth = 0f,
                OutlineEnabled = false,
            };
            bg.GetComponent<Renderer>().sharedMaterial = ToonMaterialFactory.Toon(new Color(0.05f, 0.05f, 0.08f), barBgStyle);

            var fillPivot = new GameObject("HpBarFillPivot");
            fillPivot.transform.SetParent(parent, false);
            fillPivot.transform.localPosition = new Vector3(-BarWidth * 0.5f, 0f, -0.001f);

            var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fill.name = "HpBarFill";
            fill.transform.SetParent(fillPivot.transform, false);
            fill.transform.localPosition = new Vector3(BarWidth * 0.5f, 0f, 0f);
            fill.transform.localScale = new Vector3(BarWidth, BarHeight, 1f);
            Object.Destroy(fill.GetComponent<Collider>());
            var fillRenderer = fill.GetComponent<Renderer>();
            // Instance, not the shared/cached Glow(...), since SetEliminated recolors
            // this renderer's material directly.
            fillRenderer.sharedMaterial = ToonMaterialFactory.GlowInstance(fillColor, intensity: 1.2f, softEdge: 0.02f);

            return (fillPivot.transform, fillRenderer);
        }

        public void SetHp(int hp, int maxHp)
        {
            _hp = hp;
            _maxHp = maxHp;
            float fraction = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 1f;
            _hpFillPivot.localScale = new Vector3(fraction, 1f, 1f);
            RebuildLabel();
        }

        public void SetStreak(int streak)
        {
            _streak = streak;
            RebuildLabel();
        }

        public void SetEliminated()
        {
            _eliminated = true;
            var dim = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            foreach (var renderer in _bodyRenderers)
            {
                if (renderer == null) continue;
                // Renderers built from GlowInstance materials use _TintColor, the toon
                // body/accent renderers use _BaseColor — set whichever the shader has.
                if (renderer.sharedMaterial.HasColor("_BaseColor")) renderer.sharedMaterial.SetColor("_BaseColor", dim);
                if (renderer.sharedMaterial.HasColor("_TintColor")) renderer.sharedMaterial.SetColor("_TintColor", dim);
            }
            _hpFillRenderer.sharedMaterial.SetColor("_TintColor", new Color(0.3f, 0.3f, 0.3f));
            RebuildLabel();
        }

        /// Animates the visual slide from the current position to worldPos over
        /// MoveDuration via Update() — a plain instant `transform.position = worldPos`
        /// gave no visible feedback that a move happened at all. Grid/gameplay logic
        /// never reads this transform, so the animated delay has no effect on match
        /// state; only the headless demo runners (which drive matches synchronously with
        /// no player loop, so Update never fires) need CompleteMovement() to force the
        /// final position before a screenshot.
        public void MoveTo(Vector3 worldPos)
        {
            _moveStart = transform.position;
            _moveTarget = worldPos;
            _moveElapsed = 0f;
            _moving = true;
        }

        /// Snaps immediately to the current move target. Call before capturing a
        /// screenshot in a context with no player loop.
        public void CompleteMovement()
        {
            if (!_moving) return;
            transform.position = _moveTarget;
            _moving = false;
        }

        private void Update()
        {
            if (!_moving) return;
            _moveElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_moveElapsed / MoveDuration);
            transform.position = Vector3.Lerp(_moveStart, _moveTarget, t);
            if (t >= 1f) _moving = false;
        }

        private void RebuildLabel()
        {
            string text = _displayName;
            if (_maxHp > 0) text += $"\nHP {_hp}/{_maxHp}";
            if (_streak >= 2) text += $"  x{_streak}";
            if (_eliminated) text += "\n(eliminated)";
            _nameLabel.text = text;
        }
    }
}
