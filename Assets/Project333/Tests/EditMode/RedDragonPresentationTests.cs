using System;
using System.Collections;
using System.IO;
using System.Linq;
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
    public sealed class RedDragonPresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static Type EffectType => typeof(TileTextView).Assembly.GetType("Project333.Runtime.Presentation.Battle.RedDragonFireBreath", true);

        [TearDown]
        public void CleanupCardFixtures() => BoardAnimationControllerTestUtility.Cleanup();

        [UnityTearDown]
        public IEnumerator LeavePlayModeAfterFailure()
        {
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator FireBreath_MeleeMovementCounterattackAnchorsAndCancellation()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return RunIsolatedPlayback();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CounterDamageKeepsInitiatingAttackMotionAndHitSound()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return RunIsolatedPlayback(true);
            yield return new ExitPlayMode();
        }
        private static IEnumerator RunIsolatedPlayback(bool counterDamageOnly = false)
        {
            var fixture = new GameObject("RedDragonIsolatedPlayback");
            RenderTexture renderTexture = null;
            var originalTimeScale = Time.timeScale;
            try
            {
                var cameraObject = new GameObject("PreviewCamera", typeof(Camera), typeof(AudioListener));
                cameraObject.transform.SetParent(fixture.transform);
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.05f, 0.035f, 0.025f);
                camera.orthographic = true; camera.orthographicSize = 5;
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    renderTexture = new RenderTexture(1000, 560, 24); renderTexture.Create();
                    camera.targetTexture = renderTexture;
                }
                var left = CreateTile(fixture.transform, camera, PlayerId.Player, -230);
                var right = CreateTile(fixture.transform, camera, PlayerId.AI, 230);
                var effect = (MonoBehaviour)new GameObject("FireBreathHost").AddComponent(EffectType);
                effect.transform.SetParent(fixture.transform);
                var host = new GameObject("DormantPresentationCoordinator"); host.SetActive(false); host.transform.SetParent(fixture.transform);
                var bootstrapper = host.AddComponent<BattleBootstrapper>();
                var screen = host.AddComponent<BattleScreenPresenter>();
                var board = host.AddComponent<BoardPresenter>();
                Set(bootstrapper, "_battleScreenPresenter", screen); Set(screen, "_boardPresenter", board);
                Set(board, "_playerTileViews", new[] { left.GetComponent<TileView>() });
                Set(board, "_aiTileViews", new[] { right.GetComponent<TileView>() });
                Set(bootstrapper, "_redDragonFireBreath", effect);
                Canvas.ForceUpdateCanvases(); yield return null;
                // Warm resources before measuring a short anticipation window.
                var assets = typeof(TileTextView).Assembly.GetType("Project333.Runtime.Presentation.Battle.RedDragonPresentationAssets", true);
                assets.GetProperty("FireFrames", Static).GetValue(null);
                assets.GetProperty("BreathSound", Static).GetValue(null);
                if (counterDamageOnly)
                {
                    var requestType = typeof(BattleBootstrapper).GetNestedType("AttackAnimationRequest", BindingFlags.NonPublic);
                    var coord = new TileCoord(0, 0);
                    var request = Activator.CreateInstance(requestType, new object[] { PlayerId.Player, coord, PlayerId.AI, coord, coord, false });
                    requestType.GetProperty("AttackerCardId").SetValue(request, "RedDragon");
                    requestType.GetProperty("DefenderCardId").SetValue(request, "RedDragon");
                    requestType.GetProperty("DefenderCounterattacked").SetValue(request, true);
                    requestType.GetProperty("AttackerTookDamage").SetValue(request, true);
                    var sequence = (IEnumerator)typeof(BattleBootstrapper).GetMethod("PlayAttackAnimationSequence", Hidden).Invoke(bootstrapper, new[] { request });
                    var origin = left.GetVisualWorldPosition();
                    var completion = new Completion(); effect.StartCoroutine(Complete(sequence, completion));
                    var deadline = Time.realtimeSinceStartup + 8f;
                    while (!completion.Done && Time.realtimeSinceStartup < deadline)
                    {
                        var animator = (Animator)typeof(TileTextView).GetField("_occupantVisualAnimator", Hidden).GetValue(left);
                        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("BeAttacked"), Is.False,
                            "Counterattack damage must not append a hurt motion to the initiating attacker.");
                        yield return null;
                    }
                    Assert.That(completion.Done, Is.True);
                    Assert.That(left.GetComponent<AudioSource>(), Is.Not.Null, "Counter damage keeps its shared hit sound.");
                    AssertState(left, "Idle");
                    Assert.That(Vector3.Distance(left.GetVisualWorldPosition(), origin), Is.LessThan(.01f));
                    Assert.That(left.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(33));
                    yield break;
                }
                Capture(camera, renderTexture, "red-dragon-idle.png");
                yield return new WaitForSecondsRealtime(0.3f);

                yield return VerifyFacing(effect, left, right, camera, renderTexture, "right");
                yield return VerifyFacing(effect, right, left, camera, renderTexture, "left");
                left.RestoreVisualLayout(); right.RestoreVisualLayout();
                Time.timeScale = 0f;
                Strike(effect, left, right, false); Strike(effect, right, left, true);
                yield return new WaitForSecondsRealtime(0.24f);
                var outgoing = FindImage(fixture.transform, "FireBreathAttack");
                var counter = FindImage(fixture.transform, "FireBreathCounter");
                Assert.That(outgoing.gameObject.activeSelf && counter.gameObject.activeSelf, Is.True, "Separate slots support simultaneous counterattack, even while time scale is zero.");
                AssertAnchors(outgoing, left, right); AssertAnchors(counter, right, left);
                bootstrapper.CancelCombatPresentation();
                Assert.That(outgoing.gameObject.activeSelf || counter.gameObject.activeSelf, Is.False);
                foreach (var audio in effect.GetComponents<AudioSource>()) Assert.That(audio.isPlaying, Is.False);
                yield return new WaitForSecondsRealtime(0.55f);
                Assert.That(outgoing.gameObject.activeSelf || counter.gameObject.activeSelf, Is.False, "Cancelled effects cannot revive.");
                Time.timeScale = originalTimeScale;

                Strike(effect, left, right, false);
                right.GetComponent<TileView>().Present(Unit("replacement", PlayerId.AI)); right.SetTile("replacement", "RedDragon", true);
                yield return new WaitForSecondsRealtime(0.24f);
                Assert.That(outgoing.gameObject.activeSelf, Is.False, "Stale VFX must not strike a replacement occupant.");
                right.GetComponent<TileView>().Present(Unit("red-AI", PlayerId.AI)); right.SetTile("right", "RedDragon", true);
                yield return VerifyMeleeSequence(bootstrapper, effect, left, right, camera, renderTexture);
                // Inspect a different character too: generic impacts use a lower body fraction.
                right.gameObject.SetActive(false);
                var rightGwangma = CreateTile(fixture.transform, camera, PlayerId.AI, 230, "Gwangma");
                yield return null;
                yield return VerifyFacing(effect, left, rightGwangma, camera, renderTexture, "red-vs-gwangma-right");
                Object.DestroyImmediate(rightGwangma.gameObject);
                right.gameObject.SetActive(true); left.gameObject.SetActive(false);
                var leftGwangma = CreateTile(fixture.transform, camera, PlayerId.Player, -230, "Gwangma");
                yield return null;
                yield return VerifyFacing(effect, right, leftGwangma, camera, renderTexture, "red-vs-gwangma-left");
                Object.DestroyImmediate(leftGwangma.gameObject);
                left.gameObject.SetActive(true);
                foreach (var image in new[] { outgoing, counter })
                {
                    Assert.That(image.raycastTarget, Is.False);
                    var group = image.GetComponentInParent<CanvasGroup>();
                    Assert.That(group.blocksRaycasts || group.interactable, Is.False, "VFX never consume tile input.");
                }
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.DestroyImmediate(fixture);
                if (renderTexture != null) { renderTexture.Release(); Object.DestroyImmediate(renderTexture); }
            }
        }

        private static IEnumerator VerifyFacing(MonoBehaviour effect, TileTextView source, TileTextView target,
            Camera camera, RenderTexture texture, string label)
        {
            source.RestoreVisualLayout(); target.RestoreVisualLayout();
            var contact = (Vector3)EffectType.GetMethod("GetMeleeContactPosition", Static).Invoke(null,
                new object[] { source, target, target.GetVisualWorldPosition() });
            Assert.That(contact.y, Is.EqualTo(target.GetVisualWorldPosition().y).Within(0.01f), "Contact correction must preserve the original ground line.");
            source.SetVisualWorldPosition(contact); source.PlayAttackAnimation();
            Strike(effect, source, target, false);
            var image = FindImage(effect.transform.parent, "FireBreathAttack");
            Assert.That(image.gameObject.activeSelf, Is.False, "Windup precedes flame emission.");
            yield return new WaitForSecondsRealtime(0.24f);
            Assert.That(image.gameObject.activeSelf, Is.True);
            Assert.That(image.sprite.texture.name, Is.EqualTo("FireAtlas"));
            AssertAnchors(image, source, target);
            var forward = source.GetVisualNormalizedWorldPosition(Vector2.right) - source.GetVisualNormalizedWorldPosition(Vector2.zero);
            Assert.That(Vector3.Dot(target.GetVisualImpactWorldPosition() - image.rectTransform.position, forward), Is.GreaterThan(0), "Flame must leave the mouth forward, never backward across the dragon.");
            Capture(camera, texture, "red-dragon-fire-" + label + ".png");
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(image.gameObject.activeSelf, Is.False);
            source.RestoreVisualLayout(); source.PlayIdleAnimation();
        }

        private static void AssertAnchors(Image image, TileTextView attacker, TileTextView target)
        {
            var inlet = image.rectTransform.position;
            var outlet = image.rectTransform.TransformPoint(new Vector3(image.rectTransform.rect.width * 0.90f, 0f));
            var authoredJawPositions = new[] { new Vector2(0.78f, 0.385f), new Vector2(0.92f, 0.37f),
                new Vector2(0.88f, 0.33f), new Vector2(0.76f, 0.39f) };
            Assert.That(authoredJawPositions.Any(point => Vector3.Distance(inlet,
                attacker.GetVisualNormalizedWorldPosition(point)) < 0.01f), Is.True, "Inlet follows an authored breathing jaw position.");
            Assert.That(Vector3.Distance(outlet, target.GetVisualImpactWorldPosition()), Is.LessThan(0.01f), "Outlet follows the shared lowered impact anchor.");
        }

        private static IEnumerator VerifyMeleeSequence(BattleBootstrapper bootstrapper, MonoBehaviour host,
            TileTextView attacker, TileTextView defender, Camera camera, RenderTexture texture)
        {
            var requestType = typeof(BattleBootstrapper).GetNestedType("AttackAnimationRequest", BindingFlags.NonPublic);
            var coord = new TileCoord(0, 0);
            var request = Activator.CreateInstance(requestType, new object[] { PlayerId.Player, coord, PlayerId.AI, coord, coord, false });
            requestType.GetProperty("AttackerCardId").SetValue(request, "RedDragon");
            requestType.GetProperty("DefenderCardId").SetValue(request, "RedDragon");
            requestType.GetProperty("DefenderCounterattacked").SetValue(request, true);
            var sequence = (IEnumerator)typeof(BattleBootstrapper).GetMethod("PlayAttackAnimationSequence", Hidden).Invoke(bootstrapper, new[] { request });
            attacker.RestoreVisualLayout(); defender.RestoreVisualLayout(); var origin = attacker.GetVisualWorldPosition();
            var completion = new Completion(); host.StartCoroutine(Complete(sequence, completion));
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(Vector3.Distance(attacker.GetVisualWorldPosition(), origin), Is.GreaterThan(0.1f));
            AssertState(attacker, "Run"); Capture(camera, texture, "red-dragon-approach.png");
            yield return new WaitForSecondsRealtime(0.25f);
            AssertState(attacker, "Attack");
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(FindImage(host.transform.parent, "FireBreathAttack").gameObject.activeSelf, Is.True);
            Capture(camera, texture, "red-dragon-melee-hit.png");
            var deadline = Time.realtimeSinceStartup + 4f;
            while (!completion.Done && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(completion.Done, Is.True);
            Assert.That(Vector3.Distance(attacker.GetVisualWorldPosition(), origin), Is.LessThan(0.01f));
            AssertState(attacker, "Idle");
            Assert.That(attacker.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(33));
            Assert.That(defender.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(33), "Presentation cannot apply damage.");
        }

        private static TileTextView CreateTile(Transform fixture, Camera camera, PlayerId owner, float x, string cardId = "RedDragon")
        {
            var shell = new GameObject(cardId + owner); shell.SetActive(false); shell.transform.SetParent(fixture);
            var tile = shell.AddComponent<TileView>(); tile.Configure(owner, 0, 0); tile.Present(Unit("red-" + owner, owner, cardId));
            var view = shell.AddComponent<TileTextView>();
            var canvasObject = new GameObject("TileCanvas", typeof(RectTransform), typeof(Canvas)); canvasObject.transform.SetParent(shell.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1; canvas.sortingOrder = 100;
            var label = new GameObject("TileText", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(canvasObject.transform, false);
            var text = label.GetComponent<TextMeshProUGUI>(); text.enabled = false; text.rectTransform.anchoredPosition = new Vector2(x, -30);
            var visual = new GameObject("OccupantVisual", typeof(RectTransform), typeof(Image), typeof(Animator)); visual.transform.SetParent(canvasObject.transform, false);
            Set(view, "_tileView", tile); Set(view, "_text", text); Set(view, "_showOccupantStats", false);
            Set(view, "_showHuanShuStatusIcon", false); Set(view, "_useTileImageClickSurface", false);
            Set(view, "_occupantVisualRoot", visual.GetComponent<RectTransform>()); Set(view, "_occupantVisualImage", visual.GetComponent<Image>());
            Set(view, "_occupantVisualAnimator", visual.GetComponent<Animator>()); Set(view, "_occupantVisualSize", new Vector2(340, 340));
            Set(view, "_occupantVisualAnchoredPosition", new Vector2(x, 0));
            BoardAnimationControllerTestUtility.Bind(view, cardId);
            view.SetTile(owner.ToString(), cardId, true); shell.SetActive(true); view.PlayIdleAnimation(); return view;
        }
        private static UnitState Unit(string id, PlayerId owner, string cardId = "RedDragon") => new UnitState(id, cardId, owner,
            new TileCoord(0, 0), AttackType.Melee, 33, 33, true, false, 0);
        private static void Strike(MonoBehaviour effect, TileTextView source, TileTextView target, bool counter) =>
            EffectType.GetMethod("PlayStrike", Hidden).Invoke(effect, new object[] { source, target, counter });
        private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Hidden).SetValue(instance, value);
        private sealed class Completion { public bool Done; }
        private static IEnumerator Complete(IEnumerator sequence, Completion completion) { yield return sequence; completion.Done = true; }
        private static void AssertState(TileTextView view, string state)
        {
            var animator = (Animator)typeof(TileTextView).GetField("_occupantVisualAnimator", Hidden).GetValue(view);
            Assert.That(animator.enabled, Is.True); Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(state), Is.True, state);
            Assert.That(typeof(TileTextView).GetField("_activeSpriteSequence", Hidden).GetValue(view), Is.Null);
        }
        private static Image FindImage(Transform root, string name) => root.GetComponentsInChildren<Image>(true).Single(image => image.name == name);
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