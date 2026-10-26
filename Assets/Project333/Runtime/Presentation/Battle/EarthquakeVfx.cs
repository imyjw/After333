using Project333.Runtime.Presentation.Settings;
using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    // Damage is already resolved by the gateway. This component owns only transient UI and sound.
    [DefaultExecutionOrder(10000)]
    public sealed class EarthquakeVfx : MonoBehaviour
    {
        public const string FramesResourcePath = "Project333/SpellEffects/Earthquake/Cracks";
        public const string SoundResourcePath = "Project333/SpellEffects/Earthquake/Audio/EarthquakeRumble";
        public const int FrameCount = 16;
        public const float FramesPerSecond = 16f;
        public const float DurationSeconds = 1.15f;
        public const float ShakeSeconds = .85f;
        public const float SoundGain = .30f;

        private sealed class ShakeRoot
        {
            internal Canvas Canvas;
            internal RectTransform Rect;
            internal Vector3 AppliedOffset;
        }

        private readonly RectTransform[] _anchors = new RectTransform[BoardState.ColumnCount * BoardState.RowCount];
        private readonly Canvas[] _canvases = new Canvas[BoardState.ColumnCount * BoardState.RowCount];
        private readonly Image[] _cracks = new Image[BoardState.ColumnCount * BoardState.RowCount];
        private readonly Vector3[] _corners = new Vector3[4];
        private readonly List<ShakeRoot> _shakeRoots = new List<ShakeRoot>();
        private Sprite[] _frames;
        private AudioClip _sound;
        private AudioSource _audio;
        private EarthquakeShakeReset _reset;
        private BoardPresenter _board;
        private float _startedAt;
        private bool _playing;

        internal float Play(BoardPresenter board, PlayerId casterOwnerId)
        {
            Cancel();
            if (!isActiveAndEnabled || board == null || !board.gameObject.activeInHierarchy ||
                (casterOwnerId != PlayerId.Player && casterOwnerId != PlayerId.AI)) return 0f;
            var enemy = casterOwnerId == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
            var tiles = enemy == PlayerId.Player ? board.PlayerTileViews : board.AITileViews;
            if (!ResolveAnchors(board, tiles, enemy)) return 0f;
            if (_frames == null || _frames.Length == 0)
            {
                _frames = Resources.LoadAll<Sprite>(FramesResourcePath);
                Array.Sort(_frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            if (_frames.Length != FrameCount) return 0f;
            EnsureCracks();
            if (!UpdateLayout()) { Cancel(); return 0f; }
            _board = board;
            CollectShakeRoots(board);
            if (_reset == null) _reset = gameObject.AddComponent<EarthquakeShakeReset>();
            _reset.Owner = this;
            _playing = true;
            foreach (var crack in _cracks)
            {
                crack.sprite = _frames[0];
                crack.color = Color.white;
                crack.gameObject.SetActive(true);
                // Enabling a nested Canvas also rebuilds inherited sorting; apply the override last.
                var crackCanvas = crack.GetComponent<Canvas>();
                crackCanvas.overrideSorting = true;
                crackCanvas.sortingOrder = 200;
            }
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
                // Include resource loading and source creation before starting the presentation clock.
                _startedAt = Time.unscaledTime;
                // After333AudioSettings applies master volume once through AudioListener.
                After333SoundEffects.PlayOneShot(_audio, _sound, SoundGain);
            }
            else _startedAt = Time.unscaledTime;
            return DurationSeconds;
        }

        private bool ResolveAnchors(BoardPresenter board, TileView[] tiles, PlayerId enemy)
        {
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
                // Ground cracks follow the tile background viewport, not a unit-sized visual Canvas.
                var canvas = anchor == null ? null : anchor.GetComponentInParent<Canvas>(true);
                if (canvas == null && view != null) canvas = view.GetVisualRootCanvas();
                if (anchor == null || canvas == null || !anchor.gameObject.activeInHierarchy) return false;
                var index = column * BoardState.RowCount + row;
                _anchors[index] = anchor;
                _canvases[index] = canvas.rootCanvas;
                if (!(_canvases[index].transform is RectTransform)) return false;
            }
            return true;
        }

        private void EnsureCracks()
        {
            for (var index = 0; index < _cracks.Length; index++)
            {
                if (_cracks[index] == null)
                {
                    var host = new GameObject("EarthquakeCrack_" + index.ToString("D2"), typeof(RectTransform), typeof(Canvas), typeof(Image));
                    host.SetActive(false);
                    _cracks[index] = host.GetComponent<Image>();
                    _cracks[index].raycastTarget = false;
                }
                var rect = _cracks[index].rectTransform;
                rect.SetParent(_canvases[index].transform, false);
                rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
                rect.pivot = Vector2.one * .5f;
            }
        }

        private bool UpdateLayout()
        {
            for (var index = 0; index < _anchors.Length; index++)
            {
                var anchor = _anchors[index];
                var canvas = _canvases[index];
                if (anchor == null || canvas == null || !anchor.gameObject.activeInHierarchy || !canvas.isActiveAndEnabled) return false;
                var canvasRect = (RectTransform)canvas.transform;
                if (canvasRect.rect.width <= 0f || canvasRect.rect.height <= 0f || anchor.rect.width <= 0f || anchor.rect.height <= 0f) return false;
                anchor.GetWorldCorners(_corners);
                var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                foreach (var corner in _corners)
                {
                    var point = (Vector2)canvasRect.InverseTransformPoint(corner);
                    minimum = Vector2.Min(minimum, point);
                    maximum = Vector2.Max(maximum, point);
                }
                var center = (minimum + maximum) * .5f;
                var size = .9f * Mathf.Min(maximum.x - minimum.x, maximum.y - minimum.y);
                var bounds = canvasRect.rect;
                size = Mathf.Min(size, 2f * Mathf.Min(center.x - bounds.xMin, bounds.xMax - center.x));
                size = Mathf.Min(size, 2f * Mathf.Min(center.y - bounds.yMin, bounds.yMax - center.y));
                if (size <= .01f || float.IsNaN(size) || float.IsInfinity(size)) return false;
                _cracks[index].rectTransform.localPosition = new Vector3(center.x, center.y, 0f);
                _cracks[index].rectTransform.localRotation = Quaternion.identity;
                _cracks[index].rectTransform.sizeDelta = Vector2.one * size;
            }
            return true;
        }

        private void CollectShakeRoots(BoardPresenter board)
        {
            _shakeRoots.Clear();
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (canvas.gameObject.scene != board.gameObject.scene || canvas.rootCanvas != canvas || !canvas.isActiveAndEnabled) continue;
                for (var index = 0; index < canvas.transform.childCount; index++)
                {
                    if (canvas.transform.GetChild(index) is RectTransform rect)
                        _shakeRoots.Add(new ShakeRoot { Canvas = canvas, Rect = rect });
                }
            }
        }

        // Undo only our own additive contribution, preserving movement by other presenters.
        // The early helper runs before input/layout; LateUpdate repeats this safely for manual sampling.
        internal void RestoreShakeOffsets()
        {
            foreach (var root in _shakeRoots)
            {
                if (root.Rect != null) root.Rect.localPosition -= root.AppliedOffset;
                root.AppliedOffset = Vector3.zero;
            }
        }

        private void ApplyShake(float elapsed)
        {
            if (elapsed >= ShakeSeconds) return;
            var decay = Mathf.Pow(1f - elapsed / ShakeSeconds, 2f);
            var oscillation = new Vector2(Mathf.Sin(elapsed * 79f), Mathf.Sin(elapsed * 57f));
            if (oscillation.sqrMagnitude > 1f) oscillation.Normalize();
            foreach (var root in _shakeRoots)
            {
                if (root.Rect == null || root.Canvas == null || !root.Rect.gameObject.activeInHierarchy || !root.Canvas.isActiveAndEnabled) continue;
                var canvasRect = (RectTransform)root.Canvas.transform;
                var camera = root.Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.Canvas.worldCamera;
                if (root.Canvas.renderMode != RenderMode.ScreenSpaceOverlay && camera == null) continue;
                var shortSide = camera == null ? Mathf.Min(Screen.width, Screen.height) : Mathf.Min(camera.pixelWidth, camera.pixelHeight);
                var pixels = oscillation * Mathf.Clamp(shortSide * .004f, 4f, 6f) * decay;
                var screenCenter = RectTransformUtility.WorldToScreenPoint(camera, canvasRect.TransformPoint(canvasRect.rect.center));
                if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenCenter, camera, out var normal) ||
                    !RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenCenter + pixels, camera, out var shaken)) continue;
                root.AppliedOffset = canvasRect.InverseTransformVector(shaken - normal);
                root.Rect.localPosition += root.AppliedOffset;
            }
        }

        private void LateUpdate()
        {
            RestoreShakeOffsets();
            if (!_playing) return;
            var elapsed = Mathf.Max(0f, Time.unscaledTime - _startedAt);
            if (_board == null || !_board.gameObject.activeInHierarchy || elapsed >= DurationSeconds || !UpdateLayout()) { Cancel(); return; }
            var sprite = _frames[Mathf.Min(FrameCount - 1, Mathf.FloorToInt(elapsed * FramesPerSecond))];
            var color = new Color(1f, 1f, 1f, Mathf.Clamp01((DurationSeconds - elapsed) / .2f));
            foreach (var crack in _cracks) { crack.sprite = sprite; crack.color = color; }
            ApplyShake(elapsed);
        }

        internal void Cancel()
        {
            RestoreShakeOffsets();
            _shakeRoots.Clear();
            _playing = false;
            _board = null;
            foreach (var crack in _cracks) if (crack != null) crack.gameObject.SetActive(false);
            Array.Clear(_anchors, 0, _anchors.Length);
            Array.Clear(_canvases, 0, _canvases.Length);
            if (_audio != null) _audio.Stop();
        }

        private void OnDisable() => Cancel();
        private void OnDestroy()
        {
            Cancel();
            if (_audio != null)
            {
                if (UnityEngine.Application.isPlaying) Destroy(_audio);
                else DestroyImmediate(_audio);
                _audio = null;
            }
            if (_reset != null)
            {
                _reset.Owner = null;
                if (UnityEngine.Application.isPlaying) Destroy(_reset);
                else DestroyImmediate(_reset);
            }
            foreach (var crack in _cracks)
            {
                if (crack == null) continue;
                if (UnityEngine.Application.isPlaying) Destroy(crack.gameObject);
                else DestroyImmediate(crack.gameObject);
            }
        }
    }

    // Runtime-only companion; no scene or prefab references are serialized.
    [DefaultExecutionOrder(-10000)]
    internal sealed class EarthquakeShakeReset : MonoBehaviour
    {
        internal EarthquakeVfx Owner;
        private void Update() { if (Owner != null) Owner.RestoreShakeOffsets(); }
    }
}
