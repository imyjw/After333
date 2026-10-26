using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
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
    public sealed class CounterattackIdleRecoveryTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly TileCoord Coord = new TileCoord(0,0);
        [UnityTearDown] public IEnumerator Cleanup() {Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}

        [UnityTest] public IEnumerator BlockedHitWithLethalCounterReturnsSurvivingOrcToIdle()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();Assert.That(Application.isPlaying,Is.True);
            using(var f=new Fixture(PlayerId.Player))
            {
                yield return null;
                var request=f.Request(incomingDamage:0,attackerDied:true,defenderDied:false,invincible:false);
                Assert.That(Flag(request,"DefenderCounterattacked"),Is.True);
                yield return f.Sequence(request);
                Assert.That(f.DefenderAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),Is.True,"Blocked incoming hit still needs counterattacker Idle cleanup.");
                f.AssertDisplayedFrame("Idle");
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(f.DefenderAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),Is.True);
            }
        }

        [UnityTest] public IEnumerator InvincibleHitWithNonlethalCounterReturnsSurvivingOrcToIdle()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();Assert.That(Application.isPlaying,Is.True);
            using(var f=new Fixture(PlayerId.AI))
            {
                yield return null;
                yield return f.Sequence(f.Request(0,false,false,true));
                Assert.That(f.DefenderAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),Is.True,"Invincible feedback is not a damaged-defender impact.");
                f.AssertDisplayedFrame("Idle");
            }
        }

        [UnityTest] public IEnumerator DamagedSurvivorAndDeadDefenderKeepTheirExpectedFinalStates()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();Assert.That(Application.isPlaying,Is.True);
            foreach(var died in new[]{false,true})
            using(var f=new Fixture(PlayerId.Player))
            {
                yield return null;
                yield return f.Sequence(f.Request(5,false,died,false));
                Assert.That(f.DefenderAnimator.GetCurrentAnimatorStateInfo(0).IsName(died?"Death":"Idle"),Is.True,"Never reset a dead counterattacker to Idle.");
                f.AssertDisplayedFrame(died?"Death":"Idle");
            }
        }

        private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Hidden).SetValue(target,value);
        private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Hidden).Invoke(target,args);
        private static bool Flag(object request,string name)=>(bool)request.GetType().GetProperty(name).GetValue(request);
        private sealed class Fixture:IDisposable
        {
            internal readonly GameObject Root;
            internal readonly BattleBootstrapper Boot;
            internal readonly Animator DefenderAnimator;
            private readonly PlayerId _defenderOwner,_attackerOwner;
            private readonly TileTextView _defender;
            private readonly BattleState _battle;
            internal Fixture(PlayerId defenderOwner)
            {
                _defenderOwner=defenderOwner;_attackerOwner=defenderOwner==PlayerId.Player?PlayerId.AI:PlayerId.Player;
                Root=new GameObject("CounterIdleFixture",typeof(RectTransform),typeof(Canvas));Root.SetActive(false);
                Root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                var driver=new GameObject("PassiveDriver");driver.transform.SetParent(Root.transform,false);driver.SetActive(false);
                Boot=driver.AddComponent<BattleBootstrapper>();var screen=driver.AddComponent<BattleScreenPresenter>();var board=driver.AddComponent<BoardPresenter>();
                Set(Boot,"_battleScreenPresenter",screen);Set(screen,"_boardPresenter",board);
                Set(Boot,"_autoRunAiTurns",false);Set(Boot,"_sendPlayerActionsToOnlineGateway",false);
                foreach(var field in new[]{"_attackRunDuration","_attackImpactDelay","_attackReactionDuration","_deathHoldDuration","_rangedAttackNudgeDuration"})Set(Boot,field,0f);
                var catalog=AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>("Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset");Assert.That(catalog,Is.Not.Null);
                Set(Boot,"_cardCatalogAsset",catalog);
                var flow=new BattleFlowController();var gateway=new LocalBattleGateway(flow);var deck=Enumerable.Repeat("unknown",20).ToArray();
                _battle=gateway.StartBattle(new BattleSetupRequest(deck,deck,PlayerId.Player));_battle.RestoreRuntimeState(30,_attackerOwner,PhaseType.Main);Set(Boot,"_battleGateway",gateway);
                var vampire=new UnitState("test-vampire","Vampire",_attackerOwner,Coord,AttackType.Melee,20,15,true,false,0);
                var orc=new UnitState("test-orc","OrcChieftain",_defenderOwner,Coord,AttackType.Melee,60,35,true,false,0,physicalDefense:30);
                _battle.GetBoard(_attackerOwner).Place(Coord,vampire);_battle.GetBoard(_defenderOwner).Place(Coord,orc);
                var attacker=Tile(vampire);_defender=Tile(orc);
                Set(board,"_playerTileViews",new[]{(_attackerOwner==PlayerId.Player?attacker:_defender).GetComponent<TileView>()});
                Set(board,"_aiTileViews",new[]{(_attackerOwner==PlayerId.AI?attacker:_defender).GetComponent<TileView>()});
                Root.SetActive(true);DefenderAnimator=_defender.GetComponentInChildren<Animator>();
            }
            private TileTextView Tile(UnitState unit)
            {
                var node=new GameObject(unit.CardId,typeof(RectTransform));node.transform.SetParent(Root.transform,false);
                node.GetComponent<RectTransform>().sizeDelta=new Vector2(200,200);
                node.GetComponent<RectTransform>().anchoredPosition=new Vector2(unit.OwnerId==PlayerId.Player?-250:250,0);
                var tile=node.AddComponent<TileView>();tile.Configure(unit.OwnerId,Coord.Column,Coord.Row);tile.Present(unit);
                var view=node.AddComponent<TileTextView>();var visual=new GameObject("OccupantVisual",typeof(RectTransform),typeof(Image),typeof(Animator),typeof(SpriteRenderer));visual.transform.SetParent(node.transform,false);
                Set(view,"_tileView",tile);Set(view,"_battleBootstrapper",Boot);Set(view,"_showOccupantStats",false);Set(view,"_showHuanShuStatusIcon",false);Set(view,"_useTileImageClickSurface",false);
                Set(view,"_occupantVisualRoot",visual.GetComponent<RectTransform>());Set(view,"_occupantVisualImage",visual.GetComponent<Image>());
                Set(view,"_occupantVisualAnimator",visual.GetComponent<Animator>());Set(view,"_occupantVisualSpriteRenderer",visual.GetComponent<SpriteRenderer>());
                view.SetTile(unit.CardId,unit.CardId,true);return view;
            }
            internal object Request(int incomingDamage,bool attackerDied,bool defenderDied,bool invincible)
            {
                var attack=new BattleEventDto{EventType=BattleEventType.AttackStarted,SourceOwnerId=_attackerOwner,SourceCoord=TileCoordDto.FromDomain(Coord),TargetOwnerId=_defenderOwner,TargetCoord=TileCoordDto.FromDomain(Coord)};
                var events=new List<BattleEventDto>();
                if(incomingDamage>0 || invincible)events.Add(new BattleEventDto{EventType=invincible?BattleEventType.InvinciblePrevented:BattleEventType.DamageApplied,
                    SourceOwnerId=_attackerOwner,TargetOwnerId=_defenderOwner,TargetCoord=TileCoordDto.FromDomain(Coord),RuntimeId="test-orc",SourceRuntimeId="test-vampire",SourceCardId="Vampire",ValueCause=BattleValueChangeCause.NormalAttack,DamageType=DamageType.Physical,Amount=incomingDamage,HpBefore=35,HpAfter=35-incomingDamage});
                events.Add(new BattleEventDto{EventType=BattleEventType.DamageApplied,SourceOwnerId=_defenderOwner,TargetOwnerId=_attackerOwner,TargetCoord=TileCoordDto.FromDomain(Coord),RuntimeId="test-vampire",SourceRuntimeId="test-orc",SourceCardId="OrcChieftain",ValueCause=BattleValueChangeCause.Counterattack,DamageType=DamageType.Physical,Amount=attackerDied?15:14,HpBefore=15,HpAfter=attackerDied?0:1});
                if(attackerDied)events.Add(new BattleEventDto{EventType=BattleEventType.OccupantRemoved,TargetOwnerId=_attackerOwner,TargetCoord=TileCoordDto.FromDomain(Coord),RuntimeId="test-vampire"});
                if(defenderDied)events.Add(new BattleEventDto{EventType=BattleEventType.OccupantRemoved,TargetOwnerId=_defenderOwner,TargetCoord=TileCoordDto.FromDomain(Coord),RuntimeId="test-orc"});
                return Call(Boot,"BuildOnlineAttackAnimationRequest",attack,events);
            }
            internal void AssertDisplayedFrame(string state)
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Unit/OrcChieftian/"+state+".anim");
                var frames=AnimationUtility.GetObjectReferenceCurveBindings(clip)
                    .Where(binding=>binding.propertyName=="m_Sprite")
                    .SelectMany(binding=>AnimationUtility.GetObjectReferenceCurve(clip,binding)).Select(key=>key.value).ToArray();
                var image=(Image)typeof(TileTextView).GetField("_occupantVisualImage",Hidden).GetValue(_defender);
                Assert.That(image.sprite,Is.Not.Null);Assert.That(frames,Does.Contain(image.sprite),"Displayed Orc frame must come from the final "+state+" clip.");
            }
            internal IEnumerator Sequence(object request)=>(IEnumerator)Call(Boot,"PlayAttackAnimationSequence",request);
            public void Dispose(){Object.DestroyImmediate(Root);}
        }
    }
}
