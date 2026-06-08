using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Infrastructure.Data
{
    public sealed class DamageSpellCardDefinition : SpellCardDefinition
    {
        public DamageSpellCardDefinition(
            string cardId,
            string displayName,
            ResourceSet cost,
            int damage)
            : base(cardId, displayName, cost)
        {
            Damage = damage;
        }

        public int Damage { get; }
    }
}
