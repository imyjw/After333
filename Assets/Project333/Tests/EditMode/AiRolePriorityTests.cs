using System;
using System.Linq;
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
    public sealed class AiRolePriorityTests
    {
        private static BattleState State()
        {
            var deck = Enumerable.Repeat("unknown", 20).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.AI));
            state.SetPhase(PhaseType.Main);
            foreach (var player in new[] { state.Player, state.AI })
            {
                foreach (var id in player.Hand.CardIds.ToArray()) player.Hand.Remove(id);
                player.Master.RemainingAttacksThisTurn = 0;
            }
            state.Player.Master.AddInvincibleEffect(InvincibleDurationType.Always, 0, state.TurnNumber, PlayerId.Player);
            return state;
        }

        private static AiDecisionService Planner(params CardDefinition[] cards) => new AiDecisionService(
            new InMemoryCardDefinitionProvider(cards), new TargetingService(), ZeroCardUpgradeLevelProvider.Instance,
            new AiSearchOptions { MaxDepth = 1, MaxMilliseconds = 10000, MaxSimulations = 4000 });

        private static UnitState Unit(BattleState state, PlayerId owner, TileCoord position, int attack = 0, int hp = 30,
            AttackType type = AttackType.Melee, ResourceSet gain = null, bool movable = false,
            bool flying = false, bool hiding = false)
        {
            var unit = new UnitState(Guid.NewGuid().ToString(), "test-unit", owner, position, type, attack, hp,
                movable, false, 0, gain, hasFlying: flying, hasHiding: hiding);
            unit.HasSummoningSickness = false;
            unit.RemainingAttacksThisTurn = attack > 0 ? 1 : 0;
            state.GetBoard(owner).Place(position, unit);
            return unit;
        }

        private static BuildingState Building(BattleState state, PlayerId owner, TileCoord position, ResourceSet gain,
            string id = "test-building", int hp = 10)
        {
            var building = new BuildingState(Guid.NewGuid().ToString(), id, owner, position, false, 0, hp, gain);
            state.GetBoard(owner).Place(position, building);
            return building;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Attack_KillableProducerOutranksKillableRangedAndMelee(bool building)
        {
            var state = State();
            Unit(state, PlayerId.AI, new TileCoord(2,0), 10, type: AttackType.Ranged);
            var target = new TileCoord(4,0);
            var gain = new ResourceSet(1,0,0,0);
            if (building) Building(state, PlayerId.Player, target, gain);
            else Unit(state, PlayerId.Player, target, hp: 10, gain: gain);
            Unit(state, PlayerId.Player, new TileCoord(1,0), 20, 10, AttackType.Ranged);
            Unit(state, PlayerId.Player, new TileCoord(3,0), 20, 10);
            var command = Planner().GetNextCommand(state) as AttackCommand;
            Assert.That(command, Is.Not.Null);
            Assert.That(command.TargetCoord, Is.EqualTo(target));
        }

        [TestCase(10)]
        [TestCase(100)]
        public void Attack_RangedTargetPreferredForLethalAndNonlethalDamage(int hp)
        {
            var state = State();
            Unit(state, PlayerId.AI, new TileCoord(2,0), 10, type: AttackType.Ranged);
            Unit(state, PlayerId.Player, new TileCoord(1,0), 10, hp);
            var ranged = Unit(state, PlayerId.Player, new TileCoord(4,0), 10, hp, AttackType.Ranged);
            var command = Planner().GetNextCommand(state) as AttackCommand;
            Assert.That(command, Is.Not.Null);
            Assert.That(command.TargetCoord, Is.EqualTo(ranged.Position));
        }

        [Test]
        public void Attack_PowerPlantRecognizedDespiteZeroIntrinsicGain()
        {
            var state = State();
            Unit(state, PlayerId.AI, new TileCoord(2,0), 10, type: AttackType.Ranged);
            var plant = Building(state, PlayerId.Player, new TileCoord(4,0), new ResourceSet(), PowerPlantRules.CardId);
            Unit(state, PlayerId.Player, new TileCoord(1,0), 20, 10, AttackType.Ranged);
            var command = Planner().GetNextCommand(state) as AttackCommand;
            Assert.That(command, Is.Not.Null);
            Assert.That(command.TargetCoord, Is.EqualTo(plant.Position));
        }

        [Test]
        public void Attack_ErasedProducerDoesNotKeepProductionPriority()
        {
            var state = State();
            Unit(state, PlayerId.AI, new TileCoord(2,0), 10, type: AttackType.Ranged);
            Building(state, PlayerId.Player, new TileCoord(1,0), new ResourceSet(3,0,0,0)).ApplyErasure();
            var ranged = Unit(state, PlayerId.Player, new TileCoord(4,0), 20, 10, AttackType.Ranged);
            var command = Planner().GetNextCommand(state) as AttackCommand;
            Assert.That(command, Is.Not.Null);
            Assert.That(command.TargetCoord, Is.EqualTo(ranged.Position));
        }

        [Test]
        public void Attack_InvincibleProducerDoesNotRewardZeroDamage()
        {
            var state = State();
            Unit(state, PlayerId.AI, new TileCoord(2,0), 10, type: AttackType.Ranged);
            var producer = Building(state, PlayerId.Player, new TileCoord(1,0), new ResourceSet(3,0,0,0));
            producer.AddInvincibleEffect(InvincibleDurationType.Always, 0, state.TurnNumber, PlayerId.Player);
            var ranged = Unit(state, PlayerId.Player, new TileCoord(4,0), 20, 10, AttackType.Ranged);
            var command = Planner().GetNextCommand(state) as AttackCommand;
            Assert.That(command, Is.Not.Null);
            Assert.That(command.TargetCoord, Is.EqualTo(ranged.Position));
        }

        [Test]
        public void Attack_MasterLethalStillOutranksProducer()
        {
            var state = State();
            state.Player.Master.RestoreInvincibleEffects(Array.Empty<InvincibleEffectState>());
            state.Player.Master.CurrentHp = 10;
            Unit(state, PlayerId.AI, new TileCoord(2,0), 10, type: AttackType.Ranged);
            Building(state, PlayerId.Player, new TileCoord(1,0), new ResourceSet(5,0,0,0));
            var command = Planner().GetNextCommand(state) as AttackCommand;
            Assert.That(command, Is.Not.Null);
            Assert.That(command.TargetCoord, Is.EqualTo(state.Player.Master.Position));
        }

        [Test]
        public void Attack_ProducerBonusDoesNotJustifyFatalMasterCounterattack()
        {
            var state = State();
            state.AI.Master.CurrentHp = 2;
            state.AI.Master.RemainingAttacksThisTurn = 1;
            Unit(state, PlayerId.Player, new TileCoord(1,0), 10, 3, gain: new ResourceSet(5,0,0,0));
            Assert.That(Planner().GetNextCommand(state), Is.Not.TypeOf<AttackCommand>());
        }

        private static CardDefinition Card(string role)
        {
            var gain = role.Contains("producer") ? new ResourceSet(1,0,0,0) : new ResourceSet();
            return role.Contains("building")
                ? new BuildingCardDefinition("deploy", "Deploy", new ResourceSet(), false, 0, 20, gain)
                : new UnitCardDefinition("deploy", "Deploy", new ResourceSet(),
                    role == "ranged" ? AttackType.Ranged : AttackType.Melee, role == "ranged" ? 10 : 0, 20, true, false, 0, gain);
        }

        [TestCase("producer-building")]
        [TestCase("producer-unit")]
        [TestCase("ranged")]
        public void Summon_SupportPrefersBackRowEvenWithoutExistingCover(string role)
        {
            var state = State();
            state.AI.Hand.Add("deploy");
            var command = Planner(Card(role)).GetNextCommand(state);
            var position = command is PlayBuildingCardCommand b ? b.TargetCoord : ((PlayUnitCardCommand)command).TargetCoord;
            Assert.That(position.Row, Is.EqualTo(1));
        }

        [TestCase("producer-building")]
        [TestCase("producer-unit")]
        [TestCase("ranged")]
        public void Summon_SupportPrefersBackRowWithRealCover(string role)
        {
            var state = State();
            state.AI.Hand.Add("deploy");
            Unit(state, PlayerId.AI, new TileCoord(0,0));
            var command = Planner(Card(role)).GetNextCommand(state);
            var position = command is PlayBuildingCardCommand b ? b.TargetCoord : ((PlayUnitCardCommand)command).TargetCoord;
            Assert.That(position, Is.EqualTo(new TileCoord(0,1)));
        }

        [TestCase("flying")]
        [TestCase("hiding")]
        [TestCase("drained")]
        [TestCase("sealbound")]
        public void Summon_NonblockingFrontOccupantIsNotCountedAsCover(string status)
        {
            var state = State();
            state.AI.Hand.Add("deploy");
            var falseCover = Unit(state, PlayerId.AI, new TileCoord(1,0), flying: status == "flying", hiding: status == "hiding");
            if (status == "drained") falseCover.IsDrained = true;
            if (status == "sealbound") falseCover.EnterSealbound(3);
            Unit(state, PlayerId.AI, new TileCoord(0,0));
            var command = (PlayBuildingCardCommand)Planner(Card("producer-building")).GetNextCommand(state);
            Assert.That(command.TargetCoord, Is.EqualTo(new TileCoord(0,1)));
        }

        [Test]
        public void Summon_NonattackingBuildingIsNotMisclassifiedAsRanged()
        {
            var state = State();
            state.AI.Hand.Add("deploy");
            var command = (PlayBuildingCardCommand)Planner(Card("plain-building")).GetNextCommand(state);
            Assert.That(command.TargetCoord, Is.EqualTo(new TileCoord(2,0)));
        }

        [Test]
        public void Move_ProtectsProducerBuildingBeforeProducerUnitAndRangedUnit()
        {
            var state = State();
            Building(state, PlayerId.AI, new TileCoord(0,1), new ResourceSet(1,0,0,0));
            Unit(state, PlayerId.AI, new TileCoord(1,1), gain: new ResourceSet(1,0,0,0));
            var ranged = Unit(state, PlayerId.AI, new TileCoord(3,1), 10, type: AttackType.Ranged);
            ranged.RemainingAttacksThisTurn = 0;
            Unit(state, PlayerId.AI, new TileCoord(4,0), movable: true);
            var command = Planner().GetNextCommand(state) as MoveOccupantCommand;
            Assert.That(command, Is.Not.Null);
            Assert.That(command.To, Is.EqualTo(new TileCoord(0,0)));
        }
    }
}
