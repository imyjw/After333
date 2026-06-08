using System;

namespace Project333.Runtime.Presentation.Battle
{
    public static class BattleDebugDeckFactory
    {
        public const int MinimumDeckSize = 10;

        public static string[] CreateDeck(string prefix, int deckSize = MinimumDeckSize)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentException("Deck prefix is required.", nameof(prefix));
            }

            if (deckSize < MinimumDeckSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deckSize),
                    $"Debug deck size must be at least {MinimumDeckSize}.");
            }

            var cardIds = new string[deckSize];
            for (var i = 0; i < deckSize; i++)
            {
                cardIds[i] = $"{prefix}-{i:D2}";
            }

            return cardIds;
        }
    }
}
