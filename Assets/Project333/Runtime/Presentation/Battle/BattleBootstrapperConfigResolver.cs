using System;
using System.Collections.Generic;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Presentation.Battle
{
    public static class BattleBootstrapperConfigResolver
    {
        public static BattleBootstrapperResolvedConfig Resolve(
            CardDefinitionCatalogAsset cardCatalogAsset,
            DeckDefinitionAsset playerDeckAsset,
            DeckDefinitionAsset aiDeckAsset,
            string[] playerDeckCardIds,
            string[] aiDeckCardIds,
            bool useGeneratedDebugDecksWhenEmpty,
            int generatedDeckSize,
            IReadOnlyList<string> playerDeckOverrideCardIds = null,
            IReadOnlyList<string> aiDeckOverrideCardIds = null)
        {
            var resolvedPlayerDeckIds = ResolveDeckCardIds(playerDeckAsset, playerDeckCardIds, playerDeckOverrideCardIds);
            var resolvedAIDeckIds = ResolveDeckCardIds(aiDeckAsset, aiDeckCardIds, aiDeckOverrideCardIds);

            if (useGeneratedDebugDecksWhenEmpty)
            {
                if (resolvedPlayerDeckIds.Count == 0)
                {
                    resolvedPlayerDeckIds = BattleDebugDeckFactory.CreateDeck("P", generatedDeckSize);
                }

                if (resolvedAIDeckIds.Count == 0)
                {
                    resolvedAIDeckIds = BattleDebugDeckFactory.CreateDeck("A", generatedDeckSize);
                }
            }

            var provider = ResolveProvider(cardCatalogAsset, playerDeckAsset, aiDeckAsset);

            return new BattleBootstrapperResolvedConfig(
                playerDeckCardIds: resolvedPlayerDeckIds,
                aiDeckCardIds: resolvedAIDeckIds,
                cardDefinitionProvider: provider);
        }

        private static IReadOnlyList<string> ResolveDeckCardIds(
            DeckDefinitionAsset deckAsset,
            string[] fallbackCardIds,
            IReadOnlyList<string> overrideCardIds)
        {
            if (overrideCardIds != null && overrideCardIds.Count > 0)
            {
                return new List<string>(overrideCardIds);
            }

            if (deckAsset != null)
            {
                var assetCardIds = deckAsset.ToCardIds();
                if (assetCardIds.Count > 0)
                {
                    return assetCardIds;
                }
            }

            return fallbackCardIds != null
                ? new List<string>(fallbackCardIds)
                : Array.Empty<string>();
        }

        private static ICardDefinitionProvider ResolveProvider(
            CardDefinitionCatalogAsset cardCatalogAsset,
            DeckDefinitionAsset playerDeckAsset,
            DeckDefinitionAsset aiDeckAsset)
        {
            if (cardCatalogAsset != null)
            {
                return cardCatalogAsset.CreateProvider();
            }

            var definitions = new List<CardDefinition>();

            if (playerDeckAsset != null)
            {
                definitions.AddRange(playerDeckAsset.ToDefinitions());
            }

            if (aiDeckAsset != null)
            {
                definitions.AddRange(aiDeckAsset.ToDefinitions());
            }

            return new InMemoryCardDefinitionProvider(definitions);
        }
    }
}
