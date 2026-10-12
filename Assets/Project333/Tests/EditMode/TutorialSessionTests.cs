using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Domain.Draft;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;

namespace Project333.Tests.EditMode
{
    public sealed class TutorialSessionTests
    {
        [Test]
        public void PracticeCannotStartEarlyOrByAdvanceAndChargesOnlyOnceBeforeChapterTwo()
        {
            var lesson = new TutorialSession();
            Assert.That(lesson.PerformAction(TutorialAction.StartRun), Is.False);
            AdvanceToAction(lesson, TutorialAction.StartRun);
            Assert.That(lesson.PracticeTickets, Is.EqualTo(3));
            Assert.That(lesson.TryAdvance(), Is.False);
            Assert.That(lesson.PerformAction(TutorialAction.None), Is.False);
            Assert.That(lesson.PerformAction(TutorialAction.StartRun), Is.True);
            Assert.That(lesson.PracticeTickets, Is.Zero);
            Assert.That(lesson.TutorialRunStarted, Is.True);
            Assert.That(lesson.Chapter, Is.EqualTo(2));
            Assert.That(lesson.StepIndex, Is.EqualTo(TutorialSession.SecondChapterStep));
            Assert.That(lesson.IsComplete, Is.False);
            Assert.That(lesson.DraftCardIds, Is.Empty);
            Assert.That(lesson.PerformAction(TutorialAction.StartRun), Is.False);
            Assert.That(lesson.PracticeTickets, Is.Zero);
        }

        [Test]
        public void OfferPreviewMustCompleteBeforeFirstShortClickAndCannotPickACard()
        {
            var lesson = ChapterTwo();
            AdvanceToAction(lesson, TutorialAction.PreviewOffer);
            var offer = lesson.CurrentOfferCardIds.ToArray();
            Assert.That(lesson.TryAdvance(), Is.False);
            Assert.That(lesson.PerformAction(TutorialAction.PickCard, 0), Is.False);
            Assert.That(lesson.PerformAction(TutorialAction.PreviewOffer), Is.False);
            Assert.That(lesson.PerformAction(TutorialAction.PreviewOffer, -1), Is.False);
            Assert.That(lesson.PerformAction(TutorialAction.PreviewOffer, 3), Is.False);
            Assert.That(lesson.CurrentStep.RequiredAction, Is.EqualTo(TutorialAction.PreviewOffer));
            // Merely opening or canceling the preview submits no completed action.
            Assert.That(lesson.DraftCardIds, Is.Empty);
            Assert.That(lesson.PerformAction(TutorialAction.PreviewOffer, 1), Is.True);
            Assert.That(lesson.CurrentStep.RequiredAction, Is.EqualTo(TutorialAction.PickCard));
            Assert.That(lesson.DraftCardIds, Is.Empty);
            Assert.That(lesson.CurrentOfferCardIds, Is.EqualTo(offer));
            Assert.That(lesson.PerformAction(TutorialAction.PreviewOffer, 1), Is.False);
            Assert.That(lesson.PerformAction(TutorialAction.PickCard, 2), Is.True);
            Assert.That(lesson.DraftCardIds, Is.EqualTo(new[] { offer[2] }));
            Assert.That(lesson.ManualPickCount, Is.EqualTo(1));
            Assert.That(lesson.CurrentStep.Cue, Is.EqualTo(TutorialCue.RarityCommonUncommon));
            Assert.That(lesson.CurrentStep.Dialogue, Is.EqualTo(
                "카드 아래쪽의 보석은 희귀도를 나타내네. 하얀색-커먼, 초록색-언커먼, 파란색-레어, 빨간색-유니크, 노란색-레전더리로 커먼, 언커먼, 레어, 유니크, 레전더리 순으로 희귀도가 높지."));
            Assert.That(lesson.TryAdvance(), Is.True);
            Assert.That(lesson.CurrentStep.Cue, Is.EqualTo(TutorialCue.RarityRole));
        }

        [Test]
        public void DeckPreviewCannotBeBypassedByNextOrAnotherPick()
        {
            var lesson = PickFirstCard();
            AdvanceToAction(lesson, TutorialAction.PreviewDeck);
            var selected = lesson.DraftCardIds.ToArray();
            Assert.That(lesson.TryAdvance(), Is.False);
            Assert.That(lesson.PerformAction(TutorialAction.PickCard, 0), Is.False);
            Assert.That(lesson.PerformAction(TutorialAction.PreviewOffer, 0), Is.False);
            Assert.That(lesson.PerformAction(TutorialAction.PreviewDeck, 1), Is.False);
            Assert.That(lesson.PerformAction(TutorialAction.PreviewDeck, -1), Is.False);
            Assert.That(lesson.CurrentStep.Cue, Is.EqualTo(TutorialCue.DeckPreview));
            Assert.That(lesson.DraftCardIds, Is.EqualTo(selected));
            Assert.That(lesson.PerformAction(TutorialAction.PreviewDeck, 0), Is.True);
            Assert.That(lesson.CurrentStep.Cue, Is.EqualTo(TutorialCue.CopyLimits));
            Assert.That(lesson.DraftCardIds, Is.EqualTo(selected));
        }

        [Test]
        public void FiveManualChoicesAreRequiredBeforeOnlyFinalAutoCueFillsTwentyEight()
        {
            var lesson = ManualChoices();
            var pickStep = lesson.StepIndex;
            for (var picked = 1; picked < TutorialSession.ManualDraftPicks; picked++)
            {
                Assert.That(lesson.ManualPickCount, Is.EqualTo(picked));
                Assert.That(lesson.DraftCardIds.Count, Is.EqualTo(picked));
                Assert.That(lesson.TryAdvance(), Is.False);
                Assert.That(lesson.PerformAction(TutorialAction.CompleteDeck), Is.False);
                Assert.That(lesson.PerformAction(TutorialAction.PickCard, picked % 3), Is.True);
                Assert.That(lesson.StepIndex, Is.EqualTo(picked == 4 ? pickStep + 1 : pickStep));
            }
            Assert.That(lesson.ManualPickCount, Is.EqualTo(5));
            Assert.That(lesson.DraftCardIds.Count, Is.EqualTo(5));
            Assert.That(lesson.CurrentOfferCardIds, Is.EqualTo(new[] { "Goblin", "ManaPond", "firebolt" }));
            Assert.That(lesson.PerformAction(TutorialAction.PickCard, 0), Is.False);
            Assert.That(lesson.CurrentStep.Cue, Is.EqualTo(TutorialCue.DraftFivePicked));
            Assert.That(lesson.PerformAction(TutorialAction.CompleteDeck), Is.False);
            Assert.That(lesson.TryAdvance(), Is.True);
            Assert.That(lesson.CurrentStep.Cue, Is.EqualTo(TutorialCue.AutoDraft));
            Assert.That(lesson.CurrentOfferCardIds, Is.EqualTo(new[] { "Goblin", "ManaPond", "firebolt" }));
            Assert.That(lesson.PerformAction(TutorialAction.PickCard, 2), Is.False);
            Assert.That(lesson.TryAdvance(), Is.False);
            Assert.That(lesson.IsComplete, Is.False);
            var manual = lesson.DraftCardIds.ToArray();
            Assert.That(lesson.PerformAction(TutorialAction.CompleteDeck), Is.True);
            Assert.That(lesson.CurrentStep.Cue, Is.EqualTo(TutorialCue.DraftReady));
            Assert.That(lesson.DraftCardIds.Count, Is.EqualTo(33));
            Assert.That(lesson.DraftCardIds.Take(5), Is.EqualTo(manual));
            Assert.That(lesson.ManualPickCount, Is.EqualTo(5));
            Assert.That(lesson.IsDraftReady, Is.True);
            Assert.That(lesson.IsComplete, Is.False);
            Assert.That(lesson.TryAdvance(), Is.True);
            Assert.That(lesson.Chapter, Is.EqualTo(3));
            Assert.That(lesson.StepIndex, Is.EqualTo(TutorialSession.ThirdChapterStep));
            Assert.That(lesson.CurrentOfferCardIds, Is.Empty);
            Assert.That(lesson.PerformAction(TutorialAction.CompleteDeck), Is.False);
            Assert.That(lesson.PerformAction(TutorialAction.PickCard, 0), Is.False);
            Assert.That(lesson.DraftCardIds.Count, Is.EqualTo(33));
        }

        [Test]
        public void AllTwoHundredFortyThreeChoiceBranchesUseRealCardsAndPreserveDraftLimitsDeterministically()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(
                "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            for (var branch = 0; branch < 243; branch++)
            {
                var choices = new int[5];
                var value = branch;
                for (var i = 0; i < choices.Length; i++) { choices[i] = value % 3; value /= 3; }
                var lesson = Finish(choices, catalog);
                var replay = Finish(choices, catalog);
                Assert.That(lesson.DraftCardIds, Is.EqualTo(replay.DraftCardIds), "Branch " + branch);
                Assert.That(lesson.DraftCardIds.Count, Is.EqualTo(DraftDeckState.TargetDeckSize));
                var legendaryCount = 0;
                foreach (var group in lesson.DraftCardIds.GroupBy(id => id))
                {
                    Assert.That(catalog.TryGetCardAsset(group.Key, out var card), Is.True, group.Key);
                    Assert.That(group.Count(), Is.LessThanOrEqualTo(DraftDeckState.GetMaxCopies(card.Rarity)), group.Key);
                    if (card.Rarity == CardRarity.Legendary) legendaryCount += group.Count();
                }
                Assert.That(legendaryCount, Is.EqualTo(1));
                Assert.That(catalog.TryGetCardAsset(lesson.DraftCardIds[0], out var first), Is.True);
                Assert.That(first.Rarity, Is.EqualTo(CardRarity.Legendary));
                Assert.That(lesson.PracticeTickets, Is.Zero);
            }
        }

        [Test]
        public void DirectSecondChapterSelectionInitializesTheSameRunWithoutARealTicketDependency()
        {
            var direct = ChapterTwo();
            var started = new TutorialSession();
            AdvanceToAction(started, TutorialAction.StartRun);
            Assert.That(started.PerformAction(TutorialAction.StartRun), Is.True);
            Assert.That(direct.StepIndex, Is.EqualTo(started.StepIndex));
            Assert.That(direct.CurrentOfferCardIds, Is.EqualTo(started.CurrentOfferCardIds));
            Assert.That(direct.PracticeTickets, Is.Zero);
            Assert.That(direct.TutorialRunStarted, Is.True);
            Assert.That(direct.DraftCardIds, Is.Empty);
            Assert.That(direct.ManualPickCount, Is.Zero);
            Assert.That(direct.IsLegendaryOpeningOffer, Is.True);
            Assert.That(direct.IsComplete, Is.False);
            Assert.That(direct.PerformAction(TutorialAction.StartRun), Is.False);
        }

        [Test]
        public void RestartAndFreshSessionAreIdenticalAfterCompletedPracticeAndExit()
        {
            var lesson = Finish(new[] { 2, 1, 0, 2, 1 });
            lesson.Skip(); lesson.Restart();
            var fresh = new TutorialSession();
            Assert.That(lesson.StepIndex, Is.EqualTo(fresh.StepIndex));
            Assert.That(lesson.PracticeTickets, Is.EqualTo(fresh.PracticeTickets));
            Assert.That(lesson.IsComplete, Is.False); Assert.That(lesson.IsExited, Is.False);
            Assert.That(lesson.TutorialRunStarted, Is.False);
            Assert.That(lesson.ManualPickCount, Is.Zero);
            Assert.That(lesson.DraftCardIds, Is.Empty); Assert.That(lesson.CurrentOfferCardIds, Is.Empty);
            for (var i = 0; i < TutorialSession.SecondChapterStep; i++)
            {
                Assert.That(lesson.CurrentStep.Dialogue, Is.EqualTo(fresh.CurrentStep.Dialogue));
                Assert.That(lesson.TryAdvance(), Is.EqualTo(fresh.TryAdvance()));
            }
        }

        [Test]
        public void ChapterSelectionDiscardsLocalDeckAndRestoresTheRequestedChapter()
        {
            var lesson = Finish(new[] { 0, 2, 0, 2, 0 });
            Assert.That(lesson.SelectChapter(2), Is.True);
            Assert.That(lesson.StepIndex, Is.EqualTo(TutorialSession.SecondChapterStep));
            Assert.That(lesson.DraftCardIds, Is.Empty); Assert.That(lesson.ManualPickCount, Is.Zero);
            Assert.That(lesson.CurrentOfferCardIds, Is.EqualTo(new[] { "BlueDragon", "RedDragon", "Nuclear" }));
            Assert.That(lesson.SelectChapter(1), Is.True);
            Assert.That(lesson.StepIndex, Is.EqualTo(TutorialSession.FirstChapterStep));
            Assert.That(lesson.CurrentStep.Cue, Is.EqualTo(TutorialCue.Civilizations));
            Assert.That(lesson.PracticeTickets, Is.EqualTo(3)); Assert.That(lesson.IsComplete, Is.False);
            Assert.That(lesson.TutorialRunStarted, Is.False); Assert.That(lesson.DraftCardIds, Is.Empty);
            Assert.That(lesson.CurrentOfferCardIds, Is.Empty);
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(7)]
        public void UnavailableChapterNeverChangesProgressOrLocalDeck(int chapter)
        {
            var lesson = ManualChoices();
            Assert.That(lesson.PerformAction(TutorialAction.PickCard, 1), Is.True);
            var step = lesson.StepIndex; var cards = lesson.DraftCardIds.ToArray(); var offer = lesson.CurrentOfferCardIds.ToArray();
            Assert.That(lesson.SelectChapter(chapter), Is.False);
            Assert.That(lesson.StepIndex, Is.EqualTo(step)); Assert.That(lesson.PracticeTickets, Is.Zero);
            Assert.That(lesson.DraftCardIds, Is.EqualTo(cards)); Assert.That(lesson.CurrentOfferCardIds, Is.EqualTo(offer));
        }

        [TestCase(TutorialAction.StartRun)] [TestCase(TutorialAction.PreviewOffer)]
        [TestCase(TutorialAction.PreviewDeck)] [TestCase(TutorialAction.CompleteDeck)]
        [TestCase(TutorialAction.StartGoldDungeon)]
        public void SkipStopsEveryPendingPracticeWithoutSpendingOrCompleting(TutorialAction pending)
        {
            var lesson = new TutorialSession();
            while (lesson.CurrentStep.RequiredAction != pending)
            {
                if (lesson.WaitingForAction)
                    Assert.That(lesson.PerformAction(lesson.CurrentStep.RequiredAction, 0), Is.True);
                else Assert.That(lesson.TryAdvance(), Is.True);
            }
            var cards = lesson.DraftCardIds.ToArray(); var tickets = lesson.PracticeTickets;
            lesson.Skip();
            Assert.That(lesson.IsExited, Is.True); Assert.That(lesson.TryAdvance(), Is.False);
            Assert.That(lesson.PerformAction(pending, 0), Is.False);
            Assert.That(lesson.PracticeTickets, Is.EqualTo(tickets));
            Assert.That(lesson.DraftCardIds, Is.EqualTo(cards)); Assert.That(lesson.IsComplete, Is.False);
        }

        [Test]
        public void ReturnedCardsAreReadOnlyAndEarlierDeckSnapshotsStayUnchanged()
        {
            var lesson = ChapterTwo();
            var originalOffer = lesson.CurrentOfferCardIds;
            Assert.Throws<NotSupportedException>(() => ((IList<string>)originalOffer)[0] = "UnknownCard");
            AdvanceToAction(lesson, TutorialAction.PreviewOffer);
            lesson.PerformAction(TutorialAction.PreviewOffer, 0);
            lesson.PerformAction(TutorialAction.PickCard, 0);
            var oneCard = lesson.DraftCardIds;
            Assert.Throws<NotSupportedException>(() => ((IList<string>)oneCard).Add("UnknownCard"));
            AdvanceToAction(lesson, TutorialAction.PreviewDeck);
            lesson.PerformAction(TutorialAction.PreviewDeck, 0);
            AdvanceToAction(lesson, TutorialAction.PickCard);
            lesson.PerformAction(TutorialAction.PickCard, 0);
            Assert.That(oneCard.Count, Is.EqualTo(1));
            Assert.That(lesson.DraftCardIds.Count, Is.EqualTo(2));
            Assert.That(originalOffer, Is.EqualTo(new[] { "BlueDragon", "RedDragon", "Nuclear" }));
        }

        [Test]
        public void InvalidPickIndexOrWrongActionDoesNotChangeOfferDeckOrProgress()
        {
            var lesson = ManualChoices();
            var step = lesson.StepIndex; var cards = lesson.DraftCardIds.ToArray(); var offer = lesson.CurrentOfferCardIds.ToArray();
            foreach (var index in new[] { -1, 3, int.MaxValue })
                Assert.That(lesson.PerformAction(TutorialAction.PickCard, index), Is.False);
            Assert.That(lesson.PerformAction((TutorialAction)99, 0), Is.False);
            Assert.That(lesson.PerformAction(TutorialAction.StartRun), Is.False);
            Assert.That(lesson.PerformAction(TutorialAction.PreviewDeck, 0), Is.False);
            Assert.That(lesson.StepIndex, Is.EqualTo(step)); Assert.That(lesson.DraftCardIds, Is.EqualTo(cards));
            Assert.That(lesson.CurrentOfferCardIds, Is.EqualTo(offer)); Assert.That(lesson.ManualPickCount, Is.EqualTo(1));
        }

        [Test]
        public void DirectThirdChapterHasTheSameCompletedDeckAsFiveAuthoredChoices()
        {
            var lesson = new TutorialSession();
            Assert.That(lesson.SelectChapter(3), Is.True);
            Assert.That(lesson.Chapter, Is.EqualTo(3));
            Assert.That(lesson.StepIndex, Is.EqualTo(TutorialSession.ThirdChapterStep));
            Assert.That(lesson.IsComplete, Is.False);
            Assert.That(lesson.IsDraftReady, Is.True);
            Assert.That(lesson.ManualPickCount, Is.EqualTo(5));
            Assert.That(lesson.PracticeTickets, Is.Zero);
            Assert.That(lesson.PracticeGoldDungeonRemaining, Is.EqualTo(3));
            Assert.That(lesson.CurrentOfferCardIds, Is.Empty);
            Assert.That(lesson.DraftCardIds, Is.EqualTo(Finish(new[] { 0, 0, 0, 0, 0 }).DraftCardIds));
            var cards = lesson.DraftCardIds.ToArray();
            Assert.That(lesson.SelectChapter(3), Is.True);
            Assert.That(lesson.DraftCardIds, Is.EqualTo(cards));
        }

        [Test]
        public void ThirdChapterRequiresPreviewThenGoldDungeonActionAndChargesOnlyLocalQuotaOnce()
        {
            var lesson = new TutorialSession(); lesson.SelectChapter(3);
            var cards = lesson.DraftCardIds.ToArray();
            Assert.That(lesson.PerformAction(TutorialAction.StartGoldDungeon), Is.False);
            AdvanceToAction(lesson, TutorialAction.PreviewDeck);
            Assert.That(lesson.TryAdvance(), Is.False);
            Assert.That(lesson.PerformAction(TutorialAction.StartGoldDungeon), Is.False);
            Assert.That(lesson.PerformAction(TutorialAction.PreviewDeck, -1), Is.False);
            Assert.That(lesson.PerformAction(TutorialAction.PreviewDeck, 0), Is.True);
            AdvanceToAction(lesson, TutorialAction.StartGoldDungeon);
            Assert.That(lesson.TryAdvance(), Is.False);
            Assert.That(lesson.PerformAction(TutorialAction.PreviewDeck, 0), Is.False);
            Assert.That(lesson.PracticeGoldDungeonRemaining, Is.EqualTo(3));
            Assert.That(lesson.PerformAction(TutorialAction.StartGoldDungeon), Is.True);
            Assert.That(lesson.CurrentStep.Cue, Is.EqualTo(TutorialCue.BattleEntry));
            Assert.That(lesson.IsComplete, Is.False);
            Assert.That(lesson.PracticeBattleStarted, Is.True);
            Assert.That(lesson.PracticeGoldDungeonRemaining, Is.EqualTo(2));
            Assert.That(lesson.PracticeTickets, Is.Zero);
            Assert.That(lesson.DraftCardIds, Is.EqualTo(cards));
            Assert.That(lesson.PerformAction(TutorialAction.StartGoldDungeon), Is.False);
            Assert.That(lesson.TryAdvance(), Is.True);
            Assert.That(lesson.Chapter, Is.EqualTo(4));
            lesson.SelectChapter(3);
            Assert.That(lesson.PracticeBattleStarted, Is.False);
            Assert.That(lesson.PracticeGoldDungeonRemaining, Is.EqualTo(3));
        }

        private static TutorialSession ChapterTwo()
        { var lesson = new TutorialSession(); Assert.That(lesson.SelectChapter(2), Is.True); return lesson; }
        private static TutorialSession PickFirstCard()
        {
            var lesson = ChapterTwo(); AdvanceToAction(lesson, TutorialAction.PreviewOffer);
            Assert.That(lesson.PerformAction(TutorialAction.PreviewOffer, 0), Is.True);
            Assert.That(lesson.PerformAction(TutorialAction.PickCard, 0), Is.True); return lesson;
        }
        private static TutorialSession ManualChoices()
        {
            var lesson = PickFirstCard(); AdvanceToAction(lesson, TutorialAction.PreviewDeck);
            Assert.That(lesson.PerformAction(TutorialAction.PreviewDeck, 0), Is.True);
            AdvanceToAction(lesson, TutorialAction.PickCard); return lesson;
        }
        private static void AdvanceToAction(TutorialSession lesson, TutorialAction action)
        {
            for (var attempts = 0; !lesson.WaitingForAction && attempts < lesson.StepCount; attempts++)
                Assert.That(lesson.TryAdvance(), Is.True);
            Assert.That(lesson.CurrentStep.RequiredAction, Is.EqualTo(action));
        }
        private static TutorialSession Finish(int[] choices, CardDefinitionCatalogAsset catalog = null)
        {
            var lesson = ChapterTwo(); var nextChoice = 0;
            for (var attempts = 0; !lesson.IsComplete && attempts < lesson.StepCount + TutorialSession.ManualDraftPicks; attempts++)
            {
                if (!lesson.WaitingForAction) { Assert.That(lesson.TryAdvance(), Is.True); continue; }
                if (lesson.CurrentStep.RequiredAction == TutorialAction.PickCard)
                {
                    var offer = lesson.CurrentOfferCardIds;
                    Assert.That(offer.Count, Is.EqualTo(3)); Assert.That(offer.Distinct().Count(), Is.EqualTo(3));
                    if (catalog != null)
                        foreach (var id in offer)
                        {
                            Assert.That(catalog.TryGetCardAsset(id, out var card), Is.True, id);
                            Assert.That(card.Rarity == CardRarity.Legendary, Is.EqualTo(nextChoice == 0), id);
                        }
                    Assert.That(lesson.PerformAction(TutorialAction.PickCard, choices[nextChoice++]), Is.True);
                }
                else if (lesson.CurrentStep.RequiredAction == TutorialAction.UpgradePracticeCard)
                {
                    var upgradeCatalog = catalog ?? AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(
                        "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset");
                    Assert.That(upgradeCatalog.TryGetCardAsset("Goblin", out var goblin), Is.True);
                    Assert.That(lesson.UpgradePractice.TryUpgrade(goblin.ToDefinition()), Is.True);
                    Assert.That(lesson.PerformAction(TutorialAction.UpgradePracticeCard), Is.True);
                }
                else Assert.That(lesson.PerformAction(lesson.CurrentStep.RequiredAction, 0), Is.True);
            }
            Assert.That(lesson.IsComplete, Is.True); Assert.That(nextChoice, Is.EqualTo(5));
            return lesson;
        }
    }
}
