using System;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    public static class ShielderService
    {
        public static ShielderInfo Resolve(BoardState board, TileCoord targetCoord)
        {
            return ResolveInternal(
                board,
                targetCoord,
                isNormalAttack: false,
                attackerAttackType: AttackType.Ranged,
                attackerHasActiveFlying: false);
        }

        public static ShielderInfo ResolveForNormalAttack(
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

        public static ShielderInfo ResolveForNormalAttack(
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

        private static ShielderInfo ResolveInternal(
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
                return new ShielderInfo(originalTarget, targetCoord, null, null);
            }

            var shielderCoord = new TileCoord(targetCoord.Column, 0);
            var shielder = board.GetOccupant(shielderCoord);
            if (shielder == null || !shielder.HasActiveShielder)
            {
                return new ShielderInfo(originalTarget, targetCoord, null, null);
            }

            if (isNormalAttack &&
                shielder.HasActiveFlying &&
                attackerAttackType == AttackType.Melee &&
                !attackerHasActiveFlying)
            {
                return new ShielderInfo(originalTarget, targetCoord, null, null);
            }

            return new ShielderInfo(originalTarget, targetCoord, shielder, shielderCoord);
        }

        public readonly struct ShielderInfo
        {
            public ShielderInfo(
                OccupantState originalTarget,
                TileCoord originalTargetCoord,
                OccupantState shielder,
                TileCoord? shielderCoord)
            {
                OriginalTarget = originalTarget;
                OriginalTargetCoord = originalTargetCoord;
                Shielder = shielder;
                ShielderCoord = shielderCoord;
            }

            public OccupantState OriginalTarget { get; }
            public TileCoord OriginalTargetCoord { get; }
            public OccupantState Shielder { get; }
            public TileCoord? ShielderCoord { get; }
            public bool IsProtected => Shielder != null;
        }
    }
}
