using System;
using System.Collections;
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
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class AiTrapTests
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly TileCoord MasterCoord = new TileCoord(2, 1);
        private static Type Runtime(string name) => typeof(AiDecisionService).Assembly.GetType(
            "Project333.Runtime.Application.Services." + name, true);
        private static BattleState State()
        {
            var playerMaster = new MasterState("player-master", PlayerId.Player, MasterCoord, 3, 333);
            var aiMaster = new MasterState("ai-master", PlayerId.AI, MasterCoord, 3, 333);
            var player = new PlayerState(PlayerId.Player, new ResourceSet(),
                new DeckState(Enumerable.Repeat("unknown-enemy-card", 24)), new HandState(), new DiscardState(), playerMaster);
            var ai = new PlayerState(PlayerId.AI, new ResourceSet(),
                new DeckState(Enumerable.Repeat(TrapReturnCardRules.CardId, 24)), new HandState(), new DiscardState(), aiMaster);
            var playerBoard = new BoardState(); playerBoard.Place(MasterCoord, playerMaster);
            var aiBoard = new BoardState(); aiBoard.Place(MasterCoord, aiMaster);
            var state = new BattleState(player, ai, playerBoard, aiBoard);
            state.RestoreRuntimeState(5, PlayerId.AI, PhaseType.Main);
            playerMaster.RemainingAttacksThisTurn = aiMaster.RemainingAttacksThisTurn = 0;
            return state;
        }
        private static ArmedTrapState Armed(PlayerId owner, string cardId, int gold = 4,
            string runtimeId = "trap", int installedTurn = 3, int level = 0, bool temporary = false) =>
            new ArmedTrapState(runtimeId, owner, cardId, TrapTriggerType.EnemyUnitAttacksMaster,
                TrapEffectType.ReturnAttackerToHand, new ResourceSet(0, 0, 0, gold), installedTurn, level, temporary);
        private static ICardDefinitionProvider Provider(params CardDefinition[] definitions) => new InMemoryCardDefinitionProvider(definitions);
        private static AiDecisionService Planner(ICardDefinitionProvider provider, int depth = 1) =>
            new AiDecisionService(provider, new TargetingService(), ZeroCardUpgradeLevelProvider.Instance,
                new AiSearchOptions { MaxDepth = depth, MaxSimulations = 6000, MaxMilliseconds = 10000 });
        private static IBattleCommand[] Candidates(BattleState state, ICardDefinitionProvider provider)
        {
            var type = Runtime("AiActionGenerator");
            var generator = Activator.CreateInstance(type, Any, null,
                new object[] { provider, new TargetingService() }, null);
            var actions = (IEnumerable)type.GetMethod("Generate", Any).Invoke(generator, new object[] { state });
            return actions.Cast<object>().SelectMany(action =>
                (IBattleCommand[])action.GetType().GetField("Commands", Any).GetValue(action)).ToArray();
        }
        private static double Evaluate(BattleState state)
        {
            var type = Runtime("AiPositionEvaluator");
            var evaluator = Activator.CreateInstance(type, true);
            return (double)type.GetMethod("Evaluate", Any).Invoke(evaluator, new object[] { state });
        }
        private static BattleCommandProcessor Processor(ICardDefinitionProvider provider) =>
            new BattleCommandProcessor(new PlayCardService(provider), new SpellService(provider),
                new MoveService(), new AttackService(), new EndTurnService());
        private static BattleState Simulate(BattleState state, ICardDefinitionProvider provider,
            IBattleCommand command, int handIndex = -1)
        {
            var actionType = Runtime("AiAction");
            var action = Activator.CreateInstance(actionType, Any, null,
                new object[] { 100, new IBattleCommand[] { command } }, null);
            actionType.GetField("HandIndex", Any).SetValue(action, handIndex);
            var simulationType = Runtime("AiBattleSimulation");
            var simulation = Activator.CreateInstance(simulationType, Any, null,
                new object[] { provider, ZeroCardUpgradeLevelProvider.Instance }, null);
            return (BattleState)simulationType.GetMethod("Apply", Any).Invoke(simulation, new object[] { state, action, 0 });
        }

        [Test]
        public void ObservationCopiesOnlyOwnSecretsAndPreservesEnemyCountAcrossRepeatedCopies()
        {
            var source = State();
            source.Player.Hand.Add("secret-hand-card", "private-hand", false);
            source.Player.RestoreHiddenTrapCount(1);
            source.ArmedTraps.Add(Armed(PlayerId.Player, "enemy-secret-a", 99, "enemy-a"));
            source.ArmedTraps.Add(Armed(PlayerId.AI, "own-a", 4, "own-a", 2, 0, true));
            source.ArmedTraps.Add(Armed(PlayerId.Player, "enemy-secret-b", 1, "enemy-b"));
            source.ArmedTraps.Add(Armed(PlayerId.AI, "own-b", 2, "own-b", 4));
            source.RecordTrapEvent(new BattleTrapEvent(BattleTrapEventKind.Triggered, PlayerId.Player, 3,
                "must-not-replay", "secret-event"));
            var observation = AiBattleStateCopy.Create(source, true);
            Assert.That(observation.ArmedTraps.Select(trap => trap.CardId), Is.EqualTo(new[] { "own-a", "own-b" }));
            Assert.That(observation.ArmedTraps.All(trap => trap.OwnerId == PlayerId.AI), Is.True);
            Assert.That(observation.GetArmedTrapCount(PlayerId.Player), Is.EqualTo(3));
            Assert.That(observation.GetArmedTrapCount(PlayerId.AI), Is.EqualTo(2));
            Assert.That(observation.Player.Hand.Cards.Single().CardId, Is.EqualTo(AiBattleStateCopy.UnknownCardId));
            Assert.That(observation.ArmedTraps[0], Is.Not.SameAs(source.ArmedTraps[1]));
            Assert.That(observation.ArmedTraps[0].RuntimeId, Is.EqualTo("own-a"));
            Assert.That(observation.ArmedTraps[0].InstalledTurn, Is.EqualTo(2));
            Assert.That(observation.ArmedTraps[0].IsTemporaryReplicate, Is.True);
            Assert.That(observation.ArmedTraps[0].ActivationCost.Gold, Is.EqualTo(4));
            Assert.That(observation.TrapEvents, Is.Empty);
            foreach (var repeated in new[] { AiBattleStateCopy.Create(observation), AiBattleStateCopy.Create(observation, true) })
            {
                Assert.That(repeated.ArmedTraps.Select(trap => trap.CardId), Is.EqualTo(new[] { "own-a", "own-b" }));
                Assert.That(repeated.GetArmedTrapCount(PlayerId.Player), Is.EqualTo(3));
                Assert.That(repeated.TrapEvents, Is.Empty);
                Assert.That(AiBattleStateCopy.PositionKey(repeated), Is.EqualTo(AiBattleStateCopy.PositionKey(observation)));
            }
            observation.AI.Resources.Add(new ResourceSet(0, 0, 0, 10));
            observation.ArmedTraps.RemoveAt(0);
            Assert.That(source.ArmedTraps.Count, Is.EqualTo(4));
            Assert.That(source.AI.Resources.Gold, Is.Zero);
            Assert.That(source.TrapEvents.Count, Is.EqualTo(1));
        }

        [Test]
        public void FullCopyKeepsCapturedOrderAndHiddenCountsWithoutReplayingEvents()
        {
            var source = State();
            source.Player.RestoreHiddenTrapCount(2);
            source.ArmedTraps.Add(Armed(PlayerId.AI, "first", 4, "a", 1));
            source.ArmedTraps.Add(Armed(PlayerId.Player, "opponent", 3, "b", 2));
            source.ArmedTraps.Add(Armed(PlayerId.AI, "second", 1, "c", 3));
            source.RecordTrapEvent(new BattleTrapEvent(BattleTrapEventKind.Installed, PlayerId.AI, 2));
            var copy = AiBattleStateCopy.Create(source);
            Assert.That(copy.ArmedTraps.Select(trap => trap.RuntimeId), Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(copy.ArmedTraps.Select(trap => trap.ActivationCost.Gold), Is.EqualTo(new[] { 4, 3, 1 }));
            Assert.That(copy.GetArmedTrapCount(PlayerId.Player), Is.EqualTo(3));
            Assert.That(copy.GetArmedTrapCount(PlayerId.AI), Is.EqualTo(2));
            Assert.That(copy.ArmedTraps[1], Is.Not.SameAs(source.ArmedTraps[1]));
            Assert.That(copy.TrapEvents, Is.Empty);
        }

        [Test]
        public void PositionKeyUsesOwnOrderAndCapturedCostButIgnoresRuntimeGuidsAndEventQueues()
        {
            var state = State();
            state.ArmedTraps.Add(Armed(PlayerId.AI, "first", 4, "guid-a", 2));
            state.ArmedTraps.Add(Armed(PlayerId.AI, "second", 1, "guid-b", 3));
            var baseline = AiBattleStateCopy.PositionKey(state);
            state.ArmedTraps[0] = Armed(PlayerId.AI, "first", 4, "fresh-guid", 2);
            state.RecordTrapEvent(new BattleTrapEvent(BattleTrapEventKind.Triggered, PlayerId.AI, 2, "revealed", "event-guid"));
            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(baseline));
            state.ArmedTraps.Reverse();
            Assert.That(AiBattleStateCopy.PositionKey(state), Is.Not.EqualTo(baseline), "The first affordable trap is order-sensitive.");
            state.ArmedTraps.Reverse();
            state.ArmedTraps[0] = Armed(PlayerId.AI, "first", 3, "fresh-guid", 2);
            Assert.That(AiBattleStateCopy.PositionKey(state), Is.Not.EqualTo(baseline));
        }

        [TestCase(1)]
        [TestCase(3)]
        public void SwappingEnemySecretIdentityAndCostCannotChangeDecisionOrConsumeAuthoritativeStateOrRng(int depth)
        {
            var first = State();
            first.AI.Hand.Add(TrapReturnCardRules.CardId, "own-hand", false);
            first.ArmedTraps.Add(Armed(PlayerId.Player, "hidden-a", 999, "private-a", 1, 13, true));
            var second = AiBattleStateCopy.Create(first);
            second.ArmedTraps[0] = Armed(PlayerId.Player, "hidden-b", 0, "private-b", 5);
            Assert.That(AiBattleStateCopy.PositionKey(first), Is.EqualTo(AiBattleStateCopy.PositionKey(second)));
            var beforeFirst = AiBattleStateCopy.PositionKey(first);
            var beforeSecond = AiBattleStateCopy.PositionKey(second);
            var firstDeck = first.AI.Deck.CardIds.ToArray();
            var randomState = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(777);
                var beforeRandom = UnityEngine.Random.state;
                var provider = Provider(TrapReturnCardRules.CreateDefinition());
                var a = Planner(provider, depth).GetNextCommand(first) as InstallTrapCommand;
                var b = Planner(provider, depth).GetNextCommand(second) as InstallTrapCommand;
                Assert.That(a, Is.Not.Null);
                Assert.That(b, Is.Not.Null);
                Assert.That(a.CardId, Is.EqualTo(b.CardId));
                Assert.That(a.HandCardRuntimeId, Is.EqualTo(b.HandCardRuntimeId).And.EqualTo("own-hand"));
                Assert.That(UnityEngine.Random.state, Is.EqualTo(beforeRandom));
                Assert.That(AiBattleStateCopy.PositionKey(first), Is.EqualTo(beforeFirst));
                Assert.That(AiBattleStateCopy.PositionKey(second), Is.EqualTo(beforeSecond));
                Assert.That(first.AI.Deck.CardIds, Is.EqualTo(firstDeck));
                Assert.That(first.Counters.ActionSequence, Is.Zero);
                Assert.That(first.TrapEvents, Is.Empty);
                Assert.That(first.ArmedTraps.Single().CardId, Is.EqualTo("hidden-a"));
                Assert.That(first.AI.Hand.Cards.Single().RuntimeId, Is.EqualTo("own-hand"));
            }
            finally { UnityEngine.Random.state = randomState; }
        }

        [TestCase(1)]
        [TestCase(3)]
        public void PlannerInstallsWithoutActivationGoldThenRejectsDuplicateArming(int depth)
        {
            var state = State();
            state.AI.Hand.Add(TrapReturnCardRules.CardId, "selected", false);
            state.AI.Hand.Add(TrapReturnCardRules.CardId, "duplicate", false);
            var provider = Provider(TrapReturnCardRules.CreateDefinition());
            var planner = Planner(provider, depth);
            var install = planner.GetNextCommand(state) as InstallTrapCommand;
            Assert.That(install, Is.Not.Null);
            Assert.That(install.HandCardRuntimeId, Is.EqualTo("selected"));
            Processor(provider).Execute(state, PlayerId.AI, install);
            Assert.That(state.AI.Resources.Gold, Is.Zero);
            Assert.That(state.ArmedTraps.Single().RuntimeId, Is.EqualTo("selected"));
            Assert.That(state.AI.Hand.Cards.Single().RuntimeId, Is.EqualTo("duplicate"));
            Assert.That(Candidates(state, provider).OfType<InstallTrapCommand>(), Is.Empty);
            Assert.That(planner.GetNextCommand(state), Is.TypeOf<EndTurnCommand>());
        }

        [Test]
        public void PlannerMayReinstallAfterItsTrapWasConsumedOnTheOpponentsAcceptedAttack()
        {
            var state = State();
            var provider = Provider(TrapReturnCardRules.CreateDefinition());
            state.ArmedTraps.Add(Armed(PlayerId.AI, TrapReturnCardRules.CardId, 4, "consumed"));
            state.AI.Hand.Add(TrapReturnCardRules.CardId, "replacement", false);
            state.AI.Resources.Add(new ResourceSet(0, 0, 0, 4));
            var attacker = new UnitState("player-attacker", "Golem", PlayerId.Player, new TileCoord(0, 0),
                AttackType.Ranged, 30, 30, true, false, 0);
            attacker.HasSummoningSickness = false; attacker.RemainingAttacksThisTurn = 1;
            state.PlayerBoard.Place(attacker.Position, attacker);
            state.SetActivePlayer(PlayerId.Player);
            new AttackService().Attack(state, PlayerId.Player, attacker.Position, MasterCoord);
            Assert.That(state.ArmedTraps, Is.Empty);
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
            state.SetActivePlayer(PlayerId.AI);
            var command = Planner(provider).GetNextCommand(state) as InstallTrapCommand;
            Assert.That(command, Is.Not.Null);
            Assert.That(command.HandCardRuntimeId, Is.EqualTo("replacement"));
            Processor(provider).Execute(state, PlayerId.AI, command);
            Assert.That(state.ArmedTraps.Single().RuntimeId, Is.EqualTo("replacement"));
            Assert.That(state.AI.Resources.Gold, Is.Zero);
            Assert.That(state.AI.Discard.CardIds, Is.EqualTo(new[] { TrapReturnCardRules.CardId }));
        }

        [TestCase(0, 8)]
        [TestCase(4, 28)]
        public void KnownOwnTrapUtilityIsBoundedAndEnemySecretMetadataAddsNoInventedValue(int gold, int expectedUtility)
        {
            var state = State();
            state.AI.Resources.Add(new ResourceSet(0, 0, 0, gold));
            var threat = new UnitState("visible-threat", "Golem", PlayerId.Player, new TileCoord(0, 0),
                AttackType.Ranged, 100, 100, true, false, 0);
            state.PlayerBoard.Place(threat.Position, threat);
            var baseline = Evaluate(state);
            state.ArmedTraps.Add(Armed(PlayerId.Player, "private-enemy", 999));
            Assert.That(Evaluate(state), Is.EqualTo(baseline), "Only the public count exists; do not guess the enemy effect.");
            state.ArmedTraps.Add(Armed(PlayerId.AI, TrapReturnCardRules.CardId));
            Assert.That(Evaluate(state) - baseline, Is.EqualTo(expectedUtility).Within(0.000001));
        }

        [Test]
        public void SimulatedAcceptedReturnCancelsDamageWithoutChangingTheSourceBattle()
        {
            var state = State();
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 4));
            state.ArmedTraps.Add(Armed(PlayerId.Player, TrapReturnCardRules.CardId));
            var attacker = new UnitState("ai-attacker", "Golem", PlayerId.AI, new TileCoord(0, 0),
                AttackType.Ranged, 50, 50, true, false, 0);
            attacker.HasSummoningSickness = false; attacker.RemainingAttacksThisTurn = 1;
            state.AIBoard.Place(attacker.Position, attacker);
            var before = AiBattleStateCopy.PositionKey(state);
            var after = Simulate(state, Provider(TrapReturnCardRules.CreateDefinition()),
                new AttackCommand(attacker.Position, MasterCoord));
            Assert.That(after, Is.Not.Null);
            Assert.That(after.LastAttackResolution.WasCancelledByTrap, Is.True);
            Assert.That(after.Player.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(after.AIBoard.GetOccupant(attacker.Position), Is.Null);
            Assert.That(after.AI.Hand.CardIds, Is.EqualTo(new[] { "Golem" }));
            Assert.That(after.TrapEvents.Single().Kind, Is.EqualTo(BattleTrapEventKind.Triggered));
            Assert.That(after.ValuePopupEvents, Is.Empty);
            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(before));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(4));
            Assert.That(state.AIBoard.GetOccupant(attacker.Position), Is.SameAs(attacker));
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(state.TrapEvents, Is.Empty);
        }

        [Test]
        public void GeneratedInstallCommandsRetainExactOrdinaryAndTemporaryHandInstanceIds()
        {
            var state = State();
            state.AI.Hand.Add(TrapReturnCardRules.CardId, "ordinary", false);
            state.AI.Hand.Add(TrapReturnCardRules.CardId, "temporary", true);
            var commands = Candidates(state, Provider(TrapReturnCardRules.CreateDefinition())).OfType<InstallTrapCommand>().ToArray();
            Assert.That(commands.Select(command => command.HandCardRuntimeId), Is.EqualTo(new[] { "ordinary", "temporary" }));
            Assert.That(commands.All(command => command.CardId == TrapReturnCardRules.CardId), Is.True);
            Assert.That(state.AI.Hand.Count, Is.EqualTo(2));
        }

        [Test]
        public void ScenarioRemappingInstallsTheReplicatedCardWithItsActualScenarioGuid()
        {
            var state = State();
            var definition = new TrapSpellCardDefinition("replicated-trap", "replicated-trap", new ResourceSet(0, 0, 0, 4),
                TrapTriggerType.EnemyUnitAttacksMaster, TrapEffectType.ReturnAttackerToHand, true);
            state.AI.Hand.Add(definition.CardId, "actual-scenario-guid", true);
            var after = Simulate(state, Provider(definition), new InstallTrapCommand(definition.CardId, "other-scenario-guid"), 0);
            Assert.That(after, Is.Not.Null);
            Assert.That(after.ArmedTraps.Single().RuntimeId, Is.EqualTo("actual-scenario-guid"));
            Assert.That(after.ArmedTraps.Single().IsTemporaryReplicate, Is.True);
            Assert.That(after.AI.Resources.Gold, Is.Zero);
            Assert.That(after.TrapEvents.Single().CardId, Is.Empty);
            Assert.That(state.ArmedTraps, Is.Empty);
            Assert.That(state.AI.Hand.Cards.Single().RuntimeId, Is.EqualTo("actual-scenario-guid"));
        }

        private static ArmedTrapState Mirror(PlayerId owner, string runtimeId = "mirror") =>
            new ArmedTrapState(runtimeId, owner, TrapMirrorFieldCardRules.CardId,
                TrapTriggerType.EnemyUnitAttacksUnit, TrapEffectType.ReflectAttackerAttackAsMagic,
                new ResourceSet(2, 0, 0, 0), 3);

        [TestCase(1)]
        [TestCase(3)]
        public void PlannerInstallsMirrorFreeWithoutActivationResourcesAndKeepsEnemyIdentityHidden(int depth)
        {
            var first = State();
            first.AI.Hand.Add(TrapMirrorFieldCardRules.CardId, "selected-mirror", false);
            first.ArmedTraps.Add(Mirror(PlayerId.Player, "enemy-private-mirror"));
            var second = AiBattleStateCopy.Create(first);
            second.ArmedTraps[0] = Armed(PlayerId.Player, "different-secret", 999, "different-private-id");
            var provider = Provider(TrapMirrorFieldCardRules.CreateDefinition());
            var before = AiBattleStateCopy.PositionKey(first);
            var a = Planner(provider, depth).GetNextCommand(first) as InstallTrapCommand;
            var b = Planner(provider, depth).GetNextCommand(second) as InstallTrapCommand;
            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.Not.Null);
            Assert.That(a.CardId, Is.EqualTo(TrapMirrorFieldCardRules.CardId));
            Assert.That(a.HandCardRuntimeId, Is.EqualTo(b.HandCardRuntimeId).And.EqualTo("selected-mirror"));
            Assert.That(AiBattleStateCopy.PositionKey(first), Is.EqualTo(before));
            Processor(provider).Execute(first, PlayerId.AI, a);
            Assert.That(first.AI.Resources.Mana, Is.Zero);
            Assert.That(first.AI.Resources.Gold, Is.Zero);
            Assert.That(first.ArmedTraps.Last().ActivationCost.Mana, Is.EqualTo(2));
            Assert.That(Candidates(first, provider).OfType<InstallTrapCommand>(), Is.Empty);
        }

        [TestCase(0, 0, false)]
        [TestCase(1, 0, false)]
        [TestCase(2, 0, true)]
        [TestCase(1, 1, true)]
        [TestCase(0, 2, true)]
        public void KnownMirrorUtilityUsesManaOrGoldAndRemainsBounded(int mana, int gold, bool affordable)
        {
            var state = State();
            state.AI.Resources.Add(new ResourceSet(mana, 0, 0, gold));
            var threat = new UnitState("mirror-threat", "Golem", PlayerId.Player, new TileCoord(0, 0),
                AttackType.Ranged, 100, 100, true, false, 0);
            state.PlayerBoard.Place(threat.Position, threat);
            var baseline = Evaluate(state);
            state.ArmedTraps.Add(Mirror(PlayerId.Player, "enemy-secret"));
            Assert.That(Evaluate(state), Is.EqualTo(baseline), "An enemy secret may not be assigned a known effect.");
            state.ArmedTraps.Add(Mirror(PlayerId.AI));
            var utility = Evaluate(state) - baseline;
            Assert.That(utility, Is.EqualTo(affordable ? 28 : 8).Within(0.000001));
            threat.IsDrained = true;
            state.Player.Master.IsDrained = true;
            state.ArmedTraps.RemoveAt(state.ArmedTraps.Count - 1);
            var drainedBaseline = Evaluate(state);
            state.ArmedTraps.Add(Mirror(PlayerId.AI));
            Assert.That(Evaluate(state) - drainedBaseline, Is.EqualTo(8).Within(0.000001), "An incapacitated threat cannot retain its attack utility.");
        }

        [Test]
        public void SimulatedMirrorCancelsAndReflectsMagicOnceWithoutMutatingSource()
        {
            var state = State();
            state.Player.Resources.Add(new ResourceSet(1, 0, 0, 1));
            state.ArmedTraps.Add(Mirror(PlayerId.Player));
            var attacker = new UnitState("mirror-ai-attacker", "Golem", PlayerId.AI, new TileCoord(0, 0),
                AttackType.Ranged, 30, 50, true, false, 0, magicDefense: 5);
            attacker.WasSummonedThisTurn = false; attacker.HasSummoningSickness = false;
            attacker.RemainingAttacksThisTurn = 1;
            state.AIBoard.Place(attacker.Position, attacker);
            var before = AiBattleStateCopy.PositionKey(state);
            var after = Simulate(state, Provider(TrapMirrorFieldCardRules.CreateDefinition()),
                new AttackCommand(attacker.Position, MasterCoord));
            Assert.That(after.LastAttackResolution.WasCancelledByTrap, Is.True);
            Assert.That(after.Player.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(after.AIBoard.GetOccupant(attacker.Position).CurrentHp, Is.EqualTo(25));
            Assert.That(after.Player.Resources.Mana, Is.Zero);
            Assert.That(after.Player.Resources.Gold, Is.Zero);
            Assert.That(after.ArmedTraps, Is.Empty);
            Assert.That(after.ValuePopupEvents.Count, Is.EqualTo(1));
            Assert.That(after.ValuePopupEvents.Single().DamageType, Is.EqualTo(DamageType.Magic));
            Assert.That(after.ValuePopupEvents.Single().SourceCardId, Is.EqualTo(TrapMirrorFieldCardRules.CardId));
            Assert.That(after.AI.Hand.Count, Is.Zero, "Reflection is not a return-to-hand effect.");
            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(before));
            Assert.That(attacker.CurrentHp, Is.EqualTo(50));
            Assert.That(state.Player.Resources.Mana, Is.EqualTo(1));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(1));
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
        }

        private static ArmedTrapState ReactiveArmour(PlayerId owner, string runtimeId = "reactive") =>
            new ArmedTrapState(runtimeId, owner, ExplosiveReactiveArmourCardRules.CardId,
                TrapTriggerType.EnemyMeleeUnitAttacksMaster, TrapEffectType.DestroyAttacker,
                new ResourceSet(0, 0, ExplosiveReactiveArmourCardRules.ActivationPowerCost, 0), 3);

        [TestCase(0, 0, false)]
        [TestCase(3, 0, false)]
        [TestCase(4, 0, true)]
        [TestCase(3, 1, true)]
        [TestCase(0, 4, true)]
        public void ReactiveArmourKnownUtilityUsesPowerOrGoldAndOnlyOrdinaryMeleeMasterThreats(int power, int gold, bool affordable)
        {
            var state = State();
            state.AI.Resources.Add(new ResourceSet(0, 0, power, gold));
            var threat = new UnitState("reactive-threat", "Golem", PlayerId.Player, new TileCoord(0, 0),
                AttackType.Melee, 100, 100, true, false, 0);
            state.PlayerBoard.Place(threat.Position, threat);
            var baseline = Evaluate(state);
            state.ArmedTraps.Add(ReactiveArmour(PlayerId.Player, "opponent-hidden-reactive"));
            Assert.That(Evaluate(state), Is.EqualTo(baseline), "Enemy trap identities are never valued as known effects.");
            state.ArmedTraps.Add(ReactiveArmour(PlayerId.AI));
            Assert.That(Evaluate(state) - baseline, Is.EqualTo(affordable ? 28 : 8).Within(0.000001));
            state.ArmedTraps.RemoveAt(state.ArmedTraps.Count - 1);
            state.PlayerBoard.Remove(threat.Position);
            baseline = Evaluate(state);
            state.ArmedTraps.Add(ReactiveArmour(PlayerId.AI));
            Assert.That(Evaluate(state) - baseline, Is.EqualTo(8).Within(0.000001),
                "The remaining enemy Master cannot activate reactive armour.");
        }

        [TestCase(AttackType.Ranged, false, false, 8)]
        [TestCase(AttackType.Melee, false, false, 28)]
        [TestCase(AttackType.Melee, true, false, 8)]
        [TestCase(AttackType.Melee, false, true, 8)]
        public void ReactiveArmourThreatUtilityRejectsRangedDisabledAndFrontBlockedAttacks(
            AttackType attackType, bool disabled, bool blocked, int expectedUtility)
        {
            var state = State(); state.AI.Resources.Add(new ResourceSet(0, 0, 4, 0));
            var threat = new UnitState("reactive-public-threat", "Golem", PlayerId.Player, new TileCoord(0, 0),
                attackType, 100, 100, true, false, 0);
            if (disabled) threat.ApplyStun(2);
            state.PlayerBoard.Place(threat.Position, threat);
            if (blocked)
            {
                var blocker = new UnitState("public-front-blocker", "Golem", PlayerId.AI, new TileCoord(2, 0),
                    AttackType.Melee, 1, 100, true, false, 0);
                state.AIBoard.Place(blocker.Position, blocker);
            }
            var baseline = Evaluate(state);
            state.ArmedTraps.Add(ReactiveArmour(PlayerId.AI));
            Assert.That(Evaluate(state) - baseline, Is.EqualTo(expectedUtility).Within(0.000001));
        }

        [Test]
        public void CapturedLegacyReactiveRuleStillValuesItsOriginalRangedMasterCondition()
        {
            var state = State(); state.AI.Resources.Add(new ResourceSet(0, 0, 5, 0));
            var threat = new UnitState("legacy-public-threat", "Golem", PlayerId.Player, new TileCoord(0, 0),
                AttackType.Ranged, 100, 100, true, false, 0);
            state.PlayerBoard.Place(threat.Position, threat);
            var baseline = Evaluate(state);
            state.ArmedTraps.Add(new ArmedTrapState("captured-legacy", PlayerId.AI, ExplosiveReactiveArmourCardRules.CardId,
                TrapTriggerType.EnemyUnitAttacksMaster, TrapEffectType.DestroyAttacker, new ResourceSet(0, 0, 5, 0), 2));
            Assert.That(Evaluate(state) - baseline, Is.EqualTo(28).Within(0.000001));
        }

        [TestCase(1)]
        [TestCase(3)]
        public void ReactiveArmourFreeInstallPlanningDoesNotDependOnEnemySecrets(int depth)
        {
            var first = State();
            first.AI.Hand.Add(ExplosiveReactiveArmourCardRules.CardId, "selected-reactive", false);
            first.ArmedTraps.Add(ReactiveArmour(PlayerId.Player, "private-enemy-reactive"));
            var second = AiBattleStateCopy.Create(first);
            second.ArmedTraps[0] = Armed(PlayerId.Player, "different-secret", 999, "different-private-id");
            var provider = Provider(ExplosiveReactiveArmourCardRules.CreateDefinition());
            var before = AiBattleStateCopy.PositionKey(first);
            var a = Planner(provider, depth).GetNextCommand(first) as InstallTrapCommand;
            var b = Planner(provider, depth).GetNextCommand(second) as InstallTrapCommand;
            Assert.That(a, Is.Not.Null); Assert.That(b, Is.Not.Null);
            Assert.That(a.CardId, Is.EqualTo(ExplosiveReactiveArmourCardRules.CardId));
            Assert.That(a.HandCardRuntimeId, Is.EqualTo(b.HandCardRuntimeId).And.EqualTo("selected-reactive"));
            Assert.That(AiBattleStateCopy.PositionKey(first), Is.EqualTo(before));
            Processor(provider).Execute(first, PlayerId.AI, a);
            Assert.That(first.AI.Resources.Power + first.AI.Resources.Gold, Is.Zero);
            Assert.That(first.ArmedTraps.Last().EffectType, Is.EqualTo(TrapEffectType.DestroyAttacker));
            Assert.That(first.ArmedTraps.Last().TriggerType, Is.EqualTo(TrapTriggerType.EnemyMeleeUnitAttacksMaster));
            Assert.That(first.ArmedTraps.Last().ActivationCost.Power, Is.EqualTo(4));
            Assert.That(Candidates(first, provider).OfType<InstallTrapCommand>(), Is.Empty);
            var observed = AiBattleStateCopy.Create(first, hidePrivateZones: true);
            var repeated = AiBattleStateCopy.Create(observed, hidePrivateZones: true);
            Assert.That(repeated.ArmedTraps.Select(t => t.RuntimeId), Is.EqualTo(new[] { "selected-reactive" }));
            Assert.That(repeated.ArmedTraps.Single().ActivationCost.Power, Is.EqualTo(4));
            Assert.That(repeated.ArmedTraps.Single().TriggerType, Is.EqualTo(TrapTriggerType.EnemyMeleeUnitAttacksMaster));
            Assert.That(repeated.GetArmedTrapCount(PlayerId.Player), Is.EqualTo(1));
            Assert.That(AiBattleStateCopy.PositionKey(repeated), Does.Not.Contain("private-enemy-reactive"));
        }

        [TestCase(AttackType.Melee, true)]
        [TestCase(AttackType.Ranged, false)]
        public void SimulatedReactiveArmourUsesOnlyMeleeWithoutMutatingSourceOrInventingDamage(AttackType attackType, bool triggers)
        {
            var state = State(); state.Player.Resources.Add(new ResourceSet(0, 0, 3, 1));
            state.ArmedTraps.Add(ReactiveArmour(PlayerId.Player));
            var attacker = new UnitState("reactive-ai-attacker", "Golem", PlayerId.AI, new TileCoord(0, 0),
                attackType, 30, 50, true, false, 0);
            attacker.WasSummonedThisTurn = false; attacker.HasSummoningSickness = false;
            attacker.RemainingAttacksThisTurn = 1; state.AIBoard.Place(attacker.Position, attacker);
            var before = AiBattleStateCopy.PositionKey(state);
            var after = Simulate(state, Provider(ExplosiveReactiveArmourCardRules.CreateDefinition()),
                new AttackCommand(attacker.Position, MasterCoord));
            Assert.That(after, Is.Not.Null);
            Assert.That(after.LastAttackResolution.WasCancelledByTrap, Is.EqualTo(triggers));
            if (triggers)
            {
                Assert.That(after.Player.Master.CurrentHp, Is.EqualTo(333));
                Assert.That(after.AIBoard.GetOccupant(attacker.Position), Is.Null);
                Assert.That(after.Player.Resources.Power + after.Player.Resources.Gold, Is.Zero);
                Assert.That(after.ArmedTraps, Is.Empty); Assert.That(after.ValuePopupEvents, Is.Empty);
                Assert.That(after.Player.Discard.CardIds, Is.EqualTo(new[] { ExplosiveReactiveArmourCardRules.CardId }));
                Assert.That(after.AI.Hand.Count, Is.Zero, "Destruction does not return the attacker to its hand.");
            }
            else
            {
                Assert.That(after.Player.Master.CurrentHp, Is.EqualTo(303));
                Assert.That(after.AIBoard.GetOccupant(attacker.Position).RuntimeId, Is.EqualTo(attacker.RuntimeId));
                Assert.That(after.Player.Resources.Power, Is.EqualTo(3)); Assert.That(after.Player.Resources.Gold, Is.EqualTo(1));
                Assert.That(after.ArmedTraps.Count, Is.EqualTo(1)); Assert.That(after.TrapEvents, Is.Empty);
                Assert.That(after.Player.Discard.CardIds, Is.Empty);
                Assert.That(after.ValuePopupEvents.Any(e => e.SourceCardId == ExplosiveReactiveArmourCardRules.CardId), Is.False);
            }
            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(before));
            Assert.That(attacker.CurrentHp, Is.EqualTo(50));
            Assert.That(state.Player.Resources.Power, Is.EqualTo(3)); Assert.That(state.Player.Resources.Gold, Is.EqualTo(1));
        }

        private static ArmedTrapState IronDome(PlayerId owner, string runtimeId = "reactive") =>
            new ArmedTrapState(runtimeId, owner, IronDomeCardRules.CardId,
                TrapTriggerType.EnemyRangedUnitAttacksMaster, TrapEffectType.DestroyAttacker,
                new ResourceSet(0, 0, IronDomeCardRules.ActivationPowerCost, 0), 3);

        [TestCase(0, 0, false)]
        [TestCase(3, 0, false)]
        [TestCase(4, 0, true)]
        [TestCase(3, 1, true)]
        [TestCase(0, 4, true)]
        public void IronDomeKnownUtilityUsesPowerOrGoldAndOnlyOrdinaryRangedMasterThreats(int power, int gold, bool affordable)
        {
            var state = State();
            state.AI.Resources.Add(new ResourceSet(0, 0, power, gold));
            var threat = new UnitState("reactive-threat", "Golem", PlayerId.Player, new TileCoord(0, 0),
                AttackType.Ranged, 100, 100, true, false, 0);
            state.PlayerBoard.Place(threat.Position, threat);
            var baseline = Evaluate(state);
            state.ArmedTraps.Add(IronDome(PlayerId.Player, "opponent-hidden-reactive"));
            Assert.That(Evaluate(state), Is.EqualTo(baseline), "Enemy trap identities are never valued as known effects.");
            state.ArmedTraps.Add(IronDome(PlayerId.AI));
            Assert.That(Evaluate(state) - baseline, Is.EqualTo(affordable ? 28 : 8).Within(0.000001));
            state.ArmedTraps.RemoveAt(state.ArmedTraps.Count - 1);
            state.PlayerBoard.Remove(threat.Position);
            baseline = Evaluate(state);
            state.ArmedTraps.Add(IronDome(PlayerId.AI));
            Assert.That(Evaluate(state) - baseline, Is.EqualTo(8).Within(0.000001),
                "The remaining enemy Master cannot activate reactive armour.");
        }

        [TestCase(AttackType.Melee, false, false, 8)]
        [TestCase(AttackType.Ranged, false, false, 28)]
        [TestCase(AttackType.Ranged, true, false, 8)]
        [TestCase(AttackType.Ranged, false, true, 28)]
        public void IronDomeThreatUtilityRejectsMeleeAndDisabledButIgnoresFrontBlocking(
            AttackType attackType, bool disabled, bool blocked, int expectedUtility)
        {
            var state = State(); state.AI.Resources.Add(new ResourceSet(0, 0, 4, 0));
            var threat = new UnitState("reactive-public-threat", "Golem", PlayerId.Player, new TileCoord(0, 0),
                attackType, 100, 100, true, false, 0);
            if (disabled) threat.ApplyStun(2);
            state.PlayerBoard.Place(threat.Position, threat);
            if (blocked)
            {
                var blocker = new UnitState("public-front-blocker", "Golem", PlayerId.AI, new TileCoord(2, 0),
                    AttackType.Ranged, 1, 100, true, false, 0);
                state.AIBoard.Place(blocker.Position, blocker);
            }
            var baseline = Evaluate(state);
            state.ArmedTraps.Add(IronDome(PlayerId.AI));
            Assert.That(Evaluate(state) - baseline, Is.EqualTo(expectedUtility).Within(0.000001));
        }

        [TestCase(1)]
        [TestCase(3)]
        public void IronDomeFreeInstallPlanningDoesNotDependOnEnemySecrets(int depth)
        {
            var first = State();
            first.AI.Hand.Add(IronDomeCardRules.CardId, "selected-reactive", false);
            first.ArmedTraps.Add(IronDome(PlayerId.Player, "private-enemy-reactive"));
            var second = AiBattleStateCopy.Create(first);
            second.ArmedTraps[0] = Armed(PlayerId.Player, "different-secret", 999, "different-private-id");
            var provider = Provider(IronDomeCardRules.CreateDefinition());
            var before = AiBattleStateCopy.PositionKey(first);
            var a = Planner(provider, depth).GetNextCommand(first) as InstallTrapCommand;
            var b = Planner(provider, depth).GetNextCommand(second) as InstallTrapCommand;
            Assert.That(a, Is.Not.Null); Assert.That(b, Is.Not.Null);
            Assert.That(a.CardId, Is.EqualTo(IronDomeCardRules.CardId));
            Assert.That(a.HandCardRuntimeId, Is.EqualTo(b.HandCardRuntimeId).And.EqualTo("selected-reactive"));
            Assert.That(AiBattleStateCopy.PositionKey(first), Is.EqualTo(before));
            Processor(provider).Execute(first, PlayerId.AI, a);
            Assert.That(first.AI.Resources.Power + first.AI.Resources.Gold, Is.Zero);
            Assert.That(first.ArmedTraps.Last().EffectType, Is.EqualTo(TrapEffectType.DestroyAttacker));
            Assert.That(first.ArmedTraps.Last().TriggerType, Is.EqualTo(TrapTriggerType.EnemyRangedUnitAttacksMaster));
            Assert.That(first.ArmedTraps.Last().ActivationCost.Power, Is.EqualTo(4));
            Assert.That(Candidates(first, provider).OfType<InstallTrapCommand>(), Is.Empty);
            var observed = AiBattleStateCopy.Create(first, hidePrivateZones: true);
            var repeated = AiBattleStateCopy.Create(observed, hidePrivateZones: true);
            Assert.That(repeated.ArmedTraps.Select(t => t.RuntimeId), Is.EqualTo(new[] { "selected-reactive" }));
            Assert.That(repeated.ArmedTraps.Single().ActivationCost.Power, Is.EqualTo(4));
            Assert.That(repeated.ArmedTraps.Single().TriggerType, Is.EqualTo(TrapTriggerType.EnemyRangedUnitAttacksMaster));
            Assert.That(repeated.GetArmedTrapCount(PlayerId.Player), Is.EqualTo(1));
            Assert.That(AiBattleStateCopy.PositionKey(repeated), Does.Not.Contain("private-enemy-reactive"));
        }

        [TestCase(AttackType.Ranged, true)]
        [TestCase(AttackType.Melee, false)]
        public void SimulatedIronDomeUsesOnlyRangedWithoutMutatingSourceOrInventingDamage(AttackType attackType, bool triggers)
        {
            var state = State(); state.Player.Resources.Add(new ResourceSet(0, 0, 3, 1));
            state.ArmedTraps.Add(IronDome(PlayerId.Player));
            var attacker = new UnitState("reactive-ai-attacker", "Golem", PlayerId.AI, new TileCoord(0, 0),
                attackType, 30, 50, true, false, 0);
            attacker.WasSummonedThisTurn = false; attacker.HasSummoningSickness = false;
            attacker.RemainingAttacksThisTurn = 1; state.AIBoard.Place(attacker.Position, attacker);
            var before = AiBattleStateCopy.PositionKey(state);
            var after = Simulate(state, Provider(IronDomeCardRules.CreateDefinition()),
                new AttackCommand(attacker.Position, MasterCoord));
            Assert.That(after, Is.Not.Null);
            Assert.That(after.LastAttackResolution.WasCancelledByTrap, Is.EqualTo(triggers));
            if (triggers)
            {
                Assert.That(after.Player.Master.CurrentHp, Is.EqualTo(333));
                Assert.That(after.AIBoard.GetOccupant(attacker.Position), Is.Null);
                Assert.That(after.Player.Resources.Power + after.Player.Resources.Gold, Is.Zero);
                Assert.That(after.ArmedTraps, Is.Empty); Assert.That(after.ValuePopupEvents, Is.Empty);
                Assert.That(after.Player.Discard.CardIds, Is.EqualTo(new[] { IronDomeCardRules.CardId }));
                Assert.That(after.AI.Hand.Count, Is.Zero, "Destruction does not return the attacker to its hand.");
            }
            else
            {
                Assert.That(after.Player.Master.CurrentHp, Is.EqualTo(303));
                Assert.That(after.AIBoard.GetOccupant(attacker.Position).RuntimeId, Is.EqualTo(attacker.RuntimeId));
                Assert.That(after.Player.Resources.Power, Is.EqualTo(3)); Assert.That(after.Player.Resources.Gold, Is.EqualTo(1));
                Assert.That(after.ArmedTraps.Count, Is.EqualTo(1)); Assert.That(after.TrapEvents, Is.Empty);
                Assert.That(after.Player.Discard.CardIds, Is.Empty);
                Assert.That(after.ValuePopupEvents.Any(e => e.SourceCardId == IronDomeCardRules.CardId), Is.False);
            }
            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(before));
            Assert.That(attacker.CurrentHp, Is.EqualTo(50));
            Assert.That(state.Player.Resources.Power, Is.EqualTo(3)); Assert.That(state.Player.Resources.Gold, Is.EqualTo(1));
        }

        [Test]
        public void PublicEnemyTrapCountChangesTheCacheKeyWithoutRevealingItsCardIdentity()
        {
            var state = State();
            var before = AiBattleStateCopy.PositionKey(state);
            state.Player.RestoreHiddenTrapCount(1);
            var after = AiBattleStateCopy.PositionKey(state);
            Assert.That(after, Is.Not.EqualTo(before));
            Assert.That(after, Does.Contain("enemy-traps:1"));
            Assert.That(after, Does.Not.Contain("TrapReturn"));
        }
    }
}
