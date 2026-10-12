using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Domain.Effects
{
    /// <summary>A private installed card, with the rule and activation cost captured at installation.</summary>
    public sealed class ArmedTrapState
    {
        private readonly ResourceSet _activationCost;

        public ArmedTrapState(string runtimeId, PlayerId ownerId, string cardId,
            TrapTriggerType triggerType, TrapEffectType effectType, ResourceSet activationCost,
            int installedTurn, int upgradeLevel = 0, bool isTemporaryReplicate = false, int effectAmount = 0)
        {
            if (string.IsNullOrWhiteSpace(runtimeId)) throw new ArgumentException("Trap runtime id is required.", nameof(runtimeId));
            if (string.IsNullOrWhiteSpace(cardId)) throw new ArgumentException("Trap card id is required.", nameof(cardId));
            if (ownerId != PlayerId.Player && ownerId != PlayerId.AI) throw new ArgumentOutOfRangeException(nameof(ownerId));
            if (!Enum.IsDefined(typeof(TrapTriggerType), triggerType) || triggerType == TrapTriggerType.None)
                throw new ArgumentOutOfRangeException(nameof(triggerType));
            if (!Enum.IsDefined(typeof(TrapEffectType), effectType) || effectType == TrapEffectType.None)
                throw new ArgumentOutOfRangeException(nameof(effectType));
            if (activationCost == null) throw new ArgumentNullException(nameof(activationCost));
            if (activationCost.Mana < 0 || activationCost.Qi < 0 || activationCost.Power < 0 || activationCost.Gold < 0)
                throw new ArgumentOutOfRangeException(nameof(activationCost));
            if (installedTurn < 0) throw new ArgumentOutOfRangeException(nameof(installedTurn));
            if (upgradeLevel < 0 || upgradeLevel > CardUpgradeRules.MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(upgradeLevel));

            if (effectAmount < 0) throw new ArgumentOutOfRangeException(nameof(effectAmount));

            RuntimeId = runtimeId;
            OwnerId = ownerId;
            CardId = cardId;
            TriggerType = triggerType;
            EffectType = effectType;
            _activationCost = activationCost.Clone();
            InstalledTurn = installedTurn;
            UpgradeLevel = upgradeLevel;
            IsTemporaryReplicate = isTemporaryReplicate;
            EffectAmount = effectAmount;
        }

        public string RuntimeId { get; }
        public PlayerId OwnerId { get; }
        public string CardId { get; }
        public TrapTriggerType TriggerType { get; }
        public TrapEffectType EffectType { get; }
        public ResourceSet ActivationCost => _activationCost.Clone();
        public int InstalledTurn { get; }
        public int UpgradeLevel { get; }
        public bool IsTemporaryReplicate { get; }
        // Immutable installed damage, including the owner's installation-time upgrade level.
        public int EffectAmount { get; }
    }
}
