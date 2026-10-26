using Project333.Runtime.Presentation.Settings;
using System;
using Project333.Runtime.Domain.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class TimedBombPresentationVfx : MonoBehaviour
    {
        public const string BodyResourcePath = "Project333/SpellEffects/TimedBomb/BombBody";
        public const string ExplosionResourcePath = "Project333/SpellEffects/TimedBomb/Explosion";
        public const string SoundResourcePath = "Project333/SpellEffects/TimedBomb/Audio/TimedBombExplosion";
        public const float CountdownDuration = 0.8f;
        public const float ExplosionDuration = 1f;
        public const float SoundGain = 0.30f;
        private const int ExplosionFrameCount = 16;
        private readonly Vector3[] _corners = new Vector3[4];
        private readonly Image[] _bursts = new Image[10];
        private readonly bool[] _burstValid = new bool[10];
        private RectTransform[] _anchors;
        private RectTransform _root;
        private Image _body;
        private BombTimerGraphic _digits;
        private CanvasGroup _bodyGroup;
        private AudioSource _audio;
        private Sprite _bodySprite;
        private Sprite[] _frames;
        private AudioClip _sound;
        private float _startedAt;
        private bool _playing;
        private bool _detonates;
        private bool _exploding;

        internal static float DurationFor(int before, int after) =>
            before > 0 && after == before - 1 ? CountdownDuration + (after == 0 ? ExplosionDuration : 0f) : 0f;

        internal float Play(BoardPresenter board, PlayerId caster, int before, int after)
        {
            Cancel();
            var duration = DurationFor(before, after);
            if (!isActiveAndEnabled || board == null || duration <= 0) return 0;
            var tiles = caster == PlayerId.Player ? board.AITileViews : board.PlayerTileViews;
            if (tiles == null || tiles.Length == 0) return 0;
            _anchors = new RectTransform[tiles.Length];
            Canvas canvas = null;
            for (var i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                if (tile == null) continue;
                var view = tile.GetComponent<TileTextView>();
                _anchors[i] = view?.ActiveEffectButtonAnchor ?? tile.transform as RectTransform;
                if (_anchors[i] == null || canvas != null) continue;
                // Occupant visuals may own a separate, unit-sized root Canvas. The tile
                // background belongs to the full viewport Canvas needed by this overlay.
                var sourceCanvas = _anchors[i].GetComponentInParent<Canvas>(true) ?? view?.GetVisualRootCanvas();
                if (sourceCanvas != null) canvas = sourceCanvas.rootCanvas;
            }
            if (canvas == null) return 0;
            if (_bodySprite == null) _bodySprite = Resources.Load<Sprite>(BodyResourcePath);
            if (_bodySprite == null) return 0;
            if (_frames == null || _frames.Length == 0)
            {
                _frames = Resources.LoadAll<Sprite>(ExplosionResourcePath);
                Array.Sort(_frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            EnsureOverlay(canvas);
            if (!UpdateLayout()) { Cancel(); return 0; }
            _body.sprite = _bodySprite;
            _digits.SetCountdown(before, after, 0);
            // Keep the LCD cover opaque; fading both PNG and cover would expose its printed digits.
            _bodyGroup.alpha = 1;
            _body.gameObject.SetActive(true);
            _root.gameObject.SetActive(true);
            // Parenting and activation rebuild nested Canvas sorting; assign it last.
            var overlay = _root.GetComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = 4910;
            _startedAt = Time.unscaledTime;
            _detonates = after == 0;
            _exploding = false;
            _playing = true;
            return duration;
        }

        private void EnsureOverlay(Canvas canvas)
        {
            if (_root == null)
            {
                var root = new GameObject("TimedBombOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
                root.SetActive(false);
                _root = (RectTransform)root.transform;
                var overlay = root.GetComponent<Canvas>(); overlay.overrideSorting = true; overlay.sortingOrder = 4910;
                var group = root.GetComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
                _body = CreateImage("TimedBombBody");
                _bodyGroup = _body.gameObject.AddComponent<CanvasGroup>(); _bodyGroup.interactable = false; _bodyGroup.blocksRaycasts = false;
                var timer = new GameObject("TimedBombTimer", typeof(RectTransform), typeof(BombTimerGraphic));
                timer.transform.SetParent(_body.transform, false);
                _digits = timer.GetComponent<BombTimerGraphic>(); _digits.raycastTarget = false;
                var timerRect = (RectTransform)timer.transform; timerRect.anchorMin = Vector2.zero; timerRect.anchorMax = Vector2.one;
                timerRect.offsetMin = timerRect.offsetMax = Vector2.zero;
                for (var i = 0; i < _bursts.Length; i++) _bursts[i] = CreateImage("TimedBombBurst" + i);
            }
            _root.SetParent(canvas.transform, false);
            _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;
        }

        private Image CreateImage(string name)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(_root, false);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.one * .5f;
            image.raycastTarget = false; image.gameObject.SetActive(false); return image;
        }

        private bool UpdateLayout()
        {
            if (_root == null || _root.parent == null || !_root.parent.gameObject.activeInHierarchy || _anchors == null) return false;
            var bounds = _root.rect;
            if (bounds.width <= 0 || bounds.height <= 0) return false;
            _body.rectTransform.localPosition = bounds.center;
            _body.rectTransform.sizeDelta = Vector2.one * Mathf.Min(bounds.width * .55f, bounds.height * .62f);
            var found = false;
            for (var i = 0; i < _bursts.Length; i++)
            {
                var anchor = i < _anchors.Length ? _anchors[i] : null;
                _burstValid[i] = false;
                if (anchor == null || !anchor.gameObject.activeInHierarchy) { _bursts[i].gameObject.SetActive(false); continue; }
                anchor.GetWorldCorners(_corners);
                var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                foreach (var corner in _corners)
                {
                    var p = (Vector2)_root.InverseTransformPoint(corner);
                    minimum = Vector2.Min(minimum, p); maximum = Vector2.Max(maximum, p);
                }
                var center = (minimum + maximum) * .5f;
                var span = maximum - minimum;
                var size = Mathf.Max(span.x, span.y) * 1.5f;
                size = Mathf.Min(size, 2 * Mathf.Min(center.x - bounds.xMin, bounds.xMax - center.x));
                size = Mathf.Min(size, 2 * Mathf.Min(center.y - bounds.yMin, bounds.yMax - center.y));
                if (size <= 0) { _bursts[i].gameObject.SetActive(false); continue; }
                _bursts[i].rectTransform.localPosition = center;
                _bursts[i].rectTransform.sizeDelta = Vector2.one * size;
                _burstValid[i] = true;
                if (_exploding) _bursts[i].gameObject.SetActive(true);
                found = true;
            }
            return found;
        }

        private void LateUpdate()
        {
            if (!_playing) return;
            var elapsed = Mathf.Max(0, Time.unscaledTime - _startedAt);
            if (!UpdateLayout()) { Cancel(); return; }
            if (elapsed < CountdownDuration)
            {
                _digits.SetBlend(Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - .18f) / .25f)));
                return;
            }
            if (!_detonates) { Cancel(); return; }
            var explosionAge = elapsed - CountdownDuration;
            if (explosionAge >= ExplosionDuration) { Cancel(); return; }
            if (!_exploding)
            {
                _exploding = true; _body.gameObject.SetActive(false);
                if (_frames.Length != ExplosionFrameCount) { Cancel(); return; }
                if (_sound == null) _sound = Resources.Load<AudioClip>(SoundResourcePath);
                if (_sound != null)
                {
                    if (_audio == null) { _audio = gameObject.AddComponent<AudioSource>(); _audio.playOnAwake = false; _audio.spatialBlend = 0; _audio.loop = false; }
                    // Master volume is already applied once by After333AudioSettings via AudioListener.
                    After333SoundEffects.PlayOneShot(_audio, _sound, SoundGain);
                }
            }
            var sprite = _frames[Mathf.Min(ExplosionFrameCount - 1, Mathf.FloorToInt(explosionAge * ExplosionFrameCount))];
            var color = new Color(1, 1, 1, Mathf.Clamp01((ExplosionDuration - explosionAge) / .2f));
            for (var i = 0; i < _bursts.Length; i++)
                if (_burstValid[i])
                {
                    _bursts[i].sprite = sprite; _bursts[i].color = color; _bursts[i].gameObject.SetActive(true);
                }
        }

        internal void Cancel()
        {
            _playing = false; _exploding = false; _anchors = null;
            if (_root != null) _root.gameObject.SetActive(false);
            if (_body != null) _body.gameObject.SetActive(false);
            foreach (var burst in _bursts) if (burst != null) burst.gameObject.SetActive(false);
            if (_audio != null) _audio.Stop();
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
