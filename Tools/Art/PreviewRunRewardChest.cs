using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Draft;

// Isolated, unsaved uGUI snapshots only. Demo receipts never enter account/economy state.
public static class PreviewRunRewardChest
{
    private const string ProofFolder = "../RunRewardsChest_20261001";
    private static readonly string[] ScriptPaths =
    {
        "Assets/Project333/Runtime/Presentation/Draft/RunRewardRevealView.cs",
        "Assets/Project333/Runtime/Presentation/Draft/RunRewardDisplayContent.cs",
        "Assets/Project333/Runtime/Application/Accounts/PendingRunRewardPresentation.cs",
        "Assets/Tests/RunRewardRevealViewTests.cs",
        "Assets/Tests/RunRewardDisplayContentTests.cs",
        "Assets/Tests/PendingRunRewardPresentationTests.cs",
        "Assets/Tests/GameStartRewardRecoveryTests.cs",
    };

    private static string Hash(string path)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }
    [Serializable] public sealed class NativeScript
    {
        public string Path, Guid, SourceSha256, MetaSha256;
        public bool MonoImporter, NativeScriptLoaded, Idempotent;
    }
    [Serializable] public sealed class NativeProof
    {
        public NativeScript[] Scripts;
        public bool ScenesPreserved, SourcesPreserved;
        public string[] Previews;
    }

    public static void Batch()
    {
        var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        if (!root.StartsWith("C:/Project_333/TempBuild/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Reward preview must stay inside retained isolated TempBuild Editor.");
        Directory.CreateDirectory(Path.GetFullPath(ProofFolder));
        var scenes = Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories).ToArray();
        var sceneHashes = scenes.Select(Hash).ToArray();
        var sources = ScriptPaths.Select(Hash).ToArray();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var records = ScriptPaths.Select(path =>
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var guid = AssetDatabase.AssetPathToGUID(path);
            var initial = Hash(path + ".meta");
            var importer = AssetImporter.GetAtPath(path);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var record = new NativeScript
            {
                Path = path, Guid = guid, SourceSha256 = Hash(path), MetaSha256 = Hash(path + ".meta"),
                MonoImporter = importer is MonoImporter, NativeScriptLoaded = script != null,
                Idempotent = Hash(path + ".meta") == initial && AssetDatabase.AssetPathToGUID(path) == guid
            };
            if (!record.MonoImporter || !record.NativeScriptLoaded || !record.Idempotent ||
                string.IsNullOrEmpty(guid))
                throw new InvalidOperationException("Native script metadata validation failed " + path);
            return record;
        }).ToArray();
        File.WriteAllText(Path.GetFullPath(ProofFolder + "/reward-native-script-metadata.json"),
            JsonUtility.ToJson(new NativeProof { Scripts = records,
                SourcesPreserved = ScriptPaths.Select(Hash).SequenceEqual(sources),
                ScenesPreserved = scenes.Select(Hash).SequenceEqual(sceneHashes) }, true));
        ImportRunRewardChestAssets.Batch();
        var previews = new System.Collections.Generic.List<string>();
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1600, 1200) })
            previews.AddRange(Render(size.x, size.y));
        var proof = new NativeProof
        {
            Scripts = records, ScenesPreserved = scenes.Select(Hash).SequenceEqual(sceneHashes),
            SourcesPreserved = ScriptPaths.Select(Hash).SequenceEqual(sources), Previews = previews.ToArray()
        };
        File.WriteAllText(Path.GetFullPath(ProofFolder + "/reward-native-scripts-previews.json"), JsonUtility.ToJson(proof, true));
        if (!proof.ScenesPreserved || !proof.SourcesPreserved) throw new InvalidOperationException("Preview changed source or scenes.");
        Debug.Log("PASS reward UI native metadata/preview: 7 native scripts, stable ids, unsaved Canvas/camera captures, sources/scenes preserved.");
    }

    private static string[] Render(int width, int height)
    {
        var cameraObject = new GameObject("UnsavedRunRewardPreviewCamera");
        var camera = cameraObject.AddComponent<Camera>();
        cameraObject.transform.position = new Vector3(0, 0, -1000);
        camera.orthographic = true;
        camera.orthographicSize = height / 2f;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 2000f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.17f, .22f, .28f, 1);
        camera.cullingMask = 1 << 5;
        var render = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        render.Create();
        camera.targetTexture = render;
        var canvasObject = new GameObject("UnsavedRunRewardPreviewCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.layer = 5;
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 100;
        canvas.scaleFactor = 1;
        var rect = (RectTransform)canvas.transform;
        rect.sizeDelta = new Vector2(width, height);
        var view = RunRewardRevealView.Create(rect, null, null);
        foreach (var transform in canvasObject.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 5;
        var paths = new System.Collections.Generic.List<string>();
        var suffix = width + "x" + height;
        try
        {
            view.ShowClosed(33, 2, true);
            paths.Add(Capture(camera, render, "closed-" + suffix));
            view.TryBeginOpening();
            Advance(view, .5f);
            paths.Add(Capture(camera, render, "opening-" + suffix));
            Advance(view, .8f);
            paths.Add(Capture(camera, render, "open-waiting-" + suffix));
            var cards = JsonCardDefinitionDatabase.FromJson(Resources.Load<TextAsset>("Project333/Data/cards").text).Cards
                .Where(card => card.IncludeInRewards).Take(41).ToArray();
            var receipt = RunRewardDisplayContent.Build(new RewardGrantDto
            {
                ResourceGoldDelta = 99,
                TicketDelta = 1,
                CardRewardItems = cards.Select((card, index) => new RewardItemDto
                    { Id = card.Id, Count = index < 17 ? 3 : 2 }).ToArray()
            }, 33, 2, 12345, id => cards.First(card => card.Id == id).DisplayName);
            view.SetReceipt(receipt);
            paths.Add(Capture(camera, render, "receipt-" + suffix));
            var scroll = view.GetComponentsInChildren<ScrollRect>(true).Single();
            scroll.verticalNormalizedPosition = 0;
            paths.Add(Capture(camera, render, "receipt-bottom-" + suffix));
            view.Hide();
            view.ShowClosed(0, 3, true);
            view.TryBeginOpening();
            view.SetReceipt(RunRewardDisplayContent.Build(new RewardGrantDto(), 0, 3, 0));
            Advance(view, 1.2f);
            paths.Add(Capture(camera, render, "zero-reward-" + suffix));
            return paths.ToArray();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(canvasObject);
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(cameraObject);
            render.Release();
            UnityEngine.Object.DestroyImmediate(render);
        }
    }

    private static void Advance(RunRewardRevealView view, float time) =>
        typeof(RunRewardRevealView).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(view, new object[] { time });

    private static string Capture(Camera camera, RenderTexture render, string name)
    {
        Canvas.ForceUpdateCanvases();
        var previewCanvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Single(canvas => canvas.worldCamera == camera);
        foreach (var text in previewCanvas.GetComponentsInChildren<Text>())
            text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
        Canvas.ForceUpdateCanvases();
        // Synchronous Editor captures do not run the runtime LateUpdate that
        // normally refreshes scrollbar bounds/handles after content changes.
        foreach (var scroll in previewCanvas.GetComponentsInChildren<ScrollRect>())
        {
            typeof(ScrollRect).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scroll, null);
            if (scroll.content.rect.width > scroll.viewport.rect.width + .1f)
                throw new InvalidOperationException("Reward content exceeds visible viewport width.");
            var viewportCorners = new Vector3[4];
            scroll.viewport.GetWorldCorners(viewportCorners);
            foreach (var text in scroll.content.GetComponentsInChildren<Text>())
            {
                var corners = new Vector3[4];
                text.rectTransform.GetWorldCorners(corners);
                if (corners[0].x < viewportCorners[0].x - .1f || corners[2].x > viewportCorners[2].x + .1f)
                    throw new InvalidOperationException("Reward name/count rect clips horizontally: " + text.name);
            }
        }
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = render;
        var capture = new Texture2D(render.width, render.height, TextureFormat.RGBA32, false);
        try
        {
            capture.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0);
            capture.Apply();
            var path = Path.GetFullPath(ProofFolder + "/" + name + ".png");
            File.WriteAllBytes(path, capture.EncodeToPNG());
            return path;
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(capture);
        }
    }
}

