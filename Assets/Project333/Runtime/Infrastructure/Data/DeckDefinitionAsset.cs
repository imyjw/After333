using System.Collections.Generic;
using UnityEngine;

namespace Project333.Runtime.Infrastructure.Data
{
    [CreateAssetMenu(menuName = "Project333/Decks/Deck Definition", fileName = "DeckDefinition")]
    public sealed class DeckDefinitionAsset : ScriptableObject
    {
        [SerializeField] private CardDefinitionAsset[] _cards;

        public IReadOnlyList<CardDefinitionAsset> Cards => _cards;

        public IReadOnlyList<string> ToCardIds()
        {
            var cardIds = new List<string>();

            if (_cards == null)
            {
                return cardIds;
            }

            foreach (var card in _cards)
            {
                if (card == null || string.IsNullOrWhiteSpace(card.CardId))
                {
                    continue;
                }

                cardIds.Add(card.CardId);
            }

            return cardIds;
        }

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

        public void ConfigureForTests(CardDefinitionAsset[] cards)
        {
            _cards = cards;
        }
    }
}
