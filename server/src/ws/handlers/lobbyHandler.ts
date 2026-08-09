import type { ClientConnection, Envelope } from "../wsServer";
import { send } from "../wsServer";
import { buildEmit, requireLive, requirePlayerId } from "../liveMatchEmit";
import { findMatchByJoinCode } from "../../db/repositories/matchRepo";
import {
  getOrCreateLiveMatch,
  joinLobby,
  lobbySnapshot,
  selectCharacter,
  selectTeam,
  setReady,
  startMatchFlow,
  type LiveMatch,
} from "../../matchEngine/LiveMatchRegistry";

function describeStartError(error: string | undefined, live: LiveMatch): string {
  if (error === "not_enough_players") {
    const entries = [...live.lobby.values()];
    const readyCount = entries.filter((e) => e.ready && e.characterId).length;
    return `Need at least 2 ready players with a character picked to start (currently ${readyCount} ready out of ${entries.length} in the lobby).`;
  }
  if (error === "question_bank_empty") {
    return "This match's question bank has no questions yet — add some from the Question Banks page.";
  }
  return "Could not start the match.";
}

export function handleJoinLobby(conn: ClientConnection, msg: Envelope) {
  const payload = msg.payload as { matchId?: number; joinCode?: string; name?: string } | undefined;
  const matchId = payload?.matchId ?? (payload?.joinCode ? findMatchByJoinCode(payload.joinCode)?.id : undefined);
  if (!matchId) {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: "not_found", message: "Unknown match" } });
    return;
  }

  let live;
  try {
    live = getOrCreateLiveMatch(matchId);
  } catch {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: "not_found", message: "Match not found" } });
    return;
  }

  conn.matchId = matchId;
  conn.playerId = conn.playerId ?? conn.id;
  const name = payload?.name ?? `Player${conn.playerId}`;
  joinLobby(live, conn.playerId, conn.id, name);

  buildEmit(matchId)({ type: "lobby_state", payload: lobbySnapshot(live) }, "broadcast");
}

export function handleSelectCharacter(conn: ClientConnection, msg: Envelope) {
  const live = requireLive(conn, msg.correlationId);
  const playerId = requirePlayerId(conn, msg.correlationId);
  if (!live || playerId === null) return;

  const payload = msg.payload as { characterId?: string } | undefined;
  const result = selectCharacter(live, playerId, payload?.characterId ?? "");
  const emit = buildEmit(live.matchId);
  if (!result.ok) {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: result.error, message: "select_character rejected" } });
    return;
  }
  emit({ type: "character_locked", payload: { playerId, characterId: payload?.characterId } }, "broadcast");
}

export function handleSelectTeam(conn: ClientConnection, msg: Envelope) {
  const live = requireLive(conn, msg.correlationId);
  const playerId = requirePlayerId(conn, msg.correlationId);
  if (!live || playerId === null) return;

  const payload = msg.payload as { team?: string } | undefined;
  const result = selectTeam(live, playerId, payload?.team ?? "");
  const emit = buildEmit(live.matchId);
  if (!result.ok) {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: result.error, message: "select_team rejected" } });
    return;
  }
  emit({ type: "lobby_state", payload: lobbySnapshot(live) }, "broadcast");
}

export function handlePlayerReady(conn: ClientConnection, msg: Envelope) {
  const live = requireLive(conn, msg.correlationId);
  const playerId = requirePlayerId(conn, msg.correlationId);
  if (!live || playerId === null) return;

  const payload = msg.payload as { ready?: boolean } | undefined;
  const result = setReady(live, playerId, payload?.ready ?? true);
  const emit = buildEmit(live.matchId);
  if (!result.ok) {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: result.error, message: "player_ready rejected" } });
    return;
  }
  emit({ type: "lobby_state", payload: lobbySnapshot(live) }, "broadcast");
}

// Teacher-triggered: starts the match once enough players are ready with characters picked.
export function handleTeacherStartMatch(conn: ClientConnection, msg: Envelope) {
  const live = requireLive(conn, msg.correlationId);
  if (!live) return;
  if (conn.role !== "teacher") {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: "forbidden", message: "Only a teacher can start the match" } });
    return;
  }
  const emit = buildEmit(live.matchId);
  const result = startMatchFlow(live, emit);
  if (!result.ok) {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: result.error, message: describeStartError(result.error, live) } });
  }
}
