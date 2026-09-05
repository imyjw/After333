using System;
using System.Collections.Generic;
using Project333.Runtime.Infrastructure.Data;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    public static class BattleBootstrapperConfigResolver
    {
        private const string CardDefinitionJsonResourcePath = "Project333/Data/cards";

        public static BattleBootstrapperResolvedConfig Resolve(
            CardDefinitionCatalogAsset cardCatalogAsset,
            DeckDefinitionAsset playerDeckAsset,
            DeckDefinitionAsset aiDeckAsset,
            string[] playerDeckCardIds,
            string[] aiDeckCardIds,
            bool useGeneratedDebugDecksWhenEmpty,
            int generatedDeckSize,
            IReadOnlyList<string> playerDeckOverrideCardIds = null,
            IReadOnlyList<string> aiDeckOverrideCardIds = null,
            bool preferJsonCardDefinitions = false)
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

            var provider = ResolveProvider(cardCatalogAsset, playerDeckAsset, aiDeckAsset, preferJsonCardDefinitions);

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
            DeckDefinitionAsset aiDeckAsset,
            bool preferJsonCardDefinitions)
        {
            if (preferJsonCardDefinitions)
            {
                var jsonProvider = TryCreateJsonProviderFromResources();
                if (jsonProvider != null)
                {
                    return jsonProvider;
                }
            }

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

        private static ICardDefinitionProvider TryCreateJsonProviderFromResources()
        {
            var jsonAsset = Resources.Load<TextAsset>(CardDefinitionJsonResourcePath);
            if (jsonAsset == null || string.IsNullOrWhiteSpace(jsonAsset.text))
            {
                return null;
            }

            try
            {
                var validation = CardDatabaseValidator.ValidateJson(jsonAsset.text);
                if (!validation.IsValid)
                {
                    Debug.LogError(
                        $"JSON card definitions failed validation from Resources/{CardDefinitionJsonResourcePath}: {string.Join("; ", validation.Errors)}");
                    return null;
                }

                foreach (var warning in validation.Warnings)
                {
                    Debug.LogWarning($"JSON card definition warning: {warning}");
                }

                var database = JsonCardDefinitionDatabase.FromJson(jsonAsset.text);
                return database.CreateProvider();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to load JSON card definitions from Resources/{CardDefinitionJsonResourcePath}: {exception.Message}");
                return null;
            }
        }
    }
}
