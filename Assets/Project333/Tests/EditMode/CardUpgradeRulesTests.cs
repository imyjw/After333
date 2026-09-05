using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class CardUpgradeRulesTests
    {
        private static readonly string[] ExpectedUpgradeableCardIds =
        {
            "A-111",
            "A-301",
            "BiochemicalBomb",
            "BlueDragon",
            "Cerberus",
            "DemonKing",
            "ElfLongbowScout",
            "firebolt",
            "Firewall",
            "GaebangBranch",
            "Goblin",
            "GoldMiner",
            "Golem",
            "Gwangma",
            "Hero",
            "Inn",
            "ManaPond",
            "MerchantCaravan",
            "ManaWeaver",
            "NuclearPowerPlant",
            "OrcWarrior",
            "PowerPlant",
            "RedDragon",
            "RobotFactory",
            "Shaolin_1st_Disciple",
            "Shieldbearer",
            "Skeleton",
            "TimedBomb",
            "Vampire",
            "Werewolf",
            "Zombie",
        };

        private static readonly string[] ExpectedNonUpgradeableCardIds =
        {
            "CheonraJimang",
            "Daehwandan",
            "Gu",
            "HuanShu",
            "ManaStone",
            "ManaStoneBundle",
            "Microreactor",
            "PowerBank",
            "RobotFusion",
            "TenThousandYearSnowGinseng",
        };

        [Test]
        public void TryGetNextUpgradeCost_FromLevelZero_UsesTargetLevelCost()
        {
            var hasCost = CardUpgradeRules.TryGetNextUpgradeCost(0, out var cost);

            Assert.That(hasCost, Is.True);
            Assert.That(cost.LevelFrom, Is.EqualTo(0));
            Assert.That(cost.LevelTo, Is.EqualTo(1));
            Assert.That(cost.RequiredCopyCount, Is.EqualTo(3));
            Assert.That(cost.RequiredResourceGold, Is.EqualTo(3));
        }

        [Test]
        public void TryGetNextUpgradeCost_ToMaxLevel_UsesTargetLevelCost()
        {
            var hasCost = CardUpgradeRules.TryGetNextUpgradeCost(12, out var cost);

            Assert.That(hasCost, Is.True);
            Assert.That(cost.LevelFrom, Is.EqualTo(12));
            Assert.That(cost.LevelTo, Is.EqualTo(13));
            Assert.That(cost.RequiredCopyCount, Is.EqualTo(39));
            Assert.That(cost.RequiredResourceGold, Is.EqualTo(39));
        }

        [Test]
        public void TryGetNextUpgradeCost_AtMaxLevel_ReturnsFalse()
        {
            var hasCost = CardUpgradeRules.TryGetNextUpgradeCost(13, out _);

            Assert.That(hasCost, Is.False);
            Assert.That(CardUpgradeRules.IsMaxLevel(13), Is.True);
        }

        [TestCase(-1, 10)]
        [TestCase(0, 10)]
        [TestCase(13, 23)]
        [TestCase(14, 10)]
        [TestCase(99, 10)]
        public void ApplyDamageBonus_UsesLevelZeroOutsideSupportedRange(
            int upgradeLevel,
            int expectedDamage)
        {
            Assert.That(
                CardLevelSpellRules.ApplyDamageBonus(baseDamage: 10, upgradeLevel),
                Is.EqualTo(expectedDamage));
        }

        [TestCase(0, 0, 0)]
        [TestCase(1, 0, 1)]
        [TestCase(2, 0, 2)]
        [TestCase(3, 1, 2)]
        [TestCase(6, 2, 4)]
        [TestCase(9, 3, 6)]
        [TestCase(13, 4, 9)]
        public void CalculateBonus_UsesAttackBonusAtMilestoneLevels(
            int upgradeLevel,
            int expectedAttackBonus,
            int expectedHpBonus)
        {
            var bonus = CardLevelStatRules.CalculateBonus(upgradeLevel);

            Assert.That(bonus.AttackBonus, Is.EqualTo(expectedAttackBonus));
            Assert.That(bonus.HpBonus, Is.EqualTo(expectedHpBonus));
        }

        [Test]
        public void CalculateBonus_ClampsLevelsToSupportedRange()
        {
            var belowMinimum = CardLevelStatRules.CalculateBonus(-3);
            var aboveMaximum = CardLevelStatRules.CalculateBonus(99);

            Assert.That(belowMinimum.AttackBonus, Is.EqualTo(0));
            Assert.That(belowMinimum.HpBonus, Is.EqualTo(0));
            Assert.That(aboveMaximum.AttackBonus, Is.EqualTo(4));
            Assert.That(aboveMaximum.HpBonus, Is.EqualTo(9));
        }

        [TestCase(2, 0, 2)]
        [TestCase(3, 1, 2)]
        [TestCase(6, 2, 4)]
        [TestCase(9, 3, 6)]
        [TestCase(13, 4, 9)]
        public void CalculateBonus_A301_UsesStandardUnitMilestones(
            int upgradeLevel,
            int expectedAttackBonus,
            int expectedHpBonus)
        {
            var bonus = CardLevelStatRules.CalculateBonus("A-301", upgradeLevel);

            Assert.That(bonus.AttackBonus, Is.EqualTo(expectedAttackBonus));
            Assert.That(bonus.HpBonus, Is.EqualTo(expectedHpBonus));
        }

        [TestCase(2, 0, 2)]
        [TestCase(3, 1, 2)]
        [TestCase(6, 2, 4)]
        [TestCase(9, 3, 6)]
        [TestCase(13, 4, 9)]
        public void CalculateBonus_Werewolf_UsesStandardUnitMilestones(
            int upgradeLevel,
            int expectedAttackBonus,
            int expectedHpBonus)
        {
            var bonus = CardLevelStatRules.CalculateBonus(WerewolfRules.CardId, upgradeLevel);

            Assert.That(bonus.AttackBonus, Is.EqualTo(expectedAttackBonus));
            Assert.That(bonus.HpBonus, Is.EqualTo(expectedHpBonus));
        }

        [TestCase(2, 0, 2)]
        [TestCase(3, 1, 2)]
        [TestCase(6, 2, 4)]
        [TestCase(9, 3, 6)]
        [TestCase(12, 3, 9)]
        [TestCase(13, 6, 9)]
        public void CalculateBonus_DemonKing_UsesTripleAttackBonusAtLevelThirteen(
            int upgradeLevel,
            int expectedAttackBonus,
            int expectedHpBonus)
        {
            var bonus = CardLevelStatRules.CalculateBonus(DemonKingRules.CardId, upgradeLevel);

            Assert.That(bonus.AttackBonus, Is.EqualTo(expectedAttackBonus));
            Assert.That(bonus.HpBonus, Is.EqualTo(expectedHpBonus));
        }

        [TestCase(2, 0, 2)]
        [TestCase(3, 1, 2)]
        [TestCase(6, 2, 4)]
        [TestCase(9, 3, 6)]
        [TestCase(12, 3, 9)]
        [TestCase(13, 6, 9)]
        public void CalculateBonus_Hero_UsesTripleAttackBonusAtLevelThirteen(
            int upgradeLevel,
            int expectedAttackBonus,
            int expectedHpBonus)
        {
            var bonus = CardLevelStatRules.CalculateBonus(HeroRules.CardId, upgradeLevel);

            Assert.That(bonus.AttackBonus, Is.EqualTo(expectedAttackBonus));
            Assert.That(bonus.HpBonus, Is.EqualTo(expectedHpBonus));
        }

        [TestCase(1, 0, 1)]
        [TestCase(3, 0, 3)]
        [TestCase(13, 0, 13)]
        public void CalculateBonus_RobotFactory_GrantsOnlyHpAtEveryLevel(
            int upgradeLevel,
            int expectedAttackBonus,
            int expectedHpBonus)
        {
            var bonus = CardLevelStatRules.CalculateBonus("RobotFactory", upgradeLevel);

            Assert.That(bonus.AttackBonus, Is.EqualTo(expectedAttackBonus));
            Assert.That(bonus.HpBonus, Is.EqualTo(expectedHpBonus));
        }

        [TestCase(1, 0, 1)]
        [TestCase(3, 0, 3)]
        [TestCase(13, 0, 13)]
        public void CalculateBonus_GaebangBranch_GrantsOnlyHpAtEveryLevel(
            int upgradeLevel,
            int expectedAttackBonus,
            int expectedHpBonus)
        {
            var bonus = CardLevelStatRules.CalculateBonus("GaebangBranch", upgradeLevel);

            Assert.That(bonus.AttackBonus, Is.EqualTo(expectedAttackBonus));
            Assert.That(bonus.HpBonus, Is.EqualTo(expectedHpBonus));
        }

        [TestCase(1, 0, 1)]
        [TestCase(3, 0, 3)]
        [TestCase(13, 0, 13)]
        public void CalculateBonus_MerchantCaravan_GrantsOnlyHpAtEveryLevel(
            int upgradeLevel,
            int expectedAttackBonus,
            int expectedHpBonus)
        {
            var bonus = CardLevelStatRules.CalculateBonus(
                MerchantCaravanRules.CardId,
                upgradeLevel);

            Assert.That(bonus.AttackBonus, Is.EqualTo(expectedAttackBonus));
            Assert.That(bonus.HpBonus, Is.EqualTo(expectedHpBonus));
        }

        [TestCase(1, 0, 1)]
        [TestCase(3, 0, 3)]
        [TestCase(13, 0, 13)]
        public void CalculateBonus_Inn_GrantsOnlyHpAtEveryLevel(
            int upgradeLevel,
            int expectedAttackBonus,
            int expectedHpBonus)
        {
            var bonus = CardLevelStatRules.CalculateBonus(InnRules.CardId, upgradeLevel);

            Assert.That(bonus.AttackBonus, Is.EqualTo(expectedAttackBonus));
            Assert.That(bonus.HpBonus, Is.EqualTo(expectedHpBonus));
        }

        [TestCase(1, 0, 1)]
        [TestCase(3, 0, 3)]
        [TestCase(13, 0, 13)]
        public void CalculateBonus_PowerPlant_GrantsOnlyHpAtEveryLevel(
            int upgradeLevel,
            int expectedAttackBonus,
            int expectedHpBonus)
        {
            var bonus = CardLevelStatRules.CalculateBonus("PowerPlant", upgradeLevel);

            Assert.That(bonus.AttackBonus, Is.EqualTo(expectedAttackBonus));
            Assert.That(bonus.HpBonus, Is.EqualTo(expectedHpBonus));
        }

        [TestCase(1, 0, 1)]
        [TestCase(3, 0, 3)]
        [TestCase(13, 0, 13)]
        public void CalculateBonus_NuclearPowerPlant_GrantsOnlyHpAtEveryLevel(
            int upgradeLevel,
            int expectedAttackBonus,
            int expectedHpBonus)
        {
            var bonus = CardLevelStatRules.CalculateBonus(
                NuclearPowerPlantRules.CardId,
                upgradeLevel);

            Assert.That(bonus.AttackBonus, Is.EqualTo(expectedAttackBonus));
            Assert.That(bonus.HpBonus, Is.EqualTo(expectedHpBonus));
        }

        [Test]
        public void IsCardUpgradeable_CurrentCardsJson_MatchesLockedPolicy()
        {
            var path = Path.Combine(Application.dataPath, "Project333/Resources/Project333/Data/cards.json");
            var database = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path));
            var definitions = database.ToDefinitions();

            var upgradeableCardIds = definitions
                .Where(CardUpgradeRules.IsCardUpgradeable)
                .Select(definition => definition.CardId)
                .ToArray();
            var nonUpgradeableCardIds = definitions
                .Where(definition => !CardUpgradeRules.IsCardUpgradeable(definition))
                .Select(definition => definition.CardId)
                .ToArray();

            Assert.That(upgradeableCardIds, Is.EquivalentTo(ExpectedUpgradeableCardIds));
            Assert.That(nonUpgradeableCardIds, Is.EquivalentTo(ExpectedNonUpgradeableCardIds));
        }
    }
}
