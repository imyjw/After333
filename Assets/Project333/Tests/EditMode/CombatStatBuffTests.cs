using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class CombatStatBuffTests
    {
        private static BattleState State()
        {
            var deck=Enumerable.Repeat("Goblin",33).ToArray();
            var state=new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck,deck,PlayerId.Player));
            state.SetPhase(PhaseType.Main);return state;
        }
        private static UnitState Unit(string id="Goblin",int col=0,PlayerId owner=PlayerId.Player) =>
            new UnitState(Guid.NewGuid().ToString(),id,owner,new TileCoord(col,0),AttackType.Melee,33,50,true,false,0,
                races:new[]{UnitRace.Robot});
        private static ICardDefinitionProvider Provider() =>
            JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).CreateProvider();

        [Test]
        public void SourceRecords_MergeSameSource_KeepDifferentSources_IgnoreOrdinaryDamage()
        {
            var unit=Unit();
            unit.ApplyCombatStatBuff("Hero",attackBonus:33);
            unit.ApplyCombatStatBuff("Hero",maxHpBonus:33);
            unit.ApplyCombatStatBuff("RobotFusion",attackBonus:5,maxHpBonus:7);
            unit.CurrentHp-=20;
            Assert.That(unit.CombatStatBuffs.Count,Is.EqualTo(2));
            var hero=unit.CombatStatBuffs.Single(b=>b.SourceCardId=="Hero");
            Assert.That(hero.AttackBonus,Is.EqualTo(33));Assert.That(hero.MaxHpBonus,Is.EqualTo(33));
            Assert.That(unit.BaseAttack-unit.OriginalAttack,Is.EqualTo(38));
            Assert.That(unit.MaxHp-unit.OriginalMaxHp,Is.EqualTo(40));
        }

        [Test]
        public void Erasure_RemovesOldRecords_AllowsNewBuffs_PreservesUpgradeBaseline()
        {
            var unit=Unit(); // original 33/50 includes whatever level was used at summon.
            unit.ApplyCombatStatBuff("RobotFusion",attackBonus:20,maxHpBonus:10);
            unit.ApplyErasure();
            Assert.That(unit.CombatStatBuffs,Is.Empty);
            Assert.That(unit.BaseAttack,Is.EqualTo(33));Assert.That(unit.MaxHp,Is.EqualTo(50));
            unit.ApplyCombatStatBuff("RobotFusion",attackBonus:3,maxHpBonus:4);
            Assert.That(unit.CombatStatBuffs.Single().AttackBonus,Is.EqualTo(3));
            Assert.That(unit.MaxHp,Is.EqualTo(54));
            unit.IsDrained=true; // Erasure blocks Drained, not the subsequent buff.
            Assert.That(unit.CombatStatBuffs.Count,Is.EqualTo(1));
        }

        [Test]
        public void Sealed_DoesNotApplyOrRecordNewBonuses()
        {
            var unit=Unit();unit.EnterSealbound(2);
            unit.ApplyCombatStatBuff("Hero",attackBonus:33,maxHpBonus:33);
            Assert.That(unit.BaseAttack,Is.EqualTo(33));Assert.That(unit.CombatStatBuffs,Is.Empty);
        }

        [TestCase(PlayerId.Player)][TestCase(PlayerId.AI)]
        public void OwnershipProjectionCloneAndRestore_DoNotReapplyOrLoseBonuses(PlayerId viewer)
        {
            var state=State();var unit=Unit();
            unit.ApplyCombatStatBuff("RobotFusion",attackBonus:7,maxHpBonus:11);
            unit.TransferOwnership(PlayerId.AI);state.AIBoard.Place(unit.Position,unit);
            var clone=AiBattleStateCopy.Create(state);
            var cloned=clone.AIBoard.GetOccupant(unit.Position);
            cloned.ApplyCombatStatBuff("RobotFusion",attackBonus:5);
            Assert.That(unit.CombatStatBuffs.Single().AttackBonus,Is.EqualTo(7));
            var view=new BattleStateViewFactory().CreateForPlayer(state,"buffs",viewer);
            var projected=BattleStateViewProjector.CreateLocalPerspectiveState(view);
            var restored=projected.GetBoard(viewer==PlayerId.AI?PlayerId.Player:PlayerId.AI).GetOccupant(unit.Position);
            Assert.That(restored.BaseAttack,Is.EqualTo(40));Assert.That(restored.MaxHp,Is.EqualTo(61));
            Assert.That(restored.CombatStatBuffs.Single().MaxHpBonus,Is.EqualTo(11));
            var before=restored.BaseAttack;
            restored.RestoreCombatStatBuffs(restored.CombatStatBuffs);
            Assert.That(restored.BaseAttack,Is.EqualTo(before));
        }

        [Test]
        public void HeroAttack_RecordsActualRandomResult()
        {
            var state=State();var hero=Unit("Hero");hero.HasSummoningSickness=false;
            var victim=Unit(owner:PlayerId.AI);victim.CurrentHp=1;
            state.PlayerBoard.Place(hero.Position,hero);state.AIBoard.Place(victim.Position,victim);
            new AttackService(new TargetingService(),_=>0).Attack(state,PlayerId.Player,hero.Position,victim.Position);
            var buff=hero.CombatStatBuffs.Single();
            Assert.That(buff.SourceCardId,Is.EqualTo("Hero"));
            Assert.That(buff.AttackBonus+buff.MaxHpBonus,Is.EqualTo(33));
        }

        [Test]
        public void Fusion_RecordsAbsorbedCurrentStatsInsteadOfOriginalMaximum()
        {
            var state=State();var survivor=Unit("A-111");var material=Unit("A-111",1);
            material.CurrentHp=7;material.IncreaseBaseAttack(2);
            state.PlayerBoard.Place(survivor.Position,survivor);state.PlayerBoard.Place(material.Position,material);
            RobotFusionRules.Resolve(state,PlayerId.Player,new[]{survivor.Position,material.Position});
            var buff=survivor.CombatStatBuffs.Single();
            Assert.That(buff.SourceCardId,Is.EqualTo("RobotFusion"));
            Assert.That(buff.AttackBonus,Is.EqualTo(35));Assert.That(buff.MaxHpBonus,Is.EqualTo(7));
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void Daehwandan_RecordsMasterAttackSeparatelyFromResourceEffect(PlayerId viewer)
        {
            var state=State();state.Player.Hand.Add("Daehwandan");
            state.Player.Resources.Add(new ResourceSet(0,5,0,0));
            new SpellService(Provider()).CastScriptedSpell(state,PlayerId.Player,"Daehwandan");
            var buff=state.Player.Master.CombatStatBuffs.Single();
            Assert.That(buff.SourceCardId,Is.EqualTo("Daehwandan"));
            Assert.That(buff.AttackBonus,Is.EqualTo(30));
            AssertMasterTooltip();
            state.PersistentEffects.Single().Expire();
            Assert.That(state.Player.Master.CombatStatBuffs.Single().AttackBonus,Is.EqualTo(30));
            AssertMasterTooltip();

            void AssertMasterTooltip()
            {
                var view = new BattleStateViewFactory().CreateForPlayer(state, "daehwandan", viewer);
                var received = JsonConvert.DeserializeObject<BattleStateViewDto>(JsonConvert.SerializeObject(view));
                var projected = BattleStateViewProjector.CreateLocalPerspectiveState(received);
                var owner = viewer == PlayerId.Player ? PlayerId.Player : PlayerId.AI;
                var master = projected.GetPlayer(owner).Master;
                Assert.That(master.BaseAttack, Is.EqualTo(master.OriginalAttack + 30));
                Assert.That(master.CombatStatBuffs.Single().SourceCardId, Is.EqualTo("Daehwandan"));
                Assert.That(OccupantStatusTooltipCatalog.BuildAttached(null, master, projected).Select(e => e.Title),
                    Is.EquivalentTo(new[] { "대환단: 공격력 +30" }));
            }
        }

        [Test]
        public void DemonRevival_ReplacesOldModifiers_AndErasureDoesNotInferFromRevivalCount()
        {
            var king=Unit("DemonKing");
            king.ApplyCombatStatBuff("RobotFusion",attackBonus:20,maxHpBonus:20);
            Assert.That(king.TryEnterDemonKingRevivalSeal(PlayerId.Player,1),Is.True);
            for(var i=2;i<=4;i++)king.ResolveDemonKingRevivalTurnStart(PlayerId.Player,i);
            var buff=king.CombatStatBuffs.Single();
            Assert.That(buff.SourceCardId,Is.EqualTo("DemonKing"));
            Assert.That(buff.AttackBonus,Is.EqualTo(33));Assert.That(buff.MaxHpBonus,Is.EqualTo(33));
            king.ApplyErasure();
            Assert.That(king.DemonKingRevivalCount,Is.EqualTo(1));
            Assert.That(king.CombatStatBuffs,Is.Empty);
        }
    }
}
