using System.Collections.Generic;

namespace Project333.Runtime.Application.Services
{
    public sealed class DraftAdvanceResult
    {
        public DraftAdvanceResult(
            bool isComplete,
            DraftOffer nextOffer,
            IReadOnlyList<string> completedDeckCardIds,
            string selectedCardId)
        {
            IsComplete = isComplete;
            NextOffer = nextOffer;
            CompletedDeckCardIds = completedDeckCardIds;
            SelectedCardId = selectedCardId;
        }

        public bool IsComplete { get; }

        public DraftOffer NextOffer { get; }

        public IReadOnlyList<string> CompletedDeckCardIds { get; }

        public string SelectedCardId { get; }
    }
}
