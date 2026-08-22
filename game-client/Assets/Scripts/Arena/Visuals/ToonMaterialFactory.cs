using System.Collections.Generic;
using QuizBattle.Arena;
using UnityEngine;

namespace QuizBattle.Arena.Visuals
{
    /// Tunable knobs for QuizBattle/Toon beyond the base color.
    public struct ToonStyle
    {
        public Color ShadowTint;
        public Color RimColor;
        public float RimIntensity;
        public float RimPower;
        public Color SpecTint;
        public float Gloss;
        public float SpecIntensity;
        public Color EmissionColor;
        public float EmissionIntensity;
        public Color OutlineColor;
        public float OutlineWidth;
        public bool OutlineEnabled;

        // Chunkier/darker outline, glossy toy specular, and a vibrant rim highlight —
        // matches the signature Clash-Royale plastic/resin toy look.
        public static ToonStyle Default => new ToonStyle
        {
            ShadowTint = QuizBattlePalette.ShadowTint,
            RimColor = new Color(0.92f, 0.96f, 1f),
            RimIntensity = 1.25f,
            RimPower = 2.0f,
            SpecTint = Color.white,
            Gloss = 22f,
            SpecIntensity = 0.95f,
            EmissionColor = Color.black,
            EmissionIntensity = 0f,
            OutlineColor = QuizBattlePalette.OutlineColor,
            OutlineWidth = 2.2f,
            OutlineEnabled = true,
        };

        // Ultra glossy specular and studio rim for Character Select and hero units
        public static ToonStyle GlossyToy => new ToonStyle
        {
            ShadowTint = new Color(0.20f, 0.22f, 0.42f),
            RimColor = new Color(0.85f, 0.95f, 1f),
            RimIntensity = 1.65f,
            RimPower = 1.8f,
            SpecTint = Color.white,
            Gloss = 18f,
            SpecIntensity = 1.40f,
            EmissionColor = Color.black,
            EmissionIntensity = 0f,
            OutlineColor = new Color(0.06f, 0.06f, 0.12f),
            OutlineWidth = 2.4f,
            OutlineEnabled = true,
        };

        // Translucent frosted ice crystal style
        public static ToonStyle IceBlockStyle => new ToonStyle
        {
            ShadowTint = new Color(0.35f, 0.70f, 0.95f),
            RimColor = Color.white,
            RimIntensity = 2.2f,
            RimPower = 1.5f,
            SpecTint = Color.white,
            Gloss = 16f,
            SpecIntensity = 1.5f,
            EmissionColor = new Color(0.3f, 0.75f, 1f),
            EmissionIntensity = 0.4f,
            OutlineColor = new Color(0.4f, 0.85f, 1f),
            OutlineWidth = 2.5f,
            OutlineEnabled = true,
        };
    }

    /// Replaces the project's old `new Material(Shader.Find("Unlit/Color"))` pattern.
    /// Static geometry (grid tiles) should use the cached Toon(...)/Glow(...) overloads;
    /// anything mutated per-object at runtime (e.g. CharacterToken.SetEliminated dimming
    /// a single token) must use Instance(...) instead, or the mutation would apply to
    /// every object sharing that cached material.
    public static class ToonMaterialFactory
    {
        private const string ToonShaderName = "QuizBattle/Toon";
        private const string GlowShaderName = "QuizBattle/GlowAdditive";

        private static Shader _toonShader;
        private static Shader _glowShader;
        private static readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

        private static Shader ToonShader
        {
            get
            {
                if (_toonShader == null)
                {
                    _toonShader = Shader.Find(ToonShaderName);
                    if (_toonShader == null)
                        Debug.LogError($"[ToonMaterialFactory] Shader '{ToonShaderName}' not found — check GraphicsSettings always-included shaders.");
                }
                return _toonShader;
            }
        }

        private static Shader GlowShader
        {
            get
            {
                if (_glowShader == null)
                {
                    _glowShader = Shader.Find(GlowShaderName);
                    if (_glowShader == null)
                        Debug.LogError($"[ToonMaterialFactory] Shader '{GlowShaderName}' not found — check GraphicsSettings always-included shaders.");
                }
                return _glowShader;
            }
        }

        public static Material Toon(Color baseColor) => Toon(baseColor, ToonStyle.Default);

        public static Material Toon(Color baseColor, ToonStyle style) => Toon(baseColor, style, null, default);

        /// Textured variant — mainTex is multiplied into baseColor. Only meshes that
        /// actually carry UV0 (e.g. grid tiles, imported model geometry) should use this;
        /// PrimitiveMeshFactory's procedural cone/torus meshes have no UVs.
        public static Material Toon(Color baseColor, ToonStyle style, Texture2D mainTex, Vector4 tiling)
        {
            string key = ToonKey(baseColor, style, mainTex, tiling);
            // The Unity-side `!= null` check (not plain reference equality) matters here:
            // a scene reload (e.g. Lobby -> Arena between matches, or the Editor demo
            // runners looping scenarios in one process) can unload runtime-created
            // materials that nothing else references, leaving a stale "destroyed but not
            // null" entry in this C# dictionary — using it renders the whole object magenta.
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var mat = BuildToon(baseColor, style, mainTex, tiling);
            _cache[key] = mat;
            return mat;
        }

        public static Material Glow(Color color, float intensity = 1.5f, float softEdge = 0.5f, float pulseSpeed = 0f, float pulseAmount = 0f)
        {
            string key = $"glow|{color}|{intensity}|{softEdge}|{pulseSpeed}|{pulseAmount}";
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var mat = BuildGlow(color, intensity, softEdge, pulseSpeed, pulseAmount);
            _cache[key] = mat;
            return mat;
        }

        /// Non-shared instance for anything mutated per-object at runtime.
        public static Material Instance(Color baseColor) => BuildToon(baseColor, ToonStyle.Default, null, default);

        public static Material Instance(Color baseColor, ToonStyle style) => BuildToon(baseColor, style, null, default);

        public static Material Instance(Color baseColor, ToonStyle style, Texture2D mainTex, Vector4 tiling) =>
            BuildToon(baseColor, style, mainTex, tiling);

        /// Non-shared glow instance for anything mutated per-object at runtime (e.g. an
        /// HP bar fill that recolors when its owner is eliminated).
        public static Material GlowInstance(Color color, float intensity = 1.5f, float softEdge = 0.5f) =>
            BuildGlow(color, intensity, softEdge, 0f, 0f);

        private static Material BuildToon(Color baseColor, ToonStyle style, Texture2D mainTex, Vector4 tiling)
        {
            // Runtime-created assets referenced only from a C# field (not a live scene
            // object) can otherwise be reclaimed by Unity's unused-asset cleanup on a
            // scene load, which would leave the cache holding a stale/destroyed material.
            var mat = new Material(ToonShader) { name = "QB_Toon_Instance", hideFlags = HideFlags.DontUnloadUnusedAsset };
            mat.SetColor("_BaseColor", baseColor);
            mat.SetColor("_ShadowTint", style.ShadowTint);
            mat.SetColor("_RimColor", style.RimColor);
            mat.SetFloat("_RimIntensity", style.RimIntensity);
            mat.SetFloat("_RimPower", style.RimPower > 0.01f ? style.RimPower : 3f);
            mat.SetColor("_SpecTint", style.SpecTint != default ? style.SpecTint : Color.white);
            mat.SetFloat("_Gloss", style.Gloss > 0.01f ? style.Gloss : 36f);
            mat.SetFloat("_SpecIntensity", style.SpecIntensity);
            mat.SetColor("_EmissionColor", style.EmissionColor);
            mat.SetFloat("_EmissionIntensity", style.EmissionIntensity);
            mat.SetColor("_OutlineColor", style.OutlineColor);
            mat.SetFloat("_OutlineWidth", style.OutlineWidth);
            if (style.OutlineEnabled) mat.EnableKeyword("_OUTLINE_ON");
            else mat.DisableKeyword("_OUTLINE_ON");

            if (mainTex != null)
            {
                mat.SetTexture("_MainTex", mainTex);
                mat.SetVector("_MainTex_ST", tiling == default ? new Vector4(1f, 1f, 0f, 0f) : tiling);
                mat.EnableKeyword("_USE_MAINTEX");
            }
            else
            {
                mat.DisableKeyword("_USE_MAINTEX");
            }
            return mat;
        }

        private static Material BuildGlow(Color color, float intensity, float softEdge, float pulseSpeed, float pulseAmount)
        {
            var mat = new Material(GlowShader) { name = "QB_Glow_Instance", hideFlags = HideFlags.DontUnloadUnusedAsset };
            mat.SetColor("_TintColor", color);
            mat.SetFloat("_Intensity", intensity);
            mat.SetFloat("_SoftEdge", softEdge);
            mat.SetFloat("_PulseSpeed", pulseSpeed);
            mat.SetFloat("_PulseAmount", pulseAmount);
            return mat;
        }

        private static string ToonKey(Color baseColor, ToonStyle style, Texture2D mainTex, Vector4 tiling) =>
            $"toon|{baseColor}|{style.ShadowTint}|{style.RimColor}|{style.RimIntensity}|{style.RimPower}|{style.SpecTint}|{style.Gloss}|{style.SpecIntensity}|{style.EmissionColor}|{style.EmissionIntensity}|{style.OutlineColor}|{style.OutlineWidth}|{style.OutlineEnabled}|{(mainTex != null ? mainTex.GetEntityId() : default)}|{tiling}";
    }
}
