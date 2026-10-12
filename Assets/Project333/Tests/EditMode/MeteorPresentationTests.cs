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
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class MeteorPresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        [UnityTearDown] public IEnumerator LeavePlayMode() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void NativeFullCellsAndTwoQuietClipsMatchTheFlightAndImpactContract()
        {
            var flight = Resources.LoadAll<Sprite>(MeteorVfx.FlightResourcePath); Assert.That(flight.Length, Is.EqualTo(1));
            Assert.That(flight[0].rect, Is.EqualTo(new Rect(0, 0, 512, 512))); Assert.That(flight[0].pivot, Is.EqualTo(Vector2.one * 256));
            var frames = Resources.LoadAll<Sprite>(MeteorVfx.ImpactResourcePath).OrderByDescending(s => s.rect.y).ThenBy(s => s.rect.x).ToArray();
            Assert.That(frames.Length, Is.EqualTo(16));
            for (var index = 0; index < frames.Length; index++)
            {
                Assert.That(frames[index].name, Is.EqualTo("Impact_" + index).Or.EqualTo("Impact_" + index.ToString("D2")));
                Assert.That(frames[index].rect, Is.EqualTo(new Rect(index % 4 * 512, (3 - index / 4) * 512, 512, 512)));
                Assert.That(frames[index].pivot, Is.EqualTo(Vector2.one * 256));
            }
            foreach (var sprite in new[] { flight[0], frames[0] })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            }
            var incoming = Resources.Load<AudioClip>(MeteorVfx.IncomingSoundResourcePath); var impact = Resources.Load<AudioClip>(MeteorVfx.ImpactSoundResourcePath);
            Assert.That(incoming.length, Is.EqualTo(.65f).Within(.002f)); Assert.That(impact.length, Is.EqualTo(1f).Within(.002f));
            Assert.That(new[] { incoming, impact }.All(c => c.channels == 1 && c.frequency == 44100), Is.True);
            Assert.That(MeteorVfx.ImpactDurationSeconds, Is.GreaterThan(impact.length)); Assert.That(MeteorVfx.DurationSeconds, Is.EqualTo(1.8f).Within(.0001f));
        }

        [TestCase(PlayerId.Player)] [TestCase(PlayerId.AI)]
        public void HeadLandsOnActualEnemyFieldCenterAndGiantImpactFitsTheViewport(PlayerId caster)
        {
            using (var fixture = new Fixture(false))
            {
                foreach (var size in new[] { new Vector2(2340, 1080), new Vector2(1280, 720), new Vector2(1080, 2340) })
                {
                    fixture.Layout(size); Assert.That(fixture.Play(caster), Is.EqualTo(MeteorVfx.DurationSeconds));
                    var initial = fixture.FlightHeadWorld;
                    fixture.SampleFlight(.3f); var moving = fixture.FlightHeadWorld;
                    Assert.That(Vector3.Distance(moving, fixture.FieldCenter(caster)), Is.LessThan(Vector3.Distance(initial, fixture.FieldCenter(caster))));
                    fixture.SampleFlight(.66f); fixture.AssertGeometry(caster);
                    Assert.That(fixture.Flight.gameObject.activeInHierarchy, Is.False); Assert.That(fixture.Impact.gameObject.activeInHierarchy, Is.True);
                    Assert.That(fixture.Impact.GetComponentInParent<Canvas>().sortingOrder, Is.EqualTo(4908));
                    Assert.That(fixture.Impact.raycastTarget || fixture.Flight.raycastTarget, Is.False);
                    Assert.That(fixture.Impact.GetComponentInParent<CanvasGroup>().blocksRaycasts, Is.False);
                    fixture.Cancel();
                }
            }
        }

        [Test]
        public void RepeatedImpactAndFrameHitchKeepOneImpactOriginAndACompleteTail()
        {
            using (var fixture = new Fixture(false))
            {
                fixture.Play(PlayerId.Player); fixture.SampleFlight(0); var firstPosition = fixture.FlightHeadWorld;
                Assert.That(fixture.BeginImpact(), Is.False); Assert.That(fixture.FlightHeadWorld, Is.EqualTo(firstPosition));
                fixture.SampleFlight(1f); Assert.That(fixture.Impact.sprite.name, Is.EqualTo("Impact_0").Or.EqualTo("Impact_00"));
                Assert.That(fixture.Remaining, Is.GreaterThanOrEqualTo(MeteorVfx.ImpactDurationSeconds - .01f), "A delayed impact begins with a full sound and visible tail.");
                var impactStartedAt = Get(fixture.Effect, "_impactStartedAt"); var endsAt = Get(fixture.Effect, "_endsAt");
                Assert.That(fixture.BeginImpact(), Is.True); Assert.That(Get(fixture.Effect, "_impactStartedAt"), Is.EqualTo(impactStartedAt)); Assert.That(Get(fixture.Effect, "_endsAt"), Is.EqualTo(endsAt));
                fixture.SampleImpact(.51f); Assert.That(fixture.Impact.sprite.name, Is.EqualTo("Impact_8").Or.EqualTo("Impact_08"));
                fixture.SampleImpact(1.02f); Assert.That(fixture.Impact.color.a, Is.InRange(.1f, .8f));
                fixture.SampleImpact(1.16f); fixture.AssertHiddenAndSilent(); Assert.That(fixture.Remaining, Is.Zero);
            }
        }

        [Test]
        public void InvalidAndDisabledLayoutsDoNotExposeAnOverlayOrStartSound()
        {
            using (var fixture = new Fixture(false))
            {
                fixture.Layout(Vector2.zero); Assert.That(fixture.Play(PlayerId.Player), Is.Zero); fixture.AssertHiddenAndSilent();
                fixture.Layout(new Vector2(2340, 1080)); fixture.Play(PlayerId.Player);
                fixture.Board.AITileViews[0].gameObject.SetActive(false); fixture.SampleFlight(.2f); fixture.AssertHiddenAndSilent();
                Assert.That(fixture.BeginImpact(), Is.False);
                fixture.Board.AITileViews[0].gameObject.SetActive(true); fixture.Play(PlayerId.AI); fixture.Effect.enabled = false;
                // EditMode has no automatic MonoBehaviour lifecycle for this runtime-only component.
                typeof(MeteorVfx).GetMethod("OnDisable", Hidden).Invoke(fixture.Effect, null);
                fixture.AssertHiddenAndSilent(); Assert.That(fixture.Remaining, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator ReuseCancellationAndComponentOnlyDestroyReleaseTheOwnedAudioAndOverlay()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                var cameraPosition = fixture.Camera.transform.position; var canvasPosition = fixture.CanvasRect.position;
                fixture.Play(PlayerId.Player); Assert.That(fixture.Effect.GetComponent<AudioSource>().isPlaying, Is.True);
                fixture.Effect.enabled = false; fixture.AssertHiddenAndSilent(); fixture.Effect.enabled = true; fixture.Play(PlayerId.Player);
                fixture.SampleFlight(.66f); Assert.That(fixture.Effect.GetComponents<AudioSource>().Length, Is.EqualTo(1));
                fixture.Cancel(); fixture.AssertHiddenAndSilent(); fixture.Play(PlayerId.AI);
                Assert.That(fixture.Effect.GetComponents<AudioSource>().Length, Is.EqualTo(1)); fixture.SampleFlight(.66f);
                yield return new WaitForSecondsRealtime(MeteorVfx.ImpactDurationSeconds + .05f); fixture.AssertHiddenAndSilent();
                Assert.That(fixture.Camera.transform.position, Is.EqualTo(cameraPosition)); Assert.That(fixture.CanvasRect.position, Is.EqualTo(canvasPosition));
                fixture.Play(PlayerId.Player); var host = fixture.Effect.gameObject; Object.Destroy(fixture.Effect); yield return null; yield return null;
                Assert.That(host.GetComponents<AudioSource>(), Is.Empty); Assert.That(fixture.Root.GetComponentsInChildren<Image>(true).Any(i => i.name == "MeteorFlight" || i.name == "MeteorImpact"), Is.False);
            }
            yield return new ExitPlayMode();
        }

        [TestCase(PlayerId.Player, PlayerId.Player)] [TestCase(PlayerId.Player, PlayerId.AI)]
        [TestCase(PlayerId.AI, PlayerId.Player)] [TestCase(PlayerId.AI, PlayerId.AI)]
        public void RealViewerProjectionUsesOppositeCasterAndStateOnlyDoesNotReplay(PlayerId caster, PlayerId localSeat)
        {
            using (var fixture = new Fixture(false))
            {
                var host = new GameObject("DormantProjection"); host.SetActive(false); host.transform.SetParent(fixture.Root.transform);
                var connection = host.AddComponent<OnlineBattleConnectionTester>(); Set(connection, "_localPlayerId", localSeat);
                var cast = new BattleEventDto { EventType = BattleEventType.SpellCast, CardId = MeteorRules.CardId, SourceCardId = MeteorRules.CardId, SourceOwnerId = caster, TargetOwnerId = caster };
                var projected = (System.Collections.Generic.IReadOnlyList<BattleEventDto>)typeof(OnlineBattleConnectionTester).GetMethod("ProjectBattleEventsToLocalPerspective", Hidden).Invoke(connection, new object[] { new[] { cast } });
                var request = fixture.OnlineRequest(projected[0], projected); var owner = caster == localSeat ? PlayerId.Player : PlayerId.AI;
                Assert.That(Property(request, "SpellCasterOwnerId"), Is.EqualTo(owner)); Assert.That(Property(request, "SpellEffectId"), Is.EqualTo(MeteorRules.EffectId));
                Assert.That(fixture.Estimate(request), Is.GreaterThanOrEqualTo(MeteorVfx.DurationSeconds));
                fixture.Play(owner); fixture.SampleFlight(.66f); fixture.AssertGeometry(owner);
                fixture.Cancel(); var restored = fixture.OnlineRequest(null, Array.Empty<BattleEventDto>());
                Assert.That(Property(restored, "SpellEffectId"), Is.Null); Assert.That(fixture.Estimate(restored), Is.Zero); fixture.AssertHiddenAndSilent();
            }
        }

        [UnityTest]
        public IEnumerator ActualGatewayTenVictimsHaveNoFlightFeedbackAndAllReactOnTheImpactBoundary()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                fixture.MakeTenFeedbackTargets(); yield return null;
                var hpBefore = fixture.FeedbackViews.ToDictionary(tile => tile.CurrentOccupantRuntimeId, tile => tile.CurrentOccupant.CurrentHp);
                fixture.State.Player.Hand.Add(MeteorRules.CardId); fixture.State.Player.Resources.Add(new ResourceSet(7, 0, 0, 0));
                var before = Capture(fixture.State); fixture.Gateway.ExecuteCommand(PlayerId.Player, new CastScriptedSpellCommand(MeteorRules.CardId));
                Assert.That(fixture.State.AI.Master.CurrentHp, Is.EqualTo(293)); Assert.That(fixture.State.Player.Master.CurrentHp, Is.EqualTo(333));
                var request = Build(before, Capture(fixture.State), fixture.State.ValuePopupEvents.ToArray(), MeteorRules.CardId, PlayerId.Player);
                var sequence = (IEnumerator)typeof(BattleBootstrapper).GetMethod("PlayImpactAnimationSequence", Hidden).Invoke(fixture.Bootstrapper, new[] { request });
                Assert.That(sequence.MoveNext(), Is.True); var spell = (IEnumerator)sequence.Current;
                Assert.That(spell.MoveNext(), Is.True); Assert.That(spell.Current, Is.TypeOf<WaitForSecondsRealtime>());
                Assert.That(fixture.PopupCount, Is.Zero); Assert.That(fixture.FeedbackViews.Where(v => v.CurrentOccupantCardId == "A-301").Any(v => IsHurt(v.GetComponent<TileTextView>())), Is.False);
                fixture.SampleFlight(.4f); Assert.That(fixture.PopupCount, Is.Zero); Assert.That(fixture.Flight.gameObject.activeInHierarchy, Is.True);
                foreach (var tile in fixture.FeedbackViews) { tile.GetComponent<TileTextView>().Refresh(); AssertDisplayedHp(tile, hpBefore[tile.CurrentOccupantRuntimeId]); }
                fixture.SampleFlight(.66f); Assert.That(spell.MoveNext(), Is.False, "The flight-only spell stage ends exactly at impact.");
                Assert.That(sequence.MoveNext(), Is.True); var impactFrame = Time.frameCount;
                Assert.That((int)typeof(TileTextView).GetField("_lastCommonHitFrame", Static).GetValue(null), Is.EqualTo(impactFrame));
                foreach (var tile in fixture.FeedbackViews.Where(v => v.CurrentOccupantCardId == "A-301")) Assert.That(IsHurt(tile.GetComponent<TileTextView>()), Is.True, "Every living unit reacts in the same impact invocation.");
                foreach (var tile in fixture.FeedbackViews) AssertDisplayedHp(tile, tile.CurrentOccupant.CurrentHp);
                yield return null; Assert.That(fixture.PopupCount, Is.EqualTo(10), "All unique victims, including Master, display damage without a per-tile cascade.");
                Assert.That(fixture.Impact.gameObject.activeInHierarchy, Is.True); Assert.That(fixture.Remaining, Is.GreaterThan(.7f));
                fixture.Bootstrapper.CancelCombatPresentation(); fixture.AssertHiddenAndSilent();
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CanceledFlightCannotImpactOrReactAfterTheEffectIsReused()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                fixture.MakeTenFeedbackTargets(); yield return null;
                fixture.State.Player.Hand.Add(MeteorRules.CardId); fixture.State.Player.Resources.Add(new ResourceSet(7, 0, 0, 0));
                var before = Capture(fixture.State); fixture.Gateway.ExecuteCommand(PlayerId.Player, new CastScriptedSpellCommand(MeteorRules.CardId));
                var request = Build(before, Capture(fixture.State), fixture.State.ValuePopupEvents.ToArray(), MeteorRules.CardId, PlayerId.Player);
                var sequence = (IEnumerator)typeof(BattleBootstrapper).GetMethod("PlayImpactAnimationSequence", Hidden).Invoke(fixture.Bootstrapper, new[] { request });
                Assert.That(sequence.MoveNext(), Is.True); var spell = (IEnumerator)sequence.Current; Assert.That(spell.MoveNext(), Is.True);
                fixture.Bootstrapper.CancelCombatPresentation(); fixture.AssertHiddenAndSilent();
                foreach (var tile in fixture.FeedbackViews) AssertDisplayedHp(tile, tile.CurrentOccupant.CurrentHp);
                fixture.Play(PlayerId.AI); fixture.SampleFlight(.2f);
                Assert.That(spell.MoveNext(), Is.False); Assert.That(sequence.MoveNext(), Is.False);
                Assert.That(fixture.Flight.gameObject.activeInHierarchy, Is.True); Assert.That(fixture.Impact.gameObject.activeInHierarchy, Is.False);
                yield return null; Assert.That(fixture.PopupCount, Is.Zero); Assert.That(fixture.FeedbackViews.Any(v => IsHurt(v.GetComponent<TileTextView>())), Is.False);
                Assert.That(fixture.State.AI.Master.CurrentHp, Is.EqualTo(293), "Presentation cancellation never rolls back or repeats gateway damage."); fixture.Cancel();
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RebindingDuringFlightDoesNotGiveTheReplacementOldDamageFeedback()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                fixture.MakeTenFeedbackTargets(); yield return null;
                fixture.State.Player.Hand.Add(MeteorRules.CardId); fixture.State.Player.Resources.Add(new ResourceSet(7, 0, 0, 0));
                var before = Capture(fixture.State); fixture.Gateway.ExecuteCommand(PlayerId.Player, new CastScriptedSpellCommand(MeteorRules.CardId));
                var request = Build(before, Capture(fixture.State), fixture.State.ValuePopupEvents.ToArray(), MeteorRules.CardId, PlayerId.Player);
                var sequence = (IEnumerator)typeof(BattleBootstrapper).GetMethod("PlayImpactAnimationSequence", Hidden).Invoke(fixture.Bootstrapper, new[] { request });
                Assert.That(sequence.MoveNext(), Is.True); var spell = (IEnumerator)sequence.Current; Assert.That(spell.MoveNext(), Is.True);
                var replacementTile = fixture.FeedbackViews.First(v => v.CurrentOccupantCardId == "A-301");
                var replacement = new UnitState("meteor-replacement", "A-301", PlayerId.AI, replacementTile.Coord, AttackType.Ranged, 1, 150, true, false, 0);
                replacementTile.Present(replacement); var view = replacementTile.GetComponent<TileTextView>(); view.SetTile(replacementTile.Title, replacementTile.Content, true); view.PlayIdleAnimation();
                fixture.SampleFlight(.66f); Assert.That(spell.MoveNext(), Is.False); Assert.That(sequence.MoveNext(), Is.True);
                Assert.That(IsHurt(view), Is.False); Assert.That(replacement.CurrentHp, Is.EqualTo(150)); AssertDisplayedHp(replacementTile, 150);
                foreach (var tile in fixture.FeedbackViews.Where(v => v != replacementTile && v.CurrentOccupantCardId == "A-301")) Assert.That(IsHurt(tile.GetComponent<TileTextView>()), Is.True);
                yield return null; Assert.That(fixture.PopupCount, Is.EqualTo(9), "Only the nine matching original runtime IDs show damage, including Master."); fixture.Bootstrapper.CancelCombatPresentation();
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SuccessfulEmptyEnemyDropRejectsOwnTilesBusyAndMissingHandWithoutRepeatingDamage()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                fixture.State.Player.Hand.Add(MeteorRules.CardId); fixture.State.Player.Resources.Add(new ResourceSet(7, 0, 0, 0));
                Assert.That(fixture.Bootstrapper.TryUsePlayerCardOnTile(MeteorRules.CardId, PlayerId.Player, new TileCoord(4, 1), out _), Is.False); fixture.AssertHiddenAndSilent();
                Assert.That(fixture.Bootstrapper.TryUsePlayerCardOnTile(MeteorRules.CardId, PlayerId.AI, new TileCoord(4, 1), out _), Is.True);
                Assert.That(fixture.Flight.gameObject.activeInHierarchy, Is.True); Assert.That(fixture.State.AI.Master.CurrentHp, Is.EqualTo(293));
                Assert.That(fixture.Bootstrapper.TryCastPlayerPersistentSpell(MeteorRules.CardId, out _), Is.False); Assert.That(fixture.State.AI.Master.CurrentHp, Is.EqualTo(293));
                fixture.Bootstrapper.CancelCombatPresentation(); fixture.AssertHiddenAndSilent();
                var timeout = Time.unscaledTime + MeteorVfx.DurationSeconds + 1f; while (fixture.Bootstrapper.IsBusy && Time.unscaledTime < timeout) yield return null;
                Assert.That(fixture.Bootstrapper.IsBusy, Is.False); LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Cast persistent spell interaction failed.*"));
                Assert.That(fixture.Bootstrapper.TryCastPlayerPersistentSpell(MeteorRules.CardId, out _), Is.False); fixture.AssertHiddenAndSilent(); Assert.That(fixture.State.AI.Master.CurrentHp, Is.EqualTo(293));
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ActualCameraPixelsShowMovingRockThenGiantImpactOnBothEnemyFields()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            foreach (var caster in new[] { PlayerId.Player, PlayerId.AI })
            {
                fixture.Cancel(); var baseline = fixture.Pixels(); fixture.Play(caster); fixture.SampleFlight(.51f);
                var flight = fixture.Pixels("Meteor-Flight-" + caster);
                fixture.SampleFlight(.66f); fixture.SampleImpact(.3f); var impact = fixture.Pixels("Meteor-Impact-" + caster);
                try
                {
                    Assert.That(fixture.DifferentPixels(flight, baseline), Is.GreaterThan(2000), "A giant burning rock must actually render.");
                    Assert.That(fixture.DifferentPixels(impact, baseline), Is.GreaterThan(5000), "Explosion pixels must cover a readable area.");
                    fixture.AssertGeometry(caster);
                }
                finally { Object.DestroyImmediate(baseline); Object.DestroyImmediate(flight); Object.DestroyImmediate(impact); }
            }
            yield return new ExitPlayMode();
        }

        private static object Get(object instance, string name) => instance.GetType().GetField(name, Hidden).GetValue(instance);
        private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Hidden).SetValue(instance, value);
        private static object Property(object instance, string name) => instance.GetType().GetProperty(name).GetValue(instance);
        private static object Capture(BattleState state) => typeof(BattleBootstrapper).GetMethod("CaptureBattleSnapshot", Static).Invoke(null, new object[] { state });
        private static object Build(object before, object after, System.Collections.Generic.IReadOnlyList<BattleValuePopupEvent> events, string card, PlayerId caster) => typeof(BattleBootstrapper).GetMethod("BuildImpactAnimationRequest", Static).Invoke(null, new object[] { before, after, events, card, caster });
        private static void AssertDisplayedHp(TileView tile, int hp)
        {
            var text = (TMP_Text)Get(tile.GetComponent<TileTextView>(), "_occupantStatHpText");
            Assert.That(text, Is.Not.Null); Assert.That(text.text, Is.EqualTo(hp.ToString()), "Only the presentation HP label changes at the impact boundary.");
        }
        private static bool IsHurt(TileTextView view)
        { var animator = (Animator)Get(view, "_occupantVisualAnimator"); return animator != null && animator.runtimeAnimatorController != null && animator.GetCurrentAnimatorStateInfo(0).IsName("BeAttacked"); }

        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root = new GameObject("MeteorPresentationFixture");
            internal readonly MeteorVfx Effect;
            internal readonly BattleBootstrapper Bootstrapper;
            internal readonly BoardPresenter Board;
            internal readonly LocalBattleGateway Gateway;
            internal readonly Camera Camera;
            internal readonly RectTransform CanvasRect;
            internal BattleState State => Gateway.CurrentBattleState;
            internal Image Flight => Root.GetComponentsInChildren<Image>(true).SingleOrDefault(i => i.name == "MeteorFlight");
            internal Image Impact => Root.GetComponentsInChildren<Image>(true).SingleOrDefault(i => i.name == "MeteorImpact");
            internal TileView[] FeedbackViews => Board.AITileViews;
            internal int PopupCount => Root.GetComponentsInChildren<TMP_Text>(true).Count(t => t.name == "ValuePopupText" && t.gameObject.activeInHierarchy);
            internal float Remaining => (float)typeof(MeteorVfx).GetProperty("RemainingDurationSeconds", Hidden).GetValue(Effect);
            internal Vector3 FlightHeadWorld => Flight.rectTransform.TransformPoint(new Vector3(Flight.rectTransform.rect.xMin + Flight.rectTransform.rect.width * .65f, Flight.rectTransform.rect.yMin + Flight.rectTransform.rect.height * .359375f, 0));
            private readonly RenderTexture _render;
            private readonly string _oldToken;

            internal Fixture(bool active = true)
            {
                var cameraHost = new GameObject("PreviewCamera", typeof(Camera)); cameraHost.transform.SetParent(Root.transform);
                Camera = cameraHost.GetComponent<Camera>(); Camera.transform.position = new Vector3(0, 0, -10); Camera.orthographic = true; Camera.orthographicSize = 5.4f;
                Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(.035f, .045f, .06f);
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) { _render = new RenderTexture(2340, 1080, 24); _render.Create(); Camera.targetTexture = _render; }
                var canvas = new GameObject("BattleCanvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>(); canvas.transform.SetParent(Root.transform, false);
                canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera; CanvasRect = (RectTransform)canvas.transform; CanvasRect.localScale = Vector3.one * .01f;
                var boardHost = new GameObject("Board"); boardHost.SetActive(false); boardHost.transform.SetParent(Root.transform);
                Board = boardHost.AddComponent<BoardPresenter>(); Set(Board, "_useResponsiveLayout", false); boardHost.SetActive(true);
                Set(Board, "_playerTileViews", MakeTiles(PlayerId.Player)); Set(Board, "_aiTileViews", MakeTiles(PlayerId.AI));
                Effect = new GameObject("MeteorEffectHost").AddComponent<MeteorVfx>(); Effect.transform.SetParent(Root.transform);
                var host = new GameObject("Coordinator"); host.SetActive(false); host.transform.SetParent(Root.transform);
                Bootstrapper = host.AddComponent<BattleBootstrapper>(); Set(Bootstrapper, "_cardCatalogAsset", AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>("Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset")); Set(Bootstrapper, "_startOnAwake", false); Set(Bootstrapper, "_autoRunAiTurns", false); Set(Bootstrapper, "_returnToDraftSceneWhenRunSessionActive", false);
                var screenHost = new GameObject("DormantScreen"); screenHost.SetActive(false); screenHost.transform.SetParent(Root.transform); var screen = screenHost.AddComponent<BattleScreenPresenter>(); Set(screen, "_boardPresenter", Board);
                Set(Bootstrapper, "_battleScreenPresenter", screen); Set(Bootstrapper, "_meteorVfx", Effect);
                var provider = JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).CreateProvider();
                var flow = new BattleFlowController(new BattleSetupService(), new MulliganService(), new TurnStartService(), new BattleCommandProcessor(new PlayCardService(provider), new SpellService(provider), new MoveService(), new AttackService(new TargetingService()), new EndTurnService()));
                Gateway = new LocalBattleGateway(flow); var deck = Enumerable.Repeat("Goblin", 33).ToArray(); Gateway.StartBattle(new BattleSetupRequest(deck, deck, PlayerId.Player)); State.SetPhase(PhaseType.Main);
                Set(Bootstrapper, "_battleGateway", Gateway); Set(Bootstrapper, "_cardDefinitionProvider", provider); _oldToken = AccountSessionState.SessionToken;
                if (active) { typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null, "synthetic-meteor-presentation-session"); host.SetActive(true); }
                Layout(new Vector2(2340, 1080));
            }
            private TileView[] MakeTiles(PlayerId owner)
            {
                var tiles = new TileView[10];
                for (var index = 0; index < tiles.Length; index++)
                {
                    var tile = new GameObject(owner + "Tile" + index, typeof(RectTransform), typeof(Image), typeof(TileView)).GetComponent<TileView>(); tile.transform.SetParent(CanvasRect, false); tile.Configure(owner, index / 2, index % 2);
                    tile.GetComponent<Image>().color = new Color(.11f, .17f, .22f); tiles[index] = tile;
                }
                return tiles;
            }
            internal void Layout(Vector2 size)
            {
                CanvasRect.sizeDelta = size; var layout = BoardResponsiveLayoutCalculator.Calculate((int)size.x, (int)size.y, new Rect(0, 0, size.x, size.y), .9f, 24f, 48f, new Vector2(8, 8), 1f, 170f);
                foreach (var tile in Board.PlayerTileViews.Concat(Board.AITileViews)) { var rect = (RectTransform)tile.transform; rect.sizeDelta = layout.TileSize; rect.anchoredPosition = layout.GetTileCenter(tile.OwnerId, tile.Coord); }
                Canvas.ForceUpdateCanvases();
            }
            internal void MakeTenFeedbackTargets()
            {
                foreach (var tile in Board.AITileViews)
                {
                    var occupant = State.AIBoard.GetOccupant(tile.Coord);
                    if (occupant == null) { occupant = new UnitState("meteor-target-" + tile.Coord, "A-301", PlayerId.AI, tile.Coord, AttackType.Ranged, 1, 150, true, false, 0); State.AIBoard.Place(tile.Coord, occupant); }
                    tile.gameObject.SetActive(false); var label = new GameObject("TileLabel", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>(); label.transform.SetParent(tile.transform, false);
                    label.rectTransform.sizeDelta = ((RectTransform)tile.transform).sizeDelta; var view = tile.gameObject.AddComponent<TileTextView>(); Set(view, "_tileView", tile); Set(view, "_text", label); Set(view, "_battleBootstrapper", Bootstrapper);
                    tile.Present(occupant); tile.gameObject.SetActive(true); view.SetTile(tile.Title, tile.Content, tile.IsOccupied); view.PlayIdleAnimation(); typeof(TileTextView).GetMethod("LateUpdate", Hidden).Invoke(view, null);
                }
            }
            internal Vector3 FieldCenter(PlayerId caster)
            { var tiles = caster == PlayerId.Player ? Board.AITileViews : Board.PlayerTileViews; return tiles.Aggregate(Vector3.zero, (sum, tile) => sum + tile.transform.position) / tiles.Length; }
            internal float Play(PlayerId caster) => (float)typeof(MeteorVfx).GetMethod("Play", Hidden).Invoke(Effect, new object[] { Board, caster });
            internal bool BeginImpact() => (bool)typeof(MeteorVfx).GetMethod("TryBeginImpact", Hidden).Invoke(Effect, null);
            internal void Cancel() { if (Effect != null) typeof(MeteorVfx).GetMethod("Cancel", Hidden).Invoke(Effect, null); }
            internal void SampleFlight(float age)
            { Set(Effect, "_startedAt", Time.unscaledTime - age); Set(Effect, "_endsAt", Time.unscaledTime - age + MeteorVfx.DurationSeconds); typeof(MeteorVfx).GetMethod("LateUpdate", Hidden).Invoke(Effect, null); Canvas.ForceUpdateCanvases(); }
            internal void SampleImpact(float age)
            { Set(Effect, "_impactStartedAt", Time.unscaledTime - age); Set(Effect, "_endsAt", Time.unscaledTime - age + MeteorVfx.ImpactDurationSeconds); typeof(MeteorVfx).GetMethod("LateUpdate", Hidden).Invoke(Effect, null); Canvas.ForceUpdateCanvases(); }
            internal object OnlineRequest(BattleEventDto source, System.Collections.Generic.IReadOnlyList<BattleEventDto> events) => typeof(BattleBootstrapper).GetMethod("BuildOnlineImpactAnimationRequest", Hidden).Invoke(Bootstrapper, new object[] { source?.CardId, source, events });
            internal float Estimate(object request) => (float)typeof(BattleBootstrapper).GetMethod("EstimateImpactAnimationDuration", Hidden).Invoke(Bootstrapper, new[] { request });
            internal void AssertGeometry(PlayerId caster)
            {
                Assert.That(Vector3.Distance(FlightHeadWorld, FieldCenter(caster)), Is.LessThan(.002f));
                var origin = (Vector2)typeof(MeteorVfx).GetField("ImpactOriginNormalizedBottomLeft", Static).GetValue(null);
                var rect = Impact.rectTransform.rect; var point = Impact.rectTransform.TransformPoint(new Vector3(rect.xMin + rect.width * origin.x, rect.yMin + rect.height * origin.y, 0));
                Assert.That(Vector3.Distance(point, FieldCenter(caster)), Is.LessThan(.002f));
                var tileSize = ((RectTransform)(caster == PlayerId.Player ? Board.AITileViews[0] : Board.PlayerTileViews[0]).transform).rect.width;
                Assert.That(Impact.rectTransform.rect.width, Is.GreaterThan(tileSize * 1.75f));
                var corners = new Vector3[4]; Impact.rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners) { var p = CanvasRect.InverseTransformPoint(corner); Assert.That(p.x, Is.InRange(CanvasRect.rect.xMin - .01f, CanvasRect.rect.xMax + .01f)); Assert.That(p.y, Is.InRange(CanvasRect.rect.yMin - .01f, CanvasRect.rect.yMax + .01f)); }
            }
            internal void AssertHiddenAndSilent()
            { Assert.That(Flight == null || !Flight.gameObject.activeInHierarchy, Is.True); Assert.That(Impact == null || !Impact.gameObject.activeInHierarchy, Is.True); Assert.That(Effect == null || !Effect.GetComponents<AudioSource>().Any(s => s.isPlaying), Is.True); }
            internal Texture2D Pixels(string evidenceName = null)
            {
                Assert.That(_render, Is.Not.Null); Canvas.ForceUpdateCanvases(); Camera.Render(); var old = RenderTexture.active; var texture = new Texture2D(_render.width, _render.height, TextureFormat.RGB24, false);
                try { RenderTexture.active = _render; texture.ReadPixels(new Rect(0, 0, _render.width, _render.height), 0, 0); texture.Apply(); if (evidenceName != null) { var path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Tools/Art/Preview/" + evidenceName + ".png")); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, texture.EncodeToPNG()); } return texture; }
                finally { RenderTexture.active = old; }
            }
            internal int DifferentPixels(Texture2D image, Texture2D baseline)
            { var current = image.GetPixels32(); var previous = baseline.GetPixels32(); return current.Where((pixel, index) => !pixel.Equals(previous[index])).Count(); }
            public void Dispose() { typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null, _oldToken); Object.DestroyImmediate(Root); if (_render != null) { _render.Release(); Object.DestroyImmediate(_render); } }
        }
    }
}
