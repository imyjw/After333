using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    public sealed class MoveService
    {
        public void Move(BattleState battleState, PlayerId playerId, TileCoord from, TileCoord to)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            var board = battleState.GetBoard(playerId);
            var occupant = board.GetOccupant(from);
            if (occupant == null)
            {
                throw new InvalidOperationException("대상 타일이 비어있습니다.");
            }

            if (occupant.OwnerId != playerId)
            {
                throw new InvalidOperationException("상대 유닛은 이동시킬 수 없습니다.");
            }

            if (occupant.CannotMoveDueToState)
            {
                throw new InvalidOperationException("이 유닛은 이동 불가 상태입니다.");
            }

            if (!occupant.CanMove)
            {
                throw new InvalidOperationException("이 유닛은 이동 불가능합니다.");
            }

            if (!board.IsInside(to))
            {
                throw new InvalidOperationException("비정상적인 이동입니다.");
            }

            if (board.IsEmpty(to))
            {
                board.Move(from, to);
                return;
            }

            var targetOccupant = board.GetOccupant(to);
            if (targetOccupant.OwnerId != playerId)
            {
                throw new InvalidOperationException("상대 소환물 위로 이동할 수 없습니다.");
            }

            if (targetOccupant.CannotMoveDueToState)
            {
                throw new InvalidOperationException("대상 유닛은 이동 불가 상태입니다.");
            }

            if (!targetOccupant.CanMove)
            {
                throw new InvalidOperationException("대상 유닛은 이동 불가 상태입니다.");
            }

            board.Swap(from, to);
        }
    }
}
