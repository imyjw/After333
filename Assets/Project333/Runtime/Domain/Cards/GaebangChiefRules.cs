using System;
using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Domain.Cards
{
    public static class GaebangChiefRules
    {
        public const string CardId = "GaebangChief";
        public const int BaseAttack = 10;
        public const int BaseHealth = 60;
        public const int QiCost = 7;
        public const int AttackPerGold = 10;
        public const string EffectDescription = "현재 소유한 골드 1당 공격력 +10. 골드가 변하면 즉시 반영되며 골드를 소모하지 않습니다.";

        public static int CalculateGoldAttackBonus(OccupantState source, BoardState currentBoard)
        {
            if (source == null || currentBoard == null || source.Kind != OccupantKind.Unit ||
                !string.Equals(source.CardId, CardId, StringComparison.Ordinal) || !source.IsAlive || source.EffectsSuppressed)
                return 0;

            // This is a current-owner resource effect, never a permanent combat stat gain.
            var bonus = (long)Math.Max(0, currentBoard.GetOwnerGold(source.OwnerId)) * AttackPerGold;
            return (int)Math.Min(bonus, int.MaxValue - (long)Math.Max(0, source.BaseAttack));
        }
    }
}
