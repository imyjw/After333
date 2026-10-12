using System;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;
namespace Project333.Tests.EditMode
{
    public sealed class TutorialCombatLogRecorderTests
    {
        private static ICardDefinitionProvider Provider() => AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(
            "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset").CreateProvider();
        private static string Name(string id) => id == "Master" ? "마스터" : Provider().GetRequired(id).DisplayName;
        private static string Format(BattleCombatLogEntryDto record) => BattleCombatLogEntryFormatter.Format(record, Name);
        [Test]
        public void BlockedAttackProducesNoSuccessRecordAndAuthoredStagingIsNotInventedHistory()
        {
            var p=Provider();var g=TutorialCombatBattle.Create(p);var b=g.CurrentBattleState;var log=new TutorialCombatLogRecorder(b);
            TutorialCombatBattle.PrepareFrontline(b,p);log.Rebase(b);
            Assert.Throws<InvalidOperationException>(()=>g.ExecuteCommand(PlayerId.Player,new AttackCommand(TutorialResourceBattle.GoblinTile,TutorialResourceBattle.MasterTile)));
            Assert.That(log.Capture(b),Is.Empty);Assert.That(log.Capture(b),Is.Empty);
            TutorialCombatBattle.EmptyPracticeDeck(b);TutorialCombatBattle.PrepareVictory(b);log.Rebase(b);Assert.That(log.Capture(b),Is.Empty);
        }
        [Test]
        public void SummonTurnIncomeAndPhysicalRangedAttackUseOnlineRecordFieldsWithoutClearingEvents()
        {
            var g=TutorialCombatBattle.Create(Provider());var b=g.CurrentBattleState;var log=new TutorialCombatLogRecorder(b);
            g.ExecuteCommand(PlayerId.Player,new PlayUnitCardCommand("SkeletonArcher",TutorialCombatBattle.ArcherTile));
            var summoned=log.Capture(b).Single();Assert.That(summoned.EntryType,Is.EqualTo(BattleCombatLogEntryType.UnitSummoned));
            Assert.That(Format(summoned),Is.EqualTo("플레이어가 스켈레톤아처를 (2,0)에 소환"));
            g.EndTurn();Assert.That(log.Capture(b).Single().EntryType,Is.EqualTo(BattleCombatLogEntryType.TurnEnded));
            g.ResolveTurnStart();log.Capture(b);g.EndTurn();log.Capture(b);g.ResolveTurnStart();
            var income=log.Capture(b);Assert.That(income[0].EntryType,Is.EqualTo(BattleCombatLogEntryType.TurnStarted));
            Assert.That(income.Single(x=>x.EntryType==BattleCombatLogEntryType.ResourceGained).SourceCardId,Is.EqualTo("ManaPond"));
            Assert.That(Format(income.Single(x=>x.EntryType==BattleCombatLogEntryType.ResourceGained)),Is.EqualTo("마나의 샘 효과로 플레이어 마나 +3"));
            g.ExecuteCommand(PlayerId.Player,new AttackCommand(TutorialCombatBattle.ArcherTile,TutorialResourceBattle.MasterTile));
            var popupCount=b.ValuePopupEvents.Count;var actual=log.Capture(b).Single();
            var expected=new BattleCombatLogEntryDto { EntryType=BattleCombatLogEntryType.Attack,SourceOwnerId=PlayerId.Player,TargetOwnerId=PlayerId.AI,
                SourceCardId="SkeletonArcher",TargetCardId="Master",AttackType=Project333.Runtime.Domain.Cards.AttackType.Ranged,
                DamageType=Project333.Runtime.Domain.Cards.DamageType.Physical,TargetHpHistory=new System.Collections.Generic.List<int>{333,323} };
            Assert.That(Format(actual),Is.EqualTo(Format(expected)));Assert.That(b.ValuePopupEvents.Count,Is.EqualTo(popupCount));Assert.That(log.Capture(b),Is.Empty);
        }
        [Test]
        public void SimultaneousCounterattackRecordsBothDeathHistoriesAndSpellUsesActualThirtyDamage()
        {
            var p=Provider();var g=TutorialCombatBattle.Create(p);var b=g.CurrentBattleState;
            TutorialCombatBattle.PrepareFrontline(b,p);g.EndTurn();g.ResolveTurnStart();
            g.ExecuteCommand(PlayerId.AI,TutorialCombatBattle.OpponentSummon(b,false));g.EndTurn();g.ResolveTurnStart();
            var log=new TutorialCombatLogRecorder(b);g.ExecuteCommand(PlayerId.Player,new AttackCommand(TutorialResourceBattle.GoblinTile,TutorialCombatBattle.EnemyGoblinTile));
            var attack=log.Capture(b).Single();Assert.That(attack.HasCounterattack,Is.True);Assert.That(attack.SourceHpHistory,Is.EqualTo(new[]{20,0}));Assert.That(attack.TargetHpHistory,Is.EqualTo(new[]{20,0}));Assert.That(Format(attack),Does.Contain("[반격함]"));Assert.That(Format(attack),Does.Contain("처치됨"));
            g.ExecuteCommand(PlayerId.Player,new CastDamageSpellCommand("firebolt",PlayerId.AI,TutorialResourceBattle.MasterTile));var spell=log.Capture(b);
            Assert.That(spell[0].EntryType,Is.EqualTo(BattleCombatLogEntryType.SpellCast));Assert.That(Format(spell[0]),Is.EqualTo("플레이어가 파이어볼 마법카드를 사용했다."));
            var damage=spell.Single(x=>x.EntryType==BattleCombatLogEntryType.Damage);Assert.That(damage.Amount,Is.EqualTo(30));Assert.That(damage.TargetHpHistory,Is.EqualTo(new[]{333,303}));Assert.That(damage.ValueCause,Is.EqualTo(BattleValueChangeCause.Spell));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void SwapProducesOneOnlineStyleRecordWithoutDuplicateMoveRecords(bool masterFirst)
        {
            var g=TutorialCombatBattle.Create(Provider());var b=g.CurrentBattleState;
            g.ExecuteCommand(PlayerId.Player,new PlayUnitCardCommand("SkeletonArcher",TutorialCombatBattle.ArcherTile));
            g.ExecuteCommand(PlayerId.Player,new MoveOccupantCommand(TutorialCombatBattle.ArcherTile,TutorialCombatBattle.MovedArcherTile));
            var log=new TutorialCombatLogRecorder(b);
            g.ExecuteCommand(PlayerId.Player,new MoveOccupantCommand(
                masterFirst ? TutorialResourceBattle.MasterTile : TutorialCombatBattle.MovedArcherTile,
                masterFirst ? TutorialCombatBattle.MovedArcherTile : TutorialResourceBattle.MasterTile));
            var entry=log.Capture(b).Single();
            Assert.That(entry.EntryType,Is.EqualTo(BattleCombatLogEntryType.OccupantsSwapped));
            Assert.That(new[]{entry.SourceCardId,entry.TargetCardId},Is.EquivalentTo(new[]{"master","SkeletonArcher"}));
            Assert.That(Format(entry),Does.Contain("위치를 교환"));
            Assert.That(log.Capture(b),Is.Empty);
        }
        [Test]
        public void MoveFatigueAndVictoryUseActualHpAndProduceEachRecordOnce()
        {
            var g=TutorialCombatBattle.Create(Provider());var b=g.CurrentBattleState;
            g.ExecuteCommand(PlayerId.Player,new PlayUnitCardCommand("SkeletonArcher",TutorialCombatBattle.ArcherTile));var log=new TutorialCombatLogRecorder(b);
            g.ExecuteCommand(PlayerId.Player,new MoveOccupantCommand(TutorialCombatBattle.ArcherTile,TutorialCombatBattle.MovedArcherTile));Assert.That(log.Capture(b).Single().EntryType,Is.EqualTo(BattleCombatLogEntryType.OccupantMoved));
            TutorialCombatBattle.EmptyPracticeDeck(b);log.Rebase(b);g.EndTurn();log.Capture(b);g.ResolveTurnStart();log.Capture(b);g.EndTurn();log.Capture(b);g.ResolveTurnStart();
            var records=log.Capture(b);Assert.That(records[0].EntryType,Is.EqualTo(BattleCombatLogEntryType.TurnStarted));
            var resourceIndex=records.ToList().FindIndex(x=>x.EntryType==BattleCombatLogEntryType.ResourceGained);
            var fatigueIndex=records.ToList().FindIndex(x=>x.ValueCause==BattleValueChangeCause.DeckExhaustion);Assert.That(resourceIndex,Is.LessThan(fatigueIndex));
            var fatigue=records[fatigueIndex];Assert.That(fatigue.Amount,Is.EqualTo(3));Assert.That(fatigue.TargetHpHistory,Is.EqualTo(new[]{333,330}));Assert.That(Format(fatigue),Does.Contain("덱 고갈로 3의 고정 피해"));
            TutorialCombatBattle.PrepareVictory(b);log.Rebase(b);g.ExecuteCommand(PlayerId.Player,new AttackCommand(TutorialCombatBattle.MovedArcherTile,TutorialResourceBattle.MasterTile));var win=log.Capture(b);Assert.That(win.Last().EntryType,Is.EqualTo(BattleCombatLogEntryType.BattleEnded));Assert.That(Format(win.Last()),Is.EqualTo("전투 종료: 플레이어 승리"));Assert.That(log.Capture(b),Is.Empty);
        }
    }
}
