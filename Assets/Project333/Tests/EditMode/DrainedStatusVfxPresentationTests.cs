using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Presentation.Battle;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class DrainedStatusVfxPresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string ImageName = "DrainedElectricArcs";
        private const string ResourcePath = "Project333/StatusEffects/Drained/ElectricArcs";
        private static Type EffectType => typeof(TileTextView).Assembly.GetType(
            "Project333.Runtime.Presentation.Battle.DrainedStatusVfx", true);

        [TearDown] public void Cleanup() => BoardAnimationControllerTestUtility.Cleanup();
        [UnityTearDown] public IEnumerator LeavePlayMode()
        {
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator BothOwnersRenderNativeDrainedArcsWithoutChangingHpResourcesOrAudio()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                fixture.WarmFrames();
                fixture.LeftUnit.IsDrained = true; fixture.RightUnit.IsDrained = true;
                var truth = fixture.Truth();
                yield return WaitVisible(fixture.Left); yield return WaitVisible(fixture.Right);
                AssertNativeState(fixture.Left, "Drained"); AssertNativeState(fixture.Right, "Drained");
                Assert.That(Arc(fixture.Left).sprite.texture, Is.Not.Null);
                Assert.That(Arc(fixture.Right).sprite.texture, Is.Not.Null);
                Assert.That(Arc(fixture.Left).raycastTarget || Arc(fixture.Right).raycastTarget, Is.False);
                yield return new WaitForSecondsRealtime(.12f);
                fixture.Capture("drained-status-both-owners.png");
                yield return new WaitForSecondsRealtime(.22f);
                Assert.That(fixture.Truth(), Is.EqualTo(truth), "A status overlay cannot mutate authoritative HP, attack usage or resources.");
                Assert.That(fixture.Root.GetComponentsInChildren<AudioSource>(true).Length, Is.Zero,
                    "The requested weak electricity has no new sound.");
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SameOccupantMutationRefreshesPerFrameAndReusesOneOverlay()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                yield return null; Assert.That(Visible(Arc(fixture.Left)), Is.False);
                fixture.LeftUnit.IsDrained = true;
                yield return WaitVisible(fixture.Left);
                var image = Arc(fixture.Left); var effect = Effect(fixture.Left);
                for (var i = 0; i < 3; i++)
                {
                    fixture.LeftUnit.IsDrained = false;
                    yield return null; yield return null;
                    Assert.That(image.enabled, Is.False, "A same-object state mutation must be observed without Present or SetTile.");
                    AssertNativeState(fixture.Left, "Idle");
                    fixture.LeftUnit.IsDrained = true;
                    yield return WaitVisible(fixture.Left);
                    Assert.That(Arc(fixture.Left), Is.SameAs(image));
                    Assert.That(Effect(fixture.Left), Is.SameAs(effect));
                }
                Assert.That(Images(fixture.Left).Length, Is.EqualTo(1));
                Assert.That(Effects(fixture.Left).Length, Is.EqualTo(1));
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RestoredWireStateStartsAlreadyDrainedForEitherLocalPerspective()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var original = CreateState();
            original.PlayerBoard.GetOccupant(new TileCoord(0, 0)).IsDrained = true;
            original.AIBoard.GetOccupant(new TileCoord(0, 0)).IsDrained = true;
            foreach (var viewer in new[] { PlayerId.Player, PlayerId.AI })
            {
                var wire = new BattleStateViewFactory().CreateForPlayer(original, "drained-vfx", viewer);
                var restored = BattleStateViewProjector.CreateLocalPerspectiveState(wire);
                var fixture = new Fixture(restored);
                try
                {
                    Assert.That(fixture.LeftUnit.IsDrained && fixture.RightUnit.IsDrained, Is.True);
                    Assert.That(fixture.LeftUnit.OwnerId, Is.EqualTo(PlayerId.Player));
                    Assert.That(fixture.RightUnit.OwnerId, Is.EqualTo(PlayerId.AI));
                    var truth = fixture.Truth();
                    yield return WaitVisible(fixture.Left); yield return WaitVisible(fixture.Right);
                    AssertNativeState(fixture.Left, "Drained"); AssertNativeState(fixture.Right, "Drained");
                    Assert.That(fixture.Truth(), Is.EqualTo(truth));
                    yield return new WaitForSecondsRealtime(.12f);
                    fixture.Capture(viewer == PlayerId.Player ? "drained-status-restored-player.png" : "drained-status-restored-ai.png");
                }
                finally { fixture.Dispose(); }
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SameAndDifferentRuntimeIdRebindsUseFreshOccupantsWithoutOldArcs()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                fixture.LeftUnit.IsDrained = true; yield return WaitVisible(fixture.Left);
                yield return new WaitForSecondsRealtime(.12f);
                var pooled = Arc(fixture.Left); var effect = Effect(fixture.Left);
                var startedAt = (float)EffectType.GetField("_startedAt", Hidden).GetValue(effect);
                var snapshot = Unit(PlayerId.Player, fixture.LeftUnit.CardId, fixture.LeftUnit.RuntimeId);
                snapshot.IsDrained = true; fixture.Present(fixture.Left, snapshot);
                yield return null; yield return null;
                Assert.That(fixture.Left.GetComponent<TileView>().CurrentOccupant, Is.SameAs(snapshot));
                Assert.That((float)EffectType.GetField("_startedAt", Hidden).GetValue(effect), Is.EqualTo(startedAt),
                    "A fresh snapshot of the same drained RuntimeId must retain its burst phase rather than restart at frame zero.");
                Assert.That(Arc(fixture.Left), Is.SameAs(pooled));
                Assert.That(Visible(pooled), Is.True, "The already-visible burst continues across a same-id snapshot.");
                var sameId = Unit(PlayerId.Player, fixture.LeftUnit.CardId, fixture.LeftUnit.RuntimeId);
                fixture.Present(fixture.Left, sameId);
                yield return null; yield return null;
                Assert.That(pooled.enabled, Is.False, "The old drained object cannot leak through a fresh object with the same RuntimeId.");
                sameId.IsDrained = true; yield return WaitVisible(fixture.Left);
                Assert.That(Arc(fixture.Left), Is.SameAs(pooled));
                var sameIdStartedAt = (float)EffectType.GetField("_startedAt", Hidden).GetValue(effect);
                yield return new WaitForSecondsRealtime(.12f);
                var different = Unit(PlayerId.Player, "GaebangChief", "replacement-drained");
                different.IsDrained = true; fixture.Present(fixture.Left, different, true);
                yield return WaitVisible(fixture.Left);
                Assert.That(fixture.Left.GetComponent<TileView>().CurrentOccupant, Is.SameAs(different));
                Assert.That(fixture.Left.VisualRuntimeId, Is.EqualTo("replacement-drained"));
                Assert.That((float)EffectType.GetField("_startedAt", Hidden).GetValue(effect), Is.GreaterThan(sameIdStartedAt),
                    "A genuinely different occupant starts its own burst.");
                Assert.That(Arc(fixture.Left), Is.SameAs(pooled));
                fixture.Present(fixture.Left, Unit(PlayerId.Player, "GaebangChief", "replacement-idle"));
                yield return null; yield return null;
                Assert.That(pooled.enabled, Is.False); Assert.That(Images(fixture.Left).Length, Is.EqualTo(1));
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ErasureClearedTilesInvisibleVisualsAndDefeatLeaveNoResidualElectricity()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                fixture.LeftUnit.IsDrained = true; yield return WaitVisible(fixture.Left);
                var image = Arc(fixture.Left);
                fixture.LeftUnit.ApplyErasure();
                yield return null; yield return null;
                Assert.That(fixture.LeftUnit.IsDrained, Is.False); Assert.That(image.enabled, Is.False);
                var replacement = Unit(PlayerId.Player, "ManaCannon", "visual-toggle"); replacement.IsDrained = true;
                fixture.Present(fixture.Left, replacement); yield return WaitVisible(fixture.Left);
                Set(fixture.Left, "_showOccupantVisual", false);
                yield return null; yield return null; Assert.That(image.enabled, Is.False);
                Set(fixture.Left, "_showOccupantVisual", true);
                yield return WaitVisible(fixture.Left);
                replacement.CurrentHp = 0;
                yield return null; yield return null; Assert.That(image.enabled, Is.False);
                replacement.CurrentHp = 100;
                yield return WaitVisible(fixture.Left);
                fixture.Present(fixture.Left, null);
                yield return null; yield return null; Assert.That(image.enabled, Is.False);
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SealboundAndDeathPresentationSuppressArcsAndRecoveryRestoresThem()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                fixture.LeftUnit.IsDrained = true; yield return WaitVisible(fixture.Left);
                var image = Arc(fixture.Left); var hp = fixture.LeftUnit.CurrentHp;
                fixture.LeftUnit.EnterSealbound(1);
                yield return null; yield return null; Assert.That(image.enabled, Is.False);
                fixture.LeftUnit.ResolveSealboundOwnerTurnStart();
                fixture.LeftUnit.IsDrained = true;
                yield return WaitVisible(fixture.Left);
                fixture.Left.PlayDeathAnimation();
                yield return null; yield return null;
                AssertNativeState(fixture.Left, "Death"); Assert.That(image.enabled, Is.False);
                yield return new WaitForSecondsRealtime(.12f); Assert.That(image.enabled, Is.False);
                fixture.Capture("drained-status-death-no-arcs.png");
                Assert.That(fixture.LeftUnit.CurrentHp, Is.EqualTo(hp), "A death presentation must not apply authoritative damage.");
                fixture.Left.ResetCombatPresentation();
                yield return WaitVisible(fixture.Left); AssertNativeState(fixture.Left, "Drained");
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DefensiveHiddenEnemyFilterDoesNotHideOwnStaleVisibleStatus()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                var own = Unit(PlayerId.Player, "ManaCannon", "own-hidden", true);
                var enemy = Unit(PlayerId.AI, "GaebangChief", "enemy-hidden", true);
                fixture.Present(fixture.Left, own); fixture.Present(fixture.Right, enemy);
                yield return null; yield return null;
                Assert.That(own.IsHiding && enemy.IsHiding, Is.True);
                Assert.That(Visible(Arc(fixture.Left)) || Visible(Arc(fixture.Right)), Is.False);
                // A legal Drained transition reveals Hiding. This deliberately models only a
                // transient stale view overlap to prove the presentation's independent no-leak guard.
                typeof(OccupantState).GetField("_isDrained", Hidden).SetValue(own, true);
                typeof(OccupantState).GetField("_isDrained", Hidden).SetValue(enemy, true);
                yield return WaitVisible(fixture.Left);
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(Visible(Arc(fixture.Right)), Is.False, "Enemy hidden information cannot be exposed by an overlay.");
                Assert.That(own.IsHiding && enemy.IsHiding, Is.True, "Presentation must not normalize authoritative state.");
                enemy.RevealHiding();
                yield return WaitVisible(fixture.Right);
                yield return new WaitForSecondsRealtime(.12f);
                fixture.Capture("drained-status-hidden-visibility.png");
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ComponentAndGameObjectDisableCancelWithoutDuplicatingThePool()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                fixture.LeftUnit.IsDrained = true; yield return WaitVisible(fixture.Left);
                var image = Arc(fixture.Left); var effect = Effect(fixture.Left);
                fixture.Left.enabled = false;
                yield return null; yield return null; Assert.That(image.enabled, Is.False);
                fixture.Left.enabled = true; yield return WaitVisible(fixture.Left);
                effect.enabled = false;
                yield return null; yield return null;
                Assert.That(image.enabled, Is.False, "Tile refresh must not restart an explicitly disabled effect.");
                effect.enabled = true; yield return WaitVisible(fixture.Left);
                fixture.Left.gameObject.SetActive(false);
                yield return null; Assert.That(image.enabled, Is.False);
                fixture.Left.gameObject.SetActive(true); yield return WaitVisible(fixture.Left);
                Assert.That(Arc(fixture.Left), Is.SameAs(image)); Assert.That(Effect(fixture.Left), Is.SameAs(effect));
                Assert.That(Images(fixture.Left).Length, Is.EqualTo(1)); Assert.That(Effects(fixture.Left).Length, Is.EqualTo(1));
                Object.Destroy(fixture.Left);
                yield return null; yield return null;
                Assert.That(image == null, Is.True); Assert.That(effect == null, Is.True);
                Assert.That(fixture.Root.GetComponentsInChildren<Image>(true).Any(i => i.name == ImageName), Is.False);
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PausedClockStillAdvancesFramesFadeRestAndLiveGeometryWithoutBlockingInput()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture(); var oldScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f; fixture.LeftUnit.IsDrained = true;
                yield return WaitVisible(fixture.Left);
                var image = Arc(fixture.Left); var effect = Effect(fixture.Left);
                var raycasters = fixture.Root.GetComponentsInChildren<GraphicRaycaster>(true).Length;
                var frames = new HashSet<Sprite>(); var alphas = new List<float>(); var sawRest = false; var sawResume = false;
                var deadline = Time.realtimeSinceStartup + 2.5f;
                while (Time.realtimeSinceStartup < deadline && !sawResume)
                {
                    if (image.enabled)
                    {
                        frames.Add(image.sprite); alphas.Add(image.color.a);
                        Assert.That(image.color.a, Is.InRange(0f, .5501f));
                        if (sawRest && image.color.a > .01f) sawResume = true;
                    }
                    else sawRest = true;
                    yield return null;
                }
                Assert.That(frames.Count, Is.GreaterThan(1), "Native arc frames advance while Time.timeScale is zero.");
                Assert.That(alphas.Max() - alphas.Min(), Is.GreaterThan(.15f), "The burst fades using the unscaled clock.");
                Assert.That(sawRest && sawResume, Is.True, "There is a quiet rest and another weak burst while paused.");
                Assert.That(Arc(fixture.Left), Is.SameAs(image)); Assert.That(Effect(fixture.Left), Is.SameAs(effect));
                var body = Visual(fixture.Left); var oldSize = image.rectTransform.rect.size;
                var offset = image.rectTransform.position - body.rectTransform.position;
                var move = new Vector3(24, -13, 0);
                fixture.Left.SetVisualWorldPosition(fixture.Left.GetVisualWorldPosition() + move);
                yield return null; yield return null;
                Assert.That(Vector3.Distance(image.rectTransform.position - body.rectTransform.position, offset), Is.LessThan(.03f));
                body.rectTransform.sizeDelta *= .65f;
                yield return null; yield return null;
                Assert.That(image.rectTransform.rect.width, Is.LessThan(oldSize.x * .8f));
                Assert.That(image.rectTransform.rect.height, Is.LessThan(oldSize.y * .8f));
                AssertBodyRegion(fixture.Left);
                Assert.That(image.raycastTarget, Is.False);
                Assert.That(fixture.Root.GetComponentsInChildren<GraphicRaycaster>(true).Length, Is.EqualTo(raycasters));
                Assert.That(image.GetComponents<Collider>().Length + image.GetComponents<Collider2D>().Length, Is.Zero);
                Assert.That(Images(fixture.Left).Length, Is.EqualTo(1));
            }
            finally { Time.timeScale = oldScale; fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator NativeDifferentShapesKeepWeakArcsAroundBodyAboveFeetAndBelowFace()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                foreach (var card in new[] { "ManaCannon", "Gwangma", "A-301" })
                {
                    var occupant = Unit(PlayerId.Player, card, "shape-" + card); occupant.IsDrained = true;
                    fixture.Present(fixture.Left, occupant, true);
                    yield return WaitVisible(fixture.Left);
                    Assert.That(Visual(fixture.Left).sprite, Is.Not.Null);
                    var animator = Visual(fixture.Left).GetComponent<Animator>();
                    Assert.That(animator.runtimeAnimatorController, Is.SameAs(BoardAnimationControllerTestUtility.Controller(card)),
                        "Use the actual native controller; never substitute a generated fallback.");
                    yield return new WaitForSecondsRealtime(.12f);
                    AssertBodyRegion(fixture.Left);
                    Assert.That(Arc(fixture.Left).color.a, Is.LessThanOrEqualTo(.5501f));
                    fixture.Capture("drained-status-body-" + card.ToLowerInvariant() + ".png");
                }
                Assert.That(Images(fixture.Left).Length, Is.EqualTo(1));
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root = new GameObject("DrainedStatusVfxFixture");
            internal readonly BattleState Authority;
            internal readonly TileTextView Left, Right;
            internal UnitState LeftUnit => (UnitState)Left.GetComponent<TileView>().CurrentOccupant;
            internal UnitState RightUnit => (UnitState)Right.GetComponent<TileView>().CurrentOccupant;
            private readonly Camera _camera; private readonly RenderTexture _render;

            internal Fixture(BattleState state = null)
            {
                Authority = state ?? CreateState();
                var cameraObject = new GameObject("DrainedStatusCamera", typeof(Camera), typeof(AudioListener));
                cameraObject.transform.SetParent(Root.transform);
                _camera = cameraObject.GetComponent<Camera>(); _camera.transform.position = new Vector3(0, 0, -10);
                _camera.orthographic = true; _camera.orthographicSize = 5; _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(.035f, .045f, .06f);
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    _render = new RenderTexture(1000, 560, 24); _render.Create(); _camera.targetTexture = _render;
                }
                Left = CreateTile(Root.transform, _camera, Authority.PlayerBoard.GetOccupant(new TileCoord(0, 0)), -220);
                Right = CreateTile(Root.transform, _camera, Authority.AIBoard.GetOccupant(new TileCoord(0, 0)), 220);
                Canvas.ForceUpdateCanvases();
            }
            internal void WarmFrames() => Assert.That(Resources.LoadAll<Sprite>(ResourcePath).Length, Is.EqualTo(8));
            internal void Present(TileTextView view, OccupantState occupant, bool rebindCard = false)
            {
                view.GetComponent<TileView>().Present(occupant);
                if (rebindCard && occupant != null) BoardAnimationControllerTestUtility.Bind(view, occupant.CardId);
                // No SetTile/Refresh here: the real LateUpdate must observe the live occupant.
            }
            internal string Truth() => string.Join("|", new[] {
                LeftUnit?.CurrentHp.ToString(), LeftUnit?.MaxHp.ToString(), LeftUnit?.RemainingAttacksThisTurn.ToString(),
                RightUnit?.CurrentHp.ToString(), RightUnit?.MaxHp.ToString(), RightUnit?.RemainingAttacksThisTurn.ToString(),
                ResourceTruth(Authority.Player.Resources), ResourceTruth(Authority.AI.Resources) });
            internal void Capture(string filename)
            {
                if (_render == null) return;
                Canvas.ForceUpdateCanvases(); _camera.Render();
                var old = RenderTexture.active; var texture = new Texture2D(_render.width, _render.height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = _render; texture.ReadPixels(new Rect(0, 0, _render.width, _render.height), 0, 0); texture.Apply();
                    var directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Captures/DrainedStatusVfx_20261001"));
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

        private static TileTextView CreateTile(Transform fixture, Camera camera, OccupantState occupant, float x)
        {
            Assert.That(occupant, Is.Not.Null);
            var shell = new GameObject("DrainedTile-" + occupant.OwnerId); shell.SetActive(false); shell.transform.SetParent(fixture);
            var tile = shell.AddComponent<TileView>(); tile.Configure(occupant.OwnerId, 0, 0); tile.Present(occupant);
            var view = shell.AddComponent<TileTextView>();
            var canvasObject = new GameObject("TileCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(shell.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            var label = new GameObject("TileText", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(canvasObject.transform, false);
            var text = label.GetComponent<TextMeshProUGUI>(); text.enabled = false;
            text.rectTransform.anchoredPosition = new Vector2(x, -30); text.rectTransform.sizeDelta = new Vector2(96, 96);
            var visual = new GameObject("OccupantVisual", typeof(RectTransform), typeof(Image), typeof(Animator));
            visual.transform.SetParent(canvasObject.transform, false);
            Set(view, "_tileView", tile); Set(view, "_text", text); Set(view, "_showOccupantStats", false);
            Set(view, "_showHuanShuStatusIcon", false); Set(view, "_useTileImageClickSurface", false);
            Set(view, "_occupantVisualRoot", visual.GetComponent<RectTransform>());
            Set(view, "_occupantVisualImage", visual.GetComponent<Image>()); Set(view, "_occupantVisualAnimator", visual.GetComponent<Animator>());
            Set(view, "_occupantVisualSize", new Vector2(340, 340)); Set(view, "_occupantVisualAnchoredPosition", new Vector2(x, 0));
            BoardAnimationControllerTestUtility.Bind(view, occupant.CardId);
            shell.SetActive(true); view.SetTile(occupant.OwnerId.ToString(), occupant.CardId, true);
            return view;
        }

        private static BattleState CreateState()
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.Player));
            state.Player.Resources.Add(new ResourceSet(2, 3, 4, 5)); state.AI.Resources.Add(new ResourceSet(3, 2, 5, 4));
            state.PlayerBoard.Place(new TileCoord(0, 0), Unit(PlayerId.Player, "ManaCannon", "drained-player"));
            state.AIBoard.Place(new TileCoord(0, 0), Unit(PlayerId.AI, "GaebangChief", "drained-ai"));
            return state;
        }
        private static UnitState Unit(PlayerId owner, string card, string runtimeId, bool hiding = false) =>
            new UnitState(runtimeId, card, owner, new TileCoord(0, 0), card == "ManaCannon" ? AttackType.Ranged : AttackType.Melee,
                card == "ManaCannon" ? 50 : 10, 100, true, false, 0, damageType: card == "ManaCannon" ? DamageType.Magic : DamageType.Physical,
                hasHiding: hiding);
        private static string ResourceTruth(ResourceSet r) => r.Mana + "," + r.Qi + "," + r.Power + "," + r.Gold;
        private static Image Visual(TileTextView view) => view.GetComponentsInChildren<Image>(true).Single(i => i.name == "OccupantVisual");
        private static Image[] Images(TileTextView view) => view.GetComponentsInChildren<Image>(true).Where(i => i.name == ImageName).ToArray();
        private static Image Arc(TileTextView view) => Images(view).SingleOrDefault();
        private static MonoBehaviour[] Effects(TileTextView view) => view.GetComponents<MonoBehaviour>().Where(c => c != null && c.GetType() == EffectType).ToArray();
        private static MonoBehaviour Effect(TileTextView view) => Effects(view).Single();
        private static bool Visible(Image image) => image != null && image.enabled && image.gameObject.activeInHierarchy && image.sprite != null && image.color.a > .01f;
        private static IEnumerator WaitVisible(TileTextView view)
        {
            // A previous occupant can still have a visible pooled Image at this call.
            // Let the real LateUpdate bind the current object/controller before observing it.
            yield return null; yield return null;
            var deadline = Time.realtimeSinceStartup + 3f;
            while (!Visible(Arc(view)) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Visible(Arc(view)), Is.True, "Expected visible native weak electricity for " + view.name);
        }
        private static void AssertNativeState(TileTextView view, string state)
        {
            var animator = Visual(view).GetComponent<Animator>();
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(state), Is.True, state);
        }
        private static void AssertBodyRegion(TileTextView view)
        {
            var body = Visual(view); var arc = Arc(view); var frame = body.GetPixelAdjustedRect(); var corners = new Vector3[4];
            arc.rectTransform.GetWorldCorners(corners);
            var minimum = body.rectTransform.InverseTransformPoint(corners[0]);
            var maximum = body.rectTransform.InverseTransformPoint(corners[2]);
            Assert.That((maximum.x - minimum.x) / frame.width, Is.InRange(.35f, .85f));
            Assert.That((maximum.y - minimum.y) / frame.height, Is.InRange(.25f, .65f));
            Assert.That(Mathf.Abs((minimum.x + maximum.x) * .5f - frame.center.x), Is.LessThan(frame.width * .02f));
            Assert.That(minimum.y, Is.GreaterThan(frame.yMin + frame.height * .04f), "Arcs stay above the feet/bottom frame margin.");
            Assert.That(maximum.y, Is.LessThan(frame.yMin + frame.height * .7f), "Arcs stay below the face region.");
            Assert.That(arc.rectTransform.IsChildOf(body.transform), Is.True, "VFX follows the actual moving occupant image.");
        }
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);
    }
}

