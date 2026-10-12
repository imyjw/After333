using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class DrainedStatusVfxAssetTests
    {
        const string Sheet="Assets/Project333/Resources/Project333/StatusEffects/Drained/ElectricArcs.png";
        const string ResourceKey="Project333/StatusEffects/Drained/ElectricArcs";
        static Sprite[] Sprites()=>AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>().OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
        [Test]
        public void EightNativeFullCellSpritesHaveStableNamesCenterPivotsAndPointReadableImport()
        {
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Sheet);var importer=AssetImporter.GetAtPath(Sheet) as TextureImporter;var sprites=Sprites();
            Assert.That(texture,Is.Not.Null);Assert.That(importer,Is.Not.Null);Assert.That(texture.width,Is.EqualTo(2048));Assert.That(texture.height,Is.EqualTo(1024));
            Assert.That(sprites.Length,Is.EqualTo(8));Assert.That(importer.textureType,Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode,Is.EqualTo(SpriteImportMode.Multiple));Assert.That(importer.spritePixelsPerUnit,Is.EqualTo(100));
            Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(importer.mipmapEnabled,Is.False);Assert.That(importer.isReadable,Is.True);
            Assert.That(importer.alphaIsTransparency,Is.True);Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));
            var ids=new long[8];var expectedGuid=AssetDatabase.AssetPathToGUID(Sheet);
            for(int i=0;i<8;i++)
            {
                var sprite=sprites[i];Assert.That(sprite.name,Is.EqualTo("ElectricArcs_"+i));
                Assert.That(sprite.rect,Is.EqualTo(new Rect(i%4*512,(1-i/4)*512,512,512)));Assert.That(sprite.pivot,Is.EqualTo(new Vector2(256,256)));
                Assert.That(sprite.pixelsPerUnit,Is.EqualTo(100));Assert.That(sprite.vertices.Length,Is.EqualTo(4));
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite,out string guid,out long id),Is.True);
                Assert.That(guid,Is.EqualTo(expectedGuid));Assert.That(id,Is.Not.Zero);ids[i]=id;
            }
            Assert.That(ids.Distinct().Count(),Is.EqualTo(8));
        }
        [Test]
        public void EveryElectricArcFramePreservesPaddedTransparentCenterAndDistinctPhasePixels()
        {
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Sheet);Assert.That(texture,Is.Not.Null);
            var pixels=texture.GetPixels32();var hashes=new string[8];
            for(int f=0;f<8;f++)
            {
                int visible=0;var frameBytes=new byte[512*512*4];
                for(int y=0;y<512;y++)for(int x=0;x<512;x++)
                {
                    var c=pixels[((1-f/4)*512+y)*2048+f%4*512+x];int index=(y*512+x)*4;
                    frameBytes[index]=c.r;frameBytes[index+1]=c.g;frameBytes[index+2]=c.b;frameBytes[index+3]=c.a;
                    if(c.a==0)continue;visible++;
                    Assert.That(x,Is.InRange(16,495),"horizontal margin frame "+f);Assert.That(y,Is.InRange(16,495),"vertical margin frame "+f);
                    Assert.That(x<208||x>303||y<208||y>303,Is.True,"Status overlay preserves central96px empty space frame "+f);
                }
                Assert.That(visible,Is.GreaterThan(0));Assert.That(visible,Is.LessThan(512*512));hashes[f]=Hash(frameBytes);
            }
            Assert.That(hashes.Distinct().Count(),Is.EqualTo(8),"Generated arc animation retains eight distinct phases.");
        }
        [Test]
        public void ResourceLookupReturnsExactNativeSpritesWithoutChangingReviewedPngOrMetadataBytes()
        {
            var pngBefore=Hash(File.ReadAllBytes(Sheet));var metaBefore=Hash(File.ReadAllBytes(Sheet+".meta"));
            var native=Sprites();var nativeIds=native.Select(s=>{AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s,out string guid,out long id);return guid+":"+id;}).ToArray();
            var loaded=Resources.LoadAll<Sprite>(ResourceKey).OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
            Assert.That(loaded,Is.EqualTo(native));Assert.That(loaded.Length,Is.EqualTo(8));
            Assert.That(loaded.Select(s=>{AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s,out string guid,out long id);return guid+":"+id;}).ToArray(),Is.EqualTo(nativeIds));
            Assert.That(Hash(File.ReadAllBytes(Sheet)),Is.EqualTo(pngBefore));Assert.That(Hash(File.ReadAllBytes(Sheet+".meta")),Is.EqualTo(metaBefore));
        }
        static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
    }
}
