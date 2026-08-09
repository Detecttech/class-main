import { clients, send, type ClientConnection } from "./wsServer";
import { getLiveMatch, type Emit, type LiveMatch } from "../matchEngine/LiveMatchRegistry";

export function buildEmit(matchId: number): Emit {
  return (event, target) => {
    if (target === "broadcast" || target === "spectators") {
      for (const conn of clients.values()) {
        if (conn.matchId !== matchId) continue;
        if (target === "spectators" && !conn.isSpectator) continue;
        send(conn, event);
      }
      return;
    }
    // target is a playerId — find the connection currently mapped to it.
    for (const conn of clients.values()) {
      if (conn.matchId === matchId && conn.playerId === target) {
        send(conn, event);
        return;
      }
    }
  };
}

export function resolveLive(conn: ClientConnection): LiveMatch | null {
  if (conn.matchId === undefined) return null;
  return getLiveMatch(conn.matchId) ?? null;
}

export function requireLive(conn: ClientConnection, correlationId: string | undefined): LiveMatch | null {
  const live = resolveLive(conn);
  if (!live) {
    send(conn, { type: "error", correlationId, payload: { code: "not_in_match", message: "Join a match lobby first" } });
    return null;
  }
  return live;
}

export function requirePlayerId(conn: ClientConnection, correlationId: string | undefined): number | null {
  if (conn.playerId === undefined) {
    send(conn, { type: "error", correlationId, payload: { code: "no_player_id", message: "Not identified in this match" } });
    return null;
  }
  return conn.playerId;
}
