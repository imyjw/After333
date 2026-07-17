#nullable enable

using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Domain.Battle
{
    public enum BattleValueChangeCause
    {
        Unknown = 0,
        NormalAttack = 1,
        Counterattack = 2,
        LifeSteal = 3,
        Spell = 4,
        BlueDragon = 5,
        RedDragon = 6,
        DeckExhaustion = 7,
        Firewall = 9
    }

    public sealed class BattleValuePopupEvent
    {
        public BattleValuePopupEvent(string runtimeId, PlayerId ownerId, TileCoord coord, bool isHealing, int amount)
            : this(
                runtimeId,
                ownerId,
                coord,
                isHealing,
                amount,
                ownerId,
                string.Empty,
                string.Empty,
                BattleValueChangeCause.Unknown,
                DamageType.None,
                0,
                0,
                false)
        {
        }

        public BattleValuePopupEvent(
            string runtimeId,
            PlayerId ownerId,
            TileCoord coord,
            bool isHealing,
            int amount,
            PlayerId sourceOwnerId,
            string? sourceRuntimeId,
            string? sourceCardId,
            BattleValueChangeCause cause,
            DamageType damageType,
            int hpBefore,
            int hpAfter,
            bool isInvinciblePrevented = false)
        {
            RuntimeId = runtimeId ?? string.Empty;
            OwnerId = ownerId;
            Coord = coord;
            IsHealing = isHealing;
            Amount = amount < 0 ? 0 : amount;
            SourceOwnerId = sourceOwnerId;
            SourceRuntimeId = sourceRuntimeId ?? string.Empty;
            SourceCardId = sourceCardId ?? string.Empty;
            Cause = cause;
            DamageType = damageType;
            HpBefore = hpBefore < 0 ? 0 : hpBefore;
            HpAfter = hpAfter < 0 ? 0 : hpAfter;
            IsInvinciblePrevented = isInvinciblePrevented;
        }

        public string RuntimeId { get; }

        public PlayerId OwnerId { get; }

        public TileCoord Coord { get; }

        public bool IsHealing { get; }

        public int Amount { get; }

        public PlayerId SourceOwnerId { get; }

        public string SourceRuntimeId { get; }

        public string SourceCardId { get; }

        public BattleValueChangeCause Cause { get; }

        public DamageType DamageType { get; }

        public int HpBefore { get; }

        public int HpAfter { get; }

        public bool IsInvinciblePrevented { get; }
    }
}
