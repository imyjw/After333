using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class FirewallVfx : MonoBehaviour
    {
        public const string FramesResourcePath = "Project333/SpellEffects/Firewall/FlameWall";
        public const int FrameCount = 16;
        public const float FramesPerSecond = 12f;
        public const float DurationSeconds = FrameCount / FramesPerSecond;
        internal static readonly Vector2 OriginPoint = new Vector2(.5f,16f/512f);
        private readonly TileView[] _tiles = new TileView[BoardState.ColumnCount];
        private readonly RectTransform[] _anchors = new RectTransform[BoardState.ColumnCount];
        private readonly RectTransform[] _otherAnchors = new RectTransform[BoardState.ColumnCount];
        private readonly Vector2[] _origins = new Vector2[BoardState.ColumnCount];
        private readonly Image[] _images = new Image[BoardState.ColumnCount];
        private readonly Vector3[] _corners = new Vector3[4];
        private Sprite[] _frames;
        private RectTransform _root;
        private Camera _viewCamera;
        private PlayerId _owner;
        private int _row;
        private float _startedAt;
        private bool _playing;

        internal float Play(BoardPresenter board,PlayerId targetOwnerId,int targetRow)
        {
            Cancel();
            if(!isActiveAndEnabled||board==null||targetRow<0||targetRow>=BoardState.RowCount||
                (targetOwnerId!=PlayerId.Player&&targetOwnerId!=PlayerId.AI)) return 0;
            var tiles=targetOwnerId==PlayerId.Player?board.PlayerTileViews:board.AITileViews;
            if(tiles==null) return 0;
            Canvas canvas=null;
            for(var column=0;column<BoardState.ColumnCount;column++)
            {
                TileView selected=null,other=null;
                foreach(var tile in tiles)
                {
                    if(tile==null||tile.OwnerId!=targetOwnerId||tile.Coord.Column!=column) continue;
                    if(tile.Coord.Row==targetRow) selected=tile; else other=tile;
                }
                if(selected==null||other==null) return 0;
                _tiles[column]=selected;
                var view=selected.GetComponent<TileTextView>();
                _anchors[column]=view?.ActiveEffectButtonAnchor??selected.transform as RectTransform;
                _otherAnchors[column]=other.GetComponent<TileTextView>()?.ActiveEffectButtonAnchor??other.transform as RectTransform;
                // Each native battle tile has its own Canvas. Convert every ground anchor
                // into the first background Canvas's viewport, not the unit sorting Canvas.
                var tileCanvas=_anchors[column]?.GetComponentInParent<Canvas>(true)
                    ??view?.GetVisualRootCanvas()??selected.GetComponentInParent<Canvas>();
                if(_anchors[column]==null||_otherAnchors[column]==null||tileCanvas==null) return 0;
                if(canvas==null) canvas=tileCanvas.rootCanvas;
            }
            if(canvas==null||!canvas.isActiveAndEnabled) return 0;
            if(_frames==null||_frames.Length==0)
            {
                _frames=Resources.LoadAll<Sprite>(FramesResourcePath);
                Array.Sort(_frames,(a,b)=>string.CompareOrdinal(a.name,b.name));
            }
            if(_frames.Length!=FrameCount) return 0;
            _owner=targetOwnerId;_row=targetRow;
            _viewCamera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            EnsureOverlay(canvas);
            if(!UpdateLayout()) { Cancel();return 0; }
            foreach(var image in _images) { image.sprite=_frames[0];image.color=Color.white; }
            _root.gameObject.SetActive(true);
            // Initial activation restores the parent order; set sorting afterwards so
            // tiles belonging to the other root Canvases cannot cover any flame.
            var overlay=_root.GetComponent<Canvas>();overlay.overrideSorting=true;overlay.sortingOrder=4907;
            _startedAt=Time.unscaledTime;_playing=true;
            return DurationSeconds;
        }

        private void EnsureOverlay(Canvas canvas)
        {
            if(_root==null)
            {
                var root=new GameObject("FirewallOverlay",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));
                root.SetActive(false);_root=(RectTransform)root.transform;
                var group=root.GetComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=false;
                for(var column=0;column<_images.Length;column++)
                {
                    var image=new GameObject("FirewallFlameWall_"+column,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
                    image.rectTransform.SetParent(_root,false);image.raycastTarget=false;
                    image.rectTransform.anchorMin=image.rectTransform.anchorMax=Vector2.one*.5f;
                    image.rectTransform.pivot=OriginPoint;
                    _images[column]=image;
                }
            }
            _root.SetParent(canvas.transform,false);_root.anchorMin=Vector2.zero;_root.anchorMax=Vector2.one;
            _root.offsetMin=_root.offsetMax=Vector2.zero;
        }

        private bool UpdateLayout()
        {
            if(_root==null||_root.parent==null||!_root.parent.gameObject.activeInHierarchy) return false;
            var viewRotation=_viewCamera!=null?_viewCamera.transform.rotation:Quaternion.identity;
            var right=((Vector2)_root.InverseTransformVector(viewRotation*Vector3.right)).normalized;
            var up=((Vector2)_root.InverseTransformVector(viewRotation*Vector3.up)).normalized;
            var size=float.PositiveInfinity;
            for(var i=0;i<_anchors.Length;i++)
            {
                if(_tiles[i]==null||_tiles[i].OwnerId!=_owner||_tiles[i].Coord.Row!=_row||_tiles[i].Coord.Column!=i||
                    _anchors[i]==null||!_anchors[i].gameObject.activeInHierarchy||_otherAnchors[i]==null||!_otherAnchors[i].gameObject.activeInHierarchy) return false;
                var center=(Vector2)_root.InverseTransformPoint(_anchors[i].TransformPoint(_anchors[i].rect.center));
                _anchors[i].GetWorldCorners(_corners);
                var xMin=float.PositiveInfinity;var xMax=float.NegativeInfinity;
                var yMin=float.PositiveInfinity;var yMax=float.NegativeInfinity;
                foreach(var corner in _corners)
                {
                    var point=(Vector2)_root.InverseTransformPoint(corner)-center;
                    var x=Vector2.Dot(point,right);var y=Vector2.Dot(point,up);
                    xMin=Mathf.Min(xMin,x);xMax=Mathf.Max(xMax,x);
                    yMin=Mathf.Min(yMin,y);yMax=Mathf.Max(yMax,y);
                }
                var tileWidth=xMax-xMin;var tileHeight=yMax-yMin;
                if(tileWidth<=0||tileHeight<=0) return false;
                // A 512px square cell contains a narrow upright flame. Uniform scaling
                // keeps its original proportions; each ground origin belongs to one tile.
                size=Mathf.Min(size,Mathf.Min(tileWidth,tileHeight)*1.35f);
                _origins[i]=center+up*(yMin+tileHeight*.05f);
            }
            var bounds=_root.rect;var margin=Mathf.Min(bounds.width,bounds.height)*.01f;
            bounds.xMin+=margin;bounds.xMax-=margin;bounds.yMin+=margin;bounds.yMax-=margin;
            foreach(var origin in _origins)
            {
                if(!bounds.Contains(origin)) return false;
                for(var xSide=-1;xSide<=1;xSide+=2)
                for(var ySide=0;ySide<2;ySide++)
                {
                    var direction=right*(xSide*.5f)+up*(ySide-OriginPoint.y);
                    size=CapSize(size,origin.x,direction.x,bounds.xMin,bounds.xMax);
                    size=CapSize(size,origin.y,direction.y,bounds.yMin,bounds.yMax);
                }
            }
            if(size<=0||float.IsInfinity(size)) return false;
            var rotation=Quaternion.Inverse(_root.rotation)*viewRotation;
            for(var i=0;i<_images.Length;i++)
            {
                var rect=_images[i].rectTransform;
                rect.localPosition=_origins[i];rect.localRotation=rotation;
                rect.localScale=Vector3.one;rect.sizeDelta=Vector2.one*size;
                // Lower ground lines render in front on both mirrored player fields.
                var order=0;
                for(var j=0;j<_origins.Length;j++)
                    if(Vector2.Dot(_origins[j]-_origins[i],up)>0) order++;
                if(rect.GetSiblingIndex()!=order) rect.SetSiblingIndex(order);
            }
            return true;
        }

        private static float CapSize(float size,float origin,float direction,float minimum,float maximum)
        {
            if(direction>.00001f) return Mathf.Min(size,(maximum-origin)/direction);
            if(direction<-.00001f) return Mathf.Min(size,(origin-minimum)/-direction);
            return size;
        }

        private void LateUpdate()
        {
            if(!_playing) return;
            var elapsed=Mathf.Max(0,Time.unscaledTime-_startedAt);
            if(elapsed>=DurationSeconds||!UpdateLayout()) { Cancel();return; }
            var frame=_frames[Mathf.Min(FrameCount-1,Mathf.FloorToInt(elapsed*FramesPerSecond))];
            var color=new Color(1,1,1,Mathf.Clamp01((DurationSeconds-elapsed)/.25f));
            foreach(var image in _images) { image.sprite=frame;image.color=color; }
        }

        internal void Cancel()
        {
            _playing=false;
            if(_root!=null) _root.gameObject.SetActive(false);
        }
        private void OnDisable()=>Cancel();
        private void OnDestroy()
        {
            Cancel();if(_root==null) return;
            if(UnityEngine.Application.isPlaying) Destroy(_root.gameObject);else DestroyImmediate(_root.gameObject);
        }
    }
}
