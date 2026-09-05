using System;
using NUnit.Framework;
using Project333.Runtime.Application.Ads;

namespace Project333.Tests.EditMode
{
    public sealed class RewardedAdServiceRegistryTests
    {
        [TearDown]
        public void TearDown()
        {
            RewardedAdServiceRegistry.RegisterFactory(() => new UnavailableRewardedAdService());
        }

        [Test]
        public void Shared_ReusesTheRegisteredProviderWithinOneRuntimeSession()
        {
            var createCount = 0;
            RewardedAdServiceRegistry.RegisterFactory(() =>
            {
                createCount++;
                return new FakeRewardedAdService();
            });

            var first = RewardedAdServiceRegistry.Shared;
            var second = RewardedAdServiceRegistry.Shared;

            Assert.That(second, Is.SameAs(first));
            Assert.That(createCount, Is.EqualTo(1));
        }

        [Test]
        public void RegisterFactory_DiscardsTheProviderFromThePreviousRuntimeSession()
        {
            RewardedAdServiceRegistry.RegisterFactory(() => new FakeRewardedAdService());
            var previous = RewardedAdServiceRegistry.Shared;

            RewardedAdServiceRegistry.RegisterFactory(() => new FakeRewardedAdService());
            var current = RewardedAdServiceRegistry.Shared;

            Assert.That(current, Is.Not.SameAs(previous));
        }

        private sealed class FakeRewardedAdService : IRewardedAdService
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

            public bool IsReady => true;
            public bool IsShowing => false;
            public string StatusMessage => string.Empty;

            public void Initialize(string appKey, string providerUserId, string rewardedAdUnitId)
            {
            }

            public bool TryShow(string placementName, string dynamicUserId, out string errorMessage)
            {
                errorMessage = string.Empty;
                return true;
            }
        }
    }
}
