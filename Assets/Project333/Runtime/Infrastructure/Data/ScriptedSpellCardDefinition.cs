using System;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Infrastructure.Data
{
    public sealed class ScriptedSpellCardDefinition : SpellCardDefinition
    {
        public ScriptedSpellCardDefinition(
            string cardId,
            string displayName,
            ResourceSet cost,
            string effectId)
            : base(cardId, displayName, cost)
        {
            if (string.IsNullOrWhiteSpace(effectId))
            {
                throw new ArgumentException("Effect id is required.", nameof(effectId));
            }

            EffectId = effectId;
        }

        public string EffectId { get; }
    }
}
