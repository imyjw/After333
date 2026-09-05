namespace Project333.Runtime.Domain.Battle
{
    public sealed class BattleCounters
    {
        public int ActionSequence { get; private set; }

        public int NextActionSequence()
        {
            ActionSequence += 1;
            return ActionSequence;
        }

        public void Reset()
        {
            ActionSequence = 0;
        }

        public void Restore(int actionSequence)
        {
            ActionSequence = actionSequence < 0 ? 0 : actionSequence;
        }
    }
}
