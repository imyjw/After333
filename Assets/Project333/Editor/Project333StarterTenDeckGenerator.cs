using System.Collections.Generic;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;
using UnityEngine;

namespace Project333.Editor
{
    public static class Project333StarterTenDeckGenerator
    {
        private const string StarterRoot = "Assets/Project333/ScriptableObjects/StarterTen";
        private const string CardsRoot = StarterRoot + "/Cards";
        private const string DecksRoot = StarterRoot + "/Decks";

        [MenuItem("Tools/Project333/Generate Starter 10 Deck Assets")]
        public static void Generate()
        {
            EnsureFolderExists(StarterRoot, "Decks");

            var cards = LoadExistingStarterCards();
            if (cards.Count == 0)
            {
                Debug.LogWarning(
                    "No starter card assets were found under Assets/Project333/ScriptableObjects/StarterTen/Cards. " +
                    "Create or generate the starter cards first.");
                return;
            }

            var catalog = LoadOrCreate<CardDefinitionCatalogAsset>($"{StarterRoot}/StarterTenCardCatalog.asset");
            catalog.ConfigureForTests(cards.ToArray());
            EditorUtility.SetDirty(catalog);

            var deckCards = BuildMirrorDeck(cards, 3);

            var playerDeck = LoadOrCreate<DeckDefinitionAsset>($"{DecksRoot}/StarterTenPlayerDeck.asset");
            playerDeck.ConfigureForTests(deckCards.ToArray());
            EditorUtility.SetDirty(playerDeck);

            var aiDeck = LoadOrCreate<DeckDefinitionAsset>($"{DecksRoot}/StarterTenAiDeck.asset");
            aiDeck.ConfigureForTests(deckCards.ToArray());
            EditorUtility.SetDirty(aiDeck);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = playerDeck;

            Debug.Log(
                $"Generated StarterTen decks from {cards.Count} existing card assets. " +
                "Created/updated StarterTenCardCatalog, StarterTenPlayerDeck, and StarterTenAiDeck.");
        }

        private static List<CardDefinitionAsset> LoadExistingStarterCards()
        {
            var guids = AssetDatabase.FindAssets("t:CardDefinitionAsset", new[] { CardsRoot });
            var cards = new List<CardDefinitionAsset>(guids.Length);

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>(assetPath);
                if (card == null)
                {
                    continue;
                }

                cards.Add(card);
            }

            cards.Sort((left, right) =>
            {
                var nameComparison = string.Compare(left.DisplayName, right.DisplayName, System.StringComparison.Ordinal);
                if (nameComparison != 0)
                {
                    return nameComparison;
                }

                return string.Compare(left.CardId, right.CardId, System.StringComparison.Ordinal);
            });

            return cards;
        }

        private static List<CardDefinitionAsset> BuildMirrorDeck(
            IReadOnlyList<CardDefinitionAsset> cards,
            int copiesPerCard)
        {
            var deck = new List<CardDefinitionAsset>(cards.Count * copiesPerCard);

            for (var copyIndex = 0; copyIndex < copiesPerCard; copyIndex++)
            {
                for (var cardIndex = 0; cardIndex < cards.Count; cardIndex++)
                {
                    deck.Add(cards[cardIndex]);
                }
            }

            return deck;
        }

        private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, assetPath);
            return created;
        }

        private static void EnsureFolderExists(string parentFolder, string childFolder)
        {
            var fullPath = $"{parentFolder}/{childFolder}";
            if (AssetDatabase.IsValidFolder(fullPath))
            {
                return;
            }

            AssetDatabase.CreateFolder(parentFolder, childFolder);
        }
    }
}
