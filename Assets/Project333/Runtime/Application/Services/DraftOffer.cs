using System;
using System.Collections.Generic;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Services
{
    public sealed class DraftOffer
    {
        public DraftOffer(
            IReadOnlyList<CardDefinitionAsset> candidateCards,
            int pickNumber,
            bool isLegendaryOpeningOffer)
        {
            CandidateCards = candidateCards ?? throw new ArgumentNullException(nameof(candidateCards));
            PickNumber = pickNumber;
            IsLegendaryOpeningOffer = isLegendaryOpeningOffer;
        }

        public IReadOnlyList<CardDefinitionAsset> CandidateCards { get; }

        public int PickNumber { get; }

        public bool IsLegendaryOpeningOffer { get; }
    }
}
