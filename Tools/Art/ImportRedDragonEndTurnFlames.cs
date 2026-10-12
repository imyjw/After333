// One-time reviewed import in the isolated Pipeline Editor; invoke Run through Unity CLI eval.
// Sprite metadata is owned by Unity data providers. Never rerun over later artist edits.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ImportRedDragonEndTurnFlames
{
    public const string Sheet = "Assets/Project333/Resources/Project333/Presentation/RedDragon/EndTurnFlames.png";
    private static ISpriteEditorDataProvider CheckedProvider(SpriteDataProviderFactories factories, TextureImporter importer)
    {
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new InvalidOperationException("Missing Firewall sprite provider.");
        provider.InitSpriteEditorDataProvider();
        var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (capability == null) throw new InvalidOperationException("Missing sprite edit capability; aborting.");
        foreach (var edit in new[] { EEditCapability.EditSpriteName, EEditCapability.EditSpriteRect,
            EEditCapability.EditPivot, EEditCapability.EditBorder, EEditCapability.CreateAndDeleteSprite })
            if (!capability.GetEditCapability().HasCapability(edit))
                throw new InvalidOperationException("Unsupported sprite operation " + edit + "; aborting.");
        if (provider.GetDataProvider<ISpriteNameFileIdDataProvider>() == null)
            throw new InvalidOperationException("Missing sprite-name fileID provider; aborting.");
        return provider;
    }
    public static void Execute()
    {
        var proof = "C:/Users/youndonghyeok/.codex/visualizations/2026/09/09/01a08439-297d-79c0-a9dc-a0153e47e32b/RedDragonEndFlames_20261006";
        var validation = Run();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        var identities = sprites.Select(s => {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out string guid, out long id);
            return s.name + ":" + guid + ":" + id;
        });
        File.WriteAllText(proof + "/asset-import-result.txt", validation + "\n" + string.Join("\n", identities));
        Debug.Log(validation);
    }
    public static string Run()
    {
        ValidateSource();
        var importer = AssetImporter.GetAtPath(Sheet) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing staged FlameWall sheet.");
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = CheckedProvider(factories, importer);
        var prior = provider.GetSpriteRects();
        var rects = Enumerable.Range(0, 16).Select(i => {
            var name = "EndTurnFlames_" + i.ToString("00");
            var existing = prior.FirstOrDefault(s => s.name == name);
            return new SpriteRect { name = name, rect = new Rect(i % 4 * 512, (3 - i / 4) * 512, 512, 512),
                pivot = new Vector2(.5f, 16f / 512f), alignment = SpriteAlignment.Custom,
                border = Vector4.zero, spriteID = existing == null ? GUID.Generate() : existing.spriteID };
        }).ToArray();
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100; importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None; importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true; importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.isReadable = false; importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        // A fresh provider is required after switching a new Default texture to Multiple.
        provider = CheckedProvider(factories, importer);
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
            .SetNameFileIdPairs(rects.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)).ToList());
        provider.Apply(); importer.SaveAndReimport(); AssetDatabase.SaveAssets();
        return Validate();
    }
    private static void ValidateSource()
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try {
            if (!texture.LoadImage(File.ReadAllBytes(Sheet), false) || texture.width != 2048 || texture.height != 2048)
                throw new InvalidOperationException("Require a 4x4 512px FlameWall atlas.");
            var pixels = texture.GetPixels32();
            for (int f = 0; f < 16; f++) {
                int visible = 0, clear = 0, lowest = 512;
                for (int y = 0; y < 512; y++) for (int x = 0; x < 512; x++) {
                    var alpha = pixels[((3 - f / 4) * 512 + y) * 2048 + f % 4 * 512 + x].a;
                    if (alpha == 0) { clear++; continue; }
                    if (alpha < 16) throw new InvalidOperationException("Unreviewed low-alpha noise in frame " + f);
                    visible++; lowest = Math.Min(lowest, y);
                    if (x < 16 || x > 495 || y < 16 || y > 495)
                        throw new InvalidOperationException("FlameWall spills across frame " + f);
                }
                if (visible == 0 || clear == 0 || lowest != 16)
                    throw new InvalidOperationException("Empty/opaque frame or floor mismatch " + f);
            }
        } finally { UnityEngine.Object.DestroyImmediate(texture); }
    }
    public static string Validate()
    {
        ValidateSource();
        var importer = AssetImporter.GetAtPath(Sheet) as TextureImporter;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Sheet);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        if (texture == null || texture.width != 2048 || texture.height != 2048 || sprites.Length != 16)
            throw new InvalidOperationException("FlameWall import dimensions/count mismatch.");
        if (importer == null || importer.spritePixelsPerUnit != 100 || importer.filterMode != FilterMode.Point ||
            importer.mipmapEnabled || !importer.alphaIsTransparency || importer.isReadable ||
            importer.textureCompression != TextureImporterCompression.Uncompressed)
            throw new InvalidOperationException("FlameWall importer settings mismatch.");
        for (int i = 0; i < sprites.Length; i++) {
            var sprite = sprites[i];
            if (sprite.name != "EndTurnFlames_" + i.ToString("00") ||
                sprite.rect != new Rect(i % 4 * 512, (3 - i / 4) * 512, 512, 512) ||
                sprite.pivot != new Vector2(256, 16) || sprite.pixelsPerUnit != 100)
                throw new InvalidOperationException("FlameWall frame/order/pivot mismatch " + i);
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId) ||
                string.IsNullOrEmpty(guid) || fileId == 0)
                throw new InvalidOperationException("Missing native sprite identity " + i);
        }
        return "RedDragon end turn: 16 chronological512px FlameWall cells; atlas2048x2048; pivot256,16; visible floor16; PPU100/Point/noMip/uncompressed/alpha; native name-fileIDs.";
    }
}