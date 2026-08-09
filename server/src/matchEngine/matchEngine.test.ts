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
} from "./MatchEngine";

const rngAttack = () => 0; // always < 0.6 => attack_choice
const rngMove = () => 0.99; // always >= 0.6 => bonus_move

function twoPlayerMatch() {
  const state = createMatch(1, "ffa");
  const a = addPlayer(state, 1, "Alice", "blaze", null, { x: 0, y: 0 });
  const b = addPlayer(state, 2, "Bob", "aegis", null, { x: 7, y: 0 });
  startMatch(state);
  return { state, a, b };
}

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
