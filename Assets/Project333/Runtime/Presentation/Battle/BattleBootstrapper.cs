using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Cards;
using Project333.Runtime.Presentation.Draft;
using Project333.Runtime.Presentation.Online;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class BattleBootstrapper : MonoBehaviour, IDraftOverlayHost
    {
        [System.Serializable]
        public sealed class CombatLogChangedEvent : UnityEvent<string>
        {
        }

        private const int MaxCombatLogEntries = 33;
        private const string BusyMessage = "Battle is still resolving animations.";
        private const string MasterCardAssetResourcePath = "Project333/SpecialCards/MasterCard";
        private const string FireboltSpellEffectId = "firebolt";
        private const string FireboltSpellEffectResourcePath = "Project333/SpellEffects/Firebolt-Sheet";
        private const int FireboltSpellEffectFrameCount = 7;
        private const float FireboltSpellEffectFramesPerSecond = 14f;
        private const float OnlineErrorToastFadeInSeconds = 0.08f;
        private const float OnlineErrorToastFadeOutSeconds = 0.25f;
        private const float OnlineErrorToastVisibleSeconds = 2.2f;
        private const float ServerRunResultSyncTimeoutSeconds = 4f;
        private const int DefaultOpponentPlayedCardSortingOrder = 1000;
        private const int LegacyOpponentPlayedCardSortingOrder = 20005;
        private static readonly Vector2 FireboltSpellEffectSize = new Vector2(220f, 220f);
        private static readonly string[] KoreanFontCandidates =
        {
            "Malgun Gothic",
            "맑은 고딕",
            "Arial Unicode MS",
            "Noto Sans CJK KR",
            "Noto Sans KR"
        };
        private static readonly string OnlineClientTokenSuffix = Guid.NewGuid().ToString("N").Substring(0, 8);

        private static IReadOnlyList<Sprite> s_fireboltSpellEffectFrames;

        [SerializeField] private BattleScreenPresenter _battleScreenPresenter;
        [SerializeField] private CardDefinitionCatalogAsset _cardCatalogAsset;
        [SerializeField] private bool _useJsonCardDefinitions = true;
        [SerializeField] private DeckDefinitionAsset _playerDeckAsset;
        [SerializeField] private DeckDefinitionAsset _aiDeckAsset;
        [SerializeField] private string[] _playerDeckCardIds = Array.Empty<string>();
        [SerializeField] private string[] _aiDeckCardIds = Array.Empty<string>();
        [SerializeField] private bool _useGeneratedDebugDecksWhenEmpty = true;
        [Min(BattleDebugDeckFactory.MinimumDeckSize)]
        [SerializeField] private int _generatedDeckSize = BattleDebugDeckFactory.MinimumDeckSize;
        [SerializeField] private bool _startOnAwake = true;
        [SerializeField] private bool _autoPassPlayerMulligan;
        [SerializeField] private bool _autoResolveTurnStartAfterAutoPass = true;
        [SerializeField] private bool _autoRunAiTurns = true;
        [SerializeField] private bool _autoResolvePlayerTurnStartAfterAi = true;
        [Header("Draft")]
        [SerializeField] private bool _startWithDraftBeforeBattle;
        [SerializeField] private DraftOverlayPresenter _draftOverlayPresenter;
        [SerializeField] private bool _useFixedDraftSeed;
        [SerializeField] private int _fixedDraftSeed = 333;
        [SerializeField] private bool _autoStartBattleAfterDraft;
        [Header("Scene Flow")]
        [SerializeField] private bool _returnToDraftSceneWhenRunSessionActive = true;
        [SerializeField] private float _returnToDraftSceneDelaySeconds = 1.5f;
        [Header("Online")]
        [SerializeField] private bool _sendPlayerActionsToOnlineGateway;
        [SerializeField] private OnlineBattleConnectionTester _onlineConnectionTester;
        [SerializeField] private string _onlineServerUrl = "ws://127.0.0.1:7333/battle";
        [SerializeField] private string _onlineMatchIdPrefix = "draft-run";
        [SerializeField] private string _onlinePlayerToken = "player-a";
        [SerializeField] private bool _animateOnlineMoveEvents;
        [SerializeField] private string _onlineErrorToastPrefix = "명령 실패";
        [SerializeField] private string _onlineStatusToastPrefix = "온라인";
        [Header("Online Error Toast")]
        [SerializeField] private Font _onlineErrorToastFont;
        [SerializeField] private Vector2 _onlineErrorToastAnchor = new Vector2(0.5f, 0.86f);
        [SerializeField] private Vector2 _onlineErrorToastOffset = Vector2.zero;
        [SerializeField] private Vector2 _onlineErrorToastSize = new Vector2(900f, 72f);
        [SerializeField] private Vector2 _onlineErrorToastTextPadding = new Vector2(28f, 8f);
        [SerializeField] private float _onlineErrorToastFontSize = 26f;
        [SerializeField] private Color _onlineErrorToastPanelColor = new Color(0.36f, 0.04f, 0.035f, 0.94f);
        [SerializeField] private Color _onlineStatusToastPanelColor = new Color(0.05f, 0.15f, 0.30f, 0.94f);
        [SerializeField] private Color _onlineSuccessToastPanelColor = new Color(0.04f, 0.26f, 0.14f, 0.94f);
        [SerializeField] private Color _onlineErrorToastTextColor = new Color(1f, 0.92f, 0.86f, 1f);
        [Header("Online Turn Timer")]
        [SerializeField] private Vector2 _onlineTurnTimerAnchor = new Vector2(0.5f, 0.965f);
        [SerializeField] private Vector2 _onlineTurnTimerOffset = new Vector2(0f, -20f);
        [SerializeField] private Vector2 _onlineTurnTimerSize = new Vector2(360f, 54f);
        [SerializeField] private Vector2 _onlineTurnTimerTextPadding = new Vector2(20f, 6f);
        [SerializeField] private float _onlineTurnTimerFontSize = 28f;
        [SerializeField] private int _onlineTurnTimerRevealThresholdSeconds = 20;
        [SerializeField] private int _onlineTurnTimerDangerThresholdSeconds = 10;
        [SerializeField] private Color _onlineTurnTimerPlayerPanelColor = new Color(0.05f, 0.22f, 0.13f, 0.92f);
        [SerializeField] private Color _onlineTurnTimerOpponentPanelColor = new Color(0.08f, 0.10f, 0.18f, 0.90f);
        [SerializeField] private Color _onlineTurnTimerDangerPanelColor = new Color(0.42f, 0.05f, 0.035f, 0.95f);
        [SerializeField] private Color _onlineTurnTimerTextColor = new Color(1f, 0.96f, 0.84f, 1f);
        [SerializeField] private Color _onlineTurnTimerDangerTextColor = new Color(1f, 0.78f, 0.62f, 1f);
        [Header("Opponent Played Card Reveal")]
        [SerializeField] private Vector2 _opponentPlayedCardRevealSize = new Vector2(320f, 440f);
        [SerializeField] [Min(0.1f)] private float _opponentPlayedCardRevealVisualScale = 1.2f;
        [SerializeField] [Min(0f)] private float _opponentPlayedCardNormalDisplaySeconds = 3f;
        [SerializeField] [Min(0f)] private float _opponentPlayedCardQueuedDisplaySeconds = 2f;
        [SerializeField] [Min(0f)] private float _opponentPlayedCardFadeInSeconds = 0.15f;
        [SerializeField] [Min(0f)] private float _opponentPlayedCardFadeOutSeconds = 0.15f;
        [SerializeField] private int _opponentPlayedCardSortingOrder = DefaultOpponentPlayedCardSortingOrder;
        [SerializeField] private bool _showOpponentPlayedCardStats = true;
        [SerializeField] private Vector2 _opponentPlayedCardAttackStatNormalizedPosition = new Vector2(0.07f, 0.09f);
        [SerializeField] private Vector2 _opponentPlayedCardHpStatNormalizedPosition = new Vector2(0.92f, 0.09f);
        [SerializeField] private Vector2 _opponentPlayedCardStatTextSize = new Vector2(72f, 54f);
        [SerializeField] private int _opponentPlayedCardStatFontSize = 34;
        [SerializeField] private Color _opponentPlayedCardStatTextColor = Color.white;
        [SerializeField] private Color _opponentPlayedCardStatOutlineColor = new Color(0f, 0f, 0f, 0.95f);
        [SerializeField] private Vector2 _opponentPlayedCardStatOutlineDistance = new Vector2(2f, -2f);
        [Header("PvP Matchmaking Overlay")]
        [SerializeField] private Vector2 _pvpMatchmakingPanelSize = new Vector2(680f, 300f);
        [SerializeField] private Vector2 _pvpMatchmakingPanelOffset = Vector2.zero;
        [SerializeField] private Vector2 _pvpMatchmakingTextPadding = new Vector2(42f, 72f);
        [SerializeField] private Vector2 _pvpMatchmakingCancelButtonSize = new Vector2(220f, 64f);
        [SerializeField] private Vector2 _pvpMatchmakingCancelButtonOffset = new Vector2(0f, 42f);
        [SerializeField] private float _pvpMatchmakingTextFontSize = 30f;
        [SerializeField] private float _pvpMatchmakingCancelFontSize = 24f;
        [SerializeField] private Color _pvpMatchmakingDimColor = new Color(0.01f, 0.015f, 0.025f, 0.72f);
        [SerializeField] private Color _pvpMatchmakingPanelColor = new Color(0.045f, 0.075f, 0.12f, 0.96f);
        [SerializeField] private Color _pvpMatchmakingPanelOutlineColor = new Color(0.65f, 0.78f, 1f, 0.52f);
        [SerializeField] private Color _pvpMatchmakingTextColor = new Color(0.94f, 0.98f, 1f, 1f);
        [SerializeField] private Color _pvpMatchmakingCancelButtonColor = new Color(0.22f, 0.23f, 0.28f, 1f);
        [SerializeField] private string _pvpMatchmakingWaitingText = "상대를 찾는 중입니다...";
        [SerializeField] private string _pvpMatchmakingFoundText = "상대를 찾았습니다!";
        [SerializeField] private string _pvpMatchmakingConnectionFailedText = "서버 연결에 실패했습니다. 나중에 다시 시도해주세요.";
        [SerializeField] private string _pvpMatchmakingCancelButtonLabel = "취소";
        [SerializeField] private string _pvpMatchmakingReturnButtonLabel = "돌아가기";
        [Header("Attack Animation")]
        [SerializeField] private bool _animateAttackSequences = true;
        [SerializeField] private float _attackRunDuration = 0.18f;
        [SerializeField] private float _attackImpactDelay = 0.04f;
        [SerializeField] private float _attackReactionDuration = 0.10f;
        [SerializeField] private float _attackReturnDuration = 0.18f;
        [SerializeField] private float _deathHoldDuration = 0.45f;
        [SerializeField] private float _valuePopupCascadeDelay = 0.18f;
        [SerializeField] private float _meleeAttackContactOffset = 36f;
        [SerializeField] private float _meleeAttackSideOffset = 24f;
        [SerializeField] private float _rangedAttackNudgeDistance = 28f;
        [SerializeField] private float _rangedAttackNudgeDuration = 0.08f;
        [SerializeField] private CombatLogChangedEvent _onCombatLogChanged = new CombatLogChangedEvent();
        [TextArea(6, 16)]
        [SerializeField] private string _combatLogText = "전투 기록이 없습니다.";
        [TextArea(2, 6)]
        [SerializeField] private string _lastInteractionStatus = "No interaction yet.";

        private BattleFlowController _battleFlowController;
        private IBattleGateway _battleGateway;
        private ICardDefinitionProvider _cardDefinitionProvider;
        private AiDecisionService _aiDecisionService;
        private AiTurnRunner _aiTurnRunner;
        private DraftSessionService _draftSessionService;
        private Coroutine _presentationSequenceCoroutine;
        private Coroutine _onlineErrorToastCoroutine;
        private Coroutine _onlineReconnectCountdownCoroutine;
        private Coroutine _onlineTurnTimerCoroutine;
        private bool _hasOpponentReconnectCountdown;
        private bool _isLocalConnectionRecoveryStatusVisible;
        private float _opponentReconnectCountdownEndTime;
        [SerializeField, HideInInspector] private CanvasGroup _onlineErrorToastCanvasGroup;
        [SerializeField, HideInInspector] private RectTransform _onlineErrorToastPanelRect;
        [SerializeField, HideInInspector] private RectTransform _onlineErrorToastTextRect;
        [SerializeField, HideInInspector] private Image _onlineErrorToastPanelImage;
        [SerializeField, HideInInspector] private Text _onlineErrorToastText;
        [SerializeField, HideInInspector] private bool _onlineErrorToastUsesSceneLayout;
        [SerializeField, HideInInspector] private CanvasGroup _onlineTurnTimerCanvasGroup;
        [SerializeField, HideInInspector] private RectTransform _onlineTurnTimerPanelRect;
        [SerializeField, HideInInspector] private RectTransform _onlineTurnTimerTextRect;
        [SerializeField, HideInInspector] private Image _onlineTurnTimerPanelImage;
        [SerializeField, HideInInspector] private Text _onlineTurnTimerText;
        [SerializeField, HideInInspector] private bool _onlineTurnTimerUsesSceneLayout;
        [SerializeField, HideInInspector] private CanvasGroup _opponentPlayedCardRevealCanvasGroup;
        [SerializeField, HideInInspector] private RectTransform _opponentPlayedCardRevealRect;
        [SerializeField, HideInInspector] private Image _opponentPlayedCardRevealImage;
        [SerializeField, HideInInspector] private Text _opponentPlayedCardFallbackText;
        [SerializeField, HideInInspector] private Text _opponentPlayedCardAttackText;
        [SerializeField, HideInInspector] private Text _opponentPlayedCardHpText;
        [SerializeField, HideInInspector] private bool _opponentPlayedCardRevealUsesSceneLayout;
        [SerializeField, HideInInspector] private CanvasGroup _pvpMatchmakingCanvasGroup;
        [SerializeField, HideInInspector] private RectTransform _pvpMatchmakingDimRect;
        [SerializeField, HideInInspector] private Image _pvpMatchmakingDimImage;
        [SerializeField, HideInInspector] private RectTransform _pvpMatchmakingPanelRect;
        [SerializeField, HideInInspector] private Image _pvpMatchmakingPanelImage;
        [SerializeField, HideInInspector] private Outline _pvpMatchmakingPanelOutline;
        [SerializeField, HideInInspector] private RectTransform _pvpMatchmakingTextRect;
        [SerializeField, HideInInspector] private Text _pvpMatchmakingText;
        [SerializeField, HideInInspector] private RectTransform _pvpMatchmakingCancelButtonRect;
        [SerializeField, HideInInspector] private Image _pvpMatchmakingCancelButtonImage;
        [SerializeField, HideInInspector] private Button _pvpMatchmakingCancelButton;
        [SerializeField, HideInInspector] private Text _pvpMatchmakingCancelButtonText;
        [SerializeField, HideInInspector] private bool _pvpMatchmakingUsesSceneLayout;
#if UNITY_EDITOR
        private bool _hasQueuedEditorBattleUiMaterialization;
#endif
        private static Font s_runtimeKoreanToastFont;
        private readonly RandomFirstPlayerSelector _firstPlayerSelector = new RandomFirstPlayerSelector();
        private readonly List<string> _combatLogEntries = new List<string>();
        private readonly Queue<OpponentPlayedCardRevealRequest> _opponentPlayedCardRevealQueue =
            new Queue<OpponentPlayedCardRevealRequest>();
        private Coroutine _opponentPlayedCardRevealCoroutine;
        private IReadOnlyList<string> _runtimePlayerDeckCardIdsOverride = Array.Empty<string>();
        private IReadOnlyList<string> _runtimeAIDeckCardIdsOverride = Array.Empty<string>();
        private bool _isBusy;
        private bool _hasQueuedDraftSceneReturn;
        private bool _isPvpMatchmakingOverlayVisible;
        private bool _isCancellingPvpMatchmaking;

        public BattleState CurrentBattleState => GetCurrentBattleState();

        public string CombatLogText => _combatLogText;

        public string LastInteractionStatus => _lastInteractionStatus;

        public bool IsBusy => _isBusy;

        public bool IsPvpMatchmakingOverlayVisible => _isPvpMatchmakingOverlayVisible;

        public bool CanPlayerSurrender
        {
            get
            {
                var battleState = CurrentBattleState;
                if (battleState == null || battleState.IsEnded)
                {
                    return false;
                }

                if (!_sendPlayerActionsToOnlineGateway)
                {
                    return true;
                }

                return _onlineConnectionTester != null &&
                       _onlineConnectionTester.HasActiveAuthoritativeBattle;
            }
        }

        public bool CanSendPlayerSurrender =>
            CanPlayerSurrender &&
            (!_sendPlayerActionsToOnlineGateway ||
             (_onlineConnectionTester != null && _onlineConnectionTester.IsReadyForCommands));

        public bool IsCancellingPvpMatchmaking => _isCancellingPvpMatchmaking;

        public bool IsMulliganPresentationActive =>
            _battleScreenPresenter != null && _battleScreenPresenter.IsMulliganPresentationActive;

        public void ShowOnlineErrorToast(string message)
        {
            ShowOnlineToast(message, "온라인 오류", _onlineErrorToastPanelColor);
        }

        public void ShowOnlineStatusToast(string message)
        {
            ShowOnlineToast(message, _onlineStatusToastPrefix, _onlineStatusToastPanelColor);
        }

        public void ShowLocalConnectionRecoveryStatus(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            StopOnlineReconnectCountdown();
            EnsureOnlineErrorToast();
            if (_onlineErrorToastCanvasGroup == null || _onlineErrorToastText == null)
            {
                return;
            }

            if (_onlineErrorToastCoroutine != null)
            {
                StopCoroutine(_onlineErrorToastCoroutine);
                _onlineErrorToastCoroutine = null;
            }

            _isLocalConnectionRecoveryStatusVisible = true;
            _lastInteractionStatus = message;
            ApplyOnlineErrorToastSettings();
            if (_onlineErrorToastPanelImage != null)
            {
                _onlineErrorToastPanelImage.color = _onlineStatusToastPanelColor;
            }

            _onlineErrorToastText.text = message;
            _onlineErrorToastCanvasGroup.gameObject.SetActive(true);
            _onlineErrorToastCanvasGroup.alpha = 1f;
            _onlineErrorToastCanvasGroup.interactable = false;
            _onlineErrorToastCanvasGroup.blocksRaycasts = false;
        }

        public void HideLocalConnectionRecoveryStatus()
        {
            if (!_isLocalConnectionRecoveryStatusVisible)
            {
                return;
            }

            _isLocalConnectionRecoveryStatusVisible = false;
            if (TryResumeOpponentReconnectCountdown())
            {
                return;
            }

            if (_onlineErrorToastCanvasGroup != null)
            {
                _onlineErrorToastCanvasGroup.alpha = 0f;
                _onlineErrorToastCanvasGroup.interactable = false;
                _onlineErrorToastCanvasGroup.blocksRaycasts = false;
                _onlineErrorToastCanvasGroup.gameObject.SetActive(false);
            }
        }

        public void ShowOnlineSuccessToast(string message)
        {
            ShowOnlineToast(message, _onlineStatusToastPrefix, _onlineSuccessToastPanelColor);
        }

        public void ShowBattleResultToast(string message)
        {
            ShowOnlineToast(message, string.Empty, _onlineSuccessToastPanelColor);
        }

        public void ShowCommandErrorToast(string message)
        {
            ShowOnlineToast(message, _onlineErrorToastPrefix, _onlineErrorToastPanelColor);
        }

        public void ShowOpponentReconnectCountdown(int durationSeconds)
        {
            _isLocalConnectionRecoveryStatusVisible = false;
            var safeDurationSeconds = Mathf.Max(0, durationSeconds);
            _hasOpponentReconnectCountdown = true;
            _opponentReconnectCountdownEndTime = Time.unscaledTime + safeDurationSeconds;
            StopOnlineReconnectCountdown();
            EnsureOnlineErrorToast();
            if (_onlineErrorToastCanvasGroup == null || _onlineErrorToastText == null)
            {
                return;
            }

            _onlineErrorToastCanvasGroup.gameObject.SetActive(true);
            if (_onlineErrorToastCoroutine != null)
            {
                StopCoroutine(_onlineErrorToastCoroutine);
                _onlineErrorToastCoroutine = null;
            }

            ApplyOnlineErrorToastSettings();
            if (_onlineErrorToastPanelImage != null)
            {
                _onlineErrorToastPanelImage.color = _onlineStatusToastPanelColor;
            }

            _onlineErrorToastCanvasGroup.alpha = 1f;
            _onlineErrorToastCanvasGroup.blocksRaycasts = false;
            _onlineReconnectCountdownCoroutine = StartCoroutine(OpponentReconnectCountdownSequence());
        }

        public void HideOpponentReconnectCountdown()
        {
            _hasOpponentReconnectCountdown = false;
            _opponentReconnectCountdownEndTime = 0f;
            StopOnlineReconnectCountdown();
            if (_onlineErrorToastCanvasGroup != null)
            {
                _onlineErrorToastCanvasGroup.alpha = 0f;
                _onlineErrorToastCanvasGroup.blocksRaycasts = false;
            }
        }

        public void ShowOnlineTurnTimer(bool isLocalPlayerTurn, float remainingSeconds, int durationSeconds)
        {
            ShowOnlinePhaseTimer(
                isMulligan: false,
                localMulliganConfirmed: false,
                isLocalPlayerTurn: isLocalPlayerTurn,
                remainingSeconds: remainingSeconds,
                durationSeconds: durationSeconds);
        }

        public void ShowOnlineMulliganTimer(bool localMulliganConfirmed, float remainingSeconds, int durationSeconds)
        {
            ShowOnlinePhaseTimer(
                isMulligan: true,
                localMulliganConfirmed: localMulliganConfirmed,
                isLocalPlayerTurn: true,
                remainingSeconds: remainingSeconds,
                durationSeconds: durationSeconds);
        }

        private void ShowOnlinePhaseTimer(
            bool isMulligan,
            bool localMulliganConfirmed,
            bool isLocalPlayerTurn,
            float remainingSeconds,
            int durationSeconds)
        {
            var safeRemainingSeconds = Mathf.Max(0f, remainingSeconds);
            var safeDurationSeconds = Mathf.Max(1, durationSeconds);
            StopOnlineTurnTimer();
            EnsureOnlineTurnTimer();
            if (_onlineTurnTimerCanvasGroup == null || _onlineTurnTimerText == null)
            {
                return;
            }

            _onlineTurnTimerCanvasGroup.gameObject.SetActive(true);
            _onlineTurnTimerCanvasGroup.alpha = 1f;
            _onlineTurnTimerCanvasGroup.blocksRaycasts = false;
            _onlineTurnTimerCoroutine = StartCoroutine(OnlineTurnTimerSequence(
                isMulligan,
                localMulliganConfirmed,
                isLocalPlayerTurn,
                safeRemainingSeconds,
                safeDurationSeconds));
        }

        public void HideOnlineTurnTimer()
        {
            StopOnlineTurnTimer();
            if (_onlineTurnTimerCanvasGroup != null)
            {
                _onlineTurnTimerCanvasGroup.alpha = 0f;
                _onlineTurnTimerCanvasGroup.blocksRaycasts = false;
                _onlineTurnTimerCanvasGroup.gameObject.SetActive(false);
            }
        }

        public void ShowPvpMatchmakingOverlay()
        {
            EnsurePvpMatchmakingOverlay();
            if (_pvpMatchmakingCanvasGroup == null || _pvpMatchmakingText == null)
            {
                return;
            }

            ApplyPvpMatchmakingOverlaySettings();
            _isPvpMatchmakingOverlayVisible = true;
            _isCancellingPvpMatchmaking = false;
            _pvpMatchmakingText.text = _pvpMatchmakingWaitingText;
            SetPvpMatchmakingCancelButtonLabel(_pvpMatchmakingCancelButtonLabel);
            if (_pvpMatchmakingCancelButton != null)
            {
                _pvpMatchmakingCancelButton.interactable = true;
            }

            _pvpMatchmakingCanvasGroup.gameObject.SetActive(true);
            _pvpMatchmakingCanvasGroup.alpha = 1f;
            _pvpMatchmakingCanvasGroup.interactable = true;
            _pvpMatchmakingCanvasGroup.blocksRaycasts = true;
        }

        public void HidePvpMatchmakingOverlay(bool showFoundMessage = false)
        {
            if (!_isPvpMatchmakingOverlayVisible && _pvpMatchmakingCanvasGroup == null)
            {
                return;
            }

            if (showFoundMessage)
            {
                ShowOnlineSuccessToast(_pvpMatchmakingFoundText);
            }

            _isPvpMatchmakingOverlayVisible = false;
            _isCancellingPvpMatchmaking = false;
            if (_pvpMatchmakingCanvasGroup == null)
            {
                return;
            }

            _pvpMatchmakingCanvasGroup.alpha = 0f;
            _pvpMatchmakingCanvasGroup.interactable = false;
            _pvpMatchmakingCanvasGroup.blocksRaycasts = false;
            _pvpMatchmakingCanvasGroup.gameObject.SetActive(false);
        }

        public void ShowPvpMatchmakingConnectionFailure(string message = null)
        {
            EnsurePvpMatchmakingOverlay();
            if (_pvpMatchmakingCanvasGroup == null || _pvpMatchmakingText == null)
            {
                return;
            }

            ApplyPvpMatchmakingOverlaySettings();
            _isPvpMatchmakingOverlayVisible = true;
            _isCancellingPvpMatchmaking = false;
            _pvpMatchmakingText.text = string.IsNullOrWhiteSpace(message)
                ? _pvpMatchmakingConnectionFailedText
                : message;
            SetPvpMatchmakingCancelButtonLabel(_pvpMatchmakingReturnButtonLabel);
            if (_pvpMatchmakingCancelButton != null)
            {
                _pvpMatchmakingCancelButton.interactable = true;
            }

            _pvpMatchmakingCanvasGroup.gameObject.SetActive(true);
            _pvpMatchmakingCanvasGroup.alpha = 1f;
            _pvpMatchmakingCanvasGroup.interactable = true;
            _pvpMatchmakingCanvasGroup.blocksRaycasts = true;
        }

        public void SetPvpMatchmakingOverlayMessage(string message)
        {
            if (!_isPvpMatchmakingOverlayVisible || _pvpMatchmakingText == null)
            {
                return;
            }

            _pvpMatchmakingText.text = string.IsNullOrWhiteSpace(message)
                ? _pvpMatchmakingWaitingText
                : message;
        }

        public async void CancelPvpMatchmakingFromUi()
        {
            if (!_isPvpMatchmakingOverlayVisible || _isCancellingPvpMatchmaking)
            {
                return;
            }

            _isCancellingPvpMatchmaking = true;
            _lastInteractionStatus = "PvP matchmaking cancelled.";
            SetPvpMatchmakingOverlayMessage("매칭을 취소하는 중입니다...");
            SetPvpMatchmakingCancelButtonLabel(_pvpMatchmakingCancelButtonLabel);
            if (_pvpMatchmakingCancelButton != null)
            {
                _pvpMatchmakingCancelButton.interactable = false;
            }

            if (_onlineConnectionTester != null)
            {
                await _onlineConnectionTester.DisconnectFromServerAsync();
            }

            HidePvpMatchmakingOverlay();
            DraftRunSessionState.RestoreCurrentDraftDeckFromCompletedDeckIfNeeded();

            if (!string.IsNullOrWhiteSpace(DraftRunSessionState.DraftSceneName))
            {
                SceneManager.LoadScene(DraftRunSessionState.DraftSceneName);
            }
        }

        private void SetPvpMatchmakingCancelButtonLabel(string label)
        {
            if (_pvpMatchmakingCancelButtonText != null)
            {
                _pvpMatchmakingCancelButtonText.text = string.IsNullOrWhiteSpace(label)
                    ? _pvpMatchmakingCancelButtonLabel
                    : label;
            }
        }

        private void ShowOnlineToast(string message, string prefix, Color panelColor)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var shouldResumeReconnectCountdown = HasActiveOpponentReconnectCountdown();
            _isLocalConnectionRecoveryStatusVisible = false;
            StopOnlineReconnectCountdown();
            var displayMessage = string.IsNullOrWhiteSpace(prefix)
                ? message
                : $"{prefix}: {message}";

            _lastInteractionStatus = displayMessage;
            EnsureOnlineErrorToast();
            if (_onlineErrorToastCanvasGroup == null || _onlineErrorToastText == null)
            {
                return;
            }

            ApplyOnlineErrorToastSettings();
            if (_onlineErrorToastPanelImage != null)
            {
                _onlineErrorToastPanelImage.color = panelColor;
            }

            _onlineErrorToastText.text = displayMessage;
            if (_onlineErrorToastCoroutine != null)
            {
                StopCoroutine(_onlineErrorToastCoroutine);
            }

            _onlineErrorToastCoroutine = StartCoroutine(OnlineErrorToastSequence(shouldResumeReconnectCountdown));
        }

        private void StopOnlineReconnectCountdown()
        {
            if (_onlineReconnectCountdownCoroutine == null)
            {
                return;
            }

            StopCoroutine(_onlineReconnectCountdownCoroutine);
            _onlineReconnectCountdownCoroutine = null;
        }

        private bool HasActiveOpponentReconnectCountdown()
        {
            return _hasOpponentReconnectCountdown &&
                   _opponentReconnectCountdownEndTime > Time.unscaledTime;
        }

        private bool TryResumeOpponentReconnectCountdown()
        {
            if (!HasActiveOpponentReconnectCountdown())
            {
                return false;
            }

            StopOnlineReconnectCountdown();
            EnsureOnlineErrorToast();
            if (_onlineErrorToastCanvasGroup == null || _onlineErrorToastText == null)
            {
                return false;
            }

            ApplyOnlineErrorToastSettings();
            if (_onlineErrorToastPanelImage != null)
            {
                _onlineErrorToastPanelImage.color = _onlineStatusToastPanelColor;
            }

            _onlineErrorToastCanvasGroup.gameObject.SetActive(true);
            _onlineErrorToastCanvasGroup.alpha = 1f;
            _onlineErrorToastCanvasGroup.blocksRaycasts = false;
            _onlineReconnectCountdownCoroutine = StartCoroutine(OpponentReconnectCountdownSequence());
            return true;
        }

        private IEnumerator OpponentReconnectCountdownSequence()
        {
            while (true)
            {
                var remainingSeconds = Mathf.Max(0, Mathf.CeilToInt(_opponentReconnectCountdownEndTime - Time.unscaledTime));
                var message = $"상대의 재접속을 기다리는 중...{remainingSeconds}";
                _lastInteractionStatus = message;
                if (_onlineErrorToastText != null)
                {
                    _onlineErrorToastText.text = message;
                }

                if (remainingSeconds <= 0)
                {
                    break;
                }

                yield return new WaitForSecondsRealtime(0.2f);
            }

            _onlineReconnectCountdownCoroutine = null;
            _hasOpponentReconnectCountdown = false;
        }

        private void StopOnlineTurnTimer()
        {
            if (_onlineTurnTimerCoroutine == null)
            {
                return;
            }

            StopCoroutine(_onlineTurnTimerCoroutine);
            _onlineTurnTimerCoroutine = null;
        }

        private IEnumerator OnlineTurnTimerSequence(
            bool isMulligan,
            bool localMulliganConfirmed,
            bool isLocalPlayerTurn,
            float remainingSeconds,
            int durationSeconds)
        {
            var endTime = Time.unscaledTime + Mathf.Max(0f, remainingSeconds);
            while (true)
            {
                var remainingWholeSeconds = Mathf.Max(0, Mathf.CeilToInt(endTime - Time.unscaledTime));
                ApplyOnlineTurnTimerValue(
                    isMulligan,
                    localMulliganConfirmed,
                    isLocalPlayerTurn,
                    remainingWholeSeconds,
                    durationSeconds);

                if (remainingWholeSeconds <= 0)
                {
                    break;
                }

                yield return new WaitForSecondsRealtime(0.2f);
            }

            _onlineTurnTimerCoroutine = null;
        }

        private void ApplyOnlineTurnTimerValue(
            bool isMulligan,
            bool localMulliganConfirmed,
            bool isLocalPlayerTurn,
            int remainingSeconds,
            int durationSeconds)
        {
            ApplyOnlineTurnTimerSettings();

            var shouldReveal = isMulligan ||
                               remainingSeconds <= Mathf.Max(0, _onlineTurnTimerRevealThresholdSeconds);
            if (_onlineTurnTimerCanvasGroup != null)
            {
                _onlineTurnTimerCanvasGroup.alpha = shouldReveal ? 1f : 0f;
                _onlineTurnTimerCanvasGroup.blocksRaycasts = false;
            }

            var isDanger = remainingSeconds <= Mathf.Max(0, _onlineTurnTimerDangerThresholdSeconds);
            if (_onlineTurnTimerPanelImage != null)
            {
                _onlineTurnTimerPanelImage.color = isDanger
                    ? _onlineTurnTimerDangerPanelColor
                    : isMulligan && localMulliganConfirmed
                        ? _onlineTurnTimerOpponentPanelColor
                        : isLocalPlayerTurn
                        ? _onlineTurnTimerPlayerPanelColor
                        : _onlineTurnTimerOpponentPanelColor;
            }

            if (_onlineTurnTimerText != null)
            {
                if (isMulligan)
                {
                    _onlineTurnTimerText.text = remainingSeconds <= 0
                        ? "멀리건 처리 중..."
                        : localMulliganConfirmed
                            ? $"상대 멀리건 대기 중... {remainingSeconds}"
                            : $"멀리건 {remainingSeconds}";
                }
                else
                {
                    var turnLabel = isLocalPlayerTurn ? "내 턴" : "상대 턴";
                    _onlineTurnTimerText.text = remainingSeconds <= 0
                        ? $"{turnLabel} 처리 중..."
                        : $"{turnLabel} {remainingSeconds}";
                }
                _onlineTurnTimerText.color = isDanger ? _onlineTurnTimerDangerTextColor : _onlineTurnTimerTextColor;
            }
        }

        public float PlayOnlineSpellCastAnimation(string cardId, PlayerId targetOwnerId, TileCoord targetCoord, int damageAmount)
        {
            var spellEffectId = ResolveDamageSpellEffectId(cardId);
            if (string.IsNullOrWhiteSpace(spellEffectId))
            {
                return 0f;
            }

            var targetView = FindTileTextView(targetOwnerId, targetCoord);
            if (targetView == null)
            {
                return 0f;
            }

            var effectFrames = GetSpellEffectFrames(spellEffectId);
            if (effectFrames == null || effectFrames.Count == 0)
            {
                return 0f;
            }

            var duration = targetView.PlayTransientSpriteEffect(
                effectFrames,
                GetSpellEffectFramesPerSecond(spellEffectId),
                GetSpellEffectSize(spellEffectId));

            if (damageAmount > 0)
            {
                targetView.QueueFloatingValuePopup(damageAmount, isHealing: false, delaySeconds: 0.08f);
            }

            return duration;
        }

        public void QueueOpponentPlayedCardReveals(IReadOnlyList<BattleEventDto> battleEvents)
        {
            if (battleEvents == null)
            {
                return;
            }

            for (var eventIndex = 0; eventIndex < battleEvents.Count; eventIndex++)
            {
                var battleEvent = battleEvents[eventIndex];
                if (!OpponentPlayedCardRevealRules.ShouldReveal(battleEvent))
                {
                    continue;
                }

                _opponentPlayedCardRevealQueue.Enqueue(new OpponentPlayedCardRevealRequest(
                    battleEvent.CardId,
                    battleEvent.CardAttack,
                    battleEvent.CardMaxHp));
            }

            if (_opponentPlayedCardRevealQueue.Count <= 0 || _opponentPlayedCardRevealCoroutine != null)
            {
                return;
            }

            EnsureOpponentPlayedCardReveal();
            if (_opponentPlayedCardRevealCanvasGroup == null)
            {
                _opponentPlayedCardRevealQueue.Clear();
                return;
            }

            _opponentPlayedCardRevealCoroutine = StartCoroutine(OpponentPlayedCardRevealSequence());
        }

        public void ClearOpponentPlayedCardReveals()
        {
            _opponentPlayedCardRevealQueue.Clear();
            if (_opponentPlayedCardRevealCoroutine != null)
            {
                StopCoroutine(_opponentPlayedCardRevealCoroutine);
                _opponentPlayedCardRevealCoroutine = null;
            }

            HideOpponentPlayedCardReveal();
        }

        private IEnumerator OpponentPlayedCardRevealSequence()
        {
            while (_opponentPlayedCardRevealQueue.Count > 0)
            {
                var request = _opponentPlayedCardRevealQueue.Dequeue();
                PresentOpponentPlayedCardReveal(request);

                var sequenceStartedAt = Time.unscaledTime;
                yield return FadeOpponentPlayedCardReveal(
                    0f,
                    1f,
                    Mathf.Max(0f, _opponentPlayedCardFadeInSeconds));

                while (true)
                {
                    RefreshOpponentPlayedCardStatLayout();
                    var displayDuration = OpponentPlayedCardRevealRules.ResolveDisplayDuration(
                        _opponentPlayedCardRevealQueue.Count > 0,
                        _opponentPlayedCardNormalDisplaySeconds,
                        _opponentPlayedCardQueuedDisplaySeconds);
                    var fadeOutStart = Mathf.Max(
                        _opponentPlayedCardFadeInSeconds,
                        displayDuration - _opponentPlayedCardFadeOutSeconds);
                    if (Time.unscaledTime - sequenceStartedAt >= fadeOutStart)
                    {
                        break;
                    }

                    yield return null;
                }

                yield return FadeOpponentPlayedCardReveal(
                    _opponentPlayedCardRevealCanvasGroup == null
                        ? 0f
                        : _opponentPlayedCardRevealCanvasGroup.alpha,
                    0f,
                    Mathf.Max(0f, _opponentPlayedCardFadeOutSeconds));
                HideOpponentPlayedCardReveal();
            }

            _opponentPlayedCardRevealCoroutine = null;
        }

        private IEnumerator FadeOpponentPlayedCardReveal(float fromAlpha, float toAlpha, float durationSeconds)
        {
            if (_opponentPlayedCardRevealCanvasGroup == null)
            {
                yield break;
            }

            if (durationSeconds <= 0f)
            {
                _opponentPlayedCardRevealCanvasGroup.alpha = toAlpha;
                yield break;
            }

            var startedAt = Time.unscaledTime;
            while (true)
            {
                RefreshOpponentPlayedCardStatLayout();
                var progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / durationSeconds);
                _opponentPlayedCardRevealCanvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, progress);
                if (progress >= 1f)
                {
                    yield break;
                }

                yield return null;
            }
        }

        private void PresentOpponentPlayedCardReveal(OpponentPlayedCardRevealRequest request)
        {
            EnsureOpponentPlayedCardReveal();
            if (_opponentPlayedCardRevealCanvasGroup == null || _opponentPlayedCardRevealImage == null)
            {
                return;
            }

            var hasArtwork = CardArtworkLibrary.TryGetArtwork(request.CardId, out var artwork) && artwork != null;
            _opponentPlayedCardRevealImage.sprite = hasArtwork ? artwork : null;
            _opponentPlayedCardRevealImage.color = hasArtwork
                ? Color.white
                : new Color(0.035f, 0.055f, 0.085f, 0.96f);
            _opponentPlayedCardRevealImage.enabled = true;

            if (_opponentPlayedCardFallbackText != null)
            {
                _opponentPlayedCardFallbackText.text = request.CardId;
                _opponentPlayedCardFallbackText.gameObject.SetActive(!hasArtwork);
            }

            var showStats = _showOpponentPlayedCardStats && request.CardMaxHp > 0;
            if (_opponentPlayedCardAttackText != null)
            {
                _opponentPlayedCardAttackText.text = request.CardAttack.ToString();
                _opponentPlayedCardAttackText.gameObject.SetActive(showStats);
            }

            if (_opponentPlayedCardHpText != null)
            {
                _opponentPlayedCardHpText.text = request.CardMaxHp.ToString();
                _opponentPlayedCardHpText.gameObject.SetActive(showStats);
            }

            _opponentPlayedCardRevealCanvasGroup.alpha = 0f;
            _opponentPlayedCardRevealCanvasGroup.interactable = false;
            _opponentPlayedCardRevealCanvasGroup.blocksRaycasts = false;
            _opponentPlayedCardRevealCanvasGroup.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            RefreshOpponentPlayedCardStatLayout();
        }

        private void RefreshOpponentPlayedCardStatLayout()
        {
            if (_opponentPlayedCardRevealImage == null ||
                _opponentPlayedCardRevealImage.sprite == null)
            {
                return;
            }

            var artworkRectTransform = _opponentPlayedCardRevealImage.rectTransform;
            var containerRect = artworkRectTransform.rect;
            var renderedSpriteRect = CalculateOpponentPlayedCardRenderedSpriteRect(
                containerRect,
                _opponentPlayedCardRevealImage.sprite,
                _opponentPlayedCardRevealImage.preserveAspect);
            PositionOpponentPlayedCardStatText(
                _opponentPlayedCardAttackText,
                containerRect,
                renderedSpriteRect,
                _opponentPlayedCardAttackStatNormalizedPosition);
            PositionOpponentPlayedCardStatText(
                _opponentPlayedCardHpText,
                containerRect,
                renderedSpriteRect,
                _opponentPlayedCardHpStatNormalizedPosition);
        }

        private static Rect CalculateOpponentPlayedCardRenderedSpriteRect(
            Rect containerRect,
            Sprite sprite,
            bool preserveAspect)
        {
            if (!preserveAspect ||
                sprite == null ||
                containerRect.width <= 0f ||
                containerRect.height <= 0f ||
                sprite.rect.width <= 0f ||
                sprite.rect.height <= 0f)
            {
                return containerRect;
            }

            var spriteAspect = sprite.rect.width / sprite.rect.height;
            var containerAspect = containerRect.width / containerRect.height;
            if (spriteAspect > containerAspect)
            {
                var renderedHeight = containerRect.width / spriteAspect;
                return new Rect(
                    containerRect.xMin,
                    containerRect.center.y - (renderedHeight * 0.5f),
                    containerRect.width,
                    renderedHeight);
            }

            var renderedWidth = containerRect.height * spriteAspect;
            return new Rect(
                containerRect.center.x - (renderedWidth * 0.5f),
                containerRect.yMin,
                renderedWidth,
                containerRect.height);
        }

        private static void PositionOpponentPlayedCardStatText(
            Text text,
            Rect containerRect,
            Rect renderedSpriteRect,
            Vector2 normalizedSpritePosition)
        {
            if (text == null || containerRect.width <= 0f || containerRect.height <= 0f)
            {
                return;
            }

            var clampedPosition = new Vector2(
                Mathf.Clamp01(normalizedSpritePosition.x),
                Mathf.Clamp01(normalizedSpritePosition.y));
            var localPoint = new Vector2(
                Mathf.Lerp(renderedSpriteRect.xMin, renderedSpriteRect.xMax, clampedPosition.x),
                Mathf.Lerp(renderedSpriteRect.yMin, renderedSpriteRect.yMax, clampedPosition.y));
            var anchor = new Vector2(
                Mathf.InverseLerp(containerRect.xMin, containerRect.xMax, localPoint.x),
                Mathf.InverseLerp(containerRect.yMin, containerRect.yMax, localPoint.y));

            var rectTransform = text.rectTransform;
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }

        private void HideOpponentPlayedCardReveal()
        {
            if (_opponentPlayedCardRevealCanvasGroup == null)
            {
                return;
            }

            _opponentPlayedCardRevealCanvasGroup.alpha = 0f;
            _opponentPlayedCardRevealCanvasGroup.interactable = false;
            _opponentPlayedCardRevealCanvasGroup.blocksRaycasts = false;
            _opponentPlayedCardRevealCanvasGroup.gameObject.SetActive(false);
        }

        public float PlayOnlineBattleEventAnimations(IReadOnlyList<BattleEventDto> battleEvents)
        {
            if (battleEvents == null || battleEvents.Count == 0)
            {
                return 0f;
            }

            QueueOpponentPlayedCardReveals(battleEvents);

            var animationGroups = BuildOnlineAnimationGroups(battleEvents);
            if (animationGroups.Count == 0)
            {
                return 0f;
            }

            if (animationGroups.Count == 1)
            {
                return PlayOnlineBattleEventAnimationGroup(animationGroups[0]);
            }

            StartCoroutine(PlayOnlineBattleEventAnimationGroups(animationGroups));
            return EstimateOnlineBattleEventAnimationGroupsDuration(animationGroups);
        }

        private float PlayOnlineBattleEventAnimationGroup(IReadOnlyList<BattleEventDto> battleEvents)
        {
            var attackEvent = FindFirstBattleEvent(battleEvents, BattleEventType.AttackStarted);
            if (attackEvent != null)
            {
                return PlayOnlineAttackAnimation(attackEvent, battleEvents);
            }

            var spellEvent = FindFirstBattleEvent(battleEvents, BattleEventType.SpellCast);
            if (spellEvent != null)
            {
                return PlayOnlineImpactAnimation(spellEvent.CardId, spellEvent, battleEvents);
            }

            if (HasOnlineImpactEvents(battleEvents))
            {
                return PlayOnlineImpactAnimation(null, null, battleEvents);
            }

            return _animateOnlineMoveEvents
                ? PlayOnlineMoveAnimations(battleEvents)
                : 0f;
        }

        private IEnumerator PlayOnlineBattleEventAnimationGroups(IReadOnlyList<IReadOnlyList<BattleEventDto>> animationGroups)
        {
            if (animationGroups == null)
            {
                yield break;
            }

            foreach (var animationGroup in animationGroups)
            {
                var duration = PlayOnlineBattleEventAnimationGroup(animationGroup);
                if (duration > 0f)
                {
                    yield return new WaitForSecondsRealtime(duration);
                }
            }
        }

        private float EstimateOnlineBattleEventAnimationGroupsDuration(IReadOnlyList<IReadOnlyList<BattleEventDto>> animationGroups)
        {
            if (animationGroups == null)
            {
                return 0f;
            }

            var duration = 0f;
            foreach (var animationGroup in animationGroups)
            {
                duration += EstimateOnlineBattleEventAnimationGroupDuration(animationGroup);
            }

            return duration;
        }

        private float EstimateOnlineBattleEventAnimationGroupDuration(IReadOnlyList<BattleEventDto> battleEvents)
        {
            if (battleEvents == null || battleEvents.Count == 0)
            {
                return 0f;
            }

            var attackEvent = FindFirstBattleEvent(battleEvents, BattleEventType.AttackStarted);
            if (attackEvent != null)
            {
                var request = BuildOnlineAttackAnimationRequest(attackEvent, battleEvents);
                return request == null ? 0f : EstimateAttackAnimationDuration(request);
            }

            var spellEvent = FindFirstBattleEvent(battleEvents, BattleEventType.SpellCast);
            if (spellEvent != null)
            {
                var request = BuildOnlineImpactAnimationRequest(spellEvent.CardId, spellEvent, battleEvents);
                return request == null || !HasPresentationImpact(request)
                    ? 0f
                    : EstimateImpactAnimationDuration(request);
            }

            if (HasOnlineImpactEvents(battleEvents))
            {
                var request = BuildOnlineImpactAnimationRequest(null, null, battleEvents);
                return request == null || !HasPresentationImpact(request)
                    ? 0f
                    : EstimateImpactAnimationDuration(request);
            }

            return _animateOnlineMoveEvents && FindFirstBattleEvent(battleEvents, BattleEventType.OccupantMoved) != null
                ? _attackRunDuration
                : 0f;
        }

        private static List<IReadOnlyList<BattleEventDto>> BuildOnlineAnimationGroups(IReadOnlyList<BattleEventDto> battleEvents)
        {
            var groups = new List<IReadOnlyList<BattleEventDto>>();
            List<BattleEventDto> currentGroup = null;

            void FlushCurrentGroup()
            {
                if (currentGroup == null || currentGroup.Count == 0)
                {
                    currentGroup = null;
                    return;
                }

                groups.Add(currentGroup);
                currentGroup = null;
            }

            if (battleEvents == null)
            {
                return groups;
            }

            foreach (var battleEvent in battleEvents)
            {
                if (battleEvent == null)
                {
                    continue;
                }

                if (StartsOnlineAnimationGroup(battleEvent.EventType))
                {
                    FlushCurrentGroup();
                    currentGroup = new List<BattleEventDto> { battleEvent };
                    continue;
                }

                if (IsOnlineImpactEvent(battleEvent.EventType))
                {
                    currentGroup ??= new List<BattleEventDto>();
                    currentGroup.Add(battleEvent);
                    continue;
                }

                FlushCurrentGroup();
            }

            FlushCurrentGroup();
            return groups;
        }

        private static bool StartsOnlineAnimationGroup(BattleEventType eventType)
        {
            return eventType == BattleEventType.AttackStarted ||
                   eventType == BattleEventType.SpellCast ||
                   eventType == BattleEventType.OccupantMoved;
        }

        private static bool IsOnlineImpactEvent(BattleEventType eventType)
        {
            return eventType == BattleEventType.DamageApplied ||
                   eventType == BattleEventType.HealingApplied ||
                   eventType == BattleEventType.OccupantRemoved;
        }

        public void ConfigureOnlineInput(OnlineBattleConnectionTester onlineConnectionTester, bool sendPlayerActionsToOnlineGateway)
        {
            _onlineConnectionTester = onlineConnectionTester;
            _sendPlayerActionsToOnlineGateway = sendPlayerActionsToOnlineGateway;
        }

        private void ConfigureLocalInput()
        {
            _sendPlayerActionsToOnlineGateway = false;
            _onlineConnectionTester = null;
        }

        public void PresentOnlineProjectedState(OnlineBattleConnectionTester onlineConnectionTester)
        {
            if (onlineConnectionTester == null || onlineConnectionTester.CurrentProjectedBattleState == null)
            {
                return;
            }

            if (_onlineConnectionTester != null && !ReferenceEquals(_onlineConnectionTester, onlineConnectionTester))
            {
                return;
            }

            _onlineConnectionTester = onlineConnectionTester;
            _sendPlayerActionsToOnlineGateway = true;
            ReplaceCombatLogEntries(onlineConnectionTester.CombatLogEntries);
            RefreshPresenter();
        }

        public OnlineBattleConnectionTester OnlineConnectionTester => _onlineConnectionTester;

        public bool TryGetCardDefinition(string cardId, out CardDefinition definition)
        {
            definition = null;

            if (_cardDefinitionProvider == null || string.IsNullOrWhiteSpace(cardId))
            {
                return false;
            }

            try
            {
                definition = _cardDefinitionProvider.GetRequired(cardId);
                return definition != null;
            }
            catch
            {
                return false;
            }
        }

        public bool TryGetCardDefinitionAsset(string cardId, out CardDefinitionAsset cardAsset)
        {
            cardAsset = null;

            if (_cardCatalogAsset != null &&
                _cardCatalogAsset.TryGetCardAsset(cardId, out cardAsset))
            {
                return true;
            }

            if (string.Equals(cardId, "master", StringComparison.OrdinalIgnoreCase))
            {
                cardAsset = Resources.Load<CardDefinitionAsset>(MasterCardAssetResourcePath);
                return cardAsset != null;
            }

            return false;
        }

        public void StartBattleWithDeckCardIds(IReadOnlyList<string> playerDeckCardIds, IReadOnlyList<string> aiDeckCardIds = null)
        {
            if (playerDeckCardIds == null || playerDeckCardIds.Count == 0)
            {
                throw new InvalidOperationException("Drafted player deck card ids must be provided before starting the battle.");
            }

            _runtimePlayerDeckCardIdsOverride = new List<string>(playerDeckCardIds);
            _runtimeAIDeckCardIdsOverride = aiDeckCardIds != null && aiDeckCardIds.Count > 0
                ? new List<string>(aiDeckCardIds)
                : Array.Empty<string>();

            StartBattle();
        }

        public void StartOnlineServerAiBattleWithDeckCardIds(IReadOnlyList<string> playerDeckCardIds)
        {
            if (playerDeckCardIds == null || playerDeckCardIds.Count == 0)
            {
                throw new InvalidOperationException("Drafted player deck card ids must be provided before starting the online battle.");
            }

            CancelPresentationSequence();
            _battleScreenPresenter?.SetBattleUiVisible(true);
            _draftOverlayPresenter?.Hide();
            _hasQueuedDraftSceneReturn = false;
            ClearCombatLog();

            _runtimePlayerDeckCardIdsOverride = new List<string>(playerDeckCardIds);
            _runtimeAIDeckCardIdsOverride = Array.Empty<string>();

            var resolvedConfig = ResolveStartConfig();
            _cardDefinitionProvider = resolvedConfig.CardDefinitionProvider ?? new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>());
            _aiDecisionService = null;
            _aiTurnRunner = null;
            _battleFlowController = null;
            _battleGateway = null;

            _sendPlayerActionsToOnlineGateway = true;
            var tester = ResolveOrCreateOnlineConnectionTester();
            ConfigureOnlineInput(tester, sendPlayerActionsToOnlineGateway: true);

            var matchId = CreateOnlineDraftMatchId();
            _lastInteractionStatus = "Connecting to server AI battle.";
            Debug.Log($"[After333 Battle Launch] {BuildOnlineLaunchTrace("PVE", matchId, _runtimePlayerDeckCardIdsOverride)}");
            AddCombatLogEntry($"Connecting to server AI battle: {matchId} ({FormatShortRunDeckTrace()})");
            tester.ConnectForServerAiBattle(
                OnlineServerUrl,
                matchId,
                _onlinePlayerToken,
                _runtimePlayerDeckCardIdsOverride);
            RefreshPresenter();
        }

        public void StartOnlineMatchmakingBattleWithDeckCardIds(IReadOnlyList<string> playerDeckCardIds)
        {
            var hasLocalDeck = playerDeckCardIds != null && playerDeckCardIds.Count == 33;
            var isReconnectLaunch = DraftRunSessionState.ConsumeLastBattleStartWasPvpReconnect() || !hasLocalDeck;
            if (!hasLocalDeck && !AccountSessionState.IsAuthenticated)
            {
                throw new InvalidOperationException("A drafted player deck or authenticated reconnect account is required before entering matchmaking.");
            }

            CancelPresentationSequence();
            _battleScreenPresenter?.SetBattleUiVisible(true);
            _draftOverlayPresenter?.Hide();
            _hasQueuedDraftSceneReturn = false;
            ClearCombatLog();

            _runtimePlayerDeckCardIdsOverride = hasLocalDeck
                ? new List<string>(playerDeckCardIds)
                : new List<string>();
            _runtimeAIDeckCardIdsOverride = Array.Empty<string>();

            var resolvedConfig = ResolveStartConfig();
            _cardDefinitionProvider = resolvedConfig.CardDefinitionProvider ?? new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>());
            _aiDecisionService = null;
            _aiTurnRunner = null;
            _battleFlowController = null;
            _battleGateway = null;

            _sendPlayerActionsToOnlineGateway = true;
            var preparedSession = OnlineBattleSessionCoordinator.Instance;
            if (preparedSession != null && preparedSession.TryAttachToBattle(this))
            {
                _onlineConnectionTester = preparedSession.ConnectionTester;
                _lastInteractionStatus = "Connected to the matched PVP battle.";
                Debug.Log(
                    $"[After333 Battle Launch] attached prepared PVP session match={FormatLogValue(_onlineConnectionTester.CurrentMatchId)}");
                HidePvpMatchmakingOverlay();
                RefreshPresenter();
                return;
            }

            var tester = ResolveOrCreateOnlineConnectionTester();
            ConfigureOnlineInput(tester, sendPlayerActionsToOnlineGateway: true);

            var playerToken = CreateOnlineMatchmakingPlayerToken();
            _lastInteractionStatus = "Waiting for PvP matchmaking or reconnecting to an interrupted PvP battle.";
            Debug.Log($"[After333 Battle Launch] {BuildOnlineLaunchTrace("PVP", "pvp-matchmaking", _runtimePlayerDeckCardIdsOverride)} account={FormatLogValue(AccountSessionState.AccountId)} token={playerToken}");
            ShowPvpMatchmakingOverlay();
            if (isReconnectLaunch)
            {
                SetPvpMatchmakingOverlayMessage("전투에 재접속하는 중...");
            }

            ShowOnlineStatusToast(isReconnectLaunch ? "전투에 재접속하는 중입니다." : "상대를 찾는 중입니다.");
            tester.ConnectForPvpMatchmaking(
                OnlineServerUrl,
                playerToken,
                _runtimePlayerDeckCardIdsOverride);
            RefreshPresenter();
        }

        public void BeginDraft()
        {
            CancelPresentationSequence();
            _battleScreenPresenter?.SetBattleUiVisible(false);
            _runtimePlayerDeckCardIdsOverride = Array.Empty<string>();
            _runtimeAIDeckCardIdsOverride = Array.Empty<string>();
            _draftSessionService = new DraftSessionService(_cardCatalogAsset, CreateDraftRandom());
            var validation = _draftSessionService.ValidateCatalog();

            _battleFlowController = null;
            _battleGateway = null;
            _lastInteractionStatus = validation.IsValid ? "Draft started." : validation.Message;
            ClearCombatLog();
            RefreshPresenter();

            if (!validation.IsValid)
            {
                _battleScreenPresenter?.SetBattleUiVisible(true);
                ResolveDraftOverlayPresenter()?.ShowValidationMessage(validation.Message);
                Debug.LogWarning(validation.Message);
                return;
            }

            var openingOffer = _draftSessionService.BeginDraft();
            ResolveDraftOverlayPresenter()?.ShowOffer(openingOffer, _draftSessionService.DeckState.CardIds);
        }

        public void SelectDraftCard(string cardId)
        {
            if (_draftSessionService == null)
            {
                _lastInteractionStatus = "Draft is not active.";
                return;
            }

            try
            {
                var result = _draftSessionService.SelectCard(cardId);
                _lastInteractionStatus = $"Draft picked '{result.SelectedCardId}'.";

                if (result.IsComplete)
                {
                    _draftOverlayPresenter?.Hide();
                    _runtimePlayerDeckCardIdsOverride = new List<string>(result.CompletedDeckCardIds);
                    _lastInteractionStatus = _autoStartBattleAfterDraft
                        ? "Draft complete. Starting battle."
                        : "Draft complete. Press Start Battle when you want to use the drafted deck.";

                    _draftSessionService = null;

                    if (_autoStartBattleAfterDraft)
                    {
                        StartBattleWithDeckCardIds(result.CompletedDeckCardIds);
                        return;
                    }

                    _battleScreenPresenter?.SetBattleUiVisible(true);
                    return;
                }

                ResolveDraftOverlayPresenter()?.ShowOffer(result.NextOffer, _draftSessionService.DeckState.CardIds);
            }
            catch (Exception ex)
            {
                _lastInteractionStatus = ex.Message;
                ResolveDraftOverlayPresenter()?.ShowValidationMessage(ex.Message);
                Debug.LogWarning(ex.Message);
            }
        }

        public void ReturnFromDraftOverlay()
        {
            _draftOverlayPresenter?.Hide();
            _battleScreenPresenter?.SetBattleUiVisible(true);
            _lastInteractionStatus = "Draft closed.";
            RefreshPresenter();
        }

        private string OnlineServerUrl =>
            Project333.Runtime.Presentation.Project333ServerEndpointSettings.ResolveBattleWebSocketUrl(_onlineServerUrl);

        private void Awake()
        {
            if (UnityEngine.Application.isPlaying && !AccountSessionState.IsAuthenticated)
            {
                SceneManager.LoadScene("GameStart_VSlice");
                return;
            }

            AutoAssignOnlineConnectionTester();

            if (_battleScreenPresenter != null)
            {
                _battleScreenPresenter.Bind(this);
            }

            if (_draftOverlayPresenter != null || _startWithDraftBeforeBattle)
            {
                ResolveDraftOverlayPresenter();
            }

            EnsureOpponentPlayedCardReveal();
            ApplyOpponentPlayedCardRevealSettings();

            if (_startOnAwake)
            {
                if (DraftRunSessionState.TryConsumePendingBattleStart(out var draftedDeckCardIds, out var launchMode))
                {
                    Debug.Log(
                        $"[After333 Battle Launch] consumedPending mode={launchMode} run={FormatLogValue(AccountSessionState.ActiveRunId)} deck={FormatLogValue(AccountSessionState.ActiveDeckId)} runStatus={FormatLogValue(AccountSessionState.ActiveRunStatus)} record={AccountSessionState.ActiveRunWins}-{AccountSessionState.ActiveRunLosses} deckCards={draftedDeckCardIds?.Count ?? 0}");
                    if (launchMode == DraftBattleLaunchMode.OnlineMatchmaking)
                    {
                        StartOnlineMatchmakingBattleWithDeckCardIds(draftedDeckCardIds);
                    }
                    else if (launchMode == DraftBattleLaunchMode.OnlineServerAi)
                    {
                        StartOnlineServerAiBattleWithDeckCardIds(draftedDeckCardIds);
                    }
                    else
                    {
                        StartBattleWithDeckCardIds(draftedDeckCardIds);
                    }
                }
                else if (_startWithDraftBeforeBattle)
                {
                    BeginDraft();
                }
                else
                {
                    StartBattle();
                }
            }
        }

        private void OnValidate()
        {
            _onlineErrorToastFontSize = Mathf.Max(1f, _onlineErrorToastFontSize);
            _onlineErrorToastSize = new Vector2(
                Mathf.Max(1f, _onlineErrorToastSize.x),
                Mathf.Max(1f, _onlineErrorToastSize.y));
            _onlineErrorToastTextPadding = new Vector2(
                Mathf.Max(0f, _onlineErrorToastTextPadding.x),
                Mathf.Max(0f, _onlineErrorToastTextPadding.y));
            _onlineTurnTimerFontSize = Mathf.Max(1f, _onlineTurnTimerFontSize);
            _onlineTurnTimerSize = new Vector2(
                Mathf.Max(1f, _onlineTurnTimerSize.x),
                Mathf.Max(1f, _onlineTurnTimerSize.y));
            _onlineTurnTimerTextPadding = new Vector2(
                Mathf.Max(0f, _onlineTurnTimerTextPadding.x),
                Mathf.Max(0f, _onlineTurnTimerTextPadding.y));
            _onlineTurnTimerRevealThresholdSeconds = Mathf.Max(0, _onlineTurnTimerRevealThresholdSeconds);
            _onlineTurnTimerDangerThresholdSeconds = Mathf.Max(0, _onlineTurnTimerDangerThresholdSeconds);
            _opponentPlayedCardRevealSize = new Vector2(
                Mathf.Max(1f, _opponentPlayedCardRevealSize.x),
                Mathf.Max(1f, _opponentPlayedCardRevealSize.y));
            _opponentPlayedCardRevealVisualScale = Mathf.Max(0.1f, _opponentPlayedCardRevealVisualScale);
            _opponentPlayedCardNormalDisplaySeconds = Mathf.Max(0f, _opponentPlayedCardNormalDisplaySeconds);
            _opponentPlayedCardQueuedDisplaySeconds = Mathf.Max(0f, _opponentPlayedCardQueuedDisplaySeconds);
            _opponentPlayedCardFadeInSeconds = Mathf.Max(0f, _opponentPlayedCardFadeInSeconds);
            _opponentPlayedCardFadeOutSeconds = Mathf.Max(0f, _opponentPlayedCardFadeOutSeconds);
            _opponentPlayedCardStatTextSize = new Vector2(
                Mathf.Max(1f, _opponentPlayedCardStatTextSize.x),
                Mathf.Max(1f, _opponentPlayedCardStatTextSize.y));
            _opponentPlayedCardStatFontSize = Mathf.Max(1, _opponentPlayedCardStatFontSize);
            if (UnityEngine.Application.isPlaying)
            {
                ApplyOnlineErrorToastSettings();
                ApplyOnlineTurnTimerSettings();
                ApplyOpponentPlayedCardRevealSettings();
            }
#if UNITY_EDITOR
            else
            {
                QueuePersistentBattleUiMaterialization();
            }
#endif
        }

#if UNITY_EDITOR
        private void QueuePersistentBattleUiMaterialization()
        {
            if (_hasQueuedEditorBattleUiMaterialization)
            {
                return;
            }

            _hasQueuedEditorBattleUiMaterialization = true;
            EditorApplication.delayCall += MaterializePersistentBattleUiForEditor;
        }

        [ContextMenu("Materialize Missing Persistent Battle Runtime UI")]
        public void MaterializePersistentBattleUiForEditor()
        {
            _hasQueuedEditorBattleUiMaterialization = false;
            if (this == null || UnityEngine.Application.isPlaying)
            {
                return;
            }

            EnsureOnlineErrorToast();
            EnsureOnlineTurnTimer();
            EnsureOpponentPlayedCardReveal();
            EnsurePvpMatchmakingOverlay();

            EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
#endif

        private void EnsureOnlineErrorToast()
        {
            if (_onlineErrorToastCanvasGroup != null && _onlineErrorToastText != null)
            {
                return;
            }

            _onlineErrorToastUsesSceneLayout = false;

            var canvasObject = new GameObject(
                "OnlineErrorToastCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(CanvasGroup));
            MoveCreatedObjectToBattleScene(canvasObject);
            RegisterEditorCreatedObject(canvasObject, "Create Online Error Toast UI");
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 20000;

            var canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.matchWidthOrHeight = 1f;

            _onlineErrorToastCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
            _onlineErrorToastCanvasGroup.alpha = 0f;
            _onlineErrorToastCanvasGroup.interactable = false;
            _onlineErrorToastCanvasGroup.blocksRaycasts = false;

            var panelObject = new GameObject("OnlineErrorToastPanel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(canvasObject.transform, false);
            _onlineErrorToastPanelRect = panelObject.GetComponent<RectTransform>();
            _onlineErrorToastPanelImage = panelObject.GetComponent<Image>();
            _onlineErrorToastPanelImage.raycastTarget = false;

            var textObject = new GameObject("OnlineErrorToastText", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panelObject.transform, false);
            _onlineErrorToastTextRect = textObject.GetComponent<RectTransform>();

            _onlineErrorToastText = textObject.GetComponent<Text>();
            _onlineErrorToastText.alignment = TextAnchor.MiddleCenter;
            _onlineErrorToastText.fontStyle = FontStyle.Bold;
            _onlineErrorToastText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _onlineErrorToastText.verticalOverflow = VerticalWrapMode.Truncate;
            _onlineErrorToastText.supportRichText = false;
            _onlineErrorToastText.raycastTarget = false;
            ApplyOnlineErrorToastSettings();

            if (!UnityEngine.Application.isPlaying)
            {
                _onlineErrorToastUsesSceneLayout = true;
            }

            canvasObject.SetActive(false);
        }

        private void EnsureOnlineTurnTimer()
        {
            if (_onlineTurnTimerCanvasGroup != null && _onlineTurnTimerText != null)
            {
                return;
            }

            _onlineTurnTimerUsesSceneLayout = false;

            var canvasObject = new GameObject(
                "OnlineTurnTimerCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(CanvasGroup));
            MoveCreatedObjectToBattleScene(canvasObject);
            RegisterEditorCreatedObject(canvasObject, "Create Online Turn Timer UI");
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 19990;

            var canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.matchWidthOrHeight = 1f;

            _onlineTurnTimerCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
            _onlineTurnTimerCanvasGroup.alpha = 0f;
            _onlineTurnTimerCanvasGroup.interactable = false;
            _onlineTurnTimerCanvasGroup.blocksRaycasts = false;

            var panelObject = new GameObject("OnlineTurnTimerPanel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(canvasObject.transform, false);
            _onlineTurnTimerPanelRect = panelObject.GetComponent<RectTransform>();
            _onlineTurnTimerPanelImage = panelObject.GetComponent<Image>();
            _onlineTurnTimerPanelImage.raycastTarget = false;

            var textObject = new GameObject("OnlineTurnTimerText", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panelObject.transform, false);
            _onlineTurnTimerTextRect = textObject.GetComponent<RectTransform>();

            _onlineTurnTimerText = textObject.GetComponent<Text>();
            _onlineTurnTimerText.alignment = TextAnchor.MiddleCenter;
            _onlineTurnTimerText.fontStyle = FontStyle.Bold;
            _onlineTurnTimerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _onlineTurnTimerText.verticalOverflow = VerticalWrapMode.Truncate;
            _onlineTurnTimerText.supportRichText = false;
            _onlineTurnTimerText.raycastTarget = false;
            ApplyOnlineTurnTimerSettings();

            if (!UnityEngine.Application.isPlaying)
            {
                _onlineTurnTimerUsesSceneLayout = true;
            }

            canvasObject.SetActive(false);
        }

        private void EnsureOpponentPlayedCardReveal()
        {
            if (_opponentPlayedCardRevealCanvasGroup != null &&
                _opponentPlayedCardRevealImage != null &&
                _opponentPlayedCardFallbackText != null &&
                _opponentPlayedCardAttackText != null &&
                _opponentPlayedCardHpText != null)
            {
                return;
            }

            _opponentPlayedCardRevealUsesSceneLayout = false;

            var canvasObject = new GameObject(
                "OpponentPlayedCardRevealCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(CanvasGroup));
            MoveCreatedObjectToBattleScene(canvasObject);
            RegisterEditorCreatedObject(canvasObject, "Create Opponent Played Card Reveal UI");

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = ResolveOpponentPlayedCardSortingOrder();

            var canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.matchWidthOrHeight = 1f;

            _opponentPlayedCardRevealCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
            _opponentPlayedCardRevealCanvasGroup.alpha = 0f;
            _opponentPlayedCardRevealCanvasGroup.interactable = false;
            _opponentPlayedCardRevealCanvasGroup.blocksRaycasts = false;

            var cardObject = new GameObject("CardImage", typeof(RectTransform), typeof(Image));
            cardObject.transform.SetParent(canvasObject.transform, false);
            _opponentPlayedCardRevealRect = cardObject.GetComponent<RectTransform>();
            _opponentPlayedCardRevealImage = cardObject.GetComponent<Image>();
            _opponentPlayedCardRevealImage.preserveAspect = true;
            _opponentPlayedCardRevealImage.raycastTarget = false;

            var fallbackObject = new GameObject("FallbackCardIdText", typeof(RectTransform), typeof(Text));
            fallbackObject.transform.SetParent(cardObject.transform, false);
            _opponentPlayedCardFallbackText = fallbackObject.GetComponent<Text>();
            _opponentPlayedCardFallbackText.alignment = TextAnchor.MiddleCenter;
            _opponentPlayedCardFallbackText.fontStyle = FontStyle.Bold;
            _opponentPlayedCardFallbackText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _opponentPlayedCardFallbackText.verticalOverflow = VerticalWrapMode.Truncate;
            _opponentPlayedCardFallbackText.supportRichText = false;
            _opponentPlayedCardFallbackText.raycastTarget = false;

            _opponentPlayedCardAttackText = CreateOpponentPlayedCardStatText(
                cardObject.transform,
                "AttackValueText");
            _opponentPlayedCardHpText = CreateOpponentPlayedCardStatText(
                cardObject.transform,
                "HpValueText");

            ApplyOpponentPlayedCardRevealSettings();
            if (!UnityEngine.Application.isPlaying)
            {
                _opponentPlayedCardRevealUsesSceneLayout = true;
            }

            canvasObject.SetActive(false);
        }

        private Text CreateOpponentPlayedCardStatText(Transform parent, string objectName)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text), typeof(Outline));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private void EnsurePvpMatchmakingOverlay()
        {
            if (_pvpMatchmakingCanvasGroup != null && _pvpMatchmakingText != null && _pvpMatchmakingCancelButton != null)
            {
                _pvpMatchmakingCancelButton.onClick.RemoveListener(CancelPvpMatchmakingFromUi);
                _pvpMatchmakingCancelButton.onClick.AddListener(CancelPvpMatchmakingFromUi);
                ApplyPvpMatchmakingOverlaySettings();
                return;
            }

            _pvpMatchmakingUsesSceneLayout = false;

            var canvasObject = new GameObject(
                "PvpMatchmakingOverlayCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            MoveCreatedObjectToBattleScene(canvasObject);
            RegisterEditorCreatedObject(canvasObject, "Create PVP Matchmaking UI");
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 20010;

            var canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.matchWidthOrHeight = 1f;

            _pvpMatchmakingCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
            _pvpMatchmakingCanvasGroup.alpha = 0f;
            _pvpMatchmakingCanvasGroup.interactable = false;
            _pvpMatchmakingCanvasGroup.blocksRaycasts = false;

            var dimObject = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dimObject.transform.SetParent(canvasObject.transform, false);
            _pvpMatchmakingDimRect = dimObject.GetComponent<RectTransform>();
            _pvpMatchmakingDimImage = dimObject.GetComponent<Image>();

            var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Outline));
            panelObject.transform.SetParent(canvasObject.transform, false);
            _pvpMatchmakingPanelRect = panelObject.GetComponent<RectTransform>();
            _pvpMatchmakingPanelImage = panelObject.GetComponent<Image>();
            _pvpMatchmakingPanelOutline = panelObject.GetComponent<Outline>();

            var textObject = new GameObject("StatusText", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panelObject.transform, false);
            _pvpMatchmakingTextRect = textObject.GetComponent<RectTransform>();
            _pvpMatchmakingText = textObject.GetComponent<Text>();
            _pvpMatchmakingText.alignment = TextAnchor.MiddleCenter;
            _pvpMatchmakingText.fontStyle = FontStyle.Bold;
            _pvpMatchmakingText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _pvpMatchmakingText.verticalOverflow = VerticalWrapMode.Truncate;
            _pvpMatchmakingText.supportRichText = false;
            _pvpMatchmakingText.raycastTarget = false;

            var cancelButtonObject = new GameObject("CancelButton", typeof(RectTransform), typeof(Image), typeof(Button));
            cancelButtonObject.transform.SetParent(panelObject.transform, false);
            _pvpMatchmakingCancelButtonRect = cancelButtonObject.GetComponent<RectTransform>();
            _pvpMatchmakingCancelButtonImage = cancelButtonObject.GetComponent<Image>();
            _pvpMatchmakingCancelButton = cancelButtonObject.GetComponent<Button>();
            _pvpMatchmakingCancelButton.onClick.AddListener(CancelPvpMatchmakingFromUi);

            var cancelTextObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            cancelTextObject.transform.SetParent(cancelButtonObject.transform, false);
            var cancelTextRect = cancelTextObject.GetComponent<RectTransform>();
            cancelTextRect.anchorMin = Vector2.zero;
            cancelTextRect.anchorMax = Vector2.one;
            cancelTextRect.offsetMin = Vector2.zero;
            cancelTextRect.offsetMax = Vector2.zero;

            _pvpMatchmakingCancelButtonText = cancelTextObject.GetComponent<Text>();
            _pvpMatchmakingCancelButtonText.alignment = TextAnchor.MiddleCenter;
            _pvpMatchmakingCancelButtonText.fontStyle = FontStyle.Bold;
            _pvpMatchmakingCancelButtonText.supportRichText = false;
            _pvpMatchmakingCancelButtonText.raycastTarget = false;

            ApplyPvpMatchmakingOverlaySettings();
            if (!UnityEngine.Application.isPlaying)
            {
                _pvpMatchmakingUsesSceneLayout = true;
            }

            canvasObject.SetActive(false);
        }

        private static void RegisterEditorCreatedObject(GameObject createdObject, string undoLabel)
        {
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying && createdObject != null)
            {
                Undo.RegisterCreatedObjectUndo(createdObject, undoLabel);
            }
#endif
        }

        private void MoveCreatedObjectToBattleScene(GameObject createdObject)
        {
            if (createdObject != null && gameObject.scene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(createdObject, gameObject.scene);
            }
        }

        private void ApplyOnlineErrorToastSettings()
        {
            if (_onlineErrorToastPanelRect != null && !_onlineErrorToastUsesSceneLayout)
            {
                var anchor = new Vector2(
                    Mathf.Clamp01(_onlineErrorToastAnchor.x),
                    Mathf.Clamp01(_onlineErrorToastAnchor.y));
                _onlineErrorToastPanelRect.anchorMin = anchor;
                _onlineErrorToastPanelRect.anchorMax = anchor;
                _onlineErrorToastPanelRect.pivot = new Vector2(0.5f, 0.5f);
                _onlineErrorToastPanelRect.anchoredPosition = _onlineErrorToastOffset;
                _onlineErrorToastPanelRect.sizeDelta = new Vector2(
                    Mathf.Max(1f, _onlineErrorToastSize.x),
                    Mathf.Max(1f, _onlineErrorToastSize.y));
            }

            if (_onlineErrorToastPanelImage != null)
            {
                _onlineErrorToastPanelImage.color = _onlineErrorToastPanelColor;
                _onlineErrorToastPanelImage.raycastTarget = false;
            }

            if (_onlineErrorToastTextRect != null && !_onlineErrorToastUsesSceneLayout)
            {
                var horizontalPadding = Mathf.Max(0f, _onlineErrorToastTextPadding.x);
                var verticalPadding = Mathf.Max(0f, _onlineErrorToastTextPadding.y);
                _onlineErrorToastTextRect.anchorMin = Vector2.zero;
                _onlineErrorToastTextRect.anchorMax = Vector2.one;
                _onlineErrorToastTextRect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
                _onlineErrorToastTextRect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
            }

            if (_onlineErrorToastText != null)
            {
                var resolvedFont = ResolveOnlineErrorToastFont();
                if (resolvedFont != null)
                {
                    _onlineErrorToastText.font = resolvedFont;
                }

                _onlineErrorToastText.color = _onlineErrorToastTextColor;
                _onlineErrorToastText.fontSize = Mathf.Max(1, Mathf.RoundToInt(_onlineErrorToastFontSize));
            }
        }

        private Font ResolveOnlineErrorToastFont()
        {
            if (_onlineErrorToastFont != null)
            {
                return _onlineErrorToastFont;
            }

            if (s_runtimeKoreanToastFont != null)
            {
                return s_runtimeKoreanToastFont;
            }

            try
            {
                s_runtimeKoreanToastFont = Font.CreateDynamicFontFromOSFont(KoreanFontCandidates, 32);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not create runtime Korean font for online error toast: {ex.Message}");
                s_runtimeKoreanToastFont = null;
            }

            return s_runtimeKoreanToastFont != null
                ? s_runtimeKoreanToastFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void ApplyOnlineTurnTimerSettings()
        {
            if (_onlineTurnTimerPanelRect != null && !_onlineTurnTimerUsesSceneLayout)
            {
                var anchor = new Vector2(
                    Mathf.Clamp01(_onlineTurnTimerAnchor.x),
                    Mathf.Clamp01(_onlineTurnTimerAnchor.y));
                _onlineTurnTimerPanelRect.anchorMin = anchor;
                _onlineTurnTimerPanelRect.anchorMax = anchor;
                _onlineTurnTimerPanelRect.pivot = new Vector2(0.5f, 0.5f);
                _onlineTurnTimerPanelRect.anchoredPosition = _onlineTurnTimerOffset;
                _onlineTurnTimerPanelRect.sizeDelta = new Vector2(
                    Mathf.Max(1f, _onlineTurnTimerSize.x),
                    Mathf.Max(1f, _onlineTurnTimerSize.y));
            }

            if (_onlineTurnTimerPanelImage != null)
            {
                _onlineTurnTimerPanelImage.raycastTarget = false;
            }

            if (_onlineTurnTimerTextRect != null && !_onlineTurnTimerUsesSceneLayout)
            {
                var horizontalPadding = Mathf.Max(0f, _onlineTurnTimerTextPadding.x);
                var verticalPadding = Mathf.Max(0f, _onlineTurnTimerTextPadding.y);
                _onlineTurnTimerTextRect.anchorMin = Vector2.zero;
                _onlineTurnTimerTextRect.anchorMax = Vector2.one;
                _onlineTurnTimerTextRect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
                _onlineTurnTimerTextRect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
            }

            if (_onlineTurnTimerText != null)
            {
                var resolvedFont = ResolveOnlineErrorToastFont();
                if (resolvedFont != null)
                {
                    _onlineTurnTimerText.font = resolvedFont;
                }

                _onlineTurnTimerText.fontSize = Mathf.Max(1, Mathf.RoundToInt(_onlineTurnTimerFontSize));
            }
        }

        private int ResolveOpponentPlayedCardSortingOrder()
        {
            return _opponentPlayedCardSortingOrder == LegacyOpponentPlayedCardSortingOrder
                ? DefaultOpponentPlayedCardSortingOrder
                : _opponentPlayedCardSortingOrder;
        }

        private void ApplyOpponentPlayedCardRevealSettings()
        {
            if (_opponentPlayedCardRevealCanvasGroup != null)
            {
                var canvas = _opponentPlayedCardRevealCanvasGroup.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.sortingOrder = ResolveOpponentPlayedCardSortingOrder();
                }

                _opponentPlayedCardRevealCanvasGroup.interactable = false;
                _opponentPlayedCardRevealCanvasGroup.blocksRaycasts = false;
            }

            if (_opponentPlayedCardRevealImage != null)
            {
                _opponentPlayedCardRevealImage.preserveAspect = true;
                _opponentPlayedCardRevealImage.raycastTarget = false;
            }

            if (_opponentPlayedCardRevealRect != null)
            {
                _opponentPlayedCardRevealRect.localScale = Vector3.one *
                    Mathf.Max(0.1f, _opponentPlayedCardRevealVisualScale);
            }

            if (_opponentPlayedCardRevealUsesSceneLayout)
            {
                return;
            }

            if (_opponentPlayedCardRevealRect != null)
            {
                _opponentPlayedCardRevealRect.anchorMin = new Vector2(0.5f, 0.5f);
                _opponentPlayedCardRevealRect.anchorMax = new Vector2(0.5f, 0.5f);
                _opponentPlayedCardRevealRect.pivot = new Vector2(0.5f, 0.5f);
                _opponentPlayedCardRevealRect.anchoredPosition = Vector2.zero;
                _opponentPlayedCardRevealRect.sizeDelta = new Vector2(
                    Mathf.Max(1f, _opponentPlayedCardRevealSize.x),
                    Mathf.Max(1f, _opponentPlayedCardRevealSize.y));
            }

            var font = ResolveOnlineErrorToastFont();
            if (_opponentPlayedCardFallbackText != null)
            {
                var fallbackRect = _opponentPlayedCardFallbackText.rectTransform;
                fallbackRect.anchorMin = Vector2.zero;
                fallbackRect.anchorMax = Vector2.one;
                fallbackRect.offsetMin = new Vector2(18f, 18f);
                fallbackRect.offsetMax = new Vector2(-18f, -18f);
                _opponentPlayedCardFallbackText.font = font;
                _opponentPlayedCardFallbackText.fontSize = 30;
                _opponentPlayedCardFallbackText.color = Color.white;
            }

            ApplyOpponentPlayedCardStatSettings(
                _opponentPlayedCardAttackText,
                _opponentPlayedCardAttackStatNormalizedPosition,
                font);
            ApplyOpponentPlayedCardStatSettings(
                _opponentPlayedCardHpText,
                _opponentPlayedCardHpStatNormalizedPosition,
                font);
        }

        private void ApplyOpponentPlayedCardStatSettings(Text text, Vector2 normalizedPosition, Font font)
        {
            if (text == null)
            {
                return;
            }

            var rect = text.rectTransform;
            var anchor = new Vector2(
                Mathf.Clamp01(normalizedPosition.x),
                Mathf.Clamp01(normalizedPosition.y));
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = _opponentPlayedCardStatTextSize;

            text.font = font;
            text.fontSize = Mathf.Max(1, _opponentPlayedCardStatFontSize);
            text.color = _opponentPlayedCardStatTextColor;
            if (text.TryGetComponent<Outline>(out var outline))
            {
                outline.effectColor = _opponentPlayedCardStatOutlineColor;
                outline.effectDistance = _opponentPlayedCardStatOutlineDistance;
                outline.useGraphicAlpha = true;
            }
        }

        private void ApplyPvpMatchmakingOverlaySettings()
        {
            if (_pvpMatchmakingDimRect != null && !_pvpMatchmakingUsesSceneLayout)
            {
                _pvpMatchmakingDimRect.anchorMin = Vector2.zero;
                _pvpMatchmakingDimRect.anchorMax = Vector2.one;
                _pvpMatchmakingDimRect.offsetMin = Vector2.zero;
                _pvpMatchmakingDimRect.offsetMax = Vector2.zero;
            }

            if (_pvpMatchmakingDimImage != null)
            {
                _pvpMatchmakingDimImage.color = _pvpMatchmakingDimColor;
                _pvpMatchmakingDimImage.raycastTarget = true;
            }

            if (_pvpMatchmakingPanelRect != null && !_pvpMatchmakingUsesSceneLayout)
            {
                _pvpMatchmakingPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
                _pvpMatchmakingPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
                _pvpMatchmakingPanelRect.pivot = new Vector2(0.5f, 0.5f);
                _pvpMatchmakingPanelRect.anchoredPosition = _pvpMatchmakingPanelOffset;
                _pvpMatchmakingPanelRect.sizeDelta = new Vector2(
                    Mathf.Max(1f, _pvpMatchmakingPanelSize.x),
                    Mathf.Max(1f, _pvpMatchmakingPanelSize.y));
            }

            if (_pvpMatchmakingPanelImage != null)
            {
                _pvpMatchmakingPanelImage.color = _pvpMatchmakingPanelColor;
                _pvpMatchmakingPanelImage.raycastTarget = true;
            }

            if (_pvpMatchmakingPanelOutline != null)
            {
                _pvpMatchmakingPanelOutline.effectColor = _pvpMatchmakingPanelOutlineColor;
                _pvpMatchmakingPanelOutline.effectDistance = new Vector2(2f, -2f);
            }

            if (_pvpMatchmakingTextRect != null && !_pvpMatchmakingUsesSceneLayout)
            {
                _pvpMatchmakingTextRect.anchorMin = Vector2.zero;
                _pvpMatchmakingTextRect.anchorMax = Vector2.one;
                _pvpMatchmakingTextRect.offsetMin = new Vector2(
                    Mathf.Max(0f, _pvpMatchmakingTextPadding.x),
                    Mathf.Max(0f, _pvpMatchmakingTextPadding.y));
                _pvpMatchmakingTextRect.offsetMax = new Vector2(
                    -Mathf.Max(0f, _pvpMatchmakingTextPadding.x),
                    0f);
            }

            var resolvedFont = ResolveOnlineErrorToastFont();
            if (_pvpMatchmakingText != null)
            {
                if (resolvedFont != null)
                {
                    _pvpMatchmakingText.font = resolvedFont;
                }

                _pvpMatchmakingText.color = _pvpMatchmakingTextColor;
                _pvpMatchmakingText.fontSize = Mathf.Max(1, Mathf.RoundToInt(_pvpMatchmakingTextFontSize));
            }

            if (_pvpMatchmakingCancelButtonRect != null && !_pvpMatchmakingUsesSceneLayout)
            {
                _pvpMatchmakingCancelButtonRect.anchorMin = new Vector2(0.5f, 0f);
                _pvpMatchmakingCancelButtonRect.anchorMax = new Vector2(0.5f, 0f);
                _pvpMatchmakingCancelButtonRect.pivot = new Vector2(0.5f, 0f);
                _pvpMatchmakingCancelButtonRect.anchoredPosition = _pvpMatchmakingCancelButtonOffset;
                _pvpMatchmakingCancelButtonRect.sizeDelta = new Vector2(
                    Mathf.Max(1f, _pvpMatchmakingCancelButtonSize.x),
                    Mathf.Max(1f, _pvpMatchmakingCancelButtonSize.y));
            }

            if (_pvpMatchmakingCancelButtonImage != null)
            {
                _pvpMatchmakingCancelButtonImage.color = _pvpMatchmakingCancelButtonColor;
                _pvpMatchmakingCancelButtonImage.raycastTarget = true;
            }

            if (_pvpMatchmakingCancelButtonText != null)
            {
                if (resolvedFont != null)
                {
                    _pvpMatchmakingCancelButtonText.font = resolvedFont;
                }

                _pvpMatchmakingCancelButtonText.text = _pvpMatchmakingCancelButtonLabel;
                _pvpMatchmakingCancelButtonText.color = _pvpMatchmakingTextColor;
                _pvpMatchmakingCancelButtonText.fontSize = Mathf.Max(1, Mathf.RoundToInt(_pvpMatchmakingCancelFontSize));
            }
        }

        private IEnumerator OnlineErrorToastSequence(bool resumeReconnectCountdownAfter)
        {
            if (_onlineErrorToastCanvasGroup == null)
            {
                yield break;
            }

            _onlineErrorToastCanvasGroup.gameObject.SetActive(true);
            _onlineErrorToastCanvasGroup.alpha = 0f;

            yield return FadeOnlineErrorToast(0f, 1f, OnlineErrorToastFadeInSeconds);
            yield return new WaitForSecondsRealtime(OnlineErrorToastVisibleSeconds);
            yield return FadeOnlineErrorToast(1f, 0f, OnlineErrorToastFadeOutSeconds);

            if (resumeReconnectCountdownAfter && TryResumeOpponentReconnectCountdown())
            {
                _onlineErrorToastCoroutine = null;
                yield break;
            }

            if (_onlineErrorToastCanvasGroup != null)
            {
                _onlineErrorToastCanvasGroup.alpha = 0f;
                _onlineErrorToastCanvasGroup.gameObject.SetActive(false);
            }

            _onlineErrorToastCoroutine = null;
        }

        private IEnumerator FadeOnlineErrorToast(float fromAlpha, float toAlpha, float durationSeconds)
        {
            if (_onlineErrorToastCanvasGroup == null)
            {
                yield break;
            }

            if (durationSeconds <= 0f)
            {
                _onlineErrorToastCanvasGroup.alpha = toAlpha;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < durationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / durationSeconds);
                _onlineErrorToastCanvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, progress);
                yield return null;
            }

            _onlineErrorToastCanvasGroup.alpha = toAlpha;
        }

        public void StartBattle()
        {
            if (_startWithDraftBeforeBattle && (_runtimePlayerDeckCardIdsOverride == null || _runtimePlayerDeckCardIdsOverride.Count == 0))
            {
                BeginDraft();
                return;
            }

            ConfigureLocalInput();
            CancelPresentationSequence();
            _battleScreenPresenter?.SetBattleUiVisible(true);
            _draftOverlayPresenter?.Hide();
            _hasQueuedDraftSceneReturn = false;
            ClearCombatLog();
            var resolvedConfig = ResolveStartConfig();
            _cardDefinitionProvider = resolvedConfig.CardDefinitionProvider ?? new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>());
            _aiDecisionService = new AiDecisionService(_cardDefinitionProvider);
            _aiTurnRunner = new AiTurnRunner(_aiDecisionService);

            _battleFlowController = CreateBattleFlowController(_cardDefinitionProvider);
            _battleGateway = new LocalBattleGateway(_battleFlowController);
            var firstPlayerId = _firstPlayerSelector.SelectFirstPlayer();
            _battleGateway.StartBattle(new BattleSetupRequest(
                playerDeckCardIds: resolvedConfig.PlayerDeckCardIds,
                aiDeckCardIds: resolvedConfig.AIDeckCardIds,
                firstPlayerId: firstPlayerId));

            AddCombatLogEntry(CombatLogFormatter.FormatBattleStarted(firstPlayerId));

            if (_autoPassPlayerMulligan && CurrentBattleState.Phase == PhaseType.Mulligan)
            {
                _battleGateway.PassMulligan(PlayerId.Player);
            }

            if (_autoResolveTurnStartAfterAutoPass && CurrentBattleState.Phase == PhaseType.TurnStart)
            {
                _battleGateway.ResolveTurnStart();
                AddCombatLogEntry(CombatLogFormatter.FormatTurnStartResolved(CurrentBattleState.ActivePlayerId, CurrentBattleState.TurnNumber));
            }

            _lastInteractionStatus = "Battle started.";
            RefreshPresenter();
            MaybeRunAiTurn();
        }

        [ContextMenu("Fill Debug Decks")]
        public void FillDebugDecks()
        {
            _playerDeckAsset = null;
            _aiDeckAsset = null;
            _playerDeckCardIds = BattleDebugDeckFactory.CreateDeck("P", _generatedDeckSize);
            _aiDeckCardIds = BattleDebugDeckFactory.CreateDeck("A", _generatedDeckSize);
        }

        public void PassPlayerMulligan()
        {
            SubmitPlayerMulligan(Array.Empty<string>());
        }

        public bool SubmitPlayerMulligan(IReadOnlyList<string> selectedCardIds)
        {
            if (!TryBeginImmediateInteraction())
            {
                return false;
            }

            EnsureBattleStarted();
            if (CurrentBattleState.Phase != PhaseType.Mulligan || CurrentBattleState.Player.HasUsedMulligan)
            {
                _lastInteractionStatus = "Mulligan has already been confirmed.";
                return false;
            }

            var replacements = selectedCardIds == null
                ? new List<string>()
                : new List<string>(selectedCardIds);

            if (ShouldRoutePlayerActionsOnline())
            {
                if (_onlineConnectionTester.TrySendMulligan(replacements, out var onlineMessage))
                {
                    _lastInteractionStatus = onlineMessage;
                    return true;
                }

                _lastInteractionStatus = onlineMessage;
                ShowCommandErrorToast(onlineMessage);
                return false;
            }

            if (replacements.Count == 0)
            {
                _battleGateway.PassMulligan(PlayerId.Player);
            }
            else
            {
                _battleGateway.ApplyMulligan(PlayerId.Player, replacements, new SystemDeckShuffler());
            }

            if (_autoResolveTurnStartAfterAutoPass && CurrentBattleState.Phase == PhaseType.TurnStart)
            {
                _battleGateway.ResolveTurnStart();
                AddCombatLogEntry(CombatLogFormatter.FormatTurnStartResolved(CurrentBattleState.ActivePlayerId, CurrentBattleState.TurnNumber));
            }

            RefreshPresenter();
            MaybeRunAiTurn();
            return true;
        }

        public void ResolveTurnStart()
        {
            if (!TryBeginImmediateInteraction())
            {
                return;
            }

            EnsureBattleStarted();
            var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
            _battleGateway.ResolveTurnStart();
            var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
            AddCombatLogEntry(CombatLogFormatter.FormatTurnStartResolved(CurrentBattleState.ActivePlayerId, CurrentBattleState.TurnNumber));
            var impactRequest = BuildImpactAnimationRequest(beforeSnapshot, afterSnapshot, CloneValuePopupEvents(CurrentBattleState));
            if (ShouldAnimateAttackSequences() && HasPresentationImpact(impactRequest))
            {
                StartPresentationSequence(PresentationOnlySequence(impactRequest));
                return;
            }

            RefreshPresenter();
            MaybeRunAiTurn();
        }

        public void EndTurn()
        {
            if (!TryBeginImmediateInteraction())
            {
                return;
            }

            EnsureBattleStarted();
            if (ShouldRoutePlayerActionsOnline())
            {
                if (_onlineConnectionTester.TrySendEndTurn(out var onlineMessage))
                {
                    AddCombatLogEntry(onlineMessage);
                }

                _lastInteractionStatus = onlineMessage;
                return;
            }

            var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
            AddCombatLogEntry(CombatLogFormatter.FormatEndTurn(CurrentBattleState.ActivePlayerId));
            _battleGateway.EndTurn();
            var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
            var impactRequest = BuildImpactAnimationRequest(beforeSnapshot, afterSnapshot, CloneValuePopupEvents(CurrentBattleState));
            if (ShouldAnimateAttackSequences() && HasPresentationImpact(impactRequest))
            {
                StartPresentationSequence(PlayerSpellImpactSequence(impactRequest));
                return;
            }

            RefreshPresenter();
            MaybeRunAiTurn();
        }

        public bool TrySurrenderPlayer(out string message)
        {
            var battleState = CurrentBattleState;
            if (battleState == null)
            {
                message = "진행 중인 전투가 없습니다.";
                ShowCommandErrorToast(message);
                return false;
            }

            if (battleState.IsEnded)
            {
                message = "이미 종료된 전투입니다.";
                ShowCommandErrorToast(message);
                return false;
            }

            if (ShouldRoutePlayerActionsOnline())
            {
                if (_onlineConnectionTester.TrySendSurrender(out message))
                {
                    _lastInteractionStatus = message;
                    return true;
                }

                ShowCommandErrorToast(message);
                return false;
            }

            battleState.EndBattle(PlayerId.AI);
            message = "항복했습니다.";
            _lastInteractionStatus = message;
            AddCombatLogEntry(message);
            RefreshPresenter();
            return true;
        }

        public bool TryMovePlayerOccupant(TileCoord fromCoord, TileCoord toCoord, out string message)
        {
            EnsureBattleStarted();

            if (IsBusy)
            {
                message = BusyMessage;
                _lastInteractionStatus = message;
                return false;
            }

            if (ShouldRoutePlayerActionsOnline())
            {
                return TrySendOnlinePlayerCommand(
                    new MoveOccupantCommand(fromCoord, toCoord),
                    $"Sent online move from {fromCoord} to {toCoord}.",
                    out message);
            }

            try
            {
                var isSwap = CurrentBattleState.PlayerBoard.GetOccupant(toCoord) != null;
                _battleGateway.ExecuteCommand(PlayerId.Player, new MoveOccupantCommand(fromCoord, toCoord));
                message = isSwap
                    ? CombatLogFormatter.FormatSwap(fromCoord, toCoord)
                    : $"Moved unit from {fromCoord} to {toCoord}.";
                _lastInteractionStatus = message;
                AddCombatLogEntry(message);
                RefreshPresenter();
                MaybeRunAiTurn();
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                _lastInteractionStatus = message;
                Debug.LogWarning($"Move interaction failed from {fromCoord} to {toCoord}: {ex.Message}");
                return false;
            }
        }

        public bool TryAttackWithPlayerOccupant(TileCoord attackerCoord, TileCoord targetCoord, out string message)
        {
            EnsureBattleStarted();

            if (IsBusy)
            {
                message = BusyMessage;
                _lastInteractionStatus = message;
                return false;
            }

            if (ShouldRoutePlayerActionsOnline())
            {
                return TrySendOnlinePlayerCommand(
                    new AttackCommand(attackerCoord, targetCoord),
                    $"Sent online attack from {attackerCoord} to {targetCoord}.",
                    out message);
            }

            try
            {
                var attackCommand = new AttackCommand(attackerCoord, targetCoord);
                if (!ShouldAnimateAttackSequences())
                {
                    _battleGateway.ExecuteCommand(PlayerId.Player, attackCommand);
                    message = $"Attacked from {attackerCoord} to {targetCoord}.";
                    _lastInteractionStatus = message;
                    AddCombatLogEntry(message);
                    RefreshPresenter();
                    MaybeRunAiTurn();
                    return true;
                }

                var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                var attackerIsRanged = IsRangedAttacker(CurrentBattleState, PlayerId.Player, attackerCoord);
                var attacker = CurrentBattleState.GetBoard(PlayerId.Player).GetOccupant(attackerCoord);
                var guardInfo = GuardService.ResolveForNormalAttack(
                    CurrentBattleState.GetOpponentBoard(PlayerId.Player),
                    targetCoord,
                    attacker);
                var defenderCounterattacks = CanDefenderCounterattack(
                    CurrentBattleState,
                    PlayerId.Player,
                    attackerCoord,
                    PlayerId.AI,
                    guardInfo);
                _battleGateway.ExecuteCommand(PlayerId.Player, attackCommand);
                var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                var valuePopupEvents = CloneValuePopupEvents(CurrentBattleState);
                var animationRequest = BuildAttackAnimationRequest(beforeSnapshot, afterSnapshot, PlayerId.Player, attackerCoord, PlayerId.AI, targetCoord, guardInfo, attackerIsRanged, defenderCounterattacks, valuePopupEvents);

                message = $"Attacked from {attackerCoord} to {targetCoord}.";
                _lastInteractionStatus = message;
                AddCombatLogEntry(message);
                StartPresentationSequence(PlayerAttackSequence(animationRequest));
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                _lastInteractionStatus = message;
                Debug.LogWarning($"Attack interaction failed from {attackerCoord} to {targetCoord}: {ex.Message}");
                return false;
            }
        }

        public bool TryUsePlayerCardOnTile(string cardId, PlayerId targetOwnerId, TileCoord targetCoord, out string message)
        {
            return TryUsePlayerCardOnTile(
                cardId,
                handCardRuntimeId: null,
                targetOwnerId,
                targetCoord,
                out message);
        }

        public bool TryUsePlayerCardOnTile(
            string cardId,
            string handCardRuntimeId,
            PlayerId targetOwnerId,
            TileCoord targetCoord,
            out string message)
        {
            EnsureBattleStarted();

            if (IsBusy)
            {
                return FailPlayerCardUse(BusyMessage, out message);
            }

            if (string.IsNullOrWhiteSpace(cardId))
            {
                return FailPlayerCardUse("No card is selected.", out message);
            }

            try
            {
                var definition = _cardDefinitionProvider.GetRequired(cardId);

                switch (definition.CardType)
                {
                    case CardType.Unit:
                        if (targetOwnerId != PlayerId.Player)
                        {
                            return FailPlayerCardUseSilently($"Card '{cardId}' must be placed on a player field tile.", out message);
                        }

                        var playUnitCommand = new PlayUnitCardCommand(
                            cardId,
                            targetCoord,
                            handCardRuntimeId);
                        if (ShouldRoutePlayerActionsOnline())
                        {
                            return TrySendOnlinePlayerCommand(
                                playUnitCommand,
                                $"Sent online play '{cardId}' to {targetCoord}.",
                                out message);
                        }

                        _battleGateway.ExecuteCommand(PlayerId.Player, playUnitCommand);
                        break;

                    case CardType.Building:
                        if (targetOwnerId != PlayerId.Player)
                        {
                            return FailPlayerCardUseSilently($"Card '{cardId}' must be placed on a player field tile.", out message);
                        }

                        var playBuildingCommand = new PlayBuildingCardCommand(
                            cardId,
                            targetCoord,
                            handCardRuntimeId);
                        if (ShouldRoutePlayerActionsOnline())
                        {
                            return TrySendOnlinePlayerCommand(
                                playBuildingCommand,
                                $"Sent online play '{cardId}' to {targetCoord}.",
                                out message);
                        }

                        _battleGateway.ExecuteCommand(PlayerId.Player, playBuildingCommand);
                        break;

                    case CardType.Spell:
                        if (definition is DamageSpellCardDefinition)
                        {
                            var castDamageCommand = new CastDamageSpellCommand(
                                cardId,
                                targetOwnerId,
                                targetCoord,
                                handCardRuntimeId);
                            if (ShouldRoutePlayerActionsOnline())
                            {
                                return TrySendOnlinePlayerCommand(
                                    castDamageCommand,
                                    $"Sent online cast '{cardId}' on {targetOwnerId} {targetCoord}.",
                                    out message);
                            }

                            if (!ShouldAnimateAttackSequences())
                            {
                                _battleGateway.ExecuteCommand(PlayerId.Player, castDamageCommand);
                                break;
                            }

                            var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                            _battleGateway.ExecuteCommand(PlayerId.Player, castDamageCommand);
                            var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                            var impactRequest = BuildImpactAnimationRequest(beforeSnapshot, afterSnapshot, CloneValuePopupEvents(CurrentBattleState), cardId);

                            message = $"Cast '{cardId}' on {targetOwnerId} {targetCoord}.";
                            _lastInteractionStatus = message;
                            AddCombatLogEntry(message);
                            StartPresentationSequence(PlayerSpellImpactSequence(impactRequest));
                            return true;
                        }

                        if (definition is ScriptedSpellCardDefinition scriptedSpellDefinition)
                        {
                            if (string.Equals(scriptedSpellDefinition.EffectId, "cheonra_jimang", StringComparison.Ordinal) ||
                                string.Equals(scriptedSpellDefinition.EffectId, "firewall", StringComparison.Ordinal))
                            {
                                var castScriptedTargetCommand = new CastScriptedSpellCommand(
                                    cardId,
                                    targetOwnerId,
                                    targetCoord,
                                    handCardRuntimeId);
                                if (ShouldRoutePlayerActionsOnline())
                                {
                                    return TrySendOnlinePlayerCommand(
                                        castScriptedTargetCommand,
                                        $"Sent online cast '{cardId}' on {targetOwnerId} {targetCoord}.",
                                        out message);
                                }

                                _battleGateway.ExecuteCommand(PlayerId.Player, castScriptedTargetCommand);
                                break;
                            }

                            return FailPlayerCardUseSilently($"Card '{cardId}' is a self-cast scripted spell. Drag it upward to cast it.", out message);
                        }

                        if (definition is PersistentResourceSpellCardDefinition)
                        {
                            return FailPlayerCardUseSilently($"Card '{cardId}' is a persistent spell. Drag it upward to cast it.", out message);
                        }

                        return FailPlayerCardUse($"Card '{cardId}' uses an unsupported spell type in the current slice.", out message);

                    default:
                        return FailPlayerCardUse($"Card '{cardId}' cannot be used on a board tile in the current slice.", out message);
                }

                message = definition.CardType == CardType.Spell
                    ? $"Cast '{cardId}' on {targetOwnerId} {targetCoord}."
                    : $"Played '{cardId}' to {targetCoord}.";
                _lastInteractionStatus = message;
                AddCombatLogEntry(message);
                RefreshPresenter();
                MaybeRunAiTurn();
                return true;
            }
            catch (Exception ex)
            {
                FailPlayerCardUse(ex.Message, out message);
                Debug.LogWarning($"Play card interaction failed for '{cardId}' at {targetCoord}: {ex.Message}");
                return false;
            }
        }

        public bool TryCastPlayerPersistentSpell(string cardId, out string message)
        {
            return TryCastPlayerPersistentSpell(cardId, handCardRuntimeId: null, out message);
        }

        public bool TryCastPlayerPersistentSpell(
            string cardId,
            string handCardRuntimeId,
            out string message)
        {
            EnsureBattleStarted();

            if (IsBusy)
            {
                return FailPlayerCardUse(BusyMessage, out message);
            }

            if (string.IsNullOrWhiteSpace(cardId))
            {
                return FailPlayerCardUse("No card is selected.", out message);
            }

            try
            {
                var definition = _cardDefinitionProvider.GetRequired(cardId);
                if (definition is PersistentResourceSpellCardDefinition)
                {
                    var castPersistentCommand = new CastPersistentResourceSpellCommand(
                        cardId,
                        handCardRuntimeId);
                    if (ShouldRoutePlayerActionsOnline())
                    {
                        return TrySendOnlinePlayerCommand(
                            castPersistentCommand,
                            $"Sent online cast persistent spell '{cardId}'.",
                            out message);
                    }

                    _battleGateway.ExecuteCommand(PlayerId.Player, castPersistentCommand);
                    message = $"Cast persistent spell '{cardId}'.";
                }
                else if (definition is ScriptedSpellCardDefinition scriptedSpellDefinition &&
                         (string.Equals(scriptedSpellDefinition.EffectId, "daehwandan", StringComparison.Ordinal) ||
                          string.Equals(scriptedSpellDefinition.EffectId, TimedBombRules.EffectId, StringComparison.Ordinal)))
                {
                    var castScriptedCommand = new CastScriptedSpellCommand(
                        cardId,
                        handCardRuntimeId);
                    if (ShouldRoutePlayerActionsOnline())
                    {
                        return TrySendOnlinePlayerCommand(
                            castScriptedCommand,
                            $"Sent online cast spell '{cardId}'.",
                            out message);
                    }

                    _battleGateway.ExecuteCommand(PlayerId.Player, castScriptedCommand);
                    message = $"Cast spell '{cardId}'.";
                }
                else
                {
                    return FailPlayerCardUse($"Card '{cardId}' is not a self-cast spell.", out message);
                }

                _lastInteractionStatus = message;
                AddCombatLogEntry(message);
                RefreshPresenter();
                MaybeRunAiTurn();
                return true;
            }
            catch (Exception ex)
            {
                FailPlayerCardUse(ex.Message, out message);
                Debug.LogWarning($"Cast persistent spell interaction failed for '{cardId}': {ex.Message}");
                return false;
            }
        }

        public bool TryBeginPlayerRobotFusion(string cardId, out string message)
        {
            return TryBeginPlayerRobotFusion(cardId, handCardRuntimeId: null, out message);
        }

        public bool TryBeginPlayerRobotFusion(
            string cardId,
            string handCardRuntimeId,
            out string message)
        {
            EnsureBattleStarted();

            if (IsBusy)
            {
                return FailPlayerCardUse(BusyMessage, out message);
            }

            if (string.IsNullOrWhiteSpace(cardId))
            {
                return FailPlayerCardUse("No card is selected.", out message);
            }

            try
            {
                var definition = _cardDefinitionProvider.GetRequired(cardId) as ScriptedSpellCardDefinition;
                if (definition == null ||
                    !string.Equals(definition.EffectId, RobotFusionRules.EffectId, StringComparison.Ordinal))
                {
                    return FailPlayerCardUse($"Card '{cardId}' is not Robot Fusion.", out message);
                }

                if (!RobotFusionRules.CanBegin(CurrentBattleState, PlayerId.Player))
                {
                    return FailPlayerCardUse("살아 있는 아군 로봇 유닛이 2기 이상 필요합니다.", out message);
                }

                var command = new CastScriptedSpellCommand(cardId, handCardRuntimeId);
                if (ShouldRoutePlayerActionsOnline())
                {
                    return TrySendOnlinePlayerCommand(
                        command,
                        "로봇 합체 대상을 선택하세요.",
                        out message);
                }

                _battleGateway.ExecuteCommand(PlayerId.Player, command);
                message = "로봇 합체 대상을 선택하세요.";
                _lastInteractionStatus = message;
                RefreshPresenter();
                return true;
            }
            catch (Exception ex)
            {
                FailPlayerCardUse(ex.Message, out message);
                Debug.LogWarning($"Begin Robot Fusion failed for '{cardId}': {ex.Message}");
                return false;
            }
        }

        public bool TryResolvePlayerRobotFusion(
            string cardId,
            IReadOnlyList<TileCoord> selectedCoords,
            out string message)
        {
            EnsureBattleStarted();

            if (IsBusy)
            {
                return FailPlayerCardUse(BusyMessage, out message);
            }

            if (selectedCoords == null || selectedCoords.Count < RobotFusionRules.MinimumRobotCount)
            {
                return FailPlayerCardUse("합체할 살아 있는 아군 로봇 유닛을 2기 이상 선택하세요.", out message);
            }

            try
            {
                var command = new CastScriptedSpellCommand(cardId, selectedCoords);
                if (ShouldRoutePlayerActionsOnline())
                {
                    return TrySendOnlinePlayerCommand(command, "로봇 합체 명령을 전송했습니다.", out message);
                }

                _battleGateway.ExecuteCommand(PlayerId.Player, command);
                message = "로봇 합체가 완료되었습니다.";
                _lastInteractionStatus = message;
                RefreshPresenter();
                return true;
            }
            catch (Exception ex)
            {
                FailPlayerCardUse(ex.Message, out message);
                Debug.LogWarning($"Resolve Robot Fusion failed for '{cardId}': {ex.Message}");
                return false;
            }
        }

        private void RefreshPresenter()
        {
            _battleScreenPresenter?.Present(CurrentBattleState);

            if (ShouldQueueDraftSceneReturn())
            {
                StartCoroutine(ReturnToDraftSceneAfterDelay());
            }
        }

        private BattleState GetCurrentBattleState()
        {
            if (_sendPlayerActionsToOnlineGateway &&
                _onlineConnectionTester != null &&
                _onlineConnectionTester.CurrentProjectedBattleState != null)
            {
                return _onlineConnectionTester.CurrentProjectedBattleState;
            }

            return _battleGateway?.CurrentBattleState;
        }

        private bool ShouldRoutePlayerActionsOnline()
        {
            if (!_sendPlayerActionsToOnlineGateway)
            {
                return false;
            }

            AutoAssignOnlineConnectionTester();
            return _onlineConnectionTester != null;
        }

        private bool TrySendOnlinePlayerCommand(IBattleCommand command, string successMessage, out string message)
        {
            if (!ShouldRoutePlayerActionsOnline())
            {
                message = string.Empty;
                return false;
            }

            if (_onlineConnectionTester.TrySendPlayerCommand(command, out var sendMessage))
            {
                message = string.IsNullOrWhiteSpace(successMessage) ? sendMessage : successMessage;
                _lastInteractionStatus = message;
                AddCombatLogEntry(message);
                return true;
            }

            message = sendMessage;
            ShowCommandErrorToast(message);
            return false;
        }

        private bool FailPlayerCardUse(string failureMessage, out string message)
        {
            message = string.IsNullOrWhiteSpace(failureMessage)
                ? "Card use failed."
                : failureMessage;
            ShowCommandErrorToast(message);
            return false;
        }

        private static bool FailPlayerCardUseSilently(string failureMessage, out string message)
        {
            message = string.IsNullOrWhiteSpace(failureMessage)
                ? "Card use failed."
                : failureMessage;
            return false;
        }

        private void AutoAssignOnlineConnectionTester()
        {
            if (_onlineConnectionTester != null || !_sendPlayerActionsToOnlineGateway)
            {
                return;
            }

            var testers = UnityEngine.Object.FindObjectsByType<OnlineBattleConnectionTester>(FindObjectsSortMode.None);
            foreach (var tester in testers)
            {
                if (tester != null && tester.PresentsStateViewsToBattleScreen)
                {
                    _onlineConnectionTester = tester;
                    return;
                }
            }

            if (testers.Length > 0)
            {
                _onlineConnectionTester = testers[0];
            }
        }

        private OnlineBattleConnectionTester ResolveOrCreateOnlineConnectionTester()
        {
            AutoAssignOnlineConnectionTester();
            if (_onlineConnectionTester != null)
            {
                return _onlineConnectionTester;
            }

            var testerObject = new GameObject("OnlineBattleConnectionTester_Player");
            testerObject.transform.SetParent(transform, false);
            _onlineConnectionTester = testerObject.AddComponent<OnlineBattleConnectionTester>();
            return _onlineConnectionTester;
        }

        private string BuildOnlineLaunchTrace(
            string modeLabel,
            string matchId,
            IReadOnlyList<string> playerDeckCardIds)
        {
            return
                $"mode={modeLabel} match={FormatLogValue(matchId)} server={FormatLogValue(OnlineServerUrl)} run={FormatLogValue(AccountSessionState.ActiveRunId)} deck={FormatLogValue(AccountSessionState.ActiveDeckId)} runStatus={FormatLogValue(AccountSessionState.ActiveRunStatus)} record={AccountSessionState.ActiveRunWins}-{AccountSessionState.ActiveRunLosses} deckCards={playerDeckCardIds?.Count ?? 0} serverDeckCards={AccountSessionState.ActiveDeckCardCount} upgradedCardKinds={CountUpgradedCardKinds(playerDeckCardIds)}";
        }

        private static string FormatShortRunDeckTrace()
        {
            return $"run={FormatLogValue(AccountSessionState.ActiveRunId)}, deck={FormatLogValue(AccountSessionState.ActiveDeckId)}";
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

        private string CreateOnlineDraftMatchId()
        {
            var prefix = string.IsNullOrWhiteSpace(_onlineMatchIdPrefix)
                ? "draft-run"
                : _onlineMatchIdPrefix.Trim();
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            return $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{suffix}";
        }

        private string CreateOnlineMatchmakingPlayerToken()
        {
            var baseToken = string.IsNullOrWhiteSpace(_onlinePlayerToken)
                ? "player"
                : _onlinePlayerToken.Trim();
            return $"{baseToken}-{OnlineClientTokenSuffix}";
        }

        private void MaybeRunAiTurn()
        {
            if (!_autoRunAiTurns || CurrentBattleState == null || CurrentBattleState.IsEnded || CurrentBattleState.ActivePlayerId != PlayerId.AI)
            {
                return;
            }

            if (ShouldAnimateAttackSequences() && _aiDecisionService != null)
            {
                StartPresentationSequence(RunAiTurnSequence());
                return;
            }

            if (_aiTurnRunner == null)
            {
                return;
            }

            _aiTurnRunner.RunAiTurn(_battleGateway, AddCombatLogEntry);

            if (_autoResolvePlayerTurnStartAfterAi &&
                CurrentBattleState != null &&
                !CurrentBattleState.IsEnded &&
                CurrentBattleState.ActivePlayerId == PlayerId.Player &&
                CurrentBattleState.Phase == PhaseType.TurnStart)
            {
                _battleGateway.ResolveTurnStart();
                AddCombatLogEntry(CombatLogFormatter.FormatTurnStartResolved(PlayerId.Player, CurrentBattleState.TurnNumber));
            }

            RefreshPresenter();
        }

        private void ClearCombatLog()
        {
            _combatLogEntries.Clear();
            _combatLogText = "전투 기록이 없습니다.";
            _onCombatLogChanged.Invoke(_combatLogText);
        }

        private void ReplaceCombatLogEntries(IReadOnlyList<BattleCombatLogEntryDto> entries)
        {
            _combatLogEntries.Clear();
            if (entries != null)
            {
                var firstIndex = Math.Max(0, entries.Count - MaxCombatLogEntries);
                for (var i = firstIndex; i < entries.Count; i++)
                {
                    var formattedEntry = BattleCombatLogEntryFormatter.Format(
                        entries[i],
                        ResolveCombatLogCardDisplayName);
                    if (!string.IsNullOrWhiteSpace(formattedEntry))
                    {
                        _combatLogEntries.Add(formattedEntry);
                    }
                }
            }

            _combatLogText = _combatLogEntries.Count == 0
                ? "전투 기록이 없습니다."
                : string.Join(Environment.NewLine, _combatLogEntries);
            _onCombatLogChanged.Invoke(_combatLogText);
        }

        private string ResolveCombatLogCardDisplayName(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return "카드";
            }

            if (string.Equals(cardId, "master", StringComparison.OrdinalIgnoreCase))
            {
                return "마스터";
            }

            if (TryGetCardDefinition(cardId, out var definition) &&
                !string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                return definition.DisplayName;
            }

            if (TryGetCardDefinitionAsset(cardId, out var cardAsset) &&
                !string.IsNullOrWhiteSpace(cardAsset.DisplayName))
            {
                return cardAsset.DisplayName;
            }

            return cardId;
        }

        private void AddCombatLogEntry(string entry)
        {
            // Online PVE/PVP logs come from the authoritative server StateView.
            if (_sendPlayerActionsToOnlineGateway)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(entry))
            {
                return;
            }

            _combatLogEntries.Add(entry);
            while (_combatLogEntries.Count > MaxCombatLogEntries)
            {
                _combatLogEntries.RemoveAt(0);
            }

            _combatLogText = string.Join(Environment.NewLine, _combatLogEntries);
            _onCombatLogChanged.Invoke(_combatLogText);
        }

        private bool TryBeginImmediateInteraction()
        {
            if (!IsBusy)
            {
                return true;
            }

            _lastInteractionStatus = BusyMessage;
            return false;
        }

        private void EnsureBattleStarted()
        {
            if (ShouldRoutePlayerActionsOnline())
            {
                return;
            }

            if (_battleGateway == null || CurrentBattleState == null)
            {
                throw new InvalidOperationException("Battle must be started before scene actions can be used.");
            }
        }

        private BattleBootstrapperResolvedConfig ResolveStartConfig()
        {
            var resolvedConfig = BattleBootstrapperConfigResolver.Resolve(
                _cardCatalogAsset,
                _playerDeckAsset,
                _aiDeckAsset,
                _playerDeckCardIds,
                _aiDeckCardIds,
                _useGeneratedDebugDecksWhenEmpty,
                _generatedDeckSize,
                _runtimePlayerDeckCardIdsOverride,
                _runtimeAIDeckCardIdsOverride,
                _useJsonCardDefinitions);

            if (resolvedConfig.PlayerDeckCardIds == null || resolvedConfig.PlayerDeckCardIds.Count == 0)
            {
                throw new InvalidOperationException("Player deck card ids must be configured before starting the battle.");
            }

            if (resolvedConfig.AIDeckCardIds == null || resolvedConfig.AIDeckCardIds.Count == 0)
            {
                throw new InvalidOperationException("AI deck card ids must be configured before starting the battle.");
            }

            return resolvedConfig;
        }

        private static BattleFlowController CreateBattleFlowController(ICardDefinitionProvider cardDefinitionProvider)
        {
            var provider = cardDefinitionProvider ?? new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>());
            var processor = new BattleCommandProcessor(
                new PlayCardService(provider),
                new SpellService(provider),
                new MoveService(),
                new AttackService(),
                new EndTurnService());

            return new BattleFlowController(
                new BattleSetupService(),
                new MulliganService(),
                new TurnStartService(provider),
                processor);
        }

        private bool ShouldAnimateAttackSequences()
        {
            return _animateAttackSequences &&
                   UnityEngine.Application.isPlaying &&
                   _battleScreenPresenter != null &&
                   _battleScreenPresenter.BoardPresenter != null;
        }

        private DraftOverlayPresenter ResolveDraftOverlayPresenter()
        {
            if (_draftOverlayPresenter != null)
            {
                _draftOverlayPresenter.Bind(this);
                return _draftOverlayPresenter;
            }

            _draftOverlayPresenter = GetComponent<DraftOverlayPresenter>();
            if (_draftOverlayPresenter == null)
            {
                var presenterObject = new GameObject("DraftOverlayPresenter");
                _draftOverlayPresenter = presenterObject.AddComponent<DraftOverlayPresenter>();
            }

            _draftOverlayPresenter.Bind(this);
            return _draftOverlayPresenter;
        }

        private System.Random CreateDraftRandom()
        {
            return _useFixedDraftSeed
                ? new System.Random(_fixedDraftSeed)
                : new System.Random();
        }

        private bool ShouldQueueDraftSceneReturn()
        {
            return !_hasQueuedDraftSceneReturn &&
                   _returnToDraftSceneWhenRunSessionActive &&
                   HasDraftRunContextForSceneReturn() &&
                   CurrentBattleState != null &&
                   CurrentBattleState.IsEnded &&
                   !string.IsNullOrWhiteSpace(DraftRunSessionState.DraftSceneName);
        }

        private static bool HasDraftRunContextForSceneReturn()
        {
            return DraftRunSessionState.HasDraftedDeckReady ||
                   AccountSessionState.HasSavedDraftDeck;
        }

        private IEnumerator ReturnToDraftSceneAfterDelay()
        {
            _hasQueuedDraftSceneReturn = true;

            if (_returnToDraftSceneDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(_returnToDraftSceneDelaySeconds);
            }

            if (CurrentBattleState == null || !CurrentBattleState.IsEnded)
            {
                _hasQueuedDraftSceneReturn = false;
                yield break;
            }

            var playerWon = CurrentBattleState.Result.Winner == PlayerId.Player;
            var syncedServerRunRecord = false;
            if (ShouldSyncServerRunRecordBeforeDraftReturn())
            {
                yield return TrySyncServerRunRecordBeforeDraftReturn(
                    succeeded => syncedServerRunRecord = succeeded);
            }

            if (!syncedServerRunRecord)
            {
                DraftRunSessionState.RecordBattleResult(playerWon);
            }

            Debug.Log(
                $"[After333 Battle Result] returningToDraft localResult={(playerWon ? "win" : "loss")} serverSynced={syncedServerRunRecord} run={FormatLogValue(AccountSessionState.ActiveRunId)} deck={FormatLogValue(AccountSessionState.ActiveDeckId)} localRecord={DraftRunSessionState.Wins}-{DraftRunSessionState.Losses} serverActiveRecord={AccountSessionState.ActiveRunWins}-{AccountSessionState.ActiveRunLosses} latestRecord={AccountSessionState.LatestRunWins}-{AccountSessionState.LatestRunLosses}");
            SceneManager.LoadScene(DraftRunSessionState.DraftSceneName);
        }

        private bool ShouldSyncServerRunRecordBeforeDraftReturn()
        {
            return ShouldRoutePlayerActionsOnline() &&
                   AccountSessionState.IsAuthenticated &&
                   !string.IsNullOrWhiteSpace(AccountSessionState.SessionToken);
        }

        private IEnumerator TrySyncServerRunRecordBeforeDraftReturn(Action<bool> completed)
        {
            using var cancellation = new CancellationTokenSource();
            var syncTask = SyncServerRunRecordBeforeDraftReturnAsync(cancellation.Token);
            var deadline = Time.unscaledTime + ServerRunResultSyncTimeoutSeconds;

            while (!syncTask.IsCompleted && Time.unscaledTime < deadline)
            {
                yield return null;
            }

            if (!syncTask.IsCompleted)
            {
                cancellation.Cancel();
                while (!syncTask.IsCompleted)
                {
                    yield return null;
                }

                Debug.LogWarning("[After333 Battle Result] server run sync timed out before returning to Draft. Falling back to local result cache.");
                completed?.Invoke(false);
                yield break;
            }

            if (syncTask.IsCanceled)
            {
                completed?.Invoke(false);
                yield break;
            }

            if (syncTask.IsFaulted)
            {
                var message = syncTask.Exception?.GetBaseException().Message ?? "unknown error";
                Debug.LogWarning($"[After333 Battle Result] server run sync failed before returning to Draft: {message}");
                completed?.Invoke(false);
                yield break;
            }

            completed?.Invoke(syncTask.Result);
        }

        private static async Task<bool> SyncServerRunRecordBeforeDraftReturnAsync(CancellationToken cancellationToken)
        {
            var client = new GuestAuthClient(
                Project333.Runtime.Presentation.Project333ServerEndpointSettings.ResolveHttpUrl(null));
            var response = await client.GetMeAsync(AccountSessionState.SessionToken, cancellationToken);
            AccountSessionState.ApplyMeResponse(response);

            if (AccountSessionState.HasResumableRun)
            {
                DraftRunSessionState.ApplyServerRunRecord(
                    AccountSessionState.ActiveRunWins,
                    AccountSessionState.ActiveRunLosses);
                return true;
            }

            if (AccountSessionState.HasLatestRun)
            {
                DraftRunSessionState.ApplyServerRunRecord(
                    AccountSessionState.LatestRunWins,
                    AccountSessionState.LatestRunLosses);
                return true;
            }

            return false;
        }

        private void StartPresentationSequence(IEnumerator sequence)
        {
            if (sequence == null)
            {
                return;
            }

            CancelPresentationSequence();
            _presentationSequenceCoroutine = StartCoroutine(RunPresentationSequence(sequence));
        }

        private void CancelPresentationSequence()
        {
            if (_presentationSequenceCoroutine != null)
            {
                StopCoroutine(_presentationSequenceCoroutine);
                _presentationSequenceCoroutine = null;
            }

            _isBusy = false;
        }

        private IEnumerator RunPresentationSequence(IEnumerator sequence)
        {
            _isBusy = true;
            yield return sequence;
            _presentationSequenceCoroutine = null;
            _isBusy = false;
            RefreshPresenter();
        }

        private IEnumerator PlayerAttackSequence(AttackAnimationRequest animationRequest)
        {
            yield return PlayAttackAnimationSequence(animationRequest);
            RefreshPresenter();

            if (_autoRunAiTurns && CurrentBattleState != null && !CurrentBattleState.IsEnded && CurrentBattleState.ActivePlayerId == PlayerId.AI)
            {
                yield return RunAiTurnSequence();
            }
        }

        private IEnumerator PlayerSpellImpactSequence(ImpactAnimationRequest impactRequest)
        {
            yield return PlayImpactAnimationSequence(impactRequest);
            RefreshPresenter();

            if (_autoRunAiTurns && CurrentBattleState != null && !CurrentBattleState.IsEnded && CurrentBattleState.ActivePlayerId == PlayerId.AI)
            {
                yield return RunAiTurnSequence();
            }
        }

        private IEnumerator PresentationOnlySequence(ImpactAnimationRequest impactRequest)
        {
            yield return PlayImpactAnimationSequence(impactRequest);
        }

        private IEnumerator RunAiTurnSequence()
        {
            while (CurrentBattleState != null && !CurrentBattleState.IsEnded && CurrentBattleState.ActivePlayerId == PlayerId.AI)
            {
                if (CurrentBattleState.Phase == PhaseType.TurnStart)
                {
                    var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    _battleGateway.ResolveTurnStart();
                    var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    AddCombatLogEntry(CombatLogFormatter.FormatTurnStartResolved(PlayerId.AI, CurrentBattleState.TurnNumber));
                    var impactRequest = BuildImpactAnimationRequest(beforeSnapshot, afterSnapshot, CloneValuePopupEvents(CurrentBattleState));
                    if (HasPresentationImpact(impactRequest))
                    {
                        yield return PlayImpactAnimationSequence(impactRequest);
                    }

                    RefreshPresenter();
                    yield return null;
                    continue;
                }

                if (CurrentBattleState.Phase != PhaseType.Main || _aiDecisionService == null)
                {
                    yield break;
                }

                var command = _aiDecisionService.GetNextCommand(CurrentBattleState);
                if (command is EndTurnCommand)
                {
                    var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    AddCombatLogEntry(CombatLogFormatter.FormatEndTurn(PlayerId.AI));
                    _battleGateway.EndTurn();
                    var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    var impactRequest = BuildImpactAnimationRequest(beforeSnapshot, afterSnapshot, CloneValuePopupEvents(CurrentBattleState));
                    if (HasPresentationImpact(impactRequest))
                    {
                        yield return PlayImpactAnimationSequence(impactRequest);
                    }

                    RefreshPresenter();
                    break;
                }

                if (command is AttackCommand attackCommand)
                {
                    var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    var attackerIsRanged = IsRangedAttacker(CurrentBattleState, PlayerId.AI, attackCommand.AttackerCoord);
                    var attacker = CurrentBattleState.GetBoard(PlayerId.AI).GetOccupant(attackCommand.AttackerCoord);
                    var guardInfo = GuardService.ResolveForNormalAttack(
                        CurrentBattleState.GetOpponentBoard(PlayerId.AI),
                        attackCommand.TargetCoord,
                        attacker);
                    var defenderCounterattacks = CanDefenderCounterattack(
                        CurrentBattleState,
                        PlayerId.AI,
                        attackCommand.AttackerCoord,
                        PlayerId.Player,
                        guardInfo);
                    _battleGateway.ExecuteCommand(PlayerId.AI, attackCommand);
                    AddCombatLogEntry(CombatLogFormatter.FormatCommand(PlayerId.AI, attackCommand));
                    var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    var valuePopupEvents = CloneValuePopupEvents(CurrentBattleState);
                    var animationRequest = BuildAttackAnimationRequest(beforeSnapshot, afterSnapshot, PlayerId.AI, attackCommand.AttackerCoord, PlayerId.Player, attackCommand.TargetCoord, guardInfo, attackerIsRanged, defenderCounterattacks, valuePopupEvents);
                    yield return PlayAttackAnimationSequence(animationRequest);
                    RefreshPresenter();
                    yield return null;
                    continue;
                }

                if (command is CastDamageSpellCommand castDamageSpellCommand)
                {
                    var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    _battleGateway.ExecuteCommand(PlayerId.AI, castDamageSpellCommand);
                    AddCombatLogEntry(CombatLogFormatter.FormatCommand(PlayerId.AI, castDamageSpellCommand));
                    var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    var impactRequest = BuildImpactAnimationRequest(beforeSnapshot, afterSnapshot, CloneValuePopupEvents(CurrentBattleState), castDamageSpellCommand.CardId);
                    yield return PlayImpactAnimationSequence(impactRequest);
                    RefreshPresenter();
                    yield return null;
                    continue;
                }

                _battleGateway.ExecuteCommand(PlayerId.AI, command);
                AddCombatLogEntry(CombatLogFormatter.FormatCommand(PlayerId.AI, command));
                RefreshPresenter();
                yield return null;
            }

            if (_autoResolvePlayerTurnStartAfterAi &&
                CurrentBattleState != null &&
                !CurrentBattleState.IsEnded &&
                CurrentBattleState.ActivePlayerId == PlayerId.Player &&
                CurrentBattleState.Phase == PhaseType.TurnStart)
            {
                var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                _battleGateway.ResolveTurnStart();
                var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                AddCombatLogEntry(CombatLogFormatter.FormatTurnStartResolved(PlayerId.Player, CurrentBattleState.TurnNumber));
                var impactRequest = BuildImpactAnimationRequest(beforeSnapshot, afterSnapshot, CloneValuePopupEvents(CurrentBattleState));
                if (HasPresentationImpact(impactRequest))
                {
                    yield return PlayImpactAnimationSequence(impactRequest);
                }

                RefreshPresenter();
            }
        }

        private float PlayOnlineAttackAnimation(BattleEventDto attackEvent, IReadOnlyList<BattleEventDto> battleEvents)
        {
            var request = BuildOnlineAttackAnimationRequest(attackEvent, battleEvents);
            if (request == null)
            {
                return 0f;
            }

            StartCoroutine(PlayAttackAnimationSequence(request));
            return EstimateAttackAnimationDuration(request);
        }

        private AttackAnimationRequest BuildOnlineAttackAnimationRequest(BattleEventDto attackEvent, IReadOnlyList<BattleEventDto> battleEvents)
        {
            if (attackEvent == null || attackEvent.SourceCoord == null || attackEvent.TargetCoord == null)
            {
                return null;
            }

            var attackerOwnerId = attackEvent.SourceOwnerId;
            var attackerCoord = attackEvent.SourceCoord.ToDomain();
            var defenderOwnerId = attackEvent.TargetOwnerId;
            var declaredTargetCoord = attackEvent.TargetCoord.ToDomain();
            var attacker = CurrentBattleState?.GetBoard(attackerOwnerId)?.GetOccupant(attackerCoord);
            if (attacker == null)
            {
                return null;
            }

            var request = new AttackAnimationRequest(
                attackerOwnerId,
                attackerCoord,
                defenderOwnerId,
                declaredTargetCoord,
                FindOnlineTravelTargetCoord(defenderOwnerId, declaredTargetCoord, battleEvents),
                attacker.AttackType == AttackType.Ranged);

            request.ValuePopupEvents.AddRange(BuildOnlineValuePopupEvents(battleEvents));
            foreach (var battleEvent in battleEvents)
            {
                if (battleEvent == null || battleEvent.TargetCoord == null)
                {
                    continue;
                }

                var targetOwnerId = battleEvent.TargetOwnerId;
                var targetCoord = battleEvent.TargetCoord.ToDomain();
                var targetsAttacker = IsSameTarget(targetOwnerId, targetCoord, attackerOwnerId, attackerCoord);

                switch (battleEvent.EventType)
                {
                    case BattleEventType.DamageApplied:
                        if (targetsAttacker)
                        {
                            request.AttackerTookDamage = true;
                            request.DefenderCounterattacked = true;
                        }
                        else
                        {
                            AddOrMergeImpact(request.DefenderImpacts, targetOwnerId, targetCoord, tookDamage: true, died: false);
                        }
                        break;

                    case BattleEventType.OccupantRemoved:
                        if (targetsAttacker)
                        {
                            request.AttackerDied = true;
                        }
                        else
                        {
                            AddOrMergeImpact(request.DefenderImpacts, targetOwnerId, targetCoord, tookDamage: false, died: true);
                        }
                        break;
                }
            }

            return request;
        }

        private float PlayOnlineImpactAnimation(
            string sourceCardId,
            BattleEventDto sourceEvent,
            IReadOnlyList<BattleEventDto> battleEvents)
        {
            var request = BuildOnlineImpactAnimationRequest(sourceCardId, sourceEvent, battleEvents);
            if (request == null || !HasPresentationImpact(request))
            {
                return 0f;
            }

            StartCoroutine(PlayImpactAnimationSequence(request));
            return EstimateImpactAnimationDuration(request);
        }

        private ImpactAnimationRequest BuildOnlineImpactAnimationRequest(
            string sourceCardId,
            BattleEventDto sourceEvent,
            IReadOnlyList<BattleEventDto> battleEvents)
        {
            var request = new ImpactAnimationRequest
            {
                SpellEffectId = ResolveDamageSpellEffectId(sourceCardId),
            };
            request.ValuePopupEvents.AddRange(BuildOnlineValuePopupEvents(battleEvents));

            foreach (var battleEvent in battleEvents)
            {
                if (battleEvent == null || battleEvent.TargetCoord == null)
                {
                    continue;
                }

                var ownerId = battleEvent.TargetOwnerId;
                var coord = battleEvent.TargetCoord.ToDomain();
                switch (battleEvent.EventType)
                {
                    case BattleEventType.DamageApplied:
                        AddOrMergeImpact(request.Impacts, ownerId, coord, tookDamage: true, died: false);
                        break;

                    case BattleEventType.OccupantRemoved:
                        AddOrMergeImpact(request.Impacts, ownerId, coord, tookDamage: false, died: true);
                        break;
                }
            }

            if (request.Impacts.Count == 0 &&
                sourceEvent != null &&
                sourceEvent.TargetCoord != null &&
                !string.IsNullOrWhiteSpace(request.SpellEffectId))
            {
                AddOrMergeImpact(
                    request.Impacts,
                    sourceEvent.TargetOwnerId,
                    sourceEvent.TargetCoord.ToDomain(),
                    tookDamage: true,
                    died: false);
            }

            return request;
        }

        private float PlayOnlineMoveAnimations(IReadOnlyList<BattleEventDto> battleEvents)
        {
            if (battleEvents == null)
            {
                return 0f;
            }

            var duration = 0f;
            foreach (var battleEvent in battleEvents)
            {
                if (battleEvent == null ||
                    battleEvent.EventType != BattleEventType.OccupantMoved ||
                    battleEvent.SourceCoord == null ||
                    battleEvent.TargetCoord == null)
                {
                    continue;
                }

                StartCoroutine(PlayOnlineMoveAnimationSequence(
                    battleEvent.SourceOwnerId,
                    battleEvent.SourceCoord.ToDomain(),
                    battleEvent.TargetOwnerId,
                    battleEvent.TargetCoord.ToDomain()));
                duration = Mathf.Max(duration, _attackRunDuration);
            }

            return duration;
        }

        private IEnumerator PlayOnlineMoveAnimationSequence(
            PlayerId sourceOwnerId,
            TileCoord sourceCoord,
            PlayerId targetOwnerId,
            TileCoord targetCoord)
        {
            var sourceView = FindTileTextView(sourceOwnerId, sourceCoord);
            var targetView = FindTileTextView(targetOwnerId, targetCoord);
            if (sourceView == null || targetView == null)
            {
                yield break;
            }

            sourceView.RestoreVisualLayout();
            targetView.RestoreVisualLayout();
            var sourcePosition = sourceView.GetVisualWorldPosition();
            var targetPosition = targetView.GetVisualWorldPosition();
            sourceView.PlayRunAnimation();
            yield return AnimateVisualTravel(sourceView, sourcePosition, targetPosition, _attackRunDuration);
        }

        private static BattleEventDto FindFirstBattleEvent(IReadOnlyList<BattleEventDto> battleEvents, BattleEventType eventType)
        {
            if (battleEvents == null)
            {
                return null;
            }

            foreach (var battleEvent in battleEvents)
            {
                if (battleEvent != null && battleEvent.EventType == eventType)
                {
                    return battleEvent;
                }
            }

            return null;
        }

        private static bool HasOnlineImpactEvents(IReadOnlyList<BattleEventDto> battleEvents)
        {
            if (battleEvents == null)
            {
                return false;
            }

            foreach (var battleEvent in battleEvents)
            {
                if (battleEvent == null)
                {
                    continue;
                }

                if (battleEvent.EventType == BattleEventType.DamageApplied ||
                    battleEvent.EventType == BattleEventType.HealingApplied ||
                    battleEvent.EventType == BattleEventType.OccupantRemoved)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<BattleValuePopupEvent> BuildOnlineValuePopupEvents(IReadOnlyList<BattleEventDto> battleEvents)
        {
            var valuePopupEvents = new List<BattleValuePopupEvent>();
            if (battleEvents == null)
            {
                return valuePopupEvents;
            }

            foreach (var battleEvent in battleEvents)
            {
                if (battleEvent == null ||
                    battleEvent.TargetCoord == null ||
                    (battleEvent.EventType != BattleEventType.DamageApplied &&
                     battleEvent.EventType != BattleEventType.HealingApplied &&
                     battleEvent.EventType != BattleEventType.InvinciblePrevented))
                {
                    continue;
                }

                valuePopupEvents.Add(new BattleValuePopupEvent(
                    battleEvent.RuntimeId,
                    battleEvent.TargetOwnerId,
                    battleEvent.TargetCoord.ToDomain(),
                    battleEvent.EventType == BattleEventType.HealingApplied,
                    battleEvent.Amount,
                    battleEvent.TargetOwnerId,
                    battleEvent.SourceRuntimeId,
                    battleEvent.SourceCardId,
                    battleEvent.ValueCause,
                    battleEvent.DamageType,
                    battleEvent.HpBefore,
                    battleEvent.HpAfter,
                    battleEvent.EventType == BattleEventType.InvinciblePrevented));
            }

            return valuePopupEvents;
        }

        private static void AddOrMergeImpact(
            List<AttackAnimationImpact> impacts,
            PlayerId ownerId,
            TileCoord coord,
            bool tookDamage,
            bool died)
        {
            if (impacts == null)
            {
                return;
            }

            for (var i = 0; i < impacts.Count; i++)
            {
                var existing = impacts[i];
                if (existing == null || !IsSameTarget(existing.OwnerId, existing.Coord, ownerId, coord))
                {
                    continue;
                }

                impacts[i] = new AttackAnimationImpact(
                    ownerId,
                    coord,
                    existing.TookDamage || tookDamage,
                    existing.Died || died);
                return;
            }

            impacts.Add(new AttackAnimationImpact(ownerId, coord, tookDamage, died));
        }

        private static bool IsSameTarget(PlayerId leftOwnerId, TileCoord leftCoord, PlayerId rightOwnerId, TileCoord rightCoord)
        {
            return leftOwnerId == rightOwnerId && leftCoord == rightCoord;
        }

        private static TileCoord FindOnlineTravelTargetCoord(
            PlayerId defenderOwnerId,
            TileCoord declaredTargetCoord,
            IReadOnlyList<BattleEventDto> battleEvents)
        {
            if (battleEvents == null)
            {
                return declaredTargetCoord;
            }

            foreach (var battleEvent in battleEvents)
            {
                if (battleEvent == null ||
                    battleEvent.TargetCoord == null ||
                    battleEvent.EventType != BattleEventType.DamageApplied ||
                    battleEvent.TargetOwnerId != defenderOwnerId)
                {
                    continue;
                }

                return battleEvent.TargetCoord.ToDomain();
            }

            return declaredTargetCoord;
        }

        private float EstimateAttackAnimationDuration(AttackAnimationRequest request)
        {
            if (request == null)
            {
                return 0f;
            }

            var attackerView = FindTileTextView(request.AttackerOwnerId, request.AttackerCoord);
            var travelDuration = request.IsRangedAttacker
                ? _rangedAttackNudgeDuration * 2f
                : _attackRunDuration + _attackReturnDuration;
            var attackDuration = attackerView == null
                ? _attackImpactDelay
                : attackerView.GetAttackAnimationDurationSeconds();
            var popupDuration = request.ValuePopupEvents.Count > 0
                ? (request.ValuePopupEvents.Count * Mathf.Max(0f, _valuePopupCascadeDelay)) + Mathf.Max(0f, _valuePopupCascadeDelay)
                : 0f;
            var deathDuration = (request.AttackerDied || HasAnyDeathImpact(request.DefenderImpacts))
                ? Mathf.Max(0f, _deathHoldDuration)
                : 0f;

            return travelDuration + attackDuration + popupDuration + Mathf.Max(0f, _attackReactionDuration) + deathDuration + 0.1f;
        }

        private float EstimateImpactAnimationDuration(ImpactAnimationRequest request)
        {
            if (request == null)
            {
                return 0f;
            }

            var spellDuration = string.IsNullOrWhiteSpace(request.SpellEffectId)
                ? 0f
                : 0.6f;
            var popupDuration = request.ValuePopupEvents.Count > 0
                ? (request.ValuePopupEvents.Count * Mathf.Max(0f, _valuePopupCascadeDelay)) + Mathf.Max(0f, _valuePopupCascadeDelay)
                : 0f;
            var deathDuration = HasAnyDeathImpact(request.Impacts)
                ? Mathf.Max(0f, _deathHoldDuration)
                : 0f;

            return spellDuration + popupDuration + Mathf.Max(0f, _attackReactionDuration) + deathDuration + 0.1f;
        }

        private static bool HasAnyDeathImpact(IReadOnlyList<AttackAnimationImpact> impacts)
        {
            if (impacts == null)
            {
                return false;
            }

            foreach (var impact in impacts)
            {
                if (impact != null && impact.Died)
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerator PlayAttackAnimationSequence(AttackAnimationRequest request)
        {
            if (request == null)
            {
                yield break;
            }

            var attackerView = FindTileTextView(request.AttackerOwnerId, request.AttackerCoord);
            if (attackerView == null)
            {
                yield break;
            }

            var destinationView = FindTileTextView(request.DefenderOwnerId, request.TravelTargetCoord) ??
                                  FindTileTextView(request.DefenderOwnerId, request.DeclaredTargetCoord);

            attackerView.RestoreVisualLayout();
            destinationView?.RestoreVisualLayout();
            var attackerStartPosition = attackerView.GetVisualWorldPosition();
            var attackDestination = destinationView != null
                ? destinationView.GetVisualWorldPosition()
                : attackerStartPosition;
            var attackAnimationDuration = attackerView.GetAttackAnimationDurationSeconds();
            var shouldTravelForAttack = !request.IsRangedAttacker;
            var meleeContactTarget = shouldTravelForAttack
                ? GetMeleeAttackContactTarget(destinationView, request.DefenderOwnerId, attackDestination, _meleeAttackContactOffset, _meleeAttackSideOffset)
                : attackerStartPosition;
            var rangedNudgeTarget = shouldTravelForAttack
                ? attackerStartPosition
                : GetRangedAttackNudgeTarget(
                    attackerStartPosition,
                    attackDestination,
                    _rangedAttackNudgeDistance * attackerView.GetResponsiveTileScale());

            if (shouldTravelForAttack)
            {
                attackerView.PlayRunAnimation();
                yield return AnimateVisualTravel(attackerView, attackerStartPosition, meleeContactTarget, _attackRunDuration);
            }
            else
            {
                attackerView.SetVisualWorldPosition(attackerStartPosition);
                yield return AnimateVisualTravel(attackerView, attackerStartPosition, rangedNudgeTarget, _rangedAttackNudgeDuration);
            }

            attackerView.PlayAttackAnimation();
            var attackImpactDelay = attackAnimationDuration > 0f
                ? Mathf.Min(_attackImpactDelay, attackAnimationDuration)
                : _attackImpactDelay;
            if (attackImpactDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(attackImpactDelay);
            }

            var defenderCounterattacked = request.DefenderCounterattacked && destinationView != null;
            var defenderCounterattackDuration = 0f;
            if (defenderCounterattacked)
            {
                destinationView.PlayAttackAnimation();
                defenderCounterattackDuration = destinationView.GetAttackAnimationDurationSeconds();
            }

            var remainingAttackDuration = Mathf.Max(0f, attackAnimationDuration - attackImpactDelay);
            var attackPhaseWaitDuration = Mathf.Max(remainingAttackDuration, defenderCounterattackDuration);
            if (attackPhaseWaitDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(attackPhaseWaitDuration);
            }

            var valuePopupCascadeDuration = QueueValuePopupAnimations(request.ValuePopupEvents);
            if (valuePopupCascadeDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(valuePopupCascadeDuration);
            }

            var reactionDuration = 0f;
            foreach (var impact in request.DefenderImpacts)
            {
                if (!impact.TookDamage)
                {
                    continue;
                }

                var impactView = FindTileTextView(impact.OwnerId, impact.Coord);
                impactView?.PlayBeAttackedAnimation();

                if (impactView != null)
                {
                    reactionDuration = Mathf.Max(reactionDuration, impactView.GetBeAttackedAnimationDurationSeconds());
                }
            }

            if (request.AttackerTookDamage)
            {
                attackerView.PlayBeAttackedAnimation();
                reactionDuration = Mathf.Max(reactionDuration, attackerView.GetBeAttackedAnimationDurationSeconds());
            }

            var reactionWaitDuration = Mathf.Max(_attackReactionDuration, reactionDuration);
            if (reactionWaitDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(reactionWaitDuration);
            }

            var hasAnyDeath = false;
            var deathDuration = 0f;
            foreach (var impact in request.DefenderImpacts)
            {
                var impactView = FindTileTextView(impact.OwnerId, impact.Coord);
                if (impactView == null)
                {
                    continue;
                }

                if (impact.Died)
                {
                    impactView.PlayDeathAnimation();
                    hasAnyDeath = true;
                    deathDuration = Mathf.Max(deathDuration, impactView.GetDeathAnimationDurationSeconds());
                }
                else if (impact.TookDamage)
                {
                    impactView.PlayIdleAnimation();
                }
            }

            if (request.AttackerDied)
            {
                attackerView.PlayDeathAnimation();
                hasAnyDeath = true;
                deathDuration = Mathf.Max(deathDuration, attackerView.GetDeathAnimationDurationSeconds());

                var deathWaitDuration = Mathf.Max(_deathHoldDuration, deathDuration);
                if (hasAnyDeath && deathWaitDuration > 0f)
                {
                    yield return new WaitForSecondsRealtime(deathWaitDuration);
                }

                yield break;
            }

            if (shouldTravelForAttack)
            {
                attackerView.PlayRunAnimation();
                yield return AnimateVisualTravel(attackerView, meleeContactTarget, attackerStartPosition, _attackReturnDuration);
            }
            else
            {
                yield return AnimateVisualTravel(attackerView, rangedNudgeTarget, attackerStartPosition, _rangedAttackNudgeDuration);
            }

            attackerView.RestoreVisualLayout();
            attackerView.PlayIdleAnimation();

            var finalDeathWaitDuration = Mathf.Max(_deathHoldDuration, deathDuration);
            if (hasAnyDeath && finalDeathWaitDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(finalDeathWaitDuration);
            }
        }

        private IEnumerator PlayImpactAnimationSequence(ImpactAnimationRequest request)
        {
            if (request == null || !HasPresentationImpact(request))
            {
                yield break;
            }

            yield return PlaySpellEffectAnimationSequence(request);

            var valuePopupCascadeDuration = QueueValuePopupAnimations(request.ValuePopupEvents);
            if (valuePopupCascadeDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(valuePopupCascadeDuration);
            }

            var reactionDuration = 0f;
            foreach (var impact in request.Impacts)
            {
                if (!impact.TookDamage)
                {
                    continue;
                }

                var impactView = FindTileTextView(impact.OwnerId, impact.Coord);
                impactView?.PlayBeAttackedAnimation();
                if (impactView != null)
                {
                    reactionDuration = Mathf.Max(reactionDuration, impactView.GetBeAttackedAnimationDurationSeconds());
                }
            }

            var reactionWaitDuration = Mathf.Max(_attackReactionDuration, reactionDuration);
            if (reactionWaitDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(reactionWaitDuration);
            }

            var hasAnyDeath = false;
            var deathDuration = 0f;
            foreach (var impact in request.Impacts)
            {
                var impactView = FindTileTextView(impact.OwnerId, impact.Coord);
                if (impactView == null)
                {
                    continue;
                }

                if (impact.Died)
                {
                    impactView.PlayDeathAnimation();
                    hasAnyDeath = true;
                    deathDuration = Mathf.Max(deathDuration, impactView.GetDeathAnimationDurationSeconds());
                }
                else if (impact.TookDamage)
                {
                    impactView.PlayIdleAnimation();
                }
            }

            var deathWaitDuration = Mathf.Max(_deathHoldDuration, deathDuration);
            if (hasAnyDeath && deathWaitDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(deathWaitDuration);
            }
        }

        private IEnumerator AnimateVisualTravel(TileTextView tileTextView, Vector3 fromWorldPosition, Vector3 toWorldPosition, float duration)
        {
            if (tileTextView == null)
            {
                yield break;
            }

            tileTextView.SetVisualWorldPosition(fromWorldPosition);
            if (duration <= 0f)
            {
                tileTextView.SetVisualWorldPosition(toWorldPosition);
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var easedProgress = Mathf.SmoothStep(0f, 1f, progress);
                tileTextView.SetVisualWorldPosition(Vector3.LerpUnclamped(fromWorldPosition, toWorldPosition, easedProgress));
                yield return null;
            }

            tileTextView.SetVisualWorldPosition(toWorldPosition);
        }

        private static Vector3 GetRangedAttackNudgeTarget(Vector3 attackerStartPosition, Vector3 attackDestination, float nudgeDistance)
        {
            if (nudgeDistance <= 0f)
            {
                return attackerStartPosition;
            }

            var direction = attackDestination - attackerStartPosition;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.up;
            }
            else
            {
                direction.Normalize();
            }

            return attackerStartPosition + (direction * nudgeDistance);
        }

        private static Vector3 GetMeleeAttackContactTarget(
            TileTextView destinationView,
            PlayerId defenderOwnerId,
            Vector3 fallbackDestination,
            float contactOffset,
            float sideOffset)
        {
            if (destinationView == null)
            {
                return fallbackDestination;
            }

            var localOffset = defenderOwnerId == PlayerId.AI
                ? new Vector2(-30f, 50f)
                : new Vector2(30f, 50f);

            return destinationView.GetTileAnchorWorldPosition(localOffset);
        }

        private TileTextView FindTileTextView(PlayerId ownerId, TileCoord coord)
        {
            var boardPresenter = _battleScreenPresenter == null ? null : _battleScreenPresenter.BoardPresenter;
            if (boardPresenter == null)
            {
                return null;
            }

            var tileViews = ownerId == PlayerId.Player
                ? boardPresenter.PlayerTileViews
                : boardPresenter.AITileViews;

            if (tileViews == null)
            {
                return null;
            }

            foreach (var tileView in tileViews)
            {
                if (tileView != null && tileView.Coord == coord)
                {
                    return tileView.GetComponent<TileTextView>();
                }
            }

            return null;
        }

        private static BattlePresentationSnapshot CaptureBattleSnapshot(BattleState battleState)
        {
            var snapshot = new BattlePresentationSnapshot();
            if (battleState == null)
            {
                return snapshot;
            }

            CaptureBoardSnapshots(snapshot, battleState.PlayerBoard);
            CaptureBoardSnapshots(snapshot, battleState.AIBoard);
            return snapshot;
        }

        private static List<BattleValuePopupEvent> CloneValuePopupEvents(BattleState battleState)
        {
            var clonedEvents = new List<BattleValuePopupEvent>();
            if (battleState?.ValuePopupEvents == null)
            {
                return clonedEvents;
            }

            foreach (var valuePopupEvent in battleState.ValuePopupEvents)
            {
                if (valuePopupEvent == null ||
                    (valuePopupEvent.Amount <= 0 && !valuePopupEvent.IsInvinciblePrevented))
                {
                    continue;
                }

                clonedEvents.Add(new BattleValuePopupEvent(
                    valuePopupEvent.RuntimeId,
                    valuePopupEvent.OwnerId,
                    valuePopupEvent.Coord,
                    valuePopupEvent.IsHealing,
                    valuePopupEvent.Amount,
                    valuePopupEvent.SourceOwnerId,
                    valuePopupEvent.SourceRuntimeId,
                    valuePopupEvent.SourceCardId,
                    valuePopupEvent.Cause,
                    valuePopupEvent.DamageType,
                    valuePopupEvent.HpBefore,
                    valuePopupEvent.HpAfter,
                    valuePopupEvent.IsInvinciblePrevented));
            }

            return clonedEvents;
        }

        private static void CaptureBoardSnapshots(BattlePresentationSnapshot snapshot, BoardState boardState)
        {
            if (snapshot == null || boardState == null)
            {
                return;
            }

            foreach (var occupant in boardState.EnumerateOccupants())
            {
                if (occupant == null)
                {
                    continue;
                }

                snapshot.Occupants.Add(new OccupantPresentationSnapshot(
                    occupant.RuntimeId,
                    occupant.OwnerId,
                    occupant.Position,
                    occupant.CurrentHp));
            }
        }

        private float QueueValuePopupAnimations(IReadOnlyList<BattleValuePopupEvent> valuePopupEvents)
        {
            if (valuePopupEvents == null || valuePopupEvents.Count == 0)
            {
                return 0f;
            }

            var perRuntimeIdDelaySteps = new Dictionary<string, int>(StringComparer.Ordinal);
            var maxScheduledDelay = 0f;
            var anyPopupQueued = false;

            foreach (var valuePopupEvent in valuePopupEvents)
            {
                if (valuePopupEvent == null ||
                    (valuePopupEvent.Amount <= 0 && !valuePopupEvent.IsInvinciblePrevented))
                {
                    continue;
                }

                var targetView = FindTileTextView(valuePopupEvent.OwnerId, valuePopupEvent.Coord);
                if (targetView == null)
                {
                    continue;
                }

                var runtimeId = string.IsNullOrWhiteSpace(valuePopupEvent.RuntimeId)
                    ? $"{valuePopupEvent.OwnerId}:{valuePopupEvent.Coord}"
                    : valuePopupEvent.RuntimeId;
                perRuntimeIdDelaySteps.TryGetValue(runtimeId, out var delayStepIndex);
                var scheduledDelay = delayStepIndex * Mathf.Max(0f, _valuePopupCascadeDelay);
                if (valuePopupEvent.IsInvinciblePrevented)
                {
                    targetView.QueueInvinciblePopup(scheduledDelay, delayStepIndex);
                }
                else
                {
                    targetView.QueueFloatingValuePopup(
                        valuePopupEvent.Amount,
                        valuePopupEvent.IsHealing,
                        scheduledDelay,
                        delayStepIndex);
                }
                perRuntimeIdDelaySteps[runtimeId] = delayStepIndex + 1;
                maxScheduledDelay = Mathf.Max(maxScheduledDelay, scheduledDelay);
                anyPopupQueued = true;
            }

            return anyPopupQueued ? maxScheduledDelay + Mathf.Max(0f, _valuePopupCascadeDelay) : 0f;
        }

        private static AttackAnimationRequest BuildAttackAnimationRequest(
            BattlePresentationSnapshot beforeSnapshot,
            BattlePresentationSnapshot afterSnapshot,
            PlayerId attackerOwnerId,
            TileCoord attackerCoord,
            PlayerId defenderOwnerId,
            TileCoord declaredTargetCoord,
            GuardService.GuardInfo guardInfo,
            bool attackerIsRanged,
            bool defenderCounterattacks,
            IReadOnlyList<BattleValuePopupEvent> valuePopupEvents)
        {
            if (beforeSnapshot == null)
            {
                return null;
            }

            var attackerBefore = beforeSnapshot.FindByCoord(attackerOwnerId, attackerCoord);
            if (attackerBefore == null)
            {
                return null;
            }

            var attackerAfter = afterSnapshot == null ? null : afterSnapshot.FindByRuntimeId(attackerBefore.RuntimeId);
            var request = new AttackAnimationRequest(
                attackerOwnerId,
                attackerCoord,
                defenderOwnerId,
                declaredTargetCoord,
                guardInfo.IsProtected && guardInfo.GuardCoord.HasValue
                    ? guardInfo.GuardCoord.Value
                    : declaredTargetCoord,
                attackerIsRanged)
            {
                DefenderCounterattacked = defenderCounterattacks,
                AttackerTookDamage = DidLoseHp(attackerBefore, attackerAfter),
                AttackerDied = DidDie(attackerBefore, attackerAfter),
            };
            request.ValuePopupEvents.AddRange(valuePopupEvents ?? Array.Empty<BattleValuePopupEvent>());

            AddImpactIfNeeded(request, beforeSnapshot, afterSnapshot, guardInfo.OriginalTarget.OwnerId, guardInfo.OriginalTargetCoord, guardInfo.OriginalTarget.RuntimeId);

            if (guardInfo.IsProtected && guardInfo.GuardCoord.HasValue && guardInfo.Guard != null)
            {
                AddImpactIfNeeded(request, beforeSnapshot, afterSnapshot, guardInfo.Guard.OwnerId, guardInfo.GuardCoord.Value, guardInfo.Guard.RuntimeId);
            }

            return request;
        }

        private static bool IsRangedAttacker(BattleState battleState, PlayerId ownerId, TileCoord attackerCoord)
        {
            if (battleState == null)
            {
                return false;
            }

            var attacker = battleState.GetBoard(ownerId)?.GetOccupant(attackerCoord);
            return attacker != null && attacker.AttackType == AttackType.Ranged;
        }

        private static ImpactAnimationRequest BuildImpactAnimationRequest(
            BattlePresentationSnapshot beforeSnapshot,
            BattlePresentationSnapshot afterSnapshot,
            IReadOnlyList<BattleValuePopupEvent> valuePopupEvents,
            string sourceCardId = null)
        {
            if (beforeSnapshot == null)
            {
                return null;
            }

            var request = new ImpactAnimationRequest
            {
                SpellEffectId = ResolveDamageSpellEffectId(sourceCardId),
            };
            request.ValuePopupEvents.AddRange(valuePopupEvents ?? Array.Empty<BattleValuePopupEvent>());
            foreach (var before in beforeSnapshot.Occupants)
            {
                if (before == null)
                {
                    continue;
                }

                var after = afterSnapshot == null ? null : afterSnapshot.FindByRuntimeId(before.RuntimeId);
                var tookDamage = DidLoseHp(before, after);
                var died = DidDie(before, after);
                if (!tookDamage && !died)
                {
                    continue;
                }

                request.Impacts.Add(new AttackAnimationImpact(before.OwnerId, before.Coord, tookDamage, died));
            }

            return request;
        }

        private static bool HasPresentationImpact(ImpactAnimationRequest request)
        {
            if (request == null)
            {
                return false;
            }

            return request.Impacts.Count > 0 ||
                   request.ValuePopupEvents.Count > 0 ||
                   !string.IsNullOrWhiteSpace(request.SpellEffectId);
        }

        private IEnumerator PlaySpellEffectAnimationSequence(ImpactAnimationRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.SpellEffectId))
            {
                yield break;
            }

            var effectFrames = GetSpellEffectFrames(request.SpellEffectId);
            if (effectFrames == null || effectFrames.Count == 0)
            {
                yield break;
            }

            var framesPerSecond = GetSpellEffectFramesPerSecond(request.SpellEffectId);
            var effectSize = GetSpellEffectSize(request.SpellEffectId);
            var effectDuration = 0f;

            foreach (var impact in request.Impacts)
            {
                if (!impact.TookDamage && !impact.Died)
                {
                    continue;
                }

                var impactView = FindTileTextView(impact.OwnerId, impact.Coord);
                if (impactView == null)
                {
                    continue;
                }

                effectDuration = Mathf.Max(
                    effectDuration,
                    impactView.PlayTransientSpriteEffect(effectFrames, framesPerSecond, effectSize));
            }

            if (effectDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(effectDuration);
            }
        }

        private static string ResolveDamageSpellEffectId(string sourceCardId)
        {
            if (string.IsNullOrWhiteSpace(sourceCardId))
            {
                return null;
            }

            return sourceCardId.IndexOf("firebolt", StringComparison.OrdinalIgnoreCase) >= 0
                ? FireboltSpellEffectId
                : null;
        }

        private static IReadOnlyList<Sprite> GetSpellEffectFrames(string spellEffectId)
        {
            if (string.Equals(spellEffectId, FireboltSpellEffectId, StringComparison.Ordinal))
            {
                return GetFireboltSpellEffectFrames();
            }

            return Array.Empty<Sprite>();
        }

        private static float GetSpellEffectFramesPerSecond(string spellEffectId)
        {
            return string.Equals(spellEffectId, FireboltSpellEffectId, StringComparison.Ordinal)
                ? FireboltSpellEffectFramesPerSecond
                : 12f;
        }

        private static Vector2 GetSpellEffectSize(string spellEffectId)
        {
            return string.Equals(spellEffectId, FireboltSpellEffectId, StringComparison.Ordinal)
                ? FireboltSpellEffectSize
                : new Vector2(180f, 180f);
        }

        private static IReadOnlyList<Sprite> GetFireboltSpellEffectFrames()
        {
            if (s_fireboltSpellEffectFrames != null && s_fireboltSpellEffectFrames.Count > 0)
            {
                return s_fireboltSpellEffectFrames;
            }

            var importedSprites = Resources.LoadAll<Sprite>(FireboltSpellEffectResourcePath);
            if (importedSprites != null && importedSprites.Length > 0)
            {
                Array.Sort(importedSprites, static (left, right) => string.CompareOrdinal(left.name, right.name));
                s_fireboltSpellEffectFrames = importedSprites;
                return s_fireboltSpellEffectFrames;
            }

            var texture = Resources.Load<Texture2D>(FireboltSpellEffectResourcePath);
            if (texture == null)
            {
                s_fireboltSpellEffectFrames = Array.Empty<Sprite>();
                return s_fireboltSpellEffectFrames;
            }

            var frameCount = Mathf.Max(1, FireboltSpellEffectFrameCount);
            var generatedSprites = new Sprite[frameCount];
            var frameWidth = texture.width / frameCount;
            var frameHeight = texture.height;
            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                var frameRect = new Rect(frameIndex * frameWidth, 0f, frameWidth, frameHeight);
                generatedSprites[frameIndex] = Sprite.Create(
                    texture,
                    frameRect,
                    new Vector2(0.5f, 0.5f),
                    frameHeight,
                    0,
                    SpriteMeshType.FullRect);
                generatedSprites[frameIndex].name = $"FireboltEffect_{frameIndex:D2}";
            }

            s_fireboltSpellEffectFrames = generatedSprites;
            return s_fireboltSpellEffectFrames;
        }

        private static bool CanDefenderCounterattack(
            BattleState battleState,
            PlayerId attackerOwnerId,
            TileCoord attackerCoord,
            PlayerId defenderOwnerId,
            GuardService.GuardInfo guardInfo)
        {
            if (battleState == null)
            {
                return false;
            }

            var attacker = battleState.GetBoard(attackerOwnerId)?.GetOccupant(attackerCoord);
            if (attacker == null || attacker.AttackType != AttackType.Melee)
            {
                return false;
            }

            var actualDefenderCoord = guardInfo.IsProtected && guardInfo.GuardCoord.HasValue
                ? guardInfo.GuardCoord.Value
                : guardInfo.OriginalTargetCoord;

            var defender = battleState.GetBoard(defenderOwnerId)?.GetOccupant(actualDefenderCoord);
            return defender != null &&
                   defender.AttackType == AttackType.Melee &&
                   !defender.CannotCounterattack &&
                   defender.Attack > 0;
        }

        private static void AddImpactIfNeeded(
            AttackAnimationRequest request,
            BattlePresentationSnapshot beforeSnapshot,
            BattlePresentationSnapshot afterSnapshot,
            PlayerId ownerId,
            TileCoord coord,
            string runtimeId)
        {
            if (request == null || beforeSnapshot == null || string.IsNullOrWhiteSpace(runtimeId))
            {
                return;
            }

            var before = beforeSnapshot.FindByRuntimeId(runtimeId) ?? beforeSnapshot.FindByCoord(ownerId, coord);
            if (before == null)
            {
                return;
            }

            var after = afterSnapshot == null ? null : afterSnapshot.FindByRuntimeId(runtimeId);
            var tookDamage = DidLoseHp(before, after);
            var died = DidDie(before, after);
            if (!tookDamage && !died)
            {
                return;
            }

            request.DefenderImpacts.Add(new AttackAnimationImpact(ownerId, coord, tookDamage, died));
        }

        private static bool DidLoseHp(OccupantPresentationSnapshot before, OccupantPresentationSnapshot after)
        {
            if (before == null)
            {
                return false;
            }

            var afterHp = after == null ? 0 : after.CurrentHp;
            return afterHp < before.CurrentHp;
        }

        private static bool DidDie(OccupantPresentationSnapshot before, OccupantPresentationSnapshot after)
        {
            return before != null && before.CurrentHp > 0 && (after == null || after.CurrentHp <= 0);
        }

        private sealed class BattlePresentationSnapshot
        {
            public List<OccupantPresentationSnapshot> Occupants { get; } = new List<OccupantPresentationSnapshot>();

            public OccupantPresentationSnapshot FindByRuntimeId(string runtimeId)
            {
                if (string.IsNullOrWhiteSpace(runtimeId))
                {
                    return null;
                }

                foreach (var occupant in Occupants)
                {
                    if (occupant.RuntimeId == runtimeId)
                    {
                        return occupant;
                    }
                }

                return null;
            }

            public OccupantPresentationSnapshot FindByCoord(PlayerId ownerId, TileCoord coord)
            {
                foreach (var occupant in Occupants)
                {
                    if (occupant.OwnerId == ownerId && occupant.Coord == coord)
                    {
                        return occupant;
                    }
                }

                return null;
            }
        }

        private sealed class OccupantPresentationSnapshot
        {
            public OccupantPresentationSnapshot(string runtimeId, PlayerId ownerId, TileCoord coord, int currentHp)
            {
                RuntimeId = runtimeId;
                OwnerId = ownerId;
                Coord = coord;
                CurrentHp = currentHp;
            }

            public string RuntimeId { get; }
            public PlayerId OwnerId { get; }
            public TileCoord Coord { get; }
            public int CurrentHp { get; }
        }

        private sealed class AttackAnimationRequest
        {
            public AttackAnimationRequest(
                PlayerId attackerOwnerId,
                TileCoord attackerCoord,
                PlayerId defenderOwnerId,
                TileCoord declaredTargetCoord,
                TileCoord travelTargetCoord,
                bool isRangedAttacker)
            {
                AttackerOwnerId = attackerOwnerId;
                AttackerCoord = attackerCoord;
                DefenderOwnerId = defenderOwnerId;
                DeclaredTargetCoord = declaredTargetCoord;
                TravelTargetCoord = travelTargetCoord;
                IsRangedAttacker = isRangedAttacker;
                DefenderImpacts = new List<AttackAnimationImpact>();
                ValuePopupEvents = new List<BattleValuePopupEvent>();
            }

            public PlayerId AttackerOwnerId { get; }
            public TileCoord AttackerCoord { get; }
            public PlayerId DefenderOwnerId { get; }
            public TileCoord DeclaredTargetCoord { get; }
            public TileCoord TravelTargetCoord { get; }
            public bool IsRangedAttacker { get; }
            public bool DefenderCounterattacked { get; set; }
            public bool AttackerTookDamage { get; set; }
            public bool AttackerDied { get; set; }
            public List<AttackAnimationImpact> DefenderImpacts { get; }
            public List<BattleValuePopupEvent> ValuePopupEvents { get; }
        }

        private sealed class ImpactAnimationRequest
        {
            public List<AttackAnimationImpact> Impacts { get; } = new List<AttackAnimationImpact>();
            public List<BattleValuePopupEvent> ValuePopupEvents { get; } = new List<BattleValuePopupEvent>();

            public string SpellEffectId { get; set; }
        }

        private sealed class AttackAnimationImpact
        {
            public AttackAnimationImpact(PlayerId ownerId, TileCoord coord, bool tookDamage, bool died)
            {
                OwnerId = ownerId;
                Coord = coord;
                TookDamage = tookDamage;
                Died = died;
            }

            public PlayerId OwnerId { get; }
            public TileCoord Coord { get; }
            public bool TookDamage { get; }
            public bool Died { get; }
        }

        private sealed class OpponentPlayedCardRevealRequest
        {
            public OpponentPlayedCardRevealRequest(string cardId, int cardAttack, int cardMaxHp)
            {
                CardId = cardId ?? string.Empty;
                CardAttack = Mathf.Max(0, cardAttack);
                CardMaxHp = Mathf.Max(0, cardMaxHp);
            }

            public string CardId { get; }
            public int CardAttack { get; }
            public int CardMaxHp { get; }
        }
    }
}
