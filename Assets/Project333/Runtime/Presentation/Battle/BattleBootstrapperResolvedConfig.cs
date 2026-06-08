using System.Collections.Generic;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class BattleBootstrapperResolvedConfig
    {
        public BattleBootstrapperResolvedConfig(
            IReadOnlyList<string> playerDeckCardIds,
            IReadOnlyList<string> aiDeckCardIds,
            ICardDefinitionProvider cardDefinitionProvider)
        {
            PlayerDeckCardIds = playerDeckCardIds;
            AIDeckCardIds = aiDeckCardIds;
            CardDefinitionProvider = cardDefinitionProvider;
        }

        public IReadOnlyList<string> PlayerDeckCardIds { get; }

        public IReadOnlyList<string> AIDeckCardIds { get; }

        public ICardDefinitionProvider CardDefinitionProvider { get; }
    }
}
