using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Tutorial
{
    // Independent lesson inventory. Never writes account wallets, collection assets or receipts.
    public sealed class TutorialUpgradePractice
    {
        public const string CardId = "Goblin";
        public long Gold { get; private set; } = 3;
        public int Copies { get; private set; } = 3;
        public int Level { get; private set; }
        public bool TryUpgrade(CardDefinition definition)
        {
            if (definition?.CardId != CardId || Level != 0 || !CardUpgradeRules.IsCardUpgradeable(definition) ||
                !CardUpgradeRules.TryGetNextUpgradeCost(Level, out var cost) || Copies < cost.RequiredCopyCount || Gold < cost.RequiredResourceGold)
                return false;
            Copies -= cost.RequiredCopyCount; Gold -= cost.RequiredResourceGold; Level = cost.LevelTo;
            return true;
        }
    }
}
