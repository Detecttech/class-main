using QuizBattle.GameState;
using QuizBattle.Networking;
using UnityEngine;

namespace QuizBattle.Bootstrap
{
    /// Persistent (DontDestroyOnLoad) singleton holding the one WsClient/MatchStateStore
    /// pair used across every scene — screens read from Instance rather than each owning
    /// their own connection. Auto-created before the first scene loads.
    public class AppRoot : MonoBehaviour
    {
        private static AppRoot _instance;

        public static AppRoot Instance
        {
            get
            {
                EnsureBootstrapped();
                return _instance;
            }
        }

        public WsClient Client { get; private set; }
        public MatchStateStore Store { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureBootstrapped()
        {
            if (_instance != null) return;
            var obj = new GameObject("AppRoot");
            _instance = obj.AddComponent<AppRoot>();
            Object.DontDestroyOnLoad(obj);
            _instance.Client = new WsClient();
            _instance.Store = new MatchStateStore();
            _instance.Store.Bind(_instance.Client);
        }

        private void Update()
        {
            Client?.PumpMessages();
        }
    }
}
