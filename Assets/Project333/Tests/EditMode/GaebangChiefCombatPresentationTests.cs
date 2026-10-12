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
    public sealed class GaebangChiefCombatPresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static Type AssetsType => typeof(TileTextView).Assembly.GetType("Project333.Runtime.Presentation.Battle.GaebangChiefPresentationAssets", true);
        private static Type EffectType => typeof(TileTextView).Assembly.GetType("Project333.Runtime.Presentation.Battle.GaebangChiefCoinPunch", true);
        [TearDown] public void Cleanup() => BoardAnimationControllerTestUtility.Cleanup();
        [UnityTearDown] public IEnumerator LeavePlayMode() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void CoinResourcesUseNativeEight512CellsAndOneQuietShortAttackSound()
        {
            var frames = (IReadOnlyList<Sprite>)AssetsType.GetProperty("ImpactFrames", Static).GetValue(null);
            Assert.That(frames, Is.Not.Null); Assert.That(frames.Count, Is.EqualTo(8));
            Assert.That(frames.Select(s => s.rect).Distinct().Count(), Is.EqualTo(8));
            for (var i = 0; i < frames.Count; i++)
            {
                Assert.That(frames[i].name, Is.EqualTo("CoinImpactAtlas_" + i));
                Assert.That(frames[i].rect.size, Is.EqualTo(new Vector2(512, 512)));
                Assert.That(frames[i].pivot, Is.EqualTo(new Vector2(256, 256)));
            }
            var sound = (AudioClip)AssetsType.GetProperty("AttackSound", Static).GetValue(null);
            Assert.That(sound, Is.Not.Null); Assert.That(sound.length, Is.EqualTo(.44f).Within(.002f));
            Assert.That(sound.channels, Is.EqualTo(1)); Assert.That(sound.frequency, Is.EqualTo(44100));
            var samples = new float[sound.samples]; Assert.That(sound.GetData(samples, 0), Is.True);
            Assert.That(samples.Max(s => Mathf.Abs(s)), Is.LessThan(.101f));
        }

        [UnityTest]
        public IEnumerator TwoDirectionsTrackContactAnchorAndRespectCancelReplacementAndMute()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture(); var oldScale = Time.timeScale; var oldVolume = AudioListener.volume;
            try
            {
                fixture.WarmResources(); yield return null; Time.timeScale = 0f; AudioListener.volume = 0f;
                var originalSize = fixture.Left.GetComponentInChildren<Image>().rectTransform.sizeDelta;
                fixture.Strike(fixture.Left, fixture.Right, 1, false);
                fixture.Strike(fixture.Right, fixture.Left, 1, true);
                Assert.That(fixture.Sparks(false).gameObject.activeSelf, Is.False);
                Assert.That(fixture.Effect.GetComponents<AudioSource>().Length, Is.EqualTo(2));
                Assert.That(AudioListener.volume, Is.EqualTo(0f), "Attack sound must preserve global mute.");
                yield return new WaitForSecondsRealtime(.29f);
                foreach (var counter in new[] { false, true })
                {
                    var image = fixture.Sparks(counter); var target = counter ? fixture.Left : fixture.Right;
                    Assert.That(image.gameObject.activeSelf, Is.True);
                    Assert.That(Vector3.Distance(image.rectTransform.position, target.GetVisualImpactWorldPosition()), Is.LessThan(.01f));
                    Assert.That(image.raycastTarget, Is.False);
                    Assert.That(image.GetComponentInParent<CanvasGroup>().blocksRaycasts, Is.False);
                }
                fixture.Right.SetVisualWorldPosition(fixture.Right.GetVisualWorldPosition() + new Vector3(12, -7, 0));
                yield return null;
                Assert.That(Vector3.Distance(fixture.Sparks(false).rectTransform.position, fixture.Right.GetVisualImpactWorldPosition()), Is.LessThan(.01f));
                Assert.That(fixture.Left.GetComponentInChildren<Image>().rectTransform.sizeDelta, Is.EqualTo(originalSize));
                fixture.Bootstrapper.CancelCombatPresentation();
                Assert.That(fixture.Sparks(false).gameObject.activeSelf || fixture.Sparks(true).gameObject.activeSelf, Is.False);
                Assert.That(fixture.Effect.GetComponents<AudioSource>().Any(a => a.isPlaying), Is.False);

                fixture.Strike(fixture.Left, fixture.Right, 1, false);
                fixture.Right.GetComponent<TileView>().Present(Unit(PlayerId.AI, "replacement"));
                fixture.Right.SetTile("right", "GaebangChief", true);
                yield return new WaitForSecondsRealtime(.29f);
                Assert.That(fixture.Sparks(false).gameObject.activeSelf, Is.False, "A replaced target must not receive stale VFX.");

                fixture.Strike(fixture.Left, fixture.Right, 1, false);
                yield return new WaitForSecondsRealtime(.29f);
                fixture.Strike(fixture.Left, fixture.Right, 1, false);
                yield return new WaitForSecondsRealtime(.29f);
                Assert.That(fixture.Sparks(false).gameObject.activeSelf, Is.True, "Old slot cleanup cannot hide a new punch.");
                fixture.Effect.enabled = false;
                Assert.That(fixture.Sparks(false).gameObject.activeSelf, Is.False);
                Assert.That(fixture.Effect.GetComponents<AudioSource>().Any(a => a.isPlaying), Is.False);
                yield return new WaitForSecondsRealtime(.8f);
                Assert.That(fixture.Sparks(false).gameObject.activeSelf, Is.False);
            }
            finally { Time.timeScale = oldScale; AudioListener.volume = oldVolume; fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator MeleePunchBothDirectionsUsesControllerAndSingleAuthoritativePacket()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            foreach (var reverse in new[] { false, true })
            {
                var fixture = new Fixture();
                try
                {
                    fixture.WarmResources(); yield return null;
                    var source = reverse ? fixture.Right : fixture.Left;
                    var target = reverse ? fixture.Left : fixture.Right;
                    var origin = source.GetVisualWorldPosition();
                    var request = fixture.Request(true, true, reverse);
                    var completion = fixture.Play(request);
                    yield return new WaitForSecondsRealtime(.12f);
                    AssertState(source, "Run");
                    Assert.That(Vector3.Distance(source.GetVisualWorldPosition(), origin), Is.GreaterThan(.1f));
                    yield return new WaitForSecondsRealtime(.24f);
                    var deadline = Time.realtimeSinceStartup + 2f; var observed = false;
                    while (Time.realtimeSinceStartup < deadline && !completion.Done)
                    {
                        var packetCount = fixture.Root.GetComponentsInChildren<TMP_Text>(true)
                            .Count(t => t.name == "ValuePopupText" && t.text == "10");
                        if (fixture.Sparks(false).gameObject.activeSelf && fixture.Sparks(true).gameObject.activeSelf && packetCount == 2)
                        {
                            AssertState(source, "Attack"); AssertState(target, "Attack");
                            fixture.Capture(reverse ? "gaebang-chief-contact-ai.png" : "gaebang-chief-contact-player.png");
                            observed = true; break;
                        }
                        yield return null;
                    }
                    Assert.That(observed, Is.True, "A single attack and counterattack packet should appear at punch contact.");
                    Assert.That(source.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(60));
                    Assert.That(target.GetComponent<TileView>().CurrentOccupant.CurrentHp, Is.EqualTo(60), "VFX cannot apply damage.");
                    while (!completion.Done && Time.realtimeSinceStartup < completion.Deadline) yield return null;
                    Assert.That(completion.Done, Is.True);
                    AssertState(source, "Idle");
                    Assert.That(Vector3.Distance(source.GetVisualWorldPosition(), origin), Is.LessThan(.02f));
                    Assert.That(fixture.Sparks(false).gameObject.activeSelf || fixture.Sparks(true).gameObject.activeSelf, Is.False);
                    fixture.Capture(reverse ? "gaebang-chief-return-ai.png" : "gaebang-chief-return-player.png");
                }
                finally { fixture.Dispose(); }
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DeathUsesAuthoredCollapseAndCancellationStopsPendingPunch()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); var fixture = new Fixture();
            try
            {
                fixture.WarmResources(); yield return null;
                var request = fixture.Request(false, false);
                request.GetType().GetProperty("AttackerDied").SetValue(request, true);
                var completion = fixture.Play(request);
                while (!IsState(fixture.Left, "Death") && !completion.Done && Time.realtimeSinceStartup < completion.Deadline) yield return null;
                AssertState(fixture.Left, "Death");
                yield return new WaitForSecondsRealtime(.91f);
                fixture.Capture("gaebang-chief-death.png");
                while (!completion.Done && Time.realtimeSinceStartup < completion.Deadline) yield return null;
                Assert.That(completion.Done, Is.True);
                AssertState(fixture.Left, "Death");
                fixture.Left.PlayIdleAnimation();
                var pending = fixture.Play(fixture.Request(false, true));
                yield return new WaitForSecondsRealtime(.1f);
                fixture.Bootstrapper.CancelCombatPresentation();
                yield return new WaitForSecondsRealtime(1f);
                Assert.That(pending.Done, Is.True);
                Assert.That(fixture.Root.GetComponentsInChildren<TMP_Text>(true).Any(t => t.name == "ValuePopupText" && !string.IsNullOrEmpty(t.text)), Is.False);
                Assert.That(fixture.Sparks(false).gameObject.activeSelf, Is.False);
                Assert.That(fixture.Effect.GetComponents<AudioSource>().Any(a => a.isPlaying), Is.False);
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }


        [UnityTest]
        public IEnumerator ComponentOnlyDestroyReleasesOwnedAudioAndOverlay()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); var fixture = new Fixture();
            try
            {
                fixture.WarmResources(); yield return null;
                fixture.Strike(fixture.Left, fixture.Right, 1, false);
                fixture.Strike(fixture.Right, fixture.Left, 1, true);
                var host = fixture.Effect.gameObject;
                Assert.That(host.GetComponents<AudioSource>().Length, Is.EqualTo(2));
                Object.Destroy(fixture.Effect);
                yield return null; yield return null;
                Assert.That(host, Is.Not.Null);
                Assert.That(host.GetComponents<AudioSource>().Length, Is.EqualTo(0), "Destroying only the effect must release its owned emitters.");
                Assert.That(fixture.Root.GetComponentsInChildren<Image>(true).Any(i => i.name == "CoinAttackImpact" || i.name == "CoinCounterImpact"), Is.False);
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root = new GameObject("GaebangChiefCombatFixture");
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
                var effects = new GameObject("CoinEffectHost"); effects.transform.SetParent(Root.transform);
                Effect = (MonoBehaviour)effects.AddComponent(EffectType);
                var host = new GameObject("DormantPresentationCoordinator"); host.SetActive(false); host.transform.SetParent(Root.transform);
                Bootstrapper = host.AddComponent<BattleBootstrapper>();
                var screen = host.AddComponent<BattleScreenPresenter>(); var board = host.AddComponent<BoardPresenter>();
                Set(Bootstrapper, "_battleScreenPresenter", screen); Set(screen, "_boardPresenter", board);
                Set(board, "_playerTileViews", new[] { Left.GetComponent<TileView>() });
                Set(board, "_aiTileViews", new[] { Right.GetComponent<TileView>() });
                Set(Bootstrapper, "_gaebangChiefCoinPunch", Effect);
                Canvas.ForceUpdateCanvases();
            }
            internal void WarmResources()
            {
                AssetsType.GetProperty("ImpactFrames", Static).GetValue(null);
                AssetsType.GetProperty("AttackSound", Static).GetValue(null);
            }
            internal void Strike(TileTextView source, TileTextView target, int hits, bool counter) =>
                EffectType.GetMethod("PlayStrike", Hidden).Invoke(Effect, new object[] { source, target, hits, .125f, .25f, counter });
            internal Image Sparks(bool counter) => Root.GetComponentsInChildren<Image>(true).Single(i => i.name == (counter ? "CoinCounterImpact" : "CoinAttackImpact"));
            internal object Request(bool counter, bool popups, bool reverse = false)
            {
                var type = typeof(BattleBootstrapper).GetNestedType("AttackAnimationRequest", BindingFlags.NonPublic);
                var coord = new TileCoord(0, 0);
                var request = Activator.CreateInstance(type, new object[] { reverse ? PlayerId.AI : PlayerId.Player, coord, reverse ? PlayerId.Player : PlayerId.AI, coord, coord, false });
                type.GetProperty("AttackerCardId").SetValue(request, "GaebangChief"); type.GetProperty("DefenderCardId").SetValue(request, "GaebangChief");
                type.GetProperty("DefenderCounterattacked").SetValue(request, counter); type.GetProperty("AttackerTookDamage").SetValue(request, counter);
                if (popups)
                {
                    var events = (List<BattleValuePopupEvent>)type.GetProperty("ValuePopupEvents").GetValue(request);
                    for (var hit = 0; hit < 1; hit++)
                    {
                        events.Add(Cue(reverse ? Left : Right, reverse ? Right : Left, BattleValueChangeCause.NormalAttack, hit));
                        if (counter) events.Add(Cue(reverse ? Right : Left, reverse ? Left : Right, BattleValueChangeCause.Counterattack, hit));
                    }
                }
                return request;
            }
            private static BattleValuePopupEvent Cue(TileTextView target, TileTextView source, BattleValueChangeCause cause, int hit) =>
                new BattleValuePopupEvent(target.VisualRuntimeId, target.GetComponent<TileView>().OwnerId, new TileCoord(0, 0),
                    false, 10, source.GetComponent<TileView>().OwnerId, source.VisualRuntimeId, "GaebangChief", cause, DamageType.Physical, 60-hit*10, 50-hit*10);
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
            var shell = new GameObject("GaebangChief" + owner); shell.SetActive(false); shell.transform.SetParent(fixture);
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
            Set(view,"_occupantVisualAnchoredPosition",new Vector2(x,0)); BoardAnimationControllerTestUtility.Bind(view,"GaebangChief");
            view.SetTile(owner.ToString(),"GaebangChief",true); shell.SetActive(true); view.PlayIdleAnimation(); return view;
        }
        private static UnitState Unit(PlayerId owner, string id = null) =>
            new UnitState(id ?? "a212-" + owner, "GaebangChief", owner, new TileCoord(0,0), AttackType.Melee,10,60,true,false,0,hitsPerAttack:1);
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

