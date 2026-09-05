using NUnit.Framework;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Hand;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class OpponentHandPresenterTests
    {
        [Test]
        public void PresentCount_CreatesHiddenCardBackSlotsAndShowsRequestedCount()
        {
            var presenterObject = new GameObject(
                "OpponentHandPresenterTest",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(OpponentHandPresenter));

            try
            {
                var presenter = presenterObject.GetComponent<OpponentHandPresenter>();

                presenter.PresentCount(5);

                var cardBackImages = presenterObject.GetComponentsInChildren<Image>(includeInactive: true);
                var activeCardBackCount = 0;
                foreach (var image in cardBackImages)
                {
                    Assert.That(image.raycastTarget, Is.False);
                    if (image.gameObject.activeSelf)
                    {
                        activeCardBackCount++;
                    }
                }

                Assert.That(cardBackImages.Length, Is.EqualTo(PlayerState.AbsoluteMaxHandSize));
                Assert.That(activeCardBackCount, Is.EqualTo(5));
                Assert.That(presenter.VisibleCardCount, Is.EqualTo(5));
            }
            finally
            {
                Object.DestroyImmediate(presenterObject);
            }
        }

        [Test]
        public void PresentCount_ClampsToAbsoluteMaximumHandSize()
        {
            var presenterObject = new GameObject(
                "OpponentHandPresenterClampTest",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(OpponentHandPresenter));

            try
            {
                var presenter = presenterObject.GetComponent<OpponentHandPresenter>();

                presenter.PresentCount(PlayerState.AbsoluteMaxHandSize + 10);

                Assert.That(presenter.VisibleCardCount, Is.EqualTo(PlayerState.AbsoluteMaxHandSize));
            }
            finally
            {
                Object.DestroyImmediate(presenterObject);
            }
        }
    }
}
