using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Draft;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class DraftSceneVisualStyleTests
    {
        private GameObject _root;
        private DraftSceneController _controller;
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        [SetUp]
        public void SetUp()
        {
            // Inactive UI avoids Awake/Start, account requests and run state changes.
            _root = new GameObject("DraftVisualTest", typeof(RectTransform));
            _root.SetActive(false);
            _controller = _root.AddComponent<DraftSceneController>();
            SetField("_draftVisualVersion", 1);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [TestCase("ConfigureBattleModeButton")]
        [TestCase("ConfigureReturnToStartButton")]
        [TestCase("ConfigureClaimRewardsButton")]
        public void PixelLayout_ButtonRefreshPreservesAuthoredStyle(string method)
        {
            var button = CreateButton(out var label);
            var rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(91f, -37f);
            rect.sizeDelta = new Vector2(520f, 111f);
            label.fontSize = 37;
            button.GetComponent<Image>().color = Color.cyan;
            Invoke(method, method == "ConfigureBattleModeButton"
                ? new object[] { button, Vector2.zero, Color.red }
                : new object[] { button });

            Assert.That(label.fontSize, Is.EqualTo(37));
            Assert.That(button.GetComponent<Image>().color, Is.EqualTo(Color.cyan));
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(91f, -37f)));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(520f, 111f)));
        }

        [Test]
        public void PixelLayout_WalletRefreshPreservesStyleAndStillUpdatesNumbers()
        {
            CreateButton(out var label);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 31;
            label.color = Color.cyan;
            label.text = "old";
            SetField("_serverWalletText", label);
            Invoke("RefreshServerWalletDisplay");

            Assert.That(label.fontSize, Is.EqualTo(31));
            Assert.That(label.color, Is.EqualTo(Color.cyan));
            Assert.That(label.text, Is.Empty);
            var wallet = label.GetComponent<AccountWalletView>();
            Assert.That(wallet, Is.Not.Null);
            Assert.That(wallet.TicketValue.text, Is.Not.Empty);
            Assert.That(wallet.GoldValue.text, Is.Not.Empty);
            Assert.That(wallet.TicketValue.fontSize, Is.EqualTo(31));
            Assert.That(wallet.TicketValue.color, Is.EqualTo(Color.cyan));
        }

        [Test]
        public void LegacyLayout_ButtonDefaultsStillApply()
        {
            SetField("_draftVisualVersion", 0);
            SetField("_battleModeButtonFontSize", 24);
            var button = CreateButton(out var label);
            label.fontSize = 37;
            Invoke("ConfigureBattleModeButton", button, Vector2.zero, Color.red);
            Assert.That(label.fontSize, Is.EqualTo(24));
            Assert.That(button.GetComponent<Image>().color, Is.EqualTo(Color.red));
        }

        private Button CreateButton(out Text label)
        {
            var buttonObject = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(_root.transform, false);
            var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);
            label = textObject.GetComponent<Text>();
            return buttonObject.GetComponent<Button>();
        }

        private void SetField(string name, object value) => typeof(DraftSceneController)
            .GetField(name, PrivateInstance).SetValue(_controller, value);

        private void Invoke(string name, params object[] args) => typeof(DraftSceneController)
            .GetMethod(name, PrivateInstance).Invoke(_controller, args);
    }
}
