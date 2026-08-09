export interface GridPos {
  x: number;
  y: number;
}

export type RewardType = "attack_choice" | "bonus_move";

export interface PendingReward {
  rewardId: string;
  type: RewardType;
  expiresAtQuestion: number; // relative to the owning player's own questionsAnswered count
}

export interface ActiveQuestion {
  questionId: number;
  correctIndex: number;
  questionNumber: number; // this player's own Nth question, not a match-wide round
}

export interface PlayerState {
  playerId: number;
  name: string;
  characterId: string;
  team: string | null;
  hp: number;
  maxHp: number;
  pos: GridPos;
  alive: boolean;
  consecutiveCorrect: number;
  lastRewardType: RewardType | null;
  pendingReward: PendingReward | null;
  pendingDot: { damage: number; remainingRounds: number } | null;
  totalCorrectAnswers: number;
  maxStreak: number;
  questionsAnswered: number; // drives reward expiry + the match-length forced-decision cap
  currentQuestion: ActiveQuestion | null; // independent per player, not shared
  goalReached: boolean;
}

export type WinReason = "hp" | "goal" | "progress";

export interface MatchResult {
  winnerId: number | string | null; // playerId (FFA) or team name (teams)
  reason: WinReason;
}

export interface MatchState {
  matchId: number;
  mode: "ffa" | "teams";
  status: "lobby" | "active" | "completed";
  maxRounds: number; // max questions any one player answers before a forced progress tiebreak
  grid: { width: number; height: number };
  players: Map<number, PlayerState>;
  result: MatchResult | null;
}

export const DEFAULT_GRID = { width: 8, height: 6 };
export const REWARD_EXPIRY_QUESTIONS = 4;
export const DEFAULT_BONUS_MOVE_STEPS = 2;

export function createMatchState(
  matchId: number,
  mode: "ffa" | "teams",
  maxRounds = 20
): MatchState {
  return {
    matchId,
    mode,
    status: "lobby",
    maxRounds,
    grid: DEFAULT_GRID,
    players: new Map(),
    result: null,
  };
}
