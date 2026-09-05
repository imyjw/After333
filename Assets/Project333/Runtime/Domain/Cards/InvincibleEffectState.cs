using System;
using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Domain.Cards
{
    public sealed class InvincibleEffectState
    {
        public InvincibleEffectState(
            InvincibleDurationType duration,
            int ownerTurnsRemaining,
            int appliedTurnNumber,
            PlayerId appliedActivePlayerId)
        {
            if (!Enum.IsDefined(typeof(InvincibleDurationType), duration) ||
                duration == InvincibleDurationType.None)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            if (HasTurnCount(duration))
            {
                if (ownerTurnsRemaining <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(ownerTurnsRemaining));
                }
            }
            else if (ownerTurnsRemaining != 0)
            {
                throw new ArgumentException(
                    "Only counted Invincible effects may have a remaining-turn count.",
                    nameof(ownerTurnsRemaining));
            }

            Duration = duration;
            OwnerTurnsRemaining = ownerTurnsRemaining;
            AppliedTurnNumber = Math.Max(0, appliedTurnNumber);
            AppliedActivePlayerId = appliedActivePlayerId;
        }

        public InvincibleDurationType Duration { get; }

        public int OwnerTurnsRemaining { get; private set; }

        public int AppliedTurnNumber { get; }

        public PlayerId AppliedActivePlayerId { get; }

        public bool IsActive(PlayerId ownerId, PlayerId activePlayerId)
        {
            return Duration switch
            {
                InvincibleDurationType.Always => true,
                InvincibleDurationType.SummonTurn => true,
                InvincibleDurationType.OwnerTurnOnly => activePlayerId == ownerId,
                InvincibleDurationType.OpponentTurnOnly => activePlayerId != ownerId,
                InvincibleDurationType.UntilTurnEnd => true,
                InvincibleDurationType.OwnerTurns => OwnerTurnsRemaining > 0,
                InvincibleDurationType.GlobalTurnEnds => OwnerTurnsRemaining > 0,
                _ => false,
            };
        }

        public bool ResolveTurnEnd(PlayerId ownerId, PlayerId endingPlayerId)
        {
            if (Duration == InvincibleDurationType.SummonTurn ||
                Duration == InvincibleDurationType.UntilTurnEnd)
            {
                return true;
            }

            if (Duration == InvincibleDurationType.OwnerTurns && endingPlayerId == ownerId)
            {
                OwnerTurnsRemaining = Math.Max(0, OwnerTurnsRemaining - 1);
                return OwnerTurnsRemaining == 0;
            }

            if (Duration == InvincibleDurationType.GlobalTurnEnds)
            {
                OwnerTurnsRemaining = Math.Max(0, OwnerTurnsRemaining - 1);
                return OwnerTurnsRemaining == 0;
            }

            return false;
        }

        private static bool HasTurnCount(InvincibleDurationType duration)
        {
            return duration == InvincibleDurationType.OwnerTurns ||
                   duration == InvincibleDurationType.GlobalTurnEnds;
        }

        public InvincibleEffectState Clone()
        {
            return new InvincibleEffectState(
                Duration,
                OwnerTurnsRemaining,
                AppliedTurnNumber,
                AppliedActivePlayerId);
        }
    }
}
