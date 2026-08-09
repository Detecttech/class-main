namespace QuizBattle.Networking
{
    /// Holds the current connection's identity/session info in memory for the
    /// duration of the app run. Not persisted — students re-enter classCode/name/PIN
    /// each session per the privacy-conscious identity design (see project plan).
    public static class SessionManager
    {
        public static string ServerHost;
        public static int ServerPort = 7777;
        public static string AuthToken;
        public static int? PlayerId;
        public static string Role; // "student" | "teacher"
        public static string StudentName;
        public static string SelectedCharacterId;
        public static int MatchId;
        public static string JoinCode;

        public static string WsUrl => $"ws://{ServerHost}:{ServerPort}/ws";
        public static string HttpBaseUrl => $"http://{ServerHost}:{ServerPort}";

        public static void Reset()
        {
            AuthToken = null;
            PlayerId = null;
            Role = null;
        }
    }
}
