using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace QuizBattle.EditorTools.Graphics
{
    /// Builds a real WebGL player — the only way to catch shader compile failures
    /// (the QB_Toon/QB_GlowAdditive custom shaders were never verified against WebGL's
    /// GLES3/WebGPU shader compiler) or other browser-platform-specific regressions the
    /// Editor won't show. Output path matches server/src/config.ts's webGLBuildDist so
    /// the server can serve it directly at /play — see http/app.ts.
    public static class WebGLBuildCheck
    {
        [MenuItem("Tools/Scaffold/WebGL Build Check")]
        public static void Run()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
                if (!switched)
                {
                    Debug.LogError("[WebGLBuildCheck] Failed to switch active build target to WebGL.");
                    Finish(false);
                    return;
                }
            }

            // Uncompressed output: express.static (server/src/http/app.ts's /play route)
            // serves these files as-is with no Content-Encoding handling, so a compressed
            // .wasm.gz/.data.gz would just be fed to the browser as if it were raw
            // wasm/data and fail to load. Fine to trade download size for zero server
            // config on a LAN classroom connection.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;

            var options = new BuildPlayerOptions
            {
                scenes = System.Array.ConvertAll(EditorBuildSettings.scenes, s => s.path),
                locationPathName = "D:/game/game-client/webgl-build",
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log($"[WebGLBuildCheck] result={report.summary.result} errors={report.summary.totalErrors} warnings={report.summary.totalWarnings} size={report.summary.totalSize}");
            Finish(ok);
        }

        private static void Finish(bool ok)
        {
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
