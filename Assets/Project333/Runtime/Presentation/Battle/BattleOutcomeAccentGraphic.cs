using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    /// <summary>A small, bounded UI mesh: golden rays/sparkles or settling dust.</summary>
    public sealed class BattleOutcomeAccentGraphic : MaskableGraphic
    {
        private bool _victory;
        private float _elapsed;

        public void Animate(bool victory, float elapsed)
        {
            _victory = victory;
            _elapsed = elapsed;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var height = rectTransform.rect.height;
            var fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_elapsed / .55f));
            var tint = _victory ? new Color(1f, .73f, .28f) : new Color(.66f, .56f, .40f);
            if (_victory)
            {
                for (var i = 0; i < 18; i++)
                {
                    var angle = i * Mathf.PI * 2f / 18f + _elapsed * .035f;
                    var radius = height * (.68f + .035f * Mathf.Sin(_elapsed * 1.8f + i));
                    AddTriangle(mesh, Vector2.zero,
                        Direction(angle - .035f) * radius, Direction(angle + .035f) * radius,
                        WithAlpha(tint, .16f * fade), WithAlpha(tint, 0f));
                }
            }
            for (var i = 0; i < 24; i++)
            {
                var phase = Mathf.Repeat(_elapsed * (_victory ? .27f : .4f) + i * .618034f, 1f);
                var x = Mathf.Sin(i * 18.37f) * rectTransform.rect.width * .43f;
                var y = (_victory ? phase - .4f : .6f - phase) * height * .82f;
                var size = (_victory ? 2.2f : 1.5f) + (i % 4);
                var alpha = Mathf.Sin(phase * Mathf.PI) * fade * (_victory ? .85f : .33f);
                var center = new Vector2(x, y);
                var color = WithAlpha(tint, alpha);
                AddTriangle(mesh, center + Vector2.up * size * 2f,
                    center + Vector2.left * size, center + Vector2.down * size * 2f, color, color);
                AddTriangle(mesh, center + Vector2.up * size * 2f,
                    center + Vector2.down * size * 2f, center + Vector2.right * size, color, color);
            }
        }

        private static Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        private static Color WithAlpha(Color value, float alpha) => new Color(value.r, value.g, value.b, alpha);

        private static void AddTriangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color center, Color edge)
        {
            var index = mesh.currentVertCount;
            mesh.AddVert(a, center, Vector2.zero);
            mesh.AddVert(b, edge, Vector2.zero);
            mesh.AddVert(c, edge, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
        }
    }
}
