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
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class AiProducerProtectionTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        private static Type Runtime(string name) => typeof(AiDecisionService).Assembly.GetType(
            "Project333.Runtime.Application.Services." + name, true);

        private static BattleState State(bool invincibleEnemy = true)
        {
            var deck = Enumerable.Repeat("unseen", 24).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.AI));
            state.SetPhase(PhaseType.Main);
            foreach (var p in new[] { state.Player, state.AI })
            {
                foreach (var card in p.Hand.CardIds.ToArray()) p.Hand.Remove(card);
                p.Resources.Spend(p.Resources.Clone());
                p.Master.RemainingAttacksThisTurn = 0;
            }
            if (invincibleEnemy) state.Player.Master.AddInvincibleEffect(InvincibleDurationType.Always, 0, state.TurnNumber, PlayerId.Player);
            return state;
        }

        private static CardDefinition Definition(string id, ResourceSet cost, bool building = false) =>
            building ? (CardDefinition)new BuildingCardDefinition(id, id, cost, false, 0, 100, new ResourceSet(1,0,0,0)) :
                new UnitCardDefinition(id, id, cost, AttackType.Melee, 0, 100, true, false, 0, new ResourceSet(1,0,0,0));
        private static ICardDefinitionProvider Provider(params CardDefinition[] definitions) => new InMemoryCardDefinitionProvider(definitions);
        private static object Policy(ICardDefinitionProvider provider) => Activator.CreateInstance(Runtime("AiProducerProtectionPolicy"),
            Hidden, null, new object[] { provider }, null);
        private static double MoveScore(BattleState state, OccupantState guard, TileCoord to, ICardDefinitionProvider provider) =>
            (double)Runtime("AiMovementPolicy").GetMethod("ScoreUsingCardCosts", Hidden).Invoke(null,
                new object[] { state, new MoveOccupantCommand(guard.Position, to), Policy(provider) });
        private static double LegacyMoveScore(BattleState state, OccupantState guard, TileCoord to) =>
            (double)Runtime("AiMovementPolicy").GetMethod("Score", Hidden).Invoke(null,
                new object[] { state, new MoveOccupantCommand(guard.Position, to) });
        private static double PositionScore(BattleState state, ICardDefinitionProvider provider)
        {
            var type = Runtime("AiPositionEvaluator");
            var instance = Activator.CreateInstance(type, Hidden, null, new[] { Policy(provider) }, null);
            return (double)type.GetMethod("Evaluate", Hidden).Invoke(instance, new object[] { state });
        }
        private static AiDecisionService Planner(ICardDefinitionProvider provider, int depth) => new AiDecisionService(
            provider, new TargetingService(), ZeroCardUpgradeLevelProvider.Instance,
            new AiSearchOptions { MaxDepth = depth, MaxMilliseconds = 10000, MaxSimulations = 6000 });

        private static OccupantState Producer(BattleState state, string id, int col, bool building = false,
            int row = 1, PlayerId owner = PlayerId.AI)
        {
            OccupantState target = building ? (OccupantState)new BuildingState("producer-" + id, id, owner,
                new TileCoord(col,row), false, 0, 100, new ResourceSet(1,0,0,0)) :
                new UnitState("producer-" + id, id, owner, new TileCoord(col,row), AttackType.Melee,
                    0, 100, true, false, 0, new ResourceSet(1,0,0,0));
            target.RemainingAttacksThisTurn = 0;
            state.GetBoard(owner).Place(target.Position, target);
            return target;
        }
        private static UnitState Tank(BattleState state, string id, int col, int hp, int maxHp = 0, int row = 0,
            PlayerId owner = PlayerId.AI)
        {
            var tank = new UnitState(id, id, owner, new TileCoord(col,row), AttackType.Melee,
                0, maxHp == 0 ? hp : maxHp, true, false, 0);
            tank.CurrentHp = hp; tank.RemainingAttacksThisTurn = 0;
            state.GetBoard(owner).Place(tank.Position, tank);
            return tank;
        }

        private static OccupantState Cover(BattleState s,OccupantState producer) => producer.Position.Row==1 ?
            s.GetBoard(producer.OwnerId).GetOccupant(new TileCoord(producer.Position.Column,0)) : null;
        private static void AssertCoveredBy(BattleState s,OccupantState producer,OccupantState guard)
        {
            Assert.That(producer.Position.Row,Is.EqualTo(1));
            Assert.That(Cover(s,producer),Is.SameAs(guard));
            Assert.That(guard.DoesNotBlockFrontRow,Is.False);
        }

        [TestCase(1)]
        [TestCase(3)]
        public void Planner_FirstProtectsDearerUnitWithStrongTankThenRecalculatesCheapBuilding(int depth)
        {
            var s = State();
            var provider = Provider(Definition("dear",new ResourceSet(5,0,0,0)),
                Definition("cheap",new ResourceSet(0,0,1,0),true));
            var dear = Producer(s,"dear",0);
            var cheap = Producer(s,"cheap",1,true);
            var strong = Tank(s,"strong",4,100);
            var weak = Tank(s,"weak",3,40);
            var planner = Planner(provider,depth);
            var before = AiBattleStateCopy.PositionKey(s);
            var first = planner.GetNextCommand(s) as MoveOccupantCommand;
            Assert.That(first,Is.Not.Null);
            Assert.That(AiBattleStateCopy.PositionKey(s),Is.EqualTo(before),"planning changed real state or RNG");
            new MoveService().Move(s,PlayerId.AI,first.From,first.To);
            AssertCoveredBy(s,dear,strong);
            Assert.That(Cover(s,cheap),Is.Null,"first protect the dear producer, not the cheap one");
            var second = planner.GetNextCommand(s) as MoveOccupantCommand;
            Assert.That(second,Is.Not.Null);
            Assert.That(second.From,Is.EqualTo(weak.Position));
            Assert.That(second.To,Is.EqualTo(new TileCoord(cheap.Position.Column,0)));
            new MoveService().Move(s,PlayerId.AI,second.From,second.To);
            AssertCoveredBy(s,dear,strong);
            AssertCoveredBy(s,cheap,weak);
            Assert.That(planner.GetNextCommand(s),Is.TypeOf<EndTurnCommand>(),"do not swap already assigned guards");
            Assert.That(s.AI.Master.Position,Is.EqualTo(new TileCoord(2,1)),"Master is not the highest-HP tank");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PrintedCostOutranksUnitBuildingRole(bool dearIsBuilding)
        {
            var s = State();
            var provider = Provider(Definition("dear",new ResourceSet(5,0,0,0),dearIsBuilding),
                Definition("cheap",new ResourceSet(0,0,1,0),!dearIsBuilding));
            var dear=Producer(s,"dear",0,dearIsBuilding);var cheap=Producer(s,"cheap",1,!dearIsBuilding);
            var guard = Tank(s,"guard",4,100);
            Assert.That(MoveScore(s,guard,new TileCoord(0,0),provider),
                Is.GreaterThan(MoveScore(s,guard,new TileCoord(1,0),provider)));
            var move=Planner(provider,1).GetNextCommand(s) as MoveOccupantCommand;
            Assert.That(move,Is.Not.Null);
            new MoveService().Move(s,PlayerId.AI,move.From,move.To);
            AssertCoveredBy(s,dear,guard);
            Assert.That(Cover(s,cheap),Is.Null);
        }

        [TestCase(5,0,0,0)]
        [TestCase(0,5,0,0)]
        [TestCase(0,0,5,0)]
        [TestCase(0,0,0,5)]
        [TestCase(2,1,1,1)]
        public void AllPrintedResourceTypesHaveSameGoldSubstitutionWeight(int mana,int qi,int power,int gold)
        {
            var s=State();
            var provider=Provider(Definition("left",new ResourceSet(mana,qi,power,gold)),
                Definition("right",new ResourceSet(0,0,0,5),true));
            Producer(s,"left",0);Producer(s,"right",1,true);var guard=Tank(s,"guard",4,80);
            Assert.That(MoveScore(s,guard,new TileCoord(0,0),provider),
                Is.EqualTo(MoveScore(s,guard,new TileCoord(1,0),provider)).Within(.000001));
        }

        [Test]
        public void GuardQualityUsesCurrentHpNotMaximumHpAndCostStepCannotBeReversed()
        {
            var s=State();var provider=Provider(Definition("dear",new ResourceSet(5,0,0,0)),
                Definition("cheap",new ResourceSet(1,0,0,0),true));
            var dear=Producer(s,"dear",0);var cheap=Producer(s,"cheap",1,true);
            var currentStrong=Tank(s,"current-strong",4,100,100);
            var maximumStrong=Tank(s,"max-strong",3,80,240);
            Assert.That(MoveScore(s,currentStrong,new TileCoord(0,0),provider),
                Is.GreaterThan(MoveScore(s,maximumStrong,new TileCoord(0,0),provider)));
            Assert.That(MoveScore(s,maximumStrong,new TileCoord(0,0),provider),
                Is.GreaterThan(MoveScore(s,currentStrong,new TileCoord(1,0),provider)));
            var move=Planner(provider,3).GetNextCommand(s) as MoveOccupantCommand;
            Assert.That(move,Is.Not.Null);
            new MoveService().Move(s,PlayerId.AI,move.From,move.To);
            AssertCoveredBy(s,dear,currentStrong);
            Assert.That(Cover(s,cheap),Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PersistentPlacementPrefersStrongDearWeakCheapPairing(bool enemyBoard)
        {
            var s=State();var owner=enemyBoard?PlayerId.Player:PlayerId.AI;
            var provider=Provider(Definition("dear",new ResourceSet(5,0,0,0)),Definition("cheap",new ResourceSet(1,0,0,0),true));
            Producer(s,"dear",0,owner:owner);Producer(s,"cheap",1,true,owner:owner);
            var weak=Tank(s,"weak",0,40,owner:owner);var strong=Tank(s,"strong",1,100,owner:owner);
            var before=PositionScore(s,provider);
            new MoveService().Move(s,owner,weak.Position,strong.Position);
            var after=PositionScore(s,provider);
            Assert.That(after-before,enemyBoard?Is.EqualTo(0).Within(.000001):Is.GreaterThan(0));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExistingProtectedProducerDoesNotLoseOrExchangeItsGuard(bool unknownProtected)
        {
            var s=State();var provider=Provider(Definition("dear",new ResourceSet(10,0,0,0)),
                Definition("old",new ResourceSet(0,0,0,0),true));
            var oldProducer=Producer(s,unknownProtected?"unregistered":"old",0,true);
            Producer(s,"dear",1);
            var guard=Tank(s,"guard",0,100);
            Assert.That(MoveScore(s,guard,new TileCoord(1,0),provider),Is.LessThanOrEqualTo(0));
            var command=Planner(provider,3).GetNextCommand(s);
            if(command is MoveOccupantCommand move)new MoveService().Move(s,PlayerId.AI,move.From,move.To);
            else Assert.That(command,Is.TypeOf<EndTurnCommand>());
            AssertCoveredBy(s,oldProducer,guard);
            Assert.That(guard.Position,Is.EqualTo(new TileCoord(0,0)),"existing guard must not be stolen");
        }

        [Test]
        public void NewTankCannotStealAlreadyAssignedCoverForHpUpgrade()
        {
            var s=State();var provider=Provider(Definition("dear",new ResourceSet(5,0,0,0)));
            Producer(s,"dear",0);var weak=Tank(s,"weak",0,40);var strong=Tank(s,"strong",4,100);
            Assert.That(MoveScore(s,strong,weak.Position,provider),Is.LessThan(0));
            Assert.That(Planner(provider,3).GetNextCommand(s),Is.TypeOf<EndTurnCommand>());
        }

        [TestCase("missing",false,46)]
        [TestCase("missing",true,56)]
        [TestCase("negative",false,46)]
        [TestCase("negative",true,56)]
        public void UnknownOrInvalidPrintedCostPreservesExactLegacyScore(string id,bool building,double expected)
        {
            var s=State();var provider=Provider(Definition("negative",new ResourceSet(-1,0,0,0),building));
            Producer(s,id,0,building);var tank=Tank(s,"guard",4,100);
            Assert.That(MoveScore(s,tank,new TileCoord(0,0),provider),Is.EqualTo(expected));
            Assert.That(LegacyMoveScore(s,tank,new TileCoord(0,0)),Is.EqualTo(expected));
        }

        [TestCase("drained")]
        [TestCase("seal")]
        [TestCase("freeze")]
        [TestCase("hiding")]
        [TestCase("flying")]
        public void NonBlockingTankCannotCreateProductionProtection(string status)
        {
            var s=State();var provider=Provider(Definition("dear",new ResourceSet(5,0,0,0)));
            var target=Producer(s,"dear",4,row:0);
            var guard=new UnitState("guard","guard",PlayerId.AI,new TileCoord(0,0),AttackType.Melee,0,100,
                true,false,0,hasFlying:status=="flying",hasHiding:status=="hiding");
            if(status=="drained")guard.IsDrained=true;
            if(status=="seal")guard.EnterSealbound(3);
            if(status=="freeze")guard.ApplyFreeze(3);
            s.AIBoard.Place(guard.Position,guard);
            Assert.That(MoveScore(s,target,new TileCoord(0,1),provider),Is.LessThan(0));
        }

        [Test]
        public void ErasureRemovesCostProtectionRole()
        {
            var s=State();var provider=Provider(Definition("dear",new ResourceSet(5,0,0,0)));
            var producer=Producer(s,"dear",0,true);var guard=Tank(s,"guard",4,100);
            Assert.That(MoveScore(s,guard,new TileCoord(0,0),provider),Is.GreaterThan(0));
            producer.ApplyErasure();
            Assert.That(MoveScore(s,guard,new TileCoord(0,0),provider),Is.LessThan(0));
        }

        [Test]
        public void CostSummationDoesNotOverflowAndImmediateWinStillBeatsProtection()
        {
            var s=State(false);var provider=Provider(Definition("huge",new ResourceSet(int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue)));
            Producer(s,"huge",0);Tank(s,"guard",4,100);
            var killer=new UnitState("killer","killer",PlayerId.AI,new TileCoord(3,0),AttackType.Ranged,100,100,true,false,0);
            killer.HasSummoningSickness=false;killer.RemainingAttacksThisTurn=1;s.AIBoard.Place(killer.Position,killer);
            s.Player.Master.CurrentHp=1;
            Assert.That(MoveScore(s,s.AIBoard.GetOccupant(new TileCoord(4,0)),new TileCoord(0,0),provider),Is.InRange(0,100));
            Assert.That(Planner(provider,3).GetNextCommand(s),Is.TypeOf<AttackCommand>());
        }


        [TestCase(1)]
        [TestCase(3)]
        public void LimitedSimulationBudgetStillInspectsDearStrongPairFirst(int depth)
        {
            var s=State();var provider=Provider(Definition("dear",new ResourceSet(5,0,0,0)),
                Definition("cheap",new ResourceSet(1,0,0,0),true));
            var dear=Producer(s,"dear",4);var cheap=Producer(s,"cheap",0,true);
            var weak=Tank(s,"weak",1,40);var strong=Tank(s,"strong",3,100);
            var planner=new AiDecisionService(provider,new TargetingService(),ZeroCardUpgradeLevelProvider.Instance,
                new AiSearchOptions{MaxDepth=depth,MaxMilliseconds=10000,MaxSimulations=8});
            var command=planner.GetNextCommand(s) as MoveOccupantCommand;
            Assert.That(command,Is.Not.Null);
            new MoveService().Move(s,PlayerId.AI,command.From,command.To);
            AssertCoveredBy(s,dear,strong);Assert.That(Cover(s,cheap),Is.Null);
            Assert.That(planner.LastSimulationCount,Is.LessThanOrEqualTo(8));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ProductionFocusEndsWhenNearSnapshotErasesOrSealsProducer(bool sealedTarget)
        {
            var s=State();var provider=Provider(Definition("dear",new ResourceSet(5,0,0,0)));
            var producer=Producer(s,"dear",0);var guard=Tank(s,"guard",4,100);
            var policy=Policy(provider);var move=new MoveOccupantCommand(guard.Position,new TileCoord(0,0));
            var focus=Runtime("AiProducerProtectionPolicy").GetMethod("CreateFocus",Hidden).Invoke(policy,new object[]{s,move});
            var near=AiBattleStateCopy.Create(s);new MoveService().Move(near,PlayerId.AI,move.From,move.To);
            var target=near.AIBoard.GetOccupant(producer.Position);
            if(sealedTarget)target.EnterSealbound(3);else target.ApplyErasure();
            var valid=(bool)Runtime("AiProducerProtectionPolicy").GetMethod("IsValidFocus",Hidden)
                .Invoke(null,new object[]{focus,near.AIBoard,null});
            Assert.That(valid,Is.False);
        }

        [Test]
        public void MetadataIsBoundToProviderAndCostDoesNotReadHeldResources()
        {
            var s=State();Producer(s,"left",0);Producer(s,"right",1,true);var guard=Tank(s,"guard",4,100);
            var leftDear=Provider(Definition("left",new ResourceSet(5,0,0,0)),Definition("right",new ResourceSet(1,0,0,0),true));
            var rightDear=Provider(Definition("left",new ResourceSet(1,0,0,0)),Definition("right",new ResourceSet(0,0,0,5),true));
            var before=MoveScore(s,guard,new TileCoord(0,0),leftDear);
            s.AI.Resources.Add(new ResourceSet(100,100,100,100));
            Assert.That(MoveScore(s,guard,new TileCoord(0,0),leftDear),Is.EqualTo(before));
            Assert.That(before,Is.GreaterThan(MoveScore(s,guard,new TileCoord(1,0),leftDear)));
            Assert.That(MoveScore(s,guard,new TileCoord(0,0),rightDear),
                Is.LessThan(MoveScore(s,guard,new TileCoord(1,0),rightDear)));
        }

        [Test]
        public void KnownProducerDoesNotReceiveNearCoverFromGuardKilledByRealBomb()
        {
            var s=State();var provider=Provider(Definition("dear",new ResourceSet(5,0,0,0),true));
            var dear=Producer(s,"dear",0,true);var guard=Tank(s,"guard",4,25);
            s.PersistentEffects.Add(new PersistentEffectState(BiochemicalBombRules.CardId,PlayerId.Player,
                BiochemicalBombRules.EffectId,s.TurnNumber,"four global starts",new ResourceSet(),
                remainingTriggers:4,effectDamage:25,effectDamageType:DamageType.Fixed,targetStartColumn:0));
            var move=new MoveOccupantCommand(guard.Position,new TileCoord(0,0));
            var focus=Runtime("AiProducerProtectionPolicy").GetMethod("CreateFocus",Hidden)
                .Invoke(Policy(provider),new object[]{s,move});
            var near=ProjectToEnemyMain(s,move);
            Assert.That(near.AIBoard.GetOccupant(move.To),Is.Null);
            Assert.That((bool)Runtime("AiProducerProtectionPolicy").GetMethod("IsValidFocus",Hidden)
                .Invoke(null,new object[]{focus,near.AIBoard,null}),Is.False);
            var chosen=Planner(provider,1).GetNextCommand(s);
            Assert.That(chosen is MoveOccupantCommand chosenMove && chosenMove.From==guard.Position,Is.False,
                "a high printed cost must not reward a guard destroyed before enemy main");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void KnownProductionFocusForecastKeepsLateDoomedGuardButRejectsLateDoomedTarget(bool targetDies)
        {
            var s=State();var provider=Provider(Definition("dear",new ResourceSet(5,0,0,0)));
            var target=Producer(s,"dear",0);target.CurrentHp=targetDies?20:100;
            var guard=Tank(s,"guard",4,targetDies?100:20);
            var dragon=new UnitState("dragon","RedDragon",PlayerId.Player,new TileCoord(4,1),AttackType.Melee,
                0,100,true,false,0);s.PlayerBoard.Place(dragon.Position,dragon);
            var move=new MoveOccupantCommand(guard.Position,new TileCoord(0,0));
            var focus=Runtime("AiProducerProtectionPolicy").GetMethod("CreateFocus",Hidden)
                .Invoke(Policy(provider),new object[]{s,move});
            var near=ProjectToEnemyMain(s,move);var future=AiBattleStateCopy.Create(near);
            new EndTurnService(new Random(43)).EndTurn(future);
            Assert.That((bool)Runtime("AiProducerProtectionPolicy").GetMethod("IsValidFocus",Hidden)
                .Invoke(null,new object[]{focus,near.AIBoard,future.AIBoard}),Is.EqualTo(!targetDies));
            var evaluated=(double)Runtime("AiPositionEvaluator").GetMethod("EvaluateWithSurvivalForecast",Hidden)
                .Invoke(Activator.CreateInstance(Runtime("AiPositionEvaluator"),Hidden,null,new[]{Policy(provider)},null),
                    new object[]{near,near,future.AIBoard});
            Assert.That(double.IsNaN(evaluated)||double.IsInfinity(evaluated),Is.False);
        }

        private static BattleState ProjectToEnemyMain(BattleState s,MoveOccupantCommand move)
        {
            var near=AiBattleStateCopy.Create(s);new MoveService().Move(near,PlayerId.AI,move.From,move.To);
            new EndTurnService(new Random(41)).EndTurn(near);
            new TurnStartService(new ScienceUpkeepService()).ResolveTurnStart(near);return near;
        }

        [Test]
        public void PlanningWithKnownMetadataPreservesStateRngAndIndependentCopies()
        {
            var s=State();var provider=Provider(Definition("dear",new ResourceSet(5,0,0,0)));
            Producer(s,"dear",0);Tank(s,"guard",4,100);
            var key=AiBattleStateCopy.PositionKey(s);
            var planner=Planner(provider,3);planner.GetNextCommand(s);
            Assert.That(AiBattleStateCopy.PositionKey(s),Is.EqualTo(key));
            var copy=AiBattleStateCopy.Create(s);
            Assert.That(PositionScore(copy,provider),Is.EqualTo(PositionScore(s,provider)).Within(.000001));
        }
    }
}
