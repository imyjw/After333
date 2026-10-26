using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation.Draft;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class DraftRunRewardLayoutTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(true)]
        [TestCase(false)]
        public void RewardCanvas_FillsDraftCanvas_ForExistingAndNewHierarchy(bool existing)
        {
            var root = new GameObject("DraftLayoutTest", typeof(RectTransform), typeof(Canvas));
            root.SetActive(false); // No account refresh or gameplay lifecycle.
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)root.transform).sizeDelta = new Vector2(2340, 1080);
            var host = new GameObject("DraftSceneController", typeof(RectTransform));
            host.transform.SetParent(root.transform, false);
            Stretch((RectTransform)host.transform);
            var controller = host.AddComponent<DraftSceneController>();
            if (existing)
            {
                var oldCanvas = new GameObject("DraftClaimRewardsCanvas", typeof(RectTransform), typeof(Canvas));
                oldCanvas.transform.SetParent(host.transform, false);
                ((RectTransform)oldCanvas.transform).sizeDelta = new Vector2(100, 100);
            }
            root.SetActive(true); // EditMode: normal MonoBehaviours do not run Awake/Start.
            try
            {
                var rect = EnsureCanvas(controller);
                AssertCanvas(rect, (RectTransform)root.transform);
                Assert.That(EnsureCanvas(controller), Is.SameAs(rect), "Repeated entry must reuse the canvas.");
                Assert.That(host.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(1));
                // Recover the cached path too, rather than only newly discovered Canvases.
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(100, 100);
                ((RectTransform)root.transform).sizeDelta = new Vector2(1920, 1080);
                AssertCanvas(EnsureCanvas(controller), (RectTransform)root.transform);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void AuthoredDraftScene_RewardCanvasAndBackdropFillTheActualRoot()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Draft_VSlice.unity");
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DraftSceneController>(true)).Single();
                var canvas = controller.GetComponentInParent<Canvas>();
                Assert.That(canvas, Is.Not.Null, "Keep the actual nested Canvas reproduction.");
                // Preview scenes have no display-driven root Canvas layout.
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(2340, 1080);
                var rect = EnsureCanvas(controller);
                AssertCanvas(rect, (RectTransform)canvas.transform);
                var view = RunRewardRevealView.Create(rect, null, null);
                view.ShowClosed(6, 3, true);
                view.transform.GetComponent<RectTransform>().ForceUpdateRectTransforms();
                var backdrop = view.GetComponentsInChildren<Image>(true).Single(image => image.name == "DimmedBackground");
                Assert.That(backdrop.rectTransform.rect.size, Is.EqualTo(rect.rect.size));
                Assert.That(backdrop.raycastTarget, Is.True);
                Assert.That(rect.rect.width, Is.GreaterThan(1000), "The screenshot's 100 x 100 host must never return.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static RectTransform EnsureCanvas(DraftSceneController controller) =>
            (RectTransform)typeof(DraftSceneController).GetMethod("EnsureClaimRewardsCanvasRoot", Private).Invoke(controller, null);

        private static void AssertCanvas(RectTransform rect, RectTransform root)
        {
            rect.ForceUpdateRectTransforms();
            Assert.That(rect.rect.size.x, Is.EqualTo(root.rect.width).Within(.1f));
            Assert.That(rect.rect.size.y, Is.EqualTo(root.rect.height).Within(.1f));
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
            Assert.That(rect.GetComponent<Canvas>().overrideSorting, Is.True);
            Assert.That(rect.GetComponent<Canvas>().sortingOrder, Is.EqualTo(3350));
            Assert.That(rect.GetComponent<GraphicRaycaster>(), Is.Not.Null);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
