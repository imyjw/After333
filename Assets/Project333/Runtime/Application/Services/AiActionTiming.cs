using System;

namespace Project333.Runtime.Application.Services
{
    public static class AiActionTiming
    {
        public const double ActionPauseSeconds = 1.0;
        public const double CardPlayMinimumSeconds = 3.0;

        public static double CalculatePostActionPauseSeconds(bool isCardPlay, double animationSeconds)
        {
            return isCardPlay
                ? Math.Max(ActionPauseSeconds, CardPlayMinimumSeconds - Math.Max(0, animationSeconds))
                : ActionPauseSeconds;
        }
    }
}
