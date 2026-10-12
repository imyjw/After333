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
                throw new InvalidOperationException("공격자 타일이 비어있습니다.");
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

        // Empty means legal. Keep the reason tied to the same normal-attack targeting rules.
        public string GetTargetRejectionMessage(BattleState battleState, PlayerId attackerOwnerId,
            TileCoord attackerCoord, TileCoord targetCoord)
        {
            if (CanTarget(battleState, attackerOwnerId, attackerCoord, targetCoord)) return string.Empty;
            var enemyBoard = battleState.GetOpponentBoard(attackerOwnerId);
            var target = enemyBoard.GetOccupant(targetCoord);
            if (target?.IsHiding == true) return "은신한 유닛은 지정할 수 없습니다.";
            if (target?.IsSealbound == true) return "봉인된 대상은 지정할 수 없습니다.";
            var attacker = battleState.GetBoard(attackerOwnerId).GetOccupant(attackerCoord);
            if (target != null && attacker.Attack > 0 && attacker.AttackType == AttackType.Melee &&
                !attacker.HasActiveFlying && targetCoord.Row == 1 &&
                HasFrontRowBlocker(enemyBoard.GetOccupant(new TileCoord(targetCoord.Column, 0))))
                return "대상은 전열의 소환물에 의해 보호받고 있습니다.";
            return "대상은 지정 불가 상태입니다.";
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
