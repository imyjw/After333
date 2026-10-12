using System;
using System.Collections;
using System.Collections.Generic;
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
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class ManaCannonCombatPresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static Type AssetsType => typeof(TileTextView).Assembly.GetType("Project333.Runtime.Presentation.Battle.ManaCannonPresentationAssets", true);
        private static Type EffectType => typeof(TileTextView).Assembly.GetType("Project333.Runtime.Presentation.Battle.ManaCannonProjectile", true);
        [TearDown] public void Cleanup() => BoardAnimationControllerTestUtility.Cleanup();
        [UnityTearDown] public IEnumerator LeavePlayMode() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest]
        public IEnumerator BothDirectionsStayOnTileAndDeliverOneRecordedMagicPacketAtProjectileImpact()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            foreach (var reverse in new[] { false, true })
            {
                var fixture = new Fixture();
                try
                {
                    fixture.WarmResources(); yield return null;
                    var source = reverse ? fixture.Right : fixture.Left; var target = reverse ? fixture.Left : fixture.Right;
                    var origin = source.GetVisualWorldPosition(); var targetOrigin = target.GetVisualWorldPosition();
                    var size = source.GetComponentsInChildren<Image>(true).Single(i => i.name == "OccupantVisual").rectTransform.sizeDelta;
                    var completion = fixture.Play(fixture.Request(false, reverse));
                    yield return new WaitForSecondsRealtime(.12f);
                    AssertState(source, "Attack"); AssertState(target, "Idle"); Assert.That(fixture.PopupCount, Is.Zero);
                    Assert.That(IsVisible(fixture.Projectile) || IsVisible(fixture.Impact), Is.False, "The cannon charges before emitting a projectile.");
                    if (!reverse) fixture.Capture("mana-cannon-charge.png");
                    while (!IsVisible(fixture.Projectile) && !completion.Done && Time.realtimeSinceStartup < completion.Deadline) yield return null;
                    Assert.That(IsVisible(fixture.Projectile), Is.True, "Charged cannon must emit its native mana projectile.");
                    yield return new WaitForSecondsRealtime(.08f);
                    Assert.That(IsVisible(fixture.Projectile), Is.True); Assert.That(fixture.PopupCount, Is.Zero, "Damage must wait until the projectile reaches its target.");
                    fixture.AssertFlight(source, target);
                    Assert.That(Vector3.Distance(source.GetVisualWorldPosition(), origin), Is.LessThan(.02f), "Ranged attacks do not move the cannon toward its target.");
                    Assert.That(Vector3.Distance(target.GetVisualWorldPosition(), targetOrigin), Is.LessThan(.02f));
                    Assert.That(source.GetComponentsInChildren<Image>(true).Single(i => i.name == "OccupantVisual").rectTransform.sizeDelta, Is.EqualTo(size));
                    AssertState(source, "Attack"); fixture.Capture(reverse ? "mana-cannon-flight-ai.png" : "mana-cannon-flight-player.png");
                    var sawHurt = false;
                    while ((!IsVisible(fixture.Impact) || fixture.PopupCount == 0) && !completion.Done && Time.realtimeSinceStartup < completion.Deadline)
                    {
                        sawHurt |= IsState(target, "BeAttacked"); yield return null;
                    }
                    Assert.That(IsVisible(fixture.Impact), Is.True); Assert.That(fixture.PopupCount, Is.EqualTo(1));
                    Assert.That(IsVisible(fixture.Projectile), Is.False); fixture.AssertImpact(target);
                    Assert.That(fixture.Effect.GetComponents<AudioSource>().Any(a => a.isPlaying), Is.True, "Mana explosion has a distinct quiet impact sound.");
                    Assert.That(target.GetComponents<AudioSource>().Any(a => a.isPlaying), Is.True, "Common UnitHit remains the shared hurt sound.");
                    fixture.Capture(reverse ? "mana-cannon-impact-ai.png" : "mana-cannon-impact-player.png");
                    while (!completion.Done && Time.realtimeSinceStartup < completion.Deadline)
                    {
                        sawHurt |= IsState(target, "BeAttacked");
                        Assert.That(Vector3.Distance(source.GetVisualWorldPosition(), origin), Is.LessThan(.02f)); yield return null;
                    }
                    Assert.That(completion.Done, Is.True); Assert.That(sawHurt, Is.True);
                    AssertState(source, "Idle"); AssertState(target, "Idle"); fixture.AssertHiddenAndSilent();
                    Assert.That(source.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(100));
                    Assert.That(target.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(100), "The provided cue cannot mutate authoritative HP.");
                    Assert.That(fixture.Root.GetComponentsInChildren<TMP_Text>(true).Count(t => t.name == "ValuePopupText" && t.text == "50"), Is.LessThanOrEqualTo(1));
                }
                finally { fixture.Dispose(); }
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ProjectileUsesUnscaledTimingGlobalMuteAndOneReusableEmitter()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); var fixture = new Fixture(); var oldScale = Time.timeScale; var oldVolume = AudioListener.volume;
            try
            {
                fixture.WarmResources(); yield return null; Time.timeScale = 0f; AudioListener.volume = 0f;
                fixture.Shot(fixture.Left, fixture.Right); var deadline = Time.realtimeSinceStartup + 3f;
                while (!IsVisible(fixture.Projectile) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(IsVisible(fixture.Projectile), Is.True); Assert.That(AudioListener.volume, Is.Zero); fixture.AssertFlight(fixture.Left, fixture.Right);
                Assert.That(fixture.Projectile.raycastTarget, Is.False); Assert.That(fixture.Projectile.GetComponentInParent<CanvasGroup>().blocksRaycasts, Is.False);
                var audio = fixture.Effect.GetComponents<AudioSource>(); Assert.That(audio.Length, Is.EqualTo(1)); var emitter = audio[0];
                var position = fixture.Projectile.rectTransform.position; yield return new WaitForSecondsRealtime(.07f);
                Assert.That(Vector3.Distance(position, fixture.Projectile.rectTransform.position), Is.GreaterThan(.01f), "Flight advances while the scaled clock is paused.");
                fixture.Right.SetVisualWorldPosition(fixture.Right.GetVisualWorldPosition() + new Vector3(10, -5, 0));
                while (!IsVisible(fixture.Impact) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(IsVisible(fixture.Impact), Is.True); fixture.AssertImpact(fixture.Right);
                Assert.That(AudioListener.volume, Is.Zero); Assert.That(fixture.Impact.raycastTarget, Is.False);
                Assert.That(fixture.Effect.GetComponents<AudioSource>().Single(), Is.SameAs(emitter));
                fixture.Bootstrapper.CancelCombatPresentation(); fixture.AssertHiddenAndSilent();
                for (var i = 0; i < 4; i++) fixture.Shot(fixture.Right, fixture.Left);
                Assert.That(fixture.Effect.GetComponents<AudioSource>().Single(), Is.SameAs(emitter));
                deadline = Time.realtimeSinceStartup + 3f;
                while (!IsVisible(fixture.Impact) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(IsVisible(fixture.Impact), Is.True); fixture.AssertImpact(fixture.Left);
                var reboundCanvas = fixture.Impact.GetComponentInParent<Canvas>();
                Assert.That(reboundCanvas.overrideSorting, Is.True, "Moving the reused overlay between root Canvases retains its own sorting.");
                Assert.That(reboundCanvas.sortingOrder, Is.EqualTo(4905));
                while ((IsVisible(fixture.Impact) || emitter.isPlaying) && Time.realtimeSinceStartup < deadline) yield return null;
                fixture.AssertHiddenAndSilent(); Assert.That(AudioListener.volume, Is.Zero);
            }
            finally { Time.timeScale = oldScale; AudioListener.volume = oldVolume; fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CancelDuringChargeAndFlightCannotDeliverDelayedDamageOrExplosion()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            foreach (var waitForFlight in new[] { false, true })
            {
                var fixture = new Fixture();
                try
                {
                    fixture.WarmResources(); yield return null; var completion = fixture.Play(fixture.Request(false));
                    if (waitForFlight)
                    {
                        while (!IsVisible(fixture.Projectile) && Time.realtimeSinceStartup < completion.Deadline) yield return null;
                        Assert.That(IsVisible(fixture.Projectile), Is.True);
                    }
                    else yield return new WaitForSecondsRealtime(.1f);
                    fixture.Bootstrapper.CancelCombatPresentation(); fixture.AssertHiddenAndSilent();
                    while (!completion.Done && Time.realtimeSinceStartup < completion.Deadline) yield return null;
                    Assert.That(completion.Done, Is.True); yield return new WaitForSecondsRealtime(.8f);
                    Assert.That(fixture.PopupCount, Is.Zero); fixture.AssertHiddenAndSilent();
                    AssertState(fixture.Left, "Idle"); AssertState(fixture.Right, "Idle");
                    Assert.That(fixture.Left.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(100));
                    Assert.That(fixture.Right.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(100));
                }
                finally { fixture.Dispose(); }
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ReboundTargetDuringFlightCannotReceiveOldProjectileImpactHurtOrPacket()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); var fixture = new Fixture();
            try
            {
                fixture.WarmResources(); yield return null; var completion = fixture.Play(fixture.Request(false));
                while (!IsVisible(fixture.Projectile) && Time.realtimeSinceStartup < completion.Deadline) yield return null;
                Assert.That(IsVisible(fixture.Projectile), Is.True);
                fixture.Right.GetComponent<TileView>().Present(Unit(PlayerId.AI, "replacement-target"));
                fixture.Right.SetTile("replacement", "ManaCannon", true);
                while (!completion.Done && Time.realtimeSinceStartup < completion.Deadline) yield return null;
                Assert.That(completion.Done, Is.True); yield return new WaitForSecondsRealtime(.6f);
                Assert.That(fixture.PopupCount, Is.Zero); fixture.AssertHiddenAndSilent(); AssertState(fixture.Right, "Idle");
                Assert.That(fixture.Right.GetComponents<AudioSource>().Any(a => a.isPlaying), Is.False);
                Assert.That(fixture.Right.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(100));
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DisabledSourceTargetOrEffectStopsItsOwnedProjectileAndAudio()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); var fixture = new Fixture();
            try
            {
                fixture.WarmResources(); yield return null;
                foreach (var disable in new[] { "source", "target", "effect" })
                {
                    fixture.Shot(fixture.Left, fixture.Right); var deadline = Time.realtimeSinceStartup + 3f;
                    while (!IsVisible(fixture.Projectile) && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(IsVisible(fixture.Projectile), Is.True);
                    if (disable == "source") fixture.Left.enabled = false;
                    else if (disable == "target") fixture.Right.enabled = false;
                    else fixture.Effect.enabled = false;
                    yield return null; fixture.AssertHiddenAndSilent();
                    fixture.Left.enabled = true; fixture.Right.enabled = true; fixture.Effect.enabled = true;
                    yield return new WaitForSecondsRealtime(.7f); fixture.AssertHiddenAndSilent();
                }
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DefeatedTargetUsesFullRegisteredDestructionAfterImpactWithoutChangingTruth()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); var fixture = new Fixture();
            try
            {
                fixture.WarmResources(); yield return null; var completion = fixture.Play(fixture.Request(true));
                while (!IsState(fixture.Right, "Death") && !completion.Done && Time.realtimeSinceStartup < completion.Deadline) yield return null;
                AssertState(fixture.Right, "Death"); var deathStart = Time.realtimeSinceStartup;
                yield return new WaitForSecondsRealtime(.4f); Assert.That(completion.Done, Is.False, "The full authored destruction clip remains visible.");
                fixture.Capture("mana-cannon-death.png");
                while (!completion.Done && Time.realtimeSinceStartup < completion.Deadline) yield return null;
                Assert.That(completion.Done, Is.True); Assert.That(Time.realtimeSinceStartup - deathStart, Is.GreaterThanOrEqualTo(.9f));
                AssertState(fixture.Right, "Death"); AssertState(fixture.Left, "Idle"); fixture.AssertHiddenAndSilent();
                Assert.That(fixture.Right.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(100));
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ComponentOnlyDestroyReleasesReusableEmitterAndBothOverlayImages()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); var fixture = new Fixture();
            try
            {
                fixture.WarmResources(); yield return null; fixture.Shot(fixture.Left, fixture.Right);
                var host = fixture.Effect.gameObject; Assert.That(host.GetComponents<AudioSource>().Length, Is.EqualTo(1));
                Object.Destroy(fixture.Effect); yield return null; yield return null;
                Assert.That(host, Is.Not.Null); Assert.That(host.GetComponents<AudioSource>().Length, Is.Zero);
                Assert.That(fixture.Root.GetComponentsInChildren<Image>(true).Any(i => i.name == "ManaCannonProjectile" || i.name == "ManaCannonImpact"), Is.False);
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root = new GameObject("ManaCannonCombatFixture");
            internal readonly TileTextView Left, Right;
            internal readonly MonoBehaviour Effect;
            internal readonly BattleBootstrapper Bootstrapper;
            private readonly Camera _camera; private readonly RenderTexture _render;
            internal Fixture()
            {
                var cameraObject = new GameObject("PreviewCamera", typeof(Camera), typeof(AudioListener)); cameraObject.transform.SetParent(Root.transform);
                _camera = cameraObject.GetComponent<Camera>(); _camera.transform.position = new Vector3(0, 0, -10);
                _camera.orthographic = true; _camera.orthographicSize = 5; _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(.035f, .045f, .06f);
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                { _render = new RenderTexture(1000, 560, 24); _render.Create(); _camera.targetTexture = _render; }
                Left = CreateTile(Root.transform, _camera, PlayerId.Player, -230); Right = CreateTile(Root.transform, _camera, PlayerId.AI, 230);
                var effects = new GameObject("ManaCannonEffectHost"); effects.transform.SetParent(Root.transform); Effect = (MonoBehaviour)effects.AddComponent(EffectType);
                var host = new GameObject("DormantPresentationCoordinator"); host.SetActive(false); host.transform.SetParent(Root.transform);
                Bootstrapper = host.AddComponent<BattleBootstrapper>(); var screen = host.AddComponent<BattleScreenPresenter>(); var board = host.AddComponent<BoardPresenter>();
                Set(Bootstrapper, "_battleScreenPresenter", screen); Set(screen, "_boardPresenter", board);
                Set(board, "_playerTileViews", new[] { Left.GetComponent<TileView>() }); Set(board, "_aiTileViews", new[] { Right.GetComponent<TileView>() });
                Set(Bootstrapper, "_manaCannonProjectile", Effect); Canvas.ForceUpdateCanvases();
            }
            internal Image Projectile => Root.GetComponentsInChildren<Image>(true).SingleOrDefault(i => i.name == "ManaCannonProjectile");
            internal Image Impact => Root.GetComponentsInChildren<Image>(true).SingleOrDefault(i => i.name == "ManaCannonImpact");
            internal int PopupCount => Root.GetComponentsInChildren<TMP_Text>(true).Count(t => t.name == "ValuePopupText" && t.text == "50");
            internal void WarmResources()
            {
                foreach (var name in new[] { "ProjectileFrames", "ImpactFrames", "AttackSound", "ImpactSound" })
                    Assert.That(AssetsType.GetProperty(name, Static).GetValue(null), Is.Not.Null, name);
            }
            internal void Shot(TileTextView source, TileTextView target) => EffectType.GetMethod("PlayShot", Hidden).Invoke(Effect, new object[] { source, target });
            internal void AssertFlight(TileTextView source, TileTextView target)
            {
                Assert.That(IsVisible(Projectile), Is.True);
                var emitter = (Vector2)AssetsType.GetField("EmitterPoint", Static).GetValue(null);
                var core = (Vector2)AssetsType.GetField("ProjectileCorePoint", Static).GetValue(null);
                Assert.That(Projectile.rectTransform.pivot, Is.EqualTo(core), "Projectile transform identifies its luminous head, not the center of its trailing canvas.");
                var start = source.GetVisualNormalizedWorldPosition(emitter); var end = target.GetVisualImpactWorldPosition();
                var path = end - start; var point = Projectile.rectTransform.position;
                var amount = Vector3.Dot(point - start, path) / path.sqrMagnitude;
                Assert.That(amount, Is.InRange(-.01f, 1.01f));
                Assert.That(Vector3.Distance(point, start + path * amount), Is.LessThan(.03f), "Projectile travels along a straight line from the actual muzzle toward the target.");
            }
            internal void AssertImpact(TileTextView target)
            {
                Assert.That(IsVisible(Impact), Is.True);
                Assert.That(Vector3.Distance(Impact.rectTransform.position, target.GetVisualImpactWorldPosition()), Is.LessThan(.02f));
            }
            internal void AssertHiddenAndSilent()
            {
                Assert.That(IsVisible(Projectile) || IsVisible(Impact), Is.False);
                if (Effect != null) Assert.That(Effect.GetComponents<AudioSource>().Any(source => source.isPlaying), Is.False);
            }
            internal object Request(bool died, bool reverse = false)
            {
                var type = typeof(BattleBootstrapper).GetNestedType("AttackAnimationRequest", BindingFlags.NonPublic); var coord = new TileCoord(0, 0);
                var owner = reverse ? PlayerId.AI : PlayerId.Player; var targetOwner = reverse ? PlayerId.Player : PlayerId.AI;
                var source = reverse ? Right : Left; var target = reverse ? Left : Right;
                var request = Activator.CreateInstance(type, new object[] { owner, coord, targetOwner, coord, coord, true });
                type.GetProperty("AttackerCardId").SetValue(request, "ManaCannon"); type.GetProperty("DefenderCardId").SetValue(request, "ManaCannon");
                type.GetProperty("DefenderCounterattacked").SetValue(request, false);
                var impacts = (IList)type.GetProperty("DefenderImpacts").GetValue(request);
                var impactType = typeof(BattleBootstrapper).GetNestedType("AttackAnimationImpact", BindingFlags.NonPublic);
                impacts.Add(Activator.CreateInstance(impactType, new object[] { targetOwner, coord, true, died }));
                ((List<BattleValuePopupEvent>)type.GetProperty("ValuePopupEvents").GetValue(request)).Add(new BattleValuePopupEvent(target.VisualRuntimeId, targetOwner, coord,
                    false, 50, owner, source.VisualRuntimeId, "ManaCannon", BattleValueChangeCause.NormalAttack, DamageType.Magic, died ? 50 : 100, died ? 0 : 50));
                return request;
            }
            internal Completion Play(object request)
            {
                var sequence = (IEnumerator)typeof(BattleBootstrapper).GetMethod("PlayAttackAnimationSequence", Hidden).Invoke(Bootstrapper, new[] { request });
                var completion = new Completion { Deadline = Time.realtimeSinceStartup + 5f }; Effect.StartCoroutine(Complete(sequence, completion)); return completion;
            }
            internal void Capture(string filename)
            {
                if (_render == null) return; Canvas.ForceUpdateCanvases(); _camera.Render();
                var old = RenderTexture.active; var texture = new Texture2D(_render.width, _render.height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = _render; texture.ReadPixels(new Rect(0, 0, _render.width, _render.height), 0, 0); texture.Apply();
                    var directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Captures")); Directory.CreateDirectory(directory);
                    File.WriteAllBytes(Path.Combine(directory, filename), texture.EncodeToPNG());
                }
                finally { RenderTexture.active = old; Object.DestroyImmediate(texture); }
            }
            public void Dispose() { Object.DestroyImmediate(Root); if (_render != null) { _render.Release(); Object.DestroyImmediate(_render); } }
        }

        private static TileTextView CreateTile(Transform fixture, Camera camera, PlayerId owner, float x)
        {
            var shell = new GameObject("ManaCannon" + owner); shell.SetActive(false); shell.transform.SetParent(fixture);
            var tile = shell.AddComponent<TileView>(); tile.Configure(owner, 0, 0); tile.Present(Unit(owner)); var view = shell.AddComponent<TileTextView>();
            var canvasObject = new GameObject("TileCanvas", typeof(RectTransform), typeof(Canvas)); canvasObject.transform.SetParent(shell.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var label = new GameObject("TileText", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(canvasObject.transform, false);
            var text = label.GetComponent<TextMeshProUGUI>(); text.enabled = false; text.rectTransform.anchoredPosition = new Vector2(x, -30);
            var visual = new GameObject("OccupantVisual", typeof(RectTransform), typeof(Image), typeof(Animator)); visual.transform.SetParent(canvasObject.transform, false);
            Set(view, "_tileView", tile); Set(view, "_text", text); Set(view, "_showOccupantStats", false); Set(view, "_showHuanShuStatusIcon", false); Set(view, "_useTileImageClickSurface", false);
            Set(view, "_occupantVisualRoot", visual.GetComponent<RectTransform>()); Set(view, "_occupantVisualImage", visual.GetComponent<Image>());
            Set(view, "_occupantVisualAnimator", visual.GetComponent<Animator>()); Set(view, "_occupantVisualSize", new Vector2(340, 340)); Set(view, "_occupantVisualAnchoredPosition", new Vector2(x, 0));
            BoardAnimationControllerTestUtility.Bind(view, "ManaCannon"); view.SetTile(owner.ToString(), "ManaCannon", true); shell.SetActive(true); view.PlayIdleAnimation(); return view;
        }
        // Public-state fixture HP is intentionally unchanged; damage is supplied solely as an authoritative cue.
        private static UnitState Unit(PlayerId owner, string id = null) => new UnitState(id ?? "mana-cannon-" + owner, "ManaCannon", owner, new TileCoord(0, 0),
            AttackType.Ranged, 50, 100, true, false, 0, damageType: DamageType.Magic);
        private sealed class Completion { internal bool Done; internal float Deadline; }
        private static IEnumerator Complete(IEnumerator sequence, Completion completion) { yield return sequence; completion.Done = true; }
        private static bool IsVisible(Image image) => image != null && image.gameObject.activeInHierarchy;
        private static bool IsState(TileTextView view, string state) => ((Animator)typeof(TileTextView).GetField("_occupantVisualAnimator", Hidden).GetValue(view)).GetCurrentAnimatorStateInfo(0).IsName(state);
        private static void AssertState(TileTextView view, string state)
        {
            Assert.That(IsState(view, state), Is.True, state);
            Assert.That(typeof(TileTextView).GetField("_activeSpriteSequence", Hidden).GetValue(view), Is.Null, "Registered card Animator Controller owns its motion.");
        }
        private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Hidden).SetValue(instance, value);
    }
}
