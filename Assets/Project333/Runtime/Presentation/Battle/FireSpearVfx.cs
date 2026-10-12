using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    // One visual projectile; the gateway has already applied both column damage packets.
    public sealed class FireSpearVfx : MonoBehaviour
    {
        public const string FramesResourcePath = "Project333/SpellEffects/FireSpear/FlameSpear";
        public const int FrameCount = 8;
        public const float PlaybackSpeed = 1.5f;
        public const float FramesPerSecond = 10f * PlaybackSpeed;
        public const float ImpactDelaySeconds = .30f / PlaybackSpeed;
        public const float TailDurationSeconds = .50f / PlaybackSpeed;
        private const float FadeDurationSeconds = .20f / PlaybackSpeed;
        public const float DurationSeconds = ImpactDelaySeconds + TailDurationSeconds;

        private static Sprite[] s_frames;
        private static Rect s_visibleBounds;
        private static Vector2 s_headNormalized;
        private readonly TileView[] _tiles = new TileView[BoardState.RowCount];
        private readonly RectTransform[] _anchors = new RectTransform[BoardState.RowCount];
        private readonly Canvas[] _anchorCanvases = new Canvas[BoardState.RowCount];
        private readonly Vector2[] _centers = new Vector2[BoardState.RowCount];
        private readonly Vector3[] _corners = new Vector3[4];
        private BoardPresenter _board;
        private Canvas _canvas;
        private RectTransform _root, _clip;
        private Image _image;
        private PlayerId _targetOwner, _caster;
        private int _column;
        private float _startedAt, _impactStartedAt, _endsAt;
        private float _startX, _hitX, _finishX, _spriteSize;
        private float _flightSign = 1f;
        private bool _playing, _hasImpacted;
        internal float RemainingDurationSeconds => _playing ? Mathf.Max(0f, _endsAt - Time.unscaledTime) : 0f;
        internal static Rect VisibleBounds => s_visibleBounds;
        internal static Vector2 HeadNormalized => s_headNormalized;

        internal float Play(BoardPresenter board, PlayerId caster, PlayerId targetOwner, int column)
        {
            Cancel();
            if (!isActiveAndEnabled || board == null || !board.isActiveAndEnabled ||
                column < 0 || column >= BoardState.ColumnCount || !IsPlayer(caster) || !IsPlayer(targetOwner))
                return 0f;
            var tiles = targetOwner == PlayerId.Player ? board.PlayerTileViews : board.AITileViews;
            if (tiles == null) return 0f;
            Canvas canvas = null;
            for (var row = 0; row < BoardState.RowCount; row++)
            {
                TileView selected = null;
                foreach (var tile in tiles)
                    if (tile != null && tile.OwnerId == targetOwner && tile.Coord == new TileCoord(column, row))
                    { selected = tile; break; }
                if (selected == null) return 0f;
                var view = selected.GetComponent<TileTextView>();
                _tiles[row] = selected;
                _anchors[row] = view?.ActiveEffectButtonAnchor ?? selected.transform as RectTransform;
                // TileBackground is in the screen UI Canvas; the occupant body may use its own
                // small WorldSpace Canvas and must not determine this projectile's viewport.
                var tileCanvas = _anchors[row]?.GetComponentInParent<Canvas>() ?? view?.GetVisualRootCanvas();
                if (_anchors[row] == null || tileCanvas == null || !tileCanvas.isActiveAndEnabled) return 0f;
                // Actual battle tiles each own a root Canvas. Keep the source of each anchor
                // and project its screen position into one overlay instead of rejecting the cast.
                _anchorCanvases[row] = tileCanvas.rootCanvas;
                if (canvas == null) canvas = tileCanvas.rootCanvas;
            }
            if (!LoadFrames() || canvas == null) return 0f;
            _board = board; _canvas = canvas; _caster = caster; _targetOwner = targetOwner; _column = column;
            EnsureOverlay(canvas);
            if (!UpdateLayout()) { Cancel(); return 0f; }
            _root.gameObject.SetActive(true);
            var activeCanvas = _root.GetComponent<Canvas>(); activeCanvas.overrideSorting = true; activeCanvas.sortingOrder = 4908;
            _startedAt = Time.unscaledTime; _endsAt = _startedAt + DurationSeconds;
            _hasImpacted = false; _playing = true;
            _image.sprite = s_frames[0]; _image.color = Color.white; PositionHead(_startX);
            return DurationSeconds;
        }

        private static bool IsPlayer(PlayerId player) => player == PlayerId.Player || player == PlayerId.AI;
        private static bool LoadFrames()
        {
            if (s_frames != null && s_frames.Length == FrameCount) return true;
            s_frames = Resources.LoadAll<Sprite>(FramesResourcePath);
            Array.Sort(s_frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            if (s_frames.Length != FrameCount) return false;
            // Default calibration of the preserved full512 cells; readable native art is measured once.
            s_visibleBounds = new Rect(51f / 512f, 171f / 512f, 412f / 512f, 160f / 512f);
            s_headNormalized = new Vector2(450f / 512f, 227f / 512f);
            var texture = s_frames[0].texture;
            if (!texture.isReadable || s_frames[0].packed) return true;
            var pixels = texture.GetPixels32();
            var minX = 512; var minY = 512; var maxX = -1; var maxY = -1;
            double brightY = 0, brightWeight = 0;
            foreach (var frame in s_frames)
            {
                if (frame.texture != texture || frame.rect.size != new Vector2(512, 512)) continue;
                var x0 = Mathf.RoundToInt(frame.rect.x); var y0 = Mathf.RoundToInt(frame.rect.y);
                for (var y = 0; y < 512; y++)
                for (var x = 0; x < 512; x++)
                {
                    var pixel = pixels[(y0 + y) * texture.width + x0 + x];
                    if (pixel.a < 16) continue;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    // The white-hot head, rather than the wavering outer flames, defines the flight axis.
                    if (x >= 410 && pixel.a >= 200 && pixel.r >= 240 && pixel.g >= 220 && pixel.b >= 100)
                    { brightY += (y + .5) * pixel.a; brightWeight += pixel.a; }
                }
            }
            if (maxX >= minX && maxY >= minY)
            {
                s_visibleBounds = Rect.MinMaxRect(minX / 512f, minY / 512f, (maxX + 1) / 512f, (maxY + 1) / 512f);
                s_headNormalized.x = (maxX - 5f) / 512f;
                if (brightWeight > 0) s_headNormalized.y = (float)(brightY / brightWeight / 512d);
            }
            return true;
        }

        private void EnsureOverlay(Canvas canvas)
        {
            if (_root == null)
            {
                var host = new GameObject("FireSpearOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
                host.SetActive(false); _root = host.GetComponent<RectTransform>();
                var overlay = host.GetComponent<Canvas>(); overlay.overrideSorting = true; overlay.sortingOrder = 4908;
                var group = host.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
                _clip = new GameObject("FireSpearColumnClip", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
                _clip.SetParent(_root, false); _clip.anchorMin = _clip.anchorMax = Vector2.one * .5f; _clip.pivot = Vector2.one * .5f;
                _image = new GameObject("FireSpearProjectile", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                _image.rectTransform.SetParent(_clip, false); _image.raycastTarget = false; _image.preserveAspect = true;
                _image.rectTransform.anchorMin = _image.rectTransform.anchorMax = Vector2.one * .5f;
                _image.rectTransform.pivot = Vector2.one * .5f;
            }
            _root.SetParent(canvas.transform, false); _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
            var rootCanvas = _root.GetComponent<Canvas>(); rootCanvas.overrideSorting = true; rootCanvas.sortingOrder = 4908;
            _root.offsetMin = _root.offsetMax = Vector2.zero;
        }

        private bool UpdateLayout()
        {
            if (_root == null || _canvas == null || !_canvas.isActiveAndEnabled ||
                _root.parent == null || !_root.parent.gameObject.activeInHierarchy ||
                _root.rect.width <= 0f || _root.rect.height <= 0f) return false;
            var centers = _centers;
            for (var row = 0; row < BoardState.RowCount; row++)
            {
                var tile = _tiles[row]; var anchor = _anchors[row];
                if (tile == null || tile.OwnerId != _targetOwner || tile.Coord != new TileCoord(_column, row) ||
                    anchor == null || !anchor.gameObject.activeInHierarchy || anchor.rect.width <= 0f || anchor.rect.height <= 0f)
                    return false;
                if (_anchorCanvases[row] == null || !_anchorCanvases[row].isActiveAndEnabled ||
                    !TryProjectAnchorPoint(row, anchor.TransformPoint(anchor.rect.center), out centers[row])) return false;
            }
            var first = _targetOwner == _caster ? 1 : 0;
            var last = 1 - first;
            var axis = centers[last] - centers[first];
            if (axis.sqrMagnitude < .001f) return false;
            axis.Normalize();
            // RectMask2D measures corner2 - corner0 and rejects a 180-degree mask as a
            // negative rectangle. Keep its axis positive; turn only the flying artwork.
            _flightSign = axis.x < 0f ? -1f : 1f;
            axis *= _flightSign;
            var normal = new Vector2(-axis.y, axis.x);
            var origin = (centers[0] + centers[1]) * .5f;
            var minAlong = float.PositiveInfinity; var maxAlong = float.NegativeInfinity;
            var minCross = float.PositiveInfinity; var maxCross = float.NegativeInfinity;
            var nearTileWidth = 0f;
            for (var row = 0; row < BoardState.RowCount; row++)
            {
                _anchors[row].GetWorldCorners(_corners);
                var tileMin = float.PositiveInfinity; var tileMax = float.NegativeInfinity;
                foreach (var corner in _corners)
                {
                    if (!TryProjectAnchorPoint(row, corner, out var point)) return false;
                    var relative = point - origin;
                    var along = Vector2.Dot(relative, axis); var cross = Vector2.Dot(relative, normal);
                    minAlong = Mathf.Min(minAlong, along); maxAlong = Mathf.Max(maxAlong, along);
                    minCross = Mathf.Min(minCross, cross); maxCross = Mathf.Max(maxCross, cross);
                    tileMin = Mathf.Min(tileMin, along); tileMax = Mathf.Max(tileMax, along);
                }
                if (row == first) nearTileWidth = tileMax - tileMin;
            }
            var length = maxAlong - minAlong; var thickness = maxCross - minCross;
            if (length <= .01f || thickness <= .01f ||
                Mathf.Abs(Vector2.Dot(centers[1] - centers[0], normal)) > thickness * .05f) return false;
            _clip.localPosition = origin + axis * ((minAlong + maxAlong) * .5f) + normal * ((minCross + maxCross) * .5f);
            _clip.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg);
            _clip.sizeDelta = new Vector2(length, thickness);
            var projectedLength = Mathf.Max(thickness * 1.2f, length * .78f);
            _spriteSize = projectedLength / Mathf.Max(.01f, s_visibleBounds.width);
            // Cap only the rendered flame thickness, not an empty512px square.
            _spriteSize = Mathf.Min(_spriteSize, thickness * .86f / Mathf.Max(.01f, s_visibleBounds.height));
            _image.rectTransform.sizeDelta = Vector2.one * _spriteSize;
            _image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _flightSign < 0f ? 180f : 0f);
            _startX = Vector2.Dot(centers[first] - (Vector2)_clip.localPosition, axis) - _flightSign * nearTileWidth * .60f;
            _hitX = Vector2.Dot(centers[last] - (Vector2)_clip.localPosition, axis);
            _finishX = _flightSign * (length * .5f + (s_headNormalized.x - s_visibleBounds.xMin) * _spriteSize);
            return true;
        }

        private bool TryProjectAnchorPoint(int row, Vector3 worldPoint, out Vector2 localPoint)
        {
            var source = _anchorCanvases[row];
            if (source == _canvas)
            {
                localPoint = _root.InverseTransformPoint(worldPoint);
                return true;
            }
            var sourceCamera = source.renderMode == RenderMode.ScreenSpaceOverlay ? null : source.worldCamera;
            var overlayCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            var screenPoint = RectTransformUtility.WorldToScreenPoint(sourceCamera, worldPoint);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screenPoint, overlayCamera, out localPoint);
        }
        private void PositionHead(float x)
        {
            _image.rectTransform.anchoredPosition = new Vector2(
                x - _flightSign * (s_headNormalized.x - .5f) * _spriteSize,
                -_flightSign * (s_headNormalized.y - .5f) * _spriteSize);
        }

        internal bool TryBeginImpact()
        {
            if (!_playing || !isActiveAndEnabled || _board == null || !_board.isActiveAndEnabled || !UpdateLayout())
            { Cancel(); return false; }
            if (_hasImpacted) return true;
            if (Time.unscaledTime - _startedAt < ImpactDelaySeconds) return false;
            _hasImpacted = true; _impactStartedAt = Time.unscaledTime;
            _endsAt = Mathf.Max(_endsAt, _impactStartedAt + TailDurationSeconds);
            PositionHead(_hitX); _image.sprite = s_frames[3]; _image.color = Color.white;
            return true;
        }

        private void LateUpdate()
        {
            if (!_playing) return;
            if (_board == null || !_board.isActiveAndEnabled || !UpdateLayout()) { Cancel(); return; }
            var elapsed = Mathf.Max(0f, Time.unscaledTime - _startedAt);
            if (!_hasImpacted)
            {
                if (elapsed >= ImpactDelaySeconds) { TryBeginImpact(); return; }
                PositionHead(Mathf.Lerp(_startX, _hitX, Mathf.Clamp01(elapsed / ImpactDelaySeconds)));
                _image.sprite = s_frames[Mathf.Min(2, Mathf.FloorToInt(elapsed * FramesPerSecond))];
                return;
            }
            if (Time.unscaledTime >= _endsAt) { Cancel(); return; }
            var tailAge = Mathf.Max(0f, Time.unscaledTime - _impactStartedAt);
            PositionHead(Mathf.Lerp(_hitX, _finishX, Mathf.Clamp01(tailAge / TailDurationSeconds)));
            _image.sprite = s_frames[Mathf.Min(FrameCount - 1, 3 + Mathf.FloorToInt(tailAge * FramesPerSecond))];
            _image.color = new Color(1f, 1f, 1f, Mathf.Clamp01((TailDurationSeconds - tailAge) / FadeDurationSeconds));
        }

        internal void Cancel()
        {
            _playing = false; _hasImpacted = false; _endsAt = 0f; _board = null; _canvas = null;
            if (_root != null) _root.gameObject.SetActive(false);
        }
        private void OnDisable() => Cancel();
        private void OnDestroy()
        {
            Cancel(); if (_root == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(_root.gameObject); else DestroyImmediate(_root.gameObject);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { s_frames = null; s_visibleBounds = Rect.zero; s_headNormalized = Vector2.zero; }
    }
}

