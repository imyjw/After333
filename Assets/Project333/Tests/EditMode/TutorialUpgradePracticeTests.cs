using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Application.Services;
using UnityEditor;

namespace Project333.Tests.EditMode
{
    public sealed class TutorialUpgradePracticeTests
    {
        private static CardDefinitionCatalogAsset Catalog => AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>("Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset");
        [Test]
        public void LocalUpgradeUsesSharedCostsAndHpScheduleWithoutChangingCardAssets()
        {
            Assert.That(Catalog.TryGetCardAsset("Goblin",out var asset),Is.True);
            var definition=asset.ToDefinition();var practice=new TutorialUpgradePractice();
            Assert.That(CardStatDisplay.TryCreate(definition,0,out var before),Is.True);
            Assert.That(CardStatDisplay.TryCreate(definition,1,out var after),Is.True);
            Assert.That(after.Hp,Is.EqualTo(before.Hp+1));
            Assert.That(practice.TryUpgrade(Catalog.Cards.First(a=>a.CardId=="ManaPond").ToDefinition()),Is.False);
            Assert.That(practice.Gold,Is.EqualTo(3));Assert.That(practice.Copies,Is.EqualTo(3));
            Assert.That(practice.TryUpgrade(definition),Is.True);
            Assert.That(practice.Level,Is.EqualTo(1));Assert.That(practice.Gold,Is.Zero);Assert.That(practice.Copies,Is.Zero);
            Assert.That(practice.TryUpgrade(definition),Is.False);
            Assert.That(CardStatDisplay.TryCreate(definition,0,out var unchanged),Is.True);Assert.That(unchanged.Hp,Is.EqualTo(before.Hp));
        }
        [Test]
        public void ChapterSixRequiresActualUpgradeAndEveryEntryResetsLessonInventory()
        {
            var session=new TutorialSession();Assert.That(session.SelectChapter(6),Is.True);
            Assert.That(session.StepIndex,Is.EqualTo(TutorialSession.SixthChapterStep));Assert.That(session.PracticeBattleStarted,Is.False);
            var actions=new System.Collections.Generic.List<TutorialAction>();
            while(!session.IsComplete)
            {
                if(!session.WaitingForAction){Assert.That(session.TryAdvance(),Is.True);continue;}
                var action=session.CurrentStep.RequiredAction;actions.Add(action);
                Assert.That(session.TryAdvance(),Is.False);Assert.That(session.PerformAction(TutorialAction.EndTurn),Is.False);
                if(action==TutorialAction.UpgradePracticeCard)
                {
                    Assert.That(session.PerformAction(action),Is.False);
                    Assert.That(Catalog.TryGetCardAsset("Goblin",out var asset),Is.True);
                    Assert.That(session.UpgradePractice.TryUpgrade(asset.ToDefinition()),Is.True);
                }
                Assert.That(session.PerformAction(action),Is.True);
            }
            Assert.That(actions,Is.EqualTo(new[]{TutorialAction.SelectUpgradeCard,TutorialAction.ShowUpgradeTable,TutorialAction.RestoreCardArtwork,TutorialAction.UpgradePracticeCard}));
            Assert.That(session.CurrentStep.Cue,Is.EqualTo(TutorialCue.TutorialFarewell));
            var old=session.UpgradePractice;session.SelectChapter(6);
            Assert.That(session.UpgradePractice,Is.Not.SameAs(old));Assert.That(session.UpgradePractice.Level,Is.Zero);Assert.That(session.UpgradePractice.Gold,Is.EqualTo(3));Assert.That(session.UpgradePractice.Copies,Is.EqualTo(3));
        }
    }
}
