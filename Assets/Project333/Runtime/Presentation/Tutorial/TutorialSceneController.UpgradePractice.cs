using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Presentation.OwnedCards;
using Project333.Runtime.Presentation.Shop;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Tutorial
{
    public sealed partial class TutorialSceneController
    {
        private OwnedCardsSceneController _practiceCollection;
        private ShopSceneController _practiceShop;
        private TutorialAction _pendingUpgradeAction;
        private int _upgradeActionRevision;
        private bool IsShopLesson => _session.StepIndex < TutorialSession.SixthChapterStep + 6;
        private bool IsConclusionLesson => _session.StepIndex >= TutorialSession.SixthChapterStep + 18;
        private bool UpgradeActionAllowed(TutorialAction action) => CanPractice && _session.Chapter == 6 && _session.WaitingForAction && _session.CurrentStep.RequiredAction == action;
        private void OnUpgradePracticeCompleted(TutorialAction action)
        {
            if (_returning || _session.IsExited || _session.Chapter != 6) return;
            _pendingUpgradeAction = action; _upgradeActionRevision = _session.Revision;
            RefreshUpgradePractice();
        }
        private void RefreshUpgradePractice()
        {
            if (_session.Chapter != 6) { _pendingUpgradeAction = TutorialAction.None; return; }
            RefreshPracticeAdvanceButton();
            if (_pendingUpgradeAction == TutorialAction.None || !CanPractice) return;
            var action = _pendingUpgradeAction; _pendingUpgradeAction = TutorialAction.None;
            if (_upgradeActionRevision == _session.Revision && _session.PerformAction(action)) PresentStep();
        }
        private void PresentUpgradePracticeScene()
        {
            _practiceShop = FindInScene<ShopSceneController>(_practiceScene);
            _practiceCollection = FindInScene<OwnedCardsSceneController>(_practiceScene);
            _practiceShop?.ConfigureTutorialShop();
            _practiceCollection?.ConfigureTutorialCollection(_session.UpgradePractice, UpgradeActionAllowed, OnUpgradePracticeCompleted);
            ConfigurePracticeHighlights();
        }
        private void ConfigureUpgradePracticeHighlights()
        {
            if (_session.Chapter != 6 || IsConclusionLesson) return;
            var cue = _session.CurrentStep.Cue;
            if (_practiceShop != null)
            {
                if (cue == TutorialCue.ShopIntro)
                { AssignPracticeHighlight(0, _practiceShop.TutorialTicketPanel); AssignPracticeHighlight(1, _practiceShop.TutorialAdPanel); AssignPracticeHighlight(2, _practiceShop.TutorialPackPanel); }
                else if (cue == TutorialCue.ShopAds) AssignPracticeHighlight(0, _practiceShop.TutorialAdPanel);
                else if (cue == TutorialCue.ShopTickets) AssignPracticeHighlight(0, _practiceShop.TutorialTicketPanel);
                else if (cue == TutorialCue.ShopAccountGold) AssignPracticeHighlight(0, _practiceShop.TutorialWalletRect);
                else AssignPracticeHighlight(0, _practiceShop.TutorialPackPanel);
                return;
            }
            if (_practiceCollection == null) return;
            switch (cue)
            {
                case TutorialCue.CollectionIntro:
                case TutorialCue.CollectionEligibility: AssignPracticeHighlight(0, _practiceCollection.TutorialGoblinCardRect); break;
                case TutorialCue.CollectionDetail:
                case TutorialCue.UpgradePracticeIntro:
                case TutorialCue.UpgradeMaximum:
                case TutorialCue.UpgradeAccountIsolation: AssignPracticeHighlight(0, _practiceCollection.TutorialOwnershipRect); break;
                case TutorialCue.UpgradeTableFlip: AssignPracticeHighlight(0, _practiceCollection.TutorialArtworkRect, _practiceCollection.TutorialArtwork); break;
                case TutorialCue.UpgradeTableExplanation:
                case TutorialCue.UpgradeVariations:
                case TutorialCue.UpgradeTableReturn: AssignPracticeHighlight(0, _practiceCollection.TutorialUpgradeTableRect); break;
                case TutorialCue.UpgradePracticeCost: AssignPracticeHighlight(0, _practiceCollection.TutorialUpgradeButtonRect); break;
                case TutorialCue.UpgradePracticeResult:
                    AssignPracticeHighlight(0, _practiceCollection.TutorialOwnershipRect); AssignPracticeHighlight(1, _practiceCollection.TutorialHpRect); break;
            }
        }
        private bool AllowsUpgradePracticePointer(Vector2 point)
        {
            if (!CanPractice || !RequiresPlayerPractice) return false;
            foreach (var highlight in _practiceHighlights)
                if (highlight.Assigned && highlight.Frame.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(highlight.Frame, point, EventCameraFor(highlight.Frame))) return true;
            return false;
        }
        private void RefreshUpgradeDialogueLayout(RectTransform panel)
        {
            if (IsConclusionLesson)
            {
                Place(panel, new Vector2(.31f, .32f), new Vector2(.94f, .57f));
                Place(_guidePortrait.rectTransform, new Vector2(.04f, .22f), new Vector2(.30f, .61f));
            }
            else
            {
                Place(panel, new Vector2(.15f, .008f), new Vector2(.985f, .18f));
                Place(_guidePortrait.rectTransform, new Vector2(.006f, .002f), new Vector2(.15f, .17f));
            }
        }
    }
}
