using UnityEditor;
using UnityEngine;

public static class PlatformCheck
{
    public static void Run()
    {
        var active = EditorUserBuildSettings.activeBuildTarget;
        var supported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);
        Debug.Log($"[PlatformCheck] activeBuildTarget={active} androidSupported={supported}");
        EditorApplication.Exit(supported && active == BuildTarget.Android ? 0 : 1);
    }
}
