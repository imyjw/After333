using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Hand;
using Project333.Runtime.Presentation.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Tutorial
{
    public sealed partial class TutorialSceneController
    {
        private Text _practiceResourceNote;
        private void OnBattlePracticeCompleted(TutorialAction action)
        { if (CanPractice && _session.PerformAction(action)) PresentStep(); }
        private void OnMulliganSelectionCanceled()
        { if (CanPractice && _session.CancelMulliganSelection()) PresentStep(); }
        private void RefreshBattlePracticeInput()
        {
            if (_practiceBattle == null || _session.Chapter < 4) return;
            RefreshPracticeAdvanceButton();
            if (_session.Chapter == 5) { RefreshCombatPracticeGuidance(); return; }
            // A collapsed-hand drag also expands through the common HandPresenter.
            if (CanPractice && _session.CurrentStep.RequiredAction == TutorialAction.ExpandHand &&
                _practiceBattle.TutorialScreen?.TutorialHand?.IsExpanded == true)
                OnBattlePracticeCompleted(TutorialAction.ExpandHand);
        }
        private Image PracticeHandArtwork(string cardId)
        {
            var hand = _practiceBattle?.TutorialScreen?.TutorialHand;
            if (hand == null) return null;
            foreach (var view in hand.HandCardViews)
                if (view != null && view.HasCard && view.CardId == cardId)
                    return view.GetComponent<HandCardTextView>()?.TutorialArtwork;
            return null;
        }
        private RectTransform PracticeTile(TileCoord coord, PlayerId owner = PlayerId.Player)
        {
            var board = _practiceBattle?.TutorialScreen?.BoardPresenter;
            if (board == null) return null;
            foreach (var tile in owner == PlayerId.Player ? board.PlayerTileViews : board.AITileViews)
                if (tile != null && tile.Coord.Equals(coord)) return tile.GetComponent<TileTextView>()?.ActiveEffectButtonAnchor;
            return null;
        }
        private void ConfigureBattlePracticeHighlights()
        {
            var screen = _practiceBattle.TutorialScreen;
            if (screen == null) return;
            if (_session.Chapter == 5) { ConfigureCombatPracticeHighlights(); return; }
            var action = _session.CurrentStep.RequiredAction;
            var resources = FindInScene<ResourceBarTextView>(_practiceScene);
            switch (action)
            {
                case TutorialAction.SelectMulliganCard:
                    var card = screen.TutorialMulligan?.TutorialCardArtwork(2);
                    AssignPracticeHighlight(0, card?.rectTransform, card);
                    break;
                case TutorialAction.ConfirmMulligan:
                    AssignPracticeHighlight(0, screen.TutorialMulligan?.TutorialConfirmRect);
                    break;
                case TutorialAction.ExpandHand:
                    var index = 0;
                    foreach (var handCard in screen.TutorialHand.HandCardViews)
                    {
                        if (handCard == null || !handCard.HasCard) continue;
                        var art = handCard.GetComponent<HandCardTextView>()?.TutorialArtwork;
                        AssignPracticeHighlight(index++, art?.rectTransform, art, matchArtworkSorting: true);
                    }
                    break;
                case TutorialAction.SelectGoblin:
                    var goblin = PracticeHandArtwork("Goblin"); AssignPracticeHighlight(0, goblin?.rectTransform, goblin, matchArtworkSorting: true); break;
                case TutorialAction.SummonGoblin:
                    AssignPracticeHighlight(0, PracticeTile(TutorialResourceBattle.GoblinTile)); break;
                case TutorialAction.SelectManaPond:
                    var pond = PracticeHandArtwork("ManaPond"); AssignPracticeHighlight(0, pond?.rectTransform, pond, matchArtworkSorting: true); break;
                case TutorialAction.SummonManaPond:
                    AssignPracticeHighlight(0, PracticeTile(TutorialResourceBattle.PondTile)); break;
                case TutorialAction.EndTurn:
                    AssignPracticeHighlight(0, screen.TutorialEndTurnRect); break;
                default:
                    var cue = _session.CurrentStep.Cue;
                    if (cue == TutorialCue.MasterObjective || cue == TutorialCue.MasterStats)
                    { AssignPracticeHighlight(0, PracticeTile(TutorialResourceBattle.MasterTile)); AssignPracticeHighlight(1, PracticeTile(TutorialResourceBattle.MasterTile, PlayerId.AI)); }
                    else if (cue == TutorialCue.OpponentResources) AssignPracticeHighlight(0, resources?.TutorialOpponentResourcesRect);
                    else if (cue == TutorialCue.DrawReturned)
                    { var fireball = PracticeHandArtwork("firebolt"); AssignPracticeHighlight(0, fireball?.rectTransform, fireball, matchArtworkSorting: true); }
                    else if (cue == TutorialCue.PlayerResources || cue == TutorialCue.TurnIncome || cue == TutorialCue.UniqueResources ||
                        cue == TutorialCue.CarryResources || cue == TutorialCue.GoldSeparation || cue == TutorialCue.PracticeGrant || cue == TutorialCue.ResourcesReturned)
                        AssignPracticeHighlight(0, resources?.TutorialPlayerResourcesRect);
                    break;
            }
        }
        private bool AllowsBattlePracticePointer(Vector2 point, Camera eventCamera)
        {
            if (_session.Chapter == 6) return AllowsUpgradePracticePointer(point);
            if (_practiceBattle == null || _session.Chapter < 4 || !CanPractice || !RequiresPlayerPractice) return false;
            foreach (var highlight in _practiceHighlights)
                if (highlight.Assigned && highlight.Frame.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(highlight.Frame, point, EventCameraFor(highlight.Frame))) return true;
            var action = _session.CurrentStep.RequiredAction;
            // Allow cancel/reselect without spending or accidentally using another card.
            Image card = action == TutorialAction.ConfirmMulligan ? _practiceBattle.TutorialScreen.TutorialMulligan.TutorialCardArtwork(2) :
                action == TutorialAction.SummonGoblin ? PracticeHandArtwork("Goblin") : action == TutorialAction.SummonManaPond ? PracticeHandArtwork("ManaPond") : null;
            return card != null && RectTransformUtility.RectangleContainsScreenPoint(card.rectTransform, point, EventCameraFor(card.rectTransform));
        }
        private void RefreshBattlePracticeDialogueLayout(RectTransform panel)
        {
            var mulligan = _practiceBattle.IsMulliganPresentationActive || _practiceBattle.CurrentBattleState?.Phase == PhaseType.Mulligan;
            if (_session.Chapter == 5 && TileTextView.VisibleCardPreviewArtwork != null)
            {
                Place(panel, new Vector2(.24f,.018f), new Vector2(.77f,.19f));
                Place(_guidePortrait.rectTransform, new Vector2(.155f,.008f), new Vector2(.235f,.18f));
            }
            else if (mulligan)
            {
                // The real Confirm button stays centered below the cards. Guidance sits to its left.
                var right = .39f;
                var confirm = _practiceBattle.TutorialScreen?.TutorialMulligan?.TutorialConfirmRect;
                if (confirm != null)
                {
                    var bounds = PracticeOverlayBounds(confirm, confirm.rect, EventCameraFor(confirm));
                    right = Mathf.Min(right, (bounds.xMin - _safeRoot.rect.xMin - 18f) / _safeRoot.rect.width);
                }
                Place(panel, new Vector2(.09f,.018f), new Vector2(Mathf.Max(.27f,right),.23f));
                Place(_guidePortrait.rectTransform, new Vector2(.003f,.008f), new Vector2(.085f,.22f));
            }
            else
            {
                // Keep both resource rows, both tile columns, the hand and End button uncovered.
                var bottom = .49f;
                var hand = _practiceBattle.TutorialScreen?.TutorialHand;
                if (hand != null)
                {
                    foreach (var view in hand.HandCardViews)
                    {
                        var artwork = view != null && view.HasCard ? view.GetComponent<HandCardTextView>()?.TutorialArtwork : null;
                        if (artwork == null || !artwork.gameObject.activeInHierarchy) continue;
                        var rendered = HandCardStatOverlayLayout.CalculateRenderedSpriteRect(artwork);
                        var bounds = PracticeOverlayBounds(artwork.rectTransform, rendered, EventCameraFor(artwork.rectTransform));
                        bottom = Mathf.Max(bottom, (bounds.yMax + 16f - _safeRoot.rect.yMin) / _safeRoot.rect.height);
                    }
                }
                bottom = Mathf.Min(bottom, .70f);
                var right = .725f;
                var board = _practiceBattle.TutorialScreen?.BoardPresenter;
                if (board != null)
                    foreach (var tile in board.AITileViews)
                    {
                        var target = tile?.GetComponent<TileTextView>()?.ActiveEffectButtonAnchor;
                        if (target == null) continue;
                        var bounds = PracticeOverlayBounds(target, target.rect, EventCameraFor(target));
                        right = Mathf.Min(right, (bounds.xMin - _safeRoot.rect.xMin - 18f) / _safeRoot.rect.width);
                    }
                Place(panel, new Vector2(.38f,bottom), new Vector2(right,bottom + .20f));
                Place(_guidePortrait.rectTransform, new Vector2(.30f,bottom), new Vector2(.378f,bottom + .19f));
            }
            if (_practiceResourceNote == null)
            {
                _practiceResourceNote = Label(_safeRoot, "PracticeResourceNote", string.Empty, 24, Gold, TextAnchor.MiddleCenter);
                Place(_practiceResourceNote.rectTransform, new Vector2(.30f,.74f),new Vector2(.74f,.81f));
            }
            var cue = _session.CurrentStep.Cue;
            _practiceResourceNote.gameObject.SetActive(!IsReviewingDialogue && (cue == TutorialCue.PracticeGrant || cue == TutorialCue.ResourcesReturned));
            _practiceResourceNote.text = cue == TutorialCue.PracticeGrant ? "연습 자원: 마나 +" + _practiceBattle.TutorialManaGrant + " · 골드 +" + _practiceBattle.TutorialGoldGrant : "골드 +3 · 마나 +3 · 카드 +1";
        }
    }
}
