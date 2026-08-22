using QuizBattle.Arena.Visuals;
using QuizBattle.Characters;
using TMPro;
using UnityEngine;

namespace QuizBattle.Arena
{
    /// A per-archetype stylized body (see CharacterVisualBuilder) plus a billboarded
    /// nameplate (name text + HP bar) above it. Features juicy Clash Royale-style
    /// parabolic hopping, squash & stretch landing impacts, grounded blob contact shadows,
    /// and hit reactions.
    public class CharacterToken : MonoBehaviour
    {
        private const float BarWidth = 0.9f;
        private const float BarHeight = 0.1f;
        private const float NameplateHeight = 1.5f;
        private const float MoveDuration = 0.32f;
        private const float HopHeight = 0.42f;

        private TMP_Text _nameLabel;
        private Renderer[] _bodyRenderers;
        private Transform _bodyContainer;
        private TokenIdleAnimator _animator;
        private Transform _hpFillPivot;
        private Renderer _hpFillRenderer;
        private string _displayName;
        private int _hp;
        private int _maxHp;
        private int _streak;
        private bool _eliminated;
        private GameObject _frozenIndicator;

        private Vector3 _moveStart;
        private Vector3 _moveTarget;
        private float _moveElapsed;
        private bool _moving;

        private float _landingBounceTime;
        private float _flinchTime;

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
            var frozenIndicator = CreateFrozenIndicator(root.transform);

            if (Camera.main != null) billboard.Align(Camera.main);

            var token = root.AddComponent<CharacterToken>();
            token._nameLabel = nameLabel;
            token._bodyRenderers = visualResult.Renderers;
            token._bodyContainer = bodyContainer.transform;
            token._animator = bodyContainer.GetComponent<TokenIdleAnimator>();
            token._hpFillPivot = fillPivot;
            token._hpFillRenderer = fillRenderer;
            token._frozenIndicator = frozenIndicator;
            token._displayName = displayName;
            token.SetHp(0, 0);
            return token;
        }

        /// 3D Translucent Frosted Ice Block with crystal spikes and glowing frost ring
        /// that encases the character completely when frozen.
        private static GameObject CreateFrozenIndicator(Transform parent)
        {
            var go = new GameObject("FrozenIceBlock");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;

            var iceMat = ToonMaterialFactory.Instance(new Color(0.65f, 0.92f, 1.0f, 0.75f), ToonStyle.IceBlockStyle);

            // Main translucent ice block encasing the character
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "MainIceCube";
            cube.transform.SetParent(go.transform, false);
            cube.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            cube.transform.localScale = new Vector3(0.95f, 1.30f, 0.95f);
            Object.Destroy(cube.GetComponent<Collider>());
            cube.GetComponent<Renderer>().sharedMaterial = iceMat;

            // 4 corner crystal spikes
            float[] xz = { -0.45f, 0.45f };
            for (int i = 0; i < 4; i++)
            {
                var spike = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spike.name = $"IceSpike_{i}";
                spike.transform.SetParent(go.transform, false);
                spike.transform.localPosition = new Vector3(xz[i % 2], 0.65f, xz[i / 2]);
                spike.transform.localScale = new Vector3(0.18f, 0.75f, 0.18f);
                spike.transform.localRotation = Quaternion.Euler((i % 2 == 0 ? 12f : -12f), 0f, (i / 2 == 0 ? 12f : -12f));
                Object.Destroy(spike.GetComponent<Collider>());
                spike.GetComponent<Renderer>().sharedMaterial = iceMat;
            }

            // Glowing frosted base disc
            var frostRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            frostRing.name = "FrostRing";
            frostRing.transform.SetParent(go.transform, false);
            frostRing.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            frostRing.transform.localScale = new Vector3(1.25f, 0.03f, 1.25f);
            Object.Destroy(frostRing.GetComponent<Collider>());
            frostRing.GetComponent<Renderer>().sharedMaterial =
                ToonMaterialFactory.Glow(new Color(0.4f, 0.85f, 1f), intensity: 1.5f, softEdge: 0.25f, pulseSpeed: 2f, pulseAmount: 0.4f);

            go.SetActive(false);
            return go;
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
            fillRenderer.sharedMaterial = ToonMaterialFactory.GlowInstance(fillColor, intensity: 1.2f, softEdge: 0.02f);

            return (fillPivot.transform, fillRenderer);
        }

        public void SetHp(int hp, int maxHp)
        {
            if (_maxHp > 0 && hp < _hp)
            {
                _flinchTime = 0.25f;
            }

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
                if (renderer.sharedMaterial.HasColor("_BaseColor")) renderer.sharedMaterial.SetColor("_BaseColor", dim);
                if (renderer.sharedMaterial.HasColor("_TintColor")) renderer.sharedMaterial.SetColor("_TintColor", dim);
            }
            _hpFillRenderer.sharedMaterial.SetColor("_TintColor", new Color(0.3f, 0.3f, 0.3f));
            RebuildLabel();
        }

        public void SetFrozen(bool frozen)
        {
            if (_frozenIndicator != null) _frozenIndicator.SetActive(frozen);
            if (_animator != null) _animator.SetPaused(frozen);
        }

        /// Animates the juicy parabolic hop from current position to worldPos with
        /// squash & stretch physics.
        public void MoveTo(Vector3 worldPos)
        {
            _moveStart = transform.position;
            _moveTarget = worldPos;
            _moveElapsed = 0f;
            _moving = true;
            if (_animator != null) _animator.SetPaused(true);
        }

        /// Snaps immediately to the current move target. Call before capturing a
        /// screenshot in a context with no player loop.
        public void CompleteMovement()
        {
            if (!_moving) return;
            transform.position = _moveTarget;
            if (_bodyContainer != null)
            {
                _bodyContainer.localPosition = Vector3.zero;
                _bodyContainer.localRotation = Quaternion.identity;
                _bodyContainer.localScale = Vector3.one;
            }
            _moving = false;
            if (_animator != null) _animator.SetPaused(false);
        }

        private void Update()
        {
            if (_moving)
            {
                _moveElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_moveElapsed / MoveDuration);

                // Horizontal movement
                Vector3 currentPos = Vector3.Lerp(_moveStart, _moveTarget, t);

                // Parabolic hop arc
                float hop = Mathf.Sin(t * Mathf.PI) * HopHeight;
                transform.position = currentPos + Vector3.up * hop;

                if (_bodyContainer != null)
                {
                    // Directional tilt while moving
                    Vector3 moveDir = (_moveTarget - _moveStart).normalized;
                    if (moveDir.sqrMagnitude > 0.001f)
                    {
                        float tilt = Mathf.Sin(t * Mathf.PI) * 14f;
                        _bodyContainer.localRotation = Quaternion.Euler(tilt, 0f, 0f);
                    }

                    // Squash & stretch curve
                    float scaleY;
                    float scaleXZ;
                    if (t < 0.25f)
                    {
                        // Takeoff launch stretch
                        float launchT = t / 0.25f;
                        scaleY = 1f + Mathf.Sin(launchT * Mathf.PI) * 0.22f;
                        scaleXZ = 1f - Mathf.Sin(launchT * Mathf.PI) * 0.12f;
                    }
                    else if (t > 0.80f)
                    {
                        // Landing impact squash
                        float landT = (t - 0.80f) / 0.20f;
                        scaleY = 1f - (1f - landT) * 0.22f;
                        scaleXZ = 1f + (1f - landT) * 0.15f;
                    }
                    else
                    {
                        // Mid-air slight stretch
                        scaleY = 1.08f;
                        scaleXZ = 0.95f;
                    }
                    _bodyContainer.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);
                }

                if (t >= 1f)
                {
                    _moving = false;
                    _landingBounceTime = 0.2f;
                    if (_animator != null) _animator.SetPaused(false);
                }
            }
            else if (_landingBounceTime > 0f)
            {
                // Damped spring bounce upon landing
                _landingBounceTime -= Time.deltaTime;
                float bounceT = Mathf.Clamp01(_landingBounceTime / 0.2f);
                float spring = Mathf.Sin((1f - bounceT) * Mathf.PI * 2f) * 0.12f * bounceT;
                if (_bodyContainer != null)
                {
                    _bodyContainer.localRotation = Quaternion.identity;
                    _bodyContainer.localScale = new Vector3(1f - spring, 1f + spring, 1f - spring);
                }
            }
            else if (_flinchTime > 0f)
            {
                // Damage flinch shake
                _flinchTime -= Time.deltaTime;
                float flinchT = Mathf.Clamp01(_flinchTime / 0.25f);
                float shake = Mathf.Sin(flinchT * Mathf.PI * 6f) * 0.08f * flinchT;
                if (_bodyContainer != null)
                {
                    _bodyContainer.localPosition = new Vector3(shake, -shake * 0.5f, 0f);
                    _bodyContainer.localScale = new Vector3(1f + flinchT * 0.15f, 1f - flinchT * 0.2f, 1f + flinchT * 0.15f);
                }
            }
            else if (_bodyContainer != null)
            {
                _bodyContainer.localPosition = Vector3.zero;
                _bodyContainer.localRotation = Quaternion.identity;
            }
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
