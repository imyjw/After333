using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using UnityEditor;

namespace Project333.Tests.EditMode
{
    public sealed class FirewallImmediateTests
    {
        private static ICardDefinitionProvider Provider() => JsonCardDefinitionDatabase.FromJson(
            File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).CreateProvider();

        [TestCase(PlayerId.Player,PlayerId.Player)] [TestCase(PlayerId.Player,PlayerId.AI)]
        [TestCase(PlayerId.AI,PlayerId.Player)] [TestCase(PlayerId.AI,PlayerId.AI)]
        public void CastHitsSelectedRowImmediatelyAndAtExactlyTwoLaterGlobalStarts(PlayerId caster, PlayerId owner)
        {
            var state=State(caster); var target=Add(state,owner,0,0);var other=Add(state,owner,0,1);
            var mana=state.GetPlayer(caster).Resources.Mana;
            Cast(state,caster,owner,0);
            Assert.That(target.CurrentHp,Is.EqualTo(85));Assert.That(other.CurrentHp,Is.EqualTo(100));
            Assert.That(state.GetPlayer(caster).Resources.Mana,Is.EqualTo(mana-4));
            Assert.That(state.GetPlayer(caster).Hand.Contains("Firewall"),Is.False);
            Assert.That(state.PersistentEffects.Single().RemainingTriggers,Is.EqualTo(2));
            Assert.That(state.AreaSpellEffectEvents,Is.Empty);
            Assert.That(state.ValuePopupEvents.Single().Cause,Is.EqualTo(BattleValueChangeCause.Firewall));
            for(var i=0;i<3;i++)
            {
                Tick(state,i+2,i%2==0?state.GetOpponent(caster).Id:caster);
                Assert.That(target.CurrentHp,Is.EqualTo(i==0?70:55));
                Assert.That(state.AreaSpellEffectEvents.Count,Is.EqualTo(i<2?1:0));
            }
            Assert.That(state.PersistentEffects.Single().IsExpired,Is.True);
        }

        [Test]
        public void UpgradeDamageIsStoredButSpellPowerIsReadAgainForEveryHit()
        {
            var state=State();var target=Add(state,PlayerId.AI,0,0,200);
            var upgrades=new InMemoryCardUpgradeLevelProvider();upgrades.SetUpgradeLevel(PlayerId.Player,"Firewall",13);
            new SpellService(Provider(),upgrades).CastScriptedSpell(state,PlayerId.Player,"Firewall",PlayerId.AI,new TileCoord(2,0));
            Assert.That(target.CurrentHp,Is.EqualTo(172));
            var buff=new UnitState("spell-power","ManaWeaver",PlayerId.Player,new TileCoord(0,0),AttackType.Ranged,1,100,true,false,0,spellPower:20);
            state.PlayerBoard.Place(buff.Position,buff);
            Tick(state,2,PlayerId.AI);Assert.That(target.CurrentHp,Is.EqualTo(124));
            buff.ApplyErasure();
            Tick(state,3,PlayerId.Player);Assert.That(target.CurrentHp,Is.EqualTo(96));
        }

        [Test]
        public void EmptyRowStillPersistsAndNewOccupantsOnlyReceiveRemainingHits()
        {
            var state=State();Cast(state,PlayerId.Player,PlayerId.AI,0);
            Assert.That(state.ValuePopupEvents,Is.Empty);
            var target=Add(state,PlayerId.AI,4,0);
            Tick(state,2,PlayerId.AI);Assert.That(target.CurrentHp,Is.EqualTo(85));
            state.AIBoard.Remove(target.Position);target.Position=new TileCoord(4,1);state.AIBoard.Place(target.Position,target);
            Tick(state,3,PlayerId.Player);Assert.That(target.CurrentHp,Is.EqualTo(85));
        }

        [Test]
        public void ImmediateDamageRespectsMagicDefenseAndSealbound()
        {
            var state=State();var defended=Add(state,PlayerId.AI,0,0,100,magicDefense:3);
            var sealedTarget=Add(state,PlayerId.AI,1,0);sealedTarget.EnterSealbound(2);
            Cast(state,PlayerId.Player,PlayerId.AI,0);
            Assert.That(defended.CurrentHp,Is.EqualTo(88));Assert.That(sealedTarget.CurrentHp,Is.EqualTo(100));
            Assert.That(state.ValuePopupEvents.Single().RuntimeId,Is.EqualTo(defended.RuntimeId));
        }

        [Test]
        public void ImmediateKillTriggersLegacyOnceAndDeadUnitPaysNoLaterUpkeep()
        {
            var state=State();var target=new UnitState("legacy-victim","Skeleton",PlayerId.AI,new TileCoord(0,0),
                AttackType.Melee,3,10,true,true,1);
            state.AIBoard.Place(target.Position,target);state.AI.Resources.Add(new ResourceSet(0,0,1,0));
            var hand=state.AI.Hand.Count;Cast(state,PlayerId.Player,PlayerId.AI,0);
            Assert.That(state.AIBoard.GetOccupant(target.Position),Is.Null);
            Assert.That(state.AI.Hand.Count,Is.EqualTo(hand+1));
            Assert.That(state.CardDrawEvents.Single().SourceCardId,Is.EqualTo("Skeleton"));
            Tick(state,2,PlayerId.AI);Assert.That(state.AI.Resources.Power,Is.EqualTo(1));
        }

        [TestCase(PlayerId.Player)] [TestCase(PlayerId.AI)]
        public void ImmediateMasterKillEndsBattleWithoutWaitingForAnotherTurn(PlayerId owner)
        {
            var state=State();state.GetPlayer(owner).Master.CurrentHp=15;
            Cast(state,PlayerId.Player,owner,state.GetPlayer(owner).Master.Position.Row);
            Assert.That(state.IsEnded,Is.True);Assert.That(state.Result.Winner,Is.EqualTo(state.GetOpponent(owner).Id));
        }

        [Test]
        public void ThreeTotalResourcesCannotCastAndFailureIsAtomic()
        {
            var state=State();state.Player.Resources.Spend(new ResourceSet(4,0,0,0));
            // BattleSetup supplies 3 Gold. It cannot cover the new four-mana cost.
            var target=Add(state,PlayerId.AI,0,0);
            Assert.Throws<InvalidOperationException>(()=>Cast(state,PlayerId.Player,PlayerId.AI,0));
            Assert.That(state.Player.Hand.Contains("Firewall"),Is.True);
            Assert.That(state.Player.Resources.Gold,Is.EqualTo(3));
            Assert.That(target.CurrentHp,Is.EqualTo(100));Assert.That(state.PersistentEffects,Is.Empty);
        }

        [Test]
        public void JsonNativeAndPreviewAgreeOnCostAndExactDescription()
        {
            var definition=(ScriptedSpellCardDefinition)Provider().GetRequired("Firewall");
            var native=AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>("Assets/Project333/ScriptableObjects/StarterTen/Cards/Firewall.asset");
            var nativeDefinition=(ScriptedSpellCardDefinition)native.ToDefinition();
            var record=JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).Cards.Single(c=>c.Id=="Firewall");
            Assert.That(record.EffectText,Is.EqualTo(FirewallRules.EffectDescription));
            Assert.That(record.SpecialEffectText,Is.EqualTo(FirewallRules.EffectDescription));
            Assert.That(native.EffectText,Is.EqualTo(FirewallRules.EffectDescription));
            Assert.That(native.SpecialEffectText,Is.EqualTo(FirewallRules.EffectDescription));
            foreach(var card in new[]{definition,nativeDefinition})
            {
                Assert.That(card.Cost.Mana,Is.EqualTo(4));Assert.That(card.Cost.Gold+card.Cost.Power+card.Cost.Qi,Is.Zero);
                Assert.That(card.Damage,Is.EqualTo(15));Assert.That(card.TriggerCount,Is.EqualTo(2));
                var tooltip=OccupantSpecialEffectTooltipCatalog.Build(card).Single(e=>e.Title=="파이어월");
                Assert.That(tooltip.Description,Is.EqualTo(FirewallRules.EffectDescription));
            }
        }

        private static BattleState State(PlayerId caster=PlayerId.Player)
        {
            var deck=Enumerable.Repeat("Goblin",33).ToArray();
            var state=new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck,deck,PlayerId.Player));
            state.RestoreRuntimeState(1,caster,PhaseType.Main);state.ClearCardDrawEvents();
            state.GetPlayer(caster).Hand.Add("Firewall");state.GetPlayer(caster).Resources.Add(new ResourceSet(4,0,0,0));
            return state;
        }
        private static UnitState Add(BattleState state,PlayerId owner,int column,int row,int hp=100,int magicDefense=0)
        {
            var target=new UnitState(Guid.NewGuid().ToString("N"),"test-target",owner,new TileCoord(column,row),
                AttackType.Melee,1,hp,true,false,0,magicDefense:magicDefense);
            state.GetBoard(owner).Place(target.Position,target);return target;
        }
        private static void Cast(BattleState state,PlayerId caster,PlayerId owner,int row) =>
            new SpellService(Provider()).CastScriptedSpell(state,caster,"Firewall",owner,new TileCoord(2,row));
        private static void Tick(BattleState state,int turn,PlayerId active)
        {
            state.ClearValuePopupEvents();state.RestoreRuntimeState(turn,active,PhaseType.TurnStart);
            new TurnStartService().ResolveTurnStart(state);
        }
    }
}
