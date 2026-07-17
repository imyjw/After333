using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Infrastructure.Data
{
    public sealed class DamageSpellCardDefinition : SpellCardDefinition
    {
        public DamageSpellCardDefinition(
            string cardId,
            string displayName,
            ResourceSet cost,
            int damage,
            DamageType damageType = DamageType.Magic,
            bool hasReplicate = false)
            : base(cardId, displayName, cost, hasReplicate)
        {
            Damage = damage;
            DamageType = damageType;
        }

        public int Damage { get; }
        public DamageType DamageType { get; }
    }
}
