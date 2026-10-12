using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Presentation.Battle;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class IronDomeMissileAssetTests
    {
        private const string MissilePath = "Assets/Project333/Resources/Project333/Vfx/IronDome/Missile.png";
        private const string MissileResourcePath = "Project333/Vfx/IronDome/Missile";
        private const string ReviewedMissilePngHash = "D71296C45B80CFC5E968950EFCA95492E4BA29A0ECDCC8E2BE0EAB15B3B56686";
        private const string ExplosionPath = "Assets/Project333/Resources/Project333/SpellEffects/TimedBomb/Explosion.png";
        private const string OriginalExplosionPngHash = "1E8CD23CEEFC44D98F3EE3089CF1FE1CFBA0D359070ADC2AC6C19C4FD3F3DDF3";
        private const string OriginalExplosionMetaHash = "725132E4BDD0FC19277D1D18F9486AA03765A88C087BDFB97EA8D17FB59FB8EC";

        private static string Hash(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
        }

        [Test]
        public void CompleteMissileResourceRetainsFullSourceCanvasWithoutNativeResizingOrCropping()
        {
            Assert.That(Hash(MissilePath), Is.EqualTo(ReviewedMissilePngHash));
            var sprite = Resources.Load<Sprite>(MissileResourcePath);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(MissilePath));
            Assert.That(sprite.name, Is.EqualTo("Missile"));
            Assert.That(sprite.vertices.Length, Is.EqualTo(4), "The full rectangle retains the missile, fins and exhaust.");
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(100));
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(source.LoadImage(File.ReadAllBytes(MissilePath), false), Is.True);
                Assert.That(new Vector2Int(source.width, source.height), Is.EqualTo(new Vector2Int(1254, 1254)));
                Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, source.width, source.height)));
                Assert.That(sprite.pivot, Is.EqualTo(new Vector2(source.width / 2f, source.height / 2f)));
                Assert.That(new Vector2Int(sprite.texture.width, sprite.texture.height),
                    Is.EqualTo(new Vector2Int(source.width, source.height)), "Native NPOT import must preserve the source pixels.");
            }
            finally { Object.DestroyImmediate(source); }
        }

        [Test]
        public void MissileImportUsesPointFullRectAlphaAndNoCompressionOrMipmaps()
        {
            var importer = AssetImporter.GetAtPath(MissilePath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.GetPlatformTextureSettings("Standalone").overridden, Is.False);
            Assert.That(importer.GetPlatformTextureSettings("Android").overridden, Is.False);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            Assert.That(settings.spriteAlignment, Is.EqualTo((int)SpriteAlignment.Center));
            Assert.That(settings.spritePivot, Is.EqualTo(new Vector2(.5f, .5f)));
            var sprite = Resources.Load<Sprite>(MissileResourcePath);
            Assert.That(importer.maxTextureSize, Is.GreaterThanOrEqualTo(Mathf.NextPowerOfTwo(
                Mathf.Max((int)sprite.rect.width, (int)sprite.rect.height))));
        }

        [Test]
        public void SourceMissileContainsRealTransparentCornersAndVisibleArt()
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(source.LoadImage(File.ReadAllBytes(MissilePath), false), Is.True);
                var pixels = source.GetPixels32();
                Assert.That(pixels.Count(p => p.a <= 2), Is.GreaterThan(200));
                Assert.That(pixels.Count(p => p.a >= 200), Is.GreaterThan(200));
                foreach (var corner in new[] { new Vector2Int(0, 0), new Vector2Int(source.width - 1, 0),
                    new Vector2Int(0, source.height - 1), new Vector2Int(source.width - 1, source.height - 1) })
                    Assert.That(source.GetPixel(corner.x, corner.y).a, Is.LessThanOrEqualTo(2f / 255f));
            }
            finally { Object.DestroyImmediate(source); }
        }

        [Test]
        public void NativeMissileGuidAndFileIdResolveTheActualResourceSprite()
        {
            var sprite = Resources.Load<Sprite>(MissileResourcePath);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId), Is.True);
            Assert.That(guid, Is.Not.Empty);
            Assert.That(fileId, Is.Not.Zero);
            Assert.That(guid, Is.EqualTo(AssetDatabase.AssetPathToGUID(MissilePath)));
            Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(MissilePath));
            Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(MissilePath), Is.SameAs(sprite));
        }

        [Test]
        public void ExistingTimedBombExplosionIsReusedWithAllSixteenOriginalCellIdentities()
        {
            Assert.That(Hash(ExplosionPath), Is.EqualTo(OriginalExplosionPngHash), "Reuse preserves the existing PNG bytes.");
            Assert.That(Hash(ExplosionPath + ".meta"), Is.EqualTo(OriginalExplosionMetaHash), "Reuse preserves existing sprite IDs and slicing.");
            var frames = Resources.LoadAll<Sprite>(TimedBombPresentationVfx.ExplosionResourcePath)
                .OrderBy(frame => frame.name, StringComparer.Ordinal).ToArray();
            var ids = new long[] { -15746173, -1251228678, -562009592, 1255172742,
                1286118659, -649167341, -234024574, 505313890, 1526359651, -1541979407,
                -814921042, 1801781362, -1141055201, -1682850568, -1682925064, -1579225789 };
            Assert.That(frames.Length, Is.EqualTo(16));
            for (var i = 0; i < frames.Length; i++)
            {
                Assert.That(frames[i].name, Is.EqualTo("Explosion_" + i.ToString("00")));
                Assert.That(AssetDatabase.GetAssetPath(frames[i]), Is.EqualTo(ExplosionPath));
                Assert.That(frames[i].rect, Is.EqualTo(new Rect(i % 4 * 512, (3 - i / 4) * 512, 512, 512)));
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(frames[i], out string guid, out long fileId), Is.True);
                Assert.That(guid, Is.EqualTo("7f7826d548c467a4abfb0019ee762a6f"));
                Assert.That(fileId, Is.EqualTo(ids[i]));
            }
        }
    }
}
