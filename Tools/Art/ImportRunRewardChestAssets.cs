using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Run this standalone importer in the retained isolated validation Editor.
// Unity owns all sprite/fileID metadata; no scenes or unrelated assets are saved.
public static class ImportRunRewardChestAssets
{
    public const string Sheet = "Assets/Project333/Resources/Project333/UI/RunRewards/RewardChestStates.png";
    private static readonly string[] Names = { "RewardChestClosed", "RewardChestOpen" };
    private const int CellSize = 887;
    private const int Width = 1774;
    private const int Height = 887;

    private static ISpriteEditorDataProvider Provider(TextureImporter importer)
    {
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new InvalidOperationException("Missing chest sprite provider.");
        provider.InitSpriteEditorDataProvider();
        var capabilities = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (capabilities == null) throw new InvalidOperationException("Missing sprite editing capability.");
        foreach (var edit in new[] { EEditCapability.EditSpriteName, EEditCapability.EditSpriteRect,
            EEditCapability.EditBorder, EEditCapability.EditPivot, EEditCapability.CreateAndDeleteSprite })
            if (!capabilities.GetEditCapability().HasCapability(edit))
                throw new InvalidOperationException("Unsupported native sprite edit " + edit);
        if (provider.GetDataProvider<ISpriteNameFileIdDataProvider>() == null)
            throw new InvalidOperationException("Missing sprite name/fileID provider.");
        return provider;
    }

    private static void ValidateSource()
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(Sheet), false) || texture.width != Width || texture.height != Height)
                throw new InvalidOperationException("Require the reviewed 1774x887 chest atlas.");
            var pixels = texture.GetPixels32();
            for (var f = 0; f < 2; f++)
            {
                var visible = 0;
                var clear = 0;
                for (var y = 0; y < Height; y++)
                    for (var x = 0; x < CellSize; x++)
                    {
                        var alpha = pixels[y * Width + f * CellSize + x].a;
                        if (alpha == 0) clear++;
                        if (alpha > 16) visible++;
                        if ((x == 0 || x == CellSize - 1 || y == 0 || y == Height - 1) && alpha > 16)
                            throw new InvalidOperationException("Chest/glow crosses cell boundary " + f);
                    }
                if (visible < 200000 || clear < 300000)
                    throw new InvalidOperationException("Chest cell requires visible art and genuine alpha " + f);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    public static void Run()
    {
        ValidateSource();
        var importer = AssetImporter.GetAtPath(Sheet) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing reviewed chest PNG.");
        var provider = Provider(importer);
        var prior = provider.GetSpriteRects();
        // A new Default texture reports a synthetic whole-image rect; only a
        // previously authored Sprite texture can conflict with this atlas.
        if (importer.textureType == TextureImporterType.Sprite && prior.Length > 0 && prior.Any(rect => !Names.Contains(rect.name)))
            throw new InvalidOperationException("Unexpected prior chest atlas layout/name.");
        var pairs = provider.GetDataProvider<ISpriteNameFileIdDataProvider>().GetNameFileIdPairs().ToArray();
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        var ready = prior.Length == 2 && pairs.Length == 2 &&
            Names.All(name => prior.Any(rect => rect.name == name && !rect.spriteID.Empty() &&
                rect.rect == new Rect(Array.IndexOf(Names, name) * CellSize, 0, CellSize, CellSize) &&
                rect.pivot == new Vector2(.5f, .5f) && rect.alignment == SpriteAlignment.Center && rect.border == Vector4.zero) &&
                pairs.Any(pair => pair.name == name && !pair.GetFileGUID().Empty())) &&
            importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Multiple &&
            importer.spritePixelsPerUnit == 100 && importer.maxTextureSize == 2048 &&
            importer.npotScale == TextureImporterNPOTScale.None && !importer.mipmapEnabled &&
            importer.alphaSource == TextureImporterAlphaSource.FromInput && importer.alphaIsTransparency &&
            importer.filterMode == FilterMode.Point && importer.wrapMode == TextureWrapMode.Clamp &&
            importer.textureCompression == TextureImporterCompression.Uncompressed && !importer.isReadable &&
            !importer.GetPlatformTextureSettings("Standalone").overridden &&
            !importer.GetPlatformTextureSettings("Android").overridden && settings.spriteMeshType == SpriteMeshType.FullRect;
        if (!ready)
        {
            var rects = Names.Select((name, index) => new SpriteRect
            {
                name = name,
                rect = new Rect(index * CellSize, 0, CellSize, CellSize),
                border = Vector4.zero,
                pivot = new Vector2(.5f, .5f),
                alignment = SpriteAlignment.Center,
                spriteID = prior.FirstOrDefault(rect => rect.name == name)?.spriteID ?? GUID.Generate()
            }).ToArray();
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
            importer.ClearPlatformTextureSettings("Standalone");
            importer.ClearPlatformTextureSettings("Android");
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            importer = (TextureImporter)AssetImporter.GetAtPath(Sheet);
            provider = Provider(importer);
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)).ToArray());
            provider.Apply();
            importer.SaveAndReimport();
        }
        ValidateImported();
    }

    private static Sprite[] ValidateImported()
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Sheet);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>().OrderBy(sprite => Array.IndexOf(Names, sprite.name)).ToArray();
        if (texture == null || texture.width != Width || texture.height != Height || sprites.Length != 2)
            throw new InvalidOperationException("Chest native atlas dimensions/count mismatch.");
        for (var i = 0; i < 2; i++)
        {
            var sprite = sprites[i];
            if (sprite.name != Names[i] || sprite.rect != new Rect(i * CellSize, 0, CellSize, CellSize) ||
                sprite.pivot != new Vector2(CellSize / 2f, CellSize / 2f) || sprite.pixelsPerUnit != 100 || sprite.vertices.Length != 4 ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long localId) ||
                string.IsNullOrEmpty(guid) || localId == 0)
                throw new InvalidOperationException("Invalid native chest sprite identity/rect/pivot " + i);
        }
        return sprites;
    }

    private static string Hash(string path)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }

    [Serializable] public sealed class Report
    {
        public bool SourcePngPreserved, NativeMetadataIdempotent, SceneFilesPreserved;
        public int Width, Height, CellSize;
        public string ArtworkGuid, SourcePngSha256;
        public string[] Names;
        public long[] SpriteFileIds;
    }

    public static void Batch()
    {
        var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        if (!root.StartsWith("C:/Project_333/TempBuild/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chest importer is restricted to an isolated TempBuild Editor.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var scenePaths = Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories)
            .SelectMany(path => new[] { path, path + ".meta" }).Where(File.Exists).ToArray();
        var sceneHashes = scenePaths.Select(Hash).ToArray();
        var pngHash = Hash(Sheet);
        Run();
        var firstMetadata = Hash(Sheet + ".meta");
        Run();
        var sprites = ValidateImported();
        var report = new Report
        {
            SourcePngPreserved = Hash(Sheet) == pngHash,
            NativeMetadataIdempotent = Hash(Sheet + ".meta") == firstMetadata,
            SceneFilesPreserved = scenePaths.Select(Hash).SequenceEqual(sceneHashes),
            Width = Width, Height = Height, CellSize = CellSize,
            ArtworkGuid = AssetDatabase.AssetPathToGUID(Sheet), SourcePngSha256 = pngHash,
            Names = sprites.Select(sprite => sprite.name).ToArray(),
            SpriteFileIds = sprites.Select(sprite =>
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long fileId);
                return fileId;
            }).ToArray()
        };
        var proof = Path.GetFullPath("../RunRewardsChest_20261001/chest-native-import.json");
        Directory.CreateDirectory(Path.GetDirectoryName(proof));
        File.WriteAllText(proof, JsonUtility.ToJson(report, true));
        if (!report.SourcePngPreserved || !report.NativeMetadataIdempotent || !report.SceneFilesPreserved)
            throw new InvalidOperationException("Chest native import preservation/idempotence proof failed.");
        Debug.Log("PASS chest native import: exact2x887cells, Point/FullRect/alpha, stable fileIDs, source/scenes preserved.");
    }
}

