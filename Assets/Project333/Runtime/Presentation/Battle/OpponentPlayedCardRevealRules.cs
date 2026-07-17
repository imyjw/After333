using Project333.Runtime.Application.Online;
using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Presentation.Battle
{
    public static class OpponentPlayedCardRevealRules
    {
        public static bool ShouldReveal(BattleEventDto battleEvent)
        {
            if (battleEvent == null ||
                battleEvent.SourceOwnerId != PlayerId.AI ||
                string.IsNullOrWhiteSpace(battleEvent.CardId))
            {
                return false;
            }

            return battleEvent.EventType == BattleEventType.CardPlayed ||
                   battleEvent.EventType == BattleEventType.SpellCast;
        }

        public static bool IsBoardCardPlay(BattleEventDto battleEvent)
        {
            return ShouldReveal(battleEvent) &&
                   battleEvent.EventType == BattleEventType.CardPlayed;
        }

        public static float ResolveDisplayDuration(
            bool hasWaitingCards,
            float normalDisplaySeconds,
            float queuedDisplaySeconds)
        {
            var normal = normalDisplaySeconds < 0f ? 0f : normalDisplaySeconds;
            var queued = queuedDisplaySeconds < 0f ? 0f : queuedDisplaySeconds;
            return hasWaitingCards ? queued : normal;
        }
    }
}
