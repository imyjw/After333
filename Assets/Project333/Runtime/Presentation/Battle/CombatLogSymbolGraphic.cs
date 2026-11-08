using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    // Authored icons for combat/damage/death/direction/heal with vector fallback when Resources sprites are unavailable.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CombatLogSymbolGraphic : MaskableGraphic
    {
        public enum Symbol { Swords, Damage, Skull, RightTriangle, Heal }
        public Symbol Kind;
        private static Sprite s_swordsSprite, s_damageSprite, s_skullSprite, s_rightTriangleSprite, s_healSprite;

        private Sprite IconSprite
        {
            get
            {
                if (Kind == Symbol.Swords)
                {
                    if (s_swordsSprite == null)
                        s_swordsSprite = Resources.Load<Sprite>("Project333/UI/CombatLog/CrossedSwords");
                    return s_swordsSprite;
                }
                if (Kind == Symbol.Damage)
                {
                    if (s_damageSprite == null)
                        s_damageSprite = Resources.Load<Sprite>("Project333/UI/CombatLog/DamageBurst");
                    return s_damageSprite;
                }
                if (Kind == Symbol.Skull)
                {
                    if (s_skullSprite == null)
                        s_skullSprite = Resources.Load<Sprite>("Project333/UI/CombatLog/Skull");
                    return s_skullSprite;
                }
                if (Kind == Symbol.RightTriangle)
                {
                    if (s_rightTriangleSprite == null)
                        s_rightTriangleSprite = Resources.Load<Sprite>("Project333/UI/CombatLog/RightTriangle");
                    return s_rightTriangleSprite;
                }
                if (Kind == Symbol.Heal)
                {
                    if (s_healSprite == null)
                        s_healSprite = Resources.Load<Sprite>("Project333/UI/CombatLog/HealIcon");
                    return s_healSprite;
                }
                return null;
            }
        }

        public override Texture mainTexture
        {
            get
            {
                var icon = IconSprite;
                return icon != null ? icon.texture : base.mainTexture;
            }
        }

        protected override void Awake() { base.Awake(); raycastTarget = false; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var icon = IconSprite;
            if (icon != null)
            {
                DrawIcon(vh, icon);
                return;
            }
            if (Kind == Symbol.Damage)
            {
                Burst(vh, .50f, .30f, new Color32(77, 43, 13, 255));
                Burst(vh, .46f, .27f, new Color32(255, 188, 46, 255));
                Burst(vh, .37f, .24f, new Color32(255, 218, 100, 255));
            }
            else if (Kind == Symbol.Swords)
            {
                Sword(vh, -42, new Color32(84, 164, 216, 80), 1.12f);
                Sword(vh, 42, new Color32(84, 164, 216, 80), 1.12f);
                Sword(vh, -42, Color.white, 1);
                Sword(vh, 42, Color.white, 1);
            }
            else if (Kind == Symbol.RightTriangle)
            {
                Poly(vh, new Color32(86, 240, 111, 255), new Vector2(-.32f, -.38f), new Vector2(-.32f, .38f), new Vector2(.38f, 0f));
            }
            else if (Kind == Symbol.Heal)
            {
                Rect(vh, Vector2.zero, new Vector2(.22f, .64f), new Color32(86, 240, 111, 255), 0);
                Rect(vh, Vector2.zero, new Vector2(.64f, .22f), new Color32(86, 240, 111, 255), 0);
            }
            else
            {
                Skull(vh, 1.06f, new Color32(39, 37, 38, 255));
                Skull(vh, 1, new Color32(246, 235, 205, 255));
                Poly(vh, new Color32(54, 49, 46, 255), new Vector2(-.28f,.08f),new Vector2(-.08f,.01f),new Vector2(-.1f,-.12f),new Vector2(-.26f,-.1f));
                Poly(vh, new Color32(54, 49, 46, 255), new Vector2(.28f,.08f),new Vector2(.08f,.01f),new Vector2(.1f,-.12f),new Vector2(.26f,-.1f));
                Poly(vh, new Color32(54, 49, 46, 255),new Vector2(0,-.1f),new Vector2(-.07f,-.22f),new Vector2(.07f,-.22f));
                for(var i=-1;i<=1;i++) Rect(vh,new Vector2(i*.11f,-.34f),new Vector2(.018f,.09f),new Color32(123,112,94,255),0);
            }
        }
        private void DrawIcon(VertexHelper vh, Sprite icon)
        {
            var rect = rectTransform.rect;
            var aspect = icon.rect.width / icon.rect.height;
            var size = rect.size;
            if (size.x > size.y * aspect) size.x = size.y * aspect;
            else size.y = size.x / aspect;
            var half = size * .5f;
            var center = rect.center;
            var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(icon);
            vh.AddVert(center + new Vector2(-half.x, -half.y), color, new Vector2(uv.x, uv.y));
            vh.AddVert(center + new Vector2(-half.x, half.y), color, new Vector2(uv.x, uv.w));
            vh.AddVert(center + new Vector2(half.x, half.y), color, new Vector2(uv.z, uv.w));
            vh.AddVert(center + new Vector2(half.x, -half.y), color, new Vector2(uv.z, uv.y));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        private void Skull(VertexHelper vh,float s,Color c) => Poly(vh,c,
            new Vector2(-.32f,.3f)*s,new Vector2(-.2f,.4f)*s,new Vector2(.2f,.4f)*s,new Vector2(.32f,.3f)*s,
            new Vector2(.36f,.05f)*s,new Vector2(.31f,-.18f)*s,new Vector2(.19f,-.23f)*s,new Vector2(.18f,-.4f)*s,
            new Vector2(-.18f,-.4f)*s,new Vector2(-.19f,-.23f)*s,new Vector2(-.31f,-.18f)*s,new Vector2(-.36f,.05f)*s);
        private void Burst(VertexHelper vh,float outer,float inner,Color c)
        {
            var points=new Vector2[24];for(var i=0;i<points.Length;i++){var angle=(i*15f+7)*Mathf.Deg2Rad;points[i]=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(i%2==0?outer:inner);}Poly(vh,c,points);
        }
        private void Sword(VertexHelper vh,float angle,Color tint,float scale)
        {
            var rot=Quaternion.Euler(0,0,angle);
            var blade=new[]{new Vector2(-.07f,-.14f),new Vector2(-.07f,.33f),new Vector2(0,.46f),new Vector2(.07f,.33f),new Vector2(.07f,-.14f)};
            for(var i=0;i<blade.Length;i++)blade[i]=rot*(blade[i]*scale);
            Poly(vh,new Color(.12f,.16f,.19f,tint.a),blade);
            Rect(vh,(Vector2)(rot*new Vector2(-.018f,.1f)),new Vector2(.06f,.43f)*scale,new Color(.83f,.91f,.96f,tint.a),angle);
            Rect(vh,(Vector2)(rot*new Vector2(.027f,.1f)),new Vector2(.035f,.43f)*scale,new Color(.49f,.64f,.73f,tint.a),angle);
            Rect(vh,(Vector2)(rot*new Vector2(0,-.17f)),new Vector2(.27f,.06f)*scale,new Color(.88f,.65f,.26f,tint.a),angle);
            Rect(vh,(Vector2)(rot*new Vector2(0,-.3f)),new Vector2(.075f,.22f)*scale,new Color(.32f,.21f,.12f,tint.a),angle);
            Rect(vh,(Vector2)(rot*new Vector2(0,-.42f)),new Vector2(.11f,.06f)*scale,new Color(.88f,.65f,.26f,tint.a),angle);
        }
        private void Rect(VertexHelper vh,Vector2 center,Vector2 size,Color c,float rotation)
        {
            var q=Quaternion.Euler(0,0,rotation);var half=size*.5f;
            Poly(vh,c,center+(Vector2)(q*new Vector2(-half.x,-half.y)),center+(Vector2)(q*new Vector2(-half.x,half.y)),center+(Vector2)(q*new Vector2(half.x,half.y)),center+(Vector2)(q*new Vector2(half.x,-half.y)));
        }
        private void Poly(VertexHelper vh,Color c,params Vector2[] points)
        {
            var index=vh.currentVertCount;var rect=rectTransform.rect;var center=Vector2.zero;
            foreach(var point in points)center+=point;center/=points.Length;
            vh.AddVert(rect.center+Vector2.Scale(center,rect.size),c,Vector2.zero);
            foreach(var point in points)vh.AddVert(rect.center+Vector2.Scale(point,rect.size),c,Vector2.zero);
            for(var i=0;i<points.Length;i++)vh.AddTriangle(index,index+1+i,index+1+(i+1)%points.Length);
        }
    }
}
