using UnityEngine;

namespace Project333.Runtime.Presentation.Hand
{
    // Owned by a hand presenter, so settled geometry survives successive draws without
    // static state or event subscriptions. Metadata checks do not project any points.
    public sealed class CardDrawOriginCache
    {
        private RectTransform _source, _parent;
        private Transform _sourceParent, _parentParent;
        private UnityEngine.UI.Image _image;
        private Canvas _sourceCanvas, _parentCanvas;
        private LayoutKey _key;
        private CardDrawFlight.Pose _pose;
        private bool _hasPose;

#if UNITY_EDITOR
        internal int RecalculationCount { get; private set; }
#endif

        public bool TryGetPose(RectTransform source, RectTransform parent, out CardDrawFlight.Pose pose)
        {
            pose = default;
            if (source == null || parent == null) return false;
            if (_source != source || _parent != parent ||
                _sourceParent != source.parent || _parentParent != parent.parent)
            {
                _source = source; _parent = parent;
                _sourceParent = source.parent; _parentParent = parent.parent;
                _image = source.GetComponent<UnityEngine.UI.Image>();
                _sourceCanvas = source.GetComponentInParent<Canvas>(true);
                _parentCanvas = parent.GetComponentInParent<Canvas>(true);
                _hasPose = false;
            }
            // Graphic tracks Canvas hierarchy changes. Keep the last known Canvas when
            // the exact final deck layer is hidden after its card was drawn.
            if (_image != null && _image.isActiveAndEnabled && _image.canvas != null)
                _sourceCanvas = _image.canvas;
            var from = _sourceCanvas == null ? null : _sourceCanvas.rootCanvas;
            var to = _parentCanvas == null ? null : _parentCanvas.rootCanvas;
            var sprite = _image == null ? null : _image.overrideSprite;
            var key = new LayoutKey
            {
                SourceToWorld = source.localToWorldMatrix, WorldToParent = parent.worldToLocalMatrix,
                SourceRect = source.rect, ParentRect = parent.rect,
                Sprite = sprite, SpriteRect = sprite == null ? default : sprite.rect,
                PreserveAspect = _image != null && _image.preserveAspect,
                SourceRoot = from, ParentRoot = to,
                SourceScale = _sourceCanvas == null ? 1f : _sourceCanvas.scaleFactor,
                ParentScale = _parentCanvas == null ? 1f : _parentCanvas.scaleFactor,
                PixelPerfect = _sourceCanvas != null && _sourceCanvas.pixelPerfect,
                SourceMode = from == null ? RenderMode.ScreenSpaceOverlay : from.renderMode,
                ParentMode = to == null ? RenderMode.ScreenSpaceOverlay : to.renderMode,
                SourceDisplay = from == null ? 0 : from.targetDisplay,
                ParentDisplay = to == null ? 0 : to.targetDisplay,
                ScreenSize = new Vector2Int(Screen.width, Screen.height), SafeArea = Screen.safeArea,
                SourceCamera = CameraKey.Capture(from), ParentCamera = CameraKey.Capture(to)
            };
            if (_hasPose && _key.Matches(key)) { pose = _pose; return true; }
#if UNITY_EDITOR
            RecalculationCount++;
#endif
            if (!CardDrawFlight.Pose.TryCapture(source, parent, out pose)) { _hasPose = false; return false; }
            _key = key; _pose = pose; _hasPose = true;
            return true;
        }

        public void Clear()
        {
            _hasPose = false; _key = default; _pose = default;
            _source = null; _parent = null; _sourceParent = null; _parentParent = null;
            _image = null; _sourceCanvas = null; _parentCanvas = null;
#if UNITY_EDITOR
            RecalculationCount = 0;
#endif
        }

        private struct CameraKey
        {
            public Camera Camera;
            public Matrix4x4 View, Projection;
            public Rect PixelRect;
            public RenderTexture Target;
            public int Display;
            public static CameraKey Capture(Canvas canvas)
            {
                var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                return camera == null ? default : new CameraKey
                { Camera = camera, View = camera.worldToCameraMatrix, Projection = camera.projectionMatrix,
                    PixelRect = camera.pixelRect, Target = camera.targetTexture, Display = camera.targetDisplay };
            }
            public bool Matches(CameraKey other) => Camera == other.Camera && View.Equals(other.View) &&
                Projection.Equals(other.Projection) && PixelRect.Equals(other.PixelRect) &&
                Target == other.Target && Display == other.Display;
        }

        private struct LayoutKey
        {
            public Matrix4x4 SourceToWorld, WorldToParent;
            public Rect SourceRect, ParentRect, SpriteRect, SafeArea;
            public Sprite Sprite;
            public bool PreserveAspect, PixelPerfect;
            public Canvas SourceRoot, ParentRoot;
            public float SourceScale, ParentScale;
            public RenderMode SourceMode, ParentMode;
            public int SourceDisplay, ParentDisplay;
            public Vector2Int ScreenSize;
            public CameraKey SourceCamera, ParentCamera;
            public bool Matches(LayoutKey other) =>
                SourceToWorld.Equals(other.SourceToWorld) && WorldToParent.Equals(other.WorldToParent) &&
                SourceRect.Equals(other.SourceRect) && ParentRect.Equals(other.ParentRect) &&
                Sprite == other.Sprite && SpriteRect.Equals(other.SpriteRect) && PreserveAspect == other.PreserveAspect &&
                SourceRoot == other.SourceRoot && ParentRoot == other.ParentRoot &&
                SourceScale == other.SourceScale && ParentScale == other.ParentScale && PixelPerfect == other.PixelPerfect &&
                SourceMode == other.SourceMode && ParentMode == other.ParentMode &&
                SourceDisplay == other.SourceDisplay && ParentDisplay == other.ParentDisplay &&
                ScreenSize.Equals(other.ScreenSize) && SafeArea.Equals(other.SafeArea) &&
                SourceCamera.Matches(other.SourceCamera) && ParentCamera.Matches(other.ParentCamera);
        }
    }
}
