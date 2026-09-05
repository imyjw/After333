using System;

namespace Project333.Runtime.Domain.Cards
{
    public static class DemonKingRules
    {
        public const string CardId = "DemonKing";
        public const int ManaCost = 3;
        public const int GoldCost = 3;
        public const int BaseAttack = 33;
        public const int BaseHealth = 33;
        public const int PhysicalDefense = 3;
        public const int MagicDefense = 3;
        public const int RevivalTurnStarts = 3;
        public const int RevivalStatGain = 33;

        public static bool IsDemonKing(OccupantState occupant)
        {
            return occupant != null &&
                   occupant.Kind == OccupantKind.Unit &&
                   string.Equals(occupant.CardId, CardId, StringComparison.Ordinal);
        }

        public static int CalculateRevivedStat(int originalStat, int revivalCount)
        {
            var normalizedCount = Math.Max(0, revivalCount);
            var result = originalStat + ((long)normalizedCount * RevivalStatGain);
            return result >= int.MaxValue ? int.MaxValue : (int)result;
        }
    }
}
