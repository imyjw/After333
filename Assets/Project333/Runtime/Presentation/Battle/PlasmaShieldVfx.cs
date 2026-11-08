using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    // A successful cast cue only. No persistent shield object or battle-state writes.
    public sealed class PlasmaShieldVfx : MonoBehaviour
    {
        public const string FramesResourcePath = "Project333/SpellEffects/PlasmaShield/Formation";
        public const int FrameCount = 8;
        public const float FixedFrameSize = 512f;
        public const float FramesPerSecond = 12f;
        public const float FormationDurationSeconds = FrameCount / FramesPerSecond;
        public const float FadeDurationSeconds = .15f;
        public const float DurationSeconds = FormationDurationSeconds + FadeDurationSeconds;
        private static Sprite[] s_frames;
        private TileTextView _view;
        private TileView _tile;
        private Image _bodyImage, _image;
        private RectTransform _root;
        private string _runtimeId;
        private float _startedAt;
        private bool _playing;
        private int _imageFrame;
        internal bool IsPlaying => _playing;
        internal float RemainingDurationSeconds => _playing ? Mathf.Max(0, DurationSeconds - (Time.unscaledTime - _startedAt)) : 0;

        internal float Play(TileTextView view, string runtimeId)
        {
            Cancel();
            if (!isActiveAndEnabled || view == null || !view.isActiveAndEnabled ||
                string.IsNullOrEmpty(runtimeId) || view.VisualRuntimeId != runtimeId) return 0;
            _view = view; _tile = view.GetComponent<TileView>(); _runtimeId = runtimeId;
            _bodyImage = view.OccupantVisualImage;
            if (!HasCurrentVisibleTarget()) { Cancel(); return 0; }
            var canvas = view.GetVisualRootCanvas();
            if (canvas == null || !canvas.isActiveAndEnabled || !LoadFrames()) { Cancel(); return 0; }
            _imageFrame = 0;
            EnsureOverlay(canvas.rootCanvas);
            if (!UpdateLayout()) { Cancel(); return 0; }
            _startedAt = Time.unscaledTime; _playing = true;
            _image.sprite = s_frames[0]; _image.color = new Color(1,1,1,.38f);
            _root.gameObject.SetActive(true);
            // Unity resets nested Canvas sorting on parenting and activation.
            var overlay = _root.GetComponent<Canvas>(); overlay.overrideSorting = true; overlay.sortingOrder = 4908;
            return DurationSeconds;
        }

        private bool HasCurrentVisibleTarget()
        {
            var occupant = _tile == null ? null : _tile.CurrentOccupant;
            return _view != null && _view.isActiveAndEnabled && !_view.IsDeathPresentation &&
                _tile != null && _tile.CurrentOccupantRuntimeId == _runtimeId &&
                _view.VisualRuntimeId == _runtimeId && occupant != null && occupant.IsAlive &&
                occupant.Kind != OccupantKind.Master && !occupant.IsSealbound &&
                !(occupant.OwnerId == PlayerId.AI && occupant.IsHiding) &&
                _bodyImage != null && _bodyImage.isActiveAndEnabled && _bodyImage.sprite != null;
        }

        private static bool LoadFrames()
        {
            if (s_frames != null && s_frames.Length == FrameCount) return true;
            s_frames = Resources.LoadAll<Sprite>(FramesResourcePath);
            Array.Sort(s_frames,(a,b)=>string.CompareOrdinal(a.name,b.name));
            return s_frames.Length == FrameCount;
        }

        private void EnsureOverlay(Canvas canvas)
        {
            if (_root == null)
            {
                var host = new GameObject("PlasmaShieldOverlay",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));
                host.SetActive(false); _root = host.GetComponent<RectTransform>();
                var group = host.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
                _image = new GameObject("PlasmaShieldFormation",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
                _image.rectTransform.SetParent(_root,false); _image.raycastTarget = false; _image.preserveAspect = false;
                _image.rectTransform.anchorMin = _image.rectTransform.anchorMax = Vector2.one * .5f;
                _image.rectTransform.pivot = new Vector2(.5f,0f);
            }
            _root.SetParent(canvas.transform,false); _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;
            var overlay = _root.GetComponent<Canvas>(); overlay.overrideSorting = true; overlay.sortingOrder = 4908;
        }

        private bool UpdateLayout()
        {
            if (!HasCurrentVisibleTarget() || _root == null || _root.parent == null ||
                !_root.parent.gameObject.activeInHierarchy || _root.rect.width <= 0 || _root.rect.height <= 0) return false;
            // Follow the shared 16px foot line in a 512px cell without measuring body bounds.
            // The independent overlay never inherits occupant scale or mirroring.
            _image.rectTransform.pivot = new Vector2(.5f,0f);
            _image.rectTransform.anchoredPosition = _root.InverseTransformPoint(_view.GetVisualNormalizedWorldPosition(new Vector2(.5f,1f/32f)));
            _image.rectTransform.sizeDelta = Vector2.one * FixedFrameSize;
            _image.rectTransform.localScale = Vector3.one;
            _image.rectTransform.localRotation = Quaternion.identity;
            return true;
        }

        private void LateUpdate()
        {
            if (!_playing) return;
            var age=Mathf.Max(0,Time.unscaledTime-_startedAt);
            if (age>=DurationSeconds) { Cancel();return; }
            _imageFrame=Mathf.Min(FrameCount-1,Mathf.FloorToInt(age*FramesPerSecond));
            if (!UpdateLayout()) { Cancel();return; }
            _image.sprite=s_frames[_imageFrame];
            var alpha=.72f*Mathf.SmoothStep(.53f,1f,Mathf.Clamp01(age/.08f));
            if(age>=FormationDurationSeconds)alpha*=Mathf.Clamp01((DurationSeconds-age)/FadeDurationSeconds);
            _image.color=new Color(1,1,1,alpha);
        }
        internal void Cancel()
        {
            _playing=false;_view=null;_tile=null;_bodyImage=null;_runtimeId=null;
            if(_root!=null)_root.gameObject.SetActive(false);
        }
        private void OnDisable()=>Cancel();
        private void OnDestroy(){Cancel();if(_root!=null){if(UnityEngine.Application.isPlaying)Destroy(_root.gameObject);else DestroyImmediate(_root.gameObject);}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache(){s_frames=null;}
    }
}

