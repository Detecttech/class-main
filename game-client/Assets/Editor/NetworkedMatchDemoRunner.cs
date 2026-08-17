using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using QuizBattle.Arena;
using QuizBattle.Characters;
using QuizBattle.GameState;
using QuizBattle.Networking;
using QuizBattle.Networking.Protocol;
using QuizBattle.UI.HUD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Headless end-to-end proof for Phase 2 (networked LAN multiplayer): two "student"
/// WsClients and one "teacher" WsClient talk to a REAL running Node server (see
/// server/bootstrap-match.mjs, run separately to produce D:/temp/match-bootstrap.json)
/// through the full lobby -> character-select -> ready -> start -> question -> answer ->
/// reward -> attack -> player-advanced -> match-end flow. MatchStateStore + NetworkedArenaView
/// render the result using the same visual layer as the Phase 1 local demo, and a
/// screenshot is captured so the render can be inspected, not just asserted on.
public static class NetworkedMatchDemoRunner
{
    private class PlayerConn
    {
        public string Name;
        public string CharacterId;
        public WsClient Client = new WsClient();
        public MatchStateStore Store = new MatchStateStore();
        public int PlayerId;
        public int? LastAnsweredQuestionId;

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
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkedMatchDemoRunner] FAILED: {e}");
            EditorApplication.Exit(1);
        }
    }

    private static async Task RunAsync()
    {
        var bootstrap = JObject.Parse(File.ReadAllText("D:/temp/match-bootstrap.json"));
        int matchId = bootstrap["matchId"].Value<int>();
        string joinCode = bootstrap["joinCode"].Value<string>();
        string teacherToken = bootstrap["teacherToken"].Value<string>();

        var alice = new PlayerConn { Name = "Alice", CharacterId = "blaze" };
        var bob = new PlayerConn { Name = "Bob", CharacterId = "aegis" };

        await ConnectAndJoin(alice, joinCode).ConfigureAwait(false);
        await ConnectAndJoin(bob, joinCode).ConfigureAwait(false);
        Debug.Log($"[NetworkedMatchDemoRunner] connected: alice={alice.PlayerId} bob={bob.PlayerId}");

        alice.Client.Send("select_character", new { characterId = alice.CharacterId });
        bob.Client.Send("select_character", new { characterId = bob.CharacterId });
        await PumpUntil(new[] { alice.Client, bob.Client }, () =>
            alice.Store.LobbyPlayers.Find(p => p.playerId == alice.PlayerId)?.characterId == alice.CharacterId &&
            alice.Store.LobbyPlayers.Find(p => p.playerId == bob.PlayerId)?.characterId == bob.CharacterId,
            5000).ConfigureAwait(false);

        alice.Client.Send("player_ready", new { ready = true });
        bob.Client.Send("player_ready", new { ready = true });
        await Task.Delay(300).ConfigureAwait(false);
        alice.Client.PumpMessages();
        bob.Client.PumpMessages();

        var teacher = new WsClient();
        bool teacherReady = false;
        teacher.MessageReceived += env => { if (env.Type == "hello_ack") teacherReady = true; };
        await teacher.Connect("ws://localhost:7777/ws").ConfigureAwait(false);
        teacher.Send("hello", new { role = "teacher", token = teacherToken });
        await PumpUntil(new[] { teacher }, () => teacherReady, 5000).ConfigureAwait(false);

        teacher.Send("teacher_join_match", new { matchId });
        teacher.Send("teacher_start_match", new { });
        Debug.Log("[NetworkedMatchDemoRunner] teacher requested match start");

        // Consume attack_choice rewards immediately; let bonus_move rewards expire
        // unconsumed (movement isn't exercised by this particular test).
        alice.Store.AnswerResultReceived += r => MaybeAttack(alice, bob, r);
        bob.Store.AnswerResultReceived += r => MaybeAttack(bob, alice, r);

        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (alice.Store.MatchResult == null && DateTime.UtcNow < deadline)
        {
            alice.PumpAndAutoPlay();
            bob.PumpAndAutoPlay();
            teacher.PumpMessages();
            await Task.Delay(100).ConfigureAwait(false);
        }

        if (alice.Store.MatchResult == null)
        {
            throw new Exception("Match did not conclude within the 60s timeout");
        }

        Debug.Log($"[NetworkedMatchDemoRunner] PASSED — winner={alice.Store.MatchResult.WinnerId} reason={alice.Store.MatchResult.Reason}");

        await teacher.Close().ConfigureAwait(false);
        await alice.Client.Close().ConfigureAwait(false);
        await bob.Client.Close().ConfigureAwait(false);

        // Rendering must happen back on the main thread — Editor scene/GameObject APIs
        // aren't safe to call from the background thread this method is running on.
        EditorApplication.delayCall += () =>
        {
            try
            {
                RenderAndScreenshot(alice.Store);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkedMatchDemoRunner] render failed: {e}");
                EditorApplication.Exit(1);
            }
        };
    }

    private static void MaybeAttack(PlayerConn attacker, PlayerConn target, AnswerResultPayload result)
    {
        if (result.RewardOffered?.Type == "attack_choice")
        {
            attacker.Client.Send("use_attack", new { rewardId = result.RewardOffered.RewardId, targetPlayerId = target.PlayerId });
        }
    }

    private static async Task ConnectAndJoin(PlayerConn conn, string joinCode)
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

    private static void RenderAndScreenshot(MatchStateStore aliceStore)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var gridObj = new GameObject("Grid");
        var grid = gridObj.AddComponent<GridController>();
        var hud = HudController.Create();
        var rig = ArenaEnvironment.Acquire(new Color(0.08f, 0.08f, 0.13f));
        var camera = rig.Camera;

        // Load the real character defs so this demo exercises the same data path as the
        // shipped game, instead of keeping a second hardcoded color table in sync.
        var visuals = new Dictionary<string, CharacterVisual>();
        foreach (var def in CharacterCatalogLoader.LoadAll()) visuals[def.characterId] = CharacterVisual.From(def);

        // The store already has all events from the live match; replay MatchStarted's
        // effects by constructing the view now and manually re-applying final state,
        // since the view binds to *future* events, not history.
        var view = new NetworkedArenaView(grid, hud, rig, aliceStore, visuals);
        ReplayFinalState(grid, rig, visuals, aliceStore);

        // No player loop in this synchronous batch run — force tokens to their final
        // animated-move target before capturing.
        foreach (var token in UnityEngine.Object.FindObjectsByType<CharacterToken>(FindObjectsSortMode.None))
        {
            token.CompleteMovement();
        }

        var rt = new RenderTexture(1280, 720, 24);
        camera.targetTexture = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        camera.Render();
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        camera.targetTexture = null;
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(rt);

        var path = "D:/temp/arena-networked-demo.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        Debug.Log($"[NetworkedMatchDemoRunner] screenshot saved to {path}");
    }

    private static void ReplayFinalState(GridController grid, ArenaRig rig, Dictionary<string, CharacterVisual> visuals, MatchStateStore store)
    {
        grid.BuildGrid(store.GridWidth, store.GridHeight, store.GoalRow);

        ArenaEnvironment.FrameGrid(rig, grid, store.GridWidth, store.GridHeight);

        foreach (var p in store.Players.Values)
        {
            var visual = visuals.TryGetValue(p.characterId, out var v) ? v : CharacterVisual.Fallback(Color.gray);
            var token = CharacterToken.Create(p.name, visual, grid.TileToWorldPos(p.pos.x, p.pos.y));
            token.SetHp(p.hp, p.maxHp);
            if (p.streak >= 2) token.SetStreak(p.streak);
            if (!p.alive) token.SetEliminated();
        }
    }
}
