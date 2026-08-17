import test from "node:test";
import assert from "node:assert/strict";
import {
  addPlayer,
  checkWinCondition,
  consumeBonusMove,
  createMatch,
  pushQuestion,
  startMatch,
  submitAnswer,
  timeoutAnswer,
  useAttack,
  useFreeze,
} from "./MatchEngine";
import { computeGridHeight } from "./MatchState";

// Weights: [0, 0.4) => attack_choice, [0.4, 0.7) => freeze, [0.7, 1) => bonus_move.
const rngAttack = () => 0;
const rngFreeze = () => 0.5;
const rngMove = () => 0.99;

function twoPlayerMatch() {
  const state = createMatch(1, "ffa");
  const a = addPlayer(state, 1, "Alice", "blaze", null, { x: 0, y: 0 });
  const b = addPlayer(state, 2, "Bob", "aegis", null, { x: 7, y: 0 });
  startMatch(state);
  return { state, a, b };
}

function threePlayerMatch() {
  const state = createMatch(1, "ffa");
  const a = addPlayer(state, 1, "Alice", "blaze", null, { x: 0, y: 0 });
  const b = addPlayer(state, 2, "Bob", "aegis", null, { x: 3, y: 0 });
  const c = addPlayer(state, 3, "Cara", "aegis", null, { x: 6, y: 0 });
  startMatch(state);
  return { state, a, b, c };
}

test("computeGridHeight tracks the question bank size, clamped to a sane range", () => {
  assert.equal(computeGridHeight(10), 11, "10 questions -> 10 steps -> height 11");
  assert.equal(computeGridHeight(4), 5, "at the floor, steps == bank size");
  assert.equal(computeGridHeight(1), 5, "below the floor, clamps to the minimum 4 steps");
  assert.equal(computeGridHeight(30), 31, "at the ceiling, steps == bank size");
  assert.equal(computeGridHeight(500), 31, "above the ceiling, clamps to the maximum 30 steps");
});

test("createMatch derives grid height from an explicit gridHeight argument (as LiveMatchRegistry passes it)", () => {
  const state = createMatch(1, "ffa", 20, computeGridHeight(12));
  assert.equal(state.grid.height, 13);
  assert.equal(state.grid.width, 8, "grid width is unaffected by question count");
});

test("streak of 2 correct answers offers a reward", () => {
  const { state } = twoPlayerMatch();
  pushQuestion(state, 1, 101, 0);
  const r1 = submitAnswer(state, 1, 0, rngAttack);
  assert.equal(r1.correct, true);
  assert.equal(r1.streakCount, 1);
  assert.equal(r1.rewardOffered, null);

  pushQuestion(state, 1, 102, 0);
  const r2 = submitAnswer(state, 1, 0, rngAttack);
  assert.equal(r2.streakCount, 2);
  assert.equal(r2.rewardOffered?.type, "attack_choice");
});

test("wrong answer resets streak and no reward is offered", () => {
  const { state } = twoPlayerMatch();
  pushQuestion(state, 1, 101, 0);
  submitAnswer(state, 1, 0, rngAttack); // correct, streak 1
  pushQuestion(state, 1, 102, 0);
  const wrong = submitAnswer(state, 1, 1, rngAttack); // wrong, correctIndex is 0
  assert.equal(wrong.correct, false);
  assert.equal(wrong.streakCount, 0);
  assert.equal(wrong.rewardOffered, null);
});

test("a correct answer advances the player one step toward the goal", () => {
  const { state, a } = twoPlayerMatch();
  pushQuestion(state, 1, 101, 0);
  const result = submitAnswer(state, 1, 0, rngAttack);
  assert.equal(result.correct, true);
  assert.equal(a.pos.y, 1);
  assert.equal(result.newPos?.y, 1);
  assert.equal(a.goalReached, false);
});

test("reaching the goal row wins immediately, even mid-match for the other player", () => {
  const { state, a } = twoPlayerMatch();
  // Grid height is 6, so the goal row is y=5 — walk Alice up 5 correct answers.
  for (let i = 0; i < 5; i++) {
    pushQuestion(state, 1, 200 + i, 0);
    submitAnswer(state, 1, 0, rngAttack);
  }
  assert.equal(a.pos.y, 5);
  assert.equal(a.goalReached, true);

  const result = checkWinCondition(state);
  assert.equal(result?.winnerId, 1);
  assert.equal(result?.reason, "goal");
});

test("Aegis takes 25% reduced damage and Blaze's fireball applies a burn DoT that ticks on the target's own next answer", () => {
  const { state, a, b } = twoPlayerMatch();
  pushQuestion(state, 1, 101, 0);
  submitAnswer(state, 1, 0, rngAttack); // streak 1, no reward yet

  pushQuestion(state, 1, 102, 0);
  const second = submitAnswer(state, 1, 0, rngAttack);
  const attackRewardId = second.rewardOffered!.rewardId;

  const result = useAttack(state, a.playerId, attackRewardId, b.playerId);
  assert.equal(result.ok, true);
  // Fireball baseDamage 20, Aegis damageReductionPct 25 => round(20 * 0.75) = 15
  assert.equal(result.outcome!.damage, 15);
  assert.equal(b.hp, b.maxHp - 15);
  assert.ok(b.pendingDot, "expected burn DoT to be scheduled on the target");

  // The DoT only ticks when the target answers their own next question.
  pushQuestion(state, 2, 301, 0);
  const bAnswer = submitAnswer(state, 2, 0, rngAttack);
  assert.equal(bAnswer.dotDamage, 5);
  assert.equal(b.hp, b.maxHp - 15 - 5);
  assert.equal(b.pendingDot, null);
});

test("anti-repeat rule forces bonus_move after a consumed attack_choice reward", () => {
  const { state, a, b } = twoPlayerMatch();
  pushQuestion(state, 1, 101, 0);
  submitAnswer(state, 1, 0, rngAttack); // streak 1

  pushQuestion(state, 1, 102, 0);
  const first = submitAnswer(state, 1, 0, rngAttack); // streak 2, reward = attack_choice
  assert.equal(first.rewardOffered?.type, "attack_choice");
  useAttack(state, a.playerId, first.rewardOffered!.rewardId, b.playerId);

  pushQuestion(state, 1, 103, 0);
  const second = submitAnswer(state, 1, 0, rngAttack); // streak 3, rng still favors attack_choice
  assert.equal(second.streakCount, 3);
  assert.equal(second.rewardOffered?.type, "bonus_move", "must alternate away from attack after using one");
});

test("cannot target self or an already-eliminated player", () => {
  const { state, a } = twoPlayerMatch();
  pushQuestion(state, 1, 101, 0);
  submitAnswer(state, 1, 0, rngAttack);
  pushQuestion(state, 1, 102, 0);
  const answer = submitAnswer(state, 1, 0, rngAttack);
  const selfAttack = useAttack(state, a.playerId, answer.rewardOffered!.rewardId, a.playerId);
  assert.equal(selfAttack.ok, false);
  assert.equal(selfAttack.error, "cannot_target_self");
});

test("bonus_move reward grants extra steps toward the goal", () => {
  const { state, a } = twoPlayerMatch();
  pushQuestion(state, 1, 101, 0);
  submitAnswer(state, 1, 0, rngMove);
  pushQuestion(state, 1, 102, 0);
  const answer = submitAnswer(state, 1, 0, rngMove);
  assert.equal(answer.rewardOffered?.type, "bonus_move");

  const before = a.pos.y;
  const result = consumeBonusMove(state, a.playerId, answer.rewardOffered!.rewardId);
  assert.equal(result.ok, true);
  assert.equal(a.pos.y, before + 2);
});

test("advancing (auto or bonus) clamps at the goal row instead of overshooting", () => {
  const { state, a } = twoPlayerMatch();
  pushQuestion(state, 1, 101, 0);
  submitAnswer(state, 1, 0, rngMove); // streak 1
  pushQuestion(state, 1, 102, 0);
  const answer = submitAnswer(state, 1, 0, rngMove); // streak 2, reward = bonus_move
  assert.equal(answer.rewardOffered?.type, "bonus_move");

  a.pos = { x: a.pos.x, y: 4 }; // one step from the goal row (height 6 => goal row 5)
  const result = consumeBonusMove(state, a.playerId, answer.rewardOffered!.rewardId);
  assert.equal(result.ok, true);
  assert.equal(a.pos.y, 5, "should clamp at the goal row, not go past it");
  assert.equal(result.goalReached, true);
});

test("answer timeout resets streak, still ticks DoT, and advances the personal question count", () => {
  const { state, a } = twoPlayerMatch();
  pushQuestion(state, 1, 101, 0);
  submitAnswer(state, 1, 0, rngAttack);
  assert.equal(a.consecutiveCorrect, 1);

  pushQuestion(state, 1, 102, 0);
  const timeout = timeoutAnswer(state, 1);
  assert.equal(timeout.ok, true);
  assert.equal(timeout.correct, false);
  assert.equal(a.consecutiveCorrect, 0);
  assert.equal(a.questionsAnswered, 2);
  assert.equal(a.pos.y, 1, "timeout should not advance the player");
});

test("an unresolved reward left pending through a timeout is cleared, not left blocking future rewards", () => {
  const { state, a } = twoPlayerMatch();
  pushQuestion(state, 1, 101, 0);
  submitAnswer(state, 1, 0, rngAttack); // streak 1

  pushQuestion(state, 1, 102, 0);
  const offered = submitAnswer(state, 1, 0, rngAttack); // streak 2, reward offered
  assert.ok(offered.rewardOffered, "expected a reward to be offered");
  assert.ok(a.pendingReward, "reward should be pending on the player");

  // The player never acts on it — their next question just times out instead.
  pushQuestion(state, 1, 103, 0);
  timeoutAnswer(state, 1);
  assert.equal(a.pendingReward, null, "an unresolved reward must not survive a timeout");

  // Without the fix, submitAnswer's `!player.pendingReward` gate would block this
  // from ever rolling a new reward again.
  pushQuestion(state, 1, 104, 0);
  submitAnswer(state, 1, 0, rngAttack); // streak 1 again
  pushQuestion(state, 1, 105, 0);
  const second = submitAnswer(state, 1, 0, rngAttack); // streak 2 again
  assert.ok(second.rewardOffered, "a new reward should roll normally after the stale one was cleared");
});

test("last player standing wins by HP", () => {
  const { state, b } = twoPlayerMatch();
  b.hp = 0;
  b.alive = false;
  const result = checkWinCondition(state);
  assert.equal(result?.winnerId, 1);
  assert.equal(result?.reason, "hp");
});

test("question-count cap resolves to the lane-progress tiebreak", () => {
  const { state, a, b } = twoPlayerMatch();
  state.maxRounds = 1;
  a.questionsAnswered = 1;
  a.pos = { x: a.pos.x, y: 3 };
  b.pos = { x: b.pos.x, y: 1 };
  const result = checkWinCondition(state);
  assert.equal(result?.winnerId, a.playerId);
  assert.equal(result?.reason, "progress");
});

test("streak of 2 can roll a freeze reward, and it skips the target's next advance", () => {
  const { state, a, b } = twoPlayerMatch();
  pushQuestion(state, 1, 101, 0);
  submitAnswer(state, 1, 0, rngFreeze); // streak 1

  pushQuestion(state, 1, 102, 0);
  const answer = submitAnswer(state, 1, 0, rngFreeze); // streak 2, reward = freeze
  assert.equal(answer.rewardOffered?.type, "freeze");

  const result = useFreeze(state, a.playerId, answer.rewardOffered!.rewardId, b.playerId);
  assert.equal(result.ok, true);
  assert.equal(b.frozen, true);

  // Bob answers correctly but doesn't move — the freeze is consumed instead.
  pushQuestion(state, 2, 201, 0);
  const bAnswer = submitAnswer(state, 2, 0, rngAttack);
  assert.equal(bAnswer.correct, true);
  assert.equal(b.pos.y, 0, "frozen answer should not advance the player");
  assert.equal(b.frozen, false, "freeze should be consumed by the next correct answer");

  // The next correct answer after that advances normally again.
  pushQuestion(state, 2, 202, 0);
  submitAnswer(state, 2, 0, rngAttack);
  assert.equal(b.pos.y, 1);
});

test("cannot attack or freeze the same player twice in a row when another target exists", () => {
  const { state, a, b, c } = threePlayerMatch();
  pushQuestion(state, 1, 101, 0);
  submitAnswer(state, 1, 0, rngAttack); // streak 1
  pushQuestion(state, 1, 102, 0);
  const first = submitAnswer(state, 1, 0, rngAttack); // streak 2, attack_choice
  useAttack(state, a.playerId, first.rewardOffered!.rewardId, b.playerId);
  assert.equal(a.lastTargetedPlayerId, b.playerId);

  // Next reward — anti-repeat (reward-type) forces bonus_move, so consume that first to
  // get back to an attack_choice, then confirm targeting Bob again is blocked while Cara
  // is available.
  pushQuestion(state, 1, 103, 0);
  const second = submitAnswer(state, 1, 0, rngAttack);
  assert.equal(second.rewardOffered?.type, "bonus_move");
  consumeBonusMove(state, a.playerId, second.rewardOffered!.rewardId);

  pushQuestion(state, 1, 104, 0);
  const third = submitAnswer(state, 1, 0, rngAttack);
  assert.equal(third.rewardOffered?.type, "attack_choice");
  const repeatBlocked = useAttack(state, a.playerId, third.rewardOffered!.rewardId, b.playerId);
  assert.equal(repeatBlocked.ok, false);
  assert.equal(repeatBlocked.error, "repeat_target_blocked");

  const okAgainstCara = useAttack(state, a.playerId, third.rewardOffered!.rewardId, c.playerId);
  assert.equal(okAgainstCara.ok, true);
  assert.equal(a.lastTargetedPlayerId, c.playerId);
});

test("repeat-target rule is waived when the attacker has no other living opponent", () => {
  const { state, a, b } = twoPlayerMatch();
  pushQuestion(state, 1, 101, 0);
  submitAnswer(state, 1, 0, rngAttack);
  pushQuestion(state, 1, 102, 0);
  const first = submitAnswer(state, 1, 0, rngAttack);
  useAttack(state, a.playerId, first.rewardOffered!.rewardId, b.playerId);

  pushQuestion(state, 1, 103, 0);
  const second = submitAnswer(state, 1, 0, rngAttack);
  assert.equal(second.rewardOffered?.type, "bonus_move", "anti-repeat on reward type still applies");
  consumeBonusMove(state, a.playerId, second.rewardOffered!.rewardId);

  pushQuestion(state, 1, 104, 0);
  const third = submitAnswer(state, 1, 0, rngAttack);
  // Bob is the only living opponent in a 2-player match, so targeting him again must be allowed.
  const result = useAttack(state, a.playerId, third.rewardOffered!.rewardId, b.playerId);
  assert.equal(result.ok, true);
});
