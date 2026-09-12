using System;
using System.IO;
using Project333.Runtime.Presentation.Cards;
using UnityEditor;
using UnityEngine;

namespace Project333.Editor
{
    public sealed class Project333CardArtworkImporter : AssetPostprocessor
    {
        private const string ArtworkRoot = "Assets/Project333/Resources/Project333/CardArtwork";

        private void OnPreprocessTexture()
        {
            if (IsCardFront(assetPath))
                Normalize((TextureImporter)assetImporter);
        }

        private static bool IsCardFront(string path)
        {
            return string.Equals(Path.GetDirectoryName(path)?.Replace('\\', '/'), ArtworkRoot,
                       StringComparison.Ordinal) &&
                   string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(Path.GetFileNameWithoutExtension(path), "OpponentCardBack",
                       StringComparison.OrdinalIgnoreCase);
        }

        public static bool Normalize(TextureImporter importer)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Single &&
                importer.npotScale == TextureImporterNPOTScale.None &&
                !importer.mipmapEnabled && importer.alphaIsTransparency &&
                settings.spriteMeshType == SpriteMeshType.FullRect)
                return false;

            // UI and stat anchors use the complete card rectangle, never a power-of-two stretch or tight mesh.
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            return true;
        }

        [MenuItem("Tools/Project333/Cards/Normalize Card Artwork Imports")]
        public static void NormalizeAll()
        {
            var updated = 0;
            foreach (var path in Directory.GetFiles(ArtworkRoot, "*.png"))
            {
                var assetPath = path.Replace('\\', '/');
                if (!IsCardFront(assetPath))
                    continue;
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null || !Normalize(importer))
                    continue;
                importer.SaveAndReimport();
                updated++;
            }
            CardArtworkLibrary.ClearCacheForTests();
            Debug.Log($"After333: normalized {updated} card artwork imports. Source images, filtering and compression were preserved.");
        }
    }
}
