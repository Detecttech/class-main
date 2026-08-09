using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using QuizBattle.GameState;
using QuizBattle.Networking;
using UnityEditor;
using UnityEngine;

/// Headless verification for the NEW auth-backed flow added alongside the real UI
/// screens: AuthClient.StudentLogin (REST) against a real teacher-provisioned roster
/// entry, then re-authenticating the WS connection with that JWT and joining the match
/// lobby as the REAL student profile (not a fake ws-connection-id stand-in, which is what
/// exposed the server crash bug earlier). This is the part of ConnectScreen/NameEntryScreen
/// that safely runs outside Play mode (AuthClient/WsClient touch no Unity APIs);
/// the MonoBehaviour screens themselves need real Play mode to test end-to-end since they
/// rely on Unity's main-thread SynchronizationContext after each await.
public static class AuthFlowDemoRunner
{
    public static void Run()
    {
        try
        {
            Task.Run(RunAsync).GetAwaiter().GetResult();
            Debug.Log("[AuthFlowDemoRunner] PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AuthFlowDemoRunner] FAILED: {e}");
            EditorApplication.Exit(1);
        }
    }

    private static async Task RunAsync()
    {
        var bootstrap = JObject.Parse(File.ReadAllText("D:/temp/match-bootstrap.json"));
        string joinCode = bootstrap["joinCode"].Value<string>();
        string classCode = bootstrap["classCode"].Value<string>();

        SessionManager.ServerHost = "localhost";
        SessionManager.ServerPort = 7777;

        // First login for "Alice" sets her PIN server-side (see authRoutes.ts).
        var login = await AuthClient.StudentLogin(classCode, "Alice", "1234").ConfigureAwait(false);
        Debug.Log($"[AuthFlowDemoRunner] logged in as studentId={login.StudentId} name={login.Name}");
        if (string.IsNullOrEmpty(login.Token)) throw new Exception("login did not return a token");

        // Second login must succeed with the SAME pin now that it's set.
        var relogin = await AuthClient.StudentLogin(classCode, "Alice", "1234").ConfigureAwait(false);
        if (relogin.StudentId != login.StudentId) throw new Exception("relogin returned a different student id");

        // A wrong PIN on an already-set account must be rejected.
        try
        {
            await AuthClient.StudentLogin(classCode, "Alice", "9999").ConfigureAwait(false);
            throw new Exception("expected wrong-PIN login to fail, but it succeeded");
        }
        catch (Exception e) when (!e.Message.StartsWith("expected wrong-PIN"))
        {
            Debug.Log($"[AuthFlowDemoRunner] wrong PIN correctly rejected: {e.Message}");
        }

        var client = new WsClient();
        int? ackedPlayerId = null;
        client.MessageReceived += env =>
        {
            if (env.Type == "hello_ack") ackedPlayerId = env.Payload["playerId"].ToObject<int>();
        };

        await client.Connect(SessionManager.WsUrl).ConfigureAwait(false);
        client.Send("hello", new { role = "student", token = login.Token });
        await PumpUntil(client, () => ackedPlayerId != null, 5000).ConfigureAwait(false);

        if (ackedPlayerId != login.StudentId)
        {
            throw new Exception($"expected hello_ack playerId to equal the real studentId ({login.StudentId}), got {ackedPlayerId}");
        }
        Debug.Log("[AuthFlowDemoRunner] WS re-auth bound the connection to the real student profile");

        var store = new MatchStateStore();
        store.Bind(client);
        client.Send("join_lobby", new { joinCode, name = login.Name });
        bool inLobby = await PumpUntil(client, () => store.LobbyPlayers.Exists(p => p.playerId == login.StudentId), 5000).ConfigureAwait(false);
        if (!inLobby) throw new Exception("did not appear in lobby_state after join_lobby");

        Debug.Log("[AuthFlowDemoRunner] joined match lobby as an authenticated student");
        await client.Close().ConfigureAwait(false);
    }

    private static async Task<bool> PumpUntil(WsClient client, Func<bool> condition, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            client.PumpMessages();
            await Task.Delay(50).ConfigureAwait(false);
        }
        return condition();
    }
}
