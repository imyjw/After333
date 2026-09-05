using System;

namespace Project333.Runtime.Domain.Cards
{
    public static class HeroRules
    {
        public const string CardId = "Hero";
        public const int ManaCost = 3;
        public const int BaseAttack = 3;
        public const int BaseHealth = 3;
        public const int PhysicalDefense = 0;
        public const int MagicDefense = 0;
        public const int InvincibleTurnEnds = 3;
        public const int GrowthAmount = 13;

        public static bool IsHero(OccupantState occupant)
        {
            return occupant != null &&
                   occupant.Kind == OccupantKind.Unit &&
                   string.Equals(occupant.CardId, CardId, StringComparison.Ordinal);
        }
    }
}
