using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class ClientBuildSettingsLabelTests
    {
        private GameObject _root;
        private GlobalSettingsMenuController _controller;
        private RectTransform _panel;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ClientBuildSettingsTest", typeof(RectTransform));
            _root.SetActive(false);
            _controller = _root.AddComponent<GlobalSettingsMenuController>();
            _controller.EnsureEditableHierarchy();
            _panel = (RectTransform)_root.transform.Find("SafeAreaRoot/ModalRoot/MainPanel");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void EnsureBuildInfoLabel_IsRuntimeOnlyAndReusesOneCurrentLabel()
        {
            Assert.That(_panel.Find("BuildInfoText"), Is.Null,
                "Edit-time hierarchy materialization must not save a build label into scenes.");
            var label = EnsureLabel();
            label.text = "stale build";
            Assert.That(EnsureLabel(), Is.SameAs(label));
            Assert.That(label.text, Is.EqualTo(Project333ClientBuildInfo.BuildDisplayText));
            Assert.That(label.raycastTarget, Is.False);
            Assert.That(label.supportRichText, Is.False);
            var labels = 0;
            foreach (var text in _panel.GetComponentsInChildren<Text>(true))
                if (text.name == "BuildInfoText") labels++;
            Assert.That(labels, Is.EqualTo(1));
        }

        [Test]
        public void BuildInfoLabel_ThreeLinesFitBelowAllFourMenuButtons()
        {
            var label = EnsureLabel();
            label.text = Project333ClientBuildInfo.FormatDisplay(new ClientBuildMetadata
            {
                ApplicationVersion = "1.0",
                BuildId = "20260913-235959-ffffffff",
                BuiltAtUtc = "2026-09-13T23:59:59.0000000+00:00"
            });

            _controller.enabled = false;
            _root.SetActive(true);
            _panel.parent.gameObject.SetActive(true);
            _panel.gameObject.SetActive(true);
            var stack = (RectTransform)_panel.Find("MainButtonStack");
            var buttons = stack.GetComponentsInChildren<Button>(true);
            Assert.That(buttons.Length, Is.EqualTo(4));
            foreach (var button in buttons) button.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(stack);

            Assert.That(label.text.Split('\n').Length, Is.EqualTo(3));
            Assert.That(label.preferredHeight, Is.GreaterThan(0f));
            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height));
            Assert.That(label.preferredWidth, Is.GreaterThan(0f));
            Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width));
            Assert.That(label.raycastTarget, Is.False);
            var labelBounds = BoundsIn(_panel, label.rectTransform);
            Assert.That(labelBounds.xMin, Is.GreaterThanOrEqualTo(_panel.rect.xMin));
            Assert.That(labelBounds.xMax, Is.LessThanOrEqualTo(_panel.rect.xMax));
            Assert.That(labelBounds.yMin, Is.GreaterThanOrEqualTo(_panel.rect.yMin));
            Assert.That(labelBounds.yMax, Is.LessThanOrEqualTo(_panel.rect.yMax));
            foreach (var button in buttons)
            {
                var buttonBounds = BoundsIn(_panel, (RectTransform)button.transform);
                Assert.That(labelBounds.yMax, Is.LessThan(buttonBounds.yMin),
                    "Build identity must sit below " + button.name + " even with all menu buttons visible.");
            }
        }

        private Text EnsureLabel()
        {
            typeof(GlobalSettingsMenuController)
                .GetMethod("EnsureBuildInfoLabel", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_controller, null);
            return _panel.Find("BuildInfoText").GetComponent<Text>();
        }

        private static Rect BoundsIn(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            var bottomLeft = parent.InverseTransformPoint(corners[0]);
            var topRight = parent.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
        }
    }
}
