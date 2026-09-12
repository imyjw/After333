namespace Project333.Runtime.Domain.Draft
{
    public static class DraftRarityWeights
    {
        // Basis points per offer slot, not quotas for the completed deck.
        public const int Common = 6600;
        public const int Uncommon = 2000;
        public const int Rare = 1000;
        public const int Unique = 400;
    }
}
