using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    public sealed class TargetingService
    {
        public IReadOnlyList<TileCoord> GetLegalTargets(BattleState battleState, PlayerId attackerOwnerId, TileCoord attackerCoord)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            var attackerBoard = battleState.GetBoard(attackerOwnerId);
            var enemyBoard = battleState.GetOpponentBoard(attackerOwnerId);
            var attacker = attackerBoard.GetOccupant(attackerCoord);
            if (attacker == null)
            {
                throw new InvalidOperationException("Attacker tile is empty.");
            }

            if (attacker.Attack <= 0)
            {
                return Array.Empty<TileCoord>();
            }

            var legalTargets = new List<TileCoord>();

            for (var column = 0; column < BoardState.ColumnCount; column++)
            {
                var frontCoord = new TileCoord(column, 0);
                var backCoord = new TileCoord(column, 1);

                var frontOccupant = enemyBoard.GetOccupant(frontCoord);
                var backOccupant = enemyBoard.GetOccupant(backCoord);

                if (attacker.AttackType == AttackType.Ranged || attacker.HasActiveFlying)
                {
                    if (CanBeTargetedByNormalAttack(
                            attacker.AttackType,
                            attacker.HasActiveFlying,
                            frontOccupant))
                    {
                        legalTargets.Add(frontCoord);
                    }

                    if (CanBeTargetedByNormalAttack(
                            attacker.AttackType,
                            attacker.HasActiveFlying,
                            backOccupant))
                    {
                        legalTargets.Add(backCoord);
                    }

                    continue;
                }

                if (CanBeTargetedByNormalAttack(
                        attacker.AttackType,
                        attacker.HasActiveFlying,
                        frontOccupant))
                {
                    legalTargets.Add(frontCoord);
                }

                if (CanBeTargetedByNormalAttack(
                        attacker.AttackType,
                        attacker.HasActiveFlying,
                        backOccupant) &&
                    !HasFrontRowBlocker(frontOccupant))
                {
                    legalTargets.Add(backCoord);
                }
            }

            return legalTargets;
        }

        public bool CanTarget(BattleState battleState, PlayerId attackerOwnerId, TileCoord attackerCoord, TileCoord targetCoord)
        {
            var legalTargets = GetLegalTargets(battleState, attackerOwnerId, attackerCoord);
            foreach (var legalTarget in legalTargets)
            {
                if (legalTarget == targetCoord)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasFrontRowBlocker(OccupantState frontOccupant)
        {
            if (frontOccupant == null)
            {
                return false;
            }

            if (frontOccupant.DoesNotBlockFrontRow)
            {
                return false;
            }

            return true;
        }

        public static bool CanBeTargetedByNormalAttack(
            AttackType attackerAttackType,
            bool attackerHasActiveFlying,
            OccupantState occupant)
        {
            if (occupant == null || !occupant.CanBeAffected || occupant.IsHiding)
            {
                return false;
            }

            return true;
        }
    }
}
