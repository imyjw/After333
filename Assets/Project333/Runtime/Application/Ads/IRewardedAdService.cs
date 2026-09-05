using System;

namespace Project333.Runtime.Application.Ads
{
    public interface IRewardedAdService
    {
        event Action StateChanged;
        event Action Rewarded;
        event Action Closed;

        bool IsReady { get; }
        bool IsShowing { get; }
        string StatusMessage { get; }

        void Initialize(string appKey, string providerUserId, string rewardedAdUnitId);
        bool TryShow(string placementName, string dynamicUserId, out string errorMessage);
    }

    public static class RewardedAdServiceRegistry
    {
        private static Func<IRewardedAdService> s_factory = () => new UnavailableRewardedAdService();
        private static IRewardedAdService s_shared;

        public static IRewardedAdService Shared => s_shared ??= s_factory();

        public static void RegisterFactory(Func<IRewardedAdService> factory)
        {
            s_factory = factory ?? throw new ArgumentNullException(nameof(factory));
            s_shared = null;
        }
    }

    public sealed class UnavailableRewardedAdService : IRewardedAdService
    {
        public event Action StateChanged
        {
            add { }
            remove { }
        }

        public event Action Rewarded
        {
            add { }
            remove { }
        }

        public event Action Closed
        {
            add { }
            remove { }
        }

        public bool IsReady => false;
        public bool IsShowing => false;
        public string StatusMessage => "LevelPlay 광고 패키지가 준비되지 않았습니다.";

        public void Initialize(string appKey, string providerUserId, string rewardedAdUnitId)
        {
        }

        public bool TryShow(string placementName, string dynamicUserId, out string errorMessage)
        {
            errorMessage = StatusMessage;
            return false;
        }
    }
}
