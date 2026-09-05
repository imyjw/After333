using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project333.Runtime.Infrastructure.Data
{
    [CreateAssetMenu(menuName = "Project333/Cards/Card Catalog", fileName = "CardDefinitionCatalog")]
    public sealed class CardDefinitionCatalogAsset : ScriptableObject
    {
        [SerializeField] private CardDefinitionAsset[] _cards;

        public IReadOnlyList<CardDefinitionAsset> Cards => _cards;

        public IReadOnlyList<CardDefinition> ToDefinitions()
        {
            var definitions = new List<CardDefinition>();

            if (_cards == null)
            {
                return definitions;
            }

            foreach (var card in _cards)
            {
                if (card == null)
                {
                    continue;
                }

                definitions.Add(card.ToDefinition());
            }

            return definitions;
        }

        public ICardDefinitionProvider CreateProvider()
        {
            return new InMemoryCardDefinitionProvider(ToDefinitions());
        }

        public bool TryGetCardAsset(string cardId, out CardDefinitionAsset cardAsset)
        {
            cardAsset = null;

            if (_cards == null || string.IsNullOrWhiteSpace(cardId))
            {
                return false;
            }

            foreach (var card in _cards)
            {
                if (card == null)
                {
                    continue;
                }

                if (string.Equals(card.CardId, cardId, StringComparison.Ordinal))
                {
                    cardAsset = card;
                    return true;
                }
            }

            return false;
        }

        public void ConfigureForTests(CardDefinitionAsset[] cards)
        {
            _cards = cards;
        }
    }
}
