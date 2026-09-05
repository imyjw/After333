using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Online
{
    public enum BattleCombatLogEntryType
    {
        Unknown = 0,
        BattleStarted = 1,
        UnitSummoned = 2,
        BuildingConstructed = 3,
        UpgradeApplied = 4,
        OccupantMoved = 5,
        OccupantsSwapped = 6,
        Attack = 7,
        ShielderRedirected = 8,
        [System.Obsolete("Use ShielderRedirected. This alias only reads pre-migration combat-log snapshots.")]
        GuardRedirected = ShielderRedirected,
        SpellCast = 9,
        AttackBuffApplied = 10,
        DestructionMarked = 11,
        Damage = 12,
        Healing = 13,
        ResourceGained = 14,
        OccupantRemoved = 15,
        DestroyedByMark = 16,
        DrainedApplied = 17,
        DrainedCleared = 18,
        ErasureApplied = 19,
        ErasureCleared = 20,
        TurnEnded = 21,
        TurnTimedOut = 22,
        TurnStarted = 23,
        BattleEnded = 24,
        RobotFusion = 25,
        CardGenerated = 26,
        SealboundApplied = 27,
        SealboundReleased = 28,
        HidingApplied = 29,
        HidingRevealed = 30,
        OccupantControlled = 31,
        HuanShuApplied = 32,
        HuanShuCleared = 33,
        HuanShuRedirected = 34,
        ResourceSpent = 35,
        DemonKingRevivalStarted = 36,
        DemonKingRevived = 37,
        HeroGrowth = 38,
        CardDrawn = 39,
    }

    public enum BattleCombatLogResourceType
    {
        None = 0,
        Mana = 1,
        Qi = 2,
        Power = 3,
        Gold = 4,
    }

    /// <summary>
    /// Language-neutral combat history authored by the authoritative server.
    /// The client owns localization and presentation.
    /// </summary>
    public sealed class BattleCombatLogEntryDto
    {
        public long Sequence { get; set; }

        public BattleCombatLogEntryType EntryType { get; set; }

        public PlayerId SourceOwnerId { get; set; }

        public PlayerId TargetOwnerId { get; set; }

        public string SourceCardId { get; set; } = string.Empty;

        public string TargetCardId { get; set; } = string.Empty;

        public TileCoordDto SourceCoord { get; set; }

        public TileCoordDto TargetCoord { get; set; }

        public List<int> SourceHpHistory { get; set; } = new List<int>();

        public List<int> TargetHpHistory { get; set; } = new List<int>();

        public bool SourceRemoved { get; set; }

        public bool TargetRemoved { get; set; }

        public bool SourceDamagePrevented { get; set; }

        public bool TargetDamagePrevented { get; set; }

        public bool HasCounterattack { get; set; }

        public bool IsDraw { get; set; }

        public int Amount { get; set; }

        public int AttackBonus { get; set; }

        public int HpBonus { get; set; }

        public int TurnNumber { get; set; }

        public AttackType AttackType { get; set; }

        public DamageType DamageType { get; set; }

        public BattleValueChangeCause ValueCause { get; set; }

        public BattleCombatLogResourceType ResourceType { get; set; }
    }
}
