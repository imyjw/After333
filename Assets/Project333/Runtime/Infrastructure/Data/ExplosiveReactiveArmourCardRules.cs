using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Infrastructure.Data
{
    public static class ExplosiveReactiveArmourCardRules
    {
        public const string CardId = "ExplosiveReactiveArmour";
        public const string DisplayName = "트랩 - 반응 장갑";
        public const int ActivationPowerCost = 4;
        public const string SpecialEffectText = "트랩: 전력-4. 상대방 유닛이 내 마스터를 근접 공격 시, 그 공격을 취소하고 그 유닛을 파괴한다.(마스터 제외)";

        public static TrapSpellCardDefinition CreateDefinition() => new TrapSpellCardDefinition(CardId, DisplayName,
            new ResourceSet(0, 0, ActivationPowerCost, 0), TrapTriggerType.EnemyMeleeUnitAttacksMaster,
            TrapEffectType.DestroyAttacker);
    }
}
