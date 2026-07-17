#nullable enable

using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    internal static class BattleValuePopupRecorder
    {
        public static void RecordDamage(
            BattleState battleState,
            OccupantState occupant,
            int actualDamage,
            BattleValueChangeCause cause = BattleValueChangeCause.Unknown,
            DamageType damageType = DamageType.None,
            PlayerId? sourceOwnerId = null,
            string? sourceRuntimeId = null,
            string? sourceCardId = null)
        {
            if (battleState == null || occupant == null || actualDamage < 0)
            {
                return;
            }

            var invinciblePrevented = actualDamage == 0 && occupant.IsInvincible;
            if (actualDamage == 0 && !invinciblePrevented)
            {
                return;
            }

            var hpAfter = Math.Max(0, occupant.CurrentHp);
            battleState.ValuePopupEvents.Add(new BattleValuePopupEvent(
                occupant.RuntimeId,
                occupant.OwnerId,
                occupant.Position,
                isHealing: false,
                amount: actualDamage,
                sourceOwnerId: sourceOwnerId ?? occupant.OwnerId,
                sourceRuntimeId: sourceRuntimeId,
                sourceCardId: sourceCardId,
                cause: cause,
                damageType: damageType,
                hpBefore: hpAfter + actualDamage,
                hpAfter: hpAfter,
                isInvinciblePrevented: invinciblePrevented));
        }

        public static int HealAndRecord(
            BattleState battleState,
            OccupantState occupant,
            int requestedAmount,
            BattleValueChangeCause cause = BattleValueChangeCause.Unknown,
            PlayerId? sourceOwnerId = null,
            string? sourceRuntimeId = null,
            string? sourceCardId = null)
        {
            if (battleState == null || occupant == null || requestedAmount <= 0)
            {
                return 0;
            }

            var previousHp = occupant.CurrentHp;
            occupant.Heal(requestedAmount);
            var actualHealedAmount = occupant.CurrentHp - previousHp;
            if (actualHealedAmount > 0)
            {
                battleState.ValuePopupEvents.Add(new BattleValuePopupEvent(
                    occupant.RuntimeId,
                    occupant.OwnerId,
                    occupant.Position,
                    isHealing: true,
                    amount: actualHealedAmount,
                    sourceOwnerId: sourceOwnerId ?? occupant.OwnerId,
                    sourceRuntimeId: sourceRuntimeId,
                    sourceCardId: sourceCardId,
                    cause: cause,
                    damageType: DamageType.None,
                    hpBefore: previousHp,
                    hpAfter: occupant.CurrentHp));
            }

            return actualHealedAmount;
        }
    }
}
