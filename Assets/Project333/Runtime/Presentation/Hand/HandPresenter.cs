using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Hand
{
    public sealed class HandPresenter : MonoBehaviour
    {
        private const int MinimumHandSlotCount = 10;

        [SerializeField] private Transform _handRoot;
        [SerializeField] private HandCardView[] _handCardViews = Array.Empty<HandCardView>();

        [Header("Curved Hand Layout")]
        [SerializeField] private Vector2 _cardSize = new Vector2(270f, 378f);
        [SerializeField] private float _bottomPadding = 18f;
        [SerializeField] private float _handBandHeight = 440f;
        [SerializeField] [Range(0.45f, 1f)] private float _handWidthFactor = 0.8f;
        [SerializeField] private float _minSpacing = 90f;
        [SerializeField] private float _maxSpacing = 152f;
        [SerializeField] private float _maxRotationDegrees = 17f;
        [SerializeField] private float _arcHeight = 96f;
        [SerializeField] private float _edgeDrop = 34f;
        [SerializeField] private float _minimumLargeHandScale = 0.82f;
        [SerializeField] private float _selectedLift = 72f;
        [SerializeField] private float _selectedScaleMultiplier = 1.08f;
        [SerializeField] private int _canvasSortingOrder = 35;

        private readonly List<VisibleHandCard> _visibleHandCards = new List<VisibleHandCard>(MinimumHandSlotCount);

        private Canvas _runtimeCanvas;
        private RectTransform _runtimeCanvasRect;
        private RectTransform _runtimeHandRoot;

        public HandCardView[] HandCardViews => _handCardViews;

        private void Awake()
        {
            RebuildCardRegistry();
        }

        private void LateUpdate()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                return;
            }

            EnsureCardRegistry();
            EnsureRuntimeHandCanvas();
            BindRuntimeVisualParents();
            ApplyCurvedLayout();
        }

        private void OnDestroy()
        {
            if (_runtimeCanvas != null)
            {
                Destroy(_runtimeCanvas.gameObject);
            }
        }

        public void RebuildCardRegistry()
        {
            EnsureMinimumHandSlots();
            _handCardViews = CollectDirectChildHandCardViews(_handRoot);
            ConfigureHandCardViews(_handCardViews);
        }

        public void Present(HandState handState)
        {
            EnsureCardRegistry();

            for (var i = 0; i < _handCardViews.Length; i++)
            {
                var handCardView = _handCardViews[i];
                if (handCardView == null)
                {
                    continue;
                }

                handCardView.Present(HandPresenterLayout.GetCardIdForSlot(handState?.CardIds, i));
            }

            if (!UnityEngine.Application.isPlaying)
            {
                return;
            }

            EnsureRuntimeHandCanvas();
            BindRuntimeVisualParents();
            ApplyCurvedLayout();
        }

        private void EnsureCardRegistry()
        {
            if (_handRoot == null)
            {
                return;
            }

            if (_handCardViews.Length < MinimumHandSlotCount)
            {
                RebuildCardRegistry();
                return;
            }

            if (_handCardViews.Length == 0)
            {
                RebuildCardRegistry();
            }
        }

        private void EnsureMinimumHandSlots()
        {
            if (_handRoot == null)
            {
                return;
            }

            var missingSlotCount = MinimumHandSlotCount - _handRoot.childCount;
            for (var i = 0; i < missingSlotCount; i++)
            {
                CreateAutoHandSlot(_handRoot.childCount);
            }
        }

        private void CreateAutoHandSlot(int slotIndex)
        {
            var slotObject = new GameObject(
                $"Hand_{slotIndex:00}",
                typeof(RectTransform),
                typeof(HandCardView),
                typeof(HandCardTextView));

            var slotTransform = slotObject.GetComponent<RectTransform>();
            slotTransform.SetParent(_handRoot, false);
            slotTransform.anchorMin = new Vector2(0.5f, 0f);
            slotTransform.anchorMax = new Vector2(0.5f, 0f);
            slotTransform.pivot = new Vector2(0.5f, 0.5f);
            slotTransform.sizeDelta = _cardSize;
            slotTransform.anchoredPosition = Vector2.zero;
            slotTransform.localScale = Vector3.one;
        }

        private void EnsureRuntimeHandCanvas()
        {
            if (_runtimeCanvas != null && _runtimeHandRoot != null)
            {
                UpdateRuntimeHandRootRect();
                return;
            }

            var canvasObject = new GameObject(
                "RuntimeHandCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            canvasObject.transform.SetParent(transform, false);

            _runtimeCanvasRect = canvasObject.GetComponent<RectTransform>();
            _runtimeCanvas = canvasObject.GetComponent<Canvas>();
            var canvasScaler = canvasObject.GetComponent<CanvasScaler>();

            _runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _runtimeCanvas.sortingOrder = _canvasSortingOrder;
            _runtimeCanvas.pixelPerfect = false;

            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;

            _runtimeCanvasRect.anchorMin = Vector2.zero;
            _runtimeCanvasRect.anchorMax = Vector2.one;
            _runtimeCanvasRect.offsetMin = Vector2.zero;
            _runtimeCanvasRect.offsetMax = Vector2.zero;

            var handRootObject = new GameObject("RuntimeHandRoot", typeof(RectTransform));
            _runtimeHandRoot = handRootObject.GetComponent<RectTransform>();
            _runtimeHandRoot.SetParent(_runtimeCanvasRect, false);

            UpdateRuntimeHandRootRect();
        }

        private void UpdateRuntimeHandRootRect()
        {
            if (_runtimeHandRoot == null)
            {
                return;
            }

            _runtimeHandRoot.anchorMin = new Vector2(0f, 0f);
            _runtimeHandRoot.anchorMax = new Vector2(1f, 0f);
            _runtimeHandRoot.pivot = new Vector2(0.5f, 0f);
            _runtimeHandRoot.offsetMin = new Vector2(0f, _bottomPadding);
            _runtimeHandRoot.offsetMax = new Vector2(0f, _bottomPadding + _handBandHeight);
        }

        private void BindRuntimeVisualParents()
        {
            if (_runtimeHandRoot == null)
            {
                return;
            }

            for (var i = 0; i < _handCardViews.Length; i++)
            {
                var handCardView = _handCardViews[i];
                if (handCardView == null)
                {
                    continue;
                }

                var textView = handCardView.GetComponent<HandCardTextView>();
                if (textView == null)
                {
                    continue;
                }

                textView.BindRuntimeHandRoot(_runtimeHandRoot);
            }
        }

        private void ApplyCurvedLayout()
        {
            _visibleHandCards.Clear();

            for (var i = 0; i < _handCardViews.Length; i++)
            {
                var handCardView = _handCardViews[i];
                if (handCardView == null)
                {
                    continue;
                }

                var textView = handCardView.GetComponent<HandCardTextView>();
                if (textView == null)
                {
                    continue;
                }

                if (!handCardView.HasCard)
                {
                    textView.ApplyRuntimeLayout(Vector2.zero, 0f, _cardSize, 0.92f, -1, false);
                    continue;
                }

                _visibleHandCards.Add(new VisibleHandCard(handCardView, textView));
            }

            if (_visibleHandCards.Count == 0)
            {
                return;
            }

            var availableWidth = ResolveAvailableWidth();
            var layouts = new HandCardFanLayout[_visibleHandCards.Count];
            var selectedVisibleIndex = -1;

            for (var i = 0; i < _visibleHandCards.Count; i++)
            {
                var visibleHandCard = _visibleHandCards[i];
                var layout = HandPresenterLayout.CalculateFanLayout(
                    _visibleHandCards.Count,
                    i,
                    availableWidth,
                    _minSpacing,
                    _maxSpacing,
                    _maxRotationDegrees,
                    _arcHeight,
                    _edgeDrop,
                    _minimumLargeHandScale);

                if (visibleHandCard.View.HighlightState == BattleHighlightState.Selected)
                {
                    selectedVisibleIndex = i;
                    layout = new HandCardFanLayout(
                        layout.AnchoredPosition + new Vector2(0f, _selectedLift),
                        0f,
                        layout.Scale * _selectedScaleMultiplier,
                        layout.NormalizedPosition);
                }

                layouts[i] = layout;
            }

            var siblingOrder = HandPresenterLayout.GetSiblingOrderForFan(layouts, selectedVisibleIndex);
            var siblingRankByVisibleIndex = new int[siblingOrder.Length];
            for (var i = 0; i < siblingOrder.Length; i++)
            {
                siblingRankByVisibleIndex[siblingOrder[i]] = i;
            }

            for (var i = 0; i < _visibleHandCards.Count; i++)
            {
                var visibleHandCard = _visibleHandCards[i];
                var layout = layouts[i];
                visibleHandCard.TextView.ApplyRuntimeLayout(
                    layout.AnchoredPosition,
                    layout.RotationDegrees,
                    _cardSize,
                    layout.Scale,
                    siblingRankByVisibleIndex[i],
                    true);
            }
        }

        private float ResolveAvailableWidth()
        {
            if (_runtimeHandRoot != null && _runtimeHandRoot.rect.width > 0.1f)
            {
                return _runtimeHandRoot.rect.width * _handWidthFactor;
            }

            return Screen.width * _handWidthFactor;
        }

        private static HandCardView[] CollectDirectChildHandCardViews(Transform root)
        {
            if (root == null)
            {
                return Array.Empty<HandCardView>();
            }

            var handCardViews = new HandCardView[root.childCount];
            for (var i = 0; i < root.childCount; i++)
            {
                handCardViews[i] = root.GetChild(i).GetComponent<HandCardView>();
            }

            return handCardViews;
        }

        private static void ConfigureHandCardViews(HandCardView[] handCardViews)
        {
            for (var i = 0; i < handCardViews.Length; i++)
            {
                var handCardView = handCardViews[i];
                if (handCardView == null)
                {
                    continue;
                }

                handCardView.Configure(i);
            }
        }

        private readonly struct VisibleHandCard
        {
            public VisibleHandCard(HandCardView view, HandCardTextView textView)
            {
                View = view;
                TextView = textView;
            }

            public HandCardView View { get; }

            public HandCardTextView TextView { get; }
        }
    }
}
