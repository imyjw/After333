using System;
using Project333.Runtime.Domain.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    // One transient spell overlay. Battle services and server events own all damage.
    public sealed class NuclearExplosionVfx : MonoBehaviour
    {
        public const string FramesResourcePath = "Project333/SpellEffects/Nuclear/MushroomExplosion";
        public const string SoundResourcePath = "Project333/SpellEffects/Nuclear/Audio/NuclearExplosion";
        public const int FrameCount = 16;
        public const float FramesPerSecond = 9f;
        public const float DurationSeconds = FrameCount / FramesPerSecond;
        public const float SoundGain = 0.30f;
        // Ignition/shockwave origin, measured in a full 512px cell from the bottom left.
        internal static readonly Vector2 OriginPoint = new Vector2(0.5f, 32f / 512f);

        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform[] _anchors;
        private RectTransform _root;
        private RectTransform _canvasRect;
        private Image _image;
        private Sprite[] _frames;
        private AudioClip _sound;
        private AudioSource _audio;
        private float _startedAt;
        private float _elapsed;
        private bool _playing;

        internal float Play(BoardPresenter board, PlayerId casterOwnerId)
        {
            Cancel();
            if (!isActiveAndEnabled || board == null) return 0f;
            var tiles = casterOwnerId == PlayerId.Player ? board.AITileViews : board.PlayerTileViews;
            if (!ResolveAnchors(tiles, out var canvas)) return 0f;
            if (_frames == null || _frames.Length == 0)
            {
                _frames = Resources.LoadAll<Sprite>(FramesResourcePath);
                Array.Sort(_frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            if (_frames.Length != FrameCount) return 0f;
            EnsureOverlay(canvas);
            if (!UpdateLayout()) { Cancel(); return 0f; }
            _elapsed = 0f;
            _startedAt = Time.unscaledTime;
            _playing = true;
            _image.color = Color.white;
            _image.sprite = _frames[0];
            _root.gameObject.SetActive(true);
            // Unity can reset a nested Canvas override while parenting or activating it.
            var overlayCanvas = _root.GetComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 4906;
            if (_sound == null) _sound = Resources.Load<AudioClip>(SoundResourcePath);
            if (_sound != null)
            {
                if (_audio == null)
                {
                    _audio = gameObject.AddComponent<AudioSource>();
                    _audio.playOnAwake = false;
                    _audio.spatialBlend = 0f;
                    _audio.loop = false;
                }
                // Master volume is already applied once by After333AudioSettings via AudioListener.
                _audio.PlayOneShot(_sound, SoundGain);
            }
            return DurationSeconds;
        }

        private bool ResolveAnchors(TileView[] tiles, out Canvas canvas)
        {
            canvas = null;
            if (tiles == null || tiles.Length == 0) return false;
            _anchors = new RectTransform[tiles.Length];
            for (var i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                if (tile == null) continue;
                var view = tile.GetComponent<TileTextView>();
                var anchor = view != null ? view.ActiveEffectButtonAnchor : tile.transform as RectTransform;
                if (anchor == null) continue;
                _anchors[i] = anchor;
                if (canvas == null)
                {
                    // Whole-field effects use the tile background viewport, not the occupant sorting Canvas.
                    var tileCanvas = anchor.GetComponentInParent<Canvas>(true);
                    if (tileCanvas == null && view != null) tileCanvas = view.GetVisualRootCanvas();
                    canvas = tileCanvas == null ? null : tileCanvas.rootCanvas;
                }
            }
            return canvas != null && canvas.transform is RectTransform;
        }

        private void EnsureOverlay(Canvas canvas)
        {
            _canvasRect = (RectTransform)canvas.transform;
            if (_root == null)
            {
                var root = new GameObject("NuclearExplosionOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
                root.SetActive(false);
                _root = root.GetComponent<RectTransform>();
                var effectCanvas = root.GetComponent<Canvas>();
                effectCanvas.overrideSorting = true;
                effectCanvas.sortingOrder = 4906;
                var group = root.GetComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
                _image = new GameObject("NuclearMushroomExplosion", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                _image.rectTransform.SetParent(_root, false);
                _image.rectTransform.anchorMin = _image.rectTransform.anchorMax = Vector2.one * 0.5f;
                _image.rectTransform.pivot = OriginPoint;
                _image.raycastTarget = false;
            }
            _root.SetParent(_canvasRect, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;
        }

        private bool UpdateLayout()
        {
            if (_root == null || _canvasRect == null || !_canvasRect.gameObject.activeInHierarchy) return false;
            var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var found = false;
            foreach (var anchor in _anchors)
            {
                if (anchor == null || !anchor.gameObject.activeInHierarchy) continue;
                anchor.GetWorldCorners(_corners);
                for (var i = 0; i < _corners.Length; i++)
                {
                    var point = (Vector2)_root.InverseTransformPoint(_corners[i]);
                    minimum = Vector2.Min(minimum, point);
                    maximum = Vector2.Max(maximum, point);
                }
                found = true;
            }
            if (!found) return false;
            var origin = (minimum + maximum) * 0.5f;
            var bounds = _root.rect;
            var margin = Mathf.Min(bounds.width, bounds.height) * 0.02f;
            // Production fields have two rows horizontally and five columns vertically.
            // Start with their longer span; viewport fitting below keeps the rising cloud on screen.
            var fieldSize = maximum - minimum;
            var size = Mathf.Max(fieldSize.x, fieldSize.y) * 1.1f;
            size = Mathf.Min(size, (origin.x - bounds.xMin - margin) / OriginPoint.x);
            size = Mathf.Min(size, (bounds.xMax - margin - origin.x) / (1f - OriginPoint.x));
            size = Mathf.Min(size, (origin.y - bounds.yMin - margin) / OriginPoint.y);
            size = Mathf.Min(size, (bounds.yMax - margin - origin.y) / (1f - OriginPoint.y));
            if (size <= 0f) return false;
            _image.rectTransform.localPosition = new Vector3(origin.x, origin.y, 0f);
            _image.rectTransform.sizeDelta = Vector2.one * size;
            return true;
        }

        private void LateUpdate()
        {
            if (!_playing) return;
            _elapsed = Mathf.Max(0f, Time.unscaledTime - _startedAt);
            if (_elapsed >= DurationSeconds || !UpdateLayout()) { Cancel(); return; }
            _image.sprite = _frames[Mathf.Min(FrameCount - 1, Mathf.FloorToInt(_elapsed * FramesPerSecond))];
            _image.color = new Color(1f, 1f, 1f, Mathf.Clamp01((DurationSeconds - _elapsed) / 0.35f));
        }

        internal void Cancel()
        {
            _playing = false;
            _anchors = null;
            if (_root != null) _root.gameObject.SetActive(false);
            if (_audio != null) _audio.Stop();
        }

        private void OnDisable() => Cancel();
        private void OnDestroy()
        {
            Cancel();
            if (_root == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(_root.gameObject);
            else DestroyImmediate(_root.gameObject);
        }
    }
}
