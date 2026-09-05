using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Tests.EditMode
{
    public sealed class NuclearPowerPlantTests
    {
        [Test]
        public void ResolveTurnStart_ActivePlantGrantsPowerAndSuppressedPlantDoesNot()
        {
            var battleState = CreateBattleState(PhaseType.TurnStart);
            var active = CreatePlant("active", PlayerId.Player, new TileCoord(0, 0));
            var erased = CreatePlant("erased", PlayerId.Player, new TileCoord(1, 0));
            erased.ApplyErasure();
            battleState.PlayerBoard.Place(active.Position, active);
            battleState.PlayerBoard.Place(erased.Position, erased);

            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(
                battleState.Player.Resources.Power,
                Is.EqualTo(NuclearPowerPlantRules.TurnStartPowerGain));
            Assert.That(
                battleState.ResourceChangeEvents,
                Has.Some.Matches<BattleResourceChangeEvent>(change =>
                    change.SourceCardId == NuclearPowerPlantRules.CardId &&
                    change.Gained.Power == NuclearPowerPlantRules.TurnStartPowerGain));
        }

        [Test]
        public void ResolveDefeatedOccupants_ExplosionHitsBothBoardsWithPhysicalDefense()
        {
            var battleState = CreateBattleState(PhaseType.Main);
            var plant = CreatePlant("plant", PlayerId.Player, new TileCoord(0, 0));
            var alliedTarget = CreateUnit(
                "ally",
                PlayerId.Player,
                new TileCoord(1, 0),
                maxHp: 100,
                physicalDefense: 3);
            var enemyTarget = CreateUnit(
                "enemy",
                PlayerId.AI,
                new TileCoord(0, 0),
                maxHp: 100,
                physicalDefense: 7);
            battleState.PlayerBoard.Place(plant.Position, plant);
            battleState.PlayerBoard.Place(alliedTarget.Position, alliedTarget);
            battleState.AIBoard.Place(enemyTarget.Position, enemyTarget);
            plant.CurrentHp = 0;

            OccupantDestructionService.ResolveDefeatedOccupants(battleState);

            Assert.That(battleState.PlayerBoard.GetOccupant(plant.Position), Is.Null);
            Assert.That(alliedTarget.CurrentHp, Is.EqualTo(73));
            Assert.That(enemyTarget.CurrentHp, Is.EqualTo(77));
            Assert.That(battleState.Player.Master.CurrentHp, Is.EqualTo(303));
            Assert.That(battleState.AI.Master.CurrentHp, Is.EqualTo(303));
            Assert.That(
                battleState.ValuePopupEvents,
                Has.Some.Matches<BattleValuePopupEvent>(entry =>
                    entry.Cause == BattleValueChangeCause.NuclearPowerPlant &&
                    entry.DamageType == DamageType.Physical &&
                    entry.SourceCardId == NuclearPowerPlantRules.CardId));
        }

        [Test]
        public void Attack_DestroyingPlantExplodesBeforeBattleContinues()
        {
            var battleState = CreateBattleState(PhaseType.Main);
            var attacker = CreateUnit(
                "attacker",
                PlayerId.Player,
                new TileCoord(0, 0),
                maxHp: 100,
                physicalDefense: 0,
                attack: 50,
                attackType: AttackType.Ranged);
            var plant = CreatePlant("plant", PlayerId.AI, new TileCoord(0, 0));
            attacker.HasSummoningSickness = false;
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(plant.Position, plant);

            new AttackService().Attack(
                battleState,
                PlayerId.Player,
                attacker.Position,
                plant.Position);

            Assert.That(battleState.AIBoard.GetOccupant(plant.Position), Is.Null);
            Assert.That(attacker.CurrentHp, Is.EqualTo(70));
            Assert.That(battleState.Player.Master.CurrentHp, Is.EqualTo(303));
            Assert.That(battleState.AI.Master.CurrentHp, Is.EqualTo(303));
        }

        [Test]
        public void ResolveDefeatedOccupants_ChainReactionTriggersEachPlantOnce()
        {
            var battleState = CreateBattleState(PhaseType.Main);
            var firstPlant = CreatePlant("first", PlayerId.Player, new TileCoord(0, 0));
            var secondPlant = CreatePlant("second", PlayerId.AI, new TileCoord(0, 0));
            var target = CreateUnit(
                "target",
                PlayerId.Player,
                new TileCoord(1, 0),
                maxHp: 200,
                physicalDefense: 0);
            battleState.PlayerBoard.Place(firstPlant.Position, firstPlant);
            battleState.AIBoard.Place(secondPlant.Position, secondPlant);
            battleState.PlayerBoard.Place(target.Position, target);
            firstPlant.CurrentHp = 0;
            secondPlant.CurrentHp = 30;

            OccupantDestructionService.ResolveDefeatedOccupants(battleState);

            Assert.That(battleState.PlayerBoard.GetOccupant(firstPlant.Position), Is.Null);
            Assert.That(battleState.AIBoard.GetOccupant(secondPlant.Position), Is.Null);
            Assert.That(target.CurrentHp, Is.EqualTo(140));
            Assert.That(
                battleState.ValuePopupEvents,
                Has.Exactly(2).Matches<BattleValuePopupEvent>(entry =>
                    entry.RuntimeId == target.RuntimeId &&
                    entry.Cause == BattleValueChangeCause.NuclearPowerPlant));
        }

        [Test]
        public void ResolveDefeatedOccupants_SuppressedPlantDoesNotExplode()
        {
            var battleState = CreateBattleState(PhaseType.Main);
            var plant = CreatePlant("plant", PlayerId.Player, new TileCoord(0, 0));
            var target = CreateUnit(
                "target",
                PlayerId.AI,
                new TileCoord(0, 0),
                maxHp: 100,
                physicalDefense: 0);
            plant.ApplyErasure();
            plant.CurrentHp = 0;
            battleState.PlayerBoard.Place(plant.Position, plant);
            battleState.AIBoard.Place(target.Position, target);

            OccupantDestructionService.ResolveDefeatedOccupants(battleState);

            Assert.That(battleState.PlayerBoard.GetOccupant(plant.Position), Is.Null);
            Assert.That(target.CurrentHp, Is.EqualTo(100));
            Assert.That(battleState.ValuePopupEvents, Is.Empty);
        }

        [Test]
        public void ResolveDefeatedOccupants_BothMastersDefeatedHigherFinalHpWins()
        {
            var battleState = CreateBattleState(PhaseType.Main);
            var plant = CreatePlant("plant", PlayerId.Player, new TileCoord(0, 0));
            battleState.PlayerBoard.Place(plant.Position, plant);
            plant.CurrentHp = 0;
            battleState.Player.Master.CurrentHp = 20;
            battleState.AI.Master.CurrentHp = 30;

            OccupantDestructionService.ResolveDefeatedOccupants(battleState);

            Assert.That(battleState.Player.Master.CurrentHp, Is.EqualTo(-10));
            Assert.That(battleState.AI.Master.CurrentHp, Is.EqualTo(0));
            Assert.That(battleState.Result.HasWinner, Is.True);
            Assert.That(battleState.Result.Winner, Is.EqualTo(PlayerId.AI));
        }

        [Test]
        public void ResolveDefeatedOccupants_BothMastersEqualFinalHpEndsInDraw()
        {
            var battleState = CreateBattleState(PhaseType.Main);
            var plant = CreatePlant("plant", PlayerId.Player, new TileCoord(0, 0));
            battleState.PlayerBoard.Place(plant.Position, plant);
            plant.CurrentHp = 0;
            battleState.Player.Master.CurrentHp = 30;
            battleState.AI.Master.CurrentHp = 30;

            OccupantDestructionService.ResolveDefeatedOccupants(battleState);

            Assert.That(battleState.Player.Master.CurrentHp, Is.EqualTo(0));
            Assert.That(battleState.AI.Master.CurrentHp, Is.EqualTo(0));
            Assert.That(battleState.Result.IsDraw, Is.True);
        }

        private static BuildingState CreatePlant(
            string runtimeId,
            PlayerId ownerId,
            TileCoord position)
        {
            return new BuildingState(
                runtimeId,
                NuclearPowerPlantRules.CardId,
                ownerId,
                position,
                canAttack: false,
                attack: 0,
                maxHp: NuclearPowerPlantRules.BaseHealth,
                turnStartResourceGain: new ResourceSet(
                    mana: 0,
                    qi: 0,
                    power: NuclearPowerPlantRules.TurnStartPowerGain,
                    gold: 0),
                damageType: DamageType.None);
        }

        private static UnitState CreateUnit(
            string runtimeId,
            PlayerId ownerId,
            TileCoord position,
            int maxHp,
            int physicalDefense,
            int attack = 1,
            AttackType attackType = AttackType.Melee)
        {
            return new UnitState(
                runtimeId,
                runtimeId,
                ownerId,
                position,
                attackType,
                attack,
                maxHp,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                physicalDefense: physicalDefense,
                damageType: DamageType.Physical);
        }

        private static BattleState CreateBattleState(PhaseType phase)
        {
            var battleState = new BattleSetupService().CreateInitialState(new BattleSetupRequest(
                CreateDeck("P"),
                CreateDeck("A"),
                PlayerId.Player));
            new MulliganService().PassMulligan(battleState, PlayerId.Player);
            battleState.RestoreRuntimeState(1, PlayerId.Player, phase);
            return battleState;
        }

        private static IReadOnlyList<string> CreateDeck(string prefix)
        {
            var cards = new List<string>();
            for (var i = 0; i < 20; i++)
            {
                cards.Add($"{prefix}-{i}");
            }

            return cards;
        }
    }
}
