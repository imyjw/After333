using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class NuclearExplosionPresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        [UnityTearDown] public IEnumerator LeavePlayMode() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void AssetsHaveSixteenOrderedFullCellsAndRestrainedExplosionSound()
        {
            var frames = Resources.LoadAll<Sprite>(NuclearExplosionVfx.FramesResourcePath).OrderBy(s => s.name).ToArray();
            Assert.That(frames.Length, Is.EqualTo(16));
            for (var i = 0; i < frames.Length; i++)
            {
                Assert.That(frames[i].name, Is.EqualTo("MushroomExplosion_" + i.ToString("00")));
                Assert.That(frames[i].rect, Is.EqualTo(new Rect(i % 4 * 512, (3 - i / 4) * 512, 512, 512)));
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frames[0]));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            var sound = Resources.Load<AudioClip>(NuclearExplosionVfx.SoundResourcePath);
            Assert.That(sound, Is.Not.Null);
            Assert.That(sound.channels, Is.EqualTo(1));
            Assert.That(sound.length, Is.InRange(1.2f, 1.8f));
            var samples = new float[sound.samples];
            Assert.That(sound.GetData(samples, 0), Is.True);
            Assert.That(samples.Max(v => Mathf.Abs(v)), Is.LessThan(0.3f));
            Assert.That(NuclearExplosionVfx.DurationSeconds, Is.InRange(1.6f, 1.9f));
        }

        [UnityTest]
        public IEnumerator AuthoredBattleTileCanvasesShowOppositeFieldMushroomAndPreserveOverlayOrdering()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Battle_VSlice.unity", OpenSceneMode.Single);
            var bootstrapper = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleBootstrapper>(true)).Single();
            bootstrapper.enabled = false;
            Set(bootstrapper, "_startOnAwake", false);
            Set(bootstrapper, "_startWithDraftBeforeBattle", false);
            Set(bootstrapper, "_autoRunAiTurns", false);
            foreach (var connection in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Project333.Runtime.Presentation.Online.OnlineBattleConnectionTester>(true)).ToArray())
                Object.DestroyImmediate(connection);
            // Keep the authored hierarchy while preventing unrelated authentication startup across Play Mode reload.
            Object.DestroyImmediate(bootstrapper);
            var previousToken = AccountSessionState.SessionToken;
            typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null, "nuclear-actual-scene-fixture-no-requests");
            GameObject host = null;
            try
            {
                yield return new EnterPlayMode();
                var board = Object.FindFirstObjectByType<BoardPresenter>();
                Assert.That(board, Is.Not.Null);
                board.ApplyResponsiveLayoutNow();
                Canvas.ForceUpdateCanvases();
                yield return null;
                host = new GameObject("ActualSceneNuclearFixture");
                var effect = host.AddComponent<NuclearExplosionVfx>();
                foreach (var caster in new[] { PlayerId.Player, PlayerId.AI, PlayerId.Player })
                {
                    var targetTiles = caster == PlayerId.Player ? board.AITileViews : board.PlayerTileViews;
                    var anchors = targetTiles.Select(t => t.GetComponent<TileTextView>().ActiveEffectButtonAnchor).ToArray();
                    var fieldCanvas = anchors[0].GetComponentInParent<Canvas>(true).rootCanvas;
                    Assert.That(anchors.Select(a => a.GetComponentInParent<Canvas>(true).rootCanvas).Distinct().Count(), Is.GreaterThan(1),
                        "Retain the authored independent tile canvases, including their unit sorting canvases.");
                    var duration = (float)typeof(NuclearExplosionVfx).GetMethod("Play", Hidden).Invoke(effect, new object[] { board, caster });
                    var root = (RectTransform)typeof(NuclearExplosionVfx).GetField("_root", Hidden).GetValue(effect);
                    Assert.That(duration, Is.EqualTo(NuclearExplosionVfx.DurationSeconds),
                        "A valid authored target field must start the explosion. caster=" + caster + " root=" + (root == null ? "null" : root.rect.ToString()));
                    Assert.That(root.parent, Is.SameAs(fieldCanvas.transform), "A field effect uses the tile background viewport, not the inactive unit-sized sorting Canvas.");
                    Assert.That(root.rect.width, Is.EqualTo(((RectTransform)fieldCanvas.transform).rect.width).Within(.01f));
                    Assert.That(root.rect.height, Is.EqualTo(((RectTransform)fieldCanvas.transform).rect.height).Within(.01f));
                    Assert.That(root.gameObject.activeInHierarchy, Is.True);
                    Assert.That(root.GetComponent<Canvas>().overrideSorting, Is.True, "Parenting and activating a pooled Canvas must not reset its sorting override.");
                    Assert.That(root.GetComponent<Canvas>().sortingOrder, Is.EqualTo(4906));
                    var image = (Image)typeof(NuclearExplosionVfx).GetField("_image", Hidden).GetValue(effect);
                    Assert.That(image.gameObject.activeInHierarchy && image.sprite != null, Is.True);
                    Assert.That(image.sprite.name, Is.EqualTo("MushroomExplosion_00"));
                    Assert.That(image.raycastTarget, Is.False);
                    Assert.That(root.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                    var worldCenter = anchors.Select(a => a.position).Aggregate(Vector3.zero, (sum, p) => sum + p) / anchors.Length;
                    Assert.That(Vector3.Distance(image.rectTransform.position, worldCenter), Is.LessThan(.1f), "The ignition origin stays at the enemy field center for either caster.");
                    Assert.That(image.rectTransform.rect.width, Is.GreaterThan(anchors[0].rect.width * 1.75f), "The mushroom must cover the field instead of being fitted to one occupant.");
                    var corners = new Vector3[4];
                    image.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = root.InverseTransformPoint(corner);
                        Assert.That(point.x, Is.InRange(root.rect.xMin - .1f, root.rect.xMax + .1f));
                        Assert.That(point.y, Is.InRange(root.rect.yMin - .1f, root.rect.yMax + .1f));
                    }
                    Set(effect, "_startedAt", Time.unscaledTime - .55f);
                    typeof(NuclearExplosionVfx).GetMethod("LateUpdate", Hidden).Invoke(effect, null);
                    Assert.That(image.sprite.name, Is.EqualTo("MushroomExplosion_04"));
                    typeof(NuclearExplosionVfx).GetMethod("Cancel", Hidden).Invoke(effect, null);
                    Assert.That(root.gameObject.activeInHierarchy, Is.False);
                    Assert.That(effect.GetComponents<AudioSource>().Any(a => a.isPlaying), Is.False);
                }
                Object.Destroy(host);
                host = null;
                yield return null;
                Assert.That(Object.FindObjectsByType<Image>(FindObjectsSortMode.None).Any(i => i.name == "NuclearMushroomExplosion"), Is.False);
                yield return new ExitPlayMode();
            }
            finally
            {
                typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null, previousToken);
                if (host != null) Object.DestroyImmediate(host);
            }
        }
        [UnityTest]
        public IEnumerator ExplosionOriginTracksOppositeBoardAndFitsResponsiveRotatedCanvas()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                foreach (var size in new[] { new Vector2(2340, 1080), new Vector2(620, 1100), new Vector2(1800, 600) })
                {
                    fixture.Layout(size);
                    fixture.CanvasRect.localRotation = Quaternion.Euler(0, 0, 13);
                    Canvas.ForceUpdateCanvases();
                    foreach (var caster in new[] { PlayerId.Player, PlayerId.AI })
                    {
                        Assert.That(fixture.Play(caster), Is.EqualTo(NuclearExplosionVfx.DurationSeconds));
                        fixture.AssertGeometry(caster);
                    }
                }
                fixture.Layout(new Vector2(2340, 1080));
                fixture.CanvasRect.localRotation = Quaternion.identity;
                fixture.Play(PlayerId.Player);
                yield return new WaitForSecondsRealtime(0.85f);
                fixture.AssertGeometry(PlayerId.Player);
                fixture.Capture();
                Assert.That(fixture.Image.raycastTarget, Is.False);
                Assert.That(fixture.Image.GetComponentInParent<CanvasGroup>().blocksRaycasts, Is.False);
                Assert.That(fixture.Image.rectTransform.rect.width, Is.GreaterThan(1.75f * fixture.TileWidth * (96f * 1.35f / 80f)),
                    "On the actual two-row/five-column layout the fitted cloud is about 1.82 times the registered scene unit width.");
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator StartAfterPreviousFrameHitchKeepsFirstFrameAndSoundAlive()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                fixture.Play(PlayerId.Player);
                fixture.Bootstrapper.CancelCombatPresentation();
                yield return null;
                System.Threading.Thread.Sleep(250);
                yield return null;
                fixture.Play(PlayerId.Player);
                typeof(NuclearExplosionVfx).GetMethod("LateUpdate", Hidden).Invoke(fixture.Effect, null);
                Assert.That(fixture.Image.sprite.name, Is.EqualTo("MushroomExplosion_00"), "A hitch before casting is not animation age.");
                Assert.That(fixture.Effect.GetComponents<AudioSource>().Single().isPlaying, Is.True);
                yield return new WaitForSecondsRealtime(0.2f);
                Assert.That(fixture.Image.sprite.name, Is.Not.EqualTo("MushroomExplosion_00"));
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator UnscaledFramesFadeOnceAndCancellationDisableDestroyStopSoundAndOverlay()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var oldTimeScale = Time.timeScale;
            try
            {
                using (var fixture = new Fixture())
                {
                    Time.timeScale = 0;
                    fixture.Play(PlayerId.Player);
                    var first = fixture.Image.sprite;
                    yield return new WaitForSecondsRealtime(0.25f);
                    Assert.That(fixture.Image.sprite, Is.Not.SameAs(first));
                    Assert.That(fixture.Effect.GetComponents<AudioSource>().Single().isPlaying, Is.True);
                    fixture.Bootstrapper.CancelCombatPresentation();
                    fixture.AssertHiddenAndSilent();
                    fixture.Play(PlayerId.AI);
                    fixture.Effect.enabled = false;
                    fixture.AssertHiddenAndSilent();
                    fixture.Effect.enabled = true;
                    fixture.Play(PlayerId.Player);
                    yield return new WaitForSecondsRealtime(NuclearExplosionVfx.DurationSeconds - 0.16f);
                    Assert.That(fixture.Image.color.a, Is.InRange(0.01f, 0.6f));
                    yield return new WaitForSecondsRealtime(0.25f);
                    fixture.AssertHiddenAndSilent();
                    fixture.Play(PlayerId.AI);
                    Assert.That(fixture.Effect.GetComponents<AudioSource>().Length, Is.EqualTo(1));
                    Object.Destroy(fixture.Effect);
                    yield return null;
                    Assert.That(fixture.Root.GetComponentsInChildren<Image>(true).Any(i => i.name == "NuclearMushroomExplosion"), Is.False);
                }
            }
            finally { Time.timeScale = oldTimeScale; }
            yield return new ExitPlayMode();
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void LocalAndOnlineRequestsUseCasterSeatAndEqualDuration_StateOnlyDoesNotReplay(PlayerId caster)
        {
            using (var fixture = new Fixture(false))
            {
                var state = fixture.State;
                state.RestoreRuntimeState(state.TurnNumber, caster, PhaseType.Main);
                var before = Capture(state);
                state.GetPlayer(caster).Hand.Add("Nuclear");
                state.GetPlayer(caster).Resources.Add(new ResourceSet(0, 0, NuclearRules.PowerCost, 0));
                fixture.Gateway.ExecuteCommand(caster, new CastScriptedSpellCommand("Nuclear"));
                var local = typeof(BattleBootstrapper).GetMethod("BuildImpactAnimationRequest", Static).Invoke(null,
                    new object[] { before, Capture(state), state.ValuePopupEvents.ToArray(), "Nuclear", caster });
                var cast = CastEvent(caster);
                var online = fixture.OnlineRequest(cast, new[] { cast });
                foreach (var request in new[] { local, online })
                {
                    Assert.That(Property(request, "SpellEffectId"), Is.EqualTo(NuclearRules.EffectId));
                    Assert.That(Property(request, "SpellCasterOwnerId"), Is.EqualTo(caster));
                    Assert.That(fixture.Estimate(request), Is.GreaterThanOrEqualTo(NuclearExplosionVfx.DurationSeconds));
                }
                var restored = fixture.OnlineRequest(null, Array.Empty<BattleEventDto>());
                Assert.That(Property(restored, "SpellEffectId"), Is.Null);
                Assert.That(fixture.Estimate(restored), Is.Zero);
                Assert.That(state.GetOpponent(caster).Master.CurrentHp, Is.EqualTo(233));
                Assert.That(state.GetPlayer(caster).Master.CurrentHp, Is.EqualTo(333));
            }
        }

        [Test]
        public void InvalidLayoutCannotExposeBlankOverlayOrStartSound()
        {
            using (var fixture = new Fixture(false))
            {
                fixture.Layout(Vector2.zero);
                Assert.That(fixture.Play(PlayerId.Player), Is.Zero);
                fixture.AssertHiddenAndSilent();
                fixture.Layout(new Vector2(2340, 1080));
                Assert.That(fixture.Play(PlayerId.Player), Is.EqualTo(NuclearExplosionVfx.DurationSeconds));
                fixture.AssertGeometry(PlayerId.Player);
                fixture.Bootstrapper.CancelCombatPresentation();
                fixture.AssertHiddenAndSilent();
            }
        }

        [UnityTest]
        public IEnumerator SuccessfulLocalCastStartsExplosionOnce_FailedAndBusyCastsDoNotReplay()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                fixture.State.Player.Hand.Add("Nuclear");
                fixture.State.Player.Resources.Add(new ResourceSet(0, 0, NuclearRules.PowerCost, 0));
                Assert.That(fixture.Bootstrapper.TryCastPlayerPersistentSpell("Nuclear", out _), Is.True);
                Assert.That(fixture.Image.gameObject.activeInHierarchy, Is.True);
                fixture.AssertGeometry(PlayerId.Player);
                Assert.That(fixture.State.AI.Master.CurrentHp, Is.EqualTo(233), "Damage already resolved through the real gateway.");
                Assert.That(fixture.State.AIBoard.FalloutStacks, Is.EqualTo(1));
                Assert.That(fixture.Bootstrapper.TryCastPlayerPersistentSpell("Nuclear", out _), Is.False);
                Assert.That(fixture.State.AIBoard.FalloutStacks, Is.EqualTo(1));
                fixture.Bootstrapper.CancelCombatPresentation();
                yield return new WaitForSecondsRealtime(NuclearExplosionVfx.DurationSeconds + 0.15f);
                fixture.AssertHiddenAndSilent();
                fixture.State.Player.Hand.Add("Nuclear");
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Cast persistent spell interaction failed.*"));
                Assert.That(fixture.Bootstrapper.TryCastPlayerPersistentSpell("Nuclear", out _), Is.False);
                fixture.AssertHiddenAndSilent();
                Assert.That(fixture.State.AIBoard.FalloutStacks, Is.EqualTo(1));
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator OnlineCastsPlaySequentiallyOnOppositeBoards_CancelStopsLaterGroup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                var events = new[] { CastEvent(PlayerId.Player), CastEvent(PlayerId.AI) };
                var duration = fixture.Bootstrapper.PlayOnlineBattleEventAnimations(events);
                Assert.That(duration, Is.GreaterThanOrEqualTo(2 * NuclearExplosionVfx.DurationSeconds));
                fixture.AssertGeometry(PlayerId.Player);
                yield return new WaitForSecondsRealtime(NuclearExplosionVfx.DurationSeconds + 0.2f);
                fixture.AssertGeometry(PlayerId.AI);
                Assert.That(fixture.State.Player.Master.CurrentHp, Is.EqualTo(333));
                Assert.That(fixture.State.AI.Master.CurrentHp, Is.EqualTo(333), "Online presentation never applies damage.");
                fixture.Bootstrapper.CancelCombatPresentation();
                fixture.AssertHiddenAndSilent();
                fixture.Bootstrapper.PlayOnlineBattleEventAnimations(events);
                fixture.Bootstrapper.CancelCombatPresentation();
                yield return new WaitForSecondsRealtime(NuclearExplosionVfx.DurationSeconds + 0.3f);
                fixture.AssertHiddenAndSilent();
                Assert.That(fixture.Bootstrapper.PlayOnlineBattleEventAnimations(Array.Empty<BattleEventDto>()), Is.Zero);
                fixture.AssertHiddenAndSilent();
            }
            yield return new ExitPlayMode();
        }

        private static BattleEventDto CastEvent(PlayerId caster) => new BattleEventDto
        {
            EventType = BattleEventType.SpellCast, CardId = NuclearRules.CardId,
            SourceOwnerId = caster, TargetOwnerId = caster, // Untargeted spells must not use this default as their board.
        };
        private static object Capture(BattleState state) => typeof(BattleBootstrapper).GetMethod("CaptureBattleSnapshot", Static).Invoke(null, new object[] { state });
        private static object Property(object request, string name) => request.GetType().GetProperty(name).GetValue(request);
        private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Hidden).SetValue(instance, value);

        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root = new GameObject("NuclearPresentationFixture");
            internal readonly BattleBootstrapper Bootstrapper;
            internal readonly NuclearExplosionVfx Effect;
            internal readonly LocalBattleGateway Gateway;
            internal readonly RectTransform CanvasRect;
            internal readonly BoardPresenter Board;
            internal float TileWidth;
            internal BattleState State => Gateway.CurrentBattleState;
            internal Image Image => Root.GetComponentsInChildren<Image>(true).SingleOrDefault(i => i.name == "NuclearMushroomExplosion");
            private readonly Camera _camera;
            private readonly RenderTexture _render;
            private readonly string _oldToken;

            internal Fixture(bool active = true)
            {
                var cameraObject = new GameObject("PreviewCamera", typeof(Camera), typeof(AudioListener));
                cameraObject.transform.SetParent(Root.transform);
                _camera = cameraObject.GetComponent<Camera>();
                _camera.transform.position = new Vector3(0, 0, -10);
                _camera.orthographic = true; _camera.orthographicSize = 5.4f;
                _camera.clearFlags = CameraClearFlags.SolidColor; _camera.backgroundColor = new Color(.035f, .045f, .06f);
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    _render = new RenderTexture(2340, 1080, 24); _render.Create(); _camera.targetTexture = _render;
                }
                var canvas = new GameObject("BattleCanvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
                canvas.transform.SetParent(Root.transform, false); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = _camera;
                CanvasRect = (RectTransform)canvas.transform; CanvasRect.localScale = Vector3.one * 0.01f;
                var boardHost = new GameObject("Board"); boardHost.SetActive(false); boardHost.transform.SetParent(Root.transform);
                Board = boardHost.AddComponent<BoardPresenter>(); Set(Board, "_useResponsiveLayout", false);
                Set(Board, "_playerTileViews", MakeTiles(PlayerId.Player)); Set(Board, "_aiTileViews", MakeTiles(PlayerId.AI));
                Effect = new GameObject("NuclearEffectHost").AddComponent<NuclearExplosionVfx>(); Effect.transform.SetParent(Root.transform);
                var host = new GameObject("Coordinator"); host.SetActive(false); host.transform.SetParent(Root.transform);
                Bootstrapper = host.AddComponent<BattleBootstrapper>(); Set(Bootstrapper, "_startOnAwake", false);
                Set(Bootstrapper, "_autoRunAiTurns", false); Set(Bootstrapper, "_returnToDraftSceneWhenRunSessionActive", false);
                var screenHost = new GameObject("DormantScreenPresenter"); screenHost.SetActive(false); screenHost.transform.SetParent(Root.transform);
                var screen = screenHost.AddComponent<BattleScreenPresenter>(); Set(screen, "_boardPresenter", Board);
                Set(Bootstrapper, "_battleScreenPresenter", screen); Set(Bootstrapper, "_nuclearExplosionVfx", Effect);
                var provider = JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).CreateProvider();
                var flow = new BattleFlowController(new BattleSetupService(), new MulliganService(), new TurnStartService(),
                    new BattleCommandProcessor(new PlayCardService(provider), new SpellService(provider), new MoveService(),
                        new AttackService(new TargetingService()), new EndTurnService()));
                Gateway = new LocalBattleGateway(flow);
                var deck = Enumerable.Repeat("Goblin", 33).ToArray(); Gateway.StartBattle(new BattleSetupRequest(deck, deck, PlayerId.Player));
                State.SetPhase(PhaseType.Main);
                Set(Bootstrapper, "_battleGateway", Gateway); Set(Bootstrapper, "_cardDefinitionProvider", provider);
                _oldToken = AccountSessionState.SessionToken;
                if (active)
                {
                    typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null, "synthetic-nuclear-presentation-session");
                    host.SetActive(true);
                }
                Layout(new Vector2(2340, 1080));
            }

            private TileView[] MakeTiles(PlayerId owner)
            {
                var tiles = new TileView[10];
                for (var i = 0; i < tiles.Length; i++)
                {
                    var tile = new GameObject(owner + "Tile" + i, typeof(RectTransform), typeof(Image), typeof(TileView)).GetComponent<TileView>();
                    tile.transform.SetParent(CanvasRect, false); tile.Configure(owner, i % 5, i / 5);
                    var image = tile.GetComponent<Image>(); image.color = new Color(.1f, .16f, .21f, .8f); image.raycastTarget = false;
                    var unit = new GameObject("ReferenceUnit", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    unit.transform.SetParent(tile.transform, false); unit.raycastTarget = false;
                    unit.sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Unit/A-301/Idle.png").OfType<Sprite>().First();
                    tiles[i] = tile;
                }
                return tiles;
            }

            internal void Layout(Vector2 size)
            {
                CanvasRect.sizeDelta = size;
                var layout = BoardResponsiveLayoutCalculator.Calculate((int)size.x, (int)size.y,
                    new Rect(0, 0, size.x, size.y), .9f, 24f, 48f, new Vector2(8, 8), 1f, 170f);
                TileWidth = layout.TileSize.x;
                foreach (var tiles in new[] { Board.PlayerTileViews, Board.AITileViews })
                {
                    foreach (var tile in tiles)
                    {
                        var rect = (RectTransform)tile.transform; rect.sizeDelta = layout.TileSize;
                        rect.anchoredPosition = layout.GetTileCenter(tile.OwnerId, tile.Coord);
                        ((RectTransform)tile.transform.Find("ReferenceUnit")).sizeDelta = layout.TileSize * (96f * 1.35f / 80f);
                    }
                }
                Canvas.ForceUpdateCanvases();
            }

            internal float Play(PlayerId caster) => (float)typeof(NuclearExplosionVfx).GetMethod("Play", Hidden).Invoke(Effect, new object[] { Board, caster });
            internal object OnlineRequest(BattleEventDto cast, IReadOnlyList<BattleEventDto> events) =>
                typeof(BattleBootstrapper).GetMethod("BuildOnlineImpactAnimationRequest", Hidden).Invoke(Bootstrapper, new object[] { cast?.CardId, cast, events });
            internal float Estimate(object request) => (float)typeof(BattleBootstrapper).GetMethod("EstimateImpactAnimationDuration", Hidden).Invoke(Bootstrapper, new[] { request });
            internal void AssertGeometry(PlayerId caster)
            {
                Assert.That(Image != null && Image.gameObject.activeInHierarchy, Is.True);
                var tiles = caster == PlayerId.Player ? Board.AITileViews : Board.PlayerTileViews;
                var worldCenter = tiles.Select(t => t.transform.position).Aggregate(Vector3.zero, (sum, p) => sum + p) / tiles.Length;
                Assert.That(Vector3.Distance(Image.rectTransform.position, worldCenter), Is.LessThan(.001f), "Ignition origin stays at target board center.");
                Assert.That(Image.rectTransform.pivot, Is.EqualTo(new Vector2(.5f, 32f / 512f)));
                var corners = new Vector3[4]; Image.rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var local = CanvasRect.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(CanvasRect.rect.xMin, CanvasRect.rect.xMax));
                    Assert.That(local.y, Is.InRange(CanvasRect.rect.yMin, CanvasRect.rect.yMax));
                }
            }
            internal void AssertHiddenAndSilent()
            {
                Assert.That(Image == null || !Image.gameObject.activeInHierarchy, Is.True);
                Assert.That(Effect.GetComponents<AudioSource>().Any(a => a.isPlaying), Is.False);
            }
            internal void Capture()
            {
                if (_render == null) return;
                Canvas.ForceUpdateCanvases(); _camera.Render();
                var old = RenderTexture.active; var texture = new Texture2D(_render.width, _render.height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = _render; texture.ReadPixels(new Rect(0, 0, _render.width, _render.height), 0, 0); texture.Apply();
                    var path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Tools/Art/Preview/NuclearExplosionPreview.png"));
                    Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally { RenderTexture.active = old; Object.DestroyImmediate(texture); }
            }
            public void Dispose()
            {
                typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null, _oldToken);
                Object.DestroyImmediate(Root);
                if (_render != null) { _render.Release(); Object.DestroyImmediate(_render); }
            }
        }
    }
}
