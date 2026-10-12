using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    // Reads the current occupant only; this loop never applies or clears a combat status.
    internal sealed class DrainedStatusVfx : MonoBehaviour
    {
        internal const string SpriteResourcePath = "Project333/StatusEffects/Drained/ElectricArcs";
        internal const string OverlayImageName = "DrainedElectricArcs";
        internal const float FramesPerSecond = 12f;
        internal const float RestDuration = .75f;
        internal const float MaximumAlpha = .55f;
        internal const float FadeDuration = .1f;

        private static Sprite[] s_frames;
        private Image _image;
        private OccupantState _boundOccupant;
        private RectTransform _visualRoot;
        private float _startedAt;

        internal static IReadOnlyList<Sprite> Frames
        {
            get
            {
                if (s_frames != null) return s_frames;
                s_frames = Resources.LoadAll<Sprite>(SpriteResourcePath);
                Array.Sort(s_frames, (left, right) => string.CompareOrdinal(left.name, right.name));
                return s_frames;
            }
        }

        // The tile calls this after its native Animator/Image synchronization, avoiding
        // a second update-order dependency and following live motion and reconnect objects.
        internal void Refresh(OccupantState occupant, RectTransform visualRoot,
            Image visualImage, bool isVisualActive, bool isDeathPresentation)
        {
            if (!UnityEngine.Application.isPlaying || !isActiveAndEnabled || !isVisualActive ||
                isDeathPresentation || occupant == null || !occupant.IsAlive ||
                !occupant.IsDrained || occupant.IsSealbound ||
                (occupant.OwnerId == PlayerId.AI && occupant.IsHiding) ||
                visualRoot == null || !visualRoot.gameObject.activeInHierarchy ||
                visualImage == null || !visualImage.isActiveAndEnabled || visualImage.sprite == null)
            {
                Cancel();
                return;
            }

            var frames = Frames;
            if (frames.Count == 0) { Cancel(); return; }
            EnsureImage(visualRoot);
            if (_boundOccupant == null ||
                !string.Equals(_boundOccupant.RuntimeId, occupant.RuntimeId, StringComparison.Ordinal) ||
                _visualRoot != visualRoot)
            {
                _startedAt = Time.unscaledTime;
            }
            // Online snapshots may replace every state object. Keep the visual clock
            // for the same occupant while always reading the freshly supplied state.
            _boundOccupant = occupant;
            _visualRoot = visualRoot;

            ApplyBodyLayout(visualImage);
            var burstDuration = frames.Count / FramesPerSecond;
            var elapsed = Mathf.Repeat(Time.unscaledTime - _startedAt, burstDuration + RestDuration);
            if (elapsed >= burstDuration)
            {
                _image.enabled = false;
                return;
            }

            var frame = Mathf.Min(frames.Count - 1, Mathf.FloorToInt(elapsed * FramesPerSecond));
            if (_image.sprite != frames[frame]) _image.sprite = frames[frame];
            var fade = Mathf.SmoothStep(0f, 1f, Mathf.Min(elapsed / FadeDuration,
                (burstDuration - elapsed) / FadeDuration));
            _image.color = new Color(1f, 1f, 1f, MaximumAlpha * fade);
            _image.enabled = true;
        }

        internal void Cancel()
        {
            _boundOccupant = null;
            if (_image != null) _image.enabled = false;
        }

        private void EnsureImage(RectTransform visualRoot)
        {
            if (_image == null)
            {
                var overlay = new GameObject(OverlayImageName, typeof(RectTransform), typeof(Image));
                _image = overlay.GetComponent<Image>();
                _image.raycastTarget = false;
                _image.preserveAspect = false;
            }
            if (_image.rectTransform.parent != visualRoot)
                _image.rectTransform.SetParent(visualRoot, false);
            // The electric fragments sit above the sprite, below the tile's stat children.
            if (_image.rectTransform.GetSiblingIndex() != 0)
                _image.rectTransform.SetAsFirstSibling();
        }

        private void ApplyBodyLayout(Image visualImage)
        {
            var drawRect = visualImage.GetPixelAdjustedRect();
            var sprite = visualImage.sprite;
            var spriteSize = sprite.rect.size;
            if (visualImage.preserveAspect && spriteSize.x > 0f && spriteSize.y > 0f &&
                drawRect.width > 0f && drawRect.height > 0f)
            {
                var scale = Mathf.Min(drawRect.width / spriteSize.x, drawRect.height / spriteSize.y);
                var drawSize = spriteSize * scale;
                drawRect = new Rect(drawRect.center - drawSize * .5f, drawSize);
            }

            // Use native sprite bounds (including its pivot) when a tight mesh is authored.
            // Full-rect 512px sprites use the same proportional lower-body region.
            var bounds = sprite.bounds;
            var pivot = sprite.pivot;
            var pixelsPerUnit = sprite.pixelsPerUnit;
            var minimum = new Vector2(bounds.min.x, bounds.min.y) * pixelsPerUnit + pivot;
            var maximum = new Vector2(bounds.max.x, bounds.max.y) * pixelsPerUnit + pivot;
            minimum = new Vector2(Mathf.Clamp01(minimum.x / spriteSize.x), Mathf.Clamp01(minimum.y / spriteSize.y));
            maximum = new Vector2(Mathf.Clamp01(maximum.x / spriteSize.x), Mathf.Clamp01(maximum.y / spriteSize.y));
            var bodySize = Vector2.Scale(maximum - minimum, drawRect.size);
            var bodyCenter = drawRect.min + Vector2.Scale(minimum, drawRect.size) +
                Vector2.Scale(bodySize, new Vector2(.5f, .35f));

            var rect = _image.rectTransform;
            rect.anchorMin = rect.anchorMax = visualImage.rectTransform.pivot;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = bodyCenter;
            rect.sizeDelta = Vector2.Scale(bodySize, new Vector2(.74f, .5f));
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private void OnDisable() => Cancel();

        private void OnDestroy()
        {
            Cancel();
            if (_image == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(_image.gameObject);
            else DestroyImmediate(_image.gameObject);
            _image = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => s_frames = null;
    }
}
