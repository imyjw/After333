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
