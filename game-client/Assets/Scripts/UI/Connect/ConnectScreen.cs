using System;
using System.Threading.Tasks;
using QuizBattle.Bootstrap;
using QuizBattle.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QuizBattle.UI.Connect
{
    /// LAN/WAN connect screen: v1 covers manual IP:port entry (the reliable fallback per
    /// the plan — school WiFi often blocks the UDP broadcast discovery path). Discovery
    /// and QR-scan entry points can be added alongside this later without changing the
    /// underlying Connect() flow.
    public class ConnectScreen : MonoBehaviour
    {
        private TMP_InputField _hostField;
        private TMP_InputField _portField;
        private TMP_Text _statusText;
        private Button _connectButton;

        private void Start()
        {
            Build();
        }

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas();
            var title = UiFactory.CreateText(canvas.transform, "Title", new Vector2(0.5f, 0.85f), new Vector2(700, 60), 32);
            title.text = "Connect to Classroom Server";

            _hostField = UiFactory.CreateInputField(canvas.transform, "HostField", new Vector2(0.5f, 0.6f), new Vector2(320, 50), "Server IP (e.g. 192.168.1.5)");
            _portField = UiFactory.CreateInputField(canvas.transform, "PortField", new Vector2(0.5f, 0.5f), new Vector2(320, 50), "Port (default 7777)");

            _connectButton = UiFactory.CreateButton(canvas.transform, "ConnectButton", new Vector2(0.5f, 0.38f), new Vector2(200, 50), "Connect");
            _connectButton.onClick.AddListener(OnConnectClicked);

            _statusText = UiFactory.CreateText(canvas.transform, "Status", new Vector2(0.5f, 0.25f), new Vector2(700, 80), 18);
        }

        public void OnConnectClicked()
        {
            var host = string.IsNullOrWhiteSpace(_hostField.text) ? "localhost" : _hostField.text.Trim();
            var port = int.TryParse(_portField.text, out var p) ? p : 7777;
            _ = ConnectTo(host, port);
        }

        public async Task ConnectTo(string host, int port)
        {
            _connectButton.interactable = false;
            _statusText.text = $"Connecting to {host}:{port}...";
            SessionManager.ServerHost = host;
            SessionManager.ServerPort = port;

            var client = AppRoot.Instance.Client;
            // Deliberately NOT using ConfigureAwait(false): this method touches Unity UI
            // (Text/Button) after each await, which is only safe on the main thread. In
            // real gameplay, Unity's SynchronizationContext resumes these continuations
            // there by default — don't "optimize" this away.
            try
            {
                await client.Connect(SessionManager.WsUrl);
            }
            catch (Exception e)
            {
                _statusText.text = $"Connection failed: {e.Message}";
                _connectButton.interactable = true;
                return;
            }

            bool acked = false;
            void OnAck(Networking.Protocol.Envelope env)
            {
                if (env.Type != "hello_ack") return;
                SessionManager.PlayerId = env.Payload["playerId"].ToObject<int>();
                acked = true;
            }
            client.MessageReceived += OnAck;
            client.Send("hello", new { role = "student" });

            await WsClient.WaitUntil(client, () => acked, 5000);
            client.MessageReceived -= OnAck;

            if (!acked)
            {
                _statusText.text = "Server did not respond. Check the address and try again.";
                _connectButton.interactable = true;
                return;
            }

            _statusText.text = "Connected!";
            SceneManager.LoadScene("NameEntry");
        }
    }
}
