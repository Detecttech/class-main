using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuizBattle.EditorTools.Graphics
{
    /// Ensures the hand-written toon/glow shaders survive Android build stripping.
    /// A shader referenced by no material/scene asset gets stripped from the player
    /// build even though Shader.Find works fine in the Editor — this is what causes
    /// "works on desktop, magenta on device." Adding them to GraphicsSettings'
    /// always-included list is the cheapest guarantee against that.
    public static class GraphicsAssetSetup
    {
        private static readonly string[] RequiredShaderNames =
        {
            "QuizBattle/Toon",
            "QuizBattle/GlowAdditive",
        };

        [MenuItem("Tools/Scaffold/Ensure Always-Included Shaders")]
        public static void Run()
        {
            var graphicsSettingsObj = GraphicsSettings.GetGraphicsSettings();
            var so = new SerializedObject(graphicsSettingsObj);
            var prop = so.FindProperty("m_AlwaysIncludedShaders");
            if (prop == null)
            {
                Debug.LogError("[GraphicsAssetSetup] Could not find m_AlwaysIncludedShaders on GraphicsSettings.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            bool changed = false;
            foreach (var name in RequiredShaderNames)
            {
                var shader = Shader.Find(name);
                if (shader == null)
                {
                    Debug.LogError($"[GraphicsAssetSetup] Shader '{name}' not found — cannot add to always-included list.");
                    continue;
                }

                if (AlreadyIncluded(prop, shader)) continue;

                int index = prop.arraySize;
                prop.InsertArrayElementAtIndex(index);
                prop.GetArrayElementAtIndex(index).objectReferenceValue = shader;
                changed = true;
                Debug.Log($"[GraphicsAssetSetup] Added '{name}' to always-included shaders.");
            }

            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
            else
            {
                Debug.Log("[GraphicsAssetSetup] Always-included shaders already up to date.");
            }

            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static bool AlreadyIncluded(SerializedProperty arrayProp, Shader shader)
        {
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                if (arrayProp.GetArrayElementAtIndex(i).objectReferenceValue == shader) return true;
            }
            return false;
        }
    }
}
