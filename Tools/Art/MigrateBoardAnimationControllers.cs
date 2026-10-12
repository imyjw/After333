// One-time migration: run in a Unity Editor after copying the chosen 512px PNGs
// into Assets/Unit/<card>. This deliberately has no automatic import/menu hook.
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class MigrateBoardAnimationControllers
{
    private static readonly string[] Motions = { "Idle", "Attack", "BeAttacked", "Run", "Death" };
    private static readonly float[] Fps = { 6f, 12f, 15f, 12f, 8f };

    public static string Run()
    {
        foreach (var card in new[] { "BlueDragon", "Gwangma" })
        {
            string folder = "Assets/Unit/" + card + "/";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(folder + card + ".controller");
            if (controller == null || controller.layers.Length == 0) throw new Exception("Missing controller: " + card);
            var machine = controller.layers[0].stateMachine;
            for (int i = 0; i < Motions.Length; i++)
            {
                string motion = Motions[i];
                // Preserve the user's restored four-frame BlueDragon Idle, including its importer.
                if (card == "BlueDragon" && motion == "Idle") continue;
                var frames = ImportFrames(folder + motion + ".png", motion);
                var clip = SetClip(folder + motion + ".anim", motion, frames, Fps[i], motion == "Idle" || motion == "Run");
                SetState(machine, motion, clip);
            }
            var idle = AssetDatabase.LoadAllAssetsAtPath(folder + "Idle.png").OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();
            if (idle.Length == 0) throw new Exception("Idle frames missing: " + card);
            var drained = SetClip(folder + "Drained.anim", "Drained", new[] { idle[0] }, 1f, true);
            SetState(machine, "Drained", drained);
            var idleState = machine.states.FirstOrDefault(s => s.state.name == "Idle").state;
            if (idleState == null) throw new Exception("Idle state missing: " + card);
            machine.defaultState = idleState;
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
        }
        AssetDatabase.SaveAssets();
        return "Both controllers now reference the 512px motion clips, with original BlueDragon Idle preserved and Drained freeze states added.";
    }

    private static Sprite[] ImportFrames(string path, string motion)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new Exception("Missing texture importer: " + path);
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new Exception("Sprite data provider unavailable: " + path);
        provider.InitSpriteEditorDataProvider();
        var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (capability == null) throw new Exception("Sprite edit capability unavailable: " + path);
        foreach (var required in new[] { EEditCapability.EditSpriteName, EEditCapability.EditSpriteRect,
            EEditCapability.EditPivot, EEditCapability.EditBorder, EEditCapability.CreateAndDeleteSprite })
            if (!capability.GetEditCapability().HasCapability(required)) throw new Exception("Unsupported sprite edit: " + required);
        var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (names == null) throw new Exception("Sprite name/fileID provider unavailable: " + path);
        var previous = provider.GetSpriteRects();
        var rects = new SpriteRect[6];
        for (int i = 0; i < rects.Length; i++)
        {
            string name = motion + "_" + i;
            var existing = previous.FirstOrDefault(r => r.name == name);
            rects[i] = new SpriteRect { name = name, rect = new Rect(i * 512, 0, 512, 512),
                pivot = new Vector2(0.5f, 0.5f), alignment = (int)SpriteAlignment.Center,
                border = Vector4.zero, spriteID = existing == null ? GUID.Generate() : existing.spriteID };
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        importer.maxTextureSize = 4096;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.ClearPlatformTextureSettings("Standalone");
        importer.ClearPlatformTextureSettings("Android");
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        provider.SetSpriteRects(rects);
        names.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture.width != 3072 || texture.height != 512) throw new Exception("Wrong import size: " + path);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();
        if (sprites.Length != 6 || sprites.Any(s => s.rect.size != new Vector2(512, 512))) throw new Exception("Wrong sprite slicing: " + path);
        return sprites;
    }

    private static AnimationClip SetClip(string path, string name, Sprite[] frames, float fps, bool loop)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, path); }
        clip.frameRate = fps;
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = frames.Select((sprite, index) => new ObjectReferenceKeyframe { time = index / fps, value = sprite }).ToArray();
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop; settings.startTime = 0; settings.stopTime = frames.Length / fps;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void SetState(AnimatorStateMachine machine, string name, AnimationClip clip)
    {
        var state = machine.states.FirstOrDefault(s => s.state.name == name).state;
        if (state == null) state = machine.AddState(name);
        state.motion = clip;
        state.speed = 1;
        EditorUtility.SetDirty(state);
    }
}