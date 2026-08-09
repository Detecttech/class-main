using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QuizBattle.Networking
{
    /// REST calls for the auth bootstrap steps that happen before the WS connection is
    /// "who this player is" — kept on plain HttpClient (not UnityWebRequest) so it can be
    /// driven from a synchronous background-thread loop in Editor tooling exactly like
    /// WsClient, without needing Unity's player loop to be ticking.
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

        public static async Task<StudentLoginResult> StudentLogin(string classCode, string name, string pin)
        {
            var json = await Post("/api/auth/student/login", new { classCode, name, pin }).ConfigureAwait(false);
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
            var json = await Post("/api/auth/teacher/login", new { username, password }).ConfigureAwait(false);
            return new TeacherLoginResult
            {
                Token = json["token"]!.ToString(),
                TeacherId = json["teacher"]!["id"]!.ToObject<int>(),
                Username = json["teacher"]!["username"]!.ToString(),
                DisplayName = json["teacher"]!["displayName"]!.ToString(),
            };
        }

        private static async Task<JObject> Post(string path, object body)
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
    }
}
