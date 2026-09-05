using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    public static class GuRules
    {
        public const string CardId = "Gu";
        public const string EffectId = "gu";
        public const int QiCost = 10;

        public static bool CanCast(BattleState battleState, PlayerId casterId)
        {
            if (battleState == null || !TryFindDestination(battleState, casterId, out _))
            {
                return false;
            }

            var targetOwnerId = battleState.GetOpponent(casterId).Id;
            for (var column = 0; column < BoardState.ColumnCount; column++)
            {
                for (var row = 0; row < BoardState.RowCount; row++)
                {
                    if (IsLegalTarget(
                            battleState,
                            casterId,
                            targetOwnerId,
                            new TileCoord(column, row)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool TryFindDestination(
            BattleState battleState,
            PlayerId casterId,
            out TileCoord destination)
        {
            destination = default;
            if (battleState == null)
            {
                return false;
            }

            var casterBoard = battleState.GetBoard(casterId);
            for (var column = 0; column < BoardState.ColumnCount; column++)
            {
                for (var row = 0; row < BoardState.RowCount; row++)
                {
                    var candidate = new TileCoord(column, row);
                    if (casterBoard.IsEmpty(candidate))
                    {
                        destination = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsLegalTarget(
            BattleState battleState,
            PlayerId casterId,
            PlayerId targetOwnerId,
            TileCoord targetCoord)
        {
            if (battleState == null || targetOwnerId != battleState.GetOpponent(casterId).Id)
            {
                return false;
            }

            var targetBoard = battleState.GetBoard(targetOwnerId);
            if (!targetBoard.IsInside(targetCoord))
            {
                return false;
            }

            var target = targetBoard.GetOccupant(targetCoord);
            return target?.Kind == OccupantKind.Unit &&
                   target.IsAlive &&
                   target.CanBeAffected &&
                   !target.IsHiding;
        }
    }
}
