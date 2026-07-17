using NUnit.Framework;
using Project333.Runtime.Presentation.Hand;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class HandCardStatOverlayLayoutTests
    {
        [Test]
        public void CalculateRenderedSpriteRect_PortraitSpriteInWideCard_CentersRenderedWidth()
        {
            var containerRect = new Rect(-150f, -187.5f, 300f, 375f);

            var result = HandCardStatOverlayLayout.CalculateRenderedSpriteRect(
                containerRect,
                new Vector2(1086f, 1448f),
                preserveAspect: true);

            Assert.That(result.width, Is.EqualTo(281.25f).Within(0.001f));
            Assert.That(result.height, Is.EqualTo(375f).Within(0.001f));
            Assert.That(result.center, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void CalculateContainerAnchor_UsesRenderedCardInsteadOfUnusedSideMargins()
        {
            var containerRect = new Rect(-150f, -187.5f, 300f, 375f);
            var renderedSpriteRect = new Rect(-140.625f, -187.5f, 281.25f, 375f);
            var attackPoint = HandCardStatOverlayLayout.CalculateRenderedSpritePoint(
                renderedSpriteRect,
                new Vector2(0.07f, 0.09f));
            var hpPoint = HandCardStatOverlayLayout.CalculateRenderedSpritePoint(
                renderedSpriteRect,
                new Vector2(0.92f, 0.09f));

            var attackAnchor = HandCardStatOverlayLayout.CalculateContainerAnchor(containerRect, attackPoint);
            var hpAnchor = HandCardStatOverlayLayout.CalculateContainerAnchor(containerRect, hpPoint);

            Assert.That(attackAnchor.x, Is.EqualTo(0.096875f).Within(0.0001f));
            Assert.That(attackAnchor.y, Is.EqualTo(0.09f).Within(0.0001f));
            Assert.That(hpAnchor.x, Is.EqualTo(0.89375f).Within(0.0001f));
            Assert.That(hpAnchor.y, Is.EqualTo(0.09f).Within(0.0001f));
        }

        [Test]
        public void CalculateScaledFontSize_CurrentHandCardSize_ScalesLegacyTemplateFont()
        {
            var result = HandCardStatOverlayLayout.CalculateScaledFontSize(
                templateFontSize: 22,
                renderedCardHeight: 375f,
                referenceCardHeight: 250f);

            Assert.That(result, Is.EqualTo(33));
        }
    }
}
