using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Standalone import entry point for the retained isolated validation project.
// This only configures the reviewed missile PNG; it never rewrites other assets.
public static class ImportIronDomeMissile
{
    private const string MissilePath = "Assets/Project333/Resources/Project333/Vfx/IronDome/Missile.png";
    private const string SpriteName = "Missile";
    private const string ExplosionPath = "Assets/Project333/Resources/Project333/SpellEffects/TimedBomb/Explosion.png";

    private static string Hash(string path)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }

    private static ISpriteEditorDataProvider Provider(TextureImporter importer)
    {
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new InvalidOperationException("Missing native missile sprite provider.");
        provider.InitSpriteEditorDataProvider();
        var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (capability == null) throw new InvalidOperationException("Missing native missile edit capabilities.");
        foreach (var requested in new[] { EEditCapability.EditSpriteName, EEditCapability.EditSpriteRect,
            EEditCapability.EditBorder, EEditCapability.EditPivot, EEditCapability.CreateAndDeleteSprite })
            if (!capability.GetEditCapability().HasCapability(requested))
                throw new InvalidOperationException("Unsupported missile sprite operation: " + requested);
        if (provider.GetDataProvider<ISpriteNameFileIdDataProvider>() == null)
            throw new InvalidOperationException("Missing native sprite name/file GUID provider.");
        return provider;
    }

    private static Sprite Import(out int width, out int height, out bool wasAlreadyConfigured)
    {
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!source.LoadImage(File.ReadAllBytes(MissilePath), false))
                throw new InvalidOperationException("Missile PNG decode failed.");
            width = source.width; height = source.height;
            var pixels = source.GetPixels32();
            if (pixels.Count(p => p.a <= 2) < 200 || pixels.Count(p => p.a >= 200) < 200)
                throw new InvalidOperationException("Missile requires visible content and actual transparent pixels.");
            foreach (var corner in new[] { new Vector2Int(0, 0), new Vector2Int(width - 1, 0),
                new Vector2Int(0, height - 1), new Vector2Int(width - 1, height - 1) })
                if (source.GetPixel(corner.x, corner.y).a > 2f / 255f)
                    throw new InvalidOperationException("Missile canvas corners must retain transparency.");
        }
        finally { UnityEngine.Object.DestroyImmediate(source); }
        var maxSize = Math.Max(32, Mathf.NextPowerOfTwo(Math.Max(width, height)));
        if (maxSize > 16384) throw new InvalidOperationException("Missile exceeds native supported texture size.");

        var importer = AssetImporter.GetAtPath(MissilePath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing reviewed missile PNG importer.");
        var provider = Provider(importer);
        var previous = provider.GetSpriteRects();
        if (previous.Length > 1 || previous.Any(rect => rect.name != SpriteName))
            throw new InvalidOperationException("Unexpected prior missile sprite count/name.");
        var prior = previous.SingleOrDefault();
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        var pairs = provider.GetDataProvider<ISpriteNameFileIdDataProvider>().GetNameFileIdPairs().ToArray();
        // The installed Sprite Editor reconstructs name-pair GUIDs on every read for
        // Single sprites (only Multiple rects restore that GUID). Their equality with
        // the Single spriteID cannot be a no-op guard. Validate the native pair name,
        // actual Sprite asset GUID/fileID and persisted rect GUID instead; Batch also
        // requires that the second import performs no mutation and retains exact bytes.
        var reviewed = prior != null && prior.rect == new Rect(0, 0, width, height) &&
            prior.border == Vector4.zero && prior.pivot == new Vector2(.5f, .5f) && prior.alignment == SpriteAlignment.Center &&
            !prior.spriteID.Empty() && pairs.Length == 1 && pairs[0].name == SpriteName && !pairs[0].GetFileGUID().Empty() &&
            importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single &&
            importer.spritePixelsPerUnit == 100 && importer.maxTextureSize >= maxSize &&
            importer.npotScale == TextureImporterNPOTScale.None && !importer.mipmapEnabled &&
            importer.alphaSource == TextureImporterAlphaSource.FromInput && importer.alphaIsTransparency &&
            !importer.isReadable && importer.filterMode == FilterMode.Point && importer.wrapMode == TextureWrapMode.Clamp &&
            importer.textureCompression == TextureImporterCompression.Uncompressed &&
            !importer.GetPlatformTextureSettings("Standalone").overridden &&
            !importer.GetPlatformTextureSettings("Android").overridden &&
            settings.spriteMeshType == SpriteMeshType.FullRect && settings.spriteAlignment == (int)SpriteAlignment.Center &&
            settings.spritePivot == new Vector2(.5f, .5f);
        wasAlreadyConfigured = reviewed;
        if (!reviewed)
        {
            var rect = new SpriteRect
            {
                name = SpriteName, rect = new Rect(0, 0, width, height), border = Vector4.zero,
                pivot = new Vector2(.5f, .5f), alignment = SpriteAlignment.Center,
                spriteID = prior == null ? GUID.Generate() : prior.spriteID
            };
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.maxTextureSize = maxSize;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.ClearPlatformTextureSettings("Standalone");
            importer.ClearPlatformTextureSettings("Android");
            // Read after the importer assignments so an old settings snapshot cannot
            // undo Sprite/NPOT/alpha/mipmap configuration on SetTextureSettings.
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            importer = (TextureImporter)AssetImporter.GetAtPath(MissilePath);
            provider = Provider(importer);
            provider.SetSpriteRects(new[] { rect });
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                new[] { new SpriteNameFileIdPair(rect.name, rect.spriteID) });
            provider.Apply();
            importer.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MissilePath);
        if (sprite == null || sprite.name != SpriteName || sprite.rect != new Rect(0, 0, width, height) ||
            sprite.pivot != new Vector2(width / 2f, height / 2f) || sprite.vertices.Length != 4 ||
            sprite.pixelsPerUnit != 100 || sprite.texture.width != width || sprite.texture.height != height)
            throw new InvalidOperationException("Complete unscaled native missile sprite validation failed.");
        return sprite;
    }

    [Serializable]
    public sealed class Report
    {
        public int Width, Height, PreservedAssetCount;
        public bool Idempotent, SourcePngPreserved, PriorAssetsPreserved, NativeIdentityPreserved,
            NativeSpriteIdPreserved, FirstImportWasNoOp, SecondImportWasNoOp;
        public string MissileGuid, SpriteRectGuid, SourcePngSha256, MissileMetaSha256, ExplosionPngSha256, ExplosionMetaSha256;
        public long MissileFileId;
        public string[] PreservedAssetPaths;
    }

    public static void Batch()
    {
        var project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        if (!project.StartsWith("C:/Project_333/TempBuild/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Native missile import is restricted to an isolated TempBuild project.");
        var preserved = Directory.GetFiles("Assets/Project333/Resources", "*", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles("Assets/Project333/ScriptableObjects", "*", SearchOption.AllDirectories))
            .Select(path => path.Replace('\\', '/'))
            .Where(path => path != MissilePath && path != MissilePath + ".meta")
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        var preservedHashes = preserved.Select(Hash).ToArray();
        var sourceHash = Hash(MissilePath);
        var priorAssetGuid = AssetDatabase.AssetPathToGUID(MissilePath);
        var report = new Report { SourcePngSha256 = sourceHash, PreservedAssetPaths = preserved,
            PreservedAssetCount = preserved.Length, ExplosionPngSha256 = Hash(ExplosionPath),
            ExplosionMetaSha256 = Hash(ExplosionPath + ".meta") };

        var first = Import(out var width, out var height, out var firstWasNoOp);
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(first, out string firstGuid, out long firstFileId))
            throw new InvalidOperationException("Missing native missile GUID/file identity.");
        var firstMetaHash = Hash(MissilePath + ".meta");
        var firstSpriteId = Provider((TextureImporter)AssetImporter.GetAtPath(MissilePath)).GetSpriteRects().Single().spriteID;
        var second = Import(out _, out _, out var secondWasNoOp);
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(second, out string secondGuid, out long secondFileId))
            throw new InvalidOperationException("Missing repeated native missile identity.");
        report.Width = width; report.Height = height;
        report.MissileGuid = secondGuid; report.MissileFileId = secondFileId;
        var secondSpriteId = Provider((TextureImporter)AssetImporter.GetAtPath(MissilePath)).GetSpriteRects().Single().spriteID;
        report.SpriteRectGuid = secondSpriteId.ToString();
        report.FirstImportWasNoOp = firstWasNoOp;
        report.SecondImportWasNoOp = secondWasNoOp;
        report.NativeSpriteIdPreserved = !firstSpriteId.Empty() && firstSpriteId == secondSpriteId;
        report.MissileMetaSha256 = Hash(MissilePath + ".meta");
        report.Idempotent = secondWasNoOp && firstMetaHash == report.MissileMetaSha256;
        report.SourcePngPreserved = sourceHash == Hash(MissilePath);
        report.PriorAssetsPreserved = preserved.Select(Hash).SequenceEqual(preservedHashes);
        report.NativeIdentityPreserved = priorAssetGuid == secondGuid && firstGuid == secondGuid && firstFileId == secondFileId;
        var reportPath = Path.GetFullPath("../IronDomeVfx_20261001/native-import.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        if (!report.Idempotent || !report.SourcePngPreserved || !report.PriorAssetsPreserved ||
            !report.NativeIdentityPreserved || !report.NativeSpriteIdPreserved)
            throw new InvalidOperationException("Native Iron Dome missile import proof failed.");
        Debug.Log("PASS native IronDome missile: full source canvas, alpha, stable GUID/fileID, idempotent metadata, prior assets preserved.");
    }
}
