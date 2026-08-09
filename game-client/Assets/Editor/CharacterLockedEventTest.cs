using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using QuizBattle.GameState;
using QuizBattle.Networking;
using UnityEditor;
using UnityEngine;

/// Regression test for the bug where CharacterSelectScreen got stuck forever on
/// "Waiting for confirmation..." — MatchStateStore updated LobbyPlayers when
/// character_locked arrived but never raised an event, so nothing told the screen to
/// re-check. This confirms the event now actually fires (the mechanism the screen fix
/// depends on) — it does NOT exercise the MonoBehaviour screen itself, which still needs
/// real Play mode to verify end-to-end.
public static class CharacterLockedEventTest
{
    public static void Run()
    {
        try
        {
            Task.Run(RunAsync).GetAwaiter().GetResult();
            Debug.Log("[CharacterLockedEventTest] PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterLockedEventTest] FAILED: {e}");
            EditorApplication.Exit(1);
        }
    }

    private static async Task RunAsync()
    {
        var bootstrap = JObject.Parse(File.ReadAllText("D:/temp/match-bootstrap.json"));
        string joinCode = bootstrap["joinCode"].Value<string>();

        var client = new WsClient();
        var store = new MatchStateStore();
        store.Bind(client);

        int? playerId = null;
        client.MessageReceived += env => { if (env.Type == "hello_ack") playerId = env.Payload["playerId"].ToObject<int>(); };
        await client.Connect("ws://localhost:7777/ws").ConfigureAwait(false);
        client.Send("hello", new { role = "student" });
        await PumpUntil(client, () => playerId != null, 5000).ConfigureAwait(false);

        client.Send("join_lobby", new { joinCode, name = "EventTester" });
        await PumpUntil(client, () => store.LobbyPlayers.Exists(p => p.playerId == playerId), 5000).ConfigureAwait(false);

        if (store.Mode != "teams" && store.Mode != "ffa")
        {
            throw new Exception($"expected store.Mode to be populated from lobby_state, got \"{store.Mode}\"");
        }
        Debug.Log($"[CharacterLockedEventTest] store.Mode correctly populated: {store.Mode}");

        bool eventFired = false;
        string eventCharacterId = null;
        store.CharacterLocked += payload =>
        {
            if (payload.PlayerId == playerId)
            {
                eventFired = true;
                eventCharacterId = payload.CharacterId;
            }
        };

        client.Send("select_character", new { characterId = "blaze" });
        await PumpUntil(client, () => eventFired, 5000).ConfigureAwait(false);

        if (!eventFired) throw new Exception("CharacterLocked event never fired after select_character — the bug is NOT fixed");
        if (eventCharacterId != "blaze") throw new Exception($"CharacterLocked fired with wrong characterId: {eventCharacterId}");

        var mine = store.LobbyPlayers.Find(p => p.playerId == playerId);
        if (mine?.characterId != "blaze") throw new Exception("LobbyPlayers entry was not updated correctly");

        Debug.Log("[CharacterLockedEventTest] CharacterLocked event fired correctly and data matches");
        await client.Close().ConfigureAwait(false);
    }

    private static async Task PumpUntil(WsClient client, Func<bool> condition, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            client.PumpMessages();
            await Task.Delay(50).ConfigureAwait(false);
        }
        if (!condition()) throw new Exception("PumpUntil timed out");
    }
}
