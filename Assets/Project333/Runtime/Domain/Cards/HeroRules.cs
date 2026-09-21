using System;

namespace Project333.Runtime.Domain.Cards
{
    public static class HeroRules
    {
        public const string CardId = "Hero";
        public const int ManaCost = 3;
        public const int BaseAttack = 33;
        public const int BaseHealth = 33;
        public const int PhysicalDefense = 0;
        public const int MagicDefense = 0;
        public const int GrowthAmount = 33;
        public const string EffectDescription = "상대방 소환물을 공격해 파괴할 때마다 공격력 또는 체력+33";

        public static bool IsHero(OccupantState occupant)
        {
            return occupant != null &&
                   occupant.Kind == OccupantKind.Unit &&
                   string.Equals(occupant.CardId, CardId, StringComparison.Ordinal);
        }
    }
}
