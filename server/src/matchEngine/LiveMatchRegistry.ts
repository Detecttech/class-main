import { findMatchById, appendMatchEvent, setMatchStatus, setMatchWinner, upsertParticipant } from "../db/repositories/matchRepo";
import { listQuestionsByBank, type Question } from "../db/repositories/questionRepo";
import { addXp, findStudentProfileById, listUnlockedCharacterIds, unlockCharacter } from "../db/repositories/studentRepo";
import { computeMatchXp, newlyUnlockedCharacters } from "../xp/xpRules";
import { getCharacter } from "./characters/characterConfig";
import * as Engine from "./MatchEngine";
import { computeGridHeight, type GridPos, type MatchState, type PlayerState } from "./MatchState";

const QUESTION_TIME_LIMIT_MS = 15_000;

export interface LobbyEntry {
  playerId: number;
  connId: number;
  name: string;
  characterId: string | null;
  team: string | null;
  ready: boolean;
}

export interface LiveMatch {
  matchId: number;
  state: MatchState;
  questions: Question[];
  lobby: Map<number, LobbyEntry>;
  connToPlayer: Map<number, number>;
  spectatorConnIds: Set<number>;
  // Each player answers on their own pace — a shared match-wide round timer
  // no longer makes sense, so every player gets their own timeout.
  playerTimers: Map<number, ReturnType<typeof setTimeout>>;
  // Every player gets the *entire* question bank shuffled into their own random order
  // at match start, rather than a fresh random draw each time — that way nobody sees a
  // question repeat until they've been asked every question in the bank once.
  // Reshuffled (fresh random order, all questions again) once a player exhausts it.
  playerQuestionOrders: Map<number, Question[]>;
  playerQuestionCursors: Map<number, number>;
  eventSeq: number;
}

function shuffled<T>(items: T[]): T[] {
  const arr = [...items];
  for (let i = arr.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [arr[i], arr[j]] = [arr[j], arr[i]];
  }
  return arr;
}

const liveMatches = new Map<number, LiveMatch>();

export type Emit = (event: { type: string; payload?: unknown }, target: "broadcast" | "spectators" | number) => void;

export function getLiveMatch(matchId: number): LiveMatch | undefined {
  return liveMatches.get(matchId);
}

export function getOrCreateLiveMatch(matchId: number): LiveMatch {
  const existing = liveMatches.get(matchId);
  if (existing) return existing;

  const match = findMatchById(matchId);
  if (!match) throw new Error("match_not_found");

  const questions = listQuestionsByBank(match.question_bank_id);
  // Steps to win track the class's own question bank size (see computeGridHeight) —
  // maxRounds (the forced progress-tiebreak cap) must stay comfortably above that or a
  // larger bank could hit the cap before anyone's even able to reach the goal.
  const gridHeight = computeGridHeight(questions.length);
  const maxRounds = Math.max(20, (gridHeight - 1) * 2);

  const live: LiveMatch = {
    matchId,
    state: Engine.createMatch(matchId, match.mode, maxRounds, gridHeight),
    questions,
    lobby: new Map(),
    connToPlayer: new Map(),
    spectatorConnIds: new Set(),
    playerTimers: new Map(),
    playerQuestionOrders: new Map(),
    playerQuestionCursors: new Map(),
    eventSeq: 1,
  };
  liveMatches.set(matchId, live);
  return live;
}

function log(live: LiveMatch, type: string, payload: unknown) {
  appendMatchEvent(live.matchId, live.eventSeq++, type, payload);
}

/** Spreads players evenly across the bottom row, one lane each — everyone
 * races straight up their own column toward the goal row. */
function startPositions(count: number, gridWidth: number): GridPos[] {
  const positions: GridPos[] = [];
  for (let i = 0; i < count; i++) {
    positions.push({ x: Math.floor((i + 0.5) * (gridWidth / count)), y: 0 });
  }
  return positions;
}

export function joinLobby(live: LiveMatch, playerId: number, connId: number, name: string): LobbyEntry {
  live.connToPlayer.set(connId, playerId);
  const existing = live.lobby.get(playerId);
  if (existing) {
    existing.connId = connId;
    return existing;
  }
  const entry: LobbyEntry = { playerId, connId, name, characterId: null, team: null, ready: false };
  live.lobby.set(playerId, entry);
  return entry;
}

export interface SelectCharacterResult {
  ok: boolean;
  error?: string;
}

// Character picks are not exclusive — with only 4 characters defined and lobbies now
// supporting up to 8 players, requiring a unique pick per player would permanently
// lock out half the lobby. Multiple players can play as the same character.
export function selectCharacter(live: LiveMatch, playerId: number, characterId: string): SelectCharacterResult {
  const entry = live.lobby.get(playerId);
  if (!entry) return { ok: false, error: "not_in_lobby" };
  try {
    getCharacter(characterId);
  } catch {
    return { ok: false, error: "unknown_character" };
  }
  entry.characterId = characterId;
  return { ok: true };
}

const VALID_TEAMS = ["A", "B"];

export function selectTeam(live: LiveMatch, playerId: number, team: string): SelectCharacterResult {
  if (live.state.mode !== "teams") return { ok: false, error: "not_a_team_match" };
  const entry = live.lobby.get(playerId);
  if (!entry) return { ok: false, error: "not_in_lobby" };
  if (!VALID_TEAMS.includes(team)) return { ok: false, error: "invalid_team" };
  entry.team = team;
  return { ok: true };
}

export function setReady(live: LiveMatch, playerId: number, ready: boolean): SelectCharacterResult {
  const entry = live.lobby.get(playerId);
  if (!entry) return { ok: false, error: "not_in_lobby" };
  if (ready && !entry.characterId) return { ok: false, error: "no_character_selected" };
  if (ready && live.state.mode === "teams" && !entry.team) return { ok: false, error: "no_team_selected" };
  entry.ready = ready;
  return { ok: true };
}

export function lobbySnapshot(live: LiveMatch) {
  return {
    matchId: live.matchId,
    mode: live.state.mode,
    players: [...live.lobby.values()].map((e) => ({
      playerId: e.playerId,
      name: e.name,
      characterId: e.characterId,
      team: e.team,
      ready: e.ready,
    })),
  };
}

export interface StartMatchResult {
  ok: boolean;
  error?: string;
}

export function startMatchFlow(live: LiveMatch, emit: Emit): StartMatchResult {
  const entries = [...live.lobby.values()];
  const readyPlayers = entries.filter((e) => e.ready && e.characterId);
  if (readyPlayers.length < 2) return { ok: false, error: "not_enough_players" };
  if (live.questions.length === 0) return { ok: false, error: "question_bank_empty" };

  const positions = startPositions(readyPlayers.length, live.state.grid.width);
  readyPlayers.forEach((entry, i) => {
    Engine.addPlayer(live.state, entry.playerId, entry.name, entry.characterId!, entry.team, positions[i]);
    live.playerQuestionOrders.set(entry.playerId, shuffled(live.questions));
    live.playerQuestionCursors.set(entry.playerId, 0);
  });

  Engine.startMatch(live.state);
  setMatchStatus(live.matchId, "active");
  log(live, "match_start", lobbySnapshot(live));

  emit(
    {
      type: "match_start",
      payload: {
        arenaLayout: { grid: live.state.grid, goalRow: live.state.grid.height - 1 },
        players: [...live.state.players.values()].map(publicPlayer),
        teams: live.state.mode === "teams",
      },
    },
    "broadcast"
  );

  for (const player of live.state.players.values()) {
    pushQuestionToPlayer(live, player.playerId, emit);
  }
  return { ok: true };
}

function publicPlayer(p: PlayerState) {
  return { playerId: p.playerId, name: p.name, characterId: p.characterId, team: p.team, hp: p.hp, maxHp: p.maxHp, pos: p.pos, alive: p.alive };
}

/** Pushes this player the next question from their own shuffled order (assigned once,
 * whole-bank, at match start in startMatchFlow) and (re)starts their personal timer.
 * Reshuffles a fresh full-bank order once they've been asked every question in it —
 * this only matters for very long matches with a small question bank. */
function pushQuestionToPlayer(live: LiveMatch, playerId: number, emit: Emit) {
  const player = live.state.players.get(playerId);
  if (!player || !player.alive) return;

  let order = live.playerQuestionOrders.get(playerId);
  let cursor = live.playerQuestionCursors.get(playerId) ?? 0;
  if (!order || cursor >= order.length) {
    order = shuffled(live.questions);
    cursor = 0;
    live.playerQuestionOrders.set(playerId, order);
  }
  const q = order[cursor];
  live.playerQuestionCursors.set(playerId, cursor + 1);
  Engine.pushQuestion(live.state, playerId, q.id, q.correct_index);

  emit(
    {
      type: "question_push",
      payload: {
        questionId: q.id,
        text: q.text,
        choices: [q.choice_0, q.choice_1, q.choice_2, q.choice_3],
        timeLimitMs: QUESTION_TIME_LIMIT_MS,
        questionNumber: player.questionsAnswered + 1,
      },
    },
    playerId
  );

  clearPlayerTimer(live, playerId);
  live.playerTimers.set(
    playerId,
    setTimeout(() => handleAnswerTimeout(live, playerId, emit), QUESTION_TIME_LIMIT_MS)
  );
}

function clearPlayerTimer(live: LiveMatch, playerId: number) {
  const existing = live.playerTimers.get(playerId);
  if (existing) {
    clearTimeout(existing);
    live.playerTimers.delete(playerId);
  }
}

function broadcastPlayerAdvanced(live: LiveMatch, playerId: number, emit: Emit) {
  const player = live.state.players.get(playerId);
  if (!player) return;
  emit(
    {
      type: "player_advanced",
      payload: {
        playerId: player.playerId,
        newGridPos: player.pos,
        hp: player.hp,
        maxHp: player.maxHp,
        alive: player.alive,
        streak: player.consecutiveCorrect,
        goalReached: player.goalReached,
        frozen: player.frozen,
      },
    },
    "broadcast"
  );
  if (!player.alive) {
    emit({ type: "player_eliminated", payload: { playerId } }, "broadcast");
  }
}

// A player's personal timer/setTimeout callback runs outside any request/response
// cycle — an uncaught throw here would crash the whole Node process, taking down
// every other concurrent match with it. Catch, log, and fail just this one match.
function handleAnswerTimeout(live: LiveMatch, playerId: number, emit: Emit) {
  try {
    live.playerTimers.delete(playerId);
    const result = Engine.timeoutAnswer(live.state, playerId);
    if (!result.ok) return; // already answered via a race with this timer; nothing to do

    log(live, "answer_timeout", { playerId });
    broadcastPlayerAdvanced(live, playerId, emit);

    if (result.result) {
      endMatch(live, emit, result.result);
      return;
    }
    pushQuestionToPlayer(live, playerId, emit);
  } catch (err) {
    console.error(`[matchEngine] handleAnswerTimeout failed for match ${live.matchId}, player ${playerId}:`, err);
    emit({ type: "error", payload: { code: "internal_error", message: "The match hit an unexpected error and had to end." } }, "broadcast");
    setMatchStatus(live.matchId, "completed");
    liveMatches.delete(live.matchId);
  }
}

function endMatch(live: LiveMatch, emit: Emit, result: NonNullable<MatchState["result"]>) {
  for (const timer of live.playerTimers.values()) clearTimeout(timer);
  live.playerTimers.clear();

  setMatchStatus(live.matchId, "completed");
  setMatchWinner(live.matchId, String(result.winnerId ?? "none"));

  const standings = [...live.state.players.values()]
    .sort(
      (a, b) =>
        Number(b.alive) - Number(a.alive) ||
        Number(b.goalReached) - Number(a.goalReached) ||
        b.pos.y - a.pos.y ||
        b.hp - a.hp
    )
    .map((p, i) => ({ playerId: p.playerId, placement: i + 1, hp: p.hp, alive: p.alive }));

  for (const p of live.state.players.values()) {
    const won = live.state.mode === "teams" ? result.winnerId === p.team : result.winnerId === p.playerId;
    const xp = computeMatchXp({
      totalCorrectAnswers: p.totalCorrectAnswers,
      maxStreak: p.maxStreak,
      won,
      mode: live.state.mode,
    });

    // Only persist participant/XP rows for real, authenticated student profiles —
    // anonymous dev/test connections (playerId falls back to the raw ws connection id)
    // have no matching student_profiles row, and match_participants.student_profile_id
    // is a foreign key: writing it for a fake id throws and, uncaught, would crash the
    // whole server (see endMatch's caller — this runs inside a setTimeout).
    const profile = findStudentProfileById(p.playerId);
    if (!profile) continue;

    upsertParticipant({
      match_id: live.matchId,
      student_profile_id: p.playerId,
      character_id: p.characterId,
      team: p.team,
      final_hp: p.hp,
      final_placement: standings.find((s) => s.playerId === p.playerId)?.placement ?? null,
      xp_awarded: xp.total,
    });

    const updated = addXp(p.playerId, xp.total);
    const alreadyUnlocked = new Set(listUnlockedCharacterIds(p.playerId));
    const newUnlocks = newlyUnlockedCharacters(updated.xp_total, alreadyUnlocked);
    for (const characterId of newUnlocks) unlockCharacter(p.playerId, characterId);

    emit(
      { type: "xp_award", payload: { xpGained: xp.total, newTotalXp: updated.xp_total, newUnlocks } },
      p.playerId
    );
  }

  log(live, "match_end", { result, standings });
  emit({ type: "match_end", payload: { winnerId: result.winnerId, reason: result.reason, standings } }, "broadcast");
  liveMatches.delete(live.matchId);
}

export function handleSubmitAnswer(live: LiveMatch, playerId: number, choiceIndex: number, emit: Emit) {
  const result = Engine.submitAnswer(live.state, playerId, choiceIndex);
  if (!result.ok) {
    emit({ type: "answer_result", payload: { error: result.error } }, playerId);
    return result;
  }

  clearPlayerTimer(live, playerId);
  log(live, "submit_answer", { playerId, choiceIndex, correct: result.correct });
  emit(
    { type: "answer_result", payload: { ok: true, correct: result.correct, streakCount: result.streakCount, rewardOffered: result.rewardOffered } },
    playerId
  );
  broadcastPlayerAdvanced(live, playerId, emit);

  if (result.result) {
    endMatch(live, emit, result.result);
    return result;
  }
  pushQuestionToPlayer(live, playerId, emit);
  return result;
}

export function handleUseAttack(live: LiveMatch, playerId: number, rewardId: string, targetId: number, emit: Emit) {
  const result = Engine.useAttack(live.state, playerId, rewardId, targetId);
  if (result.ok) {
    log(live, "use_attack", { playerId, targetId, outcome: result.outcome });
    emit(
      {
        type: "attack_result",
        payload: {
          attackerId: playerId,
          targetId,
          damage: result.outcome!.damage,
          targetHpAfter: result.outcome!.targetHpAfter,
          vfxTag: result.outcome!.vfxTag,
          eliminated: result.outcome!.eliminated,
        },
      },
      "broadcast"
    );
    if (result.outcome!.eliminated) {
      emit({ type: "player_eliminated", payload: { playerId: targetId } }, "broadcast");
    }
    if (result.result) {
      endMatch(live, emit, result.result);
    }
  } else {
    emit({ type: "error", payload: { code: result.error, message: "use_attack rejected" } }, playerId);
  }
  return result;
}

export function handleUseFreeze(live: LiveMatch, playerId: number, rewardId: string, targetId: number, emit: Emit) {
  const result = Engine.useFreeze(live.state, playerId, rewardId, targetId);
  if (result.ok) {
    log(live, "use_freeze", { playerId, targetId });
    emit({ type: "freeze_result", payload: { casterId: playerId, targetId } }, "broadcast");
  } else {
    emit({ type: "error", payload: { code: result.error, message: "use_freeze rejected" } }, playerId);
  }
  return result;
}

export function handleWaiveReward(live: LiveMatch, playerId: number, rewardId: string, emit: Emit) {
  const result = Engine.waiveReward(live.state, playerId, rewardId);
  if (result.ok) {
    log(live, "waive_reward", { playerId });
  } else {
    emit({ type: "error", payload: { code: result.error, message: "reward_consumed rejected" } }, playerId);
  }
  return result;
}

export function handleConsumeBonusMove(live: LiveMatch, playerId: number, rewardId: string, emit: Emit) {
  const result = Engine.consumeBonusMove(live.state, playerId, rewardId);
  if (result.ok) {
    log(live, "bonus_move", { playerId, pos: result.newPos });
    broadcastPlayerAdvanced(live, playerId, emit);
    if (result.result) {
      endMatch(live, emit, result.result);
    }
  } else {
    emit({ type: "error", payload: { code: result.error, message: "reward_consumed rejected" } }, playerId);
  }
  return result;
}

export function addSpectator(live: LiveMatch, connId: number) {
  live.spectatorConnIds.add(connId);
}

export function removeConnection(live: LiveMatch, connId: number, emit?: Emit) {
  live.spectatorConnIds.delete(connId);
  const playerId = live.connToPlayer.get(connId);
  if (playerId === undefined) return;
  live.connToPlayer.delete(connId);

  // Only drop the LOBBY entry pre-match — a disconnect after the match is active needs
  // real reconnection handling (not built yet), so leave MatchEngine's player state as
  // the source of truth there. Without this, every reconnect during testing (e.g. a
  // Unity script recompile resetting the connection, which gets a fresh playerId since
  // there's no persistent auth binding it to the old one) leaves a stale "ghost" entry
  // in the lobby forever — inflating the count the teacher sees and confusing the
  // ready-players check in startMatchFlow.
  if (live.state.status === "lobby" && live.lobby.delete(playerId) && emit) {
    emit({ type: "lobby_state", payload: lobbySnapshot(live) }, "broadcast");
  }
}

export function liveDashboard(live: LiveMatch) {
  return {
    matchId: live.matchId,
    status: live.state.status,
    players: [...live.state.players.values()].map((p) => ({
      playerId: p.playerId,
      name: p.name,
      hp: p.hp,
      alive: p.alive,
      streak: p.consecutiveCorrect,
      pos: p.pos,
      goalReached: p.goalReached,
      questionsAnswered: p.questionsAnswered,
    })),
  };
}
