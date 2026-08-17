using System;
using System.Text;
using System.Threading.Tasks;
using QuizBattle.Arena;
using QuizBattle.Bootstrap;
using QuizBattle.GameState;
using QuizBattle.Networking;
using QuizBattle.Networking.Protocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuizBattle.UI.TeacherDashboard
{
    /// In-app Teacher Mode: login, then spectate a live match (same WS teacher-spectator
    /// events the web portal's LiveMatchMonitorPage consumes — see
    /// server/src/ws/handlers/teacherSpectatorHandler.ts) and trigger the match start.
    /// Deliberately NOT using ConfigureAwait(false) anywhere below: every await here is
    /// followed by Unity UI updates, which need the main-thread SynchronizationContext
    /// that only exists in real Play mode (see ConnectScreen/NameEntryScreen for the same
    /// note — this bit the project once already).
    public class TeacherModeScreen : MonoBehaviour
    {
        private GameObject _loginPanel;
        private TMP_InputField _usernameField;
        private TMP_InputField _passwordField;
        private TMP_Text _loginStatusText;

        private GameObject _dashboardPanel;
        private TMP_InputField _matchIdField;
        private TMP_Text _dashboardStatusText;
        private TMP_Text _playerListText;
        private Button _startMatchButton;

        private MatchStateStore _store;

        private void Start()
        {
            Build();
        }

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas();

            _loginPanel = UiFactory.CreatePanel(canvas.transform, "LoginPanel", new Vector2(0.5f, 0.5f), new Vector2(420, 320), new Color(QuizBattlePalette.PanelDeep.r, QuizBattlePalette.PanelDeep.g, QuizBattlePalette.PanelDeep.b, 0.95f)).gameObject;
            UiFactory.CreateText(_loginPanel.transform, "Title", new Vector2(0.5f, 0.85f), new Vector2(380, 50), 26).text = "Teacher Login";
            _usernameField = UiFactory.CreateInputField(_loginPanel.transform, "UsernameField", new Vector2(0.5f, 0.65f), new Vector2(320, 50), "Username");
            _passwordField = UiFactory.CreateInputField(_loginPanel.transform, "PasswordField", new Vector2(0.5f, 0.52f), new Vector2(320, 50), "Password");
            _passwordField.contentType = TMP_InputField.ContentType.Password;
            var loginButton = UiFactory.CreateButton(_loginPanel.transform, "LoginButton", new Vector2(0.5f, 0.35f), new Vector2(200, 50), "Log in");
            loginButton.onClick.AddListener(OnLoginClicked);
            _loginStatusText = UiFactory.CreateText(_loginPanel.transform, "Status", new Vector2(0.5f, 0.18f), new Vector2(380, 60), 16);

            _dashboardPanel = UiFactory.CreatePanel(canvas.transform, "DashboardPanel", new Vector2(0.5f, 0.5f), new Vector2(600, 500), new Color(QuizBattlePalette.PanelDeep.r, QuizBattlePalette.PanelDeep.g, QuizBattlePalette.PanelDeep.b, 0.95f)).gameObject;
            UiFactory.CreateText(_dashboardPanel.transform, "Title", new Vector2(0.5f, 0.93f), new Vector2(560, 50), 26).text = "Live Match Monitor";
            _matchIdField = UiFactory.CreateInputField(_dashboardPanel.transform, "MatchIdField", new Vector2(0.35f, 0.83f), new Vector2(220, 45), "Match ID");
            var connectButton = UiFactory.CreateButton(_dashboardPanel.transform, "ConnectButton", new Vector2(0.72f, 0.83f), new Vector2(160, 45), "Watch");
            connectButton.onClick.AddListener(OnWatchClicked);

            _startMatchButton = UiFactory.CreateButton(_dashboardPanel.transform, "StartMatchButton", new Vector2(0.5f, 0.73f), new Vector2(240, 45), "Start Match");
            _startMatchButton.onClick.AddListener(OnStartMatchClicked);
            _startMatchButton.gameObject.SetActive(false);

            var listPanel = UiFactory.CreatePanel(_dashboardPanel.transform, "PlayerListPanel", new Vector2(0.5f, 0.4f), new Vector2(540, 340), new Color(0, 0, 0, 0.4f));
            _playerListText = UiFactory.CreateText(listPanel.transform, "PlayerList", new Vector2(0.5f, 0.5f), new Vector2(520, 320), 16);
            _playerListText.alignment = TextAlignmentOptions.TopLeft;

            _dashboardStatusText = UiFactory.CreateText(_dashboardPanel.transform, "Status", new Vector2(0.5f, 0.06f), new Vector2(560, 40), 14);

            _dashboardPanel.SetActive(false);
        }

        private void OnLoginClicked() => _ = Login(_usernameField.text.Trim(), _passwordField.text);

        private async Task Login(string username, string password)
        {
            _loginStatusText.text = "Signing in...";
            AuthClient.TeacherLoginResult login;
            try
            {
                login = await AuthClient.TeacherLogin(username, password);
            }
            catch (Exception e)
            {
                _loginStatusText.text = e.Message;
                return;
            }

            SessionManager.AuthToken = login.Token;
            SessionManager.Role = "teacher";
            SessionManager.StudentName = login.DisplayName;

            _loginPanel.SetActive(false);
            _dashboardPanel.SetActive(true);
        }

        private void OnWatchClicked()
        {
            if (!int.TryParse(_matchIdField.text.Trim(), out var matchId))
            {
                _dashboardStatusText.text = "Enter a numeric match ID.";
                return;
            }
            _ = Watch(matchId);
        }

        private async Task Watch(int matchId)
        {
            _dashboardStatusText.text = "Connecting...";

            var client = AppRoot.Instance.Client;
            _store = AppRoot.Instance.Store;
            _store.DashboardUpdated += OnDashboardUpdated;
            _store.LobbyUpdated += _ => RenderLobby();
            _store.MatchStarted += _ => _startMatchButton.gameObject.SetActive(false);

            if (!client.IsConnected)
            {
                SessionManager.ServerHost = string.IsNullOrEmpty(SessionManager.ServerHost) ? "localhost" : SessionManager.ServerHost;
                await client.Connect(SessionManager.WsUrl);
            }

            bool acked = false;
            void OnAck(Envelope env) { if (env.Type == "hello_ack") acked = true; }
            client.MessageReceived += OnAck;
            client.Send("hello", new { role = "teacher", token = SessionManager.AuthToken });
            await WsClient.WaitUntil(client, () => acked, 5000);
            client.MessageReceived -= OnAck;

            if (!acked)
            {
                _dashboardStatusText.text = "Could not reach the server.";
                return;
            }

            client.Send("teacher_join_match", new { matchId });
            _dashboardStatusText.text = $"Watching match #{matchId}";
            _startMatchButton.gameObject.SetActive(true);
        }

        private void OnStartMatchClicked()
        {
            AppRoot.Instance.Client.Send("teacher_start_match", new { });
        }

        private void OnDashboardUpdated(LiveDashboardPayload dashboard)
        {
            _dashboardStatusText.text = $"Status: {dashboard.Status}";
            var sb = new StringBuilder();
            foreach (var p in dashboard.Players)
            {
                sb.AppendLine($"{p.Name} — HP {p.Hp} — streak {p.Streak} — {(p.Alive ? "alive" : "eliminated")}");
            }
            _playerListText.text = sb.ToString();
        }

        private void RenderLobby()
        {
            var sb = new StringBuilder();
            foreach (var p in _store.LobbyPlayers)
            {
                sb.AppendLine($"{p.name} — {(string.IsNullOrEmpty(p.characterId) ? "no character" : p.characterId)} — {(p.ready ? "Ready" : "Not ready")}");
            }
            _playerListText.text = sb.ToString();
        }
    }
}
