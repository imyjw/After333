using System;
using System.Diagnostics;
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
    public sealed class AiOwnMasterHpBandTests
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private static AiDecisionService Planner() => new AiDecisionService(
            new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>()), new TargetingService(),
            ZeroCardUpgradeLevelProvider.Instance,
            new AiSearchOptions { MaxDepth = 3, MaxMilliseconds = 10000, MaxSimulations = 16000 });

        private static BattleState State(int masterHp = 333, int counterHits = 1)
        {
            var deck = Enumerable.Repeat("unknown", 20).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.AI));
            state.SetPhase(PhaseType.Main);
            foreach (var player in new[] { state.Player, state.AI })
            {
                foreach (var id in player.Hand.CardIds.ToArray()) player.Hand.Remove(id);
                player.Resources.Spend(player.Resources.Clone());
                player.Master.RemainingAttacksThisTurn = 0;
            }
            state.AI.Master.CurrentHp = masterHp;
            state.AI.Master.RemainingAttacksThisTurn = 1;
            var miner = new UnitState("miner", "GoldMiner", PlayerId.Player, new TileCoord(0,0),
                AttackType.Melee, 3, 15, true, false, 0, new ResourceSet(0,0,0,1), hitsPerAttack: counterHits);
            miner.HasSummoningSickness = false;
            miner.RemainingAttacksThisTurn = 1;
            state.PlayerBoard.Place(miner.Position, miner);
            return state;
        }

        private static double Evaluate(string method, params object[] arguments)
        {
            var type = typeof(AiDecisionService).Assembly.GetType("Project333.Runtime.Application.Services.AiPositionEvaluator", true);
            return (double)type.GetMethod(method).Invoke(Activator.CreateInstance(type, true), arguments);
        }

        private static object Root(BattleState state)
        {
            var type = typeof(AiDecisionService).GetNestedType("Node", BindingFlags.NonPublic);
            var root = Activator.CreateInstance(type, true);
            type.GetField("States").SetValue(root, new[] { state, state });
            type.GetField("MasterExposureHp").SetValue(root, state.AI.Master.CurrentHp);
            return root;
        }

        private static object Expand(AiDecisionService planner, object parent, IBattleCommand command)
        {
            var actionType = typeof(AiDecisionService).Assembly.GetType("Project333.Runtime.Application.Services.AiAction", true);
            var action = Activator.CreateInstance(actionType, new object[] { 100, new[] { command } });
            var node = typeof(AiDecisionService).GetMethod("Expand", Hidden)
                .Invoke(planner, new[] { parent, action, Stopwatch.StartNew() });
            Assert.That(node, Is.Not.Null, "The shared battle rules must accept the tested command.");
            return node;
        }

        private static T Field<T>(object node, string name) => (T)node.GetType().GetField(name).GetValue(node);

        [TestCase(333,332,-0.8)] [TestCase(250,249,-0.8)]
        [TestCase(249,248,-0.84)] [TestCase(150,149,-0.84)]
        [TestCase(149,148,-1.2)] [TestCase(80,79,-1.2)] [TestCase(79,78,-3.0)]
        [TestCase(250,240,-8.0)] [TestCase(150,140,-8.4)]
        [TestCase(80,70,-12.0)] [TestCase(79,69,-30.0)]
        [TestCase(70,70,0.0)] [TestCase(10,-50,-30.0)] [TestCase(70,80,30.0)]
        public void OwnHpChangeUsesBeforeActionBandActualLossAndOppositeHealingSign(int before, int after, double expected)
        {
            Assert.That(Evaluate("EvaluateOwnMasterHpChange",before,after), Is.EqualTo(expected).Within(0.000001));
        }

        [Test]
        public void PositionValueHasNoOldLinearInverseOrExposureHpPenalty()
        {
            var state = State();
            var before = Evaluate("EvaluateForPlanning",state,333);
            state.AI.Master.CurrentHp = 149;
            Assert.That(Evaluate("EvaluateForPlanning",state,333), Is.EqualTo(before).Within(0.000001));
            // Wounded rear-unit protection remains a separate policy; compare within that role.
            state.AI.Master.CurrentHp = 79;
            var wounded = Evaluate("EvaluateForPlanning",state,333);
            state.AI.Master.CurrentHp = 70;
            Assert.That(Evaluate("EvaluateForPlanning",state,333), Is.EqualTo(wounded).Within(0.000001));
            Assert.That(Evaluate("EvaluateOwnMasterHpChange",333,70), Is.EqualTo(-210.4).Within(0.000001));
        }

        [TestCase(333,0.6)] [TestCase(250,0.6)] [TestCase(249,0.48)] [TestCase(150,0.48)]
        [TestCase(149,-0.6)] [TestCase(80,-0.6)] [TestCase(79,-6.0)]
        public void ActualMeleeCounterattackHasExactBandCostWithoutDuplicatedMasterHpPenalty(int hp, double scoreGain)
        {
            var state = State(hp);
            var planner = Planner();
            var root = Root(state);
            var attack = Expand(planner,root,new AttackCommand(state.AI.Master.Position,new TileCoord(0,0)));
            var end = Expand(planner,root,new EndTurnCommand());
            Assert.That(Field<double>(attack,"Score")-Field<double>(end,"Score"), Is.EqualTo(scoreGain).Within(0.000001));
            Assert.That(Field<BattleState[]>(attack,"States")[0].AI.Master.CurrentHp, Is.EqualTo(hp-3));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(hp), "Planning never mutates the live battle.");
        }

        [TestCase(333,true)] [TestCase(250,true)] [TestCase(249,true)] [TestCase(150,true)]
        [TestCase(149,false)] [TestCase(80,false)] [TestCase(79,false)]
        public void PlannerAttacksTheSurvivingGoldMinerAtHighHpAndReservesLowHpMaster(int hp, bool attacks)
        {
            var state = State(hp);
            var command = Planner().GetNextCommand(state);
            if (attacks)
            {
                Assert.That(command, Is.TypeOf<AttackCommand>());
                Assert.That(((AttackCommand)command).TargetCoord, Is.EqualTo(new TileCoord(0,0)));
            }
            else Assert.That(command, Is.TypeOf<EndTurnCommand>());
        }

        [Test]
        public void MultipleCounterHitsUseTheHpBeforeTheWholeAttack()
        {
            var state = State(250,3);
            var planner = Planner();
            var root = Root(state);
            var attack = Expand(planner,root,new AttackCommand(state.AI.Master.Position,new TileCoord(0,0)));
            var end = Expand(planner,root,new EndTurnCommand());
            Assert.That(Field<BattleState[]>(attack,"States")[0].AI.Master.CurrentHp, Is.EqualTo(241));
            Assert.That(Field<double[]>(attack,"OwnMasterHpScores"), Is.All.EqualTo(-7.2).Within(0.000001));
            Assert.That(Field<double>(attack,"Score")-Field<double>(end,"Score"), Is.EqualTo(-4.2).Within(0.000001));
        }

        [Test]
        public void ForecastFalloutIsWeightedOnceAndBecomesActualOnlyWhenEndingTurn()
        {
            var state = State();
            state.AIBoard.AddFalloutStack();
            var planner = Planner();
            var attack = Expand(planner,Root(state),new AttackCommand(state.AI.Master.Position,new TileCoord(0,0)));
            Assert.That(Field<double[]>(attack,"OwnMasterHpScores"), Is.All.EqualTo(-2.4).Within(0.000001));
            var ended = Expand(planner,attack,new EndTurnCommand());
            Assert.That(Field<double[]>(ended,"OwnMasterHpScores"), Is.All.EqualTo(-10.4).Within(0.000001));
            Assert.That(Field<BattleState[]>(ended,"States")[0].AI.Master.CurrentHp, Is.EqualTo(320));
            Assert.That(Field<double>(ended,"Score")-Field<double>(attack,"Score"), Is.EqualTo(-0.25).Within(0.000001),
                "The same projected damage is not credited twice; only the extra action cost changes.");
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(333));
        }
    }
}
