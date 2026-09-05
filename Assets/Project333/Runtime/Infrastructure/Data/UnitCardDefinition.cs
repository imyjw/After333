using System;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Infrastructure.Data
{
    public sealed class UnitCardDefinition : CardDefinition
    {
        public UnitCardDefinition(
            string cardId,
            string displayName,
            ResourceSet cost,
            AttackType attackType,
            int attack,
            int health,
            bool canMove,
            bool isScience,
            int sciencePowerUpkeep,
            ResourceSet turnStartResourceGain = null,
            int maxAttacksPerTurn = 1,
            bool canAttackOnSummon = false,
            int hitsPerAttack = 1,
            bool hasBerserker = false,
            bool hasEndure = false,
            bool hasShielder = false,
            bool hasLifeSteal = false,
            DamageType damageType = DamageType.Physical,
            int physicalDefense = 0,
            int magicDefense = 0,
            bool hasRobot = false,
            bool includeInDraft = true,
            bool hasRush = false,
            bool hasReplicate = false,
            int sealboundOwnerTurnStarts = 0,
            bool hasHiding = false,
            bool hasFlying = false,
            int spellPower = 0,
            InvincibleDurationType invincibleDuration = InvincibleDurationType.None,
            int invincibleOwnerTurns = 0,
            bool hasPiercing = false)
            : base(cardId, displayName, CardType.Unit, cost, includeInDraft, hasReplicate)
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

            AttackType = attackType;
            Attack = attack;
            Health = health;
            CanMove = canMove;
            IsScience = isScience;
            SciencePowerUpkeep = sciencePowerUpkeep;
            TurnStartResourceGain = turnStartResourceGain?.Clone() ?? new ResourceSet();
            MaxAttacksPerTurn = maxAttacksPerTurn;
            HasRush = hasRush || canAttackOnSummon;
            HitsPerAttack = hitsPerAttack < 1 ? 1 : hitsPerAttack;
            HasBerserker = hasBerserker;
            HasEndure = hasEndure;
            HasShielder = hasShielder;
            HasLifeSteal = hasLifeSteal;
            DamageType = damageType;
            PhysicalDefense = physicalDefense;
            MagicDefense = magicDefense;
            HasRobot = hasRobot;
            SealboundOwnerTurnStarts = sealboundOwnerTurnStarts;
            HasHiding = hasHiding;
            HasFlying = hasFlying;
            SpellPower = spellPower;
            InvincibleDuration = invincibleDuration;
            InvincibleOwnerTurns = invincibleOwnerTurns;
            HasPiercing = hasPiercing;
        }

        public AttackType AttackType { get; }
        public int Attack { get; }
        public int Health { get; }
        public bool CanMove { get; }
        public bool IsScience { get; }
        public int SciencePowerUpkeep { get; }
        public ResourceSet TurnStartResourceGain { get; }
        public int MaxAttacksPerTurn { get; }
        public bool HasRush { get; }
        public bool CanAttackOnSummon => HasRush;
        public int HitsPerAttack { get; }
        public bool HasBerserker { get; }
        public bool HasEndure { get; }
        public bool HasShielder { get; }
        public bool HasLifeSteal { get; }
        public DamageType DamageType { get; }
        public int PhysicalDefense { get; }
        public int MagicDefense { get; }
        public bool HasRobot { get; }
        public int SealboundOwnerTurnStarts { get; }
        public bool HasHiding { get; }
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
