using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ImportTmpEssentials
{
    [MenuItem("Tools/Scaffold/Import TMP Essential Resources")]
    public static void RunFromMenu()
    {
        var packageCacheDir = Path.Combine(Application.dataPath, "..", "Library", "PackageCache");
        var uguiDir = Directory.GetDirectories(packageCacheDir, "com.unity.ugui*").FirstOrDefault();
        var packagePath = Path.Combine(uguiDir!, "Package Resources", "TMP Essential Resources.unitypackage");
        AssetDatabase.ImportPackage(packagePath, true); // interactive: shows the normal import dialog
    }

    public static void Run()
    {
        var packageCacheDir = Path.Combine(Application.dataPath, "..", "Library", "PackageCache");
        var uguiDir = Directory.GetDirectories(packageCacheDir, "com.unity.ugui*").FirstOrDefault();
        if (uguiDir == null)
        {
            Debug.LogError("[ImportTmpEssentials] com.unity.ugui package not found in PackageCache");
            EditorApplication.Exit(1);
            return;
        }

        var packagePath = Path.Combine(uguiDir, "Package Resources", "TMP Essential Resources.unitypackage");
        if (!File.Exists(packagePath))
        {
            Debug.LogError($"[ImportTmpEssentials] not found: {packagePath}");
            EditorApplication.Exit(1);
            return;
        }

        AssetDatabase.importPackageCompleted += _ =>
        {
            Debug.Log("[ImportTmpEssentials] import completed");
            EditorApplication.Exit(0);
        };
        AssetDatabase.importPackageFailed += (_, err) =>
        {
            Debug.LogError($"[ImportTmpEssentials] import failed: {err}");
            EditorApplication.Exit(1);
        };

        AssetDatabase.ImportPackage(packagePath, false);
    }
}
