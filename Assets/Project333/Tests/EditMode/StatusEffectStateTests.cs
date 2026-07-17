using NUnit.Framework;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Tests.EditMode
{
    public sealed class StatusEffectStateTests
    {
        [Test]
        public void Berserker_WhenActive_AddsLostHpToAttack()
        {
            var unit = CreateUnit("berserker", attack: 10, maxHp: 40, hasBerserker: true);

            unit.CurrentHp = 25;

            Assert.That(unit.Attack, Is.EqualTo(25));
        }

        [Test]
        public void Berserker_WhenDrainedOrErasure_UsesBaseAttack()
        {
            var unit = CreateUnit("berserker", attack: 10, maxHp: 40, hasBerserker: true);

            unit.CurrentHp = 25;
            unit.IsDrained = true;
            Assert.That(unit.Attack, Is.EqualTo(10));

            unit.IsDrained = false;
            unit.ApplyErasure();
            Assert.That(unit.Attack, Is.EqualTo(10));
        }

        [Test]
        public void Guard_WhenDrainedOrErasure_IsInactive()
        {
            var unit = CreateUnit("guard", attack: 0, maxHp: 40, hasGuard: true);

            Assert.That(unit.HasActiveGuard, Is.True);

            unit.IsDrained = true;
            Assert.That(unit.HasActiveGuard, Is.False);

            unit.IsDrained = false;
            unit.ApplyErasure();
            Assert.That(unit.HasActiveGuard, Is.False);
        }

        [Test]
        public void Erasure_WhenApplied_ClearsDrainedAndRemovesExistingCombatModifiers()
        {
            var unit = new UnitState(
                runtimeId: "erasure-target",
                cardId: "erasure-target",
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 10,
                maxHp: 40,
                canMove: true,
                isScience: true,
                sciencePowerUpkeep: 1,
                maxAttacksPerTurn: 3,
                hitsPerAttack: 3,
                hasRobot: true,
                physicalDefense: 3,
                magicDefense: 2);
            unit.CurrentHp = 25;
            unit.IncreaseBaseAttack(7);
            unit.IncreaseMaxHpAndCurrentHp(10);
            unit.IncreasePhysicalDefense(4);
            unit.IncreaseMagicDefense(5);
            unit.RemainingAttacksThisTurn = 2;
            unit.IsDrained = true;

            unit.ApplyErasure();

            Assert.That(unit.IsErasure, Is.True);
            Assert.That(unit.IsDrained, Is.False);
            Assert.That(unit.BaseAttack, Is.EqualTo(10));
            Assert.That(unit.MaxHp, Is.EqualTo(40));
            Assert.That(unit.CurrentHp, Is.EqualTo(35), "Removing a max-HP bonus must not heal the unit.");
            Assert.That(unit.PhysicalDefense, Is.EqualTo(3));
            Assert.That(unit.MagicDefense, Is.EqualTo(2));
            Assert.That(unit.EffectiveHitsPerAttack, Is.EqualTo(1));
            Assert.That(unit.EffectiveMaxAttacksPerTurn, Is.EqualTo(1));
            Assert.That(unit.RemainingAttacksThisTurn, Is.EqualTo(0));
            Assert.That(unit.HasActiveRobot, Is.False);
            Assert.That(unit.CannotAttack, Is.False);
            Assert.That(unit.CannotCounterattack, Is.False);
            Assert.That(unit.CannotMoveDueToState, Is.False);
            Assert.That(unit.DoesNotBlockFrontRow, Is.False);
        }

        [Test]
        public void Erasure_BuffsAppliedAfterwardRemainActive()
        {
            var unit = CreateUnit("erasure-buffed", attack: 10, maxHp: 40);
            unit.ApplyErasure();

            unit.IncreaseBaseAttack(4);
            unit.IncreaseMaxHpAndCurrentHp(5);
            unit.IncreasePhysicalDefense(2);
            unit.IncreaseMagicDefense(3);

            Assert.That(unit.Attack, Is.EqualTo(14));
            Assert.That(unit.MaxHp, Is.EqualTo(45));
            Assert.That(unit.CurrentHp, Is.EqualTo(45));
            Assert.That(unit.PhysicalDefense, Is.EqualTo(2));
            Assert.That(unit.MagicDefense, Is.EqualTo(3));
        }

        [Test]
        public void Erasure_PreventsDrainedFromBeingAppliedLater()
        {
            var unit = CreateUnit("erasure-upkeep", attack: 10, maxHp: 40);
            unit.ApplyErasure();

            unit.IsDrained = true;

            Assert.That(unit.IsDrained, Is.False);
            Assert.That(unit.IsErasure, Is.True);
        }

        private static UnitState CreateUnit(
            string id,
            int attack,
            int maxHp,
            bool hasBerserker = false,
            bool hasGuard = false)
        {
            return new UnitState(
                runtimeId: id,
                cardId: id,
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: attack,
                maxHp: maxHp,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hasBerserker: hasBerserker,
                hasGuard: hasGuard);
        }
    }
}
