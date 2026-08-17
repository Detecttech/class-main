using System;
using System.Collections;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#if UNITY_WEBGL && !UNITY_EDITOR
using QuizBattle.Bootstrap;
using UnityEngine.Networking;
#endif

namespace QuizBattle.Networking
{
    /// REST calls for the auth bootstrap steps that happen before the WS connection is
    /// "who this player is". Two different HTTP implementations depending on whether a
    /// Unity player loop is actually ticking:
    ///
    /// - Editor tooling (e.g. AuthFlowDemoRunner.cs, driven via a raw `Task.Run` with no
    ///   scene/Play mode/player loop at all) and every native platform: plain
    ///   System.Net.Http.HttpClient, which needs no Unity machinery to run.
    ///
    /// - An actual WebGL browser build: HttpClient doesn't work at all there — a browser
    ///   sandbox has no raw socket access, the same fundamental limitation that rules out
    ///   DiscoveryClient's UDP broadcast on WebGL. UnityWebRequest is Unity's own
    ///   WebGL-compatible HTTP client, but it's coroutine-driven, so it needs
    ///   AppRoot.Instance.StartCoroutine (see WsClient.cs's WaitForConditionAsync for the
    ///   same coroutine-bridging pattern and why a Task.Delay/Task.Yield-based approach
    ///   doesn't work reliably in a real browser).
    public static class AuthClient
    {
        private static readonly HttpClient Http = new HttpClient();

        public class StudentLoginResult
        {
            public string Token;
            public int StudentId;
            public string Name;
            public int XpTotal;
        }

        public class TeacherLoginResult
        {
            public string Token;
            public int TeacherId;
            public string Username;
            public string DisplayName;
        }

        // Deliberately NOT using ConfigureAwait(false) on the Post(...) awaits below — see
        // NameEntryScreen/ConnectScreen for the same note. On WebGL, Post's continuation is
        // resolved by PostCoroutine running inside a genuine Unity Coroutine (a
        // MonoBehaviour Update-cycle context), which correctly signals a continuation
        // captured via Unity's own SynchronizationContext. ConfigureAwait(false)
        // deliberately opts OUT of that and asks to resume on a background thread-pool
        // thread instead — but WebGL has no thread pool at all (single-threaded), so that
        // continuation just never ran: StudentLogin/TeacherLogin stayed stuck even though
        // the underlying HTTP call had already completed successfully (confirmed via
        // logging — the request returned 200 and PostCoroutine's TaskCompletionSource was
        // resolved, but execution never returned here). Harmless to drop it for the
        // non-WebGL/Editor-tooling path too: that one is reached via Task.Run with no
        // Unity SynchronizationContext captured in the first place, so there's no "wrong"
        // context for a bare `await` to accidentally capture.
        public static async Task<StudentLoginResult> StudentLogin(string classCode, string name, string pin)
        {
            var json = await Post("/api/auth/student/login", new { classCode, name, pin });
            return new StudentLoginResult
            {
                Token = json["token"]!.ToString(),
                StudentId = json["student"]!["id"]!.ToObject<int>(),
                Name = json["student"]!["name"]!.ToString(),
                XpTotal = json["student"]!["xpTotal"]!.ToObject<int>(),
            };
        }

        public static async Task<TeacherLoginResult> TeacherLogin(string username, string password)
        {
            var json = await Post("/api/auth/teacher/login", new { username, password });
            return new TeacherLoginResult
            {
                Token = json["token"]!.ToString(),
                TeacherId = json["teacher"]!["id"]!.ToObject<int>(),
                Username = json["teacher"]!["username"]!.ToString(),
                DisplayName = json["teacher"]!["displayName"]!.ToString(),
            };
        }

        private static Task<JObject> Post(string path, object body)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<JObject>();
            AppRoot.Instance.StartCoroutine(PostCoroutine(path, body, tcs));
            return tcs.Task;
#else
            return PostViaHttpClient(path, body);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static IEnumerator PostCoroutine(string path, object body, TaskCompletionSource<JObject> tcs)
        {
            var payload = JsonConvert.SerializeObject(body);
            var bytes = Encoding.UTF8.GetBytes(payload);

            using var request = new UnityWebRequest($"{SessionManager.HttpBaseUrl}{path}", "POST");
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            var text = request.downloadHandler.text;
            bool ok = request.result == UnityWebRequest.Result.Success;

            if (!ok)
            {
                string message = $"Request failed: {request.responseCode}";
                try { message = JObject.Parse(text)["message"]?.ToString() ?? message; } catch { /* fall back to status code */ }
                tcs.TrySetException(new Exception(message));
                yield break;
            }

            JObject json;
            try
            {
                json = JObject.Parse(text);
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
                yield break;
            }

            tcs.TrySetResult(json);
        }
#else
        private static async Task<JObject> PostViaHttpClient(string path, object body)
        {
            var payload = JsonConvert.SerializeObject(body);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await Http.PostAsync($"{SessionManager.HttpBaseUrl}{path}", content).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string message = $"Request failed: {response.StatusCode}";
                try { message = JObject.Parse(text)["message"]?.ToString() ?? message; } catch { /* fall back to status code */ }
                throw new Exception(message);
            }

            return JObject.Parse(text);
        }
#endif
    }
}
