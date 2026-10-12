using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Application.Services
{
    // Like other scripted card effects, supported actives are registered by stable CardId.
    // Description text is never parsed as executable rules.
    public static class ActiveEffectService
    {
        public const string PowerPlantDescription = "1턴에 1번, 골드를 1 소모해서 전력을 2 얻는다.";
        public static bool HasActiveEffect(OccupantState occupant) =>
            occupant != null && occupant.Kind == OccupantKind.Building &&
            string.Equals(occupant.CardId, PowerPlantRules.CardId, StringComparison.Ordinal);

        // Eligibility controls button visibility; affordability is checked only on activation.
        public static bool CanOfferActivation(BattleState state, PlayerId actor, TileCoord source)
        {
            if (state == null || state.IsEnded || state.Phase != PhaseType.Main ||
                state.ActivePlayerId != actor || state.PendingRobotFusion != null) return false;
            var occupant = state.GetBoard(actor).GetOccupant(source);
            return HasActiveEffect(occupant) && occupant.OwnerId == actor &&
                occupant.IsAlive && !occupant.CannotActivate &&
                occupant.LastActiveEffectTurnNumber != state.TurnNumber;
        }

        public static string GetCostFailureMessage(BattleState state, PlayerId actor, TileCoord source)
        {
            var occupant = state?.GetBoard(actor).GetOccupant(source);
            if (HasActiveEffect(occupant) && state.GetPlayer(actor).Resources.Gold < PowerPlantRules.TriggerGoldCost)
                return "자원이 부족합니다.";
            return string.Empty;
        }

        public static bool CanActivate(BattleState state, PlayerId actor, TileCoord source) =>
            CanOfferActivation(state, actor, source) && string.IsNullOrEmpty(GetCostFailureMessage(state, actor, source));

        public static void Activate(BattleState state, PlayerId actor, TileCoord source, string runtimeId)
        {
            if (!CanOfferActivation(state, actor, source))
                throw new InvalidOperationException("지금은 액티브 효과를 사용할 수 없습니다.");
            var occupant = state.GetBoard(actor).GetOccupant(source);
            if (string.IsNullOrWhiteSpace(runtimeId) || occupant.RuntimeId != runtimeId)
                throw new InvalidOperationException("선택한 소환물이 변경되었습니다.");
            var costFailure = GetCostFailureMessage(state, actor, source);
            if (!string.IsNullOrEmpty(costFailure)) throw new InvalidOperationException(costFailure);
            var spent = new ResourceSet(0, 0, 0, PowerPlantRules.TriggerGoldCost);
            var gained = new ResourceSet(0, 0, PowerPlantRules.PowerGain, 0);
            var player = state.GetPlayer(actor);
            player.Resources.Spend(spent);
            player.Resources.Add(gained);
            occupant.RestoreActiveEffectUsage(state.TurnNumber);
            state.RecordResourceChange(new BattleResourceChangeEvent(actor, occupant.CardId, gained, spent));
        }
    }
}
