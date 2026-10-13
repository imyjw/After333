using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;

namespace Project333.Tests.EditMode
{
    public sealed class TutorialResourceBattleTests
    {
        private static ICardDefinitionProvider Provider() => AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(
            "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset").CreateProvider();
        [Test]
        public void FourthChapterDirectEntryAndReplayHaveTheSamePracticeGates()
        {
            var session = new TutorialSession(); Assert.That(session.SelectChapter(4), Is.True);
            Assert.That(session.Chapter, Is.EqualTo(4)); Assert.That(session.StepIndex, Is.EqualTo(TutorialSession.FourthChapterStep));
            Assert.That(session.PracticeBattleStarted, Is.True); Assert.That(session.DraftCardIds.Count, Is.EqualTo(33));
            Assert.That(session.TryAdvance(), Is.True);
            Assert.That(session.CurrentStep.RequiredAction, Is.EqualTo(TutorialAction.SelectMulliganCard));
            Assert.That(session.TryAdvance(), Is.False); Assert.That(session.PerformAction(TutorialAction.EndTurn), Is.False);
            Assert.That(session.PerformAction(TutorialAction.SelectMulliganCard), Is.True);
            Assert.That(session.CurrentStep.RequiredAction, Is.EqualTo(TutorialAction.ConfirmMulligan));
            Assert.That(session.CancelMulliganSelection(), Is.True);
            Assert.That(session.CurrentStep.RequiredAction, Is.EqualTo(TutorialAction.SelectMulliganCard));
            var revision = session.Revision;
            Assert.That(session.SelectChapter(4), Is.True); Assert.That(session.Revision, Is.GreaterThan(revision));
            Assert.That(session.StepIndex, Is.EqualTo(TutorialSession.FourthChapterStep));
            Assert.That(session.SelectChapter(5), Is.True);
            Assert.That(session.Chapter, Is.EqualTo(5));
        }
        [Test]
        public void OpeningExchangeAndDrawOrderAreFixedAcrossFreshEntries()
        {
            var provider = Provider();
            for (var i = 0; i < 3; i++)
            {
                var gateway = TutorialResourceBattle.Create(provider); var state = gateway.CurrentBattleState;
                Assert.That(state.Phase, Is.EqualTo(PhaseType.Mulligan)); Assert.That(state.Player.Resources.Gold, Is.EqualTo(3));
                Assert.That(state.AI.Resources.Gold, Is.EqualTo(4));
                Assert.That(state.AIBoard.EnumerateOccupants().Count(), Is.EqualTo(1));
                Assert.That(state.AIBoard.GetOccupant(TutorialResourceBattle.OpponentArcherTile), Is.Null);
                Assert.That(state.Player.Hand.CardIds, Is.EqualTo(new[]{"Goblin","ManaPond","firebolt","ManaShield","PlasmaShield"}));
                gateway.ApplyMulligan(PlayerId.Player,new[]{"firebolt"},new TutorialResourceBattle.MulliganDeck());
                Assert.That(state.Phase, Is.EqualTo(PhaseType.TurnStart));
                Assert.That(state.Player.Hand.CardIds, Is.EqualTo(new[]{"Goblin","ManaPond","ManaShield","PlasmaShield","SkeletonArcher"}));
                gateway.ResolveTurnStart();
                Assert.That(state.Phase, Is.EqualTo(PhaseType.Main)); Assert.That(state.Player.Resources.Gold, Is.EqualTo(3));
                Assert.That(state.Player.Hand.CardIds.Last(), Is.EqualTo("PowerPlant"));
            }
            Assert.That(TutorialResourceBattle.CreateDeck().Count, Is.EqualTo(33));
            Assert.That(TutorialResourceBattle.CreateDeck().GroupBy(x=>x).All(x=>x.Count()<=3), Is.True);
        }
        [Test]
        public void RealCommandsSpendManaFirstThenGoldAndOpponentSummonsWithoutAttacking()
        {
            var provider = Provider(); var gateway = TutorialResourceBattle.Create(provider); var state = gateway.CurrentBattleState;
            gateway.ApplyMulligan(PlayerId.Player,new[]{"firebolt"},new TutorialResourceBattle.MulliganDeck()); gateway.ResolveTurnStart();
            var grant = TutorialResourceBattle.GrantPracticeResources(state);
            Assert.That(grant.Mana, Is.EqualTo(3)); Assert.That(grant.Gold, Is.EqualTo(5));
            Assert.That(state.Player.Resources.Mana, Is.EqualTo(3)); Assert.That(state.Player.Resources.Gold, Is.EqualTo(8));
            gateway.ExecuteCommand(PlayerId.Player,new PlayUnitCardCommand("Goblin",TutorialResourceBattle.GoblinTile));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(8)); Assert.That(state.Player.Resources.Mana, Is.EqualTo(1));
            var goblin = state.PlayerBoard.GetOccupant(TutorialResourceBattle.GoblinTile);
            Assert.That(goblin.Attack, Is.EqualTo(20)); Assert.That(goblin.CurrentHp, Is.EqualTo(20));
            Assert.That(goblin.HasSummoningSickness, Is.True);
            gateway.ExecuteCommand(PlayerId.Player,new PlayBuildingCardCommand("ManaPond",TutorialResourceBattle.PondTile));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(3)); Assert.That(state.Player.Resources.Mana, Is.Zero);
            var handCount = state.Player.Hand.Count; gateway.EndTurn(); gateway.ResolveTurnStart();
            Assert.That(state.AI.Resources.Gold, Is.EqualTo(4));
            gateway.ExecuteCommand(PlayerId.AI,new PlayUnitCardCommand("SkeletonArcher",TutorialResourceBattle.OpponentArcherTile));
            Assert.That(state.AI.Resources.Gold, Is.EqualTo(3));
            var archer = state.AIBoard.GetOccupant(TutorialResourceBattle.OpponentArcherTile);
            Assert.That(archer.HasSummoningSickness, Is.True);
            Assert.Throws<System.InvalidOperationException>(() => gateway.ExecuteCommand(PlayerId.AI,
                new AttackCommand(TutorialResourceBattle.OpponentArcherTile,TutorialResourceBattle.MasterTile)));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.PlayerBoard.GetOccupant(TutorialResourceBattle.GoblinTile), Is.SameAs(goblin));
            gateway.EndTurn(); gateway.ResolveTurnStart();
            Assert.That(state.ActivePlayerId, Is.EqualTo(PlayerId.Player)); Assert.That(state.TurnNumber, Is.EqualTo(3));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(6)); Assert.That(state.Player.Resources.Mana, Is.EqualTo(3));
            Assert.That(state.Player.Hand.Count, Is.EqualTo(handCount+1)); Assert.That(state.Player.Hand.CardIds.Last(), Is.EqualTo("firebolt"));
            Assert.Throws<System.InvalidOperationException>(()=>gateway.ResolveTurnStart());
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(6)); Assert.That(state.Player.Resources.Mana, Is.EqualTo(3));
        }
    }
}
