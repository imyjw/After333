using System.Linq;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class ShopEditableLayoutTests
    {
        private Scene _scene;
        private ShopSceneController _shop;

        [SetUp]
        public void OpenSavedShop()
        {
            _scene = EditorSceneManager.OpenPreviewScene("Assets/Shop_VSlice.unity");
            _shop = _scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ShopSceneController>(true)).Single();
        }

        [TearDown]
        public void CloseSavedShop() => EditorSceneManager.ClosePreviewScene(_scene);

        [Test]
        public void SavedScene_ContainsAllThreeProductsWithoutRuntimeInitialization()
        {
            Assert.That(Application.isPlaying, Is.False);
            var pack = _shop.transform.Find("CardPackProductPanel");
            Assert.That(pack, Is.Not.Null, "The card pack must be saved in the scene, not created only after Play.");
            Assert.That(pack.Find("PurchaseCardPackButton").GetComponent<Button>(), Is.Not.Null);
            Assert.That(pack.Find("CardPackArtwork").GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(pack.Find("CardPackPrice").GetComponent<Text>().text, Is.EqualTo("골드 33"));
            Assert.That(((RectTransform)_shop.transform.Find("TicketProductPanel")).anchorMax.x, Is.EqualTo(.335f).Within(.001f));
            Assert.That(((RectTransform)_shop.transform.Find("RewardedTicketProductPanel")).anchorMin.x, Is.EqualTo(.665f).Within(.001f));
            Assert.That(_shop.ShopVisualVersion, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void RuntimeInitialization_PreservesAuthoredLayoutIconAndSoundComponents()
        {
            var pack = _shop.transform.Find("CardPackProductPanel");
            Assert.That(pack, Is.Not.Null);
            var button = pack.Find("PurchaseCardPackButton").GetComponent<Button>();
            var sound = button.gameObject.AddComponent<ButtonTouchSound>();
            var buttonRect = (RectTransform)button.transform;
            buttonRect.anchorMin = new Vector2(.12f, .11f);
            var title = _shop.transform.Find("TicketProductPanel/ProductTitleText").GetComponent<Text>();
            title.fontSize = 31;
            var icon = _shop.transform.Find("TicketProductPanel/TicketIllustration").GetComponent<Image>();
            var sprite = icon.sprite;
            var components = _shop.GetComponentsInChildren<Transform>(true).Length;
            for (var i = 0; i < 3; i++)
                typeof(ShopSceneController).GetMethod("EnsureShopProducts", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_shop, null);
            Assert.That(_shop.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(components));
            Assert.That(button, Is.SameAs(pack.Find("PurchaseCardPackButton").GetComponent<Button>()));
            Assert.That(button.GetComponent<ButtonTouchSound>(), Is.SameAs(sound));
            Assert.That(buttonRect.anchorMin, Is.EqualTo(new Vector2(.12f, .11f)));
            Assert.That(title.fontSize, Is.EqualTo(31));
            Assert.That(icon.sprite, Is.SameAs(sprite));
        }

        [Test]
        public void ReopeningSerializedScene_StillHasEditablePackAndUserTicketIcons()
        {
            var ticket = Resources.Load<Sprite>(AccountWalletView.TicketResourcePath);
            foreach (var name in new[] { "TicketProductPanel", "RewardedTicketProductPanel" })
            {
                var image = _shop.transform.Find(name + "/TicketIllustration").GetComponent<Image>();
                Assert.That(image.sprite, Is.SameAs(ticket));
                Assert.That(image.preserveAspect, Is.True);
                Assert.That(image.color, Is.EqualTo(Color.white));
                Assert.That(image.GetComponentInChildren<ButtonTouchSound>(true), Is.Null);
            }
            var serialized = new SerializedObject(_shop);
            Assert.That(serialized.FindProperty("_purchaseTicketButton").objectReferenceValue, Is.Not.Null);
            Assert.That(_shop.transform.Find("CardPackProductPanel/PurchaseCardPackButton").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void EditorUpgrade_PreservesDirtySceneAndGoldUpdateDoesNotDowngradeIt()
        {
            const string path = "Assets/Shop_VSlice.unity";
            var savedScene = File.ReadAllBytes(path);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var shop = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ShopSceneController>(true)).Single();
                var heading = shop.transform.Find("ShopHeading").GetComponent<Text>();
                heading.text = "Unsaved custom heading";
                var ticketIcon = shop.transform.Find("TicketProductPanel/TicketIllustration").GetComponent<Image>();
                var sprite = ticketIcon.sprite;
                var button = shop.transform.Find("TicketProductPanel/PurchaseTicketButton").GetComponent<Button>();
                var sound = button.GetComponent<ButtonTouchSound>();
                Assert.That(sound, Is.Not.Null, "Preserve the user's existing ticket-button sound.");
                var serialized = new SerializedObject(shop);
                serialized.FindProperty("_shopVisualVersion").intValue = 2;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);

                Project333.Editor.Project333ShopVisualBuilder.UpdateCurrentShopLayout();
                Assert.That(scene.isDirty, Is.True);
                Assert.That(heading.text, Is.EqualTo("Unsaved custom heading"));
                Assert.That(ticketIcon.sprite, Is.SameAs(sprite));
                Assert.That(button.GetComponent<ButtonTouchSound>(), Is.SameAs(sound));
                Assert.That(shop.transform.Cast<Transform>().Count(x => x.name == "CardPackProductPanel"), Is.EqualTo(1));
                CollectionAssert.AreEqual(savedScene, File.ReadAllBytes(path), "Do not save over an already-dirty scene.");
                Project333.Editor.Project333ShopVisualBuilder.UpdateShopGoldIcon();
                Assert.That(shop.ShopVisualVersion, Is.EqualTo(3));
                CollectionAssert.AreEqual(savedScene, File.ReadAllBytes(path));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
