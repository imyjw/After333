using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Application.Services
{
    public sealed class ScienceUpkeepService
    {
        private static readonly TileCoord[] PaymentOrder =
        {
            new TileCoord(0, 0),
            new TileCoord(0, 1),
            new TileCoord(1, 0),
            new TileCoord(1, 1),
            new TileCoord(2, 0),
            new TileCoord(2, 1),
            new TileCoord(3, 0),
            new TileCoord(3, 1),
            new TileCoord(4, 0),
            new TileCoord(4, 1),
        };

        public void ResolveUpkeep(BattleState battleState)
        {
            var activePlayer = battleState.GetPlayer(battleState.ActivePlayerId);
            var activeBoard = battleState.GetBoard(battleState.ActivePlayerId);

            foreach (var tileCoord in PaymentOrder)
            {
                var occupant = activeBoard.GetOccupant(tileCoord);
                if (!(occupant is UnitState unitState) || !unitState.IsScience)
                {
                    continue;
                }

                if (unitState.SciencePowerUpkeep <= 0)
                {
                    unitState.IsDisabled = false;
                    continue;
                }

                var upkeepCost = new ResourceSet(mana: 0, qi: 0, power: unitState.SciencePowerUpkeep, gold: 0);
                if (activePlayer.Resources.CanAfford(upkeepCost))
                {
                    activePlayer.Resources.Spend(upkeepCost);
                    unitState.IsDisabled = false;
                }
                else
                {
                    unitState.IsDisabled = true;
                }
            }
        }
    }
}
