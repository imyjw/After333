using NUnit.Framework;
using Project333.Runtime.Presentation.Hand;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class OpponentDeckPresenterTests
    {
        [Test]
        public void PresentCount_CreatesTopRightOpponentDeckStackBelowGear()
        {
            var presenterObject = CreatePresenterObject();

            try
            {
                var presenter = presenterObject.GetComponent<OpponentDeckPresenter>();

                presenter.PresentCount(12);

                var deckRoot = FindChildComponent<RectTransform>(presenterObject, "OpponentDeckRoot");
                Assert.That(deckRoot, Is.Not.Null);
                Assert.That(deckRoot.anchorMin, Is.EqualTo(Vector2.one));
                Assert.That(deckRoot.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(deckRoot.pivot, Is.EqualTo(Vector2.one));
                Assert.That(deckRoot.anchoredPosition, Is.EqualTo(new Vector2(-24f, -126f)));
                Assert.That(CountActiveCardBackImages(presenterObject), Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(presenterObject);
            }
        }

        [Test]
        public void TouchingOpponentDeck_ShowsOpponentRemainingDeckCount()
        {
            var presenterObject = CreatePresenterObject();

            try
            {
                var presenter = presenterObject.GetComponent<OpponentDeckPresenter>();
                presenter.PresentCount(17);
                var button = FindChildComponent<Button>(presenterObject, "DeckTouchTarget");

                Assert.That(button, Is.Not.Null);
                button.onClick.Invoke();

                Assert.That(presenter.IsTooltipVisible, Is.True);
                Assert.That(presenter.TooltipMessage, Is.EqualTo("상대방 덱에 카드가 17장 남았습니다."));
                var tooltip = FindChildComponent<RectTransform>(presenterObject, "DeckCountTooltip");
                Assert.That(tooltip.pivot, Is.EqualTo(Vector2.one));
            }
            finally
            {
                Object.DestroyImmediate(presenterObject);
            }
        }

        private static GameObject CreatePresenterObject()
        {
            return new GameObject(
                "OpponentDeckPresenterTest",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(OpponentDeckPresenter));
        }

        private static int CountActiveCardBackImages(GameObject root)
        {
            var count = 0;
            foreach (var image in root.GetComponentsInChildren<Image>(includeInactive: true))
            {
                if (image.name.StartsWith("OpponentDeckCardBack_") && image.gameObject.activeSelf)
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
