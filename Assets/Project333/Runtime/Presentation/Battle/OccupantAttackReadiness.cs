using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Presentation.Battle
{
    // Display policy only. Resources and target legality are still checked by the attack service.
    public static class OccupantAttackReadiness
    {
        public static bool ShouldShow(BattleState battle, OccupantState occupant)
        {
            return battle != null && !battle.IsEnded && battle.Phase == PhaseType.Main &&
                battle.ActivePlayerId == PlayerId.Player && occupant != null &&
                occupant.OwnerId == PlayerId.Player && occupant.IsAlive &&
                !occupant.CannotAttack && !occupant.HasSummoningSickness &&
                occupant.RemainingAttacksThisTurn > 0 && occupant.Attack > 0 &&
                (!(occupant is BuildingState building) || building.CanAttackAsBuilding);
        }
    }
}
