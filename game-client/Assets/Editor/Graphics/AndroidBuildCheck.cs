using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace QuizBattle.EditorTools.Graphics
{
    /// Builds a real Android player — the only way to catch GLES3/Vulkan shader compile
    /// failures or shader-stripping regressions that the Editor won't show (Phase 7 of
    /// the graphics upgrade plan).
    public static class AndroidBuildCheck
    {
        [MenuItem("Tools/Scaffold/Android Build Check")]
        public static void Run()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                if (!switched)
                {
                    Debug.LogError("[AndroidBuildCheck] Failed to switch active build target to Android.");
                    Finish(false);
                    return;
                }
            }

            var options = new BuildPlayerOptions
            {
                scenes = System.Array.ConvertAll(EditorBuildSettings.scenes, s => s.path),
                locationPathName = "D:/temp/android-build-check/quizbattle.apk",
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log($"[AndroidBuildCheck] result={report.summary.result} errors={report.summary.totalErrors} warnings={report.summary.totalWarnings} size={report.summary.totalSize}");
            Finish(ok);
        }

        private static void Finish(bool ok)
        {
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
