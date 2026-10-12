using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Domain.Cards
{
    public static class FireSpearRules
    {
        public const string CardId = "FireSpear";
        public const string EffectId = "fire_spear";
        public const int ManaCost = 3;
        public const int BaseDamage = 30;
        public const string EffectDescription = "선택한 대상이 속한 전장의 같은 열 소환물 모두에게 마법 데미지를 입힌다.";

        // The selected occupant is an anchor. Collateral uses area-damage rules.
        // Hand, resources and cast phase are validated separately by SpellService.
        public static bool IsLegalTarget(BattleState state, PlayerId casterId,
            PlayerId targetOwnerId, TileCoord targetCoord)
        {
            if (state == null || (casterId != PlayerId.Player && casterId != PlayerId.AI) ||
                (targetOwnerId != PlayerId.Player && targetOwnerId != PlayerId.AI)) return false;
            var board = state.GetBoard(targetOwnerId);
            if (!board.IsInside(targetCoord)) return false;
            var occupant = board.GetOccupant(targetCoord);
            return occupant != null && occupant.IsAlive && !occupant.IsSealbound &&
                (targetOwnerId == casterId || !occupant.IsHiding);
        }
    }
}
