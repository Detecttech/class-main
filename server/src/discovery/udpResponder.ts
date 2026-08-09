import dgram from "node:dgram";
import { config } from "../config";

const DISCOVERY_REQUEST = "quizbattle-discover";

export function startDiscoveryResponder() {
  const socket = dgram.createSocket({ type: "udp4", reuseAddr: true });

  socket.on("message", (msg, rinfo) => {
    let parsed: { discover?: string };
    try {
      parsed = JSON.parse(msg.toString());
    } catch {
      return;
    }
    if (parsed.discover !== DISCOVERY_REQUEST) return;

    const response = JSON.stringify({
      serverName: config.serverName,
      port: config.httpPort,
      mode: config.mode,
    });
    socket.send(response, rinfo.port, rinfo.address);
  });

  socket.on("error", (err) => {
    console.error("[discovery] UDP responder error:", err);
  });

  socket.bind(config.discoveryPort, () => {
    socket.setBroadcast(true);
    console.log(`[discovery] UDP responder listening on ${config.discoveryPort}`);
  });

  return socket;
}
