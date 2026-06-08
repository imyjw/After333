using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Application.Services
{
    public interface IDeckShuffler
    {
        void Shuffle(DeckState deckState);
    }
}
