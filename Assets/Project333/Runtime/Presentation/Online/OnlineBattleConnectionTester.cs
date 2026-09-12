using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Presentation.Battle;
using UnityEngine;

namespace Project333.Runtime.Presentation.Online
{
    public sealed class OnlineBattleConnectionTester : MonoBehaviour
    {
        private const float MinimumAutomaticReconnectWindowSeconds = 60f;
        private const float ServerAiBoardCardStateWaitSeconds = 2f;
        private const int MaxCombatLogEntries = 33;

        [SerializeField] private string _serverUrl = "ws://127.0.0.1:7333/battle";
        [SerializeField] private string _clientVersion =
            Project333.Runtime.Presentation.Project333ClientBuildInfo.CurrentClientVersion;
        [SerializeField] private string _matchId = "local-test";
        [SerializeField] private string _playerToken = "player-a";
        [SerializeField] private PlayerId _localPlayerId = PlayerId.Player;
        [SerializeField] private OnlineBattleSeatId _localOnlineSeatId = OnlineBattleSeatId.None;
        [SerializeField] private bool _connectOnStart;
        [SerializeField] private bool _presentStateViewsToBattleScreen = true;
        [SerializeField] private float _connectTimeoutSeconds = 5f;
        [Header("Automatic Battle Reconnect")]
        [SerializeField] private bool _automaticallyReconnectDuringBattle = true;
        [SerializeField] [Min(1f)] private float _automaticReconnectWindowSeconds = 60f;
        [SerializeField] [Min(0.1f)] private float _automaticReconnectInitialDelaySeconds = 0.5f;
        [SerializeField] [Min(0.1f)] private float _automaticReconnectMaximumDelaySeconds = 5f;
        [SerializeField] [Min(1f)] private float _automaticReconnectJoinTimeoutSeconds = 6f;
        [SerializeField] [Min(1f)] private float _clientHeartbeatIntervalSeconds = 3f;
        [SerializeField] private BattleScreenPresenter _battleScreenPresenter;
        [SerializeField] private BattleBootstrapper _battleBootstrapper;
        [TextArea(3, 8)]
        [SerializeField] private string _status = "Disconnected.";
        [SerializeField] private int _receivedMessageCount;
        [SerializeField] private int _receivedErrorCount;
        [SerializeField] private bool _hasAssignedSeat;

        private readonly object _queueLock = new object();
        private readonly Queue<QueuedOnlineBattleEnvelope> _receivedMessages = new Queue<QueuedOnlineBattleEnvelope>();
        private readonly Queue<string> _receivedErrors = new Queue<string>();
        private readonly Queue<string> _connectionClosedReasons = new Queue<string>();
        private readonly Queue<OnlineBattleEnvelope> _presentationMessages = new Queue<OnlineBattleEnvelope>();
        private readonly List<string> _joinPlayerDeckCardIds = new List<string>();
        private readonly List<BattleCombatLogEntryDto> _combatLogEntries =
            new List<BattleCombatLogEntryDto>();
        private string _joinRunId = string.Empty;
        private string _joinDeckId = string.Empty;
        private bool _joinUseServerAiOpponent = true;
        private bool _joinUseMatchmakingQueue;

        private WebSocketBattleMessageSender _messageSender;
        private OnlineBattleGateway _gateway;
        private BattleState _currentProjectedBattleState;
        private BattleStateViewDto _latestStateView;
        private BattleStateViewDto _pendingStateView;
        private float _deferStateViewUntilRealtime;
        private Coroutine _presentationQueueCoroutine;
        private bool _hasReceivedFirstStateView;
        private string _assignedConnectionId = string.Empty;
        private bool _hasServerClockOffset;
        private long _serverClockOffsetMilliseconds;
        private NetworkReachability _lastInternetReachability;
        private bool _isDestroying;
        private bool _isAutomaticReconnectActive;
        private bool _isAutomaticReconnectAttemptInProgress;
        private bool _receivedLocalReconnectCancellation;
        private float _automaticReconnectDeadlineRealtime;
        private float _nextAutomaticReconnectAttemptRealtime;
        private float _automaticReconnectJoinDeadlineRealtime;
        private int _automaticReconnectAttemptCount;
        private int _automaticReconnectGeneration;
        private int _lastPresentedAutomaticReconnectSeconds = -1;
        private float _nextClientHeartbeatRealtime;
        private string _automaticReconnectPreviousConnectionId = string.Empty;
        private string _matchmakingFailureMessage = string.Empty;
        private long _latestCombatLogSequence;

        private string ServerUrl =>
            Project333.Runtime.Presentation.Project333ServerEndpointSettings.ResolveBattleWebSocketUrl(_serverUrl);

        private string ResolvedClientVersion =>
            Project333.Runtime.Presentation.Project333ClientBuildInfo.ResolveClientVersion(_clientVersion);

        public string Status => _status;

        public PlayerId LocalPlayerId => _localPlayerId;

        public OnlineBattleSeatId LocalOnlineSeatId => _localOnlineSeatId;

        public bool PresentsStateViewsToBattleScreen => _presentStateViewsToBattleScreen;

        public bool IsReadyForCommands => _gateway != null &&
                                          _messageSender != null &&
                                          _messageSender.IsConnected &&
                                          _hasAssignedSeat;

        public bool HasAuthoritativeBattleState =>
            _hasReceivedFirstStateView && _currentProjectedBattleState != null;

        public bool HasActiveAuthoritativeBattle =>
            HasAuthoritativeBattleState && !_currentProjectedBattleState.IsEnded;

        public bool IsTransportConnected => _messageSender != null && _messageSender.IsConnected;

        public string MatchmakingFailureMessage => _matchmakingFailureMessage;

        public string CurrentMatchId => _matchId;

        public BattleState CurrentProjectedBattleState => _currentProjectedBattleState;

        public BattleStateViewDto LatestStateView => _latestStateView;

        public IReadOnlyList<BattleCombatLogEntryDto> CombatLogEntries => _combatLogEntries;

        private void Awake()
        {
            _lastInternetReachability = UnityEngine.Application.internetReachability;
            AutoAssignBattleScreenPresenter();
            AutoAssignBattleBootstrapper();
        }

        public void ConfigureForLocalTest(
            string serverUrl,
            string matchId,
            string playerToken,
            PlayerId localPlayerId,
            bool presentStateViewsToBattleScreen = true)
        {
            _serverUrl = string.IsNullOrWhiteSpace(serverUrl) ? _serverUrl : serverUrl;
            _matchId = string.IsNullOrWhiteSpace(matchId) ? _matchId : matchId;
            _playerToken = string.IsNullOrWhiteSpace(playerToken) ? _playerToken : playerToken;
            _localPlayerId = localPlayerId;
            _localOnlineSeatId = OnlineBattleSeatId.None;
            _presentStateViewsToBattleScreen = presentStateViewsToBattleScreen;
            _hasAssignedSeat = false;
            _assignedConnectionId = string.Empty;
        }

        private async void Start()
        {
            if (_connectOnStart)
            {
                await ConnectAsync();
            }
        }

        private void Update()
        {
            DrainQueuedMessages();
            DetectMobileNetworkTransition();
            MonitorAutomaticReconnect();
            SendClientHeartbeatIfNeeded();
        }

        private async void OnDestroy()
        {
            _isDestroying = true;
            StopAutomaticReconnect(hideStatus: true);
            await DisconnectAsync();
        }

        [ContextMenu("Connect")]
        public async void Connect()
        {
            if (!EnsurePlayMode("connect to the PvP test server"))
            {
                return;
            }

            StopAutomaticReconnect(hideStatus: true);
            SetJoinPlayerDeck(null);
            SetJoinRunDeckMetadata(null, null);
            ConfigureJoinMode(useServerAiOpponent: true, useMatchmakingQueue: false);
            ResetPresentationStateForNewConnection();
            await ConnectAsync();
        }

        public async void ConnectForServerAiBattle(
            string serverUrl,
            string matchId,
            string playerToken,
            IReadOnlyList<string> playerDeckCardIds)
        {
            if (!EnsurePlayMode("connect to the PvP server AI battle"))
            {
                return;
            }

            StopAutomaticReconnect(hideStatus: true);
            ConfigureForLocalTest(
                serverUrl,
                matchId,
                playerToken,
                PlayerId.Player,
                presentStateViewsToBattleScreen: true);
            SetJoinPlayerDeck(playerDeckCardIds);
            SetJoinRunDeckMetadata(AccountSessionState.ActiveRunId, AccountSessionState.ActiveDeckId);
            ConfigureJoinMode(useServerAiOpponent: true, useMatchmakingQueue: false);
            _currentProjectedBattleState = null;
            ResetPresentationStateForNewConnection();
            await DisconnectAsync();
            await ConnectAsync();
        }

        public async void ConnectForPvpMatchmaking(
            string serverUrl,
            string playerToken,
            IReadOnlyList<string> playerDeckCardIds,
            bool presentStateViewsToBattleScreen = true)
        {
            if (!EnsurePlayMode("connect to PvP matchmaking"))
            {
                return;
            }

            StopAutomaticReconnect(hideStatus: true);
            ConfigureForLocalTest(
                serverUrl,
                "pvp-matchmaking",
                playerToken,
                PlayerId.Player,
                presentStateViewsToBattleScreen);
            SetJoinPlayerDeck(playerDeckCardIds);
            SetJoinRunDeckMetadata(AccountSessionState.ActiveRunId, AccountSessionState.ActiveDeckId);
            ConfigureJoinMode(useServerAiOpponent: false, useMatchmakingQueue: true);
            _currentProjectedBattleState = null;
            _matchmakingFailureMessage = string.Empty;
            ResetPresentationStateForNewConnection();
            await DisconnectAsync();
            await ConnectAsync();
        }

        [ContextMenu("Send End Turn Test Command")]
        public void SendEndTurnTestCommand()
        {
            SendTestCommand("EndTurn", gateway => gateway.EndTurn());
        }

        [ContextMenu("Send Play Goblin Test Command")]
        public void SendPlayGoblinTestCommand()
        {
            SendTestCommand(
                "PlayUnitCard Goblin",
                gateway => gateway.ExecuteCommand(
                    PlayerId.Player,
                    new PlayUnitCardCommand("Goblin", new TileCoord(0, 0))));
        }

        [ContextMenu("Send Move Goblin Test Command")]
        public void SendMoveGoblinTestCommand()
        {
            SendTestCommand(
                "MoveOccupant Goblin",
                gateway => gateway.ExecuteCommand(
                    PlayerId.Player,
                    new MoveOccupantCommand(new TileCoord(0, 0), new TileCoord(1, 0))));
        }

        [ContextMenu("Send Master Attack Test Command")]
        public void SendMasterAttackTestCommand()
        {
            SendTestCommand(
                "Attack enemy Master",
                gateway => gateway.ExecuteCommand(
                    PlayerId.Player,
                    new AttackCommand(new TileCoord(2, 1), new TileCoord(2, 1))));
        }

        [ContextMenu("Send Firebolt Enemy Master Test Command")]
        public void SendFireboltEnemyMasterTestCommand()
        {
            SendTestCommand(
                "Cast Firebolt on enemy Master",
                gateway => gateway.ExecuteCommand(
                    PlayerId.Player,
                    new CastDamageSpellCommand("firebolt", PlayerId.AI, new TileCoord(2, 1))));
        }

        [ContextMenu("Disconnect")]
        public async void Disconnect()
        {
            await DisconnectAsync();
        }

        public async System.Threading.Tasks.Task DisconnectFromServerAsync()
        {
            await DisconnectAsync();
        }

        [ContextMenu("Drive Battle Screen From This Client")]
        public void DriveBattleScreenFromThisClient()
        {
            if (!EnsurePlayMode("drive the battle screen from this online client"))
            {
                return;
            }

            AutoAssignBattleBootstrapper();
            if (_battleBootstrapper == null)
            {
                SetStatus("Cannot drive battle screen because BattleBootstrapper was not found.");
                return;
            }

            _presentStateViewsToBattleScreen = true;
            _battleBootstrapper.ConfigureOnlineInput(this, sendPlayerActionsToOnlineGateway: true);
            PresentProjectedStateToBattleScreen();
            SetStatus($"Driving battle screen from this client as {_playerToken}/{FormatLocalSeatLabel()}.");
        }

        public void DetachBattlePresentation()
        {
            _presentStateViewsToBattleScreen = false;
            _battleBootstrapper = null;
            _battleScreenPresenter = null;
        }

        public void AttachToBattlePresentation(BattleBootstrapper battleBootstrapper)
        {
            if (battleBootstrapper == null)
            {
                throw new ArgumentNullException(nameof(battleBootstrapper));
            }

            _battleBootstrapper = battleBootstrapper;
            _battleScreenPresenter = null;
            _presentStateViewsToBattleScreen = true;
            battleBootstrapper.ConfigureOnlineInput(this, sendPlayerActionsToOnlineGateway: true);
            PresentProjectedStateToBattleScreen();
            UpdateOnlineTurnTimer(_latestStateView);
        }

        public bool TrySendPlayerCommand(IBattleCommand command, out string message)
        {
            if (command == null)
            {
                message = "Cannot send an empty online battle command.";
                SetStatus(message);
                return false;
            }

            if (!IsReadyForCommands)
            {
                message = "Cannot send online battle command until this client is connected and seated.";
                SetStatus(message);
                return false;
            }

            try
            {
                _gateway.ExecuteCommand(PlayerId.Player, command);
                message = $"Sent online {command.GetType().Name}.";
                SetStatus(message);
                return true;
            }
            catch (Exception ex)
            {
                message = $"Online command send failed: {ex.Message}";
                SetStatus(message);
                return false;
            }
        }

        public bool TrySendEndTurn(out string message)
        {
            if (!IsReadyForCommands)
            {
                message = "Cannot send online end turn until this client is connected and seated.";
                SetStatus(message);
                return false;
            }

            try
            {
                _gateway.EndTurn();
                message = "Sent online EndTurn.";
                SetStatus(message);
                return true;
            }
            catch (Exception ex)
            {
                message = $"Online end turn send failed: {ex.Message}";
                SetStatus(message);
                return false;
            }
        }

        public bool TrySendSurrender(out string message)
        {
            if (!IsReadyForCommands)
            {
                message = "항복하려면 서버 연결과 좌석 배정이 완료되어야 합니다.";
                SetStatus(message);
                return false;
            }

            try
            {
                _gateway.SendSurrender();
                message = "항복 요청을 서버에 전송했습니다.";
                SetStatus(message);
                return true;
            }
            catch (Exception ex)
            {
                message = $"항복 요청 전송 실패: {ex.Message}";
                SetStatus(message);
                return false;
            }
        }

        public bool TrySendMulligan(IReadOnlyList<string> selectedCardIds, out string message)
        {
            if (!IsReadyForCommands)
            {
                message = "멀리건을 확정하려면 서버 연결과 좌석 배정이 완료되어야 합니다.";
                SetStatus(message);
                return false;
            }

            try
            {
                if (selectedCardIds == null || selectedCardIds.Count == 0)
                {
                    _gateway.PassMulligan(PlayerId.Player);
                    message = "현재 손패 유지 결정을 서버에 전송했습니다.";
                }
                else
                {
                    _gateway.ApplyMulligan(
                        PlayerId.Player,
                        selectedCardIds,
                        new SystemDeckShuffler());
                    message = $"카드 {selectedCardIds.Count}장의 멀리건 결정을 서버에 전송했습니다.";
                }

                SetStatus(message);
                return true;
            }
            catch (Exception ex)
            {
                message = $"멀리건 전송에 실패했습니다: {ex.Message}";
                SetStatus(message);
                return false;
            }
        }

        private async System.Threading.Tasks.Task ConnectAsync()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                return;
            }

            if (_messageSender != null && _messageSender.IsConnected)
            {
                SetStatus("Already connected.");
                return;
            }

            if (_messageSender != null)
            {
                CleanupSender();
            }

            try
            {
                var sessionToken = AccountSessionState.SessionToken ?? string.Empty;
                var accountId = AccountSessionState.AccountId ?? string.Empty;
                if (_joinUseMatchmakingQueue && string.IsNullOrWhiteSpace(sessionToken))
                {
                    const string authRequiredMessage = "PVP matchmaking requires a server account login.";
                    _matchmakingFailureMessage = authRequiredMessage;
                    SetStatus(authRequiredMessage);
                    ShowOnlineErrorToast(authRequiredMessage);
                    return;
                }

                var resolvedServerUrl = ServerUrl;
                var resolvedClientVersion = ResolvedClientVersion;
                _messageSender = new WebSocketBattleMessageSender(resolvedServerUrl, resolvedClientVersion);
                _messageSender.MessageReceived += QueueMessage;
                _messageSender.ErrorReceived += QueueError;
                _messageSender.ConnectionClosed += QueueConnectionClosed;
                _gateway = new OnlineBattleGateway(
                    _matchId,
                    _playerToken,
                    _localPlayerId,
                    _messageSender,
                    sessionToken,
                    accountId);

                SetStatus($"Connecting to {resolvedServerUrl}...");
                PresentConnectionProgress($"서버에 연결 중입니다. {resolvedServerUrl}");

                using (var timeoutCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(Mathf.Max(1f, _connectTimeoutSeconds))))
                {
                    await _messageSender.ConnectAsync(timeoutCancellation.Token);
                }

                _hasAssignedSeat = false;
                Debug.Log(
                    $"[After333 Online Join] sending match={FormatLogValue(_matchId)} account={FormatLogValue(accountId)} token={FormatLogValue(_playerToken)} localSeat={_localPlayerId} serverAi={_joinUseServerAiOpponent} matchmaking={_joinUseMatchmakingQueue} clientVersion={FormatLogValue(resolvedClientVersion)} run={FormatLogValue(_joinRunId)} deck={FormatLogValue(_joinDeckId)} deckCards={_joinPlayerDeckCardIds.Count}");
                _messageSender.SendJoinMatch(
                    _matchId,
                    _playerToken,
                    _joinPlayerDeckCardIds,
                    _joinUseServerAiOpponent,
                    _joinUseMatchmakingQueue,
                    _joinRunId,
                    _joinDeckId,
                    sessionToken,
                    accountId,
                    _isAutomaticReconnectActive,
                    _automaticReconnectPreviousConnectionId);
                _nextClientHeartbeatRealtime = UnityEngine.Time.unscaledTime +
                                               Mathf.Max(1f, _clientHeartbeatIntervalSeconds);
                SetStatus($"Connected to {resolvedServerUrl}. Joining match {_matchId} with run={FormatLogValue(_joinRunId)}, deck={FormatLogValue(_joinDeckId)}.");
                PresentConnectionProgress("서버에 연결했습니다. 전투를 복원하는 중입니다.");
            }
            catch (OperationCanceledException)
            {
                var timeoutSeconds = Mathf.Max(1f, _connectTimeoutSeconds);
                _matchmakingFailureMessage = "서버 연결 시간이 초과되었습니다. 나중에 다시 시도해주세요.";
                SetStatus($"Connection failed: timed out after {timeoutSeconds:0.#} seconds.");
                PresentConnectionFailure();
                CleanupSender();
            }
            catch (Exception ex)
            {
                _matchmakingFailureMessage = "서버 연결에 실패했습니다. 나중에 다시 시도해주세요.";
                SetStatus($"Connection failed: {ex.Message}");
                PresentConnectionFailure();
                CleanupSender();
            }
        }

        private void PresentConnectionProgress(string message)
        {
            if (_isAutomaticReconnectActive)
            {
                PresentAutomaticReconnectStatus();
                return;
            }

            ShowOnlineStatusToast(message);
        }

        private void PresentConnectionFailure()
        {
            if (_isAutomaticReconnectActive)
            {
                PresentAutomaticReconnectStatus();
                return;
            }

            const string message = "서버 연결에 실패했습니다. 나중에 다시 시도해주세요.";
            ShowOnlineErrorToast(message);
            _battleBootstrapper?.ShowPvpMatchmakingConnectionFailure(message);
        }

        private void SendTestCommand(string label, Action<OnlineBattleGateway> send)
        {
            if (!EnsurePlayMode("send an online battle command"))
            {
                return;
            }

            if (_gateway == null || _messageSender == null || !_messageSender.IsConnected)
            {
                SetStatus($"Cannot send {label} because the test client is not connected.");
                return;
            }

            if (!_hasAssignedSeat)
            {
                SetStatus($"Cannot send {label} until the server assigns this client a match seat.");
                return;
            }

            try
            {
                send(_gateway);
                SetStatus($"Sent {label} test command.");
            }
            catch (Exception ex)
            {
                SetStatus($"Send failed: {ex.Message}");
            }
        }

        private void SetJoinPlayerDeck(IReadOnlyList<string> playerDeckCardIds)
        {
            _joinPlayerDeckCardIds.Clear();
            if (playerDeckCardIds == null)
            {
                return;
            }

            for (var i = 0; i < playerDeckCardIds.Count; i++)
            {
                var cardId = playerDeckCardIds[i];
                if (!string.IsNullOrWhiteSpace(cardId))
                {
                    _joinPlayerDeckCardIds.Add(cardId);
                }
            }
        }

        private void SetJoinRunDeckMetadata(string runId, string deckId)
        {
            _joinRunId = string.IsNullOrWhiteSpace(runId) ? string.Empty : runId.Trim();
            _joinDeckId = string.IsNullOrWhiteSpace(deckId) ? string.Empty : deckId.Trim();
        }

        private static string FormatLogValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private void ConfigureJoinMode(bool useServerAiOpponent, bool useMatchmakingQueue)
        {
            _joinUseServerAiOpponent = useServerAiOpponent;
            _joinUseMatchmakingQueue = useMatchmakingQueue;
        }

        private async System.Threading.Tasks.Task DisconnectAsync()
        {
            StopAutomaticReconnect(hideStatus: true);
            if (_messageSender == null)
            {
                _gateway = null;
                return;
            }

            try
            {
                await _messageSender.DisconnectAsync();
                SetStatus("Disconnected.");
            }
            catch (Exception ex)
            {
                SetStatus($"Disconnect failed: {ex.Message}");
            }
            finally
            {
                CleanupSender();
            }
        }

        private void CleanupSender()
        {
            if (_messageSender != null)
            {
                _messageSender.MessageReceived -= QueueMessage;
                _messageSender.ErrorReceived -= QueueError;
                _messageSender.ConnectionClosed -= QueueConnectionClosed;
                _messageSender.Dispose();
            }

            _messageSender = null;
            _gateway = null;
            _hasAssignedSeat = false;
            _assignedConnectionId = string.Empty;
            _localOnlineSeatId = OnlineBattleSeatId.None;
            _hasServerClockOffset = false;
            _serverClockOffsetMilliseconds = 0;
            _nextClientHeartbeatRealtime = 0f;

            if (ShouldDriveBattleScreen())
            {
                AutoAssignBattleBootstrapper();
                _battleBootstrapper?.HideOnlineTurnTimer();
            }
        }

        private void QueueMessage(OnlineBattleEnvelope envelope)
        {
            lock (_queueLock)
            {
                _receivedMessages.Enqueue(new QueuedOnlineBattleEnvelope(
                    envelope,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
                _receivedMessageCount += 1;
            }
        }

        private void QueueError(string message)
        {
            lock (_queueLock)
            {
                _receivedErrors.Enqueue(message ?? string.Empty);
                _receivedErrorCount += 1;
            }
        }

        private void QueueConnectionClosed(string reason)
        {
            lock (_queueLock)
            {
                _connectionClosedReasons.Enqueue(reason ?? string.Empty);
            }
        }

        private void DrainQueuedMessages()
        {
            while (TryDequeueConnectionClosed(out var closeReason))
            {
                if (CanAutomaticallyRecoverCurrentBattle())
                {
                    BeginAutomaticReconnect(closeReason, resetCurrentSender: true);
                }
                else if (!_isDestroying)
                {
                    var message = $"Server connection closed: {closeReason}";
                    if (_joinUseMatchmakingQueue && !_hasReceivedFirstStateView)
                    {
                        _matchmakingFailureMessage = "서버 연결이 종료되었습니다. 나중에 다시 시도해주세요.";
                    }
                    SetStatus(message);
                    ShowOnlineErrorToast(message);
                }
            }

            while (TryDequeueError(out var error))
            {
                var message = $"Server connection error: {error}";
                SetStatus(message);
                if (CanAutomaticallyRecoverCurrentBattle() || _isAutomaticReconnectActive)
                {
                    BeginAutomaticReconnect(error, resetCurrentSender: true);
                }
                else
                {
                    if (_joinUseMatchmakingQueue && !_hasReceivedFirstStateView)
                    {
                        _matchmakingFailureMessage = "서버 연결에 실패했습니다. 나중에 다시 시도해주세요.";
                    }
                    ShowOnlineErrorToast(message);
                }
            }

            while (TryDequeueMessage(out var queuedEnvelope))
            {
                var envelope = queuedEnvelope.Envelope;
                if (envelope.MessageType == OnlineBattleMessageType.StateView)
                {
                    UpdateServerClockOffset(envelope.StateView, queuedEnvelope.ReceivedLocalUnixTimeMilliseconds);
                    UpdateOnlineTurnTimer(envelope.StateView);
                    if (!_hasReceivedFirstStateView)
                    {
                        StopPresentationQueue();
                        HandleEnvelope(envelope);
                        continue;
                    }
                }

                QueuePresentationEnvelope(envelope);
            }
        }

        private void ResetPresentationStateForNewConnection()
        {
            StopPresentationQueue();
            AutoAssignBattleBootstrapper();
            _battleBootstrapper?.ClearOpponentPlayedCardReveals();
            _pendingStateView = null;
            _latestStateView = null;
            _combatLogEntries.Clear();
            _latestCombatLogSequence = 0;
            _deferStateViewUntilRealtime = 0f;
            _hasReceivedFirstStateView = false;
        }

        private void StopPresentationQueue()
        {
            if (_presentationQueueCoroutine != null)
            {
                StopCoroutine(_presentationQueueCoroutine);
                _presentationQueueCoroutine = null;
            }

            _presentationMessages.Clear();
        }

        private void QueuePresentationEnvelope(OnlineBattleEnvelope envelope)
        {
            if (envelope == null)
            {
                return;
            }

            _presentationMessages.Enqueue(envelope);
            if (_presentationQueueCoroutine == null)
            {
                _presentationQueueCoroutine = StartCoroutine(ProcessPresentationQueue());
            }
        }

        private IEnumerator ProcessPresentationQueue()
        {
            while (_presentationMessages.Count > 0)
            {
                var envelope = _presentationMessages.Peek();
                while (ShouldDeferBattleEventsForMulliganPresentation(envelope))
                {
                    yield return null;
                }

                envelope = _presentationMessages.Dequeue();
                var followingStatePresented = false;
                if (ShouldPresentFollowingStateBeforeServerAiCardReveal(envelope))
                {
                    var stateWaitDeadline = Time.unscaledTime + ServerAiBoardCardStateWaitSeconds;
                    while (_presentationMessages.Count == 0 && Time.unscaledTime < stateWaitDeadline)
                    {
                        yield return null;
                    }

                    if (_presentationMessages.Count > 0 &&
                        _presentationMessages.Peek()?.MessageType == OnlineBattleMessageType.StateView)
                    {
                        HandleEnvelope(_presentationMessages.Dequeue());
                        followingStatePresented = true;
                        // Let the newly summoned occupant render before the card reveal fades in.
                        yield return null;
                    }
                }

                var durationSeconds = HandleEnvelope(envelope);
                if (durationSeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(durationSeconds);
                }

                var pauseSeconds = envelope.MessageType == OnlineBattleMessageType.BattleEvents
                    ? AiActionPacing.GetPostActionPauseSeconds(_joinUseServerAiOpponent, envelope.BattleEvents, durationSeconds)
                    : 0;
                if (pauseSeconds > 0)
                {
                    // Apply this action's HP/position result before pausing, not after the pause.
                    if (!followingStatePresented)
                    {
                        var stateWaitDeadline = Time.unscaledTime + ServerAiBoardCardStateWaitSeconds;
                        while (_presentationMessages.Count == 0 && Time.unscaledTime < stateWaitDeadline)
                        {
                            yield return null;
                        }
                        if (_presentationMessages.Count > 0 &&
                            _presentationMessages.Peek()?.MessageType == OnlineBattleMessageType.StateView)
                        {
                            HandleEnvelope(_presentationMessages.Dequeue());
                            yield return null;
                        }
                    }
                    if (_currentProjectedBattleState == null || !_currentProjectedBattleState.IsEnded)
                    {
                        yield return new WaitForSecondsRealtime((float)pauseSeconds);
                    }
                }
            }

            _presentationQueueCoroutine = null;
        }

        private bool ShouldPresentFollowingStateBeforeServerAiCardReveal(OnlineBattleEnvelope envelope)
        {
            if (!_joinUseServerAiOpponent ||
                envelope == null ||
                envelope.MessageType != OnlineBattleMessageType.BattleEvents ||
                envelope.BattleEvents == null)
            {
                return false;
            }

            for (var eventIndex = 0; eventIndex < envelope.BattleEvents.Count; eventIndex++)
            {
                if (OpponentPlayedCardRevealRules.IsBoardCardPlay(envelope.BattleEvents[eventIndex]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ShouldDeferBattleEventsForMulliganPresentation(OnlineBattleEnvelope envelope)
        {
            if (envelope == null ||
                envelope.MessageType != OnlineBattleMessageType.BattleEvents ||
                _currentProjectedBattleState == null ||
                _currentProjectedBattleState.Phase == PhaseType.Mulligan)
            {
                return false;
            }

            AutoAssignBattleBootstrapper();
            return _battleBootstrapper != null && _battleBootstrapper.IsMulliganPresentationActive;
        }

        private bool TryDequeueMessage(out QueuedOnlineBattleEnvelope queuedEnvelope)
        {
            lock (_queueLock)
            {
                if (_receivedMessages.Count == 0)
                {
                    queuedEnvelope = null;
                    return false;
                }

                queuedEnvelope = _receivedMessages.Dequeue();
                return true;
            }
        }

        private bool TryDequeueError(out string error)
        {
            lock (_queueLock)
            {
                if (_receivedErrors.Count == 0)
                {
                    error = string.Empty;
                    return false;
                }

                error = _receivedErrors.Dequeue();
                return true;
            }
        }

        private bool TryDequeueConnectionClosed(out string reason)
        {
            lock (_queueLock)
            {
                if (_connectionClosedReasons.Count == 0)
                {
                    reason = string.Empty;
                    return false;
                }

                reason = _connectionClosedReasons.Dequeue();
                return true;
            }
        }

        private void DetectMobileNetworkTransition()
        {
            var currentReachability = UnityEngine.Application.internetReachability;
            if (currentReachability == _lastInternetReachability)
            {
                return;
            }

            var previousReachability = _lastInternetReachability;
            _lastInternetReachability = currentReachability;
            if (!CanAutomaticallyRecoverCurrentBattle())
            {
                return;
            }

            BeginAutomaticReconnect(
                $"Network changed from {previousReachability} to {currentReachability}.",
                resetCurrentSender: true);
        }

        private void MonitorAutomaticReconnect()
        {
            if (!_isAutomaticReconnectActive)
            {
                if (CanAutomaticallyRecoverCurrentBattle() &&
                    _messageSender != null &&
                    !_messageSender.IsConnected)
                {
                    BeginAutomaticReconnect("The battle connection is no longer open.", resetCurrentSender: true);
                }

                return;
            }

            if (_currentProjectedBattleState == null || _currentProjectedBattleState.IsEnded)
            {
                StopAutomaticReconnect(hideStatus: true);
                return;
            }

            var now = UnityEngine.Time.unscaledTime;
            var remainingSeconds = Mathf.Max(
                0,
                Mathf.CeilToInt(_automaticReconnectDeadlineRealtime - now));
            if (remainingSeconds != _lastPresentedAutomaticReconnectSeconds)
            {
                _lastPresentedAutomaticReconnectSeconds = remainingSeconds;
                PresentAutomaticReconnectStatus();
            }

            if (remainingSeconds <= 0)
            {
                FailAutomaticReconnect();
                return;
            }

            if (_isAutomaticReconnectAttemptInProgress ||
                UnityEngine.Application.internetReachability == NetworkReachability.NotReachable)
            {
                return;
            }

            if (_messageSender != null && _messageSender.IsConnected)
            {
                if (_automaticReconnectJoinDeadlineRealtime > 0f &&
                    now >= _automaticReconnectJoinDeadlineRealtime)
                {
                    CleanupSender();
                    ScheduleNextAutomaticReconnectAttempt();
                }

                return;
            }

            if (now >= _nextAutomaticReconnectAttemptRealtime)
            {
                AttemptAutomaticReconnectAsync();
            }
        }

        private bool CanAutomaticallyRecoverCurrentBattle()
        {
            return _automaticallyReconnectDuringBattle &&
                   !_isDestroying &&
                   _hasReceivedFirstStateView &&
                   _currentProjectedBattleState != null &&
                   !_currentProjectedBattleState.IsEnded &&
                   !string.IsNullOrWhiteSpace(_matchId);
        }

        private void BeginAutomaticReconnect(string reason, bool resetCurrentSender)
        {
            if (!_isAutomaticReconnectActive && !CanAutomaticallyRecoverCurrentBattle())
            {
                return;
            }

            if (!_isAutomaticReconnectActive)
            {
                _automaticReconnectPreviousConnectionId = _assignedConnectionId;
                _isAutomaticReconnectActive = true;
                _automaticReconnectGeneration += 1;
                _automaticReconnectAttemptCount = 0;
                _automaticReconnectDeadlineRealtime = UnityEngine.Time.unscaledTime +
                                                     Mathf.Max(
                                                         MinimumAutomaticReconnectWindowSeconds,
                                                         _automaticReconnectWindowSeconds);
                _lastPresentedAutomaticReconnectSeconds = -1;
                _receivedLocalReconnectCancellation = false;
                StopPresentationQueue();
                AutoAssignBattleBootstrapper();
                _battleBootstrapper?.ClearOpponentPlayedCardReveals();
                ClearQueuedTransportMessages();
                _pendingStateView = null;
                _deferStateViewUntilRealtime = 0f;
            }

            if (resetCurrentSender && _messageSender != null)
            {
                CleanupSender();
            }

            _automaticReconnectJoinDeadlineRealtime = 0f;
            if (!_isAutomaticReconnectAttemptInProgress)
            {
                _nextAutomaticReconnectAttemptRealtime = UnityEngine.Time.unscaledTime;
            }

            SetStatus($"Automatic battle reconnect started: {reason}");
            AutoAssignBattleBootstrapper();
            _battleBootstrapper?.HideOnlineTurnTimer();
            PresentAutomaticReconnectStatus();
        }

        private async void AttemptAutomaticReconnectAsync()
        {
            if (!_isAutomaticReconnectActive ||
                _isAutomaticReconnectAttemptInProgress ||
                _isDestroying)
            {
                return;
            }

            var reconnectGeneration = _automaticReconnectGeneration;
            _isAutomaticReconnectAttemptInProgress = true;
            _automaticReconnectAttemptCount += 1;
            if (_messageSender != null)
            {
                CleanupSender();
            }

            try
            {
                await ConnectAsync();
            }
            finally
            {
                if (!_isDestroying &&
                    _isAutomaticReconnectActive &&
                    reconnectGeneration == _automaticReconnectGeneration)
                {
                    _isAutomaticReconnectAttemptInProgress = false;
                    if (_messageSender != null && _messageSender.IsConnected)
                    {
                        _automaticReconnectJoinDeadlineRealtime = UnityEngine.Time.unscaledTime +
                                                                  Mathf.Max(1f, _automaticReconnectJoinTimeoutSeconds);
                    }
                    else
                    {
                        ScheduleNextAutomaticReconnectAttempt();
                    }
                }
            }
        }

        private void ScheduleNextAutomaticReconnectAttempt()
        {
            var delaySeconds = CalculateAutomaticReconnectDelaySeconds(
                _automaticReconnectAttemptCount,
                _automaticReconnectInitialDelaySeconds,
                _automaticReconnectMaximumDelaySeconds);
            _automaticReconnectJoinDeadlineRealtime = 0f;
            _nextAutomaticReconnectAttemptRealtime = UnityEngine.Time.unscaledTime + delaySeconds;
        }

        public static float CalculateAutomaticReconnectDelaySeconds(
            int completedAttemptCount,
            float initialDelaySeconds,
            float maximumDelaySeconds)
        {
            var safeInitialDelay = Mathf.Max(0.1f, initialDelaySeconds);
            var safeMaximumDelay = Mathf.Max(safeInitialDelay, maximumDelaySeconds);
            var exponent = Mathf.Clamp(completedAttemptCount - 1, 0, 8);
            return Mathf.Min(safeMaximumDelay, safeInitialDelay * Mathf.Pow(2f, exponent));
        }

        public static bool IsExpiredPveReconnect(string code, bool reconnecting, bool serverAi, bool matchmaking)
        {
            return reconnecting && serverAi && !matchmaking &&
                   string.Equals(code, "battle_reconnect_expired", StringComparison.Ordinal);
        }

        private bool IsExpiredPveReconnectError(string code)
        {
            return IsExpiredPveReconnect(code, _isAutomaticReconnectActive, _joinUseServerAiOpponent, _joinUseMatchmakingQueue);
        }

        private void FinishExpiredPveReconnectAsDefeat()
        {
            StopPresentationQueue();
            StopAutomaticReconnect(hideStatus: true);
            CleanupSender();
            if (_currentProjectedBattleState != null && !_currentProjectedBattleState.IsEnded)
            {
                _currentProjectedBattleState.EndBattle(PlayerId.AI);
                PresentProjectedStateToBattleScreen();
            }

            const string message = "PVE 전투 재접속 시간이 만료되어 패배 처리되었습니다.";
            SetStatus(message);
            ShowOnlineErrorToast(message);
        }

        private void CompleteAutomaticReconnect(bool battleEnded)
        {
            if (!_isAutomaticReconnectActive)
            {
                return;
            }

            var showSuccessToast = !battleEnded && !_receivedLocalReconnectCancellation;
            StopAutomaticReconnect(hideStatus: true);
            if (showSuccessToast)
            {
                ShowOnlineSuccessToast("전투에 재접속했습니다.");
            }
        }

        private void FailAutomaticReconnect()
        {
            StopAutomaticReconnect(hideStatus: true);
            CleanupSender();
            const string message = "서버 연결에 실패했습니다. 나중에 다시 시도해주세요.";
            SetStatus(message);
            ShowOnlineErrorToast(message);
        }

        private void StopAutomaticReconnect(bool hideStatus)
        {
            _isAutomaticReconnectActive = false;
            _isAutomaticReconnectAttemptInProgress = false;
            _automaticReconnectGeneration += 1;
            _automaticReconnectDeadlineRealtime = 0f;
            _nextAutomaticReconnectAttemptRealtime = 0f;
            _automaticReconnectJoinDeadlineRealtime = 0f;
            _automaticReconnectAttemptCount = 0;
            _lastPresentedAutomaticReconnectSeconds = -1;
            _receivedLocalReconnectCancellation = false;
            _automaticReconnectPreviousConnectionId = string.Empty;

            if (hideStatus)
            {
                AutoAssignBattleBootstrapper();
                _battleBootstrapper?.HideLocalConnectionRecoveryStatus();
            }
        }

        private void PresentAutomaticReconnectStatus()
        {
            if (!_isAutomaticReconnectActive)
            {
                return;
            }

            var remainingSeconds = Mathf.Max(
                0,
                Mathf.CeilToInt(_automaticReconnectDeadlineRealtime - UnityEngine.Time.unscaledTime));
            AutoAssignBattleBootstrapper();
            _battleBootstrapper?.ShowLocalConnectionRecoveryStatus(
                $"전투에 재접속하는 중... ({remainingSeconds}초)");
        }

        private void SendClientHeartbeatIfNeeded()
        {
            if (_messageSender == null || !_messageSender.IsConnected)
            {
                return;
            }

            var now = UnityEngine.Time.unscaledTime;
            if (now < _nextClientHeartbeatRealtime)
            {
                return;
            }

            _nextClientHeartbeatRealtime = now + Mathf.Max(1f, _clientHeartbeatIntervalSeconds);
            _messageSender.SendKeepAlive(
                _matchId,
                _playerToken,
                AccountSessionState.AccountId ?? string.Empty);
        }

        private void ClearQueuedTransportMessages()
        {
            lock (_queueLock)
            {
                _receivedMessages.Clear();
                _receivedErrors.Clear();
                _connectionClosedReasons.Clear();
            }
        }

        private float HandleEnvelope(OnlineBattleEnvelope envelope)
        {
            if (envelope == null)
            {
                return 0f;
            }

            ApplySeatAssignmentIfOwned(envelope);

            switch (envelope.MessageType)
            {
                case OnlineBattleMessageType.BattleEvents:
                case OnlineBattleMessageType.KeepAlive:
                    HandleBattleStatusEvents(envelope.BattleEvents);
                    var durationSeconds = ShouldDriveBattleScreen()
                        ? PlayBattleEventAnimations(envelope.BattleEvents)
                        : 0f;
                    SetStatus(FormatEventEnvelope(envelope));
                    return durationSeconds;

                case OnlineBattleMessageType.StateView:
                    ApplyCombatLogUpdate(envelope.StateView);
                    _latestStateView = envelope.StateView;
                    _gateway?.ApplyServerView(envelope.StateView);
                    SetStatus(FormatStateViewEnvelope(envelope));
                    ApplyStateView(envelope.StateView);
                    HandleEndedStateView(envelope.StateView);
                    if (_isAutomaticReconnectActive && envelope.StateView != null)
                    {
                        CompleteAutomaticReconnect(envelope.StateView.IsEnded);
                    }

                    if (!_hasReceivedFirstStateView && envelope.StateView != null)
                    {
                        _hasReceivedFirstStateView = true;
                        var wasPvpMatchmaking = _battleBootstrapper != null &&
                                                _battleBootstrapper.IsPvpMatchmakingOverlayVisible &&
                                                !_battleBootstrapper.IsCancellingPvpMatchmaking;
                        _battleBootstrapper?.HidePvpMatchmakingOverlay(showFoundMessage: wasPvpMatchmaking);
                        if (!wasPvpMatchmaking)
                        {
                            ShowOnlineSuccessToast("서버 전투 준비 완료.");
                        }
                    }

                    return 0f;

                case OnlineBattleMessageType.Error:
                    var errorCode = envelope.Error?.Code ?? string.Empty;
                    if (IsExpiredPveReconnectError(errorCode))
                    {
                        FinishExpiredPveReconnectAsDefeat();
                        return 0f;
                    }

                    var userFacingErrorMessage = FormatUserFacingServerError(errorCode, envelope.Error?.Message);
                    var errorMessage = string.IsNullOrWhiteSpace(errorCode)
                        ? userFacingErrorMessage
                        : $"Server error [{errorCode}]: {userFacingErrorMessage}";
                    if (_joinUseMatchmakingQueue && !_hasReceivedFirstStateView)
                    {
                        _matchmakingFailureMessage = userFacingErrorMessage;
                    }
                    SetStatus(errorMessage);
                    if (_isAutomaticReconnectActive)
                    {
                        _isAutomaticReconnectAttemptInProgress = false;
                        CleanupSender();
                        ScheduleNextAutomaticReconnectAttempt();
                        PresentAutomaticReconnectStatus();
                        return 0f;
                    }

                    ShowOnlineErrorToast(userFacingErrorMessage);
                    ShowPvpMatchmakingFailureIfNeeded(userFacingErrorMessage);
                    return 0f;

                default:
                    SetStatus($"Received {envelope.MessageType} message.");
                    return 0f;
            }
        }

        private void ApplySeatAssignmentIfOwned(OnlineBattleEnvelope envelope)
        {
            if (!envelope.HasAssignedSeat)
            {
                return;
            }

            var localAccountId = AccountSessionState.AccountId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(localAccountId) &&
                !string.IsNullOrWhiteSpace(envelope.AccountId))
            {
                if (!string.Equals(envelope.AccountId, localAccountId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            else if (!string.Equals(envelope.PlayerToken, _playerToken, StringComparison.Ordinal))
            {
                return;
            }

            if (_isAutomaticReconnectActive &&
                !string.IsNullOrWhiteSpace(_automaticReconnectPreviousConnectionId) &&
                string.Equals(
                    envelope.ConnectionId,
                    _automaticReconnectPreviousConnectionId,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (!_isAutomaticReconnectActive &&
                !string.IsNullOrWhiteSpace(_assignedConnectionId) &&
                !string.Equals(envelope.ConnectionId, _assignedConnectionId, StringComparison.Ordinal))
            {
                return;
            }

            _assignedConnectionId = envelope.ConnectionId ?? string.Empty;
            _localPlayerId = envelope.AssignedSeatId;
            _localOnlineSeatId = envelope.AssignedOnlineSeatId;
            _hasAssignedSeat = true;
            _gateway?.SetLocalPlayerId(_localPlayerId);
            _gateway?.SetMatchId(envelope.MatchId);
            if (!string.IsNullOrWhiteSpace(envelope.MatchId))
            {
                _matchId = envelope.MatchId;
            }
        }

        private static string FormatEventEnvelope(OnlineBattleEnvelope envelope)
        {
            if (envelope.BattleEvents == null || envelope.BattleEvents.Count == 0)
            {
                return $"Received {envelope.MessageType} message.";
            }

            var latestEvent = envelope.BattleEvents[envelope.BattleEvents.Count - 1];
            return string.IsNullOrWhiteSpace(latestEvent.Message)
                ? $"Received {latestEvent.EventType} event."
                : latestEvent.Message;
        }

        private string FormatStateViewEnvelope(OnlineBattleEnvelope envelope)
        {
            var view = envelope.StateView;
            if (view == null)
            {
                return "Received empty server state view.";
            }

            var occupantCount = view.Occupants?.Count ?? 0;
            var persistentEffectCount = view.PersistentEffects?.Count ?? 0;
            var goblinText = FormatFirstGoblin(view);
            var masterHpText = FormatMasterHpSummary(view);
            var turnTimerText = FormatTurnTimer(view);

            var viewerLabel = FormatSeatLabel(view.ViewerOnlineSeatId, view.ViewerId);
            var activeLabel = FormatSeatLabel(view.ActiveOnlineSeatId, view.ActivePlayerId);
            return $"StateView viewer={viewerLabel} turn={view.TurnNumber} active={activeLabel} phase={view.Phase}{turnTimerText} hand={view.Player?.HandCount ?? 0} opponentHand={view.Opponent?.HandCount ?? 0} occupants={occupantCount} effects={persistentEffectCount}{goblinText}{masterHpText}";
        }

        private void ApplyStateView(BattleStateViewDto view)
        {
            if (view == null)
            {
                return;
            }

            try
            {
                _currentProjectedBattleState = BattleStateViewProjector.CreateLocalPerspectiveState(view);
                PresentProjectedStateToBattleScreen();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[After333 Online Test:{_playerToken}/{FormatLocalSeatLabel()}] Failed to present StateView to battle screen: {ex.Message}");
            }
        }

        private void HandleEndedStateView(BattleStateViewDto view)
        {
            if (view == null || !view.IsEnded)
            {
                return;
            }

            AutoAssignBattleBootstrapper();
            _battleBootstrapper?.HideOpponentReconnectCountdown();
            _battleBootstrapper?.HideOnlineTurnTimer();
        }

        private void UpdateOnlineTurnTimer(BattleStateViewDto view)
        {
            if (!ShouldDriveBattleScreen())
            {
                return;
            }

            AutoAssignBattleBootstrapper();
            if (_battleBootstrapper == null)
            {
                return;
            }

            if (view == null ||
                view.IsEnded ||
                (view.Phase != PhaseType.Mulligan && view.Phase != PhaseType.Main) ||
                view.TurnTimerDeadlineUnixTimeMilliseconds <= 0 ||
                view.ServerUnixTimeMilliseconds <= 0)
            {
                _battleBootstrapper.HideOnlineTurnTimer();
                return;
            }

            var remainingMilliseconds = CalculateTurnTimerRemainingMilliseconds(view);
            var remainingSeconds = Mathf.Max(0f, remainingMilliseconds / 1000f);
            var durationSeconds = view.TurnTimerDurationSeconds > 0
                ? view.TurnTimerDurationSeconds
                : view.Phase == PhaseType.Mulligan
                    ? 33
                    : 73;

            if (view.Phase == PhaseType.Mulligan)
            {
                _battleBootstrapper.ShowOnlineMulliganTimer(
                    view.Player?.HasUsedMulligan == true,
                    remainingSeconds,
                    durationSeconds);
                return;
            }

            var isLocalPlayerTurn = ToLocalOwnerId(view.ActivePlayerId) == PlayerId.Player;
            _battleBootstrapper.ShowOnlineTurnTimer(isLocalPlayerTurn, remainingSeconds, durationSeconds);
        }

        private void UpdateServerClockOffset(BattleStateViewDto view, long receivedLocalUnixTimeMilliseconds)
        {
            if (view == null || view.ServerUnixTimeMilliseconds <= 0 || receivedLocalUnixTimeMilliseconds <= 0)
            {
                return;
            }

            // Use the server timestamp embedded in StateView to avoid each client trusting its own wall clock.
            _serverClockOffsetMilliseconds = view.ServerUnixTimeMilliseconds - receivedLocalUnixTimeMilliseconds;
            _hasServerClockOffset = true;
        }

        private void ApplyOrDeferStateView(BattleStateViewDto view)
        {
            if (view == null)
            {
                return;
            }

            if (Time.unscaledTime < _deferStateViewUntilRealtime)
            {
                _pendingStateView = view;
                return;
            }

            ApplyStateView(view);
        }

        private void ApplyPendingStateViewIfReady()
        {
            if (_pendingStateView == null || Time.unscaledTime < _deferStateViewUntilRealtime)
            {
                return;
            }

            var pendingStateView = _pendingStateView;
            _pendingStateView = null;
            ApplyStateView(pendingStateView);
        }

        private void DeferStateViewPresentationFor(float durationSeconds)
        {
            if (durationSeconds <= 0f)
            {
                return;
            }

            _deferStateViewUntilRealtime = Mathf.Max(
                _deferStateViewUntilRealtime,
                Time.unscaledTime + durationSeconds);
        }

        private float PlayBattleEventAnimations(IReadOnlyList<BattleEventDto> battleEvents)
        {
            if (!ShouldDriveBattleScreen() || battleEvents == null || battleEvents.Count == 0)
            {
                return 0f;
            }

            AutoAssignBattleBootstrapper();
            if (_battleBootstrapper == null)
            {
                return 0f;
            }

            return _battleBootstrapper.PlayOnlineBattleEventAnimations(
                ProjectBattleEventsToLocalPerspective(battleEvents));
        }

        private void HandleBattleStatusEvents(IReadOnlyList<BattleEventDto> battleEvents)
        {
            if (battleEvents == null || battleEvents.Count == 0)
            {
                return;
            }

            AutoAssignBattleBootstrapper();
            if (_battleBootstrapper == null || !ShouldShowBattleStatusOverlay())
            {
                return;
            }

            for (var i = 0; i < battleEvents.Count; i++)
            {
                var battleEvent = battleEvents[i];
                if (battleEvent == null)
                {
                    continue;
                }

                switch (battleEvent.EventType)
                {
                    case BattleEventType.ReconnectGraceStarted:
                        var reconnectGraceSeconds = battleEvent.Amount > 0 ? battleEvent.Amount : 60;
                        _battleBootstrapper.ShowOpponentReconnectCountdown(reconnectGraceSeconds);
                        SetStatus($"Opponent reconnect grace started: {reconnectGraceSeconds} seconds.");
                        break;

                    case BattleEventType.ReconnectGraceCancelled:
                        var opponentReconnected =
                            ToLocalOwnerId(battleEvent.SourceOwnerId) == PlayerId.AI;
                        if (!opponentReconnected && _isAutomaticReconnectActive)
                        {
                            _receivedLocalReconnectCancellation = true;
                        }

                        _battleBootstrapper.HideOpponentReconnectCountdown();
                        _battleBootstrapper.ShowOnlineSuccessToast(
                            opponentReconnected
                                ? "상대 연결이 끊겼다가 재접속했습니다."
                                : "전투에 재접속했습니다.");
                        break;

                    case BattleEventType.BattleEnded:
                        _battleBootstrapper.HideOpponentReconnectCountdown();
                        _battleBootstrapper.HideOnlineTurnTimer();
                        if (IsDisconnectTimeoutBattleEnd(battleEvent) &&
                            ToLocalOwnerId(battleEvent.SourceOwnerId) == PlayerId.Player)
                        {
                            _battleBootstrapper.ShowBattleResultToast("상대가 도망쳤습니다");
                        }

                        break;
                }
            }
        }

        private bool ShouldShowBattleStatusOverlay()
        {
            if (ShouldDriveBattleScreen())
            {
                return true;
            }

            return _hasAssignedSeat &&
                   _messageSender != null &&
                   _messageSender.IsConnected;
        }

        private static bool IsDisconnectTimeoutBattleEnd(BattleEventDto battleEvent)
        {
            return battleEvent != null &&
                   battleEvent.EventType == BattleEventType.BattleEnded &&
                   !string.IsNullOrWhiteSpace(battleEvent.Message) &&
                   battleEvent.Message.IndexOf("disconnect timeout", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ShowOnlineErrorToast(string message)
        {
            if (!ShouldDriveBattleScreen() || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            AutoAssignBattleBootstrapper();
            _battleBootstrapper?.ShowOnlineErrorToast(message);
        }

        private void ShowOnlineStatusToast(string message)
        {
            if (!ShouldDriveBattleScreen() || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            AutoAssignBattleBootstrapper();
            _battleBootstrapper?.ShowOnlineStatusToast(message);
        }

        private void ShowOnlineSuccessToast(string message)
        {
            if (!ShouldDriveBattleScreen() || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            AutoAssignBattleBootstrapper();
            _battleBootstrapper?.ShowOnlineSuccessToast(message);
        }

        private void ShowPvpMatchmakingFailureIfNeeded(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || !_joinUseMatchmakingQueue || _hasReceivedFirstStateView)
            {
                return;
            }

            AutoAssignBattleBootstrapper();
            _battleBootstrapper?.ShowPvpMatchmakingConnectionFailure(message);
        }

        private static string FormatUserFacingServerError(string code, string fallbackMessage)
        {
            switch (code)
            {
                case "already_in_pvp_match":
                    return "이미 이 계정으로 진행 중인 PVP 전투 연결이 있습니다. 기존 전투 창을 닫거나 잠시 후 다시 시도해 주세요.";
                case "battle_deck_required":
                    return "PVP에 사용할 서버 저장 덱이 없습니다.\n드래프트를 완료한 뒤 다시 시도해 주세요.";
                case "battle_deck_not_found":
                    return "서버에서 현재 드래프트 덱을 찾지 못했습니다.\n드래프트 화면으로 돌아가 덱 상태를 다시 확인해 주세요.";
                case "invalid_battle_deck_size":
                    return "PVP 전투 덱은 정확히 33장이어야 합니다.\n드래프트를 완료한 뒤 다시 시도해 주세요.";
                case "client_version_mismatch":
                    return "서버 버전과 현재 클라이언트 버전이 맞지 않습니다.\n최신 게임 클라이언트로 다시 실행해 주세요.";
                case "account_required":
                case "invalid_session":
                    return "서버 계정 로그인이 필요합니다.\n시작 화면에서 다시 로그인한 뒤 시도해 주세요.";
                case "db_not_configured":
                    return "서버 DB 연결이 설정되지 않았습니다.\nPowerShell 서버의 PROJECT333_DB_CONNECTION 설정을 확인해 주세요.";
                case "match_full":
                    return "매칭 방이 이미 가득 찼습니다.\n잠시 후 다시 시도해 주세요.";
                default:
                    return string.IsNullOrWhiteSpace(fallbackMessage)
                        ? "서버에서 전투 요청을 처리하지 못했습니다."
                        : fallbackMessage;
            }
        }

        private float PlaySpellCastAnimation(BattleEventDto battleEvent)
        {
            if (battleEvent == null || battleEvent.TargetCoord == null)
            {
                return 0f;
            }

            AutoAssignBattleBootstrapper();
            if (_battleBootstrapper == null)
            {
                return 0f;
            }

            var localTargetOwnerId = ToLocalOwnerId(battleEvent.TargetOwnerId);
            return _battleBootstrapper.PlayOnlineSpellCastAnimation(
                battleEvent.CardId,
                localTargetOwnerId,
                battleEvent.TargetCoord.ToDomain(),
                battleEvent.Amount);
        }

        private List<BattleEventDto> ProjectBattleEventsToLocalPerspective(IReadOnlyList<BattleEventDto> battleEvents)
        {
            var localEvents = new List<BattleEventDto>();
            if (battleEvents == null)
            {
                return localEvents;
            }

            foreach (var battleEvent in battleEvents)
            {
                if (battleEvent == null)
                {
                    continue;
                }

                localEvents.Add(new BattleEventDto
                {
                    EventType = battleEvent.EventType,
                    SourceOwnerId = ToLocalOwnerId(battleEvent.SourceOwnerId),
                    SourceCoord = CloneCoord(battleEvent.SourceCoord),
                    TargetOwnerId = ToLocalOwnerId(battleEvent.TargetOwnerId),
                    TargetCoord = CloneCoord(battleEvent.TargetCoord),
                    TargetCoords = CloneCoords(battleEvent.TargetCoords),
                    RuntimeId = battleEvent.RuntimeId,
                    CardId = battleEvent.CardId,
                    SourceRuntimeId = battleEvent.SourceRuntimeId,
                    SourceCardId = battleEvent.SourceCardId,
                    EffectId = battleEvent.EffectId,
                    TargetRuntimeId = battleEvent.TargetRuntimeId,
                    TargetCardId = battleEvent.TargetCardId,
                    CardAttack = battleEvent.CardAttack,
                    CardMaxHp = battleEvent.CardMaxHp,
                    CardSpellDamage = battleEvent.CardSpellDamage,
                    AttackBonus = battleEvent.AttackBonus,
                    HpBonus = battleEvent.HpBonus,
                    Amount = battleEvent.Amount,
                    HpBefore = battleEvent.HpBefore,
                    HpAfter = battleEvent.HpAfter,
                    AttackType = battleEvent.AttackType,
                    DamageType = battleEvent.DamageType,
                    ValueCause = battleEvent.ValueCause,
                    Message = battleEvent.Message
                });
            }

            return localEvents;
        }

        private void ApplyCombatLogUpdate(BattleStateViewDto view)
        {
            if (view == null)
            {
                return;
            }

            var shouldReplace = view.ReplaceCombatLogEntries ||
                                (view.CombatLogLatestSequence > 0 &&
                                 view.CombatLogLatestSequence < _latestCombatLogSequence);
            if (shouldReplace)
            {
                _combatLogEntries.Clear();
                _latestCombatLogSequence = 0;
            }

            var incomingEntries = view.CombatLogEntries;
            if (incomingEntries != null)
            {
                for (var i = 0; i < incomingEntries.Count; i++)
                {
                    var incomingEntry = incomingEntries[i];
                    if (incomingEntry == null ||
                        incomingEntry.EntryType == BattleCombatLogEntryType.Unknown ||
                        incomingEntry.Sequence <= _latestCombatLogSequence)
                    {
                        continue;
                    }

                    _combatLogEntries.Add(ProjectCombatLogEntryToLocalPerspective(incomingEntry));
                    _latestCombatLogSequence = incomingEntry.Sequence;
                }
            }

            while (_combatLogEntries.Count > MaxCombatLogEntries)
            {
                _combatLogEntries.RemoveAt(0);
            }

            if (view.CombatLogLatestSequence > _latestCombatLogSequence)
            {
                _latestCombatLogSequence = view.CombatLogLatestSequence;
            }
        }

        private BattleCombatLogEntryDto ProjectCombatLogEntryToLocalPerspective(
            BattleCombatLogEntryDto entry)
        {
            return new BattleCombatLogEntryDto
            {
                Sequence = entry.Sequence,
                EntryType = entry.EntryType,
                SourceOwnerId = ToLocalOwnerId(entry.SourceOwnerId),
                TargetOwnerId = ToLocalOwnerId(entry.TargetOwnerId),
                SourceCardId = entry.SourceCardId,
                TargetCardId = entry.TargetCardId,
                SourceCoord = CloneCoord(entry.SourceCoord),
                TargetCoord = CloneCoord(entry.TargetCoord),
                SourceHpHistory = entry.SourceHpHistory == null
                    ? new List<int>()
                    : new List<int>(entry.SourceHpHistory),
                TargetHpHistory = entry.TargetHpHistory == null
                    ? new List<int>()
                    : new List<int>(entry.TargetHpHistory),
                SourceRemoved = entry.SourceRemoved,
                TargetRemoved = entry.TargetRemoved,
                SourceDamagePrevented = entry.SourceDamagePrevented,
                TargetDamagePrevented = entry.TargetDamagePrevented,
                HasCounterattack = entry.HasCounterattack,
                IsDraw = entry.IsDraw,
                Amount = entry.Amount,
                AttackBonus = entry.AttackBonus,
                HpBonus = entry.HpBonus,
                TurnNumber = entry.TurnNumber,
                AttackType = entry.AttackType,
                DamageType = entry.DamageType,
                ValueCause = entry.ValueCause,
                ResourceType = entry.ResourceType
            };
        }

        private static TileCoordDto CloneCoord(TileCoordDto coord)
        {
            if (coord == null)
            {
                return null;
            }

            return new TileCoordDto
            {
                Column = coord.Column,
                Row = coord.Row
            };
        }

        private static List<TileCoordDto> CloneCoords(IReadOnlyList<TileCoordDto> coords)
        {
            var cloned = new List<TileCoordDto>();
            if (coords == null)
            {
                return cloned;
            }

            for (var index = 0; index < coords.Count; index++)
            {
                var coord = CloneCoord(coords[index]);
                if (coord != null)
                {
                    cloned.Add(coord);
                }
            }

            return cloned;
        }

        private void PresentProjectedStateToBattleScreen()
        {
            if (!ShouldDriveBattleScreen() || _currentProjectedBattleState == null)
            {
                return;
            }

            AutoAssignBattleBootstrapper();
            if (_battleBootstrapper != null)
            {
                _battleBootstrapper.PresentOnlineProjectedState(this);
                return;
            }

            AutoAssignBattleScreenPresenter();
            if (_battleScreenPresenter == null)
            {
                return;
            }

            _battleScreenPresenter.Present(_currentProjectedBattleState);
        }

        private bool ShouldDriveBattleScreen()
        {
            if (!_presentStateViewsToBattleScreen)
            {
                return false;
            }

            AutoAssignBattleBootstrapper();
            return _battleBootstrapper == null ||
                   _battleBootstrapper.OnlineConnectionTester == null ||
                   ReferenceEquals(_battleBootstrapper.OnlineConnectionTester, this);
        }

        private void AutoAssignBattleScreenPresenter()
        {
            if (_battleScreenPresenter != null)
            {
                return;
            }

            _battleScreenPresenter = UnityEngine.Object.FindFirstObjectByType<BattleScreenPresenter>();
        }

        private void AutoAssignBattleBootstrapper()
        {
            if (_battleBootstrapper != null)
            {
                return;
            }

            _battleBootstrapper = UnityEngine.Object.FindFirstObjectByType<BattleBootstrapper>();
        }

        private PlayerId ToLocalOwnerId(PlayerId remoteOwnerId)
        {
            return remoteOwnerId == _localPlayerId ? PlayerId.Player : PlayerId.AI;
        }

        private string FormatTurnTimer(BattleStateViewDto view)
        {
            if (view == null ||
                view.TurnTimerDeadlineUnixTimeMilliseconds <= 0 ||
                view.ServerUnixTimeMilliseconds <= 0)
            {
                return string.Empty;
            }

            var remainingMilliseconds = CalculateTurnTimerRemainingMilliseconds(view);
            var remainingSeconds = (remainingMilliseconds + 999) / 1000;
            return $" timer={remainingSeconds}";
        }

        private long CalculateTurnTimerRemainingMilliseconds(BattleStateViewDto view)
        {
            if (view == null || view.TurnTimerDeadlineUnixTimeMilliseconds <= 0)
            {
                return 0;
            }

            var localUtcNowMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var estimatedServerNowMilliseconds = _hasServerClockOffset
                ? localUtcNowMilliseconds + _serverClockOffsetMilliseconds
                : view.ServerUnixTimeMilliseconds > 0
                    ? view.ServerUnixTimeMilliseconds
                    : localUtcNowMilliseconds;
            var remainingMilliseconds = view.TurnTimerDeadlineUnixTimeMilliseconds - estimatedServerNowMilliseconds;
            var maxDurationMilliseconds = Math.Max(1, view.TurnTimerDurationSeconds) * 1000L;

            if (remainingMilliseconds < 0)
            {
                return 0;
            }

            if (remainingMilliseconds <= maxDurationMilliseconds + 1000L)
            {
                return Math.Min(remainingMilliseconds, maxDurationMilliseconds);
            }

            if (view.ServerUnixTimeMilliseconds > 0)
            {
                var serverRelativeRemaining = view.TurnTimerDeadlineUnixTimeMilliseconds - view.ServerUnixTimeMilliseconds;
                return Math.Max(0, Math.Min(serverRelativeRemaining, maxDurationMilliseconds));
            }

            return maxDurationMilliseconds;
        }

        private static string FormatFirstGoblin(BattleStateViewDto view)
        {
            if (view.Occupants == null)
            {
                return string.Empty;
            }

            foreach (var occupant in view.Occupants)
            {
                if (!string.Equals(occupant.CardId, "Goblin", StringComparison.Ordinal) || occupant.Coord == null)
                {
                    continue;
                }

                return $" goblin={FormatSeatLabel(occupant.OwnerOnlineSeatId, occupant.OwnerId)}({occupant.Coord.Column},{occupant.Coord.Row})";
            }

            return string.Empty;
        }

        private static string FormatMasterHpSummary(BattleStateViewDto view)
        {
            if (view.Occupants == null)
            {
                return string.Empty;
            }

            int? viewerMasterHp = null;
            int? opponentMasterHp = null;
            foreach (var occupant in view.Occupants)
            {
                if (occupant.Kind != OccupantKind.Master)
                {
                    continue;
                }

                if (occupant.OwnerId == view.ViewerId)
                {
                    viewerMasterHp = occupant.CurrentHp;
                }
                else
                {
                    opponentMasterHp = occupant.CurrentHp;
                }
            }

            var viewerText = viewerMasterHp.HasValue ? viewerMasterHp.Value.ToString() : "?";
            var opponentText = opponentMasterHp.HasValue ? opponentMasterHp.Value.ToString() : "?";
            return $" myMasterHp={viewerText} opponentMasterHp={opponentText}";
        }

        private void SetStatus(string status)
        {
            _status = status ?? string.Empty;
            Debug.Log($"[After333 Online Test:{_playerToken}/{FormatLocalSeatLabel()}] {_status}");
        }

        private string FormatLocalSeatLabel()
        {
            return _localOnlineSeatId == OnlineBattleSeatId.None
                ? _localPlayerId.ToString()
                : _localOnlineSeatId.ToString();
        }

        private static string FormatSeatLabel(OnlineBattleSeatId onlineSeatId, PlayerId fallbackPlayerId)
        {
            return onlineSeatId == OnlineBattleSeatId.None
                ? fallbackPlayerId.ToString()
                : onlineSeatId.ToString();
        }

        private bool EnsurePlayMode(string action)
        {
            if (UnityEngine.Application.isPlaying)
            {
                return true;
            }

            SetStatus($"Enter Play Mode before trying to {action}.");
            return false;
        }

        private sealed class QueuedOnlineBattleEnvelope
        {
            public QueuedOnlineBattleEnvelope(OnlineBattleEnvelope envelope, long receivedLocalUnixTimeMilliseconds)
            {
                Envelope = envelope;
                ReceivedLocalUnixTimeMilliseconds = receivedLocalUnixTimeMilliseconds;
            }

            public OnlineBattleEnvelope Envelope { get; }

            public long ReceivedLocalUnixTimeMilliseconds { get; }
        }
    }
}
