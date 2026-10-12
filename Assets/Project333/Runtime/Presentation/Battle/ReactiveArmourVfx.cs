using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    // The gateway already canceled the attack and resolved destruction. This cue only
    // keeps the confirmed pre-resolution bodies visible through the barrier and blast.
    public sealed class ReactiveArmourVfx : MonoBehaviour
    {
        public const string FramesResourcePath = "Project333/Vfx/ReactiveArmour/Activation";
        public const int FrameCount = 8;
        public const float BarrierHoldSeconds = .12f;
        public const float FrameDurationSeconds = .10f;
        public const float EngulfDelaySeconds = BarrierHoldSeconds + 3f * FrameDurationSeconds;
        public const float TailDurationSeconds = 4f * FrameDurationSeconds;
        public const float DurationSeconds = EngulfDelaySeconds + TailDurationSeconds;
        private static readonly Vector2 BarrierPivot = new Vector2(160f / 512f, .5f);
        private const float PeakCenterOffset = (370f - 160f) / 512f;
        private static Sprite[] s_frames;
        private static Dictionary<string, Rect> s_manifest;
        private static readonly Dictionary<Sprite, Rect> s_bounds = new Dictionary<Sprite, Rect>();

        private readonly TileTextView[] _views = new TileTextView[2];
        private readonly TileView[] _tiles = new TileView[2];
        private readonly string[] _runtimeIds = new string[2];
        private readonly Image[] _bodyImages = new Image[2];
        private readonly RectTransform[] _anchors = new RectTransform[2];
        private readonly Canvas[] _sourceCanvases = new Canvas[2];
        private readonly Rect[] _bodyBounds = new Rect[2];
        private readonly Rect[] _projectedBodies = new Rect[2];
        private readonly Vector3[] _corners = new Vector3[4];
        private Canvas _canvas;
        private RectTransform _root;
        private Image _image;
        private float _startedAt, _engulfStartedAt, _endsAt;
        private bool _playing, _hasEngulfed;

        internal bool IsPlaying => _playing;
        internal float RemainingDurationSeconds => _playing
            ? Mathf.Max(0f, _endsAt - Time.unscaledTime) : 0f;

        internal float Play(TileTextView master, string masterRuntimeId,
            TileTextView attacker, string attackerRuntimeId)
        {
            Cancel();
            if (!isActiveAndEnabled || master == null || attacker == null ||
                string.IsNullOrWhiteSpace(masterRuntimeId) || string.IsNullOrWhiteSpace(attackerRuntimeId) ||
                masterRuntimeId == attackerRuntimeId || !LoadFrames()) return 0f;
            _views[0] = master; _views[1] = attacker;
            _runtimeIds[0] = masterRuntimeId; _runtimeIds[1] = attackerRuntimeId;
            for (var index = 0; index < 2; index++)
            {
                var view = _views[index];
                _tiles[index] = view.GetComponent<TileView>();
                if (!MatchesConfirmedView(index)) { Cancel(); return 0f; }
                var body = view.OccupantVisualImage;
                _bodyImages[index] = body != null && body.isActiveAndEnabled && body.sprite != null ? body : null;
                _anchors[index] = _bodyImages[index] != null ? body.rectTransform
                    : view.ActiveEffectButtonAnchor ?? view.transform as RectTransform;
                var canvas = _anchors[index]?.GetComponentInParent<Canvas>() ?? view.GetVisualRootCanvas();
                if (_anchors[index] == null || canvas == null || !canvas.isActiveAndEnabled)
                { Cancel(); return 0f; }
                _sourceCanvases[index] = canvas.rootCanvas;
                _bodyBounds[index] = _bodyImages[index] == null ? new Rect(0, 0, 1, 1)
                    : BodyBounds(body.sprite, view.VisualControllerName,
                        _tiles[index]?.CurrentOccupantCardId);
            }
            // TileBackground belongs to the screen UI Canvas. The unit body may own
            // a small WorldSpace Canvas, whose sorting cannot overlay a card reveal.
            var overlayCanvas = master.ActiveEffectButtonAnchor?.GetComponentInParent<Canvas>();
            _canvas = overlayCanvas != null ? overlayCanvas.rootCanvas : _sourceCanvases[0];
            EnsureOverlay(_canvas);
            if (!UpdateLayout()) { Cancel(); return 0f; }
            _image.sprite = s_frames[0]; _image.color = Color.white;
            _root.gameObject.SetActive(true);
            var overlay = _root.GetComponent<Canvas>(); overlay.overrideSorting = true; overlay.sortingOrder = 4908;
            _startedAt = Time.unscaledTime; _endsAt = _startedAt + DurationSeconds;
            _hasEngulfed = false; _playing = true;
            return DurationSeconds;
        }

        internal static int ResolveFrameIndex(float elapsed)
        {
            if (elapsed < BarrierHoldSeconds) return 0;
            return Mathf.Clamp(1 + Mathf.FloorToInt((elapsed - BarrierHoldSeconds) / FrameDurationSeconds),
                1, FrameCount - 1);
        }

        internal static bool TryBuildLayout(Rect masterBody, Rect attackerBody,
            out Vector2 barrierPosition, out float rotationDegrees, out float uniformCellSize)
        {
            barrierPosition = Vector2.zero; rotationDegrees = 0f; uniformCellSize = 0f;
            if (masterBody.width <= 0f || masterBody.height <= 0f ||
                attackerBody.width <= 0f || attackerBody.height <= 0f) return false;
            var direction = attackerBody.center - masterBody.center;
            var distance = direction.magnitude;
            if (distance <= .01f) return false;
            direction /= distance;
            // Fit the explosion's round core around the whole attacker, not the empty
            // 512-cell. Put its barrier pivot on the line between the two bodies.
            uniformCellSize = Mathf.Max(attackerBody.size.magnitude / .40f, masterBody.height / .62f);
            var offset = Mathf.Min(distance * .72f, uniformCellSize * PeakCenterOffset);
            barrierPosition = attackerBody.center - direction * offset;
            rotationDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return true;
        }

        private bool MatchesConfirmedView(int index)
        {
            var view = _views[index];
            var tile = _tiles[index];
            return view != null && view.isActiveAndEnabled && view.VisualRuntimeId == _runtimeIds[index] &&
                   tile != null && tile.CurrentOccupantRuntimeId == _runtimeIds[index];
        }

        private static bool LoadFrames()
        {
            if (s_frames != null && s_frames.Length == FrameCount) return true;
            s_frames = Resources.LoadAll<Sprite>(FramesResourcePath);
            Array.Sort(s_frames, (left, right) => string.CompareOrdinal(left.name, right.name));
            return s_frames.Length == FrameCount;
        }

        private void EnsureOverlay(Canvas canvas)
        {
            if (_root == null)
            {
                var host = new GameObject("ReactiveArmourOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
                host.SetActive(false); _root = host.GetComponent<RectTransform>();
                var group = host.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
                _image = new GameObject("ReactiveArmourBarrierBlast", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                _image.rectTransform.SetParent(_root, false); _image.raycastTarget = false; _image.preserveAspect = true;
                _image.rectTransform.anchorMin = _image.rectTransform.anchorMax = Vector2.one * .5f;
                _image.rectTransform.pivot = BarrierPivot;
            }
            _root.SetParent(canvas.transform, false); _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;
            var overlay = _root.GetComponent<Canvas>(); overlay.overrideSorting = true; overlay.sortingOrder = 4908;
        }

        private bool UpdateLayout()
        {
            if (_root == null || _canvas == null || !_canvas.isActiveAndEnabled ||
                _root.parent == null || !_root.parent.gameObject.activeInHierarchy ||
                _root.rect.width <= 0f || _root.rect.height <= 0f) return false;
            for (var index = 0; index < 2; index++)
            {
                if (!MatchesConfirmedView(index) || _anchors[index] == null ||
                    !_anchors[index].gameObject.activeInHierarchy || _sourceCanvases[index] == null ||
                    !_sourceCanvases[index].isActiveAndEnabled) return false;
                var anchor = _anchors[index];
                var draw = _bodyImages[index] == null ? anchor.rect : _bodyImages[index].GetPixelAdjustedRect();
                if (_bodyImages[index] != null && _bodyImages[index].preserveAspect)
                {
                    // Keep the initial fitting bounds when Death swaps the displayed frame.
                    var sprite = _bodyImages[index].sprite;
                    if (sprite == null) return false;
                    var size = sprite.rect.size;
                    var scale = Mathf.Min(draw.width / size.x, draw.height / size.y);
                    size *= scale; draw = new Rect(draw.center - size * .5f, size);
                }
                var bounds = _bodyBounds[index];
                var min = draw.min + Vector2.Scale(bounds.min, draw.size);
                var max = draw.min + Vector2.Scale(bounds.max, draw.size);
                _corners[0] = anchor.TransformPoint(new Vector3(min.x, min.y));
                _corners[1] = anchor.TransformPoint(new Vector3(min.x, max.y));
                _corners[2] = anchor.TransformPoint(new Vector3(max.x, max.y));
                _corners[3] = anchor.TransformPoint(new Vector3(max.x, min.y));
                var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                foreach (var corner in _corners)
                {
                    if (!TryProjectPoint(index, corner, out var point)) return false;
                    minimum = Vector2.Min(minimum, point); maximum = Vector2.Max(maximum, point);
                }
                _projectedBodies[index] = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
            }
            if (!TryBuildLayout(_projectedBodies[0], _projectedBodies[1], out var position, out var rotation, out var cellSize)) return false;
            _image.rectTransform.anchoredPosition = position;
            _image.rectTransform.sizeDelta = Vector2.one * cellSize;
            _image.rectTransform.localScale = Vector3.one;
            _image.rectTransform.localRotation = Quaternion.Euler(0, 0, rotation);
            return true;
        }

        private bool TryProjectPoint(int index, Vector3 worldPoint, out Vector2 localPoint)
        {
            var source = _sourceCanvases[index];
            if (source == _canvas) { localPoint = _root.InverseTransformPoint(worldPoint); return true; }
            var sourceCamera = source.renderMode == RenderMode.ScreenSpaceOverlay ? null : source.worldCamera;
            var targetCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            var point = RectTransformUtility.WorldToScreenPoint(sourceCamera, worldPoint);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, point, targetCamera, out localPoint);
        }

        private static Rect BodyBounds(Sprite sprite, string controllerName, string cardId)
        {
            if (s_bounds.TryGetValue(sprite, out var cached)) return cached;
            if (s_manifest == null)
            {
                s_manifest = new Dictionary<string, Rect>(StringComparer.Ordinal);
                var manifest = Resources.Load<StatusSpriteBoundsAsset>(ActionDebuffStatusVfx.BoundsResourcePath);
                if (manifest != null) foreach (var entry in manifest.Entries)
                    s_manifest[StatusSpriteBoundsAsset.CreateKey(entry.VisualKey, entry.TextureName, entry.SpriteName, entry.SpriteRect)] = entry.NormalizedBounds;
            }
            var key = StatusSpriteBoundsAsset.CreateKey(string.IsNullOrEmpty(controllerName) ? cardId : controllerName,
                sprite.texture.name, sprite.name, sprite.rect);
            if (!s_manifest.TryGetValue(key, out var result))
            {
                key = StatusSpriteBoundsAsset.CreateKey(cardId, sprite.texture.name, sprite.name, sprite.rect);
                if (!s_manifest.TryGetValue(key, out result))
                {
                    var min = (Vector2)sprite.bounds.min * sprite.pixelsPerUnit + sprite.pivot;
                    var max = (Vector2)sprite.bounds.max * sprite.pixelsPerUnit + sprite.pivot;
                    result = Rect.MinMaxRect(Mathf.Clamp01(min.x / sprite.rect.width), Mathf.Clamp01(min.y / sprite.rect.height),
                        Mathf.Clamp01(max.x / sprite.rect.width), Mathf.Clamp01(max.y / sprite.rect.height));
                }
            }
            if (result.width <= 0 || result.height <= 0) result = new Rect(0, 0, 1, 1);
            s_bounds.Add(sprite, result); return result;
        }

        private void LateUpdate()
        {
            if (!_playing) return;
            var elapsed = Mathf.Max(0f, Time.unscaledTime - _startedAt);
            if (!UpdateLayout()) { Cancel(); return; }
            if (!_hasEngulfed)
            {
                // A slow frame must not finish the blast before the orchestrator can
                // start the actual Death clip. Hold its growing face until that cue.
                _image.sprite = s_frames[Mathf.Min(3, ResolveFrameIndex(elapsed))];
                return;
            }
            if (Time.unscaledTime >= _endsAt) { Cancel(); return; }
            var tailAge = Mathf.Max(0f, Time.unscaledTime - _engulfStartedAt);
            _image.sprite = s_frames[Mathf.Min(FrameCount - 1, 4 + Mathf.FloorToInt(tailAge / FrameDurationSeconds))];
            _image.color = new Color(1, 1, 1, Mathf.Clamp01((_endsAt - Time.unscaledTime) / FrameDurationSeconds));
        }

        internal bool TryBeginEngulf()
        {
            if (!_playing || !isActiveAndEnabled || !UpdateLayout()) { Cancel(); return false; }
            if (_hasEngulfed) return true;
            if (Time.unscaledTime - _startedAt < EngulfDelaySeconds) return false;
            _hasEngulfed = true; _engulfStartedAt = Time.unscaledTime;
            _endsAt = Mathf.Max(_endsAt, _engulfStartedAt + TailDurationSeconds);
            _image.sprite = s_frames[4]; _image.color = Color.white;
            return true;
        }

        internal void Cancel()
        {
            _playing = false; _hasEngulfed = false; _endsAt = 0f; _canvas = null;
            for (var index = 0; index < 2; index++)
            { _views[index] = null; _tiles[index] = null; _runtimeIds[index] = null; _bodyImages[index] = null; _anchors[index] = null; _sourceCanvases[index] = null; }
            if (_root != null) _root.gameObject.SetActive(false);
        }
        private void OnDisable() => Cancel();
        private void OnDestroy()
        {
            Cancel();
            if (_root != null)
            { if (UnityEngine.Application.isPlaying) Destroy(_root.gameObject); else DestroyImmediate(_root.gameObject); }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { s_frames = null; s_manifest = null; s_bounds.Clear(); }
    }
}
