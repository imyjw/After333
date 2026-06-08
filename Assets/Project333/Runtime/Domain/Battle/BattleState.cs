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
            Counters = new BattleCounters();
            Result = new BattleResultState();
            Phase = PhaseType.BattleStart;
            ActivePlayerId = PlayerId.Player;
            TurnNumber = 0;
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

        public BattleCounters Counters { get; }

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
        }

        public void SetActivePlayer(PlayerId activePlayerId)
        {
            ActivePlayerId = activePlayerId;
        }

        public void SetPhase(PhaseType phase)
        {
            Phase = phase;
        }

        public void EndBattle(PlayerId winner)
        {
            Result.SetWinner(winner);
            Phase = PhaseType.Ended;
        }

        public void ClearValuePopupEvents()
        {
            ValuePopupEvents.Clear();
        }
    }
}
