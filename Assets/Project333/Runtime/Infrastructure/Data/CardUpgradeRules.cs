using System;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Infrastructure.Data
{
    public static class CardUpgradeRules
    {
        public const int MaxLevel = 13;
        public const int CostMultiplier = 3;
        private const string FireboltCardId = "firebolt";
        private const string FirewallCardId = "Firewall";

        public static bool IsCardUpgradeable(CardDefinition cardDefinition)
        {
            if (cardDefinition == null)
            {
                return false;
            }

            switch (cardDefinition.CardType)
            {
                case CardType.Unit:
                case CardType.Building:
                    return true;

                case CardType.Spell:
                    return string.Equals(cardDefinition.CardId, FireboltCardId, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(cardDefinition.CardId, FirewallCardId, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(cardDefinition.CardId, TimedBombRules.CardId, StringComparison.OrdinalIgnoreCase);

                default:
                    return false;
            }
        }

        public static bool IsMaxLevel(int currentLevel)
        {
            return currentLevel >= MaxLevel;
        }

        public static bool TryGetNextUpgradeCost(int currentLevel, out CardUpgradeCost cost)
        {
            var normalizedCurrentLevel = currentLevel < 0 ? 0 : currentLevel;
            if (normalizedCurrentLevel >= MaxLevel)
            {
                cost = default;
                return false;
            }

            var nextLevel = normalizedCurrentLevel + 1;
            var requiredAmount = nextLevel * CostMultiplier;
            cost = new CardUpgradeCost(
                normalizedCurrentLevel,
                nextLevel,
                requiredAmount,
                requiredAmount);
            return true;
        }
    }
}
