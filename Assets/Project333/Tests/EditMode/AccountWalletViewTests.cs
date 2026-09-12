using NUnit.Framework;
using Project333.Runtime.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class AccountWalletViewTests
    {
        private GameObject _root;
        private Text _host;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("WalletTest", typeof(RectTransform), typeof(Canvas));
            var child = new GameObject("WalletText", typeof(RectTransform), typeof(Text));
            child.transform.SetParent(_root.transform, false);
            _host = child.GetComponent<Text>();
            _host.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _host.fontSize = 27;
            _host.color = Color.yellow;
            _host.rectTransform.sizeDelta = new Vector2(360f, 90f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void Render_ReplacesLabelsWithTwoSpritesAndLiveNumbers()
        {
            var view = AccountWalletView.Attach(_host, false, false);
            view.Render(true, 9, 12345678901L);
            Assert.That(_host.text, Is.Empty);
            Assert.That(view.TicketValue.text, Is.EqualTo("9"));
            Assert.That(view.GoldValue.text, Is.EqualTo("12345678901"));
            foreach (var icon in _host.GetComponentsInChildren<Image>(true))
            {
                Assert.That(icon.sprite, Is.Not.Null);
                Assert.That(icon.preserveAspect, Is.True);
                Assert.That(icon.raycastTarget, Is.False);
            }
            Assert.That(_host.GetComponentsInChildren<Image>(true).Length, Is.EqualTo(2));
            view.Render(true, 6, 4);
            Assert.That(view.TicketValue.text, Is.EqualTo("6"));
            Assert.That(view.GoldValue.text, Is.EqualTo("4"));
        }

        [Test]
        public void Render_LogoutClearsPreviousBalancesButKeepsIcons()
        {
            var view = AccountWalletView.Attach(_host, false, true);
            view.Render(true, 12, 99, "Player");
            view.Render(false, 12, 99, "Login");
            Assert.That(view.TicketValue.text, Is.EqualTo("-"));
            Assert.That(view.GoldValue.text, Is.EqualTo("-"));
            Assert.That(view.AccountNameText.text, Is.EqualTo("Login"));
            Assert.That(view.AccountNameText.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void RefreshAndEnable_PreserveAuthoredNumberStyleAndHostRect()
        {
            var originalPosition = new Vector2(71f, -29f);
            _host.rectTransform.anchoredPosition = originalPosition;
            var size = _host.rectTransform.sizeDelta;
            var view = AccountWalletView.Attach(_host, false, false);
            var customFont = AssetDatabase.LoadAssetAtPath<Font>(
                "Assets/Thaleah_PixelFont/Materials/ThaleahFat_TTF.ttf");
            Assert.That(customFont, Is.Not.Null);
            view.TicketValue.font = customFont;
            view.TicketValue.fontSize = 38;
            view.TicketValue.resizeTextMaxSize = 38;
            view.TicketValue.color = Color.cyan;
            view.Render(true, 3, 500);
            _host.gameObject.SetActive(false);
            _host.gameObject.SetActive(true);
            AccountWalletView.ShowSession(_host);
            Assert.That(view.TicketValue.font, Is.SameAs(customFont));
            Assert.That(view.TicketValue.fontSize, Is.EqualTo(38));
            Assert.That(view.TicketValue.color, Is.EqualTo(Color.cyan));
            Assert.That(_host.rectTransform.anchoredPosition, Is.EqualTo(originalPosition));
            Assert.That(_host.rectTransform.sizeDelta, Is.EqualTo(size));
        }

        [TestCase(false, false, 560f, 66f)]
        [TestCase(true, false, 246f, 75f)]
        [TestCase(false, true, 600f, 100f)]
        [TestCase(false, true, 190f, 60f)]
        public void Layout_IconsAndValuesStayInsideResizedHost(bool vertical, bool heading, float width, float height)
        {
            var view = AccountWalletView.Attach(_host, vertical, heading);
            _host.rectTransform.sizeDelta = new Vector2(width, height);
            view.Render(true, 999, 9999999999L, heading ? "Name" : null);
            Canvas.ForceUpdateCanvases();
            view.ApplyLayout();
            var bounds = _host.rectTransform.rect;
            foreach (var child in _host.GetComponentsInChildren<RectTransform>())
            {
                if (child == _host.rectTransform) continue;
                var corners = new Vector3[4];
                child.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var local = _host.rectTransform.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(bounds.xMin - 0.01f, bounds.xMax + 0.01f));
                    Assert.That(local.y, Is.InRange(bounds.yMin - 0.01f, bounds.yMax + 0.01f));
                }
            }
        }

        [Test]
        public void RepeatedAttach_DoesNotDuplicateChildren()
        {
            for (var i = 0; i < 5; i++)
                AccountWalletView.Attach(_host, true, false).Render(true, i, i);
            Assert.That(_host.GetComponents<AccountWalletView>().Length, Is.EqualTo(1));
            Assert.That(_host.transform.childCount, Is.EqualTo(4));
        }

        [Test]
        public void WalletWithoutHeading_DoesNotRecreateAccountNameAfterEnableOrRefresh()
        {
            var view = AccountWalletView.Attach(_host, false, false);
            _host.gameObject.SetActive(false);
            _host.gameObject.SetActive(true);
            AccountWalletView.ShowSession(_host);
            view.Render(true, 9, 30);
            Assert.That(view.AccountNameText, Is.Null);
            Assert.That(_host.transform.Find("AccountNameText"), Is.Null);
            Assert.That(view.CurrencyAreaFraction, Is.EqualTo(1f));
            Assert.That(view.TicketValue.text, Is.EqualTo("9"));
            Assert.That(view.GoldValue.text, Is.EqualTo("30"));
        }

        [TestCase(1920f, 135f)]
        [TestCase(1280f, 135f)]
        [TestCase(960f, 135f)]
        [TestCase(1920f, 90f)]
        public void HeaderLayout_AlignsWalletLeftOfBackAndAvoidsTitle(float width, float height)
        {
            var header = new GameObject("Header", typeof(RectTransform)).GetComponent<RectTransform>();
            header.SetParent(_root.transform, false);
            header.sizeDelta = new Vector2(width, height);
            _host.transform.SetParent(header, false);
            var title = new GameObject("Title", typeof(RectTransform)).GetComponent<RectTransform>();
            title.SetParent(header, false);
            title.anchorMin = new Vector2(0.025f, 0.25f);
            title.anchorMax = new Vector2(0.35f, 0.85f);
            title.offsetMin = title.offsetMax = Vector2.zero;
            var back = new GameObject("Back", typeof(RectTransform)).GetComponent<RectTransform>();
            back.SetParent(header, false);
            back.anchorMin = back.anchorMax = new Vector2(1f, 0.5f);
            back.pivot = new Vector2(1f, 0.5f);
            back.sizeDelta = new Vector2(235f, 65f);
            back.anchoredPosition = new Vector2(-130f, 0f);
            var view = AccountWalletView.Attach(_host, false, false);
            view.TicketValue.fontSize = 31;
            view.GoldValue.color = Color.cyan;
            var layout = _host.gameObject.AddComponent<AccountWalletHeaderLayout>();
            layout.Configure(back, title);
            AssertHeaderBounds(header, title, back);
            Assert.That(back.sizeDelta, Is.EqualTo(new Vector2(235f, 65f)));
            Assert.That(back.anchoredPosition, Is.EqualTo(new Vector2(-130f, 0f)));
            if (height >= 135f)
            {
                foreach (var icon in _host.GetComponentsInChildren<Image>())
                    Assert.That(BoundsIn(header, icon.rectTransform).center.y,
                        Is.EqualTo(BoundsIn(header, back).center.y).Within(0.01f));
            }

            header.sizeDelta += new Vector2(200f, 20f);
            back.anchoredPosition += new Vector2(-35f, -5f);
            layout.ApplyLayout();
            AssertHeaderBounds(header, title, back);
            view.Render(true, 7, 99, "Account");
            Assert.That(view.TicketValue.fontSize, Is.EqualTo(31));
            Assert.That(view.GoldValue.color, Is.EqualTo(Color.cyan));
        }

        private void AssertHeaderBounds(RectTransform header, RectTransform title, RectTransform back)
        {
            var walletBounds = BoundsIn(header, _host.rectTransform);
            Assert.That(walletBounds.xMax, Is.EqualTo(BoundsIn(header, back).xMin - 24f).Within(0.01f));
            Assert.That(walletBounds.xMin, Is.GreaterThanOrEqualTo(BoundsIn(header, title).xMax + 23.99f));
            Assert.That(walletBounds.width, Is.InRange(1f, 360.01f));
            Assert.That(walletBounds.yMin, Is.GreaterThanOrEqualTo(header.rect.yMin + 7.99f));
            Assert.That(walletBounds.yMax, Is.LessThanOrEqualTo(header.rect.yMax - 7.99f));
        }

        private static Rect BoundsIn(RectTransform parent, RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var bottomLeft = parent.InverseTransformPoint(corners[0]);
            var topRight = parent.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
        }

        [TestCase(AccountWalletView.TicketResourcePath)]
        [TestCase(AccountWalletView.GoldResourcePath)]
        public void WalletSprite_IsFullRectUncompressedPixelSprite(string resourcePath)
        {
            var sprite = Resources.Load<Sprite>(resourcePath);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(128f, 128f)));
            var path = AssetDatabase.GetAssetPath(sprite);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        }

        [TestCase("GameStart_VSlice")]
        [TestCase("DeckBuilding_VSlice")]
        [TestCase("Draft_VSlice")]
        [TestCase("Shop_VSlice")]
        [TestCase("OwnedCards_VSlice")]
        public void SavedScene_HasWalletIconsBeforePlayMode(string sceneName)
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/" + sceneName + ".unity");
            try
            {
                var count = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    // Preview scenes have no display, so saved overlay canvases retain zero scale.
                    foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                    {
                        if (!canvas.isRootCanvas) continue;
                        canvas.renderMode = RenderMode.WorldSpace;
                        var canvasRect = (RectTransform)canvas.transform;
                        canvasRect.localScale = Vector3.one;
                        canvasRect.sizeDelta = new Vector2(1920f, 1080f);
                        canvasRect.ForceUpdateRectTransforms();
                    }
                    foreach (var view in root.GetComponentsInChildren<AccountWalletView>(true))
                    {
                        count++;
                        Assert.That(view.GetComponent<Text>().text, Is.Empty);
                        Assert.That(view.transform.Find("ServerTicketIcon").GetComponent<Image>().sprite, Is.Not.Null);
                        Assert.That(view.transform.Find("ServerGoldIcon").GetComponent<Image>().sprite, Is.Not.Null);
                        Assert.That(view.TicketValue, Is.Not.Null);
                        Assert.That(view.GoldValue, Is.Not.Null);
                        if (sceneName == "Shop_VSlice" || sceneName == "OwnedCards_VSlice")
                        {
                            Assert.That(view.transform.Find("AccountNameText"), Is.Null);
                            Assert.That(view.AccountNameText, Is.Null);
                            Assert.That(view.CurrencyAreaFraction, Is.EqualTo(1f));
                            var layout = view.GetComponent<AccountWalletHeaderLayout>();
                            Assert.That(layout, Is.Not.Null);
                            var serialized = new SerializedObject(layout);
                            var back = serialized.FindProperty("_backButton").objectReferenceValue as RectTransform;
                            var title = serialized.FindProperty("_title").objectReferenceValue as RectTransform;
                            Assert.That(back, Is.Not.Null);
                            Assert.That(title, Is.Not.Null);
                            layout.ApplyLayout();
                            var parent = (RectTransform)view.transform.parent;
                            Assert.That(BoundsIn(parent, (RectTransform)view.transform).xMax,
                                Is.EqualTo(BoundsIn(parent, back).xMin - 24f).Within(0.01f));
                        }
                    }
                }
                Assert.That(count, Is.EqualTo(1));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
