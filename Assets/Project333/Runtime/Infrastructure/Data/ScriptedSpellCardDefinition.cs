using System;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Infrastructure.Data
{
    public sealed class ScriptedSpellCardDefinition : SpellCardDefinition
    {
        public ScriptedSpellCardDefinition(
            string cardId,
            string displayName,
            ResourceSet cost,
            string effectId,
            int damage = 0,
            DamageType damageType = DamageType.None,
            int triggerCount = 0,
            bool hasReplicate = false,
            int effectAmount = 0)
            : base(cardId, displayName, cost, hasReplicate)
        {
            if (string.IsNullOrWhiteSpace(effectId))
            {
                throw new ArgumentException("Effect id is required.", nameof(effectId));
            }

            if (effectAmount < 0) throw new ArgumentOutOfRangeException(nameof(effectAmount));
            EffectAmount = effectAmount;
            EffectId = effectId;
            Damage = damage;
            DamageType = damageType;
            TriggerCount = triggerCount;
        }

        public string EffectId { get; }

        public int Damage { get; }

        public DamageType DamageType { get; }

        public int TriggerCount { get; }

        // Non-damage magnitude. Existing spells and assets keep the safe default of zero.
        public int EffectAmount { get; }
    }
}
