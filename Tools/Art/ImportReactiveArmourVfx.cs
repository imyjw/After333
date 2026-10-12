// Explicit native Editor import for Reactive Armour VFX. No startup callback, scene mutation,
// hand-written metadata, or card/catalog changes. The runtime owns positional playback.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ImportReactiveArmourVfx
{
    public const string AtlasPath = "Assets/Project333/Resources/Project333/Vfx/ReactiveArmour/Activation.png";
    public const string ResourcePath = "Project333/Vfx/ReactiveArmour/Activation";
    private const int CellSize = 512;
    private const int FrameCount = 8;
    private static readonly Vector2 Pivot = new Vector2(160f / CellSize, .5f);

    private static string Hash(string path)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }

    private static long LocalId(Sprite sprite)
    {
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long id))
            throw new InvalidOperationException("Missing native Reactive Armour sprite identity.");
        return id;
    }

    private static ISpriteEditorDataProvider Provider()
    {
        var importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Stage the reviewed Reactive Armour PNG first.");
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new InvalidOperationException("Missing native Sprite Editor provider.");
        provider.InitSpriteEditorDataProvider();
        var capabilities = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (capabilities == null) throw new InvalidOperationException("Missing native sprite edit capability.");
        foreach (var edit in new[] { EEditCapability.EditSpriteName, EEditCapability.EditSpriteRect,
            EEditCapability.EditBorder, EEditCapability.EditPivot, EEditCapability.CreateAndDeleteSprite })
            if (!capabilities.GetEditCapability().HasCapability(edit))
                throw new InvalidOperationException("Unsupported native sprite operation: " + edit);
        if (provider.GetDataProvider<ISpriteNameFileIdDataProvider>() == null)
            throw new InvalidOperationException("Missing sprite name/fileID provider.");
        return provider;
    }

    private static FramePixels[] ValidatePixels()
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(AtlasPath), false)
                || texture.width != 2048 || texture.height != 1024)
                throw new InvalidOperationException("Expected a 2048x1024 eight-frame RGBA atlas.");
            var pixels = texture.GetPixels32();
            var frames = new FramePixels[FrameCount];
            for (int frame = 0; frame < FrameCount; frame++)
            {
                int x0 = frame % 4 * CellSize, y0 = (1 - frame / 4) * CellSize;
                int xmin = CellSize, ymin = CellSize, xmax = -1, ymax = -1;
                int transparent = 0, semi = 0, right = 0;
                for (int y = 0; y < CellSize; y++)
                for (int x = 0; x < CellSize; x++)
                {
                    var pixel = pixels[(y0 + y) * texture.width + x0 + x];
                    if (pixel.a == 0) transparent++;
                    else if (pixel.a < 255) semi++;
                    if (pixel.a < 16) continue;
                    xmin = Math.Min(xmin, x); ymin = Math.Min(ymin, y);
                    xmax = Math.Max(xmax, x); ymax = Math.Max(ymax, y);
                    if (x > 280) right++;
                }
                if (xmax < 0 || xmin < 16 || ymin < 16 || xmax > 495 || ymax > 495
                    || transparent == 0 || semi == 0)
                    throw new InvalidOperationException("Nonempty alpha art must stay inside each cell with a 16px border: frame " + frame);
                frames[frame] = new FramePixels
                {
                    Frame = frame, Alpha16Bounds = new[] { xmin, ymin, xmax, ymax },
                    TransparentPixels = transparent, SemitransparentPixels = semi, RightSidePixels = right
                };
            }
            if (frames[0].Alpha16Bounds[2] > 280 || frames[0].RightSidePixels != 0
                || frames[1].RightSidePixels <= 0 || frames[4].RightSidePixels <= frames[1].RightSidePixels)
                throw new InvalidOperationException("Frame zero must be barrier-only; later frames must expand toward the attacker.");
            return frames;
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static bool Run()
    {
        ValidatePixels();
        var provider = Provider();
        var old = provider.GetSpriteRects();
        var names = Enumerable.Range(0, FrameCount).Select(i => "Activation_" + i).ToArray();
        if (old.Any(rect => !names.Contains(rect.name) && rect.name != "Activation")
            || old.GroupBy(rect => rect.name).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Unexpected artist-owned sprite layout; do not replace it.");
        // A redundant provider Apply regenerates importer housekeeping metadata even when frames match.
        // Leave the reviewed native import untouched when every public setting and native mapping already agrees.
        if (AlreadyConfigured(provider, old))
        {
            ValidateNative();
            return true;
        }
        var rects = Enumerable.Range(0, FrameCount).Select(frame =>
        {
            string name = names[frame];
            var prior = old.SingleOrDefault(rect => rect.name == name);
            return new SpriteRect
            {
                name = name, rect = new Rect(frame % 4 * CellSize, (1 - frame / 4) * CellSize, CellSize, CellSize),
                pivot = Pivot, alignment = SpriteAlignment.Custom, border = Vector4.zero,
                spriteID = prior == null ? GUID.Generate() : prior.spriteID
            };
        }).ToArray();
        var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
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
        importer.isReadable = true;
        importer.ClearPlatformTextureSettings("Standalone");
        importer.ClearPlatformTextureSettings("Android");
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = Pivot;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        provider = Provider();
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        ValidateNative();
        return false;
    }

    private static bool AlreadyConfigured(ISpriteEditorDataProvider provider, SpriteRect[] rects)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Multiple
            || importer.spritePixelsPerUnit != 100 || importer.maxTextureSize != 2048
            || importer.npotScale != TextureImporterNPOTScale.None || importer.mipmapEnabled
            || importer.alphaSource != TextureImporterAlphaSource.FromInput || !importer.alphaIsTransparency
            || importer.filterMode != FilterMode.Point || importer.wrapMode != TextureWrapMode.Clamp
            || importer.textureCompression != TextureImporterCompression.Uncompressed || !importer.isReadable
            || settings.spriteMeshType != SpriteMeshType.FullRect || settings.spriteAlignment != (int)SpriteAlignment.Custom
            || settings.spritePivot != Pivot
            || importer.GetPlatformTextureSettings("Standalone").overridden
            || importer.GetPlatformTextureSettings("Android").overridden
            || rects.Length != FrameCount)
            return false;
        var pairs = provider.GetDataProvider<ISpriteNameFileIdDataProvider>().GetNameFileIdPairs().ToArray();
        if (pairs.Length != FrameCount) return false;
        for (int frame = 0; frame < FrameCount; frame++)
        {
            string name = "Activation_" + frame;
            var rect = rects.SingleOrDefault(candidate => candidate.name == name);
            if (rect == null || rect.rect != new Rect(frame % 4 * CellSize, (1 - frame / 4) * CellSize, CellSize, CellSize)
                || rect.pivot != Pivot || rect.alignment != SpriteAlignment.Custom || rect.border != Vector4.zero
                || rect.spriteID.Empty())
                return false;
            var pair = pairs.SingleOrDefault(candidate => candidate.name == name);
            if (pair == null || pair.GetFileGUID() != rect.spriteID) return false;
        }
        return true;
    }

    private static Sprite[] ValidateNative()
    {
        var sprites = Resources.LoadAll<Sprite>(ResourcePath).OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
        if (sprites.Length != FrameCount) throw new InvalidOperationException("Expected eight native Reactive Armour sprites.");
        for (int frame = 0; frame < FrameCount; frame++)
        {
            var sprite = sprites[frame];
            if (sprite.name != "Activation_" + frame
                || sprite.rect != new Rect(frame % 4 * CellSize, (1 - frame / 4) * CellSize, CellSize, CellSize)
                || sprite.pivot != new Vector2(160, 256) || sprite.pixelsPerUnit != 100
                || sprite.vertices.Length != 4 || sprite.texture.width != 2048 || sprite.texture.height != 1024)
                throw new InvalidOperationException("Native Reactive Armour frame order/rect/pivot/full mesh mismatch.");
        }
        return sprites;
    }

    [Serializable]
    public sealed class FramePixels
    {
        public int Frame, TransparentPixels, SemitransparentPixels, RightSidePixels;
        public int[] Alpha16Bounds;
    }

    [Serializable]
    public sealed class Report
    {
        public string AtlasPath, ResourcePath, TextureGuid, SourcePngSha256, NativeMetaSha256, FirstNativeMetaSha256;
        public bool FirstRunNoOp, SecondRunNoOp, NativeMetadataStable, SpriteFileIdsStable, SpriteGuidsStable;
        public bool Idempotent, RetainedNamedFileIds, SourcePngPreserved;
        public string[] SpriteNames, SpriteGuids;
        public long[] SpriteFileIds;
        public FramePixels[] Frames;
    }

    public static void Batch()
    {
        var sourceHash = Hash(AtlasPath);
        var priorGuid = AssetDatabase.AssetPathToGUID(AtlasPath);
        var prior = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>()
            .Where(sprite => sprite.name.StartsWith("Activation_", StringComparison.Ordinal))
            .ToDictionary(sprite => sprite.name, LocalId);
        bool firstRunNoOp = Run();
        var first = ValidateNative();
        var firstIds = first.Select(LocalId).ToArray();
        var firstSpriteGuids = Provider().GetSpriteRects().OrderBy(rect => rect.name, StringComparer.Ordinal)
            .Select(rect => rect.spriteID.ToString()).ToArray();
        var firstMetaHash = Hash(AtlasPath + ".meta");
        bool secondRunNoOp = Run();
        var final = ValidateNative();
        var finalSpriteGuids = Provider().GetSpriteRects().OrderBy(rect => rect.name, StringComparer.Ordinal)
            .Select(rect => rect.spriteID.ToString()).ToArray();
        var report = new Report
        {
            AtlasPath = AtlasPath, ResourcePath = ResourcePath,
            TextureGuid = AssetDatabase.AssetPathToGUID(AtlasPath),
            SourcePngSha256 = sourceHash, NativeMetaSha256 = Hash(AtlasPath + ".meta"),
            FirstNativeMetaSha256 = firstMetaHash, FirstRunNoOp = firstRunNoOp, SecondRunNoOp = secondRunNoOp,
            NativeMetadataStable = firstMetaHash == Hash(AtlasPath + ".meta"),
            SpriteFileIdsStable = final.Select(LocalId).SequenceEqual(firstIds),
            SpriteGuidsStable = finalSpriteGuids.SequenceEqual(firstSpriteGuids),
            Idempotent = firstMetaHash == Hash(AtlasPath + ".meta") && final.Select(LocalId).SequenceEqual(firstIds)
                && finalSpriteGuids.SequenceEqual(firstSpriteGuids),
            RetainedNamedFileIds = final.All(sprite => !prior.TryGetValue(sprite.name, out long id) || LocalId(sprite) == id)
                && (string.IsNullOrEmpty(priorGuid) || priorGuid == AssetDatabase.AssetPathToGUID(AtlasPath)),
            SourcePngPreserved = Hash(AtlasPath) == sourceHash,
            SpriteNames = final.Select(sprite => sprite.name).ToArray(),
            SpriteGuids = finalSpriteGuids, SpriteFileIds = final.Select(LocalId).ToArray(), Frames = ValidatePixels()
        };
        var reportPath = Path.GetFullPath("../ReactiveArmourVfx_20261001/native-import.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        if (!report.Idempotent || !report.RetainedNamedFileIds || !report.SourcePngPreserved)
            throw new InvalidOperationException("Reactive Armour native sprite import proof failed.");
        Debug.Log("PASS Reactive Armour eight-frame native import: alpha/borders, frame order, stable GUID/fileIDs, idempotent metadata and source PNG preserved.");
    }
}



