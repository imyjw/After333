using System;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;

namespace Project333.Tests.EditMode
{
    public sealed class TutorialCombatBattleTests
    {
        private static ICardDefinitionProvider Provider() => AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(
            "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset").CreateProvider();
        private static void OpponentTurn(LocalBattleGateway gateway, bool golem)
        {
            gateway.EndTurn(); gateway.ResolveTurnStart();
            gateway.ExecuteCommand(PlayerId.AI, TutorialCombatBattle.OpponentSummon(gateway.CurrentBattleState, golem));
            gateway.EndTurn(); gateway.ResolveTurnStart();
        }
        [Test]
        public void DirectEntryReplaysChapterFourAndKeepsLevelZeroResourcesAndHand()
        {
            for (var i=0; i<2; i++)
            {
                var battle = TutorialCombatBattle.Create(Provider()).CurrentBattleState;
                Assert.That(battle.TurnNumber, Is.EqualTo(3)); Assert.That(battle.Phase, Is.EqualTo(PhaseType.Main));
                Assert.That(battle.Player.Resources.Gold, Is.EqualTo(6)); Assert.That(battle.Player.Resources.Mana, Is.EqualTo(3));
                Assert.That(battle.Player.Hand.CardIds, Is.EqualTo(new[]{"SkeletonArcher","PowerPlant","firebolt"}));
                Assert.That(battle.PlayerBoard.GetOccupant(TutorialResourceBattle.GoblinTile).HasSummoningSickness, Is.False);
                Assert.That(battle.PlayerBoard.GetOccupant(TutorialResourceBattle.MasterTile).CurrentHp, Is.EqualTo(333));
            }
        }
        [Test]
        public void ActualCombatDemonstratesBlockingRangedBypassMutualDeathAndSevenDamage()
        {
            var provider = Provider(); var gateway = TutorialCombatBattle.Create(provider); var b=gateway.CurrentBattleState;
            TutorialCombatBattle.PrepareFrontline(b,provider); TutorialCombatBattle.PrepareFrontline(b,provider);
            var goblin=b.PlayerBoard.GetOccupant(TutorialResourceBattle.GoblinTile);
            Assert.Throws<InvalidOperationException>(()=>gateway.ExecuteCommand(PlayerId.Player,new AttackCommand(TutorialResourceBattle.GoblinTile,TutorialResourceBattle.MasterTile)));
            Assert.That(goblin.RemainingAttacksThisTurn,Is.EqualTo(1)); Assert.That(b.AIBoard.GetOccupant(TutorialResourceBattle.MasterTile).CurrentHp,Is.EqualTo(333));
            gateway.ExecuteCommand(PlayerId.Player,new PlayUnitCardCommand("SkeletonArcher",TutorialCombatBattle.ArcherTile));
            Assert.Throws<InvalidOperationException>(()=>gateway.ExecuteCommand(PlayerId.Player,new AttackCommand(TutorialCombatBattle.ArcherTile,TutorialResourceBattle.MasterTile)));
            OpponentTurn(gateway,false);
            gateway.ExecuteCommand(PlayerId.Player,new AttackCommand(TutorialCombatBattle.ArcherTile,TutorialResourceBattle.MasterTile));
            Assert.That(b.AIBoard.GetOccupant(TutorialResourceBattle.MasterTile).CurrentHp,Is.EqualTo(323));
            Assert.That(b.PlayerBoard.GetOccupant(TutorialCombatBattle.ArcherTile).CurrentHp,Is.EqualTo(3));
            gateway.ExecuteCommand(PlayerId.Player,new AttackCommand(TutorialResourceBattle.GoblinTile,TutorialCombatBattle.EnemyGoblinTile));
            Assert.That(b.PlayerBoard.GetOccupant(TutorialResourceBattle.GoblinTile),Is.Null);Assert.That(b.AIBoard.GetOccupant(TutorialCombatBattle.EnemyGoblinTile),Is.Null);
            OpponentTurn(gateway,true);
            gateway.ExecuteCommand(PlayerId.Player,new AttackCommand(TutorialCombatBattle.ArcherTile,TutorialCombatBattle.GolemTile));
            Assert.That(b.AIBoard.GetOccupant(TutorialCombatBattle.GolemTile).CurrentHp,Is.EqualTo(23));
            var mana=b.Player.Resources.Mana;var gold=b.Player.Resources.Gold;
            gateway.ExecuteCommand(PlayerId.Player,new CastDamageSpellCommand("firebolt",PlayerId.AI,TutorialResourceBattle.MasterTile));
            Assert.That(b.AIBoard.GetOccupant(TutorialResourceBattle.MasterTile).CurrentHp,Is.EqualTo(293));
            Assert.That(b.Player.Resources.Mana,Is.EqualTo(mana-1));Assert.That(b.Player.Resources.Gold,Is.EqualTo(gold));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void SharedMoveFatigueAndFinalVictoryDoNotUseAReplacementRuleEngine(bool masterFirst)
        {
            var provider=Provider();var gateway=TutorialCombatBattle.Create(provider);var b=gateway.CurrentBattleState;
            gateway.ExecuteCommand(PlayerId.Player,new PlayUnitCardCommand("SkeletonArcher",TutorialCombatBattle.ArcherTile));
            gateway.ExecuteCommand(PlayerId.Player,new MoveOccupantCommand(TutorialCombatBattle.ArcherTile,TutorialCombatBattle.MovedArcherTile));
            var master=b.PlayerBoard.GetOccupant(TutorialResourceBattle.MasterTile);
            var archer=b.PlayerBoard.GetOccupant(TutorialCombatBattle.MovedArcherTile);
            gateway.ExecuteCommand(PlayerId.Player,new MoveOccupantCommand(
                masterFirst ? TutorialResourceBattle.MasterTile : TutorialCombatBattle.MovedArcherTile,
                masterFirst ? TutorialCombatBattle.MovedArcherTile : TutorialResourceBattle.MasterTile));
            Assert.That(b.PlayerBoard.GetOccupant(TutorialCombatBattle.MovedArcherTile), Is.SameAs(master));
            Assert.That(b.PlayerBoard.GetOccupant(TutorialResourceBattle.MasterTile), Is.SameAs(archer));
            Assert.Throws<InvalidOperationException>(()=>gateway.ExecuteCommand(PlayerId.Player,new MoveOccupantCommand(TutorialResourceBattle.PondTile,TutorialCombatBattle.BuildingMoveTile)));
            Assert.That(b.PlayerBoard.GetOccupant(TutorialResourceBattle.PondTile).CardId,Is.EqualTo("ManaPond"));
            TutorialCombatBattle.EmptyPracticeDeck(b);Assert.That(b.Player.Deck.Count,Is.Zero);Assert.That(master.CurrentHp,Is.EqualTo(333));
            gateway.EndTurn();gateway.ResolveTurnStart();gateway.EndTurn();gateway.ResolveTurnStart();
            Assert.That(master.CurrentHp,Is.EqualTo(330));
            TutorialCombatBattle.PrepareVictory(b);
            gateway.ExecuteCommand(PlayerId.Player,new AttackCommand(TutorialCombatBattle.FindPlayerArcher(b),TutorialResourceBattle.MasterTile));
            Assert.That(b.IsEnded,Is.True);Assert.That(b.Result.Winner,Is.EqualTo(PlayerId.Player));
        }
        [Test]
        public void FifthChapterPracticeActionsCannotBeSkippedAndReplayHasTheSameSequence()
        {
            var s=new TutorialSession();Assert.That(s.SelectChapter(5),Is.True);
            Assert.That(s.StepIndex,Is.EqualTo(TutorialSession.FifthChapterStep));Assert.That(s.Chapter,Is.EqualTo(5));
            var actions=new System.Collections.Generic.List<TutorialAction>();
            while (s.Chapter == 5)
            {
                if (!s.WaitingForAction) { Assert.That(s.TryAdvance(),Is.True); continue; }
                Assert.That(s.TryAdvance(),Is.False);Assert.That(s.PerformAction(TutorialAction.EndTurn),Is.False);
                var action=s.CurrentStep.RequiredAction;actions.Add(action);Assert.That(s.PerformAction(action),Is.True);
            }
            Assert.That(s.CurrentStep.Cue,Is.EqualTo(TutorialCue.ShopIntro));Assert.That(s.IsComplete,Is.False);
            Assert.That(actions,Is.EqualTo(new[]{TutorialAction.AttemptBlockedAttack,TutorialAction.SummonArcherAndEndTurn,TutorialAction.RangedMasterAttack,TutorialAction.MeleeCounterattack,TutorialAction.EndTurnForGolem,TutorialAction.AttackGolem,TutorialAction.CastPracticeFireball,TutorialAction.PreviewOccupant,TutorialAction.MovePracticeUnit,TutorialAction.SwapPracticeUnits,TutorialAction.AttemptBuildingMove,TutorialAction.FinishPracticeBattle}));
            Assert.That(s.SelectChapter(5),Is.True);Assert.That(s.CurrentStep.Cue,Is.EqualTo(TutorialCue.CombatReady));Assert.That(s.IsComplete,Is.False);
        }
    }
}
