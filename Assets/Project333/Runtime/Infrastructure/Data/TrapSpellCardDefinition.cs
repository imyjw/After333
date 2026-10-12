using System;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Infrastructure.Data
{
    public sealed class TrapSpellCardDefinition : SpellCardDefinition
    {
        private readonly ResourceSet _activationCost;

        public TrapSpellCardDefinition(string cardId, string displayName, ResourceSet activationCost,
            TrapTriggerType triggerType, TrapEffectType effectType, bool hasReplicate = false, int damage = 0)
            : base(cardId, displayName, new ResourceSet(), hasReplicate)
        {
            if (activationCost == null) throw new ArgumentNullException(nameof(activationCost));
            if (activationCost.Mana < 0 || activationCost.Qi < 0 || activationCost.Power < 0 || activationCost.Gold < 0)
                throw new ArgumentOutOfRangeException(nameof(activationCost));
            if (!Enum.IsDefined(typeof(TrapTriggerType), triggerType) || triggerType == TrapTriggerType.None)
                throw new ArgumentOutOfRangeException(nameof(triggerType));
            if (!Enum.IsDefined(typeof(TrapEffectType), effectType) || effectType == TrapEffectType.None)
                throw new ArgumentOutOfRangeException(nameof(effectType));
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            Damage = damage;
            _activationCost = activationCost.Clone();
            TriggerType = triggerType;
            EffectType = effectType;
        }

        public override bool HasTrap => true;
        public ResourceSet ActivationCost => _activationCost.Clone();
        public TrapTriggerType TriggerType { get; }
        public TrapEffectType EffectType { get; }
        public int Damage { get; }
    }
}
