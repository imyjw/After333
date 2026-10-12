using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation.Startup;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class GameStartRewardRecoveryTests
    {
        private const BindingFlags HiddenInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags HiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private readonly Dictionary<FieldInfo, object> _previousSession = new Dictionary<FieldInfo, object>();
        private readonly List<(string Server, string Account, string Run)> _markers = new List<(string, string, string)>();
        private GameObject _root;
        private GameStartSceneController _controller;
        private string _server;
        private string _account;
        private string _run;

        [SetUp]
        public void SetUp()
        {
            _account = Guid.NewGuid().ToString("D");
            _run = Guid.NewGuid().ToString("D");
            SetSession("SessionToken", "reward-presentation-test-token");
            SetSession("AccountId", _account);
            SetSession("LatestRunId", _run);
            SetSession("LatestRunStatus", "completed");
            SetSession("LatestRunRewardClaimedAt", "2026-10-02T00:00:00+09:00");
            SetSession("Tickets", 0);
            SetSession("ActiveRunId", string.Empty);
            SetSession("ActiveRunStatus", string.Empty);
            SetSession("ActiveDeckId", string.Empty);
            _root = new GameObject("RewardRecoveryStartController");
            _root.SetActive(false); // No startup/auth/network callbacks are invoked by this fixture.
            _controller = _root.AddComponent<GameStartSceneController>();
            SetController("_useServerAccount", true);
            SetController("_ticketCost", 3);
            _server = (string)typeof(GameStartSceneController).GetProperty("AccountServerUrl", HiddenInstance)
                .GetValue(_controller);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var marker in _markers)
                PendingRunRewardPresentation.Clear(marker.Server, marker.Account, marker.Run);
            _markers.Clear();
            UnityEngine.Object.DestroyImmediate(_root);
            foreach (var value in _previousSession) value.Key.SetValue(null, value.Value);
            _previousSession.Clear();
        }

        [Test]
        public void ClaimedSameRunPendingConfirmationRemainsReachableWithNoTickets()
        {
            Remember(_server, _account, _run);

            Assert.IsTrue(Invoke<bool>("CanStartGame"));
            Assert.AreEqual("보상 확인하기", Invoke<string>("BuildStartButtonLabel"));
            StringAssert.Contains("보상 결과", Invoke<string>("BuildStatusText"));
            PendingRunRewardPresentation.Clear(_server, _account, _run);
            Assert.IsFalse(Invoke<bool>("CanStartGame"));
            StringAssert.StartsWith("게임 시작", Invoke<string>("BuildStartButtonLabel"));
        }

        [Test]
        public void AlreadyClaimedRunWithoutMarkerUsesExistingNewRunGate()
        {
            Assert.IsFalse(Invoke<bool>("CanStartGame"));
            StringAssert.StartsWith("게임 시작", Invoke<string>("BuildStartButtonLabel"));
        }

        [Test]
        public void PendingDifferentRunDoesNotResumeCurrentRunReward()
        {
            Remember(_server, _account, Guid.NewGuid().ToString("D"));

            Assert.IsFalse(Invoke<bool>("CanStartGame"));
            StringAssert.StartsWith("게임 시작", Invoke<string>("BuildStartButtonLabel"));
        }

        [Test]
        public void OtherAccountMarkerCannotOpenCurrentAccountResult()
        {
            Remember(_server, Guid.NewGuid().ToString("D"), _run);

            Assert.IsFalse(Invoke<bool>("CanStartGame"));
            StringAssert.StartsWith("게임 시작", Invoke<string>("BuildStartButtonLabel"));
        }

        [Test]
        public void OtherEndpointMarkerCannotOpenCurrentServerResult()
        {
            Remember(_server + "/other", _account, _run);

            Assert.IsFalse(Invoke<bool>("CanStartGame"));
            StringAssert.StartsWith("게임 시작", Invoke<string>("BuildStartButtonLabel"));
        }

        [Test]
        public void NonCompletedRunDoesNotResumeARewardEvenWithMatchingMarker()
        {
            Remember(_server, _account, _run);
            SetSession("LatestRunStatus", "drafting");

            Assert.IsFalse(Invoke<bool>("CanStartGame"));
            StringAssert.StartsWith("게임 시작", Invoke<string>("BuildStartButtonLabel"));
        }

        [Test]
        public void UnauthenticatedAccountCannotResumeResultFromMarker()
        {
            Remember(_server, _account, _run);
            SetSession("SessionToken", string.Empty);

            Assert.IsFalse(Invoke<bool>("CanStartGame"));
            StringAssert.StartsWith("게임 시작", Invoke<string>("BuildStartButtonLabel"));
        }

        [Test]
        public void ResumableActiveRunKeepsPriorityOverAnOldRewardDisplay()
        {
            Remember(_server, _account, _run);
            SetSession("ActiveRunId", Guid.NewGuid().ToString("D"));
            SetSession("ActiveRunStatus", "ready");
            SetSession("ActiveDeckId", Guid.NewGuid().ToString("D"));

            Assert.IsTrue(Invoke<bool>("CanStartGame"));
            Assert.AreEqual("이어하기", Invoke<string>("BuildStartButtonLabel"));
        }

        [Test]
        public void UntouchedUnclaimedRunRetainsExistingRewardRouteWithNoMarker()
        {
            SetSession("LatestRunRewardClaimedAt", string.Empty);

            Assert.IsTrue(Invoke<bool>("CanStartGame"));
            Assert.AreEqual("보상 수령하기", Invoke<string>("BuildStartButtonLabel"));
        }

        private void Remember(string server, string owner, string run)
        {
            PendingRunRewardPresentation.Remember(server, owner, run);
            _markers.Add((server, owner, run));
        }

        private void SetSession(string property, object value)
        {
            var field = typeof(AccountSessionState).GetField("<" + property + ">k__BackingField", HiddenStatic);
            Assert.IsNotNull(field, property);
            if (!_previousSession.ContainsKey(field)) _previousSession[field] = field.GetValue(null);
            field.SetValue(null, value);
        }

        private void SetController(string name, object value)
        {
            typeof(GameStartSceneController).GetField(name, HiddenInstance).SetValue(_controller, value);
        }

        private T Invoke<T>(string name)
        {
            return (T)typeof(GameStartSceneController).GetMethod(name, HiddenInstance).Invoke(_controller, null);
        }
    }
}
