using System;
using System.Threading.Tasks;
using QuizBattle.Bootstrap;
using QuizBattle.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QuizBattle.UI.NameEntry
{
    /// The "greeted by a name screen" flow, scoped to a class per the privacy-conscious
    /// identity design: classCode -> a name the student picks themselves (created on
    /// first use, no teacher pre-registration required) -> a PIN the student sets on
    /// first login and confirms on every later one, plus the match code the teacher is
    /// displaying for this session. No email, no third-party auth — see
    /// server/src/http/routes/authRoutes.ts for the matching login endpoint.
    public class NameEntryScreen : MonoBehaviour
    {
        private TMP_InputField _classCodeField;
        private TMP_InputField _nameField;
        private TMP_InputField _pinField;
        private TMP_InputField _matchCodeField;
        private TMP_Text _statusText;
        private Button _joinButton;

        private void Start()
        {
            Build();
        }

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas();
            var title = UiFactory.CreateText(canvas.transform, "Title", new Vector2(0.5f, 0.88f), new Vector2(700, 60), 32);
            title.text = "Enter Your Name";

            _classCodeField = UiFactory.CreateInputField(canvas.transform, "ClassCodeField", new Vector2(0.5f, 0.7f), new Vector2(320, 50), "Class code");
            _nameField = UiFactory.CreateInputField(canvas.transform, "NameField", new Vector2(0.5f, 0.61f), new Vector2(320, 50), "Your name");
            _pinField = UiFactory.CreateInputField(canvas.transform, "PinField", new Vector2(0.5f, 0.52f), new Vector2(320, 50), "PIN (4 digits)");
            _pinField.contentType = TMP_InputField.ContentType.IntegerNumber;
            _pinField.characterLimit = 4;
            _matchCodeField = UiFactory.CreateInputField(canvas.transform, "MatchCodeField", new Vector2(0.5f, 0.43f), new Vector2(320, 50), "Match code");

            _joinButton = UiFactory.CreateButton(canvas.transform, "JoinButton", new Vector2(0.5f, 0.3f), new Vector2(200, 50), "Join");
            _joinButton.onClick.AddListener(OnJoinClicked);

            _statusText = UiFactory.CreateText(canvas.transform, "Status", new Vector2(0.5f, 0.17f), new Vector2(700, 80), 18);
        }

        public void OnJoinClicked()
        {
            _ = Join(_classCodeField.text.Trim(), _nameField.text.Trim(), _pinField.text.Trim(), _matchCodeField.text.Trim());
        }

        public async Task Join(string classCode, string name, string pin, string matchCode)
        {
            _joinButton.interactable = false;
            _statusText.text = "Signing in...";

            // Deliberately NOT using ConfigureAwait(false) anywhere in this method: it
            // touches Unity UI after every await, which needs the main-thread
            // SynchronizationContext that's only present with ConfigureAwait's default.
            AuthClient.StudentLoginResult login;
            try
            {
                login = await AuthClient.StudentLogin(classCode, name, pin);
            }
            catch (Exception e)
            {
                _statusText.text = e.Message;
                _joinButton.interactable = true;
                return;
            }

            SessionManager.AuthToken = login.Token;
            SessionManager.StudentName = login.Name;
            SessionManager.Role = "student";
            SessionManager.JoinCode = matchCode;

            var client = AppRoot.Instance.Client;
            var store = AppRoot.Instance.Store;

            // Re-authenticate the already-open WS connection as this real student profile
            // (see ConnectScreen — the first hello had no token, so the server was only
            // tracking us by raw connection id until now).
            bool acked = false;
            void OnAck(Networking.Protocol.Envelope env)
            {
                if (env.Type == "hello_ack")
                {
                    SessionManager.PlayerId = env.Payload["playerId"].ToObject<int>();
                    acked = true;
                }
            }
            client.MessageReceived += OnAck;
            client.Send("hello", new { role = "student", token = login.Token });
            if (!await WsClient.WaitUntil(client, () => acked, 5000))
            {
                client.MessageReceived -= OnAck;
                _statusText.text = "Lost connection to the server. Please reconnect.";
                _joinButton.interactable = true;
                return;
            }
            client.MessageReceived -= OnAck;

            _statusText.text = "Joining match...";
            client.Send("join_lobby", new { joinCode = matchCode, name = login.Name });
            bool inLobby = await WsClient.WaitUntil(client, () => store.LobbyPlayers.Exists(p => p.playerId == SessionManager.PlayerId), 5000);

            if (!inLobby)
            {
                _statusText.text = "Could not join that match. Check the match code.";
                _joinButton.interactable = true;
                return;
            }

            SceneManager.LoadScene("CharacterSelect");
        }
    }
}
