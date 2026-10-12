using System;
using Project333.Runtime.Presentation.Tutorial;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Startup
{
    public sealed partial class GameStartSceneController
    {
        // A borrowed scene stays local through its teardown, even if the host exits first.
        private bool _tutorialPracticeLoaded;
        private bool _tutorialPracticeUiInitialized;
        private int _tutorialPracticeTickets = 3;
        private Action _tutorialPracticeStart;
        private bool _tutorialPracticeStartEnabled;
        private bool IsTutorialPracticeMode => _tutorialPracticeLoaded || TutorialRuntimeIsolation.IsActive;

        public RectTransform TutorialStartButtonRect => _startGameButton == null
            ? null : _startGameButton.transform as RectTransform;

        public void ConfigureTutorialPractice(int tickets, Action onStart, bool enabled)
        {
            if (!TutorialRuntimeIsolation.IsActive)
                throw new InvalidOperationException("A tutorial host must be active before borrowing the start scene.");
            TryInitializeTutorialPractice();
            _tutorialPracticeTickets = Mathf.Max(0, tickets);
            _tutorialPracticeStart = onStart;
            _tutorialPracticeStartEnabled = enabled;
            RefreshTutorialPracticeUi();
        }

        private bool TryInitializeTutorialPractice()
        {
            if (!IsTutorialPracticeMode) return false;
            _tutorialPracticeLoaded = true;
            if (!_tutorialPracticeUiInitialized)
            {
                _tutorialPracticeUiInitialized = true;
                AutoAssignBackgroundImage();
                AutoAssignStartButtonImage();
                AutoAssignAccountInfoPanel();
                AutoAssignShopButton();
                AutoAssignOwnedCardsButton();
                AutoAssignReconnectPvpButton();
                AutoAssignAccountLoginGate();
                AutoAssignServerSettingsPanel();
                RemoveLegacyAccountManagementPanels();
                EnsureAccountInfoPanel();
                EnsureShopButton();
                EnsureOwnedCardsButton();
                EnsureReconnectPvpButton();
                EnsureBackgroundSprite();
                EnsureStartButtonSprite();
                ApplyBackgroundVisual();
                ApplyStartButtonVisual();
                ApplyAccountInfoVisual();
                ApplyShopButtonVisual();
                ApplyOwnedCardsButtonVisual();
                ApplyReconnectPvpButtonVisual();
                if (_startGameButton != null)
                {
                    _startGameButton.onClick.RemoveListener(StartGameFromUi);
                    _startGameButton.onClick.AddListener(StartGameFromUi);
                }
            }
            RefreshTutorialPracticeUi();
            return true;
        }

        private void RefreshTutorialPracticeUi()
        {
            if (_startGameButton != null)
                _startGameButton.interactable = true;
            if (_startGameButtonLabel != null) { _startGameButtonLabel.gameObject.SetActive(true); _startGameButtonLabel.enabled = true; _startGameButtonLabel.text = "게임 시작"; }
            if (_ticketText != null)
                AccountWalletView.Attach(_ticketText, false, false)?.Render(true, _tutorialPracticeTickets, 0);
            if (_accountInfoText != null)
                _accountInfoText.text = "튜토리얼";
            if (_statusText != null) _statusText.text = string.Empty;
            if (_shopButton != null) _shopButton.interactable = true;
            if (_ownedCardsButton != null) _ownedCardsButton.interactable = true;
            if (_reconnectPvpButton != null) _reconnectPvpButton.gameObject.SetActive(false);
            _accountLoginGate?.SetVisible(false);
            if (_serverSettingsPanel != null) _serverSettingsPanel.gameObject.SetActive(false);
            var entryRoot = transform.Find(TutorialEntryCanvasName);
            if (entryRoot != null) entryRoot.gameObject.SetActive(false);
        }

        private void InvokeTutorialStart()
        {
            if (!_tutorialPracticeStartEnabled || _tutorialPracticeStart == null) return;
            // The host owns ticket consumption and the required-action gate.
            _tutorialPracticeStartEnabled = false;
            RefreshTutorialPracticeUi();
            _tutorialPracticeStart.Invoke();
        }
    }
}
