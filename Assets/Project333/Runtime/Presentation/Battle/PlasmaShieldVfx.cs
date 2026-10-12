using System;
using System.Collections.Generic;
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
        public const float FramesPerSecond = 12f;
        public const float FormationDurationSeconds = FrameCount / FramesPerSecond;
        public const float FadeDurationSeconds = .15f;
        public const float DurationSeconds = FormationDurationSeconds + FadeDurationSeconds;
        private static Sprite[] s_frames;
        private static Rect s_effectBounds;
        private static Vector2[] s_groundAnchors;
        private static Dictionary<string, Rect> s_manifest;
        private static readonly Dictionary<Sprite, Dictionary<string, Rect>> s_bounds =
            new Dictionary<Sprite, Dictionary<string, Rect>>();
        private readonly Vector3[] _corners = new Vector3[4];
        private TileTextView _view;
        private TileView _tile;
        private Image _bodyImage, _image;
        private RectTransform _root;
        private string _runtimeId;
        private float _startedAt;
        private bool _playing;
        private int _imageFrame;
        internal bool IsPlaying => _playing;
        internal static Rect EffectBounds => s_effectBounds;
        internal static IReadOnlyList<Vector2> GroundAnchors => s_groundAnchors;
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
            if (s_frames.Length != FrameCount) return false;
            var pixels = s_frames[0].texture.isReadable ? s_frames[0].texture.GetPixels32() : null;
            s_groundAnchors = new Vector2[FrameCount];
            s_effectBounds = UnionBounds(s_frames,pixels);
            return true;
        }
        private static Rect UnionBounds(Sprite[] frames, Color32[] pixels)
        {
            var result = Rect.zero;
            for (var index = 0; index < frames.Length; index++)
            {
                var frame = frames[index];
                var raw = ReadBounds(frame,pixels);
                s_groundAnchors[index] = GroundAnchor(frame,raw,pixels);
                var next = new Rect(raw.min-s_groundAnchors[index],raw.size);
                if (next.width <= 0 || next.height <= 0) continue;
                result = result.width <= 0 ? next : Rect.MinMaxRect(Mathf.Min(result.xMin,next.xMin),
                    Mathf.Min(result.yMin,next.yMin),Mathf.Max(result.xMax,next.xMax),Mathf.Max(result.yMax,next.yMax));
            }
            return result.width > 0 ? result : new Rect(0,0,1,1);
        }

        // Measure the bright lower ground ring once; never recenter a growing dome by its frame bounds.
        private static Vector2 GroundAnchor(Sprite sprite,Rect bounds,Color32[] pixels)
        {
            if (pixels == null || sprite.packed) return new Vector2(bounds.center.x,bounds.yMin+.025f);
            var r=sprite.rect;var width=Mathf.RoundToInt(r.width);var height=Mathf.RoundToInt(r.height);
            var x0=Mathf.RoundToInt(r.x);var y0=Mathf.RoundToInt(r.y);
            var low=Mathf.Clamp(Mathf.FloorToInt(bounds.yMin*height),0,height-1);
            var high=Mathf.Min(height-1,low+40);var row=low;double best=0;
            for(var y=low;y<=high;y++)
            {
                double strength=0;
                for(var x=width/4;x<width*3/4;x++)
                {var p=pixels[(y0+y)*sprite.texture.width+x0+x];if(p.a>=96&&p.g>=120&&p.b>=150)strength+=p.a*p.g;}
                if(strength>best){best=strength;row=y;}
            }
            double sx=0,sy=0,total=0;
            for(var y=Mathf.Max(low,row-6);y<=Mathf.Min(high,row+6);y++)
            for(var x=width/4;x<width*3/4;x++)
            {
                var p=pixels[(y0+y)*sprite.texture.width+x0+x];
                if(p.a<96||p.g<120||p.b<150)continue;
                var weight=(double)p.a*p.g;sx+=(x+.5)*weight;sy+=(y+.5)*weight;total+=weight;
            }
            return total>0 ? new Vector2((float)(sx/total/width),(float)(sy/total/height)) :
                new Vector2(bounds.center.x,bounds.yMin+.025f);
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
                _image.rectTransform.pivot = s_groundAnchors[_imageFrame];
            }
            _root.SetParent(canvas.transform,false); _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;
            var overlay = _root.GetComponent<Canvas>(); overlay.overrideSorting = true; overlay.sortingOrder = 4908;
        }

        private bool UpdateLayout()
        {
            if (!HasCurrentVisibleTarget() || _root == null || _root.parent == null ||
                !_root.parent.gameObject.activeInHierarchy || _root.rect.width <= 0 || _root.rect.height <= 0) return false;
            _bodyImage = _view.OccupantVisualImage;
            var opaque = BodyBounds(_bodyImage.sprite,_view.VisualControllerName,_tile.CurrentOccupantCardId);
            if (opaque.width <= 0 || opaque.height <= 0) return false;
            var draw = _bodyImage.GetPixelAdjustedRect();
            if (_bodyImage.preserveAspect)
            {
                var spriteSize = _bodyImage.sprite.rect.size;
                var scale = Mathf.Min(draw.width / spriteSize.x,draw.height / spriteSize.y);
                var size = spriteSize * scale; draw = new Rect(draw.center-size*.5f,size);
            }
            var localMin = draw.min + Vector2.Scale(opaque.min,draw.size);
            var localMax = draw.min + Vector2.Scale(opaque.max,draw.size);
            _corners[0] = _bodyImage.rectTransform.TransformPoint(new Vector3(localMin.x,localMin.y));
            _corners[1] = _bodyImage.rectTransform.TransformPoint(new Vector3(localMin.x,localMax.y));
            _corners[2] = _bodyImage.rectTransform.TransformPoint(new Vector3(localMax.x,localMax.y));
            _corners[3] = _bodyImage.rectTransform.TransformPoint(new Vector3(localMax.x,localMin.y));
            var minimum = new Vector2(float.PositiveInfinity,float.PositiveInfinity);
            var maximum = new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            foreach(var corner in _corners)
            {
                var p=(Vector2)_root.InverseTransformPoint(corner); minimum=Vector2.Min(minimum,p);maximum=Vector2.Max(maximum,p);
            }
            var body = Rect.MinMaxRect(minimum.x,minimum.y,maximum.x,maximum.y);
            var visibleSize = new Vector2(Mathf.Max(body.width*1.16f,body.height*.55f),body.height*1.12f);
            _image.rectTransform.pivot = s_groundAnchors[_imageFrame];
            _image.rectTransform.anchoredPosition = new Vector2(body.center.x,body.yMin);
            _image.rectTransform.sizeDelta = new Vector2(visibleSize.x / s_effectBounds.width,visibleSize.y / Mathf.Max(.001f,s_effectBounds.yMax));
            _image.rectTransform.localScale = Vector3.one; _image.rectTransform.localRotation = Quaternion.identity;
            return true;
        }

        private static Rect BodyBounds(Sprite sprite,string controllerName,string cardId)
        {
            var key = string.IsNullOrEmpty(controllerName) ? cardId ?? string.Empty : controllerName;
            if (!s_bounds.TryGetValue(sprite,out var byKey)) { byKey = new Dictionary<string,Rect>(StringComparer.Ordinal);s_bounds.Add(sprite,byKey); }
            if (byKey.TryGetValue(key,out var cached)) return cached;
            if (s_manifest == null)
            {
                s_manifest = new Dictionary<string,Rect>(StringComparer.Ordinal);
                var asset = Resources.Load<StatusSpriteBoundsAsset>(ActionDebuffStatusVfx.BoundsResourcePath);
                if (asset != null) foreach(var entry in asset.Entries)
                    s_manifest[StatusSpriteBoundsAsset.CreateKey(entry.VisualKey,entry.TextureName,entry.SpriteName,entry.SpriteRect)] = entry.NormalizedBounds;
            }
            var lookup = StatusSpriteBoundsAsset.CreateKey(key,sprite.texture.name,sprite.name,sprite.rect);
            if (!s_manifest.TryGetValue(lookup,out var result))
            {
                lookup = StatusSpriteBoundsAsset.CreateKey(cardId,sprite.texture.name,sprite.name,sprite.rect);
                if (!s_manifest.TryGetValue(lookup,out result)) result = ReadBounds(sprite,null);
            }
            byKey.Add(key,result);return result;
        }

        private static Rect ReadBounds(Sprite sprite,Color32[] sharedPixels)
        {
            if (!sprite.texture.isReadable || sprite.packed)
            {
                var bounds=sprite.bounds;var rect=sprite.rect;
                var min=(Vector2)bounds.min*sprite.pixelsPerUnit+sprite.pivot;
                var max=(Vector2)bounds.max*sprite.pixelsPerUnit+sprite.pivot;
                return Rect.MinMaxRect(Mathf.Clamp01(min.x/rect.width),Mathf.Clamp01(min.y/rect.height),
                    Mathf.Clamp01(max.x/rect.width),Mathf.Clamp01(max.y/rect.height));
            }
            var pixels=sharedPixels??sprite.texture.GetPixels32();var r=sprite.rect;
            var width=Mathf.RoundToInt(r.width);var height=Mathf.RoundToInt(r.height);var x0=Mathf.RoundToInt(r.x);var y0=Mathf.RoundToInt(r.y);
            var minX=width;var minY=height;var maxX=-1;var maxY=-1;
            for(var y=0;y<height;y++)for(var x=0;x<width;x++)
            {
                var index=(y0+y)*sprite.texture.width+x0+x;
                if(index<0||index>=pixels.Length||pixels[index].a<16)continue;
                minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);
            }
            return maxX<minX ? Rect.zero : Rect.MinMaxRect((float)minX/width,(float)minY/height,(float)(maxX+1)/width,(float)(maxY+1)/height);
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
        private static void ResetCache(){s_frames=null;s_effectBounds=Rect.zero;s_groundAnchors=null;s_manifest=null;s_bounds.Clear();}
    }
}

