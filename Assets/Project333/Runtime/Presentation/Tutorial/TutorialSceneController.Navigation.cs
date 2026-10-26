using System.Collections.Generic;
using Project333.Runtime.Application.Tutorial;
using UnityEngine;

namespace Project333.Runtime.Presentation.Tutorial
{
    public sealed partial class TutorialSceneController
    {
        // Reviewing changes only the displayed dialogue. The actual lesson, scene and
        // completed actions stay at their current position and are never replayed.
        private sealed class DialoguePage
        {
            public int StepIndex, Chapter;
            public TutorialStep Step;
        }
        private readonly List<DialoguePage> _dialoguePages = new List<DialoguePage>();
        private int _dialoguePageIndex = -1;
        private int _dialogueRevision = -1;
        private bool IsReviewingDialogue => _dialoguePageIndex >= 0 && _dialoguePageIndex < _dialoguePages.Count - 1;
        private TutorialStep DisplayedDialogueStep => _dialoguePageIndex >= 0 ? _dialoguePages[_dialoguePageIndex].Step : _session.CurrentStep;
        private int DisplayedDialogueChapter => _dialoguePageIndex >= 0 ? _dialoguePages[_dialoguePageIndex].Chapter : _session.Chapter;

        private void RecordDialoguePage()
        {
            if (_dialogueRevision != _session.Revision)
            {
                _dialoguePages.Clear();
                _dialogueRevision = _session.Revision;
            }
            var existing = _dialoguePages.FindIndex(page => page.StepIndex == _session.StepIndex);
            if (existing >= 0)
            {
                // Deselecting a mulligan returns to its selection instruction. Trim
                // that branch rather than accumulating duplicate selection pages.
                _dialoguePages.RemoveRange(existing + 1, _dialoguePages.Count - existing - 1);
            }
            else _dialoguePages.Add(new DialoguePage { StepIndex = _session.StepIndex, Chapter = _session.Chapter, Step = _session.CurrentStep });
            _dialoguePageIndex = _dialoguePages.Count - 1;
        }

        private void PreviousDialogue()
        {
            if (!CanNavigateDialogue || _dialoguePageIndex <= 0) return;
            CancelPracticePreviews();
            _dialoguePageIndex--;
            PresentDialoguePage();
        }

        private void PresentDialoguePage()
        {
            var chapter = DisplayedDialogueChapter;
            _dialogue.text = DisplayedDialogueStep.Dialogue;
            _stepCounter.text = chapter == 0 ? "개방 방주의 안내" :
                chapter == 1 ? "1. 게임에 대한 설명" : chapter == 2 ? "2. 덱 빌딩" :
                chapter == 3 ? "3. 게임 모드" : chapter == 4 ? "4. 자원 시스템과 전투 1" :
                chapter == 5 ? "5. 전투 2" : "6. 카드 업그레이드 시스템";
            _nextLabel.text = IsReviewingDialogue ? "다음" : _session.IsComplete ? "튜토리얼 마치기" :
                _session.CurrentStep.RequiredAction == TutorialAction.CompleteDeck ? "계속" : "다음";
            RefreshGuidePortrait();
            RefreshDialogueInputBlocker();
            ConfigurePracticeHighlights();
            RefreshPracticeAdvanceButton();
            RefreshChapterOneVisualAids();
        }

        private void RefreshDialogueInputBlocker()
        {
            if (_practiceInputBlocker != null)
                _practiceInputBlocker.gameObject.SetActive(IsReviewingDialogue || _session.PracticeBattleStarted || _session.Chapter == 6);
        }

        private void RefreshDialogueNavigationLayout()
        {
            if (_previousButton == null || _nextButton == null || _dialogue == null) return;
            var panel = _dialogue.rectTransform.parent as RectTransform;
            if (panel == null) return;
            var wide = panel.rect.width >= 900f;
            // Small combat guidance panels need a bottom row; keep the full line
            // width for dialogue instead of squeezing it between two buttons.
            var text = _dialogue.rectTransform;
            Place(text, new Vector2(0f, wide ? .05f : 0f), new Vector2(1f, .72f));
            text.offsetMin = new Vector2(22f, wide ? 8f : 60f);
            text.offsetMax = new Vector2(wide ? -242f : -18f, -3f);
            PlaceDialogueButton(_nextButton.GetComponent<RectTransform>(), 16f, wide);
            PlaceDialogueButton(_previousButton.GetComponent<RectTransform>(), 126f, wide);
            // During required combat practice Next is hidden. Its slot is reused
            // by the existing action hint, beside Previous rather than over it.
            if (_combatActionHint != null) PlaceDialogueButton(_combatActionHint.rectTransform, 16f, wide);
        }

        private static void PlaceDialogueButton(RectTransform rect, float right, bool wide)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, wide ? .35f : 0f);
            rect.pivot = new Vector2(1f, wide ? .5f : 0f);
            rect.sizeDelta = new Vector2(100f, wide ? 56f : 44f);
            rect.anchoredPosition = new Vector2(-right, wide ? 0f : 8f);
        }
    }
}
