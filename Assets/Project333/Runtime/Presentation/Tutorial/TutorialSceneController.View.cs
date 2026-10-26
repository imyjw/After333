using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Tutorial
{
    public sealed partial class TutorialSceneController
    {
        private static readonly Color Gold = new Color(.95f, .79f, .40f, 1);
        private void BuildView()
        {
            // Keep the actual game scene full-screen. Only guidance is overlaid.
            var host = new GameObject("TutorialCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            host.transform.SetParent(transform, false);
            var canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            var scaler = host.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            _safeRoot = Rect(host.transform,"SafeAreaRoot",Vector2.zero,Vector2.one);
            _safeRoot.gameObject.AddComponent<SafeAreaFitter>().Configure();
            _practiceInputBlocker = Rect(_safeRoot, "PracticeInputBlocker", Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            _practiceInputBlocker.color = Color.clear;
            _practiceInputBlocker.gameObject.AddComponent<TutorialPracticeInputBlocker>().AllowsPointer = AllowsBattlePracticePointer;
            _practiceInputBlocker.gameObject.SetActive(false);
            _skipButton = MakeButton(_safeRoot,"SkipButton","스킵",ReturnToStart);
            _chapterSelectionButton = MakeButton(_safeRoot,"TutorialSelectionButton","튜토리얼 선택",OpenSelection);
            RefreshTutorialControlsLayout();
            _guidePortrait = Rect(_safeRoot,"GuidePortrait",new Vector2(.006f,.002f),new Vector2(.15f,.17f)).gameObject.AddComponent<Image>();
            _guidePortrait.preserveAspect = true; _guidePortrait.raycastTarget = false;
            if (_guideIdleFrames.Length > 0) _guidePortrait.sprite = _guideIdleFrames[0];
            var dialoguePanel = Panel(_safeRoot,"DialoguePanel",new Vector2(.15f,.016f),new Vector2(.985f,.165f));
            Project333.Runtime.Presentation.Hand.PreserveExpandedHand.Attach(dialoguePanel.gameObject);
            var speaker = Label(dialoguePanel,"Speaker","개방 방주",25,Gold,TextAnchor.MiddleLeft);
            Place(speaker.rectTransform,new Vector2(0,.72f),new Vector2(.45f,1));
            speaker.rectTransform.offsetMin = new Vector2(22,0);
            _stepCounter = Label(dialoguePanel,"StepCounter",string.Empty,21,new Color(.7f,.79f,.86f),TextAnchor.MiddleRight);
            Place(_stepCounter.rectTransform,new Vector2(.45f,.72f),Vector2.one);
            _stepCounter.rectTransform.offsetMax = new Vector2(-22,0);
            _dialogue = Label(dialoguePanel,"DialogueText",string.Empty,26,Color.white,TextAnchor.UpperLeft);
            Place(_dialogue.rectTransform,new Vector2(0,.05f),new Vector2(.875f,.72f));
            _dialogue.rectTransform.offsetMin = new Vector2(22,8); _dialogue.rectTransform.offsetMax = new Vector2(-18,-3);
            _dialogue.resizeTextForBestFit = true; _dialogue.resizeTextMinSize = 19; _dialogue.resizeTextMaxSize = 26;
            _previousButton = MakeButton(dialoguePanel,"PreviousButton","이전",PreviousDialogue);
            _nextButton = MakeButton(dialoguePanel,"NextButton","다음",Advance);
            _nextLabel = _nextButton.GetComponentInChildren<Text>();
            foreach (var button in new[] { _previousButton, _nextButton })
            {
                var label = button.GetComponentInChildren<Text>();
                label.fontSize = 25; label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 16; label.resizeTextMaxSize = 25;
            }
            RefreshDialogueNavigationLayout();
            BuildChapterOneVisualAids();
        }
        private void RefreshTutorialControlsLayout()
        {
            // The native battle HUD occupies the far upper-left corner. Keep the
            // tutorial controls in the free space between it and the opponent hand.
            var battleEntry = _session.PracticeBattleStarted && _session.Chapter < 6;
            TopButton(_skipButton.GetComponent<RectTransform>(), battleEntry ? 440 : 24, battleEntry ? 115 : 140);
            TopButton(_chapterSelectionButton.GetComponent<RectTransform>(), battleEntry ? 567 : 180, battleEntry ? 200 : 250);
            _chapterSelectionButton.GetComponentInChildren<Text>().fontSize = battleEntry ? 26 : 29;
        }
        private void OpenSelection()
        {
            if (_selectionModal != null || _returning) return;
            CancelPracticePreviews();
            HidePracticeHighlights();
            HideChapterOneVisualAids();
            var shade = Rect(_safeRoot,"TutorialSelectionModal",Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();
            shade.color = new Color(0,0,0,.76f); _selectionModal = shade.gameObject;
            var panel = Panel(shade.rectTransform,"ChapterList",new Vector2(.20f,.18f),new Vector2(.80f,.87f));
            var title = Label(panel,"Title","튜토리얼 선택",40,Gold,TextAnchor.MiddleCenter);
            Place(title.rectTransform,new Vector2(.05f,.83f),new Vector2(.95f,.98f));
            var names = new[] { "게임에 대한 설명", "덱 빌딩", "게임 모드", "자원 시스템과 전투 1", "전투 2", "카드 업그레이드 시스템" };
            for (var i = 0; i < names.Length; i++)
            {
                var chapter = i + 1;
                var button = MakeButton(panel,"Chapter"+chapter,chapter+". "+names[i]+string.Empty,
                    () => { if (_session.SelectChapter(chapter)) { CloseSelection(); PresentStep(); } });
                Place(button.GetComponent<RectTransform>(),new Vector2(.07f,.72f-i*.095f),new Vector2(.93f,.80f-i*.095f));
                button.interactable = true; button.GetComponentInChildren<Text>().fontSize = 27;
            }
            var close = MakeButton(panel,"CloseSelectionButton","닫기",CloseSelection);
            Place(close.GetComponent<RectTransform>(),new Vector2(.34f,.035f),new Vector2(.66f,.13f));
        }
        private void CloseSelection()
        {
            if (_selectionModal == null) return;
            var old = _selectionModal; _selectionModal = null; old.SetActive(false); RemoveObject(old);
            ConfigurePracticeHighlights();
            RefreshChapterOneVisualAids();
        }
        private static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max)
        {
            var go = new GameObject(name,typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent,false); Place(rect,min,max); return rect;
        }
        private static void Place(RectTransform rect,Vector2 min,Vector2 max)
        { rect.anchorMin=min; rect.anchorMax=max; rect.pivot=new Vector2(.5f,.5f); rect.offsetMin=rect.offsetMax=Vector2.zero; }
        private static RectTransform Panel(Transform parent,string name,Vector2 min,Vector2 max)
        {
            var rect=Rect(parent,name,min,max); NoticeTheme.Panel(rect.gameObject.AddComponent<Image>(),false); return rect;
        }
        private static Text Label(Transform parent,string name,string text,int size,Color color,TextAnchor anchor)
        {
            var label=Rect(parent,name,Vector2.zero,Vector2.one).gameObject.AddComponent<Text>();
            NoticeTheme.Text(label,size); if(label.font==null)label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text=text; label.color=color; label.alignment=anchor; label.raycastTarget=false;
            label.verticalOverflow=VerticalWrapMode.Truncate; return label;
        }
        private static Button MakeButton(Transform parent,string name,string text,UnityEngine.Events.UnityAction action)
        {
            var rect=Rect(parent,name,Vector2.zero,Vector2.one); rect.gameObject.AddComponent<Image>();
            var button=rect.gameObject.AddComponent<Button>();
            var label=Label(rect,"Label",text,29,Color.white,TextAnchor.MiddleCenter);
            label.rectTransform.offsetMin=new Vector2(8,2); label.rectTransform.offsetMax=new Vector2(-8,-2);
            NoticeTheme.Button(button); button.onClick.AddListener(action); return button;
        }
        private static void TopButton(RectTransform rect,float x,float width)
        {
            rect.anchorMin=rect.anchorMax=new Vector2(0,1); rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-18); rect.sizeDelta=new Vector2(width,64);
        }
        private static void RemoveObject(GameObject target)
        { if(UnityEngine.Application.isPlaying) Destroy(target); else DestroyImmediate(target); }
    }
}
