using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Presentation.Battle;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class StunFreezeVfxAssetTests
    {
        private const string Root = "Assets/Project333/Resources/Project333/StatusEffects/";
        private const string Stars = Root + "Stun/OrbitStars.png";
        private const string Ice = Root + "Freeze/IceCrystal.png";
        private static readonly string[] Shapes = { "Gwangma", "BlueDragon", "A-301", "ManaCannon" };
        private static readonly Dictionary<string, (int width, int height, Color32[] pixels)> DecodedPixels =
            new Dictionary<string, (int width, int height, Color32[] pixels)>();

        [TearDown] public void ReleaseReadOnlyCopies() => DecodedPixels.Clear();

        [Test]
        public void NativeStatusAtlasesHaveStableCompleteCellsAndRuntimeImportSettings()
        {
            AssertAtlas(Stars, "OrbitStars", 8, 2048, 1024);
            AssertAtlas(Ice, "IceCrystal", 1, 512, 512);
            Assert.That(Resources.LoadAll<Sprite>("Project333/StatusEffects/Stun/OrbitStars")
                .Select(s => s.name).OrderBy(n => n), Is.EqualTo(Enumerable.Range(0, 8).Select(i => "OrbitStars_" + i)));
            Assert.That(Resources.LoadAll<Sprite>("Project333/StatusEffects/Freeze/IceCrystal")
                .Select(s => s.name), Is.EqualTo(new[] { "IceCrystal_0" }));
        }

        [Test]
        public void ActualArtKeepsStarsDistinctAndClearAtTheCenterWhileCrystalRemainsTranslucent()
        {
            var signatures = new HashSet<ulong>();
            foreach (var sprite in LoadSprites(Stars))
            {
                var pixels = CellPixels(sprite); var visible = AlphaBounds(pixels);
                Assert.That(visible.xMin, Is.GreaterThanOrEqualTo(16f));
                Assert.That(visible.yMin, Is.GreaterThanOrEqualTo(16f));
                Assert.That(visible.xMax, Is.LessThanOrEqualTo(496f));
                Assert.That(visible.yMax, Is.LessThanOrEqualTo(496f));
                Assert.That(pixels.All(c => c.a == 0 || c.a >= 16), Is.True, "Near-transparent source haze was removed.");
                Assert.That(pixels.Any(c => c.a >= 200), Is.True, "Each star frame retains a readable bright star.");
                for (var y = 224; y < 288; y++)
                for (var x = 224; x < 288; x++)
                    Assert.That(pixels[y * 512 + x].a, Is.Zero, "The central 64px hole remains clear.");
                signatures.Add(Fingerprint(pixels));
            }
            Assert.That(signatures.Count, Is.EqualTo(8), "Eight native frames must contain eight distinct orbit positions.");
            var crystal = CellPixels(LoadSprites(Ice).Single()); var bounds = AlphaBounds(crystal);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(16f)); Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(16f));
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(496f)); Assert.That(bounds.yMax, Is.LessThanOrEqualTo(496f));
            Assert.That(crystal.All(c => c.a == 0 || c.a >= 16), Is.True);
            var center = new List<byte>();
            for (var y = 208; y < 304; y++)
            for (var x = 208; x < 304; x++) center.Add(crystal[y * 512 + x].a);
            Assert.That(center.Average(a => (double)a) / 255d, Is.InRange(.10d, .40d),
                "Ice keeps a translucent interior so the held unit pose remains readable.");
            Assert.That(center.Count(a => a >= 240), Is.LessThan(center.Count * .05f));
        }

        [Test]
        public void BodyBoundsUseOnlyMetadataAndMatchTheCurrentNativeFramesOfDifferentShapes()
        {
            var asset = Resources.Load<StatusSpriteBoundsAsset>("Project333/StatusEffects/BodyBounds");
            Assert.That(asset, Is.Not.Null);
            var path = AssetDatabase.GetAssetPath(asset);
            Assert.That(AssetDatabase.GetDependencies(path, true)
                .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)), Is.Empty,
                "Bounds metadata must not create Resources dependencies on every unit atlas.");
            var fields = typeof(StatusSpriteBoundsAsset.Entry).GetFields();
            Assert.That(fields.Any(f => typeof(Object).IsAssignableFrom(f.FieldType)), Is.False,
                "Entries hold string keys and rects, with no Sprite or Texture references.");
            Assert.That(asset.Entries, Is.Not.Empty);
            foreach (var entry in asset.Entries)
            {
                Assert.That(entry, Is.Not.Null); Assert.That(entry.VisualKey, Is.Not.Null.And.Not.Empty);
                Assert.That(entry.TextureName, Is.Not.Null.And.Not.Empty); Assert.That(entry.SpriteName, Is.Not.Null.And.Not.Empty);
                Assert.That(entry.SpriteRect.width, Is.GreaterThan(0f)); Assert.That(entry.SpriteRect.height, Is.GreaterThan(0f));
                if (entry.NormalizedBounds.width == 0f || entry.NormalizedBounds.height == 0f)
                {
                    Assert.That(entry.NormalizedBounds, Is.EqualTo(Rect.zero), "An empty source frame uses a paired zero rect.");
                    Assert.That(entry.VisualKey.StartsWith("unit:", StringComparison.Ordinal), Is.True,
                        "Only unused original-unit coverage may be empty; actual card/controller aliases require visible alpha.");
                    continue;
                }
                Assert.That(entry.NormalizedBounds.width, Is.GreaterThan(0f)); Assert.That(entry.NormalizedBounds.height, Is.GreaterThan(0f));
                Assert.That(entry.NormalizedBounds.xMin, Is.InRange(0f, 1f)); Assert.That(entry.NormalizedBounds.xMax, Is.InRange(0f, 1f));
                Assert.That(entry.NormalizedBounds.yMin, Is.InRange(0f, 1f)); Assert.That(entry.NormalizedBounds.yMax, Is.InRange(0f, 1f));
            }
            var seen = new HashSet<string>();
            foreach (var card in Shapes)
            {
                var controller = BoardAnimationControllerTestUtility.Controller(card);
                Assert.That(controller, Is.Not.Null);
                foreach (var clip in controller.animationClips)
                foreach (var sprite in BoardAnimationControllerTestUtility.Frames(clip))
                {
                    var identity = card + "|" + sprite.texture.name + "|" + sprite.name + "|" + sprite.rect;
                    if (!seen.Add(identity)) continue;
                    var matches = asset.Entries.Where(e => (e.VisualKey == controller.name || e.VisualKey == card) &&
                        e.TextureName == sprite.texture.name && e.SpriteName == sprite.name && e.SpriteRect == sprite.rect).ToArray();
                    Assert.That(matches.Length, Is.GreaterThan(0), "Missing bounds for actual native " + identity);
                    var actualPixels = CellPixels(sprite, out var sourceWidth, out var sourceHeight);
                    var expected = AlphaBounds(actualPixels, sourceWidth, sourceHeight);
                    expected = new Rect(expected.x / sourceWidth, expected.y / sourceHeight,
                        expected.width / sourceWidth, expected.height / sourceHeight);
                    foreach (var entry in matches)
                    {
                        Assert.That(entry.NormalizedBounds.xMin, Is.EqualTo(expected.xMin).Within(1f / 512f), identity);
                        Assert.That(entry.NormalizedBounds.xMax, Is.EqualTo(expected.xMax).Within(1f / 512f), identity);
                        Assert.That(entry.NormalizedBounds.yMin, Is.EqualTo(expected.yMin).Within(1f / 512f), identity);
                        Assert.That(entry.NormalizedBounds.yMax, Is.EqualTo(expected.yMax).Within(1f / 512f), identity);
                    }
                }
            }
            Assert.That(seen.Count, Is.GreaterThan(20), "Validate several actual poses, not only one generic frame.");
        }

        private static void AssertAtlas(string path, string name, int count, int width, int height)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.isReadable, Is.True); Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture.width, Is.EqualTo(width)); Assert.That(texture.height, Is.EqualTo(height));
            var frames = LoadSprites(path); Assert.That(frames.Length, Is.EqualTo(count));
            for (var i = 0; i < count; i++)
            {
                var sprite = frames[i];
                Assert.That(sprite.name, Is.EqualTo(name + "_" + i));
                Assert.That(sprite.rect, Is.EqualTo(new Rect((i % 4) * 512, count == 1 ? 0 : (1 - i / 4) * 512, 512, 512)));
                Assert.That(sprite.pivot, Is.EqualTo(new Vector2(256, 256)));
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(100f));
                Assert.That(sprite.packed, Is.False); Assert.That(sprite.vertices.Length, Is.EqualTo(4), "Native FullRect geometry.");
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long id), Is.True);
                Assert.That(guid, Is.Not.Empty); Assert.That(id, Is.Not.EqualTo(0));
            }
        }
        private static Sprite[] LoadSprites(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
        private static Color32[] CellPixels(Sprite sprite)
        {
            var pixels = CellPixels(sprite, out var width, out var height);
            Assert.That(new Vector2(width, height), Is.EqualTo(new Vector2(512, 512)), "Status art uses complete 512px cells.");
            return pixels;
        }
        private static Color32[] CellPixels(Sprite sprite, out int cellWidth, out int cellHeight)
        {
            // Decode a read-only copy; protected unit importer readability is left unchanged.
            var path = AssetDatabase.GetAssetPath(sprite);
            if (!DecodedPixels.TryGetValue(path, out var decoded))
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True);
                    decoded = (texture.width, texture.height, texture.GetPixels32()); DecodedPixels.Add(path, decoded);
                }
                finally { Object.DestroyImmediate(texture); }
            }
            var rect = sprite.rect;
            var scaleX = decoded.width / (double)sprite.texture.width;
            var scaleY = decoded.height / (double)sprite.texture.height;
            // Recover original pixel-cell edges from a possibly downscaled imported Sprite.rect.
            var x0 = Math.Max(0, (int)Math.Round(rect.xMin * scaleX));
            var y0 = Math.Max(0, (int)Math.Round(rect.yMin * scaleY));
            var x1 = Math.Min(decoded.width, (int)Math.Round(rect.xMax * scaleX));
            var y1 = Math.Min(decoded.height, (int)Math.Round(rect.yMax * scaleY));
            cellWidth = x1 - x0; cellHeight = y1 - y0;
            Assert.That(cellWidth, Is.GreaterThan(0)); Assert.That(cellHeight, Is.GreaterThan(0));
            var result = new Color32[cellWidth * cellHeight];
            for (var y = 0; y < cellHeight; y++)
                Array.Copy(decoded.pixels, (y0 + y) * decoded.width + x0, result, y * cellWidth, cellWidth);
            return result;
        }
        private static Rect AlphaBounds(Color32[] pixels, int width = 512, int height = 512)
        {
            var minX = width; var minY = height; var maxX = -1; var maxY = -1;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a < 16) continue;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
            }
            Assert.That(maxX, Is.GreaterThanOrEqualTo(minX)); Assert.That(maxY, Is.GreaterThanOrEqualTo(minY));
            return Rect.MinMaxRect(minX, minY, maxX + 1, maxY + 1);
        }
        private static ulong Fingerprint(Color32[] pixels)
        {
            var value = 14695981039346656037UL;
            unchecked
            {
                foreach (var pixel in pixels)
                {
                    value = (value ^ (pixel.a == 0 ? 0U : pixel.r)) * 1099511628211UL;
                    value = (value ^ (pixel.a == 0 ? 0U : pixel.g)) * 1099511628211UL;
                    value = (value ^ (pixel.a == 0 ? 0U : pixel.b)) * 1099511628211UL;
                    value = (value ^ pixel.a) * 1099511628211UL;
                }
            }
            return value;
        }
    }
}

