using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Presentation.Battle;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class GwangmaPresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const string ResourceRoot = "Project333/Presentation/Gwangma/";
        private static Type AssetsType => typeof(TileTextView).Assembly.GetType("Project333.Runtime.Presentation.Battle.GwangmaPresentationAssets", true);
        private static Type EffectType => typeof(TileTextView).Assembly.GetType("Project333.Runtime.Presentation.Battle.GwangmaCrossSlash", true);

        [TearDown]
        public void CleanupCardFixtures() => BoardAnimationControllerTestUtility.Cleanup();

        [TestCase("Idle", 6, 6f, true)]
        [TestCase("Attack", 6, 12f, false)]
        [TestCase("BeAttacked", 6, 15f, false)]
        [TestCase("Run", 6, 12f, true)]
        [TestCase("Death", 6, 8f, false)]
        public void ControllerMotion_HasDistinct512FramesAndAuthoredDuration(string state, int count, float fps, bool loop)
        {
            var clip = BoardAnimationControllerTestUtility.Clip("Gwangma", state);
            var frames = BoardAnimationControllerTestUtility.Frames(clip);
            Assert.That(frames.Length, Is.EqualTo(count));
            Assert.That(clip.frameRate, Is.EqualTo(fps));
            Assert.That(clip.isLooping, Is.EqualTo(loop));
            AssertValidFrames(frames, AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Unit/Gwangma/" + state + ".png"));
            foreach (var frame in frames)
            {
                Assert.That(frame.rect.size, Is.EqualTo(new Vector2(512, 512)));
                Assert.That(frame.pivot, Is.EqualTo(new Vector2(256, 256)));
            }
            Assert.That(frames.Distinct().Count(), Is.EqualTo(count));
            Assert.That(clip.length, Is.EqualTo(count / fps).Within(0.0001f));
        }

        [Test]
        public void Drained_HoldsControllerIdleAndAllFiveSheetsAreSeparate()
        {
            var sheets = new HashSet<Texture2D>();
            foreach (var state in new[] { "Idle", "Attack", "BeAttacked", "Run", "Death" })
                sheets.Add(BoardAnimationControllerTestUtility.Frames(BoardAnimationControllerTestUtility.Clip("Gwangma", state))[0].texture);
            Assert.That(sheets.Count, Is.EqualTo(5));
            var idle = BoardAnimationControllerTestUtility.Frames(BoardAnimationControllerTestUtility.Clip("Gwangma", "Idle"));
            var drained = BoardAnimationControllerTestUtility.Frames(BoardAnimationControllerTestUtility.Clip("Gwangma", "Drained"));
            Assert.That(drained.Length, Is.EqualTo(1));
            Assert.That(drained[0], Is.SameAs(idle[0]));
        }
        [TestCase("Idle", 6, 1)]
        [TestCase("Attack", 6, 1)]
        [TestCase("BeAttacked", 6, 1)]
        [TestCase("Run", 6, 1)]
        [TestCase("Death", 6, 1)]
        [TestCase("CrossSlashAtlas", 4, 2)]
        public void Atlas_ImportsWithAlphaAndWithoutPixelArtFiltering(string name, int columns, int rows)
        {
            var texture = rows == 1 ? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Unit/Gwangma/" + name + ".png") : Resources.Load<Texture2D>(ResourceRoot + name);
            Assert.That(texture, Is.Not.Null);
            Assert.That((float)texture.width / columns, Is.EqualTo((float)texture.height / rows).Within(0.01f), "Atlas cells must be square; half-pixel boundaries are valid.");
            if (rows == 1)
            {
                Assert.That(texture.width, Is.EqualTo(3072));
                Assert.That(texture.height, Is.EqualTo(512));
            }
            Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point));
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            // Inspect the actual alpha channel, independent of the runtime slice table.
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(source.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(texture))), Is.True);
                var pixels = source.GetPixels32();
                Assert.That(pixels.Any(pixel => pixel.a == 0), Is.True, "Atlas must not include an opaque background.");
                for (var row = 0; row < rows; row++)
                for (var column = 0; column < columns; column++)
                {
                    var visible = 0;
                    for (var y = row * source.height / rows; y < (row + 1) * source.height / rows; y++)
                    for (var x = column * source.width / columns; x < (column + 1) * source.width / columns; x++)
                        if (pixels[y * source.width + x].a > 20) visible++;
                    Assert.That(visible, Is.GreaterThan(20), "Empty atlas cell: " + column + "," + row);
                }
            }
            finally { Object.DestroyImmediate(source); }
        }

        [Test]
        public void CrossSlash_HasEightIndependentCenteredImpactFrames()
        {
            var frames = (IReadOnlyList<Sprite>)AssetsType.GetProperty("ImpactFrames", Static).GetValue(null);
            Assert.That(frames.Count, Is.EqualTo(8));
            AssertValidFrames(frames, Resources.Load<Texture2D>(ResourceRoot + "CrossSlashAtlas"));
            Assert.That(frames.Select(frame => frame.rect).Distinct().Count(), Is.EqualTo(8));
            foreach (var frame in frames) Assert.That(frame.pivot, Is.EqualTo(frame.rect.size * 0.5f));
        }

        [TestCase("SwordDraw")]
        [TestCase("SwordSwing")]
        [TestCase("CrossSlashImpact")]
        [TestCase("Dash")]
        [TestCase("GwangmaHurt")]
        [TestCase("GwangmaDeath")]
        public void Sound_IsPackagedAsShortPlayableAudio(string name)
        {
            var clip = (AudioClip)AssetsType.GetMethod("GetSound", Static).Invoke(null, new object[] { name });
            Assert.That(clip, Is.Not.Null, name);
            Assert.That(clip.length, Is.InRange(0.05f, 2f));
            Assert.That(clip.samples, Is.GreaterThan(0));
            Assert.That(clip.frequency, Is.EqualTo(44100));
            Assert.That(clip.channels, Is.EqualTo(1));
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void OnlineRequest_RetainsBothPresentedCardIdentitiesWithoutApplyingDamage(PlayerId sourceOwner)
        {
            var root = new GameObject("GwangmaRequestTest");
            root.SetActive(false);
            try
            {
                var bootstrapper = root.AddComponent<BattleBootstrapper>();
                var gateway = new LocalBattleGateway(new BattleFlowController());
                var deck = Enumerable.Repeat("unknown", 20).ToArray();
                var state = gateway.StartBattle(new BattleSetupRequest(deck, deck, PlayerId.Player));
                Set(bootstrapper, "_battleGateway", gateway);
                var destinationOwner = sourceOwner == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
                var coordinate = new TileCoord(0, 0);
                state.GetBoard(sourceOwner).Place(coordinate, Unit("source", sourceOwner));
                state.GetBoard(destinationOwner).Place(coordinate, Unit("target", destinationOwner));
                var attack = new BattleEventDto { EventType = BattleEventType.AttackStarted,
                    SourceOwnerId = sourceOwner, SourceCoord = TileCoordDto.FromDomain(coordinate),
                    TargetOwnerId = destinationOwner, TargetCoord = TileCoordDto.FromDomain(coordinate) };
                var request = typeof(BattleBootstrapper).GetMethod("BuildOnlineAttackAnimationRequest", Hidden)
                    .Invoke(bootstrapper, new object[] { attack, Array.Empty<BattleEventDto>() });
                Assert.That(request.GetType().GetProperty("AttackerCardId").GetValue(request), Is.EqualTo("Gwangma"));
                Assert.That(request.GetType().GetProperty("DefenderCardId").GetValue(request), Is.EqualTo("Gwangma"));
                Assert.That(request.GetType().GetProperty("IsRangedAttacker").GetValue(request), Is.False);
                Assert.That(state.GetBoard(sourceOwner).GetOccupant(coordinate).CurrentHp, Is.EqualTo(33));
                Assert.That(state.GetBoard(destinationOwner).GetOccupant(coordinate).CurrentHp, Is.EqualTo(33));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator Runtime_MeleeApproachCrossSlashCounterattackCancelAndIdentityAreSafe()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return RunIsolatedPlayback();
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator LeavePlayModeAfterFailure()
        {
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        private static IEnumerator RunIsolatedPlayback()
        {
            var fixture = new GameObject("GwangmaIsolatedPlayback");
            RenderTexture targetTexture = null;
            var originalTimeScale = Time.timeScale;
            try
            {
                var cameraObject = new GameObject("PreviewCamera", typeof(Camera), typeof(AudioListener));
                cameraObject.transform.SetParent(fixture.transform);
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.035f, 0.023f, 0.055f);
                camera.orthographic = true; camera.orthographicSize = 5;
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    targetTexture = new RenderTexture(1000, 560, 24); targetTexture.Create();
                    camera.targetTexture = targetTexture;
                }
                var left = CreateTile(fixture.transform, camera, PlayerId.Player, -230);
                var right = CreateTile(fixture.transform, camera, PlayerId.AI, 230);
                var effect = (MonoBehaviour)new GameObject("CrossSlashHost").AddComponent(EffectType);
                effect.transform.SetParent(fixture.transform);
                var host = new GameObject("DormantPresentationCoordinator");
                host.SetActive(false); host.transform.SetParent(fixture.transform);
                // Dormant bootstrapper: Awake never runs, so no login/gateway/server or gameplay scene is used.
                var bootstrapper = host.AddComponent<BattleBootstrapper>();
                var screen = host.AddComponent<BattleScreenPresenter>();
                var board = host.AddComponent<BoardPresenter>();
                Set(bootstrapper, "_battleScreenPresenter", screen); Set(screen, "_boardPresenter", board);
                Set(board, "_playerTileViews", new[] { left.GetComponent<TileView>() });
                Set(board, "_aiTileViews", new[] { right.GetComponent<TileView>() });
                Set(bootstrapper, "_gwangmaCrossSlash", effect);
                Canvas.ForceUpdateCanvases(); yield return null;
                Assert.That(left.GetComponentInParent<Canvas>(), Is.Null, "Actual tiles own a child Canvas.");
                Assert.That(left.VisualRuntimeId, Is.EqualTo("gwangma-Player"));
                var baseBody = left.GetVisualNormalizedWorldPosition(new Vector2(0.5f, 0.4f));
                var lowered = left.GetVisualImpactWorldPosition();
                Assert.That(lowered.y, Is.LessThan(baseBody.y), "Reuse the lower impact anchor requested for BlueDragon.");

                // Finish first-use audio import and UI shader compilation before timing short visual cues.
                foreach (var sound in new[] { "SwordDraw", "SwordSwing", "CrossSlashImpact", "Dash" })
                    AssetsType.GetMethod("GetSound", Static).Invoke(null, new object[] { sound });
                Capture(camera, targetTexture, "gwangma-idle.png");
                yield return new WaitForSecondsRealtime(0.3f);
                yield return VerifyStrike(effect, left, right, camera, targetTexture, "right");
                yield return VerifyStrike(effect, right, left, camera, targetTexture, "left");

                Time.timeScale = 0f;
                Strike(effect, left, right, false); Strike(effect, right, left, true);
                yield return new WaitForSecondsRealtime(0.3f);
                var attackImage = FindImage(fixture.transform, "CrossSlashAttack");
                var counterImage = FindImage(fixture.transform, "CrossSlashCounter");
                Assert.That(attackImage.gameObject.activeInHierarchy, Is.True);
                Assert.That(counterImage.gameObject.activeInHierarchy, Is.True, "Counterattack must not replace the outgoing X slash.");
                Assert.That(Vector3.Distance(attackImage.rectTransform.position, right.GetVisualImpactWorldPosition()), Is.LessThan(0.01f));
                Assert.That(Vector3.Distance(counterImage.rectTransform.position, left.GetVisualImpactWorldPosition()), Is.LessThan(0.01f));
                bootstrapper.CancelCombatPresentation();
                Assert.That(attackImage.gameObject.activeSelf || counterImage.gameObject.activeSelf, Is.False);
                Assert.That(effect.GetComponent<AudioSource>().isPlaying, Is.False);
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.That(attackImage.gameObject.activeSelf || counterImage.gameObject.activeSelf, Is.False, "Cancelled coroutines cannot revive effects.");
                Time.timeScale = 1f;

                Strike(effect, left, right, false);
                right.GetComponent<TileView>().Present(Unit("replacement", PlayerId.AI));
                right.SetTile("replacement", "Gwangma", true);
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.That(attackImage.gameObject.activeSelf, Is.False, "An old strike cannot hit a newly presented occupant.");
                right.GetComponent<TileView>().Present(Unit("gwangma-AI", PlayerId.AI));
                right.SetTile("right", "Gwangma", true);

                yield return VerifyFullMeleeSequence(bootstrapper, effect, left, right, camera, targetTexture);
                foreach (var image in fixture.GetComponentsInChildren<Image>(true))
                    if (image.name.StartsWith("CrossSlash")) Assert.That(image.raycastTarget, Is.False);
                var overlay = FindImage(fixture.transform, "CrossSlashAttack").GetComponentInParent<CanvasGroup>();
                Assert.That(overlay.blocksRaycasts || overlay.interactable, Is.False);
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.DestroyImmediate(fixture);
                if (targetTexture != null) { targetTexture.Release(); Object.DestroyImmediate(targetTexture); }
            }
        }

        private static IEnumerator VerifyStrike(MonoBehaviour effect, TileTextView source, TileTextView target,
            Camera camera, RenderTexture texture, string direction)
        {
            Strike(effect, source, target, false);
            var impact = FindImage(effect.transform.parent, "CrossSlashAttack");
            Assert.That(impact.gameObject.activeSelf, Is.False, "Anticipation must precede the X impact.");
            var audio = effect.GetComponent<AudioSource>();
            Assert.That(audio, Is.Not.Null); Assert.That(audio.spatialBlend, Is.Zero);
            var hasAudioDevice = audio.isPlaying;
            audio.Stop();
            yield return new WaitForSecondsRealtime(0.19f);
            Assert.That(impact.gameObject.activeSelf, Is.False, "Swing starts before impact.");
            if (hasAudioDevice) Assert.That(audio.isPlaying, Is.True, "Sword swing restarts audio after draw was stopped.");
            audio.Stop();
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(impact.gameObject.activeInHierarchy, Is.True);
            Assert.That(impact.sprite.texture.name, Is.EqualTo("CrossSlashAtlas"));
            Assert.That(impact.rectTransform.pivot, Is.EqualTo(Vector2.one * 0.5f));
            Assert.That(Mathf.Sign(impact.rectTransform.localScale.x), Is.EqualTo(direction == "right" ? 1f : -1f));
            Assert.That(Vector3.Distance(impact.rectTransform.position, target.GetVisualImpactWorldPosition()), Is.LessThan(0.01f));
            Assert.That(audio.isPlaying, Is.False,
                "X impact must not restart a per-character hit sound; TileTextView owns the common hit cue.");
            if (!hasAudioDevice) TestContext.Progress.WriteLine("No audio backend: timing/assets verified; swing audibility not asserted.");
            yield return new WaitForSecondsRealtime(0.1f);
            Capture(camera, texture, "gwangma-impact-" + direction + ".png");
            var deadline = Time.realtimeSinceStartup + 2f;
            while (impact.gameObject.activeSelf && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(impact.gameObject.activeSelf, Is.False, "Completed impact returns its slot to the pool.");
        }

        private static IEnumerator VerifyFullMeleeSequence(BattleBootstrapper bootstrapper, MonoBehaviour host,
            TileTextView attacker, TileTextView defender, Camera camera, RenderTexture texture)
        {
            var requestType = typeof(BattleBootstrapper).GetNestedType("AttackAnimationRequest", BindingFlags.NonPublic);
            var coord = new TileCoord(0, 0);
            var request = Activator.CreateInstance(requestType, new object[] { PlayerId.Player, coord, PlayerId.AI, coord, coord, false });
            requestType.GetProperty("AttackerCardId").SetValue(request, "Gwangma");
            requestType.GetProperty("DefenderCardId").SetValue(request, "Gwangma");
            requestType.GetProperty("DefenderCounterattacked").SetValue(request, true);
            var sequence = (IEnumerator)typeof(BattleBootstrapper).GetMethod("PlayAttackAnimationSequence", Hidden).Invoke(bootstrapper, new[] { request });
            attacker.RestoreVisualLayout(); var origin = attacker.GetVisualWorldPosition();
            var completion = new Completion();
            host.StartCoroutine(Complete(sequence, completion));
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(Vector3.Distance(attacker.GetVisualWorldPosition(), origin), Is.GreaterThan(0.1f), "Melee unit must actually approach its target.");
            AssertSequenceSheet(attacker, "Run", "Approach uses Run.");
            Capture(camera, texture, "gwangma-approach.png");
            yield return new WaitForSecondsRealtime(0.22f);
            AssertSequenceSheet(attacker, "Attack", "Contact switches to Attack.");
            yield return new WaitForSecondsRealtime(0.24f);
            Assert.That(FindImage(host.transform.parent, "CrossSlashAttack").gameObject.activeSelf, Is.True);
            Capture(camera, texture, "gwangma-melee-hit.png");
            var deadline = Time.realtimeSinceStartup + 4f;
            while (!completion.Done && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(completion.Done, Is.True);
            Assert.That(Vector3.Distance(attacker.GetVisualWorldPosition(), origin), Is.LessThan(0.01f), "Survivor returns to its own tile.");
            AssertSequenceSheet(attacker, "Idle", "Return restores Idle.");
            Assert.That(attacker.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(33));
            Assert.That(defender.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(33), "Presentation must not mutate battle HP.");
        }

        private static TileTextView CreateTile(Transform fixture, Camera camera, PlayerId owner, float x)
        {
            var shell = new GameObject("Gwangma" + owner); shell.SetActive(false); shell.transform.SetParent(fixture);
            var tile = shell.AddComponent<TileView>(); tile.Configure(owner, 0, 0); tile.Present(Unit("gwangma-" + owner, owner));
            var view = shell.AddComponent<TileTextView>();
            var canvasObject = new GameObject("TileCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(shell.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1; canvas.sortingOrder = 100;
            var label = new GameObject("TileText", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(canvasObject.transform, false);
            var text = label.GetComponent<TextMeshProUGUI>(); text.enabled = false; text.rectTransform.anchoredPosition = new Vector2(x, -30);
            var visual = new GameObject("OccupantVisual", typeof(RectTransform), typeof(Image), typeof(Animator)); visual.transform.SetParent(canvasObject.transform, false);
            Set(view, "_tileView", tile); Set(view, "_text", text); Set(view, "_showOccupantStats", false);
            Set(view, "_showHuanShuStatusIcon", false); Set(view, "_useTileImageClickSurface", false);
            Set(view, "_occupantVisualRoot", visual.GetComponent<RectTransform>());
            Set(view, "_occupantVisualImage", visual.GetComponent<Image>()); Set(view, "_occupantVisualAnimator", visual.GetComponent<Animator>());
            Set(view, "_occupantVisualSize", new Vector2(340, 340)); Set(view, "_occupantVisualAnchoredPosition", new Vector2(x, 0));
            BoardAnimationControllerTestUtility.Bind(view, "Gwangma");
            view.SetTile(owner.ToString(), "Gwangma", true); shell.SetActive(true); view.PlayIdleAnimation();
            return view;
        }

        private static UnitState Unit(string runtimeId, PlayerId owner) => new UnitState(runtimeId, "Gwangma", owner,
            new TileCoord(0, 0), AttackType.Melee, 33, 33, true, false, 0);
        private static void Strike(MonoBehaviour effect, TileTextView source, TileTextView target, bool counter) =>
            EffectType.GetMethod("PlayStrike", Hidden).Invoke(effect, new object[] { source, target, counter });
        private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Hidden).SetValue(instance, value);
        private sealed class Completion { public bool Done; }
        private static IEnumerator Complete(IEnumerator sequence, Completion completion) { yield return sequence; completion.Done = true; }
        private static void AssertSequenceSheet(TileTextView view, string sheet, string message)
        {
            var animator = (Animator)typeof(TileTextView).GetField("_occupantVisualAnimator", Hidden).GetValue(view);
            Assert.That(animator.enabled, Is.True, message);
            Assert.That(animator.runtimeAnimatorController, Is.SameAs(BoardAnimationControllerTestUtility.Controller("Gwangma")), message);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(sheet), Is.True, message);
            Assert.That(typeof(TileTextView).GetField("_activeSpriteSequence", Hidden).GetValue(view), Is.Null, "Native Animator owns playback.");
        }
        private static Image FindImage(Transform root, string name)
        {
            var image = root.GetComponentsInChildren<Image>(true).FirstOrDefault(value => value.name == name);
            Assert.That(image, Is.Not.Null, name); return image;
        }
        private static void AssertValidFrames(IReadOnlyList<Sprite> frames, Texture2D texture)
        {
            Assert.That(texture, Is.Not.Null);
            foreach (var frame in frames)
            {
                Assert.That(frame.texture, Is.SameAs(texture));
                Assert.That(frame.rect.width, Is.GreaterThan(0)); Assert.That(frame.rect.height, Is.GreaterThan(0));
                Assert.That(frame.rect.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(frame.rect.yMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(frame.rect.xMax, Is.LessThanOrEqualTo(texture.width)); Assert.That(frame.rect.yMax, Is.LessThanOrEqualTo(texture.height));
            }
        }
        private static void Capture(Camera camera, RenderTexture texture, string name)
        {
            if (texture == null) return;
            Canvas.ForceUpdateCanvases(); camera.Render();
            var previous = RenderTexture.active; Texture2D image = null;
            try
            {
                RenderTexture.active = texture; image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
                var output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Captures"));
                Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; if (image != null) Object.DestroyImmediate(image); }
        }
    }
}