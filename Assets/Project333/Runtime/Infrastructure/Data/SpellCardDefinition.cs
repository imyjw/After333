using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Infrastructure.Data
{
    public abstract class SpellCardDefinition : CardDefinition
    {
        protected SpellCardDefinition(
            string cardId,
            string displayName,
            ResourceSet cost,
            bool hasReplicate = false)
            : base(cardId, displayName, CardType.Spell, cost, hasReplicate: hasReplicate)
        {
        }
    }
}
