using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    // The original PNG is intact. A mesh covers only its LCD and maps LED digits into that same quad.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BombTimerGraphic : MaskableGraphic
    {
        private static readonly int[] DigitMasks = { 0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f };
        private int _before, _after;
        private float _blend;
        internal int CurrentNumber => _blend < .5f ? _before : _after;
        internal void SetCountdown(int before, int after, float blend) { _before = before; _after = after; SetBlend(blend); }
        internal void SetBlend(float blend) { _blend = blend; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Quad(mesh, new Rect(0, 0, 1, 1), new Color(.085f, .008f, .012f, 1));
            DrawTimer(mesh, _before, 1 - _blend); DrawTimer(mesh, _after, _blend);
        }
        private void DrawTimer(VertexHelper mesh, int count, float alpha)
        {
            if (alpha <= 0) return;
            var c = new Color(1, .045f, .025f, alpha);
            var slot = 0;
            for (var i = 0; i < 8; i++)
            {
                var x = .06f + slot * .143f;
                if (i == 2 || i == 5)
                {
                    if (i == 5) x += .03f;
                    Quad(mesh, new Rect(x + .004f, .36f, .014f, .04f), c);
                    Quad(mesh, new Rect(x + .004f, .58f, .014f, .04f), c);
                    continue;
                }
                var digit = i < 6 ? 0 : i == 6 ? Mathf.Clamp(count / 10, 0, 9) : Mathf.Clamp(count % 10, 0, 9);
                var mask = DigitMasks[digit];
                // Colon positions use an extra quarter-slot between digit pairs.
                var shift = i > 2 ? .03f : 0; if (i > 5) shift += .03f;
                x += shift;
                if ((mask & 1) != 0) Quad(mesh, new Rect(x + .016f, .76f, .078f, .045f), c);
                if ((mask & 2) != 0) Quad(mesh, new Rect(x + .090f, .50f, .018f, .25f), c);
                if ((mask & 4) != 0) Quad(mesh, new Rect(x + .090f, .22f, .018f, .25f), c);
                if ((mask & 8) != 0) Quad(mesh, new Rect(x + .016f, .17f, .078f, .045f), c);
                if ((mask & 16) != 0) Quad(mesh, new Rect(x, .22f, .018f, .25f), c);
                if ((mask & 32) != 0) Quad(mesh, new Rect(x, .50f, .018f, .25f), c);
                if ((mask & 64) != 0) Quad(mesh, new Rect(x + .016f, .465f, .078f, .045f), c);
                slot++;
            }
        }
        private void Quad(VertexHelper mesh, Rect region, Color color)
        {
            var start = mesh.currentVertCount;
            mesh.AddVert(Map(region.xMin, region.yMin), color, Vector2.zero);
            mesh.AddVert(Map(region.xMin, region.yMax), color, Vector2.zero);
            mesh.AddVert(Map(region.xMax, region.yMax), color, Vector2.zero);
            mesh.AddVert(Map(region.xMax, region.yMin), color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
        private Vector2 Map(float u, float v)
        {
            var bottom = Vector2.Lerp(Packed(348, 287), Packed(637, 271), u);
            var top = Vector2.Lerp(Packed(337, 140), Packed(626, 132), u);
            var point = Vector2.Lerp(bottom, top, v);
            return rectTransform.rect.min + Vector2.Scale(point, rectTransform.rect.size);
        }
        private static Vector2 Packed(float x, float y) => new Vector2((16 + x * 480 / 713) / 512, 1 - (130 + y * 252 / 374) / 512);
    }
}
