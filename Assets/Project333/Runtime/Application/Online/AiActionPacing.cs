using System.Collections.Generic;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Application.Online
{
    public static class AiActionPacing
    {
        public static double GetPostActionPauseSeconds(bool useServerAiOpponent,
            IReadOnlyList<BattleEventDto> events, double animationSeconds)
        {
            if (!useServerAiOpponent || events == null) return 0;
            var hasAction = false;
            var hasCardPlay = false;
            foreach (var battleEvent in events)
            {
                if (battleEvent == null) continue;
                if (battleEvent.EventType == BattleEventType.BattleEnded ||
                    battleEvent.EventType == BattleEventType.TurnEnded) return 0;
                if (battleEvent.SourceOwnerId != PlayerId.AI) continue;
                switch (battleEvent.EventType)
                {
                    case BattleEventType.CardPlayed:
                    case BattleEventType.SpellCast:
                        hasCardPlay = true;
                        hasAction = true;
                        break;
                    case BattleEventType.AttackStarted:
                    case BattleEventType.OccupantMoved:
                    case BattleEventType.RobotFusionResolved:
                        hasAction = true;
                        break;
                }
            }
            return hasAction ? AiActionTiming.CalculatePostActionPauseSeconds(hasCardPlay, animationSeconds) : 0;
        }
    }
}
