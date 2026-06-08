using System;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    public static class DamageResolutionRules
    {
        public static int GetDamageAgainst(OccupantState attacker, OccupantState defender)
        {
            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (defender == null)
            {
                throw new ArgumentNullException(nameof(defender));
            }

            return attacker.Attack;
        }

        public static int ApplyAttackDamage(OccupantState recipient, int damage)
        {
            return ApplyDamage(recipient, damage, allowEndure: true);
        }

        public static int ApplyEffectDamage(OccupantState recipient, int damage)
        {
            return ApplyDamage(recipient, damage, allowEndure: false);
        }

        public static int ProjectAttackDamageTaken(
            int currentHp,
            bool isDisabled,
            bool canTriggerEndure,
            int damage,
            out int remainingHp,
            out bool triggeredEndure)
        {
            return ProjectDamageTaken(currentHp, isDisabled, canTriggerEndure, damage, allowEndure: true, out remainingHp, out triggeredEndure);
        }

        public static int ProjectEffectDamageTaken(
            int currentHp,
            bool isDisabled,
            int damage,
            out int remainingHp)
        {
            return ProjectDamageTaken(currentHp, isDisabled, false, damage, allowEndure: false, out remainingHp, out _);
        }

        private static int ApplyDamage(OccupantState recipient, int damage, bool allowEndure)
        {
            if (recipient == null)
            {
                throw new ArgumentNullException(nameof(recipient));
            }

            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage));
            }

            var hpBefore = recipient.CurrentHp;
            var resolvedDamage = recipient.IsDisabled ? damage * 3 : damage;
            recipient.CurrentHp -= resolvedDamage;

            if (allowEndure && recipient.CurrentHp <= 0 && recipient.CanTriggerEndure)
            {
                recipient.CurrentHp = 1;
                recipient.EndureUsed = true;
            }

            var hpAfterForAbsorption = Math.Max(recipient.CurrentHp, 0);
            return Math.Max(0, hpBefore - hpAfterForAbsorption);
        }

        private static int ProjectDamageTaken(
            int currentHp,
            bool isDisabled,
            bool canTriggerEndure,
            int damage,
            bool allowEndure,
            out int remainingHp,
            out bool triggeredEndure)
        {
            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage));
            }

            var resolvedDamage = isDisabled ? damage * 3 : damage;
            remainingHp = currentHp - resolvedDamage;
            triggeredEndure = allowEndure && remainingHp <= 0 && canTriggerEndure;
            if (triggeredEndure)
            {
                remainingHp = 1;
            }

            var hpAfterForAbsorption = Math.Max(remainingHp, 0);
            return Math.Max(0, currentHp - hpAfterForAbsorption);
        }
    }
}


