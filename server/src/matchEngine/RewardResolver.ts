import type { PendingReward, PlayerState, RewardType } from "./MatchState";
import { REWARD_EXPIRY_QUESTIONS } from "./MatchState";

// Three-way split: roll < ATTACK_CHOICE_WEIGHT => attack_choice; between that and
// ATTACK_CHOICE_WEIGHT + FREEZE_WEIGHT => freeze; the remainder => bonus_move.
const ATTACK_CHOICE_WEIGHT = 0.4;
const FREEZE_WEIGHT = 0.3;

let nextRewardSeq = 1;

/**
 * Rolls a reward for a player whose streak just reached >=2 correct answers.
 * Anti-repeat rule: if the roll would grant another attack_choice immediately
 * after the player's last *consumed* reward was also an attack, it is
 * downgraded to bonus_move instead — this is what "can't attack twice in a
 * row" means when every v1 character has exactly one attack. (Only attack_choice
 * gets this treatment — freeze isn't a damage source, so it isn't rate-limited
 * the same way; the separate "can't target the same player twice in a row"
 * rule in MatchEngine.useAttack/useFreeze covers repeat-target spam instead.)
 */
export function rollReward(player: PlayerState, currentQuestionCount: number, rng: () => number = Math.random): PendingReward {
  const roll = rng();
  let type: RewardType = roll < ATTACK_CHOICE_WEIGHT ? "attack_choice" : roll < ATTACK_CHOICE_WEIGHT + FREEZE_WEIGHT ? "freeze" : "bonus_move";
  if (type === "attack_choice" && player.lastRewardType === "attack_choice") {
    type = "bonus_move";
  }
  return {
    rewardId: `reward-${nextRewardSeq++}`,
    type,
    expiresAtQuestion: currentQuestionCount + REWARD_EXPIRY_QUESTIONS,
  };
}

/** Each player's reward expiry is evaluated against their own question count,
 * not a shared round — call this for a single player right after their
 * `questionsAnswered` count changes. */
export function expireStaleRewards(players: Iterable<PlayerState>, currentQuestionCount: number): void {
  for (const player of players) {
    if (player.pendingReward && player.pendingReward.expiresAtQuestion < currentQuestionCount) {
      player.pendingReward = null;
    }
  }
}

export interface ConsumeRewardResult {
  ok: boolean;
  error?: string;
}

export function consumeReward(player: PlayerState, rewardId: string): ConsumeRewardResult {
  if (!player.pendingReward || player.pendingReward.rewardId !== rewardId) {
    return { ok: false, error: "no_such_reward" };
  }
  player.lastRewardType = player.pendingReward.type;
  player.pendingReward = null;
  return { ok: true };
}
