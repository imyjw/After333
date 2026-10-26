using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using NUnit.Framework;
using Project333.Runtime.Presentation.Hand;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class PlayerDeckPresenterTests
    {
        [Test]
        public void PresentCount_ShowsUpToThreeCardBackLayers()
        {
            var presenterObject = CreatePresenterObject();

            try
            {
                var presenter = presenterObject.GetComponent<PlayerDeckPresenter>();

                presenter.PresentCount(12);

                Assert.That(presenter.RemainingDeckCount, Is.EqualTo(12));
                Assert.That(CountCardBackImages(presenterObject, activeOnly: false), Is.EqualTo(3));
                Assert.That(CountCardBackImages(presenterObject, activeOnly: true), Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(presenterObject);
            }
        }

        [Test]
        public void PresentCount_UsesRemainingCountForVisibleStackDepth()
        {
            var presenterObject = CreatePresenterObject();

            try
            {
                var presenter = presenterObject.GetComponent<PlayerDeckPresenter>();

                presenter.PresentCount(2);

                Assert.That(CountCardBackImages(presenterObject, activeOnly: true), Is.EqualTo(2));

                presenter.PresentCount(0);

                Assert.That(CountCardBackImages(presenterObject, activeOnly: true), Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(presenterObject);
            }
        }

        [Test]
        public void TouchingDeck_ShowsCurrentRemainingDeckCount()
        {
            var presenterObject = CreatePresenterObject();

            try
            {
                var presenter = presenterObject.GetComponent<PlayerDeckPresenter>();
                presenter.PresentCount(17);
                var button = FindChildComponent<Button>(presenterObject, "DeckTouchTarget");

                Assert.That(button, Is.Not.Null);
                button.onClick.Invoke();

                Assert.That(presenter.IsTooltipVisible, Is.True);
                Assert.That(presenter.TooltipMessage, Is.EqualTo("덱에 카드가 17장 남았습니다."));
            }
            finally
            {
                Object.DestroyImmediate(presenterObject);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RefreshPreservesAuthoredDeckCardsAndDrawSource(bool opponent)
        {
            var root = CreateAuthoredDeck(opponent);
            try { ExerciseAuthoredDeck(root.GetComponent<PlayerDeckPresenter>(), opponent); }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void OpponentHandRefreshPreservesEachAuthoredBackIncludingHiddenSlots()
        {
            var root = new GameObject("AuthoredOpponentHand", typeof(RectTransform), typeof(Canvas), typeof(OpponentHandPresenter));
            try
            {
                var hand = root.GetComponent<OpponentHandPresenter>();
                hand.PresentCount(5);
                AuthorHandBacks(root);
                ExerciseAuthoredHand(hand);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private const string SavedLayoutTestKey = "Project333.Tests.CardBackSavedLayout";

        [UnityTearDown]
        public IEnumerator CleanupSavedLayoutTest()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            var path = SessionState.GetString(SavedLayoutTestKey, "");
            if (path.StartsWith("Assets/__CardBackLayoutTest_", StringComparison.Ordinal))
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Assert.That(AssetDatabase.DeleteAsset(path), Is.True, "Generated scene must be cleaned up.");
                SessionState.EraseString(SavedLayoutTestKey);
            }
        }

        [UnityTest]
        public IEnumerator SavedDeckEditsAndOpponentBacksSurvivePlayAndReenable()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var scenePath = "Assets/__CardBackLayoutTest_" + Guid.NewGuid().ToString("N") + ".unity";
            SessionState.SetString(SavedLayoutTestKey, scenePath);
            CreateAuthoredDeck(false);
            CreateAuthoredDeck(true);
            var handObject = new GameObject("AuthoredOpponentHand", typeof(RectTransform), typeof(Canvas), typeof(OpponentHandPresenter));
            handObject.GetComponent<OpponentHandPresenter>().PresentCount(5);
            AuthorHandBacks(handObject);
            Assert.That(EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath), Is.True);
            // Reload saved references before entering Play, as the actual scene does.
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            for (var pass = 0; pass < 2; pass++)
            {
                ExerciseAuthoredDeck(GameObject.Find("AuthoredPlayerDeck").GetComponent<PlayerDeckPresenter>(), false);
                ExerciseAuthoredDeck(GameObject.Find("AuthoredOpponentDeck").GetComponent<PlayerDeckPresenter>(), true);
                ExerciseAuthoredHand(GameObject.Find("AuthoredOpponentHand").GetComponent<OpponentHandPresenter>());
                foreach (var name in new[] { "AuthoredPlayerDeck", "AuthoredOpponentDeck", "AuthoredOpponentHand" })
                {
                    var node = GameObject.Find(name); node.SetActive(false); node.SetActive(true);
                }
                yield return null;
            }
            yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Iterator locals are not retained across ExitPlayMode's domain reload.
            // UnityTearDown uses the SessionState path to delete exactly this generated scene.
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingDeckLayerAndBlankSpriteUseDefaultsWithoutResettingNeighbors(bool opponent)
        {
            var root = CreateAuthoredDeck(opponent);
            try
            {
                var prefix = opponent ? "OpponentDeckCardBack_" : "PlayerDeckCardBack_";
                Object.DestroyImmediate(FindChildComponent<Image>(root, prefix + "00").gameObject);
                FindChildComponent<Image>(root, prefix + "01").sprite = null;
                root.GetComponent<PlayerDeckPresenter>().PresentCount(3);
                var created = FindChildComponent<Image>(root, prefix + "00");
                var blank = FindChildComponent<Image>(root, prefix + "01");
                var neighbor = FindChildComponent<Image>(root, prefix + "02");
                Assert.That(created.sprite, Is.Not.Null);
                Assert.That(created.rectTransform.sizeDelta, Is.EqualTo(new Vector2(100, 133)));
                Assert.That(blank.sprite, Is.Not.Null);
                AssertAuthoredRect(blank.rectTransform, 1);
                Assert.That(neighbor.sprite, Is.EqualTo(AuthoredSprite(2)));
                AssertAuthoredRect(neighbor.rectTransform, 2);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MissingAndBlankOpponentHandBacksUseDefaultsWithoutResettingNeighbors()
        {
            var root = new GameObject("PartialOpponentHand", typeof(RectTransform), typeof(Canvas), typeof(OpponentHandPresenter));
            try
            {
                var hand = root.GetComponent<OpponentHandPresenter>();
                hand.PresentCount(5); AuthorHandBacks(root);
                Object.DestroyImmediate(FindChildComponent<Image>(root, "OpponentCardBack_00").gameObject);
                FindChildComponent<Image>(root, "OpponentCardBack_01").sprite = null;
                hand.PresentCount(5);
                Assert.That(FindChildComponent<Image>(root, "OpponentCardBack_00").sprite, Is.Not.Null);
                Assert.That(FindChildComponent<Image>(root, "OpponentCardBack_01").sprite, Is.Not.Null);
                Assert.That(FindChildComponent<Image>(root, "OpponentCardBack_02").sprite, Is.EqualTo(AuthoredSprite(2)));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject CreateAuthoredDeck(bool opponent)
        {
            var root = new GameObject(opponent ? "AuthoredOpponentDeck" : "AuthoredPlayerDeck", typeof(RectTransform), typeof(Canvas));
            var presenter = (PlayerDeckPresenter)root.AddComponent(opponent ? typeof(OpponentDeckPresenter) : typeof(PlayerDeckPresenter));
            presenter.EnsureEditableDeckUi();
            var prefix = opponent ? "OpponentDeckCardBack_" : "PlayerDeckCardBack_";
            for (var i = 0; i < 3; i++)
            {
                var image = FindChildComponent<Image>(root, prefix + i.ToString("00"));
                image.sprite = AuthoredSprite(i);
                SetAuthoredRect(image.rectTransform, i);
            }
            SetAuthoredRect(FindChildComponent<RectTransform>(root, opponent ? "OpponentDeckRoot" : "PlayerDeckRoot"), 3);
            return root;
        }

        private static void ExerciseAuthoredDeck(PlayerDeckPresenter presenter, bool opponent)
        {
            var prefix = opponent ? "OpponentDeckCardBack_" : "PlayerDeckCardBack_";
            foreach (var count in new[] { 1, 0, 33, 2, 33 })
            {
                presenter.PresentCount(count);
                presenter.EnsureEditableDeckUi();
                for (var i = 0; i < 3; i++)
                {
                    var image = FindChildComponent<Image>(presenter.gameObject, prefix + i.ToString("00"));
                    Assert.That(image.sprite, Is.EqualTo(AuthoredSprite(i)), image.name + " sprite");
                    AssertAuthoredRect(image.rectTransform, i);
                    Assert.That(image.gameObject.activeSelf, Is.EqualTo(i >= 3 - Mathf.Min(count, 3)));
                }
                AssertAuthoredRect(FindChildComponent<RectTransform>(presenter.gameObject, opponent ? "OpponentDeckRoot" : "PlayerDeckRoot"), 3);
                var touch = FindChildComponent<Button>(presenter.gameObject, "DeckTouchTarget").GetComponent<RectTransform>();
                var corners = new Vector3[4];
                foreach (var image in presenter.GetComponentsInChildren<Image>(true).Where(i => i.name.StartsWith(prefix)))
                {
                    image.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = touch.InverseTransformPoint(corner);
                        Assert.That(point.x, Is.InRange(touch.rect.xMin - .01f, touch.rect.xMax + .01f));
                        Assert.That(point.y, Is.InRange(touch.rect.yMin - .01f, touch.rect.yMax + .01f));
                    }
                }
                if (count > 0)
                {
                    Assert.That(presenter.DrawSourceRect, Is.EqualTo(FindChildComponent<Image>(presenter.gameObject, prefix + "02").rectTransform));
                    Assert.That(presenter.DrawCardBackSprite, Is.EqualTo(AuthoredSprite(2)));
                }
            }
            FindChildComponent<Button>(presenter.gameObject, "DeckTouchTarget").onClick.Invoke();
            Assert.That(presenter.IsTooltipVisible, Is.True);
        }

        private static void AuthorHandBacks(GameObject root)
        {
            var cards = root.GetComponentsInChildren<Image>(true).Where(i => i.name.StartsWith("OpponentCardBack_")).OrderBy(i => i.name).ToArray();
            for (var i = 0; i < cards.Length; i++) cards[i].sprite = AuthoredSprite(i);
        }

        private static void ExerciseAuthoredHand(OpponentHandPresenter hand)
        {
            foreach (var count in new[] { 0, 1, 5, Project333.Runtime.Domain.Battle.PlayerState.AbsoluteMaxHandSize, 2 })
            {
                hand.PresentCount(count); hand.EnsureEditableCardBackSlots();
                var cards = hand.GetComponentsInChildren<Image>(true).Where(i => i.name.StartsWith("OpponentCardBack_")).OrderBy(i => i.name).ToArray();
                for (var i = 0; i < cards.Length; i++)
                {
                    Assert.That(cards[i].sprite, Is.EqualTo(AuthoredSprite(i)), cards[i].name);
                    Assert.That(cards[i].gameObject.activeSelf, Is.EqualTo(i < count));
                    Assert.That(cards[i].raycastTarget, Is.False);
                }
                if (count >= 2) Assert.That(cards[0].rectTransform.anchoredPosition.x, Is.LessThan(cards[1].rectTransform.anchoredPosition.x), "Hand still fans as its count changes.");
            }
        }

        private static Sprite AuthoredSprite(int index)
        {
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(index % 2 == 0 ? "UI/Skin/Knob.psd" : "UI/Skin/UISprite.psd");
            Assert.That(sprite, Is.Not.Null); return sprite;
        }

        private static void SetAuthoredRect(RectTransform rect, int index)
        {
            rect.anchorMin = new Vector2(.25f, .3f); rect.anchorMax = new Vector2(.25f, .3f);
            rect.pivot = new Vector2(.35f, .6f);
            rect.anchoredPosition = new Vector2(123 + index * 17, -77 + index * 23);
            rect.sizeDelta = new Vector2(81 + index, 145 + index);
            rect.localScale = new Vector3(1.2f, .85f, 1);
            rect.localRotation = Quaternion.Euler(0, 0, 7 + index);
        }

        private static void AssertAuthoredRect(RectTransform rect, int index)
        {
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(.25f, .3f)), rect.name + " anchor");
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(.25f, .3f)));
            Assert.That(rect.pivot, Is.EqualTo(new Vector2(.35f, .6f)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(123 + index * 17, -77 + index * 23)));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(81 + index, 145 + index)));
            Assert.That(Vector3.Distance(rect.localScale, new Vector3(1.2f, .85f, 1)), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(rect.localRotation, Quaternion.Euler(0, 0, 7 + index)), Is.LessThan(.01f));
        }


        private static GameObject CreatePresenterObject()
        {
            return new GameObject(
                "PlayerDeckPresenterTest",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(PlayerDeckPresenter));
        }

        private static int CountCardBackImages(GameObject root, bool activeOnly)
        {
            var count = 0;
            foreach (var image in root.GetComponentsInChildren<Image>(includeInactive: true))
            {
                if (!image.name.StartsWith("PlayerDeckCardBack_"))
                {
                    continue;
                }

                if (!activeOnly || image.gameObject.activeSelf)
                {
                    count++;
                }
            }

            return count;
        }

        private static T FindChildComponent<T>(GameObject root, string objectName) where T : Component
        {
            foreach (var component in root.GetComponentsInChildren<T>(includeInactive: true))
            {
                if (component.name == objectName)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
