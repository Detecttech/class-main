import type { ClientConnection, Envelope } from "../wsServer";
import { send } from "../wsServer";
import { buildEmit, requireLive, requirePlayerId } from "../liveMatchEmit";
import { handleConsumeBonusMove } from "../../matchEngine/LiveMatchRegistry";

// Only handles the bonus_move branch of reward_consumed — attack_choice rewards
// are consumed via `use_attack` (see attackHandler.ts), which already targets an
// opponent and rolls the reward into the attack itself.
export function handleRewardConsumed(conn: ClientConnection, msg: Envelope) {
  const live = requireLive(conn, msg.correlationId);
  const playerId = requirePlayerId(conn, msg.correlationId);
  if (!live || playerId === null) return;

  const payload = msg.payload as { rewardId?: string; choice?: string } | undefined;
  if (payload?.choice !== "bonus_move") {
    send(conn, {
      type: "error",
      correlationId: msg.correlationId,
      payload: { code: "use_attack_instead", message: "Attack rewards are consumed via use_attack" },
    });
    return;
  }
  const emit = buildEmit(live.matchId);
  handleConsumeBonusMove(live, playerId, payload?.rewardId ?? "", emit);
}
