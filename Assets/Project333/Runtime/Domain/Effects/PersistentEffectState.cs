using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Domain.Effects
{
    public sealed class PersistentEffectState
    {
        public PersistentEffectState(
            string sourceCardId,
            PlayerId ownerId,
            string effectId,
            int appliedTurn,
            string endConditionText,
            ResourceSet turnStartResourceGain,
            int ownerTurnStartsRemaining = 0,
            string targetRuntimeId = null)
        {
            SourceCardId = sourceCardId;
            OwnerId = ownerId;
            EffectId = effectId;
            AppliedTurn = appliedTurn;
            EndConditionText = endConditionText;
            TurnStartResourceGain = turnStartResourceGain ?? new ResourceSet();
            OwnerTurnStartsRemaining = ownerTurnStartsRemaining;
            TargetRuntimeId = targetRuntimeId;
        }

        public string SourceCardId { get; }

        public PlayerId OwnerId { get; }

        public string EffectId { get; }

        public int AppliedTurn { get; }

        public string EndConditionText { get; }

        public ResourceSet TurnStartResourceGain { get; }

        public int OwnerTurnStartsRemaining { get; private set; }

        public string TargetRuntimeId { get; }

        public bool IsExpired { get; private set; }

        public void ResolveOwnerTurnStart()
        {
            if (IsExpired || OwnerTurnStartsRemaining <= 0)
            {
                return;
            }

            OwnerTurnStartsRemaining -= 1;
            if (OwnerTurnStartsRemaining == 0)
            {
                Expire();
            }
        }

        public void Expire()
        {
            IsExpired = true;
        }
    }
}
