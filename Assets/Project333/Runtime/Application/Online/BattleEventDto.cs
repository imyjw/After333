using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Online
{
    public enum BattleEventType
    {
        Unknown = 0,
        StateChanged = 1,
        TurnStarted = 2,
        TurnEnded = 3,
        CardPlayed = 4,
        SpellCast = 5,
        OccupantMoved = 6,
        AttackStarted = 7,
        DamageApplied = 8,
        HealingApplied = 9,
        OccupantRemoved = 10,
        BattleEnded = 11,
        ReconnectGraceStarted = 12,
        ReconnectGraceCancelled = 13,
        TurnTimerExpired = 14,
        RobotFusionResolved = 15,
        OccupantAbsorbed = 16,
        InvinciblePrevented = 17,
        HuanShuRedirected = 18,
        AreaSpellEffectTriggered = 19,
        CardDrawn = 20
    }

    public sealed class BattleEventDto
    {
        public BattleEventType EventType { get; set; }

        public PlayerId SourceOwnerId { get; set; }

        public TileCoordDto SourceCoord { get; set; }

        public PlayerId TargetOwnerId { get; set; }

        public TileCoordDto TargetCoord { get; set; }

        public List<TileCoordDto> TargetCoords { get; set; } = new List<TileCoordDto>();

        public string RuntimeId { get; set; } = string.Empty;

        public string CardId { get; set; } = string.Empty;

        public string SourceRuntimeId { get; set; } = string.Empty;

        public string SourceCardId { get; set; } = string.Empty;

        public string EffectId { get; set; } = string.Empty;

        public string TargetRuntimeId { get; set; } = string.Empty;

        public string TargetCardId { get; set; } = string.Empty;

        public int CardAttack { get; set; }

        public int CardMaxHp { get; set; }

        public int? CardSpellDamage { get; set; }

        public int AttackBonus { get; set; }

        public int HpBonus { get; set; }

        public int Amount { get; set; }

        public int HpBefore { get; set; }

        public int HpAfter { get; set; }

        public bool IsDraw { get; set; }

        public AttackType AttackType { get; set; }

        public DamageType DamageType { get; set; }

        public BattleValueChangeCause ValueCause { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}
