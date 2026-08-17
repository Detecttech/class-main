using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace QuizBattle.Networking
{
    [Serializable]
    public class DiscoveredServer
    {
        public string serverName;
        public int port;
        public string mode;
        public string ip;
    }

    /// LAN discovery: broadcasts a UDP packet matching server/src/discovery/udpResponder.ts's
    /// expected {"discover":"quizbattle-discover"} payload and collects responses for a short
    /// window. Falls back to manual IP entry / QR scan in the UI when nothing responds
    /// (school WiFi frequently blocks broadcast traffic).
    public static class DiscoveryClient
    {
        private const int DiscoveryPort = 7778;
        private const string DiscoveryRequest = "quizbattle-discover";

        public static async Task<DiscoveredServer[]> Discover(int timeoutMs = 2000)
        {
            var found = new System.Collections.Generic.List<DiscoveredServer>();
#if UNITY_WEBGL && !UNITY_EDITOR
            // Raw UDP sockets don't exist in a browser sandbox — System.Net.Sockets isn't
            // even available on this platform. WebGL players always fall through to the
            // manual IP-entry field in ConnectScreen, same as any LAN that blocks broadcast.
            await Task.Yield();
            return found.ToArray();
#else
            using var udp = new UdpClient { EnableBroadcast = true };

            var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { discover = DiscoveryRequest }));
            try
            {
                await udp.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DiscoveryClient] broadcast failed: {e.Message}");
                return found.ToArray();
            }

            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining.TotalMilliseconds <= 0) break;

                var receiveTask = udp.ReceiveAsync();
                var completed = await Task.WhenAny(receiveTask, Task.Delay(remaining));
                if (completed != receiveTask) break;

                var result = receiveTask.Result;
                try
                {
                    var server = JsonConvert.DeserializeObject<DiscoveredServer>(Encoding.UTF8.GetString(result.Buffer));
                    server.ip = result.RemoteEndPoint.Address.ToString();
                    found.Add(server);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DiscoveryClient] bad response: {e.Message}");
                }
            }

            return found.ToArray();
#endif
        }
    }
}
