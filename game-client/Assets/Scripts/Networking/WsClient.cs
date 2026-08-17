using System;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NativeWebSocket;
using QuizBattle.Bootstrap;
using QuizBattle.Networking.Protocol;
using UnityEngine;

namespace QuizBattle.Networking
{
    /// Thin wrapper around NativeWebSocket, decoupled from MonoBehaviour so it can be
    /// driven either by WsClientBehaviour.Update() at runtime or by a manual poll loop
    /// from Editor tooling (see Assets/Editor/AuthFlowDemoRunner.cs, which drives this
    /// class from a raw Task.Run — no scene, no Play mode, no player loop ticking at all).
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
        /// once the socket closes. So we don't await it here; instead we poll _socket.State
        /// directly via WaitForConditionAsync (see below) and let _socket.Connect() keep
        /// running in the background to drive the receive loop that PumpMessages() drains.
        ///
        /// A silently-dropped connection (router/WiFi client isolation, a firewall DROP
        /// instead of REJECT, etc.) means the underlying TCP attempt itself can hang far
        /// longer than any reasonable UI should wait, with none of OnOpen/OnClose/OnError
        /// ever firing — so this needs its own timeout rather than trusting the socket to
        /// eventually fail on its own.
        public async Task Connect(string url, int timeoutMs = 8000)
        {
            string errorMsg = null;
            _socket = new WebSocket(url);
            _socket.OnOpen += () => Connected?.Invoke();
            _socket.OnClose += code => Disconnected?.Invoke(code.ToString());
            _socket.OnError += err =>
            {
                errorMsg = err;
                Error?.Invoke(err);
            };
            _socket.OnMessage += OnRawMessage;
            _ = _socket.Connect();

            await WaitForConditionAsync(
                () => _socket.State == WebSocketState.Open || _socket.State == WebSocketState.Closed || errorMsg != null,
                timeoutMs);

            if (_socket.State == WebSocketState.Open) return;
            if (errorMsg != null) throw new Exception($"WebSocket error: {errorMsg}");
            if (_socket.State == WebSocketState.Closed)
                throw new Exception("WebSocket closed before it finished connecting.");

            throw new TimeoutException(
                $"Timed out after {timeoutMs}ms connecting to {url}. The server never responded — " +
                "check the phone and server are on the same WiFi (not a guest network with client " +
                "isolation enabled), and that nothing between them is silently dropping the connection.");
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
        /// Unity main thread. No-op on WebGL — NativeWebSocket's browser-backed
        /// implementation dispatches inline via the JS event loop and doesn't expose
        /// DispatchMessageQueue() at all (see NativeWebSocket's own WebSocket.cs, which
        /// only compiles that method outside `#if UNITY_WEBGL && !UNITY_EDITOR`).
        public void PumpMessages()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            _socket?.DispatchMessageQueue();
#endif
        }

        public async Task Close()
        {
            if (_socket != null) await _socket.Close();
        }

        public static Task<bool> WaitUntil(WsClient client, Func<bool> condition, int timeoutMs) =>
            WaitForConditionAsync(condition, timeoutMs, client.PumpMessages);

        // Two different polling strategies depending on whether a Unity player loop is
        // actually ticking:
        //
        // - Editor tooling (e.g. AuthFlowDemoRunner.cs, driven via a raw `Task.Run` with
        //   no scene/Play mode/AppRoot at all) and every native platform: a plain
        //   Task.Delay(...) loop. This is what the whole networking layer used
        //   originally and it's always worked fine here — real .NET threads/timers exist.
        //
        // - An actual WebGL browser build: needs a genuine Unity Coroutine instead.
        //   History, for whoever touches this next — two prior approaches were tried and
        //   both failed in a real browser:
        //     1. A TaskCompletionSource resolved from inside NativeWebSocket's WebGL
        //        OnOpen/OnMessage callbacks (which fire via a JS reverse-P/Invoke bridge,
        //        see WebSocketFactory.DelegateOnOpenEvent) — the `await`er never resumed,
        //        even though the browser's Network tab confirmed the WS handshake itself
        //        succeeded (101 Switching Protocols) while the UI stayed stuck forever.
        //     2. `await Task.Yield()` in a loop — this un-stuck case 1, but turned out not
        //        to reliably cede control back to the browser's own event loop either:
        //        when the loop had to run its *full* timeout instead of exiting after a
        //        couple of iterations, it froze the entire tab (Firefox's own "page is
        //        slowing down" watchdog fired) for the whole duration.
        //   A Coroutine's `yield return null` is paced by Unity's actual frame loop
        //   (ultimately requestAnimationFrame on WebGL), which cannot busy-spin, and
        //   resolving the bridging TaskCompletionSource from there — a normal
        //   MonoBehaviour Update-cycle context, not a raw JS-originated callback — has
        //   been reliable where the other two approaches weren't.
        private static async Task<bool> WaitForConditionAsync(Func<bool> condition, int timeoutMs, Action onEachIteration = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            AppRoot.Instance.StartCoroutine(WaitForConditionCoroutine(condition, timeoutMs, onEachIteration, tcs));
            return await tcs.Task;
#else
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (!condition() && DateTime.UtcNow < deadline)
            {
                onEachIteration?.Invoke();
                await Task.Delay(50);
            }
            return condition();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static IEnumerator WaitForConditionCoroutine(Func<bool> condition, int timeoutMs, Action onEachIteration, TaskCompletionSource<bool> tcs)
        {
            float deadline = Time.realtimeSinceStartup + timeoutMs / 1000f;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                onEachIteration?.Invoke();
                yield return null;
            }
            tcs.TrySetResult(condition());
        }
#endif
    }
}
