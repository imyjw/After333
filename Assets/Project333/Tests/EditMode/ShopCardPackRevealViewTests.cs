using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Shop;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class ShopCardPackRevealViewTests
    {
        private GameObject _canvas;
        private ShopCardPackRevealView _view;
        private int _dismissals;

        [SetUp]
        public void SetUp()
        {
            _dismissals = 0;
            _canvas = new GameObject("ShopPackRevealTest", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)_canvas.transform).sizeDelta = new Vector2(1920, 1080);
            _view = ShopCardPackRevealView.Create((RectTransform)_canvas.transform, () => _dismissals++);
            _view.ShowPack(Items());
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_canvas);

        [Test]
        public void ClickingPackRepeatedly_StartsOneOpening_AndOutsideClicksCannotSkipIt()
        {
            var button = Find<Button>("CardPack");
            button.onClick.Invoke(); Advance(.2f); button.onClick.Invoke(); button.onClick.Invoke();
            Find<Button>("DimmedBackground").onClick.Invoke();
            Assert.That(_view.Phase, Is.EqualTo(ShopCardPackRevealPhase.Tearing));
            Assert.That(button.interactable, Is.False);
            Assert.That(_dismissals, Is.Zero);
            Assert.That(Find<RectTransform>("PackResults").gameObject.activeSelf, Is.False);
            Advance(.3f);
            Assert.That(_view.Phase, Is.EqualTo(ShopCardPackRevealPhase.Ejecting));
        }

        [Test]
        public void TornPack_EjectsTenWhiteCards_BeforeShowingActualResults()
        {
            Open(); Advance(.7f);
            Assert.That(_view.Phase, Is.EqualTo(ShopCardPackRevealPhase.Ejecting));
            Assert.That(Find<Image>("OpenPack").color.a, Is.EqualTo(1));
            var flying = _view.GetComponentsInChildren<CanvasGroup>(true).Where(x => x.name.StartsWith("FlyingCard")).ToArray();
            Assert.That(flying.Length, Is.EqualTo(10));
            Assert.That(flying.Any(x => x.alpha > 0), Is.True);
            foreach (var card in flying) Assert.That(card.transform.Find("WhiteCard").GetComponent<Image>().color, Is.EqualTo(Color.white));
            Assert.That(Find<RectTransform>("PackResults").gameObject.activeSelf, Is.False);
            Advance(1.1f);
            Assert.That(_view.Phase, Is.EqualTo(ShopCardPackRevealPhase.Results));
            Assert.That(Find<RectTransform>("PackResults").gameObject.activeSelf, Is.True);
            Assert.That(Find<RectTransform>("CardPack").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ResultClickCarryoverAndSlowFrame_CannotDismissBeforeAllCardsAppear()
        {
            Open(); Advance(100);
            var dismiss = Find<Button>("DimmedBackground");
            dismiss.onClick.Invoke();
            Assert.That(_dismissals, Is.Zero);
            Advance(.4f); dismiss.onClick.Invoke();
            Assert.That(_dismissals, Is.Zero);
            Advance(.2f);
            Assert.That(_view.GetComponentsInChildren<CanvasGroup>(true).Where(x => x.name.StartsWith("CardSlot")).All(x => x.alpha == 1), Is.True);
            dismiss.onClick.Invoke(); dismiss.onClick.Invoke();
            Assert.That(_dismissals, Is.EqualTo(1));
            Assert.That(_view.Phase, Is.EqualTo(ShopCardPackRevealPhase.Hidden));
        }

        [Test]
        public void DuplicateCopies_RemainTenDistinctResultCards_WithLiteralNamesAndNeoFont()
        {
            _view.Hide();
            var duplicates = Enumerable.Range(0, 10).Select(i => new ShopCardPackRevealItem("Same", "같은 카드 <b>" + i + "</b>", CardRarity.Rare, null)).ToArray();
            _view.ShowPack(duplicates); Open(); Advance(2); Advance(1);
            Assert.That(Find<RectTransform>("TenCards").childCount, Is.EqualTo(10));
            for (var i = 0; i < 10; i++)
            {
                var slot = Find<RectTransform>("CardSlot" + i);
                Assert.That(slot.Find("CardName").GetComponent<Text>().text, Is.EqualTo(duplicates[i].CardName));
                Assert.That(slot.Find("Card/MissingArtwork").gameObject.activeSelf, Is.True);
            }
            foreach (var text in _view.GetComponentsInChildren<Text>(true))
            {
                Assert.That(text.supportRichText, Is.False);
                Assert.That(text.font, Is.EqualTo(NoticeTheme.Font));
            }
        }

        [Test]
        public void RarityAuras_AreBlueRedAndYellow_AndLegendaryHasMostOrbitingSparks()
        {
            Open(); Advance(2); Advance(1);
            var commons = Find<RectTransform>("CardSlot0").Find("Card");
            Assert.That(commons.Find("AuraEdge0").gameObject.activeSelf, Is.False);
            var uncommon = Find<RectTransform>("CardSlot1").Find("Card");
            Assert.That(uncommon.Find("AuraEdge0").gameObject.activeSelf, Is.False);
            var blue = Aura(2); var red = Aura(3); var yellow = Aura(4);
            Assert.That(blue.b, Is.GreaterThan(blue.r));
            Assert.That(red.r, Is.GreaterThan(red.b));
            Assert.That(yellow.r, Is.GreaterThan(.9));
            Assert.That(yellow.g, Is.GreaterThan(.6));
            Assert.That(SparkCount(2), Is.EqualTo(4));
            Assert.That(SparkCount(3), Is.EqualTo(6));
            Assert.That(SparkCount(4), Is.EqualTo(10));
            var before = Find<RectTransform>("CardSlot4").Find("Card/AuraSpark0").GetComponent<RectTransform>().anchoredPosition;
            Advance(.7f);
            var after = Find<RectTransform>("CardSlot4").Find("Card/AuraSpark0").GetComponent<RectTransform>().anchoredPosition;
            Assert.That(after, Is.Not.EqualTo(before));
        }

        [TestCase(1920, 1080)]
        [TestCase(1280, 720)]
        [TestCase(1600, 1200)]
        public void Results_AreFiveColumnsAndTwoRows_InsideAvailableSafeArea(int width, int height)
        {
            ((RectTransform)_canvas.transform).sizeDelta = new Vector2(width, height);
            Canvas.ForceUpdateCanvases();
            Open(); Advance(2); Advance(1); Canvas.ForceUpdateCanvases();
            var grid = Find<RectTransform>("TenCards");
            var first = Find<RectTransform>("CardSlot0");
            for (var i = 0; i < 10; i++)
            {
                var slot = Find<RectTransform>("CardSlot" + i);
                var p = slot.anchoredPosition;
                Assert.That(Mathf.Abs(p.x) + slot.rect.width * .5f, Is.LessThanOrEqualTo(grid.rect.width * .5f + .1f));
                Assert.That(Mathf.Abs(p.y) + slot.rect.height * .5f, Is.LessThanOrEqualTo(grid.rect.height * .5f + .1f));
                if (i < 5) Assert.That(p.y, Is.EqualTo(first.anchoredPosition.y).Within(.01));
                else Assert.That(p.y, Is.LessThan(first.anchoredPosition.y));
                if (i >= 5) Assert.That(p.x, Is.EqualTo(Find<RectTransform>("CardSlot" + (i - 5)).anchoredPosition.x).Within(.01));
            }
        }

        [Test]
        public void InputRefresh_CannotReplaceReceiptDuringOpeningOrResults()
        {
            Open(); Advance(.2f);
            var replacements = Enumerable.Range(0, 10).Select(i => new ShopCardPackRevealItem("Other", "잘못된 카드", CardRarity.Common, null)).ToArray();
            _view.ShowPack(replacements);
            Assert.That(_view.Phase, Is.EqualTo(ShopCardPackRevealPhase.Tearing));
            Advance(2); Advance(1); _view.ShowPack(replacements);
            Assert.That(Find<Text>("CardName").text, Is.EqualTo("카드 0"));
        }

        [Test]
        public void PaidReceiptList_IsCopiedBeforeCallerChangesItsArray()
        {
            _view.Hide(); var items = Items(); _view.ShowPack(items);
            items[0] = new ShopCardPackRevealItem("Changed", "변경", CardRarity.Common, null);
            Open(); Advance(2); Advance(1);
            Assert.That(Find<Text>("CardName").text, Is.EqualTo("카드 0"));
        }

        [Test]
        public void InvalidReceiptCountOrNullItem_DoesNotShowInventedCards()
        {
            _view.Hide();
            Assert.Throws<ArgumentException>(() => _view.ShowPack(Items().Take(9).ToArray()));
            Assert.Throws<ArgumentException>(() => _view.ShowPack(Items().Concat(Items()).ToArray()));
            var items = Items(); items[2] = null;
            Assert.Throws<ArgumentException>(() => _view.ShowPack(items));
            Assert.That(_view.Phase, Is.EqualTo(ShopCardPackRevealPhase.Hidden));
            Assert.That(_dismissals, Is.Zero);
        }

        [Test]
        public void OwnerHideAndDisable_NeverAcknowledgePaidReceipt_AndNextShowStartsSealed()
        {
            Open(); Advance(.7f); _view.Hide();
            Assert.That(_dismissals, Is.Zero);
            _view.ShowPack(Items());
            Assert.That(_view.Phase, Is.EqualTo(ShopCardPackRevealPhase.Sealed));
            typeof(ShopCardPackRevealView).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_view, null);
            Assert.That(_view.Phase, Is.EqualTo(ShopCardPackRevealPhase.Hidden));
            Assert.That(_dismissals, Is.Zero);
            _view.ShowPack(Items());
            Assert.That(Find<Button>("CardPack").interactable, Is.True);
        }

        [Test]
        public void NativeFoilFrames_AreRegisteredAndHaveMatchingFullSquareCells()
        {
            var closed = Find<Image>("ClosedPack").sprite;
            var open = Find<Image>("OpenPack").sprite;
            var top = Find<Image>("TornTop").sprite;
            Assert.That(closed, Is.Not.Null); Assert.That(open, Is.Not.Null); Assert.That(top, Is.Not.Null);
            Assert.That(closed.name, Is.EqualTo("CardPackClosed"));
            Assert.That(open.name, Is.EqualTo("CardPackOpen"));
            Assert.That(top.name, Is.EqualTo("CardPackTornTop"));
            Assert.That(closed.rect.size, Is.EqualTo(open.rect.size));
            Assert.That(closed.rect.size, Is.EqualTo(top.rect.size));
        }

        [Test]
        public void AllResultGraphics_PassAnyClickToBackdrop_WhileSealedPackBlocksSceneInput()
        {
            Assert.That(Find<Image>("DimmedBackground").raycastTarget, Is.True);
            Assert.That(Find<Image>("ClosedPack").raycastTarget, Is.True);
            Open(); Advance(2); Advance(1);
            foreach (var graphic in Find<RectTransform>("PackResults").GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False, graphic.name);
        }

        private static ShopCardPackRevealItem[] Items() => Enumerable.Range(0, 10)
            .Select(i => new ShopCardPackRevealItem("Card" + i, "카드 " + i, (CardRarity)(i % 5), null)).ToArray();
        private void Open() => Find<Button>("CardPack").onClick.Invoke();
        private void Advance(float delta) => typeof(ShopCardPackRevealView).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_view, new object[] { delta });
        private T Find<T>(string name) where T : Component => _view.GetComponentsInChildren<T>(true).First(x => x.name == name);
        private Color Aura(int slot) => Find<RectTransform>("CardSlot" + slot).Find("Card/AuraEdge0").GetComponent<Image>().color;
        private int SparkCount(int slot) => Find<RectTransform>("CardSlot" + slot).Find("Card").Cast<Transform>().Count(x => x.name.StartsWith("AuraSpark") && x.gameObject.activeSelf);
    }
}
