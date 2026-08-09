using NUnit.Framework;
using QuizBattle.Characters;
using QuizBattle.GameState.MockEngine;

namespace QuizBattle.Tests.EditMode
{
    /// Mirrors server/src/matchEngine/matchEngine.test.ts — same scenarios, same
    /// expected numbers, so the two engines are verified to agree.
    public class MatchEngineTests
    {
        private static double RngAttack() => 0.0; // always < 0.6 => attack_choice
        private static double RngMove() => 0.99; // always >= 0.6 => bonus_move

        [SetUp]
        public void SetUp()
        {
            CharacterCatalog.Clear();
            CharacterCatalog.Register(new CharacterConfig
            {
                characterId = "blaze", maxHp = 100, abilityId = "fireball", abilityType = AbilityType.Active,
                targeting = AbilityTargeting.Ranged, range = 3, baseDamage = 20, dotDamage = 5, dotRounds = 1,
                vfxTag = "vfx_fireball",
            });
            CharacterCatalog.Register(new CharacterConfig
            {
                characterId = "aegis", maxHp = 120, abilityId = "bulwark", abilityType = AbilityType.Passive,
                damageReductionPct = 25, vfxTag = "vfx_shield_shimmer",
            });
        }

        private (MatchState state, PlayerState a, PlayerState b) TwoPlayerMatch()
        {
            var state = MatchEngine.CreateMatch(1, MatchMode.Ffa);
            var a = MatchEngine.AddPlayer(state, 1, "Alice", "blaze", null, new GridPos(0, 0));
            var b = MatchEngine.AddPlayer(state, 2, "Bob", "aegis", null, new GridPos(7, 5));
            MatchEngine.StartMatch(state);
            return (state, a, b);
        }

        [Test]
        public void StreakOfTwoCorrectAnswersOffersReward()
        {
            var (state, _, _) = TwoPlayerMatch();
            MatchEngine.PushQuestion(state, 101, 0);
            var r1 = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            Assert.IsTrue(r1.correct);
            Assert.AreEqual(1, r1.streakCount);
            Assert.IsNull(r1.rewardOffered);

            MatchEngine.ResolveRound(state);
            MatchEngine.PushQuestion(state, 102, 0);
            var r2 = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            Assert.AreEqual(2, r2.streakCount);
            Assert.AreEqual(RewardType.AttackChoice, r2.rewardOffered.type);
        }

        [Test]
        public void WrongAnswerResetsStreakAndOffersNoReward()
        {
            var (state, _, _) = TwoPlayerMatch();
            MatchEngine.PushQuestion(state, 101, 0);
            MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            MatchEngine.ResolveRound(state);
            MatchEngine.PushQuestion(state, 102, 0);
            var wrong = MatchEngine.SubmitAnswer(state, 1, 1, RngAttack);
            Assert.IsFalse(wrong.correct);
            Assert.AreEqual(0, wrong.streakCount);
            Assert.IsNull(wrong.rewardOffered);
        }

        [Test]
        public void AegisReducesDamageAndBlazeAppliesBurnDot()
        {
            var (state, a, b) = TwoPlayerMatch();
            MatchEngine.PushQuestion(state, 101, 0);
            MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            MatchEngine.ResolveRound(state); // zone drain: b 120 -> 117
            Assert.AreEqual(117, b.hp);

            MatchEngine.PushQuestion(state, 102, 0);
            var answer = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            var rewardId = answer.rewardOffered.rewardId;

            var result = MatchEngine.UseAttack(state, a.playerId, rewardId, b.playerId);
            Assert.IsTrue(result.ok);
            Assert.AreEqual(15, result.outcome.damage); // 20 * 0.75
            Assert.AreEqual(102, b.hp); // 117 - 15
            Assert.IsNotNull(b.pendingDot);

            var resolution = MatchEngine.ResolveRound(state); // dot -5, zone drain -3
            Assert.AreEqual(94, b.hp);
            Assert.IsNull(b.pendingDot);
            Assert.AreEqual(1, resolution.dotDamage.Count);
        }

        [Test]
        public void AntiRepeatForcesBonusMoveAfterConsumedAttack()
        {
            var (state, a, b) = TwoPlayerMatch();
            MatchEngine.PushQuestion(state, 101, 0);
            MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            MatchEngine.ResolveRound(state);

            MatchEngine.PushQuestion(state, 102, 0);
            var first = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            Assert.AreEqual(RewardType.AttackChoice, first.rewardOffered.type);
            MatchEngine.UseAttack(state, a.playerId, first.rewardOffered.rewardId, b.playerId);
            MatchEngine.ResolveRound(state);

            MatchEngine.PushQuestion(state, 103, 0);
            var second = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            Assert.AreEqual(3, second.streakCount);
            Assert.AreEqual(RewardType.BonusMove, second.rewardOffered.type);
        }

        [Test]
        public void CannotTargetSelf()
        {
            var (state, a, _) = TwoPlayerMatch();
            MatchEngine.PushQuestion(state, 101, 0);
            MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            MatchEngine.ResolveRound(state);
            MatchEngine.PushQuestion(state, 102, 0);
            var answer = MatchEngine.SubmitAnswer(state, 1, 0, RngAttack);
            var selfAttack = MatchEngine.UseAttack(state, a.playerId, answer.rewardOffered.rewardId, a.playerId);
            Assert.IsFalse(selfAttack.ok);
            Assert.AreEqual("cannot_target_self", selfAttack.error);
        }

        [Test]
        public void BonusMoveGrantsExtraSteps()
        {
            var (state, a, _) = TwoPlayerMatch();
            MatchEngine.PushQuestion(state, 101, 0);
            MatchEngine.SubmitAnswer(state, 1, 0, RngMove);
            MatchEngine.ResolveRound(state);
            MatchEngine.PushQuestion(state, 102, 0);
            var answer = MatchEngine.SubmitAnswer(state, 1, 0, RngMove);
            Assert.AreEqual(RewardType.BonusMove, answer.rewardOffered.type);

            int before = a.movementBudget;
            var result = MatchEngine.ConsumeBonusMove(state, a.playerId, answer.rewardOffered.rewardId);
            Assert.IsTrue(result.ok);
            Assert.AreEqual(before + 2, a.movementBudget);
        }

        [Test]
        public void MovementRespectsBoundsAndOccupancy()
        {
            var (state, a, b) = TwoPlayerMatch();
            var oob = MatchEngine.Move(state, a.playerId, "up");
            Assert.IsFalse(oob.ok);
            Assert.AreEqual("out_of_bounds", oob.error);

            b.pos = new GridPos(1, 0);
            var blocked = MatchEngine.Move(state, a.playerId, "right");
            Assert.IsFalse(blocked.ok);
            Assert.AreEqual("tile_occupied", blocked.error);
        }

        [Test]
        public void ZoneControlDrainsAndEliminates()
        {
            var (state, a, b) = TwoPlayerMatch();
            a.pos = new GridPos(3, 2); // sits on zone-a
            b.hp = MatchState.ZoneDrainHp;
            var tick = ZoneController.TickZones(state);
            Assert.AreEqual(1, tick.controllingTeamOrPlayer.Count);
            Assert.AreEqual(0, b.hp);
            Assert.IsFalse(b.alive);
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
        public void RoundCapResolvesToZoneControlTiebreak()
        {
            var (state, a, _) = TwoPlayerMatch();
            state.maxRounds = 1;
            state.round = 1;
            a.zoneControlTicks = 5;
            MatchEngine.PushQuestion(state, 101, 0);
            var resolution = MatchEngine.ResolveRound(state);
            Assert.AreEqual(a.playerId, resolution.result.winnerId);
            Assert.AreEqual(WinReason.ZoneControl, resolution.result.reason);
        }
    }
}
