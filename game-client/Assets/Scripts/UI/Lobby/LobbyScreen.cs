using System;
using System.Text;
using QuizBattle.Bootstrap;
using QuizBattle.Networking;
using QuizBattle.Networking.Protocol;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QuizBattle.UI.Lobby
{
    /// Shows who else has joined and their ready state; the match itself starts only when
    /// the teacher sends teacher_start_match (see ws/handlers/lobbyHandler.ts) — this
    /// screen just reflects lobby_state and reacts to match_start by moving on to Arena.
    /// For team matches, players pick their own team here (Kahoot-style self-assignment)
    /// via select_team — see server/src/matchEngine/LiveMatchRegistry.ts selectTeam.
    public class LobbyScreen : MonoBehaviour
    {
        private TMP_Text _playerListText;
        private TMP_Text _statusText;
        private Button _readyButton;
        private Button _teamAButton;
        private Button _teamBButton;
        private bool _isReady;
        private bool _isTeamMatch;
        private bool _reconnecting;

        private void Start()
        {
            Build();

            var store = AppRoot.Instance.Store;
            store.LobbyUpdated += OnLobbyUpdated;
            store.CharacterLocked += OnCharacterLocked;
            store.MatchStarted += OnMatchStarted;
            AppRoot.Instance.Client.Disconnected += OnDisconnected;

            _isTeamMatch = store.Mode == "teams";
            _teamAButton.gameObject.SetActive(_isTeamMatch);
            _teamBButton.gameObject.SetActive(_isTeamMatch);
            if (store.LobbyPlayers.Count > 0) RenderPlayers(store);
        }

        private void OnDestroy()
        {
            if (AppRoot.Instance == null) return;
            var store = AppRoot.Instance.Store;
            store.LobbyUpdated -= OnLobbyUpdated;
            store.CharacterLocked -= OnCharacterLocked;
            store.MatchStarted -= OnMatchStarted;
            AppRoot.Instance.Client.Disconnected -= OnDisconnected;
        }

        // A dropped connection (WiFi hiccup, app backgrounded, etc.) during the lobby
        // wait silently removes this player's lobby entry server-side (see
        // LiveMatchRegistry.removeConnection) with nothing to automatically re-add them —
        // WsClient has no reconnect logic of its own. This replays the same
        // hello+join_lobby sequence NameEntryScreen used originally, using the session
        // state it already stashed in SessionManager, so the player reappears in the
        // lobby without needing to back out and re-enter their details.
        private async void OnDisconnected(string reason)
        {
            if (_reconnecting) return;
            _reconnecting = true;
            _statusText.text = "Connection lost — reconnecting...";

            var client = AppRoot.Instance.Client;
            try
            {
                await client.Connect(SessionManager.WsUrl);
            }
            catch (Exception e)
            {
                _statusText.text = $"Reconnect failed: {e.Message}";
                _reconnecting = false;
                return;
            }

            bool acked = false;
            void OnAck(Envelope env)
            {
                if (env.Type == "hello_ack")
                {
                    SessionManager.PlayerId = env.Payload["playerId"].ToObject<int>();
                    acked = true;
                }
            }
            client.MessageReceived += OnAck;
            client.Send("hello", new { role = "student", token = SessionManager.AuthToken });
            if (!await WsClient.WaitUntil(client, () => acked, 5000))
            {
                client.MessageReceived -= OnAck;
                _statusText.text = "Reconnected, but re-authentication timed out.";
                _reconnecting = false;
                return;
            }
            client.MessageReceived -= OnAck;

            var store = AppRoot.Instance.Store;
            client.Send("join_lobby", new { joinCode = SessionManager.JoinCode, name = SessionManager.StudentName });
            bool inLobby = await WsClient.WaitUntil(client, () => store.LobbyPlayers.Exists(p => p.playerId == SessionManager.PlayerId), 5000);

            _statusText.text = inLobby
                ? "Reconnected — waiting for the teacher to start the match..."
                : "Reconnected, but could not rejoin the lobby. Try going back and re-entering the match code.";
            _reconnecting = false;
        }

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas();
            var title = UiFactory.CreateText(canvas.transform, "Title", new Vector2(0.5f, 0.88f), new Vector2(700, 60), 32);
            title.text = "Lobby";

            var whoAmI = UiFactory.CreateText(canvas.transform, "WhoAmI", new Vector2(0.5f, 0.95f), new Vector2(500, 30), 16);
            whoAmI.text = $"Playing as: {SessionManager.StudentName} (id {SessionManager.PlayerId})";

            var panel = UiFactory.CreatePanel(canvas.transform, "PlayerListPanel", new Vector2(0.5f, 0.58f), new Vector2(500, 260), new Color(0, 0, 0, 0.4f));
            _playerListText = UiFactory.CreateText(panel.transform, "PlayerList", new Vector2(0.5f, 0.5f), new Vector2(460, 240), 18);
            _playerListText.alignment = TextAlignmentOptions.TopLeft;

            _teamAButton = UiFactory.CreateButton(canvas.transform, "TeamAButton", new Vector2(0.4f, 0.36f), new Vector2(160, 45), "Join Team A");
            _teamAButton.onClick.AddListener(() => OnTeamClicked("A"));
            _teamBButton = UiFactory.CreateButton(canvas.transform, "TeamBButton", new Vector2(0.6f, 0.36f), new Vector2(160, 45), "Join Team B");
            _teamBButton.onClick.AddListener(() => OnTeamClicked("B"));
            _teamAButton.gameObject.SetActive(false);
            _teamBButton.gameObject.SetActive(false);

            _readyButton = UiFactory.CreateButton(canvas.transform, "ReadyButton", new Vector2(0.5f, 0.24f), new Vector2(200, 50), "Ready");
            _readyButton.onClick.AddListener(OnReadyClicked);

            _statusText = UiFactory.CreateText(canvas.transform, "Status", new Vector2(0.5f, 0.12f), new Vector2(700, 60), 18);
            _statusText.text = "Waiting for the teacher to start the match...";
        }

        private void OnReadyClicked()
        {
            _isReady = !_isReady;
            AppRoot.Instance.Client.Send("player_ready", new { ready = _isReady });
            _readyButton.GetComponentInChildren<TMP_Text>().text = _isReady ? "Not Ready" : "Ready";
        }

        private void OnTeamClicked(string team)
        {
            AppRoot.Instance.Client.Send("select_team", new { team });
        }

        private void OnLobbyUpdated(LobbyStatePayload payload)
        {
            _isTeamMatch = payload.Mode == "teams";
            _teamAButton.gameObject.SetActive(_isTeamMatch);
            _teamBButton.gameObject.SetActive(_isTeamMatch);
            RenderPlayers(AppRoot.Instance.Store);
        }

        private void OnCharacterLocked(CharacterLockedPayload payload) => RenderPlayers(AppRoot.Instance.Store);

        private void RenderPlayers(GameState.MatchStateStore store)
        {
            var sb = new StringBuilder();
            foreach (var p in store.LobbyPlayers)
            {
                var character = string.IsNullOrEmpty(p.characterId) ? "no character" : p.characterId;
                var team = _isTeamMatch ? $" — {(string.IsNullOrEmpty(p.team) ? "no team" : $"Team {p.team}")}" : "";
                var you = p.playerId == SessionManager.PlayerId ? " (you)" : "";
                sb.AppendLine($"{p.name}{you} [id {p.playerId}] — {character}{team} — {(p.ready ? "Ready" : "Not ready")}");
            }
            _playerListText.text = sb.ToString();
        }

        private void OnMatchStarted(MatchStartPayload payload)
        {
            SceneManager.LoadScene("Arena");
        }
    }
}
