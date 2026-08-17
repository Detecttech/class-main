using NUnit.Framework;
using QuizBattle.Characters;
using QuizBattle.GameState.MockEngine;

namespace QuizBattle.Tests.EditMode
{
    /// Mirrors server/src/matchEngine/matchEngine.test.ts — same scenarios, same
    /// expected numbers, so the two engines are verified to agree.
    public class MatchEngineTests
    {
        // Weights: [0, 0.4) => attack_choice, [0.4, 0.7) => freeze, [0.7, 1) => bonus_move.
        private static double RngAttack() => 0.0;
        private static double RngFreeze() => 0.5;
        private static double RngMove() => 0.99;

        [SetUp]
        public void SetUp()
        {
            CharacterCatalog.Clear();
            CharacterCatalog.Register(new CharacterConfig
            {
                characterId = "blaze", maxHp = 60, abilityId = "fireball", abilityType = AbilityType.Active,
                targeting = AbilityTargeting.Ranged, range = 3, baseDamage = 20, dotDamage = 5, dotRounds = 1,
                vfxTag = "vfx_fireball",
            });
            CharacterCatalog.Register(new CharacterConfig
            {
                characterId = "aegis", maxHp = 70, abilityId = "bulwark", abilityType = AbilityType.Passive,
                damageReductionPct = 25, vfxTag = "vfx_shield_shimmer",
            });
        }

        private (MatchState state, PlayerState a, PlayerState b) TwoPlayerMatch()
        {
            var state = MatchEngine.CreateMatch(1, MatchMode.Ffa);
            var a = MatchEngine.AddPlayer(state, 1, "Alice", "blaze", null, new GridPos(0, 0));
            var b = MatchEngine.AddPlayer(state, 2, "Bob", "aegis", null, new GridPos(7, 0));
            MatchEngine.StartMatch(state);
            return (state, a, b);
        }

        private (MatchState state, PlayerState a, PlayerState b, PlayerState c) ThreePlayerMatch()
        {
            var state = MatchEngine.CreateMatch(1, MatchMode.Ffa);
            var a = MatchEngine.AddPlayer(state, 1, "Alice", "blaze", null, new GridPos(0, 0));
            var b = MatchEngine.AddPlayer(state, 2, "Bob", "aegis", null, new GridPos(3, 0));
            var c = MatchEngine.AddPlayer(state, 3, "Cara", "aegis", null, new GridPos(6, 0));
            MatchEngine.StartMatch(state);
            return (state, a, b, c);
        }

        [Test]
        public void StreakOfTwoCorrectAnswersOffersReward()
        {
            var (state, _, _) = TwoPlayerMatch();
            MatchEngine.PushQuestion(state, 1, 101, 0);
            var r1 = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            Assert.IsTrue(r1.correct);
            Assert.AreEqual(1, r1.streakCount);
            Assert.IsNull(r1.rewardOffered);

            MatchEngine.PushQuestion(state, 1, 102, 0);
            var r2 = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            Assert.AreEqual(2, r2.streakCount);
            Assert.AreEqual(RewardType.AttackChoice, r2.rewardOffered.type);
        }

        [Test]
        public void WrongAnswerResetsStreakAndOffersNoReward()
        {
            var (state, _, _) = TwoPlayerMatch();
            MatchEngine.PushQuestion(state, 1, 101, 0);
            MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            MatchEngine.PushQuestion(state, 1, 102, 0);
            var wrong = MatchEngine.SubmitAnswer(state, 1, 1, RngAttack);
            Assert.IsFalse(wrong.correct);
            Assert.AreEqual(0, wrong.streakCount);
            Assert.IsNull(wrong.rewardOffered);
        }

        [Test]
        public void CorrectAnswerAdvancesOneStepTowardGoal()
        {
            var (state, a, _) = TwoPlayerMatch();
            MatchEngine.PushQuestion(state, 1, 101, 0);
            var result = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            Assert.IsTrue(result.correct);
            Assert.AreEqual(1, a.pos.y);
            Assert.AreEqual(1, result.newPos.y);
            Assert.IsFalse(a.goalReached);
        }

        [Test]
        public void ReachingGoalRowWinsImmediately()
        {
            var (state, a, _) = TwoPlayerMatch();
            // Grid height is 6, so the goal row is y=5 — walk Alice up 5 correct answers.
            for (int i = 0; i < 5; i++)
            {
                MatchEngine.PushQuestion(state, 1, 200 + i, 0);
                MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            }
            Assert.AreEqual(5, a.pos.y);
            Assert.IsTrue(a.goalReached);

            var result = MatchEngine.CheckWinCondition(state);
            Assert.AreEqual(1, result.winnerId);
            Assert.AreEqual(WinReason.Goal, result.reason);
        }

        [Test]
        public void AegisReducesDamageAndBlazeAppliesBurnDotThatTicksOnTargetsOwnNextAnswer()
        {
            var (state, a, b) = TwoPlayerMatch();
            MatchEngine.PushQuestion(state, 1, 101, 0);
            MatchEngine.SubmitAnswer(state, 1, 0, RngAttack); // streak 1, no reward yet

            MatchEngine.PushQuestion(state, 1, 102, 0);
            var second = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            string attackRewardId = second.rewardOffered.rewardId;

            var result = MatchEngine.UseAttack(state, a.playerId, attackRewardId, b.playerId);
            Assert.IsTrue(result.ok);
            Assert.AreEqual(15, result.outcome.damage); // 20 * 0.75
            Assert.AreEqual(b.maxHp - 15, b.hp);
            Assert.IsNotNull(b.pendingDot);

            // The DoT only ticks when the target answers their own next question.
            MatchEngine.PushQuestion(state, 2, 301, 0);
            var bAnswer = MatchEngine.SubmitAnswer(state, 2, 0, RngAttack);
            Assert.AreEqual(5, bAnswer.dotDamage);
            Assert.AreEqual(b.maxHp - 15 - 5, b.hp);
            Assert.IsNull(b.pendingDot);
        }

        [Test]
        public void AntiRepeatForcesBonusMoveAfterConsumedAttack()
        {
            var (state, a, b) = TwoPlayerMatch();
            MatchEngine.PushQuestion(state, 1, 101, 0);
            MatchEngine.SubmitAnswer(state, 1, 0, RngAttack); // streak 1

            MatchEngine.PushQuestion(state, 1, 102, 0);
            var first = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack); // streak 2, reward = attack_choice
            Assert.AreEqual(RewardType.AttackChoice, first.rewardOffered.type);
            MatchEngine.UseAttack(state, a.playerId, first.rewardOffered.rewardId, b.playerId);

            MatchEngine.PushQuestion(state, 1, 103, 0);
            var second = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack); // streak 3, rng still favors attack_choice
            Assert.AreEqual(3, second.streakCount);
            Assert.AreEqual(RewardType.BonusMove, second.rewardOffered.type);
        }

        [Test]
        public void CannotTargetSelf()
        {
            var (state, a, _) = TwoPlayerMatch();
            MatchEngine.PushQuestion(state, 1, 101, 0);
            MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            MatchEngine.PushQuestion(state, 1, 102, 0);
            var answer = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            var selfAttack = MatchEngine.UseAttack(state, a.playerId, answer.rewardOffered.rewardId, a.playerId);
            Assert.IsFalse(selfAttack.ok);
            Assert.AreEqual("cannot_target_self", selfAttack.error);
        }

        [Test]
        public void BonusMoveGrantsExtraStepsTowardGoal()
        {
            var (state, a, _) = TwoPlayerMatch();
            MatchEngine.PushQuestion(state, 1, 101, 0);
            MatchEngine.SubmitAnswer(state, 1, 0, RngMove);
            MatchEngine.PushQuestion(state, 1, 102, 0);
            var answer = MatchEngine.SubmitAnswer(state, 1, 0, RngMove);
            Assert.AreEqual(RewardType.BonusMove, answer.rewardOffered.type);

            int before = a.pos.y;
            var result = MatchEngine.ConsumeBonusMove(state, a.playerId, answer.rewardOffered.rewardId);
            Assert.IsTrue(result.ok);
            Assert.AreEqual(before + 2, a.pos.y);
        }

        [Test]
        public void AdvancingClampsAtGoalRowInsteadOfOvershooting()
        {
            var (state, a, _) = TwoPlayerMatch();
            MatchEngine.PushQuestion(state, 1, 101, 0);
            MatchEngine.SubmitAnswer(state, 1, 0, RngMove); // streak 1
            MatchEngine.PushQuestion(state, 1, 102, 0);
            var answer = MatchEngine.SubmitAnswer(state, 1, 0, RngMove); // streak 2, reward = bonus_move
            Assert.AreEqual(RewardType.BonusMove, answer.rewardOffered.type);

            a.pos = new GridPos(a.pos.x, 4); // one step from the goal row (height 6 => goal row 5)
            var result = MatchEngine.ConsumeBonusMove(state, a.playerId, answer.rewardOffered.rewardId);
            Assert.IsTrue(result.ok);
            Assert.AreEqual(5, a.pos.y, "should clamp at the goal row, not go past it");
            Assert.IsTrue(result.goalReached);
        }

        [Test]
        public void AnswerTimeoutResetsStreakStillTicksDotAndAdvancesQuestionCount()
        {
            var (state, a, _) = TwoPlayerMatch();
            MatchEngine.PushQuestion(state, 1, 101, 0);
            MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            Assert.AreEqual(1, a.consecutiveCorrect);

            MatchEngine.PushQuestion(state, 1, 102, 0);
            var timeout = MatchEngine.TimeoutAnswer(state, 1);
            Assert.IsTrue(timeout.ok);
            Assert.IsFalse(timeout.correct);
            Assert.AreEqual(0, a.consecutiveCorrect);
            Assert.AreEqual(2, a.questionsAnswered);
            Assert.AreEqual(1, a.pos.y, "timeout should not advance the player");
        }

        [Test]
        public void UnresolvedRewardLeftPendingThroughTimeoutIsClearedNotLeftBlockingFutureRewards()
        {
            var (state, a, _) = TwoPlayerMatch();
            MatchEngine.PushQuestion(state, 1, 101, 0);
            MatchEngine.SubmitAnswer(state, 1, 0, RngAttack); // streak 1

            MatchEngine.PushQuestion(state, 1, 102, 0);
            var offered = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack); // streak 2, reward offered
            Assert.IsNotNull(offered.rewardOffered, "expected a reward to be offered");
            Assert.IsNotNull(a.pendingReward, "reward should be pending on the player");

            // The player never acts on it — their next question just times out instead.
            MatchEngine.PushQuestion(state, 1, 103, 0);
            MatchEngine.TimeoutAnswer(state, 1);
            Assert.IsNull(a.pendingReward, "an unresolved reward must not survive a timeout");

            // Without the fix, SubmitAnswer's pendingReward gate would block this from
            // ever rolling a new reward again.
            MatchEngine.PushQuestion(state, 1, 104, 0);
            MatchEngine.SubmitAnswer(state, 1, 0, RngAttack); // streak 1 again
            MatchEngine.PushQuestion(state, 1, 105, 0);
            var second = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack); // streak 2 again
            Assert.IsNotNull(second.rewardOffered, "a new reward should roll normally after the stale one was cleared");
        }

        [Test]
        public void LastPlayerStandingWinsByHp()
        {
            var (state, _, b) = TwoPlayerMatch();
            b.hp = 0;
            b.alive = false;
            var result = MatchEngine.CheckWinCondition(state);
            Assert.AreEqual(1, result.winnerId);
            Assert.AreEqual(WinReason.Hp, result.reason);
        }

        [Test]
        public void QuestionCountCapResolvesToProgressTiebreak()
        {
            var (state, a, b) = TwoPlayerMatch();
            state.maxRounds = 1;
            a.questionsAnswered = 1;
            a.pos = new GridPos(a.pos.x, 3);
            b.pos = new GridPos(b.pos.x, 1);
            var result = MatchEngine.CheckWinCondition(state);
            Assert.AreEqual(a.playerId, result.winnerId);
            Assert.AreEqual(WinReason.Progress, result.reason);
        }

        [Test]
        public void StreakOfTwoCanRollFreezeAndItSkipsTheTargetsNextAdvance()
        {
            var (state, a, b) = TwoPlayerMatch();
            MatchEngine.PushQuestion(state, 1, 101, 0);
            MatchEngine.SubmitAnswer(state, 1, 0, RngFreeze); // streak 1

            MatchEngine.PushQuestion(state, 1, 102, 0);
            var answer = MatchEngine.SubmitAnswer(state, 1, 0, RngFreeze); // streak 2, reward = freeze
            Assert.AreEqual(RewardType.Freeze, answer.rewardOffered.type);

            var result = MatchEngine.UseFreeze(state, a.playerId, answer.rewardOffered.rewardId, b.playerId);
            Assert.IsTrue(result.ok);
            Assert.IsTrue(b.frozen);

            // Bob answers correctly but doesn't move — the freeze is consumed instead.
            MatchEngine.PushQuestion(state, 2, 201, 0);
            var bAnswer = MatchEngine.SubmitAnswer(state, 2, 0, RngAttack);
            Assert.IsTrue(bAnswer.correct);
            Assert.AreEqual(0, b.pos.y, "frozen answer should not advance the player");
            Assert.IsFalse(b.frozen, "freeze should be consumed by the next correct answer");

            // The next correct answer after that advances normally again.
            MatchEngine.PushQuestion(state, 2, 202, 0);
            MatchEngine.SubmitAnswer(state, 2, 0, RngAttack);
            Assert.AreEqual(1, b.pos.y);
        }

        [Test]
        public void CannotAttackOrFreezeSamePlayerTwiceInARowWhenAnotherTargetExists()
        {
            var (state, a, b, c) = ThreePlayerMatch();
            MatchEngine.PushQuestion(state, 1, 101, 0);
            MatchEngine.SubmitAnswer(state, 1, 0, RngAttack); // streak 1
            MatchEngine.PushQuestion(state, 1, 102, 0);
            var first = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack); // streak 2, attack_choice
            MatchEngine.UseAttack(state, a.playerId, first.rewardOffered.rewardId, b.playerId);
            Assert.AreEqual(b.playerId, a.lastTargetedPlayerId);

            // Reward-type anti-repeat forces bonus_move next — consume it, then confirm
            // targeting Bob again is blocked while Cara is available.
            MatchEngine.PushQuestion(state, 1, 103, 0);
            var second = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            Assert.AreEqual(RewardType.BonusMove, second.rewardOffered.type);
            MatchEngine.ConsumeBonusMove(state, a.playerId, second.rewardOffered.rewardId);

            MatchEngine.PushQuestion(state, 1, 104, 0);
            var third = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            Assert.AreEqual(RewardType.AttackChoice, third.rewardOffered.type);
            var repeatBlocked = MatchEngine.UseAttack(state, a.playerId, third.rewardOffered.rewardId, b.playerId);
            Assert.IsFalse(repeatBlocked.ok);
            Assert.AreEqual("repeat_target_blocked", repeatBlocked.error);

            var okAgainstCara = MatchEngine.UseAttack(state, a.playerId, third.rewardOffered.rewardId, c.playerId);
            Assert.IsTrue(okAgainstCara.ok);
            Assert.AreEqual(c.playerId, a.lastTargetedPlayerId);
        }

        [Test]
        public void RepeatTargetRuleIsWaivedWhenNoOtherLivingOpponentExists()
        {
            var (state, a, b) = TwoPlayerMatch();
            MatchEngine.PushQuestion(state, 1, 101, 0);
            MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            MatchEngine.PushQuestion(state, 1, 102, 0);
            var first = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            MatchEngine.UseAttack(state, a.playerId, first.rewardOffered.rewardId, b.playerId);

            MatchEngine.PushQuestion(state, 1, 103, 0);
            var second = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            Assert.AreEqual(RewardType.BonusMove, second.rewardOffered.type, "anti-repeat on reward type still applies");
            MatchEngine.ConsumeBonusMove(state, a.playerId, second.rewardOffered.rewardId);

            MatchEngine.PushQuestion(state, 1, 104, 0);
            var third = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            // Bob is the only living opponent in a 2-player match, so targeting him again must be allowed.
            var result = MatchEngine.UseAttack(state, a.playerId, third.rewardOffered.rewardId, b.playerId);
            Assert.IsTrue(result.ok);
        }
    }
}
