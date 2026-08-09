import { WebSocketServer, WebSocket } from "ws";
import type { Server as HttpServer } from "node:http";
import { dispatch } from "./dispatcher";
import { onConnectionClosed } from "./handlers/connectionHandler";

export interface Envelope {
  type: string;
  seq?: number;
  correlationId?: string;
  payload?: unknown;
}

export interface ClientConnection {
  id: number;
  socket: WebSocket;
  role?: "student" | "teacher" | "spectator";
  playerId?: number;
  matchId?: number;
  isSpectator?: boolean;
}

let nextClientId = 1;
export const clients = new Map<number, ClientConnection>();

export function send(conn: ClientConnection, message: Envelope) {
  if (conn.socket.readyState === WebSocket.OPEN) {
    conn.socket.send(JSON.stringify(message));
  }
}

export function sendToClientId(clientId: number, message: Envelope) {
  const conn = clients.get(clientId);
  if (conn) send(conn, message);
}

export function broadcast(message: Envelope, predicate?: (conn: ClientConnection) => boolean) {
  for (const conn of clients.values()) {
    if (!predicate || predicate(conn)) send(conn, message);
  }
}

export function createWsServer(httpServer: HttpServer) {
  const wss = new WebSocketServer({ server: httpServer, path: "/ws" });

  wss.on("connection", (socket) => {
    const conn: ClientConnection = { id: nextClientId++, socket };
    clients.set(conn.id, conn);

    socket.on("message", (raw) => {
      let msg: Envelope;
      try {
        msg = JSON.parse(raw.toString());
      } catch {
        send(conn, { type: "error", payload: { code: "bad_json", message: "Invalid JSON" } });
        return;
      }
      dispatch(conn, msg);
    });

    socket.on("close", () => {
      onConnectionClosed(conn);
      clients.delete(conn.id);
    });
  });

  return wss;
}
