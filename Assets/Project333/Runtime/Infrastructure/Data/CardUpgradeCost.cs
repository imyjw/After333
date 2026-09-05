namespace Project333.Runtime.Infrastructure.Data
{
    public readonly struct CardUpgradeCost
    {
        public CardUpgradeCost(
            int levelFrom,
            int levelTo,
            int requiredCopyCount,
            long requiredResourceGold)
        {
            LevelFrom = levelFrom;
            LevelTo = levelTo;
            RequiredCopyCount = requiredCopyCount;
            RequiredResourceGold = requiredResourceGold;
        }

        public int LevelFrom { get; }

        public int LevelTo { get; }

        public int RequiredCopyCount { get; }

        public long RequiredResourceGold { get; }
    }
}
