using NUnit.Framework;
using Project333.Runtime.Presentation.Hand;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class HandCardStatOverlayLayoutTests
    {
        [TestCase(150f, 250f, 0f)]
        [TestCase(550f, 733f, 0.5f)]
        [TestCase(600f, 300f, 1f)]
        [TestCase(240f, 600f, 0.08f)]
        [TestCase(300f, 400f, 0.5f)]
        public void ImageLayout_MatchesDrawnQuadAndTracksFooterAcrossDifferentParents(float width, float height, float pivot)
        {
            var root = new GameObject("StatLayoutCanvas", typeof(RectTransform), typeof(Canvas));
            var texture = new Texture2D(3, 4);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 3, 4), Vector2.one * 0.5f,
                100, 0, SpriteMeshType.FullRect);
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var artwork = new GameObject("Artwork", typeof(RectTransform), typeof(CardStatImageProbe));
                artwork.transform.SetParent(root.transform, false);
                var image = artwork.GetComponent<CardStatImageProbe>();
                image.sprite = sprite;
                image.preserveAspect = true;
                image.rectTransform.sizeDelta = new Vector2(width, height);
                image.rectTransform.pivot = new Vector2(pivot, pivot);
                image.rectTransform.anchoredPosition = new Vector2(60, 40);
                image.rectTransform.localRotation = Quaternion.Euler(0, 0, 15);
                image.rectTransform.localScale = Vector3.one * 1.3f;
                var band = new GameObject("LegacyStatBand", typeof(RectTransform));
                band.transform.SetParent(root.transform, false);
                var bandRect = band.GetComponent<RectTransform>();
                bandRect.sizeDelta = new Vector2(80, 20);
                bandRect.anchoredPosition = new Vector2(100, 130);
                var label = new GameObject("AttackValueText", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(band.transform, false);
                var text = label.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 37;
                text.text = "25";
                text.rectTransform.sizeDelta = new Vector2(80, 60);
                var font = text.font;

                for (var pass = 0; pass < 2; pass++)
                {
                    var drawn = image.DrawnRect();
                    var calculated = HandCardStatOverlayLayout.CalculateRenderedSpriteRect(image);
                    Assert.That(Vector2.Distance(drawn.min, calculated.min), Is.LessThan(0.01f));
                    Assert.That(Vector2.Distance(drawn.max, calculated.max), Is.LessThan(0.01f));
                    foreach (var anchor in new[] { HandCardStatOverlayLayout.DefaultAttackPosition, HandCardStatOverlayLayout.DefaultHpPosition })
                    {
                        HandCardStatOverlayLayout.PositionStatText(text.rectTransform, image.rectTransform, calculated, anchor);
                        var expected = image.rectTransform.TransformPoint(new Vector2(
                            drawn.xMin + drawn.width * anchor.x, drawn.yMin + drawn.height * anchor.y));
                        var actual = text.rectTransform.TransformPoint(text.rectTransform.rect.center);
                        Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.01f));
                    }
                    image.rectTransform.sizeDelta *= 0.7f;
                    image.rectTransform.localRotation = Quaternion.Euler(0, 0, -22);
                }
                Assert.That(text.font, Is.SameAs(font));
                Assert.That(text.fontSize, Is.EqualTo(37));
                Assert.That(text.text, Is.EqualTo("25"));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

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

    public sealed class CardStatImageProbe : Image
    {
        public Rect DrawnRect()
        {
            using (var mesh = new VertexHelper())
            {
                base.OnPopulateMesh(mesh);
                var vertex = new UIVertex();
                mesh.PopulateUIVertex(ref vertex, 0);
                var min = (Vector2)vertex.position;
                var max = min;
                for (var i = 1; i < mesh.currentVertCount; i++)
                {
                    mesh.PopulateUIVertex(ref vertex, i);
                    min = Vector2.Min(min, vertex.position);
                    max = Vector2.Max(max, vertex.position);
                }
                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
        }
    }
}
