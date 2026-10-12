namespace Project333.Runtime.Domain.Cards
{
    public static class EarthquakeRules
    {
        public const string CardId = "Earthquake";
        public const string EffectId = "earthquake";
        public const int GoldCost = 3;
        public const int BaseDamage = 10;
        public const int BuildingDamageMultiplier = 3;
        public const string EffectDescription = "상대 전장 전체에 물리 데미지 10을 입힌다. 건물에는 3배의 데미지를 입힌다.";
    }
}