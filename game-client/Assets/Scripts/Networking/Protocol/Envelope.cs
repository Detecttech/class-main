using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QuizBattle.Networking.Protocol
{
    /// Mirrors the server's WS message envelope exactly (server/src/ws/wsServer.ts Envelope).
    public class Envelope
    {
        [JsonProperty("type")]
        public string Type;

        [JsonProperty("seq")]
        public int? Seq;

        [JsonProperty("correlationId")]
        public string CorrelationId;

        [JsonProperty("payload")]
        public JObject Payload;
    }
}
