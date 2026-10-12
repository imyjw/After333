using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Domain.Cards
{
    public static class ManaShieldRules
    {
        public const string CardId = "ManaShield";
        public const string EffectId = "mana_shield";
        public const int ManaCost = 1;
        public const int BaseDefenseBonus = 10;
        public const string EffectDescription = "선택한 유닛(마스터 제외) 또는 건물의 마법 방어력을 영구적으로 10 증가시킨다. 중첩 가능.";

        public static bool IsLegalTarget(BattleState state, PlayerId casterId,
            PlayerId targetOwnerId, TileCoord targetCoord)
        {
            if (state == null || (casterId != PlayerId.Player && casterId != PlayerId.AI) ||
                (targetOwnerId != PlayerId.Player && targetOwnerId != PlayerId.AI)) return false;
            var board = state.GetBoard(targetOwnerId);
            if (!board.IsInside(targetCoord)) return false;
            var target = board.GetOccupant(targetCoord);
            return target != null && target.IsAlive &&
                (target.Kind == OccupantKind.Unit || target.Kind == OccupantKind.Building) &&
                !target.IsSealbound && (casterId == targetOwnerId || !target.IsHiding);
        }

        public static int GetDefenseBonus(int baseAmount, int upgradeLevel)
        {
            if (baseAmount < 0) throw new ArgumentOutOfRangeException(nameof(baseAmount));
            // This card is not upgradeable. Ignore previously stored account levels.
            return baseAmount;
        }
    }
}
