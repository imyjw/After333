using System;
using System.Collections.Generic;
using Project333.Runtime.Presentation.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Draft
{
    public sealed partial class DraftSceneController
    {
        private bool _tutorialPracticeLoaded;
        private bool _tutorialPracticeUiInitialized;
        private IReadOnlyList<string> _tutorialPracticeDeck = Array.Empty<string>();
        private IDraftOverlayHost _tutorialDraftHost;
        private Action _tutorialPracticeReturn;
        private Action<string> _tutorialDeckPreviewReleased;
        private Action _tutorialGoldDungeonAction;
        private Func<bool> _tutorialCanInteract;
        private int _tutorialRemainingAttempts = 3;
        private bool _tutorialReconnectExample;
        private bool IsTutorialPracticeMode => _tutorialPracticeLoaded || TutorialRuntimeIsolation.IsActive;

        public DraftOverlayPresenter TutorialDraftPresenter => _draftOverlayPresenter;
        public Text TutorialDeckListText => _runtimeDeckPanelText ?? _runtimeDeckListDisplayText ?? _deckListText;

        public RectTransform TutorialGoldDungeonButtonRect => _startPveBattleButton?.GetComponent<RectTransform>();
        public RectTransform TutorialBattleButtonRect => _startPvpBattleButton?.GetComponent<RectTransform>();
        public RectTransform TutorialRunRecordRect => _recordText?.rectTransform;
        public RectTransform TutorialDeckListViewport => _deckListScrollRect?.viewport;

        public void ConfigureTutorialPractice(IReadOnlyList<string> localDeckCardIds,
            IDraftOverlayHost host = null, Action onReturn = null, Action<string> onDeckPreviewReleased = null,
            Action onGoldDungeon = null, Func<bool> canInteract = null, int remainingAttempts = 3,
            bool showReconnectExample = false)
        {
            if (!TutorialRuntimeIsolation.IsActive)
                throw new InvalidOperationException("A tutorial host must be active before borrowing the draft scene.");
            TryInitializeTutorialPractice();
            var copiedDeck = new List<string>();
            if (localDeckCardIds != null)
                for (var i = 0; i < localDeckCardIds.Count; i++) copiedDeck.Add(localDeckCardIds[i]);
            _tutorialPracticeDeck = copiedDeck.AsReadOnly();
            _tutorialDraftHost = ReferenceEquals(host, this) ? null : host;
            _tutorialPracticeReturn = onReturn;
            _tutorialDeckPreviewReleased = onDeckPreviewReleased;
            _tutorialGoldDungeonAction = onGoldDungeon;
            _tutorialCanInteract = canInteract;
            _tutorialRemainingAttempts = Mathf.Clamp(remainingAttempts, 0, 3);
            _tutorialReconnectExample = showReconnectExample;
            if (_tutorialDraftHost != null) _draftOverlayPresenter?.Bind(_tutorialDraftHost);
            RefreshTutorialPracticeUi();
        }

        private bool TryInitializeTutorialPractice()
        {
            if (!IsTutorialPracticeMode) return false;
            _tutorialPracticeLoaded = true;
            if (!_tutorialPracticeUiInitialized)
            {
                _tutorialPracticeUiInitialized = true;
                EnsureReturnToStartButton();
                EnsureBattleModeButtons();
                EnsureDeckListReferences();
                PrepareDeckListTextLayout();
                EnsureRuntimeDeckListDisplayText();
                EnsureRuntimeDeckPanelText();
                EnsureServerWalletDisplay();
                // Runtime-created wallet canvases must unload with the borrowed scene.
                if (_serverWalletCanvas != null && _serverWalletCanvas.transform.parent == null &&
                    _serverWalletCanvas.gameObject.scene != gameObject.scene)
                    SceneManager.MoveGameObjectToScene(_serverWalletCanvas.gameObject, gameObject.scene);
                _draftOverlayPresenter?.Hide();
            }
            RefreshTutorialPracticeUi();
            return true;
        }

        private void RefreshTutorialPracticeUi()
        {
            var count = _tutorialPracticeDeck.Count;
            if (_recordText != null) _recordText.text = "Wins: 0   Losses: 0";
            if (_lastResultText != null) _lastResultText.text = "Last Battle: -";
            if (_statusText != null) _statusText.text = string.Empty;
            ConfigureTutorialDeckList(_deckListText);
            ConfigureTutorialDeckList(_runtimeDeckListDisplayText);
            ConfigureTutorialDeckList(_runtimeDeckPanelText);
            if (_runtimeDeckSummaryText != null)
                _runtimeDeckSummaryText.text = $"{(count >= 33 ? "Battle Ready" : "Drafting")}   {count}/33 Cards";
            RefreshDeckListViewport();
            if (_serverWalletText != null)
                AccountWalletView.Attach(_serverWalletText, true, false)?.Render(true, 0, 0);
            var isDeckBuilding = IsDeckBuildingScene();
            RefreshBattleModeButton(_startPveBattleButton, $"골드 던전 ({_tutorialRemainingAttempts}/3)",
                $"골드 던전 ({count}/33)", _startPveBattleButtonLabel, count >= 33);
            RefreshBattleModeButton(_startPvpBattleButton, _tutorialReconnectExample ? "재접속 (70초)" : "전투", $"전투 ({count}/33)",
                _startPvpBattleButtonLabel, count >= 33);
            if (_startPveBattleButton != null) _startPveBattleButton.gameObject.SetActive(!isDeckBuilding);
            if (_startPvpBattleButton != null) _startPvpBattleButton.gameObject.SetActive(!isDeckBuilding);

            if (_startDraftButton != null) _startDraftButton.gameObject.SetActive(false);
            if (_claimRewardsButton != null) _claimRewardsButton.gameObject.SetActive(false);
            if (_returnToStartButton != null) _returnToStartButton.interactable = _tutorialPracticeReturn != null;
        }

        private void HandleTutorialDeckPreviewReleased(string cardId)
        {
            if (_tutorialCanInteract == null || _tutorialCanInteract()) _tutorialDeckPreviewReleased?.Invoke(cardId);
        }

        private void ConfigureTutorialDeckList(Text text)
        {
            if (text == null) return;
            DeckListCardHoldPreview.Configure(text, _tutorialPracticeDeck, ResolveCardAsset,
                "No drafted deck yet.", "•  ", upgradeLevelOverride: 0,
                previewReleased: HandleTutorialDeckPreviewReleased);
        }
    }
}
