using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using Project333.Runtime.Presentation.Battle;
using UnityEngine.UI;
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

        [TestCase(1, 0f)]
        [TestCase(2, 250f)]
        [TestCase(3, 225f)]
        [TestCase(4, 200f)]
        [TestCase(5, 175f)]
        [TestCase(6, 150f)]
        [TestCase(7, 125f)]
        [TestCase(8, 100f)]
        [TestCase(9, 75f)]
        [TestCase(10, 50f)]
        public void CountSettings_KeepExactSpacingOnWideScreen(int count, float expected)
        {
            var settings = new HandCardCountSpacing();
            Assert.That(settings.GetSpacing(count), Is.EqualTo(expected));
            var layouts = BuildLayouts(count, settings.GetSpacing(count), true, -1);
            Assert.That(Fit(layouts, 1920f), Is.EqualTo(1f));
            if (count > 1)
                Assert.That(layouts[1].AnchoredPosition.x - layouts[0].AnchoredPosition.x,
                    Is.EqualTo(expected).Within(0.001f));
        }

        [TestCase(true, -1)]
        [TestCase(true, 0)]
        [TestCase(true, 9)]
        [TestCase(false, -1)]
        [TestCase(false, 0)]
        public void Overflow_FitsEveryRotatedCornerWithoutChangingSizeOrFan(bool expanded, int selected)
        {
            var layouts = BuildLayouts(10, 250f, expanded, selected);
            const float width = 900f;
            var factor = Fit(layouts, width);
            Assert.That(factor, Is.GreaterThan(0f).And.LessThan(1f));
            var touchesEdge = false;
            foreach (var layout in layouts)
            {
                // Transform all four corners independently of the fit calculation.
                foreach (var x in new[] { -0.5f, 0.5f })
                foreach (var y in new[] { -0.08f, 0.92f })
                {
                    var corner = Quaternion.Euler(0, 0, layout.RotationDegrees) *
                                 new Vector3(x * 277f, y * 370f, 0) * layout.Scale;
                    var position = corner.x + layout.AnchoredPosition.x * factor;
                    Assert.That(position, Is.InRange(-width * 0.5f - 0.01f, width * 0.5f + 0.01f));
                    touchesEdge |= Mathf.Abs(Mathf.Abs(position) - width * 0.5f) < 0.01f;
                }
            }
            Assert.That(touchesEdge, Is.True, "Use the largest spacing that fits, rather than over-compressing.");
        }

        [Test]
        public void OverlapAlone_DoesNotReduceSpacing_AndWiderScreenRestoresRequestedSpacing()
        {
            var layouts = BuildLayouts(10, 50f, true, -1);
            Assert.That(Fit(layouts, 1920f), Is.EqualTo(1f), "Overlapping cards are intentional.");
            layouts = BuildLayouts(5, 250f, true, -1);
            Assert.That(Fit(layouts, 900f), Is.LessThan(1f));
            Assert.That(Fit(layouts, 1920f), Is.EqualTo(1f), "Fit must never mutate the requested spacing.");
        }

        [Test]
        public void SingleOversizedCard_DoesNotInventNegativeSpacingOrChangeScale()
        {
            var layouts = BuildLayouts(1, 250f, true, -1);
            Assert.That(Fit(layouts, 100f), Is.Zero);
            Assert.That(layouts[0].Scale, Is.EqualTo(1f));
            Assert.That(layouts[0].AnchoredPosition.x, Is.Zero);
        }

        [Test]
        public void CountSettings_SnapshotAndSerializationKeepIndependentInspectorEdits()
        {
            var obj = new GameObject("CountSpacingSnapshot");
            try
            {
                var presenter = obj.AddComponent<HandPresenter>();
                var settings = GetPrivateField<HandCardCountSpacing>(presenter, "_spacingByCardCount");
                JsonUtility.FromJsonOverwrite("{\"_twoCards\":310,\"_tenCards\":44}", settings);
                InvokePrivateMethod(presenter, "CaptureAuthoringLayout");
                JsonUtility.FromJsonOverwrite("{\"_twoCards\":100,\"_tenCards\":100}", settings);
                InvokePrivateMethod(presenter, "RestoreAuthoringLayout");
                var restored = GetPrivateField<HandCardCountSpacing>(presenter, "_spacingByCardCount");
                Assert.That(restored.GetSpacing(2), Is.EqualTo(310f));
                Assert.That(restored.GetSpacing(10), Is.EqualTo(44f));
                var reloaded = JsonUtility.FromJson<HandCardCountSpacing>(JsonUtility.ToJson(restored));
                Assert.That(reloaded.GetSpacing(2), Is.EqualTo(310f));
                Assert.That(reloaded.GetSpacing(10), Is.EqualTo(44f));

                var snapshotField = typeof(HandPresenter).GetField("_authoringLayoutSnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
                var legacySnapshot = snapshotField.GetValue(presenter);
                legacySnapshot.GetType().GetField("SpacingByCardCount").SetValue(legacySnapshot, null);
                snapshotField.SetValue(presenter, legacySnapshot);
                InvokePrivateMethod(presenter, "RestoreAuthoringLayout");
                Assert.That(GetPrivateField<HandCardCountSpacing>(presenter, "_spacingByCardCount").GetSpacing(10), Is.EqualTo(44f));
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [TestCase(2, 1920, false)]
        [TestCase(5, 1920, false)]
        [TestCase(10, 1920, false)]
        [TestCase(5, 900, false)]
        [TestCase(5, 900, true)]
        public void ActualHandRects_UseCountSettingsAndRespectViewport(int count, int width, bool selectEdge)
        {
            var root = new GameObject("CountSpacingPreview", typeof(RectTransform), typeof(Canvas));
            var cameraObj = new GameObject("CountSpacingCamera", typeof(Camera));
            var camera = cameraObj.GetComponent<Camera>();
            var target = new RenderTexture(width, 1080, 24);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.05f, 0.09f, 0.12f);
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                var handRoot = new GameObject("RuntimeHandRoot", typeof(RectTransform)).GetComponent<RectTransform>();
                handRoot.SetParent(root.transform, false);
                handRoot.anchorMin = Vector2.zero;
                handRoot.anchorMax = new Vector2(1, 0);
                handRoot.pivot = new Vector2(0.5f, 0);
                handRoot.sizeDelta = new Vector2(0, 440);
                handRoot.anchoredPosition = new Vector2(0, 80);
                var presenter = root.AddComponent<HandPresenter>();
                var cards = new HandCardView[count];
                var rects = new RectTransform[count];
                var ids = new[] { "BlueDragon", "Gwangma", "firebolt", "Goblin", "ManaPond" };
                for (var i = 0; i < count; i++)
                {
                    var slot = new GameObject("Card" + i, typeof(RectTransform), typeof(HandCardView), typeof(HandCardTextView));
                    slot.transform.SetParent(root.transform, false);
                    cards[i] = slot.GetComponent<HandCardView>();
                    cards[i].Configure(i);
                    cards[i].Present(ids[i % ids.Length]);
                    slot.GetComponent<HandCardTextView>().BindRuntimeHandRoot(handRoot);
                    Assert.That(slot.GetComponent<HandCardTextView>().TutorialArtwork.sprite, Is.Not.Null, "Render actual card artwork, not a fallback label.");
                    rects[i] = (RectTransform)handRoot.GetChild(i);
                }
                SetPrivateField(presenter, "_handCardViews", cards);
                SetPrivateField(presenter, "_runtimeHandRoot", handRoot);
                SetPrivateField(presenter, "_cardSize", new Vector2(277, 370));
                SetPrivateField(presenter, "_selectedScaleMultiplier", 1.75f);
                SetPrivateField(presenter, "_selectedLift", 110f);
                SetPrivateField(presenter, "_arcHeight", 0f);
                SetPrivateField(presenter, "_edgeDrop", 0f);
                presenter.SetExpanded(true);
                if (selectEdge) cards[0].SetHighlight(BattleHighlightState.Selected);
                Canvas.ForceUpdateCanvases();
                InvokePrivateMethod(presenter, "ApplyCurvedLayout");
                Canvas.ForceUpdateCanvases();
                var corners = new Vector3[4];
                foreach (var rect in rects)
                {
                    Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(277, 370)), "Viewport fitting must not shrink artwork.");
                    rect.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var local = handRoot.InverseTransformPoint(corner);
                        Assert.That(local.x, Is.InRange(-handRoot.rect.width * 0.5f - 0.01f, handRoot.rect.width * 0.5f + 0.01f));
                    }
                }
                var desired = new HandCardCountSpacing().GetSpacing(count);
                var actual = rects[1].anchoredPosition.x - rects[0].anchoredPosition.x;
                if (width == 1920) Assert.That(actual, Is.EqualTo(desired).Within(0.01f));
                else Assert.That(actual, Is.LessThan(desired));

                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(width, 1080, TextureFormat.RGBA32, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, width, 1080), 0, 0);
                    image.Apply();
                    var directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Captures/HandCountSpacing_20261029"));
                    Directory.CreateDirectory(directory);
                    File.WriteAllBytes(Path.Combine(directory, $"hand-{count}-width-{width}-selected-{selectEdge}.png"), image.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(image); }
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraObj);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void SavedBattleScene_LegacySnapshotKeepsNewDefaultsAndAuthoringEdits()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Battle_VSlice.unity");
            try
            {
                var hand = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<HandPresenter>(true)).Single();
                var originalSize = GetPrivateField<Vector2>(hand, "_cardSize");
                InvokePrivateMethod(hand, "RestoreAuthoringLayout");
                var spacing = GetPrivateField<HandCardCountSpacing>(hand, "_spacingByCardCount");
                Assert.That(spacing, Is.Not.Null);
                Assert.That(spacing.GetSpacing(2), Is.EqualTo(250f));
                Assert.That(spacing.GetSpacing(10), Is.EqualTo(50f));
                Assert.That(GetPrivateField<Vector2>(hand, "_cardSize"), Is.EqualTo(originalSize));
                JsonUtility.FromJsonOverwrite("{\"_fiveCards\":181}", spacing);
                InvokePrivateMethod(hand, "CaptureAuthoringLayout");
                InvokePrivateMethod(hand, "RestoreAuthoringLayout");
                Assert.That(GetPrivateField<HandCardCountSpacing>(hand, "_spacingByCardCount").GetSpacing(5), Is.EqualTo(181f));
                Assert.That(UnityEngine.Application.isPlaying, Is.False, "Reading the scene must not start a battle or contact the server.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static HandCardFanLayout[] BuildLayouts(int count, float spacing, bool expanded, int selected)
        {
            var layouts = new HandCardFanLayout[count];
            for (var i = 0; i < count; i++)
            {
                var layout = HandPresenterLayout.CalculateFanLayoutWithSpacing(count, i, spacing, 17, 96, 34, 0.82f);
                if (i == selected)
                    layout = new HandCardFanLayout(layout.AnchoredPosition + new Vector2(0, 72), 0, layout.Scale * 1.08f, layout.NormalizedPosition);
                layouts[i] = HandPresenterLayout.ApplyExpansion(layout, expanded || i == selected);
            }
            return layouts;
        }

        private static float Fit(HandCardFanLayout[] layouts, float width)
        {
            var pivots = new Vector2[layouts.Length];
            for (var i = 0; i < pivots.Length; i++) pivots[i] = new Vector2(0.5f, 0.08f);
            return HandPresenterLayout.CalculateHorizontalFitFactor(layouts, new Vector2(277, 370), pivots, width);
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
