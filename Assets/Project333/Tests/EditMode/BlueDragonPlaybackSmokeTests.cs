using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Presentation.Battle;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class BlueDragonPlaybackSmokeTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type CannonType => typeof(TileTextView).Assembly.GetType(
            "Project333.Runtime.Presentation.Battle.BlueDragonWaterCannon", true);

        [TearDown]
        public void CleanupCardFixtures() => BoardAnimationControllerTestUtility.Cleanup();

        [UnityTest]
        public IEnumerator WaterCannon_RuntimeFlightMirrorsFromMouthAndSplashesOnlyOnArrival()
        {
            // No gameplay scene, bootstrapper, account client, gateway or server is created.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return RunIsolatedPlayback();
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator LeavePlayModeOnFailure()
        {
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        // Keep runtime objects and coroutine state outside the iterator crossing domain reload.
        private static IEnumerator RunIsolatedPlayback()
        {
            var fixture = new GameObject("BlueDragonIsolatedPlayback");
            RenderTexture renderTarget = null;
            try
            {
                var cameraObject = new GameObject("PreviewCamera", typeof(Camera), typeof(AudioListener));
                cameraObject.transform.SetParent(fixture.transform);
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.075f, 0.12f);
                camera.orthographic = true;
                camera.orthographicSize = 5;
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    renderTarget = new RenderTexture(1000, 560, 24);
                    renderTarget.Create();
                    camera.targetTexture = renderTarget;
                }

                var left = CreateTileShell(fixture.transform, camera, "LeftDragon", PlayerId.Player, -230f);
                var right = CreateTileShell(fixture.transform, camera, "RightDragon", PlayerId.AI, 230f);
                var effect = (MonoBehaviour)new GameObject("CannonCoroutineHost").AddComponent(CannonType);
                effect.transform.SetParent(fixture.transform);
                Canvas.ForceUpdateCanvases();
                yield return null;

                Assert.That(left.GetComponentInParent<Canvas>(), Is.Null,
                    "Reproduce real tile hierarchy: TileTextView lives above its child Canvas.");
                Assert.That(right.GetComponentInParent<Canvas>(), Is.Null);
                Assert.That(left.VisualRuntimeId, Is.EqualTo("smoke-LeftDragon"));
                Assert.That(right.VisualRuntimeId, Is.EqualTo("smoke-RightDragon"));

                yield return VerifyFlight(effect, left, right, camera, renderTarget, "right");
                yield return new WaitForSecondsRealtime(0.45f);
                yield return VerifyFlight(effect, right, left, camera, renderTarget, "left");

                CannonType.GetMethod("Cancel", Hidden).Invoke(effect, null);
                foreach (var image in fixture.GetComponentsInChildren<Image>(true))
                    if (image.name == "WaterCannonProjectile" || image.name == "WaterCannonImpact")
                        Assert.That(image.gameObject.activeSelf, Is.False, "Cancel hides active water visuals.");
                Assert.That(effect.GetComponent<AudioSource>().isPlaying, Is.False, "Cancel stops sound.");
            }
            finally
            {
                Object.DestroyImmediate(fixture);
                if (renderTarget != null)
                {
                    renderTarget.Release();
                    Object.DestroyImmediate(renderTarget);
                }
            }
        }

        private static IEnumerator VerifyFlight(MonoBehaviour effect, TileTextView source, TileTextView target,
            Camera camera, RenderTexture renderTarget, string direction)
        {
            var mouth = source.GetVisualNormalizedWorldPosition(new Vector2(0.90f, 0.52f));
            var center = source.GetVisualNormalizedWorldPosition(new Vector2(0.5f, 0.52f));
            Assert.That(Mathf.Sign(mouth.x - center.x), Is.EqualTo(direction == "right" ? 1f : -1f),
                "Mouth socket must mirror with the actual occupant root.");
            var arrival = target.GetVisualImpactWorldPosition();
            var enumerator = (IEnumerator)CannonType.GetMethod("PlayFlight", Hidden)
                .Invoke(effect, new object[] { source, target });
            var finished = new Completion();
            effect.StartCoroutine(CompleteFlight(enumerator, finished));

            var projectile = FindImage(effect.transform.parent, "WaterCannonProjectile");
            var impact = FindImage(effect.transform.parent, "WaterCannonImpact");
            Assert.That(projectile.gameObject.activeInHierarchy, Is.True);
            Assert.That(impact.gameObject.activeSelf, Is.False, "No splash before arrival.");
            Assert.That(projectile.sprite, Is.Not.Null);
            Assert.That(projectile.sprite.texture.name, Is.EqualTo("WaterAtlas"));
            Assert.That(Vector3.Distance(projectile.rectTransform.position, mouth), Is.LessThan(0.01f),
                "Projectile front pivot starts at the source mouth.");
            Assert.That(projectile.rectTransform.pivot, Is.EqualTo(new Vector2(0.88f, 0.5f)));
            var towardTarget = (arrival - mouth).normalized;
            Assert.That(Vector3.Dot(projectile.rectTransform.right.normalized, towardTarget),
                Is.GreaterThan(0.999f), "Projectile faces its destination in either direction.");

            var audio = effect.GetComponent<AudioSource>();
            Assert.That(audio, Is.Not.Null);
            Assert.That(audio.spatialBlend, Is.Zero);
            // Stop the launch one-shot. Arrival must not add a separate victim impact cue.
            var audioDevicePlays = audio.isPlaying;
            audio.Stop();
            Assert.That(audio.isPlaying, Is.False);
            var deadline = Time.realtimeSinceStartup + 3f;
            var capturedFlight = false;
            while (!finished.Value && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(impact.gameObject.activeSelf, Is.False, "Splash cannot start while travel is running.");
                yield return null;
                if (!capturedFlight && !finished.Value && projectile.gameObject.activeSelf)
                {
                    Capture(camera, renderTarget, "water-flight-" + direction + ".png");
                    capturedFlight = true;
                }
            }
            Assert.That(finished.Value, Is.True, "Flight must complete within the watchdog.");
            Assert.That(projectile.gameObject.activeSelf, Is.False);
            Assert.That(impact.gameObject.activeInHierarchy, Is.True, "Arrival starts splash in the same frame.");
            Assert.That(impact.sprite, Is.Not.Null);
            Assert.That(impact.sprite.texture.name, Is.EqualTo("WaterAtlas"));
            Assert.That(impact.rectTransform.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(Vector3.Distance(impact.rectTransform.position, arrival), Is.LessThan(0.01f));
            Assert.That(audio.isPlaying, Is.False,
                "Water splash must not restart a per-character impact sound; TileTextView owns the common hit cue.");
            if (!audioDevicePlays)
                TestContext.Progress.WriteLine("Audio backend unavailable: launch audibility was not asserted.");
            Capture(camera, renderTarget, "water-impact-" + direction + ".png");
        }

        private sealed class Completion { public bool Value; }
        private static IEnumerator CompleteFlight(IEnumerator flight, Completion finished)
        {
            while (flight.MoveNext()) yield return flight.Current;
            finished.Value = true;
        }

        private static TileTextView CreateTileShell(Transform fixture, Camera camera, string label,
            PlayerId owner, float x)
        {
            var shell = new GameObject(label);
            shell.SetActive(false);
            shell.transform.SetParent(fixture);
            var tile = shell.AddComponent<TileView>();
            tile.Configure(owner, 0, 0);
            tile.Present(new UnitState("smoke-" + label, "BlueDragon", owner, new TileCoord(0, 0),
                AttackType.Ranged, 40, 50, true, false, 0));
            var view = shell.AddComponent<TileTextView>();
            view.enabled = false; // Exercise presentation only, without LateUpdate bindings or battle state.
            var canvasObject = new GameObject("TileCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(shell.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            canvas.sortingOrder = 100;
            var labelObject = new GameObject("TileText", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(canvasObject.transform, false);
            var text = labelObject.GetComponent<TextMeshProUGUI>();
            text.enabled = false;
            var visual = new GameObject("OccupantVisual", typeof(RectTransform), typeof(Image), typeof(Animator));
            visual.transform.SetParent(canvasObject.transform, false);
            var rect = visual.GetComponent<RectTransform>();
            Set(view, "_tileView", tile);
            Set(view, "_text", text);
            Set(view, "_showOccupantStats", false);
            Set(view, "_showHuanShuStatusIcon", false);
            Set(view, "_useTileImageClickSurface", false);
            Set(view, "_occupantVisualRoot", rect);
            Set(view, "_occupantVisualImage", visual.GetComponent<Image>());
            Set(view, "_occupantVisualAnimator", visual.GetComponent<Animator>());
            BoardAnimationControllerTestUtility.Bind(view, "BlueDragon");
            view.SetTile(label, "BlueDragon", true);
            shell.SetActive(true);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, -20);
            rect.sizeDelta = new Vector2(240, 240);
            rect.localScale = new Vector3(owner == PlayerId.Player ? 1 : -1, 1, 1);
            view.PlayIdleAnimation();
            Assert.That(visual.GetComponent<Image>().sprite.texture.name, Is.EqualTo("Idle"));
            return view;
        }

        private static Image FindImage(Transform root, string name)
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                if (image.name == name) return image;
            Assert.Fail("Missing runtime water image: " + name);
            return null;
        }

        private static void Set(object instance, string field, object value) =>
            instance.GetType().GetField(field, Hidden).SetValue(instance, value);

        private static void Capture(Camera camera, RenderTexture target, string fileName)
        {
            if (target == null) return; // -nographics still validates lifecycle and positions.
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var prior = RenderTexture.active;
            Texture2D image = null;
            try
            {
                RenderTexture.active = target;
                image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                var output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                    "../TempBuild/BlueDragonPresentation_20260926"));
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, fileName), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = prior;
                if (image != null) Object.DestroyImmediate(image);
            }
        }
    }
}
