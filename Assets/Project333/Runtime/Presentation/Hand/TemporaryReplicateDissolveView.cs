using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Hand
{
    // Independent visual snapshot. Slot recycling and hand layout never move this copy.
    public sealed class TemporaryReplicateDissolveView : MonoBehaviour
    {
        public const float Duration = .65f;
        private TemporaryReplicateDissolveGraphic _card, _dust;
        private float _started;

        public static TemporaryReplicateDissolveView Play(Image source)
        {
            if (!UnityEngine.Application.isPlaying || source == null || !source.enabled ||
                !source.gameObject.activeInHierarchy || source.overrideSprite == null || source.canvas == null) return null;
            var canvas = source.canvas;
            var parent = canvas.rootCanvas.transform;
            var bounds = HandCardStatOverlayLayout.CalculateRenderedSpriteRect(source);
            var a = parent.InverseTransformPoint(source.rectTransform.TransformPoint(bounds.min));
            var b = parent.InverseTransformPoint(source.rectTransform.TransformPoint(new Vector2(bounds.xMax,bounds.yMin)));
            var c = parent.InverseTransformPoint(source.rectTransform.TransformPoint(new Vector2(bounds.xMin,bounds.yMax)));
            if (Vector3.Distance(a,b) < 1f || Vector3.Distance(a,c) < 1f) return null;
            var node = new GameObject("TemporaryReplicateExpiry", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var rect = node.GetComponent<RectTransform>();
            rect.SetParent(parent,false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
            rect.pivot = new Vector2(.5f,.5f);
            rect.localPosition = (b+c)*.5f;
            rect.sizeDelta = new Vector2(Vector3.Distance(a,b),Vector3.Distance(a,c));
            rect.localRotation = Quaternion.Euler(0f,0f,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
            var layer = node.GetComponent<Canvas>();
            layer.overrideSorting = true; layer.sortingLayerID = canvas.sortingLayerID;
            layer.sortingOrder = canvas.sortingOrder+1;
            var group = node.GetComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
            var view = node.AddComponent<TemporaryReplicateDissolveView>();
            view._card = view.CreateGraphic("DissolvingCard", source.overrideSprite, source.color, false);
            view._dust = view.CreateGraphic("GreyDust", null, Color.white, true);
            view._started = Time.unscaledTime;
            return view;
        }

        private TemporaryReplicateDissolveGraphic CreateGraphic(string objectName, Sprite sprite, Color tint, bool dust)
        {
            var node = new GameObject(objectName,typeof(RectTransform),typeof(CanvasRenderer));
            var rect = node.GetComponent<RectTransform>(); rect.SetParent(transform,false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var graphic = node.AddComponent<TemporaryReplicateDissolveGraphic>();
            graphic.raycastTarget = false; graphic.Configure(sprite,tint,dust);
            return graphic;
        }

        private void Update()
        {
            var progress = Mathf.Clamp01((Time.unscaledTime-_started)/Duration);
            if (progress >= 1f) { Destroy(gameObject); return; }
            _card.SetProgress(progress); _dust.SetProgress(progress);
        }
    }

    // Fixed 12x18 card cells and at most 72 grey motes per card; no render-texture readback.
    public sealed class TemporaryReplicateDissolveGraphic : MaskableGraphic
    {
        private const int Columns = 12, Rows = 18;
        private Sprite _sprite;
        private bool _dust;
        private float _progress;
        public override Texture mainTexture => _sprite != null ? _sprite.texture : Texture2D.whiteTexture;
        public void Configure(Sprite sprite, Color tint, bool dust)
        { _sprite = sprite; color = tint; _dust = dust; SetAllDirty(); }
        public void SetProgress(float progress) { _progress = progress; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var bounds = rectTransform.rect;
            var uv = _sprite != null ? DataUtility.GetOuterUV(_sprite) : new Vector4(0f,0f,1f,1f);
            for (var y = 0; y < Rows; y++)
            for (var x = 0; x < Columns; x++)
            {
                var id = y*Columns+x;
                var depth = Mathf.Min(Mathf.Min(x+.5f,Columns-x-.5f),Mathf.Min(y+.5f,Rows-y-.5f))/(Columns*.5f);
                var start = depth*.48f;
                if (_dust)
                {
                    if (id%3 != 0) continue;
                    var age = (_progress-start)/.40f;
                    if (age <= 0f || age >= 1f) continue;
                    var seed = Mathf.Repeat(id*.618034f,1f);
                    var pos = new Vector2(bounds.xMin+(x+.5f)/Columns*bounds.width,
                        bounds.yMin+(y+.5f)/Rows*bounds.height);
                    pos += new Vector2((seed-.5f)*bounds.width*.30f*age,
                        bounds.height*(.035f+.12f*seed)*age);
                    var size = bounds.width*(.009f+.011f*seed)*(1f-age*.55f);
                    var grey = .42f+.30f*seed;
                    AddQuad(mesh, new Rect(pos-Vector2.one*size*.5f,Vector2.one*size),
                        new Vector4(0f,0f,1f,1f),new Color(grey,grey,grey,Mathf.Sin(age*Mathf.PI)*.9f));
                }
                else
                {
                    var alpha = 1f-Mathf.SmoothStep(0f,1f,(_progress-start)/.20f);
                    if (alpha <= 0f) continue;
                    var box = new Rect(bounds.xMin+x*bounds.width/Columns,bounds.yMin+y*bounds.height/Rows,
                        bounds.width/Columns,bounds.height/Rows);
                    var cellUv = new Vector4(Mathf.Lerp(uv.x,uv.z,(float)x/Columns),Mathf.Lerp(uv.y,uv.w,(float)y/Rows),
                        Mathf.Lerp(uv.x,uv.z,(float)(x+1)/Columns),Mathf.Lerp(uv.y,uv.w,(float)(y+1)/Rows));
                    var tint = color; tint.a *= alpha;
                    AddQuad(mesh,box,cellUv,tint);
                }
            }
        }

        private static void AddQuad(VertexHelper mesh, Rect box, Vector4 uv, Color tint)
        {
            var offset = mesh.currentVertCount;
            mesh.AddVert(new Vector3(box.xMin,box.yMin),tint,new Vector2(uv.x,uv.y));
            mesh.AddVert(new Vector3(box.xMin,box.yMax),tint,new Vector2(uv.x,uv.w));
            mesh.AddVert(new Vector3(box.xMax,box.yMax),tint,new Vector2(uv.z,uv.w));
            mesh.AddVert(new Vector3(box.xMax,box.yMin),tint,new Vector2(uv.z,uv.y));
            mesh.AddTriangle(offset,offset+1,offset+2); mesh.AddTriangle(offset,offset+2,offset+3);
        }
    }
}
