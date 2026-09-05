using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    public static class HuanShuRules
    {
        public const string CardId = "HuanShu";
        public const string EffectId = "huan_shu";
        public const int QiCost = 3;
        public const int ActiveStateMarker = 1;

        public static bool CanCast(BattleState battleState, PlayerId casterId)
        {
            if (battleState == null)
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
