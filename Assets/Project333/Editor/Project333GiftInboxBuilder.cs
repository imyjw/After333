using System;
using System.IO;
using System.Linq;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation.Settings;
using Project333.Runtime.Presentation.Startup;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Project333.Editor
{
    public static class Project333GiftInboxBuilder
    {
        private const string ScenePath = "Assets/GameStart_VSlice.unity";

        [MenuItem("Tools/Project333/Start/Install Gift Inbox")]
        public static void InstallGiftInbox()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var closeAfter = !scene.IsValid() || !scene.isLoaded;
            if (closeAfter) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var wasDirty = scene.isDirty;
            GlobalSettingsMenuController.EnsureForScene(scene);
            GiftInboxController.EnsureForScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            // Keep other unsaved user edits under the user's normal scene-save control.
            if (closeAfter || !wasDirty) EditorSceneManager.SaveScene(scene);
            if (closeAfter) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            Debug.Log("After333 start scene gift inbox installed.");
        }

        [MenuItem("Tools/Project333/Start/Capture Gift Inbox Preview")]
        public static void CaptureGiftInboxPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Preview is edit-mode only.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Preview rendering needs a graphics device; omit -nographics.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var closeAfter = !scene.IsValid() || !scene.isLoaded;
            if (closeAfter) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var inbox = GiftInboxController.EnsureForScene(scene);
            var folder = Path.GetFullPath("TempBuild/GiftInbox_20260913");
            Directory.CreateDirectory(folder);
            try
            {
                inbox.Close();
                Render(scene, Path.Combine(folder, "start-screen.png"));
                inbox.SetEditorPreview(new[]
                {
                    new GiftInboxItemDto { GiftId = "preview-1", Title = "After333 비공개 테스트 선물", Message = "테스트에 참여해주셔서 감사합니다. 게임 티켓과 골드를 선물함에서 받아보세요.", Tickets = 3, ResourceGold = 1000, Status = "unclaimed" },
                    new GiftInboxItemDto { GiftId = "preview-2", Title = "감사의 선물", Message = "즐거운 전투 되세요!", Tickets = 1, ExpiresAtUtc = "2026-09-30T14:59:00Z", Status = "unclaimed" },
                    new GiftInboxItemDto { GiftId = "preview-3", Title = "지난 선물", ResourceGold = 300, Status = "claimed" }
                }, 2);
                Render(scene, Path.Combine(folder, "gift-inbox.png"));
                Debug.Log("After333 gift inbox preview images: " + folder);
            }
            finally
            {
                inbox.SetEditorPreview(Array.Empty<GiftInboxItemDto>(), 0);
                inbox.Close();
                if (closeAfter) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void Render(Scene scene, string output)
        {
            var cameraObject = new GameObject("GiftPreviewCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -100);
            camera.orthographic = true;
            camera.orthographicSize = 540;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.06f, 0.075f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000;
            var target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;
            var canvases = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Canvas>(true)).Where(x => x.isRootCanvas).ToArray();
            var modes = canvases.Select(x => x.renderMode).ToArray();
            var cameras = canvases.Select(x => x.worldCamera).ToArray();
            var planes = canvases.Select(x => x.planeDistance).ToArray();
            var originalTarget = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                for (var i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[i].worldCamera = camera;
                    canvases[i].planeDistance = 10f;
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                texture.Apply();
                File.WriteAllBytes(output, texture.EncodeToPNG());
            }
            finally
            {
                for (var i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = modes[i];
                    canvases[i].worldCamera = cameras[i];
                    canvases[i].planeDistance = planes[i];
                }
                RenderTexture.active = originalTarget;
                camera.targetTexture = null;
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
