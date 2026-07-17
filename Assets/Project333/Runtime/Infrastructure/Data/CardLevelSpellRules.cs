namespace Project333.Runtime.Infrastructure.Data
{
    public static class CardLevelSpellRules
    {
        public static int ApplyDamageBonus(int baseDamage, int upgradeLevel)
        {
            var normalizedLevel = upgradeLevel < 0 ? 0 : upgradeLevel;
            if (normalizedLevel > CardUpgradeRules.MaxLevel)
            {
                normalizedLevel = CardUpgradeRules.MaxLevel;
            }

            return baseDamage + normalizedLevel;
        }
    }
}
