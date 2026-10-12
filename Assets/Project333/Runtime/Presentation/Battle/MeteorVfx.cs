using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    // Gateway and wire events own damage. This overlay owns only flight, impact and two quiet sounds.
    public sealed class MeteorVfx : MonoBehaviour
    {
        public const string FlightResourcePath = "Project333/SpellEffects/Meteor/Flight";
        public const string ImpactResourcePath = "Project333/SpellEffects/Meteor/Impact";
        public const string IncomingSoundResourcePath = "Project333/SpellEffects/Meteor/Audio/MeteorIncoming";
        public const string ImpactSoundResourcePath = "Project333/SpellEffects/Meteor/Audio/MeteorImpact";
        public const int ImpactFrameCount = 16;
        public const float ImpactFramesPerSecond = 16f;
        public const float ImpactDelaySeconds = .65f;
        public const float ImpactDurationSeconds = 1.15f;
        public const float DurationSeconds = ImpactDelaySeconds + ImpactDurationSeconds;
        public const float SoundGain = .30f;
        // Solid rock head in the preserved full cell (Unity bottom-left normalized coordinates).
        internal static readonly Vector2 FlightHeadNormalizedBottomLeft = new Vector2(.65f, .359375f);
        // First contact flash origin in the final packed full cell; center pivots remain intact.
        internal static readonly Vector2 ImpactOriginNormalizedBottomLeft = new Vector2(.5137399525f, .3744585921f);

        private readonly RectTransform[] _anchors = new RectTransform[BoardState.ColumnCount * BoardState.RowCount];
        private readonly Vector3[] _corners = new Vector3[4];
        private BoardPresenter _board;
        private RectTransform _root;
        private RectTransform _canvasRect;
        private Canvas _overlayCanvas;
        private Image _flightImage;
        private Image _impactImage;
        private Sprite _flight;
        private Sprite[] _impactFrames;
        private AudioClip _incomingSound;
        private AudioClip _impactSound;
        private AudioSource _audio;
        private Vector2 _fieldCenter;
        private Vector2 _flightStart;
        private float _startedAt;
        private float _impactStartedAt;
        private float _endsAt;
        private bool _playing;
        private bool _hasImpacted;

        internal float RemainingDurationSeconds => !_playing || !isActiveAndEnabled ? 0f : Mathf.Max(0f, _endsAt - Time.unscaledTime);

        internal float Play(BoardPresenter board, PlayerId casterOwnerId)
        {
            Cancel();
            if (!isActiveAndEnabled || board == null || !board.gameObject.activeInHierarchy ||
                (casterOwnerId != PlayerId.Player && casterOwnerId != PlayerId.AI)) return 0f;
            var enemy = casterOwnerId == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
            if (!ResolveAnchors(board, enemy, out var canvas) || !LoadResources()) return 0f;
            EnsureOverlay(canvas);
            if (!UpdateLayout()) { Cancel(); return 0f; }
            _board = board;
            PrepareAudio();
            _startedAt = Time.unscaledTime;
            _endsAt = _startedAt + DurationSeconds;
            _impactStartedAt = 0f;
            _hasImpacted = false;
            _playing = true;
            _flightImage.sprite = _flight;
            _flightImage.color = Color.white;
            PositionFlightHead(_flightStart);
            _flightImage.gameObject.SetActive(true);
            _impactImage.gameObject.SetActive(false);
            _root.gameObject.SetActive(true);
            // Unity rebuilds nested Canvas sorting on parenting and activation; assign it last.
            _overlayCanvas.overrideSorting = true;
            _overlayCanvas.sortingOrder = 4908;
            if (_audio != null && _incomingSound != null) _audio.PlayOneShot(_incomingSound, SoundGain);
            return DurationSeconds;
        }

        private bool LoadResources()
        {
            if (_flight == null)
            {
                var sprites = Resources.LoadAll<Sprite>(FlightResourcePath);
                if (sprites.Length != 1) return false;
                _flight = sprites[0];
            }
            if (_impactFrames == null || _impactFrames.Length == 0)
            {
                _impactFrames = Resources.LoadAll<Sprite>(ImpactResourcePath);
                Array.Sort(_impactFrames, (a, b) => FrameNumber(a.name).CompareTo(FrameNumber(b.name)));
            }
            if (_impactFrames.Length != ImpactFrameCount) return false;
            for (var index = 0; index < _impactFrames.Length; index++) if (FrameNumber(_impactFrames[index].name) != index) return false;
            return true;
        }

        private static int FrameNumber(string name)
        {
            var start = name.Length;
            while (start > 0 && char.IsDigit(name[start - 1])) start--;
            return int.TryParse(name.Substring(start), out var number) ? number : -1;
        }

        private void PrepareAudio()
        {
            if (_incomingSound == null) _incomingSound = Resources.Load<AudioClip>(IncomingSoundResourcePath);
            if (_impactSound == null) _impactSound = Resources.Load<AudioClip>(ImpactSoundResourcePath);
            if (_audio != null || (_incomingSound == null && _impactSound == null)) return;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _audio.loop = false;
            // Master volume is already applied once via After333AudioSettings' AudioListener.
        }

        private bool ResolveAnchors(BoardPresenter board, PlayerId enemy, out Canvas canvas)
        {
            canvas = null;
            var tiles = enemy == PlayerId.Player ? board.PlayerTileViews : board.AITileViews;
            if (tiles == null || tiles.Length != _anchors.Length) return false;
            for (var column = 0; column < BoardState.ColumnCount; column++)
            for (var row = 0; row < BoardState.RowCount; row++)
            {
                TileView selected = null;
                foreach (var tile in tiles)
                {
                    if (tile == null || tile.OwnerId != enemy || tile.Coord.Column != column || tile.Coord.Row != row) continue;
                    if (selected != null) return false;
                    selected = tile;
                }
                if (selected == null || selected.gameObject.scene != board.gameObject.scene) return false;
                var view = selected.GetComponent<TileTextView>();
                var anchor = view == null ? null : view.ActiveEffectButtonAnchor;
                if (anchor == null) anchor = selected.transform as RectTransform;
                // Use the tile background viewport; the occupant image can own a unit-sized sorting Canvas.
                var tileCanvas = anchor == null ? null : anchor.GetComponentInParent<Canvas>(true);
                if (tileCanvas == null && view != null) tileCanvas = view.GetVisualRootCanvas();
                if (anchor == null || tileCanvas == null || !anchor.gameObject.activeInHierarchy) return false;
                _anchors[column * BoardState.RowCount + row] = anchor;
                if (canvas == null) canvas = tileCanvas.rootCanvas;
            }
            return canvas != null && canvas.transform is RectTransform;
        }

        private void EnsureOverlay(Canvas canvas)
        {
            _canvasRect = (RectTransform)canvas.transform;
            if (_root == null)
            {
                var host = new GameObject("MeteorOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
                host.SetActive(false);
                _root = host.GetComponent<RectTransform>();
                _overlayCanvas = host.GetComponent<Canvas>();
                var group = host.GetComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                _flightImage = CreateImage("MeteorFlight");
                _impactImage = CreateImage("MeteorImpact");
            }
            _root.SetParent(_canvasRect, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;
        }

        private Image CreateImage(string name)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(_root, false);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.one * .5f;
            image.rectTransform.pivot = Vector2.one * .5f;
            image.raycastTarget = false;
            return image;
        }

        private bool UpdateLayout()
        {
            if (_root == null || _canvasRect == null || !_canvasRect.gameObject.activeInHierarchy ||
                _root.rect.width <= 0f || _root.rect.height <= 0f) return false;
            var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var anchor in _anchors)
            {
                if (anchor == null || !anchor.gameObject.activeInHierarchy || anchor.rect.width <= 0f || anchor.rect.height <= 0f) return false;
                anchor.GetWorldCorners(_corners);
                foreach (var corner in _corners)
                {
                    var point = (Vector2)_root.InverseTransformPoint(corner);
                    minimum = Vector2.Min(minimum, point);
                    maximum = Vector2.Max(maximum, point);
                }
            }
            _fieldCenter = (minimum + maximum) * .5f;
            var field = maximum - minimum;
            var bounds = _root.rect;
            var margin = Mathf.Min(bounds.width, bounds.height) * .02f;
            var impactSize = Mathf.Max(field.x, field.y) * 1.1f;
            var origin = ImpactOriginNormalizedBottomLeft;
            impactSize = Mathf.Min(impactSize, (_fieldCenter.x - bounds.xMin - margin) / origin.x);
            impactSize = Mathf.Min(impactSize, (bounds.xMax - margin - _fieldCenter.x) / (1f - origin.x));
            impactSize = Mathf.Min(impactSize, (_fieldCenter.y - bounds.yMin - margin) / origin.y);
            impactSize = Mathf.Min(impactSize, (bounds.yMax - margin - _fieldCenter.y) / (1f - origin.y));
            if (impactSize <= .01f || float.IsNaN(impactSize) || float.IsInfinity(impactSize)) return false;
            var impactCenter = _fieldCenter - (origin - Vector2.one * .5f) * impactSize;
            _impactImage.rectTransform.localPosition = new Vector3(impactCenter.x, impactCenter.y, 0f);
            _impactImage.rectTransform.sizeDelta = Vector2.one * impactSize;
            var flightSize = Mathf.Min(Mathf.Max(field.x, field.y) * .82f, Mathf.Min(bounds.width, bounds.height) * .85f);
            _flightImage.rectTransform.sizeDelta = Vector2.one * flightSize;
            var rise = bounds.height * .78f;
            _flightStart = _fieldCenter + new Vector2(-rise * .55f, rise);
            return true;
        }

        // Root calls this immediately before damage feedback; LateUpdate can call it first safely.
        internal bool TryBeginImpact()
        {
            if (!_playing || !isActiveAndEnabled || _board == null || !_board.gameObject.activeInHierarchy)
            { Cancel(); return false; }
            if (_hasImpacted) return true;
            if (Time.unscaledTime - _startedAt < ImpactDelaySeconds) return false;
            if (!UpdateLayout()) { Cancel(); return false; }
            _hasImpacted = true;
            _impactStartedAt = Time.unscaledTime;
            // A delayed rendered frame must retain a complete impact sound and fade.
            _endsAt = Mathf.Max(_endsAt, _impactStartedAt + ImpactDurationSeconds);
            PositionFlightHead(_fieldCenter);
            _flightImage.gameObject.SetActive(false);
            _impactImage.sprite = _impactFrames[0];
            _impactImage.color = Color.white;
            _impactImage.gameObject.SetActive(true);
            if (_audio != null && _impactSound != null) _audio.PlayOneShot(_impactSound, SoundGain);
            return true;
        }

        private void LateUpdate()
        {
            if (!_playing) return;
            if (_board == null || !_board.gameObject.activeInHierarchy || !UpdateLayout()) { Cancel(); return; }
            if (!_hasImpacted)
            {
                var elapsed = Mathf.Max(0f, Time.unscaledTime - _startedAt);
                if (elapsed >= ImpactDelaySeconds) { TryBeginImpact(); return; }
                var progress = Mathf.Clamp01(elapsed / ImpactDelaySeconds);
                var position = Vector2.LerpUnclamped(_flightStart, _fieldCenter, progress * progress);
                PositionFlightHead(position);
                return;
            }
            var impactElapsed = Mathf.Max(0f, Time.unscaledTime - _impactStartedAt);
            if (Time.unscaledTime >= _endsAt) { Cancel(); return; }
            _impactImage.sprite = _impactFrames[Mathf.Min(ImpactFrameCount - 1, Mathf.FloorToInt(impactElapsed * ImpactFramesPerSecond))];
            _impactImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01((ImpactDurationSeconds - impactElapsed) / .25f));
        }

        internal void Cancel()
        {
            _playing = false;
            _hasImpacted = false;
            _board = null;
            _endsAt = 0f;
            Array.Clear(_anchors, 0, _anchors.Length);
            if (_root != null) _root.gameObject.SetActive(false);
            if (_audio != null) _audio.Stop();
        }

        private void PositionFlightHead(Vector2 point)
        {
            var offset = Vector2.Scale(FlightHeadNormalizedBottomLeft - Vector2.one * .5f, _flightImage.rectTransform.sizeDelta);
            var center = point - offset;
            _flightImage.rectTransform.localPosition = new Vector3(center.x, center.y, 0f);
        }

        private void OnDisable() => Cancel();
        private void OnDestroy()
        {
            Cancel();
            if (_audio != null)
            {
                if (UnityEngine.Application.isPlaying) Destroy(_audio); else DestroyImmediate(_audio);
                _audio = null;
            }
            if (_root == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(_root.gameObject); else DestroyImmediate(_root.gameObject);
        }
    }
}
