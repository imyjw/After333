using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Online
{
    public sealed class BattleStateViewDto
    {
        public string MatchId { get; set; } = string.Empty;

        public PlayerId ViewerId { get; set; }

        public OnlineBattleSeatId ViewerOnlineSeatId { get; set; }

        public int TurnNumber { get; set; }

        public PlayerId ActivePlayerId { get; set; }

        public OnlineBattleSeatId ActiveOnlineSeatId { get; set; }

        public PhaseType Phase { get; set; }

        public int TurnTimerDurationSeconds { get; set; }

        public long TurnTimerVersion { get; set; }

        public long ServerUnixTimeMilliseconds { get; set; }

        public long TurnTimerDeadlineUnixTimeMilliseconds { get; set; }

        public bool IsEnded { get; set; }

        public bool HasWinner { get; set; }

        public PlayerId WinnerId { get; set; }

        public OnlineBattleSeatId WinnerOnlineSeatId { get; set; }

        public BattlePlayerViewDto Player { get; set; }

        public BattlePlayerViewDto Opponent { get; set; }

        public List<BoardOccupantViewDto> Occupants { get; set; } = new List<BoardOccupantViewDto>();

        public List<PersistentEffectViewDto> PersistentEffects { get; set; } = new List<PersistentEffectViewDto>();

        public bool HasPendingRobotFusion { get; set; }

        public PlayerId PendingRobotFusionOwnerId { get; set; }

        public string PendingRobotFusionCardId { get; set; } = string.Empty;

        public long CombatLogLatestSequence { get; set; }

        public bool ReplaceCombatLogEntries { get; set; }

        public List<BattleCombatLogEntryDto> CombatLogEntries { get; set; } =
            new List<BattleCombatLogEntryDto>();
    }

    public sealed class BattlePlayerViewDto
    {
        public PlayerId PlayerId { get; set; }

        public OnlineBattleSeatId OnlineSeatId { get; set; }

        public ResourceSetDto Resources { get; set; } = new ResourceSetDto();

        public int DeckCount { get; set; }

        public int HandCount { get; set; }

        public int DiscardCount { get; set; }

        public int MaxHandSize { get; set; }

        public bool HasUsedMulligan { get; set; }

        public List<string> VisibleHandCardIds { get; set; } = new List<string>();

        public List<HandCardViewDto> VisibleHandCards { get; set; } = new List<HandCardViewDto>();
    }

    public sealed class HandCardViewDto
    {
        public string RuntimeId { get; set; } = string.Empty;

        public string CardId { get; set; } = string.Empty;

        public bool IsTemporaryReplicate { get; set; }
    }

    public sealed class BoardOccupantViewDto
    {
        public PlayerId OwnerId { get; set; }

        public OnlineBattleSeatId OwnerOnlineSeatId { get; set; }

        public TileCoordDto Coord { get; set; }

        public string RuntimeId { get; set; } = string.Empty;

        public string CardId { get; set; } = string.Empty;

        public OccupantKind Kind { get; set; }

        public AttackType AttackType { get; set; }

        public DamageType DamageType { get; set; }

        public int Attack { get; set; }

        public int OriginalAttack { get; set; }

        public int PhysicalDefense { get; set; }

        public int MagicDefense { get; set; }

        public int OriginalPhysicalDefense { get; set; }

        public int OriginalMagicDefense { get; set; }

        public int CurrentHp { get; set; }

        public int MaxHp { get; set; }

        public int OriginalMaxHp { get; set; }

        public bool HasOriginalCombatStats { get; set; }

        public bool CanMove { get; set; }

        public bool IsDrained { get; set; }

        public bool IsErasure { get; set; }

        public bool IsSealbound { get; set; }

        public int SealboundOwnerTurnStartsRemaining { get; set; }

        public bool HasRush { get; set; }

        public bool HasHiding { get; set; }

        public bool HidingRevealed { get; set; }

        public bool HasFlying { get; set; }

        public int SpellPower { get; set; }

        public List<InvincibleEffectViewDto> InvincibleEffects { get; set; } =
            new List<InvincibleEffectViewDto>();

        public bool HasGuard { get; set; }

        public bool HasEndure { get; set; }

        public bool HasLifeSteal { get; set; }

        public bool HasRobot { get; set; }

        public bool EndureUsed { get; set; }

        public bool WasSummonedThisTurn { get; set; }

        public bool HasSummoningSickness { get; set; }

        public int RemainingAttacksThisTurn { get; set; }
    }

    public sealed class InvincibleEffectViewDto
    {
        public InvincibleDurationType Duration { get; set; }

        public int OwnerTurnsRemaining { get; set; }

        public int AppliedTurnNumber { get; set; }

        public PlayerId AppliedActivePlayerId { get; set; }
    }

    public sealed class PersistentEffectViewDto
    {
        public string SourceCardId { get; set; } = string.Empty;

        public PlayerId OwnerId { get; set; }

        public OnlineBattleSeatId OwnerOnlineSeatId { get; set; }

        public string EffectId { get; set; } = string.Empty;

        public int AppliedTurn { get; set; }

        public string EndConditionText { get; set; } = string.Empty;

        public ResourceSetDto TurnStartResourceGain { get; set; } = new ResourceSetDto();

        public int OwnerTurnStartsRemaining { get; set; }

        public string TargetRuntimeId { get; set; } = string.Empty;

        public int TargetRow { get; set; } = -1;

        public int RemainingTriggers { get; set; }

        public int EffectDamage { get; set; }

        public DamageType EffectDamageType { get; set; } = DamageType.None;

        public bool TargetsOwnerBoard { get; set; }

        public int CapturedSpellPower { get; set; }

        public bool IsExpired { get; set; }
    }
}
