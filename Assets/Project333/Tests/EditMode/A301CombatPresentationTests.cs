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
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class A301CombatPresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static Type AssetsType => typeof(TileTextView).Assembly.GetType("Project333.Runtime.Presentation.Battle.A301PresentationAssets", true);
        private static Type EffectType => typeof(TileTextView).Assembly.GetType("Project333.Runtime.Presentation.Battle.A301LaserAttack", true);
        [TearDown] public void Cleanup() => BoardAnimationControllerTestUtility.Cleanup();
        [UnityTearDown] public IEnumerator LeavePlayMode() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void RegisteredClipsUseFullCellsAndShortQuietLaserSound()
        {
            foreach (var state in new[] { "Idle", "Attack", "Death" })
            {
                var clip = BoardAnimationControllerTestUtility.Clip("A-301", state);
                var frames = BoardAnimationControllerTestUtility.Frames(clip);
                Assert.That(frames.Length, Is.EqualTo(8), state);
                Assert.That(frames.All(f => f != null && f.rect.size == new Vector2(512, 512)), Is.True);
                Assert.That(clip.length, Is.EqualTo(state == "Attack" ? .8f : 1f).Within(.001f));
                Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.EqualTo(state == "Idle"));
            }
            var sound = (AudioClip)AssetsType.GetProperty("AttackSound", Static).GetValue(null);
            Assert.That(sound, Is.Not.Null); Assert.That(sound.channels, Is.EqualTo(1));
            Assert.That(sound.length, Is.InRange(.15f, .35f));
            var samples = new float[sound.samples]; Assert.That(sound.GetData(samples, 0), Is.True);
            Assert.That(samples.Max(v => Mathf.Abs(v)), Is.LessThan(.25f), "Quiet cue has at least 12dB of peak headroom before runtime gain.");
        }

        [UnityTest]
        public IEnumerator StraightBeamTracksEmitterAndSharedLoweredTargetInBothDirections()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture(); var oldTimeScale = Time.timeScale;
            try
            {
                fixture.WarmResources(); yield return null;
                Time.timeScale = 0f;
                var size = fixture.Left.GetComponentsInChildren<Image>(true).First(i => i.name == "OccupantVisual").rectTransform.sizeDelta;
                fixture.Shot(fixture.Left, fixture.Right);
                fixture.AssertBeam(fixture.Left, fixture.Right);
                Assert.That(fixture.Beam.raycastTarget, Is.False);
                Assert.That(fixture.Beam.GetComponentInParent<CanvasGroup>().blocksRaycasts, Is.False);
                var move = fixture.Right.GetVisualNormalizedWorldPosition(new Vector2(.54f, .55f)) -
                    fixture.Right.GetVisualNormalizedWorldPosition(Vector2.one * .5f);
                fixture.Right.SetVisualWorldPosition(fixture.Right.GetVisualWorldPosition() + move);
                yield return null;
                fixture.AssertBeam(fixture.Left, fixture.Right);
                Assert.That(fixture.Left.GetComponentsInChildren<Image>(true).First(i => i.name == "OccupantVisual").rectTransform.sizeDelta, Is.EqualTo(size));
                fixture.Capture("a301-straight-laser.png");
                fixture.Shot(fixture.Right, fixture.Left);
                fixture.AssertBeam(fixture.Right, fixture.Left);
                fixture.Capture("a301-reverse-laser.png");
                yield return new WaitForSecondsRealtime(.30f);
                Assert.That(fixture.Beam.gameObject.activeSelf, Is.False, "Unscaled beam timing continues while the game clock is paused.");
                Assert.That(fixture.Effect.GetComponents<AudioSource>().Length, Is.EqualTo(1), "Repeated shots reuse a single quiet source.");
            }
            finally { Time.timeScale = oldTimeScale; fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CancelReplacementAndDisabledViewsEndBeamAndAudio()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                fixture.WarmResources(); yield return null;
                fixture.Shot(fixture.Left, fixture.Right);
                Assert.That(fixture.Beam.gameObject.activeSelf, Is.True);
                fixture.Bootstrapper.CancelCombatPresentation();
                fixture.AssertHiddenAndSilent();
                fixture.Shot(fixture.Left, fixture.Right);
                fixture.Shot(fixture.Right, fixture.Left);
                fixture.AssertBeam(fixture.Right, fixture.Left);
                fixture.Left.GetComponent<TileView>().Present(Unit(PlayerId.Player, "replacement"));
                fixture.Left.SetTile("replacement", "A-301", true);
                yield return null;
                fixture.AssertHiddenAndSilent();
                fixture.Shot(fixture.Left, fixture.Right);
                fixture.Right.enabled = false; yield return null;
                fixture.AssertHiddenAndSilent();
                fixture.Right.enabled = true;
                fixture.Shot(fixture.Left, fixture.Right);
                fixture.Effect.enabled = false;
                fixture.AssertHiddenAndSilent();
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator HoveringAttackEmitsBeamAndRecordedDamageTogetherThenReturnsIdle()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                fixture.WarmResources(); yield return null;
                var origin = fixture.Left.GetVisualWorldPosition();
                var completion = fixture.Play(fixture.Request(false));
                yield return new WaitForSecondsRealtime(.08f);
                AssertState(fixture.Left, "Attack");
                Assert.That(fixture.Beam == null || !fixture.Beam.gameObject.activeSelf, Is.True, "First two frames charge before emission.");
                Assert.That(fixture.PopupCount, Is.Zero);
                while ((fixture.Beam == null || !fixture.Beam.gameObject.activeSelf) && Time.realtimeSinceStartup < completion.Deadline) yield return null;
                Assert.That(fixture.Beam != null && fixture.Beam.gameObject.activeSelf, Is.True);
                Assert.That(fixture.PopupCount, Is.EqualTo(1));
                AssertState(fixture.Left, "Attack"); AssertState(fixture.Right, "BeAttacked");
                Assert.That(fixture.Effect.GetComponents<AudioSource>().Any(a => a.isPlaying), Is.True);
                Assert.That(fixture.Right.GetComponents<AudioSource>().Any(a => a.isPlaying), Is.True, "Target uses the common UnitHit at emission.");
                Assert.That(Vector3.Distance(fixture.Left.GetVisualWorldPosition(), origin), Is.LessThan(.01f), "Flying ranged unit remains on its tile.");
                while (!completion.Done && Time.realtimeSinceStartup < completion.Deadline) yield return null;
                Assert.That(completion.Done, Is.True); AssertState(fixture.Left, "Idle"); AssertState(fixture.Right, "Idle");
                Assert.That(fixture.Left.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(100));
                Assert.That(fixture.Right.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(100), "Presentation never applies the recorded damage.");
                fixture.Capture("a301-hover-idle.png");
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CancelDuringChargeCannotCreateDelayedHitOrBeam()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                fixture.WarmResources(); yield return null;
                var completion = fixture.Play(fixture.Request(false));
                yield return new WaitForSecondsRealtime(.08f);
                fixture.Bootstrapper.CancelCombatPresentation();
                yield return new WaitForSecondsRealtime(.30f);
                Assert.That(completion.Done, Is.True); Assert.That(fixture.PopupCount, Is.Zero);
                fixture.AssertHiddenAndSilent(); AssertState(fixture.Left, "Idle"); AssertState(fixture.Right, "Idle");
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ReboundTargetAtEmissionBoundaryCannotReceiveOldAttackCues()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture(); IEnumerator exchange = null;
            try
            {
                fixture.WarmResources(); yield return null;
                var request = fixture.Request(false);
                var identities = typeof(BattleBootstrapper).GetMethod("CaptureLaserImpactIdentities", Hidden).Invoke(fixture.Bootstrapper, new[] { request });
                exchange = (IEnumerator)typeof(BattleBootstrapper).GetMethod("PlayLaserAttackExchange", Hidden).Invoke(fixture.Bootstrapper,
                    new object[] { request, fixture.Left, fixture.Right, 0, fixture.Left.VisualRuntimeId, fixture.Right.VisualRuntimeId, identities });
                Assert.That(exchange.MoveNext(), Is.True);
                yield return new WaitForSecondsRealtime(.22f);
                fixture.Right.GetComponent<TileView>().Present(Unit(PlayerId.AI, "replacement-at-emission"));
                fixture.Right.SetTile("replacement", "A-301", true);
                Assert.That(exchange.MoveNext(), Is.False);
                Assert.That(fixture.PopupCount, Is.Zero); fixture.AssertHiddenAndSilent(); AssertState(fixture.Right, "Idle");
            }
            finally { (exchange as IDisposable)?.Dispose(); fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SecondaryOverflowTargetReboundDuringChargeCannotReceiveReactionOrDeath()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                fixture.WarmResources(); yield return null;
                var secondary = fixture.AddSecondary();
                var request = fixture.Request(false);
                var type = request.GetType(); var coord = new TileCoord(1, 0);
                var impacts = (IList)type.GetProperty("DefenderImpacts").GetValue(request);
                var impactType = typeof(BattleBootstrapper).GetNestedType("AttackAnimationImpact", BindingFlags.NonPublic);
                impacts.Add(Activator.CreateInstance(impactType, new object[] { PlayerId.AI, coord, true, true }));
                ((List<BattleValuePopupEvent>)type.GetProperty("ValuePopupEvents").GetValue(request)).Add(
                    new BattleValuePopupEvent(secondary.VisualRuntimeId, PlayerId.AI, coord, false, 6,
                        PlayerId.Player, fixture.Left.VisualRuntimeId, "A-301", BattleValueChangeCause.NormalAttack, DamageType.Magic, 100, 94));
                var sourceId = fixture.Left.VisualRuntimeId; var primaryId = fixture.Right.VisualRuntimeId;
                var completion = fixture.Play(request);
                yield return new WaitForSecondsRealtime(.08f);
                secondary.GetComponent<TileView>().Present(Unit(PlayerId.AI, "replacement-secondary", 1));
                secondary.SetTile("replacement", "A-301", true);
                yield return new WaitForSecondsRealtime(.26f);
                Assert.That(fixture.Left.VisualRuntimeId, Is.EqualTo(sourceId)); Assert.That(fixture.Right.VisualRuntimeId, Is.EqualTo(primaryId));
                Assert.That(completion.Done, Is.True, "Secondary identity guard must stop old presentation even while primary identities remain valid.");
                Assert.That(fixture.PopupCount, Is.Zero); fixture.AssertHiddenAndSilent();
                AssertState(secondary, "Idle"); AssertState(fixture.Left, "Idle"); AssertState(fixture.Right, "Idle");
                Assert.That(secondary.GetComponents<AudioSource>().Any(a => a.isPlaying), Is.False, "Replacement must not receive an old common hit.");
                Assert.That(secondary.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(100));
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SecondaryOverflowTargetReboundAfterEmissionCannotReceiveOldDeath()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                fixture.WarmResources(); yield return null;
                var secondary = fixture.AddSecondary(); var request = fixture.Request(false);
                var impactType = typeof(BattleBootstrapper).GetNestedType("AttackAnimationImpact", BindingFlags.NonPublic);
                ((IList)request.GetType().GetProperty("DefenderImpacts").GetValue(request)).Add(
                    Activator.CreateInstance(impactType, new object[] { PlayerId.AI, new TileCoord(1, 0), true, true }));
                var completion = fixture.Play(request);
                while ((fixture.Beam == null || !fixture.Beam.gameObject.activeSelf) && Time.realtimeSinceStartup < completion.Deadline) yield return null;
                Assert.That(fixture.Beam != null && fixture.Beam.gameObject.activeSelf, Is.True);
                AssertState(secondary, "BeAttacked");
                secondary.GetComponent<TileView>().Present(Unit(PlayerId.AI, "replacement-after-emission", 1));
                secondary.SetTile("replacement", "A-301", true);
                var deadline = Time.realtimeSinceStartup + 1f;
                while (!completion.Done && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(completion.Done, Is.True, "Old death hold must not run for a replaced secondary occupant.");
                fixture.AssertHiddenAndSilent(); AssertState(secondary, "Idle"); AssertState(fixture.Left, "Idle");
                Assert.That(secondary.GetComponents<AudioSource>().Any(a => a.isPlaying), Is.False);
                Assert.That(secondary.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(100));
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DefeatedTargetShowsRegisteredFullDeathClipAfterLaserEnds()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                fixture.WarmResources(); yield return null;
                var completion = fixture.Play(fixture.Request(true));
                while (!IsState(fixture.Right, "Death") && Time.realtimeSinceStartup < completion.Deadline) yield return null;
                AssertState(fixture.Right, "Death"); fixture.AssertHiddenAndSilent();
                var deathStarted = Time.realtimeSinceStartup;
                yield return new WaitForSecondsRealtime(.45f);
                Assert.That(completion.Done, Is.False);
                fixture.Capture("a301-death-collapse.png");
                while (!completion.Done && Time.realtimeSinceStartup < completion.Deadline) yield return null;
                Assert.That(completion.Done, Is.True);
                Assert.That(Time.realtimeSinceStartup - deathStarted, Is.GreaterThanOrEqualTo(.95f));
                Assert.That(fixture.Right.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(100));
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root = new GameObject("A301CombatFixture");
            internal readonly TileTextView Left, Right;
            internal readonly MonoBehaviour Effect;
            internal readonly BattleBootstrapper Bootstrapper;
            private readonly BoardPresenter _board;
            private readonly Camera _camera; private readonly RenderTexture _render;
            internal Fixture()
            {
                var cameraObject = new GameObject("PreviewCamera", typeof(Camera), typeof(AudioListener)); cameraObject.transform.SetParent(Root.transform);
                _camera = cameraObject.GetComponent<Camera>(); _camera.transform.position = new Vector3(0, 0, -10);
                _camera.orthographic = true; _camera.orthographicSize = 5; _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(.035f, .045f, .06f);
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                { _render = new RenderTexture(1000, 560, 24); _render.Create(); _camera.targetTexture = _render; }
                Left = CreateTile(Root.transform, _camera, PlayerId.Player, -230);
                Right = CreateTile(Root.transform, _camera, PlayerId.AI, 230);
                var effects = new GameObject("LaserEffectHost"); effects.transform.SetParent(Root.transform); Effect = (MonoBehaviour)effects.AddComponent(EffectType);
                var host = new GameObject("DormantPresentationCoordinator"); host.SetActive(false); host.transform.SetParent(Root.transform);
                Bootstrapper = host.AddComponent<BattleBootstrapper>();
                var screen = host.AddComponent<BattleScreenPresenter>(); _board = host.AddComponent<BoardPresenter>();
                Set(Bootstrapper, "_battleScreenPresenter", screen); Set(screen, "_boardPresenter", _board);
                Set(_board, "_playerTileViews", new[] { Left.GetComponent<TileView>() }); Set(_board, "_aiTileViews", new[] { Right.GetComponent<TileView>() });
                Set(Bootstrapper, "_a301LaserAttack", Effect); Canvas.ForceUpdateCanvases();
            }
            internal Image Beam => Root.GetComponentsInChildren<Image>(true).SingleOrDefault(i => i.name == "LaserBeam2");
            internal int PopupCount => Root.GetComponentsInChildren<TMP_Text>(true).Count(t => t.name == "ValuePopupText" && t.text == "6");
            internal void WarmResources() => AssetsType.GetProperty("AttackSound", Static).GetValue(null);
            internal TileTextView AddSecondary()
            {
                var view = CreateTile(Root.transform, _camera, PlayerId.AI, 480, 1);
                Set(_board, "_aiTileViews", new[] { Right.GetComponent<TileView>(), view.GetComponent<TileView>() });
                return view;
            }
            internal void Shot(TileTextView source, TileTextView target) => EffectType.GetMethod("PlayShot", Hidden).Invoke(Effect, new object[] { source, target });
            internal void AssertBeam(TileTextView source, TileTextView target)
            {
                Assert.That(Beam != null && Beam.gameObject.activeSelf, Is.True);
                var emitter = (Vector2)AssetsType.GetField("EmitterPoint", Static).GetValue(null);
                Assert.That(Vector3.Distance(Beam.rectTransform.position, source.GetVisualNormalizedWorldPosition(emitter)), Is.LessThan(.01f));
                Assert.That(Vector3.Distance(Beam.rectTransform.TransformPoint(new Vector3(Beam.rectTransform.rect.width, 0, 0)), target.GetVisualImpactWorldPosition()), Is.LessThan(.01f),
                    "Beam's full straight segment ends at the shared lower impact anchor.");
                var impact = Root.GetComponentsInChildren<Image>(true).Single(i => i.name == "LaserImpact0");
                Assert.That(Vector3.Distance(impact.rectTransform.position, target.GetVisualImpactWorldPosition()), Is.LessThan(.01f));
            }
            internal void AssertHiddenAndSilent()
            {
                Assert.That(Root.GetComponentsInChildren<Image>(true).Where(i => i.name.StartsWith("Laser", StringComparison.Ordinal)).Any(i => i.gameObject.activeSelf), Is.False);
                Assert.That(Effect.GetComponents<AudioSource>().Any(a => a.isPlaying), Is.False);
            }
            internal object Request(bool died)
            {
                var type = typeof(BattleBootstrapper).GetNestedType("AttackAnimationRequest", BindingFlags.NonPublic); var coord = new TileCoord(0, 0);
                var request = Activator.CreateInstance(type, new object[] { PlayerId.Player, coord, PlayerId.AI, coord, coord, true });
                type.GetProperty("AttackerCardId").SetValue(request, "A-301"); type.GetProperty("DefenderCardId").SetValue(request, "A-301");
                var impacts = (IList)type.GetProperty("DefenderImpacts").GetValue(request);
                var impactType = typeof(BattleBootstrapper).GetNestedType("AttackAnimationImpact", BindingFlags.NonPublic);
                impacts.Add(Activator.CreateInstance(impactType, new object[] { PlayerId.AI, coord, true, died }));
                ((List<BattleValuePopupEvent>)type.GetProperty("ValuePopupEvents").GetValue(request)).Add(new BattleValuePopupEvent(Right.VisualRuntimeId, PlayerId.AI, coord,
                    false, 6, PlayerId.Player, Left.VisualRuntimeId, "A-301", BattleValueChangeCause.NormalAttack, DamageType.Magic, 100, 94));
                return request;
            }
            internal Completion Play(object request)
            {
                var sequence = (IEnumerator)typeof(BattleBootstrapper).GetMethod("PlayAttackAnimationSequence", Hidden).Invoke(Bootstrapper, new[] { request });
                var completion = new Completion { Deadline = Time.realtimeSinceStartup + 4f }; Effect.StartCoroutine(Complete(sequence, completion)); return completion;
            }
            internal void Capture(string filename)
            {
                if (_render == null) return; Canvas.ForceUpdateCanvases(); _camera.Render();
                var old = RenderTexture.active; var texture = new Texture2D(_render.width, _render.height, TextureFormat.RGB24, false);
                try { RenderTexture.active = _render; texture.ReadPixels(new Rect(0, 0, _render.width, _render.height), 0, 0); texture.Apply();
                    var directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Captures")); Directory.CreateDirectory(directory);
                    File.WriteAllBytes(Path.Combine(directory, filename), texture.EncodeToPNG()); }
                finally { RenderTexture.active = old; Object.DestroyImmediate(texture); }
            }
            public void Dispose() { Object.DestroyImmediate(Root); if (_render != null) { _render.Release(); Object.DestroyImmediate(_render); } }
        }
        private static TileTextView CreateTile(Transform fixture, Camera camera, PlayerId owner, float x, int column = 0)
        {
            var shell = new GameObject("A301" + owner); shell.SetActive(false); shell.transform.SetParent(fixture);
            var tile = shell.AddComponent<TileView>(); tile.Configure(owner, column, 0); tile.Present(Unit(owner, null, column)); var view = shell.AddComponent<TileTextView>();
            var canvasObject = new GameObject("TileCanvas", typeof(RectTransform), typeof(Canvas)); canvasObject.transform.SetParent(shell.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var label = new GameObject("TileText", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(canvasObject.transform, false);
            var text = label.GetComponent<TextMeshProUGUI>(); text.enabled = false; text.rectTransform.anchoredPosition = new Vector2(x, -30);
            var visual = new GameObject("OccupantVisual", typeof(RectTransform), typeof(Image), typeof(Animator)); visual.transform.SetParent(canvasObject.transform, false);
            Set(view, "_tileView", tile); Set(view, "_text", text); Set(view, "_showOccupantStats", false); Set(view, "_showHuanShuStatusIcon", false); Set(view, "_useTileImageClickSurface", false);
            Set(view, "_occupantVisualRoot", visual.GetComponent<RectTransform>()); Set(view, "_occupantVisualImage", visual.GetComponent<Image>());
            Set(view, "_occupantVisualAnimator", visual.GetComponent<Animator>()); Set(view, "_occupantVisualSize", new Vector2(340, 340)); Set(view, "_occupantVisualAnchoredPosition", new Vector2(x, 0));
            BoardAnimationControllerTestUtility.Bind(view, "A-301"); view.SetTile(owner.ToString(), "A-301", true); shell.SetActive(true); view.PlayIdleAnimation(); return view;
        }
        private static UnitState Unit(PlayerId owner, string id = null, int column = 0) => new UnitState(id ?? "a301-" + owner + "-" + column, "A-301", owner, new TileCoord(column, 0), AttackType.Ranged, 6, 100, true, false, 0);
        private sealed class Completion { internal bool Done; internal float Deadline; }
        private static IEnumerator Complete(IEnumerator sequence, Completion completion) { yield return sequence; completion.Done = true; }
        private static bool IsState(TileTextView view, string state) => ((Animator)typeof(TileTextView).GetField("_occupantVisualAnimator", Hidden).GetValue(view)).GetCurrentAnimatorStateInfo(0).IsName(state);
        private static void AssertState(TileTextView view, string state)
        {
            Assert.That(IsState(view, state), Is.True, state);
            Assert.That(typeof(TileTextView).GetField("_activeSpriteSequence", Hidden).GetValue(view), Is.Null, "Registered card controller owns the motion.");
        }
        private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Hidden).SetValue(instance, value);
    }
}
