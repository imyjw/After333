using System;
using System.Collections.Generic;

namespace Project333.Runtime.Infrastructure.Data
{
    public sealed class InMemoryCardDefinitionProvider : ICardDefinitionProvider
    {
        private readonly Dictionary<string, CardDefinition> _definitions;

        public InMemoryCardDefinitionProvider(IEnumerable<CardDefinition> definitions)
        {
            _definitions = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);

            foreach (var definition in definitions)
            {
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
    }
}
