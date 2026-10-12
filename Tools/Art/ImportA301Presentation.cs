// One-time reviewed asset import. Stage in an isolated Editor and call Run via Unity CLI eval.
// Never rerun over later artist edits; sprite metadata is owned exclusively by Unity providers.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ImportA301Presentation
{
    const string Folder = "Assets/Unit/A-301/";
    const string Card = "Assets/Project333/ScriptableObjects/StarterTen/Cards/A-301.asset";
    const string Shot = "Assets/Project333/Resources/Project333/Units/A-301/Audio/LaserShot.wav";
    const string ControllerGuid = "46d9d6ef07bae8047b36b48d3ac244b6";
    static readonly string[] Motions = { "Idle", "Attack", "Death" };
    static readonly float[] Rates = { 8f, 10f, 8f };
    const int Count = 8;

    public static string Run()
    {
        string controllerPath = Folder + "A-301.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null || controller.layers.Length != 1 || AssetDatabase.AssetPathToGUID(controllerPath) != ControllerGuid)
            throw new InvalidOperationException("Stage the original A-301 controller and metadata first.");
        var changed = new HashSet<string>(Motions.SelectMany(m => new[] { Folder + m + ".png", Folder + m + ".png.meta", Folder + m + ".anim" }));
        var protectedFiles = Directory.GetFiles(Folder, "*", SearchOption.AllDirectories)
            .Where(p => !changed.Contains(p.Replace('\\', '/'))).ToDictionary(p => p, File.ReadAllBytes);
        protectedFiles[Card] = File.ReadAllBytes(Card);
        protectedFiles[Card + ".meta"] = File.ReadAllBytes(Card + ".meta");
        var guids = new Dictionary<string, string>();
        var ids = new Dictionary<string, Dictionary<string, long>>();
        // Validate every image and provider BEFORE changing any importer or clip.
        foreach (string motion in Motions)
        {
            string png = Folder + motion + ".png", anim = Folder + motion + ".anim";
            ValidateSource(png);
            RequireProvider(png);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(anim);
            var state = controller.layers[0].stateMachine.states.SingleOrDefault(s => s.state.name == motion).state;
            if (clip == null || state == null || state.motion != clip || state.speed != 1f)
                throw new InvalidOperationException("Unexpected existing state/clip " + motion);
            guids[png] = AssetDatabase.AssetPathToGUID(png);
            guids[anim] = AssetDatabase.AssetPathToGUID(anim);
            ids[png] = AssetDatabase.LoadAllAssetsAtPath(png).OfType<Sprite>().ToDictionary(s => s.name, LocalId);
        }
        for (int m = 0; m < Motions.Length; m++)
        {
            string motion = Motions[m], path = Folder + motion + ".png";
            var sprites = Slice(path, motion);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + motion + ".anim");
            clip.frameRate = Rates[m];
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"),
                sprites.Select((sprite, i) => new ObjectReferenceKeyframe { time = i / Rates[m], value = sprite }).ToArray());
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = motion == "Idle"; settings.startTime = 0; settings.stopTime = Count / Rates[m];
            AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
        }
        ImportAudio();
        AssetDatabase.SaveAssets();
        foreach (var pair in guids)
            if (AssetDatabase.AssetPathToGUID(pair.Key) != pair.Value) throw new InvalidOperationException("Existing GUID changed " + pair.Key);
        foreach (var pair in ids)
            foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(pair.Key).OfType<Sprite>())
                if (pair.Value.TryGetValue(sprite.name, out long oldId) && LocalId(sprite) != oldId)
                    throw new InvalidOperationException("Retained sprite fileID changed " + pair.Key + "/" + sprite.name);
        foreach (var pair in protectedFiles)
            if (!pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key))) throw new InvalidOperationException("Protected file changed " + pair.Key);
        return Validate();
    }

    public static void ImportAudio()
    {
        var importer = AssetImporter.GetAtPath(Shot) as AudioImporter;
        if (importer == null) throw new InvalidOperationException("Missing staged A-301 shot audio");
        importer.forceToMono = false; importer.loadInBackground = false;
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate; settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings; importer.SaveAndReimport();
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Shot);
        if (clip == null || clip.channels != 1 || clip.frequency != 44100 || Math.Abs(clip.length - .260f) > .001f)
            throw new InvalidOperationException("A-301 shot must be mono44100 PCM .260s");
    }

    static ISpriteEditorDataProvider RequireProvider(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing texture importer " + path);
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new InvalidOperationException("No sprite provider " + path);
        provider.InitSpriteEditorDataProvider();
        var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (capability == null) throw new InvalidOperationException("Missing sprite edit capability " + path);
        foreach (var edit in new[] { EEditCapability.EditSpriteName, EEditCapability.EditSpriteRect, EEditCapability.EditPivot,
            EEditCapability.EditBorder, EEditCapability.CreateAndDeleteSprite })
            if (!capability.GetEditCapability().HasCapability(edit)) throw new InvalidOperationException("Unsupported " + edit + " " + path);
        if (provider.GetDataProvider<ISpriteNameFileIdDataProvider>() == null) throw new InvalidOperationException("Missing fileID provider " + path);
        return provider;
    }

    static Sprite[] Slice(string path, string motion)
    {
        var provider = RequireProvider(path); var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var old = provider.GetSpriteRects();
        var rects = Enumerable.Range(0, Count).Select(i => {
            string name = motion + "_" + i; var prior = old.FirstOrDefault(r => r.name == name);
            return new SpriteRect { name = name, rect = new Rect(i * 512, 0, 512, 512),
                pivot = new Vector2(.5f, .5f), alignment = (int)SpriteAlignment.Center, border = Vector4.zero,
                spriteID = prior == null ? GUID.Generate() : prior.spriteID };
        }).ToArray();
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100; importer.maxTextureSize = 4096; importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.isReadable = false;
        importer.ClearPlatformTextureSettings("Standalone"); importer.ClearPlatformTextureSettings("Android");
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();
    }

    public static void ValidateSource(string path)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(path), false) || texture.width != Count * 512 || texture.height != 512)
                throw new InvalidOperationException("Expected 8 cells of 512x512: " + path);
            var pixels = texture.GetPixels32();
            for (int f = 0; f < Count; f++)
            {
                int xMin = 512, yMin = 512, xMax = -1, yMax = -1;
                for (int y = 0; y < 512; y++) for (int x = 0; x < 512; x++)
                {
                    if (pixels[y * texture.width + f * 512 + x].a < 16) continue;
                    xMin = Math.Min(xMin, x); xMax = Math.Max(xMax, x); yMin = Math.Min(yMin, y); yMax = Math.Max(yMax, y);
                }
                // Texture pixel origin is bottom-left. Intentional hover can lift above the lowest 16px envelope.
                if (xMax < 0 || xMin < 8 || xMax > 503 || yMax > 503 || yMin < 14 || yMin > 40)
                    throw new InvalidOperationException("Incomplete/overflowing pose or bad baseline " + path + " #" + f +
                        " [" + xMin + "," + yMin + "]-[" + xMax + "," + yMax + "]");
            }
        }
        finally { Object.DestroyImmediate(texture); }
    }

    public static string Validate()
    {
        var entries = new List<string>();
        for (int m = 0; m < Motions.Length; m++)
        {
            string motion = Motions[m], path = Folder + motion + ".png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + motion + ".anim");
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
            if (texture.width != 4096 || texture.height != 512 || sprites.Length != Count ||
                sprites.Any(s => s.rect.size != new Vector2(512, 512) || s.pivot != new Vector2(256, 256)) ||
                keys == null || keys.Length != Count || Enumerable.Range(0, Count).Any(i => keys[i].value != sprites[i]) ||
                clip.frameRate != Rates[m] || Math.Abs(clip.length - Count / Rates[m]) > .001f ||
                AnimationUtility.GetAnimationClipSettings(clip).loopTime != (motion == "Idle"))
                throw new InvalidOperationException("Imported motion mismatch " + motion);
            entries.Add(motion + " 8x512px @" + Rates[m] + "fps, duration " + clip.length);
        }
        entries.Add("Existing PNG/clip GUIDs and named sprite fileIDs preserved; controller/card/protected motions/aseprite sources byte preserved.");
        var shot = AssetDatabase.LoadAssetAtPath<AudioClip>(Shot);
        if (shot == null || shot.channels != 1 || shot.frequency != 44100 || Math.Abs(shot.length - .260f) > .001f)
            throw new InvalidOperationException("Imported shot audio mismatch");
        entries.Add("LaserShot mono44100 PCM .260s, decompress on load/preloaded.");
        return string.Join("; ", entries);
    }

    static long LocalId(Object asset)
    {
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long id)) throw new InvalidOperationException("No localID " + asset.name);
        return id;
    }
}
