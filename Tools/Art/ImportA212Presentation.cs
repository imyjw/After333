// One-time authoring tool; stage in an isolated Editor and call Run via Unity CLI eval.
// Never rerun over later artist edits. Sprite metadata is edited exclusively through Unity providers.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ImportA212Presentation
{
    const string Folder = "Assets/Unit/A-212/";
    const string Card = "Assets/Project333/ScriptableObjects/StarterTen/Cards/A-212.asset";
    const string Extras = "Assets/Project333/Resources/Project333/Units/A-212/";
    static readonly string[] Motions = { "Attack", "Run", "Death" };
    static readonly float[] Rates = { 8f, 12f, 8f };

    public static string Run()
    {
        var controllerPath = Folder + "A-212.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null || controller.layers.Length != 1 ||
            AssetDatabase.AssetPathToGUID(controllerPath) != "c9d0ec2e25d93f1448856b0001a54768")
            throw new InvalidOperationException("Stage original A-212 controller and metadata first.");
        var retained = new Dictionary<string, byte[]>();
        foreach (var p in new[] { "Idle.png", "Idle.png.meta", "Idle.anim", "Idle.anim.meta",
            "BeAttacked.png", "BeAttacked.png.meta", "BeAttacked.anim", "BeAttacked.anim.meta", "A-212.controller.meta" })
            retained[Folder + p] = File.ReadAllBytes(Folder + p);
        retained[Card] = File.ReadAllBytes(Card);
        var guids = new Dictionary<string, string>();
        var ids = new Dictionary<string, Dictionary<string, long>>();
        foreach (var motion in Motions)
        {
            string png = Folder + motion + ".png", anim = Folder + motion + ".anim";
            ImportRedDragonPresentation.ValidateSource(png, 8);
            RequireProvider(png);
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(anim) == null)
                throw new InvalidOperationException("Missing existing clip " + anim);
            guids[png] = AssetDatabase.AssetPathToGUID(png);
            guids[anim] = AssetDatabase.AssetPathToGUID(anim);
            ids[png] = AssetDatabase.LoadAllAssetsAtPath(png).OfType<Sprite>().ToDictionary(s => s.name, LocalId);
            if (!controller.layers[0].stateMachine.states.Any(s => s.state.name == motion))
                throw new InvalidOperationException("Missing original state " + motion);
        }
        for (int m = 0; m < Motions.Length; m++)
        {
            string name = Motions[m], path = Folder + name + ".png";
            var frames = Slice(path, name);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + name + ".anim");
            clip.frameRate = Rates[m];
            var keys = frames.Select((sprite, i) => new ObjectReferenceKeyframe { time = i / Rates[m], value = sprite }).ToList();
            // Unity sprite curves include the final frame's sampling interval in clip.length.
            // Eight keys at 0..7/fps therefore produce a duration of 8/fps.
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = name == "Run"; settings.startTime = 0; settings.stopTime = frames.Length / Rates[m];
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            var state = controller.layers[0].stateMachine.states.Single(s => s.state.name == name).state;
            // Existing controller already points to these same clip assets; no state layout or transitions changed.
            if (state.motion != clip || state.speed != 1f) throw new InvalidOperationException("Unexpected state configuration " + name);
        }
        ImportExtras();
        AssetDatabase.SaveAssets();
        foreach (var p in guids)
            if (AssetDatabase.AssetPathToGUID(p.Key) != p.Value) throw new InvalidOperationException("GUID changed " + p.Key);
        foreach (var pair in ids)
            foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(pair.Key).OfType<Sprite>())
                if (pair.Value.TryGetValue(sprite.name, out long old) && LocalId(sprite) != old)
                    throw new InvalidOperationException("Retained sprite fileID changed " + pair.Key + "/" + sprite.name);
        foreach (var p in retained)
            if (!p.Value.SequenceEqual(File.ReadAllBytes(p.Key))) throw new InvalidOperationException("Protected file changed " + p.Key);
        return Validate();
    }

    static ISpriteEditorDataProvider RequireProvider(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing texture importer " + path);
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new InvalidOperationException("No sprite provider " + path);
        provider.InitSpriteEditorDataProvider();
        var cap = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (cap == null) throw new InvalidOperationException("Missing capability provider " + path);
        foreach (var edit in new[] { EEditCapability.EditSpriteName, EEditCapability.EditSpriteRect,
            EEditCapability.EditPivot, EEditCapability.EditBorder, EEditCapability.CreateAndDeleteSprite })
            if (!cap.GetEditCapability().HasCapability(edit)) throw new InvalidOperationException("Unsupported " + edit + " " + path);
        if (provider.GetDataProvider<ISpriteNameFileIdDataProvider>() == null) throw new InvalidOperationException("Missing fileID provider " + path);
        return provider;
    }

    static Sprite[] Slice(string path, string motion)
    {
        var provider = RequireProvider(path); var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var old = provider.GetSpriteRects();
        var rects = Enumerable.Range(0, 8).Select(i => {
            var name = motion + "_" + i; var prior = old.FirstOrDefault(r => r.name == name);
            return new SpriteRect { name = name, rect = new Rect(i * 512, 0, 512, 512),
                pivot = new Vector2(.5f, .5f), alignment = (int)SpriteAlignment.Center, border = Vector4.zero,
                spriteID = prior == null ? GUID.Generate() : prior.spriteID };
        }).ToArray();
        ConfigureTexture(importer, 4096);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();
    }

    static void ConfigureTexture(TextureImporter importer, int maxSize)
    {
        importer.maxTextureSize = maxSize; importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.isReadable = false;
        importer.ClearPlatformTextureSettings("Standalone"); importer.ClearPlatformTextureSettings("Android");
    }

    static void ImportExtras()
    {
        string texture = Extras + "SawImpactAtlas.png", sound = Extras + "Audio/SawAttack.wav";
        var importer = AssetImporter.GetAtPath(texture) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing staged VFX");
        ConfigureTexture(importer, 2048); importer.textureType = TextureImporterType.Default; importer.SaveAndReimport();
        var audio = AssetImporter.GetAtPath(sound) as AudioImporter;
        if (audio == null) throw new InvalidOperationException("Missing staged attack sound");
        audio.forceToMono = false; audio.loadInBackground = false;
        var sample = audio.defaultSampleSettings; sample.loadType = AudioClipLoadType.DecompressOnLoad;
        sample.compressionFormat = AudioCompressionFormat.PCM; sample.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        sample.preloadAudioData = true;
        audio.defaultSampleSettings = sample; audio.SaveAndReimport();
    }

    public static string Validate()
    {
        var entries = new List<string>();
        for (int m = 0; m < Motions.Length; m++)
        {
            string name = Motions[m], path = Folder + name + ".png";
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + name + ".anim");
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (texture.width != 4096 || texture.height != 512 || sprites.Length != 8 ||
                sprites.Any(s => s.rect.size != new Vector2(512, 512)) || keys.Length != 8 ||
                Math.Abs(clip.length - 8 / Rates[m]) > .001f || settings.loopTime != (name == "Run") || clip.frameRate != Rates[m] ||
                Enumerable.Range(0, 8).Any(i => keys[i].value != sprites[i])) throw new InvalidOperationException("Imported motion mismatch " + name);
            entries.Add(name + " 8x512px @" + Rates[m] + "fps, duration " + clip.length);
        }
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Extras + "SawImpactAtlas.png");
        var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(Extras + "Audio/SawAttack.wav");
        if (atlas == null || atlas.width != 2048 || atlas.height != 1024 || sound == null ||
            sound.channels != 1 || sound.frequency != 44100 || Math.Abs(sound.length - .480f) > .001f)
            throw new InvalidOperationException("VFX/audio import mismatch");
        entries.Add("VFX 4x2 512px cells; SawAttack mono44100 PCM .480s; original GUID/fileIDs/controller/card/Idle/BeAttacked preserved.");
        return string.Join("; ", entries);
    }

    static long LocalId(Object asset)
    {
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long id)) throw new InvalidOperationException("No localID " + asset.name);
        return id;
    }
}
