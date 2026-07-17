using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Application.Services
{
    public sealed class BattleSetupRequest
    {
        public BattleSetupRequest(
            IEnumerable<string> playerDeckCardIds,
            IEnumerable<string> aiDeckCardIds,
            PlayerId firstPlayerId,
            bool aiMulliganEnabled = false)
        {
            PlayerDeckCardIds = new List<string>(playerDeckCardIds);
            AIDeckCardIds = new List<string>(aiDeckCardIds);
            FirstPlayerId = firstPlayerId;
            AIMulliganEnabled = aiMulliganEnabled;
        }

        public IReadOnlyList<string> PlayerDeckCardIds { get; }

        public IReadOnlyList<string> AIDeckCardIds { get; }

        public PlayerId FirstPlayerId { get; }

        public bool AIMulliganEnabled { get; }
    }
}
