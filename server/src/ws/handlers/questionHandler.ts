import type { ClientConnection, Envelope } from "../wsServer";
import { buildEmit, requireLive, requirePlayerId } from "../liveMatchEmit";
import { handleSubmitAnswer } from "../../matchEngine/LiveMatchRegistry";

export function handleAnswer(conn: ClientConnection, msg: Envelope) {
  const live = requireLive(conn, msg.correlationId);
  const playerId = requirePlayerId(conn, msg.correlationId);
  if (!live || playerId === null) return;

  const payload = msg.payload as { choiceIndex?: number } | undefined;
  const emit = buildEmit(live.matchId);
  handleSubmitAnswer(live, playerId, payload?.choiceIndex ?? -1, emit);
}
