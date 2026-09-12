using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;

namespace Project333.Tests.EditMode
{
    public sealed class AiMovementHazardTests
    {
        [TestCase(25, 100, false)]
        [TestCase(100, 25, false)]
        [TestCase(26, 100, true)]
        [TestCase(100, 26, true)]
        public void NewCover_IsCreditedOnlyWhenGuardAndProducerSurviveRealBombDamage(
            int guardHp, int producerHp, bool survives)
        {
            var state = State();
            var producer = Unit(state, "producer", 0, 1, producerHp, producer: true);
            var guard = Unit(state, "guard", 4, 0, guardHp);
            AddBomb(state);
            var move = new MoveOccupantCommand(guard.Position, new TileCoord(0, 0));
            var projected = Project(state, move);

            Assert.That(Score(state, move), Is.EqualTo(46));
            Assert.That(ScoreBeforeTurnStart(state, move, projected), Is.EqualTo(survives ? 46 : -4));
            Assert.That(projected.AIBoard.GetOccupant(move.To) != null, Is.EqualTo(guardHp > 25));
            Assert.That(projected.AIBoard.GetOccupant(producer.Position) != null, Is.EqualTo(producerHp > 25));
        }

        [Test]
        public void DemonKingPendingRevival_RemainsPresentButDoesNotEarnGuardBonus()
        {
            var state = State();
            Unit(state, "producer", 0, 1, 100, producer: true);
            var guard = Unit(state, "demon", 4, 0, 25, cardId: DemonKingRules.CardId);
            AddBomb(state);
            var move = new MoveOccupantCommand(guard.Position, new TileCoord(0, 0));
            var projected = Project(state, move);
            var pending = projected.AIBoard.GetOccupant(move.To);

            Assert.That(pending, Is.Not.Null);
            Assert.That(pending.RuntimeId, Is.EqualTo(guard.RuntimeId));
            Assert.That(pending.IsDemonKingRevivalPending, Is.True);
            Assert.That(ScoreBeforeTurnStart(state, move, projected), Is.EqualTo(-4));
        }

        [Test]
        public void SwappedProducerAndGuard_AreMatchedAtTheirResultingCoordinates()
        {
            var state = State();
            var producer = Unit(state, "producer", 0, 0, 100, producer: true);
            var guard = Unit(state, "guard", 0, 1, 100);
            AddBomb(state);
            var move = new MoveOccupantCommand(producer.Position, guard.Position);
            var projected = Project(state, move);

            Assert.That(projected.AIBoard.GetOccupant(new TileCoord(0, 1)).RuntimeId, Is.EqualTo(producer.RuntimeId));
            Assert.That(projected.AIBoard.GetOccupant(new TileCoord(0, 0)).RuntimeId, Is.EqualTo(guard.RuntimeId));
            Assert.That(ScoreBeforeTurnStart(state, move, projected), Is.EqualTo(46));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DifferentOccupantAtSameProjectedTile_DoesNotInheritCoverCredit(bool replaceGuard)
        {
            var state = State();
            Unit(state, "producer", 0, 1, 100, producer: true);
            var guard = Unit(state, "guard", 4, 0, 100);
            var move = new MoveOccupantCommand(guard.Position, new TileCoord(0, 0));
            var projected = Project(state, move);
            var row = replaceGuard ? 0 : 1;
            projected.AIBoard.Remove(new TileCoord(0, row));
            Unit(projected, "replacement", 0, row, 100, producer: !replaceGuard);

            Assert.That(ScoreBeforeTurnStart(state, move, projected), Is.EqualTo(-4));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ProjectedSealboundGuardOrProducer_DoesNotEarnCoverCredit(bool sealGuard)
        {
            var state = State();
            Unit(state, "producer", 0, 1, 100, producer: true);
            var guard = Unit(state, "guard", 4, 0, 100);
            var move = new MoveOccupantCommand(guard.Position, new TileCoord(0, 0));
            var projected = Project(state, move);
            projected.AIBoard.GetOccupant(new TileCoord(0, sealGuard ? 0 : 1)).EnterSealbound(3);

            Assert.That(ScoreBeforeTurnStart(state, move, projected), Is.EqualTo(-4));
        }

        [Test]
        public void WoundedTargetHealedAtTurnEnd_NoLongerEarnsWoundedProtectionBonus()
        {
            var state = State();
            var wounded = Unit(state, "wounded", 0, 1, 100);
            wounded.CurrentHp = 20;
            var guard = Unit(state, "guard", 4, 0, 100);
            Unit(state, "healer", 4, 1, 100, cardId: "BlueDragon");
            var move = new MoveOccupantCommand(guard.Position, new TileCoord(0, 0));
            var projected = Project(state, move);

            Assert.That(Score(state, move), Is.EqualTo(16));
            Assert.That(projected.AIBoard.GetOccupant(wounded.Position).CurrentHp, Is.EqualTo(53));
            Assert.That(ScoreBeforeTurnStart(state, move, projected), Is.EqualTo(-4));
        }

        [Test]
        public void RemovingExistingCover_PreservesOriginalCostsAndPenalties()
        {
            var state = State();
            Unit(state, "producer", 0, 1, 100, producer: true);
            var guard = Unit(state, "guard", 0, 0, 100);
            Unit(state, "ranged", 1, 1, 100, ranged: true);
            var move = new MoveOccupantCommand(guard.Position, new TileCoord(1, 0));
            var projected = Project(state, move);

            Assert.That(Score(state, move), Is.LessThan(0));
            Assert.That(ScoreBeforeTurnStart(state, move, projected), Is.EqualTo(Score(state, move)));
        }

        [Test]
        public void SurvivalAssessment_DoesNotMutateEitherInputState()
        {
            var state = State();
            Unit(state, "producer", 0, 1, 100, producer: true);
            var guard = Unit(state, "guard", 4, 0, 25);
            AddBomb(state);
            var move = new MoveOccupantCommand(guard.Position, new TileCoord(0, 0));
            var projected = Project(state, move);
            var beforeKey = AiBattleStateCopy.PositionKey(state);
            var projectedKey = AiBattleStateCopy.PositionKey(projected);

            ScoreBeforeTurnStart(state, move, projected);

            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(beforeKey));
            Assert.That(AiBattleStateCopy.PositionKey(projected), Is.EqualTo(projectedKey));
        }

        private static BattleState State()
        {
            var deck = Enumerable.Repeat("unseen", 20).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.AI));
            state.SetPhase(PhaseType.Main);
            foreach (var player in new[] { state.Player, state.AI })
            {
                foreach (var card in player.Hand.CardIds.ToArray()) player.Hand.Remove(card);
                player.Master.RemainingAttacksThisTurn = 0;
            }
            return state;
        }

        private static UnitState Unit(BattleState state, string runtimeId, int column, int row, int hp,
            bool producer = false, bool ranged = false, string cardId = "unit")
        {
            var unit = new UnitState(runtimeId, cardId, PlayerId.AI, new TileCoord(column, row),
                ranged ? AttackType.Ranged : AttackType.Melee, ranged ? 10 : 0, hp, true, false, 0,
                producer ? new ResourceSet(1, 0, 0, 0) : null);
            unit.RemainingAttacksThisTurn = 0;
            state.AIBoard.Place(unit.Position, unit);
            return unit;
        }

        private static void AddBomb(BattleState state)
        {
            state.PersistentEffects.Add(new PersistentEffectState(BiochemicalBombRules.CardId, PlayerId.Player,
                BiochemicalBombRules.EffectId, state.TurnNumber, "After four global turn starts", new ResourceSet(),
                remainingTriggers: BiochemicalBombRules.TriggerCount, effectDamage: BiochemicalBombRules.BaseDamage,
                effectDamageType: DamageType.Fixed, targetStartColumn: BiochemicalBombRules.LeftAreaStartColumn));
        }

        private static BattleState Project(BattleState state, MoveOccupantCommand move)
        {
            var projected = AiBattleStateCopy.Create(state);
            new MoveService().Move(projected, PlayerId.AI, move.From, move.To);
            new EndTurnService(new Random(41)).EndTurn(projected);
            new TurnStartService(new ScienceUpkeepService()).ResolveTurnStart(projected);
            return projected;
        }

        private static double Score(BattleState state, MoveOccupantCommand move) =>
            InvokeScore("Score", state, move);

        private static double ScoreBeforeTurnStart(BattleState state, MoveOccupantCommand move, BattleState projected) =>
            InvokeScore("ScoreBeforeTurnStart", state, move, projected);

        private static double InvokeScore(string method, params object[] args) =>
            (double)typeof(AiDecisionService).Assembly.GetType("Project333.Runtime.Application.Services.AiMovementPolicy")
                .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
    }
}