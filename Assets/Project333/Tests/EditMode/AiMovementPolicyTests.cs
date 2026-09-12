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
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class AiMovementPolicyTests
    {
        private static BattleState State()
        {
            var deck = Enumerable.Repeat("unseen", 20).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.AI));
            state.SetPhase(PhaseType.Main);
            foreach (var p in new[] { state.Player, state.AI })
            {
                foreach (var id in p.Hand.CardIds.ToArray()) p.Hand.Remove(id);
                p.Master.RemainingAttacksThisTurn = 0;
            }
            return state;
        }

        private static UnitState Unit(BattleState s, int col, int row, string role = "plain",
            int hp = 30, int maxHp = 30)
        {
            var u = new UnitState(Guid.NewGuid().ToString(), "unit", PlayerId.AI, new TileCoord(col,row),
                role == "ranged" ? AttackType.Ranged : AttackType.Melee, role == "ranged" ? 10 : 0,
                maxHp, true, false, 0, role == "producer" ? new ResourceSet(1,0,0,0) : null);
            u.CurrentHp = hp;
            u.RemainingAttacksThisTurn = 0;
            s.AIBoard.Place(u.Position, u);
            return u;
        }

        private static double Score(BattleState s, TileCoord from, TileCoord to) =>
            (double)typeof(AiDecisionService).Assembly.GetType(
                "Project333.Runtime.Application.Services.AiMovementPolicy").GetMethod(
                    "Score", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { s, new MoveOccupantCommand(from, to) });

        private static AiDecisionService Planner(int depth = 1) => new AiDecisionService(
            new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>()), new TargetingService(),
            ZeroCardUpgradeLevelProvider.Instance, new AiSearchOptions
            { MaxDepth = depth, MaxMilliseconds = 10000, MaxSimulations = 4000 });

        [TestCase("producer", 46)]
        [TestCase("ranged", 26)]
        [TestCase("wounded", 16)]
        public void Cover_PrioritizesProducerThenRangedThenWounded(string role, double expected)
        {
            var s = State();
            var target = Unit(s, 0,1, role, hp: role == "wounded" ? 6 : 30);
            var guard = Unit(s, 4,0);
            Assert.That(Score(s, guard.Position, new TileCoord(0,0)), Is.EqualTo(expected));
            var cmd = Planner().GetNextCommand(s) as MoveOccupantCommand;
            Assert.That(cmd, Is.Not.Null);
            new MoveService().Move(s, PlayerId.AI, cmd.From, cmd.To);
            var cover = s.AIBoard.GetOccupant(new TileCoord(target.Position.Column,0));
            Assert.That(target.Position.Row, Is.EqualTo(1));
            Assert.That(cover, Is.Not.Null);
            Assert.That(cover.DoesNotBlockFrontRow, Is.False);
            Assert.That(cover.CurrentHp, Is.EqualTo(cover.MaxHp));
        }

        [Test]
        public void Cover_ProducerBuildingHasHighestBonus()
        {
            var s = State();
            s.AIBoard.Place(new TileCoord(0,1), new BuildingState("b", "b", PlayerId.AI,
                new TileCoord(0,1), false, 0, 30, new ResourceSet(1,0,0,0)));
            var guard = Unit(s, 4,0);
            Assert.That(Score(s, guard.Position, new TileCoord(0,0)), Is.EqualTo(56));
        }

        [TestCase(30, 100, true)]
        [TestCase(31, 100, false)]
        [TestCase(1, 1, false)]
        [TestCase(6, 20, true)]
        [TestCase(7, 20, false)]
        public void LowHp_RequiresActualDamageAndThreshold(int hp, int maxHp, bool protect)
        {
            var s = State();
            Unit(s, 0,1, hp: hp, maxHp: maxHp);
            var guard = Unit(s, 4,0);
            Assert.That(Score(s, guard.Position, new TileCoord(0,0)) > 0, Is.EqualTo(protect));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Support_CanMoveOrSwapBehindNonSupport(bool swap)
        {
            var s = State();
            var support = Unit(s, swap ? 0 : 4,0,"producer");
            var guard = Unit(s, 0, swap ? 1 : 0);
            var score = Score(s, support.Position, new TileCoord(0,1));
            Assert.That(score, Is.EqualTo(46));
            new MoveService().Move(s, PlayerId.AI, support.Position, new TileCoord(0,1));
            Assert.That(s.AIBoard.GetOccupant(new TileCoord(0,0)), Is.SameAs(guard));
        }

        [TestCase("producer")]
        [TestCase("ranged")]
        [TestCase("wounded")]
        public void Support_CannotBeUsedAsNewGuard(string guardRole)
        {
            var s = State();
            Unit(s,0,1,"producer");
            var guard = Unit(s,4,0,guardRole,hp:guardRole == "wounded" ? 6 : 30);
            Assert.That(Score(s,guard.Position,new TileCoord(0,0)), Is.LessThan(0));
        }

        [TestCase("flying")]
        [TestCase("hiding")]
        [TestCase("drained")]
        [TestCase("sealbound")]
        public void NonBlockingOccupants_DoNotEarnCoverBonus(string status)
        {
            var s = State();
            var target = Unit(s,4,0,"producer");
            var falseGuard = new UnitState("guard","guard",PlayerId.AI,new TileCoord(0,0),
                AttackType.Melee,0,30,true,false,0,hasFlying:status == "flying",hasHiding:status == "hiding");
            falseGuard.IsDrained = status == "drained";
            if(status == "sealbound") falseGuard.EnterSealbound(3);
            s.AIBoard.Place(falseGuard.Position,falseGuard);
            Assert.That(Score(s,target.Position,new TileCoord(0,1)), Is.LessThan(0));
        }

        [Test]
        public void ExistingCover_IsPenalizedWhenPulledOutOrSwapped()
        {
            var s = State();
            Unit(s,0,1,"producer");
            var guard = Unit(s,0,0);
            var free = Unit(s,4,0);
            Unit(s,1,1,"ranged");
            Assert.That(Score(s,guard.Position,new TileCoord(1,0)),
                Is.LessThan(Score(s,free.Position,new TileCoord(1,0))));
            Assert.That(Score(s,guard.Position,new TileCoord(1,0)), Is.LessThan(0));
            Assert.That(Score(s,free.Position,guard.Position), Is.EqualTo(-29));
        }

        [Test]
        public void AlreadyCoveredSupport_DoesNotShuffleBetweenSafeSlots()
        {
            var s = State();
            Unit(s,0,0);
            var target = Unit(s,0,1,"producer");
            Unit(s,1,0);
            Assert.That(Score(s,target.Position,new TileCoord(1,1)), Is.EqualTo(-4));
            Assert.That(Planner(3).GetNextCommand(s), Is.TypeOf<EndTurnCommand>());
        }

        [TestCase(1)]
        [TestCase(3)]
        public void NonProtectiveMovement_LosesToEndTurnAtEverySearchDepth(int depth)
        {
            var s = State();
            Unit(s,0,0);
            Unit(s,1,0);
            Assert.That(Planner(depth).GetNextCommand(s), Is.TypeOf<EndTurnCommand>());
        }

        [Test]
        public void OnceProtected_NextDecisionEndsInsteadOfMovingAgain()
        {
            var s = State();
            Unit(s,0,1,"producer");
            Unit(s,4,0);
            var planner = Planner(3);
            var cmd = planner.GetNextCommand(s) as MoveOccupantCommand;
            Assert.That(cmd, Is.Not.Null);
            new MoveService().Move(s,PlayerId.AI,cmd.From,cmd.To);
            Assert.That(planner.GetNextCommand(s), Is.TypeOf<EndTurnCommand>());
        }

        [Test]
        public void HealthyBuildingWithLittleHp_IsNotWoundedUnit()
        {
            var s=State();
            var b=new BuildingState("b","b",PlayerId.AI,new TileCoord(0,1),false,0,50,new ResourceSet());
            b.CurrentHp=1;
            s.AIBoard.Place(b.Position,b);
            var guard=Unit(s,4,0);
            Assert.That(Score(s,guard.Position,new TileCoord(0,0)), Is.LessThan(0));
        }

        [Test]
        public void MoveAssessment_DoesNotMutateBoardOrPositions()
        {
            var s=State();
            var unit=Unit(s,0,0,"producer");
            var guard=Unit(s,0,1);
            var before=AiBattleStateCopy.PositionKey(s);
            Score(s,unit.Position,guard.Position);
            Assert.That(AiBattleStateCopy.PositionKey(s), Is.EqualTo(before));
        }
    }
}
