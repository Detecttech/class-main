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
            AutoDetectWebGLOrigin();
        }

        private void AutoDetectWebGLOrigin()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                if (!string.IsNullOrEmpty(Application.absoluteURL))
                {
                    var uri = new Uri(Application.absoluteURL);
                    _hostField.text = uri.Host;
                    if (uri.Port > 0 && uri.Port != 80 && uri.Port != 443)
                    {
                        _portField.text = uri.Port.ToString();
                    }
                    else
                    {
                        _portField.text = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "443" : "80";
                    }
                    SessionManager.UseSsl = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConnectScreen] WebGL URL autodetect skipped: {ex.Message}");
            }
#endif
        }

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas();
            var title = UiFactory.CreateText(canvas.transform, "Title", new Vector2(0.5f, 0.85f), new Vector2(700, 60), 32);
            title.text = "Connect to Classroom Server";

            _hostField = UiFactory.CreateInputField(canvas.transform, "HostField", new Vector2(0.5f, 0.6f), new Vector2(320, 50), "Server IP / Domain (e.g. 192.168.1.5)");
            _portField = UiFactory.CreateInputField(canvas.transform, "PortField", new Vector2(0.5f, 0.5f), new Vector2(320, 50), "Port (default 7777 / 443)");

            _connectButton = UiFactory.CreateButton(canvas.transform, "ConnectButton", new Vector2(0.5f, 0.38f), new Vector2(200, 50), "Connect");
            _connectButton.onClick.AddListener(OnConnectClicked);

            _statusText = UiFactory.CreateText(canvas.transform, "Status", new Vector2(0.5f, 0.25f), new Vector2(700, 80), 18);
        }

        public void OnConnectClicked()
        {
            var hostText = _hostField.text;
            int? explicitPort = int.TryParse(_portField.text, out var p) ? p : (int?)null;
            SessionManager.SetEndpoint(hostText, explicitPort);
            _ = ConnectTo(SessionManager.ServerHost, SessionManager.ServerPort);
        }

        public async Task ConnectTo(string host, int port)
        {
            _connectButton.interactable = false;
            _statusText.text = $"Connecting to {SessionManager.WsUrl}...";
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
