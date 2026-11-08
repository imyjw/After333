using System;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Online;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class OnlineTurnDeadlineTests
    {
        private GameObject _host;
        private OnlineBattleConnectionTester _tester;
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("OfflineTurnDeadlineTest");
            _host.SetActive(false); // No connection, account, gameplay or lifecycle startup.
            _tester = _host.AddComponent<OnlineBattleConnectionTester>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_host);

        [TestCase(PhaseType.Mulligan, 33)]
        [TestCase(PhaseType.Main, 73)]
        public void OrdinaryTimerUsesTheAuthoritativeDeadline(PhaseType phase, int duration)
        {
            var view = CreateView(duration, 0, phase);
            Assert.That(Remaining(view), Is.EqualTo(duration * 1000L).Within(500));
        }

        [TestCase(3)]
        [TestCase(7)]
        [TestCase(30)]
        public void AiPresentationCreditMustNotBecomePrematureProcessingTime(int creditSeconds)
        {
            var view = CreateView(73, creditSeconds);
            var remaining = Remaining(view);
            Assert.That(remaining, Is.EqualTo((73L + creditSeconds) * 1000L).Within(500),
                "Clamping to the nominal 73 seconds discards the AI presentation credit and expires the UI early.");
            var displayedExpiry = view.ServerUnixTimeMilliseconds + remaining;
            Assert.That(view.TurnTimerDeadlineUnixTimeMilliseconds - displayedExpiry, Is.InRange(0L, 500L));
        }

        [TestCase(0)]
        [TestCase(5)]
        [TestCase(15)]
        public void NewSnapshotOrReconnectKeepsTheRemainingCredit(int serverElapsedSeconds)
        {
            var view = CreateView(73, 7);
            view.ServerUnixTimeMilliseconds += serverElapsedSeconds * 1000L;
            SyncClock(view);
            Assert.That(Remaining(view), Is.EqualTo((80L - serverElapsedSeconds) * 1000L).Within(500));
        }

        [Test]
        public void ElapsedTimeAfterReceiptIsCountedAndZeroOccursOnlyAtTheServerDeadline()
        {
            var view = CreateView(73, 7);
            var initial = Remaining(view);
            SetOffset(75_000);
            var nearExpiry = Remaining(view);
            Assert.That(initial - nearExpiry, Is.EqualTo(75_000L).Within(500));
            Assert.That(nearExpiry, Is.InRange(4_500L, 5_000L));
            SetOffset(81_000);
            Assert.That(Remaining(view), Is.Zero);
        }

        [Test]
        public void ServerClockOffsetDoesNotRemovePresentationCredit()
        {
            var view = CreateView(73, 7);
            view.ServerUnixTimeMilliseconds += 6 * 60 * 60 * 1000L;
            view.TurnTimerDeadlineUnixTimeMilliseconds += 6 * 60 * 60 * 1000L;
            SyncClock(view);
            Assert.That(Remaining(view), Is.EqualTo(80_000L).Within(500));
        }

        [Test]
        public void BackwardLocalClockGuardUsesTheServerAnnouncedRemainingTime()
        {
            var view = CreateView(73, 7);
            SetOffset(-3_600_000);
            Assert.That(Remaining(view), Is.EqualTo(80_000L),
                "Keep the existing clock-discontinuity guard, without losing a valid server extension.");
        }

        [Test]
        public void MissingDurationMetadataDoesNotOverrideAValidDeadline()
        {
            var view = CreateView(73, 7);
            view.TurnTimerDurationSeconds = 0;
            Assert.That(Remaining(view), Is.EqualTo(80_000L).Within(500));
        }

        [Test]
        public void MissingOrExpiredDeadlineReturnsZero()
        {
            Assert.That(Remaining(null), Is.Zero);
            Assert.That(Remaining(new BattleStateViewDto()), Is.Zero);
            var view = CreateView(73, 0);
            view.TurnTimerDeadlineUnixTimeMilliseconds = view.ServerUnixTimeMilliseconds - 1;
            Assert.That(Remaining(view), Is.Zero);
        }

        private BattleStateViewDto CreateView(int duration, int extraSeconds, PhaseType phase = PhaseType.Main)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var view = new BattleStateViewDto
            {
                Phase = phase, TurnNumber = 10, ActivePlayerId = PlayerId.Player,
                TurnTimerVersion = 10, TurnTimerDurationSeconds = duration,
                ServerUnixTimeMilliseconds = now,
                TurnTimerDeadlineUnixTimeMilliseconds = now + (duration + extraSeconds) * 1000L
            };
            SyncClock(view);
            return view;
        }
        private void SyncClock(BattleStateViewDto view) => typeof(OnlineBattleConnectionTester)
            .GetMethod("UpdateServerClockOffset", Hidden).Invoke(_tester,
                new object[] { view, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        private void SetOffset(long offset) => typeof(OnlineBattleConnectionTester)
            .GetField("_serverClockOffsetMilliseconds", Hidden).SetValue(_tester, offset);
        private long Remaining(BattleStateViewDto view) => (long)typeof(OnlineBattleConnectionTester)
            .GetMethod("CalculateTurnTimerRemainingMilliseconds", Hidden).Invoke(_tester, new object[] { view });
    }
}
