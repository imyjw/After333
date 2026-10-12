#nullable enable
using System;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Domain.Battle
{
    public enum BattleTrapEventKind
    {
        Installed = 0,
        Triggered = 1
    }

    /// <summary>Transient presentation result. Installed events carry no secret card identity.</summary>
    public sealed class BattleTrapEvent
    {
        public BattleTrapEvent(BattleTrapEventKind kind, PlayerId ownerId, int armedTrapCount,
            string? cardId = null, string? runtimeId = null, int upgradeLevel = 0,
            PlayerId targetOwnerId = PlayerId.Player, string? targetRuntimeId = null,
            string? targetCardId = null, TileCoord? targetCoord = null, bool addedToHand = false, int effectAmount = 0)
        {
            if (!Enum.IsDefined(typeof(BattleTrapEventKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (ownerId != PlayerId.Player && ownerId != PlayerId.AI) throw new ArgumentOutOfRangeException(nameof(ownerId));
            if (armedTrapCount < 0) throw new ArgumentOutOfRangeException(nameof(armedTrapCount));
            Kind = kind;
            OwnerId = ownerId;
            ArmedTrapCount = armedTrapCount;

            if (kind == BattleTrapEventKind.Installed)
            {
                CardId = RuntimeId = TargetRuntimeId = TargetCardId = string.Empty;
                return;
            }
            if (string.IsNullOrWhiteSpace(cardId)) throw new ArgumentException("Triggered card id is required.", nameof(cardId));
            if (string.IsNullOrWhiteSpace(runtimeId)) throw new ArgumentException("Triggered runtime id is required.", nameof(runtimeId));
            if (upgradeLevel < 0 || upgradeLevel > CardUpgradeRules.MaxLevel) throw new ArgumentOutOfRangeException(nameof(upgradeLevel));
            CardId = cardId;
            RuntimeId = runtimeId;
            UpgradeLevel = upgradeLevel;
            TargetOwnerId = targetOwnerId;
            TargetRuntimeId = targetRuntimeId ?? string.Empty;
            TargetCardId = targetCardId ?? string.Empty;
            TargetCoord = targetCoord;
            AddedToHand = addedToHand;
            EffectAmount = Math.Max(0, effectAmount);
        }

        public BattleTrapEventKind Kind { get; }
        public PlayerId OwnerId { get; }
        public int ArmedTrapCount { get; }
        public string CardId { get; }
        public string RuntimeId { get; }
        public int UpgradeLevel { get; }
        public PlayerId TargetOwnerId { get; }
        public string TargetRuntimeId { get; }
        public string TargetCardId { get; }
        public TileCoord? TargetCoord { get; }
        public bool AddedToHand { get; }
        public int EffectAmount { get; }
    }
}
