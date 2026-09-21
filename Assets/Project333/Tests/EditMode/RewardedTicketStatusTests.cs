using System;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Ads;
using Project333.Runtime.Presentation.Ads;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class RewardedTicketStatusTests
    {
        private static readonly Type PendingType = typeof(AccountSessionState).Assembly.GetType(
            "Project333.Runtime.Application.Accounts.PendingRewardedAd", true);
        private GameObject _root;
        private RewardedTicketController _controller;
        private Button _button;
        private Text _status;
        private FakeAd _ad;
        private string _owner, _attempt, _url, _originalAccount, _originalToken;
        private int _originalTickets;

        [SetUp]
        public void SetUp()
        {
            _originalAccount = AccountSessionState.AccountId;
            _originalToken = AccountSessionState.SessionToken;
            _originalTickets = AccountSessionState.Tickets;
            _owner = Guid.NewGuid().ToString("D");
            _attempt = Guid.NewGuid().ToString("D");
            SetAccountProperty("AccountId", _owner);
            SetAccountProperty("SessionToken", "synthetic-status-test-session");

            // Keep the hierarchy inactive: no SDK initialization, login, HTTP, or scene loading.
            _root = new GameObject("RewardedTicketStatusTest", typeof(RectTransform));
            _root.SetActive(false);
            _controller = _root.AddComponent<RewardedTicketController>();
            _button = Child("Button", typeof(Button)).GetComponent<Button>();
            var label = Child("Label", typeof(Text)).GetComponent<Text>();
            _status = Child("Status", typeof(Text)).GetComponent<Text>();
            _ad = new FakeAd();
            SetField("_watchAdButton", _button);
            SetField("_watchAdButtonLabel", label);
            SetField("_rewardedAdStatusText", _status);
            SetField("_levelPlayAppKey", "synthetic-app-key");
            SetField("_rewardedAdUnitId", "synthetic-ad-unit");
            SetField("_accountServerUrl", "http://synthetic.invalid");
            SetField("_adService", _ad);
            _url = (string)typeof(RewardedTicketController).GetProperty("AccountServerUrl",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_controller);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (_url != null) Pending("Clear", _url, _owner, _attempt);
                if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            }
            finally
            {
                SetAccountProperty("AccountId", _originalAccount);
                SetAccountProperty("SessionToken", _originalToken);
            }
        }

        [TestCase(false, "현재 제공 가능한 광고가 없습니다. 60초 후 자동으로 다시 시도합니다.")]
        [TestCase(false, "광고를 다시 불러오는 중입니다.")]
        [TestCase(true, "광고 준비 완료.")]
        public void ReplayableSavedAttempt_ShowsCurrentSdkStatus(bool ready, string message)
        {
            RememberReplayable();
            _ad.Ready = ready;
            _ad.Message = message;

            _controller.RefreshUi();

            Assert.That(_button.interactable, Is.EqualTo(ready));
            Assert.That(_status.text, Is.EqualTo(message),
                "A retained attempt must not hide the actual reason that the next ad is unavailable.");
            Assert.That(_status.gameObject.activeSelf, Is.True);
            AssertRetainedReplayable();
        }

        [TestCase("오늘 받을 수 있는 광고 티켓을 모두 받았습니다.", true, "광고 준비 완료.")]
        [TestCase("다음 광고 보상까지 30초 남았습니다.", true, "광고 준비 완료.")]
        [TestCase("서버 응답 시간이 초과되었습니다.", false, "광고를 다시 불러오는 중입니다.")]
        public void ReplayableSavedAttempt_PreservesCurrentEligibilityOrRequestError(
            string explicitMessage, bool ready, string currentMessage)
        {
            RememberReplayable();
            SetField("_lastStatusMessage", explicitMessage);
            _ad.Ready = ready;
            _ad.Message = currentMessage;

            _controller.RefreshUi();

            Assert.That(_status.text, Is.EqualTo(explicitMessage));
            Assert.That(_button.interactable, Is.EqualTo(ready));
            AssertRetainedReplayable();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BlockingSavedAttempt_KeepsAutomaticRecoverySilent(bool sdkReportedReward)
        {
            Pending("Remember", _url, _owner, _attempt);
            if (sdkReportedReward) Pending("MarkRewarded", _url, _owner, _attempt);
            _ad.Ready = true;
            _ad.Message = "광고 준비 완료.";
            SetField("_lastStatusMessage", "이전 재생 안내");

            _controller.RefreshUi();

            Assert.That(_button.interactable, Is.False);
            Assert.That(_status.text, Is.Empty);
            Assert.That(_status.gameObject.activeSelf, Is.False);
            Assert.That((bool)Pending("IsBlocking", _url, _owner), Is.True);
            Assert.That(AccountSessionState.Tickets, Is.EqualTo(_originalTickets));
        }

        [Test]
        public void RepeatedInterruptedPresentations_TrackNoFillLoadingAndReadinessWithoutLosingAttempt()
        {
            // Each restart's matching server-pending response releases presentation blocking.
            // The durable attempt intentionally remains available for a late verified server reward.
            for (var cycle = 0; cycle < 3; cycle++)
            {
                Pending("Remember", _url, _owner, _attempt);
                Pending("MarkClosed", _url, _owner, _attempt, true);
                // A fresh process or the new Watch flow has no stored playback message.
                SetField("_lastStatusMessage", string.Empty);
                foreach (var current in new[]
                {
                    "현재 제공 가능한 광고가 없습니다. 60초 후 자동으로 다시 시도합니다.",
                    "광고를 다시 불러오는 중입니다.",
                    "광고 준비 완료."
                })
                {
                    _ad.Ready = current == "광고 준비 완료.";
                    _ad.Message = current;
                    _controller.RefreshUi();
                    Assert.That(_status.text, Is.EqualTo(current), "Interrupted presentation cycle " + cycle);
                    Assert.That(_button.interactable, Is.EqualTo(_ad.Ready));
                    AssertRetainedReplayable();
                }
            }
        }

        [Test]
        public void ShowingAd_PreservesPlaybackStatusAndDisablesButton()
        {
            Pending("Remember", _url, _owner, _attempt);
            _ad.Showing = true;
            _ad.Message = "광고 재생 중입니다.";
            SetField("_lastStatusMessage", "광고 재생 중입니다.");

            _controller.RefreshUi();

            Assert.That(_button.interactable, Is.False);
            Assert.That(_status.text, Is.EqualTo("광고 재생 중입니다."));
        }

        [Test]
        public void NoSavedAttempt_PreservesFinalServerRewardNotice()
        {
            _ad.Ready = true;
            _ad.Message = "광고 준비 완료.";
            SetField("_lastStatusMessage", "티켓 +1 지급 완료!");

            _controller.RefreshUi();

            Assert.That(_button.interactable, Is.True);
            Assert.That(_status.text, Is.EqualTo("티켓 +1 지급 완료!"));
        }

        private void RememberReplayable()
        {
            Pending("Remember", _url, _owner, _attempt);
            Pending("MarkClosed", _url, _owner, _attempt, true);
        }

        private void AssertRetainedReplayable()
        {
            var record = Pending("Load", _url, _owner);
            Assert.That(record, Is.Not.Null);
            Assert.That(record.GetType().GetField("AttemptId").GetValue(record), Is.EqualTo(_attempt));
            Assert.That((bool)Pending("IsBlocking", _url, _owner), Is.False);
            Assert.That(AccountSessionState.Tickets, Is.EqualTo(_originalTickets),
                "Displaying SDK availability must never grant a local reward.");
        }

        private GameObject Child(string name, Type component)
        {
            var child = new GameObject(name, typeof(RectTransform), component);
            child.transform.SetParent(_root.transform, false);
            return child;
        }

        private void SetField(string name, object value) => typeof(RewardedTicketController)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_controller, value);

        private static object Pending(string name, params object[] args) => PendingType
            .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);

        private static void SetAccountProperty(string name, string value) => typeof(AccountSessionState)
            .GetProperty(name, BindingFlags.Static | BindingFlags.Public).GetSetMethod(true).Invoke(null, new object[] { value });

        private sealed class FakeAd : IRewardedAdService
        {
            public event Action StateChanged { add { } remove { } }
            public event Action Rewarded { add { } remove { } }
            public event Action Closed { add { } remove { } }
            public bool Ready;
            public bool Showing;
            public string Message = string.Empty;
            public bool IsReady => Ready && !Showing;
            public bool IsShowing => Showing;
            public string StatusMessage => Message;
            public void Initialize(string appKey, string providerUserId, string rewardedAdUnitId) =>
                throw new InvalidOperationException("Status tests must not initialize an SDK.");
            public bool TryShow(string placementName, string dynamicUserId, out string errorMessage) =>
                throw new InvalidOperationException("Status tests must not show an ad.");
        }
    }
}