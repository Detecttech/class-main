import { CHARACTERS } from "../matchEngine/characters/characterConfig";

export const XP_PER_CORRECT_ANSWER = 10;
export const XP_STREAK_MILESTONE_BONUS = 25;
export const STREAK_MILESTONES = [3, 5];
export const XP_MATCH_WIN_FFA = 100;
export const XP_MATCH_WIN_TEAM = 75;
export const XP_PARTICIPATION = 20;

export interface XpBreakdown {
  fromAnswers: number;
  fromMilestones: number;
  fromResult: number;
  total: number;
}

export function computeMatchXp(opts: {
  totalCorrectAnswers: number;
  maxStreak: number;
  won: boolean;
  mode: "ffa" | "teams";
}): XpBreakdown {
  const fromAnswers = opts.totalCorrectAnswers * XP_PER_CORRECT_ANSWER;
  const fromMilestones = STREAK_MILESTONES.filter((t) => opts.maxStreak >= t).length * XP_STREAK_MILESTONE_BONUS;
  const fromResult = opts.won ? (opts.mode === "teams" ? XP_MATCH_WIN_TEAM : XP_MATCH_WIN_FFA) : XP_PARTICIPATION;
  return { fromAnswers, fromMilestones, fromResult, total: fromAnswers + fromMilestones + fromResult };
}

/** Characters newly unlocked by crossing an XP threshold, given the student's updated total. */
export function newlyUnlockedCharacters(newXpTotal: number, alreadyUnlocked: Set<string>): string[] {
  return CHARACTERS.filter(
    (c) => !c.unlock.defaultUnlocked && !alreadyUnlocked.has(c.id) && c.unlock.xpThreshold !== undefined && newXpTotal >= c.unlock.xpThreshold
  ).map((c) => c.id);
}
