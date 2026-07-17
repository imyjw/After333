using System;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    public static class GuardService
    {
        public static GuardInfo Resolve(BoardState board, TileCoord targetCoord)
        {
            return ResolveInternal(
                board,
                targetCoord,
                isNormalAttack: false,
                attackerAttackType: AttackType.Ranged,
                attackerHasActiveFlying: false);
        }

        public static GuardInfo ResolveForNormalAttack(
            BoardState board,
            TileCoord targetCoord,
            OccupantState attacker)
        {
            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            return ResolveForNormalAttack(
                board,
                targetCoord,
                attacker.AttackType,
                attacker.HasActiveFlying);
        }

        public static GuardInfo ResolveForNormalAttack(
            BoardState board,
            TileCoord targetCoord,
            AttackType attackerAttackType,
            bool attackerHasActiveFlying)
        {
            return ResolveInternal(
                board,
                targetCoord,
                isNormalAttack: true,
                attackerAttackType: attackerAttackType,
                attackerHasActiveFlying: attackerHasActiveFlying);
        }

        private static GuardInfo ResolveInternal(
            BoardState board,
            TileCoord targetCoord,
            bool isNormalAttack,
            AttackType attackerAttackType,
            bool attackerHasActiveFlying)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            var originalTarget = board.GetOccupant(targetCoord)
                ?? throw new InvalidOperationException("Target tile is empty.");

            if (originalTarget.IsSealbound)
            {
                throw new InvalidOperationException("Sealbound occupants cannot be targeted.");
            }

            if (targetCoord.Row != 1)
            {
                return new GuardInfo(originalTarget, targetCoord, null, null);
            }

            var guardCoord = new TileCoord(targetCoord.Column, 0);
            var guard = board.GetOccupant(guardCoord);
            if (guard == null || !guard.HasActiveGuard)
            {
                return new GuardInfo(originalTarget, targetCoord, null, null);
            }

            if (isNormalAttack &&
                guard.HasActiveFlying &&
                attackerAttackType == AttackType.Melee &&
                !attackerHasActiveFlying)
            {
                return new GuardInfo(originalTarget, targetCoord, null, null);
            }

            return new GuardInfo(originalTarget, targetCoord, guard, guardCoord);
        }

        public readonly struct GuardInfo
        {
            public GuardInfo(OccupantState originalTarget, TileCoord originalTargetCoord, OccupantState guard, TileCoord? guardCoord)
            {
                OriginalTarget = originalTarget;
                OriginalTargetCoord = originalTargetCoord;
                Guard = guard;
                GuardCoord = guardCoord;
            }

            public OccupantState OriginalTarget { get; }
            public TileCoord OriginalTargetCoord { get; }
            public OccupantState Guard { get; }
            public TileCoord? GuardCoord { get; }
            public bool IsProtected => Guard != null;
        }
    }
}
