using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Hand
{
    [DisallowMultipleComponent]
    public sealed class OpponentHandPresenter : MonoBehaviour
    {
        private const string DefaultCardBackResourcePath = "Project333/CardArtwork/OpponentCardBack";
        private const string SafeAreaRootName = "OpponentHandSafeArea";
        private const string HandRootName = "OpponentHandRoot";
        private const string CardBackObjectPrefix = "OpponentCardBack_";

        [Header("References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _handRoot;
        [SerializeField] private Sprite _cardBackSprite;
        [SerializeField] private Texture2D _cardBackTexture;
        [SerializeField] private string _cardBackResourcePath = DefaultCardBackResourcePath;
        [SerializeField] private Image[] _cardBackImages = Array.Empty<Image>();

        [Header("Top Hand Layout")]
        [SerializeField] private Vector2 _cardSize = new Vector2(120f, 160f);
        [Tooltip("Distance from the top safe-area edge. Negative values intentionally clip the opponent hand above the screen.")]
        [SerializeField] private float _topPadding = 12f;
        [SerializeField] private float _handBandHeight = 205f;
        [SerializeField] [Range(0.35f, 1f)] private float _handWidthFactor = 0.66f;
        [SerializeField] private float _minSpacing = 36f;
        [SerializeField] private float _maxSpacing = 74f;
        [SerializeField] private float _maxRotationDegrees = 12f;
        [SerializeField] private float _arcDepth = 24f;
        [SerializeField] private float _edgeLift = 10f;
        [SerializeField] private float _minimumLargeHandScale = 0.78f;
        [SerializeField] private int _canvasSortingOrder = 45;

        private int _visibleCardCount;
        private Sprite _generatedCardBackSprite;
        private Texture2D _generatedSpriteSource;

        public int VisibleCardCount => _visibleCardCount;

        private void Awake()
        {
            EnsureVisualHierarchy();
            ResolveCardBackSprite();
            ApplyCardBackVisuals();
            ApplyVisibleCardCount();
            ApplyLayout();
        }

        private void LateUpdate()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                return;
            }

            ApplyLayout();
        }

        private void OnValidate()
        {
            _cardSize.x = Mathf.Max(1f, _cardSize.x);
            _cardSize.y = Mathf.Max(1f, _cardSize.y);
            _handBandHeight = Mathf.Max(_cardSize.y, _handBandHeight);
            _minSpacing = Mathf.Max(0f, _minSpacing);
            _maxSpacing = Mathf.Max(_minSpacing, _maxSpacing);
            _maxRotationDegrees = Mathf.Max(0f, _maxRotationDegrees);
            _arcDepth = Mathf.Max(0f, _arcDepth);
            _edgeLift = Mathf.Max(0f, _edgeLift);
            _minimumLargeHandScale = Mathf.Clamp(_minimumLargeHandScale, 0.1f, 1f);

            ConfigureCanvas();
            ConfigureHandRoot();
            ResolveCardBackSprite();
            ApplyCardBackVisuals();
            ApplyVisibleCardCount();
            ApplyLayout();
        }

        private void OnDestroy()
        {
            if (_generatedCardBackSprite == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                Destroy(_generatedCardBackSprite);
            }
            else
            {
                DestroyImmediate(_generatedCardBackSprite);
            }

            _generatedCardBackSprite = null;
            _generatedSpriteSource = null;
        }

        public void Present(HandState opponentHand)
        {
            PresentCount(opponentHand?.Count ?? 0);
        }

        public void PresentCount(int opponentHandCount)
        {
            _visibleCardCount = Mathf.Clamp(
                opponentHandCount,
                0,
                PlayerState.AbsoluteMaxHandSize);

            EnsureVisualHierarchy();
            ResolveCardBackSprite();
            ApplyCardBackVisuals();
            ApplyVisibleCardCount();
            ApplyLayout();
        }

        [ContextMenu("Ensure Editable Opponent Hand Slots")]
        public void EnsureEditableCardBackSlots()
        {
            EnsureVisualHierarchy();
            ResolveCardBackSprite();
            ApplyCardBackVisuals();
            ApplyVisibleCardCount();
            ApplyLayout();
        }

        private void EnsureVisualHierarchy()
        {
            EnsureCanvas();
            EnsureHandRoot();
            EnsureCardBackSlots();
        }

        private void EnsureCanvas()
        {
            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
            }

            if (_canvas == null)
            {
                _canvas = gameObject.AddComponent<Canvas>();
            }

            if (GetComponent<CanvasScaler>() == null)
            {
                gameObject.AddComponent<CanvasScaler>();
            }

            ConfigureCanvas();
        }

        private void ConfigureCanvas()
        {
            if (_canvas == null)
            {
                return;
            }

            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = _canvasSortingOrder;

            var scaler = _canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1f;
            }

            if (_canvas.transform is RectTransform canvasRect)
            {
                canvasRect.anchorMin = Vector2.zero;
                canvasRect.anchorMax = Vector2.one;
                canvasRect.offsetMin = Vector2.zero;
                canvasRect.offsetMax = Vector2.zero;
            }
        }

        private void EnsureHandRoot()
        {
            if (_canvas == null)
            {
                return;
            }

            var canvasRect = _canvas.transform as RectTransform;
            var safeAreaRoot = SafeAreaFitter.EnsureCanvasContentRoot(canvasRect, SafeAreaRootName);
            if (_handRoot == null && safeAreaRoot != null)
            {
                _handRoot = safeAreaRoot.Find(HandRootName) as RectTransform;
            }

            if (_handRoot == null && safeAreaRoot != null)
            {
                var handRootObject = new GameObject(HandRootName, typeof(RectTransform));
                _handRoot = handRootObject.GetComponent<RectTransform>();
                _handRoot.SetParent(safeAreaRoot, false);
            }

            ConfigureHandRoot();
        }

        private void ConfigureHandRoot()
        {
            if (_handRoot == null)
            {
                return;
            }

            _handRoot.anchorMin = new Vector2(0f, 1f);
            _handRoot.anchorMax = new Vector2(1f, 1f);
            _handRoot.pivot = new Vector2(0.5f, 1f);
            _handRoot.offsetMin = new Vector2(0f, -_topPadding - _handBandHeight);
            _handRoot.offsetMax = new Vector2(0f, -_topPadding);
        }

        private void EnsureCardBackSlots()
        {
            if (_handRoot == null)
            {
                return;
            }

            if (_cardBackImages == null || _cardBackImages.Length != PlayerState.AbsoluteMaxHandSize)
            {
                _cardBackImages = new Image[PlayerState.AbsoluteMaxHandSize];
            }

            for (var i = 0; i < _cardBackImages.Length; i++)
            {
                var image = _cardBackImages[i];
                if (image == null)
                {
                    var childName = $"{CardBackObjectPrefix}{i:00}";
                    var existing = _handRoot.Find(childName);
                    if (existing != null)
                    {
                        image = existing.GetComponent<Image>();
                    }

                    if (image == null)
                    {
                        var cardObject = new GameObject(
                            childName,
                            typeof(RectTransform),
                            typeof(CanvasRenderer),
                            typeof(Image));
                        var cardRect = cardObject.GetComponent<RectTransform>();
                        cardRect.SetParent(_handRoot, false);
                        image = cardObject.GetComponent<Image>();
                    }

                    _cardBackImages[i] = image;
                }

                ConfigureCardBackImage(image);
            }
        }

        private void ConfigureCardBackImage(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.raycastTarget = false;
            image.preserveAspect = true;
            image.type = Image.Type.Simple;
            image.color = Color.white;

            var rectTransform = image.rectTransform;
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = _cardSize;
        }

        private void ResolveCardBackSprite()
        {
            if (_cardBackSprite != null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_cardBackResourcePath))
            {
                _cardBackSprite = Resources.Load<Sprite>(_cardBackResourcePath);
                if (_cardBackSprite != null)
                {
                    return;
                }
            }

            if (_cardBackTexture == null && !string.IsNullOrWhiteSpace(_cardBackResourcePath))
            {
                _cardBackTexture = Resources.Load<Texture2D>(_cardBackResourcePath);
            }

            if (_cardBackTexture == null)
            {
                return;
            }

            if (_generatedCardBackSprite != null && _generatedSpriteSource == _cardBackTexture)
            {
                _cardBackSprite = _generatedCardBackSprite;
                return;
            }

            if (_generatedCardBackSprite != null)
            {
                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(_generatedCardBackSprite);
                }
                else
                {
                    DestroyImmediate(_generatedCardBackSprite);
                }
            }

            _generatedCardBackSprite = Sprite.Create(
                _cardBackTexture,
                new Rect(0f, 0f, _cardBackTexture.width, _cardBackTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            _generatedCardBackSprite.name = $"{_cardBackTexture.name}_RuntimeSprite";
            _generatedSpriteSource = _cardBackTexture;
            _cardBackSprite = _generatedCardBackSprite;
        }

        private void ApplyCardBackVisuals()
        {
            if (_cardBackImages == null)
            {
                return;
            }

            for (var i = 0; i < _cardBackImages.Length; i++)
            {
                var image = _cardBackImages[i];
                if (image == null)
                {
                    continue;
                }

                image.sprite = _cardBackSprite;
                image.color = Color.white;
            }
        }

        private void ApplyVisibleCardCount()
        {
            if (_cardBackImages == null)
            {
                return;
            }

            for (var i = 0; i < _cardBackImages.Length; i++)
            {
                var image = _cardBackImages[i];
                if (image != null)
                {
                    image.gameObject.SetActive(i < _visibleCardCount);
                }
            }
        }

        private void ApplyLayout()
        {
            if (_handRoot == null || _cardBackImages == null || _visibleCardCount <= 0)
            {
                return;
            }

            var availableWidth = _handRoot.rect.width > 0.1f
                ? _handRoot.rect.width * _handWidthFactor
                : Screen.width * _handWidthFactor;
            var baseY = -(_cardSize.y * 0.5f);

            for (var i = 0; i < _visibleCardCount && i < _cardBackImages.Length; i++)
            {
                var image = _cardBackImages[i];
                if (image == null)
                {
                    continue;
                }

                var layout = HandPresenterLayout.CalculateFanLayout(
                    _visibleCardCount,
                    i,
                    availableWidth,
                    _minSpacing,
                    _maxSpacing,
                    _maxRotationDegrees,
                    _arcDepth,
                    _edgeLift,
                    _minimumLargeHandScale);

                var rectTransform = image.rectTransform;
                rectTransform.sizeDelta = _cardSize;
                rectTransform.anchoredPosition = new Vector2(
                    layout.AnchoredPosition.x,
                    baseY - layout.AnchoredPosition.y);
                rectTransform.localRotation = Quaternion.Euler(0f, 0f, -layout.RotationDegrees);
                rectTransform.localScale = Vector3.one * layout.Scale;
                rectTransform.SetSiblingIndex(i);
            }
        }
    }
}
