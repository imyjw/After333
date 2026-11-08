using System;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Presentation.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class OccupantAttackReadinessTests
    {
        [TestCase(PhaseType.Main, PlayerId.Player, true)]
        [TestCase(PhaseType.Main, PlayerId.AI, false)]
        [TestCase(PhaseType.Mulligan, PlayerId.Player, false)]
        [TestCase(PhaseType.TurnStart, PlayerId.Player, false)]
        [TestCase(PhaseType.TurnEnd, PlayerId.Player, false)]
        [TestCase(PhaseType.Ended, PlayerId.Player, false)]
        public void OnlyLocalMainTurnShowsTheIndicator(PhaseType phase, PlayerId active, bool expected)
        {
            var battle = CreateBattle();
            battle.RestoreRuntimeState(5, active, phase);
            Assert.That(OccupantAttackReadiness.ShouldShow(battle, CreateUnit(PlayerId.Player)), Is.EqualTo(expected));
            Assert.That(OccupantAttackReadiness.ShouldShow(battle, CreateUnit(PlayerId.AI)), Is.False);
        }

        [TestCase("summoned")]
        [TestCase("drained")]
        [TestCase("sealed")]
        [TestCase("stunned")]
        [TestCase("frozen")]
        [TestCase("dead")]
        [TestCase("spent")]
        public void AttackRestrictedOccupantsHaveNoDot(string restriction)
        {
            var battle = CreateBattle();
            var unit = CreateUnit(PlayerId.Player);
            switch (restriction)
            {
                case "summoned": unit.HasSummoningSickness = true; break;
                case "drained": unit.IsDrained = true; break;
                case "sealed": unit.EnterSealbound(2); break;
                case "stunned": unit.ApplyStun(2); break;
                case "frozen": unit.ApplyFreeze(2); break;
                case "dead": unit.CurrentHp = 0; break;
                case "spent": unit.RemainingAttacksThisTurn = 0; break;
            }
            Assert.That(OccupantAttackReadiness.ShouldShow(battle, unit), Is.False);
        }

        [Test]
        public void RealAttackConsumptionKeepsDotUntilTheFinalAttackAndNextTurnRestoresIt()
        {
            var battle = CreateBattle();
            var unit = CreateUnit(PlayerId.Player);
            battle.PlayerBoard.Place(unit.Position, unit);
            var attack = new AttackService();
            Assert.That(OccupantAttackReadiness.ShouldShow(battle, unit), Is.True);
            attack.Attack(battle, PlayerId.Player, unit.Position, battle.AI.Master.Position);
            Assert.That(unit.RemainingAttacksThisTurn, Is.EqualTo(1));
            Assert.That(OccupantAttackReadiness.ShouldShow(battle, unit), Is.True);
            attack.Attack(battle, PlayerId.Player, unit.Position, battle.AI.Master.Position);
            Assert.That(OccupantAttackReadiness.ShouldShow(battle, unit), Is.False);
            battle.RestoreRuntimeState(7, PlayerId.Player, PhaseType.TurnStart);
            new TurnStartService().ResolveTurnStart(battle);
            Assert.That(OccupantAttackReadiness.ShouldShow(battle, unit), Is.True);
        }

        [Test]
        public void MasterAndArmedBuildingParticipateButZeroAttackAndPassiveBuildingsDoNot()
        {
            var battle = CreateBattle();
            battle.Player.Master.HasSummoningSickness = false;
            Assert.That(OccupantAttackReadiness.ShouldShow(battle, battle.Player.Master), Is.True);
            var armed = new BuildingState("armed", "ManaCannon", PlayerId.Player, new TileCoord(0, 0), true, 10, 20);
            armed.HasSummoningSickness = false;
            Assert.That(OccupantAttackReadiness.ShouldShow(battle, armed), Is.True);
            var passive = new BuildingState("passive", "ManaPond", PlayerId.Player, new TileCoord(1, 0), false, 10, 20);
            passive.HasSummoningSickness = false;
            passive.RemainingAttacksThisTurn = 1;
            Assert.That(OccupantAttackReadiness.ShouldShow(battle, passive), Is.False);
            var zero = new UnitState("zero", "zero", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 0, 20, true, false, 0);
            zero.HasSummoningSickness = false;
            Assert.That(OccupantAttackReadiness.ShouldShow(battle, zero), Is.False);
            Assert.That(OccupantAttackReadiness.ShouldShow(null, armed), Is.False);
            Assert.That(OccupantAttackReadiness.ShouldShow(battle, null), Is.False);
            battle.EndBattle(PlayerId.Player);
            Assert.That(OccupantAttackReadiness.ShouldShow(battle, armed), Is.False);
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void FreshOnlineProjectionAndReconnectRespectTheViewingSeat(PlayerId viewer)
        {
            var battle = CreateBattle();
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            {
                var unit = CreateUnit(owner);
                battle.GetBoard(owner).Place(unit.Position, unit);
            }
            battle.RestoreRuntimeState(5, viewer, PhaseType.Main);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(
                new BattleStateViewFactory().CreateForPlayer(battle, "attack-ready", viewer));
            Assert.That(OccupantAttackReadiness.ShouldShow(projected, projected.PlayerBoard.GetOccupant(new TileCoord(0, 0))), Is.True);
            Assert.That(OccupantAttackReadiness.ShouldShow(projected, projected.AIBoard.GetOccupant(new TileCoord(0, 0))), Is.False);
            battle.RestoreRuntimeState(6, viewer == PlayerId.Player ? PlayerId.AI : PlayerId.Player, PhaseType.Main);
            projected = BattleStateViewProjector.CreateLocalPerspectiveState(
                new BattleStateViewFactory().CreateForPlayer(battle, "attack-ready", viewer));
            Assert.That(OccupantAttackReadiness.ShouldShow(projected, projected.PlayerBoard.GetOccupant(new TileCoord(0, 0))), Is.False);
        }

        private static BattleState CreateBattle()
        {
            var cards = Enumerable.Range(0, 33).Select(i => "test-" + i).ToArray();
            var battle = new BattleSetupService().CreateInitialState(new BattleSetupRequest(cards, cards, PlayerId.Player));
            battle.RestoreRuntimeState(5, PlayerId.Player, PhaseType.Main);
            return battle;
        }

        private static UnitState CreateUnit(PlayerId owner)
        {
            return new UnitState("ready-" + owner, "Archer", owner, new TileCoord(0, 0),
                AttackType.Ranged, 3, 50, true, false, 0, maxAttacksPerTurn: 2)
            { HasSummoningSickness = false };
        }
    }
}
