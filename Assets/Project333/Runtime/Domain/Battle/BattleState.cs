using System.Collections.Generic;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Domain.Battle
{
    public sealed class BattleState
    {
        public BattleState(
            PlayerState player,
            PlayerState ai,
            BoardState playerBoard,
            BoardState aiBoard)
        {
            Player = player;
            AI = ai;
            PlayerBoard = playerBoard;
            AIBoard = aiBoard;
            PersistentEffects = new List<PersistentEffectState>();
            ValuePopupEvents = new List<BattleValuePopupEvent>();
            CardDrawEvents = new List<BattleCardDrawEvent>();
            CardGenerationEvents = new List<BattleCardGenerationEvent>();
            ResourceChangeEvents = new List<BattleResourceChangeEvent>();
            AreaSpellEffectEvents = new List<BattleAreaSpellEffectEvent>();
            Counters = new BattleCounters();
            Result = new BattleResultState();
            Phase = PhaseType.BattleStart;
            ActivePlayerId = PlayerId.Player;
            TurnNumber = 0;
            SynchronizeOccupantTurnContext();
        }

        public int TurnNumber { get; private set; }

        public PlayerId ActivePlayerId { get; private set; }

        public PhaseType Phase { get; private set; }

        public BattleResultState Result { get; }

        public PlayerState Player { get; }

        public PlayerState AI { get; }

        public BoardState PlayerBoard { get; }

        public BoardState AIBoard { get; }

        public List<PersistentEffectState> PersistentEffects { get; }

        public List<BattleValuePopupEvent> ValuePopupEvents { get; }

        public List<BattleCardDrawEvent> CardDrawEvents { get; }

        public List<BattleCardGenerationEvent> CardGenerationEvents { get; }

        public List<BattleResourceChangeEvent> ResourceChangeEvents { get; }

        public List<BattleAreaSpellEffectEvent> AreaSpellEffectEvents { get; }

        public BattleCounters Counters { get; }

        public PendingRobotFusionState PendingRobotFusion { get; private set; }

        public BattleAttackResolution LastAttackResolution { get; private set; }

        public bool IsEnded => Result.HasResult;

        public PlayerState GetPlayer(PlayerId playerId)
        {
            return playerId == PlayerId.Player ? Player : AI;
        }

        public PlayerState GetOpponent(PlayerId playerId)
        {
            return playerId == PlayerId.Player ? AI : Player;
        }

        public BoardState GetBoard(PlayerId playerId)
        {
            return playerId == PlayerId.Player ? PlayerBoard : AIBoard;
        }

        public BoardState GetOpponentBoard(PlayerId playerId)
        {
            return playerId == PlayerId.Player ? AIBoard : PlayerBoard;
        }

        public void StartNextTurn(PlayerId activePlayerId)
        {
            ActivePlayerId = activePlayerId;
            TurnNumber += 1;
            Phase = PhaseType.TurnStart;
            SynchronizeOccupantTurnContext();
        }

        public void SetActivePlayer(PlayerId activePlayerId)
        {
            ActivePlayerId = activePlayerId;
            SynchronizeOccupantTurnContext();
        }

        public void SetPhase(PhaseType phase)
        {
            Phase = phase;
        }

        public void RestoreRuntimeState(int turnNumber, PlayerId activePlayerId, PhaseType phase)
        {
            TurnNumber = turnNumber < 0 ? 0 : turnNumber;
            ActivePlayerId = activePlayerId;
            Phase = phase;
            SynchronizeOccupantTurnContext();
        }

        public void ResolveInvincibleTurnEnd(PlayerId endingPlayerId)
        {
            ResolveInvincibleTurnEnd(PlayerBoard, endingPlayerId);
            ResolveInvincibleTurnEnd(AIBoard, endingPlayerId);
        }

        public void EndBattle(PlayerId winner)
        {
            PendingRobotFusion = null;
            Result.SetWinner(winner);
            Phase = PhaseType.Ended;
        }

        public void EndBattleAsDraw()
        {
            PendingRobotFusion = null;
            Result.SetDraw();
            Phase = PhaseType.Ended;
        }

        public void BeginRobotFusion(PlayerId ownerId, string cardId)
        {
            if (PendingRobotFusion != null)
            {
                throw new System.InvalidOperationException("A Robot Fusion selection is already pending.");
            }

            PendingRobotFusion = new PendingRobotFusionState(ownerId, cardId);
        }

        public void CompleteRobotFusion(PlayerId ownerId, string cardId)
        {
            if (PendingRobotFusion == null ||
                PendingRobotFusion.OwnerId != ownerId ||
                !string.Equals(PendingRobotFusion.CardId, cardId, System.StringComparison.Ordinal))
            {
                throw new System.InvalidOperationException("No matching Robot Fusion selection is pending.");
            }

            PendingRobotFusion = null;
        }

        public void CancelPendingRobotFusion()
        {
            PendingRobotFusion = null;
        }

        public void RestorePendingRobotFusion(PlayerId ownerId, string cardId)
        {
            PendingRobotFusion = string.IsNullOrWhiteSpace(cardId)
                ? null
                : new PendingRobotFusionState(ownerId, cardId);
        }

        public void ClearValuePopupEvents()
        {
            ValuePopupEvents.Clear();
        }

        public void RecordAttackResolution(BattleAttackResolution resolution)
        {
            LastAttackResolution = resolution;
        }

        public void ClearAttackResolution()
        {
            LastAttackResolution = null;
        }

        public void ClearCardGenerationEvents()
        {
            CardGenerationEvents.Clear();
        }

        public void ClearCardDrawEvents()
        {
            CardDrawEvents.Clear();
        }

        public void RecordCardDraw(PlayerId ownerId)
        {
            RecordCardDraw(ownerId, string.Empty);
        }

        public void RecordCardDraw(PlayerId ownerId, string sourceCardId)
        {
            CardDrawEvents.Add(new BattleCardDrawEvent(ownerId, sourceCardId));
        }

        public void RecordCardGeneration(BattleCardGenerationEvent generationEvent)
        {
            if (generationEvent != null)
            {
                CardGenerationEvents.Add(generationEvent);
            }
        }

        public void ClearResourceChangeEvents()
        {
            ResourceChangeEvents.Clear();
        }

        public void RecordResourceChange(BattleResourceChangeEvent resourceChangeEvent)
        {
            if (resourceChangeEvent != null)
            {
                ResourceChangeEvents.Add(resourceChangeEvent);
            }
        }

        public void ClearAreaSpellEffectEvents()
        {
            AreaSpellEffectEvents.Clear();
        }

        public void RecordAreaSpellEffect(BattleAreaSpellEffectEvent areaSpellEffectEvent)
        {
            if (areaSpellEffectEvent != null)
            {
                AreaSpellEffectEvents.Add(areaSpellEffectEvent);
            }
        }

        private void SynchronizeOccupantTurnContext()
        {
            SynchronizeOccupantTurnContext(PlayerBoard);
            SynchronizeOccupantTurnContext(AIBoard);
        }

        private void SynchronizeOccupantTurnContext(BoardState board)
        {
            foreach (var occupant in board.EnumerateOccupants())
            {
                occupant?.SetBattleTurnContext(ActivePlayerId, TurnNumber);
            }
        }

        private static void ResolveInvincibleTurnEnd(BoardState board, PlayerId endingPlayerId)
        {
            foreach (var occupant in board.EnumerateOccupants())
            {
                occupant?.ResolveInvincibleTurnEnd(endingPlayerId);
            }
        }
    }

    public sealed class BattleResourceChangeEvent
    {
        public BattleResourceChangeEvent(
            PlayerId ownerId,
            string sourceCardId,
            ResourceSet gained)
            : this(ownerId, sourceCardId, gained, new ResourceSet())
        {
        }

        public BattleResourceChangeEvent(
            PlayerId ownerId,
            string sourceCardId,
            ResourceSet gained,
            ResourceSet spent)
        {
            OwnerId = ownerId;
            SourceCardId = sourceCardId ?? string.Empty;
            Gained = gained == null
                ? throw new System.ArgumentNullException(nameof(gained))
                : gained.Clone();
            Spent = spent == null
                ? throw new System.ArgumentNullException(nameof(spent))
                : spent.Clone();
        }

        public PlayerId OwnerId { get; }

        public string SourceCardId { get; }

        public ResourceSet Gained { get; }

        public ResourceSet Spent { get; }
    }

    public sealed class BattleAreaSpellEffectEvent
    {
        public BattleAreaSpellEffectEvent(
            string effectId,
            string sourceCardId,
            PlayerId sourceOwnerId,
            PlayerId targetOwnerId,
            IEnumerable<TileCoord> targetCoords)
        {
            EffectId = effectId ?? string.Empty;
            SourceCardId = sourceCardId ?? string.Empty;
            SourceOwnerId = sourceOwnerId;
            TargetOwnerId = targetOwnerId;
            TargetCoords = targetCoords == null
                ? throw new System.ArgumentNullException(nameof(targetCoords))
                : new List<TileCoord>(targetCoords);
        }

        public string EffectId { get; }

        public string SourceCardId { get; }

        public PlayerId SourceOwnerId { get; }

        public PlayerId TargetOwnerId { get; }

        public IReadOnlyList<TileCoord> TargetCoords { get; }
    }

    public sealed class BattleAttackResolution
    {
        public BattleAttackResolution(
            PlayerId attackerOwnerId,
            TileCoord attackerCoord,
            string attackerRuntimeId,
            string attackerCardId,
            PlayerId declaredTargetOwnerId,
            TileCoord declaredTargetCoord,
            PlayerId resolvedTargetOwnerId,
            TileCoord resolvedTargetCoord,
            string resolvedTargetRuntimeId,
            string resolvedTargetCardId,
            bool wasHuanShuRedirected)
        {
            AttackerOwnerId = attackerOwnerId;
            AttackerCoord = attackerCoord;
            AttackerRuntimeId = attackerRuntimeId ?? string.Empty;
            AttackerCardId = attackerCardId ?? string.Empty;
            DeclaredTargetOwnerId = declaredTargetOwnerId;
            DeclaredTargetCoord = declaredTargetCoord;
            ResolvedTargetOwnerId = resolvedTargetOwnerId;
            ResolvedTargetCoord = resolvedTargetCoord;
            ResolvedTargetRuntimeId = resolvedTargetRuntimeId ?? string.Empty;
            ResolvedTargetCardId = resolvedTargetCardId ?? string.Empty;
            WasHuanShuRedirected = wasHuanShuRedirected;
        }

        public PlayerId AttackerOwnerId { get; }
        public TileCoord AttackerCoord { get; }
        public string AttackerRuntimeId { get; }
        public string AttackerCardId { get; }
        public PlayerId DeclaredTargetOwnerId { get; }
        public TileCoord DeclaredTargetCoord { get; }
        public PlayerId ResolvedTargetOwnerId { get; }
        public TileCoord ResolvedTargetCoord { get; }
        public string ResolvedTargetRuntimeId { get; }
        public string ResolvedTargetCardId { get; }
        public bool WasHuanShuRedirected { get; }
    }
}
