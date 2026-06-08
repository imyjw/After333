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
                throw new InvalidOperationException("Cannot move from an empty tile.");
            }

            if (occupant.OwnerId != playerId)
            {
                throw new InvalidOperationException("Cannot move an enemy occupant.");
            }

            if (occupant.IsDisabled)
            {
                throw new InvalidOperationException("Disabled occupants cannot move.");
            }

            if (!occupant.CanMove)
            {
                throw new InvalidOperationException("This occupant cannot move.");
            }

            if (!board.IsInside(to))
            {
                throw new InvalidOperationException("Cannot move outside the board.");
            }

            if (board.IsEmpty(to))
            {
                board.Move(from, to);
                return;
            }

            var targetOccupant = board.GetOccupant(to);
            if (targetOccupant.OwnerId != playerId)
            {
                throw new InvalidOperationException("Cannot move onto an enemy occupant.");
            }

            if (targetOccupant.IsDisabled)
            {
                throw new InvalidOperationException("Cannot swap with a disabled occupant.");
            }

            if (!targetOccupant.CanMove)
            {
                throw new InvalidOperationException("Cannot swap with an immobile occupant.");
            }

            board.Swap(from, to);
        }
    }
}
