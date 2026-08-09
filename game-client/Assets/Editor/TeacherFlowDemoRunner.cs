using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using QuizBattle.GameState;
using QuizBattle.Networking;
using UnityEditor;
using UnityEngine;

/// Headless verification for TeacherModeScreen's underlying logic: AuthClient.TeacherLogin
/// (REST), then the WS teacher_join_match / live_dashboard flow (same events
/// server/src/ws/handlers/teacherSpectatorHandler.ts sends the web portal's
/// LiveMatchMonitorPage). As with AuthFlowDemoRunner, this exercises everything that's
/// safe to run outside Play mode — the MonoBehaviour screen itself still needs real Play
/// mode for true end-to-end coverage (see its class doc comment).
public static class TeacherFlowDemoRunner
{
    public static void Run()
    {
        try
        {
            Task.Run(RunAsync).GetAwaiter().GetResult();
            Debug.Log("[TeacherFlowDemoRunner] PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError($"[TeacherFlowDemoRunner] FAILED: {e}");
            EditorApplication.Exit(1);
        }
    }

    private static async Task RunAsync()
    {
        var bootstrap = JObject.Parse(File.ReadAllText("D:/temp/match-bootstrap.json"));
        int matchId = bootstrap["matchId"].Value<int>();
        string username = bootstrap["teacherUsername"].Value<string>();
        string password = bootstrap["teacherPassword"].Value<string>();

        SessionManager.ServerHost = "localhost";
        SessionManager.ServerPort = 7777;

        var login = await AuthClient.TeacherLogin(username, password).ConfigureAwait(false);
        Debug.Log($"[TeacherFlowDemoRunner] logged in as teacherId={login.TeacherId} ({login.DisplayName})");
        if (string.IsNullOrEmpty(login.Token)) throw new Exception("teacher login did not return a token");

        // Wrong password must be rejected.
        try
        {
            await AuthClient.TeacherLogin(username, "not-the-password").ConfigureAwait(false);
            throw new Exception("expected wrong-password login to fail, but it succeeded");
        }
        catch (Exception e) when (!e.Message.StartsWith("expected wrong-password"))
        {
            Debug.Log($"[TeacherFlowDemoRunner] wrong password correctly rejected: {e.Message}");
        }

        var client = new WsClient();
        var store = new MatchStateStore();
        store.Bind(client);

        bool acked = false;
        client.MessageReceived += env => { if (env.Type == "hello_ack") acked = true; };
        await client.Connect(SessionManager.WsUrl).ConfigureAwait(false);
        client.Send("hello", new { role = "teacher", token = login.Token });
        await PumpUntil(client, () => acked, 5000).ConfigureAwait(false);
        if (!acked) throw new Exception("teacher hello_ack never arrived");

        LiveDashboardReceived dashboardSeen = new LiveDashboardReceived();
        store.DashboardUpdated += _ => dashboardSeen.Seen = true;

        client.Send("teacher_join_match", new { matchId });
        bool gotDashboard = await PumpUntil(client, () => dashboardSeen.Seen, 5000).ConfigureAwait(false);
        if (!gotDashboard) throw new Exception("did not receive live_dashboard after teacher_join_match");

        Debug.Log($"[TeacherFlowDemoRunner] received live_dashboard for match #{matchId} (status={store.Status})");
        await client.Close().ConfigureAwait(false);
    }

    private class LiveDashboardReceived { public bool Seen; }

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
