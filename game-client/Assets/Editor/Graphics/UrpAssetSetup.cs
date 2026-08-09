using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace QuizBattle.EditorTools.Graphics
{
    /// One-time setup that creates and assigns a mobile-tuned URP asset. Run only after
    /// the com.unity.render-pipelines.universal package has resolved (see
    /// Packages/manifest.json) — referencing UnityEngine.Rendering.Universal types before
    /// that would fail to compile the whole Assembly-CSharp-Editor assembly.
    public static class UrpAssetSetup
    {
        private const string SettingsDir = "Assets/Settings";
        private const string RendererPath = SettingsDir + "/QuizBattle_Renderer.asset";
        private const string PipelinePath = SettingsDir + "/QuizBattle_URP.asset";

        [MenuItem("Tools/Scaffold/Setup URP Asset")]
        public static void Run()
        {
            if (!Directory.Exists(SettingsDir)) Directory.CreateDirectory(SettingsDir);

            var rendererData = LoadOrCreate<UniversalRendererData>(RendererPath);
            rendererData.renderingMode = RenderingMode.Forward;
            rendererData.intermediateTextureMode = IntermediateTextureMode.Auto;
            EditorUtility.SetDirty(rendererData);

            UniversalRenderPipelineAsset urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (urp == null)
            {
                urp = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(urp, PipelinePath);
            }

            // Public setters.
            urp.supportsHDR = true;
            urp.hdrColorBufferPrecision = HDRColorBufferPrecision._32Bits;
            urp.msaaSampleCount = 4;
            urp.mainLightShadowmapResolution = 1024;
            urp.shadowDistance = 30f;
            urp.shadowCascadeCount = 1;
            urp.maxAdditionalLightsCount = 4;

            // Internal-set properties on this version — go through SerializedObject.
            var so = new SerializedObject(urp);
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetBool(so, "m_AdditionalLightShadowsSupported", false);
            SetEnumInt(so, "m_MainLightRenderingMode", (int)LightRenderingMode.PerPixel);
            SetEnumInt(so, "m_AdditionalLightsRenderingMode", (int)LightRenderingMode.PerPixel);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(urp);
            AssetDatabase.SaveAssets();

            AssignToGraphicsAndQuality(urp);

            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                Debug.Log("[UrpAssetSetup] Switched PlayerSettings.colorSpace to Linear (toon shading/bloom need Linear to look correct).");
            }

            AssetDatabase.Refresh();
            Debug.Log("[UrpAssetSetup] URP asset created/updated and assigned to GraphicsSettings + all quality levels.");

            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void AssignToGraphicsAndQuality(UniversalRenderPipelineAsset urp)
        {
            GraphicsSettings.defaultRenderPipeline = urp;

            int originalLevel = QualitySettings.GetQualityLevel();
            int levelCount = QualitySettings.names.Length;
            for (int i = 0; i < levelCount; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(originalLevel, false);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var instance = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(instance, path);
            return instance;
        }

        private static void SetBool(SerializedObject so, string propertyPath, bool value)
        {
            var prop = so.FindProperty(propertyPath);
            if (prop == null)
            {
                Debug.LogWarning($"[UrpAssetSetup] SerializedProperty '{propertyPath}' not found on UniversalRenderPipelineAsset — Unity version mismatch?");
                return;
            }
            prop.boolValue = value;
        }

        private static void SetEnumInt(SerializedObject so, string propertyPath, int value)
        {
            var prop = so.FindProperty(propertyPath);
            if (prop == null)
            {
                Debug.LogWarning($"[UrpAssetSetup] SerializedProperty '{propertyPath}' not found on UniversalRenderPipelineAsset — Unity version mismatch?");
                return;
            }
            prop.enumValueIndex = value;
        }
    }
}
