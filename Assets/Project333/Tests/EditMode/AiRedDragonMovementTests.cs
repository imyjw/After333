using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Tests.EditMode
{
    public sealed class AiRedDragonMovementTests
    {
        [Test]
        public void GuardKilledAtEnemyTurnEnd_StillProtectsSurvivingProducerDuringEnemyMain()
        {
            var state = State();
            var producer = Unit(state, "producer", new TileCoord(0, 1), 100, producer: true);
            var guard = Unit(state, "guard", new TileCoord(4, 0), 25);
            var move = new MoveOccupantCommand(guard.Position, new TileCoord(0, 0));
            var near = ProjectToEnemyMain(state, move);
            var future = ProjectEnemyTurnEnd(near);

            Assert.That(near.AIBoard.GetOccupant(move.To), Is.Not.Null);
            Assert.That(future.AIBoard.GetOccupant(move.To), Is.Null);
            Assert.That(future.AIBoard.GetOccupant(producer.Position).CurrentHp, Is.EqualTo(67));
            Assert.That(ScoreNear(state, move, near), Is.EqualTo(46));
            Assert.That(ScoreFuture(state, move, near, future), Is.EqualTo(46));
        }

        [Test]
        public void SwappedRearProducerKilledAtEnemyTurnEnd_DoesNotEarnFutureProtectionBonus()
        {
            var state = State();
            var producer = Unit(state, "producer", new TileCoord(0, 0), 25, producer: true);
            var guard = Unit(state, "guard", new TileCoord(0, 1), 100);
            var move = new MoveOccupantCommand(producer.Position, guard.Position);
            var near = ProjectToEnemyMain(state, move);
            var future = ProjectEnemyTurnEnd(near);

            Assert.That(near.AIBoard.GetOccupant(move.To).RuntimeId, Is.EqualTo(producer.RuntimeId));
            Assert.That(future.AIBoard.GetOccupant(move.To), Is.Null);
            Assert.That(future.AIBoard.GetOccupant(move.From).RuntimeId, Is.EqualTo(guard.RuntimeId));
            Assert.That(ScoreNear(state, move, near), Is.EqualTo(46));
            Assert.That(ScoreFuture(state, move, near, future), Is.EqualTo(-4));
        }

        [Test]
        public void MasterProtectionDuringEnemyMain_IsRetainedDespiteEnemyPassForecastDefeat()
        {
            var state = State();
            state.AI.Master.CurrentHp = 20;
            var guard = Unit(state, "guard", new TileCoord(4, 0), 25);
            var move = new MoveOccupantCommand(guard.Position, new TileCoord(2, 0));
            var near = ProjectToEnemyMain(state, move);
            var future = ProjectEnemyTurnEnd(near);
            var beforeKey = AiBattleStateCopy.PositionKey(state);
            var nearKey = AiBattleStateCopy.PositionKey(near);
            var futureKey = AiBattleStateCopy.PositionKey(future);

            Assert.That(future.IsEnded, Is.True);
            Assert.That(future.AI.Master.CurrentHp, Is.LessThanOrEqualTo(0));
            Assert.That(ScoreNear(state, move, near), Is.EqualTo(16));
            Assert.That(ScoreFuture(state, move, near, future), Is.EqualTo(16),
                "An opponent-pass estimate must not remove protection that exists during enemy main.");
            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(beforeKey));
            Assert.That(AiBattleStateCopy.PositionKey(near), Is.EqualTo(nearKey));
            Assert.That(AiBattleStateCopy.PositionKey(future), Is.EqualTo(futureKey));
        }

        private static BattleState State()
        {
            var deck = Enumerable.Repeat("unseen", 20).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.AI));
            state.SetPhase(PhaseType.Main);
            var dragon = new UnitState("enemy-dragon", "RedDragon", PlayerId.Player, new TileCoord(4, 1),
                AttackType.Melee, 0, 100, true, false, 0);
            state.PlayerBoard.Place(dragon.Position, dragon);
            return state;
        }

        private static UnitState Unit(BattleState state, string runtimeId, TileCoord position, int hp,
            bool producer = false)
        {
            var unit = new UnitState(runtimeId, "unit", PlayerId.AI, position, AttackType.Melee,
                0, hp, true, false, 0, producer ? new ResourceSet(1, 0, 0, 0) : null);
            state.AIBoard.Place(position, unit);
            return unit;
        }

        private static BattleState ProjectToEnemyMain(BattleState state, MoveOccupantCommand move)
        {
            var near = AiBattleStateCopy.Create(state);
            new MoveService().Move(near, PlayerId.AI, move.From, move.To);
            new EndTurnService(new Random(41)).EndTurn(near);
            new TurnStartService(new ScienceUpkeepService()).ResolveTurnStart(near);
            return near;
        }

        private static BattleState ProjectEnemyTurnEnd(BattleState near)
        {
            var future = AiBattleStateCopy.Create(near);
            new EndTurnService(new Random(43)).EndTurn(future);
            return future;
        }

        private static double ScoreNear(BattleState state, MoveOccupantCommand move, BattleState near) =>
            InvokeScore("ScoreBeforeTurnStart", state, move, near);

        private static double ScoreFuture(BattleState state, MoveOccupantCommand move,
            BattleState near, BattleState future) =>
            InvokeScore("ScoreWithSurvivalForecast", state, move, near, future.AIBoard);

        private static double InvokeScore(string method, params object[] arguments) =>
            (double)typeof(AiDecisionService).Assembly.GetType("Project333.Runtime.Application.Services.AiMovementPolicy")
                .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments);
    }
}
