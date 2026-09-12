using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation.Draft;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class DraftRunRewardAuthorityTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private readonly Dictionary<PropertyInfo, object> _savedAccountState =
            new Dictionary<PropertyInfo, object>();
        private GameObject _root;
        private DraftSceneController _controller;
        private Button _claimButton;
        private Text _claimLabel;

        [SetUp]
        public void SetUp()
        {
            // Keep account changes in memory: no login, credential storage, or HTTP requests.
            foreach (var property in typeof(AccountSessionState).GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                if (property.GetSetMethod(nonPublic: true) != null)
                {
                    _savedAccountState[property] = property.GetValue(null);
                }
            }

            SetAccountProperty("SessionToken", "reward-authority-test-session");
            SetAccountProperty("ActiveRunId", "current-run");
            SetAccountProperty("ActiveDeckId", "current-deck");
            SetAccountProperty("LatestRunId", "current-run");
            SetAccountProperty("LatestDeckId", "current-deck");
            SetAccountProperty("LatestRunStatus", "in_progress");
            SetAccountProperty("LatestRunWins", 0);
            SetAccountProperty("LatestRunLosses", 0);
            SetAccountProperty("LatestRunRewardClaimedAt", string.Empty);
            DraftRunSessionState.ResetSessionState();
            DraftRunSessionState.SetServerRunDeckMetadata("current-run", "current-deck");

            // Inactive hierarchy avoids Awake/Start and automatic account polling.
            _root = new GameObject("DraftRewardAuthorityTest", typeof(RectTransform));
            _root.SetActive(false);
            _controller = _root.AddComponent<DraftSceneController>();
            var buttonObject = new GameObject("Claim", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(_root.transform, false);
            _claimButton = buttonObject.GetComponent<Button>();
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            _claimLabel = labelObject.GetComponent<Text>();
            SetControllerField("_claimRewardsButton", _claimButton);
            SetControllerField("_claimRewardsButtonLabel", _claimLabel);
            SetControllerField("_claimRewardsButtonReadyLabel", "CLAIM REWARDS");
            SetControllerField("_claimRewardsButtonClaimedLabel", "REWARDS CLAIMED");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            DraftRunSessionState.ResetSessionState();
            foreach (var pair in _savedAccountState)
            {
                pair.Key.SetValue(null, pair.Value);
            }

            _savedAccountState.Clear();
        }

        [TestCase(33, 0)]
        [TestCase(0, 3)]
        public void LocalCompletion_WhenServerRunIsActive_OnlyOffersServerResultCheck(int localWins, int localLosses)
        {
            DraftRunSessionState.ApplyServerRunRecord(localWins, localLosses);

            RefreshClaimButton();

            Assert.That(DraftRunSessionState.HasRunEnded, Is.True);
            Assert.That(AccountSessionState.HasUnclaimedLatestRunRewards, Is.False);
            Assert.That(InvokeStatic<bool>("IsLatestServerRunCompleted"), Is.False);
            Assert.That(_claimButton.gameObject.activeSelf, Is.True);
            Assert.That(_claimButton.interactable, Is.True, "The player can retry the read-only server check.");
            Assert.That(_claimLabel.text, Is.EqualTo("서버 결과 확인"));
            Assert.That(InvokeStatic<string>("BuildRunCompleteStatusText"), Does.Contain("Rewards are not available yet"));
        }

        [TestCase(33, 0, "33 Wins")]
        [TestCase(4, 3, "3 Losses")]
        public void ServerCompletion_EnablesRewardsAndUsesServerOutcome(int serverWins, int serverLosses, string expectedOutcome)
        {
            // Conflicting local totals must not select the reward-completion message.
            DraftRunSessionState.ApplyServerRunRecord(serverWins == 33 ? 0 : 33, serverWins == 33 ? 3 : 0);
            SetAccountProperty("LatestRunStatus", "completed");
            SetAccountProperty("LatestRunWins", serverWins);
            SetAccountProperty("LatestRunLosses", serverLosses);

            RefreshClaimButton();

            Assert.That(AccountSessionState.HasUnclaimedLatestRunRewards, Is.True);
            Assert.That(InvokeStatic<bool>("IsLatestServerRunCompleted"), Is.True);
            Assert.That(_claimButton.interactable, Is.True);
            Assert.That(_claimLabel.text, Is.EqualTo("CLAIM REWARDS"));
            Assert.That(InvokeStatic<string>("BuildRunCompleteStatusText"), Does.Contain(expectedOutcome));
            Assert.That(InvokeStatic<string>("BuildRunCompleteStatusText"), Does.Contain("Rewards are ready"));
        }

        [Test]
        public void ClaimedServerRun_WithLocalCompletion_DoesNotAdvertiseUnclaimedRewards()
        {
            DraftRunSessionState.ApplyServerRunRecord(33, 0);
            SetAccountProperty("LatestRunStatus", "completed");
            SetAccountProperty("LatestRunWins", 33);
            SetAccountProperty("LatestRunRewardClaimedAt", "2026-09-09T00:00:00Z");

            RefreshClaimButton();

            Assert.That(AccountSessionState.HasUnclaimedLatestRunRewards, Is.False);
            Assert.That(InvokeStatic<bool>("IsLatestServerRunCompleted"), Is.False);
            Assert.That(_claimLabel.text, Is.EqualTo("REWARDS CLAIMED"));
            Assert.That(InvokeStatic<string>("BuildRunCompleteStatusText"), Is.EqualTo("Run Complete. Rewards claimed."));
        }

        [Test]
        public void LocalCompletion_WithoutAuthenticatedAccount_DoesNotEnableServerRewards()
        {
            DraftRunSessionState.ApplyServerRunRecord(33, 0);
            SetAccountProperty("SessionToken", string.Empty);

            RefreshClaimButton();

            Assert.That(InvokeStatic<bool>("IsLatestServerRunCompleted"), Is.False);
            Assert.That(_claimLabel.text, Is.EqualTo("서버 결과 확인"));
            Assert.That(InvokeStatic<string>("BuildRunCompleteStatusText"), Does.Contain("Rewards are not available yet"));
        }

        private static void SetAccountProperty(string name, object value) =>
            typeof(AccountSessionState).GetProperty(name, BindingFlags.Public | BindingFlags.Static).SetValue(null, value);

        private void SetControllerField(string name, object value) =>
            typeof(DraftSceneController).GetField(name, PrivateInstance).SetValue(_controller, value);

        private void RefreshClaimButton() =>
            typeof(DraftSceneController).GetMethod("RefreshClaimRewardsButton", PrivateInstance).Invoke(_controller, null);

        private static T InvokeStatic<T>(string method) =>
            (T)typeof(DraftSceneController).GetMethod(method, PrivateStatic).Invoke(null, null);
    }
}
