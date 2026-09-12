using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Tests.EditMode
{
    public sealed class AiHazardEvaluationTests
    {
        // These isolate score attribution using detached, manually resolved board snapshots.
        // AiPersistentHazardTests separately exercises the actual spell and turn services.
        [TestCase(false)]
        [TestCase(true)]
        public void RemovedAiOccupant_DoesNotKeepImmediateMaterialOrProductionValue(bool building)
        {
            var state = State();
            var position = new TileCoord(0, 0);
            if (building)
                state.AIBoard.Place(position, new BuildingState("producer", "producer", PlayerId.AI,
                    position, false, 0, 25, new ResourceSet(0, 0, 0, 3)));
            else
                Unit(state, PlayerId.AI, position, 80, 25);
            var projected = WithoutAiOccupant(state, position);

            Assert.That(Evaluate(state), Is.GreaterThan(Evaluate(projected)));
            Assert.That(Before(state, projected), Is.EqualTo(Before(projected, projected)).Within(0.0001));
            Assert.That(state.AIBoard.GetOccupant(position), Is.Not.Null,
                "Evaluating a detached projection must not remove the real occupant.");
        }

        [Test]
        public void RemovedFrontBlocker_DoesNotKeepProtectionValueForSurvivingRearProducer()
        {
            var state = State();
            var front = new TileCoord(0, 0);
            Unit(state, PlayerId.AI, front, 0, 25);
            Unit(state, PlayerId.AI, new TileCoord(0, 1), 0, 100, gain: new ResourceSet(1, 0, 0, 0));
            var projected = WithoutAiOccupant(state, front);

            Assert.That(Before(state, projected), Is.EqualTo(Before(projected, projected)).Within(0.0001),
                "The rear producer must not receive cover from a blocker absent after the turn transition.");
        }

        [Test]
        public void RemovedMasterGuard_DoesNotReduceProjectedEnemyAttackExposure()
        {
            var state = State();
            state.AI.Master.CurrentHp = 10;
            var guardPosition = new TileCoord(2, 0);
            var enemyPosition = new TileCoord(4, 0);
            Unit(state, PlayerId.AI, guardPosition, 0, 25);
            Unit(state, PlayerId.Player, enemyPosition, 50, 100);
            var projected = WithoutAiOccupant(state, guardPosition);
            var targeting = new TargetingService();

            Assert.That(targeting.CanTarget(state, PlayerId.Player, enemyPosition, state.AI.Master.Position), Is.False);
            Assert.That(targeting.CanTarget(projected, PlayerId.Player, enemyPosition, projected.AI.Master.Position), Is.True);
            Assert.That(Before(state, projected), Is.EqualTo(Before(projected, projected)).Within(0.0001),
                "An immediately doomed guard must neither retain material nor hide the exposed master.");
        }

        [Test]
        public void PendingDemonKing_RetainsMaterialButLosesItsImmediateAttackThreat()
        {
            var state = State();
            var position = new TileCoord(0, 0);
            var demon = Unit(state, PlayerId.AI, position, 33, 33, id: "DemonKing");
            var projected = AiBattleStateCopy.Create(state);
            var projectedDemon = projected.AIBoard.GetOccupant(position);
            Assert.That(projectedDemon.RuntimeId, Is.EqualTo(demon.RuntimeId));
            Assert.That(projectedDemon.TryEnterDemonKingRevivalSeal(PlayerId.Player, state.TurnNumber), Is.True);
            Assert.That(projectedDemon.IsAlive, Is.False);
            Assert.That(projectedDemon.IsDemonKingRevivalPending, Is.True);
            var withoutDemon = WithoutAiOccupant(state, position);

            var retainedMaterial = Before(state, projected) - Before(withoutDemon, projected);
            var liveMaterialAndThreat = Evaluate(state) - Evaluate(withoutDemon);
            Assert.That(retainedMaterial, Is.GreaterThan(0), "A pending revival remains a board asset.");
            Assert.That(retainedMaterial, Is.LessThan(liveMaterialAndThreat),
                "The pending Demon King can no longer threaten the opposing master.");
        }

        private static BattleState State()
        {
            var deck = Enumerable.Repeat("unknown", 20).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.AI));
            state.SetPhase(PhaseType.Main);
            return state;
        }

        private static UnitState Unit(BattleState state, PlayerId owner, TileCoord position,
            int attack, int hp, ResourceSet gain = null, string id = "test-unit")
        {
            var unit = new UnitState(Guid.NewGuid().ToString(), id, owner, position, AttackType.Melee,
                attack, hp, true, false, 0, gain);
            unit.HasSummoningSickness = false;
            unit.RemainingAttacksThisTurn = attack > 0 ? 1 : 0;
            state.GetBoard(owner).Place(position, unit);
            return unit;
        }

        private static BattleState WithoutAiOccupant(BattleState source, TileCoord position)
        {
            var copy = AiBattleStateCopy.Create(source);
            copy.AIBoard.Remove(position);
            return copy;
        }

        private static double Evaluate(BattleState state) => Invoke("Evaluate", state);

        private static double Before(BattleState state, BattleState projected) =>
            Invoke("EvaluateBeforeTurnStart", state, projected);

        private static double Invoke(string methodName, params object[] arguments)
        {
            var type = typeof(AiDecisionService).Assembly.GetType(
                "Project333.Runtime.Application.Services.AiPositionEvaluator", throwOnError: true);
            var evaluator = Activator.CreateInstance(type, nonPublic: true);
            return (double)type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)
                .Invoke(evaluator, arguments);
        }
    }
}
