using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    public sealed class SummonService
    {
        public void Summon(BattleState battleState, PlayerId playerId, OccupantState occupant, TileCoord targetCoord)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (occupant == null)
            {
                throw new ArgumentNullException(nameof(occupant));
            }

            if (occupant.OwnerId != playerId)
            {
                throw new InvalidOperationException("Cannot summon an occupant for a different owner.");
            }

            if (occupant.Kind == OccupantKind.Master)
            {
                throw new InvalidOperationException("Master units cannot be summoned.");
            }

            var board = battleState.GetBoard(playerId);
            if (!board.IsInside(targetCoord))
            {
                throw new InvalidOperationException("Cannot summon outside the board.");
            }

            if (!board.IsEmpty(targetCoord))
            {
                throw new InvalidOperationException("Cannot summon onto an occupied tile.");
            }

            occupant.Position = targetCoord;
            occupant.HasSummoningSickness = true;
            occupant.RemainingAttacksThisTurn = occupant.MaxAttacksPerTurn;

            board.Place(targetCoord, occupant);
        }
    }
}
