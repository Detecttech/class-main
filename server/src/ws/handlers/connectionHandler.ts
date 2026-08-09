import type { ClientConnection, Envelope } from "../wsServer";
import { send } from "../wsServer";
import { verifyToken } from "../../auth/jwt";
import { buildEmit } from "../liveMatchEmit";
import { getLiveMatch, removeConnection } from "../../matchEngine/LiveMatchRegistry";

export function handleHello(conn: ClientConnection, msg: Envelope) {
  const payload = msg.payload as { role?: ClientConnection["role"]; token?: string } | undefined;
  conn.role = payload?.role ?? "student";

  if (payload?.token) {
    try {
      const decoded = verifyToken(payload.token);
      if (decoded.role === "student") {
        conn.playerId = decoded.studentProfileId;
        conn.role = "student";
      } else if (decoded.role === "teacher") {
        conn.role = "teacher";
      }
    } catch {
      send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: "bad_token", message: "Invalid or expired token" } });
      return;
    }
  }

  send(conn, {
    type: "hello_ack",
    correlationId: msg.correlationId,
    payload: { playerId: conn.playerId ?? conn.id, serverTime: Date.now() },
  });
}

export function onConnectionClosed(conn: ClientConnection) {
  if (conn.matchId === undefined) return;
  const live = getLiveMatch(conn.matchId);
  if (live) removeConnection(live, conn.id, buildEmit(live.matchId));
}
