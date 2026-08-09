using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class PipelineCheck
{
    public static void Run()
    {
        var failures = new List<string>();

        var pipeline = GraphicsSettings.defaultRenderPipeline;
        Debug.Log($"[PipelineCheck] defaultRenderPipeline={(pipeline != null ? pipeline.GetType().Name : "null (Built-in RP)")}");
        if (!(pipeline is UniversalRenderPipelineAsset))
        {
            failures.Add("GraphicsSettings.defaultRenderPipeline is not a UniversalRenderPipelineAsset.");
        }

        int originalLevel = QualitySettings.GetQualityLevel();
        string[] names = QualitySettings.names;
        for (int i = 0; i < names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            bool ok = QualitySettings.renderPipeline is UniversalRenderPipelineAsset;
            Debug.Log($"[PipelineCheck] quality[{i}]={names[i]} renderPipeline={(ok ? "URP" : "MISSING/Built-in")}");
            if (!ok) failures.Add($"Quality level '{names[i]}' has no URP asset assigned.");
        }
        QualitySettings.SetQualityLevel(originalLevel, false);

        CheckShader("QuizBattle/Toon", failures, required: true);
        CheckShader("QuizBattle/GlowAdditive", failures, required: true);
        CheckAlwaysIncluded("QuizBattle/Toon", failures);
        CheckAlwaysIncluded("QuizBattle/GlowAdditive", failures);

        Debug.Log($"[PipelineCheck] Sprites/Default found={Shader.Find("Sprites/Default") != null}");

        if (failures.Count > 0)
        {
            foreach (var f in failures) Debug.LogError($"[PipelineCheck] FAIL: {f}");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("[PipelineCheck] All checks passed.");
        EditorApplication.Exit(0);
    }

    private static void CheckShader(string name, List<string> failures, bool required)
    {
        var shader = Shader.Find(name);
        bool found = shader != null;
        bool supported = found && shader.isSupported;
        Debug.Log($"[PipelineCheck] shader '{name}' found={found} supported={supported}");
        if (required && !supported) failures.Add($"Shader '{name}' missing or unsupported.");
    }

    private static void CheckAlwaysIncluded(string name, List<string> failures)
    {
        var shader = Shader.Find(name);
        if (shader == null) return; // already reported by CheckShader

        var graphicsSettingsObj = GraphicsSettings.GetGraphicsSettings();
        var so = new SerializedObject(graphicsSettingsObj);
        var prop = so.FindProperty("m_AlwaysIncludedShaders");
        bool included = false;
        if (prop != null)
        {
            for (int i = 0; i < prop.arraySize; i++)
            {
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue == shader) { included = true; break; }
            }
        }
        Debug.Log($"[PipelineCheck] shader '{name}' always-included={included}");
        if (!included) failures.Add($"Shader '{name}' is not in GraphicsSettings always-included shaders (would be stripped from an Android build).");
    }
}
