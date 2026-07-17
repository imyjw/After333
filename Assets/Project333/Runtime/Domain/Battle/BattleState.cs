using System.Collections.Generic;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Effects;

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
            CardGenerationEvents = new List<BattleCardGenerationEvent>();
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

        public List<BattleCardGenerationEvent> CardGenerationEvents { get; }

        public BattleCounters Counters { get; }

        public PendingRobotFusionState PendingRobotFusion { get; private set; }

        public bool IsEnded => Result.HasWinner;

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

        public void ClearCardGenerationEvents()
        {
            CardGenerationEvents.Clear();
        }

        public void RecordCardGeneration(BattleCardGenerationEvent generationEvent)
        {
            if (generationEvent != null)
            {
                CardGenerationEvents.Add(generationEvent);
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
}
