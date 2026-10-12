using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class RedDragonEndTurnFlamesVfx : MonoBehaviour
    {
        public const string FramesResourcePath = "Project333/Presentation/RedDragon/EndTurnFlames";
        public const int FrameCount = 16;
        public const float FramesPerSecond = 12f;
        public const float DurationSeconds = FrameCount / FramesPerSecond;
        public const float ImpactDelaySeconds = .25f;
        internal static readonly Vector2 OriginPoint = new Vector2(.5f, 16f / 512f);

        private readonly TileView[] _tiles = new TileView[BoardState.ColumnCount * BoardState.RowCount];
        private readonly RectTransform[] _anchors = new RectTransform[BoardState.ColumnCount * BoardState.RowCount];
        private readonly Vector2[] _origins = new Vector2[BoardState.ColumnCount * BoardState.RowCount];
        private readonly Image[] _images = new Image[BoardState.ColumnCount * BoardState.RowCount];
        private readonly Vector3[] _corners = new Vector3[4];
        private Sprite[] _frames;
        private RectTransform _root;
        private Camera _viewCamera;
        private PlayerId _owner;
        private float _startedAt;
        private bool _playing;

        internal bool IsPlaying => _playing;
        internal float RemainingDurationSeconds => _playing
            ? Mathf.Max(0f, DurationSeconds - (Time.unscaledTime - _startedAt)) : 0f;

        internal float Play(BoardPresenter board, PlayerId targetOwnerId)
        {
            Cancel();
            if (!isActiveAndEnabled || board == null ||
                (targetOwnerId != PlayerId.Player && targetOwnerId != PlayerId.AI)) return 0f;
            var tiles = targetOwnerId == PlayerId.Player ? board.PlayerTileViews : board.AITileViews;
            if (tiles == null) return 0f;
            Canvas canvas = null;
            for (var row = 0; row < BoardState.RowCount; row++)
            for (var column = 0; column < BoardState.ColumnCount; column++)
            {
                var index = row * BoardState.ColumnCount + column;
                TileView selected = null;
                foreach (var tile in tiles)
                    if (tile != null && tile.OwnerId == targetOwnerId &&
                        tile.Coord.Row == row && tile.Coord.Column == column) { selected = tile; break; }
                if (selected == null) return 0f;
                _tiles[index] = selected;
                var view = selected.GetComponent<TileTextView>();
                _anchors[index] = view?.ActiveEffectButtonAnchor ?? selected.transform as RectTransform;
                // Tile backgrounds have independent root Canvases. Project their ground
                // anchors into one overlay instead of parenting flames to unit visuals.
                var tileCanvas = _anchors[index]?.GetComponentInParent<Canvas>(true)
                    ?? view?.GetVisualRootCanvas() ?? selected.GetComponentInParent<Canvas>();
                if (_anchors[index] == null || tileCanvas == null) return 0f;
                if (canvas == null) canvas = tileCanvas.rootCanvas;
            }
            if (canvas == null || !canvas.isActiveAndEnabled) return 0f;
            if (_frames == null || _frames.Length == 0)
            {
                _frames = Resources.LoadAll<Sprite>(FramesResourcePath);
                Array.Sort(_frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            if (_frames.Length != FrameCount) return 0f;
            _owner = targetOwnerId;
            _viewCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            EnsureOverlay(canvas);
            if (!UpdateLayout()) { Cancel(); return 0f; }
            foreach (var image in _images) { image.sprite = _frames[0]; image.color = Color.white; }
            _root.gameObject.SetActive(true);
            var overlay = _root.GetComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = 4907;
            _startedAt = Time.unscaledTime;
            _playing = true;
            return DurationSeconds;
        }

        private void EnsureOverlay(Canvas canvas)
        {
            if (_root == null)
            {
                var root = new GameObject("RedDragonEndTurnFlamesOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
                root.SetActive(false);
                _root = (RectTransform)root.transform;
                var group = root.GetComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
                for (var index = 0; index < _images.Length; index++)
                {
                    var image = new GameObject("RedDragonEndTurnFlame_" + index, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    image.rectTransform.SetParent(_root, false);
                    image.raycastTarget = false;
                    image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.one * .5f;
                    image.rectTransform.pivot = OriginPoint;
                    _images[index] = image;
                }
            }
            _root.SetParent(canvas.transform, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;
        }

        private bool UpdateLayout()
        {
            if (_root == null || _root.parent == null || !_root.parent.gameObject.activeInHierarchy) return false;
            var viewRotation = _viewCamera != null ? _viewCamera.transform.rotation : Quaternion.identity;
            var right = ((Vector2)_root.InverseTransformVector(viewRotation * Vector3.right)).normalized;
            var up = ((Vector2)_root.InverseTransformVector(viewRotation * Vector3.up)).normalized;
            var size = float.PositiveInfinity;
            for (var index = 0; index < _anchors.Length; index++)
            {
                if (_tiles[index] == null || _tiles[index].OwnerId != _owner ||
                    _tiles[index].Coord.Row != index / BoardState.ColumnCount ||
                    _tiles[index].Coord.Column != index % BoardState.ColumnCount ||
                    _anchors[index] == null || !_anchors[index].gameObject.activeInHierarchy) return false;
                var center = (Vector2)_root.InverseTransformPoint(_anchors[index].TransformPoint(_anchors[index].rect.center));
                _anchors[index].GetWorldCorners(_corners);
                var xMin = float.PositiveInfinity;
                var xMax = float.NegativeInfinity;
                var yMin = float.PositiveInfinity;
                var yMax = float.NegativeInfinity;
                foreach (var corner in _corners)
                {
                    var point = (Vector2)_root.InverseTransformPoint(corner) - center;
                    var x = Vector2.Dot(point, right);
                    var y = Vector2.Dot(point, up);
                    xMin = Mathf.Min(xMin, x); xMax = Mathf.Max(xMax, x);
                    yMin = Mathf.Min(yMin, y); yMax = Mathf.Max(yMax, y);
                }
                var tileWidth = xMax - xMin;
                var tileHeight = yMax - yMin;
                if (tileWidth <= 0f || tileHeight <= 0f) return false;
                // The original horizontal flame occupies about 480px of its 512px
                // square cell. Scale uniformly; neither rotate nor elongate the art.
                size = Mathf.Min(size, Mathf.Min(tileWidth * (512f / 480f), tileHeight * 1.15f));
                _origins[index] = center + up * (yMin + tileHeight * .05f);
            }
            var bounds = _root.rect;
            var margin = Mathf.Min(bounds.width, bounds.height) * .01f;
            bounds.xMin += margin; bounds.xMax -= margin;
            bounds.yMin += margin; bounds.yMax -= margin;
            foreach (var origin in _origins)
            {
                if (!bounds.Contains(origin)) return false;
                for (var xSide = -1; xSide <= 1; xSide += 2)
                for (var ySide = 0; ySide < 2; ySide++)
                {
                    var direction = right * (xSide * .5f) + up * (ySide - OriginPoint.y);
                    size = CapSize(size, origin.x, direction.x, bounds.xMin, bounds.xMax);
                    size = CapSize(size, origin.y, direction.y, bounds.yMin, bounds.yMax);
                }
            }
            if (size <= 0f || float.IsInfinity(size)) return false;
            var rotation = Quaternion.Inverse(_root.rotation) * viewRotation;
            for (var index = 0; index < _images.Length; index++)
            {
                var rect = _images[index].rectTransform;
                rect.localPosition = _origins[index];
                rect.localRotation = rotation;
                rect.localScale = Vector3.one;
                rect.sizeDelta = Vector2.one * size;
                var order = 0;
                for (var other = 0; other < _origins.Length; other++)
                {
                    var relativeHeight = Vector2.Dot(_origins[other] - _origins[index], up);
                    if (relativeHeight > .01f || (Mathf.Abs(relativeHeight) <= .01f && other < index)) order++;
                }
                if (rect.GetSiblingIndex() != order) rect.SetSiblingIndex(order);
            }
            return true;
        }

        private static float CapSize(float size, float origin, float direction, float minimum, float maximum)
        {
            if (direction > .00001f) return Mathf.Min(size, (maximum - origin) / direction);
            if (direction < -.00001f) return Mathf.Min(size, (origin - minimum) / -direction);
            return size;
        }

        private void LateUpdate()
        {
            if (!_playing) return;
            var elapsed = Mathf.Max(0f, Time.unscaledTime - _startedAt);
            if (elapsed >= DurationSeconds || !UpdateLayout()) { Cancel(); return; }
            var frame = _frames[Mathf.Min(FrameCount - 1, Mathf.FloorToInt(elapsed * FramesPerSecond))];
            foreach (var image in _images) image.sprite = frame;
        }

        internal void Cancel()
        {
            _playing = false;
            if (_root != null) _root.gameObject.SetActive(false);
        }
        private void OnDisable() => Cancel();
        private void OnDestroy()
        {
            Cancel();
            if (_root == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(_root.gameObject); else DestroyImmediate(_root.gameObject);
        }
    }
}
