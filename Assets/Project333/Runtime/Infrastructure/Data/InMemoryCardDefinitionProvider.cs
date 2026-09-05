using System;
using System.Collections.Generic;

namespace Project333.Runtime.Infrastructure.Data
{
    public sealed class InMemoryCardDefinitionProvider : ICardDefinitionProvider
    {
        private readonly Dictionary<string, CardDefinition> _definitions;
        private readonly List<CardDefinition> _orderedDefinitions;

        public InMemoryCardDefinitionProvider(IEnumerable<CardDefinition> definitions)
        {
            _definitions = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
            _orderedDefinitions = new List<CardDefinition>();

            foreach (var definition in definitions)
            {
                if (definition == null)
                {
                    continue;
                }

                if (!_definitions.ContainsKey(definition.CardId))
                {
                    _orderedDefinitions.Add(definition);
                }

                _definitions[definition.CardId] = definition;
            }
        }

        public CardDefinition GetRequired(string cardId)
        {
            if (!_definitions.TryGetValue(cardId, out var definition))
            {
                throw new InvalidOperationException($"Card definition '{cardId}' was not found.");
            }

            return definition;
        }

        public IReadOnlyList<CardDefinition> GetAll()
        {
            return _orderedDefinitions;
        }
    }
}
