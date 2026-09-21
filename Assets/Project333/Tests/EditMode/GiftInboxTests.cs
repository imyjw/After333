using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Startup;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class GiftInboxTests
    {
        [TestCase("{\"Gifts\":[{\"GiftId\":\"gift-1\",\"Title\":\"선물\",\"Tickets\":3,\"ResourceGold\":4000000000,\"Status\":\"unclaimed\",\"ExpiresAtUtc\":null}],\"UnclaimedCount\":4000000000,\"NextCursor\":\"next\"}")]
        [TestCase("{\"gifts\":[{\"giftId\":\"gift-1\",\"title\":\"선물\",\"tickets\":3,\"resourceGold\":4000000000,\"status\":\"unclaimed\",\"expiresAtUtc\":null}],\"unclaimedCount\":4000000000,\"nextCursor\":\"next\"}")]
        public void InboxParsesBothApiCasingsAndLongAmounts(string json)
        {
            var response = JsonUtility.FromJson<GiftInboxResponse>(json);
            Assert.That(response.ResolvedUnclaimedCount, Is.EqualTo(4000000000L));
            Assert.That(response.ResolvedNextCursor, Is.EqualTo("next"));
            var gift = response.ResolvedGifts.Single();
            Assert.That(gift.ResolvedGiftId, Is.EqualTo("gift-1"));
            Assert.That(gift.ResolvedTitle, Is.EqualTo("선물"));
            Assert.That(gift.ResolvedTickets, Is.EqualTo(3));
            Assert.That(gift.ResolvedResourceGold, Is.EqualTo(4000000000L));
            Assert.That(gift.CanClaim, Is.True);
            Assert.That(GiftInboxController.BuildDateText(gift), Is.EqualTo("수령 기한 없음"));
        }

        [TestCase("claimed")]
        [TestCase("expired")]
        [TestCase("unknown")]
        [TestCase(null)]
        public void OnlyExplicitUnclaimedStatusEnablesClaim(string status) =>
            Assert.That(new GiftInboxItemDto { Status = status }.CanClaim, Is.False);

        [TestCase("owner-b", "token-a", "https://server-a")]
        [TestCase("owner-a", "token-b", "https://server-a")]
        [TestCase("owner-a", "token-a", "https://server-b")]
        [TestCase("", "", "https://server-a")]
        public void PendingResponsesAreRejectedAfterAccountSessionOrEndpointSwitch(string account, string token, string server)
        {
            var scope = new GiftInboxAccountScope("owner-a", "token-a", "https://server-a");
            Assert.That(scope.Matches(account, token, server), Is.False);
            Assert.That(scope.Matches("owner-a", "token-a", "https://server-a"), Is.True);
        }

        [Test]
        public void WalletApplicationRejectsResponsesOwnedByAnotherAccountOrToken()
        {
            var oldAccount = AccountSessionState.AccountId;
            var oldToken = AccountSessionState.SessionToken;
            var oldGold = AccountSessionState.ResourceGold;
            var oldTickets = AccountSessionState.Tickets;
            try
            {
                SetAccountProperty("AccountId", "current-account");
                SetAccountProperty("SessionToken", "current-token");
                SetAccountProperty("ResourceGold", 17L);
                SetAccountProperty("Tickets", 2);
                var wallet = new WalletDto { ResourceGold = 100, Tickets = 5 };
                Assert.That(AccountSessionState.ApplyGiftWallet("old-account", "current-token", wallet), Is.False);
                Assert.That(AccountSessionState.ApplyGiftWallet("current-account", "old-token", wallet), Is.False);
                Assert.That(AccountSessionState.ResourceGold, Is.EqualTo(17));
                Assert.That(AccountSessionState.Tickets, Is.EqualTo(2));
                Assert.That(AccountSessionState.ApplyGiftWallet("current-account", "current-token", wallet), Is.True);
                Assert.That(AccountSessionState.ResourceGold, Is.EqualTo(100));
                Assert.That(AccountSessionState.Tickets, Is.EqualTo(5));
            }
            finally
            {
                SetAccountProperty("AccountId", oldAccount);
                SetAccountProperty("SessionToken", oldToken);
                SetAccountProperty("ResourceGold", oldGold);
                SetAccountProperty("Tickets", oldTickets);
            }
        }

        [Test]
        public void GiftUiUsesSafeAreaAndShowsPlainTextWithoutInterpretingAdminMarkup()
        {
            var root = new GameObject("GiftInboxTests", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var controller = root.AddComponent<GiftInboxController>();
                controller.EnsureEditableHierarchy();
                controller.EnsureEditableHierarchy();
                Assert.That(root.GetComponentsInChildren<SafeAreaFitter>(true).Length, Is.EqualTo(1));
                var giftButton = (RectTransform)root.transform.Find("SafeAreaRoot/GiftButton");
                Assert.That(giftButton.anchoredPosition, Is.EqualTo(new Vector2(-126f, -24f)));
                Assert.That(giftButton.sizeDelta, Is.EqualTo(new Vector2(86f, 86f)));
                controller.SetEditorPreview(new[]
                {
                    new GiftInboxItemDto { GiftId = "unclaimed", Title = "<size=999>선물</size>", Message = "<color=red>본문</color>", Tickets = 3, ResourceGold = 100, Status = "unclaimed" },
                    new GiftInboxItemDto { GiftId = "claimed", Title = "받은 선물", Status = "claimed" },
                    new GiftInboxItemDto { GiftId = "expired", Title = "만료 선물", Status = "expired", ExpiresAtUtc = "2020-01-01T00:00:00Z" }
                }, 1);
                var content = root.transform.Find("SafeAreaRoot/GiftModal/Panel/Scroll/Viewport/Content");
                Assert.That(content.childCount, Is.EqualTo(2));
                var title = content.GetChild(0).Find("Title").GetComponent<Text>();
                Assert.That(title.supportRichText, Is.False);
                Assert.That(title.text, Is.EqualTo("<size=999>선물</size>"));
                Assert.That(content.GetComponentsInChildren<Text>(true).All(x => !x.supportRichText), Is.True);
                Assert.That(content.GetChild(0).Find("Footer/ClaimButton").GetComponent<Button>().interactable, Is.True);
                Assert.That(content.GetChild(1).Find("Footer/ClaimButton").GetComponent<Button>().interactable, Is.False);
                Assert.That(content.Find("Gift_claimed"), Is.Null);
                Assert.That(root.transform.Find("SafeAreaRoot/GiftButton/Badge").gameObject.activeSelf, Is.True);
                typeof(GiftInboxController).GetMethod("ResetAccount", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);
                Assert.That(content.childCount, Is.EqualTo(0));
                Assert.That(root.transform.Find("SafeAreaRoot/GiftModal").gameObject.activeSelf, Is.False);
                Assert.That(root.transform.Find("SafeAreaRoot/GiftButton/Badge").gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase("{\"GiftId\":\"gift\",\"AlreadyClaimed\":true,\"Wallet\":{\"ResourceGold\":1200,\"Tickets\":7}}")]
        [TestCase("{\"giftId\":\"gift\",\"alreadyClaimed\":true,\"wallet\":{\"resourceGold\":1200,\"tickets\":7}}")]
        public void ClaimReplayParsesAuthoritativeWallet(string json)
        {
            var result = GiftInboxClient.ParseClaimResponse(json);
            Assert.That(result.ResolvedGiftId, Is.EqualTo("gift"));
            Assert.That(result.ResolvedAlreadyClaimed, Is.True);
            Assert.That(result.ResolvedWallet.ResolvedResourceGold, Is.EqualTo(1200));
            Assert.That(result.ResolvedWallet.ResolvedTickets, Is.EqualTo(7));
        }

        [Test]
        public void GiftHierarchyCreatesEveryRequiredComponentFromEmptyGameObject()
        {
            var root = new GameObject("EmptyGiftInboxRoot");
            root.SetActive(false);
            try
            {
                var controller = root.AddComponent<GiftInboxController>();
                Assert.DoesNotThrow(() => controller.EnsureEditableHierarchy());
                Assert.That(root.GetComponent<RectTransform>() != null, Is.True);
                Assert.That(root.GetComponent<Canvas>() != null, Is.True);
                Assert.That(root.GetComponent<CanvasScaler>() != null, Is.True);
                Assert.That(root.GetComponent<GraphicRaycaster>() != null, Is.True);
                Assert.That(root.GetComponentsInChildren<ScrollRect>(true).Length, Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<ContentSizeFitter>(true).Length, Is.EqualTo(1));
                var imageCount = root.GetComponentsInChildren<Image>(true).Length;
                var textCount = root.GetComponentsInChildren<Text>(true).Length;
                Assert.That(imageCount, Is.GreaterThan(8));
                Assert.That(textCount, Is.GreaterThan(5));
                Assert.DoesNotThrow(() => controller.EnsureEditableHierarchy());
                Assert.That(root.GetComponents<Canvas>().Length, Is.EqualTo(1));
                Assert.That(root.GetComponents<CanvasScaler>().Length, Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<Image>(true).Length, Is.EqualTo(imageCount));
                Assert.That(root.GetComponentsInChildren<Text>(true).Length, Is.EqualTo(textCount));
                Assert.That(root.GetComponentsInChildren<SafeAreaFitter>(true).Length, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }
        [TestCase("{\"GiftId\":\"gift\",\"Wallet\":{\"ResourceGold\":0,\"Tickets\":0}}", 0, 0)]
        [TestCase("{\"giftId\":\"gift\",\"wallet\":{\"resourceGold\":0,\"tickets\":0}}", 0, 0)]
        [TestCase("{\"giftId\":\"gift\",\"wallet\":{\"resourceGold\":0,\"tickets\":7}}", 0, 7)]
        [TestCase("{\"giftId\":\"gift\",\"wallet\":{\"resourceGold\":1200,\"tickets\":0}}", 1200, 0)]
        public void ClaimParserPreservesValidZeroBalances(string json, long gold, int tickets)
        {
            var result = GiftInboxClient.ParseClaimResponse(json);
            Assert.That(result.ResolvedWallet.ResolvedResourceGold, Is.EqualTo(gold));
            Assert.That(result.ResolvedWallet.ResolvedTickets, Is.EqualTo(tickets));
        }

        [TestCase("{\"GiftId\":\"gift\"}")]
        [TestCase("{\"giftId\":\"gift\",\"wallet\":null}")]
        [TestCase("{\"GiftId\":\"gift\",\"Wallet\":{}}")]
        [TestCase("{\"GiftId\":\"gift\",\"Wallet\":{\"ResourceGold\":0}}")]
        [TestCase("{\"GiftId\":\"gift\",\"Wallet\":{\"Tickets\":0}}")]
        [TestCase("{\"GiftId\":\"gift\",\"Other\":{\"Wallet\":{\"ResourceGold\":1,\"Tickets\":2}}}")]
        [TestCase("{\"GiftId\":\"gift\",\"Wallet\":{\"ResourceGold\":-1,\"Tickets\":2}}")]
        [TestCase("{\"GiftId\":\"gift\",\"Wallet\":{\"ResourceGold\":1,\"Tickets\":2},\"wallet\":{\"resourceGold\":3,\"tickets\":4}}")]
        public void ClaimParserRejectsAbsentOrInvalidWalletWithoutTouchingAccount(string json)
        {
            var gold = AccountSessionState.ResourceGold;
            var tickets = AccountSessionState.Tickets;
            var error = Assert.Throws<GiftInboxException>(() => GiftInboxClient.ParseClaimResponse(json));
            Assert.That(error.Code, Is.EqualTo("invalid_response"));
            Assert.That(AccountSessionState.ResourceGold, Is.EqualTo(gold));
            Assert.That(AccountSessionState.Tickets, Is.EqualTo(tickets));
        }
        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator RebuildingSameGiftInOneFrameKeepsVisibleRowsAndClaimControls()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            yield return new UnityEngine.TestTools.EnterPlayMode();
            var root = new GameObject("GiftRuntimeRefreshTest", typeof(RectTransform));
            var controller = root.AddComponent<GiftInboxController>();
            controller.enabled = false; // No account polling or HTTP; exercise real runtime destruction only.
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var items = (System.Collections.Generic.List<GiftInboxItemDto>)typeof(GiftInboxController)
                    .GetField("_items", flags).GetValue(controller);
                var gift = new GiftInboxItemDto { GiftId = "repeat", Title = "Test gift", Tickets = 1, Status = "unclaimed" };
                items.Add(gift);
                var modal = root.transform.Find("SafeAreaRoot/GiftModal");
                modal.gameObject.SetActive(true);
                var content = modal.Find("Panel/Scroll/Viewport/Content");
                var build = typeof(GiftInboxController).GetMethod("BuildRows", flags);
                // RefreshAsync renders once in try and again in finally before Unity destroys old rows.
                build.Invoke(controller, null);
                build.Invoke(controller, null);
                yield return null;
                Assert.That(content.childCount, Is.EqualTo(1));
                var row = content.GetChild(0);
                Assert.That(row.gameObject.activeInHierarchy, Is.True);
                Assert.That(row.Find("Title").GetComponent<Text>().text, Is.EqualTo("Test gift"));
                Assert.That(row.Find("Footer/ClaimButton").GetComponent<Button>().interactable, Is.True);
                gift.Status = "claimed";
                build.Invoke(controller, null);
                build.Invoke(controller, null);
                yield return null;
                Assert.That(content.childCount, Is.EqualTo(0));
            }
            finally { Object.DestroyImmediate(root); }
            yield return new UnityEngine.TestTools.ExitPlayMode();
        }

        [UnityEngine.TestTools.UnityTearDown]
        public System.Collections.IEnumerator LeaveRuntimeAfterRegression()
        {
            if (UnityEngine.Application.isPlaying) yield return new UnityEngine.TestTools.ExitPlayMode();
        }
        private static void SetAccountProperty(string name, object value) =>
            typeof(AccountSessionState).GetProperty(name, BindingFlags.Public | BindingFlags.Static).SetValue(null, value);
    }
}
