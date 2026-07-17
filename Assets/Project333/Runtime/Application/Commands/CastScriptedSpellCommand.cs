using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Application.Commands
{
    public sealed class CastScriptedSpellCommand : IHandCardCommand
    {
        public CastScriptedSpellCommand(string cardId, string handCardRuntimeId = null)
        {
            CardId = cardId;
            HandCardRuntimeId = handCardRuntimeId ?? string.Empty;
            HasTarget = false;
        }

        public CastScriptedSpellCommand(
            string cardId,
            PlayerId targetOwnerId,
            TileCoord targetCoord,
            string handCardRuntimeId = null)
        {
            CardId = cardId;
            HandCardRuntimeId = handCardRuntimeId ?? string.Empty;
            TargetOwnerId = targetOwnerId;
            TargetCoord = targetCoord;
            HasTarget = true;
        }

        public CastScriptedSpellCommand(
            string cardId,
            IReadOnlyList<TileCoord> targetCoords,
            string handCardRuntimeId = null)
        {
            if (targetCoords == null)
            {
                throw new ArgumentNullException(nameof(targetCoords));
            }

            CardId = cardId;
            HandCardRuntimeId = handCardRuntimeId ?? string.Empty;
            TargetCoords = new List<TileCoord>(targetCoords);
            HasMultipleTargets = true;
        }

        public string CardId { get; }

        public string HandCardRuntimeId { get; }

        public bool HasTarget { get; }

        public PlayerId TargetOwnerId { get; }

        public TileCoord TargetCoord { get; }

        public bool HasMultipleTargets { get; }

        public IReadOnlyList<TileCoord> TargetCoords { get; } = Array.Empty<TileCoord>();
    }
}
