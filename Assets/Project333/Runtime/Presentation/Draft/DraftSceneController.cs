using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Development;
using Project333.Runtime.Presentation.Online;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Project333.Runtime.Presentation.Draft
{
    public sealed class DraftSceneController : MonoBehaviour, IDraftOverlayHost
    {
        private const int CompletedRunServerSyncMaxAttempts = 5;
        private const float CompletedRunServerSyncRetryDelaySeconds = 1.25f;

        [SerializeField] private CardDefinitionCatalogAsset _cardCatalogAsset;
        [SerializeField] private DraftOverlayPresenter _draftOverlayPresenter;
        [SerializeField] private Text _recordText;
        [SerializeField] private Text _lastResultText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _deckListText;
        [SerializeField] private ScrollRect _deckListScrollRect;
        [SerializeField] private Button _startDraftButton;
        [SerializeField] private Button _startBattleButton;
        [SerializeField] private Button _startPveBattleButton;
        [SerializeField] private Button _startPvpBattleButton;
        [SerializeField] private Button _returnToStartButton;
        [SerializeField] private Button _claimRewardsButton;
        [SerializeField] private Text _startBattleButtonLabel;
        [SerializeField] private Text _startPveBattleButtonLabel;
        [SerializeField] private Text _startPvpBattleButtonLabel;
        [SerializeField] private Text _claimRewardsButtonLabel;
        [SerializeField] private bool _autoOpenDraftIfNoDeck = true;
        [SerializeField] private bool _useFixedDraftSeed;
        [SerializeField] private int _fixedDraftSeed = 333;
        [SerializeField] private string _startSceneName = "GameStart_VSlice";
        [SerializeField] private string _draftSceneName = "Draft_VSlice";
        [SerializeField] private string _deckBuildingSceneName = "DeckBuilding_VSlice";
        [SerializeField] private string _battleSceneName = "Battle_VSlice";
        [SerializeField] private string _accountServerUrl = "http://127.0.0.1:7333";
        [Header("Draft Runtime UI Layout")]
        [SerializeField] private bool _autoCreateMissingDraftUi = true;
        [SerializeField] private bool _applyRuntimeDraftUiLayout;
        [SerializeField] private Vector2 _pveBattleButtonPosition = new Vector2(-176f, -108f);
        [SerializeField] private Vector2 _pvpBattleButtonPosition = new Vector2(176f, -108f);
        [SerializeField] private Vector2 _battleModeButtonSize = new Vector2(300f, 72f);
        [SerializeField] private Vector2 _battleModeButtonLabelPadding = new Vector2(10f, 10f);
        [SerializeField] private int _battleModeButtonFontSize = 24;
        [SerializeField] private Color _pveBattleButtonColor = new Color(0.26f, 0.24f, 0.13f, 1f);
        [SerializeField] private Color _pvpBattleButtonColor = new Color(0.1f, 0.22f, 0.46f, 1f);
        [SerializeField] private string _pveBattleButtonReadyLabel = "PVE 시작";
        [SerializeField] private string _pvpBattleButtonReadyLabel = "PVP 매칭";
        [SerializeField] private string _pveBattleButtonLockedLabelFormat = "PVE 잠김 ({0}/33)";
        [SerializeField] private string _pvpBattleButtonLockedLabelFormat = "PVP 잠김 ({0}/33)";
        [SerializeField] private Vector2 _returnToStartButtonPosition = new Vector2(0f, -204f);
        [SerializeField] private Vector2 _returnToStartButtonSize = new Vector2(340f, 72f);
        [SerializeField] private Vector2 _returnToStartButtonLabelPadding = new Vector2(12f, 12f);
        [SerializeField] private int _returnToStartButtonFontSize = 28;
        [SerializeField] private Color _returnToStartButtonColor = new Color(0.24f, 0.24f, 0.28f, 1f);
        [SerializeField] private string _returnToStartButtonLabel = "Back To Start";
        [SerializeField] private Vector2 _claimRewardsButtonPosition = new Vector2(0f, -28f);
        [SerializeField] private Vector2 _claimRewardsButtonSize = new Vector2(340f, 72f);
        [SerializeField] private Vector2 _claimRewardsButtonLabelPadding = new Vector2(12f, 12f);
        [SerializeField] private int _claimRewardsButtonFontSize = 28;
        [SerializeField] private Color _claimRewardsButtonColor = new Color(0.48f, 0.32f, 0.08f, 1f);
        [SerializeField] private bool _centerClaimRewardsButtonOnScreen = true;
        [SerializeField] private Vector2 _claimRewardsScreenAnchor = new Vector2(0.5f, 0.5f);
        [SerializeField] private Vector2 _claimRewardsScreenOffset = Vector2.zero;
        [SerializeField] private int _claimRewardsCanvasSortingOrder = 3350;
        [SerializeField] private string _claimRewardsButtonReadyLabel = "보상 받기";
        [SerializeField] private string _claimRewardsButtonSyncingLabel = "보상 받기";
        [SerializeField] private string _claimRewardsButtonClaimingLabel = "보상 받기";
        [SerializeField] private string _claimRewardsButtonClaimedLabel = "수령 완료";
        [Header("Reward Claim Toast")]
        [SerializeField] private Vector2 _rewardClaimToastAnchor = new Vector2(0.5f, 0.5f);
        [SerializeField] private Vector2 _rewardClaimToastOffset = Vector2.zero;
        [SerializeField] private Vector2 _rewardClaimToastSize = new Vector2(1100f, 760f);
        [SerializeField] private Vector2 _rewardClaimToastTextPadding = new Vector2(50f, 40f);
        [SerializeField] private int _rewardClaimToastFontSize = 32;
        [SerializeField] private Color _rewardClaimToastPanelColor = new Color(0.04f, 0.07f, 0.1f, 0.96f);
        [SerializeField] private Color _rewardClaimToastTextColor = new Color(1f, 0.96f, 0.78f, 1f);
        [Header("Server Wallet Display")]
        [SerializeField] private Text _serverWalletText;
        [SerializeField] private bool _autoCreateServerWalletDisplay = true;
        [SerializeField] private Vector2 _serverWalletAnchor = new Vector2(1f, 1f);
        [SerializeField] private Vector2 _serverWalletOffset = new Vector2(-28f, -24f);
        [SerializeField] private Vector2 _serverWalletSize = new Vector2(360f, 92f);
        [SerializeField] private Vector2 _serverWalletTextPadding = new Vector2(18f, 10f);
        [SerializeField] private int _serverWalletFontSize = 22;
        [SerializeField] private Color _serverWalletPanelColor = new Color(0.035f, 0.055f, 0.09f, 0.88f);
        [SerializeField] private Color _serverWalletTextColor = new Color(0.96f, 0.93f, 0.78f, 1f);
        [Header("Development Smoke Panel")]
        [SerializeField] private bool _showDevelopmentSmokePanel;
        [SerializeField] private Vector2 _developmentSmokePanelAnchor = new Vector2(0f, 1f);
        [SerializeField] private Vector2 _developmentSmokePanelOffset = new Vector2(28f, -24f);
        [SerializeField] private Vector2 _developmentSmokePanelSize = new Vector2(560f, 208f);
        [SerializeField] private Vector2 _developmentSmokePanelTextPadding = new Vector2(18f, 12f);
        [SerializeField] private int _developmentSmokePanelFontSize = 20;
        [SerializeField] private int _developmentSmokePanelSortingOrder = 3210;
        [SerializeField] private Color _developmentSmokePanelColor = new Color(0.035f, 0.055f, 0.09f, 0.86f);
        [SerializeField] private Color _developmentSmokePanelTextColor = new Color(0.78f, 0.95f, 1f, 1f);
        [SerializeField] private Vector2 _statusTitleOffsetMin = new Vector2(28f, -302f);
        [SerializeField] private Vector2 _statusTitleOffsetMax = new Vector2(-28f, -264f);
        [SerializeField] private Vector2 _statusTextOffsetMin = new Vector2(28f, 24f);
        [SerializeField] private Vector2 _statusTextOffsetMax = new Vector2(-28f, -306f);
        [Header("Draft Runtime Deck Panel")]
        [SerializeField] private bool _createRuntimeDeckPanel = true;
        [SerializeField] private bool _hideOriginalDeckListScrollRect = true;
        [SerializeField] private Vector2 _deckPanelOffsetMin = new Vector2(20f, 20f);
        [SerializeField] private Vector2 _deckPanelOffsetMax = new Vector2(-20f, -68f);
        [SerializeField] private Color _deckPanelColor = new Color(0.07f, 0.095f, 0.14f, 0.96f);
        [SerializeField] private Color _deckPanelOutlineColor = new Color(0.84f, 0.74f, 0.42f, 0.42f);
        [SerializeField] private Vector2 _deckPanelOutlineDistance = new Vector2(1.2f, -1.2f);
        [SerializeField] private Vector2 _deckSummaryBarOffsetMin = new Vector2(12f, -52f);
        [SerializeField] private Vector2 _deckSummaryBarOffsetMax = new Vector2(-12f, -12f);
        [SerializeField] private Color _deckSummaryBarColor = new Color(0.15f, 0.2f, 0.29f, 0.95f);
        [SerializeField] private Vector2 _deckSummaryTextOffsetMin = new Vector2(12f, 0f);
        [SerializeField] private Vector2 _deckSummaryTextOffsetMax = new Vector2(-12f, 0f);
        [SerializeField] private Vector2 _deckPanelTextOffsetMin = new Vector2(16f, 18f);
        [SerializeField] private Vector2 _deckPanelTextOffsetMax = new Vector2(-16f, -64f);
        [SerializeField] private Color _deckSummaryTextColor = new Color(0.98f, 0.95f, 0.84f, 1f);
        [SerializeField] private Color _deckPanelTextColor = new Color(0.93f, 0.96f, 1f, 1f);

        private DraftSessionService _draftSessionService;
        [SerializeField, HideInInspector] private Text _runtimeDeckListDisplayText;
        [SerializeField, HideInInspector] private Text _runtimeDeckPanelText;
        [SerializeField, HideInInspector] private Text _runtimeDeckSummaryText;
        [SerializeField, HideInInspector] private ScrollRect _runtimeDeckPanelScrollRect;
        [SerializeField, HideInInspector] private RectTransform _runtimeDeckPanelScrollContent;
        [SerializeField, HideInInspector] private CanvasGroup _rewardClaimToastCanvasGroup;
        [SerializeField, HideInInspector] private RectTransform _rewardClaimToastClickSurfaceRect;
        [SerializeField, HideInInspector] private RectTransform _rewardClaimToastPanelRect;
        [SerializeField, HideInInspector] private RectTransform _rewardClaimToastTextRect;
        [SerializeField, HideInInspector] private Image _rewardClaimToastPanelImage;
        [SerializeField, HideInInspector] private Button _rewardClaimToastClickSurfaceButton;
        [SerializeField, HideInInspector] private Image _rewardClaimToastClickSurfaceImage;
        [SerializeField, HideInInspector] private Text _rewardClaimToastText;
        private bool _isRewardClaimPopupAwaitingClick;
        private float _rewardClaimPopupEarliestCloseTime;
        private Coroutine _completedRunServerSyncRetryCoroutine;
        private int _completedRunServerSyncAttempts;
        private bool _isWaitingForServerRunResultSync;
        private bool _hasPvpReconnectableBattle;
        private string _pvpReconnectMatchId = string.Empty;
        private int _pvpReconnectRemainingSeconds;
        private float _pvpReconnectDeadlineRealtime;
        private int _lastPresentedPvpReconnectRemainingSeconds = -1;
        [SerializeField, HideInInspector] private DraftPvpMatchmakingOverlayView _pvpMatchmakingOverlay;
        private OnlineBattleSessionCoordinator _pvpSessionCoordinator;
        private bool _isPvpMatchmakingActive;
        private bool _isCancellingPvpMatchmaking;
        private bool _isPvpReconnectMatchmaking;
        private bool _hasPresentedPvpMatchmakingFailure;
        private bool _returnToStartAfterPvpCancellation;
        private Canvas _serverWalletCanvas;
        private ServerAccountSmokePanel _developmentSmokePanel;
        private RectTransform _serverWalletPanelRect;
        private RectTransform _serverWalletTextRect;
        private Image _serverWalletPanelImage;
        private Canvas _claimRewardsCanvas;
        [SerializeField, HideInInspector] private RectTransform _runtimeDeckPanelSurface;
        private CancellationTokenSource _completeDraftCancellation;
        private CancellationTokenSource _draftPickSaveCancellation;
        private CancellationTokenSource _accountRefreshCancellation;
        private CancellationTokenSource _claimRewardsCancellation;
        private bool _isLoadingBattleScene;
        private bool _isSavingCompletedDeck;
        private bool _isSavingDraftPicks;
        private bool _isReturningToStartFromDeckBuilding;
        private bool _isClaimingRewards;
        private bool _completedDeckSaveFailed;
        private bool _claimRewardsButtonNeedsDefaultLayout;
        private bool _serverWalletPanelNeedsDefaultLayout;
        private bool _serverWalletTextNeedsDefaultLayout;
#if UNITY_EDITOR
        private bool _hasQueuedEditorUiRefresh;
#endif
        private static readonly string[] KoreanFontCandidates =
        {
            "Malgun Gothic",
            "맑은 고딕",
            "Arial Unicode MS",
            "Noto Sans CJK KR",
            "Noto Sans KR"
        };
        private static Font s_runtimeKoreanFont;

        private sealed class RewardCardPopupItem
        {
            public string CardId;
            public string DisplayName;
            public int Count;
            public bool HasRarity;
            public CardRarity Rarity;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEngine.Application.isPlaying)
            {
                return;
            }

            QueueEditorDraftUiRefresh();
        }

        [ContextMenu("Materialize Missing Draft UI Objects")]
        private void MaterializeMissingDraftUiObjectsFromInspector()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                MaterializeEditorDraftUi();
            }
        }

        private void QueueEditorDraftUiRefresh()
        {
            if (_hasQueuedEditorUiRefresh)
            {
                return;
            }

            _hasQueuedEditorUiRefresh = true;
            EditorApplication.delayCall += MaterializeEditorDraftUi;
        }

        private void MaterializeEditorDraftUi()
        {
            _hasQueuedEditorUiRefresh = false;
            if (this == null || UnityEngine.Application.isPlaying)
            {
                return;
            }

            Undo.RecordObject(this, "Materialize Draft UI");
            EnsureReturnToStartButton();
            EnsureBattleModeButtons();
            EnsureClaimRewardsButton();
            EnsureServerWalletDisplay();
            EnsureDeckListReferences();
            EnsureRuntimeDeckListDisplayText();
            EnsureRuntimeDeckPanelText();
            EnsureRewardClaimToast();
            EnsurePvpMatchmakingOverlay();
            EditorUtility.SetDirty(this);

            var scene = gameObject.scene;
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }
#endif

        private string AccountServerUrl =>
            Project333.Runtime.Presentation.Project333ServerEndpointSettings.ResolveHttpUrl(_accountServerUrl);

        private void Awake()
        {
            DraftRunSessionState.ConfigureSceneNames(_draftSceneName, _battleSceneName);
            DraftRunSessionState.RestoreCurrentDraftDeckFromCompletedDeckIfNeeded();
            EnsureReturnToStartButton();
            EnsureBattleModeButtons();
            EnsureClaimRewardsButton();
            EnsureDeckListReferences();
            EnsurePvpMatchmakingOverlay();
            PrepareDeckListTextLayout();
            _draftOverlayPresenter?.Bind(this);
            RefreshStaticUi();
        }

        private async void Start()
        {
            await RefreshServerAccountStateAsync();
            if (this == null)
            {
                return;
            }

            if (!DraftRunSessionState.HasDraftedDeckReady &&
                CanBeginDraftInThisScene())
            {
                if (IsDeckBuildingScene())
                {
                    BeginDraftFromUi();
                }
                else if (_autoOpenDraftIfNoDeck &&
                         !TryLoadDeckBuildingScene())
                {
                    BeginDraftFromUi();
                }
            }
        }

        private void Update()
        {
            UpdateRewardClaimPopupClick();
            UpdatePvpReconnectCountdown();
            UpdatePvpMatchmakingState();
        }

        private void UpdateRewardClaimPopupClick()
        {
            if (!_isRewardClaimPopupAwaitingClick ||
                UnityEngine.Time.unscaledTime < _rewardClaimPopupEarliestCloseTime)
            {
                return;
            }

            if (UnityEngine.Input.GetMouseButtonDown(0) || HasTouchBegan())
            {
                ReturnToStartSceneAfterRewardPopupFromUi();
            }
        }

        private void UpdatePvpReconnectCountdown()
        {
            if (!_hasPvpReconnectableBattle || _pvpReconnectDeadlineRealtime <= 0f)
            {
                return;
            }

            var remainingSeconds = Mathf.Max(
                0,
                Mathf.CeilToInt(_pvpReconnectDeadlineRealtime - UnityEngine.Time.unscaledTime));
            if (remainingSeconds == _lastPresentedPvpReconnectRemainingSeconds)
            {
                return;
            }

            _lastPresentedPvpReconnectRemainingSeconds = remainingSeconds;
            _pvpReconnectRemainingSeconds = remainingSeconds;

            if (remainingSeconds <= 0)
            {
                ClearPvpReconnectStatus();
                SetStatus("PVP reconnect window expired. Start a new battle when ready.");
            }

            RefreshStaticUi();
        }

        private void OnDisable()
        {
            CancelCompleteDraftSave();
            CancelDraftPickSave();
            CancelAccountRefresh();
            CancelClaimRewards();
            CancelCompletedRunServerSyncRetry();
        }

        private static bool HasTouchBegan()
        {
            for (var i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                if (UnityEngine.Input.GetTouch(i).phase == UnityEngine.TouchPhase.Began)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDestroy()
        {
            CancelCompleteDraftSave();
            CancelDraftPickSave();
            CancelAccountRefresh();
            CancelClaimRewards();
            CancelCompletedRunServerSyncRetry();
        }

        public bool TryGetCardDefinitionAsset(string cardId, out CardDefinitionAsset cardAsset)
        {
            cardAsset = null;
            return _cardCatalogAsset != null &&
                   _cardCatalogAsset.TryGetCardAsset(cardId, out cardAsset);
        }

        public async void BeginDraftFromUi()
        {
            if (!CanBeginDraftInThisScene())
            {
                SetStatus("Return to the start screen to begin a new server run.");
                RefreshStaticUi();
                return;
            }

            if (IsRunEndedForUi())
            {
                SetStatus(BuildRunCompleteStatusText());
                RefreshStaticUi();
                return;
            }

            if (!IsDeckBuildingScene() &&
                TryLoadDeckBuildingScene())
            {
                return;
            }

            DraftRunSessionState.ConfigureSceneNames(_draftSceneName, _battleSceneName);
            _draftSessionService = new DraftSessionService(_cardCatalogAsset, CreateDraftRandom());

            var validation = _draftSessionService.ValidateCatalog();
            if (!validation.IsValid)
            {
                _draftOverlayPresenter?.ShowValidationMessage(validation.Message);
                SetStatus(validation.Message);
                RefreshStaticUi();
                return;
            }

            if (IsServerDraftPersistenceRequired())
            {
                await LoadAuthoritativeDraftStateAsync();
                return;
            }

            var resumeDraftPickCardIds = ResolveDraftPickCardIdsForResume();
            var resumeDraftOfferCardIds = ResolveDraftOfferCardIdsForResume();
            var shouldResumeDraft = (resumeDraftPickCardIds.Count > 0 &&
                                     resumeDraftPickCardIds.Count < 33) ||
                                    resumeDraftOfferCardIds.Count == 3;
            if (!shouldResumeDraft)
            {
                DraftRunSessionState.ResetForNewDraft();
            }
            else
            {
                DraftRunSessionState.SetDraftDeck(resumeDraftPickCardIds);
            }

            var openingOffer = shouldResumeDraft
                ? _draftSessionService.ResumeDraft(resumeDraftPickCardIds, resumeDraftOfferCardIds)
                : _draftSessionService.BeginDraft();
            SyncDraftRunDeckState();
            if (openingOffer != null)
            {
                _draftOverlayPresenter?.ShowOffer(openingOffer, _draftSessionService.DeckState.CardIds);
            }

            SetStatus(shouldResumeDraft
                ? $"Deck building resumed. {resumeDraftPickCardIds.Count}/33 cards selected."
                : "Deck building started.");
            RefreshStaticUi();
            await SaveInProgressDraftPicksToServerAsync(_draftSessionService.DeckState.CardIds, openingOffer);
        }

        public async void SelectDraftCard(string cardId)
        {
            if (_isSavingDraftPicks)
            {
                SetStatus("Saving the current deck-building pick. Please wait a moment.");
                RefreshStaticUi();
                return;
            }

            if (_draftSessionService == null)
            {
                SetStatus("Draft is not active.");
                return;
            }

            if (IsServerDraftPersistenceRequired())
            {
                await SelectAuthoritativeDraftCardAsync(cardId);
                return;
            }

            try
            {
                var result = _draftSessionService.SelectCard(cardId);
                SetStatus($"Draft picked '{result.SelectedCardId}'.");

                if (result.IsComplete)
                {
                    DraftRunSessionState.SetDraftDeck(result.CompletedDeckCardIds);
                    _draftSessionService = null;
                    _draftOverlayPresenter?.Hide();
                    SetStatus("Draft complete. Saving deck to server...");
                    RefreshStaticUi();
                    var savedDeck = await SaveCompletedDraftDeckToServerAsync(result.CompletedDeckCardIds);
                    RefreshStaticUi();
                    if (savedDeck)
                    {
                        LoadDraftSceneAfterDeckBuildingComplete();
                    }

                    return;
                }

                SyncDraftRunDeckState();
                _draftOverlayPresenter?.ShowOffer(result.NextOffer, _draftSessionService.DeckState.CardIds);
                RefreshStaticUi();
                var savedPicks = await SaveInProgressDraftPicksToServerAsync(
                    _draftSessionService.DeckState.CardIds,
                    result.NextOffer);
                if (this == null)
                {
                    return;
                }

                if (savedPicks)
                {
                    SetStatus(
                        $"Draft picked '{result.SelectedCardId}'. Progress saved ({_draftSessionService.DeckState.Count}/33).");
                    RefreshStaticUi();
                }
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
                _draftOverlayPresenter?.ShowValidationMessage(ex.Message);
            }
        }

        public void StartBattleFromUi()
        {
            StartPveBattleFromUi();
        }

        public void StartPveBattleFromUi()
        {
            StartBattleWithMode(ResolvePveBattleLaunchMode());
        }

        public void StartPvpBattleFromUi()
        {
            StartBattleWithMode(DraftBattleLaunchMode.OnlineMatchmaking);
        }

        private async void StartBattleWithMode(DraftBattleLaunchMode launchMode)
        {
            if (_isLoadingBattleScene || _isPvpMatchmakingActive)
            {
                return;
            }

            var isPvpReconnectLaunch = launchMode == DraftBattleLaunchMode.OnlineMatchmaking &&
                                       _hasPvpReconnectableBattle;

            if (isPvpReconnectLaunch &&
                !await RefreshPvpReconnectBeforeBattleLaunchAsync())
            {
                RefreshStaticUi();
                return;
            }

            isPvpReconnectLaunch = launchMode == DraftBattleLaunchMode.OnlineMatchmaking &&
                                   _hasPvpReconnectableBattle;

            if (!DraftRunSessionState.HasDraftedDeckReady && !isPvpReconnectLaunch)
            {
                SetStatus("Complete the 33-card draft before starting a battle.");
                RefreshStaticUi();
                return;
            }

            if (launchMode == DraftBattleLaunchMode.OnlineMatchmaking &&
                !AccountSessionState.IsAuthenticated)
            {
                SetStatus("PVP requires a server account login. Return to the start screen and log in first.");
                RefreshStaticUi();
                return;
            }

            if (IsRunEndedForUi())
            {
                SetStatus(BuildRunCompleteStatusText());
                RefreshStaticUi();
                return;
            }

            if (!isPvpReconnectLaunch && !await EnsureCompletedDraftSavedAsync())
            {
                RefreshStaticUi();
                return;
            }

            DraftRunSessionState.ConfigureSceneNames(_draftSceneName, _battleSceneName);
            DraftRunSessionState.SetServerRunDeckMetadata(
                AccountSessionState.ActiveRunId,
                AccountSessionState.ActiveDeckId);
            var deckCardIds = ResolveVisibleDeckCardIds();
            if (launchMode == DraftBattleLaunchMode.OnlineMatchmaking)
            {
                BeginPvpMatchmakingInDraft(isPvpReconnectLaunch, deckCardIds);
                return;
            }

            if (isPvpReconnectLaunch)
            {
                DraftRunSessionState.QueuePvpReconnectBattleStart();
            }
            else
            {
                DraftRunSessionState.QueueBattleStart(launchMode);
            }

            Debug.Log(
                $"[After333 Draft Launch] mode={launchMode} run={FormatLogValue(AccountSessionState.ActiveRunId)} deck={FormatLogValue(AccountSessionState.ActiveDeckId)} runStatus={FormatLogValue(AccountSessionState.ActiveRunStatus)} record={AccountSessionState.ActiveRunWins}-{AccountSessionState.ActiveRunLosses} deckCards={deckCardIds?.Count ?? 0} serverDeckCards={AccountSessionState.ActiveDeckCardCount} upgradedCardKinds={CountUpgradedCardKinds(deckCardIds)}");
            _isLoadingBattleScene = true;
            SetBattleModeButtonsInteractable(false);
            SetStatus(launchMode == DraftBattleLaunchMode.OnlineMatchmaking
                ? "Entering PvP matchmaking or reconnecting to an interrupted PvP battle..."
                : "Starting battle...");
            SceneManager.LoadScene(_battleSceneName);
        }

        private void BeginPvpMatchmakingInDraft(
            bool isReconnectLaunch,
            IReadOnlyList<string> deckCardIds)
        {
            EnsurePvpMatchmakingOverlay();
            _pvpSessionCoordinator = OnlineBattleSessionCoordinator.GetOrCreate();
            _isPvpMatchmakingActive = true;
            _isCancellingPvpMatchmaking = false;
            _isPvpReconnectMatchmaking = isReconnectLaunch;
            _hasPresentedPvpMatchmakingFailure = false;
            _returnToStartAfterPvpCancellation = false;

            SetBattleModeButtonsInteractable(false);
            SetStatus(isReconnectLaunch
                ? "전투에 재접속하는 중..."
                : "상대를 찾는 중입니다...");
            _pvpMatchmakingOverlay?.ShowWaiting(isReconnectLaunch);

            var playerToken = $"player-a-{Guid.NewGuid():N}";
            var resolvedPlayerToken = playerToken.Substring(0, Math.Min(playerToken.Length, 17));
            var battleServerUrl = Project333.Runtime.Presentation.Project333ServerEndpointSettings
                .ResolveBattleWebSocketUrl(_accountServerUrl);
            Debug.Log(
                $"[After333 Draft Matchmaking] begin reconnect={isReconnectLaunch} run={FormatLogValue(AccountSessionState.ActiveRunId)} deck={FormatLogValue(AccountSessionState.ActiveDeckId)} deckCards={deckCardIds?.Count ?? 0}");
            _pvpSessionCoordinator.BeginPvpMatchmaking(
                battleServerUrl,
                resolvedPlayerToken,
                deckCardIds,
                isReconnectLaunch);
            RefreshStaticUi();
        }

        private void UpdatePvpMatchmakingState()
        {
            if (!_isPvpMatchmakingActive || _pvpSessionCoordinator == null)
            {
                return;
            }

            if (_pvpSessionCoordinator.Phase == OnlineBattleSessionPhase.BattleReady)
            {
                CompletePvpMatchmakingAndLoadBattle();
                return;
            }

            if ((_pvpSessionCoordinator.Phase == OnlineBattleSessionPhase.Failed ||
                 _pvpSessionCoordinator.Phase == OnlineBattleSessionPhase.Ended) &&
                !_hasPresentedPvpMatchmakingFailure)
            {
                _hasPresentedPvpMatchmakingFailure = true;
                var message = _pvpSessionCoordinator.Phase == OnlineBattleSessionPhase.Ended
                    ? "이전 PVP 전투가 이미 종료되었습니다."
                    : _pvpSessionCoordinator.LastError;
                SetStatus(message);
                _pvpMatchmakingOverlay?.ShowFailure(message);
                RefreshStaticUi();
            }
        }

        private void CompletePvpMatchmakingAndLoadBattle()
        {
            if (_isLoadingBattleScene ||
                _pvpSessionCoordinator == null ||
                !_pvpSessionCoordinator.HasActiveBattle)
            {
                return;
            }

            _isPvpMatchmakingActive = false;
            _isCancellingPvpMatchmaking = false;
            _returnToStartAfterPvpCancellation = false;
            _pvpMatchmakingOverlay?.Hide();

            DraftRunSessionState.ConfigureSceneNames(_draftSceneName, _battleSceneName);
            DraftRunSessionState.SetServerRunDeckMetadata(
                AccountSessionState.ActiveRunId,
                AccountSessionState.ActiveDeckId);
            if (_isPvpReconnectMatchmaking)
            {
                DraftRunSessionState.QueuePvpReconnectBattleStart();
            }
            else
            {
                DraftRunSessionState.QueueBattleStart(DraftBattleLaunchMode.OnlineMatchmaking);
            }

            _isLoadingBattleScene = true;
            SetBattleModeButtonsInteractable(false);
            SetStatus("상대를 찾았습니다. 전투 화면으로 이동합니다.");
            Debug.Log(
                $"[After333 Draft Matchmaking] battle ready match={FormatLogValue(_pvpSessionCoordinator.ConnectionTester?.CurrentMatchId)}");
            SceneManager.LoadScene(_battleSceneName);
        }

        public async void CancelPvpMatchmakingFromUi()
        {
            if (!_isPvpMatchmakingActive || _isCancellingPvpMatchmaking)
            {
                return;
            }

            if (_pvpSessionCoordinator != null && _pvpSessionCoordinator.HasActiveBattle)
            {
                CompletePvpMatchmakingAndLoadBattle();
                return;
            }

            _isCancellingPvpMatchmaking = true;
            _pvpMatchmakingOverlay?.ShowCancelling();
            SetStatus("매칭을 취소하는 중입니다...");

            var cancelled = _pvpSessionCoordinator == null ||
                            await _pvpSessionCoordinator.CancelMatchmakingAndDisposeAsync();
            if (this == null)
            {
                return;
            }

            if (!cancelled &&
                _pvpSessionCoordinator != null &&
                _pvpSessionCoordinator.HasActiveBattle)
            {
                _isCancellingPvpMatchmaking = false;
                CompletePvpMatchmakingAndLoadBattle();
                return;
            }

            _isPvpMatchmakingActive = false;
            _isCancellingPvpMatchmaking = false;
            _pvpSessionCoordinator = null;
            _pvpMatchmakingOverlay?.Hide();
            var shouldReturnToStart = _returnToStartAfterPvpCancellation;
            _returnToStartAfterPvpCancellation = false;

            if (shouldReturnToStart)
            {
                LoadStartScene();
                return;
            }

            SetStatus("PVP 매칭을 취소했습니다.");
            RefreshStaticUi();
        }

        private void EnsurePvpMatchmakingOverlay()
        {
            if (IsDeckBuildingScene())
            {
                return;
            }

            if (_pvpMatchmakingOverlay == null)
            {
                _pvpMatchmakingOverlay = DraftPvpMatchmakingOverlayView.GetOrCreate(gameObject.scene);
            }
            else
            {
                _pvpMatchmakingOverlay.EnsureEditableHierarchy();
            }

            _pvpMatchmakingOverlay.BindCancel(CancelPvpMatchmakingFromUi);
        }

        private async Task<bool> RefreshPvpReconnectBeforeBattleLaunchAsync()
        {
            var previousWins = DraftRunSessionState.Wins;
            var previousLosses = DraftRunSessionState.Losses;

            SetStatus("PVP 전투 재접속 가능 여부를 확인하는 중입니다.");
            RefreshStaticUi();

            var client = new GuestAuthClient(AccountServerUrl);
            await RefreshPvpReconnectStatusAsync(
                client,
                AccountSessionState.SessionToken,
                CancellationToken.None);
            if (_hasPvpReconnectableBattle)
            {
                return true;
            }

            await RefreshServerAccountStateAsync();
            if (_hasPvpReconnectableBattle)
            {
                return true;
            }

            var finishedStatus = BuildFinishedPvpReconnectStatusText(previousWins, previousLosses);
            SetStatus(finishedStatus);
            SimpleNoticeToast.Show("DraftNoticeToastCanvas", finishedStatus);
            return false;
        }

        private static string BuildFinishedPvpReconnectStatusText(int previousWins, int previousLosses)
        {
            var currentWins = AccountSessionState.HasResumableRun
                ? AccountSessionState.ActiveRunWins
                : AccountSessionState.LatestRunWins;
            var currentLosses = AccountSessionState.HasResumableRun
                ? AccountSessionState.ActiveRunLosses
                : AccountSessionState.LatestRunLosses;

            if (currentWins > previousWins)
            {
                return "이전 전투에서 승리했습니다.";
            }

            if (currentLosses > previousLosses)
            {
                return "이전 전투에서 패배했습니다.";
            }

            return "이전 PVP 전투가 이미 종료되었습니다.";
        }

        public void ReturnToStartSceneFromUi()
        {
            if (_isPvpMatchmakingActive)
            {
                _returnToStartAfterPvpCancellation = true;
                CancelPvpMatchmakingFromUi();
                return;
            }

            if (IsDeckBuildingScene())
            {
                ReturnToStartSceneFromDeckBuilding();
                return;
            }

            LoadStartScene();
        }

        public void ReturnFromDraftOverlay()
        {
            ReturnToStartSceneFromUi();
        }

        private void LoadStartScene()
        {
            var startSceneName = string.IsNullOrWhiteSpace(_startSceneName)
                ? "GameStart_VSlice"
                : _startSceneName;
            SceneManager.LoadScene(startSceneName);
        }

        private async void ReturnToStartSceneFromDeckBuilding()
        {
            if (_isReturningToStartFromDeckBuilding)
            {
                return;
            }

            if (_isSavingDraftPicks)
            {
                SetStatus("Saving the current deck-building pick. Please wait a moment.");
                RefreshStaticUi();
                return;
            }

            if (IsServerDraftPersistenceRequired())
            {
                SetStatus("Deck-building progress is already saved on the server. Returning to start screen...");
                RefreshStaticUi();
                LoadStartScene();
                return;
            }

            _isReturningToStartFromDeckBuilding = true;
            SetStatus("Saving deck-building progress...");
            RefreshStaticUi();

            try
            {
                if (_draftSessionService != null &&
                    _draftSessionService.DeckState != null &&
                    !_draftSessionService.DeckState.IsComplete)
                {
                    var saved = await SaveInProgressDraftPicksToServerAsync(
                        _draftSessionService.DeckState.CardIds,
                        _draftSessionService.CurrentOffer);
                    if (this == null)
                    {
                        return;
                    }

                    if (!saved)
                    {
                        SetStatus("Deck-building progress could not be saved. Please try again.");
                        return;
                    }
                }

                SetStatus("Deck-building progress saved. Returning to start screen...");
                RefreshStaticUi();
                LoadStartScene();
            }
            finally
            {
                if (this != null)
                {
                    _isReturningToStartFromDeckBuilding = false;
                    RefreshStaticUi();
                }
            }
        }

        public async void ClaimRewardsFromUi()
        {
            if (_isClaimingRewards)
            {
                return;
            }

            var shouldShowRewardClaimPopup = false;
            if (IsCurrentRunRewardClaimed())
            {
                DraftRunSessionState.ResetForNewDraft();
                SetStatus("Rewards already claimed. Returning to the start screen.");
                RefreshStaticUi();
                ReturnToStartSceneFromUi();
                return;
            }

            if (!IsLatestServerRunCompleted())
            {
                if (DraftRunSessionState.HasRunEnded)
                {
                    SetStatus("Syncing completed run rewards with the server...");
                    RefreshStaticUi();
                    await RefreshServerAccountStateAsync();
                    if (this == null)
                    {
                        return;
                    }

                    if (IsLatestServerRunCompleted())
                    {
                        RefreshStaticUi();
                    }
                    else if (IsCurrentRunRewardClaimed())
                    {
                        DraftRunSessionState.ResetForNewDraft();
                        SetStatus("Rewards already claimed. Returning to the start screen.");
                        RefreshStaticUi();
                        ReturnToStartSceneFromUi();
                        return;
                    }
                    else
                    {
                        var syncIds = ResolveServerRunDeckIdsForLocalCompletion();
                        var syncRunId = syncIds.RunId;
                        var syncDeckId = syncIds.DeckId;
                        var syncedLocalRecord = await TrySyncLocalCompletedRunRecordAsync(syncRunId, syncDeckId);
                        if (this == null)
                        {
                            return;
                        }

                        if (syncedLocalRecord)
                        {
                            RefreshStaticUi();
                        }
                        else
                        {
                            SetStatus("Rewards are still syncing. Please wait a moment and try again.");
                            QueueCompletedRunServerSyncRetry();
                            RefreshStaticUi();
                            return;
                        }
                    }
                }
                else
                {
                    SetStatus("Run rewards are available only after the run is complete.");
                    RefreshStaticUi();
                    return;
                }
            }

            if (!IsLatestServerRunCompleted())
            {
                RefreshStaticUi();
                return;
            }

            if (AccountSessionState.IsLatestRunRewardClaimed)
            {
                SetStatus(BuildRunCompleteStatusText());
                RefreshStaticUi();
                return;
            }

            if (string.IsNullOrWhiteSpace(AccountSessionState.SessionToken) ||
                string.IsNullOrWhiteSpace(AccountSessionState.LatestRunId))
            {
                SetStatus("Server account or completed run id is missing.");
                RefreshStaticUi();
                return;
            }

            CancelClaimRewards();
            _claimRewardsCancellation = new CancellationTokenSource();
            _isClaimingRewards = true;
            SetStatus("Claiming run rewards...");
            RefreshStaticUi();

            try
            {
                var client = new ServerRunClient(AccountServerUrl);
                var response = await client.ClaimRunRewardsAsync(
                    AccountSessionState.SessionToken,
                    AccountSessionState.LatestRunId,
                    _claimRewardsCancellation.Token);
                AccountSessionState.ApplyClaimRunRewardsResponse(response);

                var reward = response.ResolvedReward;
                var resourceGoldDelta = reward?.ResolvedResourceGoldDelta ?? 0;
                var ticketDelta = reward?.ResolvedTicketDelta ?? 0;
                var serverGold = response.ResolvedWallet?.ResolvedResourceGold ?? AccountSessionState.ResourceGold;
                var rewardSummary = BuildRewardSummaryText(resourceGoldDelta, ticketDelta, reward);
                Debug.Log(
                    $"After333 run rewards claimed: run={AccountSessionState.LatestRunId}, rewards={rewardSummary}");
                DraftRunSessionState.ResetForNewDraft();
                SetStatus(BuildRewardClaimStatusText(resourceGoldDelta, ticketDelta, serverGold, reward));
                ShowRewardClaimToast(resourceGoldDelta, ticketDelta, serverGold, reward);
                shouldShowRewardClaimPopup = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 run reward claim failed: {ex.Message}");
                SetStatus($"Reward claim failed: {ex.Message}");
            }
            finally
            {
                _isClaimingRewards = false;
                CancelClaimRewards();
                if (!shouldShowRewardClaimPopup)
                {
                    RefreshStaticUi();
                }
            }
        }

        private void RefreshStaticUi()
        {
            ReconcileLocalRunRecordFromAccountState();
            EnsureReturnToStartButton();
            EnsureBattleModeButtons();
            EnsureClaimRewardsButton();
            EnsureDeckListReferences();
            EnsureRuntimeDeckListDisplayText();
            EnsureRuntimeDeckPanelText();
            EnsureServerWalletDisplay();
            RefreshServerWalletDisplay();
            var visibleDeckCardIds = ResolveVisibleDeckCardIds();
            var visibleDeckCount = visibleDeckCardIds?.Count ?? 0;
            var isRunEndedForUi = IsRunEndedForUi();
            var isDeckBuildingScene = IsDeckBuildingScene();

            if (_recordText != null)
            {
                _recordText.text = isRunEndedForUi
                    ? $"Run Complete   Wins: {DraftRunSessionState.Wins}   Losses: {DraftRunSessionState.Losses}"
                    : $"Wins: {DraftRunSessionState.Wins}   Losses: {DraftRunSessionState.Losses}";
            }

            if (_lastResultText != null)
            {
                _lastResultText.text = isRunEndedForUi
                    ? BuildRunCompleteStatusText()
                    : string.IsNullOrWhiteSpace(DraftRunSessionState.LastBattleOutcomeText)
                    ? "Last Battle: -"
                    : $"Last Battle: {DraftRunSessionState.LastBattleOutcomeText}";
            }

            if (_deckListText != null)
            {
                var deckListText = BuildDeckListText(visibleDeckCardIds);
                _deckListText.text = deckListText;

                if (_runtimeDeckListDisplayText != null)
                {
                    _runtimeDeckListDisplayText.text = deckListText;
                }

                 if (_runtimeDeckPanelText != null)
                 {
                     _runtimeDeckPanelText.text = deckListText;
                 }

                if (_runtimeDeckSummaryText != null)
                {
                    _runtimeDeckSummaryText.text = BuildDeckSummaryText(visibleDeckCardIds);
                }

                RefreshDeckListViewport();
            }

            var hasDraftDeck = DraftRunSessionState.HasDraftedDeckReady;
            var canStartBattle = hasDraftDeck &&
                                 !isDeckBuildingScene &&
                                 !isRunEndedForUi &&
                                 !_isPvpMatchmakingActive &&
                                 !IsServerDraftSaveBlockingBattle() &&
                                 !IsServerRunResultSyncBlockingBattle();
            var canStartPvpBattle = !_isPvpMatchmakingActive &&
                                    (canStartBattle || CanReconnectPvpBattle(isDeckBuildingScene, isRunEndedForUi));
            RefreshDevelopmentSmokePanel(visibleDeckCount, isRunEndedForUi, canStartBattle);
            if (isDeckBuildingScene)
            {
                if (_claimRewardsButton != null)
                {
                    _claimRewardsButton.gameObject.SetActive(false);
                }
            }
            else
            {
                RefreshClaimRewardsButton();
            }

            RefreshBattleModeButton(
                _startPveBattleButton,
                _pveBattleButtonReadyLabel,
                BuildBattleModeLockedLabel(_pveBattleButtonLockedLabelFormat, visibleDeckCount),
                _startPveBattleButtonLabel,
                canStartBattle);
            RefreshBattleModeButton(
                _startPvpBattleButton,
                BuildPvpBattleButtonReadyLabel(),
                BuildBattleModeLockedLabel(_pvpBattleButtonLockedLabelFormat, visibleDeckCount),
                _startPvpBattleButtonLabel,
                canStartPvpBattle);
            if (_startPveBattleButton != null)
            {
                _startPveBattleButton.gameObject.SetActive(!isDeckBuildingScene);
            }

            if (_startPvpBattleButton != null)
            {
                _startPvpBattleButton.gameObject.SetActive(!isDeckBuildingScene);
            }

            if (_startDraftButton != null)
            {
                var isRunCompleted = isRunEndedForUi;
                _startDraftButton.interactable = !isRunCompleted && CanBeginDraftInThisScene();
                _startDraftButton.gameObject.SetActive(!isDeckBuildingScene &&
                                                       !isRunCompleted &&
                                                       CanBeginDraftInThisScene());
            }

            if (_returnToStartButton != null)
            {
                _returnToStartButton.interactable = !_isReturningToStartFromDeckBuilding;
            }
        }

        private void EnsureDevelopmentSmokePanel()
        {
            if (!_showDevelopmentSmokePanel)
            {
                if (_developmentSmokePanel != null)
                {
                    _developmentSmokePanel.Configure(
                        false,
                        _developmentSmokePanelAnchor,
                        _developmentSmokePanelOffset,
                        _developmentSmokePanelSize,
                        _developmentSmokePanelTextPadding,
                        _developmentSmokePanelColor,
                        _developmentSmokePanelTextColor,
                        _developmentSmokePanelFontSize);
                }

                return;
            }

            _developmentSmokePanel = _developmentSmokePanel != null
                ? _developmentSmokePanel
                : ServerAccountSmokePanel.GetOrCreate("DraftSmokePanelCanvas", _developmentSmokePanelSortingOrder);
            _developmentSmokePanel.Configure(
                true,
                _developmentSmokePanelAnchor,
                _developmentSmokePanelOffset,
                _developmentSmokePanelSize,
                _developmentSmokePanelTextPadding,
                _developmentSmokePanelColor,
                _developmentSmokePanelTextColor,
                _developmentSmokePanelFontSize);
        }

        private void RefreshDevelopmentSmokePanel(int visibleDeckCount, bool isRunEndedForUi, bool canStartBattle)
        {
            EnsureDevelopmentSmokePanel();
            if (_developmentSmokePanel == null || !_showDevelopmentSmokePanel)
            {
                return;
            }

            _developmentSmokePanel.Refresh(
                "Draft",
                BuildDevelopmentSmokeNextAction(visibleDeckCount, isRunEndedForUi, canStartBattle),
                BuildDevelopmentSmokeExtraLine(visibleDeckCount, canStartBattle));
        }

        private string BuildDevelopmentSmokeNextAction(int visibleDeckCount, bool isRunEndedForUi, bool canStartBattle)
        {
            if (_isClaimingRewards)
            {
                return "Wait for reward claim response.";
            }

            if (_isSavingCompletedDeck)
            {
                return "Wait for completed deck save response.";
            }

            if (_isLoadingBattleScene)
            {
                return "Wait for Battle scene load.";
            }

            if (!AccountSessionState.IsAuthenticated)
            {
                return "Wait for server account restore, or return to Start.";
            }

            if (IsDeckBuildingScene())
            {
                if (_isReturningToStartFromDeckBuilding)
                {
                    return "Wait for deck-building progress save, then Start scene.";
                }

                if (_isSavingDraftPicks)
                {
                    return "Wait for current pick/offer save response.";
                }

                if (visibleDeckCount < 33)
                {
                    return "Pick cards or click Back To Start. Expected: picks and current offer resume.";
                }
            }

            if (!IsDeckBuildingScene() && HasInProgressDraftResumeData())
            {
                return "Enter deck building again. Expected: saved picks and same 3-card offer resume.";
            }

            if (IsCurrentRunRewardClaimed())
            {
                return "Click Back To Start. Expected: new run can start.";
            }

            if (isRunEndedForUi)
            {
                if (AccountSessionState.HasUnclaimedLatestRunRewards)
                {
                    return "보상 받기를 누릅니다. 예상 결과: 골드 보상 지급 후 시작 씬으로 이동.";
                }

                if (DraftRunSessionState.HasRunEnded)
                {
                    return "보상 받기를 눌러 로컬 런 결과를 서버와 동기화합니다.";
                }

                return "Run ended. Refresh server state or return to Start.";
            }

            if (visibleDeckCount < 33)
            {
                return "Draft until 33 cards are selected.";
            }

            if (IsServerDraftSaveBlockingBattle())
            {
                return "Wait for server deck save before battle.";
            }

            if (canStartBattle)
            {
                return "Click PVE or PVP to start a battle.";
            }

            return "Review status text; battle start is currently blocked.";
        }

        private string BuildDevelopmentSmokeExtraLine(int visibleDeckCount, bool canStartBattle)
        {
            var pveState = canStartBattle ? "ON" : "OFF";
            var claimState = IsLatestServerRunCompleted() || DraftRunSessionState.HasRunEnded ? "ON" : "OFF";
            return $"Controls: Deck {visibleDeckCount}/33 / ServerDeck {BuildServerDeckSmokeText(visibleDeckCount)} / Upgrades {CountUpgradedCardKinds(ResolveVisibleDeckCardIds())} / PVE {pveState} / Claim {claimState} / {BuildDraftResumeSmokeText(visibleDeckCount)}";
        }

        private static string BuildServerDeckSmokeText(int visibleDeckCount)
        {
            if (string.IsNullOrWhiteSpace(AccountSessionState.ActiveDeckId))
            {
                return "none";
            }

            if (AccountSessionState.ActiveDeckCardCount <= 0)
            {
                return "saved ?";
            }

            return AccountSessionState.ActiveDeckCardCount == visibleDeckCount
                ? $"saved {AccountSessionState.ActiveDeckCardCount}/33"
                : $"check {AccountSessionState.ActiveDeckCardCount}/{visibleDeckCount}";
        }

        private string BuildDraftResumeSmokeText(int visibleDeckCount)
        {
            var localPickCount = _draftSessionService?.DeckState?.Count ?? visibleDeckCount;
            var serverPickCount = AccountSessionState.ActiveDraftPicks.Count;
            var serverOfferCount = AccountSessionState.ActiveDraftOffer.Count;
            var saveState = ResolveDraftResumeSaveState(localPickCount, serverPickCount, serverOfferCount);
            return $"Resume {saveState}: local {localPickCount}/33, server {serverPickCount}/33, offer {serverOfferCount}/3";
        }

        private string ResolveDraftResumeSaveState(int localPickCount, int serverPickCount, int serverOfferCount)
        {
            if (_isSavingDraftPicks)
            {
                return "saving";
            }

            if (serverPickCount == 0 && serverOfferCount == 0)
            {
                return "none";
            }

            if (serverPickCount == localPickCount && (serverPickCount >= 33 || serverOfferCount == 3))
            {
                return "saved";
            }

            return "check";
        }

        private static bool HasInProgressDraftResumeData()
        {
            return (AccountSessionState.ActiveDraftPicks.Count > 0 &&
                    AccountSessionState.ActiveDraftPicks.Count < 33) ||
                   AccountSessionState.ActiveDraftOffer.Count == 3;
        }

        private void EnsureBattleModeButtons()
        {
            var controlPanel = transform.Find("ControlPanel") as RectTransform;

            if (_startPveBattleButton == null)
            {
                _startPveBattleButton = _startBattleButton;
            }

            if (_startPveBattleButton == null && controlPanel != null)
            {
                _startPveBattleButton = FindButton(controlPanel, "StartPveBattleButton");
            }

            if (_startPveBattleButton == null && controlPanel != null && _autoCreateMissingDraftUi)
            {
                _startPveBattleButton = CreateBattleModeButton(
                    "StartPveBattleButton",
                    controlPanel,
                    _pveBattleButtonPosition,
                    _pveBattleButtonColor,
                    StartPveBattleFromUi,
                    out _startPveBattleButtonLabel);
                _startBattleButton = _startPveBattleButton;
                _startBattleButtonLabel = _startPveBattleButtonLabel;
            }

            if (_startPveBattleButtonLabel == null)
            {
                _startPveBattleButtonLabel = _startBattleButtonLabel != null
                    ? _startBattleButtonLabel
                    : ResolveButtonLabel(_startPveBattleButton);
            }

            if (_startPveBattleButton != null)
            {
                ConfigureBattleModeButton(_startPveBattleButton, _pveBattleButtonPosition, _pveBattleButtonColor);
                _startPveBattleButton.onClick.RemoveListener(StartPveBattleFromUi);
                _startPveBattleButton.onClick.AddListener(StartPveBattleFromUi);
            }

            if (_startPvpBattleButton == null && controlPanel != null)
            {
                _startPvpBattleButton = FindButton(controlPanel, "StartPvpBattleButton");
            }

            if (_startPvpBattleButton == null && controlPanel != null && _autoCreateMissingDraftUi)
            {
                _startPvpBattleButton = CreateBattleModeButton(
                    "StartPvpBattleButton",
                    controlPanel,
                    _pvpBattleButtonPosition,
                    _pvpBattleButtonColor,
                    StartPvpBattleFromUi,
                    out _startPvpBattleButtonLabel);
            }

            if (_startPvpBattleButton != null && _startPvpBattleButtonLabel == null)
            {
                _startPvpBattleButtonLabel = ResolveButtonLabel(_startPvpBattleButton);
            }

            if (_startPvpBattleButton != null)
            {
                ConfigureBattleModeButton(_startPvpBattleButton, _pvpBattleButtonPosition, _pvpBattleButtonColor);
                _startPvpBattleButton.onClick.RemoveListener(StartPvpBattleFromUi);
                _startPvpBattleButton.onClick.AddListener(StartPvpBattleFromUi);
            }
        }

        private static Button FindButton(RectTransform parent, string objectName)
        {
            if (parent == null)
            {
                return null;
            }

            return parent.Find(objectName) is Transform child &&
                   child.TryGetComponent<Button>(out var button)
                ? button
                : null;
        }

        private Button CreateBattleModeButton(
            string objectName,
            RectTransform controlPanel,
            Vector2 anchoredPosition,
            Color color,
            UnityAction onClick,
            out Text labelText)
        {
            var buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            RegisterEditorCreatedObject(buttonObject);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(controlPanel, false);
            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.sizeDelta = _battleModeButtonSize;
            buttonRect.anchoredPosition = anchoredPosition;

            var image = buttonObject.GetComponent<Image>();
            image.color = color;

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.98f, 1f, 1f);
            colors.pressedColor = new Color(0.72f, 0.84f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(onClick);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            RegisterEditorCreatedObject(labelObject);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(buttonRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = _battleModeButtonLabelPadding;
            labelRect.offsetMax = -_battleModeButtonLabelPadding;

            labelText = labelObject.GetComponent<Text>();
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.fontStyle = FontStyle.Bold;
            labelText.fontSize = _battleModeButtonFontSize;
            labelText.color = Color.white;
            labelText.raycastTarget = false;
            labelText.font = ResolveRuntimeFont();
            return button;
        }

        private void ConfigureBattleModeButton(Button button, Vector2 anchoredPosition, Color imageColor)
        {
            if (button == null || button.transform is not RectTransform rectTransform)
            {
                return;
            }

            if (ShouldApplyDraftUiLayoutToSceneObjects())
            {
                rectTransform.sizeDelta = _battleModeButtonSize;
                rectTransform.anchoredPosition = anchoredPosition;
            }

            if (button.TryGetComponent<Image>(out var image))
            {
                image.color = imageColor;
            }

            var label = ResolveButtonLabel(button);
            if (label == null)
            {
                return;
            }

            label.fontSize = _battleModeButtonFontSize;
            if (label.rectTransform != null && ShouldApplyDraftUiLayoutToSceneObjects())
            {
                label.rectTransform.offsetMin = _battleModeButtonLabelPadding;
                label.rectTransform.offsetMax = -_battleModeButtonLabelPadding;
            }
        }

        private static Text ResolveButtonLabel(Button button)
        {
            return button == null
                ? null
                : button.GetComponentInChildren<Text>(true);
        }

        private static void RefreshBattleModeButton(
            Button button,
            string readyLabel,
            string lockedLabel,
            Text label,
            bool hasDraftDeck)
        {
            if (button != null)
            {
                button.interactable = hasDraftDeck;
            }

            if (label != null)
            {
                label.text = hasDraftDeck ? readyLabel : lockedLabel;
            }
        }

        private void SetBattleModeButtonsInteractable(bool interactable)
        {
            if (_startPveBattleButton != null)
            {
                _startPveBattleButton.interactable = interactable;
            }

            if (_startPvpBattleButton != null)
            {
                _startPvpBattleButton.interactable = interactable;
            }

            if (_startBattleButton != null &&
                !ReferenceEquals(_startBattleButton, _startPveBattleButton) &&
                !ReferenceEquals(_startBattleButton, _startPvpBattleButton))
            {
                _startBattleButton.interactable = interactable;
            }
        }

        private void EnsureClaimRewardsButton()
        {
            var controlPanel = transform.Find("ControlPanel") as RectTransform;
            var claimRewardsParent = ResolveClaimRewardsButtonParent(controlPanel);

            if (_claimRewardsButton == null && controlPanel != null)
            {
                _claimRewardsButton = FindButton(controlPanel, "ClaimRewardsButton");
            }

            if (_claimRewardsButton == null && claimRewardsParent != null)
            {
                _claimRewardsButton = FindButton(claimRewardsParent, "ClaimRewardsButton");
            }

            if (_claimRewardsButton == null && claimRewardsParent != null && _autoCreateMissingDraftUi)
            {
                _claimRewardsButton = CreateClaimRewardsButton(claimRewardsParent, out _claimRewardsButtonLabel);
            }

            if (_claimRewardsButton != null && _claimRewardsButtonLabel == null)
            {
                _claimRewardsButtonLabel = ResolveButtonLabel(_claimRewardsButton);
            }

            if (_claimRewardsButton != null)
            {
                MoveClaimRewardsButtonToConfiguredParent(claimRewardsParent);
                ConfigureClaimRewardsButton(_claimRewardsButton);
                _claimRewardsButton.onClick.RemoveListener(ClaimRewardsFromUi);
                _claimRewardsButton.onClick.AddListener(ClaimRewardsFromUi);
            }
        }

        private RectTransform ResolveClaimRewardsButtonParent(RectTransform controlPanel)
        {
            if (!_centerClaimRewardsButtonOnScreen)
            {
                return controlPanel;
            }

            return EnsureClaimRewardsCanvasRoot();
        }

        private RectTransform EnsureClaimRewardsCanvasRoot()
        {
            if (_claimRewardsCanvas != null)
            {
                return _claimRewardsCanvas.transform as RectTransform;
            }

            var existing = transform.Find("DraftClaimRewardsCanvas");
            if (existing != null && existing.TryGetComponent<Canvas>(out var existingCanvas))
            {
                _claimRewardsCanvas = existingCanvas;
                EnsureClaimRewardsCanvasComponents(existingCanvas.gameObject);
                return existing as RectTransform;
            }

            var canvasObject = new GameObject(
                "DraftClaimRewardsCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            RegisterEditorCreatedObject(canvasObject);
            canvasObject.transform.SetParent(transform, false);

            _claimRewardsCanvas = canvasObject.GetComponent<Canvas>();
            _claimRewardsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _claimRewardsCanvas.sortingOrder = _claimRewardsCanvasSortingOrder;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            return canvasObject.GetComponent<RectTransform>();
        }

        private void EnsureClaimRewardsCanvasComponents(GameObject canvasObject)
        {
            if (canvasObject == null)
            {
                return;
            }

            if (!canvasObject.TryGetComponent<GraphicRaycaster>(out _))
            {
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            if (!canvasObject.TryGetComponent<CanvasScaler>(out var scaler))
            {
                scaler = canvasObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            if (canvasObject.TryGetComponent<Canvas>(out var canvas))
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = _claimRewardsCanvasSortingOrder;
            }
        }

        private void MoveClaimRewardsButtonToConfiguredParent(RectTransform claimRewardsParent)
        {
            if (!_centerClaimRewardsButtonOnScreen ||
                _claimRewardsButton == null ||
                claimRewardsParent == null ||
                ReferenceEquals(_claimRewardsButton.transform.parent, claimRewardsParent))
            {
                return;
            }

            _claimRewardsButton.transform.SetParent(claimRewardsParent, false);
            _claimRewardsButtonNeedsDefaultLayout = true;
        }

        private Button CreateClaimRewardsButton(RectTransform controlPanel, out Text labelText)
        {
            var buttonObject = new GameObject(
                "ClaimRewardsButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            RegisterEditorCreatedObject(buttonObject);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(controlPanel, false);
            ApplyClaimRewardsButtonLayout(buttonRect);

            var image = buttonObject.GetComponent<Image>();
            image.color = _claimRewardsButtonColor;

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.8f, 1f);
            colors.pressedColor = new Color(1f, 0.86f, 0.52f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(ClaimRewardsFromUi);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            RegisterEditorCreatedObject(labelObject);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(buttonRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = _claimRewardsButtonLabelPadding;
            labelRect.offsetMax = -_claimRewardsButtonLabelPadding;

            labelText = labelObject.GetComponent<Text>();
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.fontStyle = FontStyle.Bold;
            labelText.fontSize = _claimRewardsButtonFontSize;
            labelText.color = Color.white;
            labelText.text = _claimRewardsButtonReadyLabel;
            labelText.raycastTarget = false;
            labelText.font = ResolveRuntimeFont();
            buttonObject.SetActive(false);
            return button;
        }

        private void ConfigureClaimRewardsButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            if (button.transform is RectTransform buttonRect &&
                (ShouldApplyDraftUiLayoutToSceneObjects() || _claimRewardsButtonNeedsDefaultLayout))
            {
                ApplyClaimRewardsButtonLayout(buttonRect);
                _claimRewardsButtonNeedsDefaultLayout = false;
            }

            if (button.TryGetComponent<Image>(out var image))
            {
                image.color = _claimRewardsButtonColor;
            }

            var label = ResolveButtonLabel(button);
            if (label != null)
            {
                label.fontSize = _claimRewardsButtonFontSize;
                if (ShouldApplyDraftUiLayoutToSceneObjects() && label.rectTransform != null)
                {
                    label.rectTransform.offsetMin = _claimRewardsButtonLabelPadding;
                    label.rectTransform.offsetMax = -_claimRewardsButtonLabelPadding;
                }
            }
        }

        private void ApplyClaimRewardsButtonLayout(RectTransform buttonRect)
        {
            if (buttonRect == null)
            {
                return;
            }

            if (_centerClaimRewardsButtonOnScreen)
            {
                var anchor = new Vector2(
                    Mathf.Clamp01(_claimRewardsScreenAnchor.x),
                    Mathf.Clamp01(_claimRewardsScreenAnchor.y));
                buttonRect.anchorMin = anchor;
                buttonRect.anchorMax = anchor;
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = _claimRewardsButtonSize;
                buttonRect.anchoredPosition = _claimRewardsScreenOffset;
                return;
            }

            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.sizeDelta = _claimRewardsButtonSize;
            buttonRect.anchoredPosition = _claimRewardsButtonPosition;
        }

        private void RefreshClaimRewardsButton()
        {
            if (_claimRewardsButton == null)
            {
                return;
            }

            var canClaimRewards = IsLatestServerRunCompleted();
            var rewardsAlreadyClaimed = IsCurrentRunRewardClaimed();
            var localRunEnded = DraftRunSessionState.HasRunEnded;
            var shouldShowRewardsButton = canClaimRewards || localRunEnded;
            _claimRewardsButton.gameObject.SetActive(shouldShowRewardsButton);
            _claimRewardsButton.interactable = shouldShowRewardsButton &&
                                               !_isClaimingRewards &&
                                               (rewardsAlreadyClaimed ||
                                                canClaimRewards ||
                                                localRunEnded);

            var label = _claimRewardsButtonLabel != null
                ? _claimRewardsButtonLabel
                : ResolveButtonLabel(_claimRewardsButton);
            if (label == null)
            {
                return;
            }

            if (_isClaimingRewards)
            {
                label.text = _claimRewardsButtonClaimingLabel;
            }
            else if (rewardsAlreadyClaimed)
            {
                label.text = _claimRewardsButtonClaimedLabel;
            }
            else if (!canClaimRewards && localRunEnded)
            {
                label.text = _claimRewardsButtonSyncingLabel;
            }
            else
            {
                label.text = _claimRewardsButtonReadyLabel;
            }
        }

        private async Task<bool> EnsureCompletedDraftSavedAsync()
        {
            if (!IsServerDraftPersistenceRequired() ||
                !string.IsNullOrWhiteSpace(AccountSessionState.ActiveDeckId))
            {
                return true;
            }

            if (_isSavingCompletedDeck)
            {
                SetStatus("Draft deck is still saving to the server.");
                return false;
            }

            var deckCardIds = ResolveVisibleDeckCardIds();
            if (deckCardIds == null || deckCardIds.Count != 33)
            {
                SetStatus("Complete the 33-card draft before saving the deck.");
                return false;
            }

            return await SaveCompletedDraftDeckToServerAsync(deckCardIds);
        }

        private async Task<bool> SaveCompletedDraftDeckToServerAsync(IReadOnlyList<string> deckCardIds)
        {
            if (!IsServerDraftPersistenceRequired())
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(AccountSessionState.ActiveDeckId))
            {
                if (AccountSessionState.ActiveDeckCardCount <= 0 ||
                    AccountSessionState.ActiveDeckCardCount == deckCardIds.Count)
                {
                    return true;
                }

                _completedDeckSaveFailed = true;
                SetStatus(
                    $"Server deck save mismatch: local {deckCardIds.Count}, server {AccountSessionState.ActiveDeckCardCount}.");
                return false;
            }

            if (_isSavingCompletedDeck)
            {
                return false;
            }

            CancelCompleteDraftSave();
            _completeDraftCancellation = new CancellationTokenSource();
            _isSavingCompletedDeck = true;
            _completedDeckSaveFailed = false;
            SetStatus("Saving completed draft deck to server...");
            RefreshStaticUi();

            try
            {
                var client = new ServerRunClient(AccountServerUrl);
                var response = await client.CompleteDraftAsync(
                    AccountSessionState.SessionToken,
                    AccountSessionState.ActiveRunId,
                    deckCardIds,
                    _completeDraftCancellation.Token);
                AccountSessionState.ApplyCompleteDraftResponse(response);
                DraftRunSessionState.SetServerRunDeckMetadata(
                    AccountSessionState.ActiveRunId,
                    AccountSessionState.ActiveDeckId);
                var isVerified = IsCompletedDraftSaveVerified(deckCardIds, response);
                var serverDeckCardCount = response.ResolvedDeck?.ResolvedCardCount ?? 0;
                Debug.Log(
                    $"After333 server draft deck saved: run={AccountSessionState.ActiveRunId}, deck={AccountSessionState.ActiveDeckId}, localDeckCards={deckCardIds.Count}, serverDeckCards={serverDeckCardCount}, verified={isVerified}");
                if (!isVerified)
                {
                    _completedDeckSaveFailed = true;
                    SetStatus(
                        $"Server deck save mismatch: local {deckCardIds.Count}, server {serverDeckCardCount}.");
                    return false;
                }

                SetStatus("Draft complete. Server deck verified (33/33). Press PVE or PVP when you are ready.");
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                _completedDeckSaveFailed = true;
                Debug.LogWarning($"After333 server draft deck save failed: {ex.Message}");
                SetStatus($"Server deck save failed: {ex.Message}");
                return false;
            }
            finally
            {
                _isSavingCompletedDeck = false;
                CancelCompleteDraftSave();
                RefreshStaticUi();
            }
        }

        private static bool IsCompletedDraftSaveVerified(
            IReadOnlyList<string> localDeckCardIds,
            CompleteDraftResponse response)
        {
            var deck = response?.ResolvedDeck;
            var activeRun = response?.ResolvedActiveRun;
            if (localDeckCardIds == null || deck == null || activeRun == null)
            {
                return false;
            }

            return localDeckCardIds.Count == 33 &&
                   deck.ResolvedCardCount == localDeckCardIds.Count &&
                   !string.IsNullOrWhiteSpace(deck.ResolvedId) &&
                   !string.IsNullOrWhiteSpace(activeRun.ResolvedId);
        }

        private async Task<bool> LoadAuthoritativeDraftStateAsync()
        {
            CancelDraftPickSave();
            _draftPickSaveCancellation = new CancellationTokenSource();
            _isSavingDraftPicks = true;
            SetStatus("Loading server deck-building state...");
            RefreshStaticUi();

            try
            {
                var client = new ServerRunClient(AccountServerUrl);
                var response = await client.GetDraftStateAsync(
                    AccountSessionState.SessionToken,
                    AccountSessionState.ActiveRunId,
                    _draftPickSaveCancellation.Token);
                AccountSessionState.ApplyDraftStateResponse(response);
                PresentAuthoritativeDraftState(
                    response.ResolvedDraftPickCardIds,
                    response.ResolvedCurrentOfferCardIds);

                var pickCount = response.ResolvedDraftPickCardIds.Count;
                SetStatus(pickCount > 0
                    ? $"Deck building resumed from the server. {pickCount}/33 cards selected."
                    : "Deck building started with a server-generated offer.");
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 authoritative draft load failed: {ex.Message}");
                SetStatus($"Server draft load failed: {ex.Message}");
                _draftOverlayPresenter?.ShowValidationMessage(ex.Message);
                return false;
            }
            finally
            {
                _isSavingDraftPicks = false;
                CancelDraftPickSave();
                RefreshStaticUi();
            }
        }

        private async Task SelectAuthoritativeDraftCardAsync(string cardId)
        {
            var pickIndex = _draftSessionService?.DeckState?.Count ?? -1;
            if (pickIndex < 0 || pickIndex >= 33)
            {
                SetStatus("The server draft pick index is invalid. Reload the deck-building scene.");
                RefreshStaticUi();
                return;
            }

            CancelDraftPickSave();
            _draftPickSaveCancellation = new CancellationTokenSource();
            _isSavingDraftPicks = true;
            SetStatus("Submitting the selected card to the server...");
            RefreshStaticUi();

            try
            {
                var client = new ServerRunClient(AccountServerUrl);
                var response = await client.SelectDraftCardAsync(
                    AccountSessionState.SessionToken,
                    AccountSessionState.ActiveRunId,
                    cardId,
                    pickIndex,
                    _draftPickSaveCancellation.Token);
                AccountSessionState.ApplySelectDraftCardResponse(response);

                if (response.ResolvedIsComplete)
                {
                    var completedCardIds = response.ResolvedDraftPickCardIds;
                    var deck = response.ResolvedDeck;
                    if (completedCardIds.Count != 33 || deck == null || deck.ResolvedCardCount != 33)
                    {
                        throw new InvalidOperationException(
                            "Server completed the draft without returning a verified 33-card deck.");
                    }

                    DraftRunSessionState.SetDraftDeck(completedCardIds);
                    DraftRunSessionState.SetServerRunDeckMetadata(
                        AccountSessionState.ActiveRunId,
                        AccountSessionState.ActiveDeckId);
                    _draftSessionService = null;
                    _draftOverlayPresenter?.Hide();
                    SetStatus("Draft complete. Server deck verified (33/33).");
                    RefreshStaticUi();
                    LoadDraftSceneAfterDeckBuildingComplete();
                    return;
                }

                PresentAuthoritativeDraftState(
                    response.ResolvedDraftPickCardIds,
                    response.ResolvedCurrentOfferCardIds);
                SetStatus(
                    $"Draft picked '{cardId}'. Server progress saved ({response.ResolvedDraftPickCardIds.Count}/33).");
            }
            catch (OperationCanceledException)
            {
                // Scene shutdown or a superseding request cancelled this pick.
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 authoritative draft pick failed: {ex.Message}");
                var restored = await TryRestoreAuthoritativeDraftStateAfterPickFailureAsync();
                if (!restored && this != null)
                {
                    SetStatus($"Server draft pick failed: {ex.Message}");
                    _draftOverlayPresenter?.ShowValidationMessage(ex.Message);
                }
            }
            finally
            {
                if (this != null)
                {
                    _isSavingDraftPicks = false;
                    CancelDraftPickSave();
                    RefreshStaticUi();
                }
            }
        }

        private async Task<bool> TryRestoreAuthoritativeDraftStateAfterPickFailureAsync()
        {
            if (this == null ||
                _draftPickSaveCancellation == null ||
                _draftPickSaveCancellation.IsCancellationRequested)
            {
                return false;
            }

            try
            {
                var client = new ServerRunClient(AccountServerUrl);
                var response = await client.GetDraftStateAsync(
                    AccountSessionState.SessionToken,
                    AccountSessionState.ActiveRunId,
                    _draftPickSaveCancellation.Token);
                AccountSessionState.ApplyDraftStateResponse(response);
                PresentAuthoritativeDraftState(
                    response.ResolvedDraftPickCardIds,
                    response.ResolvedCurrentOfferCardIds);
                SetStatus(
                    $"Latest server deck-building state restored ({response.ResolvedDraftPickCardIds.Count}/33).");
                return true;
            }
            catch (Exception refreshException)
            {
                Debug.LogWarning(
                    $"After333 authoritative draft recovery failed: {refreshException.Message}");
                return false;
            }
        }

        private void PresentAuthoritativeDraftState(
            IReadOnlyList<string> draftPickCardIds,
            IReadOnlyList<string> currentOfferCardIds)
        {
            if (draftPickCardIds == null || draftPickCardIds.Count >= 33)
            {
                throw new InvalidOperationException(
                    "Server draft progress must contain between 0 and 32 selected cards.");
            }

            if (currentOfferCardIds == null || currentOfferCardIds.Count != 3)
            {
                throw new InvalidOperationException(
                    "Server draft offer must contain exactly 3 cards.");
            }

            var restoredSession = new DraftSessionService(_cardCatalogAsset, CreateDraftRandom());
            var validation = restoredSession.ValidateCatalog();
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(validation.Message);
            }

            var offer = restoredSession.ResumeDraft(draftPickCardIds, currentOfferCardIds);
            if (offer == null)
            {
                throw new InvalidOperationException("Server draft did not return a current offer.");
            }

            _draftSessionService = restoredSession;
            DraftRunSessionState.ResetForNewDraft();
            DraftRunSessionState.SetServerRunDeckMetadata(
                AccountSessionState.ActiveRunId,
                string.Empty);
            DraftRunSessionState.SetDraftDeck(draftPickCardIds);
            _draftOverlayPresenter?.ShowOffer(offer, restoredSession.DeckState.CardIds);
            RefreshStaticUi();
        }

        private async Task<bool> SaveInProgressDraftPicksToServerAsync(
            IReadOnlyList<string> draftPickCardIds,
            DraftOffer currentOffer)
        {
            if (!IsServerDraftPersistenceRequired() ||
                !string.Equals(AccountSessionState.ActiveRunStatus, "drafting", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var currentOfferCardIds = ToCardIds(currentOffer);
            if (draftPickCardIds == null ||
                draftPickCardIds.Count >= 33 ||
                (draftPickCardIds.Count == 0 && currentOfferCardIds.Count == 0))
            {
                return true;
            }

            CancelDraftPickSave();
            _draftPickSaveCancellation = new CancellationTokenSource();
            _isSavingDraftPicks = true;
            RefreshStaticUi();

            try
            {
                var client = new ServerRunClient(AccountServerUrl);
                var response = await client.SaveDraftPicksAsync(
                    AccountSessionState.SessionToken,
                    AccountSessionState.ActiveRunId,
                    draftPickCardIds,
                    currentOfferCardIds,
                    _draftPickSaveCancellation.Token);
                AccountSessionState.ApplySaveDraftPicksResponse(response);
                var serverPicks = response.ResolvedDraftPickCardIds;
                if (serverPicks != null && serverPicks.Count > 0 && serverPicks.Count < 33)
                {
                    DraftRunSessionState.SetDraftDeck(serverPicks);
                }

                Debug.Log(
                    $"After333 draft picks saved: run={AccountSessionState.ActiveRunId}, picks={draftPickCardIds.Count}, offer={currentOfferCardIds.Count}");
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 draft pick save failed: {ex.Message}");
                SetStatus($"Draft pick save failed: {ex.Message}");
                return false;
            }
            finally
            {
                _isSavingDraftPicks = false;
                CancelDraftPickSave();
                RefreshStaticUi();
            }
        }

        private void CancelCompleteDraftSave()
        {
            if (_completeDraftCancellation == null)
            {
                return;
            }

            _completeDraftCancellation.Cancel();
            _completeDraftCancellation.Dispose();
            _completeDraftCancellation = null;
        }

        private void CancelDraftPickSave()
        {
            if (_draftPickSaveCancellation == null)
            {
                return;
            }

            _draftPickSaveCancellation.Cancel();
            _draftPickSaveCancellation.Dispose();
            _draftPickSaveCancellation = null;
        }

        private async Task RefreshServerAccountStateAsync()
        {
            CancelAccountRefresh();
            _accountRefreshCancellation = new CancellationTokenSource();

            try
            {
                var client = new GuestAuthClient(AccountServerUrl);
                if (!AccountSessionState.IsAuthenticated)
                {
                    SetStatus("로그인이 필요합니다. 시작 화면으로 돌아갑니다.");
                    RefreshStaticUi();
                    ReturnToStartSceneFromUi();
                    return;
                }

                var response = await client.GetMeAsync(
                    AccountSessionState.SessionToken,
                    _accountRefreshCancellation.Token);
                if (this == null)
                {
                    return;
                }

                var localWinsBeforeSync = DraftRunSessionState.Wins;
                var localLossesBeforeSync = DraftRunSessionState.Losses;
                var localRunEndedBeforeSync = DraftRunSessionState.HasRunEnded;
                var localDeckCountBeforeSync = ResolveVisibleDeckCardIds()?.Count ?? 0;
                AccountSessionState.ApplyMeResponse(response);

                if (!string.IsNullOrWhiteSpace(AccountSessionState.ActiveRunId))
                {
                    DraftRunSessionState.SetServerRunDeckMetadata(
                        AccountSessionState.ActiveRunId,
                        AccountSessionState.ActiveDeckId);
                    var activeDeckCardIds = response.ResolvedActiveDeckCardIds;
                    var serverDeckCardCount = activeDeckCardIds?.Count ?? 0;
                    if (activeDeckCardIds != null && activeDeckCardIds.Count == 33)
                    {
                        DraftRunSessionState.SetDraftDeck(activeDeckCardIds);
                    }
                    else if (string.Equals(AccountSessionState.ActiveRunStatus, "drafting", StringComparison.OrdinalIgnoreCase))
                    {
                        var activeDraftPickCardIds = response.ResolvedActiveDraftPickCardIds;
                        if (activeDraftPickCardIds != null &&
                            activeDraftPickCardIds.Count > 0 &&
                            activeDraftPickCardIds.Count < 33)
                        {
                            DraftRunSessionState.SetDraftDeck(activeDraftPickCardIds);
                        }
                    }

                    if (IsServerRunRecordBehindLocal(
                            localWinsBeforeSync,
                            localLossesBeforeSync,
                            AccountSessionState.ActiveRunWins,
                            AccountSessionState.ActiveRunLosses))
                    {
                        _isWaitingForServerRunResultSync = true;
                        SetStatus(
                            $"Waiting for server run result sync... Local W:{localWinsBeforeSync} L:{localLossesBeforeSync}, Server W:{AccountSessionState.ActiveRunWins} L:{AccountSessionState.ActiveRunLosses}.");
                        QueueCompletedRunServerSyncRetry();
                    }
                    else if (localRunEndedBeforeSync && AccountSessionState.HasResumableRun)
                    {
                        _isWaitingForServerRunResultSync = true;
                        SetStatus("Run complete locally. Waiting for server reward sync...");
                        QueueCompletedRunServerSyncRetry();
                    }
                    else
                    {
                        _isWaitingForServerRunResultSync = false;
                        _completedRunServerSyncAttempts = 0;
                        DraftRunSessionState.ApplyServerRunRecord(
                            AccountSessionState.ActiveRunWins,
                            AccountSessionState.ActiveRunLosses);

                        if (DraftRunSessionState.HasDraftedDeckReady)
                        {
                            SetStatus(
                                $"Server run synced. Wins: {AccountSessionState.ActiveRunWins} Losses: {AccountSessionState.ActiveRunLosses}.");
                        }
                        else
                        {
                            SetStatus("Server draft run synced. Continue drafting.");
                        }
                    }

                    Debug.Log(
                        $"[After333 Draft Sync] /me run={FormatLogValue(AccountSessionState.ActiveRunId)} status={FormatLogValue(AccountSessionState.ActiveRunStatus)} deck={FormatLogValue(AccountSessionState.ActiveDeckId)} serverRecord={AccountSessionState.ActiveRunWins}-{AccountSessionState.ActiveRunLosses} serverDeckCards={serverDeckCardCount} serverDraftPicks={AccountSessionState.ActiveDraftPicks.Count} localBefore={localWinsBeforeSync}-{localLossesBeforeSync} localDeckBefore={localDeckCountBeforeSync}");
                }
                else if (AccountSessionState.HasUnclaimedLatestRunRewards)
                {
                    _isWaitingForServerRunResultSync = false;
                    _completedRunServerSyncAttempts = 0;
                    DraftRunSessionState.SetServerRunDeckMetadata(
                        AccountSessionState.LatestRunId,
                        AccountSessionState.LatestDeckId);
                    var latestDeckCardIds = response.ResolvedLatestDeckCardIds;
                    var latestDeckCardCount = latestDeckCardIds?.Count ?? 0;
                    if (latestDeckCardIds != null && latestDeckCardIds.Count == 33)
                    {
                        DraftRunSessionState.SetDraftDeck(latestDeckCardIds);
                    }

                    DraftRunSessionState.ApplyServerRunRecord(
                        AccountSessionState.LatestRunWins,
                        AccountSessionState.LatestRunLosses);

                    SetStatus(AccountSessionState.IsLatestRunCompleted
                        ? BuildRunCompleteStatusText()
                        : $"Latest server run synced. Wins: {AccountSessionState.LatestRunWins} Losses: {AccountSessionState.LatestRunLosses}.");

                    Debug.Log(
                        $"[After333 Draft Sync] /me latestRun={FormatLogValue(AccountSessionState.LatestRunId)} status={FormatLogValue(AccountSessionState.LatestRunStatus)} latestDeck={FormatLogValue(AccountSessionState.LatestDeckId)} latestRecord={AccountSessionState.LatestRunWins}-{AccountSessionState.LatestRunLosses} latestDeckCards={latestDeckCardCount} localBefore={localWinsBeforeSync}-{localLossesBeforeSync} localDeckBefore={localDeckCountBeforeSync}");
                }
                else if (AccountSessionState.IsLatestRunRewardClaimed)
                {
                    if (localRunEndedBeforeSync && DraftRunSessionState.HasDraftedDeckReady && !IsCurrentRunRewardClaimed())
                    {
                        _isWaitingForServerRunResultSync = true;
                        SetStatus("Run complete locally. Waiting for server reward sync...");
                        QueueCompletedRunServerSyncRetry();
                    }
                    else
                    {
                        _isWaitingForServerRunResultSync = false;
                        DraftRunSessionState.ResetForNewDraft();
                        SetStatus("Rewards already claimed. Return to the start screen to begin a new run.");
                    }

                    Debug.Log(
                        $"[After333 Draft Sync] /me latest run rewards already claimed. latestRun={FormatLogValue(AccountSessionState.LatestRunId)} localBefore={localWinsBeforeSync}-{localLossesBeforeSync} localDeckBefore={localDeckCountBeforeSync}");
                }
                else
                {
                    if (localRunEndedBeforeSync && DraftRunSessionState.HasDraftedDeckReady)
                    {
                        _isWaitingForServerRunResultSync = true;
                        SetStatus("Run complete locally. Waiting for server reward sync...");
                        QueueCompletedRunServerSyncRetry();
                    }

                    Debug.Log(
                        $"[After333 Draft Sync] /me has no active run. localBefore={localWinsBeforeSync}-{localLossesBeforeSync} localDeckBefore={localDeckCountBeforeSync}");
                }

                await RefreshPvpReconnectStatusAsync(
                    client,
                    AccountSessionState.SessionToken,
                    _accountRefreshCancellation.Token);
                if (_hasPvpReconnectableBattle &&
                    DraftRunSessionState.HasDraftedDeckReady &&
                    !IsRunEndedForUi())
                {
                    SetStatus(
                        $"Interrupted PVP battle found. Press PVP to reconnect ({_pvpReconnectRemainingSeconds}s left).");
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 server account refresh failed: {ex.Message}");
            }
            finally
            {
                CancelAccountRefresh();
                if (this != null)
                {
                    RefreshStaticUi();
                }
            }
        }

        private async Task RefreshPvpReconnectStatusAsync(
            GuestAuthClient client,
            string sessionToken,
            CancellationToken cancellationToken)
        {
            ClearPvpReconnectStatus();

            if (client == null || string.IsNullOrWhiteSpace(sessionToken))
            {
                return;
            }

            try
            {
                var response = await client.GetPvpReconnectStatusAsync(sessionToken, cancellationToken);
                if (response == null || !response.ResolvedHasReconnectableBattle)
                {
                    return;
                }

                var remainingSeconds = Math.Max(0, response.ResolvedRemainingSeconds);
                if (remainingSeconds <= 0)
                {
                    return;
                }

                _hasPvpReconnectableBattle = true;
                _pvpReconnectMatchId = response.ResolvedMatchId ?? string.Empty;
                _pvpReconnectRemainingSeconds = remainingSeconds;
                _pvpReconnectDeadlineRealtime = UnityEngine.Time.unscaledTime + _pvpReconnectRemainingSeconds;
                _lastPresentedPvpReconnectRemainingSeconds = _pvpReconnectRemainingSeconds;
                Debug.Log(
                    $"[After333 Draft Sync] PVP reconnect available: match={FormatLogValue(_pvpReconnectMatchId)} remaining={_pvpReconnectRemainingSeconds}s seat={FormatLogValue(response.ResolvedOnlineSeatId)}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 PVP reconnect status refresh failed: {ex.Message}");
            }
        }

        private void ClearPvpReconnectStatus()
        {
            _hasPvpReconnectableBattle = false;
            _pvpReconnectMatchId = string.Empty;
            _pvpReconnectRemainingSeconds = 0;
            _pvpReconnectDeadlineRealtime = 0f;
            _lastPresentedPvpReconnectRemainingSeconds = -1;
        }

        private static bool IsServerRunRecordBehindLocal(
            int localWins,
            int localLosses,
            int serverWins,
            int serverLosses)
        {
            return serverWins < localWins || serverLosses < localLosses;
        }

        private async Task<bool> TrySyncLocalCompletedRunRecordAsync(string runId, string deckId)
        {
            if (!AccountSessionState.IsAuthenticated || !DraftRunSessionState.HasRunEnded)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(deckId))
            {
                Debug.LogWarning(
                    $"After333 local run reward sync skipped: missing run/deck id. run={FormatLogValue(runId)} deck={FormatLogValue(deckId)}");
                SetStatus("Reward sync failed: server run or deck id is missing.");
                return false;
            }

            try
            {
                CancelCompletedRunServerSyncRetry();
                SetStatus("Updating completed PVE run record on server...");
                RefreshStaticUi();
                Debug.Log(
                    $"After333 local run reward sync request: run={FormatLogValue(runId)} deck={FormatLogValue(deckId)} localRecord={DraftRunSessionState.Wins}-{DraftRunSessionState.Losses} activeRun={FormatLogValue(AccountSessionState.ActiveRunId)} activeDeck={FormatLogValue(AccountSessionState.ActiveDeckId)} latestRun={FormatLogValue(AccountSessionState.LatestRunId)} latestDeck={FormatLogValue(AccountSessionState.LatestDeckId)} sessionRun={FormatLogValue(DraftRunSessionState.ServerRunId)} sessionDeck={FormatLogValue(DraftRunSessionState.ServerDeckId)}");
                var client = new ServerRunClient(AccountServerUrl);
                var response = await client.SyncLocalRunRecordAsync(
                    AccountSessionState.SessionToken,
                    runId,
                    deckId,
                    DraftRunSessionState.Wins,
                    DraftRunSessionState.Losses,
                    CancellationToken.None);
                if (this == null)
                {
                    return false;
                }

                AccountSessionState.ApplySyncLocalRunRecordResponse(response);
                DraftRunSessionState.SetServerRunDeckMetadata(runId, deckId);
                DraftRunSessionState.ApplyServerRunRecord(
                    AccountSessionState.LatestRunWins,
                    AccountSessionState.LatestRunLosses);
                _completedRunServerSyncAttempts = 0;
                Debug.Log(
                    $"After333 local PVE run record synced: run={AccountSessionState.LatestRunId}, wins={AccountSessionState.LatestRunWins}, losses={AccountSessionState.LatestRunLosses}, status={AccountSessionState.LatestRunStatus}");
                SetStatus("Server run record synced. Claiming rewards...");
                return IsLatestServerRunCompleted();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"After333 local run reward sync failed: {ex.Message}");
                SetStatus($"Reward sync failed: {ex.Message}");
                return false;
            }
        }

        private static string ResolveServerRunIdForLocalCompletion()
        {
            if (!string.IsNullOrWhiteSpace(DraftRunSessionState.ServerRunId))
            {
                return DraftRunSessionState.ServerRunId;
            }

            if (!string.IsNullOrWhiteSpace(AccountSessionState.ActiveRunId))
            {
                return AccountSessionState.ActiveRunId;
            }

            return AccountSessionState.LatestRunId;
        }

        private static void ReconcileLocalRunRecordFromAccountState()
        {
            if (!AccountSessionState.IsAuthenticated)
            {
                return;
            }

            if (AccountSessionState.HasUnclaimedLatestRunRewards)
            {
                DraftRunSessionState.SetServerRunDeckMetadata(
                    AccountSessionState.LatestRunId,
                    AccountSessionState.LatestDeckId);
                DraftRunSessionState.TryApplyAuthoritativeServerRunRecord(
                    AccountSessionState.LatestRunWins,
                    AccountSessionState.LatestRunLosses,
                    isCompletedRun: true);
                return;
            }

            if (!AccountSessionState.HasResumableRun)
            {
                return;
            }

            var localRunId = DraftRunSessionState.ServerRunId;
            if (!string.IsNullOrWhiteSpace(localRunId) &&
                !string.Equals(localRunId, AccountSessionState.ActiveRunId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            DraftRunSessionState.SetServerRunDeckMetadata(
                AccountSessionState.ActiveRunId,
                AccountSessionState.ActiveDeckId);
            DraftRunSessionState.TryApplyAuthoritativeServerRunRecord(
                AccountSessionState.ActiveRunWins,
                AccountSessionState.ActiveRunLosses,
                isCompletedRun: false);
        }

        private static string ResolveServerDeckIdForLocalCompletion()
        {
            if (!string.IsNullOrWhiteSpace(DraftRunSessionState.ServerDeckId))
            {
                return DraftRunSessionState.ServerDeckId;
            }

            if (!string.IsNullOrWhiteSpace(AccountSessionState.ActiveDeckId))
            {
                return AccountSessionState.ActiveDeckId;
            }

            return AccountSessionState.LatestDeckId;
        }

        private static (string RunId, string DeckId) ResolveServerRunDeckIdsForLocalCompletion()
        {
            if (!string.IsNullOrWhiteSpace(AccountSessionState.ActiveRunId) &&
                !string.IsNullOrWhiteSpace(AccountSessionState.ActiveDeckId))
            {
                return (AccountSessionState.ActiveRunId, AccountSessionState.ActiveDeckId);
            }

            if (!string.IsNullOrWhiteSpace(DraftRunSessionState.ServerRunId) &&
                !string.IsNullOrWhiteSpace(DraftRunSessionState.ServerDeckId))
            {
                return (DraftRunSessionState.ServerRunId, DraftRunSessionState.ServerDeckId);
            }

            if (!string.IsNullOrWhiteSpace(AccountSessionState.LatestRunId) &&
                !string.IsNullOrWhiteSpace(AccountSessionState.LatestDeckId))
            {
                return (AccountSessionState.LatestRunId, AccountSessionState.LatestDeckId);
            }

            return (ResolveServerRunIdForLocalCompletion(), ResolveServerDeckIdForLocalCompletion());
        }

        private void CancelAccountRefresh()
        {
            if (_accountRefreshCancellation == null)
            {
                return;
            }

            _accountRefreshCancellation.Cancel();
            _accountRefreshCancellation.Dispose();
            _accountRefreshCancellation = null;
        }

        private void CancelClaimRewards()
        {
            if (_claimRewardsCancellation == null)
            {
                return;
            }

            _claimRewardsCancellation.Cancel();
            _claimRewardsCancellation.Dispose();
            _claimRewardsCancellation = null;
        }

        private static string FormatLockedBattleModeLabel(string labelFormat, int visibleDeckCount)
        {
            if (string.IsNullOrWhiteSpace(labelFormat))
            {
                return $"{visibleDeckCount}/33";
            }

            try
            {
                return string.Format(labelFormat, visibleDeckCount);
            }
            catch (FormatException)
            {
                return labelFormat;
            }
        }

        private string BuildBattleModeLockedLabel(string labelFormat, int visibleDeckCount)
        {
            if (IsRunEndedForUi())
            {
                return "Run Complete";
            }

            if (_isSavingCompletedDeck)
            {
                return "Saving Deck...";
            }

            if (_completedDeckSaveFailed)
            {
                return "Save Failed";
            }

            if (DraftRunSessionState.HasDraftedDeckReady && IsServerDraftSaveBlockingBattle())
            {
                return "Deck Not Saved";
            }

            return FormatLockedBattleModeLabel(labelFormat, visibleDeckCount);
        }

        private string BuildPvpBattleButtonReadyLabel()
        {
            if (!_hasPvpReconnectableBattle)
            {
                return _pvpBattleButtonReadyLabel;
            }

            var remainingText = _pvpReconnectRemainingSeconds > 0
                ? $" ({_pvpReconnectRemainingSeconds}초)"
                : string.Empty;
            return $"PVP 재접속{remainingText}";
        }

        private bool CanReconnectPvpBattle(bool isDeckBuildingScene, bool isRunEndedForUi)
        {
            return _hasPvpReconnectableBattle &&
                   !isDeckBuildingScene &&
                   !isRunEndedForUi &&
                   AccountSessionState.IsAuthenticated;
        }

        private static bool IsServerDraftPersistenceRequired()
        {
            return AccountSessionState.IsAuthenticated &&
                   !string.IsNullOrWhiteSpace(AccountSessionState.ActiveRunId);
        }

        private static bool IsServerDraftSaveBlockingBattle()
        {
            return IsServerDraftPersistenceRequired() &&
                   string.IsNullOrWhiteSpace(AccountSessionState.ActiveDeckId);
        }

        private bool IsServerRunResultSyncBlockingBattle()
        {
            return _isWaitingForServerRunResultSync ||
                   _completedRunServerSyncRetryCoroutine != null;
        }

        private static bool IsLatestServerRunCompleted()
        {
            return AccountSessionState.IsAuthenticated &&
                   AccountSessionState.HasUnclaimedLatestRunRewards;
        }

        private static bool IsCurrentRunRewardClaimed()
        {
            if (!AccountSessionState.IsLatestRunRewardClaimed)
            {
                return false;
            }

            var currentRunId = ResolveServerRunDeckIdsForLocalCompletion().RunId;
            return !string.IsNullOrWhiteSpace(currentRunId) &&
                   string.Equals(
                       AccountSessionState.LatestRunId,
                       currentRunId,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRunEndedForUi()
        {
            return IsLatestServerRunCompleted() || DraftRunSessionState.HasRunEnded;
        }

        private static bool CanBeginDraftInThisScene()
        {
            if (!AccountSessionState.IsAuthenticated)
            {
                return true;
            }

            return AccountSessionState.HasResumableRun &&
                   string.Equals(AccountSessionState.ActiveRunStatus, "drafting", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildRunCompleteStatusText()
        {
            if (DraftRunSessionState.Wins >= DraftRunSessionState.RunWinLimit)
            {
                return "Run Complete: 33 Wins. Rewards are ready.";
            }

            if (DraftRunSessionState.Losses >= DraftRunSessionState.RunLossLimit)
            {
                return "Run Complete: 3 Losses. Rewards are ready.";
            }

            if (AccountSessionState.IsLatestRunRewardClaimed)
            {
                return "Run Complete. Rewards claimed.";
            }

            if (AccountSessionState.LatestRunEndedByWins)
            {
                return $"Run Complete: 33 Wins. Rewards are ready.";
            }

            if (AccountSessionState.LatestRunEndedByLosses)
            {
                return $"Run Complete: 3 Losses. Rewards are ready.";
            }

            return $"Run Complete. Rewards are ready.";
        }

        private static string FormatLogValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static int CountUpgradedCardKinds(IReadOnlyList<string> cardIds)
        {
            if (cardIds == null || cardIds.Count == 0)
            {
                return 0;
            }

            var upgradedCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < cardIds.Count; i++)
            {
                var cardId = cardIds[i];
                if (!string.IsNullOrWhiteSpace(cardId) &&
                    AccountSessionState.GetOwnedCardUpgradeLevel(cardId) > 0)
                {
                    upgradedCardIds.Add(cardId);
                }
            }

            return upgradedCardIds.Count;
        }

        private void EnsureReturnToStartButton()
        {
            var controlPanel = transform.Find("ControlPanel") as RectTransform;

            if (_returnToStartButton == null && controlPanel != null)
            {
                _returnToStartButton = FindButton(controlPanel, "ReturnToStartButton");
            }

            if (_returnToStartButton != null)
            {
                ConfigureReturnToStartButton(_returnToStartButton);
                if (controlPanel != null)
                {
                    RepositionControlPanelStatusArea(controlPanel);
                }

                return;
            }

            if (!_autoCreateMissingDraftUi ||
                controlPanel == null)
            {
                return;
            }

            var buttonObject = new GameObject(
                "ReturnToStartButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            RegisterEditorCreatedObject(buttonObject);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(controlPanel, false);
            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.sizeDelta = _returnToStartButtonSize;
            buttonRect.anchoredPosition = _returnToStartButtonPosition;

            var image = buttonObject.GetComponent<Image>();
            image.color = _returnToStartButtonColor;

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(0.82f, 0.9f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(ReturnToStartSceneFromUi);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            RegisterEditorCreatedObject(labelObject);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(buttonRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = _returnToStartButtonLabelPadding;
            labelRect.offsetMax = -_returnToStartButtonLabelPadding;

            var labelText = labelObject.GetComponent<Text>();
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.fontStyle = FontStyle.Bold;
            labelText.fontSize = _returnToStartButtonFontSize;
            labelText.color = Color.white;
            labelText.text = ResolveReturnButtonLabel();
            labelText.raycastTarget = false;
            labelText.font = ResolveRuntimeFont();

            RepositionControlPanelStatusArea(controlPanel);
            _returnToStartButton = button;
        }

        private void ConfigureReturnToStartButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(ReturnToStartSceneFromUi);
            button.onClick.AddListener(ReturnToStartSceneFromUi);

            if (ShouldApplyDraftUiLayoutToSceneObjects() && button.transform is RectTransform buttonRect)
            {
                buttonRect.sizeDelta = _returnToStartButtonSize;
                buttonRect.anchoredPosition = _returnToStartButtonPosition;
            }

            if (button.TryGetComponent<Image>(out var image))
            {
                image.color = _returnToStartButtonColor;
            }

            var label = ResolveButtonLabel(button);
            if (label != null)
            {
                label.text = ResolveReturnButtonLabel();
                label.fontSize = _returnToStartButtonFontSize;
                if (ShouldApplyDraftUiLayoutToSceneObjects() && label.rectTransform != null)
                {
                    label.rectTransform.offsetMin = _returnToStartButtonLabelPadding;
                    label.rectTransform.offsetMax = -_returnToStartButtonLabelPadding;
                }
            }
        }

        private void QueueCompletedRunServerSyncRetry()
        {
            if (_completedRunServerSyncRetryCoroutine != null ||
                _completedRunServerSyncAttempts >= CompletedRunServerSyncMaxAttempts)
            {
                if (_completedRunServerSyncAttempts >= CompletedRunServerSyncMaxAttempts)
                {
                    _isWaitingForServerRunResultSync = false;
                }

                return;
            }

            _isWaitingForServerRunResultSync = true;
            _completedRunServerSyncRetryCoroutine = StartCoroutine(CompletedRunServerSyncRetrySequence());
        }

        private IEnumerator CompletedRunServerSyncRetrySequence()
        {
            _completedRunServerSyncAttempts += 1;
            yield return new WaitForSecondsRealtime(CompletedRunServerSyncRetryDelaySeconds);
            _completedRunServerSyncRetryCoroutine = null;

            var refreshTask = RefreshServerAccountStateAsync();
            while (!refreshTask.IsCompleted)
            {
                yield return null;
            }

            if (refreshTask.Exception != null)
            {
                _isWaitingForServerRunResultSync = false;
                Debug.LogWarning($"After333 completed run reward sync retry failed: {refreshTask.Exception.GetBaseException().Message}");
                RefreshStaticUi();
            }
        }

        private void CancelCompletedRunServerSyncRetry()
        {
            _isWaitingForServerRunResultSync = false;

            if (_completedRunServerSyncRetryCoroutine == null)
            {
                return;
            }

            StopCoroutine(_completedRunServerSyncRetryCoroutine);
            _completedRunServerSyncRetryCoroutine = null;
        }

        private void EnsureServerWalletDisplay()
        {
            if (_serverWalletText != null || !_autoCreateServerWalletDisplay)
            {
                return;
            }

            var canvasObject = new GameObject(
                "DraftServerWalletCanvas",
                typeof(Canvas),
                typeof(CanvasScaler));
            RegisterEditorCreatedObject(canvasObject);
            _serverWalletCanvas = canvasObject.GetComponent<Canvas>();
            _serverWalletCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _serverWalletCanvas.sortingOrder = 3200;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var panelObject = new GameObject("DraftServerWalletPanel", typeof(RectTransform), typeof(Image));
            RegisterEditorCreatedObject(panelObject);
            panelObject.transform.SetParent(canvasObject.transform, false);
            _serverWalletPanelRect = panelObject.GetComponent<RectTransform>();
            _serverWalletPanelImage = panelObject.GetComponent<Image>();
            _serverWalletPanelImage.raycastTarget = false;
            _serverWalletPanelNeedsDefaultLayout = true;

            var textObject = new GameObject("DraftServerWalletText", typeof(RectTransform), typeof(Text));
            RegisterEditorCreatedObject(textObject);
            textObject.transform.SetParent(panelObject.transform, false);
            _serverWalletTextRect = textObject.GetComponent<RectTransform>();
            _serverWalletText = textObject.GetComponent<Text>();
            _serverWalletText.alignment = TextAnchor.MiddleRight;
            _serverWalletText.fontStyle = FontStyle.Bold;
            _serverWalletText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _serverWalletText.verticalOverflow = VerticalWrapMode.Truncate;
            _serverWalletText.supportRichText = false;
            _serverWalletText.raycastTarget = false;
            _serverWalletTextNeedsDefaultLayout = true;

            ApplyServerWalletDisplaySettings();
        }

        private void RefreshServerWalletDisplay()
        {
            if (_serverWalletText == null)
            {
                return;
            }

            ApplyServerWalletDisplaySettings();
            _serverWalletText.text = AccountSessionState.IsAuthenticated
                ? $"Tickets: {AccountSessionState.Tickets}\nGold: {AccountSessionState.ResourceGold}"
                : "Tickets: -\nGold: -";
        }

        private void ApplyServerWalletDisplaySettings()
        {
            if (_serverWalletCanvas != null)
            {
                SafeAreaFitter.EnsureCanvasContentRoot(
                    _serverWalletCanvas.transform as RectTransform,
                    "DraftServerWalletSafeArea");
            }

            if (_serverWalletPanelRect != null)
            {
                if (_serverWalletPanelNeedsDefaultLayout)
                {
                    var anchor = new Vector2(
                        Mathf.Clamp01(_serverWalletAnchor.x),
                        Mathf.Clamp01(_serverWalletAnchor.y));
                    _serverWalletPanelRect.anchorMin = anchor;
                    _serverWalletPanelRect.anchorMax = anchor;
                    _serverWalletPanelRect.pivot = new Vector2(1f, 1f);
                    _serverWalletPanelRect.anchoredPosition = _serverWalletOffset;
                    _serverWalletPanelRect.sizeDelta = new Vector2(
                        Mathf.Max(1f, _serverWalletSize.x),
                        Mathf.Max(1f, _serverWalletSize.y));
                    _serverWalletPanelNeedsDefaultLayout = false;
                }
            }

            if (_serverWalletPanelImage != null)
            {
                _serverWalletPanelImage.color = _serverWalletPanelColor;
                _serverWalletPanelImage.raycastTarget = false;
            }

            if (_serverWalletTextRect != null)
            {
                if (_serverWalletTextNeedsDefaultLayout)
                {
                    var horizontalPadding = Mathf.Max(0f, _serverWalletTextPadding.x);
                    var verticalPadding = Mathf.Max(0f, _serverWalletTextPadding.y);
                    _serverWalletTextRect.anchorMin = Vector2.zero;
                    _serverWalletTextRect.anchorMax = Vector2.one;
                    _serverWalletTextRect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
                    _serverWalletTextRect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
                    _serverWalletTextNeedsDefaultLayout = false;
                }
            }

            if (_serverWalletText != null)
            {
                _serverWalletText.font = ResolveRuntimeFont();
                _serverWalletText.color = _serverWalletTextColor;
                _serverWalletText.fontSize = Mathf.Max(1, _serverWalletFontSize);
            }
        }

        private static string BuildRewardClaimStatusText(
            long resourceGoldDelta,
            int ticketDelta,
            long serverGold,
            RewardGrantDto reward)
        {
            return $"Rewards claimed. {BuildRewardSummaryText(resourceGoldDelta, ticketDelta, reward)}. Server Gold: {serverGold}.";
        }

        private string BuildRewardClaimPopupText(long resourceGoldDelta, int ticketDelta, long serverGold, RewardGrantDto reward)
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("보상 획득!");
            builder.AppendLine();

            var hasRewardLine = false;
            if (resourceGoldDelta > 0)
            {
                builder.AppendLine($"골드 +{resourceGoldDelta}");
                hasRewardLine = true;
            }

            if (ticketDelta > 0)
            {
                builder.AppendLine($"티켓 +{ticketDelta}");
                hasRewardLine = true;
            }

            var cardRewardItems = reward?.ResolvedCardRewardItems;
            if (hasRewardLine && HasRewardPopupItems(cardRewardItems))
            {
                builder.AppendLine();
            }

            hasRewardLine |= AppendGroupedCardRewardPopupItems(builder, cardRewardItems);
            hasRewardLine |= AppendRewardPopupItems(builder, "팩", reward?.ResolvedPackRewardItems, " Pack");

            if (!hasRewardLine)
            {
                builder.AppendLine("지급된 보상 변화가 없습니다.");
            }

            return builder.ToString().TrimEnd();
        }

        private bool AppendRewardPopupItems(
            System.Text.StringBuilder builder,
            string title,
            RewardItemDto[] items,
            string suffix)
        {
            if (items == null || items.Length == 0)
            {
                return false;
            }

            var appendedHeader = false;
            var appendedAny = false;
            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }

                var id = item.ResolvedId;
                var count = item.ResolvedCount;
                if (string.IsNullOrWhiteSpace(id) || count <= 0)
                {
                    continue;
                }

                if (!appendedHeader)
                {
                    builder.AppendLine($"{title}:");
                    appendedHeader = true;
                }

                builder.AppendLine($"- {ResolveRewardItemDisplayName(id)}{suffix} x{count}");
                appendedAny = true;
            }

            return appendedAny;
        }

        private static bool HasRewardPopupItems(RewardItemDto[] items)
        {
            if (items == null || items.Length == 0)
            {
                return false;
            }

            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(item.ResolvedId) && item.ResolvedCount > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool AppendGroupedCardRewardPopupItems(
            System.Text.StringBuilder builder,
            RewardItemDto[] items)
        {
            var rewards = CollectCardRewardPopupItems(items);
            if (rewards.Count == 0)
            {
                return false;
            }

            builder.AppendLine("카드 보상");
            AppendCardRewardGroup(builder, rewards, CardRarity.Legendary);
            AppendCardRewardGroup(builder, rewards, CardRarity.Unique);
            AppendCardRewardGroup(builder, rewards, CardRarity.Rare);
            AppendCardRewardGroup(builder, rewards, CardRarity.Uncommon);
            AppendCardRewardGroup(builder, rewards, CardRarity.Common);
            AppendUnknownCardRewardGroup(builder, rewards);
            return true;
        }

        private List<RewardCardPopupItem> CollectCardRewardPopupItems(RewardItemDto[] items)
        {
            var rewardsById = new Dictionary<string, RewardCardPopupItem>(StringComparer.OrdinalIgnoreCase);
            if (items == null)
            {
                return new List<RewardCardPopupItem>();
            }

            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }

                var id = item.ResolvedId;
                var count = item.ResolvedCount;
                if (string.IsNullOrWhiteSpace(id) || count <= 0)
                {
                    continue;
                }

                if (rewardsById.TryGetValue(id, out var existing))
                {
                    existing.Count += count;
                    continue;
                }

                rewardsById[id] = CreateRewardCardPopupItem(id, count);
            }

            var rewards = new List<RewardCardPopupItem>(rewardsById.Values);
            rewards.Sort(CompareRewardCardPopupItems);
            return rewards;
        }

        private RewardCardPopupItem CreateRewardCardPopupItem(string cardId, int count)
        {
            var item = new RewardCardPopupItem
            {
                CardId = cardId,
                DisplayName = string.IsNullOrWhiteSpace(cardId) ? "Unknown" : cardId,
                Count = count,
                HasRarity = false,
                Rarity = CardRarity.Common
            };

            if (_cardCatalogAsset != null &&
                _cardCatalogAsset.TryGetCardAsset(cardId, out var cardAsset) &&
                cardAsset != null)
            {
                item.DisplayName = string.IsNullOrWhiteSpace(cardAsset.DisplayName)
                    ? cardAsset.CardId
                    : cardAsset.DisplayName;
                item.Rarity = cardAsset.Rarity;
                item.HasRarity = true;
            }

            return item;
        }

        private static int CompareRewardCardPopupItems(RewardCardPopupItem left, RewardCardPopupItem right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var rarityComparison = CompareRewardRarity(left, right);
            if (rarityComparison != 0)
            {
                return rarityComparison;
            }

            var displayNameComparison = string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal);
            if (displayNameComparison != 0)
            {
                return displayNameComparison;
            }

            return string.Compare(left.CardId, right.CardId, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareRewardRarity(RewardCardPopupItem left, RewardCardPopupItem right)
        {
            if (left.HasRarity && right.HasRarity)
            {
                return GetRewardRaritySortOrder(left.Rarity).CompareTo(GetRewardRaritySortOrder(right.Rarity));
            }

            if (left.HasRarity)
            {
                return -1;
            }

            if (right.HasRarity)
            {
                return 1;
            }

            return 0;
        }

        private static int GetRewardRaritySortOrder(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common:
                    return 0;
                case CardRarity.Uncommon:
                    return 1;
                case CardRarity.Rare:
                    return 2;
                case CardRarity.Unique:
                    return 3;
                case CardRarity.Legendary:
                    return 4;
                default:
                    return 99;
            }
        }

        private static void AppendCardRewardGroup(
            System.Text.StringBuilder builder,
            IReadOnlyList<RewardCardPopupItem> rewards,
            CardRarity rarity)
        {
            var group = new List<RewardCardPopupItem>();
            var groupTotalCount = 0;
            foreach (var reward in rewards)
            {
                if (reward == null || !reward.HasRarity || reward.Rarity != rarity)
                {
                    continue;
                }

                group.Add(reward);
                groupTotalCount += reward.Count;
            }

            if (group.Count == 0)
            {
                return;
            }

            builder.AppendLine($"[{FormatRewardRarityLabel(rarity)}] {groupTotalCount}장");
            foreach (var reward in group)
            {
                builder.AppendLine($"- {FormatRewardCardLine(reward)}");
            }
        }

        private static void AppendUnknownCardRewardGroup(
            System.Text.StringBuilder builder,
            IReadOnlyList<RewardCardPopupItem> rewards)
        {
            var group = new List<RewardCardPopupItem>();
            var groupTotalCount = 0;
            foreach (var reward in rewards)
            {
                if (reward == null || reward.HasRarity)
                {
                    continue;
                }

                group.Add(reward);
                groupTotalCount += reward.Count;
            }

            if (group.Count == 0)
            {
                return;
            }

            builder.AppendLine($"[Unknown] {groupTotalCount}장");
            foreach (var reward in group)
            {
                builder.AppendLine($"- {FormatRewardCardLine(reward)}");
            }
        }

        private static string FormatRewardCardLine(RewardCardPopupItem reward)
        {
            if (reward == null)
            {
                return "Unknown x0";
            }

            var displayName = string.IsNullOrWhiteSpace(reward.DisplayName)
                ? reward.CardId
                : reward.DisplayName;
            return $"{displayName} x{reward.Count}";
        }

        private static string FormatRewardRarityLabel(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common:
                    return "Common";
                case CardRarity.Uncommon:
                    return "Uncommon";
                case CardRarity.Rare:
                    return "Rare";
                case CardRarity.Unique:
                    return "Unique";
                case CardRarity.Legendary:
                    return "Legendary";
                default:
                    return "Unknown";
            }
        }

        private string ResolveRewardItemDisplayName(string cardId)
        {
            if (!string.IsNullOrWhiteSpace(cardId) && _cardCatalogAsset?.Cards != null)
            {
                foreach (var cardAsset in _cardCatalogAsset.Cards)
                {
                    if (cardAsset == null ||
                        !string.Equals(cardAsset.CardId, cardId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    return string.IsNullOrWhiteSpace(cardAsset.DisplayName)
                        ? cardAsset.CardId
                        : cardAsset.DisplayName;
                }
            }

            return string.IsNullOrWhiteSpace(cardId) ? "Unknown" : cardId;
        }

        private static string BuildRewardSummaryText(long resourceGoldDelta, int ticketDelta, RewardGrantDto reward)
        {
            var parts = new List<string>();

            if (resourceGoldDelta > 0)
            {
                parts.Add($"Gold +{resourceGoldDelta}");
            }

            if (ticketDelta > 0)
            {
                parts.Add($"Ticket +{ticketDelta}");
            }

            AppendRewardItems(parts, reward?.ResolvedCardRewardItems, string.Empty);
            AppendRewardItems(parts, reward?.ResolvedPackRewardItems, " Pack");

            return parts.Count > 0 ? string.Join(" / ", parts) : "No reward delta";
        }

        private static void AppendRewardItems(List<string> parts, RewardItemDto[] items, string suffix)
        {
            if (items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }

                var id = item.ResolvedId;
                var count = item.ResolvedCount;
                if (string.IsNullOrWhiteSpace(id) || count <= 0)
                {
                    continue;
                }

                parts.Add($"{id}{suffix} +{count}");
            }
        }

        private void ShowRewardClaimToast(long resourceGoldDelta, int ticketDelta, long serverGold, RewardGrantDto reward)
        {
            EnsureRewardClaimToast();
            if (_rewardClaimToastCanvasGroup == null || _rewardClaimToastText == null)
            {
                return;
            }

            ApplyRewardClaimToastSettings();
            _rewardClaimToastText.text = BuildRewardClaimPopupText(resourceGoldDelta, ticketDelta, serverGold, reward);
            _rewardClaimToastCanvasGroup.gameObject.SetActive(true);
            _rewardClaimToastCanvasGroup.alpha = 1f;
            _rewardClaimToastCanvasGroup.interactable = true;
            _rewardClaimToastCanvasGroup.blocksRaycasts = true;
            _isRewardClaimPopupAwaitingClick = true;
            _rewardClaimPopupEarliestCloseTime = UnityEngine.Time.unscaledTime + 0.15f;
        }

        private void ReturnToStartSceneAfterRewardPopupFromUi()
        {
            if (!_isRewardClaimPopupAwaitingClick &&
                (_rewardClaimToastCanvasGroup == null || !_rewardClaimToastCanvasGroup.gameObject.activeInHierarchy))
            {
                return;
            }

            _isRewardClaimPopupAwaitingClick = false;
            if (_rewardClaimToastCanvasGroup != null)
            {
                _rewardClaimToastCanvasGroup.interactable = false;
                _rewardClaimToastCanvasGroup.blocksRaycasts = false;
                _rewardClaimToastCanvasGroup.alpha = 0f;
                _rewardClaimToastCanvasGroup.gameObject.SetActive(false);
            }

            ReturnToStartSceneFromUi();
        }

        private void EnsureRewardClaimToast()
        {
            if (_rewardClaimToastCanvasGroup != null &&
                _rewardClaimToastText != null &&
                _rewardClaimToastClickSurfaceButton != null)
            {
                AttachRewardClaimToastClickHandler();
                return;
            }

            var canvasObject = new GameObject(
                "RewardClaimToastCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            RegisterEditorCreatedObject(canvasObject);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            _rewardClaimToastCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
            _rewardClaimToastCanvasGroup.alpha = 0f;
            _rewardClaimToastCanvasGroup.interactable = false;
            _rewardClaimToastCanvasGroup.blocksRaycasts = false;

            var clickSurfaceObject = new GameObject("RewardClaimToastClickSurface", typeof(RectTransform), typeof(Image), typeof(Button));
            RegisterEditorCreatedObject(clickSurfaceObject);
            clickSurfaceObject.transform.SetParent(canvasObject.transform, false);
            _rewardClaimToastClickSurfaceRect = clickSurfaceObject.GetComponent<RectTransform>();
            _rewardClaimToastClickSurfaceImage = clickSurfaceObject.GetComponent<Image>();
            _rewardClaimToastClickSurfaceButton = clickSurfaceObject.GetComponent<Button>();
            _rewardClaimToastClickSurfaceButton.transition = Selectable.Transition.None;
            _rewardClaimToastClickSurfaceButton.targetGraphic = _rewardClaimToastClickSurfaceImage;
            AttachRewardClaimToastClickHandler();

            var panelObject = new GameObject("RewardClaimToastPanel", typeof(RectTransform), typeof(Image));
            RegisterEditorCreatedObject(panelObject);
            panelObject.transform.SetParent(clickSurfaceObject.transform, false);
            _rewardClaimToastPanelRect = panelObject.GetComponent<RectTransform>();
            _rewardClaimToastPanelImage = panelObject.GetComponent<Image>();
            _rewardClaimToastPanelImage.raycastTarget = false;

            var textObject = new GameObject("RewardClaimToastText", typeof(RectTransform), typeof(Text));
            RegisterEditorCreatedObject(textObject);
            textObject.transform.SetParent(panelObject.transform, false);
            _rewardClaimToastTextRect = textObject.GetComponent<RectTransform>();
            _rewardClaimToastText = textObject.GetComponent<Text>();
            _rewardClaimToastText.alignment = TextAnchor.UpperLeft;
            _rewardClaimToastText.fontStyle = FontStyle.Bold;
            _rewardClaimToastText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _rewardClaimToastText.verticalOverflow = VerticalWrapMode.Truncate;
            _rewardClaimToastText.lineSpacing = 1.05f;
            _rewardClaimToastText.supportRichText = false;
            _rewardClaimToastText.raycastTarget = false;

            ApplyRewardClaimToastSettings();
            canvasObject.SetActive(false);
        }

        private void AttachRewardClaimToastClickHandler()
        {
            if (_rewardClaimToastClickSurfaceButton == null)
            {
                return;
            }

            _rewardClaimToastClickSurfaceButton.onClick.RemoveListener(ReturnToStartSceneAfterRewardPopupFromUi);
            _rewardClaimToastClickSurfaceButton.onClick.AddListener(ReturnToStartSceneAfterRewardPopupFromUi);
        }

        private void ApplyRewardClaimToastSettings()
        {
            if (_rewardClaimToastClickSurfaceRect != null)
            {
                _rewardClaimToastClickSurfaceRect.anchorMin = Vector2.zero;
                _rewardClaimToastClickSurfaceRect.anchorMax = Vector2.one;
                _rewardClaimToastClickSurfaceRect.pivot = new Vector2(0.5f, 0.5f);
                _rewardClaimToastClickSurfaceRect.offsetMin = Vector2.zero;
                _rewardClaimToastClickSurfaceRect.offsetMax = Vector2.zero;
            }

            if (_rewardClaimToastClickSurfaceImage != null)
            {
                _rewardClaimToastClickSurfaceImage.color = new Color(0f, 0f, 0f, 0.58f);
                _rewardClaimToastClickSurfaceImage.raycastTarget = true;
            }

            if (_rewardClaimToastClickSurfaceButton != null)
            {
                _rewardClaimToastClickSurfaceButton.transition = Selectable.Transition.None;
                _rewardClaimToastClickSurfaceButton.targetGraphic = _rewardClaimToastClickSurfaceImage;
            }

            if (_rewardClaimToastPanelRect != null)
            {
                var rawSize = _rewardClaimToastSize;
                var usesLegacyToastLayout = rawSize.y < 180f;
                var anchorSource = usesLegacyToastLayout ? new Vector2(0.5f, 0.5f) : _rewardClaimToastAnchor;
                var offsetSource = usesLegacyToastLayout ? Vector2.zero : _rewardClaimToastOffset;
                var anchor = new Vector2(
                    Mathf.Clamp01(anchorSource.x),
                    Mathf.Clamp01(anchorSource.y));
                _rewardClaimToastPanelRect.anchorMin = anchor;
                _rewardClaimToastPanelRect.anchorMax = anchor;
                _rewardClaimToastPanelRect.pivot = new Vector2(0.5f, 0.5f);
                _rewardClaimToastPanelRect.anchoredPosition = offsetSource;
                _rewardClaimToastPanelRect.sizeDelta = new Vector2(
                    Mathf.Max(760f, rawSize.x),
                    Mathf.Max(420f, rawSize.y));
            }

            if (_rewardClaimToastPanelImage != null)
            {
                _rewardClaimToastPanelImage.color = _rewardClaimToastPanelColor;
                _rewardClaimToastPanelImage.raycastTarget = false;
            }

            if (_rewardClaimToastTextRect != null)
            {
                var horizontalPadding = Mathf.Max(0f, _rewardClaimToastTextPadding.x);
                var verticalPadding = Mathf.Max(0f, _rewardClaimToastTextPadding.y);
                _rewardClaimToastTextRect.anchorMin = Vector2.zero;
                _rewardClaimToastTextRect.anchorMax = Vector2.one;
                _rewardClaimToastTextRect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
                _rewardClaimToastTextRect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
            }

            if (_rewardClaimToastText != null)
            {
                _rewardClaimToastText.font = ResolveKoreanRuntimeFont();
                _rewardClaimToastText.alignment = TextAnchor.UpperLeft;
                _rewardClaimToastText.color = _rewardClaimToastTextColor;
                _rewardClaimToastText.fontSize = Mathf.Max(1, _rewardClaimToastFontSize);
                _rewardClaimToastText.lineSpacing = 1.05f;
            }
        }

        private static Font ResolveRuntimeFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static Font ResolveKoreanRuntimeFont()
        {
            if (s_runtimeKoreanFont != null)
            {
                return s_runtimeKoreanFont;
            }

            try
            {
                s_runtimeKoreanFont = Font.CreateDynamicFontFromOSFont(KoreanFontCandidates, 32);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not create runtime Korean font for reward claim toast: {ex.Message}");
                s_runtimeKoreanFont = null;
            }

            return s_runtimeKoreanFont != null
                ? s_runtimeKoreanFont
                : ResolveRuntimeFont();
        }

        private static void RegisterEditorCreatedObject(GameObject createdObject)
        {
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying && createdObject != null)
            {
                Undo.RegisterCreatedObjectUndo(createdObject, "Create Draft UI Object");
            }
#endif
        }

        private void RepositionControlPanelStatusArea(RectTransform controlPanel)
        {
            // Scene-authored UI layout is the source of truth. This method is kept
            // only so older serialized scenes can compile without the controller
            // rewriting hand-placed status text positions.
        }

        private bool ShouldApplyDraftUiLayoutToSceneObjects()
        {
            // Existing scene UI must not be repositioned or resized by code. The
            // serialized layout values above are used only as initial values when
            // a missing button is created for the first time.
            return false;
        }

        private void SetStatus(string status)
        {
            if (_statusText != null)
            {
                _statusText.text = string.IsNullOrWhiteSpace(status) ? "Ready." : status;
            }
        }

        private string BuildDeckListText(IReadOnlyList<string> draftedCardIds)
        {
            return DraftDeckListFormatter.BuildDeckListText(
                draftedCardIds,
                ResolveCardAsset,
                "No drafted deck yet.",
                "•  ");
        }

        private string BuildDeckSummaryText(IReadOnlyList<string> draftedCardIds)
        {
            var cardCount = draftedCardIds?.Count ?? 0;
            var phaseLabel = IsRunEndedForUi()
                ? "Run Complete"
                : cardCount >= 33 ? "Battle Ready" : "Drafting";
            return $"{phaseLabel}   {cardCount}/33 Cards";
        }

        private IReadOnlyList<string> ResolveVisibleDeckCardIds()
        {
            if (DraftRunSessionState.CurrentDraftDeckCardIds.Count > 0)
            {
                return DraftRunSessionState.CurrentDraftDeckCardIds;
            }

            if (DraftRunSessionState.LastCompletedDraftDeckCardIds.Count > 0)
            {
                return DraftRunSessionState.LastCompletedDraftDeckCardIds;
            }

            if (_draftSessionService?.DeckState?.CardIds != null)
            {
                return _draftSessionService.DeckState.CardIds;
            }

            return DraftRunSessionState.CurrentDraftDeckCardIds;
        }

        private static IReadOnlyList<string> ResolveDraftPickCardIdsForResume()
        {
            if (AccountSessionState.ActiveDraftPicks.Count > 0 &&
                AccountSessionState.ActiveDraftPicks.Count < 33)
            {
                return AccountSessionState.ActiveDraftPicks;
            }

            if (DraftRunSessionState.CurrentDraftDeckCardIds.Count > 0 &&
                DraftRunSessionState.CurrentDraftDeckCardIds.Count < 33)
            {
                return DraftRunSessionState.CurrentDraftDeckCardIds;
            }

            return Array.Empty<string>();
        }

        private static IReadOnlyList<string> ResolveDraftOfferCardIdsForResume()
        {
            if (AccountSessionState.ActiveDraftOffer.Count == 3)
            {
                return AccountSessionState.ActiveDraftOffer;
            }

            return Array.Empty<string>();
        }

        private static IReadOnlyList<string> ToCardIds(DraftOffer draftOffer)
        {
            if (draftOffer?.CandidateCards == null || draftOffer.CandidateCards.Count == 0)
            {
                return Array.Empty<string>();
            }

            var cardIds = new List<string>(draftOffer.CandidateCards.Count);
            for (var i = 0; i < draftOffer.CandidateCards.Count; i++)
            {
                var cardId = draftOffer.CandidateCards[i]?.CardId;
                if (!string.IsNullOrWhiteSpace(cardId))
                {
                    cardIds.Add(cardId);
                }
            }

            return cardIds;
        }

        private DraftBattleLaunchMode ResolvePveBattleLaunchMode()
        {
            return DraftBattleLaunchMode.OnlineServerAi;
        }

        private bool IsDeckBuildingScene()
        {
            return string.Equals(
                SceneManager.GetActiveScene().name,
                ResolveDeckBuildingSceneName(),
                StringComparison.OrdinalIgnoreCase);
        }

        private bool TryLoadDeckBuildingScene()
        {
            var sceneName = ResolveDeckBuildingSceneName();
            if (string.IsNullOrWhiteSpace(sceneName) ||
                string.Equals(SceneManager.GetActiveScene().name, sceneName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SetStatus("DeckBuilding scene is not in Build Settings. Run Tools > Project333 > Draft > Create Deck Building Scene.");
                RefreshStaticUi();
                return false;
            }

            SetStatus("Moving to deck building scene...");
            RefreshStaticUi();
            SceneManager.LoadScene(sceneName);
            return true;
        }

        private void LoadDraftSceneAfterDeckBuildingComplete()
        {
            if (!IsDeckBuildingScene())
            {
                return;
            }

            LoadDraftSceneFromDeckBuilding("Deck saved. Returning to draft run screen...");
        }

        private void LoadDraftSceneFromDeckBuilding(string statusMessage)
        {
            var sceneName = string.IsNullOrWhiteSpace(_draftSceneName)
                ? "Draft_VSlice"
                : _draftSceneName;
            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SetStatus("Draft scene is not in Build Settings. Deck was saved, but cannot return to Draft scene.");
                RefreshStaticUi();
                return;
            }

            SetStatus(statusMessage);
            RefreshStaticUi();
            SceneManager.LoadScene(sceneName);
        }

        private string ResolveReturnButtonLabel()
        {
            return string.IsNullOrWhiteSpace(_returnToStartButtonLabel)
                ? "Back To Start"
                : _returnToStartButtonLabel;
        }

        private string ResolveDeckBuildingSceneName()
        {
            return string.IsNullOrWhiteSpace(_deckBuildingSceneName)
                ? "DeckBuilding_VSlice"
                : _deckBuildingSceneName;
        }

        private void SyncDraftRunDeckState()
        {
            if (_draftSessionService?.DeckState?.CardIds == null)
            {
                return;
            }

            DraftRunSessionState.SetDraftDeck(_draftSessionService.DeckState.CardIds);
        }

        private void RefreshDeckListViewport()
        {
            PrepareDeckListTextLayout();
            EnsureRuntimeDeckListDisplayText();
            EnsureRuntimeDeckPanelText();

            if (_deckListScrollRect == null)
            {
                RefreshRuntimeDeckPanelScrollLayout(resetToTop: true);
                return;
            }

            var viewportHeight = _deckListScrollRect.viewport != null
                ? Mathf.Max(1f, _deckListScrollRect.viewport.rect.height)
                : 1f;
            var activeDeckListText = _runtimeDeckListDisplayText != null ? _runtimeDeckListDisplayText : _deckListText;
            var preferredTextHeight = activeDeckListText != null
                ? Mathf.Max(activeDeckListText.preferredHeight + 16f, viewportHeight)
                : viewportHeight;

            var contentRect = _deckListScrollRect.content;
            if (contentRect != null)
            {
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.pivot = new Vector2(0.5f, 1f);
                contentRect.offsetMin = new Vector2(0f, -preferredTextHeight);
                contentRect.offsetMax = new Vector2(0f, 0f);
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            }

            if (_runtimeDeckListDisplayText != null)
            {
                var runtimeTextRect = _runtimeDeckListDisplayText.rectTransform;
                runtimeTextRect.anchorMin = new Vector2(0f, 1f);
                runtimeTextRect.anchorMax = new Vector2(1f, 1f);
                runtimeTextRect.pivot = new Vector2(0.5f, 1f);
                runtimeTextRect.offsetMin = new Vector2(12f, -preferredTextHeight);
                runtimeTextRect.offsetMax = new Vector2(-12f, 0f);
                LayoutRebuilder.ForceRebuildLayoutImmediate(runtimeTextRect);
            }

            Canvas.ForceUpdateCanvases();
            if (contentRect != null)
            {
                contentRect.anchoredPosition = Vector2.zero;
            }

            _deckListScrollRect.verticalNormalizedPosition = 1f;
            RefreshRuntimeDeckPanelScrollLayout(resetToTop: true);
        }

        private void PrepareDeckListTextLayout()
        {
            if (_deckListText == null)
            {
                return;
            }

            _deckListText.alignment = TextAnchor.UpperLeft;
            _deckListText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _deckListText.verticalOverflow = VerticalWrapMode.Overflow;

            var textRect = _deckListText.rectTransform;
            if (textRect == null)
            {
                return;
            }

            var targetHeight = Mathf.Max(_deckListText.preferredHeight + 16f, 64f);
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.offsetMin = new Vector2(12f, -targetHeight);
            textRect.offsetMax = new Vector2(-12f, 0f);
        }

        private void EnsureDeckListReferences()
        {
            if (!IsOverlayDeckListText(_deckListText) &&
                _deckListText != null &&
                _deckListText.GetComponentInParent<ScrollRect>() != null)
            {
                if (_deckListScrollRect == null)
                {
                    _deckListScrollRect = _deckListText.GetComponentInParent<ScrollRect>();
                }

                return;
            }

            var overlayTransform = _draftOverlayPresenter != null ? _draftOverlayPresenter.transform : null;
            var sceneTexts = UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None);
            for (var i = 0; i < sceneTexts.Length; i++)
            {
                var text = sceneTexts[i];
                if (text == null || !string.Equals(text.gameObject.name, "DeckListText", StringComparison.Ordinal))
                {
                    continue;
                }

                if (overlayTransform != null && text.transform.IsChildOf(overlayTransform))
                {
                    continue;
                }

                var parentScrollRect = text.GetComponentInParent<ScrollRect>();
                if (parentScrollRect == null)
                {
                    continue;
                }

                _deckListText = text;
                _deckListScrollRect = parentScrollRect;
                return;
            }

            if (_deckListScrollRect == null && _deckListText != null)
            {
                _deckListScrollRect = _deckListText.GetComponentInParent<ScrollRect>();
            }
        }

        private bool IsOverlayDeckListText(Text deckListText)
        {
            return deckListText != null &&
                   _draftOverlayPresenter != null &&
                   deckListText.transform.IsChildOf(_draftOverlayPresenter.transform);
        }

        private void EnsureRuntimeDeckListDisplayText()
        {
            if (_runtimeDeckListDisplayText != null)
            {
                return;
            }

            if (_deckListScrollRect == null || _deckListScrollRect.content == null)
            {
                return;
            }

            var displayObject = new GameObject("RuntimeDeckListDisplayText", typeof(RectTransform), typeof(Text));
            RegisterEditorCreatedObject(displayObject);
            var rectTransform = displayObject.GetComponent<RectTransform>();
            rectTransform.SetParent(_deckListScrollRect.content, false);
            rectTransform.SetAsLastSibling();

            var displayText = displayObject.GetComponent<Text>();
            if (_deckListText != null)
            {
                displayText.font = _deckListText.font;
                displayText.fontSize = _deckListText.fontSize;
                displayText.fontStyle = _deckListText.fontStyle;
                displayText.color = _deckListText.color;
            }
            else
            {
                displayText.font = ResolveRuntimeFont();
                displayText.fontSize = 22;
                displayText.fontStyle = FontStyle.Normal;
                displayText.color = Color.white;
            }

            displayText.alignment = TextAnchor.UpperLeft;
            displayText.horizontalOverflow = HorizontalWrapMode.Wrap;
            displayText.verticalOverflow = VerticalWrapMode.Overflow;
            displayText.supportRichText = false;
            displayText.raycastTarget = false;
            displayText.text = string.Empty;

            _runtimeDeckListDisplayText = displayText;

            if (_deckListText != null)
            {
                _deckListText.enabled = false;
            }
        }

        private void EnsureRuntimeDeckPanelText()
        {
            if (_runtimeDeckPanelText != null)
            {
                EnsureRuntimeDeckPanelScrollView();
                ConfigureRuntimeDeckPanelLayout();
                if (_deckListScrollRect != null)
                {
                    _deckListScrollRect.gameObject.SetActive(!_hideOriginalDeckListScrollRect);
                }

                return;
            }

            if (!_createRuntimeDeckPanel)
            {
                return;
            }

            if (_deckListScrollRect == null)
            {
                return;
            }

            var panelRect = _deckListScrollRect.transform.parent as RectTransform;
            if (panelRect == null)
            {
                return;
            }

            var surfaceObject = new GameObject("RuntimeDeckPanelSurface", typeof(RectTransform), typeof(Image), typeof(Outline));
            RegisterEditorCreatedObject(surfaceObject);
            var surfaceRect = surfaceObject.GetComponent<RectTransform>();
            surfaceRect.SetParent(panelRect, false);
            surfaceRect.SetAsLastSibling();
            surfaceRect.anchorMin = new Vector2(0f, 0f);
            surfaceRect.anchorMax = new Vector2(1f, 1f);
            surfaceRect.pivot = new Vector2(0.5f, 0.5f);
            surfaceRect.offsetMin = _deckPanelOffsetMin;
            surfaceRect.offsetMax = _deckPanelOffsetMax;

            var surfaceImage = surfaceObject.GetComponent<Image>();
            surfaceImage.color = _deckPanelColor;
            surfaceImage.raycastTarget = false;

            var surfaceOutline = surfaceObject.GetComponent<Outline>();
            surfaceOutline.effectColor = _deckPanelOutlineColor;
            surfaceOutline.effectDistance = _deckPanelOutlineDistance;
            surfaceOutline.useGraphicAlpha = true;

            var summaryBarObject = new GameObject("RuntimeDeckSummaryBar", typeof(RectTransform), typeof(Image));
            RegisterEditorCreatedObject(summaryBarObject);
            var summaryBarRect = summaryBarObject.GetComponent<RectTransform>();
            summaryBarRect.SetParent(surfaceRect, false);
            summaryBarRect.anchorMin = new Vector2(0f, 1f);
            summaryBarRect.anchorMax = new Vector2(1f, 1f);
            summaryBarRect.pivot = new Vector2(0.5f, 1f);
            summaryBarRect.offsetMin = _deckSummaryBarOffsetMin;
            summaryBarRect.offsetMax = _deckSummaryBarOffsetMax;

            var summaryBarImage = summaryBarObject.GetComponent<Image>();
            summaryBarImage.color = _deckSummaryBarColor;
            summaryBarImage.raycastTarget = false;

            var summaryTextObject = new GameObject("RuntimeDeckSummaryText", typeof(RectTransform), typeof(Text));
            RegisterEditorCreatedObject(summaryTextObject);
            var summaryTextRect = summaryTextObject.GetComponent<RectTransform>();
            summaryTextRect.SetParent(summaryBarRect, false);
            summaryTextRect.anchorMin = Vector2.zero;
            summaryTextRect.anchorMax = Vector2.one;
            summaryTextRect.offsetMin = _deckSummaryTextOffsetMin;
            summaryTextRect.offsetMax = _deckSummaryTextOffsetMax;

            var summaryText = summaryTextObject.GetComponent<Text>();
            summaryText.raycastTarget = false;
            summaryText.alignment = TextAnchor.MiddleCenter;
            summaryText.horizontalOverflow = HorizontalWrapMode.Overflow;
            summaryText.verticalOverflow = VerticalWrapMode.Overflow;
            summaryText.supportRichText = false;

            var panelObject = new GameObject("RuntimeCurrentDraftDeckText", typeof(RectTransform), typeof(Text));
            RegisterEditorCreatedObject(panelObject);
            var rectTransform = panelObject.GetComponent<RectTransform>();
            rectTransform.SetParent(surfaceRect, false);
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = _deckPanelTextOffsetMin;
            rectTransform.offsetMax = _deckPanelTextOffsetMax;

            var panelText = panelObject.GetComponent<Text>();
            if (_deckListText != null)
            {
                summaryText.font = _deckListText.font;
                summaryText.fontSize = Mathf.Max(16, _deckListText.fontSize - 4);
                summaryText.fontStyle = FontStyle.Bold;
                summaryText.color = _deckSummaryTextColor;

                panelText.font = _deckListText.font;
                panelText.fontSize = Mathf.Max(18, _deckListText.fontSize - 1);
                panelText.fontStyle = _deckListText.fontStyle;
                panelText.color = _deckPanelTextColor;
            }
            else
            {
                summaryText.font = ResolveRuntimeFont();
                summaryText.fontSize = 18;
                summaryText.fontStyle = FontStyle.Bold;
                summaryText.color = _deckSummaryTextColor;

                panelText.font = ResolveRuntimeFont();
                panelText.fontSize = 20;
                panelText.fontStyle = FontStyle.Normal;
                panelText.color = _deckPanelTextColor;
            }

            panelText.alignment = TextAnchor.UpperLeft;
            panelText.horizontalOverflow = HorizontalWrapMode.Wrap;
            panelText.verticalOverflow = VerticalWrapMode.Overflow;
            panelText.supportRichText = false;
            panelText.raycastTarget = false;
            panelText.text = string.Empty;

            _runtimeDeckPanelSurface = surfaceRect;
            _runtimeDeckPanelText = panelText;
            _runtimeDeckSummaryText = summaryText;
            EnsureRuntimeDeckPanelScrollView();
            ConfigureRuntimeDeckPanelLayout();
            _deckListScrollRect.gameObject.SetActive(!_hideOriginalDeckListScrollRect);
        }

        private void EnsureRuntimeDeckPanelScrollView()
        {
            if (_runtimeDeckPanelSurface == null || _runtimeDeckPanelText == null)
            {
                return;
            }

            if (_runtimeDeckPanelScrollRect == null)
            {
                var existingScrollRect = _runtimeDeckPanelText.GetComponentInParent<ScrollRect>();
                if (existingScrollRect != null &&
                    existingScrollRect.transform.IsChildOf(_runtimeDeckPanelSurface))
                {
                    _runtimeDeckPanelScrollRect = existingScrollRect;
                }
            }

            if (_runtimeDeckPanelScrollRect == null)
            {
                var scrollObject = new GameObject(
                    "RuntimeDeckListScrollView",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(RectMask2D),
                    typeof(ScrollRect));
                RegisterEditorCreatedObject(scrollObject);
                var scrollRectTransform = scrollObject.GetComponent<RectTransform>();
                scrollRectTransform.SetParent(_runtimeDeckPanelSurface, false);
                scrollRectTransform.SetAsFirstSibling();
                scrollRectTransform.anchorMin = Vector2.zero;
                scrollRectTransform.anchorMax = Vector2.one;
                scrollRectTransform.pivot = new Vector2(0.5f, 0.5f);
                scrollRectTransform.offsetMin = _deckPanelTextOffsetMin;
                scrollRectTransform.offsetMax = _deckPanelTextOffsetMax;

                var inputImage = scrollObject.GetComponent<Image>();
                inputImage.color = Color.clear;
                inputImage.raycastTarget = true;

                var contentObject = new GameObject("Content", typeof(RectTransform));
                RegisterEditorCreatedObject(contentObject);
                _runtimeDeckPanelScrollContent = contentObject.GetComponent<RectTransform>();
                _runtimeDeckPanelScrollContent.SetParent(scrollRectTransform, false);
                ConfigureRuntimeDeckPanelTopAnchoredRect(_runtimeDeckPanelScrollContent, 1f);

                _runtimeDeckPanelText.rectTransform.SetParent(_runtimeDeckPanelScrollContent, false);
                ConfigureRuntimeDeckPanelTopAnchoredRect(_runtimeDeckPanelText.rectTransform, 1f);
                _runtimeDeckPanelText.raycastTarget = false;

                _runtimeDeckPanelScrollRect = scrollObject.GetComponent<ScrollRect>();
            }

            if (_runtimeDeckPanelScrollContent == null)
            {
                _runtimeDeckPanelScrollContent = _runtimeDeckPanelScrollRect.content != null
                    ? _runtimeDeckPanelScrollRect.content
                    : _runtimeDeckPanelText.transform.parent as RectTransform;
            }

            if (_runtimeDeckPanelScrollContent == null)
            {
                return;
            }

            if (_runtimeDeckPanelText.transform.parent != _runtimeDeckPanelScrollContent)
            {
                _runtimeDeckPanelText.rectTransform.SetParent(_runtimeDeckPanelScrollContent, false);
            }

            var viewport = _runtimeDeckPanelScrollRect.transform as RectTransform;
            _runtimeDeckPanelScrollRect.viewport = viewport;
            _runtimeDeckPanelScrollRect.content = _runtimeDeckPanelScrollContent;
            _runtimeDeckPanelScrollRect.horizontal = false;
            _runtimeDeckPanelScrollRect.vertical = true;
            _runtimeDeckPanelScrollRect.movementType = ScrollRect.MovementType.Clamped;
            _runtimeDeckPanelScrollRect.inertia = true;
            _runtimeDeckPanelScrollRect.decelerationRate = 0.135f;
            _runtimeDeckPanelScrollRect.scrollSensitivity = 30f;

            if (_runtimeDeckPanelSurface.Find("RuntimeDeckSummaryBar") is RectTransform summaryBarRect)
            {
                summaryBarRect.SetAsLastSibling();
            }
        }

        private void RefreshRuntimeDeckPanelScrollLayout(bool resetToTop)
        {
            if (_runtimeDeckPanelScrollRect == null ||
                _runtimeDeckPanelScrollContent == null ||
                _runtimeDeckPanelText == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            var viewport = _runtimeDeckPanelScrollRect.viewport != null
                ? _runtimeDeckPanelScrollRect.viewport
                : _runtimeDeckPanelScrollRect.transform as RectTransform;
            if (viewport == null)
            {
                return;
            }

            var previousPosition = _runtimeDeckPanelScrollRect.verticalNormalizedPosition;
            var viewportHeight = Mathf.Max(1f, viewport.rect.height);
            ConfigureRuntimeDeckPanelTopAnchoredRect(_runtimeDeckPanelScrollContent, viewportHeight);
            ConfigureRuntimeDeckPanelTopAnchoredRect(_runtimeDeckPanelText.rectTransform, viewportHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_runtimeDeckPanelText.rectTransform);

            var contentHeight = Mathf.Max(viewportHeight, _runtimeDeckPanelText.preferredHeight + 4f);
            ConfigureRuntimeDeckPanelTopAnchoredRect(_runtimeDeckPanelScrollContent, contentHeight);
            ConfigureRuntimeDeckPanelTopAnchoredRect(_runtimeDeckPanelText.rectTransform, contentHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_runtimeDeckPanelScrollContent);
            Canvas.ForceUpdateCanvases();

            _runtimeDeckPanelScrollRect.verticalNormalizedPosition = resetToTop
                ? 1f
                : Mathf.Clamp01(previousPosition);
        }

        private static void ConfigureRuntimeDeckPanelTopAnchoredRect(RectTransform rectTransform, float height)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.offsetMin = new Vector2(0f, -Mathf.Max(1f, height));
            rectTransform.offsetMax = Vector2.zero;
        }

        private void ConfigureRuntimeDeckPanelLayout()
        {
            if (!_applyRuntimeDraftUiLayout)
            {
                return;
            }

            if (_runtimeDeckPanelSurface != null)
            {
                _runtimeDeckPanelSurface.offsetMin = _deckPanelOffsetMin;
                _runtimeDeckPanelSurface.offsetMax = _deckPanelOffsetMax;

                if (_runtimeDeckPanelSurface.TryGetComponent<Image>(out var surfaceImage))
                {
                    surfaceImage.color = _deckPanelColor;
                }

                if (_runtimeDeckPanelSurface.TryGetComponent<Outline>(out var surfaceOutline))
                {
                    surfaceOutline.effectColor = _deckPanelOutlineColor;
                    surfaceOutline.effectDistance = _deckPanelOutlineDistance;
                }

                if (_runtimeDeckPanelSurface.Find("RuntimeDeckSummaryBar") is RectTransform summaryBarRect)
                {
                    summaryBarRect.offsetMin = _deckSummaryBarOffsetMin;
                    summaryBarRect.offsetMax = _deckSummaryBarOffsetMax;

                    if (summaryBarRect.TryGetComponent<Image>(out var summaryBarImage))
                    {
                        summaryBarImage.color = _deckSummaryBarColor;
                    }
                }
            }

            if (_runtimeDeckSummaryText != null)
            {
                _runtimeDeckSummaryText.color = _deckSummaryTextColor;
                if (_runtimeDeckSummaryText.rectTransform != null)
                {
                    _runtimeDeckSummaryText.rectTransform.offsetMin = _deckSummaryTextOffsetMin;
                    _runtimeDeckSummaryText.rectTransform.offsetMax = _deckSummaryTextOffsetMax;
                }
            }

            if (_runtimeDeckPanelText != null)
            {
                _runtimeDeckPanelText.color = _deckPanelTextColor;
                if (_runtimeDeckPanelScrollRect != null &&
                    _runtimeDeckPanelScrollRect.transform is RectTransform scrollRectTransform)
                {
                    scrollRectTransform.offsetMin = _deckPanelTextOffsetMin;
                    scrollRectTransform.offsetMax = _deckPanelTextOffsetMax;
                }
                else if (_runtimeDeckPanelText.rectTransform != null)
                {
                    _runtimeDeckPanelText.rectTransform.offsetMin = _deckPanelTextOffsetMin;
                    _runtimeDeckPanelText.rectTransform.offsetMax = _deckPanelTextOffsetMax;
                }
            }

            RefreshRuntimeDeckPanelScrollLayout(resetToTop: false);
        }


        private CardDefinitionAsset ResolveCardAsset(string cardId)
        {
            return TryGetCardDefinitionAsset(cardId, out var cardAsset)
                ? cardAsset
                : null;
        }

        private System.Random CreateDraftRandom()
        {
            return _useFixedDraftSeed
                ? new System.Random(_fixedDraftSeed)
                : new System.Random();
        }
    }
}
