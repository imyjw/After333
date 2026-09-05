using System;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Domain.Battle
{
    public sealed class PlayerState
    {
        public const int BaseMaxHandSize = 10;
        public const int AbsoluteMaxHandSize = 13;

        public PlayerState(
            PlayerId id,
            ResourceSet resources,
            DeckState deck,
            HandState hand,
            DiscardState discard,
            MasterState master)
        {
            Id = id;
            Resources = resources;
            Deck = deck;
            Hand = hand;
            Discard = discard;
            Master = master;
        }

        public PlayerId Id { get; }

        public ResourceSet Resources { get; }

        public DeckState Deck { get; }

        public HandState Hand { get; }

        public DiscardState Discard { get; }

        public MasterState Master { get; }

        public int FailedDrawCount { get; private set; }

        public bool HasUsedMulligan { get; private set; }

        public int MaxHandSizeBonus { get; private set; }

        public int MaxHandSize => Math.Min(AbsoluteMaxHandSize, BaseMaxHandSize + MaxHandSizeBonus);

        public void MarkMulliganUsed()
        {
            HasUsedMulligan = true;
        }

        public void IncreaseMaxHandSizeBonus(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            MaxHandSizeBonus = Math.Min(AbsoluteMaxHandSize - BaseMaxHandSize, MaxHandSizeBonus + amount);
        }

        public int IncrementFailedDrawCount()
        {
            FailedDrawCount += 1;
            return FailedDrawCount;
        }

        public void ResetFailedDrawCount()
        {
            FailedDrawCount = 0;
        }

        public void RestoreRuntimeState(
            int failedDrawCount,
            bool hasUsedMulligan,
            int maxHandSizeBonus)
        {
            FailedDrawCount = Math.Max(0, failedDrawCount);
            HasUsedMulligan = hasUsedMulligan;
            MaxHandSizeBonus = Math.Min(
                AbsoluteMaxHandSize - BaseMaxHandSize,
                Math.Max(0, maxHandSizeBonus));
        }
    }
}
