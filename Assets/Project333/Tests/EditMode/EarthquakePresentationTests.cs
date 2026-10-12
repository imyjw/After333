using System;
using System.Collections;
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
using Project333.Runtime.Presentation.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class EarthquakePresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        [UnityTearDown] public IEnumerator LeavePlayMode() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest]
        public IEnumerator AuthoredBattleTileCanvasesShowTenCracksAndRestoreScreenShakeForBothCasters()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Battle_VSlice.unity", OpenSceneMode.Single);
            var boot = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleBootstrapper>(true)).Single();
            boot.enabled = false;
            Set(boot, "_startOnAwake", false);
            Set(boot, "_startWithDraftBeforeBattle", false);
            Set(boot, "_autoRunAiTurns", false);
            foreach (var connection in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OnlineBattleConnectionTester>(true)).ToArray())
                Object.DestroyImmediate(connection);
            Object.DestroyImmediate(boot);
            yield return new EnterPlayMode();
            var board = Object.FindFirstObjectByType<BoardPresenter>();
            Assert.That(board, Is.Not.Null, "The authored BoardPresenter must remain active after entering Play Mode.");
            board.ApplyResponsiveLayoutNow();
            Canvas.ForceUpdateCanvases();
            yield return null;
            var effectHost = new GameObject("ActualSceneEarthquakeFixture");
            var effect = effectHost.AddComponent<EarthquakeVfx>();
            foreach (var caster in new[] { PlayerId.Player, PlayerId.AI })
            {
                var targetTiles = caster == PlayerId.Player ? board.AITileViews : board.PlayerTileViews;
                Assert.That(targetTiles, Is.Not.Null);
                Assert.That(targetTiles.Length, Is.EqualTo(10));
                foreach (var tile in targetTiles)
                {
                    Assert.That(tile, Is.Not.Null);
                    var textView = tile.GetComponent<TileTextView>();
                    Assert.That(textView, Is.Not.Null, "TileTextView missing on " + tile.name);
                    Assert.That(textView.ActiveEffectButtonAnchor, Is.Not.Null, "Tile background anchor missing on " + tile.name);
                }
                var anchors = targetTiles.OrderBy(t => t.Coord.Column).ThenBy(t => t.Coord.Row)
                    .Select(t => t.GetComponent<TileTextView>().ActiveEffectButtonAnchor).ToArray();
                var tileCanvases = anchors.Select(a => a.GetComponentInParent<Canvas>(true).rootCanvas).ToArray();
                Assert.That(tileCanvases.Distinct().Count(), Is.GreaterThan(1), "Keep the actual independent tile viewports.");
                // A lambda capturing board would create a display class before EnterPlayMode.
                // Unity TestRunner does not restore that compiler-generated closure after the domain reload.
                var ownedRects = new System.Collections.Generic.HashSet<RectTransform>();
                foreach (var crack in (Image[])effect.GetType().GetField("_cracks", Hidden).GetValue(effect))
                    if (crack != null) ownedRects.Add(crack.rectTransform);
                var movableList = new System.Collections.Generic.List<RectTransform>();
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (canvas.gameObject.scene != board.gameObject.scene || canvas.rootCanvas != canvas || !canvas.isActiveAndEnabled) continue;
                    for (var index = 0; index < canvas.transform.childCount; index++)
                        if (canvas.transform.GetChild(index) is RectTransform rect && !ownedRects.Contains(rect)) movableList.Add(rect);
                }
                var movable = movableList.ToArray();
                var cleanPositions = movable.Select(r => r.localPosition).ToArray();
                var playMethod = effect.GetType().GetMethod("Play", Hidden);
                Assert.That(playMethod, Is.Not.Null, "Earthquake Play reflection must resolve.");
                var duration = (float)playMethod.Invoke(effect, new object[] { board, caster });
                Assert.That(duration, Is.EqualTo(EarthquakeVfx.DurationSeconds),
                    "Authored unit-sized visual canvases must not prevent the field effect from playing. caster=" + caster);
                var cracks = (Image[])typeof(EarthquakeVfx).GetField("_cracks", Hidden).GetValue(effect);
                Assert.That(cracks.Length, Is.EqualTo(10));
                for (var index = 0; index < cracks.Length; index++)
                {
                    var crack = cracks[index];
                    Assert.That(crack.gameObject.activeInHierarchy, Is.True);
                    Assert.That(crack.sprite.name, Is.EqualTo("Cracks_00"));
                    Assert.That(crack.rectTransform.parent, Is.SameAs(tileCanvases[index].transform),
                        "Ground cracks must use the tile background Canvas rather than the unit body Canvas.");
                    Assert.That(crack.GetComponent<Canvas>().overrideSorting, Is.True);
                    Assert.That(crack.GetComponent<Canvas>().sortingOrder, Is.EqualTo(200));
                    Assert.That(crack.raycastTarget, Is.False);
                    var camera = tileCanvases[index].renderMode == RenderMode.ScreenSpaceOverlay ? null : tileCanvases[index].worldCamera;
                    var actual = RectTransformUtility.WorldToScreenPoint(camera, crack.rectTransform.TransformPoint(crack.rectTransform.rect.center));
                    var expected = RectTransformUtility.WorldToScreenPoint(camera, anchors[index].TransformPoint(anchors[index].rect.center));
                    Assert.That(Vector2.Distance(actual, expected), Is.LessThan(2f), "Crack " + index + " must center on its logical enemy tile.");
                    Assert.That(crack.rectTransform.rect.width, Is.GreaterThan(anchors[index].rect.width * .7f));
                }
                Set(effect, "_startedAt", Time.unscaledTime - .51f);
                typeof(EarthquakeVfx).GetMethod("LateUpdate", Hidden).Invoke(effect, null);
                Assert.That(cracks.All(c => c.gameObject.activeInHierarchy && c.sprite.name == "Cracks_08"), Is.True,
                    "All ten cracks advance their frames together.");
                Assert.That(movable.Where((r, index) => Vector3.Distance(r.localPosition, cleanPositions[index]) > .001f).Any(), Is.True,
                    "The authored scene UI visibly trembles.");
                typeof(EarthquakeVfx).GetMethod("Cancel", Hidden).Invoke(effect, null);
                Assert.That(cracks.All(c => !c.gameObject.activeInHierarchy), Is.True);
                for (var index = 0; index < movable.Length; index++)
                    Assert.That(Vector3.Distance(movable[index].localPosition, cleanPositions[index]), Is.LessThan(.002f),
                        "Cancel restores only earthquake's shake for " + movable[index].name);
                Assert.That(effect.GetComponents<AudioSource>().Length, Is.EqualTo(1));
                Assert.That(effect.GetComponent<AudioSource>().isPlaying, Is.False);
            }
            Object.Destroy(effectHost);
            yield return new ExitPlayMode();

        }

        [Test]
        public void NativeCrackCellsAndQuietSoundMatchTheResourceContract()
        {
            var frames = Resources.LoadAll<Sprite>(EarthquakeVfx.FramesResourcePath).OrderBy(s => s.name).ToArray();
            Assert.That(frames.Length, Is.EqualTo(16));
            for (var index = 0; index < frames.Length; index++)
            {
                Assert.That(frames[index].name, Is.EqualTo("Cracks_" + index.ToString("D2")));
                Assert.That(frames[index].rect, Is.EqualTo(new Rect(index % 4 * 512, (3 - index / 4) * 512, 512, 512)));
                Assert.That(frames[index].pivot, Is.EqualTo(Vector2.one * 256));
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frames[0]));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            var sound = Resources.Load<AudioClip>(EarthquakeVfx.SoundResourcePath);
            Assert.That(sound, Is.Not.Null);
            Assert.That(sound.channels, Is.EqualTo(1));
            Assert.That(sound.frequency, Is.EqualTo(44100));
            Assert.That(sound.length, Is.EqualTo(1.08f).Within(.002f));
            Assert.That(EarthquakeVfx.DurationSeconds, Is.GreaterThan(sound.length));
            Assert.That(EarthquakeVfx.SoundGain, Is.EqualTo(.30f));
        }

        [TestCase(PlayerId.Player)] [TestCase(PlayerId.AI)]
        public void TenEmptyEnemyTilesReceiveCenteredCracksAcrossScreenShapes(PlayerId caster)
        {
            using (var fixture = new Fixture(false))
            {
                foreach (var size in new[] { new Vector2(2340, 1080), new Vector2(1280, 720), new Vector2(1080, 2340) })
                {
                    fixture.Layout(size);
                    Assert.That(fixture.Play(caster), Is.EqualTo(EarthquakeVfx.DurationSeconds));
                    fixture.AssertCrackGeometry(caster);
                    Assert.That(fixture.Board.PlayerTileViews.Concat(fixture.Board.AITileViews).All(t => t.CurrentOccupant == null), Is.True);
                    fixture.Cancel();
                }
            }
        }

        [Test]
        public void OverlayHudAndIndependentCanvasesShakeOnceWithoutChangingCameraOrHierarchy()
        {
            using (var fixture = new Fixture(false, overlayHud: true))
            {
                var tile = (RectTransform)fixture.Board.AITileViews[0].transform;
                var localTile = tile.localPosition; var localHand = fixture.Hand.localPosition;
                var localGear = fixture.Gear.localPosition; var localBackground = fixture.Background.localPosition;
                var parents = new[] { tile.parent, fixture.Hand.parent, fixture.Gear.parent, fixture.Background.parent };
                var cameraPosition = fixture.Camera.transform.position;
                fixture.Play(PlayerId.Player); fixture.SampleAge(.1f);
                Assert.That(Vector3.Distance(tile.localPosition, localTile), Is.GreaterThan(.01f));
                Assert.That(Vector3.Distance(fixture.Hand.localPosition, localHand), Is.GreaterThan(.01f));
                Assert.That(Vector3.Distance(fixture.Gear.localPosition, localGear), Is.GreaterThan(.01f));
                Assert.That(Vector3.Distance(fixture.Background.localPosition, localBackground), Is.GreaterThan(.01f));
                Assert.That(fixture.Nested.localPosition, Is.EqualTo(Vector3.zero), "Nested Canvas content inherits the parent delta exactly once.");
                Assert.That(fixture.Camera.transform.position, Is.EqualTo(cameraPosition));
                Assert.That(new[] { tile.parent, fixture.Hand.parent, fixture.Gear.parent, fixture.Background.parent }, Is.EqualTo(parents));
                fixture.RestoreEarly();
                Assert.That(Vector3.Distance(tile.localPosition, localTile), Is.LessThan(.001f));
                Assert.That(Vector3.Distance(fixture.Hand.localPosition, localHand), Is.LessThan(.001f));
                fixture.SampleAge(.1f);
                var external = new Vector3(12, -7, 0); fixture.Hand.localPosition += external;
                fixture.CanvasRect.localRotation = Quaternion.Euler(0, 0, 11);
                fixture.Cancel();
                Assert.That(Vector3.Distance(fixture.Hand.localPosition, localHand + external), Is.LessThan(.001f), "Cancel removes its contribution, preserving external movement and changed canvas transforms.");
                Assert.That(Vector3.Distance(tile.localPosition, localTile), Is.LessThan(.001f));
            }
        }

        [Test]
        public void ElapsedTimeControlsFramesAndFinishesShakeBeforeTheAudioTail()
        {
            using (var fixture = new Fixture(false))
            {
                fixture.Play(PlayerId.Player);
                var first = fixture.Cracks[0].sprite;
                fixture.SampleAge(0f); Assert.That(fixture.Cracks[0].sprite, Is.SameAs(first));
                fixture.SampleAge(.51f); Assert.That(fixture.Cracks.All(c => c.sprite.name == "Cracks_08"), Is.True);
                fixture.RestoreEarly(); var clean = fixture.Hand.localPosition;
                fixture.SampleAge(.9f); Assert.That(fixture.Hand.localPosition, Is.EqualTo(clean));
                fixture.SampleAge(1.05f); Assert.That(fixture.Cracks[0].color.a, Is.InRange(.1f, .6f));
                fixture.SampleAge(1.16f); fixture.AssertHiddenAndSilent();
            }
        }

        [Test]
        public void InvalidOrDisabledLayoutCannotExposeCracksShakeOrStartSound()
        {
            using (var fixture = new Fixture(false))
            {
                var hand = fixture.Hand.localPosition;
                fixture.Layout(Vector2.zero);
                Assert.That(fixture.Play(PlayerId.Player), Is.Zero); fixture.AssertHiddenAndSilent();
                Assert.That(fixture.Hand.localPosition, Is.EqualTo(hand));
                fixture.Layout(new Vector2(2340, 1080)); fixture.Play(PlayerId.Player); fixture.SampleAge(.1f);
                fixture.Board.gameObject.SetActive(false); fixture.SampleAge(.15f); fixture.AssertHiddenAndSilent();
                Assert.That(Vector3.Distance(fixture.Hand.localPosition, hand), Is.LessThan(.001f));
                fixture.Board.gameObject.SetActive(true);
                fixture.Board.AITileViews[0].gameObject.SetActive(false);
                Assert.That(fixture.Play(PlayerId.Player), Is.Zero); fixture.AssertHiddenAndSilent();
            }
        }

        [UnityTest]
        public IEnumerator RealEarlyResetLifecycleAndCancellationKeepOneReusableAudioSource()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                var hand = fixture.Hand.localPosition;
                fixture.Play(PlayerId.Player);
                yield return null;
                yield return new WaitForSecondsRealtime(.10f);
                fixture.SampleAge(.1f);
                Assert.That(Vector3.Distance(fixture.Hand.localPosition, hand), Is.GreaterThan(.001f));
                Assert.That(fixture.Effect.GetComponents<MonoBehaviour>().Any(c => c.GetType().Name == "EarthquakeShakeReset" && c.isActiveAndEnabled), Is.True);
                Assert.That(fixture.Effect.GetComponents<AudioSource>().Length, Is.EqualTo(1));
                Assert.That(fixture.Effect.GetComponent<AudioSource>().isPlaying, Is.True);
                fixture.Effect.enabled = false; fixture.AssertHiddenAndSilent();
                Assert.That(Vector3.Distance(fixture.Hand.localPosition, hand), Is.LessThan(.001f));
                fixture.Effect.enabled = true; fixture.Play(PlayerId.AI); fixture.AssertCrackGeometry(PlayerId.AI);
                yield return new WaitForSecondsRealtime(EarthquakeVfx.DurationSeconds + .05f);
                fixture.AssertHiddenAndSilent();
                Assert.That(Vector3.Distance(fixture.Hand.localPosition, hand), Is.LessThan(.001f));
                fixture.Play(PlayerId.Player); fixture.SampleAge(.1f);
                var effectHost = fixture.Effect.gameObject;
                Object.Destroy(fixture.Effect); yield return null; yield return null;
                Assert.That(fixture.Root.GetComponentsInChildren<Image>(true).Any(i => i.name.StartsWith("EarthquakeCrack_")), Is.False);
                Assert.That(effectHost.GetComponents<AudioSource>(), Is.Empty, "Destroying only the effect component releases its owned sound source.");
                Assert.That(Vector3.Distance(fixture.Hand.localPosition, hand), Is.LessThan(.001f));
            }
            yield return new ExitPlayMode();
        }

        [TestCase(PlayerId.Player, PlayerId.Player)] [TestCase(PlayerId.Player, PlayerId.AI)]
        [TestCase(PlayerId.AI, PlayerId.Player)] [TestCase(PlayerId.AI, PlayerId.AI)]
        public void LocalAndProjectedOnlineRequestsKeepCasterIdentityAndSnapshotDoesNotReplay(PlayerId caster, PlayerId localSeat)
        {
            using (var fixture = new Fixture(false))
            {
                fixture.State.RestoreRuntimeState(fixture.State.TurnNumber, caster, PhaseType.Main);
                var before = Capture(fixture.State);
                fixture.State.GetPlayer(caster).Hand.Add(EarthquakeRules.CardId);
                fixture.State.GetPlayer(caster).Resources.Add(new ResourceSet(0, 0, 0, 3));
                fixture.Gateway.ExecuteCommand(caster, new CastScriptedSpellCommand(EarthquakeRules.CardId));
                var local = typeof(BattleBootstrapper).GetMethod("BuildImpactAnimationRequest", Static).Invoke(null,
                    new object[] { before, Capture(fixture.State), fixture.State.ValuePopupEvents.ToArray(), EarthquakeRules.CardId, caster });
                AssertRequest(local, caster);
                Assert.That(fixture.Estimate(local), Is.GreaterThanOrEqualTo(EarthquakeVfx.DurationSeconds));
                var host = new GameObject("DormantProjection"); host.SetActive(false); host.transform.SetParent(fixture.Root.transform);
                var connection = host.AddComponent<OnlineBattleConnectionTester>(); Set(connection, "_localPlayerId", localSeat);
                var cast = CastEvent(caster);
                var projected = (System.Collections.Generic.IReadOnlyList<BattleEventDto>)typeof(OnlineBattleConnectionTester)
                    .GetMethod("ProjectBattleEventsToLocalPerspective", Hidden).Invoke(connection, new object[] { new[] { cast } });
                var online = fixture.OnlineRequest(projected[0], projected);
                AssertRequest(online, caster == localSeat ? PlayerId.Player : PlayerId.AI);
                Assert.That(fixture.Estimate(online), Is.EqualTo(EarthquakeVfx.DurationSeconds + .1f).Within(.001f), "A cast cue has the same spell duration before its separate damage packets.");
                var restored = fixture.OnlineRequest(null, Array.Empty<BattleEventDto>());
                Assert.That(Property(restored, "SpellEffectId"), Is.Null);
                Assert.That(fixture.Estimate(restored), Is.Zero);
                Assert.That(fixture.State.GetOpponent(caster).Master.CurrentHp, Is.EqualTo(323));
                Assert.That(fixture.State.GetPlayer(caster).Master.CurrentHp, Is.EqualTo(333));
                fixture.AssertHiddenAndSilent();
            }
        }

        [UnityTest]
        public IEnumerator EmptyEnemyTileDropResolvesWholeBoardOnce_FailedOwnTileAndBusyDoNotReplay()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                var state = fixture.State;
                var unit = new UnitState("quake-unit", "Goblin", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 1, 50, true, false, 0);
                var building = new BuildingState("quake-building", "ManaPond", PlayerId.AI, new TileCoord(1, 0), false, 0, 80);
                state.AIBoard.Place(unit.Position, unit); state.AIBoard.Place(building.Position, building);
                state.Player.Hand.Add(EarthquakeRules.CardId); state.Player.Resources.Add(new ResourceSet(0, 0, 0, 3));
                Assert.That(fixture.Bootstrapper.TryUsePlayerCardOnTile(EarthquakeRules.CardId, PlayerId.Player, new TileCoord(4, 1), out _), Is.False);
                fixture.AssertHiddenAndSilent(); Assert.That(unit.CurrentHp, Is.EqualTo(50));
                Assert.That(fixture.Bootstrapper.TryUsePlayerCardOnTile(EarthquakeRules.CardId, PlayerId.AI, new TileCoord(4, 1), out _), Is.True);
                fixture.AssertCrackGeometry(PlayerId.Player);
                Assert.That(unit.CurrentHp, Is.EqualTo(40)); Assert.That(building.CurrentHp, Is.EqualTo(50));
                Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(323));
                Assert.That(state.ValuePopupEvents.Count(e => e.SourceCardId == EarthquakeRules.CardId), Is.EqualTo(3));
                Assert.That(fixture.Bootstrapper.TryCastPlayerPersistentSpell(EarthquakeRules.CardId, out _), Is.False);
                Assert.That(unit.CurrentHp, Is.EqualTo(40));
                fixture.Bootstrapper.CancelCombatPresentation(); fixture.AssertHiddenAndSilent();
                var timeout = Time.unscaledTime + EarthquakeVfx.DurationSeconds + 1f;
                while (fixture.Bootstrapper.IsBusy && Time.unscaledTime < timeout) yield return null;
                Assert.That(fixture.Bootstrapper.IsBusy, Is.False, "The canceled presentation must finish releasing its input lock before validating a new command.");
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Cast persistent spell interaction failed.*"));
                Assert.That(fixture.Bootstrapper.TryCastPlayerPersistentSpell(EarthquakeRules.CardId, out _), Is.False);
                fixture.AssertHiddenAndSilent();
                Assert.That(unit.CurrentHp, Is.EqualTo(40)); Assert.That(building.CurrentHp, Is.EqualTo(50));
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RealCameraPixelsShowCracksOnEveryEnemyTileAndAVisibleWholeHudShake()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                foreach (var caster in new[] { PlayerId.Player, PlayerId.AI })
                {
                    fixture.Cancel(); var baseline = fixture.CapturePixels();
                    fixture.Play(caster); fixture.SampleAge(.5f); fixture.RestoreEarly();
                    var cracks = fixture.CapturePixels("Earthquake-Cracks-" + caster);
                    try
                    {
                        foreach (var tile in fixture.EnemyTiles(caster)) Assert.That(fixture.DifferentPixels(cracks, baseline, tile.transform.position, 55), Is.GreaterThan(12), "Rendered crack pixels must exist on " + tile.OwnerId + " " + tile.Coord);
                        foreach (var tile in fixture.EnemyTiles(caster == PlayerId.Player ? PlayerId.AI : PlayerId.Player)) Assert.That(fixture.DifferentPixels(cracks, baseline, tile.transform.position, 45), Is.Zero, "Own field has no crack pixels.");
                    }
                    finally { Object.DestroyImmediate(baseline); Object.DestroyImmediate(cracks); }
                }
                fixture.Cancel(); var still = fixture.CapturePixels("Earthquake-Hud-Baseline");
                var gear = fixture.Gear.position; fixture.Play(PlayerId.Player); fixture.SampleAge(.1f);
                var shaken = fixture.CapturePixels("Earthquake-Hud-Shaken");
                try { Assert.That(fixture.DifferentPixels(shaken, still, gear, 50), Is.GreaterThan(15), "A HUD edge actually moves in the camera render."); }
                finally { Object.DestroyImmediate(still); Object.DestroyImmediate(shaken); }
            }
            yield return new ExitPlayMode();
        }

        private static object Capture(BattleState state) => typeof(BattleBootstrapper).GetMethod("CaptureBattleSnapshot", Static).Invoke(null, new object[] { state });
        private static object Property(object instance, string name) => instance.GetType().GetProperty(name).GetValue(instance);
        private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Hidden).SetValue(instance, value);
        private static void AssertRequest(object request, PlayerId caster)
        { Assert.That(Property(request, "SpellEffectId"), Is.EqualTo(EarthquakeRules.EffectId)); Assert.That(Property(request, "SpellCasterOwnerId"), Is.EqualTo(caster)); }
        private static BattleEventDto CastEvent(PlayerId caster) => new BattleEventDto { EventType = BattleEventType.SpellCast, CardId = EarthquakeRules.CardId, SourceCardId = EarthquakeRules.CardId, SourceOwnerId = caster };

        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root = new GameObject("EarthquakePresentationFixture");
            internal readonly BoardPresenter Board;
            internal readonly EarthquakeVfx Effect;
            internal readonly BattleBootstrapper Bootstrapper;
            internal readonly LocalBattleGateway Gateway;
            internal readonly Camera Camera;
            internal readonly RectTransform CanvasRect;
            internal readonly RectTransform Hand;
            internal readonly RectTransform Gear;
            internal readonly RectTransform Background;
            internal readonly RectTransform Nested;
            internal BattleState State => Gateway.CurrentBattleState;
            internal Image[] Cracks => Root.GetComponentsInChildren<Image>(true).Where(i => i.name.StartsWith("EarthquakeCrack_")).OrderBy(i => i.name).ToArray();
            private readonly RenderTexture _render;
            private readonly string _oldToken;

            internal Fixture(bool active = true, bool overlayHud = false)
            {
                var cameraHost = new GameObject("PreviewCamera", typeof(Camera)); cameraHost.transform.SetParent(Root.transform);
                Camera = cameraHost.GetComponent<Camera>(); Camera.transform.position = new Vector3(0, 0, -10);
                Camera.orthographic = true; Camera.orthographicSize = 5.4f;
                Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(.035f, .045f, .06f);
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                { _render = new RenderTexture(2340, 1080, 24); _render.Create(); Camera.targetTexture = _render; }
                var canvas = NewCanvas("BattleCanvas", false); CanvasRect = (RectTransform)canvas.transform;
                var hud = overlayHud ? NewCanvas("SeparateOverlayHud", true) : canvas;
                Hand = UiRect("Hand", hud.transform, new Vector2(0, -460), new Vector2(260, 55), new Color(.1f, .25f, .4f));
                Gear = UiRect("Gear", hud.transform, new Vector2(900, 430), new Vector2(55, 55), Color.white);
                var bgCanvas = NewCanvas("IndependentBackgroundCanvas", false); bgCanvas.sortingOrder = -1000;
                Background = UiRect("Background", bgCanvas.transform, Vector2.zero, new Vector2(2340, 1080), new Color(.04f, .07f, .09f));
                var nestedCanvas = new GameObject("NestedCanvas", typeof(RectTransform), typeof(Canvas)); nestedCanvas.transform.SetParent(Hand, false);
                Nested = UiRect("NestedContent", nestedCanvas.transform, Vector2.zero, new Vector2(10, 10), Color.cyan);
                var boardHost = new GameObject("Board"); boardHost.SetActive(false); boardHost.transform.SetParent(Root.transform);
                Board = boardHost.AddComponent<BoardPresenter>(); Set(Board, "_useResponsiveLayout", false);
                Set(Board, "_playerTileViews", MakeTiles(PlayerId.Player)); Set(Board, "_aiTileViews", MakeTiles(PlayerId.AI)); boardHost.SetActive(true);
                // Enabling BoardPresenter invokes Awake and rebuilds empty roots; restore this fixture's explicit registry.
                Set(Board, "_playerTileViews", _playerTiles); Set(Board, "_aiTileViews", _aiTiles);
                Effect = new GameObject("EarthquakeEffectHost").AddComponent<EarthquakeVfx>(); Effect.transform.SetParent(Root.transform);
                var host = new GameObject("Coordinator"); host.SetActive(false); host.transform.SetParent(Root.transform);
                Bootstrapper = host.AddComponent<BattleBootstrapper>(); Set(Bootstrapper, "_startOnAwake", false); Set(Bootstrapper, "_autoRunAiTurns", false);
                Set(Bootstrapper, "_returnToDraftSceneWhenRunSessionActive", false);
                var screenHost = new GameObject("DormantScreen"); screenHost.SetActive(false); screenHost.transform.SetParent(Root.transform);
                var screen = screenHost.AddComponent<BattleScreenPresenter>(); Set(screen, "_boardPresenter", Board);
                Set(Bootstrapper, "_battleScreenPresenter", screen); Set(Bootstrapper, "_earthquakeVfx", Effect);
                var provider = JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).CreateProvider();
                var flow = new BattleFlowController(new BattleSetupService(), new MulliganService(), new TurnStartService(),
                    new BattleCommandProcessor(new PlayCardService(provider), new SpellService(provider), new MoveService(), new AttackService(new TargetingService()), new EndTurnService()));
                Gateway = new LocalBattleGateway(flow); var deck = Enumerable.Repeat("Goblin", 33).ToArray(); Gateway.StartBattle(new BattleSetupRequest(deck, deck, PlayerId.Player)); State.SetPhase(PhaseType.Main);
                Set(Bootstrapper, "_battleGateway", Gateway); Set(Bootstrapper, "_cardDefinitionProvider", provider); _oldToken = AccountSessionState.SessionToken;
                if (active) { typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null, "synthetic-earthquake-presentation-session"); host.SetActive(true); }
                Layout(new Vector2(2340, 1080));
            }

            private TileView[] _playerTiles;
            private TileView[] _aiTiles;
            private Canvas NewCanvas(string name, bool overlay)
            {
                var canvas = new GameObject(name, typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>(); canvas.transform.SetParent(Root.transform, false);
                canvas.renderMode = overlay ? RenderMode.ScreenSpaceOverlay : RenderMode.WorldSpace; canvas.worldCamera = Camera;
                var rect = (RectTransform)canvas.transform; rect.sizeDelta = new Vector2(2340, 1080); if (!overlay) rect.localScale = Vector3.one * .01f;
                return canvas;
            }
            private static RectTransform UiRect(string name, Transform parent, Vector2 center, Vector2 size, Color color)
            {
                var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>(); image.transform.SetParent(parent, false); image.color = color; image.raycastTarget = false;
                image.rectTransform.sizeDelta = size; image.rectTransform.anchoredPosition = center; return image.rectTransform;
            }
            private TileView[] MakeTiles(PlayerId owner)
            {
                var tiles = new TileView[10];
                for (var index = 0; index < tiles.Length; index++)
                {
                    var rect = UiRect(owner + "Tile" + index, CanvasRect, Vector2.zero, Vector2.one, new Color(.13f, .18f, .23f));
                    var tile = rect.gameObject.AddComponent<TileView>(); tile.Configure(owner, index / 2, index % 2); tiles[index] = tile;
                }
                if (owner == PlayerId.Player) _playerTiles = tiles; else _aiTiles = tiles;
                return tiles;
            }
            internal TileView[] EnemyTiles(PlayerId caster) => caster == PlayerId.Player ? Board.AITileViews : Board.PlayerTileViews;
            internal void Layout(Vector2 size)
            {
                RestoreEarly(); CanvasRect.sizeDelta = size;
                var layout = BoardResponsiveLayoutCalculator.Calculate((int)size.x, (int)size.y, new Rect(0, 0, size.x, size.y), .9f, 24f, 48f, new Vector2(8, 8), 1f, 170f);
                foreach (var tile in Board.PlayerTileViews.Concat(Board.AITileViews))
                { var rect = (RectTransform)tile.transform; rect.sizeDelta = layout.TileSize; rect.anchoredPosition = layout.GetTileCenter(tile.OwnerId, tile.Coord); }
                Canvas.ForceUpdateCanvases();
            }
            internal float Play(PlayerId caster) => (float)typeof(EarthquakeVfx).GetMethod("Play", Hidden).Invoke(Effect, new object[] { Board, caster });
            internal void Cancel() { if (Effect != null) typeof(EarthquakeVfx).GetMethod("Cancel", Hidden).Invoke(Effect, null); }
            internal void RestoreEarly() { if (Effect != null) typeof(EarthquakeVfx).GetMethod("RestoreShakeOffsets", Hidden).Invoke(Effect, null); }
            internal void SampleAge(float age) { Set(Effect, "_startedAt", Time.unscaledTime - age); typeof(EarthquakeVfx).GetMethod("LateUpdate", Hidden).Invoke(Effect, null); Canvas.ForceUpdateCanvases(); }
            internal object OnlineRequest(BattleEventDto source, System.Collections.Generic.IReadOnlyList<BattleEventDto> events) => typeof(BattleBootstrapper).GetMethod("BuildOnlineImpactAnimationRequest", Hidden).Invoke(Bootstrapper, new object[] { source?.CardId, source, events });
            internal float Estimate(object request) => (float)typeof(BattleBootstrapper).GetMethod("EstimateImpactAnimationDuration", Hidden).Invoke(Bootstrapper, new[] { request });
            internal void AssertCrackGeometry(PlayerId caster)
            {
                Assert.That(Cracks.Length, Is.EqualTo(10));
                var tiles = EnemyTiles(caster);
                foreach (var crack in Cracks)
                {
                    Assert.That(crack.gameObject.activeInHierarchy, Is.True); Assert.That(crack.raycastTarget, Is.False);
                    Assert.That(crack.GetComponent<Canvas>().sortingOrder, Is.EqualTo(200));
                    var tile = tiles.Single(t => Vector3.Distance(t.transform.position, crack.transform.position) < .002f);
                    Assert.That(crack.rectTransform.rect.width, Is.EqualTo(((RectTransform)tile.transform).rect.width * .9f).Within(.02f));
                    Assert.That(crack.rectTransform.rect.height, Is.EqualTo(crack.rectTransform.rect.width));
                    var corners = new Vector3[4]; crack.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    { var p = CanvasRect.InverseTransformPoint(corner); Assert.That(p.x, Is.InRange(CanvasRect.rect.xMin - .01f, CanvasRect.rect.xMax + .01f)); Assert.That(p.y, Is.InRange(CanvasRect.rect.yMin - .01f, CanvasRect.rect.yMax + .01f)); }
                }
            }
            internal void AssertHiddenAndSilent()
            { Assert.That(Cracks.All(c => !c.gameObject.activeInHierarchy), Is.True); Assert.That(Effect == null || !Effect.GetComponents<AudioSource>().Any(a => a.isPlaying), Is.True); }
            internal Texture2D CapturePixels(string evidenceName = null)
            {
                Assert.That(_render, Is.Not.Null, "Pixel verification requires the isolated Editor graphics device."); Canvas.ForceUpdateCanvases(); Camera.Render();
                var old = RenderTexture.active; var texture = new Texture2D(_render.width, _render.height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = _render; texture.ReadPixels(new Rect(0, 0, _render.width, _render.height), 0, 0); texture.Apply();
                    if (evidenceName != null)
                    {
                        var path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Tools/Art/Preview/" + evidenceName + ".png"));
                        Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, texture.EncodeToPNG());
                    }
                    return texture;
                }
                finally { RenderTexture.active = old; }
            }
            internal int DifferentPixels(Texture2D image, Texture2D baseline, Vector3 world, int radius)
            {
                var point = Camera.WorldToViewportPoint(world); var x = Mathf.RoundToInt(point.x * image.width); var y = Mathf.RoundToInt(point.y * image.height); var count = 0;
                for (var px = Mathf.Max(0, x - radius); px < Mathf.Min(image.width, x + radius); px++)
                for (var py = Mathf.Max(0, y - radius); py < Mathf.Min(image.height, y + radius); py++) if (image.GetPixel(px, py) != baseline.GetPixel(px, py)) count++;
                return count;
            }
            public void Dispose()
            { typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null, _oldToken); Object.DestroyImmediate(Root); if (_render != null) { _render.Release(); Object.DestroyImmediate(_render); } }
        }
    }
}
