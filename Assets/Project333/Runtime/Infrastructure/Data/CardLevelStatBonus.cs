namespace Project333.Runtime.Infrastructure.Data
{
    public readonly struct CardLevelStatBonus
    {
        public CardLevelStatBonus(int attackBonus, int hpBonus)
        {
            AttackBonus = attackBonus;
            HpBonus = hpBonus;
        }

        public int AttackBonus { get; }

        public int HpBonus { get; }
    }
}
