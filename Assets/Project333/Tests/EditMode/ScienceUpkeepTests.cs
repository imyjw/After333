using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;

namespace Project333.Tests.EditMode
{
    public sealed class ScienceUpkeepTests
    {
        [Test]
        public void ResolveTurnStart_AppliesTurnStartResourceGainBeforeScienceUpkeep()
        {
            var battleState = CreateBattleState(PlayerId.Player);
            var service = new TurnStartService();

            var manaUnit = new UnitState(
                runtimeId: "mana-unit",
                cardId: "mana-unit",
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 1,
                maxHp: 3,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                turnStartResourceGain: new ResourceSet(mana: 2, qi: 0, power: 0, gold: 0));

            var goldBuilding = new BuildingState(
                runtimeId: "gold-building",
                cardId: "gold-building",
                ownerId: PlayerId.Player,
                position: new TileCoord(4, 0),
                canAttack: false,
                attack: 0,
                maxHp: 5,
                turnStartResourceGain: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 1));

            battleState.PlayerBoard.Place(new TileCoord(0, 0), manaUnit);
            battleState.PlayerBoard.Place(new TileCoord(4, 0), goldBuilding);
            battleState.Player.Resources.Add(new ResourceSet(mana: 2, qi: 1, power: 4, gold: 2));

            service.ResolveTurnStart(battleState);

            Assert.That(battleState.Player.Resources.Mana, Is.EqualTo(4));
            Assert.That(battleState.Player.Resources.Qi, Is.EqualTo(1));
            Assert.That(battleState.Player.Resources.Power, Is.EqualTo(4));
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(9));
            Assert.That(battleState.Phase, Is.EqualTo(PhaseType.Main));
        }

        [Test]
        public void ResolveTurnStart_DrainedUnitDoesNotContributeResourceGainDuringGainStep()
        {
            var battleState = CreateBattleState(PlayerId.Player);
            var service = new TurnStartService();

            var drainedUnit = new UnitState(
                runtimeId: "drained-upkeep",
                cardId: "drained-upkeep",
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 2,
                maxHp: 5,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 1,
                turnStartResourceGain: new ResourceSet(mana: 0, qi: 0, power: 2, gold: 0));

            drainedUnit.IsDrained = true;
            battleState.PlayerBoard.Place(new TileCoord(0, 0), drainedUnit);

            service.ResolveTurnStart(battleState);

            Assert.That(battleState.Player.Resources.Power, Is.EqualTo(0));
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(5));
            Assert.That(drainedUnit.IsDrained, Is.False);
        }

        [Test]
        public void ResolveTurnStart_PowerUpkeepUsesFixedTileOrderAndDrainsOnlyUnpaidUnits()
        {
            var battleState = CreateBattleState(PlayerId.Player);
            var service = new TurnStartService();

            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 5, gold: 0));
            battleState.Player.Resources.Spend(new ResourceSet(0, 0, 0, 2));

            var upkeepSeven = CreateScienceUnit("science-7", new TileCoord(0, 0), 7);
            var upkeepThree = CreateScienceUnit("science-3", new TileCoord(0, 1), 3);
            var upkeepFour = CreateScienceUnit("science-4", new TileCoord(2, 0), 4);
            var upkeepTwo = CreateScienceUnit("science-2", new TileCoord(4, 1), 2);

            battleState.PlayerBoard.Place(new TileCoord(0, 0), upkeepSeven);
            battleState.PlayerBoard.Place(new TileCoord(0, 1), upkeepThree);
            battleState.PlayerBoard.Place(new TileCoord(2, 0), upkeepFour);
            battleState.PlayerBoard.Place(new TileCoord(4, 1), upkeepTwo);

            service.ResolveTurnStart(battleState);

            Assert.That(upkeepSeven.IsDrained, Is.False);
            Assert.That(upkeepThree.IsDrained, Is.True);
            Assert.That(upkeepFour.IsDrained, Is.True);
            Assert.That(upkeepTwo.IsDrained, Is.False);
            Assert.That(battleState.Player.Resources.Power, Is.EqualTo(0));
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(0));
        }

        [Test]
        public void ResolveTurnStart_PowerUpkeepSpendsPowerFirstAndGoldForTheDeficit()
        {
            var battleState = CreateBattleState(PlayerId.Player);
            var service = new TurnStartService();
            var upkeepUnit = CreateScienceUnit("gold-backed-upkeep", new TileCoord(0, 0), 3);

            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 1, gold: 0));
            battleState.PlayerBoard.Place(upkeepUnit.Position, upkeepUnit);

            service.ResolveTurnStart(battleState);

            Assert.That(upkeepUnit.IsDrained, Is.False);
            Assert.That(battleState.Player.Resources.Power, Is.EqualTo(0));
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(4));
            var upkeepPayment = battleState.ResourceChangeEvents.Single(change =>
                change.SourceCardId == upkeepUnit.CardId);
            Assert.That(upkeepPayment.Spent.Power, Is.EqualTo(1));
            Assert.That(upkeepPayment.Spent.Gold, Is.EqualTo(2));
        }

        [Test]
        public void ResolveTurnStart_NonScienceUnitWithPowerUpkeepCanBecomeDrained()
        {
            var battleState = CreateBattleState(PlayerId.Player);
            var service = new TurnStartService();
            var upkeepUnit = new UnitState(
                runtimeId: "upkeep-unit",
                cardId: "upkeep-unit",
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 3,
                maxHp: 5,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 99);

            battleState.PlayerBoard.Place(new TileCoord(0, 0), upkeepUnit);

            service.ResolveTurnStart(battleState);

            Assert.That(upkeepUnit.IsDrained, Is.True);
        }

        [Test]
        public void ResolveTurnStart_ErasureOccupantDoesNotPayUpkeepOrBecomeDrained()
        {
            var battleState = CreateBattleState(PlayerId.Player);
            var service = new TurnStartService();
            var upkeepUnit = CreateScienceUnit("erasure-upkeep", new TileCoord(0, 0), 3);
            upkeepUnit.IsDrained = true;
            upkeepUnit.ApplyErasure();
            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 3, gold: 0));
            battleState.PlayerBoard.Place(upkeepUnit.Position, upkeepUnit);

            service.ResolveTurnStart(battleState);

            Assert.That(upkeepUnit.IsErasure, Is.True);
            Assert.That(upkeepUnit.IsDrained, Is.False);
            Assert.That(battleState.Player.Resources.Power, Is.EqualTo(3));
        }

        [TestCase(PlayerId.Player, "firewall")]
        [TestCase(PlayerId.AI, "firewall")]
        [TestCase(PlayerId.Player, "fallout")]
        [TestCase(PlayerId.AI, "fallout")]
        [TestCase(PlayerId.Player, "biochemical_bomb")]
        [TestCase(PlayerId.AI, "biochemical_bomb")]
        [TestCase(PlayerId.Player, "timed_bomb")]
        [TestCase(PlayerId.AI, "timed_bomb")]
        [TestCase(PlayerId.Player, "cheonra_jimang")]
        [TestCase(PlayerId.AI, "cheonra_jimang")]
        public void ResolveTurnStart_DestroyedOccupantPaysNeitherPowerNorGold(PlayerId owner, string effectId)
        {
            var state = CreateBattleState(owner);
            var target = new UnitState("doomed", "doomed", owner, new TileCoord(0, 0),
                AttackType.Melee, 3, 5, true, true, sciencePowerUpkeep: 9);
            state.GetBoard(owner).Place(target.Position, target);
            state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 5, 0));
            AddTurnStartHazard(state, target, effectId, 10);

            new TurnStartService().ResolveTurnStart(state);

            Assert.That(state.GetBoard(owner).GetOccupant(target.Position), Is.Null);
            Assert.That(state.GetPlayer(owner).Resources.Power, Is.EqualTo(5));
            Assert.That(state.GetPlayer(owner).Resources.Gold, Is.EqualTo(6));
            Assert.That(state.ResourceChangeEvents.Any(e => e.SourceCardId == target.CardId), Is.False);
            Assert.That(target.IsDrained, Is.False);
            Assert.That(state.Phase, Is.EqualTo(PhaseType.Main));
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void ResolveTurnStart_SurvivorCanUsePowerNotChargedToDestroyedOccupant(PlayerId owner)
        {
            var state = CreateBattleState(owner);
            var doomed = new UnitState("first", "first", owner, new TileCoord(0, 0),
                AttackType.Melee, 3, 5, true, true, sciencePowerUpkeep: 9);
            var survivor = new UnitState("next", "next", owner, new TileCoord(0, 1),
                AttackType.Melee, 3, 5, true, true, sciencePowerUpkeep: 5);
            state.GetBoard(owner).Place(doomed.Position, doomed);
            state.GetBoard(owner).Place(survivor.Position, survivor);
            state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 5, 0));
            AddTurnStartHazard(state, doomed, "firewall", 10);

            new TurnStartService().ResolveTurnStart(state);

            Assert.That(survivor.IsDrained, Is.False);
            Assert.That(state.GetPlayer(owner).Resources.Power, Is.Zero);
            Assert.That(state.GetPlayer(owner).Resources.Gold, Is.EqualTo(6));
            var payment = state.ResourceChangeEvents.Single(e => e.SourceCardId == survivor.CardId);
            Assert.That(payment.Spent.Power, Is.EqualTo(5));
            Assert.That(payment.Spent.Gold, Is.Zero);
        }

        [TestCase(false, true, 4)]
        [TestCase(true, true, 2)]
        [TestCase(false, false, 4)]
        [TestCase(true, false, 2)]
        public void ResolveTurnStart_DamageUsesDrainedStateBeforePayment(bool wasDrained, bool canPay, int hp)
        {
            var state = CreateBattleState(PlayerId.Player);
            var target = CreateScienceUnit("survives", new TileCoord(0, 0), canPay ? 3 : 99);
            target.IsDrained = wasDrained;
            state.PlayerBoard.Place(target.Position, target);
            state.Player.Resources.Add(new ResourceSet(0, 0, 1, 0));
            AddTurnStartHazard(state, target, "firewall", 1);

            new TurnStartService().ResolveTurnStart(state);

            Assert.That(target.CurrentHp, Is.EqualTo(hp));
            Assert.That(target.IsDrained, Is.EqualTo(!canPay));
            Assert.That(state.Player.Resources.Power, Is.EqualTo(canPay ? 0 : 1));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(canPay ? 4 : 6));
        }

        [Test]
        public void ResolveUpkeep_DeadOccupantStillOnBoardCannotPayOrBecomeDrained()
        {
            var state = CreateBattleState(PlayerId.Player);
            var target = CreateScienceUnit("dead", new TileCoord(0, 0), 9);
            state.PlayerBoard.Place(target.Position, target);
            target.CurrentHp = 0;
            state.Player.Resources.Add(new ResourceSet(0, 0, 6, 0));

            new ScienceUpkeepService().ResolveUpkeep(state);

            Assert.That(state.Player.Resources.Power, Is.EqualTo(6));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(3));
            Assert.That(state.ResourceChangeEvents, Is.Empty);
            Assert.That(target.IsDrained, Is.False);
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void ResolveTurnStart_DestructionChainCompletesBeforeUpkeep(PlayerId owner)
        {
            var state = CreateBattleState(owner);
            var target = new UnitState("chain-victim", "chain-victim", owner, new TileCoord(0, 1),
                AttackType.Melee, 3, 5, true, true, sciencePowerUpkeep: 9);
            state.GetBoard(owner).Place(target.Position, target);
            var plant = new BuildingState("plant", NuclearPowerPlantRules.CardId, owner,
                new TileCoord(0, 0), false, 0, 30);
            state.GetBoard(owner).Place(plant.Position, plant);
            state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 5, 0));
            state.PersistentEffects.Add(new PersistentEffectState(
                "Firewall", state.GetOpponent(owner).Id, "firewall", 2, "one trigger", new ResourceSet(),
                targetRow: 0, remainingTriggers: 1, effectDamage: 30, effectDamageType: DamageType.Magic));

            new TurnStartService().ResolveTurnStart(state);

            Assert.That(state.GetBoard(owner).GetOccupant(target.Position), Is.Null);
            Assert.That(state.GetPlayer(owner).Resources.Power, Is.EqualTo(5));
            Assert.That(state.GetPlayer(owner).Resources.Gold, Is.EqualTo(6));
            Assert.That(state.ResourceChangeEvents.Any(e => e.SourceCardId == target.CardId), Is.False);
        }
        private static void AddTurnStartHazard(BattleState state, UnitState target, string effectId, int damage)
        {
            if (effectId == "fallout")
            {
                target.RestoreFallout(1, 0);
                return;
            }
            // Timed Bomb uses its shared constant, so use its real effect id.
            if (effectId == "timed_bomb") effectId = TimedBombRules.EffectId;
            state.PersistentEffects.Add(new PersistentEffectState(
                effectId, effectId == "cheonra_jimang" ? target.OwnerId : state.GetOpponent(target.OwnerId).Id,
                effectId, 2, "one trigger", new ResourceSet(),
                targetRuntimeId: target.RuntimeId, targetRow: target.Position.Row,
                remainingTriggers: 1, effectDamage: damage,
                effectDamageType: effectId == "firewall" ? DamageType.Magic : DamageType.Fixed,
                targetStartColumn: 0));
        }
        private static UnitState CreateScienceUnit(string id, TileCoord coord, int upkeep)
        {
            return new UnitState(
                runtimeId: id,
                cardId: id,
                ownerId: PlayerId.Player,
                position: coord,
                attackType: AttackType.Melee,
                attack: 3,
                maxHp: 5,
                canMove: true,
                isScience: true,
                sciencePowerUpkeep: upkeep);
        }

        private static BattleState CreateBattleState(PlayerId firstPlayerId)
        {
            var setupService = new BattleSetupService();
            var mulliganService = new MulliganService();
            var battleState = setupService.CreateInitialState(CreateRequest(firstPlayerId));

            mulliganService.PassMulligan(battleState, PlayerId.Player);
            battleState.RestoreRuntimeState(
                turnNumber: 3,
                activePlayerId: firstPlayerId,
                phase: PhaseType.TurnStart);
            return battleState;
        }

        private static BattleSetupRequest CreateRequest(PlayerId firstPlayerId)
        {
            return new BattleSetupRequest(
                playerDeckCardIds: CreateDeck("P"),
                aiDeckCardIds: CreateDeck("A"),
                firstPlayerId: firstPlayerId);
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
