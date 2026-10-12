using System;
using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Infrastructure.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Tutorial
{
    [DisallowMultipleComponent]
    public sealed partial class TutorialSceneController : MonoBehaviour, Project333.Runtime.Presentation.Draft.IDraftOverlayHost
    {
        public const string SceneName = "Tutorial_VSlice";
        public const string ReturnSceneName = "GameStart_VSlice";
        [SerializeField] private Sprite[] _guideIdleFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite _guideGoldFrame;
        [SerializeField] private CardDefinitionCatalogAsset _practiceCardCatalog;
        private TutorialSession _session;
        private Action _returnAction;
        private RectTransform _safeRoot;
        private Image _guidePortrait;
        private Text _dialogue, _stepCounter, _nextLabel;
        private Button _nextButton, _skipButton, _chapterSelectionButton;
        private GameObject _selectionModal;
        private int _lastIdleFrame = -1;
        private bool _returning;
        public TutorialSession Session => _session;

        public void Configure(Sprite[] guideIdleFrames, Sprite guideGoldFrame)
        { _guideIdleFrames = guideIdleFrames ?? Array.Empty<Sprite>(); _guideGoldFrame = guideGoldFrame; }
        public void ConfigureReturnAction(Action returnAction) { _returnAction = returnAction; }
        public void ConfigurePracticeCatalog(CardDefinitionCatalogAsset catalog) { _practiceCardCatalog = catalog; }
        private void Awake() { Initialize(); }
        // Native hand cards animate in LateUpdate. Fit guidance again after layout,
        // immediately before Canvas rendering, so card motion cannot leave an old outline.
        private void OnEnable() { Canvas.willRenderCanvases += RefreshPracticePresentationLayouts; }
        private void OnDisable()
        {
            Canvas.willRenderCanvases -= RefreshPracticePresentationLayouts;
            HidePracticeHighlights();
            HideChapterOneVisualAids();
        }
        public void Initialize()
        {
            if (_safeRoot != null) return;
            TutorialRuntimeIsolation.RequestEntry();
            _session = new TutorialSession();
            BuildView();
            PresentStep();
        }
        private void Update()
        {
            RefreshGuidePortrait();
            RefreshBattlePracticeInput();
            RefreshUpgradePractice();
        }
        private void LateUpdate() { RefreshPracticePresentationLayouts(); }
        private void RefreshPracticePresentationLayouts()
        {
            RefreshPracticeDialogueLayout();
            RefreshPracticeHighlightLayouts();
            RefreshChapterOneVisualAids();
        }
        private void RefreshGuidePortrait()
        {
            if (_guidePortrait == null) return;
            if (_guideGoldFrame != null && _session != null && !_session.IsComplete && !_session.IsExited &&
                (_session.CurrentStep.Cue == TutorialCue.Gold || _session.CurrentStep.Cue == TutorialCue.GoldDungeonReward))
                _guidePortrait.sprite = _guideGoldFrame;
            else if (_guideIdleFrames.Length > 0)
            {
                // Match the user's authored four-frame battle Idle at 12fps.
                var frame = Mathf.FloorToInt(Time.unscaledTime * 12f) % _guideIdleFrames.Length;
                if (frame != _lastIdleFrame || _guidePortrait.sprite == _guideGoldFrame)
                { _guidePortrait.sprite = _guideIdleFrames[frame]; _lastIdleFrame = frame; }
            }
        }
        private void OnDestroy()
        {
            DisposePracticeScene();
            DisposePracticeHighlights();
            TutorialRuntimeIsolation.CancelEntry();
        }
        private void Advance()
        {
            if (!CanPractice) return;
            if (_session.IsComplete) { ReturnToStart(); return; }
            if (_session.CurrentStep.RequiredAction == TutorialAction.CompleteDeck)
            { AutoCompleteTutorialDeck(); return; }
            if (_session.TryAdvance()) PresentStep();
        }
        private void StartPractice()
        { if (CanPractice && _session.PerformAction(TutorialAction.StartRun)) PresentStep(); }
        private bool CanPractice => (_practiceBattle == null || !_practiceBattle.IsTutorialResolving) && (_practiceCollection == null || !_practiceCollection.IsTutorialFlipAnimating) && !_returning && _selectionModal == null && !_session.IsExited && IsPracticeSceneReady;
        private void OnTutorialOfferSelected(int cardIndex)
        { if (CanPractice && _session.PerformAction(TutorialAction.PickCard, cardIndex)) PresentStep(); }
        private void OnTutorialPreviewReleased(bool isDeckList, int cardIndex)
        {
            var action = isDeckList ? TutorialAction.PreviewDeck : TutorialAction.PreviewOffer;
            if (CanPractice && _session.PerformAction(action, cardIndex)) PresentStep();
        }
        private void OnLobbyDeckPreviewReleased(string cardId)
        {
            // Only the highlighted row completes the mandatory preview exercise.
            if (_session.DraftCardIds.Count == 0 || cardId != _session.DraftCardIds[0]) return;
            OnTutorialPreviewReleased(true, 0);
        }
        private void StartPracticeGoldDungeon()
        { if (CanPractice && _session.PerformAction(TutorialAction.StartGoldDungeon)) PresentStep(); }
        private void AutoCompleteTutorialDeck()
        { if (CanPractice && _session.PerformAction(TutorialAction.CompleteDeck)) PresentStep(); }
        private void Restart()
        { CloseSelection(); _returning = false; _session.Restart(); PresentStep(); }
        private void ReturnToStart()
        {
            if (_returning) return;
            _returning = true;
            HidePracticeHighlights();
            HideChapterOneVisualAids();
            CancelPracticePreviews();
            _session.Skip();
            RefreshGuidePortrait();
            if (_returnAction != null) { _returnAction(); return; }
            if (UnityEngine.Application.CanStreamedLevelBeLoaded(ReturnSceneName)) SceneManager.LoadScene(ReturnSceneName);
            else { _returning = false; Debug.LogWarning("Tutorial return scene is unavailable."); }
        }
        private void PresentStep()
        {
            RefreshGuidePortrait();
            _dialogue.text = _session.CurrentStep.Dialogue;
            _stepCounter.text = _session.Chapter == 0 ? "개방 방주의 안내" :
                _session.Chapter == 1 ? "1. 게임에 대한 설명" :
                _session.Chapter == 2 ? "2. 덱 빌딩" : _session.Chapter == 3 ? "3. 게임 모드" : _session.Chapter == 4 ? "4. 자원 시스템과 전투 1" : _session.Chapter == 5 ? "5. 전투 2" : "6. 카드 업그레이드 시스템";
            _nextLabel.text = _session.IsComplete ? "튜토리얼 마치기"
                : _session.CurrentStep.RequiredAction == TutorialAction.CompleteDeck ? "계속" : "다음";
            RequestPracticeScene();
            RefreshChapterOneVisualAids();
        }
    }
}
