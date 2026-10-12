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

        public static int ResolveIncomingDamage(
            OccupantState recipient,
            int damage,
            DamageType damageType,
            bool halveForFlying = false,
            bool isCritical = false)
        {
            if (recipient == null)
            {
                throw new ArgumentNullException(nameof(recipient));
            }

            if (recipient.IsSealbound)
            {
                return 0;
            }

            if (recipient.IsInvincible)
            {
                return 0;
            }

            var resolvedDamage = ResolveIncomingDamage(
                damage,
                damageType,
                recipient.IsDrained,
                recipient.PhysicalDefense,
                recipient.MagicDefense);
            if (halveForFlying) resolvedDamage /= 2;
            return ApplyCriticalMultiplier(resolvedDamage, isCritical);
        }

        // Critical damage doubles the final mitigated packet, before HP/Endure limits.
        public static int ApplyCriticalMultiplier(int resolvedDamage, bool isCritical)
        {
            if (resolvedDamage < 0) throw new ArgumentOutOfRangeException(nameof(resolvedDamage));
            return isCritical ? checked(resolvedDamage * 2) : resolvedDamage;
        }

        public static bool ShouldHalveForFlying(OccupantState attacker, OccupantState target)
        {
            return attacker != null && target != null &&
                   attacker.AttackType == AttackType.Melee &&
                   !attacker.HasActiveFlying && target.HasActiveFlying;
        }

        public static int ResolveIncomingDamage(
            int damage,
            DamageType damageType,
            bool isDrained,
            int physicalDefense,
            int magicDefense)
        {
            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage));
            }

            if (physicalDefense < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(physicalDefense));
            }

            if (magicDefense < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magicDefense));
            }

            var effectivePhysicalDefense = isDrained ? 0 : physicalDefense;
            var effectiveMagicDefense = isDrained ? 0 : magicDefense;
            var mitigatedDamage = damageType switch
            {
                DamageType.None => 0,
                DamageType.Physical => Math.Max(0, damage - effectivePhysicalDefense),
                DamageType.Magic => Math.Max(0, damage - effectiveMagicDefense),
                DamageType.Fixed => damage,
                _ => throw new ArgumentOutOfRangeException(nameof(damageType)),
            };

            return isDrained ? mitigatedDamage * 3 : mitigatedDamage;
        }

        public static int ApplyAttackDamage(
            OccupantState recipient,
            int damage,
            DamageType damageType,
            bool halveForFlying = false,
            bool isCritical = false)
        {
            return ApplyDamage(recipient, damage, damageType, allowEndure: true, halveForFlying: halveForFlying, isCritical: isCritical);
        }

        public static int ApplyEffectDamage(
            OccupantState recipient,
            int damage,
            DamageType damageType,
            bool isCritical = false)
        {
            return ApplyDamage(recipient, damage, damageType, allowEndure: true, isCritical: isCritical);
        }

        // Shielder overflow has already passed through the original target's mitigation.
        public static int ApplyResolvedAttackDamage(OccupantState recipient, int resolvedDamage)
        {
            return ApplyResolvedDamage(recipient, resolvedDamage, allowEndure: true);
        }

        public static int ProjectEffectDamageTaken(
            OccupantState recipient,
            int damage,
            DamageType damageType,
            out int remainingHp,
            out bool triggeredEndure)
        {
            if (recipient == null)
            {
                throw new ArgumentNullException(nameof(recipient));
            }

            if (recipient.IsSealbound || recipient.IsInvincible)
            {
                remainingHp = recipient.CurrentHp;
                triggeredEndure = false;
                return 0;
            }

            return ProjectDamageTaken(
                recipient.CurrentHp,
                recipient.IsDrained,
                recipient.PhysicalDefense,
                recipient.MagicDefense,
                recipient.CanTriggerEndure,
                damage,
                damageType,
                allowEndure: true,
                out remainingHp,
                out triggeredEndure);
        }

        public static int ProjectAttackDamageTaken(
            int currentHp,
            bool isDrained,
            int physicalDefense,
            int magicDefense,
            bool canTriggerEndure,
            int damage,
            DamageType damageType,
            out int remainingHp,
            out bool triggeredEndure)
        {
            return ProjectDamageTaken(
                currentHp,
                isDrained,
                physicalDefense,
                magicDefense,
                canTriggerEndure,
                damage,
                damageType,
                allowEndure: true,
                out remainingHp,
                out triggeredEndure);
        }

        public static int ProjectEffectDamageTaken(
            int currentHp,
            bool isDrained,
            int physicalDefense,
            int magicDefense,
            int damage,
            DamageType damageType,
            out int remainingHp)
        {
            return ProjectDamageTaken(
                currentHp,
                isDrained,
                physicalDefense,
                magicDefense,
                false,
                damage,
                damageType,
                allowEndure: false,
                out remainingHp,
                out _);
        }

        private static int ApplyDamage(
            OccupantState recipient,
            int damage,
            DamageType damageType,
            bool allowEndure,
            bool halveForFlying = false,
            bool isCritical = false)
        {
            var resolvedDamage = ResolveIncomingDamage(recipient, damage, damageType, halveForFlying, isCritical);
            return ApplyResolvedDamage(recipient, resolvedDamage, allowEndure);
        }

        private static int ApplyResolvedDamage(
            OccupantState recipient,
            int resolvedDamage,
            bool allowEndure)
        {
            if (recipient == null)
            {
                throw new ArgumentNullException(nameof(recipient));
            }

            if (resolvedDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(resolvedDamage));
            }

            if (recipient.IsSealbound || recipient.IsInvincible)
            {
                return 0;
            }

            var hpBefore = recipient.CurrentHp;
            recipient.CurrentHp -= resolvedDamage;

            if (allowEndure && recipient.CurrentHp <= 0 && recipient.CanTriggerEndure)
            {
                recipient.CurrentHp = 1;
                recipient.EndureUsed = true;
            }

            var hpAfterForAbsorption = Math.Max(recipient.CurrentHp, 0);
            var actualDamage = Math.Max(0, hpBefore - hpAfterForAbsorption);
            // Any damage source breaks Hiding only when it removes HP.
            if (actualDamage > 0) recipient.RevealHiding();
            return actualDamage;
        }

        private static int ProjectDamageTaken(
            int currentHp,
            bool isDrained,
            int physicalDefense,
            int magicDefense,
            bool canTriggerEndure,
            int damage,
            DamageType damageType,
            bool allowEndure,
            out int remainingHp,
            out bool triggeredEndure)
        {
            var resolvedDamage = ResolveIncomingDamage(
                damage,
                damageType,
                isDrained,
                physicalDefense,
                magicDefense);
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
