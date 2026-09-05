namespace Project333.Runtime.Domain.Battle
{
    public sealed class BattleResultState
    {
        public bool HasResult { get; private set; }

        public bool HasWinner { get; private set; }

        public bool IsDraw { get; private set; }

        public PlayerId Winner { get; private set; }

        public void SetWinner(PlayerId winner)
        {
            HasResult = true;
            HasWinner = true;
            IsDraw = false;
            Winner = winner;
        }

        public void SetDraw()
        {
            HasResult = true;
            HasWinner = false;
            IsDraw = true;
            Winner = default;
        }

        public void Clear()
        {
            HasResult = false;
            HasWinner = false;
            IsDraw = false;
            Winner = default;
        }
    }
}
