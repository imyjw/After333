namespace Project333.Runtime.Domain.Battle
{
    public sealed class BattleResultState
    {
        public bool HasWinner { get; private set; }

        public PlayerId Winner { get; private set; }

        public void SetWinner(PlayerId winner)
        {
            HasWinner = true;
            Winner = winner;
        }

        public void Clear()
        {
            HasWinner = false;
            Winner = default;
        }
    }
}
