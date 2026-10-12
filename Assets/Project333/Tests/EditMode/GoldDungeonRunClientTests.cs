using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation.Draft;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class GoldDungeonRunClientTests
    {
        private const BindingFlags HiddenInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly Dictionary<PropertyInfo, object> _accountBefore = new Dictionary<PropertyInfo, object>();
        private GameObject _root;
        private DraftSceneController _controller;

        [SetUp]
        public void SetUp()
        {
            // No credentials, HTTP requests, paid run, or real challenge reservation are created.
            foreach (var property in typeof(AccountSessionState).GetProperties(BindingFlags.Public | BindingFlags.Static))
                if (property.GetSetMethod(true) != null) _accountBefore[property] = property.GetValue(null);
            SetRun("run-one");
            _root = new GameObject("GoldDungeonRunClientTest");
            _root.SetActive(false);
            _controller = _root.AddComponent<DraftSceneController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            foreach (var value in _accountBefore) value.Key.SetValue(null, value.Value);
            _accountBefore.Clear();
        }

        [Test]
        public void Status_UsesServerRunIdLimitAndRemainingCount()
        {
            var response = JsonConvert.DeserializeObject<AiChallengeStatusResponse>(
                "{\"RunId\":\"run-one\",\"RunLimit\":3,\"RemainingAttempts\":1,\"PendingAttempt\":null}");
            Assert.That(response.RunLimit, Is.EqualTo(3));
            Assert.That(response.RemainingAttempts, Is.EqualTo(1));
            Assert.That(response.IsForRun("RUN-ONE"), Is.True);
            Assert.That(response.IsForRun("run-two"), Is.False);
            SetStatus(response);
            Assert.That(Label(), Is.EqualTo("골드 던전 (1/3)"));
        }

        [Test]
        public void NewRun_NeverUsesPreviousRunCountBeforeItsServerStatusArrives()
        {
            SetStatus(new AiChallengeStatusResponse { RunId = "run-one", RemainingAttempts = 0 });
            Assert.That(Label(), Is.EqualTo("골드 던전 (0/3)"));
            SetRun("run-two");
            Assert.That(Label(), Is.EqualTo("골드 던전 확인 중..."));
            SetStatus(new AiChallengeStatusResponse { RunId = "run-two", RemainingAttempts = 3 });
            Assert.That(Label(), Is.EqualTo("골드 던전 (3/3)"));
        }

        [Test]
        public void PreviousRunWithUnusedAttempts_DoesNotOpenNewRunChallenge()
        {
            SetStatus(new AiChallengeStatusResponse { RunId = "run-one", RemainingAttempts = 3,
                PendingAttempt = new AiChallengeAttemptResponse { RunId = "run-one", Status = "reserved" } });
            SetRun("run-two");
            Assert.That(typeof(DraftSceneController).GetProperty("CurrentRunAiChallengeStatus", HiddenInstance)
                .GetValue(_controller), Is.Null);
            Assert.That(Label(), Is.EqualTo("골드 던전 확인 중..."));
        }

        [Test]
        public void NoActiveRun_CannotUseAnAccountWideCounterOrReservation()
        {
            var response = JsonConvert.DeserializeObject<AiChallengeStatusResponse>(
                "{\"RunId\":null,\"RunLimit\":3,\"RemainingAttempts\":0,\"PendingAttempt\":null}");
            Assert.That(response.IsForRun(null), Is.False);
            Assert.That(response.IsForRun(string.Empty), Is.False);
            SetRun(string.Empty);
            SetStatus(response);
            Assert.That(Label(), Is.EqualTo("골드 던전 확인 중..."));
        }

        [TestCase(-1, 0)]
        [TestCase(0, 0)]
        [TestCase(3, 3)]
        [TestCase(5, 3)]
        public void Button_ClampsUnexpectedServerCountsWithoutGrantingAttempts(int remaining, int shown)
        {
            SetStatus(new AiChallengeStatusResponse { RunId = "run-one", RemainingAttempts = remaining });
            Assert.That(Label(), Is.EqualTo($"골드 던전 ({shown}/3)"));
        }

        private static void SetRun(string runId) => typeof(AccountSessionState)
            .GetProperty(nameof(AccountSessionState.ActiveRunId), BindingFlags.Public | BindingFlags.Static)
            .SetValue(null, runId);
        private void SetStatus(AiChallengeStatusResponse status) => typeof(DraftSceneController)
            .GetField("_aiChallengeStatus", HiddenInstance).SetValue(_controller, status);
        private string Label() => (string)typeof(DraftSceneController)
            .GetMethod("BuildAiChallengeButtonLabel", HiddenInstance).Invoke(_controller, null);
    }
}
