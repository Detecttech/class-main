import type { ClientConnection, Envelope } from "../wsServer";
import { send } from "../wsServer";
import { buildEmit, requireLive, requirePlayerId } from "../liveMatchEmit";
import { handleConsumeBonusMove, handleWaiveReward } from "../../matchEngine/LiveMatchRegistry";

// Handles two branches of reward_consumed: "bonus_move" (advances the player) and
// "waive" (clears a pending attack_choice/freeze reward with no other effect, for
// when the client has no valid target to offer — e.g. the only other player is
// already eliminated). attack_choice/freeze rewards are otherwise consumed via
// use_attack/use_freeze (see attackHandler.ts/freezeHandler.ts), which already
// target an opponent and roll the reward into the action itself.
export function handleRewardConsumed(conn: ClientConnection, msg: Envelope) {
  const live = requireLive(conn, msg.correlationId);
  const playerId = requirePlayerId(conn, msg.correlationId);
  if (!live || playerId === null) return;

  const payload = msg.payload as { rewardId?: string; choice?: string } | undefined;
  const emit = buildEmit(live.matchId);

  if (payload?.choice === "waive") {
    handleWaiveReward(live, playerId, payload?.rewardId ?? "", emit);
    return;
  }
  if (payload?.choice !== "bonus_move") {
    send(conn, {
      type: "error",
      correlationId: msg.correlationId,
      payload: { code: "use_attack_instead", message: "Attack rewards are consumed via use_attack" },
    });
    return;
  }
  handleConsumeBonusMove(live, playerId, payload?.rewardId ?? "", emit);
}
