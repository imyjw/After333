using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Ads;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Ads;
using Project333.Runtime.Presentation.Shop;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class ShopCardPackIntegrationTests
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private Scene _scene;
        private ShopSceneController _shop;
        private string _owner, _url, _requestId;
        private string _previousOwner, _previousToken;
        private long _previousGold;
        private int _previousTickets;

        [SetUp]
        public void SetUp()
        {
            _previousOwner = AccountSessionState.AccountId;
            _previousToken = AccountSessionState.SessionToken;
            _previousGold = AccountSessionState.ResourceGold;
            _previousTickets = AccountSessionState.Tickets;
            _owner = Guid.NewGuid().ToString("D");
            _requestId = Guid.NewGuid().ToString("D");
            _url = Project333ServerEndpointSettings.ResolveHttpUrl(Project333ServerEndpointSettings.LocalHttpUrl);
            Session("AccountId", _owner);
            Session("SessionToken", "ShopIntegrationToken");
            Session("ResourceGold", 66L);
            Session("Tickets", 7);
            _scene = EditorSceneManager.OpenPreviewScene("Assets/Shop_VSlice.unity");
            _shop = _scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ShopSceneController>(true)).Single();
            Call("EnsureShopProducts");
            Call("LoadCardDisplayData");
            Call("CaptureDisplayContext");
            Call("RefreshUi");
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            if (_shop != null) Call("OnDisable");
            PendingCardPackPresentation.Clear(_url, _owner, PendingCardPackPresentation.GetRequestId(_url, _owner));
            if (PendingAccountOperation.HasPending(_url, ServerShopClient.OperationType))
            {
                using var operation = PendingAccountOperation.Begin(_url, ServerShopClient.OperationType);
                operation.Complete(operation.RequestId, _owner);
            }
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            Session("AccountId", _previousOwner);
            Session("SessionToken", _previousToken);
            Session("ResourceGold", _previousGold);
            Session("Tickets", _previousTickets);
        }

        [TestCase(32, false)]
        [TestCase(33, true)]
        public void PurchaseButton_UsesThirtyThreeGoldBoundary_WhileTicketProductKeepsItsCost(long gold, bool expected)
        {
            Session("ResourceGold", gold);
            Call("RefreshUi");
            Assert.That(Field<Button>("_purchaseCardPackButton").interactable, Is.EqualTo(expected));
            Assert.That(Field<Button>("_purchaseTicketButton").interactable, Is.True);
            Assert.That(Field<Text>("_cardPackButtonLabel").text, Is.EqualTo("카드팩 구매"));
            if (!expected)
            {
                _shop.PurchaseCardPackFromUi();
                Assert.That(Field<Text>("_statusText").text, Does.Contain("33"));
                Assert.That(PendingAccountOperation.HasPending(_url, ServerShopClient.OperationType), Is.False);
                Assert.That(PendingCardPackPresentation.GetRequestId(_url, _owner), Is.Empty);
                Assert.That(AccountSessionState.ResourceGold, Is.EqualTo(gold));
            }
        }

        [Test]
        public void MaterializedShop_ProductButtonsStayInsidePanels_AndAdSectionsNeverOverlap()
        {
            // PreviewScene does not render ScreenSpaceOverlay canvases; give them real layout bounds.
            foreach (var canvas in _scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Canvas>(true)).Where(x => x.isRootCanvas))
            {
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null) scaler.enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.scaleFactor = 1;
                var canvasRect = (RectTransform)canvas.transform;
                canvasRect.sizeDelta = new Vector2(1920, 1080);
                canvasRect.ForceUpdateRectTransforms();
            }
            Canvas.ForceUpdateCanvases();
            // Validate the actual scene after product materialization, including serialized ads text.
            var root = (RectTransform)_shop.transform;
            var products = new[]
            {
                (Panel: "TicketProductPanel", Button: "PurchaseTicketButton"),
                (Panel: "CardPackProductPanel", Button: "PurchaseCardPackButton"),
                (Panel: "RewardedTicketProductPanel", Button: "WatchRewardedAdButton")
            };
            foreach (var product in products)
            {
                var panel = (RectTransform)root.Find(product.Panel);
                var button = (RectTransform)panel.Find(product.Button);
                AssertInside(WorldRect(panel), WorldRect(button), product.Button);
            }

            var adPanel = (RectTransform)root.Find("RewardedTicketProductPanel");
            var sections = new[] { "RewardedProductDescriptionText", "AdPriceText", "WatchRewardedAdButton", "RewardedAdStatusText" };
            var bounds = sections.Select(x => WorldRect((RectTransform)adPanel.Find(x))).ToArray();
            for (var i = 0; i < bounds.Length; i++)
            {
                AssertInside(WorldRect(adPanel), bounds[i], sections[i]);
                for (var j = i + 1; j < bounds.Length; j++)
                {
                    var overlapWidth = Mathf.Min(bounds[i].xMax, bounds[j].xMax) - Mathf.Max(bounds[i].xMin, bounds[j].xMin);
                    var overlapHeight = Mathf.Min(bounds[i].yMax, bounds[j].yMax) - Mathf.Max(bounds[i].yMin, bounds[j].yMin);
                    Assert.That(overlapWidth > .01f && overlapHeight > .01f, Is.False,
                        $"{sections[i]} {bounds[i]} overlaps {sections[j]} {bounds[j]}");
                }
            }
        }

        [Test]
        public void UncertainPackPurchase_BlocksBothEconomicButtons_AndParticipatesInRecoveryWork()
        {
            using var operation = PendingAccountOperation.Begin(_url, ServerShopClient.OperationType);
            Call("RefreshUi");
            Assert.That(PendingAccountOperation.HasAnyPending(_url), Is.True);
            Assert.That(PendingAccountOperation.PendingWork(_url).Any(x => x.Operation == ServerShopClient.OperationType && x.CardId == null), Is.True);
            Assert.That(Field<Button>("_purchaseCardPackButton").interactable, Is.False);
            Assert.That(Field<Button>("_purchaseTicketButton").interactable, Is.False);
            _shop.PurchaseCardPackFromUi();
            Assert.That(PendingCardPackPresentation.GetRequestId(_url, _owner), Is.Empty);
            Assert.That(AccountSessionState.ResourceGold, Is.EqualTo(66));
            operation.Complete(operation.RequestId, _owner);
        }

        [Test]
        public void UnreadReceipt_BlocksNewPackBeforeRestoration_CannotReplaceItsIdentity()
        {
            PendingCardPackPresentation.Remember(_url, _owner, _requestId);
            Call("RefreshUi");
            Assert.That(Field<Button>("_purchaseCardPackButton").interactable, Is.False);
            _shop.PurchaseCardPackFromUi();
            Assert.That(PendingCardPackPresentation.GetRequestId(_url, _owner), Is.EqualTo(_requestId));
            Assert.That(PendingAccountOperation.HasAnyPending(_url), Is.False);
            Assert.That(AccountSessionState.ResourceGold, Is.EqualTo(66));
        }

        [Test]
        public void AlreadyPaidModal_PreservesOriginalTenIncludingDuplicates_WithoutWalletOrCollectionMutation()
        {
            var beforeCards = AccountSessionState.OwnedCardIds.OrderBy(x => x).ToArray();
            var receipt = PaidReceipt();
            Present(receipt);
            var view = Field<ShopCardPackRevealView>("_cardPackReveal");
            var overlay = Field<RectTransform>("_revealHost").GetComponent<Canvas>();
            var settings = _scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Canvas>(true)).Single(x => x.name == "GlobalSettingsMenuCanvas");
            Assert.That(overlay.sortingOrder, Is.GreaterThan(settings.sortingOrder));
            Assert.That(overlay.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(view.Phase, Is.EqualTo(ShopCardPackRevealPhase.Sealed));
            Assert.That(Field<Button>("_purchaseTicketButton").interactable, Is.False);
            Assert.That(Field<Button>("_purchaseCardPackButton").interactable, Is.False);
            Assert.That(Field<Button>("_backButton").interactable, Is.False);
            view.GetComponentsInChildren<Button>(true).Single(x => x.name == "CardPack").onClick.Invoke();
            Advance(view, 2); Advance(view, 1);
            var items = (ShopCardPackRevealItem[])typeof(ShopCardPackRevealView).GetField("_items", Hidden).GetValue(view);
            Assert.That(items.Select(x => x.CardId), Is.EqualTo(receipt.CardIds));
            Assert.That(items[0].CardId, Is.EqualTo(items[1].CardId));
            Assert.That(view.GetComponentsInChildren<RectTransform>(true).Single(x => x.name == "TenCards").childCount, Is.EqualTo(10));
            Assert.That(AccountSessionState.ResourceGold, Is.EqualTo(66));
            Assert.That(AccountSessionState.Tickets, Is.EqualTo(7));
            Assert.That(AccountSessionState.OwnedCardIds.OrderBy(x => x).ToArray(), Is.EqualTo(beforeCards));
        }

        [Test]
        public void DisablingShop_HidesOutsideCanvasModal_AndRetainsUnreadReceipt()
        {
            Present(PaidReceipt());
            var host = Field<RectTransform>("_revealHost");
            var view = Field<ShopCardPackRevealView>("_cardPackReveal");
            Assert.That(host.gameObject.activeSelf, Is.True);
            Call("OnDisable");
            Assert.That(host.gameObject.activeSelf, Is.False);
            Assert.That(view.IsShowing, Is.False);
            Assert.That(PendingCardPackPresentation.GetRequestId(_url, _owner), Is.EqualTo(_requestId));
            Assert.That(AccountSessionState.ResourceGold, Is.EqualTo(66));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExplicitResultDismissal_ClearsOnlyTheShownReceipt(bool newerMarker)
        {
            Present(PaidReceipt());
            var retained = newerMarker ? Guid.NewGuid().ToString("D") : _requestId;
            if (newerMarker) PendingCardPackPresentation.Remember(_url, _owner, retained);
            var view = Field<ShopCardPackRevealView>("_cardPackReveal");
            view.GetComponentsInChildren<Button>(true).Single(x => x.name == "CardPack").onClick.Invoke();
            Advance(view, 2); Advance(view, 1);
            view.GetComponentsInChildren<Button>(true).Single(x => x.name == "DimmedBackground").onClick.Invoke();
            Assert.That(view.IsShowing, Is.False);
            Assert.That(PendingCardPackPresentation.GetRequestId(_url, _owner), Is.EqualTo(newerMarker ? retained : ""));
            Assert.That(AccountSessionState.ResourceGold, Is.EqualTo(66));
        }

        [Test]
        public void LateOldRequestFinally_CannotCancelOrUnlockNewRequest()
        {
            var old = (CancellationTokenSource)Call("BeginRequest");
            var current = (CancellationTokenSource)Call("BeginRequest");
            Call("EndRequest", old);
            Assert.That(Field<CancellationTokenSource>("_requestCancellation"), Is.SameAs(current));
            Assert.That(Field<bool>("_isBusy"), Is.True);
            Assert.That(current.IsCancellationRequested, Is.False);
            Call("EndRequest", current);
            Assert.That(Field<bool>("_isBusy"), Is.False);
        }

        [Test]
        public void PackPurchaseInFlight_BlocksReadyAdButtonAndDirectHandler()
        {
            var ads = Field<RewardedTicketController>("_rewardedTicketController");
            AdField(ads, "_shopSceneController", _shop);
            AdField(ads, "_levelPlayAppKey", "IntegrationApp");
            AdField(ads, "_rewardedAdUnitId", "IntegrationUnit");
            AdField(ads, "_adService", new ReadyAd());
            AdField(ads, "_isRequestingAttempt", false);
            ads.RefreshUi();
            Assert.That(AdField<Button>(ads, "_watchAdButton").interactable, Is.True);
            var request = (CancellationTokenSource)Call("BeginRequest");
            ads.RefreshUi();
            Assert.That(_shop.IsInteractionLocked, Is.True);
            Assert.That(AdField<Button>(ads, "_watchAdButton").interactable, Is.False);
            ads.WatchRewardedAdFromUi();
            Assert.That(ads.IsRewardInteractionActive, Is.False);
            Assert.That(Field<CancellationTokenSource>("_requestCancellation"), Is.SameAs(request));
            Call("EndRequest", request);
        }

        [Test]
        public void ActiveAd_DefersPackRestorationAndBlocksNewEconomicActions()
        {
            var ads = Field<RewardedTicketController>("_rewardedTicketController");
            AdField(ads, "_adService", new ReadyAd { IsShowing = true });
            PendingCardPackPresentation.Remember(_url, _owner, _requestId);
            Call("RefreshUi");
            Assert.That(ads.IsRewardInteractionActive, Is.True);
            Assert.That(Field<Button>("_purchaseCardPackButton").interactable, Is.False);
            Assert.That(Field<Button>("_purchaseTicketButton").interactable, Is.False);
            Call("RestoreCardPackAsync");
            _shop.PurchaseCardPackFromUi();
            _shop.PurchaseTicketFromUi();
            Assert.That(Field<CancellationTokenSource>("_requestCancellation"), Is.Null);
            Assert.That(Field<bool>("_isBusy"), Is.False);
            Assert.That(PendingAccountOperation.HasAnyPending(_url), Is.False);
            Assert.That(PendingCardPackPresentation.GetRequestId(_url, _owner), Is.EqualTo(_requestId));
        }

        [Test]
        public void RequestContext_RejectsAccountTokenEndpointAndDisabledOwnerChanges()
        {
            var current = (CancellationTokenSource)Call("BeginRequest");
            Assert.That((bool)Call("IsCurrentRequest", current, _owner, AccountSessionState.SessionToken, _url), Is.True);
            Assert.That((bool)Call("IsCurrentRequest", current, Guid.NewGuid().ToString("D"), AccountSessionState.SessionToken, _url), Is.False);
            Assert.That((bool)Call("IsCurrentRequest", current, _owner, "old-token", _url), Is.False);
            Assert.That((bool)Call("IsCurrentRequest", current, _owner, AccountSessionState.SessionToken, _url + "/other"), Is.False);
            _shop.enabled = false;
            Assert.That((bool)Call("IsCurrentRequest", current, _owner, AccountSessionState.SessionToken, _url), Is.False);
            _shop.enabled = true;
            Call("CancelRequest");
        }

        [Test]
        public void EditorDestroy_CleansExternalRevealHost_AndKeepsServerPaidResultMarker()
        {
            Present(PaidReceipt());
            var host = Field<RectTransform>("_revealHost");
            Call("OnDestroy");
            Assert.That(host == null, Is.True);
            Assert.That(PendingCardPackPresentation.GetRequestId(_url, _owner), Is.EqualTo(_requestId));
            Assert.That(AccountSessionState.ResourceGold, Is.EqualTo(66));
        }

        private CardPackPurchaseResponse PaidReceipt() => new CardPackPurchaseResponse
        {
            RequestId = _requestId,
            Account = new AuthAccountDto { Id = _owner },
            ResourceGoldCost = 33,
            Wallet = new WalletDto { ResourceGold = 33, Tickets = 7 },
            CardIds = new[] { "Skeleton", "Skeleton", "FireSpear", "RedDragon", "OrcBerserker", "ElfElder", "GaebangChief", "ManaCannon", "Meteor", "TrapReturn" }
        };

        private void Present(CardPackPurchaseResponse receipt)
        {
            PendingCardPackPresentation.Remember(_url, _owner, receipt.RequestId);
            Call("ShowPaidCardPack", receipt, _owner, _url);
            Call("RefreshUi");
        }
        private static void Session(string name, object value) => typeof(AccountSessionState).GetProperty(name).GetSetMethod(true).Invoke(null, new[] { value });
        private object Call(string name, params object[] arguments) => typeof(ShopSceneController).GetMethod(name, Hidden).Invoke(_shop, arguments);
        private T Field<T>(string name) => (T)typeof(ShopSceneController).GetField(name, Hidden).GetValue(_shop);
        private static void Advance(ShopCardPackRevealView view, float delta) => typeof(ShopCardPackRevealView).GetMethod("Advance", Hidden).Invoke(view, new object[] { delta });
        private static void AdField(RewardedTicketController ads, string name, object value) => typeof(RewardedTicketController).GetField(name, Hidden).SetValue(ads, value);
        private static T AdField<T>(RewardedTicketController ads, string name) => (T)typeof(RewardedTicketController).GetField(name, Hidden).GetValue(ads);
        private static Rect WorldRect(RectTransform rect)
        {
            Assert.That(rect, Is.Not.Null);
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners.Min(x => x.x), corners.Min(x => x.y), corners.Max(x => x.x), corners.Max(x => x.y));
        }
        private static void AssertInside(Rect container, Rect item, string name)
        {
            Assert.That(item.width, Is.GreaterThan(0), name);
            Assert.That(item.height, Is.GreaterThan(0), name);
            Assert.That(item.xMin, Is.GreaterThanOrEqualTo(container.xMin - .01f), name);
            Assert.That(item.xMax, Is.LessThanOrEqualTo(container.xMax + .01f), name);
            Assert.That(item.yMin, Is.GreaterThanOrEqualTo(container.yMin - .01f), name);
            Assert.That(item.yMax, Is.LessThanOrEqualTo(container.yMax + .01f), name);
        }
        private sealed class ReadyAd : IRewardedAdService
        {
            public event Action StateChanged { add { } remove { } }
            public event Action Rewarded { add { } remove { } }
            public event Action Closed { add { } remove { } }
            public bool IsReady => true;
            public bool IsShowing { get; set; }
            public string StatusMessage => "";
            public void Initialize(string appKey, string providerUserId, string rewardedAdUnitId) { }
            public bool TryShow(string placementName, string dynamicUserId, out string errorMessage) { errorMessage = ""; return true; }
        }
    }
}
