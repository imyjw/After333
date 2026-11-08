namespace Project333.Runtime.Domain.Cards
{
    public enum LegacyEffectKind
    {
        None = 0,
        DrawCards = 1,
        GlobalPhysicalDamage = 2,
    }

    // Shared registration for death/destruction effects. Card IDs and suppression
    // already survive cloning, online projection and persistence; no new wire flag.
    public static class LegacyRules
    {
        public const string TooltipDescription = "사망/파괴 시 효과를 발동한다.";
        public const string DrawDescription = "유산: 카드를 1장 드로우한다.";
        public const string ExplosionDescription = "유산: 전장 전체에 30의 물리 데미지를 입힌다.";

        public static LegacyEffectKind GetEffectKind(string cardId)
        {
            switch (cardId)
            {
                case "Zombie":
                case "SkeletonArcher":
                case "Skeleton": return LegacyEffectKind.DrawCards;
                case NuclearPowerPlantRules.CardId: return LegacyEffectKind.GlobalPhysicalDamage;
                default: return LegacyEffectKind.None;
            }
        }

        public static bool HasLegacy(string cardId) => GetEffectKind(cardId) != LegacyEffectKind.None;

        public static int GetAmount(string cardId)
        {
            switch (GetEffectKind(cardId))
            {
                case LegacyEffectKind.DrawCards: return 1;
                case LegacyEffectKind.GlobalPhysicalDamage: return NuclearPowerPlantRules.DestructionDamage;
                default: return 0;
            }
        }
    }
}
