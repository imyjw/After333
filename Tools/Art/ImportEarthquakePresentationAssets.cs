// One-time reviewed import in the isolated Pipeline Editor; invoke Run through Unity CLI eval.
// Sprite metadata is owned by Unity data providers. Never rerun over later artist edits.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ImportEarthquakePresentationAssets
{
    public const string Sheet = "Assets/Project333/Resources/Project333/SpellEffects/Earthquake/Cracks.png";
    public const string Audio = "Assets/Project333/Resources/Project333/SpellEffects/Earthquake/Audio/EarthquakeRumble.wav";
    public const int FramesPerSecond = 16;
    private static ISpriteEditorDataProvider CheckedProvider(SpriteDataProviderFactories factories, TextureImporter importer)
    {
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new InvalidOperationException("Missing Earthquake sprite provider.");
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
    public static string Run()
    {
        ValidateSource();
        var importer = AssetImporter.GetAtPath(Sheet) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing staged Cracks sheet.");
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = CheckedProvider(factories, importer);
        var prior = provider.GetSpriteRects();
        var rects = Enumerable.Range(0, 16).Select(i => {
            var name = "Cracks_" + i.ToString("00");
            var existing = prior.FirstOrDefault(s => s.name == name);
            return new SpriteRect { name = name, rect = new Rect(i % 4 * 512, (3 - i / 4) * 512, 512, 512),
                pivot = new Vector2(.5f, .5f), alignment = SpriteAlignment.Custom,
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
        provider.Apply(); importer.SaveAndReimport();
        var audioImporter = AssetImporter.GetAtPath(Audio) as AudioImporter;
        if (audioImporter == null) throw new InvalidOperationException("Missing staged Earthquake rumble.");
        audioImporter.forceToMono = false; audioImporter.loadInBackground = false;
        var audioSettings = audioImporter.defaultSampleSettings;
        audioSettings.loadType = AudioClipLoadType.DecompressOnLoad;
        audioSettings.compressionFormat = AudioCompressionFormat.PCM;
        audioSettings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        audioSettings.preloadAudioData = true;
        audioImporter.defaultSampleSettings = audioSettings; audioImporter.SaveAndReimport();
        AssetDatabase.SaveAssets();
        return Validate();
    }
    private static void ValidateSource()
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try {
            if (!texture.LoadImage(File.ReadAllBytes(Sheet), false) || texture.width != 2048 || texture.height != 2048)
                throw new InvalidOperationException("Require a 4x4 512px Cracks atlas.");
            var pixels = texture.GetPixels32();
            for (int f = 0; f < 16; f++) {
                int visible = 0, clear = 0;
                for (int y = 0; y < 512; y++) for (int x = 0; x < 512; x++) {
                    var alpha = pixels[((3 - f / 4) * 512 + y) * 2048 + f % 4 * 512 + x].a;
                    if (alpha == 0) { clear++; continue; }

                    visible++;
                    if (x < 16 || x > 495 || y < 16 || y > 495)
                        throw new InvalidOperationException("Cracks spills across frame " + f);
                }
                if (visible == 0 || clear == 0)
                    throw new InvalidOperationException("Empty/opaque frame " + f);
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
            throw new InvalidOperationException("Cracks import dimensions/count mismatch.");
        if (importer == null || importer.spritePixelsPerUnit != 100 || importer.filterMode != FilterMode.Point ||
            importer.mipmapEnabled || !importer.alphaIsTransparency || importer.isReadable ||
            importer.textureCompression != TextureImporterCompression.Uncompressed)
            throw new InvalidOperationException("Cracks importer settings mismatch.");
        for (int i = 0; i < sprites.Length; i++) {
            var sprite = sprites[i];
            if (sprite.name != "Cracks_" + i.ToString("00") ||
                sprite.rect != new Rect(i % 4 * 512, (3 - i / 4) * 512, 512, 512) ||
                sprite.pivot != new Vector2(256, 256) || sprite.pixelsPerUnit != 100)
                throw new InvalidOperationException("Cracks frame/order/pivot mismatch " + i);
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId) ||
                string.IsNullOrEmpty(guid) || fileId == 0)
                throw new InvalidOperationException("Missing native sprite identity " + i);
        }
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Audio);
        var audioImporter = AssetImporter.GetAtPath(Audio) as AudioImporter;
        if (clip == null || clip.channels != 1 || clip.frequency != 44100 || Math.Abs(clip.length - 1.08f) > .002f ||
            audioImporter == null || audioImporter.forceToMono || audioImporter.loadInBackground ||
            audioImporter.defaultSampleSettings.loadType != AudioClipLoadType.DecompressOnLoad ||
            audioImporter.defaultSampleSettings.compressionFormat != AudioCompressionFormat.PCM ||
            audioImporter.defaultSampleSettings.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate ||
            !audioImporter.defaultSampleSettings.preloadAudioData)
            throw new InvalidOperationException("Earthquake rumble import must preserve quiet mono44100 PCM1.08s without normalization.");
        return "Earthquake: 16 chronological512px Cracks cells at16fps; atlas2048x2048; centerpivot256,256; alpha-safe16px canvas margins; PPU100/Point/noMip/uncompressed; native name-fileIDs; quiet mono44100 PCM1.08s preloaded rumble.";
    }
}