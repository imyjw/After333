namespace Project333.Runtime.Domain.Cards
{
    public static class NuclearRules
    {
        public const string CardId = "Nuclear";
        public const string EffectId = "nuclear";
        public const int PowerCost = 13;
        public const int BaseDamage = 100;
        public const int FalloutDamage = 10;
        public const string EffectDescription = "(사용 즉시) 상대방 전장 전체에 고정 데미지 100을 입힌다. 사용 당시 상대 전장에 있는 유닛(마스터 포함)에 영구 낙진 +1중첩. 양쪽 턴 시작마다 중첩당 고정 피해 10.";
    }
}
