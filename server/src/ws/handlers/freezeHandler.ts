import type { ClientConnection, Envelope } from "../wsServer";
import { buildEmit, requireLive, requirePlayerId } from "../liveMatchEmit";
import { handleUseFreeze } from "../../matchEngine/LiveMatchRegistry";

export function handleFreeze(conn: ClientConnection, msg: Envelope) {
  const live = requireLive(conn, msg.correlationId);
  const playerId = requirePlayerId(conn, msg.correlationId);
  if (!live || playerId === null) return;

  const payload = msg.payload as { rewardId?: string; targetPlayerId?: number } | undefined;
  const emit = buildEmit(live.matchId);
  handleUseFreeze(live, playerId, payload?.rewardId ?? "", payload?.targetPlayerId ?? -1, emit);
}
