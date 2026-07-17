using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Tests.EditMode
{
    public sealed class MovementTests
    {
        [Test]
        public void Move_MovesMovableUnitToEmptyTile()
        {
            var battleState = CreateBattleState();
            var moveService = new MoveService();
            var mover = CreateUnit("mover", new TileCoord(1, 1), canMove: true);

            battleState.PlayerBoard.Place(new TileCoord(1, 1), mover);

            moveService.Move(battleState, PlayerId.Player, new TileCoord(1, 1), new TileCoord(3, 1));

            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(1, 1)), Is.Null);
            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(3, 1)), Is.SameAs(mover));
            Assert.That(mover.Position, Is.EqualTo(new TileCoord(3, 1)));
        }

        [Test]
        public void Move_ThrowsForBuilding()
        {
            var battleState = CreateBattleState();
            var moveService = new MoveService();
            var building = new BuildingState(
                runtimeId: "building",
                cardId: "building",
                ownerId: PlayerId.Player,
                position: new TileCoord(1, 1),
                canAttack: false,
                attack: 0,
                maxHp: 5);

            battleState.PlayerBoard.Place(new TileCoord(1, 1), building);

            Assert.Throws<InvalidOperationException>(() =>
                moveService.Move(battleState, PlayerId.Player, new TileCoord(1, 1), new TileCoord(3, 1)));
        }

        [Test]
        public void Move_SwapsWithOccupiedMovableAlly()
        {
            var battleState = CreateBattleState();
            var moveService = new MoveService();
            var mover = CreateUnit("mover", new TileCoord(1, 1), canMove: true);
            var blocker = CreateUnit("blocker", new TileCoord(4, 1), canMove: true);

            battleState.PlayerBoard.Place(new TileCoord(1, 1), mover);
            battleState.PlayerBoard.Place(new TileCoord(4, 1), blocker);

            moveService.Move(battleState, PlayerId.Player, new TileCoord(1, 1), new TileCoord(4, 1));

            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(1, 1)), Is.SameAs(blocker));
            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(4, 1)), Is.SameAs(mover));
            Assert.That(mover.Position, Is.EqualTo(new TileCoord(4, 1)));
            Assert.That(blocker.Position, Is.EqualTo(new TileCoord(1, 1)));
        }

        [Test]
        public void Move_ThrowsForOccupiedImmobileDestination()
        {
            var battleState = CreateBattleState();
            var moveService = new MoveService();
            var mover = CreateUnit("mover", new TileCoord(1, 1), canMove: true);
            var blocker = CreateUnit("blocker", new TileCoord(4, 1), canMove: false);

            battleState.PlayerBoard.Place(new TileCoord(1, 1), mover);
            battleState.PlayerBoard.Place(new TileCoord(4, 1), blocker);

            Assert.Throws<InvalidOperationException>(() =>
                moveService.Move(battleState, PlayerId.Player, new TileCoord(1, 1), new TileCoord(4, 1)));
        }

        [Test]
        public void Move_AllowsErasureMovableUnit()
        {
            var battleState = CreateBattleState();
            var moveService = new MoveService();
            var mover = CreateUnit("erasure-mover", new TileCoord(1, 1), canMove: true);

            mover.ApplyErasure();
            battleState.PlayerBoard.Place(new TileCoord(1, 1), mover);

            moveService.Move(battleState, PlayerId.Player, new TileCoord(1, 1), new TileCoord(3, 1));

            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(3, 1)), Is.SameAs(mover));
        }

        [Test]
        public void Move_ThrowsForDrainedUnit()
        {
            var battleState = CreateBattleState();
            var moveService = new MoveService();
            var mover = CreateUnit("drained-mover", new TileCoord(1, 1), canMove: true);

            mover.IsDrained = true;
            battleState.PlayerBoard.Place(new TileCoord(1, 1), mover);

            Assert.Throws<InvalidOperationException>(() =>
                moveService.Move(battleState, PlayerId.Player, new TileCoord(1, 1), new TileCoord(3, 1)));
        }

        private static UnitState CreateUnit(string id, TileCoord coord, bool canMove)
        {
            return new UnitState(
                runtimeId: id,
                cardId: id,
                ownerId: PlayerId.Player,
                position: coord,
                attackType: AttackType.Melee,
                attack: 2,
                maxHp: 5,
                canMove: canMove,
                isScience: false,
                sciencePowerUpkeep: 0);
        }

        private static BattleState CreateBattleState()
        {
            var setupService = new BattleSetupService();
            var mulliganService = new MulliganService();
            var battleState = setupService.CreateInitialState(new BattleSetupRequest(CreateDeck("P"), CreateDeck("A"), PlayerId.Player));
            mulliganService.PassMulligan(battleState, PlayerId.Player);
            battleState.SetPhase(PhaseType.Main);
            return battleState;
        }

        private static IReadOnlyList<string> CreateDeck(string prefix)
        {
            return new List<string>
            {
                prefix + "-00",
                prefix + "-01",
                prefix + "-02",
                prefix + "-03",
                prefix + "-04",
                prefix + "-05",
                prefix + "-06",
                prefix + "-07",
                prefix + "-08",
                prefix + "-09",
            };
        }
    }
}
