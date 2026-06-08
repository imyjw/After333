using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Tests.EditMode
{
    public sealed class CombatResolutionTests
    {
        [Test]
        public void Attack_MeleeVersusMeleeDealsSimultaneousDamage()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateCombatUnit("attacker", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 5, 10);
            var defender = CreateCombatUnit("defender", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 3, 15);

            attacker.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(attacker.CurrentHp, Is.EqualTo(7));
            Assert.That(defender.CurrentHp, Is.EqualTo(10));
        }

        [Test]
        public void Attack_MeleeVersusRangedDoesNotCounterattack()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateCombatUnit("attacker", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 5, 10);
            var defender = CreateCombatUnit("defender", PlayerId.AI, new TileCoord(0, 0), AttackType.Ranged, 2, 7);

            attacker.HasSummoningSickness = false;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(attacker.CurrentHp, Is.EqualTo(10));
            Assert.That(defender.CurrentHp, Is.EqualTo(2));
            Assert.That(battleState.AIBoard.GetOccupant(new TileCoord(0, 0)), Is.SameAs(defender));
        }

        [Test]
        public void Attack_WithZeroAttackThrowsAndDoesNotResolve()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateCombatUnit("attacker", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 0, 10);
            var defender = CreateCombatUnit("defender", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 3, 7);

            attacker.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            Assert.That(
                () => attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0)),
                Throws.InvalidOperationException.With.Message.Contains("0 attack"));
        }

        [Test]
        public void Attack_MeleeVersusZeroAttackMeleeDoesNotCounterattack()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateCombatUnit("attacker", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 5, 10);
            var defender = CreateCombatUnit("defender", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 0, 7);

            attacker.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(attacker.CurrentHp, Is.EqualTo(10));
            Assert.That(defender.CurrentHp, Is.EqualTo(2));
        }

        [Test]
        public void Attack_RangedVersusProtectedBackRow_HitsGuardInsteadOfOriginalTarget()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateCombatUnit("attacker", PlayerId.Player, new TileCoord(0, 0), AttackType.Ranged, 4, 7);
            var guard = CreateCombatUnit("guard", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 0, 6, hasGuard: true);
            var defender = CreateCombatUnit("defender", PlayerId.AI, new TileCoord(0, 1), AttackType.Melee, 2, 5);

            attacker.HasSummoningSickness = false;
            guard.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), guard);
            battleState.AIBoard.Place(new TileCoord(0, 1), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 1));

            Assert.That(guard.CurrentHp, Is.EqualTo(2));
            Assert.That(defender.CurrentHp, Is.EqualTo(5));
        }

        [Test]
        public void Attack_RangedVersusProtectedBackRow_WhenGuardHpIsInsufficient_SpillsRemainingDamageToOriginalTarget()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateCombatUnit("attacker", PlayerId.Player, new TileCoord(0, 0), AttackType.Ranged, 8, 7);
            var guard = CreateCombatUnit("guard", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 0, 3, hasGuard: true);
            var defender = CreateCombatUnit("defender", PlayerId.AI, new TileCoord(0, 1), AttackType.Melee, 2, 6);

            attacker.HasSummoningSickness = false;
            guard.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), guard);
            battleState.AIBoard.Place(new TileCoord(0, 1), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 1));

            Assert.That(battleState.AIBoard.GetOccupant(new TileCoord(0, 0)), Is.Null);
            Assert.That(defender.CurrentHp, Is.EqualTo(1));
            Assert.That(battleState.AIBoard.GetOccupant(new TileCoord(0, 1)), Is.SameAs(defender));
        }

        [Test]
        public void Attack_RangedVersusMeleeDoesNotCounterattack()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateCombatUnit("attacker", PlayerId.Player, new TileCoord(0, 0), AttackType.Ranged, 2, 7);
            var defender = CreateCombatUnit("defender", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 5, 10);

            attacker.HasSummoningSickness = false;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(attacker.CurrentHp, Is.EqualTo(7));
            Assert.That(defender.CurrentHp, Is.EqualTo(8));
        }

        [Test]
        public void Attack_DoubleHitMeleeVersusMelee_CountersOncePerAttackAction()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateCombatUnit("attacker", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 3, 10, hitsPerAttack: 2);
            var defender = CreateCombatUnit("defender", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 4, 6);

            attacker.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(attacker.CurrentHp, Is.EqualTo(6));
            Assert.That(battleState.AIBoard.GetOccupant(new TileCoord(0, 0)), Is.Null);
        }

        [Test]
        public void Attack_TripleHitRangedVersusMelee_DealsSequentialHitsWithoutCounterattack()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateCombatUnit("attacker", PlayerId.Player, new TileCoord(0, 0), AttackType.Ranged, 2, 7, hitsPerAttack: 3);
            var defender = CreateCombatUnit("defender", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 5, 7);

            attacker.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(attacker.CurrentHp, Is.EqualTo(7));
            Assert.That(defender.CurrentHp, Is.EqualTo(1));
        }

        [Test]
        public void Attack_BerserkerDoesNotGainAttackFromFinalSimultaneousCounterattack()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateCombatUnit(
                "attacker",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Melee,
                3,
                10,
                hitsPerAttack: 2,
                hasBerserker: true);
            var defender = CreateCombatUnit("defender", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 4, 10);

            attacker.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(attacker.CurrentHp, Is.EqualTo(6));
            Assert.That(defender.CurrentHp, Is.EqualTo(4));
        }

        [Test]
        public void Attack_PreDamagedBerserkerUsesBoostedAttackForEachHit()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateCombatUnit(
                "attacker",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Ranged,
                3,
                10,
                hitsPerAttack: 2,
                hasBerserker: true);
            var defender = CreateCombatUnit("defender", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 4, 12);

            attacker.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;
            attacker.CurrentHp = 7;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(attacker.Attack, Is.EqualTo(6));
            Assert.That(battleState.AIBoard.GetOccupant(new TileCoord(0, 0)), Is.Null);
        }

        [Test]
        public void Attack_EndureTriggersOnFirstLethalAttackAndLeavesTargetAtOne()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateCombatUnit("attacker", PlayerId.Player, new TileCoord(0, 0), AttackType.Ranged, 5, 10);
            var defender = CreateCombatUnit("defender", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 1, 5, hasEndure: true);

            attacker.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(defender.CurrentHp, Is.EqualTo(1));
            Assert.That(defender.EndureUsed, Is.True);
            Assert.That(battleState.AIBoard.GetOccupant(new TileCoord(0, 0)), Is.SameAs(defender));
        }

        [Test]
        public void Attack_EndureConsumedByEarlierHit_RemainingHitsCanStillKill()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateCombatUnit("attacker", PlayerId.Player, new TileCoord(0, 0), AttackType.Ranged, 3, 10, hitsPerAttack: 2);
            var defender = CreateCombatUnit("defender", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 1, 2, hasEndure: true);

            attacker.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(defender.EndureUsed, Is.True);
            Assert.That(battleState.AIBoard.GetOccupant(new TileCoord(0, 0)), Is.Null);
        }

        [Test]
        public void Attack_EndureCanSaveFromFinalSimultaneousCounterattack()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateCombatUnit("attacker", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 3, 5, hasEndure: true);
            var defender = CreateCombatUnit("defender", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 6, 5);

            attacker.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(attacker.CurrentHp, Is.EqualTo(1));
            Assert.That(attacker.EndureUsed, Is.True);
            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(0, 0)), Is.SameAs(attacker));
        }

        [Test]
        public void Attack_RemovesDefeatedOccupantImmediately()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateCombatUnit("attacker", PlayerId.Player, new TileCoord(0, 0), AttackType.Ranged, 5, 10);
            var defender = CreateCombatUnit("defender", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 1, 5);

            attacker.HasSummoningSickness = false;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(battleState.AIBoard.GetOccupant(new TileCoord(0, 0)), Is.Null);
        }

        [Test]
        public void Attack_AgainstDisabledUnitDealsTripleDamage()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateCombatUnit("attacker", PlayerId.Player, new TileCoord(0, 0), AttackType.Ranged, 2, 7);
            var defender = CreateCombatUnit("defender", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 1, 10);

            attacker.HasSummoningSickness = false;
            defender.IsDisabled = true;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(defender.CurrentHp, Is.EqualTo(4));
        }

        private static UnitState CreateCombatUnit(
            string id,
            PlayerId ownerId,
            TileCoord coord,
            AttackType attackType,
            int attack,
            int maxHp,
            int hitsPerAttack = 1,
            bool hasBerserker = false,
            bool hasEndure = false,
            bool hasGuard = false)
        {
            return new UnitState(
                runtimeId: id,
                cardId: id,
                ownerId: ownerId,
                position: coord,
                attackType: attackType,
                attack: attack,
                maxHp: maxHp,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hitsPerAttack: hitsPerAttack,
                hasBerserker: hasBerserker,
                hasEndure: hasEndure,
                hasGuard: hasGuard);
        }

        private static BattleState CreateBattleState()
        {
            var setupService = new BattleSetupService();
            var mulliganService = new MulliganService();
            var battleState = setupService.CreateInitialState(new BattleSetupRequest(CreateDeck("P"), CreateDeck("A"), PlayerId.Player));
            mulliganService.PassMulligan(battleState, PlayerId.Player);
            battleState.SetPhase(PhaseType.Main);
            return battleState;
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

