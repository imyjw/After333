using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Tests.EditMode
{
    public sealed class VampireEffectTests
    {
        [Test]
        public void Attack_LifeStealHealsFromDamageDealtToMaster()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var vampire = CreateLifeStealUnit("vampire", "life-steal-unit", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 20, 50);

            vampire.HasSummoningSickness = false;
            vampire.CurrentHp = 20;
            battleState.PlayerBoard.Place(new TileCoord(0, 0), vampire);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(2, 1));

            Assert.That(vampire.CurrentHp, Is.EqualTo(37));
            Assert.That(battleState.AI.Master.CurrentHp, Is.EqualTo(313));
        }

        [Test]
        public void Attack_VampireCardIdWithoutLifeStealDoesNotHeal()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var vampireByNameOnly = CreateCombatUnit("vampire", "Vampire", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 20, 50);

            vampireByNameOnly.HasSummoningSickness = false;
            vampireByNameOnly.CurrentHp = 20;
            battleState.PlayerBoard.Place(new TileCoord(0, 0), vampireByNameOnly);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(2, 1));

            Assert.That(vampireByNameOnly.CurrentHp, Is.EqualTo(17));
            Assert.That(battleState.AI.Master.CurrentHp, Is.EqualTo(313));
        }

        [Test]
        public void Attack_LifeStealCannotSaveAttackerDefeatedByCounterattack()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var vampire = CreateLifeStealUnit("vampire", "life-steal-unit", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 20, 50);
            var defender = CreateCombatUnit("defender", "defender", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 10, 20);

            vampire.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;
            vampire.CurrentHp = 5;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), vampire);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(0, 0)), Is.Null);
            Assert.That(battleState.AIBoard.GetOccupant(new TileCoord(0, 0)), Is.Null);
        }

        [Test]
        public void Attack_LifeStealHealsAfterCounterattackDamageIsApplied()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var vampire = CreateLifeStealUnit("vampire", "life-steal-unit", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 5, 20);
            var defender = CreateCombatUnit("defender", "defender", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 10, 5);

            vampire.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;
            vampire.CurrentHp = 18;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), vampire);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(vampire.CurrentHp, Is.EqualTo(13));
            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(0, 0)), Is.SameAs(vampire));
            Assert.That(battleState.AIBoard.GetOccupant(new TileCoord(0, 0)), Is.Null);
        }

        [Test]
        public void Attack_WhenBothMeleeUnitsHaveLifeSteal_BothHealAfterDamageExchange()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateLifeStealUnit("vampire-a", "Vampire", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 20, 50);
            var defender = CreateLifeStealUnit("vampire-b", "Vampire", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 20, 43);

            attacker.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(attacker.CurrentHp, Is.EqualTo(50));
            Assert.That(defender.CurrentHp, Is.EqualTo(43));
            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(0, 0)), Is.SameAs(attacker));
            Assert.That(battleState.AIBoard.GetOccupant(new TileCoord(0, 0)), Is.SameAs(defender));
        }

        [Test]
        public void Attack_WhenLifeStealDefenderIsDefeatedBeforeHealing_DefenderDoesNotHeal()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateLifeStealUnit("vampire-a", "Vampire", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 20, 50);
            var defender = CreateLifeStealUnit("vampire-b", "Vampire", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 20, 50);

            attacker.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;
            defender.CurrentHp = 4;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(attacker.CurrentHp, Is.EqualTo(34));
            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(0, 0)), Is.SameAs(attacker));
            Assert.That(battleState.AIBoard.GetOccupant(new TileCoord(0, 0)), Is.Null);
        }

        [Test]
        public void Attack_LifeStealAttackerDefeatedByCounterattackDoesNotHeal()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var vampire = CreateLifeStealUnit("vampire", "life-steal-unit", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 3, 20);
            var defender = CreateCombatUnit("defender", "defender", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 10, 30);

            vampire.HasSummoningSickness = false;
            defender.HasSummoningSickness = false;
            vampire.CurrentHp = 5;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), vampire);
            battleState.AIBoard.Place(new TileCoord(0, 0), defender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(0, 0)), Is.Null);
            Assert.That(defender.CurrentHp, Is.EqualTo(27));
        }

        [Test]
        public void Counterattack_LifeStealHealsFromCounterattackDamage()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateCombatUnit("attacker", "attacker", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 10, 30);
            var vampireDefender = CreateLifeStealUnit("vampire-defender", "life-steal-unit", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 7, 20);

            attacker.HasSummoningSickness = false;
            vampireDefender.HasSummoningSickness = false;
            vampireDefender.CurrentHp = 15;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), vampireDefender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(attacker.CurrentHp, Is.EqualTo(23));
            Assert.That(vampireDefender.CurrentHp, Is.EqualTo(12));
            Assert.That(battleState.AIBoard.GetOccupant(new TileCoord(0, 0)), Is.SameAs(vampireDefender));
        }

        [Test]
        public void Counterattack_LifeStealCounterattackerDefeatedByFinalHitDoesNotHeal()
        {
            var battleState = CreateBattleState();
            var attackService = new AttackService();
            var attacker = CreateCombatUnit("attacker", "attacker", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 10, 30);
            var vampireDefender = CreateLifeStealUnit("vampire-defender", "life-steal-unit", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 7, 20);

            attacker.HasSummoningSickness = false;
            vampireDefender.HasSummoningSickness = false;
            vampireDefender.CurrentHp = 5;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), attacker);
            battleState.AIBoard.Place(new TileCoord(0, 0), vampireDefender);

            attackService.Attack(battleState, PlayerId.Player, new TileCoord(0, 0), new TileCoord(0, 0));

            Assert.That(attacker.CurrentHp, Is.EqualTo(23));
            Assert.That(battleState.AIBoard.GetOccupant(new TileCoord(0, 0)), Is.Null);
        }

        private static UnitState CreateCombatUnit(
            string runtimeId,
            string cardId,
            PlayerId ownerId,
            TileCoord coord,
            AttackType attackType,
            int attack,
            int maxHp)
        {
            return new UnitState(
                runtimeId: runtimeId,
                cardId: cardId,
                ownerId: ownerId,
                position: coord,
                attackType: attackType,
                attack: attack,
                maxHp: maxHp,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0);
        }

        private static UnitState CreateLifeStealUnit(
            string runtimeId,
            string cardId,
            PlayerId ownerId,
            TileCoord coord,
            AttackType attackType,
            int attack,
            int maxHp)
        {
            return new UnitState(
                runtimeId: runtimeId,
                cardId: cardId,
                ownerId: ownerId,
                position: coord,
                attackType: attackType,
                attack: attack,
                maxHp: maxHp,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hasLifeSteal: true);
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
