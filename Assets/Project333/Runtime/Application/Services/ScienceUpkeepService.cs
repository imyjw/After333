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
                if (occupant == null || occupant.IsErasure || occupant.IsSealbound)
                {
                    continue;
                }

                var upkeep = occupant switch
                {
                    UnitState unit => unit.SciencePowerUpkeep,
                    BuildingState building => building.SciencePowerUpkeep,
                    _ => 0,
                };
                if (upkeep <= 0)
                {
                    continue;
                }

                var upkeepCost = new ResourceSet(mana: 0, qi: 0, power: upkeep, gold: 0);
                if (activePlayer.Resources.CanAfford(upkeepCost))
                {
                    var resourcesBeforePayment = activePlayer.Resources.Clone();
                    activePlayer.Resources.Spend(upkeepCost);
                    battleState.RecordResourceChange(new BattleResourceChangeEvent(
                        activePlayer.Id,
                        occupant.CardId,
                        gained: new ResourceSet(),
                        spent: new ResourceSet(
                            resourcesBeforePayment.Mana - activePlayer.Resources.Mana,
                            resourcesBeforePayment.Qi - activePlayer.Resources.Qi,
                            resourcesBeforePayment.Power - activePlayer.Resources.Power,
                            resourcesBeforePayment.Gold - activePlayer.Resources.Gold)));
                    SetUpkeepState(occupant, isUnpaid: false);
                }
                else
                {
                    SetUpkeepState(occupant, isUnpaid: true);
                }
            }
        }

        private static void SetUpkeepState(OccupantState occupant, bool isUnpaid)
        {
            occupant.IsDrained = isUnpaid;
        }
    }
}
