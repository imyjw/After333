using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Presentation.Hand;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class HandPresenterLayoutTests
    {
        [Test]
        public void GetCardIdForSlot_WhenSlotContainsCard_ReturnsThatCardId()
        {
            var cards = new List<string> { "card-0", "card-1", "card-2" };

            var cardId = HandPresenterLayout.GetCardIdForSlot(cards, 1);

            Assert.That(cardId, Is.EqualTo("card-1"));
        }

        [Test]
        public void GetCardIdForSlot_WhenSlotIsOutsideHand_ReturnsEmptyString()
        {
            var cards = new List<string> { "card-0" };

            var cardId = HandPresenterLayout.GetCardIdForSlot(cards, 3);

            Assert.That(cardId, Is.EqualTo(string.Empty));
        }

        [Test]
        public void CalculateFanLayout_WhenOneCard_ReturnsCenteredFlatLayout()
        {
            var layout = HandPresenterLayout.CalculateFanLayout(
                1,
                0,
                1200f,
                90f,
                152f,
                17f,
                96f,
                34f,
                0.82f);

            Assert.That(layout.AnchoredPosition.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(layout.AnchoredPosition.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(layout.RotationDegrees, Is.EqualTo(0f).Within(0.01f));
            Assert.That(layout.Scale, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void CalculateFanLayout_WhenThreeCards_CentersMiddleCardAndFansSides()
        {
            var left = HandPresenterLayout.CalculateFanLayout(3, 0, 1200f, 90f, 152f, 17f, 96f, 34f, 0.82f);
            var middle = HandPresenterLayout.CalculateFanLayout(3, 1, 1200f, 90f, 152f, 17f, 96f, 34f, 0.82f);
            var right = HandPresenterLayout.CalculateFanLayout(3, 2, 1200f, 90f, 152f, 17f, 96f, 34f, 0.82f);

            Assert.That(left.AnchoredPosition.x, Is.LessThan(0f));
            Assert.That(right.AnchoredPosition.x, Is.GreaterThan(0f));
            Assert.That(middle.AnchoredPosition.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(middle.AnchoredPosition.y, Is.GreaterThan(left.AnchoredPosition.y));
            Assert.That(left.RotationDegrees, Is.GreaterThan(0f));
            Assert.That(right.RotationDegrees, Is.LessThan(0f));
        }

        [Test]
        public void GetSiblingOrderForFan_WhenNoCardIsSelected_RightmostCardRendersOnTop()
        {
            var layouts = new[]
            {
                new HandCardFanLayout(new UnityEngine.Vector2(-100f, 0f), 10f, 1f, -1f),
                new HandCardFanLayout(new UnityEngine.Vector2(0f, 20f), 0f, 1f, 0f),
                new HandCardFanLayout(new UnityEngine.Vector2(100f, 0f), -10f, 1f, 1f)
            };

            var order = HandPresenterLayout.GetSiblingOrderForFan(layouts, -1);

            Assert.That(order, Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void GetSiblingOrderForFan_WhenCardIsSelected_SelectedCardStillRendersOnTop()
        {
            var layouts = new[]
            {
                new HandCardFanLayout(new UnityEngine.Vector2(-100f, 0f), 10f, 1f, -1f),
                new HandCardFanLayout(new UnityEngine.Vector2(0f, 20f), 0f, 1f, 0f),
                new HandCardFanLayout(new UnityEngine.Vector2(100f, 0f), -10f, 1f, 1f)
            };

            var order = HandPresenterLayout.GetSiblingOrderForFan(layouts, 0);

            Assert.That(order[^1], Is.EqualTo(0));
        }

        [Test]
        public void RestoreAuthoringLayout_WhenRuntimeValuesChange_RestoresInspectorSnapshot()
        {
            var gameObject = new GameObject("HandPresenterTest");

            try
            {
                var presenter = gameObject.AddComponent<HandPresenter>();
                SetPrivateField(presenter, "_cardSize", new Vector2(212f, 318f));
                SetPrivateField(presenter, "_bottomPadding", 144f);
                SetPrivateField(presenter, "_selectedScaleMultiplier", 1.75f);
                InvokePrivateMethod(presenter, "CaptureAuthoringLayout");

                SetPrivateField(presenter, "_cardSize", new Vector2(270f, 378f));
                SetPrivateField(presenter, "_bottomPadding", 18f);
                SetPrivateField(presenter, "_selectedScaleMultiplier", 1.08f);
                InvokePrivateMethod(presenter, "RestoreAuthoringLayout");

                Assert.That(GetPrivateField<Vector2>(presenter, "_cardSize"), Is.EqualTo(new Vector2(212f, 318f)));
                Assert.That(GetPrivateField<float>(presenter, "_bottomPadding"), Is.EqualTo(144f));
                Assert.That(GetPrivateField<float>(presenter, "_selectedScaleMultiplier"), Is.EqualTo(1.75f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static void SetPrivateField<T>(HandPresenter presenter, string fieldName, T value)
        {
            typeof(HandPresenter)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(presenter, value);
        }

        private static T GetPrivateField<T>(HandPresenter presenter, string fieldName)
        {
            return (T)typeof(HandPresenter)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(presenter);
        }

        private static void InvokePrivateMethod(HandPresenter presenter, string methodName)
        {
            typeof(HandPresenter)
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(presenter, null);
        }
    }
}
