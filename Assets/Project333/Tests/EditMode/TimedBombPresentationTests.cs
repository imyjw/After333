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
using Project333.Runtime.Domain.Effects;
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
    public sealed class TimedBombPresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        [UnityTearDown] public IEnumerator LeavePlayMode() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void AssetsPreserveBodyAndSixteenFullExplosionCellsWithQuietShortSound()
        {
            var body = Resources.Load<Sprite>(TimedBombPresentationVfx.BodyResourcePath);
            Assert.That(body, Is.Not.Null); Assert.That(body.rect.size, Is.EqualTo(new Vector2(512,512)));
            var frames=Resources.LoadAll<Sprite>(TimedBombPresentationVfx.ExplosionResourcePath).OrderBy(s=>s.name).ToArray();
            Assert.That(frames.Length,Is.EqualTo(16));
            for(var i=0;i<frames.Length;i++)
                Assert.That(frames[i].rect,Is.EqualTo(new Rect(i%4*512,(3-i/4)*512,512,512)));
            foreach(var sprite in new[]{body,frames[0]})
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(importer.mipmapEnabled,Is.False);
                Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));
            }
            var sound=Resources.Load<AudioClip>(TimedBombPresentationVfx.SoundResourcePath);
            Assert.That(sound,Is.Not.Null);Assert.That(sound.channels,Is.EqualTo(1));Assert.That(sound.length,Is.InRange(.8f,1.1f));
            var samples=new float[sound.samples];Assert.That(sound.GetData(samples,0),Is.True);
            Assert.That(samples.Max(v=>Mathf.Abs(v)),Is.LessThan(.25f));
        }

        [TestCase(PlayerId.Player)] [TestCase(PlayerId.AI)]
        public void LocalRealTurnStartsAndOnlineCuesMatch_AllThreeTransitionsAndRestoredMainDoesNotReplay(PlayerId caster)
        {
            using(var fixture=new Fixture(false))
            {
                fixture.State.RestoreRuntimeState(1,caster,PhaseType.Main);
                fixture.State.GetPlayer(caster).Hand.Add("TimedBomb");
                fixture.State.GetPlayer(caster).Resources.Add(new ResourceSet(0,0,3,0));
                fixture.Gateway.ExecuteCommand(caster,new CastScriptedSpellCommand("TimedBomb"));
                Assert.That(fixture.State.PersistentEffects.Single().RemainingTriggers,Is.EqualTo(3));
                for(var count=3;count>0;count--)
                {
                    fixture.State.ClearValuePopupEvents();
                    fixture.State.RestoreRuntimeState(5-count,count%2==0?caster:fixture.State.GetOpponent(caster).Id,PhaseType.TurnStart);
                    var before=Capture(fixture.State);
                    new TurnStartService().ResolveTurnStart(fixture.State);
                    var local=Build(before,Capture(fixture.State),Clone(fixture.State));
                    var steps=(IList)Property(local,"TimedBombSteps");Assert.That(steps.Count,Is.EqualTo(1));
                    var online=fixture.OnlineRequest(Cue(caster,0,count,count-1),new[]{Cue(caster,0,count,count-1)});
                    foreach(var request in new[]{steps[0],online})
                    {
                        var cue=Property(request,"TimedBombCountdown");
                        Assert.That(Property(cue,"Caster"),Is.EqualTo(caster));Assert.That(Property(cue,"Before"),Is.EqualTo(count));
                        Assert.That(Property(cue,"After"),Is.EqualTo(count-1));
                        Assert.That(fixture.Estimate(request),Is.GreaterThanOrEqualTo(count==1?1.8f:.8f));
                    }
                    var restored=Build(Capture(fixture.State),Capture(fixture.State),Array.Empty<BattleValuePopupEvent>());
                    Assert.That(((IList)Property(restored,"TimedBombSteps")).Count,Is.Zero,"Main-phase/view restoration cannot infer old countdown cues.");
                }
                Assert.That(fixture.State.GetOpponent(caster).Master.CurrentHp,Is.EqualTo(300));
                Assert.That(fixture.State.GetPlayer(caster).Master.CurrentHp,Is.EqualTo(333));
            }
        }

        [TestCase(PlayerId.Player,PlayerId.Player)] [TestCase(PlayerId.Player,PlayerId.AI)]
        [TestCase(PlayerId.AI,PlayerId.Player)] [TestCase(PlayerId.AI,PlayerId.AI)]
        public void ActualOnlineSeatProjectionPreservesCountdownAndOpposingTarget(PlayerId localSeat,PlayerId remoteCaster)
        {
            using(var fixture=new Fixture(false))
            {
                var connectionHost=new GameObject("DormantOnlineProjection");connectionHost.SetActive(false);
                connectionHost.transform.SetParent(fixture.Root.transform);
                var connection=connectionHost.AddComponent<OnlineBattleConnectionTester>();Set(connection,"_localPlayerId",localSeat);
                var method=typeof(OnlineBattleConnectionTester).GetMethod("ProjectBattleEventsToLocalPerspective",Hidden);
                for(var count=3;count>0;count--)
                {
                    var projected=(List<BattleEventDto>)method.Invoke(connection,new object[]{new[]{Cue(remoteCaster,7,count,count-1)}});
                    var cue=projected.Single();var caster=localSeat==remoteCaster?PlayerId.Player:PlayerId.AI;
                    Assert.That(cue.EffectIndex,Is.EqualTo(7));Assert.That(cue.RemainingTriggersBefore,Is.EqualTo(count));
                    Assert.That(cue.RemainingTriggersAfter,Is.EqualTo(count-1));Assert.That(cue.SourceOwnerId,Is.EqualTo(caster));
                    Assert.That(cue.TargetOwnerId,Is.EqualTo(caster==PlayerId.Player?PlayerId.AI:PlayerId.Player));
                    var request=fixture.OnlineRequest(cue,projected);
                    Assert.That(Property(Property(request,"TimedBombCountdown"),"Caster"),Is.EqualTo(caster));
                    Assert.That(fixture.Estimate(request),Is.GreaterThanOrEqualTo(count==1?1.8f:.8f));
                }
            }
        }

        [TestCase(3)] [TestCase(1)]
        public void MixedPersistentEffectsKeepFirewallAndBiochemicalDamageOutsideBombStep(int count)
        {
            using(var fixture=new Fixture(false))
            {
                var state=fixture.State;var target=state.AI.Master;
                state.PersistentEffects.Add(new PersistentEffectState("TimedBomb",PlayerId.Player,TimedBombRules.EffectId,1,"",new ResourceSet(),
                    remainingTriggers:count,effectDamage:33,effectDamageType:DamageType.Physical));
                state.PersistentEffects.Add(new PersistentEffectState("Firewall",PlayerId.Player,"firewall",1,"",new ResourceSet(),
                    targetRow:target.Position.Row,remainingTriggers:2,effectDamage:7,effectDamageType:DamageType.Magic));
                state.PersistentEffects.Add(new PersistentEffectState("BiochemicalBomb",PlayerId.Player,BiochemicalBombRules.EffectId,1,"",new ResourceSet(),
                    remainingTriggers:2,effectDamage:3,effectDamageType:DamageType.Magic,targetStartColumn:1));
                state.ClearValuePopupEvents();state.SetPhase(PhaseType.TurnStart);var before=Capture(state);
                new TurnStartService().ResolveTurnStart(state);
                var local=Build(before,Capture(state),Clone(state));
                var steps=(IList)Property(local,"TimedBombSteps");Assert.That(steps.Count,Is.EqualTo(1));
                var bombPopups=((IList)Property(steps[0],"ValuePopupEvents")).Cast<BattleValuePopupEvent>().ToArray();
                Assert.That(bombPopups.Length,Is.EqualTo(count==1?1:0));Assert.That(bombPopups.All(p=>p.SourceCardId=="TimedBomb"),Is.True);
                var areaSteps=(IList)Property(local,"AreaSpellSteps");Assert.That(areaSteps.Count,Is.EqualTo(2));
                Assert.That(Property(areaSteps[0],"SpellEffectId"),Is.EqualTo("firewall"));
                Assert.That(Property(areaSteps[1],"SpellEffectId"),Is.EqualTo(BiochemicalBombRules.EffectId));
                var later=areaSteps.Cast<object>().SelectMany(s=>((IList)Property(s,"ValuePopupEvents")).Cast<BattleValuePopupEvent>()).ToArray();
                Assert.That(later.Select(p=>p.SourceCardId),Is.EqualTo(new[]{"Firewall","BiochemicalBomb"}));
                Assert.That(((IList)Property(local,"ValuePopupEvents")).Count,Is.Zero);
                Assert.That(((IList)Property(local,"Impacts")).Count,Is.Zero);
                foreach(var step in areaSteps) Assert.That(((IList)Property(step,"Impacts")).Count,Is.EqualTo(1));
                var shown=bombPopups.Concat(later).ToArray();
                Assert.That(shown.Select(p=>p.SourceCardId),Is.EqualTo(state.ValuePopupEvents.Select(p=>p.SourceCardId)));
                Assert.That(shown.Sum(p=>p.Amount),Is.EqualTo(state.ValuePopupEvents.Sum(p=>p.Amount)),"Every actual packet is displayed once.");
                var wire=new List<BattleEventDto>{Cue(PlayerId.Player,0,count,count-1)};
                for(var index=0;index<=state.ValuePopupEvents.Count;index++)
                {
                    foreach(var area in state.AreaSpellEffectEvents.Where(e=>e.ValuePopupEventIndex==index))
                        wire.Add(new BattleEventDto{EventType=BattleEventType.AreaSpellEffectTriggered,EffectId=area.EffectId,
                            SourceCardId=area.SourceCardId,SourceOwnerId=area.SourceOwnerId,TargetOwnerId=area.TargetOwnerId,
                            TargetCoords=area.TargetCoords.Select(TileCoordDto.FromDomain).ToList()});
                    if(index<state.ValuePopupEvents.Count)wire.Add(Damage(state.ValuePopupEvents[index]));
                }
                var groups=Groups(wire);Assert.That(groups.Count,Is.EqualTo(3));
                Assert.That(groups[0].Count(e=>e.EventType==BattleEventType.DamageApplied),Is.EqualTo(count==1?1:0));
                for(var i=1;i<3;i++)
                    Assert.That(groups[i].Where(e=>e.EventType==BattleEventType.DamageApplied).Select(e=>e.SourceCardId),
                        Is.EqualTo(new[]{i==1?"Firewall":"BiochemicalBomb"}));
            }
        }

        [Test]
        public void ConfirmedNonMasterDeathStaysWithLethalBombBeforeLaterCountdownWithoutDuplicateDeath()
        {
            using(var fixture=new Fixture(false))
            {
                var position=new TileCoord(1,0);
                var damage=new BattleEventDto{EventType=BattleEventType.DamageApplied,RuntimeId="victim",TargetRuntimeId="victim",
                    SourceCardId="TimedBomb",SourceOwnerId=PlayerId.Player,TargetOwnerId=PlayerId.AI,TargetCoord=TileCoordDto.FromDomain(position),
                    Amount=33,HpBefore=10,HpAfter=-23,DamageType=DamageType.Physical,ValueCause=BattleValueChangeCause.Spell};
                var removal=new BattleEventDto{EventType=BattleEventType.OccupantRemoved,RuntimeId="victim",TargetRuntimeId="victim",
                    TargetOwnerId=PlayerId.AI,TargetCoord=TileCoordDto.FromDomain(position)};
                var groups=Groups(new[]{Cue(PlayerId.Player,0,1,0),damage,Cue(PlayerId.AI,1,3,2),removal});
                Assert.That(groups.Count,Is.EqualTo(2));Assert.That(groups[0].Contains(removal),Is.True);
                Assert.That(groups[1].Contains(removal),Is.False);
                var first=fixture.OnlineRequest(groups[0][0],groups[0]);
                Assert.That(Property(((IList)Property(first,"Impacts"))[0],"Died"),Is.True);
                var second=fixture.OnlineRequest(groups[1][0],groups[1]);Assert.That(((IList)Property(second,"Impacts")).Count,Is.Zero);
                damage.TargetRuntimeId="replacement";
                groups=Groups(new[]{Cue(PlayerId.Player,0,1,0),damage,Cue(PlayerId.AI,1,3,2),removal});
                Assert.That(groups[0].Contains(removal),Is.False,"A coordinate match cannot move another occupant's death.");
            }
        }

        [Test]
        public void MultipleLocalBombsKeepCreationOrderAndRawPopupBoundariesWithoutCachedOrDuplicatePopups()
        {
            using(var fixture=new Fixture(false))
            {
                fixture.State.SetPhase(PhaseType.TurnStart);
                var master=fixture.State.AI.Master;
                fixture.State.ValuePopupEvents.Add(new BattleValuePopupEvent(master.RuntimeId,PlayerId.AI,master.Position,false,5,
                    PlayerId.Player,null,"OldSpell",BattleValueChangeCause.Spell,DamageType.Physical,338,333));
                fixture.State.ValuePopupEvents.Add(new BattleValuePopupEvent(master.RuntimeId,PlayerId.AI,master.Position,false,0,
                    PlayerId.Player,null,"TimedBomb",BattleValueChangeCause.Spell,DamageType.Physical,333,333));
                var before=Capture(fixture.State);
                fixture.State.ValuePopupEvents.Add(new BattleValuePopupEvent(master.RuntimeId,PlayerId.AI,master.Position,false,3,
                    PlayerId.Player,"plant","NuclearPowerPlant",BattleValueChangeCause.NuclearPowerPlant,DamageType.Physical,333,330));
                fixture.State.RecordTimedBombCountdown(new BattleTimedBombCountdownEvent(0,"TimedBomb",PlayerId.Player,PlayerId.AI,1,0,3,4));
                fixture.State.RecordTimedBombCountdown(new BattleTimedBombCountdownEvent(1,"TimedBomb",PlayerId.AI,PlayerId.Player,2,1,4));
                fixture.State.ValuePopupEvents.Add(new BattleValuePopupEvent(master.RuntimeId,PlayerId.AI,master.Position,false,33,
                    PlayerId.Player,null,"TimedBomb",BattleValueChangeCause.Spell,DamageType.Physical,330,297));
                master.CurrentHp=297;fixture.State.SetPhase(PhaseType.Main);
                var local=Build(before,Capture(fixture.State),Clone(fixture.State));
                var steps=(IList)Property(local,"TimedBombSteps");Assert.That(steps.Count,Is.EqualTo(3));
                Assert.That(Property(steps[0],"TimedBombCountdown"),Is.Null,"This turn's earlier death-chain damage stays before the timer.");
                Assert.That(((IList)Property(steps[0],"ValuePopupEvents")).Cast<BattleValuePopupEvent>().Single().Cause,Is.EqualTo(BattleValueChangeCause.NuclearPowerPlant));
                Assert.That(Property(Property(steps[1],"TimedBombCountdown"),"EffectIndex"),Is.EqualTo(0));
                Assert.That(Property(Property(steps[2],"TimedBombCountdown"),"EffectIndex"),Is.EqualTo(1));
                Assert.That(((IList)Property(steps[0],"ValuePopupEvents")).Count,Is.EqualTo(1));
                Assert.That(((IList)Property(steps[1],"ValuePopupEvents")).Count,Is.EqualTo(1));
                Assert.That(((IList)Property(steps[2],"ValuePopupEvents")).Count,Is.Zero);
                Assert.That(((IList)Property(local,"ValuePopupEvents")).Count,Is.Zero,"Cloned old/zero entries and assigned bomb entries cannot replay in the parent.");
                Assert.That(((IList)Property(local,"Impacts")).Count,Is.Zero,"Each affected occupant reacts within its original bomb step.");
            }
        }

        [UnityTest]
        public IEnumerator ActualBattleSceneCountdownAndExplosionUseScreenCenterAndVisibleCanvas()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Battle_VSlice.unity", OpenSceneMode.Single);
            var boot = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BattleBootstrapper>(true)).Single();
            boot.enabled = false;
            Set(boot, "_startOnAwake", false); Set(boot, "_startWithDraftBeforeBattle", false); Set(boot, "_autoRunAiTurns", false);
            boot.gameObject.SetActive(false);
            foreach (var connection in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<OnlineBattleConnectionTester>(true)).ToArray())
                Object.DestroyImmediate(connection);
            var oldToken = AccountSessionState.SessionToken;
            typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null, "timed-bomb-real-scene-no-network");
            yield return new EnterPlayMode();
            TimedBombPresentationVfx effect = null;
            try
            {
                typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null, "timed-bomb-real-scene-no-network");
                boot = Object.FindObjectsByType<BattleBootstrapper>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
                var provider = JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).CreateProvider();
                var flow = new BattleFlowController(new BattleSetupService(), new MulliganService(), new TurnStartService(),
                    new BattleCommandProcessor(new PlayCardService(provider), new SpellService(provider), new MoveService(), new AttackService(new TargetingService()), new EndTurnService()));
                var gateway = new LocalBattleGateway(flow);
                var deck = Enumerable.Repeat("Goblin", 33).ToArray(); gateway.StartBattle(new BattleSetupRequest(deck, deck, PlayerId.Player));
                gateway.CurrentBattleState.SetPhase(PhaseType.Main);
                Set(boot, "_battleGateway", gateway); Set(boot, "_cardDefinitionProvider", provider); Set(boot, "_sendPlayerActionsToOnlineGateway", false);
                boot.gameObject.SetActive(true);
                var screen = Object.FindFirstObjectByType<BattleScreenPresenter>(); Set(boot, "_battleScreenPresenter", screen);
                screen.Bind(boot); typeof(BattleBootstrapper).GetMethod("RefreshPresenter", Hidden).Invoke(boot, null);
                var board = screen.BoardPresenter; Assert.That(board, Is.Not.Null);
                foreach (var tile in board.PlayerTileViews.Concat(board.AITileViews))
                {
                    Set(tile.GetComponent<TileTextView>(), "_battleBootstrapper", boot);
                    tile.Present(gateway.CurrentBattleState.GetBoard(tile.OwnerId).GetOccupant(tile.Coord));
                }
                board.ApplyResponsiveLayoutNow(); Canvas.ForceUpdateCanvases(); yield return null;
                effect = boot.gameObject.AddComponent<TimedBombPresentationVfx>();
                foreach (var caster in new[] { PlayerId.Player, PlayerId.AI })
                {
                    var duration = (float)typeof(TimedBombPresentationVfx).GetMethod("Play", Hidden).Invoke(effect, new object[] { board, caster, 1, 0 });
                    var diagnosticRoot = (RectTransform)typeof(TimedBombPresentationVfx).GetField("_root", Hidden).GetValue(effect);
                    var diagnosticTiles = caster == PlayerId.Player ? board.AITileViews : board.PlayerTileViews;
                    var diagnostic = "effect active=" + effect.isActiveAndEnabled + " root=" + (diagnosticRoot == null ? "null" : diagnosticRoot.rect + " parent=" + diagnosticRoot.parent.name + " rect=" + ((RectTransform)diagnosticRoot.parent).rect + " active=" + diagnosticRoot.parent.gameObject.activeInHierarchy) +
                        " anchors=" + string.Join(";", diagnosticTiles.Select(tile => {
                            var anchor = tile.GetComponent<TileTextView>().ActiveEffectButtonAnchor;
                            if (anchor == null) return tile.name + ":null";
                            var anchorCanvas = anchor.GetComponentInParent<Canvas>();
                            return tile.name + ":" + anchor.rect + " active=" + anchor.gameObject.activeInHierarchy + " world=" + anchor.position + " canvas=" + anchorCanvas?.name + " rect=" + (anchorCanvas?.transform as RectTransform)?.rect;
                        }));
                    Assert.That(duration, Is.EqualTo(1.8f), "Actual scene must start either caster's countdown. " + diagnostic);
                    var root = (RectTransform)typeof(TimedBombPresentationVfx).GetField("_root", Hidden).GetValue(effect);
                    Assert.That(root.rect.width, Is.GreaterThan(0)); Assert.That(root.rect.height, Is.GreaterThan(0));
                    var body = root.GetComponentsInChildren<Image>(true).Single(image => image.name == "TimedBombBody");
                    var canvas = root.GetComponent<Canvas>(); var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                    var center = RectTransformUtility.WorldToScreenPoint(camera, body.rectTransform.TransformPoint(body.rectTransform.rect.center));
                    Assert.That(Vector2.Distance(center, new Vector2(Screen.width, Screen.height) * .5f), Is.LessThan(2f),
                        "The timer belongs at full screen center despite separate tile canvases.");
                    Set(effect, "_startedAt", Time.unscaledTime - 1.05f); typeof(TimedBombPresentationVfx).GetMethod("LateUpdate", Hidden).Invoke(effect, null);
                    var bursts = root.GetComponentsInChildren<Image>(true).Where(image => image.name.StartsWith("TimedBombBurst", StringComparison.Ordinal)).ToArray();
                    Assert.That(bursts.Count(image => image.gameObject.activeInHierarchy), Is.EqualTo(10), "All ten opposing tiles, including empty tiles, must explode.");
                    for (var index = 0; index < diagnosticTiles.Length; index++)
                    {
                        var anchor = diagnosticTiles[index].GetComponent<TileTextView>().ActiveEffectButtonAnchor;
                        var sourceCanvas = anchor.GetComponentInParent<Canvas>().rootCanvas;
                        var sourceCamera = sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : sourceCanvas.worldCamera;
                        var expected = RectTransformUtility.WorldToScreenPoint(sourceCamera, anchor.TransformPoint(anchor.rect.center));
                        var actual = RectTransformUtility.WorldToScreenPoint(camera, bursts.Single(image => image.name == "TimedBombBurst" + index).rectTransform.TransformPoint(Vector3.zero));
                        Assert.That(Vector2.Distance(actual, expected), Is.LessThan(1f), "Burst must align with its actual target tile's screen center.");
                    }
                    Assert.That(canvas.overrideSorting, Is.True, "Parenting and activation must retain independent overlay sorting.");
                    Assert.That(canvas.sortingOrder, Is.EqualTo(4910));
                    typeof(TimedBombPresentationVfx).GetMethod("Cancel", Hidden).Invoke(effect, null);
                }
            }
            finally
            {
                if (effect != null) Object.DestroyImmediate(effect);
                typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null, oldToken);
            }
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator TimerTransitionsAtScreenCenterAndEveryOpposingTileExplodesOnce()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var fixture=new Fixture())
            {
                fixture.Play(PlayerId.Player,3,2);fixture.SampleAge(.12f);
                fixture.AssertBodyCentered();Assert.That(fixture.Number,Is.EqualTo(3));fixture.Capture("TimedBombCountdown03.png",3);
                fixture.Play(PlayerId.Player,3,2);fixture.SampleAge(.48f);
                fixture.AssertBodyCentered();Assert.That(fixture.Number,Is.EqualTo(2));fixture.Capture("TimedBombCountdown02.png",2);
                Assert.That(fixture.Bursts.Any(i=>i.gameObject.activeInHierarchy),Is.False);
                Assert.That(fixture.Effect.GetComponents<AudioSource>().Length,Is.Zero,"Nonzero countdowns have no explosion sound.");
                yield return new WaitForSecondsRealtime(.4f);fixture.AssertHiddenAndSilent();
                fixture.Play(PlayerId.Player,2,1);fixture.SampleAge(.48f);
                fixture.AssertBodyCentered();Assert.That(fixture.Number,Is.EqualTo(1));fixture.Capture("TimedBombCountdown01.png",1);
                yield return new WaitForSecondsRealtime(.4f);fixture.AssertHiddenAndSilent();
                foreach(var caster in new[]{PlayerId.Player,PlayerId.AI})
                {
                    Assert.That(fixture.Play(caster,1,0),Is.EqualTo(1.8f));
                    fixture.SampleAge(.5f);Assert.That(fixture.Number,Is.Zero);
                    fixture.Capture("TimedBombCountdown00-"+caster+".png",0);
                    fixture.SampleAge(1.05f);
                    fixture.AssertBurstCoverage(caster);Assert.That(fixture.Effect.GetComponents<AudioSource>().Single().isPlaying,Is.True);
                    fixture.AssertBurstCoverage(caster);fixture.Capture("TimedBombExplosion-"+caster+".png");
                    Assert.That(fixture.Bursts.Select(i=>i.sprite).Distinct().Count(),Is.EqualTo(1));
                    yield return new WaitForSecondsRealtime(.85f);fixture.AssertHiddenAndSilent();
                }
                Assert.That(fixture.Effect.GetComponents<AudioSource>().Length,Is.EqualTo(1));
                Assert.That(fixture.State.Player.Master.CurrentHp,Is.EqualTo(333));Assert.That(fixture.State.AI.Master.CurrentHp,Is.EqualTo(333));
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator WholeBoardBurstsFitLandscapePortraitAndRotatedCanvas()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var fixture=new Fixture())
            {
                foreach(var size in new[]{new Vector2(1280,720),new Vector2(1080,2340),new Vector2(2340,1080)})
                {
                    fixture.Layout(size);
                    fixture.CanvasRect.localRotation=Quaternion.Euler(0,0,23);
                    fixture.Play(PlayerId.AI,1,0);yield return new WaitForSecondsRealtime(.9f);
                    fixture.AssertBurstCoverage(PlayerId.AI);
                    fixture.Bootstrapper.CancelCombatPresentation();fixture.AssertHiddenAndSilent();
                }
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator UnscaledCountdownPreviousHitchCancellationDisableAndInvalidLayoutAreSafe()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            var oldTimeScale=Time.timeScale;
            try
            {
                using(var fixture=new Fixture())
                {
                    fixture.Play(PlayerId.Player);fixture.Bootstrapper.CancelCombatPresentation();yield return null;
                    System.Threading.Thread.Sleep(250);yield return null;
                    Time.timeScale=0;fixture.Play(PlayerId.Player);
                    typeof(TimedBombPresentationVfx).GetMethod("LateUpdate",Hidden).Invoke(fixture.Effect,null);
                    Assert.That(fixture.Number,Is.EqualTo(3),"Earlier frame hitch is not countdown age.");
                    yield return new WaitForSecondsRealtime(.48f);Assert.That(fixture.Number,Is.EqualTo(2));
                    fixture.Bootstrapper.CancelCombatPresentation();fixture.AssertHiddenAndSilent();
                    fixture.Play(PlayerId.AI,1,0);yield return new WaitForSecondsRealtime(.3f);
                    fixture.Effect.enabled=false;fixture.AssertHiddenAndSilent();fixture.Effect.enabled=true;
                    yield return new WaitForSecondsRealtime(1.6f);fixture.AssertHiddenAndSilent();
                    fixture.Layout(Vector2.zero);Assert.That(fixture.Play(PlayerId.Player),Is.Zero);fixture.AssertHiddenAndSilent();
                }
            }
            finally{Time.timeScale=oldTimeScale;}
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator MultipleOnlineCuesAreSequentialAndCancelDoesNotStartRemainingBombOrDeferredBanner()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var fixture=new Fixture())
            {
                var events=new[]{new BattleEventDto{EventType=BattleEventType.TurnStarted,SourceOwnerId=PlayerId.Player},
                    Cue(PlayerId.Player,0,3,2),Cue(PlayerId.AI,1,1,0)};
                var duration=fixture.Bootstrapper.PlayOnlineBattleEventAnimations(events);
                Assert.That(duration,Is.GreaterThanOrEqualTo(2.6f));
                Assert.That(fixture.Number,Is.EqualTo(3));Assert.That(fixture.CueBefore,Is.EqualTo(3));
                var banner=fixture.Root.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.name=="YourTurnBannerImage");
                Assert.That(banner==null||!banner.gameObject.activeInHierarchy,Is.True,"Center banner waits for countdown presentation.");
                var deadline=Time.realtimeSinceStartup+4f;
                while((fixture.CueBefore!=1||!fixture.IsPlaying)&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(fixture.CueBefore,Is.EqualTo(1));Assert.That(fixture.IsPlaying,Is.True,
                    "The second queued countdown must actually start before cancellation.");
                fixture.Bootstrapper.CancelCombatPresentation();fixture.AssertHiddenAndSilent();
                yield return new WaitForSecondsRealtime(2f);fixture.AssertHiddenAndSilent();
                Assert.That(fixture.Bootstrapper.PlayOnlineBattleEventAnimations(Array.Empty<BattleEventDto>()),Is.Zero);
                Assert.That(fixture.State.Player.Master.CurrentHp,Is.EqualTo(333));
                fixture.Bootstrapper.PlayOnlineBattleEventAnimations(new[]{
                    new BattleEventDto{EventType=BattleEventType.TurnStarted,SourceOwnerId=PlayerId.Player},
                    Cue(PlayerId.AI,2,1,0),new BattleEventDto{EventType=BattleEventType.BattleEnded}});
                yield return new WaitForSecondsRealtime(2.1f);
                banner=fixture.Root.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.name=="YourTurnBannerImage");
                Assert.That(banner==null||!banner.gameObject.activeInHierarchy,Is.True,
                    "The ended envelope suppresses a deferred turn banner before its final snapshot arrives.");
            }
            yield return new ExitPlayMode();
        }

        private static BattleEventDto Cue(PlayerId caster,int index,int before,int after)=>new BattleEventDto
        {
            EventType=BattleEventType.TimedBombCountdown,CardId="TimedBomb",SourceCardId="TimedBomb",EffectId="timed_bomb",
            SourceOwnerId=caster,TargetOwnerId=caster==PlayerId.Player?PlayerId.AI:PlayerId.Player,
            EffectIndex=index,RemainingTriggersBefore=before,RemainingTriggersAfter=after,RuntimeId="timed_bomb:"+index
        };
        private static object Capture(BattleState state)=>typeof(BattleBootstrapper).GetMethod("CaptureBattleSnapshot",Static).Invoke(null,new object[]{state});
        private static IReadOnlyList<BattleValuePopupEvent> Clone(BattleState state)=>
            (IReadOnlyList<BattleValuePopupEvent>)typeof(BattleBootstrapper).GetMethod("CloneValuePopupEvents",Static).Invoke(null,new object[]{state});
        private static List<IReadOnlyList<BattleEventDto>> Groups(IReadOnlyList<BattleEventDto> events)=>
            (List<IReadOnlyList<BattleEventDto>>)typeof(BattleBootstrapper).GetMethod("BuildOnlineAnimationGroups",Static).Invoke(null,new object[]{events});
        private static BattleEventDto Damage(BattleValuePopupEvent popup)=>new BattleEventDto
        {
            EventType=BattleEventType.DamageApplied,SourceCardId=popup.SourceCardId,SourceOwnerId=popup.SourceOwnerId,
            TargetOwnerId=popup.OwnerId,TargetCoord=TileCoordDto.FromDomain(popup.Coord),RuntimeId=popup.RuntimeId,TargetRuntimeId=popup.RuntimeId,
            Amount=popup.Amount,HpBefore=popup.HpBefore,HpAfter=popup.HpAfter,DamageType=popup.DamageType,ValueCause=popup.Cause
        };
        private static object Build(object before,object after,IReadOnlyList<BattleValuePopupEvent> events)=>
            typeof(BattleBootstrapper).GetMethod("BuildImpactAnimationRequest",Static).Invoke(null,new object[]{before,after,events,null,null});
        private static object Property(object request,string name)=>request.GetType().GetProperty(name).GetValue(request);
        private static void Set(object instance,string name,object value)=>instance.GetType().GetField(name,Hidden).SetValue(instance,value);


        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root = new GameObject("TimedBombPresentationFixture");
            internal readonly BattleBootstrapper Bootstrapper;
            internal readonly TimedBombPresentationVfx Effect;
            internal readonly LocalBattleGateway Gateway;
            internal readonly RectTransform CanvasRect;
            internal readonly BoardPresenter Board;
            internal float TileWidth;
            internal BattleState State => Gateway.CurrentBattleState;
            internal Image Image => Root.GetComponentsInChildren<Image>(true).SingleOrDefault(i => i.name == "TimedBombBody");
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
                Effect = new GameObject("TimedBombEffectHost").AddComponent<TimedBombPresentationVfx>(); Effect.transform.SetParent(Root.transform);
                var host = new GameObject("Coordinator"); host.SetActive(false); host.transform.SetParent(Root.transform);
                Bootstrapper = host.AddComponent<BattleBootstrapper>(); Set(Bootstrapper, "_startOnAwake", false);
                Set(Bootstrapper, "_autoRunAiTurns", false); Set(Bootstrapper, "_returnToDraftSceneWhenRunSessionActive", false);
                var screenHost = new GameObject("DormantScreenPresenter"); screenHost.SetActive(false); screenHost.transform.SetParent(Root.transform);
                var screen = screenHost.AddComponent<BattleScreenPresenter>(); Set(screen, "_boardPresenter", Board);
                Set(Bootstrapper, "_battleScreenPresenter", screen); Set(Bootstrapper, "_timedBombPresentationVfx", Effect);
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
                    typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null, "synthetic-timed-bomb-presentation-session");
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

            internal float Play(PlayerId caster, int before = 3, int after = 2) => (float)typeof(TimedBombPresentationVfx).GetMethod("Play", Hidden).Invoke(Effect, new object[] { Board, caster, before, after });
            internal void SampleAge(float age)
            {
                Set(Effect,"_startedAt",Time.unscaledTime-age);
                typeof(TimedBombPresentationVfx).GetMethod("LateUpdate",Hidden).Invoke(Effect,null);
                Canvas.ForceUpdateCanvases();
            }
            internal object OnlineRequest(BattleEventDto cast, IReadOnlyList<BattleEventDto> events) =>
                typeof(BattleBootstrapper).GetMethod("BuildOnlineImpactAnimationRequest", Hidden).Invoke(Bootstrapper, new object[] { cast?.CardId, cast, events });
            internal float Estimate(object request) => (float)typeof(BattleBootstrapper).GetMethod("EstimateImpactAnimationDuration", Hidden).Invoke(Bootstrapper, new[] { request });
            internal void AssertBodyCentered()
            {
                Assert.That(Image != null && Image.gameObject.activeInHierarchy, Is.True);
                Assert.That(Vector3.Distance(Image.rectTransform.position, CanvasRect.TransformPoint(CanvasRect.rect.center)), Is.LessThan(.001f));
                Assert.That(Image.raycastTarget, Is.False);
                Assert.That(Image.GetComponentInParent<CanvasGroup>().blocksRaycasts, Is.False);
            }
            internal Image[] Bursts => Root.GetComponentsInChildren<Image>(true).Where(i => i.name.StartsWith("TimedBombBurst", StringComparison.Ordinal)).OrderBy(i => i.name).ToArray();
            internal int Number => (int)typeof(BombTimerGraphic).GetProperty("CurrentNumber", Hidden).GetValue(Root.GetComponentInChildren<BombTimerGraphic>(true));
            internal int CueBefore => (int)typeof(BombTimerGraphic).GetField("_before", Hidden).GetValue(Root.GetComponentInChildren<BombTimerGraphic>(true));
            internal bool IsPlaying => (bool)typeof(TimedBombPresentationVfx).GetField("_playing", Hidden).GetValue(Effect);
            internal void AssertBurstCoverage(PlayerId caster)
            {
                var tiles = caster == PlayerId.Player ? Board.AITileViews : Board.PlayerTileViews;
                Assert.That(Bursts.Length, Is.EqualTo(10));
                for (var i=0;i<tiles.Length;i++)
                {
                    var image=Bursts[i];
                    Assert.That(image.gameObject.activeInHierarchy, Is.True);
                    Assert.That(Vector3.Distance(image.rectTransform.position, tiles[i].transform.position), Is.LessThan(.001f));
                    Assert.That(image.rectTransform.rect.width, Is.GreaterThanOrEqualTo(TileWidth));
                    Assert.That(image.raycastTarget, Is.False);
                    var corners=new Vector3[4];image.rectTransform.GetWorldCorners(corners);
                    foreach(var corner in corners)
                    {
                        var local=CanvasRect.InverseTransformPoint(corner);
                        Assert.That(local.x,Is.InRange(CanvasRect.rect.xMin-.001f,CanvasRect.rect.xMax+.001f));
                        Assert.That(local.y,Is.InRange(CanvasRect.rect.yMin-.001f,CanvasRect.rect.yMax+.001f));
                    }
                }
            }
            internal void AssertHiddenAndSilent()
            {
                Assert.That(Image == null || !Image.gameObject.activeInHierarchy, Is.True);
                Assert.That(Bursts.Any(i => i.gameObject.activeInHierarchy), Is.False);
                Assert.That(Effect.GetComponents<AudioSource>().Any(a => a.isPlaying), Is.False);
            }
            internal void Capture(string filename, int? expectedCountdown = null)
            {
                if (_render == null) return;
                Canvas.ForceUpdateCanvases(); _camera.Render();
                var old = RenderTexture.active; var texture = new Texture2D(_render.width, _render.height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = _render; texture.ReadPixels(new Rect(0, 0, _render.width, _render.height), 0, 0); texture.Apply();
                    var path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Tools/Art/Preview/" + filename));
                    Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, texture.EncodeToPNG());
                    if (expectedCountdown.HasValue) AssertTimerPixels(texture,expectedCountdown.Value);
                }
                finally { RenderTexture.active = old; Object.DestroyImmediate(texture); }
            }
            private void AssertTimerPixels(Texture2D texture, int count)
            {
                var timer=Root.GetComponentInChildren<BombTimerGraphic>(true);
                var renderer=timer.GetComponent<CanvasRenderer>();
                var mesh=renderer==null?null:renderer.GetMesh();
                {
                    var script=MonoScript.FromMonoBehaviour(timer);
                    TestContext.WriteLine("Timer rendered evidence: mesh="+(mesh==null?0:mesh.vertexCount)+", depth="+(renderer==null?-1:renderer.absoluteDepth)+
                        ", culled="+(renderer==null||renderer.cull)+", materials="+(renderer==null?0:renderer.materialCount)+
                        ", alpha="+(renderer==null?0:renderer.GetInheritedAlpha())+", rect="+timer.rectTransform.rect+
                        ", registry="+(timer.canvas!=null&&GraphicRegistry.GetGraphicsForCanvas(timer.canvas).Contains(timer))+
                        ", nativeClass="+(script==null?"null":script.GetClass()?.FullName)+", count="+count);
                    Assert.That(renderer,Is.Not.Null);Assert.That(mesh,Is.Not.Null);Assert.That(mesh.vertexCount,Is.GreaterThan(4));
                    Assert.That(script,Is.Not.Null);Assert.That(script.GetClass(),Is.EqualTo(typeof(BombTimerGraphic)));
                    Assert.That(renderer.materialCount,Is.GreaterThan(0));
                    Assert.That(renderer.absoluteDepth,Is.GreaterThanOrEqualTo(0));Assert.That(renderer.cull,Is.False);
                    Assert.That(renderer.GetInheritedAlpha(),Is.EqualTo(1).Within(.001f));
                    // The source PNG says 33. Its tens digit lacks this upper-left stroke
                    // and has a lit middle stroke; rendered 00 must show the opposite.
                    AssertLed(texture,.701f,.62f,true,"tens upper-left 0");
                    AssertLed(texture,.747f,.4875f,false,"tens middle 0");
                    AssertLed(texture,.844f,.62f,count==0,"ones upper-left");
                    AssertLed(texture,.844f,.33f,count==0||count==2,"ones lower-left");
                    AssertLed(texture,.890f,.4875f,count==2||count==3,"ones middle");
                    AssertLed(texture,.934f,.33f,count!=2,"ones lower-right");
                }
            }
            private void AssertLed(Texture2D texture,float u,float v,bool lit,string label)
            {
                // Independent screen coordinates come from the supplied bomb's original LCD quad.
                var original=Vector2.Lerp(Vector2.Lerp(new Vector2(348,287),new Vector2(637,271),u),
                    Vector2.Lerp(new Vector2(337,140),new Vector2(626,132),u),v);
                var normalized=new Vector2((16+original.x*480/713)/512,1-(130+original.y*252/374)/512);
                var local=Image.rectTransform.rect.min+Vector2.Scale(normalized,Image.rectTransform.rect.size);
                var viewport=_camera.WorldToViewportPoint(Image.rectTransform.TransformPoint(local));
                var pixel=texture.GetPixel(Mathf.RoundToInt(viewport.x*texture.width),Mathf.RoundToInt(viewport.y*texture.height));
                if(lit)Assert.That(pixel.r>.55f&&pixel.r>pixel.g*3&&pixel.r>pixel.b*3,Is.True,label+" expected red, actual "+pixel);
                else Assert.That(pixel.r<.4f&&pixel.g<.2f&&pixel.b<.2f,Is.True,label+" expected covered dark LCD, actual "+pixel);
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
