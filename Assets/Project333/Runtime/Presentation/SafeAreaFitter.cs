using System.Collections.Generic;
using UnityEngine;

namespace Project333.Runtime.Presentation
{
    public static class SafeAreaLayout
    {
        private const float MinimumSpan = 0.0001f;

        public static Rect Normalize(Rect safeArea, int screenWidth, int screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            var xMin = Mathf.Clamp01(safeArea.xMin / screenWidth);
            var yMin = Mathf.Clamp01(safeArea.yMin / screenHeight);
            var xMax = Mathf.Clamp01(safeArea.xMax / screenWidth);
            var yMax = Mathf.Clamp01(safeArea.yMax / screenHeight);

            if (xMax - xMin < MinimumSpan || yMax - yMin < MinimumSpan)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        public static Rect CalculateFullBleedAnchors(Rect normalizedSafeArea)
        {
            var width = Mathf.Max(MinimumSpan, normalizedSafeArea.width);
            var height = Mathf.Max(MinimumSpan, normalizedSafeArea.height);

            return Rect.MinMaxRect(
                -normalizedSafeArea.xMin / width,
                -normalizedSafeArea.yMin / height,
                (1f - normalizedSafeArea.xMin) / width,
                (1f - normalizedSafeArea.yMin) / height);
        }
    }

    [DisallowMultipleComponent]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private const string DefaultSafeAreaRootName = "SafeAreaRoot";

        [SerializeField] private RectTransform[] _fullBleedElements = new RectTransform[0];

        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;
        private bool _hasAppliedLayout;

        public void Configure(params RectTransform[] fullBleedElements)
        {
            _rectTransform = transform as RectTransform;
            _fullBleedElements = fullBleedElements ?? new RectTransform[0];
            _hasAppliedLayout = false;
            ApplyNow();
        }

        public static RectTransform EnsureCanvasContentRoot(
            RectTransform canvasRectTransform,
            string safeAreaRootName = DefaultSafeAreaRootName)
        {
            if (canvasRectTransform == null)
            {
                return null;
            }

            var existingRoot = canvasRectTransform.Find(safeAreaRootName) as RectTransform;
            if (existingRoot != null)
            {
                MoveDirectChildren(canvasRectTransform, existingRoot);
                EnsureFitter(existingRoot).Configure();
                return existingRoot;
            }

            var originalChildren = new List<Transform>(canvasRectTransform.childCount);
            for (var i = 0; i < canvasRectTransform.childCount; i++)
            {
                originalChildren.Add(canvasRectTransform.GetChild(i));
            }

            var safeAreaObject = new GameObject(safeAreaRootName, typeof(RectTransform));
            var safeAreaRoot = safeAreaObject.GetComponent<RectTransform>();
            safeAreaRoot.SetParent(canvasRectTransform, false);
            SetFullStretch(safeAreaRoot);

            MoveChildren(originalChildren, safeAreaRoot);

            EnsureFitter(safeAreaRoot).Configure();
            return safeAreaRoot;
        }

        private static void MoveDirectChildren(RectTransform parent, RectTransform destination)
        {
            var children = new List<Transform>(parent.childCount);
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child != destination)
                {
                    children.Add(child);
                }
            }

            MoveChildren(children, destination);
        }

        private static void MoveChildren(IReadOnlyList<Transform> children, RectTransform destination)
        {
            for (var i = 0; i < children.Count; i++)
            {
                children[i].SetParent(destination, false);
            }
        }

        private static SafeAreaFitter EnsureFitter(RectTransform target)
        {
            if (!target.TryGetComponent<SafeAreaFitter>(out var fitter))
            {
                fitter = target.gameObject.AddComponent<SafeAreaFitter>();
            }

            return fitter;
        }

        private static void SetFullStretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private void Awake()
        {
            _rectTransform = transform as RectTransform;
        }

        private void OnEnable()
        {
            _hasAppliedLayout = false;
            ApplyNow();
        }

        private void LateUpdate()
        {
            if (HasScreenLayoutChanged())
            {
                ApplyNow();
            }
        }

        private bool HasScreenLayoutChanged()
        {
            return !_hasAppliedLayout ||
                   _lastScreenWidth != Screen.width ||
                   _lastScreenHeight != Screen.height ||
                   _lastSafeArea != Screen.safeArea;
        }

        private void ApplyNow()
        {
            if (_rectTransform == null)
            {
                _rectTransform = transform as RectTransform;
            }

            if (_rectTransform == null)
            {
                return;
            }

            var normalizedSafeArea = SafeAreaLayout.Normalize(Screen.safeArea, Screen.width, Screen.height);
            _rectTransform.anchorMin = normalizedSafeArea.min;
            _rectTransform.anchorMax = normalizedSafeArea.max;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;

            ApplyFullBleedElements(normalizedSafeArea);

            _lastSafeArea = Screen.safeArea;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _hasAppliedLayout = true;
        }

        private void ApplyFullBleedElements(Rect normalizedSafeArea)
        {
            if (_fullBleedElements == null || _fullBleedElements.Length == 0)
            {
                return;
            }

            var fullBleedAnchors = SafeAreaLayout.CalculateFullBleedAnchors(normalizedSafeArea);
            for (var i = 0; i < _fullBleedElements.Length; i++)
            {
                var element = _fullBleedElements[i];
                if (element == null)
                {
                    continue;
                }

                element.anchorMin = fullBleedAnchors.min;
                element.anchorMax = fullBleedAnchors.max;
                element.offsetMin = Vector2.zero;
                element.offsetMax = Vector2.zero;
            }
        }
    }
}
