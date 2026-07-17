using System;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Infrastructure.Data
{
    public sealed class PersistentResourceSpellCardDefinition : SpellCardDefinition
    {
        public PersistentResourceSpellCardDefinition(
            string cardId,
            string displayName,
            ResourceSet cost,
            string effectId,
            ResourceSet turnStartResourceGain,
            string endConditionText,
            int ownerTurnStartsRemaining = 0,
            bool hasReplicate = false)
            : base(cardId, displayName, cost, hasReplicate)
        {
            if (string.IsNullOrWhiteSpace(effectId))
            {
                throw new ArgumentException("Effect id is required.", nameof(effectId));
            }

            if (string.IsNullOrWhiteSpace(endConditionText))
            {
                throw new ArgumentException("End condition text is required.", nameof(endConditionText));
            }

            if (ownerTurnStartsRemaining < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerTurnStartsRemaining));
            }

            EffectId = effectId;
            TurnStartResourceGain = turnStartResourceGain?.Clone() ?? throw new ArgumentNullException(nameof(turnStartResourceGain));
            EndConditionText = endConditionText;
            OwnerTurnStartsRemaining = ownerTurnStartsRemaining;
        }

        public string EffectId { get; }

        public ResourceSet TurnStartResourceGain { get; }

        public string EndConditionText { get; }

        public int OwnerTurnStartsRemaining { get; }
    }
}
