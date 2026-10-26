using System;
using Project333.Runtime.Presentation.Development;
using Project333.Runtime.Presentation.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project333.Runtime.Presentation.Startup
{
    public sealed partial class GameStartSceneController
    {
        public const string TutorialSceneName = "Tutorial_VSlice";
        private const string TutorialEntryCanvasName = "TutorialEntryCanvas";
        private const int TutorialEntrySortingOrder = 5001;
        private UnityEngine.UI.Button _tutorialButton;

        public void OpenTutorialSceneFromUi()
        {
            if (IsTutorialPracticeMode) return;
            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(TutorialSceneName))
            {
                SimpleNoticeToast.Show("GameStartNoticeToastCanvas", "튜토리얼을 불러올 수 없습니다.");
                return;
            }

            // Tutorial owns fresh local state. Entering it never starts or changes a real run.
            TutorialRuntimeIsolation.RequestEntry();
            try
            {
                SceneManager.LoadScene(TutorialSceneName);
            }
            catch (Exception exception)
            {
                TutorialRuntimeIsolation.CancelEntry();
                Debug.LogWarning($"After333 tutorial navigation failed: {exception.Message}");
                SimpleNoticeToast.Show("GameStartNoticeToastCanvas", "튜토리얼을 불러올 수 없습니다.");
            }
        }

        private void EnsureTutorialEntryButton()
        {
            var entryRoot = transform.Find(TutorialEntryCanvasName) as RectTransform;
            if (entryRoot == null)
            {
                var entryObject = new GameObject(
                    TutorialEntryCanvasName,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(UnityEngine.UI.GraphicRaycaster));
                entryRoot = entryObject.GetComponent<RectTransform>();
                entryRoot.SetParent(transform, false);
                entryRoot.anchorMin = Vector2.zero;
                entryRoot.anchorMax = Vector2.one;
                entryRoot.offsetMin = Vector2.zero;
                entryRoot.offsetMax = Vector2.zero;
            }

            // This stays above the ordinary menu. The login gate now covers and
            // blocks tutorial entry until the player returns to the start menu.
            var canvas = entryRoot.GetComponent<Canvas>();
            if (entryRoot.GetComponentInParent<Canvas>() == canvas && canvas.rootCanvas == canvas)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            canvas.overrideSorting = true;
            canvas.sortingOrder = TutorialEntrySortingOrder;

            if (_tutorialButton == null)
            {
                var existingButton = entryRoot.Find("TutorialButton");
                _tutorialButton = existingButton == null
                    ? null
                    : existingButton.GetComponent<UnityEngine.UI.Button>();
            }

            if (_tutorialButton == null)
            {
                var buttonObject = new GameObject(
                    "TutorialButton",
                    typeof(RectTransform),
                    typeof(UnityEngine.UI.Image),
                    typeof(UnityEngine.UI.Button));
                var rect = buttonObject.GetComponent<RectTransform>();
                rect.SetParent(entryRoot, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(36f, 32f);
                rect.sizeDelta = new Vector2(276f, 76f);
                _tutorialButton = buttonObject.GetComponent<UnityEngine.UI.Button>();

                var labelObject = new GameObject(
                    "Label",
                    typeof(RectTransform),
                    typeof(UnityEngine.UI.Text));
                var labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.SetParent(rect, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(16f, 8f);
                labelRect.offsetMax = new Vector2(-16f, -8f);
                var label = labelObject.GetComponent<UnityEngine.UI.Text>();
                label.font = NoticeTheme.Font != null ? NoticeTheme.Font : ResolveRuntimeFont();
                label.text = "튜토리얼";
                label.supportRichText = false;
                label.raycastTarget = false;
                NoticeTheme.Button(_tutorialButton);
            }

            _tutorialButton.onClick.RemoveListener(OpenTutorialSceneFromUi);
            _tutorialButton.onClick.AddListener(OpenTutorialSceneFromUi);
            RefreshTutorialEntryButton();
        }

        private void RefreshTutorialEntryButton()
        {
            if (_tutorialButton == null) return;
            _tutorialButton.gameObject.SetActive(true);
            _tutorialButton.interactable = true;
        }

        private void UnbindTutorialEntryButton()
        {
            if (_tutorialButton != null)
                _tutorialButton.onClick.RemoveListener(OpenTutorialSceneFromUi);
        }
    }
}