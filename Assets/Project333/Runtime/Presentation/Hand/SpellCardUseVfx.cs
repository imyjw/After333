using UnityEngine;
using UnityEngine.Sprites;

namespace Project333.Runtime.Presentation.Hand
{
    // Captures only artwork/pose. An unsent or rejected command never starts this copy.
    public sealed class SpellCardUseVfx : MonoBehaviour
    {
        public const float Duration = .145f;
        private SpellCardUseGraphic _pieces, _sparkles;
        private float _started;
        public bool IsPlaying { get; private set; }

        public static SpellCardUseVfx Capture(UnityEngine.UI.Image source)
        {
            if (!UnityEngine.Application.isPlaying || source == null || !source.enabled ||
                !source.gameObject.activeInHierarchy || source.overrideSprite == null || source.canvas == null) return null;
            var canvas = source.canvas;
            var parent = canvas.rootCanvas.transform;
            var bounds = HandCardStatOverlayLayout.CalculateRenderedSpriteRect(source);
            var a = parent.InverseTransformPoint(source.rectTransform.TransformPoint(bounds.min));
            var b = parent.InverseTransformPoint(source.rectTransform.TransformPoint(new Vector2(bounds.xMax, bounds.yMin)));
            var c = parent.InverseTransformPoint(source.rectTransform.TransformPoint(new Vector2(bounds.xMin, bounds.yMax)));
            if (Vector3.Distance(a, b) < 1f || Vector3.Distance(a, c) < 1f) return null;
            var node = new GameObject("SpellCardUseShards", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            node.SetActive(false);
            var rect = node.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.localPosition = (b + c) * .5f;
            rect.sizeDelta = new Vector2(Vector3.Distance(a, b), Vector3.Distance(a, c));
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y-a.y, b.x-a.x) * Mathf.Rad2Deg);
            var layer = node.GetComponent<Canvas>();
            layer.overrideSorting = true;
            layer.sortingLayerID = canvas.sortingLayerID;
            layer.sortingOrder = canvas.sortingOrder + 1;
            var group = node.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            var view = node.AddComponent<SpellCardUseVfx>();
            view._pieces = view.CreateGraphic("BlueCardFragments", source.overrideSprite, source.color, false);
            view._sparkles = view.CreateGraphic("BlueSparkles", null, Color.white, true);
            return view;
        }

        private SpellCardUseGraphic CreateGraphic(string label, Sprite sprite, Color tint, bool sparkles)
        {
            var node = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer));
            var rect = node.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var graphic = node.AddComponent<SpellCardUseGraphic>();
            graphic.raycastTarget = false;
            graphic.Configure(sprite, tint, sparkles);
            return graphic;
        }

        public void Hold()
        {
            _pieces.SetProgress(0f);
            _sparkles.SetProgress(0f);
            gameObject.SetActive(true);
        }

        public void Begin()
        {
            if (IsPlaying) return;
            IsPlaying = true;
            _started = Time.unscaledTime;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!IsPlaying) return;
            var progress = Mathf.Clamp01((Time.unscaledTime - _started) / Duration);
            if (progress >= 1f) { Destroy(gameObject); return; }
            _pieces.SetProgress(progress);
            _sparkles.SetProgress(progress);
        }
    }

    // Bounded 16x22 textured pieces and 88 twinkles, with no texture readback or new material.
    public sealed class SpellCardUseGraphic : UnityEngine.UI.MaskableGraphic
    {
        private const int Columns = 16, Rows = 22;
        private Sprite _sprite;
        private bool _sparkles;
        private float _progress;
        public override Texture mainTexture => _sprite != null ? _sprite.texture : Texture2D.whiteTexture;

        public void Configure(Sprite sprite, Color tint, bool sparkles)
        { _sprite = sprite; color = tint; _sparkles = sparkles; SetAllDirty(); }
        public void SetProgress(float progress) { _progress = progress; SetVerticesDirty(); }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            if (_progress >= 1f) return;
            var bounds = rectTransform.rect;
            var uv = _sprite != null ? DataUtility.GetOuterUV(_sprite) : new Vector4(0f, 0f, 1f, 1f);
            for (var y = 0; y < Rows; y++)
            for (var x = 0; x < Columns; x++)
            {
                var id = y * Columns + x;
                var seed = Mathf.Repeat(id * .618034f, 1f);
                var seed2 = Mathf.Repeat(id * .414214f + .17f, 1f);
                var age = Mathf.Clamp01((_progress - .07f - seed * .19f) / .66f);
                var position = new Vector2(bounds.xMin + (x + .5f) * bounds.width / Columns,
                    bounds.yMin + (y + .5f) * bounds.height / Rows);
                var direction = new Vector2((x + .5f) / Columns - .5f + (seed2 - .5f) * .55f,
                    (y + .5f) / Rows - .32f + seed * .3f);
                position += direction * bounds.width * age * (.32f + seed * .30f);
                if (_sparkles)
                {
                    if (age <= 0f || age >= 1f) continue;
                    // Blue overlay keeps fragments luminous even where the source art is black.
                    var shardTint = Color.Lerp(new Color(.10f, .50f, 1f), new Color(.42f, .85f, 1f), seed);
                    shardTint.a = Mathf.Clamp01(age * 7f) * .85f * (1f - Mathf.SmoothStep(.35f, 1f, age));
                    var shardSize = new Vector2(bounds.width / Columns, bounds.height / Rows) * Mathf.Lerp(1f, .12f, age);
                    Piece(mesh, position, shardSize, (seed2 - .5f) * age * 3.2f,
                        new Vector4(0f, 0f, 1f, 1f), shardTint);
                    if (id % 4 != 0) continue;
                    var pulse = Mathf.Pow(Mathf.Abs(Mathf.Sin(age * Mathf.PI * (2f + seed * 3f))), 3f);
                    var size = bounds.width * (.006f + seed * .009f) * (.5f + pulse);
                    var tint = Color.Lerp(new Color(.15f, .56f, 1f), new Color(.78f, .96f, 1f), pulse);
                    tint.a = Mathf.Sin(age * Mathf.PI) * (.4f + pulse * .6f);
                    Piece(mesh, position, new Vector2(size, size), Mathf.PI * .25f,
                        new Vector4(0f, 0f, 1f, 1f), tint);
                    if (pulse > .65f)
                    {
                        Piece(mesh, position, new Vector2(size * .28f, size * 2.7f), 0f,
                            new Vector4(0f, 0f, 1f, 1f), tint);
                        Piece(mesh, position, new Vector2(size * 2.7f, size * .28f), 0f,
                            new Vector4(0f, 0f, 1f, 1f), tint);
                    }
                }
                else
                {
                    if (age >= 1f) continue;
                    var size = new Vector2(bounds.width / Columns, bounds.height / Rows) * Mathf.Lerp(1f, .12f, age);
                    var cellUv = new Vector4(Mathf.Lerp(uv.x, uv.z, (float)x / Columns), Mathf.Lerp(uv.y, uv.w, (float)y / Rows),
                        Mathf.Lerp(uv.x, uv.z, (float)(x+1) / Columns), Mathf.Lerp(uv.y, uv.w, (float)(y+1) / Rows));
                    var tint = Color.Lerp(color, new Color(.25f, .70f, 1f, color.a), Mathf.Clamp01(_progress * 4f));
                    var flash = Mathf.Sin(Mathf.Clamp01(_progress / .20f) * Mathf.PI) * .65f;
                    tint = Color.Lerp(tint, new Color(.82f, .97f, 1f, color.a), flash);
                    tint.a = color.a * (1f - Mathf.SmoothStep(.35f, 1f, age));
                    Piece(mesh, position, size, (seed2 - .5f) * age * 3.2f, cellUv, tint);
                }
            }
        }

        private static void Piece(UnityEngine.UI.VertexHelper mesh, Vector2 center, Vector2 size, float angle, Vector4 uv, Color tint)
        {
            var start = mesh.currentVertCount;
            var right = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size.x * .5f;
            var up = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * size.y * .5f;
            mesh.AddVert(center-right-up, tint, new Vector2(uv.x, uv.y));
            mesh.AddVert(center-right+up, tint, new Vector2(uv.x, uv.w));
            mesh.AddVert(center+right+up, tint, new Vector2(uv.z, uv.w));
            mesh.AddVert(center+right-up, tint, new Vector2(uv.z, uv.y));
            mesh.AddTriangle(start, start+1, start+2);
            mesh.AddTriangle(start, start+2, start+3);
        }
    }
}
