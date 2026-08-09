using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using QuizBattle.GameState;
using QuizBattle.Networking;
using QuizBattle.Networking.Protocol;
using UnityEditor;
using UnityEngine;

/// Headless end-to-end proof for team mode: 4 real WS connections split across two
/// teams, self-assigning via the new select_team message, verifying (a) friendly fire is
/// rejected server-side even when a client tries it, (b) attacking an opponent succeeds,
/// and (c) the match concludes with a team (not a player) as the winner. Requires
/// D:/temp/match-bootstrap.json from `node bootstrap-match.mjs teams`.
public static class TeamMatchDemoRunner
{
    private class PlayerConn
    {
        public string Name;
        public string CharacterId;
        public string Team;
        public WsClient Client = new WsClient();
        public MatchStateStore Store = new MatchStateStore();
        public int PlayerId;
        public int? LastAnsweredQuestionId;
        public readonly List<ErrorPayload> Errors = new List<ErrorPayload>();

        public void PumpAndAutoPlay()
        {
            Client.PumpMessages();
            var q = Store.CurrentQuestion;
            if (q != null && LastAnsweredQuestionId != q.QuestionId)
            {
                LastAnsweredQuestionId = q.QuestionId;
                Client.Send("submit_answer", new { choiceIndex = 1 }); // matches bootstrap-match.mjs correctIndex
            }
        }
    }

    public static void Run()
    {
        try
        {
            Task.Run(RunAsync).GetAwaiter().GetResult();
            Debug.Log("[TeamMatchDemoRunner] PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError($"[TeamMatchDemoRunner] FAILED: {e}");
            EditorApplication.Exit(1);
        }
    }

    private static async Task RunAsync()
    {
        var bootstrap = JObject.Parse(File.ReadAllText("D:/temp/match-bootstrap.json"));
        int matchId = bootstrap["matchId"].Value<int>();
        string joinCode = bootstrap["joinCode"].Value<string>();

        var players = new[]
        {
            new PlayerConn { Name = "Alice", CharacterId = "blaze", Team = "A" },
            new PlayerConn { Name = "Bob", CharacterId = "aegis", Team = "A" },
            new PlayerConn { Name = "Carol", CharacterId = "zephyr", Team = "B" },
            new PlayerConn { Name = "Dave", CharacterId = "vera", Team = "B" },
        };

        foreach (var p in players) await ConnectJoinAndReady(p, joinCode).ConfigureAwait(false);
        Debug.Log($"[TeamMatchDemoRunner] all 4 connected: {string.Join(", ", players.Select(p => $"{p.Name}={p.PlayerId}/{p.Team}"))}");

        var teacher = new WsClient();
        bool teacherReady = false;
        teacher.MessageReceived += env => { if (env.Type == "hello_ack") teacherReady = true; };
        await teacher.Connect("ws://localhost:7777/ws").ConfigureAwait(false);
        teacher.Send("hello", new { role = "teacher", token = bootstrap["teacherToken"].Value<string>() });
        await PumpUntil(new[] { teacher }, () => teacherReady, 5000).ConfigureAwait(false);
        teacher.Send("teacher_join_match", new { matchId });
        teacher.Send("teacher_start_match", new { });
        Debug.Log("[TeamMatchDemoRunner] teacher requested match start");

        bool triedFriendlyFire = false;
        bool friendlyFireRejected = false;

        foreach (var p in players)
        {
            p.Client.MessageReceived += env =>
            {
                if (env.Type == "error") p.Errors.Add(env.Payload.ToObject<ErrorPayload>());
            };
            p.Store.AnswerResultReceived += result =>
            {
                if (result.RewardOffered?.Type != "attack_choice") return;
                var rewardId = result.RewardOffered.RewardId;

                var teammate = players.FirstOrDefault(o => o != p && o.Team == p.Team && IsAlive(p.Store, o.PlayerId));
                var enemy = players.FirstOrDefault(o => o.Team != p.Team && IsAlive(p.Store, o.PlayerId));

                if (teammate != null && !triedFriendlyFire)
                {
                    triedFriendlyFire = true;
                    p.Client.Send("use_attack", new { rewardId, targetPlayerId = teammate.PlayerId });
                }
                else if (enemy != null)
                {
                    p.Client.Send("use_attack", new { rewardId, targetPlayerId = enemy.PlayerId });
                }
            };
        }

        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (players[0].Store.MatchResult == null && DateTime.UtcNow < deadline)
        {
            foreach (var p in players) p.PumpAndAutoPlay();
            teacher.PumpMessages();

            if (!friendlyFireRejected)
            {
                friendlyFireRejected = players.Any(p => p.Errors.Any(e => e.Code == "friendly_fire_blocked"));
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        if (players[0].Store.MatchResult == null) throw new Exception("Match did not conclude within the 60s timeout");
        if (!triedFriendlyFire) throw new Exception("Test never got a chance to attempt friendly fire (no attack rewards offered while a teammate was alive)");
        if (!friendlyFireRejected) throw new Exception("Friendly fire was NOT rejected by the server — this is a real bug");

        var winnerId = players[0].Store.MatchResult.WinnerId?.ToString();
        Debug.Log($"[TeamMatchDemoRunner] match ended, winner={winnerId} reason={players[0].Store.MatchResult.Reason}");
        if (winnerId != "A" && winnerId != "B")
        {
            throw new Exception($"expected the winner to be a team (\"A\" or \"B\"), got \"{winnerId}\"");
        }

        bool anyDamageDealt = players[0].Store.Players.Values.Any(p => p.hp < p.maxHp);
        if (!anyDamageDealt) throw new Exception("no player ever took damage — enemy attacks may not actually be landing");
        Debug.Log("[TeamMatchDemoRunner] confirmed real combat damage occurred");

        foreach (var p in players) await p.Client.Close().ConfigureAwait(false);
        await teacher.Close().ConfigureAwait(false);
    }

    private static bool IsAlive(MatchStateStore store, int playerId) =>
        store.Players.TryGetValue(playerId, out var p) && p.alive;

    private static async Task ConnectJoinAndReady(PlayerConn conn, string joinCode)
    {
        bool acked = false;
        conn.Store.Bind(conn.Client);
        conn.Client.MessageReceived += env =>
        {
            if (env.Type == "hello_ack")
            {
                conn.PlayerId = env.Payload["playerId"].ToObject<int>();
                acked = true;
            }
        };

        await conn.Client.Connect("ws://localhost:7777/ws").ConfigureAwait(false);
        conn.Client.Send("hello", new { role = "student" });
        await PumpUntil(new[] { conn.Client }, () => acked, 5000).ConfigureAwait(false);

        conn.Client.Send("join_lobby", new { joinCode, name = conn.Name });
        await PumpUntil(new[] { conn.Client }, () => conn.Store.LobbyPlayers.Exists(p => p.playerId == conn.PlayerId), 5000).ConfigureAwait(false);

        conn.Client.Send("select_character", new { characterId = conn.CharacterId });
        conn.Client.Send("select_team", new { team = conn.Team });
        await PumpUntil(new[] { conn.Client }, () =>
        {
            var mine = conn.Store.LobbyPlayers.Find(p => p.playerId == conn.PlayerId);
            return mine != null && mine.characterId == conn.CharacterId && mine.team == conn.Team;
        }, 5000).ConfigureAwait(false);

        conn.Client.Send("player_ready", new { ready = true });
        await Task.Delay(200).ConfigureAwait(false);
        conn.Client.PumpMessages();
    }

    private static async Task PumpUntil(WsClient[] clients, Func<bool> condition, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            foreach (var c in clients) c.PumpMessages();
            await Task.Delay(50).ConfigureAwait(false);
        }
        if (!condition()) throw new Exception("PumpUntil timed out waiting for condition");
    }
}
