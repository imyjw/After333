using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    // The gateway has already canceled the attack and resolved destruction. The
    // flight and blast retain the confirmed bodies until the orchestrator starts Death.
    public sealed class IronDomeMissileVfx : MonoBehaviour
    {
        public const string MissileResourcePath = "Project333/Vfx/IronDome/Missile";
        public const int ExplosionFrameCount = 16;
        public const float FlightDurationSeconds = .5f;
        public const float ExplosionDurationSeconds = TimedBombPresentationVfx.ExplosionDuration;
        public const float ImpactDelaySeconds = FlightDurationSeconds + ExplosionDurationSeconds * 4f / ExplosionFrameCount;
        public const float TailDurationSeconds = ExplosionDurationSeconds * 12f / ExplosionFrameCount;
        public const float DurationSeconds = FlightDurationSeconds + ExplosionDurationSeconds;
        private static Sprite s_missile;
        private static Sprite[] s_explosionFrames;
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
        private Image _missile;
        private Image _explosion;
        private float _startedAt, _impactStartedAt, _endsAt;
        private bool _playing, _hasImpacted;

        internal bool IsPlaying => _playing;
        internal bool HasImpacted => _hasImpacted;
        internal float RemainingDurationSeconds => _playing ? Mathf.Max(0f, _endsAt - Time.unscaledTime) : 0f;

        internal float Play(TileTextView master, string masterRuntimeId, TileTextView attacker, string attackerRuntimeId)
        {
            Cancel();
            if (!isActiveAndEnabled || master == null || attacker == null ||
                string.IsNullOrWhiteSpace(masterRuntimeId) || string.IsNullOrWhiteSpace(attackerRuntimeId) ||
                masterRuntimeId == attackerRuntimeId || !LoadSprites()) return 0f;
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
                    : BodyBounds(body.sprite, view.VisualControllerName, _tiles[index]?.CurrentOccupantCardId);
            }
            var overlayCanvas = master.ActiveEffectButtonAnchor?.GetComponentInParent<Canvas>();
            _canvas = overlayCanvas != null ? overlayCanvas.rootCanvas : _sourceCanvases[0];
            EnsureOverlay(_canvas);
            _startedAt = Time.unscaledTime; _endsAt = _startedAt + DurationSeconds;
            _hasImpacted = false; _playing = true;
            if (!UpdateLayout(0f)) { Cancel(); return 0f; }
            _missile.sprite = s_missile; _missile.color = Color.white; _missile.gameObject.SetActive(true);
            _explosion.gameObject.SetActive(false); _root.gameObject.SetActive(true);
            var overlay = _root.GetComponent<Canvas>(); overlay.overrideSorting = true; overlay.sortingOrder = 4908;
            return DurationSeconds;
        }

        internal static bool TryBuildLayout(Rect masterBody, Rect attackerBody, float progress,
            out Vector2 missilePosition, out float rotationDegrees, out float missileCellSize, out float explosionCellSize)
        {
            missilePosition = Vector2.zero; rotationDegrees = 0f; missileCellSize = explosionCellSize = 0f;
            if (masterBody.width <= 0f || masterBody.height <= 0f || attackerBody.width <= 0f || attackerBody.height <= 0f) return false;
            var direction = attackerBody.center - masterBody.center;
            if (direction.sqrMagnitude <= .0001f) return false;
            missilePosition = Vector2.Lerp(masterBody.center, attackerBody.center, Mathf.Clamp01(progress));
            rotationDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            missileCellSize = Mathf.Clamp(Mathf.Max(masterBody.width, attackerBody.width) * 1.2f, 40f, 220f);
            // The peak of the reused 512-cell burst fills roughly 90% of its cell.
            // Its visible disk covers the attacker's actual body, not its empty cell.
            explosionCellSize = Mathf.Max(attackerBody.width, attackerBody.height) * 2.6f;
            return true;
        }

        private bool MatchesConfirmedView(int index)
        {
            var view = _views[index]; var tile = _tiles[index];
            return view != null && view.isActiveAndEnabled && view.VisualRuntimeId == _runtimeIds[index] &&
                   tile != null && tile.CurrentOccupantRuntimeId == _runtimeIds[index];
        }

        private static bool LoadSprites()
        {
            if (s_missile == null) s_missile = Resources.Load<Sprite>(MissileResourcePath);
            if (s_explosionFrames == null || s_explosionFrames.Length != ExplosionFrameCount)
            {
                s_explosionFrames = Resources.LoadAll<Sprite>(TimedBombPresentationVfx.ExplosionResourcePath);
                Array.Sort(s_explosionFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            return s_missile != null && s_explosionFrames.Length == ExplosionFrameCount;
        }

        private void EnsureOverlay(Canvas canvas)
        {
            if (_root == null)
            {
                var host = new GameObject("IronDomeMissileOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
                host.SetActive(false); _root = host.GetComponent<RectTransform>();
                var group = host.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
                _missile = CreateImage("IronDomeMissile"); _explosion = CreateImage("IronDomeExplosion");
            }
            _root.SetParent(canvas.transform, false); _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;
        }

        private Image CreateImage(string name)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(_root, false); image.raycastTarget = false; image.preserveAspect = true;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.one * .5f;
            image.rectTransform.pivot = Vector2.one * .5f; return image;
        }

        private bool UpdateLayout(float flightProgress)
        {
            if (_root == null || _canvas == null || !_canvas.isActiveAndEnabled || _root.parent == null ||
                !_root.parent.gameObject.activeInHierarchy || _root.rect.width <= 0f || _root.rect.height <= 0f) return false;
            for (var index = 0; index < 2; index++)
            {
                if (!MatchesConfirmedView(index) || _anchors[index] == null || !_anchors[index].gameObject.activeInHierarchy ||
                    _sourceCanvases[index] == null || !_sourceCanvases[index].isActiveAndEnabled) return false;
                var anchor = _anchors[index];
                var draw = _bodyImages[index] == null ? anchor.rect : _bodyImages[index].GetPixelAdjustedRect();
                if (_bodyImages[index] != null && _bodyImages[index].preserveAspect)
                {
                    var sprite = _bodyImages[index].sprite;
                    if (sprite == null) return false;
                    var size = sprite.rect.size; size *= Mathf.Min(draw.width / size.x, draw.height / size.y);
                    draw = new Rect(draw.center - size * .5f, size);
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
            if (!TryBuildLayout(_projectedBodies[0], _projectedBodies[1], flightProgress,
                out var position, out var rotation, out var missileSize, out var explosionSize)) return false;
            _missile.rectTransform.anchoredPosition = position;
            _missile.rectTransform.sizeDelta = Vector2.one * missileSize;
            _missile.rectTransform.localScale = Vector3.one;
            _missile.rectTransform.localRotation = Quaternion.Euler(0, 0, rotation);
            _explosion.rectTransform.anchoredPosition = _projectedBodies[1].center;
            _explosion.rectTransform.sizeDelta = Vector2.one * explosionSize;
            _explosion.rectTransform.localScale = Vector3.one;
            _explosion.rectTransform.localRotation = Quaternion.identity;
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
                if (!s_manifest.TryGetValue(key, out result) &&
                    !(cardId == "master" && s_manifest.TryGetValue(StatusSpriteBoundsAsset.CreateKey(
                        "unit:Assets/Unit/Master", sprite.texture.name, sprite.name, sprite.rect), out result)))
                {
                    // Master is a special card outside the normal catalog. Its baked
                    // alpha bounds retain the unit-directory key instead of a CardId.
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
            if (!UpdateLayout(Mathf.Clamp01(elapsed / FlightDurationSeconds))) { Cancel(); return; }
            if (elapsed < FlightDurationSeconds)
            {
                _missile.gameObject.SetActive(true); _explosion.gameObject.SetActive(false); return;
            }
            _missile.gameObject.SetActive(false); _explosion.gameObject.SetActive(true);
            if (!_hasImpacted)
            {
                // Hold the growing blast on a slow frame until Death can begin at its peak.
                var frame = Mathf.Clamp(Mathf.FloorToInt((elapsed - FlightDurationSeconds) * ExplosionFrameCount / ExplosionDurationSeconds), 0, 3);
                _explosion.sprite = s_explosionFrames[frame]; _explosion.color = Color.white; return;
            }
            if (Time.unscaledTime >= _endsAt) { Cancel(); return; }
            var age = Mathf.Max(0f, Time.unscaledTime - _impactStartedAt);
            _explosion.sprite = s_explosionFrames[Mathf.Min(ExplosionFrameCount - 1,
                4 + Mathf.FloorToInt(age * ExplosionFrameCount / ExplosionDurationSeconds))];
            _explosion.color = new Color(1, 1, 1, Mathf.Clamp01((_endsAt - Time.unscaledTime) / .2f));
        }

        internal bool TryBeginExplosion()
        {
            if (!_playing || !isActiveAndEnabled || !UpdateLayout(1f)) { Cancel(); return false; }
            if (_hasImpacted) return true;
            if (Time.unscaledTime - _startedAt < ImpactDelaySeconds) return false;
            _hasImpacted = true; _impactStartedAt = Time.unscaledTime;
            _endsAt = Mathf.Max(_endsAt, _impactStartedAt + TailDurationSeconds);
            _missile.gameObject.SetActive(false); _explosion.gameObject.SetActive(true);
            _explosion.sprite = s_explosionFrames[4]; _explosion.color = Color.white;
            return true;
        }

        internal void Cancel()
        {
            _playing = false; _hasImpacted = false; _endsAt = 0f; _canvas = null;
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
        private static void ResetCache() { s_missile = null; s_explosionFrames = null; s_manifest = null; s_bounds.Clear(); }
    }
}
