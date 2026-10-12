using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Infrastructure.Data
{
    public static class SummonTrapCardRules
    {
        public const string CardId = "SummonTrap";
        public const string DisplayName = "트랩 - 서먼 트랩";
        public const int ActivationGoldCost = 1;
        public const int BaseDamage = 20;
        public const string SpecialEffectText = "트랩: 골드-1. 상대방이 유닛을 소환하면 그 유닛에게 물리 데미지 20을 입힌다.";

        public static TrapSpellCardDefinition CreateDefinition() => new TrapSpellCardDefinition(CardId, DisplayName,
            new ResourceSet(0, 0, 0, ActivationGoldCost), TrapTriggerType.EnemyUnitSummoned,
            TrapEffectType.DamageSummonedUnitPhysical, damage: BaseDamage);
    }
}
