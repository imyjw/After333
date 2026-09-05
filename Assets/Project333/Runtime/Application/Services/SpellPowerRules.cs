using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    public static class SpellPowerRules
    {
        public static int GetTotal(BattleState battleState, PlayerId ownerId)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            var total = 0;
            foreach (var occupant in battleState.GetBoard(ownerId).EnumerateOccupants())
            {
                total = checked(total + occupant.EffectiveSpellPower);
            }

            return total;
        }

        public static int Capture(BattleState battleState, PlayerId ownerId, DamageType damageType)
        {
            return damageType == DamageType.Magic ? GetTotal(battleState, ownerId) : 0;
        }

        public static int ApplyCurrent(
            BattleState battleState,
            PlayerId ownerId,
            int baseDamage,
            DamageType damageType)
        {
            return ApplyCaptured(baseDamage, damageType, Capture(battleState, ownerId, damageType));
        }

        public static int ApplyCaptured(int baseDamage, DamageType damageType, int capturedSpellPower)
        {
            if (baseDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseDamage));
            }

            if (capturedSpellPower < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capturedSpellPower));
            }

            return damageType == DamageType.Magic
                ? checked(baseDamage + capturedSpellPower)
                : baseDamage;
        }
    }
}
