using System;
using Project333.Runtime.Application.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Tutorial
{
    [DisallowMultipleComponent]
    public sealed partial class TutorialSceneController : MonoBehaviour
    {
        public const string SceneName = "Tutorial_VSlice";
        public const string ReturnSceneName = "GameStart_VSlice";
        [SerializeField] private Sprite[] _guideIdleFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite _guideGoldFrame;
        private TutorialSession _session;
        private Action _returnAction;
        private RectTransform _safeRoot, _lessonContent;
        private Image _guidePortrait;
        private Text _dialogue, _stepCounter, _nextLabel;
        private Button _nextButton;
        private GameObject _selectionModal;
        private Image _focusTarget;
        private int _lastIdleFrame = -1;
        private bool _returning;
        public TutorialSession Session => _session;

        public void Configure(Sprite[] guideIdleFrames, Sprite guideGoldFrame)
        { _guideIdleFrames = guideIdleFrames ?? Array.Empty<Sprite>(); _guideGoldFrame = guideGoldFrame; }
        public void ConfigureReturnAction(Action returnAction) { _returnAction = returnAction; }
        private void Awake() { Initialize(); }
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
            if (_focusTarget != null)
                _focusTarget.color = Color.Lerp(new Color(.20f,.28f,.34f,1), new Color(.42f,.34f,.13f,1),
                    .5f + .5f * Mathf.Sin(Time.unscaledTime * 4f));
        }
        private void RefreshGuidePortrait()
        {
            if (_guidePortrait == null) return;
            if (_guideGoldFrame != null && _session != null && !_session.IsComplete && !_session.IsExited &&
                _session.CurrentStep.Cue == TutorialCue.Gold)
                _guidePortrait.sprite = _guideGoldFrame;
            else if (_guideIdleFrames.Length > 0)
            {
                // Match GaebangChief's calm Idle: two quiet poses held for one second each.
                var frame = (int)Time.unscaledTime % 2 == 0 ? 0 : Mathf.Min(4, _guideIdleFrames.Length - 1);
                if (frame != _lastIdleFrame || _guidePortrait.sprite == _guideGoldFrame)
                { _guidePortrait.sprite = _guideIdleFrames[frame]; _lastIdleFrame = frame; }
            }
        }
        private void OnDestroy()
        {
            TutorialRuntimeIsolation.CancelEntry();
            foreach (var sprite in _generatedResourceSprites.Values)
            {
                if (sprite == null) continue;
                if (UnityEngine.Application.isPlaying) Destroy(sprite); else DestroyImmediate(sprite);
            }
            _generatedResourceSprites.Clear();
        }
        private void Advance()
        { if (_selectionModal == null && _session.TryAdvance()) PresentStep(); }
        private void StartPractice()
        { if (_selectionModal == null && _session.PerformAction(TutorialAction.StartRun)) PresentCompletion(); }
        private void Restart()
        { CloseSelection(); _returning = false; _session.Restart(); PresentStep(); }
        private void ReturnToStart()
        {
            if (_returning) return;
            _returning = true;
            _session.Skip();
            RefreshGuidePortrait();
            if (_returnAction != null) { _returnAction(); return; }
            if (UnityEngine.Application.CanStreamedLevelBeLoaded(ReturnSceneName)) SceneManager.LoadScene(ReturnSceneName);
            else { _returning = false; Debug.LogWarning("Tutorial return scene is unavailable."); }
        }
        private void PresentStep()
        {
            ClearLesson();
            RefreshGuidePortrait();
            _dialogue.text = _session.CurrentStep.Dialogue;
            _stepCounter.text = _session.StepIndex < TutorialSession.FirstChapterStep ? "개방 방주의 안내" : "1. 게임에 대한 설명";
            _nextButton.gameObject.SetActive(true);
            _nextButton.interactable = !_session.WaitingForAction;
            _nextLabel.text = _session.WaitingForAction ? "실습 중" : "다음";
            switch (_session.CurrentStep.Cue)
            {
                case TutorialCue.Civilizations:
                case TutorialCue.Resources: ShowCivilizations(); break;
                case TutorialCue.Gold: ShowGold(); break;
                case TutorialCue.Substitution: ShowSubstitution(); break;
                case TutorialCue.MixedDeck: ShowCards("대륙을 넘나드는 덱", new[] { "Goblin", "Gwangma", "A-212" }, new[] { "판타지", "무림", "과학 문명" }); break;
                case TutorialCue.CardKinds: ShowCards("세 가지 카드 종류", new[] { "Goblin", "ManaPond", "firebolt" }, new[] { "유닛 · 병력을 소환", "건물 · 자원을 생산", "마법 · 전세를 바꾼다" }); break;
                case TutorialCue.Deck: ShowDeck(); break;
                case TutorialCue.Run: ShowRun(); break;
                case TutorialCue.StartPractice: ShowStartPractice(); break;
                case TutorialCue.Controls:
                    ShowWelcome("언제든 다시 배울 수 있네", "왼쪽 위의 스킵과 튜토리얼 선택 버튼을 이용하세요.");
                    _focusTarget = _safeRoot.Find(_session.StepIndex == 2 ? "SkipButton" : "TutorialSelectionButton").GetComponent<Image>();
                    break;
                case TutorialCue.Isolation: ShowWelcome("부담 없는 연습", "실제 티켓 · 골드 · 카드 · 전적은 바뀌지 않습니다."); break;
                default: ShowWelcome("After333", "개방 방주와 함께 배우는 첫 번째 수업"); break;
            }
        }
        private void ClearLesson()
        {
            _focusTarget = null;
            for (var i = _lessonContent.childCount - 1; i >= 0; i--)
            { var child = _lessonContent.GetChild(i).gameObject; child.SetActive(false); RemoveObject(child); }
        }
    }
}
