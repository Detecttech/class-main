using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Editor-only utility, invoked from the command line via -executeMethod during
// project scaffolding. Not part of the runtime game.
public static class ProjectScaffold
{
    private static readonly string[] SceneNames =
    {
        "Boot", "NameEntry", "Connect", "CharacterSelect", "Lobby", "Arena", "TeacherMode", "PostMatch"
    };

    [MenuItem("Tools/Scaffold/Create Stub Scenes")]
    public static void CreateStubScenes()
    {
        var scenePaths = new string[SceneNames.Length];
        for (int i = 0; i < SceneNames.Length; i++)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var path = $"Assets/Scenes/{SceneNames[i]}.unity";
            EditorSceneManager.SaveScene(scene, path);
            scenePaths[i] = path;
        }

        EditorBuildSettings.scenes = System.Array.ConvertAll(scenePaths, p => new EditorBuildSettingsScene(p, true));
        AssetDatabase.SaveAssets();
        UnityEngine.Debug.Log($"[ProjectScaffold] Created {scenePaths.Length} stub scenes and registered them in Build Settings.");
    }
}
