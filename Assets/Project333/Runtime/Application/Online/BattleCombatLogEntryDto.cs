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
        StunApplied = 40,
        StunCleared = 41,
        FreezeApplied = 42,
        FreezeCleared = 43,
        TrapInstalled = 44,
        TrapTriggered = 45,
        ActiveEffectActivated = 46,
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

        // Optional: old recorded matches remain readable without fabricating historical stats.
#nullable enable
        public BattleCombatLogCardSnapshotDto? SourcePreview { get; set; }
        public BattleCombatLogCardSnapshotDto? TargetPreview { get; set; }
#nullable restore
        public List<BattleCombatLogCardSnapshotDto> TargetPreviews { get; set; } = new List<BattleCombatLogCardSnapshotDto>();

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

        public bool HasHoldPreview => EntryType == BattleCombatLogEntryType.Attack
            ? SourcePreview != null && TargetPreview != null
            : TryGetDirectedPreview(out _, out _) || TryGetSingleCardPreview(out _, out _);

        public static bool IsDirectedPreviewType(BattleCombatLogEntryType entryType)
        {
            switch (entryType)
            {
                case BattleCombatLogEntryType.SpellCast:
                case BattleCombatLogEntryType.Damage:
                case BattleCombatLogEntryType.Healing:
                case BattleCombatLogEntryType.TrapTriggered:
                case BattleCombatLogEntryType.AttackBuffApplied:
                case BattleCombatLogEntryType.DestructionMarked:
                case BattleCombatLogEntryType.DestroyedByMark:
                case BattleCombatLogEntryType.CardDrawn:
                case BattleCombatLogEntryType.CardGenerated:
                    return true;
                default:
                    return false;
            }
        }

        public bool TryGetDirectedPreview(
            out BattleCombatLogCardSnapshotDto source,
            out List<BattleCombatLogCardSnapshotDto> targets)
        {
            source = null;
            targets = null;
            if (!IsDirectedPreviewType(EntryType) || SourcePreview == null)
            {
                return false;
            }

            if (TargetPreviews != null && TargetPreviews.Count > 0)
            {
                var list = new List<BattleCombatLogCardSnapshotDto>(TargetPreviews.Count);
                for (var i = 0; i < TargetPreviews.Count; i++)
                {
                    if (TargetPreviews[i] != null)
                    {
                        list.Add(TargetPreviews[i]);
                    }
                }

                if (list.Count > 0)
                {
                    source = SourcePreview;
                    targets = list;
                    return true;
                }
            }

            if (TargetPreview != null)
            {
                var singleTarget = TargetPreview.Clone();
                singleTarget.OwnerId = TargetOwnerId;
                source = SourcePreview;
                targets = new List<BattleCombatLogCardSnapshotDto> { singleTarget };
                return true;
            }

            return false;
        }

        public static bool IsSingleCardPreviewType(BattleCombatLogEntryType entryType)
        {
            switch (entryType)
            {
                case BattleCombatLogEntryType.UnitSummoned:
                case BattleCombatLogEntryType.BuildingConstructed:
                case BattleCombatLogEntryType.ActiveEffectActivated:
                case BattleCombatLogEntryType.ResourceGained:
                case BattleCombatLogEntryType.ResourceSpent:
                case BattleCombatLogEntryType.DrainedApplied:
                case BattleCombatLogEntryType.DrainedCleared:
                case BattleCombatLogEntryType.ErasureApplied:
                case BattleCombatLogEntryType.ErasureCleared:
                case BattleCombatLogEntryType.StunApplied:
                case BattleCombatLogEntryType.StunCleared:
                case BattleCombatLogEntryType.FreezeApplied:
                case BattleCombatLogEntryType.FreezeCleared:
                case BattleCombatLogEntryType.SealboundApplied:
                case BattleCombatLogEntryType.SealboundReleased:
                case BattleCombatLogEntryType.HidingApplied:
                case BattleCombatLogEntryType.HidingRevealed:
                case BattleCombatLogEntryType.HuanShuApplied:
                case BattleCombatLogEntryType.HuanShuCleared:
                    return true;
                default:
                    return false;
            }
        }

        public bool TryGetSingleCardPreview(out BattleCombatLogCardSnapshotDto preview, out PlayerId ownerId)
        {
            if (EntryType == BattleCombatLogEntryType.SpellCast)
            {
                if (!TryGetDirectedPreview(out _, out _) && SourcePreview != null)
                {
                    preview = SourcePreview;
                    ownerId = SourceOwnerId;
                    return true;
                }

                preview = null;
                ownerId = PlayerId.Player;
                return false;
            }

            if (!IsSingleCardPreviewType(EntryType))
            {
                preview = null;
                ownerId = PlayerId.Player;
                return false;
            }

            switch (EntryType)
            {
                case BattleCombatLogEntryType.UnitSummoned:
                case BattleCombatLogEntryType.BuildingConstructed:
                case BattleCombatLogEntryType.ActiveEffectActivated:
                case BattleCombatLogEntryType.ResourceGained:
                case BattleCombatLogEntryType.ResourceSpent:
                    if (SourcePreview != null)
                    {
                        preview = SourcePreview;
                        ownerId = SourceOwnerId;
                        return true;
                    }

                    preview = TargetPreview;
                    ownerId = TargetOwnerId;
                    return preview != null;
                default:
                    if (TargetPreview != null)
                    {
                        preview = TargetPreview;
                        ownerId = TargetOwnerId;
                        return true;
                    }

                    preview = SourcePreview;
                    ownerId = SourceOwnerId;
                    return preview != null;
            }
        }
    }
    public sealed class BattleCombatLogCardSnapshotDto
    {
        public const string CardBackCardId = "OpponentCardBack2";
        public string CardId { get; set; } = string.Empty;
        public string RuntimeId { get; set; } = string.Empty;
        public PlayerId OwnerId { get; set; }
        public bool HasCombatStats { get; set; } = true;
        public int Attack { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public int OriginalAttack { get; set; }
        public int OriginalMaxHp { get; set; }
        public int DamageTaken { get; set; }
        public int HealAmount { get; set; }
        public bool Died { get; set; }

        public static BattleCombatLogCardSnapshotDto Capture(OccupantState occupant) => new BattleCombatLogCardSnapshotDto
        {
            CardId = occupant.CardId, RuntimeId = occupant.RuntimeId,
            OwnerId = occupant.OwnerId, HasCombatStats = true,
            Attack = occupant.Attack, Hp = occupant.CurrentHp, MaxHp = occupant.MaxHp,
            OriginalAttack = occupant.OriginalAttack, OriginalMaxHp = occupant.OriginalMaxHp
        };

        public static BattleCombatLogCardSnapshotDto CaptureOriginal(OccupantState occupant) => new BattleCombatLogCardSnapshotDto
        {
            CardId = occupant.CardId, RuntimeId = occupant.RuntimeId,
            OwnerId = occupant.OwnerId, HasCombatStats = true,
            Attack = occupant.OriginalAttack, Hp = occupant.OriginalMaxHp, MaxHp = occupant.OriginalMaxHp,
            OriginalAttack = occupant.OriginalAttack, OriginalMaxHp = occupant.OriginalMaxHp
        };

        public static BattleCombatLogCardSnapshotDto CreateHandCard(string cardId, int attack, int hp, string runtimeId = "", PlayerId ownerId = PlayerId.Player) => new BattleCombatLogCardSnapshotDto
        {
            CardId = cardId ?? string.Empty, RuntimeId = runtimeId ?? string.Empty,
            OwnerId = ownerId, HasCombatStats = true,
            Attack = attack, Hp = hp, MaxHp = hp,
            OriginalAttack = attack, OriginalMaxHp = hp
        };

        public static BattleCombatLogCardSnapshotDto CreateSpellCard(string cardId, PlayerId ownerId = PlayerId.Player, string runtimeId = "") => new BattleCombatLogCardSnapshotDto
        {
            CardId = cardId ?? string.Empty, RuntimeId = runtimeId ?? string.Empty,
            OwnerId = ownerId, HasCombatStats = false
        };

        public static BattleCombatLogCardSnapshotDto CreateCardBack(PlayerId ownerId = PlayerId.AI) => new BattleCombatLogCardSnapshotDto
        {
            CardId = CardBackCardId, RuntimeId = string.Empty,
            OwnerId = ownerId, HasCombatStats = false
        };

        public BattleCombatLogCardSnapshotDto Clone() => (BattleCombatLogCardSnapshotDto)MemberwiseClone();
        public BattleCombatLogCardSnapshotDto WithOutcome(int damageTaken, bool died)
        {
            var copy = Clone(); copy.DamageTaken = System.Math.Max(0, damageTaken); copy.Died = died; copy.HealAmount = 0;
            return copy;
        }
        public BattleCombatLogCardSnapshotDto WithHealOutcome(int healAmount)
        {
            var copy = Clone(); copy.HealAmount = System.Math.Max(0, healAmount); copy.DamageTaken = 0; copy.Died = false;
            return copy;
        }
    }

}
