using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Draft;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class RunRewardRevealViewTests
    {
        private GameObject _canvas;
        private RunRewardRevealView _view;
        private int _opens;
        private int _confirms;

        [SetUp]
        public void SetUp()
        {
            _opens = 0;
            _confirms = 0;
            _canvas = new GameObject("RewardViewTest", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)_canvas.transform).sizeDelta = new Vector2(1920, 1080);
            _view = RunRewardRevealView.Create((RectTransform)_canvas.transform,
                () => { if (_view.TryBeginOpening()) _opens++; }, () => _confirms++);
            _view.ShowClosed(33, 2, true);
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_canvas);

        [Test]
        public void ClickSpamming_OpensOnlyOnce_AndReceiptWaitsForOpeningAnimation()
        {
            var chest = Find<Button>("RewardChest");
            chest.onClick.Invoke(); chest.onClick.Invoke(); chest.onClick.Invoke();
            Assert.That(_opens, Is.EqualTo(1));
            Assert.That(chest.interactable, Is.False);
            _view.SetReceipt(Receipt());
            Advance(.5f);
            Assert.That(_view.Phase, Is.EqualTo(RunRewardRevealPhase.Opening));
            Assert.That(Find<RectTransform>("RewardPanel").gameObject.activeSelf, Is.False);
            Advance(.7f);
            Assert.That(_view.Phase, Is.EqualTo(RunRewardRevealPhase.Rewards));
            Assert.That(Find<Text>("GoldValue").text, Is.EqualTo("+99"));
            Assert.That(Find<Text>("RewardRecord").text, Does.Contain("33승"));
            Assert.That(Find<RectTransform>("RewardPanel").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void SlowReceipt_KeepsOpenedChestWaitingWithoutUnlockingOrInventingRewards()
        {
            Find<Button>("RewardChest").onClick.Invoke();
            Advance(2);
            Assert.That(_view.Phase, Is.EqualTo(RunRewardRevealPhase.WaitingForReceipt));
            Assert.That(Find<Text>("Hint").text, Does.Contain("확인"));
            Find<Button>("RewardChest").onClick.Invoke();
            Assert.That(_opens, Is.EqualTo(1));
            _view.SetReceipt(Receipt());
            Assert.That(_view.IsShowingRewards, Is.True);
        }

        [Test]
        public void NetworkFailure_RetryStartsFreshAnimation_AndCannotRevealStaleReceipt()
        {
            Find<Button>("RewardChest").onClick.Invoke();
            Advance(.3f);
            _view.ShowError("다시 시도");
            Assert.That(_view.Phase, Is.EqualTo(RunRewardRevealPhase.Error));
            Assert.That(Find<Button>("RewardChest").interactable, Is.True);
            Assert.That(Find<Image>("ClosedChest").color.a, Is.EqualTo(1));
            Find<Button>("RewardChest").onClick.Invoke();
            Advance(2);
            Assert.That(_opens, Is.EqualTo(2));
            Assert.That(_view.Phase, Is.EqualTo(RunRewardRevealPhase.WaitingForReceipt));
        }

        [Test]
        public void OutsideClicksAndSummaryOpeningClick_DoNotCloseOrSkipRewards()
        {
            var backdrop = Find<Image>("DimmedBackground");
            Assert.That(backdrop.raycastTarget, Is.True);
            Assert.That(backdrop.GetComponent<Button>(), Is.Null);
            Find<Button>("RewardChest").onClick.Invoke();
            _view.SetReceipt(Receipt());
            Advance(2);
            var confirm = Find<Button>("Confirm");
            confirm.onClick.Invoke();
            Assert.That(_confirms, Is.Zero);
            Advance(.3f);
            confirm.onClick.Invoke(); confirm.onClick.Invoke();
            Assert.That(_confirms, Is.EqualTo(1));
            Assert.That(_view.Phase, Is.EqualTo(RunRewardRevealPhase.Hidden));
        }

        [Test]
        public void DisableDuringOpening_DropsPresentationOnly_AndClosedChestCanResume()
        {
            Find<Button>("RewardChest").onClick.Invoke();
            _view.SetReceipt(Receipt());
            _view.gameObject.SetActive(false);
            // Ordinary MonoBehaviours do not receive lifecycle callbacks in EditMode tests.
            typeof(RunRewardRevealView).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_view, null);
            Assert.That(_view.Phase, Is.EqualTo(RunRewardRevealPhase.Hidden));
            Assert.That(_confirms, Is.Zero);
            _view.ShowClosed(33, 2, true);
            Find<Button>("RewardChest").onClick.Invoke();
            Advance(2);
            Assert.That(_view.Phase, Is.EqualTo(RunRewardRevealPhase.WaitingForReceipt));
        }

        [Test]
        public void UnconfirmedServerResult_CannotOpenRewardChest()
        {
            _view.ShowClosed(33, 0, false, true);
            Assert.That(Find<Button>("RewardChest").interactable, Is.False);
            Assert.That(_view.TryBeginOpening(), Is.False);
            Assert.That(_view.Phase, Is.EqualTo(RunRewardRevealPhase.Closed));
        }

        [Test]
        public void AllFortyOneRewardKinds_RemainScrollable_AndUseLiteralNeoFontText()
        {
            var items = Enumerable.Range(0, 41).Select(i => new RewardItemDto { Id = "C" + i, Count = i < 17 ? 3 : 2 }).ToArray();
            var receipt = RunRewardDisplayContent.Build(new RewardGrantDto { ResourceGoldDelta = 99, CardRewardItems = items },
                33, 2, 132, id => "아주 긴 카드 이름 <b>" + id + "</b>");
            Find<Button>("RewardChest").onClick.Invoke(); _view.SetReceipt(receipt); Advance(2);
            Canvas.ForceUpdateCanvases();
            var scroll = Find<ScrollRect>("RewardList");
            Assert.That(scroll.content.childCount, Is.EqualTo(41));
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            scroll.verticalNormalizedPosition = 0;
            Canvas.ForceUpdateCanvases();
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0).Within(.01));
            Assert.That(Find<Text>("CardSummary").text, Does.Contain("99"));
            foreach (var text in _view.GetComponentsInChildren<Text>(true))
            {
                Assert.That(text.supportRichText, Is.False);
                Assert.That(text.font, Is.EqualTo(NoticeTheme.Font));
            }
        }

        [Test]
        public void ZeroWinRun_ShowsExplicitNoRewardMessage()
        {
            Find<Button>("RewardChest").onClick.Invoke();
            _view.SetReceipt(RunRewardDisplayContent.Build(new RewardGrantDto(), 0, 3, 10));
            Advance(2);
            Assert.That(Find<Text>("GoldValue").text, Is.EqualTo("+0"));
            Assert.That(Find<Text>("Name").text, Does.Contain("획득한 보상이 없습니다"));
            Assert.That(Find<RectTransform>("TicketReward").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void BothNativeChestFrames_AreRegisteredAndTransparent()
        {
            var closed = Find<Image>("ClosedChest").sprite;
            var open = Find<Image>("OpenChest").sprite;
            Assert.That(closed, Is.Not.Null); Assert.That(open, Is.Not.Null);
            Assert.That(closed.name, Is.EqualTo("RewardChestClosed"));
            Assert.That(open.name, Is.EqualTo("RewardChestOpen"));
            Assert.That(closed.rect.size, Is.EqualTo(open.rect.size));
            Assert.That(closed.texture.filterMode, Is.EqualTo(FilterMode.Point));
        }

        [Test]
        public void LongWrappedNameAndQuantity_FitInsideScrollableViewport()
        {
            var name = string.Concat(Enumerable.Repeat("아주긴카드이름", 30));
            Find<Button>("RewardChest").onClick.Invoke();
            _view.SetReceipt(RunRewardDisplayContent.Build(new RewardGrantDto
                { CardRewardItems = new[] { new RewardItemDto { Id = "A", Count = 99 } } }, 33, 2, 132, id => name));
            Advance(2); Canvas.ForceUpdateCanvases();
            var scroll = Find<ScrollRect>("RewardList");
            Assert.That(scroll.content.rect.width, Is.EqualTo(scroll.viewport.rect.width).Within(.1));
            var label = Find<Text>("Name");
            Assert.That(label.text, Is.EqualTo(name));
            Assert.That(label.rectTransform.rect.height, Is.GreaterThan(58));
            var viewportCorners = new Vector3[4]; var labelCorners = new Vector3[4]; var countCorners = new Vector3[4];
            scroll.viewport.GetWorldCorners(viewportCorners);
            label.rectTransform.GetWorldCorners(labelCorners);
            Find<Text>("Count").rectTransform.GetWorldCorners(countCorners);
            Assert.That(labelCorners[0].x, Is.GreaterThanOrEqualTo(viewportCorners[0].x));
            Assert.That(countCorners[2].x, Is.LessThanOrEqualTo(viewportCorners[2].x));
            Assert.That(Find<Image>("Scrollbar").raycastTarget, Is.True);
            Assert.That(Find<Image>("Handle").raycastTarget, Is.True);
        }

        private RunRewardDisplayContent Receipt() => RunRewardDisplayContent.Build(new RewardGrantDto
            { ResourceGoldDelta = 99, CardRewardItems = new[] { new RewardItemDto { Id = "A", Count = 99 } } }, 33, 2, 132);
        private T Find<T>(string name) where T : Component =>
            _view.GetComponentsInChildren<T>(true).First(component => component.name == name);
        private void Advance(float delta) => typeof(RunRewardRevealView)
            .GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_view, new object[] { delta });
    }
}
