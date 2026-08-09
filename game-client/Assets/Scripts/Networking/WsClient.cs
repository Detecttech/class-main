using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NativeWebSocket;
using QuizBattle.Networking.Protocol;
using UnityEngine;

namespace QuizBattle.Networking
{
    /// Thin wrapper around NativeWebSocket, decoupled from MonoBehaviour so it can be
    /// driven either by WsClientBehaviour.Update() at runtime or by a manual poll loop
    /// from Editor tooling (see Assets/Editor/WsHandshakeSmokeTest.cs).
    ///
    /// This is intentionally a "renderer of server state" client: it never computes game
    /// outcomes locally, it only sends intents and relays whatever the authoritative
    /// server broadcasts back (see server/src/matchEngine/MatchEngine.ts for why).
    public class WsClient
    {
        public event Action Connected;
        public event Action<string> Disconnected;
        public event Action<Envelope> MessageReceived;
        public event Action<string> Error;

        private WebSocket _socket;

        public bool IsConnected => _socket != null && _socket.State == WebSocketState.Open;

        /// NativeWebSocket's own _socket.Connect() Task does not complete on open — on
        /// non-WebGL platforms it runs the connect+receive loop inline and only resolves
        /// once the socket closes. So we don't await it here; instead we resolve our own
        /// Task from the OnOpen/OnClose/OnError events and let _socket.Connect() keep
        /// running in the background to drive the receive loop that PumpMessages() drains.
        public Task Connect(string url)
        {
            var tcs = new TaskCompletionSource<bool>();
            _socket = new WebSocket(url);
            _socket.OnOpen += () =>
            {
                Connected?.Invoke();
                tcs.TrySetResult(true);
            };
            _socket.OnClose += code =>
            {
                Disconnected?.Invoke(code.ToString());
                tcs.TrySetResult(false);
            };
            _socket.OnError += err =>
            {
                Error?.Invoke(err);
                tcs.TrySetException(new Exception($"WebSocket error: {err}"));
            };
            _socket.OnMessage += OnRawMessage;
            _ = _socket.Connect();
            return tcs.Task;
        }

        private void OnRawMessage(byte[] bytes)
        {
            var json = Encoding.UTF8.GetString(bytes);
            Envelope envelope;
            try
            {
                envelope = JsonConvert.DeserializeObject<Envelope>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WsClient] Failed to parse message: {e.Message}\n{json}");
                return;
            }
            MessageReceived?.Invoke(envelope);
        }

        public void Send(string type, object payload = null, string correlationId = null)
        {
            if (!IsConnected)
            {
                Debug.LogWarning($"[WsClient] Send({type}) called while not connected");
                return;
            }
            var envelope = new Envelope
            {
                Type = type,
                CorrelationId = correlationId,
                Payload = payload != null ? JObject.FromObject(payload) : null,
            };
            _ = _socket.SendText(JsonConvert.SerializeObject(envelope));
        }

        /// Must be called regularly (e.g. every frame) to dispatch queued messages on the
        /// Unity main thread. No-op on WebGL, where NativeWebSocket dispatches inline.
        public void PumpMessages()
        {
            _socket?.DispatchMessageQueue();
        }

        public async Task Close()
        {
            if (_socket != null) await _socket.Close();
        }
    }
}
