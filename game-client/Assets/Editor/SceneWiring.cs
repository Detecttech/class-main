using QuizBattle.Arena;
using QuizBattle.Bootstrap;
using QuizBattle.UI;
using QuizBattle.UI.CharacterSelect;
using QuizBattle.UI.Connect;
using QuizBattle.UI.Lobby;
using QuizBattle.UI.NameEntry;
using QuizBattle.UI.TeacherDashboard;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Attaches each screen's controller script to its scene so the scenes are ready for
/// real Play-mode testing / on-device builds. Screens build their own UI from code in
/// Start(), so each scene just needs one GameObject carrying the right component.
public static class SceneWiring
{
    public static void Run()
    {
        Wire("Boot", go => go.AddComponent<BootController>());
        Wire("Connect", go => go.AddComponent<ConnectScreen>());
        Wire("NameEntry", go => go.AddComponent<NameEntryScreen>());
        Wire("CharacterSelect", go => go.AddComponent<CharacterSelectScreen>());
        Wire("Lobby", go => go.AddComponent<LobbyScreen>());
        Wire("Arena", go => go.AddComponent<ArenaController>());
        Wire("PostMatch", go => go.AddComponent<PostMatchScreen>());
        Wire("TeacherMode", go => go.AddComponent<TeacherModeScreen>());

        Debug.Log("[SceneWiring] All scenes wired.");
        EditorApplication.Exit(0);
    }

    private static void Wire(string sceneName, System.Action<GameObject> attach)
    {
        var path = $"Assets/Scenes/{sceneName}.unity";
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        var existing = GameObject.Find("ScreenController");
        if (existing != null) Object.DestroyImmediate(existing);

        var controllerObj = new GameObject("ScreenController");
        attach(controllerObj);

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SceneWiring] wired {sceneName}");
    }
}
