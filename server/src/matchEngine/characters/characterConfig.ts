import fs from "node:fs";
import path from "node:path";

export type AbilityType = "active" | "passive";

export interface AbilityConfig {
  id: string;
  name: string;
  type: AbilityType;
  description: string;
  range?: number;
  targeting?: "ranged" | "melee";
  baseDamage?: number;
  dotDamage?: number;
  dotRounds?: number;
  damageReductionPct?: number;
  bonusMoveSteps?: number;
  lifestealPct?: number;
  vfxTag: string;
}

export interface CharacterConfig {
  id: string;
  name: string;
  baseStats: { hp: number; moveRange: number };
  ability: AbilityConfig;
  unlock: { defaultUnlocked: boolean; xpThreshold?: number };
}

const raw = fs.readFileSync(path.join(__dirname, "..", "..", "..", "..", "shared", "characters.json"), "utf-8");
const parsed = JSON.parse(raw) as { characters: CharacterConfig[] };

export const CHARACTERS: readonly CharacterConfig[] = parsed.characters;

const byId = new Map(CHARACTERS.map((c) => [c.id, c]));

export function getCharacter(id: string): CharacterConfig {
  const c = byId.get(id);
  if (!c) throw new Error(`Unknown character id: ${id}`);
  return c;
}

export function defaultUnlockedCharacterIds(): string[] {
  return CHARACTERS.filter((c) => c.unlock.defaultUnlocked).map((c) => c.id);
}
