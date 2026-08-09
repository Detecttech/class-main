using UnityEngine;

namespace QuizBattle.Networking
{
    /// Runtime glue: owns a WsClient and pumps its message queue every frame.
    /// Gameplay scenes should read connection/session state through SessionManager
    /// rather than talking to WsClient directly wherever practical.
    public class WsClientBehaviour : MonoBehaviour
    {
        public WsClient Client { get; private set; }

        private void Awake()
        {
            Client = new WsClient();
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            Client.PumpMessages();
        }

        private async void OnDestroy()
        {
            if (Client != null) await Client.Close();
        }
    }
}
