using System.Collections.Generic;
using QuizBattle.Arena.Visuals;
using QuizBattle.Characters;
using TMPro;
using UnityEngine;

namespace QuizBattle.Arena
{
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
        private bool _frozen;
        private GameObject _frozenIndicator;

        private Vector3 _moveStart;
        private Vector3 _moveTarget;
        private float _moveElapsed;
        private bool _moving;

        private float _landingBounceTime;
        private float _flinchTime;
        private float _attackTime;
        private Vector3 _attackDirection;

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
            var groundRing = bodyContainer.transform.Find("TeamBaseRing");
            if (groundRing != null) groundRing.SetParent(root.transform, false);

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

        private static GameObject CreateFrozenIndicator(Transform parent)
        {
            var go = new GameObject("FrozenIceBlock");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;

            var iceStyle = ToonStyle.Default;
            iceStyle.RimIntensity = 0.4f;
            iceStyle.EmissionColor = new Color(0.12f, 0.55f, 0.8f);
            iceStyle.EmissionIntensity = 0.25f;
            iceStyle.OutlineWidth = 0.8f;
            var iceMat = ToonMaterialFactory.Toon(new Color(0.5f, 0.85f, 1f), iceStyle);
            float[] xz = { -0.34f, 0.34f };
            for (int i = 0; i < 4; i++)
            {
                var spike = new GameObject($"IceSpike_{i}");
                spike.transform.SetParent(go.transform, false);
                spike.transform.localPosition = new Vector3(xz[i % 2], 0.03f, xz[i / 2]);
                spike.transform.localScale = new Vector3(1f, i % 2 == 0 ? 1f : 0.7f, 1f);
                spike.transform.localRotation = Quaternion.Euler((i % 2 == 0 ? 12f : -12f), 0f, (i / 2 == 0 ? 12f : -12f));
                spike.AddComponent<MeshFilter>().sharedMesh = PrimitiveMeshFactory.Cone(5, 0.09f, 0f, 0.6f);
                spike.AddComponent<MeshRenderer>().sharedMaterial = iceMat;
            }

            var frostRing = new GameObject("FrostRing");
            frostRing.transform.SetParent(go.transform, false);
            frostRing.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            frostRing.AddComponent<MeshFilter>().sharedMesh = PrimitiveMeshFactory.Torus(0.45f, 0.025f, 24, 6);
            frostRing.AddComponent<MeshRenderer>().sharedMaterial =
                ToonMaterialFactory.Glow(new Color(0.4f, 0.85f, 1f), intensity: 0.85f, softEdge: 0.25f, pulseSpeed: 2f, pulseAmount: 0.2f);

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
            text.richText = false;
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
            var fillStyle = barBgStyle;
            fillStyle.ShadowTint = Color.white;
            fillStyle.EmissionColor = fillColor;
            fillStyle.EmissionIntensity = 0.25f;
            fillRenderer.sharedMaterial = ToonMaterialFactory.Instance(fillColor, fillStyle);

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
            if (_eliminated) return;
            CompleteMovement();
            _eliminated = true;
            _attackTime = 0f;
            SetFrozen(false);
            var dim = new Color(0.3f, 0.3f, 0.3f);
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", dim);
            properties.SetColor("_TintColor", dim);
            properties.SetFloat("_EmissionIntensity", 0f);
            properties.SetFloat("_RimIntensity", 0f);
            foreach (var renderer in _bodyRenderers)
            {
                if (renderer == null) continue;
                renderer.SetPropertyBlock(properties);
            }
            _hpFillRenderer.SetPropertyBlock(properties);
            RebuildLabel();
        }

        public void SetFrozen(bool frozen)
        {
            _frozen = frozen && !_eliminated;
            if (_frozen) _attackTime = 0f;
            if (_frozenIndicator != null) _frozenIndicator.SetActive(_frozen);
            if (_animator != null) _animator.SetPaused(_frozen || _moving || _eliminated || _attackTime > 0f);
        }

        public void AttackToward(Vector3 target)
        {
            if (_eliminated || _frozen) return;
            _attackDirection = transform.InverseTransformDirection(target - transform.position);
            _attackDirection.y = 0f;
            if (_attackDirection.sqrMagnitude < 0.001f) return;
            _attackDirection.Normalize();
            _attackTime = 0.36f;
            if (_animator != null) _animator.SetPaused(true);
        }

        /// Animates the juicy parabolic hop from current position to worldPos with
        /// squash & stretch physics.
        public void MoveTo(Vector3 worldPos)
        {
            if (_eliminated || (worldPos - (_moving ? _moveTarget : transform.position)).sqrMagnitude < 0.0001f) return;
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
            if (_animator != null) _animator.SetPaused(_frozen || _eliminated || _attackTime > 0f);
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
                transform.position = currentPos;

                if (_bodyContainer != null)
                {
                    _bodyContainer.localPosition = Vector3.up * hop;
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
                    if (_animator != null) _animator.SetPaused(_frozen || _eliminated || _attackTime > 0f);
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
                    _bodyContainer.localPosition = Vector3.zero;
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
            else if (_attackTime > 0f)
            {
                _attackTime = Mathf.Max(0f, _attackTime - Time.deltaTime);
                float t = 1f - _attackTime / 0.36f;
                float thrust = Mathf.Sin(t * Mathf.PI * 2f) * -0.12f * (1f - t);
                float yaw = Mathf.Atan2(-_attackDirection.x, -_attackDirection.z) * Mathf.Rad2Deg;
                _bodyContainer.localPosition = _attackDirection * thrust;
                _bodyContainer.localRotation = Quaternion.Euler(thrust * 90f, Mathf.LerpAngle(yaw, 0f, t * t), 0f);
                _bodyContainer.localScale = new Vector3(1f - Mathf.Abs(thrust), 1f + Mathf.Abs(thrust), 1f - Mathf.Abs(thrust));
                if (_attackTime == 0f && _animator != null) _animator.SetPaused(_frozen || _eliminated);
            }
            else if (_bodyContainer != null)
            {
                _bodyContainer.localPosition = Vector3.zero;
                _bodyContainer.localRotation = Quaternion.identity;
                _bodyContainer.localScale = Vector3.one;
            }
        }

        private void OnDestroy()
        {
            var materials = new HashSet<Material>();
            if (_bodyRenderers != null)
                foreach (var renderer in _bodyRenderers)
                    if (renderer != null)
                        foreach (var material in renderer.sharedMaterials) materials.Add(material);
            if (_hpFillRenderer != null) materials.Add(_hpFillRenderer.sharedMaterial);
            foreach (var material in materials)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
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
