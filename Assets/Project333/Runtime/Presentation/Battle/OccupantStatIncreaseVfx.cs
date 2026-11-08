using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    // Non-blocking feedback for confirmed combat stat increases on any occupant.
    // Conditional attack from HP, resources or pack size is deliberately not a combat buff.
    [Flags]
    public enum OccupantStatIncreaseKind { None = 0, Attack = 1, MaxHealth = 2, Both = Attack | MaxHealth }

    public sealed class OccupantStatIncreaseVfx : MonoBehaviour
    {
        public const float DurationSeconds = 0.62f;
        public const float RipplePhaseEndNormalized = 0.66f;
        public const float RisingQiStartNormalized = 0.14f;
        public const float MaximumRippleAlpha = 0.50f;
        public const float MaximumRisingQiAlpha = 0.44f;
        public const int OverlaySortingOrder = 260; // Above occupant visual (250), strictly below OccupantStatBar (4950).

        private static Dictionary<string, Rect> s_manifest;
        private static readonly Dictionary<Sprite, Dictionary<string, Rect>> s_bounds =
            new Dictionary<Sprite, Dictionary<string, Rect>>();

        private readonly Vector3[] _corners = new Vector3[4];
        private readonly List<Track> _tracks = new List<Track>();
        private Dictionary<(PlayerId, string), StatSample> _previous = new Dictionary<(PlayerId, string), StatSample>();
        private Dictionary<(PlayerId, string), StatSample> _current = new Dictionary<(PlayerId, string), StatSample>();
        private readonly List<IncreaseCue> _cues = new List<IncreaseCue>();
        private bool _hasBaseline;

        private struct StatSample
        {
            public string CardId;
            public int Attack, MaxHp;
        }

        private struct IncreaseCue
        {
            public PlayerId Owner;
            public string RuntimeId;
            public OccupantStatIncreaseKind Kind;
        }

        private sealed class Track
        {
            public PlayerId OwnerId;
            public OccupantStatIncreaseKind Kind;
            public TileTextView View;
            public TileView Tile;
            public BoardPresenter Board;
            public Image BodyImage;
            public RectTransform Root;
            public Canvas OverlayCanvas;
            public StatIncreaseQiGraphic Graphic;
            public string RuntimeId;
            public float StartedAt;
            public float ElapsedSeconds;
            public bool Playing;
            public Rect BodyRect;

        }

        public bool IsPlaying
        {
            get { foreach (var track in _tracks) if (track.Playing) return true; return false; }
        }

        private Track GetTrack(PlayerId owner, string runtimeId = null)
        {
            foreach (var track in _tracks)
                if (track.Playing && track.OwnerId == owner && (runtimeId == null || track.RuntimeId == runtimeId)) return track;
            return null;
        }

        public bool IsPlayingFor(PlayerId owner, string runtimeId = null) => GetTrack(owner, runtimeId) != null;
        public string GetTargetRuntimeId(PlayerId owner) => GetTrack(owner)?.RuntimeId;
        public float GetElapsedSeconds(PlayerId owner) => GetTrack(owner)?.ElapsedSeconds ?? 0f;
        public float GetRippleAlpha(PlayerId owner) => GetTrack(owner)?.Graphic?.RippleAlpha ?? 0f;
        public float GetRisingQiAlpha(PlayerId owner) => GetTrack(owner)?.Graphic?.RisingQiAlpha ?? 0f;
        public float GetRippleHorizontalRadius(PlayerId owner) => GetTrack(owner)?.Graphic?.PrimaryRippleRadiusX ?? 0f;
        public float GetRisingQiCenterY(PlayerId owner) => GetTrack(owner)?.Graphic?.RisingQiCenterY ?? 0f;
        public Rect GetBodyRect(PlayerId owner) => GetTrack(owner)?.BodyRect ?? default;
        public Canvas GetOverlayCanvas(PlayerId owner, string runtimeId = null) => GetTrack(owner, runtimeId)?.OverlayCanvas;
        public StatIncreaseQiGraphic GetGraphic(PlayerId owner, string runtimeId = null) => GetTrack(owner, runtimeId)?.Graphic;
        public OccupantStatIncreaseKind GetIncreaseKind(PlayerId owner, string runtimeId) => GetTrack(owner, runtimeId)?.Kind ?? OccupantStatIncreaseKind.None;

        // Called AFTER the board has applied a confirmed local/projected state. Keep value
        // copies, since the local gateway mutates occupant instances in place.
        public void Observe(BattleState state, BoardPresenter board)
        {
            if (state == null || board == null) { Cancel(); return; }
            _current.Clear();
            _cues.Clear();
            ObserveBoard(state.PlayerBoard);
            ObserveBoard(state.AIBoard);
            var swap = _previous; _previous = _current; _current = swap;
            _hasBaseline = true;
            foreach (var cue in _cues)
            {
                var tiles = cue.Owner == PlayerId.Player ? board.PlayerTileViews : board.AITileViews;
                if (tiles == null) continue;
                foreach (var tile in tiles)
                {
                    if (tile == null || tile.CurrentOccupantRuntimeId != cue.RuntimeId) continue;
                    Play(tile.GetComponent<TileTextView>(), cue.RuntimeId, cue.Kind, board);
                    break;
                }
            }
        }

        private void ObserveBoard(Project333.Runtime.Domain.Board.BoardState board)
        {
            foreach (var occupant in board.EnumerateOccupants())
            {
                if (occupant == null || !occupant.IsAlive || string.IsNullOrEmpty(occupant.RuntimeId)) continue;
                var key = (occupant.OwnerId, occupant.RuntimeId);
                var value = new StatSample { CardId = occupant.CardId, Attack = occupant.BaseAttack, MaxHp = occupant.MaxHp };
                _current[key] = value;
                // A new identity/definition is a spawn/transform, not an increase to an
                // existing occupant. CurrentHp intentionally does not enter this comparison.
                if (!_hasBaseline || !_previous.TryGetValue(key, out var before) || before.CardId != value.CardId) continue;
                var kind = OccupantStatIncreaseKind.None;
                if (value.Attack > before.Attack) kind |= OccupantStatIncreaseKind.Attack;
                if (value.MaxHp > before.MaxHp) kind |= OccupantStatIncreaseKind.MaxHealth;
                if (kind != OccupantStatIncreaseKind.None)
                    _cues.Add(new IncreaseCue { Owner = occupant.OwnerId, RuntimeId = occupant.RuntimeId, Kind = kind });
            }
        }

        public float Play(TileTextView view, string runtimeId, BoardPresenter board = null) =>
            Play(view, runtimeId, OccupantStatIncreaseKind.Attack, board);

        public float Play(TileTextView view, string runtimeId, OccupantStatIncreaseKind kind, BoardPresenter board = null)
        {
            if (!isActiveAndEnabled || view == null || !view.isActiveAndEnabled || string.IsNullOrEmpty(runtimeId) || kind == OccupantStatIncreaseKind.None) return 0f;
            if (string.IsNullOrEmpty(view.VisualRuntimeId)) view.Refresh();
            var tile = view.GetComponent<TileView>();
            if (tile == null) return 0f;
            var track = GetTrack(tile.OwnerId, runtimeId);
            if (track == null)
            {
                foreach (var reusable in _tracks) if (!reusable.Playing) { track = reusable; break; }
                if (track == null) { track = new Track(); _tracks.Add(track); }
            }
            CancelTrack(track);
            track.OwnerId = tile.OwnerId;
            track.Kind = kind;
            track.View = view; track.Tile = tile; track.Board = board;
            track.RuntimeId = runtimeId; track.BodyImage = view.OccupantVisualImage;
            if (!HasCurrentVisibleTarget(track)) { CancelTrack(track); return 0f; }
            var canvas = view.GetVisualRootCanvas();
            if (canvas == null || !canvas.isActiveAndEnabled) { CancelTrack(track); return 0f; }
            EnsureOverlay(track, canvas.rootCanvas != null ? canvas.rootCanvas : canvas);
            if (!UpdateTrackLayout(track)) { CancelTrack(track); return 0f; }
            track.StartedAt = Time.unscaledTime; track.ElapsedSeconds = 0f; track.Playing = true;
            track.Root.gameObject.SetActive(true);
            track.OverlayCanvas.overrideSorting = true;
            track.OverlayCanvas.sortingLayerID = canvas.sortingLayerID;
            track.OverlayCanvas.sortingOrder = OverlaySortingOrder;
            track.Graphic.Present(track.BodyRect, 0f, kind);
            return DurationSeconds;
        }

        public void Sample(PlayerId owner, float elapsedSeconds)
        {
            foreach (var track in _tracks)
            {
                if (!track.Playing || track.OwnerId != owner) continue;
                track.StartedAt = Time.unscaledTime - Mathf.Max(0f, elapsedSeconds);
                UpdateTrack(track, Time.unscaledTime);
            }
            Canvas.ForceUpdateCanvases();
        }

        public void Cancel()
        {
            foreach (var track in _tracks) CancelTrack(track);
            _previous.Clear(); _current.Clear(); _cues.Clear(); _hasBaseline = false;
        }

        public void Cancel(PlayerId owner)
        {
            foreach (var track in _tracks) if (track.OwnerId == owner) CancelTrack(track);
        }

        private bool HasCurrentVisibleTarget(Track track)
        {
            if (track.Tile == null || track.Tile.CurrentOccupantRuntimeId != track.RuntimeId)
            {
                TryRebindMovedOccupantTile(track);
            }

            var occupant = track.Tile == null ? null : track.Tile.CurrentOccupant;
            if (track.View != null && string.IsNullOrEmpty(track.View.VisualRuntimeId) &&
                track.Tile != null && track.Tile.CurrentOccupantRuntimeId == track.RuntimeId)
            {
                track.View.Refresh();
            }

            track.BodyImage = track.View == null ? null : track.View.OccupantVisualImage;
            return track.View != null &&
                   track.View.isActiveAndEnabled &&
                   !track.View.IsDeathPresentation &&
                   track.Tile != null &&
                   track.Tile.CurrentOccupantRuntimeId == track.RuntimeId &&
                   track.View.VisualRuntimeId == track.RuntimeId &&
                   occupant != null &&
                   occupant.IsAlive &&
                   !occupant.IsSealbound &&
                   track.BodyImage != null &&
                   track.BodyImage.isActiveAndEnabled &&
                   track.BodyImage.sprite != null;
        }

        private static void TryRebindMovedOccupantTile(Track track)
        {
            if (track.Board == null || string.IsNullOrEmpty(track.RuntimeId))
            {
                return;
            }

            var tiles = track.OwnerId == PlayerId.Player
                ? track.Board.PlayerTileViews
                : track.Board.AITileViews;
            if (tiles == null)
            {
                return;
            }

            foreach (var candidate in tiles)
            {
                if (candidate == null || candidate.CurrentOccupantRuntimeId != track.RuntimeId)
                {
                    continue;
                }

                var occupant = candidate.CurrentOccupant;
                var candidateView = candidate.GetComponent<TileTextView>();
                if (occupant != null && occupant.IsAlive && candidateView != null)
                {
                    track.Tile = candidate;
                    track.View = candidateView;
                    if (candidateView.VisualRuntimeId != track.RuntimeId ||
                        candidateView.OccupantVisualImage == null ||
                        candidateView.OccupantVisualImage.sprite == null)
                    {
                        candidateView.SetTile(candidate.Title, candidate.Content, true);
                        candidateView.PlayIdleAnimation();
                    }

                    track.BodyImage = candidateView.OccupantVisualImage;
                    return;
                }
            }
        }

        private static void EnsureOverlay(Track track, Canvas rootCanvas)
        {
            if (track.Root == null)
            {
                var hostName = "StatIncreaseOverlay_" + track.RuntimeId;
                var host = new GameObject(hostName, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
                host.SetActive(false);
                track.Root = host.GetComponent<RectTransform>();
                track.OverlayCanvas = host.GetComponent<Canvas>();
                var group = host.GetComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;

                var graphicNode = new GameObject("StatIncreaseQiAura", typeof(RectTransform), typeof(CanvasRenderer));
                var graphicRect = graphicNode.GetComponent<RectTransform>();
                graphicRect.SetParent(track.Root, false);
                graphicRect.anchorMin = Vector2.zero;
                graphicRect.anchorMax = Vector2.one;
                graphicRect.offsetMin = Vector2.zero;
                graphicRect.offsetMax = Vector2.zero;
                track.Graphic = graphicNode.AddComponent<StatIncreaseQiGraphic>();
                track.Graphic.raycastTarget = false;
            }

            track.Root.SetParent(rootCanvas.transform, false);
            track.Root.anchorMin = Vector2.zero;
            track.Root.anchorMax = Vector2.one;
            track.Root.offsetMin = Vector2.zero;
            track.Root.offsetMax = Vector2.zero;
            track.Root.localScale = Vector3.one;
            track.Root.localRotation = Quaternion.identity;
            track.OverlayCanvas.overrideSorting = true;
            track.OverlayCanvas.sortingLayerID = rootCanvas.sortingLayerID;
            track.OverlayCanvas.sortingOrder = OverlaySortingOrder;
        }

        private bool UpdateTrackLayout(Track track)
        {
            if (!HasCurrentVisibleTarget(track) ||
                track.Root == null ||
                track.Root.parent == null ||
                !track.Root.parent.gameObject.activeInHierarchy ||
                track.Root.rect.width <= 0f ||
                track.Root.rect.height <= 0f)
            {
                return false;
            }

            track.BodyImage = track.View.OccupantVisualImage;
            var opaque = ResolveBodyBounds(
                track.BodyImage.sprite,
                track.View.VisualControllerName,
                track.Tile.CurrentOccupantCardId);
            if (opaque.width <= 0f || opaque.height <= 0f)
            {
                return false;
            }

            var draw = track.BodyImage.GetPixelAdjustedRect();
            if (track.BodyImage.preserveAspect)
            {
                var spriteSize = track.BodyImage.sprite.rect.size;
                if (spriteSize.x > 0f && spriteSize.y > 0f)
                {
                    var scale = Mathf.Min(draw.width / spriteSize.x, draw.height / spriteSize.y);
                    var size = spriteSize * scale;
                    draw = new Rect(draw.center - size * 0.5f, size);
                }
            }

            var localMin = draw.min + Vector2.Scale(opaque.min, draw.size);
            var localMax = draw.min + Vector2.Scale(opaque.max, draw.size);
            _corners[0] = track.BodyImage.rectTransform.TransformPoint(new Vector3(localMin.x, localMin.y));
            _corners[1] = track.BodyImage.rectTransform.TransformPoint(new Vector3(localMin.x, localMax.y));
            _corners[2] = track.BodyImage.rectTransform.TransformPoint(new Vector3(localMax.x, localMax.y));
            _corners[3] = track.BodyImage.rectTransform.TransformPoint(new Vector3(localMax.x, localMin.y));

            var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in _corners)
            {
                var point = (Vector2)track.Root.InverseTransformPoint(corner);
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }

            track.BodyRect = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
            return track.BodyRect.width > 1f && track.BodyRect.height > 1f;
        }

        private static Rect ResolveBodyBounds(Sprite sprite, string controllerName, string cardId)
        {
            var key = string.IsNullOrEmpty(controllerName) ? cardId ?? string.Empty : controllerName;
            if (!s_bounds.TryGetValue(sprite, out var byKey))
            {
                byKey = new Dictionary<string, Rect>(StringComparer.Ordinal);
                s_bounds.Add(sprite, byKey);
            }

            if (byKey.TryGetValue(key, out var cached))
            {
                return cached;
            }

            if (s_manifest == null)
            {
                s_manifest = new Dictionary<string, Rect>(StringComparer.Ordinal);
                var asset = Resources.Load<StatusSpriteBoundsAsset>(ActionDebuffStatusVfx.BoundsResourcePath);
                if (asset != null && asset.Entries != null)
                {
                    foreach (var entry in asset.Entries)
                    {
                        if (entry == null || string.IsNullOrEmpty(entry.VisualKey)) continue;
                        s_manifest[StatusSpriteBoundsAsset.CreateKey(
                            entry.VisualKey,
                            entry.TextureName,
                            entry.SpriteName,
                            entry.SpriteRect)] = entry.NormalizedBounds;
                    }
                }
            }

            var lookup = StatusSpriteBoundsAsset.CreateKey(key, sprite.texture.name, sprite.name, sprite.rect);
            if (!s_manifest.TryGetValue(lookup, out var result))
            {
                lookup = StatusSpriteBoundsAsset.CreateKey(cardId ?? string.Empty, sprite.texture.name, sprite.name, sprite.rect);
                if (!s_manifest.TryGetValue(lookup, out result))
                {
                    lookup = StatusSpriteBoundsAsset.CreateKey("unit:Assets/Unit/" + key, sprite.texture.name, sprite.name, sprite.rect);
                    if (!s_manifest.TryGetValue(lookup, out result))
                    {
                        lookup = StatusSpriteBoundsAsset.CreateKey("unit:Assets/Unit/Master", sprite.texture.name, sprite.name, sprite.rect);
                        if (!s_manifest.TryGetValue(lookup, out result))
                        {
                            result = ReadSpriteOpaqueBounds(sprite);
                        }
                    }
                }
            }

            if (result.width <= 0f || result.height <= 0f || (result.width >= 0.98f && result.height >= 0.98f))
            {
                result = new Rect(0.390625f, 0.0625f, 0.21875f, 0.296875f);
            }

            byKey.Add(key, result);
            return result;
        }

        private static Rect ReadSpriteOpaqueBounds(Sprite sprite, byte minimumAlpha = 16)
        {
            if (!sprite.texture.isReadable || sprite.packed)
            {
                var bounds = sprite.bounds;
                var rect = sprite.rect;
                var min = (Vector2)bounds.min * sprite.pixelsPerUnit + sprite.pivot;
                var max = (Vector2)bounds.max * sprite.pixelsPerUnit + sprite.pivot;
                return Rect.MinMaxRect(
                    Mathf.Clamp01(min.x / rect.width),
                    Mathf.Clamp01(min.y / rect.height),
                    Mathf.Clamp01(max.x / rect.width),
                    Mathf.Clamp01(max.y / rect.height));
            }

            var pixels = sprite.texture.GetPixels32();
            var r = sprite.rect;
            var width = Mathf.RoundToInt(r.width);
            var height = Mathf.RoundToInt(r.height);
            var x0 = Mathf.RoundToInt(r.x);
            var y0 = Mathf.RoundToInt(r.y);
            var minX = width;
            var minY = height;
            var maxX = -1;
            var maxY = -1;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var index = (y0 + y) * sprite.texture.width + x0 + x;
                if (index < 0 || index >= pixels.Length || pixels[index].a < minimumAlpha) continue;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }

            return maxX < minX
                ? new Rect(0f, 0f, 1f, 1f)
                : Rect.MinMaxRect(
                    (float)minX / width,
                    (float)minY / height,
                    (float)(maxX + 1) / width,
                    (float)(maxY + 1) / height);
        }

        private void LateUpdate()
        {
            var now = Time.unscaledTime;
            foreach (var track in _tracks) UpdateTrack(track, now);
        }

        private void UpdateTrack(Track track, float now)
        {
            if (!track.Playing)
            {
                return;
            }

            var age = Mathf.Max(0f, now - track.StartedAt);
            track.ElapsedSeconds = age;
            if (age >= DurationSeconds)
            {
                CancelTrack(track);
                return;
            }

            if (!UpdateTrackLayout(track))
            {
                CancelTrack(track);
                return;
            }

            track.Graphic.Present(track.BodyRect, age / DurationSeconds, track.Kind);
        }

        private static void CancelTrack(Track track)
        {
            track.Playing = false;
            track.Kind = OccupantStatIncreaseKind.None;
            track.View = null;
            track.Tile = null;
            track.Board = null;
            track.BodyImage = null;
            track.RuntimeId = null;
            track.ElapsedSeconds = 0f;
            if (track.Graphic != null)
            {
                track.Graphic.ClearEffect();
            }

            if (track.Root != null)
            {
                track.Root.gameObject.SetActive(false);
            }
        }

        private void OnDisable() => Cancel();

        private void OnDestroy()
        {
            Cancel();
            foreach (var track in _tracks)
            {
                if (track.Root == null) continue;
                if (UnityEngine.Application.isPlaying) Destroy(track.Root.gameObject);
                else DestroyImmediate(track.Root.gameObject);
                track.Root = null;
                track.OverlayCanvas = null;
                track.Graphic = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            s_manifest = null;
            s_bounds.Clear();
        }
    }

    // Shared two-stage geometry; only the palette varies with the increased attributes.
    public sealed class StatIncreaseQiGraphic : MaskableGraphic
    {
        private const int RingSegments = 28;
        private const int StreamSteps = 8;
        private Color CrimsonQiColor = new Color(0.90f, 0.14f, 0.18f, 1f);
        private Color ScarletQiColor = new Color(1.00f, 0.24f, 0.22f, 1f);
        private Color CoreQiColor = new Color(1.00f, 0.50f, 0.44f, 1f);

        private Rect _bodyRect;
        private float _normalizedTime = 1f;

        public override Texture mainTexture => Texture2D.whiteTexture;

        public float NormalizedTime => _normalizedTime;
        public float RippleAlpha { get; private set; }
        public float RisingQiAlpha { get; private set; }
        public float PrimaryRippleRadiusX { get; private set; }
        public float RisingQiCenterY { get; private set; }

        public Color PrimaryColor => CrimsonQiColor;

        public void Present(Rect bodyRect, float normalizedTime, OccupantStatIncreaseKind kind)
        {
            if (kind == OccupantStatIncreaseKind.MaxHealth)
            {
                CrimsonQiColor = new Color(0.10f, 0.80f, 0.25f, 1f);
                ScarletQiColor = new Color(0.20f, 1.00f, 0.38f, 1f);
                CoreQiColor = new Color(0.55f, 1.00f, 0.65f, 1f);
            }
            else if (kind == OccupantStatIncreaseKind.Both)
            {
                CrimsonQiColor = new Color(0.95f, 0.76f, 0.08f, 1f);
                ScarletQiColor = new Color(1.00f, 0.90f, 0.16f, 1f);
                CoreQiColor = new Color(1.00f, 1.00f, 0.55f, 1f);
            }
            else
            {
                CrimsonQiColor = new Color(0.90f, 0.14f, 0.18f, 1f);
                ScarletQiColor = new Color(1.00f, 0.24f, 0.22f, 1f);
                CoreQiColor = new Color(1.00f, 0.50f, 0.44f, 1f);
            }
            _bodyRect = bodyRect;
            _normalizedTime = Mathf.Clamp01(normalizedTime);
            UpdateMetrics();
            SetVerticesDirty();
        }

        public void ClearEffect()
        {
            _normalizedTime = 1f;
            RippleAlpha = 0f;
            RisingQiAlpha = 0f;
            PrimaryRippleRadiusX = 0f;
            RisingQiCenterY = 0f;
            SetVerticesDirty();
        }

        private void UpdateMetrics()
        {
            if (_normalizedTime >= 1f || _bodyRect.width <= 0f || _bodyRect.height <= 0f)
            {
                RippleAlpha = 0f;
                RisingQiAlpha = 0f;
                PrimaryRippleRadiusX = 0f;
                RisingQiCenterY = 0f;
                return;
            }

            if (_normalizedTime < OccupantStatIncreaseVfx.RipplePhaseEndNormalized)
            {
                var p = Mathf.Clamp01(_normalizedTime / OccupantStatIncreaseVfx.RipplePhaseEndNormalized);
                var ease = 1f - (1f - p) * (1f - p);
                PrimaryRippleRadiusX = Mathf.Lerp(_bodyRect.width * 0.38f, _bodyRect.width * 1.18f, ease);
                var fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p / 0.18f));
                var fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.32f, 1f, p));
                RippleAlpha = OccupantStatIncreaseVfx.MaximumRippleAlpha * fadeIn * fadeOut;
            }
            else
            {
                PrimaryRippleRadiusX = 0f;
                RippleAlpha = 0f;
            }

            if (_normalizedTime >= OccupantStatIncreaseVfx.RisingQiStartNormalized)
            {
                var q = Mathf.Clamp01(
                    (_normalizedTime - OccupantStatIncreaseVfx.RisingQiStartNormalized) /
                    (1f - OccupantStatIncreaseVfx.RisingQiStartNormalized));
                var fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(q / 0.22f));
                var fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 1f, q));
                RisingQiAlpha = OccupantStatIncreaseVfx.MaximumRisingQiAlpha * fadeIn * fadeOut;
                var footY = _bodyRect.yMin + _bodyRect.height * 0.06f;
                var topY = _bodyRect.yMax + _bodyRect.height * 0.06f;
                RisingQiCenterY = Mathf.Lerp(footY + _bodyRect.height * 0.12f, topY - _bodyRect.height * 0.04f, Mathf.SmoothStep(0f, 1f, q));
            }
            else
            {
                RisingQiAlpha = 0f;
                RisingQiCenterY = 0f;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            UpdateMetrics();
            if (_normalizedTime >= 1f || _bodyRect.width <= 1f || _bodyRect.height <= 1f)
            {
                return;
            }

            var width = _bodyRect.width;
            var height = _bodyRect.height;
            var cx = _bodyRect.center.x;
            var footY = _bodyRect.yMin + height * 0.08f;
            var topY = _bodyRect.yMax + height * 0.10f;
            var footCenter = new Vector2(cx, footY);

            // Stage 1: Circular ground ripple expanding outward from the Master's feet.
            if (RippleAlpha > 0.004f)
            {
                var p1 = Mathf.Clamp01(_normalizedTime / OccupantStatIncreaseVfx.RipplePhaseEndNormalized);
                var thickness1 = Mathf.Lerp(width * 0.20f, width * 0.09f, p1);
                var rx1 = PrimaryRippleRadiusX;
                var ry1 = rx1 * 0.34f;
                var outerColor1 = WithAlpha(CrimsonQiColor, RippleAlpha * 0.72f);
                var coreColor1 = WithAlpha(Color.Lerp(ScarletQiColor, CoreQiColor, 0.45f), RippleAlpha);
                AddFeatheredEllipseRing(vh, footCenter, rx1, ry1, thickness1, coreColor1, outerColor1);

                // Subtle inner echo ring for a crisp martial-arts Qi pulse.
                if (_normalizedTime >= 0.07f && _normalizedTime <= 0.58f)
                {
                    var p2 = Mathf.Clamp01((_normalizedTime - 0.07f) / 0.51f);
                    var ease2 = 1f - (1f - p2) * (1f - p2);
                    var rx2 = Mathf.Lerp(width * 0.22f, width * 0.86f, ease2);
                    var ry2 = rx2 * 0.34f;
                    var thickness2 = Mathf.Lerp(width * 0.15f, width * 0.07f, p2);
                    var a2 = OccupantStatIncreaseVfx.MaximumRippleAlpha * 0.76f *
                             Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p2 / 0.20f)) *
                             (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.30f, 1f, p2)));
                    if (a2 > 0.004f)
                    {
                        AddFeatheredEllipseRing(
                            vh,
                            footCenter,
                            rx2,
                            ry2,
                            thickness2,
                            WithAlpha(ScarletQiColor, a2),
                            WithAlpha(CrimsonQiColor, a2 * 0.58f));
                    }
                }
            }

            // Stage 2: Rising Qi ribbons and motes ascending along the Master's flanks and fading out.
            if (RisingQiAlpha > 0.004f)
            {
                var q = Mathf.Clamp01(
                    (_normalizedTime - OccupantStatIncreaseVfx.RisingQiStartNormalized) /
                    (1f - OccupantStatIncreaseVfx.RisingQiStartNormalized));

                for (var sideIndex = 0; sideIndex < 2; sideIndex++)
                {
                    var side = sideIndex == 0 ? -1f : 1f;
                    for (var layer = 0; layer < 3; layer++)
                    {
                        var delay = layer * 0.08f;
                        var sq = Mathf.Clamp01((q - delay) / (1f - delay));
                        if (sq <= 0f || sq >= 1f) continue;

                        var localEnv = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sq / 0.20f)) *
                                       (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 1f, sq)));
                        var streamAlpha = OccupantStatIncreaseVfx.MaximumRisingQiAlpha * localEnv * (1f - layer * 0.12f);
                        if (streamAlpha <= 0.004f) continue;

                        var yMid = Mathf.Lerp(footY + height * 0.10f, topY - height * 0.04f, Mathf.SmoothStep(0f, 1f, sq));
                        var streamLen = height * Mathf.Lerp(0.38f, 0.54f, Mathf.Sin(sq * Mathf.PI));
                        var y0 = Mathf.Max(footY - height * 0.02f, yMid - streamLen * 0.5f);
                        var y1 = Mathf.Min(topY + height * 0.10f, yMid + streamLen * 0.5f);
                        if (y1 - y0 <= 1f) continue;

                        var baseHalfWidth = width * Mathf.Lerp(0.15f, 0.095f, layer * 0.5f);
                        var outerTint = Color.Lerp(CrimsonQiColor, ScarletQiColor, layer * 0.35f);
                        var innerTint = Color.Lerp(ScarletQiColor, CoreQiColor, 0.55f);
                        AddRisingQiRibbon(
                            vh,
                            cx,
                            footY,
                            height,
                            width,
                            side,
                            layer,
                            sq,
                            y0,
                            y1,
                            baseHalfWidth,
                            WithAlpha(outerTint, streamAlpha * 0.78f),
                            WithAlpha(innerTint, streamAlpha));
                    }
                }

                // Small rising Qi motes along the outer silhouette.
                for (var i = 0; i < 6; i++)
                {
                    var side = (i % 2 == 0) ? -1f : 1f;
                    var band = i / 2;
                    var seed = 0.17f * i;
                    var mq = Mathf.Clamp01((q - band * 0.07f) / (1f - band * 0.07f));
                    if (mq <= 0f || mq >= 1f) continue;

                    var moteAlpha = RisingQiAlpha * 0.90f * Mathf.Sin(mq * Mathf.PI);
                    if (moteAlpha <= 0.004f) continue;

                    var y = Mathf.Lerp(footY + height * 0.08f, topY + height * 0.06f, mq);
                    var hNorm = Mathf.Clamp01((y - footY) / Mathf.Max(1f, height));
                    var xOffset = side * width * (0.44f + band * 0.14f - hNorm * 0.06f) +
                                  Mathf.Sin(hNorm * 5f + seed * 6f) * width * 0.04f;
                    var moteSize = new Vector2(width * 0.11f, height * 0.14f) * Mathf.Lerp(0.95f, 0.60f, mq);
                    var moteColor = WithAlpha(Color.Lerp(ScarletQiColor, CoreQiColor, 0.5f), moteAlpha);
                    AddDiamond(vh, new Vector2(cx + xOffset, y), moteSize, moteColor);
                }
            }
        }

        private static void AddFeatheredEllipseRing(
            VertexHelper vh,
            Vector2 center,
            float radiusX,
            float radiusY,
            float thickness,
            Color coreColor,
            Color edgeColor)
        {
            var half = Mathf.Max(0.5f, thickness * 0.5f);
            var innerRx = Mathf.Max(0.5f, radiusX - half);
            var innerRy = Mathf.Max(0.5f, radiusY - half * 0.38f);
            var outerRx = radiusX + half;
            var outerRy = radiusY + half * 0.38f;
            var clearEdge = new Color(edgeColor.r, edgeColor.g, edgeColor.b, 0f);

            var baseVert = vh.currentVertCount;
            for (var i = 0; i <= RingSegments; i++)
            {
                var angle = (float)i / RingSegments * Mathf.PI * 2f;
                var cos = Mathf.Cos(angle);
                var sin = Mathf.Sin(angle);

                var pInner = center + new Vector2(cos * innerRx, sin * innerRy);
                var pMid = center + new Vector2(cos * radiusX, sin * radiusY);
                var pOuter = center + new Vector2(cos * outerRx, sin * outerRy);

                vh.AddVert(pInner, clearEdge, new Vector2(0f, 0f));
                vh.AddVert(pMid, coreColor, new Vector2(0.5f, 0.5f));
                vh.AddVert(pOuter, clearEdge, new Vector2(1f, 1f));
            }

            for (var i = 0; i < RingSegments; i++)
            {
                var i0 = baseVert + i * 3;
                var i1 = baseVert + (i + 1) * 3;

                vh.AddTriangle(i0, i0 + 1, i1 + 1);
                vh.AddTriangle(i0, i1 + 1, i1);

                vh.AddTriangle(i0 + 1, i0 + 2, i1 + 2);
                vh.AddTriangle(i0 + 1, i1 + 2, i1 + 1);
            }
        }

        private static void AddRisingQiRibbon(
            VertexHelper vh,
            float centerX,
            float footY,
            float bodyHeight,
            float bodyWidth,
            float side,
            int layer,
            float streamProgress,
            float y0,
            float y1,
            float maxHalfWidth,
            Color edgeColor,
            Color coreColor)
        {
            var clearEdge = new Color(edgeColor.r, edgeColor.g, edgeColor.b, 0f);
            var baseVert = vh.currentVertCount;

            for (var step = 0; step <= StreamSteps; step++)
            {
                var t = (float)step / StreamSteps;
                var y = Mathf.Lerp(y0, y1, t);
                var hNorm = Mathf.Clamp01((y - footY) / Mathf.Max(1f, bodyHeight));
                var flankOffset = bodyWidth * (0.42f + layer * 0.13f - hNorm * 0.08f);
                var wave = Mathf.Sin(hNorm * 4.2f - streamProgress * 5.0f + layer * 1.3f) * bodyWidth * 0.05f;
                var x = centerX + side * (flankOffset + wave);

                var taper = Mathf.Sin(t * Mathf.PI);
                var halfW = Mathf.Max(0.25f, maxHalfWidth * taper);
                var localCore = new Color(coreColor.r, coreColor.g, coreColor.b, coreColor.a * taper);

                vh.AddVert(new Vector2(x - halfW, y), clearEdge, new Vector2(0f, t));
                vh.AddVert(new Vector2(x, y), localCore, new Vector2(0.5f, t));
                vh.AddVert(new Vector2(x + halfW, y), clearEdge, new Vector2(1f, t));
            }

            for (var step = 0; step < StreamSteps; step++)
            {
                var i0 = baseVert + step * 3;
                var i1 = baseVert + (step + 1) * 3;

                vh.AddTriangle(i0, i0 + 1, i1 + 1);
                vh.AddTriangle(i0, i1 + 1, i1);

                vh.AddTriangle(i0 + 1, i0 + 2, i1 + 2);
                vh.AddTriangle(i0 + 1, i1 + 2, i1 + 1);
            }
        }

        private static void AddDiamond(VertexHelper vh, Vector2 center, Vector2 size, Color coreColor)
        {
            var edge = new Color(coreColor.r, coreColor.g, coreColor.b, 0f);
            var halfX = size.x * 0.5f;
            var halfY = size.y * 0.5f;
            var start = vh.currentVertCount;

            vh.AddVert(center, coreColor, new Vector2(0.5f, 0.5f));
            vh.AddVert(center + new Vector2(0f, halfY), edge, new Vector2(0.5f, 1f));
            vh.AddVert(center + new Vector2(halfX, 0f), edge, new Vector2(1f, 0.5f));
            vh.AddVert(center + new Vector2(0f, -halfY), edge, new Vector2(0.5f, 0f));
            vh.AddVert(center + new Vector2(-halfX, 0f), edge, new Vector2(0f, 0.5f));

            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
            vh.AddTriangle(start, start + 3, start + 4);
            vh.AddTriangle(start, start + 4, start + 1);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
