using System;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Infrastructure.Data
{
    public sealed class BuildingCardDefinition : CardDefinition
    {
        public BuildingCardDefinition(
            string cardId,
            string displayName,
            ResourceSet cost,
            bool canAttack,
            int attack,
            int health,
            ResourceSet turnStartResourceGain = null,
            bool canAttackOnSummon = false,
            DamageType damageType = DamageType.Physical,
            int physicalDefense = 0,
            int magicDefense = 0,
            int sciencePowerUpkeep = 0,
            bool includeInDraft = true,
            bool hasReplicate = false,
            int sealboundOwnerTurnStarts = 0,
            bool hasFlying = false,
            int spellPower = 0,
            InvincibleDurationType invincibleDuration = InvincibleDurationType.None,
            int invincibleOwnerTurns = 0,
            bool hasPiercing = false)
            : base(cardId, displayName, CardType.Building, cost, includeInDraft, hasReplicate)
        {
            if (sealboundOwnerTurnStarts < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sealboundOwnerTurnStarts));
            }

            if (spellPower < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(spellPower));
            }

            ValidateInvincible(invincibleDuration, invincibleOwnerTurns);

            CanAttack = canAttack;
            Attack = attack;
            Health = health;
            TurnStartResourceGain = turnStartResourceGain?.Clone() ?? new ResourceSet();
            CanAttackOnSummon = canAttackOnSummon;
            DamageType = damageType;
            PhysicalDefense = physicalDefense;
            MagicDefense = magicDefense;
            SciencePowerUpkeep = sciencePowerUpkeep;
            SealboundOwnerTurnStarts = sealboundOwnerTurnStarts;
            HasFlying = hasFlying;
            SpellPower = spellPower;
            InvincibleDuration = invincibleDuration;
            InvincibleOwnerTurns = invincibleOwnerTurns;
            HasPiercing = hasPiercing;
        }

        public bool CanAttack { get; }

        public int Attack { get; }

        public int Health { get; }

        public ResourceSet TurnStartResourceGain { get; }

        public bool CanAttackOnSummon { get; }
        public DamageType DamageType { get; }
        public int PhysicalDefense { get; }
        public int MagicDefense { get; }
        public int SciencePowerUpkeep { get; }
        public int SealboundOwnerTurnStarts { get; }
        public bool HasFlying { get; }
        public int SpellPower { get; }
        public InvincibleDurationType InvincibleDuration { get; }
        public int InvincibleOwnerTurns { get; }
        public bool HasPiercing { get; }

        private static void ValidateInvincible(
            InvincibleDurationType duration,
            int ownerTurns)
        {
            if (!Enum.IsDefined(typeof(InvincibleDurationType), duration))
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            var hasTurnCount = duration == InvincibleDurationType.OwnerTurns ||
                               duration == InvincibleDurationType.GlobalTurnEnds;
            if (hasTurnCount && ownerTurns <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerTurns));
            }

            if (!hasTurnCount && ownerTurns != 0)
            {
                throw new ArgumentException(
                    "invincibleOwnerTurns must be 0 unless InvincibleDuration uses a turn count.",
                    nameof(ownerTurns));
            }
        }
    }
}
