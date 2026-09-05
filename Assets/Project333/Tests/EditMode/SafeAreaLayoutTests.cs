using NUnit.Framework;
using Project333.Runtime.Presentation;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class SafeAreaLayoutTests
    {
        [Test]
        public void Normalize_WhenSafeAreaMatchesScreen_ReturnsFullRect()
        {
            var normalized = SafeAreaLayout.Normalize(new Rect(0f, 0f, 2400f, 1080f), 2400, 1080);

            Assert.That(normalized.xMin, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(normalized.yMin, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(normalized.xMax, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(normalized.yMax, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Normalize_WhenLandscapeHasSideInsets_ReturnsNormalizedInsets()
        {
            var normalized = SafeAreaLayout.Normalize(new Rect(80f, 0f, 2240f, 1080f), 2400, 1080);

            Assert.That(normalized.xMin, Is.EqualTo(80f / 2400f).Within(0.0001f));
            Assert.That(normalized.xMax, Is.EqualTo(2320f / 2400f).Within(0.0001f));
            Assert.That(normalized.yMin, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(normalized.yMax, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void CalculateFullBleedAnchors_WhenContentIsInset_CoversOriginalScreen()
        {
            var normalized = SafeAreaLayout.Normalize(new Rect(80f, 30f, 2240f, 1020f), 2400, 1080);
            var fullBleed = SafeAreaLayout.CalculateFullBleedAnchors(normalized);

            var reconstructedMin = normalized.min + Vector2.Scale(fullBleed.min, normalized.size);
            var reconstructedMax = normalized.min + Vector2.Scale(fullBleed.max, normalized.size);

            Assert.That(reconstructedMin.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(reconstructedMin.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(reconstructedMax.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(reconstructedMax.y, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Normalize_WhenScreenSizeIsInvalid_ReturnsFullRect()
        {
            var normalized = SafeAreaLayout.Normalize(new Rect(10f, 10f, 100f, 100f), 0, 0);

            Assert.That(normalized, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
        }
    }
}
