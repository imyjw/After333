using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Startup;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class GameStartTutorialEntryTests
    {
        private const BindingFlags HiddenInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags HiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private readonly Dictionary<FieldInfo, object> _previousSession = new Dictionary<FieldInfo, object>();
        private GameObject _canvasRoot;
        private GameStartSceneController _controller;

        [SetUp]
        public void SetUp()
        {
            _canvasRoot = new GameObject("StartCanvas", typeof(RectTransform), typeof(Canvas));
            _canvasRoot.SetActive(false);
            _canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)_canvasRoot.transform).sizeDelta = new Vector2(1920f, 1080f);
            var controllerObject = new GameObject("GameStartSceneController", typeof(RectTransform));
            controllerObject.SetActive(false);
            controllerObject.transform.SetParent(_canvasRoot.transform, false);
            var rootRect = (RectTransform)controllerObject.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            _canvasRoot.SetActive(true); // Active UI-only canvas, while controller remains inactive.
            _controller = controllerObject.AddComponent<GameStartSceneController>();
            SetSession("LatestRunStatus", string.Empty);
            SetSession("AccountId", "tutorial-entry-fixture");
            SetSession("ActiveRunId", "real-run-sentinel");
            SetSession("ActiveRunStatus", "ready");
            SetSession("ActiveDeckId", "real-deck-sentinel");
            SetSession("ResourceGold", 55L);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvasRoot);
            foreach (var value in _previousSession) value.Key.SetValue(null, value.Value);
            _previousSession.Clear();
        }

        [TestCase(false, false, false)]
        [TestCase(false, true, true)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        public void TutorialRemainsAvailableWithoutTicketsAndDuringAccountWork(
            bool authenticated, bool authenticating, bool authBusy)
        {
            SetSession("SessionToken", authenticated ? "tutorial-test-token" : string.Empty);
            SetSession("Tickets", 0);
            SetController("_useServerAccount", true);
            SetController("_isAuthenticating", authenticating);
            SetController("_isGameIdAuthBusy", authBusy);
            SetController("_authFailed", !authenticated);
            Invoke("EnsureTutorialEntryButton");

            _controller.RefreshUiFromExternalState();

            var button = GetButton();
            Assert.That(button.gameObject.activeSelf, Is.True);
            Assert.That(button.interactable, Is.True);
            Assert.That(button.GetComponentInChildren<UnityEngine.UI.Text>(true).text, Is.EqualTo("튜토리얼"));
            Assert.That(AccountSessionState.Tickets, Is.Zero);
            Assert.That(AccountSessionState.ResourceGold, Is.EqualTo(55L));
            Assert.That(AccountSessionState.ActiveRunId, Is.EqualTo("real-run-sentinel"));
            Assert.That(AccountSessionState.ActiveRunStatus, Is.EqualTo("ready"));
            Assert.That(AccountSessionState.ActiveDeckId, Is.EqualTo("real-deck-sentinel"));
        }

        [Test]
        public void EntryCanvasDrawsAboveLoginGateWithoutFullScreenRaycastBlocker()
        {
            Assert.That(UnityEngine.Application.isPlaying, Is.False);
            // GameStart is not ExecuteAlways: an active EditMode host runs no login lifecycle.
            _controller.gameObject.SetActive(true);
            Invoke("EnsureAccountLoginGate");
            Invoke("EnsureTutorialEntryButton");
            Assert.That(typeof(GameStartSceneController).GetField("_hasLoadedSavedAccountCredentials", HiddenInstance)
                .GetValue(_controller), Is.False);
            Assert.That(AccountSessionState.ResourceGold, Is.EqualTo(55L));
            Assert.That(AccountSessionState.ActiveRunId, Is.EqualTo("real-run-sentinel"));
            Assert.That(AccountSessionState.ActiveRunStatus, Is.EqualTo("ready"));
            Assert.That(AccountSessionState.ActiveDeckId, Is.EqualTo("real-deck-sentinel"));
            var gate = _controller.transform.Find("AccountLoginGateCanvas").GetComponent<Canvas>();
            var entry = _controller.transform.Find("TutorialEntryCanvas").GetComponent<Canvas>();
            var button = GetButton();
            Canvas.ForceUpdateCanvases();

            Assert.That(entry.overrideSorting, Is.True);
            Assert.That(entry.sortingOrder, Is.GreaterThan(gate.sortingOrder));
            Assert.That(entry.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);
            Assert.That(entry.GetComponent<UnityEngine.UI.Image>(), Is.Null);
            Assert.That(button.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.True);
            Assert.That(button.GetComponentInChildren<UnityEngine.UI.Text>(true).raycastTarget, Is.False);
        }
        [Test]
        public void RepeatedEnsureReusesOneButtonAndKeepsLocalSceneRoute()
        {
            Invoke("EnsureTutorialEntryButton");
            var first = GetButton();
            Invoke("UnbindTutorialEntryButton");
            Invoke("EnsureTutorialEntryButton");
            Invoke("EnsureTutorialEntryButton");

            Assert.That(GetButton(), Is.SameAs(first));
            Assert.That(_controller.transform.Find("TutorialEntryCanvas").childCount, Is.EqualTo(1));
            Assert.That(GameStartSceneController.TutorialSceneName, Is.EqualTo("Tutorial_VSlice"));
        }

        [TestCase(1920, 1080, 0, 0)]
        [TestCase(2340, 1080, 92, 0)]
        [TestCase(2340, 1080, 0, 92)]
        [TestCase(2400, 1080, 120, 24)]
        public void ButtonStaysInsideSafeAreaAndAwayFromCenterMenu(int width, int height, int left, int right)
        {
            ((RectTransform)_canvasRoot.transform).sizeDelta = new Vector2(width, height);
            var fitter = _controller.gameObject.AddComponent<SafeAreaFitter>();
            fitter.Configure();
            Invoke("EnsureTutorialEntryButton");
            typeof(SafeAreaFitter).GetMethod("ApplyLayout", HiddenInstance).Invoke(
                fitter, new object[] { new Rect(left, 0f, width - left - right, height), width, height });
            Canvas.ForceUpdateCanvases();
            var buttonRect = (RectTransform)GetButton().transform;
            var rootRect = (RectTransform)_controller.transform;
            var corners = new Vector3[4];
            buttonRect.GetWorldCorners(corners);
            var lowerLeft = rootRect.InverseTransformPoint(corners[0]);
            var upperRight = rootRect.InverseTransformPoint(corners[2]);

            Assert.That(lowerLeft.x, Is.GreaterThanOrEqualTo(rootRect.rect.xMin));
            Assert.That(lowerLeft.y, Is.GreaterThanOrEqualTo(rootRect.rect.yMin));
            Assert.That(upperRight.x, Is.LessThanOrEqualTo(rootRect.rect.xMax));
            Assert.That(upperRight.y, Is.LessThanOrEqualTo(rootRect.rect.yMax));
            Assert.That(upperRight.x, Is.LessThan(-340f), "Leave the center menu unobstructed.");
        }

        private UnityEngine.UI.Button GetButton() =>
            _controller.transform.Find("TutorialEntryCanvas/TutorialButton").GetComponent<UnityEngine.UI.Button>();

        private void Invoke(string method) =>
            typeof(GameStartSceneController).GetMethod(method, HiddenInstance).Invoke(_controller, null);

        private void SetController(string name, object value) =>
            typeof(GameStartSceneController).GetField(name, HiddenInstance).SetValue(_controller, value);

        private void SetSession(string name, object value)
        {
            var field = typeof(AccountSessionState).GetField("<" + name + ">k__BackingField", HiddenStatic);
            if (!_previousSession.ContainsKey(field)) _previousSession[field] = field.GetValue(null);
            field.SetValue(null, value);
        }
    }
}