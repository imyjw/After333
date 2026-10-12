using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class GameStartSafeAreaTests
    {
        private const BindingFlags HiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags HiddenInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(2340, 1080, 92, 0)]
        [TestCase(2340, 1080, 0, 92)]
        [TestCase(2340, 1080, 92, 92)]
        [TestCase(1920, 1080, 0, 0)]
        [TestCase(2400, 1080, 120, 24)]
        public void ActualStartScene_MenuStaysOnBackgroundCenter_WithAsymmetricSafeArea(
            int width, int height, int left, int right)
        {
            var scene = EditorSceneManager.OpenScene("Assets/GameStart_VSlice.unity", OpenSceneMode.Additive);
            try
            {
                var root = scene.GetRootGameObjects()
                    .SelectMany(go => go.GetComponentsInChildren<RectTransform>(true))
                    .Single(rect => rect.name == "GameStartSceneController");
                // Give the actual scene a deterministic pixel-sized canvas in Edit Mode.
                var canvas = root.GetComponentInParent<Canvas>();
                canvas.GetComponent<UnityEngine.UI.CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                var canvasTransform = (RectTransform)canvas.transform;
                canvasTransform.localScale = Vector3.one;
                canvasTransform.sizeDelta = new Vector2(width, height);
                var center = (RectTransform)root.Find("CenterPanel");
                var background = (RectTransform)root.Find("Background");
                var position = center.anchoredPosition;
                var size = center.sizeDelta;
                var yMin = center.anchorMin.y;
                var yMax = center.anchorMax.y;
                var menuButtons = center.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                Assert.That(menuButtons.Length, Is.GreaterThanOrEqualTo(3));

                typeof(Project333RuntimeSettings).GetMethod("ConfigureNamedRoot", HiddenStatic)
                    .Invoke(null, new object[] { scene, "GameStartSceneController" });
                var fitter = root.GetComponent<SafeAreaFitter>();
                // Use the same layout path as Screen.safeArea, including rotation/reset.
                foreach (var safe in new[] {
                    new Rect(left, 0, width - left - right, height),
                    new Rect(right, 0, width - left - right, height),
                    new Rect(0, 0, width, height) })
                {
                    typeof(SafeAreaFitter).GetMethod("ApplyLayout", HiddenInstance)
                        .Invoke(fitter, new object[] { safe, width, height });
                    Canvas.ForceUpdateCanvases();
                    var canvasRect = (RectTransform)root.parent;
                    var backgroundCenter = canvasRect.InverseTransformPoint(background.TransformPoint(background.rect.center)).x;
                    var canvasCenter = canvasRect.rect.center.x;
                    Assert.That(backgroundCenter, Is.EqualTo(canvasCenter).Within(0.01f));
                    foreach (var button in menuButtons)
                    {
                        var rect = (RectTransform)button.transform;
                        var buttonCenter = canvasRect.InverseTransformPoint(rect.TransformPoint(rect.rect.center)).x;
                        Assert.That(buttonCenter, Is.EqualTo(backgroundCenter).Within(0.01f),
                            button.name + " must use the full-screen background center.");
                        var corners = new Vector3[4];
                        rect.GetWorldCorners(corners);
                        Assert.That(root.InverseTransformPoint(corners[0]).x, Is.GreaterThanOrEqualTo(root.rect.xMin - 0.01f));
                        Assert.That(root.InverseTransformPoint(corners[2]).x, Is.LessThanOrEqualTo(root.rect.xMax + 0.01f));
                    }
                    // Edge UI still uses the original (asymmetric) safe area.
                    Assert.That(root.anchorMin.x, Is.EqualTo(safe.xMin / width).Within(0.0001f));
                    Assert.That(root.anchorMax.x, Is.EqualTo(safe.xMax / width).Within(0.0001f));
                    Assert.That(center.anchoredPosition, Is.EqualTo(position));
                    Assert.That(center.sizeDelta, Is.EqualTo(size));
                    Assert.That(center.anchorMin.y, Is.EqualTo(yMin));
                    Assert.That(center.anchorMax.y, Is.EqualTo(yMax));
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}