using System;

namespace Project333.Runtime.Domain.Resources
{
    public sealed class ResourceSet
    {
        public ResourceSet()
        {
        }

        public ResourceSet(int mana, int qi, int power, int gold)
        {
            Mana = mana;
            Qi = qi;
            Power = power;
            Gold = gold;
        }

        public int Mana { get; private set; }

        public int Qi { get; private set; }

        public int Power { get; private set; }

        public int Gold { get; private set; }

        public bool CanAfford(ResourceSet cost)
        {
            if (cost == null)
            {
                throw new ArgumentNullException(nameof(cost));
            }

            var manaDeficit = Math.Max(0, cost.Mana - Mana);
            var qiDeficit = Math.Max(0, cost.Qi - Qi);
            var powerDeficit = Math.Max(0, cost.Power - Power);
            var totalGoldRequired = cost.Gold + manaDeficit + qiDeficit + powerDeficit;

            return Gold >= totalGoldRequired;
        }

        public void Add(ResourceSet amount)
        {
            Mana += amount.Mana;
            Qi += amount.Qi;
            Power += amount.Power;
            Gold += amount.Gold;
        }

        public void Spend(ResourceSet cost)
        {
            if (cost == null)
            {
                throw new ArgumentNullException(nameof(cost));
            }

            if (!CanAfford(cost))
            {
                throw new InvalidOperationException("The resource cost cannot be paid.");
            }

            var spentMana = Math.Min(Mana, cost.Mana);
            var spentQi = Math.Min(Qi, cost.Qi);
            var spentPower = Math.Min(Power, cost.Power);

            Mana -= spentMana;
            Qi -= spentQi;
            Power -= spentPower;

            var manaDeficit = cost.Mana - spentMana;
            var qiDeficit = cost.Qi - spentQi;
            var powerDeficit = cost.Power - spentPower;

            Gold -= cost.Gold + manaDeficit + qiDeficit + powerDeficit;
        }

        public ResourceSet Clone()
        {
            return new ResourceSet(Mana, Qi, Power, Gold);
        }
    }
}
