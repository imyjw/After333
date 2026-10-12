using System;

namespace Project333.Runtime.Infrastructure.Data
{
    public static class DaehwandanCardRules
    {
        public const string CardId = "Daehwandan";
        public const string EffectId = "daehwandan";
        public const int BaseEffectAmount = 30;
        public const string EffectDescription = "사용 시 내 마스터의 공격력을 증가시킨다.";

        public static int GetAttackBonus(int baseAmount, int upgradeLevel)
        {
            if (baseAmount < 0) throw new ArgumentOutOfRangeException(nameof(baseAmount));
            var level = upgradeLevel < 0 || upgradeLevel > CardUpgradeRules.MaxLevel ? 0 : upgradeLevel;
            return baseAmount + level;
        }
    }
}
