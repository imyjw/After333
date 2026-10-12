// One-time authoring import. Copy this file into an isolated Unity project's Assets/Editor,
// stage the reviewed PNGs with their original metadata, then call Run through Unity eval.
// No automatic hooks; never rerun over an artist's later edits.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ImportRedDragonPresentation
{
    private const string Folder = "Assets/Unit/RedDragon/";
    private const string CardPath = "Assets/Project333/ScriptableObjects/StarterTen/Cards/RedDragon.asset";
    private const string ControllerGuid = "96d2d0d396818d24dbc6380f8d565c28";
    private static readonly string[] Motions = { "Idle", "Attack", "BeAttacked", "Run", "Death" };
    private static readonly float[] Rates = { 6f, 12f, 15f, 12f, 8f };

    public static string Run() => RunWithSpecs(new[] { 6, 6, 6, 6, 6 }, Rates);

    // Count/rate order is Idle, Attack, BeAttacked, Run, Death. Each sheet is one row.
    public static string RunWithSpecs(int[] counts, float[] rates)
    {
        if (counts == null || rates == null || counts.Length != 5 || rates.Length != 5)
            throw new ArgumentException("Exactly five frame counts and frame rates are required.");
        var controllerPath = Folder + "RedDragon.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null || controller.layers.Length != 1 || AssetDatabase.AssetPathToGUID(controllerPath) != ControllerGuid)
            throw new InvalidOperationException("Stage the existing RedDragon controller and its original meta first.");
        var machine = controller.layers[0].stateMachine;
        var preserved = new Dictionary<string, string>();
        var originalIds = new Dictionary<string, Dictionary<string, long>>();
        byte[] cardBefore = File.Exists(CardPath) ? File.ReadAllBytes(CardPath) : null;

        // Check every source image and every provider BEFORE touching any importer or clip.
        for (int m = 0; m < Motions.Length; m++)
        {
            if (counts[m] < 1 || counts[m] > 16 || rates[m] <= 0 || float.IsNaN(rates[m]))
                throw new ArgumentException("Unsupported count/rate: " + Motions[m]);
            string png = Folder + Motions[m] + ".png", clip = Folder + Motions[m] + ".anim";
            ValidateSource(png, counts[m]);
            RequireProvider(png);
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clip) == null)
                throw new InvalidOperationException("Missing existing clip: " + clip);
            preserved[png] = AssetDatabase.AssetPathToGUID(png);
            preserved[clip] = AssetDatabase.AssetPathToGUID(clip);
            originalIds[png] = AssetDatabase.LoadAllAssetsAtPath(png).OfType<Sprite>().ToDictionary(s => s.name, LocalId);
        }
        preserved[controllerPath] = AssetDatabase.AssetPathToGUID(controllerPath);
        Sprite idleFirst = null;
        for (int m = 0; m < Motions.Length; m++)
        {
            string motion = Motions[m];
            var frames = Slice(Folder + motion + ".png", motion, counts[m]);
            var clip = SetClip(Folder + motion + ".anim", motion, frames, rates[m], motion == "Idle" || motion == "Run");
            SetState(machine, motion, clip);
            if (motion == "Idle") idleFirst = frames[0];
        }
        SetState(machine, "Drained", SetClip(Folder + "Drained.anim", "Drained", new[] { idleFirst }, 1f, true));
        machine.defaultState = machine.states.Single(s => s.state.name == "Idle").state;
        EditorUtility.SetDirty(machine); EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        foreach (var pair in preserved)
            if (AssetDatabase.AssetPathToGUID(pair.Key) != pair.Value)
                throw new InvalidOperationException("An existing GUID changed: " + pair.Key);
        foreach (var pair in originalIds)
            foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(pair.Key).OfType<Sprite>())
                if (pair.Value.TryGetValue(sprite.name, out long oldId) && LocalId(sprite) != oldId)
                    throw new InvalidOperationException("A retained sprite fileID changed: " + pair.Key + "/" + sprite.name);
        if (cardBefore != null && !cardBefore.SequenceEqual(File.ReadAllBytes(CardPath)))
            throw new InvalidOperationException("The card asset must remain byte-identical.");
        return "RedDragon: five 512px motion clips plus held Drained, original GUIDs and retained sprite fileIDs preserved. Card unchanged.";
    }

    public static void ValidateSource(string path, int count)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(path), false) || texture.width != count * 512 || texture.height != 512)
                throw new InvalidOperationException("Expected a single row of " + count + " 512x512 cells: " + path);
            var pixels = texture.GetPixels32();
            for (int f = 0; f < count; f++)
            {
                int xMin = 512, yMin = 512, xMax = -1, yMax = -1;
                for (int y = 0; y < 512; y++) for (int x = 0; x < 512; x++)
                {
                    if (pixels[y * texture.width + f * 512 + x].a < 16) continue;
                    xMin = Math.Min(xMin, x); xMax = Math.Max(xMax, x);
                    yMin = Math.Min(yMin, y); yMax = Math.Max(yMax, y);
                }
                if (xMax < 0) throw new InvalidOperationException("Empty frame: " + path + " #" + f);
                // Transparent edges are an overflow warning check, not a substitute for visual review.
                if (xMin < 1 || xMax > 510 || yMax > 510 || yMin < 8 || yMin > 24)
                    throw new InvalidOperationException("Frame needs ~16px bottom margin and clear outer edges: " + path + " #" + f +
                        " bounds [" + xMin + "," + yMin + "]-[" + xMax + "," + yMax + "]");
            }
        }
        finally { Object.DestroyImmediate(texture); }
    }

    private static ISpriteEditorDataProvider RequireProvider(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing texture importer: " + path);
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new InvalidOperationException("No sprite data provider: " + path);
        provider.InitSpriteEditorDataProvider();
        var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (capability == null) throw new InvalidOperationException("Missing sprite edit capabilities: " + path);
        foreach (var operation in new[] { EEditCapability.EditSpriteName, EEditCapability.EditSpriteRect,
            EEditCapability.EditPivot, EEditCapability.EditBorder, EEditCapability.CreateAndDeleteSprite })
            if (!capability.GetEditCapability().HasCapability(operation))
                throw new InvalidOperationException("Sprite operation unavailable: " + operation + " " + path);
        if (provider.GetDataProvider<ISpriteNameFileIdDataProvider>() == null)
            throw new InvalidOperationException("Sprite name/fileID provider missing: " + path);
        return provider;
    }

    private static Sprite[] Slice(string path, string motion, int count)
    {
        var provider = RequireProvider(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var previous = provider.GetSpriteRects();
        var rects = new SpriteRect[count];
        for (int i = 0; i < count; i++)
        {
            string name = motion + "_" + i;
            var old = previous.FirstOrDefault(r => r.name == name);
            rects[i] = new SpriteRect { name = name, rect = new Rect(i * 512, 0, 512, 512),
                pivot = new Vector2(.5f, .5f), alignment = (int)SpriteAlignment.Center,
                border = Vector4.zero, spriteID = old == null ? GUID.Generate() : old.spriteID };
        }
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100; importer.maxTextureSize = Mathf.NextPowerOfTwo(count * 512);
        importer.npotScale = TextureImporterNPOTScale.None; importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = false;
        importer.ClearPlatformTextureSettings("Standalone"); importer.ClearPlatformTextureSettings("Android");
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();
        if (texture.width != count * 512 || texture.height != 512 || frames.Length != count || frames.Any(s => s.rect.size != new Vector2(512, 512)))
            throw new InvalidOperationException("Post-import frame size/count mismatch: " + path);
        return frames;
    }

    private static AnimationClip SetClip(string path, string name, Sprite[] frames, float fps, bool loop)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, path); }
        clip.frameRate = fps;
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"),
            frames.Select((sprite, index) => new ObjectReferenceKeyframe { time = index / fps, value = sprite }).ToArray());
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop; settings.startTime = 0; settings.stopTime = frames.Length / fps;
        AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void SetState(AnimatorStateMachine machine, string name, AnimationClip clip)
    {
        var state = machine.states.SingleOrDefault(s => s.state.name == name).state;
        if (state == null) state = machine.AddState(name);
        state.motion = clip; state.speed = 1f; EditorUtility.SetDirty(state);
    }

    private static long LocalId(Object asset)
    {
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long id))
            throw new InvalidOperationException("Missing asset identity: " + asset.name);
        return id;
    }
}