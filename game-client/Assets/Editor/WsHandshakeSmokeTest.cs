using System;
using System.Threading.Tasks;
using QuizBattle.Networking;
using UnityEditor;
using UnityEngine;

/// Editor-only, headless verification that WsClient can complete the hello/hello_ack
/// handshake against a running server. Invoked via:
///   Unity.exe -batchmode -projectPath <path> -executeMethod WsHandshakeSmokeTest.Run -quit
/// Set QUIZBATTLE_WS_URL to override the default ws://localhost:7777/ws target.
public static class WsHandshakeSmokeTest
{
    public static void Run()
    {
        try
        {
            // Run on a thread-pool thread via Task.Run so awaited continuations don't
            // try to resume on Unity's main-thread SynchronizationContext — that thread
            // is the one blocked here on GetResult(), which would deadlock forever.
            Task.Run(RunAsync).GetAwaiter().GetResult();
            Debug.Log("[WsHandshakeSmokeTest] PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError($"[WsHandshakeSmokeTest] FAILED: {e}");
            EditorApplication.Exit(1);
        }
    }

    private static async Task RunAsync()
    {
        var client = new WsClient();
        bool ackReceived = false;
        int? playerId = null;

        client.MessageReceived += envelope =>
        {
            if (envelope.Type == "hello_ack")
            {
                ackReceived = true;
                playerId = envelope.Payload?["playerId"]?.ToObject<int>();
            }
        };
        client.Error += err => Debug.LogError($"[WsHandshakeSmokeTest] socket error: {err}");

        var url = Environment.GetEnvironmentVariable("QUIZBATTLE_WS_URL") ?? "ws://localhost:7777/ws";
        Debug.Log($"[WsHandshakeSmokeTest] connecting to {url}");
        await client.Connect(url).ConfigureAwait(false);

        client.Send("hello", new { role = "student" });

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!ackReceived && DateTime.UtcNow < deadline)
        {
            client.PumpMessages();
            await Task.Delay(50).ConfigureAwait(false);
        }

        await client.Close().ConfigureAwait(false);

        if (!ackReceived)
        {
            throw new Exception("Timed out waiting for hello_ack");
        }

        Debug.Log($"[WsHandshakeSmokeTest] received hello_ack with playerId={playerId}");
    }
}
