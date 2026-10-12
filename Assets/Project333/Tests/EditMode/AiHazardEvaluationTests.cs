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

        [TestCase(PlayerId.Player, AttackType.Ranged, false, 0.85)]
        [TestCase(PlayerId.AI, AttackType.Ranged, false, -0.85)]
        [TestCase(PlayerId.Player, AttackType.Melee, false, 0.55)]
        [TestCase(PlayerId.Player, AttackType.Ranged, true, 1.0)]
        public void RangedDamageValue_UsesOriginalHpWeightWithoutChangingOtherRoles(
            PlayerId owner, AttackType attackType, bool producer, double expectedGain)
        {
            var deck = Enumerable.Repeat("unknown", 20).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.AI));
            state.SetPhase(PhaseType.Main);
            var position = new TileCoord(0, 0);
            var unit = new UnitState("hp-value", "hp-value", owner, position, attackType,
                10, 20, true, false, 0, producer ? new ResourceSet(1, 0, 0, 0) : null);
            unit.HasSummoningSickness = false;
            unit.RemainingAttacksThisTurn = 1;
            state.GetBoard(owner).Place(position, unit);
            var before = Evaluate(state);
            unit.CurrentHp -= 1;
            Assert.That(Evaluate(state) - before, Is.EqualTo(expectedGain).Within(0.000001));
        }

        [TestCase(333, 332, 0.8)]
        [TestCase(250, 249, 0.8)]
        [TestCase(249, 248, 0.84)]
        [TestCase(150, 149, 0.84)]
        [TestCase(149, 148, 1.2)]
        [TestCase(80, 79, 1.2)]
        [TestCase(79, 78, 3.0)]
        [TestCase(250, 240, 8.0)]
        [TestCase(150, 140, 8.4)]
        [TestCase(80, 70, 12.0)]
        [TestCase(79, 69, 30.0)]
        [TestCase(70, 70, 0.0)]
        [TestCase(10, -50, 30.0)]
        [TestCase(70, 80, -30.0)]
        public void MasterDamageBands_UsePreActionHpAndActualHpChange(int before, int after, double expected)
        {
            Assert.That(Invoke("EvaluateEnemyMasterHpChange", before, after), Is.EqualTo(expected).Within(0.000001));
        }

        [Test]
        public void MasterDamageBands_DoNotKeepOldEnemyHpBonuses()
        {
            var state = State();
            Unit(state, PlayerId.AI, new TileCoord(0, 0), 10, 20);
            var before = Invoke("EvaluateForPlanning", state, 333);
            state.Player.Master.CurrentHp -= 100;
            Assert.That(Invoke("EvaluateForPlanning", state, 333), Is.EqualTo(before).Within(0.000001), "Enemy HP must be credited only by the pre-action transition score.");
            state.AI.Master.CurrentHp -= 10;
            Assert.That(Invoke("EvaluateForPlanning", state, 333), Is.EqualTo(before).Within(0.000001), "Own HP also belongs to the pre-action transition score.");
            Assert.That(Invoke("EvaluateOwnMasterHpChange", 333, 323), Is.EqualTo(-8).Within(0.000001));
        }

        [TestCase(317, false)]
        [TestCase(79, true)]
        [TestCase(7, true)]
        public void MasterDamageBands_ChooseArcherAtHighHpAndMasterAtLowHp(int masterHp, bool targetMaster)
        {
            var state = State();
            foreach (var player in new[] { state.Player, state.AI })
                foreach (var cardId in player.Hand.CardIds.ToArray()) player.Hand.Remove(cardId);
            state.Player.Master.CurrentHp = masterHp;
            state.AI.Master.CurrentHp = 327;
            state.AI.Master.RemainingAttacksThisTurn = 0;
            var skeleton = Unit(state, PlayerId.AI, new TileCoord(0, 0), 10, 10, id: "Skeleton");
            skeleton.CurrentHp = 7;
            // Place the archer in another column so both targets are legal.
            var archer = new UnitState("archer", "ElfLongbowScout", PlayerId.Player,
                new TileCoord(1, 0), AttackType.Ranged, 10, 20, true, false, 0);
            archer.CurrentHp = 17;
            state.PlayerBoard.Place(archer.Position, archer);
            var provider = new Project333.Runtime.Infrastructure.Data.InMemoryCardDefinitionProvider(
                Array.Empty<Project333.Runtime.Infrastructure.Data.CardDefinition>());
            var planner = new AiDecisionService(provider, new TargetingService(),
                Project333.Runtime.Application.Services.ZeroCardUpgradeLevelProvider.Instance,
                new AiSearchOptions { MaxDepth = 1, MaxMilliseconds = 10000, MaxSimulations = 4000 });
            var command = planner.GetNextCommand(state) as Project333.Runtime.Application.Commands.AttackCommand;
            Assert.That(command, Is.Not.Null);
            Assert.That(command.TargetCoord, Is.EqualTo(targetMaster ? state.Player.Master.Position : archer.Position));
        }

        private static double Evaluate(BattleState state) => Invoke("Evaluate", state);

        [TestCase(PlayerId.Player)][TestCase(PlayerId.AI)]
        public void FalloutForecast_UsesSharedMasterDefeatAndKeepsLegacyExposedBuildingsSafe(PlayerId victim)
        {
            var state=State();state.GetBoard(victim).AddFalloutStack();state.GetPlayer(victim).Master.CurrentHp=10;
            var position=new TileCoord(0,0);
            var building=new BuildingState("legacy-building","PowerPlant",victim,position,false,0,5);
            state.GetBoard(victim).Place(position,building);building.RestoreFallout(8,8);
            var projected=AiBattleStateCopy.Create(state);
            new EndTurnService(new Random(41)).EndTurn(projected);
            new TurnStartService().ResolveTurnStart(projected);
            Assert.That(projected.IsEnded,Is.True);
            Assert.That(projected.Result.Winner,Is.EqualTo(projected.GetOpponent(victim).Id));
            Assert.That(projected.GetBoard(victim).GetOccupant(position).CurrentHp,Is.EqualTo(5));
            Assert.That(Evaluate(projected),Is.EqualTo(victim==PlayerId.AI?-1000000:1000000));
            Assert.That(state.GetPlayer(victim).Master.CurrentHp,Is.EqualTo(10));
            Assert.That(state.IsEnded,Is.False,"Forecast simulation cannot mutate real battle state.");
        }

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
