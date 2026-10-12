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
    public sealed class A212CombatPresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static Type AssetsType => typeof(TileTextView).Assembly.GetType("Project333.Runtime.Presentation.Battle.A212PresentationAssets", true);
        private static Type EffectType => typeof(TileTextView).Assembly.GetType("Project333.Runtime.Presentation.Battle.A212SawAttack", true);

        [TearDown] public void Cleanup() => BoardAnimationControllerTestUtility.Cleanup();
        [UnityTearDown] public IEnumerator LeavePlayMode() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void PresentationReadsEffectiveHitsWithoutChangingCombatState()
        {
            var root = new GameObject("A212SuppressedPresentation"); root.SetActive(false);
            try
            {
                var tile = root.AddComponent<TileView>(); tile.Configure(PlayerId.Player, 0, 0);
                var unit = Unit(PlayerId.Player); tile.Present(unit);
                var view = root.AddComponent<TileTextView>(); Set(view, "_tileView", tile);
                var hits = typeof(TileTextView).GetProperty("PresentedHitsPerAttack", Hidden);
                Assert.That(hits.GetValue(view), Is.EqualTo(4));
                unit.ApplyErasure();
                Assert.That(hits.GetValue(view), Is.EqualTo(1), "Erasure's existing one-hit rule must reach the VFX.");
                Assert.That(unit.CurrentHp, Is.EqualTo(100));
                Assert.That(unit.HitsPerAttack, Is.EqualTo(4));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SawResourcesHaveEightFullCellsAndOneShortAttackSound()
        {
            var frames = (IReadOnlyList<Sprite>)AssetsType.GetProperty("ImpactFrames", Static).GetValue(null);
            Assert.That(frames, Is.Not.Null); Assert.That(frames.Count, Is.EqualTo(8));
            Assert.That(frames.Select(f => f.rect).Distinct().Count(), Is.EqualTo(8));
            foreach (var frame in frames)
            {
                Assert.That(frame.rect.size, Is.EqualTo(new Vector2(512, 512)));
                Assert.That(frame.pivot, Is.EqualTo(new Vector2(256, 256)));
            }
            var sound = (AudioClip)AssetsType.GetProperty("AttackSound", Static).GetValue(null);
            Assert.That(sound, Is.Not.Null); Assert.That(sound.length, Is.InRange(0.2f, 0.8f));
            Assert.That(sound.channels, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TwoSlotsUseLoweredAnchorAndCancelReplacementSafely()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            var oldScale = Time.timeScale;
            try
            {
                fixture.WarmResources();
                // Resource preparation can take a frame; begin timing from a settled frame.
                yield return null;
                var size = fixture.Left.GetComponentInChildren<Image>().rectTransform.sizeDelta;
                Time.timeScale = 0f;
                fixture.Strike(fixture.Left, fixture.Right, 4, false);
                fixture.Strike(fixture.Right, fixture.Left, 4, true);
                Assert.That(fixture.Sparks(false).gameObject.activeSelf, Is.False, "Motor anticipation precedes contact.");
                yield return new WaitForSecondsRealtime(0.29f);
                foreach (var counter in new[] { false, true })
                {
                    var image = fixture.Sparks(counter); var target = counter ? fixture.Left : fixture.Right;
                    Assert.That(image.gameObject.activeSelf, Is.True, "Both directions advance while time scale is zero.");
                    Assert.That(Vector3.Distance(image.rectTransform.position, target.GetVisualImpactWorldPosition()), Is.LessThan(.01f));
                    Assert.That(image.raycastTarget, Is.False);
                    Assert.That(image.GetComponentInParent<CanvasGroup>().blocksRaycasts, Is.False);
                }
                Assert.That(fixture.Effect.GetComponents<AudioSource>().Length, Is.EqualTo(2), "One reusable saw emitter per direction, independent of four hits.");
                Assert.That(fixture.Effect.GetComponents<AudioSource>().All(a => Mathf.Abs(a.volume - 1f) < .001f), Is.True);
                fixture.Right.SetVisualWorldPosition(fixture.Right.GetVisualWorldPosition() + new Vector3(12, -7, 0));
                yield return null;
                Assert.That(Vector3.Distance(fixture.Sparks(false).rectTransform.position,
                    fixture.Right.GetVisualImpactWorldPosition()), Is.LessThan(.01f));
                Assert.That(fixture.Left.GetComponentInChildren<Image>().rectTransform.sizeDelta, Is.EqualTo(size), "VFX cannot rescale the unit.");
                fixture.Bootstrapper.CancelCombatPresentation();
                Assert.That(fixture.Sparks(false).gameObject.activeSelf || fixture.Sparks(true).gameObject.activeSelf, Is.False);
                Assert.That(fixture.Effect.GetComponents<AudioSource>().Any(a => a.isPlaying), Is.False);
                yield return new WaitForSecondsRealtime(.55f);
                Assert.That(fixture.Sparks(false).gameObject.activeSelf || fixture.Sparks(true).gameObject.activeSelf, Is.False);

                fixture.Strike(fixture.Left, fixture.Right, 4, false);
                fixture.Right.GetComponent<TileView>().Present(Unit(PlayerId.AI, "replacement"));
                fixture.Right.SetTile("right", "A-212", true);
                yield return new WaitForSecondsRealtime(.29f);
                Assert.That(fixture.Sparks(false).gameObject.activeSelf, Is.False, "The old attack cannot strike a replacement occupant.");

                fixture.Strike(fixture.Left, fixture.Right, 4, false);
                yield return new WaitForSecondsRealtime(.29f);
                fixture.Strike(fixture.Left, fixture.Right, 1, false);
                yield return new WaitForSecondsRealtime(.29f);
                Assert.That(fixture.Sparks(false).gameObject.activeSelf, Is.True, "Replacing the same slot must not let old cleanup hide the new strike.");
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(fixture.Sparks(false).gameObject.activeSelf, Is.False);
                var strikeRoutines = (Coroutine[])EffectType.GetField("_strikes", Hidden).GetValue(fixture.Effect);
                Assert.That(strikeRoutines[0], Is.Not.Null, "A one-hit Erasure strike hides its sparks while guarding the full motor sound tail.");
                yield return new WaitForSecondsRealtime(.30f);
                Assert.That(strikeRoutines[0], Is.Null, "The guarded motor tail completes after its full authored duration.");
                Assert.That(fixture.Effect.GetComponents<AudioSource>().Any(a => a.isPlaying), Is.False);
                Assert.That(fixture.Effect.GetComponents<AudioSource>().Length, Is.EqualTo(2), "Repeated actions reuse their two sound emitters.");
            }
            finally { Time.timeScale = oldScale; fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator FourHitExchangeKeepsControllerMotionAndDamageAuthority()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                fixture.WarmResources();
                // Resource preparation can take a frame; begin timing from a settled frame.
                yield return null;
                var request = fixture.Request(true, true);
                var origin = fixture.Left.GetVisualWorldPosition();
                var completion = fixture.Play(request);
                yield return new WaitForSecondsRealtime(.12f);
                AssertState(fixture.Left, "Run");
                Assert.That(Vector3.Distance(fixture.Left.GetVisualWorldPosition(), origin), Is.GreaterThan(.1f));
                yield return new WaitForSecondsRealtime(.24f);
                AssertState(fixture.Left, "Attack"); AssertState(fixture.Right, "Attack");
                // Observe the fourth contact pulse and its recorded packets in the same Attack phase.
                // Five-millisecond pulse gaps make fixed wall-clock sampling unreliable.
                var contactDeadline = Time.realtimeSinceStartup + 1f;
                var observedContactWithAllPackets = false;
                while (Time.realtimeSinceStartup < contactDeadline &&
                    IsState(fixture.Left, "Attack") && IsState(fixture.Right, "Attack"))
                {
                    var visiblePacketCount = fixture.Root.GetComponentsInChildren<TMP_Text>(true)
                        .Count(t => t.name == "ValuePopupText" && t.text == "6");
                    if (fixture.Sparks(false).gameObject.activeSelf && fixture.Sparks(true).gameObject.activeSelf &&
                        visiblePacketCount == 8)
                    {
                        observedContactWithAllPackets = true;
                        break;
                    }
                    yield return null;
                }
                Assert.That(observedContactWithAllPackets, Is.True,
                    "Both saw directions must visibly contact during Attack with four outgoing and four independent counter packets shown.");
                AssertState(fixture.Left, "Attack"); AssertState(fixture.Right, "Attack");
                Assert.That(fixture.Root.GetComponentsInChildren<TMP_Text>(true)
                    .Count(t => t.name == "ValuePopupText" && t.text == "6"), Is.EqualTo(8),
                    "Four recorded outgoing packets and four independent counter packets are shown during grinding.");
                // Capture only after the packet count is proven, so GPU work cannot age out early popups.
                fixture.Capture("a212-saw-contact.png");

                while (!completion.Done && Time.realtimeSinceStartup < completion.Deadline)
                {
                    var animator = (Animator)typeof(TileTextView).GetField("_occupantVisualAnimator", Hidden).GetValue(fixture.Left);
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("BeAttacked"), Is.False, "Counter damage cannot replace the initiating attack pose.");
                    yield return null;
                }
                Assert.That(completion.Done, Is.True);
                AssertState(fixture.Left, "Idle"); AssertState(fixture.Right, "Idle");
                Assert.That(Vector3.Distance(fixture.Left.GetVisualWorldPosition(), origin), Is.LessThan(.01f));
                Assert.That(fixture.Left.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(100));
                Assert.That(fixture.Right.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(100), "Presentation never applies damage.");
                fixture.Capture("a212-return-idle.png");

                var cancelled = fixture.Play(fixture.Request(true, true));
                yield return new WaitForSecondsRealtime(.35f);
                fixture.Bootstrapper.CancelCombatPresentation();
                var popupCount = fixture.Root.GetComponentsInChildren<TMP_Text>(true).Count(t => t.name == "ValuePopupText");
                yield return new WaitForSecondsRealtime(.8f);
                Assert.That(cancelled.Done, Is.True);
                Assert.That(fixture.Root.GetComponentsInChildren<TMP_Text>(true).Count(t => t.name == "ValuePopupText"), Is.LessThanOrEqualTo(popupCount), "Cancelled grinding must not create delayed damage cues.");
                AssertState(fixture.Left, "Idle"); AssertState(fixture.Right, "Idle");
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DeathWaitsForRegisteredClipAndEndsSawAudio()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                fixture.WarmResources();
                // Resource preparation can take a frame; begin timing from a settled frame.
                yield return null;
                var request = fixture.Request(true, false);
                request.GetType().GetProperty("AttackerDied").SetValue(request, true);
                var impactType = typeof(BattleBootstrapper).GetNestedType("AttackAnimationImpact", BindingFlags.NonPublic);
                var impacts = (IList)request.GetType().GetProperty("DefenderImpacts").GetValue(request);
                impacts.Add(Activator.CreateInstance(impactType, new object[] { PlayerId.AI, new TileCoord(0, 0), false, true }));
                var completion = fixture.Play(request);
                var deadline = Time.realtimeSinceStartup + 3f;
                while (!IsState(fixture.Left, "Death") && Time.realtimeSinceStartup < deadline) yield return null;
                AssertState(fixture.Left, "Death"); AssertState(fixture.Right, "Death");
                var deathStarted = Time.realtimeSinceStartup;
                fixture.Capture("a212-death-collapse.png");
                Assert.That(fixture.Effect.GetComponents<AudioSource>().Any(a => a.isPlaying), Is.False);
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(completion.Done, Is.False, "The full one-second Death clip must remain visible.");
                while (!completion.Done && Time.realtimeSinceStartup < completion.Deadline) yield return null;
                Assert.That(completion.Done, Is.True);
                Assert.That(Time.realtimeSinceStartup - deathStarted, Is.GreaterThanOrEqualTo(.95f));
                Assert.That(fixture.Left.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(100));
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CancelAtFinalYieldCannotReactOnReplacementOccupant()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            IEnumerator exchange = null;
            try
            {
                fixture.WarmResources();
                // Resource preparation can take a frame; begin timing from a settled frame.
                yield return null;
                var request = fixture.Request(true, false);
                var impactType = typeof(BattleBootstrapper).GetNestedType("AttackAnimationImpact", BindingFlags.NonPublic);
                ((IList)request.GetType().GetProperty("DefenderImpacts").GetValue(request)).Add(
                    Activator.CreateInstance(impactType, new object[] { PlayerId.AI, new TileCoord(0, 0), true, false }));
                exchange = (IEnumerator)typeof(BattleBootstrapper).GetMethod("PlayContactAttackExchange", Hidden).Invoke(
                    fixture.Bootstrapper, new object[] { request, fixture.Left, fixture.Right, 0,
                        fixture.Left.VisualRuntimeId, fixture.Right.VisualRuntimeId });
                Assert.That(exchange.MoveNext(), Is.True, "Suspend at the grind loop's final pending yield.");
                yield return new WaitForSecondsRealtime(1.05f);
                fixture.Right.GetComponent<TileView>().Present(Unit(PlayerId.AI, "replacement-at-end"));
                fixture.Right.SetTile("replacement", "A-212", true);
                fixture.Bootstrapper.CancelCombatPresentation();
                AssertState(fixture.Right, "Idle");
                Assert.That(exchange.MoveNext(), Is.False,
                    "Elapsed time can skip the loop condition, so cancellation must also guard the reaction boundary.");
                AssertState(fixture.Right, "Idle");
                Assert.That(fixture.Right.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(100));
            }
            finally { (exchange as IDisposable)?.Dispose(); fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator TravelStartsAtOriginAndCannotSnapDisabledView()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            IEnumerator travel = null;
            try
            {
                fixture.WarmResources();
                yield return null;
                var origin = fixture.Left.GetVisualWorldPosition();
                var destination = origin + new Vector3(120f, 24f, 0f);
                travel = (IEnumerator)typeof(BattleBootstrapper).GetMethod("AnimateVisualTravel", Hidden).Invoke(
                    fixture.Bootstrapper, new object[] { fixture.Left, origin, destination, .28f });
                Assert.That(travel.MoveNext(), Is.True, "Travel must yield before consuming time from earlier in its starting frame.");
                Assert.That(Vector3.Distance(fixture.Left.GetVisualWorldPosition(), origin), Is.LessThan(.0001f),
                    "The first travel tick starts exactly at the origin, even when the current frame has a nonzero delta.");
                yield return new WaitForSecondsRealtime(.10f);
                Assert.That(travel.MoveNext(), Is.True);
                var intermediate = fixture.Left.GetVisualWorldPosition();
                Assert.That(Vector3.Distance(intermediate, origin), Is.GreaterThan(.1f));
                Assert.That(Vector3.Distance(intermediate, destination), Is.GreaterThan(.1f),
                    "A subsequent tick must visibly advance without jumping to the destination.");
                var distance = Vector3.Distance(origin, destination);
                Assert.That(Vector3.Distance(intermediate, origin), Is.LessThan(distance));
                Assert.That(Vector3.Distance(intermediate, destination), Is.LessThan(distance));
                fixture.Left.enabled = false;
                Assert.That(travel.MoveNext(), Is.False, "Disabled views abort travel.");
                Assert.That(Vector3.Distance(fixture.Left.GetVisualWorldPosition(), intermediate), Is.LessThan(.0001f),
                    "Aborting must not perform the final destination snap.");
            }
            finally { (travel as IDisposable)?.Dispose(); fixture.Dispose(); }
            yield return new ExitPlayMode();
        }


        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root = new GameObject("A212CombatFixture");
            internal readonly TileTextView Left, Right;
            internal readonly MonoBehaviour Effect;
            internal readonly BattleBootstrapper Bootstrapper;
            private readonly Camera _camera;
            private readonly RenderTexture _render;
            internal Fixture()
            {
                var cameraObject = new GameObject("PreviewCamera", typeof(Camera), typeof(AudioListener)); cameraObject.transform.SetParent(Root.transform);
                _camera = cameraObject.GetComponent<Camera>(); _camera.transform.position = new Vector3(0, 0, -10);
                _camera.orthographic = true; _camera.orthographicSize = 5; _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(.035f, .045f, .06f);
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    _render = new RenderTexture(1000, 560, 24); _render.Create(); _camera.targetTexture = _render;
                }
                Left = CreateTile(Root.transform, _camera, PlayerId.Player, -230);
                Right = CreateTile(Root.transform, _camera, PlayerId.AI, 230);
                var effects = new GameObject("SawEffectHost"); effects.transform.SetParent(Root.transform);
                Effect = (MonoBehaviour)effects.AddComponent(EffectType);
                var host = new GameObject("DormantPresentationCoordinator"); host.SetActive(false); host.transform.SetParent(Root.transform);
                Bootstrapper = host.AddComponent<BattleBootstrapper>();
                var screen = host.AddComponent<BattleScreenPresenter>(); var board = host.AddComponent<BoardPresenter>();
                Set(Bootstrapper, "_battleScreenPresenter", screen); Set(screen, "_boardPresenter", board);
                Set(board, "_playerTileViews", new[] { Left.GetComponent<TileView>() });
                Set(board, "_aiTileViews", new[] { Right.GetComponent<TileView>() });
                Set(Bootstrapper, "_a212SawAttack", Effect);
                Canvas.ForceUpdateCanvases();
            }
            internal void WarmResources()
            {
                AssetsType.GetProperty("ImpactFrames", Static).GetValue(null);
                AssetsType.GetProperty("AttackSound", Static).GetValue(null);
            }
            internal void Strike(TileTextView source, TileTextView target, int hits, bool counter) =>
                EffectType.GetMethod("PlayStrike", Hidden).Invoke(Effect, new object[] { source, target, hits, .125f, .25f, counter });
            internal Image Sparks(bool counter) => Root.GetComponentsInChildren<Image>(true).Single(i => i.name == (counter ? "SawCounterSparks" : "SawAttackSparks"));
            internal object Request(bool counter, bool popups)
            {
                var type = typeof(BattleBootstrapper).GetNestedType("AttackAnimationRequest", BindingFlags.NonPublic);
                var coord = new TileCoord(0, 0);
                var request = Activator.CreateInstance(type, new object[] { PlayerId.Player, coord, PlayerId.AI, coord, coord, false });
                type.GetProperty("AttackerCardId").SetValue(request, "A-212"); type.GetProperty("DefenderCardId").SetValue(request, "A-212");
                type.GetProperty("DefenderCounterattacked").SetValue(request, counter); type.GetProperty("AttackerTookDamage").SetValue(request, counter);
                if (popups)
                {
                    var events = (List<BattleValuePopupEvent>)type.GetProperty("ValuePopupEvents").GetValue(request);
                    for (var hit = 0; hit < 4; hit++)
                    {
                        events.Add(Cue(Right, Left, BattleValueChangeCause.NormalAttack, hit));
                        if (counter) events.Add(Cue(Left, Right, BattleValueChangeCause.Counterattack, hit));
                    }
                }
                return request;
            }
            private static BattleValuePopupEvent Cue(TileTextView target, TileTextView source, BattleValueChangeCause cause, int hit) =>
                new BattleValuePopupEvent(target.VisualRuntimeId, target.GetComponent<TileView>().OwnerId, new TileCoord(0, 0),
                    false, 6, source.GetComponent<TileView>().OwnerId, source.VisualRuntimeId, "A-212", cause, DamageType.Physical, 100-hit*6, 94-hit*6);
            internal Completion Play(object request)
            {
                var sequence = (IEnumerator)typeof(BattleBootstrapper).GetMethod("PlayAttackAnimationSequence", Hidden).Invoke(Bootstrapper, new[] { request });
                var completion = new Completion { Deadline = Time.realtimeSinceStartup + 5f };
                Effect.StartCoroutine(Complete(sequence, completion)); return completion;
            }
            internal void Capture(string filename)
            {
                if (_render == null) return;
                Canvas.ForceUpdateCanvases(); _camera.Render();
                var old = RenderTexture.active; var texture = new Texture2D(_render.width, _render.height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = _render; texture.ReadPixels(new Rect(0,0,_render.width,_render.height),0,0); texture.Apply();
                    var directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Captures"));
                    Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, filename), texture.EncodeToPNG());
                }
                finally { RenderTexture.active = old; Object.DestroyImmediate(texture); }
            }
            public void Dispose()
            {
                Object.DestroyImmediate(Root);
                if (_render != null) { _render.Release(); Object.DestroyImmediate(_render); }
            }
        }
        private static TileTextView CreateTile(Transform fixture, Camera camera, PlayerId owner, float x)
        {
            var shell = new GameObject("A212" + owner); shell.SetActive(false); shell.transform.SetParent(fixture);
            var tile = shell.AddComponent<TileView>(); tile.Configure(owner,0,0); tile.Present(Unit(owner));
            var view = shell.AddComponent<TileTextView>();
            var canvasObject = new GameObject("TileCanvas", typeof(RectTransform),typeof(Canvas)); canvasObject.transform.SetParent(shell.transform,false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
            var label = new GameObject("TileText",typeof(RectTransform),typeof(TextMeshProUGUI)); label.transform.SetParent(canvasObject.transform,false);
            var text = label.GetComponent<TextMeshProUGUI>(); text.enabled=false; text.rectTransform.anchoredPosition=new Vector2(x,-30);
            var visual = new GameObject("OccupantVisual",typeof(RectTransform),typeof(Image),typeof(Animator)); visual.transform.SetParent(canvasObject.transform,false);
            Set(view,"_tileView",tile); Set(view,"_text",text); Set(view,"_showOccupantStats",false);
            Set(view,"_showHuanShuStatusIcon",false); Set(view,"_useTileImageClickSurface",false);
            Set(view,"_occupantVisualRoot",visual.GetComponent<RectTransform>()); Set(view,"_occupantVisualImage",visual.GetComponent<Image>());
            Set(view,"_occupantVisualAnimator",visual.GetComponent<Animator>()); Set(view,"_occupantVisualSize",new Vector2(340,340));
            Set(view,"_occupantVisualAnchoredPosition",new Vector2(x,0)); BoardAnimationControllerTestUtility.Bind(view,"A-212");
            view.SetTile(owner.ToString(),"A-212",true); shell.SetActive(true); view.PlayIdleAnimation(); return view;
        }
        private static UnitState Unit(PlayerId owner, string id = null) =>
            new UnitState(id ?? "a212-" + owner, "A-212", owner, new TileCoord(0,0), AttackType.Melee,6,100,true,false,0,hitsPerAttack:4);
        private sealed class Completion { internal bool Done; internal float Deadline; }
        private static IEnumerator Complete(IEnumerator sequence, Completion completion) { yield return sequence; completion.Done=true; }
        private static bool IsState(TileTextView view, string state) =>
            ((Animator)typeof(TileTextView).GetField("_occupantVisualAnimator",Hidden).GetValue(view)).GetCurrentAnimatorStateInfo(0).IsName(state);
        private static void AssertState(TileTextView view, string state)
        {
            Assert.That(IsState(view,state),Is.True,state);
            Assert.That(typeof(TileTextView).GetField("_activeSpriteSequence",Hidden).GetValue(view),Is.Null,"The registered card controller owns motion.");
        }
        private static void Set(object instance,string name,object value) => instance.GetType().GetField(name,Hidden).SetValue(instance,value);
    }
}

