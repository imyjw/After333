using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Infrastructure.Data
{
    public static class TrapReturnCardRules
    {
        public const string CardId = "TrapReturn";
        public const string DisplayName = "트랩: 리턴";
        public const int ActivationGoldCost = 4;
        public const string SpecialEffectText = "트랩: 골드-4. 상대방 유닛이 내 마스터 공격 시, 그 유닛을 패로 되돌린다.(마스터 제외)";

        public static TrapSpellCardDefinition CreateDefinition() => new TrapSpellCardDefinition(CardId, DisplayName,
            new ResourceSet(0, 0, 0, ActivationGoldCost), TrapTriggerType.EnemyUnitAttacksMaster, TrapEffectType.ReturnAttackerToHand);
    }
}
