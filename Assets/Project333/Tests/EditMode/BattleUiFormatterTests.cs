using NUnit.Framework;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Presentation.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class BattleUiFormatterTests
    {
        [Test]
        public void FormatTileOccupant_WhenTileIsEmpty_ReturnsEmptyText()
        {
            var summary = BattleUiFormatter.FormatTileOccupant(null);

            Assert.That(summary, Is.EqualTo("Empty"));
        }

        [Test]
        public void FormatTileOccupant_WhenOccupantExists_IncludesStatsAndStatus()
        {
            var occupant = new UnitState(
                runtimeId: "unit-1",
                cardId: "unit-1",
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 3,
                maxHp: 6,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0);

            occupant.CurrentHp = 4;
            occupant.ApplyErasure();

            var summary = BattleUiFormatter.FormatTileOccupant(occupant);

            Assert.That(summary, Does.Contain("unit-1"));
            Assert.That(summary, Does.Contain("Unit / Melee"));
            Assert.That(summary, Does.Contain("ATK 3 / HP 4/6"));
            Assert.That(summary, Does.Contain("Erasure"));
        }

        [Test]
        public void FormatTileOccupant_WhenOccupantIsDrained_ShowsDrainedStatus()
        {
            var occupant = new UnitState(
                runtimeId: "unit-drained",
                cardId: "unit-drained",
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 3,
                maxHp: 6,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 1);

            occupant.IsDrained = true;

            var summary = BattleUiFormatter.FormatTileOccupant(occupant);

            Assert.That(summary, Does.Contain("Drained"));
        }

        [Test]
        public void FormatTileOccupant_WhenOccupantHasGuard_ShowsGuardStatus()
        {
            var occupant = new UnitState(
                runtimeId: "unit-guard",
                cardId: "shieldbearer",
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 0,
                maxHp: 7,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hasGuard: true);

            var summary = BattleUiFormatter.FormatTileOccupant(occupant);

            Assert.That(summary, Does.Contain("Guard"));
            Assert.That(summary, Does.Not.Contain("Guard Off"));
        }

        [Test]
        public void FormatTileOccupant_WhenOccupantHasEndure_ShowsEndureStatus()
        {
            var occupant = new UnitState(
                runtimeId: "unit-2",
                cardId: "shaolin_1st_disciple",
                ownerId: PlayerId.Player,
                position: new TileCoord(1, 0),
                attackType: AttackType.Melee,
                attack: 2,
                maxHp: 5,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hasEndure: true);

            var summary = BattleUiFormatter.FormatTileOccupant(occupant);

            Assert.That(summary, Does.Contain("Endure"));
            Assert.That(summary, Does.Not.Contain("Endure Used"));
        }

        [Test]
        public void FormatTileOccupant_WhenEndureWasUsed_ShowsSpentStatus()
        {
            var occupant = new UnitState(
                runtimeId: "unit-3",
                cardId: "shaolin_1st_disciple",
                ownerId: PlayerId.Player,
                position: new TileCoord(1, 1),
                attackType: AttackType.Melee,
                attack: 2,
                maxHp: 5,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hasEndure: true);

            occupant.EndureUsed = true;

            var summary = BattleUiFormatter.FormatTileOccupant(occupant);

            Assert.That(summary, Does.Contain("Endure Used"));
        }

        [Test]
        public void FormatHandCard_WhenCardIdIsMissing_ShowsEmptySlot()
        {
            var label = BattleUiFormatter.FormatHandCard(2, string.Empty);

            Assert.That(label, Is.EqualTo("Slot 2: Empty"));
        }

        [Test]
        public void FormatResources_WhenResourceSetExists_ShowsAllBattleResources()
        {
            var resources = new ResourceSet(mana: 1, qi: 2, power: 3, gold: 4);

            var summary = BattleUiFormatter.FormatResources(resources);

            Assert.That(summary, Is.EqualTo("M:1 Q:2 P:3 G:4"));
        }

        [Test]
        public void FormatHighlightLabel_WhenHighlightExists_ReturnsReadableLabel()
        {
            var label = BattleUiFormatter.FormatHighlightLabel(BattleHighlightState.AttackTarget);

            Assert.That(label, Is.EqualTo("[Attack]"));
        }
    }
}
