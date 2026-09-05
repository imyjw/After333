using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Presentation.Battle
{
    public static class BattlePlayerInputFormatter
    {
        public static string FormatSelectedCard(string cardId)
        {
            return string.IsNullOrWhiteSpace(cardId)
                ? "Selected Card: None"
                : $"Selected Card: {cardId}";
        }

        public static string FormatSelectedOccupant(bool hasSelectedOccupant, TileCoord coord)
        {
            return hasSelectedOccupant
                ? $"Selected Occupant: {coord}"
                : "Selected Occupant: None";
        }

        public static string FormatInteractionStatus(string interactionStatus)
        {
            return string.IsNullOrWhiteSpace(interactionStatus)
                ? "Status: Ready"
                : $"Status: {interactionStatus}";
        }
    }
}
