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

            // Player-owned turn buffs are independent of occupant suppression and survival.
            foreach (var effect in battleState.PersistentEffects)
            {
                if (!effect.IsExpired && effect.OwnerId == ownerId &&
                    string.Equals(effect.EffectId, MagicAmplificationCircleRules.EffectId, StringComparison.Ordinal))
                    total = checked(total + MagicAmplificationCircleRules.SpellPowerBonus);
            }

            return total;
        }

        public static int GetCurrentBonus(BattleState battleState, PlayerId ownerId, DamageType damageType)
        {
            return damageType == DamageType.Magic ? GetTotal(battleState, ownerId) : 0;
        }

        public static int ApplyCurrent(
            BattleState battleState,
            PlayerId ownerId,
            int baseDamage,
            DamageType damageType)
        {
            return AddBonus(baseDamage, damageType, GetCurrentBonus(battleState, ownerId, damageType));
        }

        public static int AddBonus(int baseDamage, DamageType damageType, int spellPower)
        {
            if (baseDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseDamage));
            }

            if (spellPower < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(spellPower));
            }

            return damageType == DamageType.Magic
                ? checked(baseDamage + spellPower)
                : baseDamage;
        }
    }
}
