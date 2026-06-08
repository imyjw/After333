using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Infrastructure.Data
{
    public sealed class BuildingCardDefinition : CardDefinition
    {
        public BuildingCardDefinition(
            string cardId,
            string displayName,
            ResourceSet cost,
            bool canAttack,
            int attack,
            int health,
            ResourceSet turnStartResourceGain = null,
            bool canAttackOnSummon = false)
            : base(cardId, displayName, CardType.Building, cost)
        {
            CanAttack = canAttack;
            Attack = attack;
            Health = health;
            TurnStartResourceGain = turnStartResourceGain?.Clone() ?? new ResourceSet();
            CanAttackOnSummon = canAttackOnSummon;
        }

        public bool CanAttack { get; }

        public int Attack { get; }

        public int Health { get; }

        public ResourceSet TurnStartResourceGain { get; }

        public bool CanAttackOnSummon { get; }
    }
}
