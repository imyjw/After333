using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Infrastructure.Data
{
    public static class TrapMirrorFieldCardRules
    {
        public const string CardId = "TrapMirrorField";
        public const string DisplayName = "트랩-미러 필드";
        public const int ActivationManaCost = 2;
        public const string SpecialEffectText = "트랩: 마나-2. 상대방 유닛이 내 유닛을 공격 시, 공격을 취소하고 그 유닛에게 해당 유닛의 공격력만큼 마법 데미지를 입힌다.";

        public static TrapSpellCardDefinition CreateDefinition() => new TrapSpellCardDefinition(CardId, DisplayName,
            new ResourceSet(ActivationManaCost, 0, 0, 0), TrapTriggerType.EnemyUnitAttacksUnit,
            TrapEffectType.ReflectAttackerAttackAsMagic);
    }
}
