using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Presentation.Cards;
using Project333.Runtime.Presentation.Hand;
using UnityEditor;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class CardArtworkAspectTests
    {
        private const string ArtworkRoot = "Assets/Project333/Resources/Project333/CardArtwork";

        private static IEnumerable<string> CardIds => Directory.GetFiles(ArtworkRoot, "*.png")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(id => id != "OpponentCardBack")
            .OrderBy(id => id);

        [SetUp]
        public void SetUp() => CardArtworkLibrary.ClearCacheForTests();

        [TestCaseSource(nameof(CardIds))]
        public void Artwork_PreservesFullSourceImageAndAspectRatio(string id)
        {
            var path = ArtworkRoot + "/" + id + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            Assert.That(CardArtworkLibrary.TryGetArtwork(id, out var sprite), Is.True, id);
            var sourceRatio = (float)width / height;
            var importedRatio = sprite.rect.width / sprite.rect.height;
            Assert.That(importedRatio, Is.EqualTo(sourceRatio).Within(0.002f),
                $"{id}: source {width}x{height}, displayed sprite {sprite.rect.width}x{sprite.rect.height}");
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(importer.mipmapEnabled, Is.False);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            Assert.That(sprite.rect.x, Is.Zero);
            Assert.That(sprite.rect.y, Is.Zero);
            Assert.That(sprite.rect.width, Is.EqualTo(sprite.texture.width));
            Assert.That(sprite.rect.height, Is.EqualTo(sprite.texture.height));
        }

        [TestCase(150, 200)]
        [TestCase(300, 400)]
        [TestCase(550, 733)]
        [TestCase(240, 400)]
        public void HandAndDraftLayout_AllCardsOccupyTheSameArea(int width, int height)
        {
            CardArtworkLibrary.TryGetArtwork("Zombie", out var reference);
            var container = new Rect(0, 0, width, height);
            var expected = HandCardStatOverlayLayout.CalculateRenderedSpriteRect(container,
                reference.rect.size, true);
            foreach (var id in CardIds)
            {
                CardArtworkLibrary.TryGetArtwork(id, out var sprite);
                var actual = HandCardStatOverlayLayout.CalculateRenderedSpriteRect(container,
                    sprite.rect.size, true);
                Assert.That(actual.width, Is.EqualTo(expected.width).Within(1f), id);
                Assert.That(actual.height, Is.EqualTo(expected.height).Within(1f), id);
                Assert.That(actual.center.x, Is.EqualTo(expected.center.x).Within(0.5f), id);
                Assert.That(actual.center.y, Is.EqualTo(expected.center.y).Within(0.5f), id);
            }
        }

        [Test]
        public void LowercaseFireboltId_UsesTheSameFullArtwork()
        {
            Assert.That(CardArtworkLibrary.TryGetArtwork("firebolt", out var sprite), Is.True);
            Assert.That(sprite, Is.SameAs(AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkRoot + "/Firebolt.png")));
        }
    }
}
