using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;
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
            string targetRuntimeId = null,
            int targetRow = -1,
            int remainingTriggers = 0,
            int effectDamage = 0,
            DamageType effectDamageType = DamageType.None,
            bool targetsOwnerBoard = false,
            int capturedSpellPower = 0)
        {
            if (capturedSpellPower < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capturedSpellPower));
            }

            SourceCardId = sourceCardId;
            OwnerId = ownerId;
            EffectId = effectId;
            AppliedTurn = appliedTurn;
            EndConditionText = endConditionText;
            TurnStartResourceGain = turnStartResourceGain ?? new ResourceSet();
            OwnerTurnStartsRemaining = ownerTurnStartsRemaining;
            TargetRuntimeId = targetRuntimeId;
            TargetRow = targetRow;
            RemainingTriggers = remainingTriggers;
            EffectDamage = effectDamage;
            EffectDamageType = effectDamageType;
            TargetsOwnerBoard = targetsOwnerBoard;
            CapturedSpellPower = capturedSpellPower;
        }

        public string SourceCardId { get; }

        public PlayerId OwnerId { get; }

        public string EffectId { get; }

        public int AppliedTurn { get; }

        public string EndConditionText { get; }

        public ResourceSet TurnStartResourceGain { get; }

        public int OwnerTurnStartsRemaining { get; private set; }

        public string TargetRuntimeId { get; }

        public int TargetRow { get; }

        public int RemainingTriggers { get; private set; }

        public int EffectDamage { get; }

        public DamageType EffectDamageType { get; }

        public bool TargetsOwnerBoard { get; }

        public int CapturedSpellPower { get; }

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

        public void ResolveTrigger()
        {
            if (IsExpired || RemainingTriggers <= 0)
            {
                return;
            }

            RemainingTriggers -= 1;
            if (RemainingTriggers == 0)
            {
                Expire();
            }
        }

        public void RestoreRuntimeState(
            int ownerTurnStartsRemaining,
            bool isExpired,
            int remainingTriggers = -1)
        {
            OwnerTurnStartsRemaining = ownerTurnStartsRemaining < 0 ? 0 : ownerTurnStartsRemaining;
            if (remainingTriggers >= 0)
            {
                RemainingTriggers = remainingTriggers;
            }

            IsExpired = isExpired;
        }
    }
}
