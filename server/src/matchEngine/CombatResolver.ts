import type { PlayerState } from "./MatchState";
import { getCharacter, type AbilityConfig } from "./characters/characterConfig";

export const BASIC_ATTACK: AbilityConfig = {
  id: "basic_strike",
  name: "Basic Strike",
  type: "active",
  description: "A simple melee strike available to characters without an active attack ability.",
  range: 1,
  targeting: "melee",
  baseDamage: 15,
  vfxTag: "vfx_basic_strike",
};

/** The attack a character uses when they consume an attack_choice reward. */
export function attackFor(characterId: string): AbilityConfig {
  const character = getCharacter(characterId);
  return character.ability.type === "active" ? character.ability : BASIC_ATTACK;
}

export interface AttackOutcome {
  damage: number;
  targetHpAfter: number;
  eliminated: boolean;
  healedAttacker: number;
  vfxTag: string;
}

export function resolveAttack(attacker: PlayerState, target: PlayerState, ability: AbilityConfig): AttackOutcome {
  let damage = ability.baseDamage ?? 0;

  const targetCharacter = getCharacter(target.characterId);
  if (targetCharacter.ability.type === "passive" && targetCharacter.ability.damageReductionPct) {
    damage = Math.round(damage * (1 - targetCharacter.ability.damageReductionPct / 100));
  }

  target.hp = Math.max(0, target.hp - damage);
  const eliminated = target.hp === 0;
  if (eliminated) target.alive = false;

  if (ability.dotDamage && ability.dotRounds && !eliminated) {
    target.pendingDot = { damage: ability.dotDamage, remainingRounds: ability.dotRounds };
  }

  let healedAttacker = 0;
  if (ability.lifestealPct) {
    healedAttacker = Math.round(damage * (ability.lifestealPct / 100));
    attacker.hp = Math.min(attacker.maxHp, attacker.hp + healedAttacker);
  }

  return { damage, targetHpAfter: target.hp, eliminated, healedAttacker, vfxTag: ability.vfxTag };
}

/** Applies any burn/DoT ticks queued on a player, e.g. from Blaze's Fireball. Returns damage dealt, if any. */
export function tickDot(player: PlayerState): number {
  if (!player.pendingDot) return 0;
  const dmg = player.pendingDot.damage;
  player.hp = Math.max(0, player.hp - dmg);
  if (player.hp === 0) player.alive = false;
  player.pendingDot.remainingRounds -= 1;
  if (player.pendingDot.remainingRounds <= 0) player.pendingDot = null;
  return dmg;
}
