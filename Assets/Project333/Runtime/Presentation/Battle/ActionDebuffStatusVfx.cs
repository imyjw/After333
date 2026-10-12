using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    // Separate Images and an unscaled clock keep these effects moving around a frozen body.
    internal sealed class ActionDebuffStatusVfx : MonoBehaviour
    {
        internal const string StunResourcePath = "Project333/StatusEffects/Stun/OrbitStars";
        internal const string FreezeResourcePath = "Project333/StatusEffects/Freeze/IceCrystal";
        internal const string BoundsResourcePath = "Project333/StatusEffects/BodyBounds";
        internal const string StunImageName = "StunOrbitStars";
        internal const string FreezeImageName = "FreezeIceCrystal";
        internal const float FramesPerSecond = 8f;
        internal const float StunMaximumAlpha = .86f;
        internal const float FreezeMinimumAlpha = .68f;
        internal const float FreezeMaximumAlpha = .72f;

        private static Sprite[] s_stunFrames;
        private static Sprite[] s_freezeFrames;
        private static Rect s_stunBounds;
        private static Rect s_freezeBounds;
        private static bool s_manifestLoaded;
        private static readonly Dictionary<Sprite, Dictionary<string, Rect>> s_opaqueBounds =
            new Dictionary<Sprite, Dictionary<string, Rect>>();
        private static readonly Dictionary<string, Rect> s_manifestBounds =
            new Dictionary<string, Rect>(StringComparer.Ordinal);
        private static readonly Dictionary<Texture2D, Color32[]> s_alphaPixels = new Dictionary<Texture2D, Color32[]>();
        private Image _stunImage;
        private Image _freezeImage;
        private OccupantState _boundOccupant;
        private RectTransform _visualRoot;
        private float _stunStartedAt;
        private float _freezeStartedAt;
        private bool _stunWasActive;
        private bool _freezeWasActive;

        internal static IReadOnlyList<Sprite> StunFrames
        {
            get
            {
                if (s_stunFrames == null) s_stunFrames = LoadFrames(StunResourcePath, out s_stunBounds);
                return s_stunFrames;
            }
        }

        internal static IReadOnlyList<Sprite> FreezeFrames
        {
            get
            {
                if (s_freezeFrames == null) s_freezeFrames = LoadFrames(FreezeResourcePath, out s_freezeBounds);
                return s_freezeFrames;
            }
        }

        internal void Refresh(OccupantState occupant, RectTransform visualRoot, Image visualImage,
            bool isVisualActive, bool isDeathPresentation, string controllerName = null, string cardId = null)
        {
            if (!UnityEngine.Application.isPlaying || !isActiveAndEnabled || !isVisualActive ||
                isDeathPresentation || occupant == null || !occupant.IsAlive ||
                (occupant.OwnerId == PlayerId.AI && occupant.IsHiding) ||
                visualRoot == null || !visualRoot.gameObject.activeInHierarchy ||
                visualImage == null || !visualImage.isActiveAndEnabled || visualImage.sprite == null)
            {
                Cancel();
                return;
            }
            if (!occupant.IsStunned && !occupant.IsFrozen)
            {
                Cancel();
                return;
            }
            if (_boundOccupant == null || _visualRoot != visualRoot ||
                !string.Equals(_boundOccupant.RuntimeId, occupant.RuntimeId, StringComparison.Ordinal))
            {
                _stunWasActive = _freezeWasActive = false;
            }
            // Reconnect supplies fresh objects with the same RuntimeId: preserve each effect's phase.
            _boundOccupant = occupant;
            _visualRoot = visualRoot;
            var visualKey = string.IsNullOrEmpty(controllerName) ? cardId : controllerName;
            var body = ResolveBodyRect(visualImage, visualRoot, visualKey);

            var stunFrames = StunFrames;
            if (occupant.IsStunned && stunFrames.Count > 0)
            {
                if (!_stunWasActive) _stunStartedAt = Time.unscaledTime;
                _stunWasActive = true;
                EnsureImage(ref _stunImage, StunImageName, visualRoot);
                var width = Mathf.Max(body.width * .72f, body.height * .34f);
                var size = new Vector2(width, width * .32f);
                var center = new Vector2(body.center.x, body.yMax + body.height * .035f + size.y * .5f);
                ApplyLayout(_stunImage, visualRoot, center, size, s_stunBounds);
                ShowFrame(_stunImage, stunFrames, Time.unscaledTime - _stunStartedAt, StunMaximumAlpha);
            }
            else
            {
                _stunWasActive = false;
                if (_stunImage != null) _stunImage.enabled = false;
            }

            var freezeFrames = FreezeFrames;
            if (occupant.IsFrozen && freezeFrames.Count > 0)
            {
                if (!_freezeWasActive) _freezeStartedAt = Time.unscaledTime;
                _freezeWasActive = true;
                EnsureImage(ref _freezeImage, FreezeImageName, visualRoot);
                var size = new Vector2(Mathf.Max(body.width * 1.16f, body.height * .38f), body.height * 1.12f);
                var elapsed = Time.unscaledTime - _freezeStartedAt;
                var shimmer = .5f + .5f * Mathf.Sin(elapsed * Mathf.PI);
                ApplyLayout(_freezeImage, visualRoot, body.center + Vector2.up * body.height * .02f,
                    size, s_freezeBounds);
                ShowFrame(_freezeImage, freezeFrames, elapsed,
                    Mathf.Lerp(FreezeMinimumAlpha, FreezeMaximumAlpha, shimmer));
            }
            else
            {
                _freezeWasActive = false;
                if (_freezeImage != null) _freezeImage.enabled = false;
            }
        }

        internal void Cancel()
        {
            _boundOccupant = null;
            _stunWasActive = _freezeWasActive = false;
            if (_stunImage != null) _stunImage.enabled = false;
            if (_freezeImage != null) _freezeImage.enabled = false;
        }

        private static Sprite[] LoadFrames(string path, out Rect bounds)
        {
            var frames = Resources.LoadAll<Sprite>(path);
            Array.Sort(frames, (left, right) => string.CompareOrdinal(left.name, right.name));
            bounds = new Rect(0f, 0f, 1f, 1f);
            if (frames.Length == 0) return frames;
            bounds = OpaqueBounds(frames[0], retainPixels: true);
            for (var i = 1; i < frames.Length; i++)
            {
                var next = OpaqueBounds(frames[i], retainPixels: true);
                bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, next.xMin), Mathf.Min(bounds.yMin, next.yMin),
                    Mathf.Max(bounds.xMax, next.xMax), Mathf.Max(bounds.yMax, next.yMax));
            }
            return frames;
        }

        private static void EnsureImage(ref Image image, string name, RectTransform visualRoot)
        {
            if (image == null)
            {
                var overlay = new GameObject(name, typeof(RectTransform), typeof(Image));
                image = overlay.GetComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = false;
            }
            if (image.rectTransform.parent != visualRoot)
            {
                image.rectTransform.SetParent(visualRoot, false);
                // The status art inherits the body Canvas and remains below stat/preview children.
                image.rectTransform.SetAsFirstSibling();
            }
        }

        private static void ShowFrame(Image image, IReadOnlyList<Sprite> frames, float elapsed, float alpha)
        {
            var frame = Mathf.FloorToInt(Mathf.Repeat(elapsed * FramesPerSecond, frames.Count));
            if (image.sprite != frames[frame]) image.sprite = frames[frame];
            image.color = new Color(1f, 1f, 1f, alpha);
            image.enabled = true;
        }

        private static void ApplyLayout(Image image, RectTransform root, Vector2 center, Vector2 visibleSize,
            Rect opaqueBounds)
        {
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = root.pivot;
            rect.pivot = opaqueBounds.center;
            rect.anchoredPosition = center;
            rect.sizeDelta = new Vector2(visibleSize.x / Mathf.Max(.001f, opaqueBounds.width),
                visibleSize.y / Mathf.Max(.001f, opaqueBounds.height));
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static Rect ResolveBodyRect(Image image, RectTransform root, string visualKey)
        {
            var drawRect = image.GetPixelAdjustedRect();
            var spriteSize = image.sprite.rect.size;
            if (image.preserveAspect && spriteSize.x > 0f && spriteSize.y > 0f)
            {
                var scale = Mathf.Min(drawRect.width / spriteSize.x, drawRect.height / spriteSize.y);
                var drawSize = spriteSize * scale;
                drawRect = new Rect(drawRect.center - drawSize * .5f, drawSize);
            }
            var opaque = OpaqueBounds(image.sprite, visualKey);
            var localMin = drawRect.min + Vector2.Scale(opaque.min, drawRect.size);
            var localMax = drawRect.min + Vector2.Scale(opaque.max, drawRect.size);
            var minimum = root.InverseTransformPoint(image.rectTransform.TransformPoint(localMin));
            var maximum = root.InverseTransformPoint(image.rectTransform.TransformPoint(localMax));
            return Rect.MinMaxRect(Mathf.Min(minimum.x, maximum.x), Mathf.Min(minimum.y, maximum.y),
                Mathf.Max(minimum.x, maximum.x), Mathf.Max(minimum.y, maximum.y));
        }

        private static Rect OpaqueBounds(Sprite sprite, string visualKey = null, bool retainPixels = false)
        {
            visualKey = visualKey ?? string.Empty;
            if (!s_opaqueBounds.TryGetValue(sprite, out var cachedByVisual))
            {
                cachedByVisual = new Dictionary<string, Rect>(StringComparer.Ordinal);
                s_opaqueBounds.Add(sprite, cachedByVisual);
            }
            if (cachedByVisual.TryGetValue(visualKey, out var cached)) return cached;
            EnsureManifest();
            var key = StatusSpriteBoundsAsset.CreateKey(visualKey, sprite.texture.name, sprite.name, sprite.rect);
            if (s_manifestBounds.TryGetValue(key, out var authored))
            {
                cachedByVisual.Add(visualKey, authored);
                return authored;
            }
            var rect = sprite.rect;
            var result = new Rect(0f, 0f, 1f, 1f);
            if (sprite.texture.isReadable && !sprite.packed)
            {
                var texture = sprite.texture;
                if (!s_alphaPixels.TryGetValue(texture, out var pixels))
                {
                    pixels = texture.GetPixels32();
                    // Only the two small status atlases retain CPU pixels during bounds baking.
                    // Native body art uses the metadata-only manifest, and a readable fallback
                    // keeps just its resulting Rect rather than retaining a whole unit atlas.
                    if (retainPixels) s_alphaPixels.Add(texture, pixels);
                }
                var x0 = Mathf.RoundToInt(rect.x);
                var y0 = Mathf.RoundToInt(rect.y);
                var width = Mathf.RoundToInt(rect.width);
                var height = Mathf.RoundToInt(rect.height);
                var minX = width; var minY = height; var maxX = -1; var maxY = -1;
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var index = (y0 + y) * texture.width + x0 + x;
                    if (index < 0 || index >= pixels.Length || pixels[index].a < 16) continue;
                    minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                }
                if (maxX >= minX && maxY >= minY)
                    result = Rect.MinMaxRect((float)minX / width, (float)minY / height,
                        (float)(maxX + 1) / width, (float)(maxY + 1) / height);
            }
            else
            {
                var bounds = sprite.bounds;
                var minimum = new Vector2(bounds.min.x, bounds.min.y) * sprite.pixelsPerUnit + sprite.pivot;
                var maximum = new Vector2(bounds.max.x, bounds.max.y) * sprite.pixelsPerUnit + sprite.pivot;
                result = Rect.MinMaxRect(Mathf.Clamp01(minimum.x / rect.width), Mathf.Clamp01(minimum.y / rect.height),
                    Mathf.Clamp01(maximum.x / rect.width), Mathf.Clamp01(maximum.y / rect.height));
            }
            if (result.width <= 0f || result.height <= 0f) result = new Rect(0f, 0f, 1f, 1f);
            cachedByVisual.Add(visualKey, result);
            return result;
        }

        private static void EnsureManifest()
        {
            if (s_manifestLoaded) return;
            s_manifestLoaded = true;
            var asset = Resources.Load<StatusSpriteBoundsAsset>(BoundsResourcePath);
            if (asset == null || asset.Entries == null) return;
            foreach (var entry in asset.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.VisualKey) ||
                    entry.NormalizedBounds.width <= 0f || entry.NormalizedBounds.height <= 0f) continue;
                var key = StatusSpriteBoundsAsset.CreateKey(entry.VisualKey, entry.TextureName,
                    entry.SpriteName, entry.SpriteRect);
                s_manifestBounds[key] = entry.NormalizedBounds;
            }
        }

        private void OnDisable() => Cancel();

        private void OnDestroy()
        {
            Cancel();
            DestroyImage(ref _stunImage);
            DestroyImage(ref _freezeImage);
        }

        private static void DestroyImage(ref Image image)
        {
            if (image == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(image.gameObject);
            else DestroyImmediate(image.gameObject);
            image = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            s_stunFrames = s_freezeFrames = null;
            s_manifestLoaded = false;
            s_opaqueBounds.Clear();
            s_manifestBounds.Clear();
            s_alphaPixels.Clear();
        }
    }
}
