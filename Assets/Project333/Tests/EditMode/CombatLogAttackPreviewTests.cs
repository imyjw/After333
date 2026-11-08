using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Online;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class CombatLogAttackPreviewTests
    {
        private const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
        private static object Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Hidden).Invoke(owner,args);
        private static void Set(object owner,string name,object value)=>owner.GetType().GetField(name,Hidden).SetValue(owner,value);
        private static T Get<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Hidden).GetValue(owner);
        private static TMP_FontAsset Font()=>UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/Noto_Sans_KR/static/NotoSansKR-Regular SDF.asset");
        private static BattleState Battle(PlayerId active=PlayerId.AI)
        {
            var deck=Enumerable.Repeat("Goblin",33).ToArray();
            var b=new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck,deck,PlayerId.Player));
            b.RestoreRuntimeState(4,active,PhaseType.Main);
            b.Player.Master.RemainingAttacksThisTurn=b.AI.Master.RemainingAttacksThisTurn=1; return b;
        }
        private static BattleCombatLogEntryDto Example()
        {
            var b=Battle();b.Player.Master.IncreaseBaseAttack(30);b.AI.Master.CurrentHp=323;
            var recorder=new TutorialCombatLogRecorder(b);
            new AttackService(new TargetingService()).Attack(b,PlayerId.AI,b.AI.Master.Position,b.Player.Master.Position);
            return recorder.Capture(b).Single(x=>x.EntryType==BattleCombatLogEntryType.Attack);
        }
        [Test]
        public void AttackRecordsExactPreCombatStatsAndActualCounterDamageIndependentlyOfFutureState()
        {
            var b=Battle();b.Player.Master.IncreaseBaseAttack(30);b.AI.Master.CurrentHp=323;
            var log=new TutorialCombatLogRecorder(b);
            new AttackService(new TargetingService()).Attack(b,PlayerId.AI,b.AI.Master.Position,b.Player.Master.Position);
            var e=log.Capture(b).Single(x=>x.EntryType==BattleCombatLogEntryType.Attack);
            Assert.That(e.SourcePreview.Attack,Is.EqualTo(3));Assert.That(e.SourcePreview.Hp,Is.EqualTo(323));
            Assert.That(e.TargetPreview.Attack,Is.EqualTo(33));Assert.That(e.TargetPreview.Hp,Is.EqualTo(333));
            Assert.That(e.SourcePreview.DamageTaken,Is.EqualTo(33));Assert.That(e.TargetPreview.DamageTaken,Is.EqualTo(3));
            Assert.That(e.SourcePreview.OriginalAttack,Is.EqualTo(3));Assert.That(e.TargetPreview.OriginalMaxHp,Is.EqualTo(333));
            b.Player.Master.IncreaseBaseAttack(100);b.Player.Master.CurrentHp=1;
            Assert.That(e.TargetPreview.Attack,Is.EqualTo(33));Assert.That(e.TargetPreview.Hp,Is.EqualTo(333));
            Assert.That(log.Capture(b).Any(x=>x.EntryType==BattleCombatLogEntryType.Attack),Is.False);
        }
        [Test]
        public void LocalGatewayAttackAddsAnInteractiveHistoricalRecordThroughTheExistingLogCallback()
        {
            var state=Battle(PlayerId.Player);state.Player.Master.CurrentHp=323;state.AI.Master.IncreaseBaseAttack(30);
            var provider=new Project333.Runtime.Infrastructure.Data.InMemoryCardDefinitionProvider(Array.Empty<Project333.Runtime.Infrastructure.Data.CardDefinition>());
            var flow=new BattleFlowController(new BattleSetupService(),new MulliganService(),new TurnStartService(),
                new BattleCommandProcessor(new PlayCardService(provider),new SpellService(provider),new MoveService(),new AttackService(new TargetingService()),new EndTurnService()));
            flow.RestoreBattleState(state);
            var gateway=new Project333.Runtime.Application.Gateways.LocalBattleGateway(flow);
            var go=new GameObject("LocalHistory");go.SetActive(false);var bootstrap=go.AddComponent<BattleBootstrapper>();
            try
            {
                Set(bootstrap,"_battleGateway",gateway);Set(bootstrap,"_sendPlayerActionsToOnlineGateway",false);
                Call(bootstrap,"PrimeCombatLogAttack");
                gateway.ExecuteCommand(PlayerId.Player,new Project333.Runtime.Application.Commands.AttackCommand(state.Player.Master.Position,state.AI.Master.Position));
                Call(bootstrap,"AddCombatLogEntry","local attack");
                Assert.That(bootstrap.TryGetCombatLogAttackPreview(0,out var record),Is.True);
                Assert.That(record.SourcePreview.Attack,Is.EqualTo(3));Assert.That(record.SourcePreview.Hp,Is.EqualTo(323));
                Assert.That(record.TargetPreview.Attack,Is.EqualTo(33));Assert.That(record.SourcePreview.DamageTaken,Is.EqualTo(33));
            }
            finally{Object.DestroyImmediate(go);}
        }
        [Test]
        public void JsonRoundtripPreservesSnapshotsAndLegacyRecordsRemainOptional()
        {
            var e=Example();var restored=JsonConvert.DeserializeObject<BattleCombatLogEntryDto>(JsonConvert.SerializeObject(e));
            Assert.That(restored.SourcePreview.Hp,Is.EqualTo(323));Assert.That(restored.TargetPreview.DamageTaken,Is.EqualTo(3));
            var old=JsonConvert.DeserializeObject<BattleCombatLogEntryDto>("{\"EntryType\":7}");
            Assert.That(old.SourcePreview,Is.Null);Assert.That(old.TargetPreview,Is.Null);
        }
        [Test]
        public void BothViewerSeatsKeepAttackRolesAndDeepCopyHistoricalStats()
        {
            var go=new GameObject("Connection");go.SetActive(false);var connection=go.AddComponent<OnlineBattleConnectionTester>();
            try
            {
                var original=Example();
                foreach(var seat in new[]{PlayerId.Player,PlayerId.AI})
                {
                    Set(connection,"_localPlayerId",seat);
                    var projected=(BattleCombatLogEntryDto)Call(connection,"ProjectCombatLogEntryToLocalPerspective",original);
                    Assert.That(projected.SourcePreview.Attack,Is.EqualTo(3));Assert.That(projected.TargetPreview.Attack,Is.EqualTo(33));
                    Assert.That(projected.SourcePreview,Is.Not.SameAs(original.SourcePreview));
                    projected.SourcePreview.Hp=1;Assert.That(original.SourcePreview.Hp,Is.EqualTo(323));
                }
            }
            finally{Object.DestroyImmediate(go);}
        }
        [TestCase(33,3,1)] [TestCase(3,3,0)] [TestCase(1,3,-1)] [TestCase(323,333,-1)] [TestCase(350,333,1)]
        public void StatColorUsesHistoricalOriginalValue(int value,int original,int change)
        {
            Assert.That(CombatLogAttackPreview.StatColor(value,original),Is.EqualTo(change>0?CombatLogAttackPreview.IncreasedColor:change<0?CombatLogAttackPreview.DecreasedColor:Color.white));
        }
        [Test]
        public void HistoryWindowKeepsTextAndPreviewsAlignedAndDoesNotInventOldData()
        {
            var go=new GameObject("History");go.SetActive(false);var b=go.AddComponent<BattleBootstrapper>();
            try
            {
                var e=Example();var entries=new List<BattleCombatLogEntryDto>{e};
                for(var i=0;i<34;i++)entries.Add(new BattleCombatLogEntryDto{EntryType=BattleCombatLogEntryType.TurnStarted,TurnNumber=i+1});
                entries.Add(e);Call(b,"ReplaceCombatLogEntries",entries);
                Assert.That(b.TryGetCombatLogAttackPreview(32,out var latest),Is.True);Assert.That(latest,Is.SameAs(e));
                Assert.That(b.TryGetCombatLogAttackPreview(0,out _),Is.False);
                Call(b,"ReplaceCombatLogEntries",(object)new[]{new BattleCombatLogEntryDto{EntryType=BattleCombatLogEntryType.Attack}});
                Assert.That(b.TryGetCombatLogAttackPreview(0,out _),Is.False);
                Call(b,"ClearCombatLog");Assert.That(b.TryGetCombatLogAttackPreview(0,out _),Is.False);
            }
            finally{Object.DestroyImmediate(go);}
        }
        [TestCase(false,-3)] [TestCase(false,0)] [TestCase(false,3)] [TestCase(true,3)]
        public void MarkersDisappearAtThreeSecondsWithoutChangingPreCombatStats(bool died,int damage)
        {
            var e=Example();e.SourcePreview.Died=died;e.SourcePreview.DamageTaken=damage;
            var overlay=CombatLogAttackPreview.Create(default,Font());
            try
            {
                overlay.Show(e.SourcePreview,e.TargetPreview,e.SourceOwnerId,e.TargetOwnerId);
                var marker=overlay.transform.Find("CombatPair/Attacker/ResultMarker");
                Assert.That(marker.gameObject.activeSelf,Is.EqualTo(died||damage>0));
                var markerGraphic=marker.GetComponent<CombatLogSymbolGraphic>();
                Assert.That(markerGraphic.Kind,Is.EqualTo(died?CombatLogSymbolGraphic.Symbol.Skull:CombatLogSymbolGraphic.Symbol.Damage));
                Assert.That(markerGraphic.mainTexture,Is.Not.Null);
                Assert.That(markerGraphic.mainTexture.name,Is.EqualTo(died?"Skull":"DamageBurst"));
                var text=overlay.transform.Find("CombatPair/Attacker/Attack").GetComponent<TMP_Text>();Assert.That(text.text,Is.EqualTo("3"));
                var start=Get<float>(overlay,"_shownAt");var deadline=Get<float>(overlay,"_markersExpireAt");
                Assert.That(deadline-start,Is.EqualTo(3).Within(.001f));Call(overlay,"UpdateMarkerVisibility",deadline-.001f);
                Assert.That(marker.gameObject.activeSelf,Is.EqualTo(died||damage>0));
                Call(overlay,"UpdateMarkerVisibility",deadline);Assert.That(marker.gameObject.activeSelf,Is.False);
                Assert.That(overlay.IsVisible,Is.True);Assert.That(text.text,Is.EqualTo("3"));
                Assert.That(overlay.GetComponentsInChildren<Graphic>(true).All(x=>!x.raycastTarget),Is.True);
                overlay.Hide();Assert.That(overlay.IsVisible,Is.False);
            }
            finally{Object.DestroyImmediate(overlay.gameObject);}
        }
        [Test]
        public void HoldOpensOnlyForTheLinkedAttackAndReleaseOrDragClosesIt()
        {
            var host=new GameObject("History");host.SetActive(false);var b=host.AddComponent<BattleBootstrapper>();
            var canvas=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var textGo=new GameObject("Log",typeof(RectTransform),typeof(TextMeshProUGUI));textGo.transform.SetParent(canvas.transform,false);
            var text=textGo.GetComponent<TMP_Text>();text.font=Font();text.fontSize=30;text.rectTransform.sizeDelta=new Vector2(900,300);
            var viewGo=new GameObject("View");var view=viewGo.AddComponent<CombatLogTextView>();Set(view,"_text",text);view.BindHistorySource(b);
            var hold=textGo.AddComponent<CombatLogHoldTarget>();hold.Bind(text,b);
            try
            {
                Call(b,"ReplaceCombatLogEntries",(object)new[]{Example()});view.SetLog(b.CombatLogText);text.ForceMeshUpdate();Canvas.ForceUpdateCanvases();
                Assert.That(text.textInfo.linkCount,Is.EqualTo(1));
                var link=text.textInfo.linkInfo[0];var letter=text.textInfo.characterInfo[link.linkTextfirstCharacterIndex+2];
                var point=RectTransformUtility.WorldToScreenPoint(null,text.transform.TransformPoint((letter.bottomLeft+letter.topRight)*.5f));
                var e=new PointerEventData(EventSystem.current){position=point,pointerId=-1,button=PointerEventData.InputButton.Left};
                hold.OnPointerDown(e);var at=Get<float>(hold,"_pressedAt");Call(hold,"AdvanceHold",at+.1f,point);
                Assert.That(Get<CombatLogAttackPreview>(hold,"_preview"),Is.Null);
                Call(hold,"AdvanceHold",at+.3f,point);Assert.That(Get<CombatLogAttackPreview>(hold,"_preview").IsVisible,Is.True);
                var replacement=Example();replacement.SourcePreview.Hp=100;
                Call(b,"ReplaceCombatLogEntries",(object)new[]{replacement});view.SetLog(b.CombatLogText);
                Assert.That(Get<CombatLogAttackPreview>(hold,"_preview").transform.Find("CombatPair/Attacker/Hp").GetComponent<TMP_Text>().text,Is.EqualTo("323"));
                hold.OnPointerUp(e);Assert.That(Get<CombatLogAttackPreview>(hold,"_preview").IsVisible,Is.False);
                hold.OnPointerDown(e);Call(hold,"AdvanceHold",Time.unscaledTime+.4f,point+new Vector2(100,0));
                Assert.That(Get<CombatLogAttackPreview>(hold,"_preview").IsVisible,Is.False);
                Assert.That(textGo.GetComponent<IDragHandler>(),Is.Null,"ScrollRect retains the drag handler.");
            }
            finally{Object.DestroyImmediate(viewGo);Object.DestroyImmediate(canvas);Object.DestroyImmediate(host);}
        }
        [UnityTest]
        public IEnumerator ActualRenderShowsDamageThenCleanCardsAndDeathSymbol()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            var overlay=CombatLogAttackPreview.Create(default,Font());var e=Example();
            var cameraGo=new GameObject("CaptureCamera",typeof(Camera));var camera=cameraGo.GetComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.075f,.09f);
            var target=new RenderTexture(2340,1080,24);target.Create();camera.targetTexture=target;
            var canvas=overlay.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;
            try
            {
                var folder=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../Captures/CombatLogHoldPreview_20261029"));Directory.CreateDirectory(folder);
                overlay.Show(e.SourcePreview,e.TargetPreview,e.SourceOwnerId,e.TargetOwnerId);yield return null;
                Canvas.ForceUpdateCanvases();
                foreach(var symbol in overlay.GetComponentsInChildren<CombatLogSymbolGraphic>())
                {
                    var mesh=symbol.canvasRenderer.GetMesh();
                    Assert.That(mesh,Is.Not.Null);Assert.That(mesh.vertexCount,Is.GreaterThan(0),symbol.name+" must render its icon.");
                }
                Save(camera,target,Path.Combine(folder,"01-damage.png"));
                yield return new WaitForSecondsRealtime(3.05f);
                Assert.That(overlay.transform.Find("CombatPair/Attacker/ResultMarker").gameObject.activeSelf,Is.False);
                Assert.That(overlay.transform.Find("CombatPair/Defender/ResultMarker").gameObject.activeSelf,Is.False);
                Save(camera,target,Path.Combine(folder,"02-cards.png"));
                e.SourcePreview.Died=true;e.TargetPreview.DamageTaken=1;overlay.Show(e.SourcePreview,e.TargetPreview,e.SourceOwnerId,e.TargetOwnerId);yield return null;
                Save(camera,target,Path.Combine(folder,"03-death.png"));
                Assert.That(File.ReadAllBytes(Path.Combine(folder,"01-damage.png")).Length,Is.GreaterThan(20000));
            }
            finally{Object.Destroy(overlay.gameObject);Object.Destroy(cameraGo);target.Release();Object.Destroy(target);}
            yield return new ExitPlayMode();
        }
        private static void Save(Camera camera,RenderTexture rt,string path)
        {
            Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
            var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);RenderTexture.active=previous;
        }
        [Test]
        public void SingleCardPreviewDisplaysCenteredCardOwnerLabelAndStatColorsWithoutSecondCardOrSwords()
        {
            var overlay=CombatLogAttackPreview.Create(default,Font());
            try
            {
                var snapshot=new BattleCombatLogCardSnapshotDto
                {
                    CardId="GaebangChief",RuntimeId="chief-1",
                    Attack=40,OriginalAttack=10,Hp=45,MaxHp=60,OriginalMaxHp=60,DamageTaken=15,Died=true
                };
                overlay.ShowSingle(snapshot,PlayerId.Player);
                var left=overlay.transform.Find("CombatPair/Attacker").GetComponent<RectTransform>();
                var right=overlay.transform.Find("CombatPair/Defender");
                var swords=overlay.transform.Find("CombatPair/CrossedSwords");
                var marker=overlay.transform.Find("CombatPair/Attacker/ResultMarker");
                var atk=overlay.transform.Find("CombatPair/Attacker/Attack").GetComponent<TMP_Text>();
                var hp=overlay.transform.Find("CombatPair/Attacker/Hp").GetComponent<TMP_Text>();
                var role=overlay.transform.Find("CombatPair/Attacker/Role").GetComponent<TMP_Text>();

                Assert.That(overlay.IsVisible,Is.True);
                Assert.That(left.anchoredPosition,Is.EqualTo(new Vector2(0,20)));
                Assert.That(right.gameObject.activeSelf,Is.False);
                Assert.That(swords.gameObject.activeSelf,Is.False);
                Assert.That(marker.gameObject.activeSelf,Is.False);
                Assert.That(role.text,Is.EqualTo("플레이어"));
                Assert.That(atk.text,Is.EqualTo("40"));Assert.That(atk.color,Is.EqualTo(CombatLogAttackPreview.IncreasedColor));
                Assert.That(hp.text,Is.EqualTo("45"));Assert.That(hp.color,Is.EqualTo(CombatLogAttackPreview.DecreasedColor));

                var equalSnapshot=BattleCombatLogCardSnapshotDto.CreateHandCard("Goblin",20,20);
                overlay.ShowSingle(equalSnapshot,PlayerId.AI);
                Assert.That(role.text,Is.EqualTo("상대방"));
                Assert.That(atk.color,Is.EqualTo(Color.white));Assert.That(hp.color,Is.EqualTo(Color.white));

                var attackEntry=Example();
                overlay.Show(attackEntry);
                Assert.That(left.anchoredPosition.x,Is.LessThan(0f));
                Assert.That(right.gameObject.activeSelf,Is.True);
                Assert.That(swords.gameObject.activeSelf,Is.True);
                Assert.That(role.text,Does.StartWith("공격자 · "));
            }
            finally{Object.DestroyImmediate(overlay.gameObject);}
        }
        [Test]
        public void SingleCardPreviewEntriesCaptureHandBeforeAndAfterStatsAndSupportHoldInteraction()
        {
            var b=Battle(PlayerId.Player);
            b.Player.Resources.Add(new Project333.Runtime.Domain.Resources.ResourceSet(20,20,20,20));
            b.PlayerBoard.Place(new TileCoord(4,0),new UnitState("chieftain-1","OrcChieftain",PlayerId.Player,new TileCoord(4,0),AttackType.Melee,50,70,true,false,0,new Project333.Runtime.Domain.Resources.ResourceSet(),races:new[]{UnitRace.Orc}));
            var log=new TutorialCombatLogRecorder(b);

            // #2 UnitSummoned stores hand stats (OriginalAttack/OriginalMaxHp), unaffected by OrcChieftain aura.
            var orc=new UnitState("orc-1","OrcWarrior",PlayerId.Player,new TileCoord(0,0),AttackType.Melee,20,40,true,false,0,new Project333.Runtime.Domain.Resources.ResourceSet(),races:new[]{UnitRace.Orc});
            b.PlayerBoard.Place(new TileCoord(0,0),orc);
            var summonEntry=log.Capture(b).Single(x=>x.EntryType==BattleCombatLogEntryType.UnitSummoned);
            Assert.That(summonEntry.TryGetSingleCardPreview(out var summonPreview,out var summonOwner),Is.True);
            Assert.That(summonOwner,Is.EqualTo(PlayerId.Player));
            Assert.That(summonPreview.Attack,Is.EqualTo(20));Assert.That(summonPreview.Hp,Is.EqualTo(40));
            Assert.That(orc.Attack,Is.EqualTo(30));Assert.That(orc.CurrentHp,Is.EqualTo(50));

            // #3 BuildingConstructed & #10 ActiveEffectActivated (before-stats)
            var plant=new BuildingState("plant-1","PowerPlant",PlayerId.Player,new TileCoord(1,1),false,0,30,new Project333.Runtime.Domain.Resources.ResourceSet());
            b.PlayerBoard.Place(new TileCoord(1,1),plant);
            var buildEntry=log.Capture(b).Single(x=>x.EntryType==BattleCombatLogEntryType.BuildingConstructed);
            Assert.That(buildEntry.TryGetSingleCardPreview(out var buildPreview,out _),Is.True);
            Assert.That(buildPreview.Hp,Is.EqualTo(30));Assert.That(buildPreview.OriginalMaxHp,Is.EqualTo(30));

            plant.ApplyCombatStatBuff("TestBuff",0,5);plant.CurrentHp=25;
            log.Rebase(b);
            ActiveEffectService.Activate(b,PlayerId.Player,new TileCoord(1,1),plant.RuntimeId);
            plant.CurrentHp=10; // Mutate after activation; active preview must keep the before-activation 25 HP.
            var activeEntry=log.Capture(b).Single(x=>x.EntryType==BattleCombatLogEntryType.ActiveEffectActivated);
            Assert.That(activeEntry.TryGetSingleCardPreview(out var activePreview,out _),Is.True);
            Assert.That(activePreview.Hp,Is.EqualTo(25));Assert.That(activePreview.OriginalMaxHp,Is.EqualTo(30));

            // #21~#34 Status Applied & Cleared (after-stats)
            var chief=new UnitState("chief-1","GaebangChief",PlayerId.Player,new TileCoord(2,0),AttackType.Melee,10,60,true,true,1,new Project333.Runtime.Domain.Resources.ResourceSet(),hasHiding:true);
            b.PlayerBoard.Place(new TileCoord(2,0),chief);
            var summonChiefLogs=log.Capture(b);
            Assert.That(summonChiefLogs.Single(x=>x.EntryType==BattleCombatLogEntryType.HidingApplied).HasHoldPreview,Is.True);

            chief.IsDrained=true;
            var drainedLogs=log.Capture(b);
            Assert.That(drainedLogs.Single(x=>x.EntryType==BattleCombatLogEntryType.DrainedApplied).TryGetSingleCardPreview(out var p21,out _) && p21.Attack==10,Is.True);
            Assert.That(drainedLogs.Single(x=>x.EntryType==BattleCombatLogEntryType.HidingRevealed).HasHoldPreview,Is.True);

            chief.IsDrained=false;
            var clearedLogs=log.Capture(b);
            Assert.That(clearedLogs.Single(x=>x.EntryType==BattleCombatLogEntryType.DrainedCleared).TryGetSingleCardPreview(out var p22,out _) && p22.Attack>10,Is.True);

            chief.ApplyStun(1);chief.ApplyFreeze(1);chief.ApplyHuanShu();
            var debuffLogs=log.Capture(b);
            Assert.That(debuffLogs.Single(x=>x.EntryType==BattleCombatLogEntryType.StunApplied).HasHoldPreview,Is.True);
            Assert.That(debuffLogs.Single(x=>x.EntryType==BattleCombatLogEntryType.FreezeApplied).HasHoldPreview,Is.True);
            Assert.That(debuffLogs.Single(x=>x.EntryType==BattleCombatLogEntryType.HuanShuApplied).HasHoldPreview,Is.True);

            chief.ClearActionDebuffs();chief.ClearHuanShu();
            var debuffClearedLogs=log.Capture(b);
            Assert.That(debuffClearedLogs.Single(x=>x.EntryType==BattleCombatLogEntryType.StunCleared).HasHoldPreview,Is.True);
            Assert.That(debuffClearedLogs.Single(x=>x.EntryType==BattleCombatLogEntryType.FreezeCleared).HasHoldPreview,Is.True);
            Assert.That(debuffClearedLogs.Single(x=>x.EntryType==BattleCombatLogEntryType.HuanShuCleared).HasHoldPreview,Is.True);

            chief.ApplyErasure();
            Assert.That(log.Capture(b).Single(x=>x.EntryType==BattleCombatLogEntryType.ErasureApplied).TryGetSingleCardPreview(out var p23,out _) && p23.Attack==10,Is.True);

            chief.RestoreSuppressionStates(false,false,true,1,true);
            var sealLogs=log.Capture(b);
            Assert.That(sealLogs.Single(x=>x.EntryType==BattleCombatLogEntryType.ErasureCleared).HasHoldPreview,Is.True);
            Assert.That(sealLogs.Single(x=>x.EntryType==BattleCombatLogEntryType.SealboundApplied).HasHoldPreview,Is.True);

            chief.ResolveSealboundOwnerTurnStart();
            Assert.That(log.Capture(b).Single(x=>x.EntryType==BattleCombatLogEntryType.SealboundReleased).TryGetSingleCardPreview(out var p30,out _) && p30.Attack>10,Is.True);

            // Verify HoldTarget opens centered single-card preview for a status log entry
            var host=new GameObject("History");host.SetActive(false);var boot=host.AddComponent<BattleBootstrapper>();
            var canvas=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var textGo=new GameObject("Log",typeof(RectTransform),typeof(TextMeshProUGUI));textGo.transform.SetParent(canvas.transform,false);
            var text=textGo.GetComponent<TMP_Text>();text.font=Font();text.fontSize=30;text.rectTransform.sizeDelta=new Vector2(900,300);
            var viewGo=new GameObject("View");var view=viewGo.AddComponent<CombatLogTextView>();Set(view,"_text",text);view.BindHistorySource(boot);
            var hold=textGo.AddComponent<CombatLogHoldTarget>();hold.Bind(text,boot);
            try
            {
                var statusEntry=clearedLogs.Single(x=>x.EntryType==BattleCombatLogEntryType.DrainedCleared);
                Call(boot,"ReplaceCombatLogEntries",(object)new[]{statusEntry});view.SetLog(boot.CombatLogText);text.ForceMeshUpdate();Canvas.ForceUpdateCanvases();
                Assert.That(text.textInfo.linkCount,Is.EqualTo(1));
                var link=text.textInfo.linkInfo[0];var letter=text.textInfo.characterInfo[link.linkTextfirstCharacterIndex+2];
                var point=RectTransformUtility.WorldToScreenPoint(null,text.transform.TransformPoint((letter.bottomLeft+letter.topRight)*.5f));
                var ev=new PointerEventData(EventSystem.current){position=point,pointerId=-1,button=PointerEventData.InputButton.Left};
                hold.OnPointerDown(ev);var at=Get<float>(hold,"_pressedAt");
                Call(hold,"AdvanceHold",at+.3f,point);
                var preview=Get<CombatLogAttackPreview>(hold,"_preview");
                Assert.That(preview.IsVisible,Is.True);
                Assert.That(preview.transform.Find("CombatPair/Defender").gameObject.activeSelf,Is.False);
                Assert.That(preview.transform.Find("CombatPair/CrossedSwords").gameObject.activeSelf,Is.False);
                Assert.That(preview.transform.Find("CombatPair/Attacker/Role").GetComponent<TMP_Text>().text,Is.EqualTo("플레이어"));
                Assert.That(preview.transform.Find("CombatPair/Attacker/Attack").GetComponent<TMP_Text>().color,Is.EqualTo(CombatLogAttackPreview.IncreasedColor));
                hold.OnPointerUp(ev);Assert.That(preview.IsVisible,Is.False);
            }
            finally{Object.DestroyImmediate(viewGo);Object.DestroyImmediate(canvas);Object.DestroyImmediate(host);}
        }
        [Test]
        public void DirectedPreviewDisplaysUntargetedSingleTargetAndMultiTargetGridWithMarkersAndIcons()
        {
            var overlay=CombatLogAttackPreview.Create(default,Font());
            try
            {
                // 1. Untargeted spell (ManaStone): centered single card with Attack/Hp hidden
                var untargeted=new BattleCombatLogEntryDto
                {
                    EntryType=BattleCombatLogEntryType.SpellCast,
                    SourceOwnerId=PlayerId.Player,
                    SourceCardId="ManaStone",
                    SourcePreview=BattleCombatLogCardSnapshotDto.CreateSpellCard("ManaStone",PlayerId.Player)
                };
                Assert.That(untargeted.TryGetSingleCardPreview(out var singleSpell,out var singleOwner),Is.True);
                Assert.That(singleSpell.HasCombatStats,Is.False);
                overlay.Show(untargeted);
                var left=overlay.transform.Find("CombatPair/Attacker").GetComponent<RectTransform>();
                var right=overlay.transform.Find("CombatPair/Defender").GetComponent<RectTransform>();
                var swords=overlay.transform.Find("CombatPair/CrossedSwords").GetComponent<CombatLogSymbolGraphic>();
                var grid=overlay.transform.Find("CombatPair/TargetGrid");
                var leftAtk=overlay.transform.Find("CombatPair/Attacker/Attack");
                var leftHp=overlay.transform.Find("CombatPair/Attacker/Hp");
                var leftRole=overlay.transform.Find("CombatPair/Attacker/Role").GetComponent<TMP_Text>();
                Assert.That(left.anchoredPosition,Is.EqualTo(new Vector2(0,20)));
                Assert.That(right.gameObject.activeSelf,Is.False);
                Assert.That(swords.gameObject.activeSelf,Is.False);
                Assert.That(grid.gameObject.activeSelf,Is.False);
                Assert.That(leftAtk.gameObject.activeSelf,Is.False);
                Assert.That(leftHp.gameObject.activeSelf,Is.False);
                Assert.That(leftRole.text,Is.EqualTo("플레이어"));

                // 2. Single-target spell / effect: Left source spell, RightTriangle in center, Right target with Heal/Damage/Skull marker
                var healTarget=new BattleCombatLogCardSnapshotDto
                {
                    CardId="Master",OwnerId=PlayerId.Player,HasCombatStats=true,
                    Attack=3,OriginalAttack=3,Hp=300,MaxHp=333,OriginalMaxHp=333,HealAmount=15
                };
                var singleDirected=new BattleCombatLogEntryDto
                {
                    EntryType=BattleCombatLogEntryType.Healing,
                    SourceOwnerId=PlayerId.Player,
                    SourceCardId="BlueDragon",
                    SourcePreview=new BattleCombatLogCardSnapshotDto{CardId="BlueDragon",OwnerId=PlayerId.Player,HasCombatStats=true,Attack=5,OriginalAttack=5,Hp=40,MaxHp=40,OriginalMaxHp=40},
                    TargetPreview=healTarget,
                    TargetPreviews=new List<BattleCombatLogCardSnapshotDto>{healTarget}
                };
                Assert.That(singleDirected.TryGetDirectedPreview(out _,out var singleTargets),Is.True);
                Assert.That(singleTargets.Count,Is.EqualTo(1));
                overlay.Show(singleDirected);
                Assert.That(right.gameObject.activeSelf,Is.True);
                Assert.That(grid.gameObject.activeSelf,Is.False);
                Assert.That(swords.gameObject.activeSelf,Is.True);
                Assert.That(swords.Kind,Is.EqualTo(CombatLogSymbolGraphic.Symbol.RightTriangle));
                Assert.That(swords.mainTexture,Is.Not.Null);
                Assert.That(swords.mainTexture.name,Is.EqualTo("RightTriangle"));

                var rightMarker=overlay.transform.Find("CombatPair/Defender/ResultMarker");
                var rightMarkerGraphic=rightMarker.GetComponent<CombatLogSymbolGraphic>();
                var rightMarkerText=overlay.transform.Find("CombatPair/Defender/ResultMarker/Damage").GetComponent<TMP_Text>();
                Assert.That(rightMarker.gameObject.activeSelf,Is.True);
                Assert.That(rightMarkerGraphic.Kind,Is.EqualTo(CombatLogSymbolGraphic.Symbol.Heal));
                Assert.That(rightMarkerGraphic.mainTexture,Is.Not.Null);
                Assert.That(rightMarkerGraphic.mainTexture.name,Is.EqualTo("HealIcon"));
                Assert.That(rightMarkerText.text,Is.EqualTo("+15"));

                // 3. Multi-target (17 targets -> 5x4 grid, 5 per row, scaled by 5-card step)
                var multiTargets=new List<BattleCombatLogCardSnapshotDto>();
                for(var i=0;i<17;i++)
                {
                    multiTargets.Add(new BattleCombatLogCardSnapshotDto
                    {
                        CardId="Goblin",OwnerId=PlayerId.AI,HasCombatStats=true,
                        Attack=2,OriginalAttack=2,Hp=10,MaxHp=10,OriginalMaxHp=10,
                        DamageTaken=i%3==0?4:0,
                        HealAmount=i%3==1?3:0,
                        Died=i%3==2
                    });
                }
                var multiDirected=new BattleCombatLogEntryDto
                {
                    EntryType=BattleCombatLogEntryType.SpellCast,
                    SourceOwnerId=PlayerId.Player,
                    SourceCardId="Nuclear",
                    SourcePreview=BattleCombatLogCardSnapshotDto.CreateSpellCard("Nuclear",PlayerId.Player),
                    TargetPreview=multiTargets[0],
                    TargetPreviews=multiTargets
                };
                overlay.Show(multiDirected);
                Assert.That(right.gameObject.activeSelf,Is.False);
                Assert.That(grid.gameObject.activeSelf,Is.True);
                Assert.That(swords.gameObject.activeSelf,Is.True);
                Assert.That(swords.Kind,Is.EqualTo(CombatLogSymbolGraphic.Symbol.RightTriangle));
                Assert.That(CombatLogAttackPreview.GetTargetCardScaleForCount(3),Is.GreaterThan(CombatLogAttackPreview.GetTargetCardScaleForCount(8)));
                Assert.That(CombatLogAttackPreview.GetTargetCardScaleForCount(8),Is.GreaterThan(CombatLogAttackPreview.GetTargetCardScaleForCount(13)));
                Assert.That(CombatLogAttackPreview.GetTargetCardScaleForCount(13),Is.GreaterThan(CombatLogAttackPreview.GetTargetCardScaleForCount(17)));

                var activeGridCards=new List<RectTransform>();
                for(var i=0;i<grid.childCount;i++)
                {
                    var child=grid.GetChild(i) as RectTransform;
                    if(child!=null && child.gameObject.activeSelf)activeGridCards.Add(child);
                }
                Assert.That(activeGridCards.Count,Is.EqualTo(17));
                // First 5 cards on top row share same Y and strictly increasing X
                for(var col=0;col<5;col++)
                {
                    Assert.That(activeGridCards[col].anchoredPosition.y,Is.EqualTo(activeGridCards[0].anchoredPosition.y).Within(.01f));
                    if(col>0)Assert.That(activeGridCards[col].anchoredPosition.x,Is.GreaterThan(activeGridCards[col-1].anchoredPosition.x));
                }
                // Row 1 (index 5) is below Row 0 (index 0); Row 3 (index 15) has 2 cards
                Assert.That(activeGridCards[5].anchoredPosition.y,Is.LessThan(activeGridCards[0].anchoredPosition.y));
                Assert.That(activeGridCards[15].anchoredPosition.y,Is.LessThan(activeGridCards[10].anchoredPosition.y));

                // Markers on grid cards expire after 3 seconds
                var deadline=Get<float>(overlay,"_markersExpireAt");
                Assert.That(activeGridCards[0].Find("ResultMarker").gameObject.activeSelf,Is.True);
                Call(overlay,"UpdateMarkerVisibility",deadline);
                foreach(var card in activeGridCards)
                {
                    Assert.That(card.Find("ResultMarker").gameObject.activeSelf,Is.False);
                }

                // 4. TrapInstalled (#40) has no preview; TrapTriggered (#41) has directed preview
                var trapInstalled=new BattleCombatLogEntryDto{EntryType=BattleCombatLogEntryType.TrapInstalled,SourceOwnerId=PlayerId.Player};
                Assert.That(trapInstalled.HasHoldPreview,Is.False);
                var trapTarget=new BattleCombatLogCardSnapshotDto{CardId="Goblin",OwnerId=PlayerId.AI,HasCombatStats=true,Attack=2,OriginalAttack=2,Hp=5,MaxHp=5,OriginalMaxHp=5,DamageTaken=5,Died=true};
                var trapTriggered=new BattleCombatLogEntryDto
                {
                    EntryType=BattleCombatLogEntryType.TrapTriggered,
                    SourceOwnerId=PlayerId.Player,
                    SourceCardId="ExplosiveReactiveArmour",
                    SourcePreview=BattleCombatLogCardSnapshotDto.CreateSpellCard("ExplosiveReactiveArmour",PlayerId.Player),
                    TargetPreview=trapTarget,
                    TargetPreviews=new List<BattleCombatLogCardSnapshotDto>{trapTarget}
                };
                Assert.That(trapTriggered.HasHoldPreview,Is.True);
                Assert.That(trapTriggered.TryGetDirectedPreview(out var trapSrc,out var trapTargets),Is.True);
                Assert.That(trapSrc.CardId,Is.EqualTo("ExplosiveReactiveArmour"));
                Assert.That(trapTargets.Count,Is.EqualTo(1));
            }
            finally{Object.DestroyImmediate(overlay.gameObject);}
        }
        [Test]
        public void LocalRecorderShowsResourceSourcesAndHidesAiDrawnOrGeneratedCardsBehindCardBacks()
        {
            var b=Battle(PlayerId.Player);
            var log=new TutorialCombatLogRecorder(b);
            b.RecordResourceChange(new BattleResourceChangeEvent(PlayerId.Player,"ManaStone",new Project333.Runtime.Domain.Resources.ResourceSet(3,0,0,0)));
            b.RecordCardDraw(PlayerId.Player,"JarOfGreed","Goblin");b.RecordCardDraw(PlayerId.Player,"JarOfGreed","firebolt");
            b.RecordCardDraw(PlayerId.AI,"Zombie","Goblin");
            b.RecordCardGeneration(new BattleCardGenerationEvent(PlayerId.AI,"f-1","RobotFactory",new TileCoord(4,1),"A-111",true));
            var records=log.Capture(b);
            var gained=records.Single(x=>x.EntryType==BattleCombatLogEntryType.ResourceGained);
            Assert.That(gained.TryGetSingleCardPreview(out var gainedPreview,out _),Is.True);
            Assert.That(gainedPreview.CardId,Is.EqualTo("ManaStone"));Assert.That(gainedPreview.HasCombatStats,Is.False);
            var own=records.Single(x=>x.EntryType==BattleCombatLogEntryType.CardDrawn&&x.SourceOwnerId==PlayerId.Player);
            Assert.That(own.TryGetDirectedPreview(out var ownSource,out var ownTargets),Is.True);
            Assert.That(ownSource.CardId,Is.EqualTo("JarOfGreed"));
            Assert.That(ownTargets.Select(t=>t.CardId),Is.EqualTo(new[]{"Goblin","firebolt"}));
            var ai=records.Single(x=>x.EntryType==BattleCombatLogEntryType.CardDrawn&&x.SourceOwnerId==PlayerId.AI);
            Assert.That(ai.TryGetDirectedPreview(out _,out var aiTargets),Is.True);
            Assert.That(aiTargets.Single().CardId,Is.EqualTo(BattleCombatLogCardSnapshotDto.CardBackCardId));
            var generated=records.Single(x=>x.EntryType==BattleCombatLogEntryType.CardGenerated);
            Assert.That(generated.TargetCardId,Is.Empty);
            Assert.That(generated.TryGetDirectedPreview(out var genSource,out var genTargets),Is.True);
            Assert.That(genSource.CardId,Is.EqualTo("RobotFactory"));
            Assert.That(genTargets.Single().CardId,Is.EqualTo(BattleCombatLogCardSnapshotDto.CardBackCardId));
            Assert.That(genTargets.Single().HasCombatStats,Is.False);
        }
        [Test]
        public void CardBackAndNewSkullArtworkResolveForCombatLogPreviews()
        {
            Project333.Runtime.Presentation.Cards.CardArtworkLibrary.ClearCacheForTests();
            Assert.That(Project333.Runtime.Presentation.Cards.CardArtworkLibrary.TryGetArtwork(BattleCombatLogCardSnapshotDto.CardBackCardId,out var back),Is.True);
            Assert.That(back,Is.Not.Null);
            var skull=UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Project333/Resources/Project333/UI/CombatLog/Skull.png");
            Assert.That(skull,Is.Not.Null);
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID("Assets/Project333/Resources/Project333/UI/CombatLog/Skull.png"),Is.EqualTo("b5ff22930279cfb46a948783b7a926fa"));
        }
    }
}
