using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Infrastructure.Data
{
    public static class CardLevelStatRules
    {
        private const string RobotFactoryCardId = "RobotFactory";
        private static readonly int[] AttackBonusLevels = { 3, 6, 9, 13 };

        public static CardLevelStatBonus CalculateBonus(int upgradeLevel)
        {
            var normalizedLevel = NormalizeLevel(upgradeLevel);
            var attackBonus = CalculateAttackBonus(normalizedLevel);
            var hpBonus = normalizedLevel - attackBonus;
            return new CardLevelStatBonus(attackBonus, hpBonus);
        }

        public static CardLevelStatBonus CalculateBonus(string cardId, int upgradeLevel)
        {
            var normalizedLevel = NormalizeLevel(upgradeLevel);
            if (string.Equals(cardId, "A-212", System.StringComparison.Ordinal) ||
                string.Equals(cardId, "Cerberus", System.StringComparison.Ordinal))
            {
                var attackBonus = normalizedLevel == CardUpgradeRules.MaxLevel ? 1 : 0;
                return new CardLevelStatBonus(attackBonus, normalizedLevel - attackBonus);
            }

            if (string.Equals(cardId, DemonKingRules.CardId, System.StringComparison.Ordinal) ||
                string.Equals(cardId, HeroRules.CardId, System.StringComparison.Ordinal))
            {
                return CalculateLevelThirteenTripleAttackBonus(normalizedLevel);
            }

            return GrantsHpAtEveryLevel(cardId)
                ? new CardLevelStatBonus(0, normalizedLevel)
                : CalculateBonus(normalizedLevel);
        }

        private static CardLevelStatBonus CalculateLevelThirteenTripleAttackBonus(int normalizedLevel)
        {
            var attackBonus = 0;
            var attackMilestoneCount = 0;
            for (var i = 0; i < AttackBonusLevels.Length; i++)
            {
                var milestone = AttackBonusLevels[i];
                if (normalizedLevel < milestone)
                {
                    continue;
                }

                attackMilestoneCount++;
                attackBonus += milestone == CardUpgradeRules.MaxLevel ? 3 : 1;
            }

            return new CardLevelStatBonus(
                attackBonus,
                normalizedLevel - attackMilestoneCount);
        }

        private static bool GrantsHpAtEveryLevel(string cardId)
        {
            return string.Equals(cardId, RobotFactoryCardId, System.StringComparison.Ordinal) ||
                   string.Equals(cardId, "ManaPond", System.StringComparison.Ordinal) ||
                   string.Equals(cardId, GaebangBranchRules.CardId, System.StringComparison.Ordinal) ||
                   string.Equals(cardId, InnRules.CardId, System.StringComparison.Ordinal) ||
                   string.Equals(cardId, MerchantCaravanRules.CardId, System.StringComparison.Ordinal) ||
                   string.Equals(cardId, PowerPlantRules.CardId, System.StringComparison.Ordinal) ||
                   string.Equals(cardId, NuclearPowerPlantRules.CardId, System.StringComparison.Ordinal);
        }

        public static int ApplyAttackBonus(int baseAttack, int upgradeLevel)
        {
            return baseAttack + CalculateBonus(upgradeLevel).AttackBonus;
        }

        public static int ApplyAttackBonus(string cardId, int baseAttack, int upgradeLevel)
        {
            return baseAttack + CalculateBonus(cardId, upgradeLevel).AttackBonus;
        }

        public static int ApplyHpBonus(int baseHp, int upgradeLevel)
        {
            return baseHp + CalculateBonus(upgradeLevel).HpBonus;
        }

        public static int ApplyHpBonus(string cardId, int baseHp, int upgradeLevel)
        {
            return baseHp + CalculateBonus(cardId, upgradeLevel).HpBonus;
        }

        private static int CalculateAttackBonus(int normalizedLevel)
        {
            var attackBonus = 0;
            for (var i = 0; i < AttackBonusLevels.Length; i++)
            {
                if (normalizedLevel >= AttackBonusLevels[i])
                {
                    attackBonus++;
                }
            }

            return attackBonus;
        }

        private static int NormalizeLevel(int upgradeLevel)
        {
            if (upgradeLevel <= 0)
            {
                return 0;
            }

            return upgradeLevel > CardUpgradeRules.MaxLevel
                ? CardUpgradeRules.MaxLevel
                : upgradeLevel;
        }
    }
}
