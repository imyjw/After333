using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Application.Online
{
    public sealed class ClientBattleCommandMessage
    {
        public string MatchId { get; set; } = string.Empty;

        public string PlayerToken { get; set; } = string.Empty;

        public string SessionToken { get; set; } = string.Empty;

        public string AccountId { get; set; } = string.Empty;

        public long Sequence { get; set; }

        public PlayerId ActorId { get; set; }

        public OnlineBattleCommandType CommandType { get; set; }

        public string CardId { get; set; } = string.Empty;

        public string HandCardRuntimeId { get; set; } = string.Empty;

        public bool HasTarget { get; set; }

        public PlayerId TargetOwnerId { get; set; }

        public TileCoordDto SourceCoord { get; set; }

        public TileCoordDto DestinationCoord { get; set; }

        public TileCoordDto TargetCoord { get; set; }

        public List<TileCoordDto> SelectedTargetCoords { get; set; } = new List<TileCoordDto>();

        public List<string> SelectedCardIds { get; set; } = new List<string>();
    }
}
