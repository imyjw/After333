using System;
using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Domain.Cards
{
    public static class WerewolfRules
    {
        public const string CardId = "Werewolf";
        public const int BaseAttack = 20;
        public const int BaseHealth = 30;
        public const int ManaCost = 3;
        public const int AttackPerOtherWerewolf = 10;

        public static int CalculatePackAttackBonus(
            OccupantState source,
            BoardState currentBoard)
        {
            if (source == null ||
                currentBoard == null ||
                !string.Equals(source.CardId, CardId, StringComparison.Ordinal) ||
                !source.IsAlive ||
                source.IsDrained ||
                source.IsErasure ||
                source.IsSealbound)
            {
                return 0;
            }

            var otherWerewolfCount = 0;
            foreach (var occupant in currentBoard.EnumerateOccupants())
            {
                if (occupant == null ||
                    ReferenceEquals(occupant, source) ||
                    occupant.OwnerId != source.OwnerId ||
                    !occupant.IsAlive ||
                    occupant.IsSealbound ||
                    !string.Equals(occupant.CardId, CardId, StringComparison.Ordinal))
                {
                    continue;
                }

                // Drained and Erasure suppress an occupant's effects, not its card identity.
                otherWerewolfCount++;
            }

            return otherWerewolfCount * AttackPerOtherWerewolf;
        }
    }
}
