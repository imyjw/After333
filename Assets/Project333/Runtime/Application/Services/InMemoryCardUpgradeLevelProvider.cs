using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Application.Services
{
    public sealed class InMemoryCardUpgradeLevelProvider : ICardUpgradeLevelProvider
    {
        private readonly Dictionary<PlayerId, Dictionary<string, int>> _levelsByOwner =
            new Dictionary<PlayerId, Dictionary<string, int>>();

        public void SetUpgradeLevel(PlayerId ownerId, string cardId, int upgradeLevel)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return;
            }

            if (!_levelsByOwner.TryGetValue(ownerId, out var levelsByCardId))
            {
                levelsByCardId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                _levelsByOwner[ownerId] = levelsByCardId;
            }

            levelsByCardId[cardId.Trim()] = upgradeLevel < 0 ? 0 : upgradeLevel;
        }

        public int GetUpgradeLevel(PlayerId ownerId, string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId) ||
                !_levelsByOwner.TryGetValue(ownerId, out var levelsByCardId) ||
                !levelsByCardId.TryGetValue(cardId, out var upgradeLevel))
            {
                return 0;
            }

            return upgradeLevel < 0 ? 0 : upgradeLevel;
        }
    }
}
