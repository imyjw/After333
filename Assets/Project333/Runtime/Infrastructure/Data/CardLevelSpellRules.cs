namespace Project333.Runtime.Infrastructure.Data
{
    public static class CardLevelSpellRules
    {
        public static int ApplyDamageBonus(int baseDamage, int upgradeLevel)
        {
            var normalizedLevel = upgradeLevel < 0 || upgradeLevel > CardUpgradeRules.MaxLevel
                ? 0
                : upgradeLevel;

            return baseDamage + normalizedLevel;
        }
    }
}
