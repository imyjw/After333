using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.PvpServer.Messages;
using Project333.PvpServer.BattleResults;
using ServerClientBattleCommandMessage = Project333.PvpServer.Messages.ClientBattleCommandMessage;
using ServerTileCoordDto = Project333.PvpServer.Messages.TileCoordDto;
using ServerOnlineBattleCommandType = Project333.PvpServer.Messages.OnlineBattleCommandType;

namespace Project333.PvpServer.BattleSessions;

public sealed class BattleSession
{
    private static readonly PlayerIdDto[] SeatOrder = { PlayerIdDto.Player, PlayerIdDto.AI };
    private static readonly TimeSpan TurnDuration = TimeSpan.FromSeconds(73);
    private static readonly TimeSpan MulliganDuration = TimeSpan.FromSeconds(33);
    private static readonly TimeSpan MulliganResultPresentationDuration = TimeSpan.FromSeconds(3);
    private const int MaximumConnections = 2;
    private const int MaximumCombatLogEntries = 33;
    private const string CheonraJimangEffectId = "cheonra_jimang";

    private readonly object _gate = new();
    private readonly Dictionary<string, BattleClientConnection> _connections = new();
    private readonly Dictionary<PlayerIdDto, PendingDisconnectReservation> _pendingDisconnectReservations = new();
    private readonly Dictionary<PlayerIdDto, BattleSeatMetadata> _seatMetadata = new();
    private readonly BattleStateViewFactory _stateViewFactory = new();
    private readonly RandomFirstPlayerSelector _firstPlayerSelector;
    private readonly List<BattleCombatLogEntryDto> _combatLogRecords = new();

    private BattleFlowController? _battleFlowController;
    private ICardDefinitionProvider? _cardDefinitionProvider;
    private ICardUpgradeLevelProvider _cardDisplayUpgradeLevels = ZeroCardUpgradeLevelProvider.Instance;
    private AiDecisionService? _aiDecisionService;
    private bool _aiPlanning;
    private Guid _resultId = Guid.NewGuid();
    private string _resultEndedReason = "normal";
    private BattleResult? _finalResult;
    private bool _resultQueued;
    private readonly BattleResultOutbox? _resultOutbox;
    private long _turnTimerVersion;
    private PlayerId _turnTimerPlayerId;
    private DateTimeOffset _turnTimerDeadlineUtc;
    private long _combatLogSequence;

    public BattleSession(
        string matchId,
        bool useServerAiOpponent,
        RandomFirstPlayerSelector? firstPlayerSelector = null,
        BattleResultOutbox? resultOutbox = null)
    {
        MatchId = matchId;
        UseServerAiOpponent = useServerAiOpponent;
        _firstPlayerSelector = firstPlayerSelector ?? new RandomFirstPlayerSelector();
        _resultOutbox = resultOutbox;
    }

    public string MatchId { get; }

    public bool UseServerAiOpponent { get; }

    public int RequiredHumanConnections => UseServerAiOpponent ? 1 : 2;

    public int ConnectionCount
    {
        get
        {
            lock (_gate)
            {
                return _connections.Count;
            }
        }
    }

    public bool IsBattleStarted
    {
        get
        {
            lock (_gate)
            {
                return _battleFlowController?.CurrentBattleState != null;
            }
        }
    }

    public bool IsBattleEnded
    {
        get
        {
            lock (_gate)
            {
                return _battleFlowController?.CurrentBattleState?.IsEnded == true;
            }
        }
    }

    public bool HasPendingReconnectReservations
    {
        get
        {
            lock (_gate)
            {
                return _pendingDisconnectReservations.Count > 0;
            }
        }
    }

    public IReadOnlyList<BattleClientConnection> SnapshotConnections()
    {
        lock (_gate)
        {
            return _connections.Values.ToList();
        }
    }

    public bool TryStartBattleIfReady(out string message)
    {
        lock (_gate)
        {
            if (_battleFlowController?.CurrentBattleState != null)
            {
                message = "Battle is already running.";
                return false;
            }

            if (_connections.Count < RequiredHumanConnections ||
                _connections.Values.Any(c => c.MatchmakingJoinPending || !c.IsOpen))
            {
                message = $"Waiting for a player. Connections in room: {_connections.Count}/{RequiredHumanConnections}.";
                return false;
            }

            _cardDefinitionProvider = PrototypeCardDefinitions.LoadCardDefinitionProvider();
            var cardUpgradeLevelProvider = CreateCardUpgradeLevelProvider();
            _cardDisplayUpgradeLevels = cardUpgradeLevelProvider;
            _aiDecisionService = UseServerAiOpponent
                ? new AiDecisionService(_cardDefinitionProvider, cardUpgradeLevelProvider)
                : null;
            _battleFlowController = PrototypeCardDefinitions.CreateBattleFlowController(
                _cardDefinitionProvider,
                cardUpgradeLevelProvider);
            var playerDeckCardIds = ResolvePlayerDeckCardIds();
            var selectedAiDeck = UseServerAiOpponent ? PveAiDeckCatalog.Default.SelectDeck() : null;
            var opponentDeckCardIds = UseServerAiOpponent
                ? selectedAiDeck!.CardIds
                : ResolveOpponentDeckCardIds();
            var firstPlayerId = _firstPlayerSelector.SelectFirstPlayer();
            _battleFlowController.StartBattle(new BattleSetupRequest(
                playerDeckCardIds: playerDeckCardIds,
                aiDeckCardIds: opponentDeckCardIds,
                firstPlayerId: firstPlayerId,
                aiMulliganEnabled: !UseServerAiOpponent));

            _combatLogRecords.Clear();
            _combatLogSequence = 0;
            AppendCombatLogRecord(new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.BattleStarted,
                SourceOwnerId = firstPlayerId
            });

            ResetTurnTimerLocked();

            var opponentLabel = UseServerAiOpponent ? "server AI" : "human player";
            var metadataSummary = FormatSeatMetadataSummary();
            if (selectedAiDeck != null)
                metadataSummary += $"AI deck: {selectedAiDeck.Id}. ";
            message = $"Battle started in match {MatchId}. Opponent is {opponentLabel}. PlayerA deck cards: {playerDeckCardIds.Count}. Opponent deck cards: {opponentDeckCardIds.Count}. {metadataSummary}Random first player: {FormatRuntimeSeat(firstPlayerId)}.";
            return true;
        }
    }

    public IReadOnlyList<BattleEventDto> RunServerAiActionIfNeeded(bool forceEndTurn = false)
    {
        BattleState observation;
        BattleState expectedState;
        AiDecisionService planner;
        string expectedPosition;
        lock (_gate)
        {
            var battleEvents = new List<BattleEventDto>();

            if (_aiPlanning || !UseServerAiOpponent ||
                _battleFlowController?.CurrentBattleState == null ||
                _battleFlowController.CurrentBattleState.IsEnded ||
                _battleFlowController.CurrentBattleState.ActivePlayerId != PlayerId.AI)
            {
                return battleEvents;
            }

            if (_aiDecisionService == null)
            {
                throw new InvalidOperationException("Server AI is not initialized for this battle session.");
            }

            var battleState = _battleFlowController.CurrentBattleState;
            var combatLogBeforeSnapshot = CaptureBattleSnapshot(battleState);
            if (battleState.Phase == PhaseType.TurnStart)
            {
                var beforeSnapshot = CaptureBattleSnapshot(battleState);
                _battleFlowController.ResolveTurnStart();
                battleEvents.Add(CreateTurnStartedEvent(PlayerId.AI, _battleFlowController.CurrentBattleState.TurnNumber));
                AppendCardDrawEvents(battleEvents);
                AppendValuePopupEvents(battleEvents, PlayerIdDto.AI);
                AppendRemovedOccupantEvents(battleEvents, beforeSnapshot, CaptureBattleSnapshot(_battleFlowController.CurrentBattleState));
                AppendBattleEndedEventIfNeeded(battleEvents);
                AppendCombatLogEntries(
                    battleEvents,
                    combatLogBeforeSnapshot,
                    CaptureBattleSnapshot(_battleFlowController.CurrentBattleState));
                return battleEvents;
            }

            if (battleState.Phase != PhaseType.Main)
            {
                return battleEvents;
            }

            expectedState = battleState;
            expectedPosition = AiBattleStateCopy.PositionKey(battleState);
            observation = AiBattleStateCopy.Create(battleState, hidePrivateZones: true);
            planner = _aiDecisionService;
            _aiPlanning = true;
        }

        // Do not hold the session lock while searching. Disconnects, timers and state reads remain responsive.
        try
        {
            IBattleCommand command;
            try
            {
                command = forceEndTurn ? new EndTurnCommand() : planner.GetNextCommand(observation);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                Console.WriteLine($"[match:{MatchId}] AI planning failed; ending turn: {ex.Message}");
                command = new EndTurnCommand();
            }
            lock (_gate)
            {
                var battleEvents = new List<BattleEventDto>();
                var current = _battleFlowController?.CurrentBattleState;
                if (!ReferenceEquals(current, expectedState) || current == null || current.IsEnded ||
                    current.ActivePlayerId != PlayerId.AI || current.Phase != PhaseType.Main ||
                    AiBattleStateCopy.PositionKey(current) != expectedPosition) return battleEvents;
                var combatLogBeforeSnapshot = CaptureBattleSnapshot(current);
                try
                {
                    AppendRuntimeCommandEvents(battleEvents, PlayerId.AI, command);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
                {
                    battleEvents.Clear();
                    AppendAiFallbackEndTurnEvents(battleEvents, ex.Message);
                }
                AppendCardDrawEvents(battleEvents);
                if (battleEvents.Count == 0)
                    battleEvents.Add(CreateStateChangedEvent(PlayerIdDto.AI, $"Server AI resolved {command.GetType().Name}."));
                AppendCombatLogEntries(battleEvents, combatLogBeforeSnapshot, CaptureBattleSnapshot(_battleFlowController!.CurrentBattleState));
                return battleEvents;
            }
        }
        finally
        {
            lock (_gate) _aiPlanning = false;
        }
    }

    public IReadOnlyList<BattleEventDto> ApplyCommand(ServerClientBattleCommandMessage command)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        lock (_gate)
        {
            var battleEvents = new List<BattleEventDto>();

            if (_battleFlowController?.CurrentBattleState == null)
            {
                throw new InvalidOperationException("Battle has not started yet.");
            }

            var combatLogBeforeSnapshot = CaptureBattleSnapshot(_battleFlowController.CurrentBattleState);

            switch (command.CommandType)
            {
                case ServerOnlineBattleCommandType.ApplyMulligan:
                {
                    var actorId = ToRuntimePlayerId(command.ActorId);
                    _battleFlowController.ApplyMulligan(
                        actorId,
                        command.SelectedCardIds,
                        new SystemDeckShuffler());
                    battleEvents.Add(CreateStateChangedEvent(
                        command.ActorId,
                        $"Mulligan confirmed with {command.SelectedCardIds.Count} replacement card(s)."));
                    CompleteMulliganIfReadyLocked();
                    AppendTurnStartedEventAfterMulliganIfReady(battleEvents);
                    break;
                }

                case ServerOnlineBattleCommandType.PassMulligan:
                {
                    _battleFlowController.PassMulligan(ToRuntimePlayerId(command.ActorId));
                    battleEvents.Add(CreateStateChangedEvent(
                        command.ActorId,
                        "Mulligan confirmed with the current hand."));
                    CompleteMulliganIfReadyLocked();
                    AppendTurnStartedEventAfterMulliganIfReady(battleEvents);
                    break;
                }

                case ServerOnlineBattleCommandType.PlayUnitCard:
                {
                    var actorId = ToRuntimePlayerId(command.ActorId);
                    _battleFlowController.ExecuteCommand(
                        actorId,
                        new PlayUnitCardCommand(
                            command.CardId,
                            ToTileCoord(command.TargetCoord, "target"),
                            command.HandCardRuntimeId));
                    battleEvents.Add(CreateCardPlayedEvent(command, actorId));
                    break;
                }

                case ServerOnlineBattleCommandType.PlayBuildingCard:
                {
                    var actorId = ToRuntimePlayerId(command.ActorId);
                    _battleFlowController.ExecuteCommand(
                        actorId,
                        new PlayBuildingCardCommand(
                            command.CardId,
                            ToTileCoord(command.TargetCoord, "target"),
                            command.HandCardRuntimeId));
                    battleEvents.Add(CreateCardPlayedEvent(command, actorId));
                    break;
                }

                case ServerOnlineBattleCommandType.CastDamageSpell:
                {
                    var cardSpellDamage = CaptureCardSpellDamage(command.CardId, ToRuntimePlayerId(command.ActorId));
                    var beforeSnapshot = CaptureBattleSnapshot(_battleFlowController.CurrentBattleState);
                    _battleFlowController.ExecuteCommand(
                        ToRuntimePlayerId(command.ActorId),
                        new CastDamageSpellCommand(
                            command.CardId,
                            ToRuntimePlayerId(command.TargetOwnerId),
                            ToTileCoord(command.TargetCoord, "target"),
                            command.HandCardRuntimeId));
                    battleEvents.Add(CreateSpellCastEvent(command, SumDamageValuePopupEvents(), cardSpellDamage));
                    AppendValuePopupEvents(battleEvents, command.ActorId);
                    AppendRemovedOccupantEvents(battleEvents, beforeSnapshot, CaptureBattleSnapshot(_battleFlowController.CurrentBattleState));
                    AppendBattleEndedEventIfNeeded(battleEvents);
                    break;
                }

                case ServerOnlineBattleCommandType.CastPersistentResourceSpell:
                    _battleFlowController.ExecuteCommand(
                        ToRuntimePlayerId(command.ActorId),
                        new CastPersistentResourceSpellCommand(
                            command.CardId,
                            command.HandCardRuntimeId));
                    battleEvents.Add(CreateSpellCastEvent(command.CardId, command.ActorId));
                    break;

                case ServerOnlineBattleCommandType.CastScriptedSpell:
                {
                    var cardSpellDamage = CaptureCardSpellDamage(command.CardId, ToRuntimePlayerId(command.ActorId));
                    var beforeSnapshot = CaptureBattleSnapshot(_battleFlowController.CurrentBattleState);
                    var selectedTargetCoords = ToTileCoords(command.SelectedTargetCoords);
                    var hasMultipleTargets = selectedTargetCoords.Count > 0;
                    _battleFlowController.ExecuteCommand(
                        ToRuntimePlayerId(command.ActorId),
                        hasMultipleTargets
                            ? new CastScriptedSpellCommand(
                                command.CardId,
                                selectedTargetCoords,
                                command.HandCardRuntimeId)
                            : command.HasTarget
                            ? new CastScriptedSpellCommand(
                                command.CardId,
                                ToRuntimePlayerId(command.TargetOwnerId),
                                ToTileCoord(command.TargetCoord, "target"),
                                command.HandCardRuntimeId)
                            : new CastScriptedSpellCommand(
                                command.CardId,
                                command.HandCardRuntimeId));
                    var afterSnapshot = CaptureBattleSnapshot(_battleFlowController.CurrentBattleState);
                    if (hasMultipleTargets)
                    {
                        AppendRobotFusionEvents(
                            battleEvents,
                            beforeSnapshot,
                            afterSnapshot,
                            command.ActorId,
                            selectedTargetCoords);
                    }
                    else
                    {
                        battleEvents.Add(command.HasTarget
                            ? CreateSpellCastEvent(command, SumDamageValuePopupEvents(), cardSpellDamage)
                            : CreateSpellCastEvent(command.CardId, command.ActorId, cardSpellDamage));
                        AppendMovedOccupantEvents(battleEvents, beforeSnapshot, afterSnapshot);
                    }

                    break;
                }

                case ServerOnlineBattleCommandType.MoveOccupant:
                {
                    var beforeSnapshot = CaptureBattleSnapshot(_battleFlowController.CurrentBattleState);
                    _battleFlowController.ExecuteCommand(
                        ToRuntimePlayerId(command.ActorId),
                        new MoveOccupantCommand(
                            ToTileCoord(command.SourceCoord, "source"),
                            ToTileCoord(command.DestinationCoord, "destination")));
                    AppendMovedOccupantEvents(battleEvents, beforeSnapshot, CaptureBattleSnapshot(_battleFlowController.CurrentBattleState));
                    break;
                }

                case ServerOnlineBattleCommandType.Attack:
                {
                    var beforeSnapshot = CaptureBattleSnapshot(_battleFlowController.CurrentBattleState);
                    var attackEvent = CreateAttackStartedEvent(command);
                    battleEvents.Add(attackEvent);
                    _battleFlowController.ExecuteCommand(
                        ToRuntimePlayerId(command.ActorId),
                        new AttackCommand(
                            ToTileCoord(command.SourceCoord, "source"),
                            ToTileCoord(command.TargetCoord, "target")));
                    ApplyResolvedAttackTarget(attackEvent, battleEvents);
                    AppendValuePopupEvents(battleEvents, command.ActorId);
                    AppendRemovedOccupantEvents(battleEvents, beforeSnapshot, CaptureBattleSnapshot(_battleFlowController.CurrentBattleState));
                    AppendBattleEndedEventIfNeeded(battleEvents);
                    break;
                }

                case ServerOnlineBattleCommandType.EndTurn:
                {
                    var beforeSnapshot = CaptureBattleSnapshot(_battleFlowController.CurrentBattleState);
                    var previousActivePlayer = _battleFlowController.CurrentBattleState.ActivePlayerId;
                    _battleFlowController.ExecuteCommand(ToRuntimePlayerId(command.ActorId), new EndTurnCommand());
                    ResolveTurnStartIfNeeded();
                    battleEvents.Add(CreateTurnEndedEvent(previousActivePlayer));
                    battleEvents.Add(CreateTurnStartedEvent(_battleFlowController.CurrentBattleState.ActivePlayerId, _battleFlowController.CurrentBattleState.TurnNumber));
                    AppendValuePopupEvents(battleEvents, command.ActorId);
                    AppendRemovedOccupantEvents(battleEvents, beforeSnapshot, CaptureBattleSnapshot(_battleFlowController.CurrentBattleState));
                    AppendBattleEndedEventIfNeeded(battleEvents);
                    ResetTurnTimerLocked();
                    break;
                }

                case ServerOnlineBattleCommandType.Surrender:
                {
                    var battleState = _battleFlowController.CurrentBattleState;
                    if (battleState.IsEnded)
                    {
                        throw new InvalidOperationException("Battle already ended.");
                    }

                    var actorId = ToRuntimePlayerId(command.ActorId);
                    var winnerId = battleState.GetOpponent(actorId).Id;
                    _resultEndedReason = "forfeit";
                    battleState.EndBattle(winnerId);
                    battleEvents.Add(CreateStateChangedEvent(
                        command.ActorId,
                        $"{FormatRuntimeSeat(actorId)} surrendered."));
                    AppendBattleEndedEventIfNeeded(battleEvents);
                    ResetTurnTimerLocked();
                    break;
                }

                default:
                    throw new InvalidOperationException($"Server-side command resolution is not implemented yet for '{command.CommandType}'.");
            }

            if (_battleFlowController?.CurrentBattleState?.IsEnded == true)
            {
                ResetTurnTimerLocked();
            }

            AppendCardDrawEvents(battleEvents);
            AppendCombatLogEntries(
                battleEvents,
                combatLogBeforeSnapshot,
                CaptureBattleSnapshot(_battleFlowController?.CurrentBattleState));

            return battleEvents;
        }
    }

    public OnlineBattleEnvelope CreateStateViewEnvelope(BattleClientConnection connection)
    {
        if (connection == null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        lock (_gate)
        {
            if (_battleFlowController?.CurrentBattleState == null)
            {
                throw new InvalidOperationException("Battle has not started yet.");
            }

            var viewerId = ToRuntimePlayerId(connection.AssignedSeatId);

            var stateView = _stateViewFactory.CreateForPlayer(_battleFlowController.CurrentBattleState, MatchId, viewerId);
            ApplyOnlineSeatLabels(stateView);
            ApplyTurnTimer(stateView);
            ApplyCombatLog(stateView, connection);

            return new OnlineBattleEnvelope
            {
                MessageType = OnlineBattleMessageType.StateView,
                MatchId = MatchId,
                PlayerToken = connection.PlayerToken,
                AccountId = connection.AccountId,
                ConnectionId = connection.ConnectionId,
                ConnectionCount = _connections.Count,
                HasAssignedSeat = connection.HasAssignedSeat,
                AssignedSeatId = connection.AssignedSeatId,
                AssignedOnlineSeatId = connection.AssignedOnlineSeatId,
                StateView = stateView
            };
        }
    }

    public BattleSessionPersistenceSnapshot CreatePersistenceSnapshot(string reason)
    {
        lock (_gate)
        {
            BattleStateViewDto? playerView = null;
            BattleStateViewDto? aiView = null;
            var battleState = _battleFlowController?.CurrentBattleState;
            if (battleState != null)
            {
                playerView = _stateViewFactory.CreateForPlayer(battleState, MatchId, PlayerId.Player);
                aiView = _stateViewFactory.CreateForPlayer(battleState, MatchId, PlayerId.AI);
                ApplyOnlineSeatLabels(playerView);
                ApplyOnlineSeatLabels(aiView);
                ApplyTurnTimer(playerView);
                ApplyTurnTimer(aiView);
            }

            var connections = _connections.Values.Select(CreateConnectionSnapshot).ToList();

            return new BattleSessionPersistenceSnapshot(
                MatchId,
                UseServerAiOpponent,
                ConnectionCount,
                IsBattleStarted,
                IsBattleEnded,
                string.IsNullOrWhiteSpace(reason) ? "manual" : reason.Trim(),
                DateTimeOffset.UtcNow,
                CreateDomainSnapshot(battleState),
                playerView,
                aiView,
                connections,
                _combatLogRecords.ToList(),
                _seatMetadata.Select(pair => new BattleSessionParticipantSnapshot(
                    pair.Key.ToString(), ToOnlineSeatId(pair.Key).ToString(),
                    pair.Value.AccountId, pair.Value.RunId, pair.Value.DeckId,
                    pair.Value.DeckCardIds.ToArray(),
                    new Dictionary<string, int>(pair.Value.CardUpgradeLevels))).ToArray(),
                _resultId, _resultEndedReason);
        }
    }

    public bool RestoreFromPersistenceSnapshot(BattleSessionPersistenceSnapshot snapshot)
    {
        if (snapshot?.DomainState == null ||
            !string.Equals(snapshot.MatchId, MatchId, StringComparison.Ordinal))
        {
            return false;
        }

        lock (_gate)
        {
            if (_battleFlowController?.CurrentBattleState != null)
            {
                return false;
            }

            RestoreSeatMetadata(snapshot);
            // Legacy active snapshots use a stable server-derived ID on repeated recovery.
            // Already-ended legacy snapshots have no receipt history; do not replay them.
            if (snapshot.ResultId == Guid.Empty && snapshot.IsBattleEnded)
                throw new InvalidOperationException("A legacy ended battle has no safe result replay identity.");
            _resultId = snapshot.ResultId != Guid.Empty ? snapshot.ResultId : new Guid(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("legacy-battle:" + MatchId)).AsSpan(0, 16));
            _resultEndedReason = snapshot.ResultEndedReason;
            _cardDefinitionProvider = PrototypeCardDefinitions.LoadCardDefinitionProvider();
            var cardUpgradeLevelProvider = CreateCardUpgradeLevelProvider();
            _aiDecisionService = UseServerAiOpponent
                ? new AiDecisionService(_cardDefinitionProvider, cardUpgradeLevelProvider)
                : null;
            _cardDisplayUpgradeLevels = cardUpgradeLevelProvider;
            _battleFlowController = PrototypeCardDefinitions.CreateBattleFlowController(
                _cardDefinitionProvider,
                cardUpgradeLevelProvider);
            _battleFlowController.RestoreBattleState(RestoreBattleState(
                snapshot.DomainState,
                _cardDefinitionProvider));
            _turnTimerVersion = snapshot.DomainState.TurnTimerVersion;
            _turnTimerPlayerId = ParseRuntimePlayerId(snapshot.DomainState.TurnTimerPlayerId, PlayerId.Player);
            _turnTimerDeadlineUtc = snapshot.DomainState.TurnTimerDeadlineUtc;

            _combatLogRecords.Clear();
            foreach (var record in snapshot.CombatLogEntries ?? Array.Empty<BattleCombatLogEntryDto>())
            {
                if (record == null || record.EntryType == BattleCombatLogEntryType.Unknown)
                {
                    continue;
                }

                _combatLogRecords.Add(record);
            }

            while (_combatLogRecords.Count > MaximumCombatLogEntries)
            {
                _combatLogRecords.RemoveAt(0);
            }

            _combatLogSequence = _combatLogRecords.Count == 0
                ? 0
                : _combatLogRecords.Max(record => record.Sequence);

            return true;
        }
    }

    public bool TryRestorePendingReconnectReservation(
        string runtimeSeatId,
        string onlineSeatId,
        string accountId,
        string playerToken,
        DateTimeOffset reconnectDeadlineUtc)
    {
        lock (_gate)
        {
            if (UseServerAiOpponent ||
                _battleFlowController?.CurrentBattleState == null ||
                _battleFlowController.CurrentBattleState.IsEnded ||
                reconnectDeadlineUtc <= DateTimeOffset.UtcNow)
            {
                return false;
            }

            if (!Enum.TryParse<PlayerIdDto>(runtimeSeatId, out var seatId) || !SeatOrder.Contains(seatId) ||
                !Enum.TryParse<OnlineBattleSeatId>(onlineSeatId, out var parsedOnlineSeatId))
            {
                return false;
            }
            if (!_seatMetadata.TryGetValue(seatId, out var participant) ||
                parsedOnlineSeatId != ToOnlineSeatId(seatId) ||
                !string.Equals(participant.ReconnectIdentityKey,
                    string.IsNullOrWhiteSpace(accountId) ? playerToken : accountId.Trim(), StringComparison.Ordinal))
            {
                return false;
            }
            _pendingDisconnectReservations[seatId] = new PendingDisconnectReservation(
                seatId,
                string.IsNullOrWhiteSpace(accountId) ? playerToken ?? string.Empty : accountId.Trim(),
                accountId ?? string.Empty,
                playerToken ?? string.Empty,
                parsedOnlineSeatId,
                reconnectDeadlineUtc);
            return true;
        }
    }

    public bool TryGetCurrentTurnTimer(
        out PlayerIdDto activeSeatId,
        out long timerVersion,
        out DateTimeOffset deadlineUtc)
    {
        lock (_gate)
        {
            activeSeatId = PlayerIdDto.Player;
            timerVersion = 0;
            deadlineUtc = default;

            var battleState = _battleFlowController?.CurrentBattleState;
            if (battleState == null ||
                battleState.IsEnded ||
                (battleState.Phase != PhaseType.Mulligan && battleState.Phase != PhaseType.Main))
            {
                return false;
            }

            activeSeatId = ToDtoPlayerId(battleState.ActivePlayerId);
            timerVersion = _turnTimerVersion;
            deadlineUtc = _turnTimerDeadlineUtc;
            return timerVersion > 0 && deadlineUtc != default;
        }
    }

    public TimeSpan GetServerAiActionStartDelay()
    {
        lock (_gate)
        {
            var battleState = _battleFlowController?.CurrentBattleState;
            if (!UseServerAiOpponent ||
                battleState == null ||
                battleState.IsEnded ||
                battleState.Phase != PhaseType.Main ||
                battleState.ActivePlayerId != PlayerId.AI ||
                _turnTimerDeadlineUtc == default)
            {
                return TimeSpan.Zero;
            }

            // The first post-mulligan timer includes the result presentation delay.
            // Deriving the turn start from its deadline also keeps restored snapshots consistent.
            var scheduledTurnStartUtc = _turnTimerDeadlineUtc - TurnDuration;
            var remaining = scheduledTurnStartUtc - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public bool TryCreateTurnTimeoutEnvelope(
        PlayerIdDto expectedActiveSeatId,
        long expectedTimerVersion,
        DateTimeOffset expectedDeadlineUtc,
        out OnlineBattleEnvelope? envelope)
    {
        lock (_gate)
        {
            envelope = null;

            var battleFlowController = _battleFlowController;
            var battleState = battleFlowController?.CurrentBattleState;
            if (battleFlowController == null ||
                battleState == null ||
                battleState.IsEnded ||
                (battleState.Phase != PhaseType.Mulligan && battleState.Phase != PhaseType.Main) ||
                _turnTimerVersion != expectedTimerVersion ||
                _turnTimerDeadlineUtc != expectedDeadlineUtc ||
                ToDtoPlayerId(battleState.ActivePlayerId) != expectedActiveSeatId ||
                DateTimeOffset.UtcNow < expectedDeadlineUtc)
            {
                return false;
            }

            if (battleState.Phase == PhaseType.Mulligan)
            {
                var combatLogBeforeSnapshot = CaptureBattleSnapshot(battleState);
                var mulliganEvents = new List<BattleEventDto>
                {
                    new BattleEventDto
                    {
                        EventType = BattleEventType.StateChanged,
                        SourceOwnerId = expectedActiveSeatId,
                        TargetOwnerId = expectedActiveSeatId,
                        Amount = (int)Math.Ceiling(MulliganDuration.TotalSeconds),
                        Message = "MulliganTimerExpired"
                    }
                };

                PassMulliganIfNeeded(PlayerId.Player);
                PassMulliganIfNeeded(PlayerId.AI);
                ResolveTurnStartIfNeeded();
                AppendCardDrawEvents(mulliganEvents);
                if (_battleFlowController?.CurrentBattleState != null)
                {
                    mulliganEvents.Add(CreateTurnStartedEvent(
                        _battleFlowController.CurrentBattleState.ActivePlayerId,
                        _battleFlowController.CurrentBattleState.TurnNumber));
                }

                ResetTurnTimerLocked(MulliganResultPresentationDuration);
                envelope = new OnlineBattleEnvelope
                {
                    MessageType = OnlineBattleMessageType.BattleEvents,
                    MatchId = MatchId,
                    ConnectionCount = ConnectionCount
                };

                foreach (var battleEvent in mulliganEvents)
                {
                    envelope.BattleEvents.Add(battleEvent);
                }

                AppendCombatLogEntries(
                    mulliganEvents,
                    combatLogBeforeSnapshot,
                    CaptureBattleSnapshot(_battleFlowController?.CurrentBattleState));

                return true;
            }

            var beforeSnapshot = CaptureBattleSnapshot(battleState);
            var previousActivePlayer = battleState.ActivePlayerId;
            var previousActivePlayerDto = ToDtoPlayerId(previousActivePlayer);
            var battleEvents = new List<BattleEventDto>
            {
                new BattleEventDto
                {
                    EventType = BattleEventType.TurnTimerExpired,
                    SourceOwnerId = previousActivePlayerDto,
                    TargetOwnerId = previousActivePlayerDto,
                    Amount = (int)Math.Ceiling(TurnDuration.TotalSeconds),
                    Message = $"TurnTimerExpired {FormatRuntimeSeat(previousActivePlayer)}"
                }
            };

            battleFlowController.ExecuteCommand(previousActivePlayer, new EndTurnCommand());
            ResolveTurnStartIfNeeded();
            var afterState = battleFlowController.CurrentBattleState;
            battleEvents.Add(CreateTurnEndedEvent(previousActivePlayer));
            if (afterState != null)
            {
                battleEvents.Add(CreateTurnStartedEvent(afterState.ActivePlayerId, afterState.TurnNumber));
            }

            AppendCardDrawEvents(battleEvents);
            AppendValuePopupEvents(battleEvents, previousActivePlayerDto);
            AppendRemovedOccupantEvents(
                battleEvents,
                beforeSnapshot,
                CaptureBattleSnapshot(afterState));
            AppendBattleEndedEventIfNeeded(battleEvents);
            ResetTurnTimerLocked();

            AppendCombatLogEntries(
                battleEvents,
                beforeSnapshot,
                CaptureBattleSnapshot(afterState));

            envelope = new OnlineBattleEnvelope
            {
                MessageType = OnlineBattleMessageType.BattleEvents,
                MatchId = MatchId,
                ConnectionCount = ConnectionCount
            };

            foreach (var battleEvent in battleEvents)
            {
                envelope.BattleEvents.Add(battleEvent);
            }

            return true;
        }
    }

    public bool TryAddConnection(BattleClientConnection connection, out string errorMessage)
    {
        lock (_gate)
        {
            if (_connections.Count >= MaximumConnections || _connections.Values.Any(c =>
                string.Equals(GetReconnectIdentityKey(c), GetReconnectIdentityKey(connection), StringComparison.Ordinal)))
            {
                errorMessage = $"Match {MatchId} is full.";
                return false;
            }

            if (!TryAssignSeat(connection))
            {
                errorMessage = $"Match {MatchId} is full.";
                return false;
            }

            _connections[connection.ConnectionId] = connection;
            RecordConnectionMetadata(connection);
            errorMessage = string.Empty;
            return true;
        }
    }

    public bool CanReplaceConnectionForReconnect(
        string reconnectIdentityKey,
        string previousConnectionId)
    {
        if (string.IsNullOrWhiteSpace(reconnectIdentityKey) ||
            string.IsNullOrWhiteSpace(previousConnectionId))
        {
            return false;
        }

        lock (_gate)
        {
            return _battleFlowController?.CurrentBattleState != null &&
                   !_battleFlowController.CurrentBattleState.IsEnded &&
                   _connections.TryGetValue(previousConnectionId, out var existing) &&
                   existing.HasAssignedSeat &&
                   string.Equals(
                       GetReconnectIdentityKey(existing),
                       reconnectIdentityKey,
                       StringComparison.Ordinal);
        }
    }

    public bool TryReplaceConnectionForReconnect(
        BattleClientConnection replacement,
        string previousConnectionId,
        out BattleClientConnection? replacedConnection,
        out string errorMessage)
    {
        if (replacement == null)
        {
            throw new ArgumentNullException(nameof(replacement));
        }

        lock (_gate)
        {
            replacedConnection = null;
            var reconnectIdentityKey = GetReconnectIdentityKey(replacement);
            if (_battleFlowController?.CurrentBattleState == null ||
                _battleFlowController.CurrentBattleState.IsEnded ||
                string.IsNullOrWhiteSpace(reconnectIdentityKey) ||
                string.IsNullOrWhiteSpace(previousConnectionId) ||
                !_connections.TryGetValue(previousConnectionId, out var existing) ||
                !existing.HasAssignedSeat ||
                !_seatMetadata.TryGetValue(existing.AssignedSeatId, out var participant) ||
                !string.Equals(participant.ReconnectIdentityKey, reconnectIdentityKey, StringComparison.Ordinal) ||
                !string.Equals(
                    GetReconnectIdentityKey(existing),
                    reconnectIdentityKey,
                    StringComparison.Ordinal))
            {
                errorMessage = "The previous battle connection could not be verified for reconnect takeover.";
                return false;
            }

            existing.MarkSuperseded();
            _connections.Remove(existing.ConnectionId);
            _pendingDisconnectReservations.Remove(existing.AssignedSeatId);

            replacement.AssignedSeatId = existing.AssignedSeatId;
            replacement.AssignedOnlineSeatId = existing.AssignedOnlineSeatId;
            replacement.HasAssignedSeat = true;
            replacement.ReconnectedToPendingSeat = true;
            _connections[replacement.ConnectionId] = replacement;
            RecordConnectionMetadata(replacement);

            replacedConnection = existing;
            errorMessage = string.Empty;
            return true;
        }
    }

    public void RefreshConnectionMetadata(BattleClientConnection connection)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(connection.ConnectionId, out var registered) ||
                !ReferenceEquals(registered, connection))
            {
                throw new InvalidOperationException("Only the registered connection may refresh its participant metadata.");
            }
            RecordConnectionMetadata(connection);
        }
    }

    public bool RemoveConnection(string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.Remove(connectionId, out var removed))
            {
                return false;
            }
            if (_battleFlowController?.CurrentBattleState == null)
            {
                _seatMetadata.Remove(removed.AssignedSeatId);
            }
            return true;
        }
    }

    public BattleResult? GetFinalResult()
    {
        lock (_gate)
        {
            if (_finalResult != null) return _finalResult;
            var state = _battleFlowController?.CurrentBattleState;
            if (state?.Result == null || !state.Result.HasResult) return null;
            var records = new List<BattleRunResultRecord>();
            foreach (var seatId in SeatOrder)
            {
                if (!_seatMetadata.TryGetValue(seatId, out var metadata) ||
                    string.IsNullOrWhiteSpace(metadata.AccountId) ||
                    string.IsNullOrWhiteSpace(metadata.RunId) || string.IsNullOrWhiteSpace(metadata.DeckId)) continue;
                records.Add(new BattleRunResultRecord(seatId, ToOnlineSeatId(seatId), metadata.AccountId,
                    metadata.RunId, metadata.DeckId, !state.Result.IsDraw && seatId == ToDtoPlayerId(state.Result.Winner)));
            }
            _finalResult = new BattleResult(_resultId, MatchId, UseServerAiOpponent, state.Result.IsDraw,
                state.Result.IsDraw ? OnlineBattleSeatId.None : ToOnlineSeatId(state.Result.Winner),
                _resultEndedReason, records).ValidatedCopy();
            return _finalResult;
        }
    }

    public void ConfirmMatchmakingJoin(BattleClientConnection connection)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(connection.ConnectionId, out var registered) || !ReferenceEquals(registered, connection))
                throw new InvalidOperationException("Only the registered connection can complete matchmaking.");
            connection.MatchmakingJoinPending = false;
            connection.MatchmakingReservationId = null;
        }
    }

    public BattleDisconnect? DetachConnection(BattleClientConnection connection, TimeSpan gracePeriod)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(connection.ConnectionId, out var registered) ||
                !ReferenceEquals(registered, connection)) return null;
            var reserved = TryCreateDisconnectReconnectGraceEnvelope(connection, gracePeriod,
                out var envelope, out var seat, out var identity, out var deadline);
            // Reservation and removal are atomic with respect to a reconnect/takeover.
            RemoveConnection(connection.ConnectionId);
            return new BattleDisconnect(reserved, envelope, seat, identity, deadline);
        }
    }

    private void CaptureResultIfEnded()
    {
        if (_resultQueued || _resultOutbox == null) return;
        var result = GetFinalResult();
        if (result == null) return;
        _resultOutbox.Capture(result);
        _resultQueued = true;
    }

    public Task PersistResultAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            CaptureResultIfEnded();
            return _finalResult == null || _resultOutbox == null ? Task.CompletedTask :
                _resultOutbox.DeliverAsync(_resultId, cancellationToken);
        }
    }


    public bool TryDelayCurrentPlayerTurnTimerForServerAiPresentation(TimeSpan presentationDelay)
    {
        lock (_gate)
        {
            if (!UseServerAiOpponent ||
                presentationDelay <= TimeSpan.Zero ||
                _turnTimerDeadlineUtc == default)
            {
                return false;
            }

            var battleState = _battleFlowController?.CurrentBattleState;
            if (battleState == null ||
                battleState.IsEnded ||
                battleState.Phase != PhaseType.Main ||
                battleState.ActivePlayerId != PlayerId.Player)
            {
                return false;
            }

            _turnTimerDeadlineUtc = _turnTimerDeadlineUtc.Add(presentationDelay);
            return true;
        }
    }

    public bool TryCreateDisconnectReconnectGraceEnvelope(
        BattleClientConnection disconnectedConnection,
        TimeSpan gracePeriod,
        out OnlineBattleEnvelope? envelope,
        out PlayerIdDto disconnectedSeatId,
        out string reconnectIdentityKey,
        out DateTimeOffset reconnectDeadlineUtc)
    {
        if (disconnectedConnection == null)
        {
            throw new ArgumentNullException(nameof(disconnectedConnection));
        }

        lock (_gate)
        {
            envelope = null;
            disconnectedSeatId = disconnectedConnection.AssignedSeatId;
            reconnectIdentityKey = GetReconnectIdentityKey(disconnectedConnection);
            reconnectDeadlineUtc = DateTimeOffset.UtcNow;

            if (disconnectedConnection.IsSuperseded ||
                !_connections.TryGetValue(disconnectedConnection.ConnectionId, out var currentConnection) ||
                !ReferenceEquals(currentConnection, disconnectedConnection) ||
                !disconnectedConnection.HasAssignedSeat ||
                _battleFlowController?.CurrentBattleState == null ||
                _battleFlowController.CurrentBattleState.IsEnded)
            {
                return false;
            }

            var safeGracePeriod = gracePeriod <= TimeSpan.Zero
                ? TimeSpan.FromSeconds(60)
                : gracePeriod;
            disconnectedSeatId = disconnectedConnection.AssignedSeatId;
            reconnectDeadlineUtc = DateTimeOffset.UtcNow.Add(safeGracePeriod);

            _pendingDisconnectReservations[disconnectedSeatId] = new PendingDisconnectReservation(
                disconnectedSeatId,
                reconnectIdentityKey,
                disconnectedConnection.AccountId,
                disconnectedConnection.PlayerToken ?? string.Empty,
                disconnectedConnection.AssignedOnlineSeatId,
                reconnectDeadlineUtc);

            envelope = new OnlineBattleEnvelope
            {
                MessageType = OnlineBattleMessageType.BattleEvents,
                MatchId = MatchId,
                ConnectionCount = Math.Max(0, _connections.Count - 1),
                BattleEvents =
                {
                    new BattleEventDto
                    {
                        EventType = BattleEventType.ReconnectGraceStarted,
                        SourceOwnerId = disconnectedSeatId,
                        TargetOwnerId = disconnectedSeatId,
                        Amount = (int)Math.Ceiling(safeGracePeriod.TotalSeconds),
                        Message = $"{FormatConnection(disconnectedConnection)} disconnected. Waiting {(int)Math.Ceiling(safeGracePeriod.TotalSeconds)} seconds for reconnection before forfeit."
                    }
                }
            };

            return true;
        }
    }

    public bool TryCreateExpiredDisconnectForfeitEnvelope(
        PlayerIdDto disconnectedSeatId,
        string reconnectIdentityKey,
        DateTimeOffset reconnectDeadlineUtc,
        out OnlineBattleEnvelope? envelope)
    {
        lock (_gate)
        {
            envelope = null;

            if (_battleFlowController?.CurrentBattleState == null ||
                _battleFlowController.CurrentBattleState.IsEnded ||
                !_pendingDisconnectReservations.TryGetValue(disconnectedSeatId, out var reservation) ||
                !string.Equals(reservation.ReconnectIdentityKey, reconnectIdentityKey ?? string.Empty, StringComparison.Ordinal) ||
                reservation.ReconnectDeadlineUtc != reconnectDeadlineUtc)
            {
                return false;
            }

            var disconnectedPlayerId = ToRuntimePlayerId(disconnectedSeatId);
            var winnerId = disconnectedPlayerId == PlayerId.Player
                ? PlayerId.AI
                : PlayerId.Player;
            var winnerDto = ToDtoPlayerId(winnerId);
            var combatLogBeforeSnapshot = CaptureBattleSnapshot(_battleFlowController.CurrentBattleState);

            _pendingDisconnectReservations.Clear();
            _battleFlowController.CurrentBattleState.EndBattle(winnerId);
            _resultEndedReason = "reconnect_timeout";
            CaptureResultIfEnded();

            envelope = new OnlineBattleEnvelope
            {
                MessageType = OnlineBattleMessageType.BattleEvents,
                MatchId = MatchId,
                ConnectionCount = _connections.Count,
                BattleEvents =
                {
                    new BattleEventDto
                    {
                        EventType = BattleEventType.StateChanged,
                        SourceOwnerId = winnerDto,
                        TargetOwnerId = winnerDto,
                        Message = $"{FormatSeatForLog(disconnectedSeatId)} did not reconnect in time. {FormatRuntimeSeat(winnerId)} wins by forfeit."
                    },
                    new BattleEventDto
                    {
                        EventType = BattleEventType.BattleEnded,
                        SourceOwnerId = winnerDto,
                        TargetOwnerId = winnerDto,
                        Message = $"BattleEnded winner={FormatRuntimeSeat(winnerId)} by disconnect timeout"
                    }
                }
            };

            AppendCombatLogEntries(
                envelope.BattleEvents,
                combatLogBeforeSnapshot,
                CaptureBattleSnapshot(_battleFlowController.CurrentBattleState));

            return true;
        }
    }

    public bool CanReconnectIdentityKey(string reconnectIdentityKey)
    {
        if (string.IsNullOrWhiteSpace(reconnectIdentityKey))
        {
            return false;
        }

        lock (_gate)
        {
            return !UseServerAiOpponent &&
                   _battleFlowController?.CurrentBattleState != null &&
                   !_battleFlowController.CurrentBattleState.IsEnded &&
                   _pendingDisconnectReservations.Values.Any(reservation =>
                       reservation.ReconnectDeadlineUtc > DateTimeOffset.UtcNow &&
                       string.Equals(reservation.ReconnectIdentityKey, reconnectIdentityKey, StringComparison.Ordinal));
        }
    }

    public bool TryGetReconnectStatus(
        string reconnectIdentityKey,
        out PendingReconnectStatusSnapshot? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(reconnectIdentityKey))
        {
            return false;
        }

        lock (_gate)
        {
            if (UseServerAiOpponent ||
                _battleFlowController?.CurrentBattleState == null ||
                _battleFlowController.CurrentBattleState.IsEnded)
            {
                return false;
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var reservation in _pendingDisconnectReservations.Values)
            {
                if (reservation.ReconnectDeadlineUtc <= now ||
                    !string.Equals(
                        reservation.ReconnectIdentityKey,
                        reconnectIdentityKey,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var remainingSeconds = Math.Max(
                    0,
                    (int)Math.Ceiling((reservation.ReconnectDeadlineUtc - now).TotalSeconds));
                status = new PendingReconnectStatusSnapshot(
                    MatchId,
                    reservation.SeatId,
                    reservation.OnlineSeatId,
                    remainingSeconds,
                    reservation.ReconnectDeadlineUtc);
                return true;
            }

            return false;
        }
    }

    public bool HasConnectedIdentityKey(string reconnectIdentityKey)
    {
        if (string.IsNullOrWhiteSpace(reconnectIdentityKey))
        {
            return false;
        }

        lock (_gate)
        {
            return _connections.Values.Any(connection =>
                string.Equals(GetReconnectIdentityKey(connection), reconnectIdentityKey, StringComparison.Ordinal));
        }
    }

    public OnlineBattleEnvelope CreateConnectedEnvelope(BattleClientConnection connection)
    {
        var actionText = connection.ReconnectedToPendingSeat
            ? "reconnected to"
            : "joined";

        var envelope = new OnlineBattleEnvelope
        {
            MessageType = OnlineBattleMessageType.BattleEvents,
            MatchId = MatchId,
            PlayerToken = connection.PlayerToken,
            AccountId = connection.AccountId,
            ConnectionId = connection.ConnectionId,
            ConnectionCount = ConnectionCount,
            HasAssignedSeat = connection.HasAssignedSeat,
            AssignedSeatId = connection.AssignedSeatId,
            AssignedOnlineSeatId = connection.AssignedOnlineSeatId,
            BattleEvents =
            {
                new BattleEventDto
                {
                    EventType = BattleEventType.StateChanged,
                    SourceOwnerId = connection.AssignedSeatId,
                    Message = $"{FormatConnection(connection)} {actionText} match {MatchId}. Opponent mode: {(UseServerAiOpponent ? "server AI" : "PvP")}. Connections in room: {ConnectionCount}/{RequiredHumanConnections}."
                }
            }
        };

        if (connection.ReconnectedToPendingSeat)
        {
            envelope.BattleEvents.Add(new BattleEventDto
            {
                EventType = BattleEventType.ReconnectGraceCancelled,
                SourceOwnerId = connection.AssignedSeatId,
                TargetOwnerId = connection.AssignedSeatId,
                Message = $"{FormatConnection(connection)} reconnected. Forfeit countdown cancelled."
            });
        }

        return envelope;
    }

    public OnlineBattleEnvelope CreateAckEnvelope(
        BattleClientConnection sender,
        ServerClientBattleCommandMessage command,
        IReadOnlyList<BattleEventDto>? commandEvents = null)
    {
        var envelope = new OnlineBattleEnvelope
        {
            MessageType = OnlineBattleMessageType.BattleEvents,
            MatchId = MatchId,
            PlayerToken = sender.PlayerToken,
            AccountId = sender.AccountId,
            ConnectionId = sender.ConnectionId,
            ConnectionCount = ConnectionCount,
            HasAssignedSeat = sender.HasAssignedSeat,
            AssignedSeatId = sender.AssignedSeatId,
            AssignedOnlineSeatId = sender.AssignedOnlineSeatId,
            BattleEvents =
            {
                new BattleEventDto
                {
                    EventType = BattleEventType.StateChanged,
                    SourceOwnerId = sender.AssignedSeatId,
                    Message = $"ACK {command.CommandType} #{command.Sequence} from {FormatConnection(sender)}"
                }
            }
        };

        if (commandEvents != null)
        {
            foreach (var commandEvent in commandEvents)
            {
                if (commandEvent != null)
                {
                    envelope.BattleEvents.Add(commandEvent);
                }
            }
        }

        return envelope;
    }

    public OnlineBattleEnvelope CreateBattleStartedEnvelope(string message)
    {
        return new OnlineBattleEnvelope
        {
            MessageType = OnlineBattleMessageType.BattleEvents,
            MatchId = MatchId,
            ConnectionCount = ConnectionCount,
            BattleEvents =
            {
                new BattleEventDto
                {
                    EventType = BattleEventType.StateChanged,
                    Message = message
                }
            }
        };
    }

    public OnlineBattleEnvelope CreateServerAiTurnEnvelope(IReadOnlyList<BattleEventDto> aiEvents)
    {
        var envelope = new OnlineBattleEnvelope
        {
            MessageType = OnlineBattleMessageType.BattleEvents,
            MatchId = MatchId,
            ConnectionCount = ConnectionCount,
            BattleEvents =
            {
                new BattleEventDto
                {
                    EventType = BattleEventType.StateChanged,
                    SourceOwnerId = PlayerIdDto.AI,
                    Message = "Server AI resolved its turn."
                }
            }
        };

        if (aiEvents != null)
        {
            foreach (var aiEvent in aiEvents)
            {
                if (aiEvent != null)
                {
                    envelope.BattleEvents.Add(aiEvent);
                }
            }
        }

        return envelope;
    }

    private bool TryAssignSeat(BattleClientConnection connection)
    {
        if (connection.HasAssignedSeat && _battleFlowController?.CurrentBattleState == null)
        {
            return true;
        }

        connection.ReconnectedToPendingSeat = false;

        if (_battleFlowController?.CurrentBattleState != null)
        {
            return TryAssignPendingReconnectSeat(connection);
        }

        foreach (var seatId in SeatOrder)
        {
            if (connection.ReservedOnlineSeatId != OnlineBattleSeatId.None &&
                ToOnlineSeatId(seatId) != connection.ReservedOnlineSeatId) continue;
            if (_connections.Values.Any(existing => existing.HasAssignedSeat && existing.AssignedSeatId == seatId))
            {
                continue;
            }

            connection.AssignedSeatId = seatId;
            connection.AssignedOnlineSeatId = ToOnlineSeatId(seatId);
            connection.HasAssignedSeat = true;
            return true;
        }

        return false;
    }

    private bool TryAssignPendingReconnectSeat(BattleClientConnection connection)
    {
        var reconnectIdentityKey = GetReconnectIdentityKey(connection);
        if (string.IsNullOrWhiteSpace(reconnectIdentityKey))
        {
            return false;
        }

        foreach (var seatId in SeatOrder)
        {
            if (!_pendingDisconnectReservations.TryGetValue(seatId, out var reservation) ||
                reservation.ReconnectDeadlineUtc <= DateTimeOffset.UtcNow ||
                !string.Equals(reservation.ReconnectIdentityKey, reconnectIdentityKey, StringComparison.Ordinal) ||
                !_seatMetadata.TryGetValue(seatId, out var participant) ||
                !string.Equals(participant.ReconnectIdentityKey, reconnectIdentityKey, StringComparison.Ordinal) ||
                _connections.Values.Any(existing => existing.HasAssignedSeat && existing.AssignedSeatId == seatId))
            {
                continue;
            }

            _pendingDisconnectReservations.Remove(seatId);
            connection.AssignedSeatId = seatId;
            connection.AssignedOnlineSeatId = reservation.OnlineSeatId;
            connection.HasAssignedSeat = true;
            connection.ReconnectedToPendingSeat = true;
            return true;
        }

        return false;
    }

    private void AppendRuntimeCommandEvents(List<BattleEventDto> battleEvents, PlayerId actorId, IBattleCommand command)
    {
        if (battleEvents == null)
        {
            return;
        }

        if (_battleFlowController?.CurrentBattleState == null)
        {
            throw new InvalidOperationException("Battle has not started yet.");
        }

        var actorDto = ToDtoPlayerId(actorId);
        switch (command)
        {
            case PlayUnitCardCommand playUnitCardCommand:
                _battleFlowController.ExecuteCommand(actorId, playUnitCardCommand);
                battleEvents.Add(CreateCardPlayedEvent(
                    playUnitCardCommand.CardId,
                    actorId,
                    actorDto,
                    playUnitCardCommand.TargetCoord));
                break;

            case PlayBuildingCardCommand playBuildingCardCommand:
                _battleFlowController.ExecuteCommand(actorId, playBuildingCardCommand);
                battleEvents.Add(CreateCardPlayedEvent(
                    playBuildingCardCommand.CardId,
                    actorId,
                    actorDto,
                    playBuildingCardCommand.TargetCoord));
                break;

            case CastDamageSpellCommand castDamageSpellCommand:
            {
                var cardSpellDamage = CaptureCardSpellDamage(castDamageSpellCommand.CardId, actorId);
                var beforeSnapshot = CaptureBattleSnapshot(_battleFlowController.CurrentBattleState);
                _battleFlowController.ExecuteCommand(actorId, castDamageSpellCommand);
                battleEvents.Add(CreateSpellCastEvent(
                    castDamageSpellCommand.CardId,
                    actorDto,
                    castDamageSpellCommand.TargetOwnerId,
                    castDamageSpellCommand.TargetCoord,
                    SumDamageValuePopupEvents(), cardSpellDamage));
                AppendValuePopupEvents(battleEvents, actorDto);
                AppendRemovedOccupantEvents(battleEvents, beforeSnapshot, CaptureBattleSnapshot(_battleFlowController.CurrentBattleState));
                AppendBattleEndedEventIfNeeded(battleEvents);
                break;
            }

            case CastPersistentResourceSpellCommand castPersistentResourceSpellCommand:
                _battleFlowController.ExecuteCommand(actorId, castPersistentResourceSpellCommand);
                battleEvents.Add(CreateSpellCastEvent(
                    castPersistentResourceSpellCommand.CardId,
                    actorDto));
                break;

            case CastScriptedSpellCommand castScriptedSpellCommand:
            {
                var cardSpellDamage = CaptureCardSpellDamage(castScriptedSpellCommand.CardId, actorId);
                var beforeSnapshot = CaptureBattleSnapshot(_battleFlowController.CurrentBattleState);
                _battleFlowController.ExecuteCommand(actorId, castScriptedSpellCommand);
                var afterSnapshot = CaptureBattleSnapshot(_battleFlowController.CurrentBattleState);
                if (castScriptedSpellCommand.HasMultipleTargets)
                {
                    AppendRobotFusionEvents(
                        battleEvents,
                        beforeSnapshot,
                        afterSnapshot,
                        actorDto,
                        castScriptedSpellCommand.TargetCoords);
                }
                else if (castScriptedSpellCommand.HasTarget)
                {
                    battleEvents.Add(CreateSpellCastEvent(
                        castScriptedSpellCommand.CardId,
                        actorDto,
                        castScriptedSpellCommand.TargetOwnerId,
                        castScriptedSpellCommand.TargetCoord,
                        SumDamageValuePopupEvents(), cardSpellDamage));
                }
                else
                {
                    battleEvents.Add(CreateSpellCastEvent(
                        castScriptedSpellCommand.CardId,
                        actorDto, cardSpellDamage));
                }

                if (!castScriptedSpellCommand.HasMultipleTargets)
                {
                    AppendMovedOccupantEvents(battleEvents, beforeSnapshot, afterSnapshot);
                }

                AppendValuePopupEvents(battleEvents, actorDto);
                if (!castScriptedSpellCommand.HasMultipleTargets)
                {
                    AppendRemovedOccupantEvents(
                        battleEvents,
                        beforeSnapshot,
                        afterSnapshot);
                }

                AppendBattleEndedEventIfNeeded(battleEvents);
                break;
            }

            case MoveOccupantCommand moveOccupantCommand:
            {
                var beforeSnapshot = CaptureBattleSnapshot(_battleFlowController.CurrentBattleState);
                _battleFlowController.ExecuteCommand(actorId, moveOccupantCommand);
                AppendMovedOccupantEvents(battleEvents, beforeSnapshot, CaptureBattleSnapshot(_battleFlowController.CurrentBattleState));
                break;
            }

            case AttackCommand attackCommand:
            {
                var beforeSnapshot = CaptureBattleSnapshot(_battleFlowController.CurrentBattleState);
                var attackEvent = CreateAttackStartedEvent(
                    actorDto,
                    attackCommand.AttackerCoord,
                    GetOpponent(actorDto),
                    attackCommand.TargetCoord);
                battleEvents.Add(attackEvent);
                _battleFlowController.ExecuteCommand(actorId, attackCommand);
                ApplyResolvedAttackTarget(attackEvent, battleEvents);
                AppendValuePopupEvents(battleEvents, actorDto);
                AppendRemovedOccupantEvents(battleEvents, beforeSnapshot, CaptureBattleSnapshot(_battleFlowController.CurrentBattleState));
                AppendBattleEndedEventIfNeeded(battleEvents);
                break;
            }

            case EndTurnCommand endTurnCommand:
            {
                var beforeSnapshot = CaptureBattleSnapshot(_battleFlowController.CurrentBattleState);
                var previousActivePlayer = _battleFlowController.CurrentBattleState.ActivePlayerId;
                _battleFlowController.ExecuteCommand(actorId, endTurnCommand);
                ResolveTurnStartIfNeeded();
                battleEvents.Add(CreateTurnEndedEvent(previousActivePlayer));
                var afterState = _battleFlowController.CurrentBattleState;
                if (afterState != null)
                {
                    battleEvents.Add(CreateTurnStartedEvent(
                        afterState.ActivePlayerId,
                        afterState.TurnNumber));
                }

                AppendValuePopupEvents(battleEvents, actorDto);
                AppendRemovedOccupantEvents(battleEvents, beforeSnapshot, CaptureBattleSnapshot(afterState));
                AppendBattleEndedEventIfNeeded(battleEvents);
                ResetTurnTimerLocked();
                break;
            }

            default:
                throw new InvalidOperationException($"Server AI command resolution is not implemented yet for '{command.GetType().Name}'.");
        }
    }

    private void AppendAiFallbackEndTurnEvents(List<BattleEventDto> battleEvents, string failureMessage)
    {
        if (battleEvents == null)
        {
            return;
        }

        battleEvents.Add(CreateStateChangedEvent(
            PlayerIdDto.AI,
            string.IsNullOrWhiteSpace(failureMessage)
                ? "Server AI command failed. Ending AI turn."
                : $"Server AI command failed: {failureMessage}. Ending AI turn."));

        if (_battleFlowController?.CurrentBattleState == null ||
            _battleFlowController.CurrentBattleState.IsEnded ||
            _battleFlowController.CurrentBattleState.ActivePlayerId != PlayerId.AI ||
            _battleFlowController.CurrentBattleState.Phase != PhaseType.Main)
        {
            AppendBattleEndedEventIfNeeded(battleEvents);
            return;
        }

        var beforeSnapshot = CaptureBattleSnapshot(_battleFlowController.CurrentBattleState);
        var previousActivePlayer = _battleFlowController.CurrentBattleState.ActivePlayerId;
        _battleFlowController.ExecuteCommand(PlayerId.AI, new EndTurnCommand());
        ResolveTurnStartIfNeeded();
        battleEvents.Add(CreateTurnEndedEvent(previousActivePlayer));

        var afterState = _battleFlowController.CurrentBattleState;
        if (afterState != null)
        {
            battleEvents.Add(CreateTurnStartedEvent(afterState.ActivePlayerId, afterState.TurnNumber));
        }

        AppendValuePopupEvents(battleEvents, PlayerIdDto.AI);
        AppendRemovedOccupantEvents(battleEvents, beforeSnapshot, CaptureBattleSnapshot(afterState));
        AppendBattleEndedEventIfNeeded(battleEvents);
        ResetTurnTimerLocked();
    }

    private void ResolveTurnStartIfNeeded()
    {
        if (_battleFlowController?.CurrentBattleState == null ||
            _battleFlowController.CurrentBattleState.IsEnded ||
            _battleFlowController.CurrentBattleState.Phase != PhaseType.TurnStart)
        {
            return;
        }

        _battleFlowController.ResolveTurnStart();
    }

    private void ResetTurnTimerLocked(TimeSpan startDelay = default)
    {
        _turnTimerVersion += 1;

        var battleState = _battleFlowController?.CurrentBattleState;
        if (battleState == null ||
            battleState.IsEnded ||
            (battleState.Phase != PhaseType.Mulligan && battleState.Phase != PhaseType.Main))
        {
            _turnTimerDeadlineUtc = default;
            return;
        }

        _turnTimerPlayerId = battleState.ActivePlayerId;
        var duration = battleState.Phase == PhaseType.Mulligan
            ? MulliganDuration
            : TurnDuration;
        _turnTimerDeadlineUtc = DateTimeOffset.UtcNow.Add(startDelay).Add(duration);
    }

    private void CompleteMulliganIfReadyLocked()
    {
        if (_battleFlowController?.CurrentBattleState?.Phase != PhaseType.TurnStart)
        {
            return;
        }

        ResolveTurnStartIfNeeded();
        ResetTurnTimerLocked(MulliganResultPresentationDuration);
    }

    private void AppendTurnStartedEventAfterMulliganIfReady(List<BattleEventDto> battleEvents)
    {
        var battleState = _battleFlowController?.CurrentBattleState;
        if (battleEvents == null || battleState == null || battleState.Phase != PhaseType.Main ||
            battleEvents.Any(candidate => candidate?.EventType == BattleEventType.TurnStarted))
        {
            return;
        }

        battleEvents.Add(CreateTurnStartedEvent(battleState.ActivePlayerId, battleState.TurnNumber));
    }

    private void PassMulliganIfNeeded(PlayerId playerId)
    {
        if (_battleFlowController?.CurrentBattleState == null ||
            _battleFlowController.CurrentBattleState.Phase != PhaseType.Mulligan)
        {
            return;
        }

        var playerState = _battleFlowController.CurrentBattleState.GetPlayer(playerId);
        if (playerState.HasUsedMulligan)
        {
            return;
        }

        _battleFlowController.PassMulligan(playerId);
    }

    private static PlayerId ToRuntimePlayerId(PlayerIdDto seatId)
    {
        return seatId == PlayerIdDto.Player
            ? PlayerId.Player
            : PlayerId.AI;
    }

    private static TileCoord ToTileCoord(ServerTileCoordDto? coord, string role)
    {
        if (coord == null)
        {
            throw new InvalidOperationException($"The {role} coordinate is required for this command.");
        }

        if (coord.Column < 0 ||
            coord.Column >= BoardState.ColumnCount ||
            coord.Row < 0 ||
            coord.Row >= BoardState.RowCount)
        {
            throw new InvalidOperationException(
                $"The {role} coordinate ({coord.Column},{coord.Row}) is outside the board.");
        }

        return new TileCoord(coord.Column, coord.Row);
    }

    private static IReadOnlyList<TileCoord> ToTileCoords(IReadOnlyList<ServerTileCoordDto>? coords)
    {
        if (coords == null || coords.Count == 0)
        {
            return Array.Empty<TileCoord>();
        }

        var result = new List<TileCoord>(coords.Count);
        for (var index = 0; index < coords.Count; index += 1)
        {
            result.Add(ToTileCoord(coords[index], $"selected target #{index + 1}"));
        }

        return result;
    }

    private int? CaptureCardSpellDamage(string? cardId, PlayerId ownerId)
    {
        if (_cardDefinitionProvider == null || string.IsNullOrWhiteSpace(cardId))
            return null;

        // Capture before resolution: a spell may remove its caster's SpellPower sources.
        var definition = _cardDefinitionProvider.GetRequired(cardId);
        var level = _cardDisplayUpgradeLevels.GetUpgradeLevel(ownerId, cardId);
        var spellPower = SpellPowerRules.GetTotal(_battleFlowController!.CurrentBattleState, ownerId);
        return CardStatDisplay.TryGetSpellDamage(definition, level, spellPower, out var damage)
            ? damage : null;
    }

    private static BattleEventDto CreateSpellCastEvent(ServerClientBattleCommandMessage command, int damageAmount, int? cardSpellDamage = null)
    {
        return CreateSpellCastEvent(
            command.CardId ?? string.Empty,
            command.ActorId,
            ToRuntimePlayerId(command.TargetOwnerId),
            ToTileCoord(command.TargetCoord, "target"),
            damageAmount, cardSpellDamage);
    }

    private static BattleEventDto CreateSpellCastEvent(
        string cardId,
        PlayerIdDto sourceOwnerId,
        PlayerId targetOwnerId,
        TileCoord targetCoord,
        int damageAmount,
        int? cardSpellDamage = null)
    {
        return new BattleEventDto
        {
            EventType = BattleEventType.SpellCast,
            SourceOwnerId = sourceOwnerId,
            TargetOwnerId = ToDtoPlayerId(targetOwnerId),
            TargetCoord = ToServerTileCoord(targetCoord),
            CardId = cardId ?? string.Empty,
            SourceCardId = cardId ?? string.Empty,
            Amount = Math.Max(0, damageAmount),
            CardSpellDamage = cardSpellDamage,
            Message = $"SpellCast {cardId}"
        };
    }

    private static BattleEventDto CreateSpellCastEvent(string cardId, PlayerIdDto sourceOwnerId, int? cardSpellDamage = null)
    {
        return new BattleEventDto
        {
            EventType = BattleEventType.SpellCast,
            SourceOwnerId = sourceOwnerId,
            TargetOwnerId = sourceOwnerId,
            CardId = cardId ?? string.Empty,
            SourceCardId = cardId ?? string.Empty,
            CardSpellDamage = cardSpellDamage,
            Message = $"SpellCast {cardId}"
        };
    }

    private static BattleEventDto CreateStateChangedEvent(PlayerIdDto sourceOwnerId, string message)
    {
        return new BattleEventDto
        {
            EventType = BattleEventType.StateChanged,
            SourceOwnerId = sourceOwnerId,
            TargetOwnerId = sourceOwnerId,
            Message = message ?? string.Empty
        };
    }

    private BattleEventDto CreateCardPlayedEvent(ServerClientBattleCommandMessage command, PlayerId actorId)
    {
        return CreateCardPlayedEvent(
            command.CardId ?? string.Empty,
            actorId,
            command.ActorId,
            ToTileCoord(command.TargetCoord, "target"));
    }

    private BattleEventDto CreateCardPlayedEvent(string cardId, PlayerId actorId, PlayerIdDto sourceOwnerId, TileCoord targetCoord)
    {
        var occupant = _battleFlowController?.CurrentBattleState?.GetBoard(actorId)?.GetOccupant(targetCoord);
        var baseAttack = 0;
        var baseHp = 0;
        if (_cardDefinitionProvider != null && !string.IsNullOrWhiteSpace(cardId))
        {
            try
            {
                switch (_cardDefinitionProvider.GetRequired(cardId))
                {
                    case UnitCardDefinition unitDefinition:
                        baseAttack = unitDefinition.Attack;
                        baseHp = unitDefinition.Health;
                        break;
                    case BuildingCardDefinition buildingDefinition:
                        baseAttack = buildingDefinition.Attack;
                        baseHp = buildingDefinition.Health;
                        break;
                }
            }
            catch
            {
                baseAttack = occupant?.BaseAttack ?? 0;
                baseHp = occupant?.MaxHp ?? 0;
            }
        }

        return new BattleEventDto
        {
            EventType = BattleEventType.CardPlayed,
            SourceOwnerId = sourceOwnerId,
            TargetOwnerId = sourceOwnerId,
            TargetCoord = ToServerTileCoord(targetCoord),
            RuntimeId = occupant?.RuntimeId ?? string.Empty,
            CardId = cardId ?? string.Empty,
            SourceRuntimeId = occupant?.RuntimeId ?? string.Empty,
            SourceCardId = cardId ?? string.Empty,
            TargetRuntimeId = occupant?.RuntimeId ?? string.Empty,
            TargetCardId = cardId ?? string.Empty,
            CardAttack = occupant?.BaseAttack ?? 0,
            CardMaxHp = occupant?.MaxHp ?? 0,
            AttackBonus = Math.Max(0, (occupant?.BaseAttack ?? 0) - baseAttack),
            HpBonus = Math.Max(0, (occupant?.MaxHp ?? 0) - baseHp),
            AttackType = occupant?.AttackType ?? AttackType.Melee,
            DamageType = occupant?.DamageType ?? DamageType.None,
            Message = $"CardPlayed {cardId}"
        };
    }

    private BattleEventDto CreateAttackStartedEvent(ServerClientBattleCommandMessage command)
    {
        return CreateAttackStartedEvent(
            command.ActorId,
            ToTileCoord(command.SourceCoord, "source"),
            GetOpponent(command.ActorId),
            ToTileCoord(command.TargetCoord, "target"));
    }

    private BattleEventDto CreateAttackStartedEvent(
        PlayerIdDto sourceOwnerId,
        TileCoord sourceCoord,
        PlayerIdDto targetOwnerId,
        TileCoord targetCoord)
    {
        var sourceRuntimeOwnerId = ToRuntimePlayerId(sourceOwnerId);
        var targetRuntimeOwnerId = ToRuntimePlayerId(targetOwnerId);
        var attacker = _battleFlowController?.CurrentBattleState?
            .GetBoard(sourceRuntimeOwnerId)?
            .GetOccupant(sourceCoord);
        var target = _battleFlowController?.CurrentBattleState?
            .GetBoard(targetRuntimeOwnerId)?
            .GetOccupant(targetCoord);

        return new BattleEventDto
        {
            EventType = BattleEventType.AttackStarted,
            SourceOwnerId = sourceOwnerId,
            SourceCoord = ToServerTileCoord(sourceCoord),
            TargetOwnerId = targetOwnerId,
            TargetCoord = ToServerTileCoord(targetCoord),
            RuntimeId = attacker?.RuntimeId ?? string.Empty,
            CardId = attacker?.CardId ?? string.Empty,
            SourceRuntimeId = attacker?.RuntimeId ?? string.Empty,
            SourceCardId = attacker?.CardId ?? string.Empty,
            TargetRuntimeId = target?.RuntimeId ?? string.Empty,
            TargetCardId = target?.CardId ?? string.Empty,
            AttackType = attacker?.AttackType ?? AttackType.Melee,
            DamageType = attacker?.DamageType ?? DamageType.None,
            Message = "AttackStarted"
        };
    }

    private void ApplyResolvedAttackTarget(
        BattleEventDto attackEvent,
        ICollection<BattleEventDto> battleEvents)
    {
        var resolution = _battleFlowController?.CurrentBattleState?.LastAttackResolution;
        if (attackEvent == null || resolution == null)
        {
            return;
        }

        attackEvent.TargetOwnerId = ToDtoPlayerId(resolution.ResolvedTargetOwnerId);
        attackEvent.TargetCoord = ToServerTileCoord(resolution.ResolvedTargetCoord);
        attackEvent.TargetRuntimeId = resolution.ResolvedTargetRuntimeId;
        attackEvent.TargetCardId = resolution.ResolvedTargetCardId;

        if (!resolution.WasHuanShuRedirected || battleEvents == null)
        {
            return;
        }

        battleEvents.Add(new BattleEventDto
        {
            EventType = BattleEventType.HuanShuRedirected,
            SourceOwnerId = ToDtoPlayerId(resolution.AttackerOwnerId),
            SourceCoord = ToServerTileCoord(resolution.AttackerCoord),
            SourceRuntimeId = resolution.AttackerRuntimeId,
            SourceCardId = resolution.AttackerCardId,
            TargetOwnerId = ToDtoPlayerId(resolution.ResolvedTargetOwnerId),
            TargetCoord = ToServerTileCoord(resolution.ResolvedTargetCoord),
            TargetRuntimeId = resolution.ResolvedTargetRuntimeId,
            TargetCardId = resolution.ResolvedTargetCardId,
            Message = "HuanShuRedirected"
        });
    }

    private BattleEventDto CreateTurnEndedEvent(PlayerId previousActivePlayer)
    {
        var ownerId = ToDtoPlayerId(previousActivePlayer);
        return new BattleEventDto
        {
            EventType = BattleEventType.TurnEnded,
            SourceOwnerId = ownerId,
            TargetOwnerId = ownerId,
            Message = $"TurnEnded {FormatRuntimeSeat(previousActivePlayer)}"
        };
    }

    private BattleEventDto CreateTurnStartedEvent(PlayerId activePlayer, int turnNumber)
    {
        var ownerId = ToDtoPlayerId(activePlayer);
        return new BattleEventDto
        {
            EventType = BattleEventType.TurnStarted,
            SourceOwnerId = ownerId,
            TargetOwnerId = ownerId,
            Amount = turnNumber,
            Message = $"TurnStarted {FormatRuntimeSeat(activePlayer)} #{turnNumber}"
        };
    }

    private void AppendCardDrawEvents(List<BattleEventDto> battleEvents)
    {
        var battleState = _battleFlowController?.CurrentBattleState;
        if (battleEvents == null || battleState == null || battleState.CardDrawEvents.Count == 0)
        {
            return;
        }

        foreach (var cardDrawEvent in battleState.CardDrawEvents)
        {
            if (cardDrawEvent == null)
            {
                continue;
            }

            var ownerId = ToDtoPlayerId(cardDrawEvent.OwnerId);
            battleEvents.Add(new BattleEventDto
            {
                EventType = BattleEventType.CardDrawn,
                SourceOwnerId = ownerId,
                TargetOwnerId = ownerId,
                SourceCardId = cardDrawEvent.SourceCardId,
                Amount = 1,
                Message = "CardDrawn"
            });
        }

        battleState.ClearCardDrawEvents();
    }

    private void AppendValuePopupEvents(List<BattleEventDto> battleEvents, PlayerIdDto sourceOwnerId)
    {
        var battleState = _battleFlowController?.CurrentBattleState;
        if (battleEvents == null || battleState == null)
        {
            return;
        }

        AppendAreaSpellEffectEvents(battleEvents, battleState);

        if (battleState.ValuePopupEvents == null)
        {
            return;
        }

        foreach (var valuePopupEvent in battleState.ValuePopupEvents)
        {
            if (valuePopupEvent == null ||
                (valuePopupEvent.Amount <= 0 && !valuePopupEvent.IsInvinciblePrevented))
            {
                continue;
            }

            battleEvents.Add(new BattleEventDto
            {
                EventType = valuePopupEvent.IsInvinciblePrevented
                    ? BattleEventType.InvinciblePrevented
                    : valuePopupEvent.IsHealing
                        ? BattleEventType.HealingApplied
                        : BattleEventType.DamageApplied,
                SourceOwnerId = valuePopupEvent.Cause == BattleValueChangeCause.Unknown
                    ? sourceOwnerId
                    : ToDtoPlayerId(valuePopupEvent.SourceOwnerId),
                TargetOwnerId = ToDtoPlayerId(valuePopupEvent.OwnerId),
                TargetCoord = ToServerTileCoord(valuePopupEvent.Coord),
                RuntimeId = valuePopupEvent.RuntimeId ?? string.Empty,
                SourceRuntimeId = valuePopupEvent.SourceRuntimeId ?? string.Empty,
                SourceCardId = valuePopupEvent.SourceCardId ?? string.Empty,
                TargetRuntimeId = valuePopupEvent.RuntimeId ?? string.Empty,
                Amount = valuePopupEvent.Amount,
                HpBefore = valuePopupEvent.HpBefore,
                HpAfter = valuePopupEvent.HpAfter,
                DamageType = valuePopupEvent.DamageType,
                ValueCause = valuePopupEvent.Cause,
                Message = valuePopupEvent.IsInvinciblePrevented
                    ? "InvinciblePrevented"
                    : valuePopupEvent.IsHealing
                        ? $"HealingApplied {valuePopupEvent.Amount}"
                        : $"DamageApplied {valuePopupEvent.Amount}"
            });
        }

        battleState.ClearValuePopupEvents();
    }

    private static void AppendAreaSpellEffectEvents(
        List<BattleEventDto> battleEvents,
        BattleState battleState)
    {
        if (battleEvents == null || battleState?.AreaSpellEffectEvents == null)
        {
            return;
        }

        foreach (var areaEffectEvent in battleState.AreaSpellEffectEvents)
        {
            if (areaEffectEvent == null || areaEffectEvent.TargetCoords.Count == 0)
            {
                continue;
            }

            var targetCoords = areaEffectEvent.TargetCoords
                .Select(ToServerTileCoord)
                .ToList();
            battleEvents.Add(new BattleEventDto
            {
                EventType = BattleEventType.AreaSpellEffectTriggered,
                SourceOwnerId = ToDtoPlayerId(areaEffectEvent.SourceOwnerId),
                TargetOwnerId = ToDtoPlayerId(areaEffectEvent.TargetOwnerId),
                TargetCoord = targetCoords[0],
                TargetCoords = targetCoords,
                CardId = areaEffectEvent.SourceCardId,
                SourceCardId = areaEffectEvent.SourceCardId,
                EffectId = areaEffectEvent.EffectId,
                Message = $"AreaSpellEffectTriggered {areaEffectEvent.EffectId}"
            });
        }

        battleState.ClearAreaSpellEffectEvents();
    }

    private static void AppendMovedOccupantEvents(
        List<BattleEventDto> battleEvents,
        ServerBattleSnapshot beforeSnapshot,
        ServerBattleSnapshot afterSnapshot)
    {
        if (battleEvents == null || beforeSnapshot == null || afterSnapshot == null)
        {
            return;
        }

        foreach (var beforeOccupant in beforeSnapshot.Occupants)
        {
            var afterOccupant = afterSnapshot.FindByRuntimeId(beforeOccupant.RuntimeId);
            if (afterOccupant == null ||
                (afterOccupant.Coord == beforeOccupant.Coord &&
                 afterOccupant.OwnerId == beforeOccupant.OwnerId))
            {
                continue;
            }

            battleEvents.Add(new BattleEventDto
            {
                EventType = BattleEventType.OccupantMoved,
                SourceOwnerId = ToDtoPlayerId(beforeOccupant.OwnerId),
                SourceCoord = ToServerTileCoord(beforeOccupant.Coord),
                TargetOwnerId = ToDtoPlayerId(afterOccupant.OwnerId),
                TargetCoord = ToServerTileCoord(afterOccupant.Coord),
                RuntimeId = beforeOccupant.RuntimeId,
                CardId = beforeOccupant.CardId,
                SourceRuntimeId = beforeOccupant.RuntimeId,
                SourceCardId = beforeOccupant.CardId,
                TargetRuntimeId = beforeOccupant.RuntimeId,
                TargetCardId = beforeOccupant.CardId,
                Message = $"OccupantMoved {beforeOccupant.CardId}"
            });
        }
    }

    private static void AppendRemovedOccupantEvents(
        List<BattleEventDto> battleEvents,
        ServerBattleSnapshot beforeSnapshot,
        ServerBattleSnapshot afterSnapshot)
    {
        if (battleEvents == null || beforeSnapshot == null || afterSnapshot == null)
        {
            return;
        }

        foreach (var beforeOccupant in beforeSnapshot.Occupants)
        {
            var afterOccupant = afterSnapshot.FindByRuntimeId(beforeOccupant.RuntimeId);
            if (afterOccupant != null && (beforeOccupant.CurrentHp <= 0 || afterOccupant.CurrentHp > 0))
            {
                continue;
            }

            battleEvents.Add(new BattleEventDto
            {
                EventType = BattleEventType.OccupantRemoved,
                SourceOwnerId = ToDtoPlayerId(beforeOccupant.OwnerId),
                TargetOwnerId = ToDtoPlayerId(beforeOccupant.OwnerId),
                TargetCoord = ToServerTileCoord(beforeOccupant.Coord),
                RuntimeId = beforeOccupant.RuntimeId,
                CardId = beforeOccupant.CardId,
                TargetRuntimeId = beforeOccupant.RuntimeId,
                TargetCardId = beforeOccupant.CardId,
                Message = $"OccupantRemoved {beforeOccupant.CardId}"
            });
        }
    }

    private static void AppendRobotFusionEvents(
        List<BattleEventDto> battleEvents,
        ServerBattleSnapshot beforeSnapshot,
        ServerBattleSnapshot afterSnapshot,
        PlayerIdDto actorId,
        IReadOnlyList<TileCoord> selectedCoords)
    {
        if (battleEvents == null || beforeSnapshot == null || afterSnapshot == null ||
            selectedCoords == null || selectedCoords.Count < RobotFusionRules.MinimumRobotCount)
        {
            return;
        }

        var runtimeOwnerId = ToRuntimePlayerId(actorId);
        var selectedBefore = selectedCoords
            .Select(coord => beforeSnapshot.FindByCoord(runtimeOwnerId, coord))
            .Where(occupant => occupant != null)
            .ToList();
        var survivorBefore = selectedBefore.FirstOrDefault(occupant =>
            afterSnapshot.FindByRuntimeId(occupant!.RuntimeId) != null);
        if (survivorBefore == null)
        {
            return;
        }

        var survivorAfter = afterSnapshot.FindByRuntimeId(survivorBefore.RuntimeId);
        if (survivorAfter == null)
        {
            return;
        }

        battleEvents.Add(new BattleEventDto
        {
            EventType = BattleEventType.RobotFusionResolved,
            SourceOwnerId = actorId,
            TargetOwnerId = actorId,
            TargetCoord = ToServerTileCoord(survivorAfter.Coord),
            RuntimeId = survivorAfter.RuntimeId,
            CardId = RobotFusionRules.CardId,
            SourceCardId = RobotFusionRules.CardId,
            TargetRuntimeId = survivorAfter.RuntimeId,
            TargetCardId = survivorAfter.CardId,
            AttackBonus = Math.Max(0, survivorAfter.BaseAttack - survivorBefore.BaseAttack),
            HpBonus = Math.Max(0, survivorAfter.MaxHp - survivorBefore.MaxHp),
            Message = $"RobotFusionResolved survivor={survivorAfter.CardId}"
        });

        foreach (var absorbed in selectedBefore)
        {
            if (absorbed == null || string.Equals(absorbed.RuntimeId, survivorAfter.RuntimeId, StringComparison.Ordinal))
            {
                continue;
            }

            battleEvents.Add(new BattleEventDto
            {
                EventType = BattleEventType.OccupantAbsorbed,
                SourceOwnerId = actorId,
                TargetOwnerId = actorId,
                TargetCoord = ToServerTileCoord(absorbed.Coord),
                RuntimeId = absorbed.RuntimeId,
                CardId = absorbed.CardId,
                SourceRuntimeId = survivorAfter.RuntimeId,
                SourceCardId = RobotFusionRules.CardId,
                TargetRuntimeId = absorbed.RuntimeId,
                TargetCardId = absorbed.CardId,
                Message = $"OccupantAbsorbed {absorbed.CardId}"
            });
        }
    }

    private void AppendBattleEndedEventIfNeeded(List<BattleEventDto> battleEvents)
    {
        var battleState = _battleFlowController?.CurrentBattleState;
        if (battleEvents == null || battleState?.Result == null || !battleState.Result.HasResult)
        {
            return;
        }

        _pendingDisconnectReservations.Clear();
        CaptureResultIfEnded();
        if (battleState.Result.IsDraw)
        {
            battleEvents.Add(new BattleEventDto
            {
                EventType = BattleEventType.BattleEnded,
                IsDraw = true,
                Message = "BattleEnded draw"
            });
            return;
        }

        var winnerId = ToDtoPlayerId(battleState.Result.Winner);
        battleEvents.Add(new BattleEventDto
        {
            EventType = BattleEventType.BattleEnded,
            SourceOwnerId = winnerId,
            TargetOwnerId = winnerId,
            Message = $"BattleEnded winner={FormatRuntimeSeat(battleState.Result.Winner)}"
        });
    }

    private int SumDamageValuePopupEvents()
    {
        var battleState = _battleFlowController?.CurrentBattleState;
        if (battleState?.ValuePopupEvents == null)
        {
            return 0;
        }

        var amount = 0;
        foreach (var valuePopupEvent in battleState.ValuePopupEvents)
        {
            if (valuePopupEvent != null && !valuePopupEvent.IsHealing)
            {
                amount += valuePopupEvent.Amount;
            }
        }

        return amount;
    }

    private static ServerTileCoordDto? CloneCoord(ServerTileCoordDto? coord)
    {
        return coord == null
            ? null
            : new ServerTileCoordDto
            {
                Column = coord.Column,
                Row = coord.Row
            };
    }

    private static ServerTileCoordDto ToServerTileCoord(TileCoord coord)
    {
        return new ServerTileCoordDto
        {
            Column = coord.Column,
            Row = coord.Row
        };
    }

    private static PlayerIdDto ToDtoPlayerId(PlayerId playerId)
    {
        return playerId == PlayerId.Player
            ? PlayerIdDto.Player
            : PlayerIdDto.AI;
    }

    private static PlayerIdDto GetOpponent(PlayerIdDto playerId)
    {
        return playerId == PlayerIdDto.Player
            ? PlayerIdDto.AI
            : PlayerIdDto.Player;
    }

    public string FormatSeatForLog(PlayerIdDto seatId)
    {
        return ToOnlineSeatId(seatId).ToString();
    }

    private string FormatRuntimeSeat(PlayerId playerId)
    {
        return ToOnlineSeatId(playerId).ToString();
    }

    private void ApplyTurnTimer(BattleStateViewDto view)
    {
        if (view == null)
        {
            return;
        }

        view.ServerUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var battleState = _battleFlowController?.CurrentBattleState;
        view.TurnTimerDurationSeconds = battleState?.Phase == PhaseType.Mulligan
            ? (int)Math.Ceiling(MulliganDuration.TotalSeconds)
            : (int)Math.Ceiling(TurnDuration.TotalSeconds);

        if (battleState == null ||
            battleState.IsEnded ||
            (battleState.Phase != PhaseType.Mulligan && battleState.Phase != PhaseType.Main) ||
            _turnTimerVersion <= 0 ||
            _turnTimerDeadlineUtc == default)
        {
            view.TurnTimerVersion = 0;
            view.TurnTimerDeadlineUnixTimeMilliseconds = 0;
            return;
        }

        view.TurnTimerVersion = _turnTimerVersion;
        view.TurnTimerDeadlineUnixTimeMilliseconds = _turnTimerDeadlineUtc.ToUnixTimeMilliseconds();
    }

    private OnlineBattleSeatId ToOnlineSeatId(PlayerId playerId)
    {
        return playerId == PlayerId.Player
            ? OnlineBattleSeatId.PlayerA
            : UseServerAiOpponent
                ? OnlineBattleSeatId.ServerAI
                : OnlineBattleSeatId.PlayerB;
    }

    private OnlineBattleSeatId ToOnlineSeatId(PlayerIdDto seatId)
    {
        return seatId == PlayerIdDto.Player
            ? OnlineBattleSeatId.PlayerA
            : UseServerAiOpponent
                ? OnlineBattleSeatId.ServerAI
                : OnlineBattleSeatId.PlayerB;
    }

    private void ApplyOnlineSeatLabels(BattleStateViewDto view)
    {
        if (view == null)
        {
            return;
        }

        view.ViewerOnlineSeatId = ToOnlineSeatId(view.ViewerId);
        view.ActiveOnlineSeatId = ToOnlineSeatId(view.ActivePlayerId);
        view.WinnerOnlineSeatId = view.HasWinner
            ? ToOnlineSeatId(view.WinnerId)
            : OnlineBattleSeatId.None;

        if (view.Player != null)
        {
            view.Player.OnlineSeatId = ToOnlineSeatId(view.Player.PlayerId);
        }

        if (view.Opponent != null)
        {
            view.Opponent.OnlineSeatId = ToOnlineSeatId(view.Opponent.PlayerId);
        }

        if (view.Occupants != null)
        {
            foreach (var occupant in view.Occupants)
            {
                if (occupant != null)
                {
                    occupant.OwnerOnlineSeatId = ToOnlineSeatId(occupant.OwnerId);
                }
            }
        }

        if (view.PersistentEffects != null)
        {
            foreach (var persistentEffect in view.PersistentEffects)
            {
                if (persistentEffect != null)
                {
                    persistentEffect.OwnerOnlineSeatId = ToOnlineSeatId(persistentEffect.OwnerId);
                }
            }
        }
    }

    private BattleSessionDomainSnapshot? CreateDomainSnapshot(BattleState? battleState)
    {
        if (battleState == null)
        {
            return null;
        }

        return new BattleSessionDomainSnapshot(
            battleState.TurnNumber,
            battleState.ActivePlayerId.ToString(),
            battleState.Phase.ToString(),
            battleState.Result.HasWinner,
            battleState.Result.Winner.ToString(),
            battleState.Counters.ActionSequence,
            _turnTimerVersion,
            _turnTimerPlayerId.ToString(),
            _turnTimerDeadlineUtc,
            CreatePlayerSnapshot(battleState.Player),
            CreatePlayerSnapshot(battleState.AI),
            CreateBoardSnapshot(battleState.PlayerBoard),
            CreateBoardSnapshot(battleState.AIBoard),
            battleState.PersistentEffects
                .Where(effect => effect != null)
                .Select(CreatePersistentEffectSnapshot)
                .ToList(),
            battleState.PendingRobotFusion == null
                ? null
                : new BattleSessionPendingRobotFusionSnapshot(
                    battleState.PendingRobotFusion.OwnerId.ToString(),
                    battleState.PendingRobotFusion.CardId),
            battleState.Result.IsDraw);
    }

    private static BattleSessionPlayerSnapshot CreatePlayerSnapshot(PlayerState player)
    {
        return new BattleSessionPlayerSnapshot(
            player.Id.ToString(),
            CreateResourceSnapshot(player.Resources),
            player.Deck.CardIds.ToList(),
            player.Hand.CardIds.ToList(),
            player.Discard.CardIds.ToList(),
            player.FailedDrawCount,
            player.HasUsedMulligan,
            player.MaxHandSizeBonus,
            player.Master.RuntimeId,
            player.Hand.Cards
                .Select(card => new BattleSessionHandCardSnapshot(
                    card.RuntimeId,
                    card.CardId,
                    card.IsTemporaryReplicate))
                .ToList());
    }

    private static BattleSessionBoardSnapshot CreateBoardSnapshot(BoardState board)
    {
        return new BattleSessionBoardSnapshot(
            board.EnumerateOccupants()
                .Where(occupant => occupant != null)
                .Select(CreateOccupantSnapshot)
                .ToList());
    }

    private static BattleSessionOccupantSnapshot CreateOccupantSnapshot(OccupantState occupant)
    {
        var unit = occupant as UnitState;
        var building = occupant as BuildingState;
        return new BattleSessionOccupantSnapshot(
            occupant.RuntimeId,
            occupant.CardId,
            occupant.OwnerId.ToString(),
            occupant.Kind.ToString(),
            occupant.Position.Column,
            occupant.Position.Row,
            occupant.AttackType.ToString(),
            occupant.BaseAttack,
            occupant.MaxHp,
            occupant.CurrentHp,
            occupant.CanMove,
            CreateResourceSnapshot(occupant.TurnStartResourceGain),
            occupant.MaxAttacksPerTurn,
            occupant.HitsPerAttack,
            occupant.HasBerserker,
            occupant.HasEndure,
            occupant.HasShielder,
            occupant.HasLifeSteal,
            occupant.EndureUsed,
            occupant.IsDrained,
            occupant.IsErasure,
            occupant.HasSummoningSickness,
            occupant.RemainingAttacksThisTurn,
            unit?.IsScience == true || (building?.SciencePowerUpkeep ?? 0) > 0,
            unit?.SciencePowerUpkeep ?? building?.SciencePowerUpkeep ?? 0,
            building?.CanAttackAsBuilding == true,
            occupant.DamageType.ToString(),
            occupant.PhysicalDefense,
            occupant.MagicDefense,
            unit?.HasRobot == true,
            occupant.OriginalAttack,
            occupant.OriginalMaxHp,
            occupant.OriginalPhysicalDefense,
            occupant.OriginalMagicDefense,
            occupant.WasSummonedThisTurn,
            LegacyIsDisabled: false,
            HasRush: occupant.HasRush,
            IsSealbound: occupant.IsSealbound,
            SealboundOwnerTurnStartsRemaining: occupant.SealboundOwnerTurnStartsRemaining,
            HasHiding: occupant.HasHiding,
            HidingRevealed: occupant.HidingRevealed,
            HasFlying: occupant.HasFlying,
            SpellPower: occupant.SpellPower,
            InvincibleEffects: occupant.InvincibleEffects
                .Select(effect => new BattleSessionInvincibleEffectSnapshot(
                    effect.Duration.ToString(),
                    effect.OwnerTurnsRemaining,
                    effect.AppliedTurnNumber,
                    effect.AppliedActivePlayerId.ToString()))
                .ToList(),
            HuanShuOwnerTurnsRemaining: occupant.HuanShuOwnerTurnsRemaining,
            HuanShuEligibleAfterTurnNumber: occupant.HuanShuEligibleAfterTurnNumber,
            HasPiercing: occupant.HasPiercing,
            IsDemonKingRevivalPending: occupant.IsDemonKingRevivalPending,
            DemonKingRevivalTurnStartsRemaining: occupant.DemonKingRevivalTurnStartsRemaining,
            DemonKingRevivalCountdownPlayerId: occupant.DemonKingRevivalCountdownPlayerId.ToString(),
            DemonKingRevivalEligibleAfterTurnNumber: occupant.DemonKingRevivalEligibleAfterTurnNumber,
            DemonKingRevivalCount: occupant.DemonKingRevivalCount);
    }

    private static BattleSessionPersistentEffectSnapshot CreatePersistentEffectSnapshot(PersistentEffectState effect)
    {
        return new BattleSessionPersistentEffectSnapshot(
            effect.SourceCardId,
            effect.OwnerId.ToString(),
            effect.EffectId,
            effect.AppliedTurn,
            effect.EndConditionText,
            CreateResourceSnapshot(effect.TurnStartResourceGain),
            effect.OwnerTurnStartsRemaining,
            effect.TargetRuntimeId ?? string.Empty,
            effect.IsExpired,
            effect.TargetRow,
            effect.RemainingTriggers,
            effect.EffectDamage,
            effect.EffectDamageType.ToString(),
            effect.TargetsOwnerBoard,
            effect.CapturedSpellPower,
            effect.TargetStartColumn);
    }

    private static BattleSessionResourceSnapshot CreateResourceSnapshot(ResourceSet resources)
    {
        return new BattleSessionResourceSnapshot(
            resources?.Mana ?? 0,
            resources?.Qi ?? 0,
            resources?.Power ?? 0,
            resources?.Gold ?? 0);
    }

    private static BattleState RestoreBattleState(
        BattleSessionDomainSnapshot snapshot,
        ICardDefinitionProvider cardDefinitionProvider)
    {
        var playerBoard = new BoardState();
        var aiBoard = new BoardState();
        var player = RestorePlayer(
            snapshot.Player,
            snapshot.PlayerBoard,
            playerBoard,
            PlayerId.Player,
            cardDefinitionProvider);
        var ai = RestorePlayer(
            snapshot.AI,
            snapshot.AIBoard,
            aiBoard,
            PlayerId.AI,
            cardDefinitionProvider);
        var battleState = new BattleState(player, ai, playerBoard, aiBoard);
        battleState.RestoreRuntimeState(
            snapshot.TurnNumber,
            ParseRuntimePlayerId(snapshot.ActivePlayerId, PlayerId.Player),
            ParsePhase(snapshot.Phase, PhaseType.Main));
        battleState.Counters.Restore(snapshot.ActionSequence);
        battleState.PersistentEffects.Clear();

        foreach (var effectSnapshot in snapshot.PersistentEffects ?? Array.Empty<BattleSessionPersistentEffectSnapshot>())
        {
            var effect = new PersistentEffectState(
                effectSnapshot.SourceCardId ?? string.Empty,
                ParseRuntimePlayerId(effectSnapshot.OwnerId, PlayerId.Player),
                effectSnapshot.EffectId ?? string.Empty,
                effectSnapshot.AppliedTurn,
                effectSnapshot.EndConditionText ?? string.Empty,
                RestoreResourceSet(effectSnapshot.TurnStartResourceGain),
                effectSnapshot.OwnerTurnStartsRemaining,
                effectSnapshot.TargetRuntimeId,
                effectSnapshot.TargetRow,
                effectSnapshot.RemainingTriggers,
                effectSnapshot.EffectDamage,
                ParseDamageType(effectSnapshot.EffectDamageType, DamageType.None),
                effectSnapshot.TargetsOwnerBoard,
                effectSnapshot.CapturedSpellPower,
                effectSnapshot.TargetStartColumn);
            effect.RestoreRuntimeState(
                effectSnapshot.OwnerTurnStartsRemaining,
                effectSnapshot.IsExpired,
                effectSnapshot.RemainingTriggers);
            battleState.PersistentEffects.Add(effect);
        }

        if (snapshot.PendingRobotFusion != null &&
            !string.IsNullOrWhiteSpace(snapshot.PendingRobotFusion.CardId))
        {
            battleState.RestorePendingRobotFusion(
                ParseRuntimePlayerId(snapshot.PendingRobotFusion.OwnerId, PlayerId.Player),
                snapshot.PendingRobotFusion.CardId);
        }

        if (snapshot.IsDraw)
        {
            battleState.EndBattleAsDraw();
        }
        else if (snapshot.HasWinner)
        {
            battleState.EndBattle(ParseRuntimePlayerId(snapshot.WinnerId, PlayerId.Player));
        }
        else
        {
            battleState.Result.Clear();
            battleState.SetPhase(ParsePhase(snapshot.Phase, PhaseType.Main));
        }

        battleState.ClearValuePopupEvents();
        battleState.ClearCardDrawEvents();
        battleState.ClearResourceChangeEvents();
        battleState.ClearAreaSpellEffectEvents();
        return battleState;
    }

    private static PlayerState RestorePlayer(
        BattleSessionPlayerSnapshot playerSnapshot,
        BattleSessionBoardSnapshot boardSnapshot,
        BoardState board,
        PlayerId fallbackPlayerId,
        ICardDefinitionProvider cardDefinitionProvider)
    {
        var playerId = ParseRuntimePlayerId(playerSnapshot?.PlayerId, fallbackPlayerId);
        var occupants = (boardSnapshot?.Occupants ?? Array.Empty<BattleSessionOccupantSnapshot>())
            .Select(snapshot => RestoreOccupant(snapshot, playerId, cardDefinitionProvider))
            .Where(occupant => occupant != null)
            .ToList();

        var master = occupants.OfType<MasterState>().FirstOrDefault();
        if (master == null)
        {
            master = new MasterState(
                $"{playerId.ToString().ToLowerInvariant()}-master",
                playerId,
                new TileCoord(2, 1),
                3,
                333);
            occupants.Add(master);
        }

        foreach (var occupant in occupants)
        {
            if (board.IsInside(occupant.Position) && board.IsEmpty(occupant.Position))
            {
                board.Place(occupant.Position, occupant);
            }
        }

        var hand = new HandState();
        if (playerSnapshot?.HandCards != null)
        {
            foreach (var card in playerSnapshot.HandCards)
            {
                if (card == null || string.IsNullOrWhiteSpace(card.CardId))
                {
                    continue;
                }

                hand.Add(
                    card.CardId,
                    string.IsNullOrWhiteSpace(card.RuntimeId)
                        ? Guid.NewGuid().ToString("N")
                        : card.RuntimeId,
                    card.IsTemporaryReplicate);
            }
        }
        else
        {
            foreach (var cardId in playerSnapshot?.HandCardIds ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(cardId))
                {
                    hand.Add(cardId);
                }
            }
        }

        var discard = new DiscardState();
        foreach (var cardId in playerSnapshot?.DiscardCardIds ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(cardId))
            {
                discard.Add(cardId);
            }
        }

        var player = new PlayerState(
            playerId,
            RestoreResourceSet(playerSnapshot?.Resources),
            new DeckState(playerSnapshot?.DeckCardIds ?? Array.Empty<string>()),
            hand,
            discard,
            master);
        player.RestoreRuntimeState(
            playerSnapshot?.FailedDrawCount ?? 0,
            playerSnapshot?.HasUsedMulligan == true,
            playerSnapshot?.MaxHandSizeBonus ?? 0);
        return player;
    }

    private static OccupantState RestoreOccupant(
        BattleSessionOccupantSnapshot snapshot,
        PlayerId fallbackOwnerId,
        ICardDefinitionProvider cardDefinitionProvider)
    {
        var ownerId = ParseRuntimePlayerId(snapshot.OwnerId, fallbackOwnerId);
        var position = new TileCoord(snapshot.Column, snapshot.Row);
        var kind = ParseOccupantKind(snapshot.Kind, OccupantKind.Unit);
        var damageType = ResolveSnapshotDamageType(snapshot, kind, cardDefinitionProvider);
        OccupantState occupant = kind == OccupantKind.Master
            ? new MasterState(
                snapshot.RuntimeId,
                ownerId,
                position,
                snapshot.BaseAttack,
                snapshot.MaxHp,
                snapshot.PhysicalDefense,
                snapshot.MagicDefense,
                snapshot.HasFlying,
                snapshot.SpellPower,
                snapshot.HasPiercing)
            : kind == OccupantKind.Building
                ? new BuildingState(
                    snapshot.RuntimeId,
                    snapshot.CardId,
                    ownerId,
                    position,
                    snapshot.CanAttackAsBuilding,
                    snapshot.BaseAttack,
                    snapshot.MaxHp,
                    RestoreResourceSet(snapshot.TurnStartResourceGain),
                    snapshot.HitsPerAttack,
                    damageType,
                    snapshot.PhysicalDefense,
                    snapshot.MagicDefense,
                    snapshot.SciencePowerUpkeep,
                    snapshot.HasFlying,
                    snapshot.SpellPower,
                    snapshot.HasPiercing)
                : new UnitState(
                    snapshot.RuntimeId,
                    snapshot.CardId,
                    ownerId,
                    position,
                    ParseAttackType(snapshot.AttackType, AttackType.Melee),
                    snapshot.BaseAttack,
                    snapshot.MaxHp,
                    snapshot.CanMove,
                    snapshot.IsScience,
                    snapshot.SciencePowerUpkeep,
                    RestoreResourceSet(snapshot.TurnStartResourceGain),
                    snapshot.MaxAttacksPerTurn,
                    OccupantKind.Unit,
                    snapshot.HitsPerAttack,
                    snapshot.HasBerserker,
                    snapshot.HasEndure,
                    snapshot.HasShielder || snapshot.LegacyHasGuard,
                    snapshot.HasLifeSteal,
                    damageType,
                    snapshot.PhysicalDefense,
                    snapshot.MagicDefense,
                    snapshot.HasRobot,
                    snapshot.HasRush,
                    snapshot.HasHiding,
                    snapshot.HasFlying,
                    snapshot.SpellPower,
                    snapshot.HasPiercing);

        occupant.CurrentHp = snapshot.CurrentHp;
        occupant.EndureUsed = snapshot.EndureUsed;
        occupant.RestoreOriginalCombatStats(
            snapshot.OriginalAttack ?? snapshot.BaseAttack,
            snapshot.OriginalMaxHp ?? snapshot.MaxHp,
            snapshot.OriginalPhysicalDefense ?? snapshot.PhysicalDefense,
            snapshot.OriginalMagicDefense ?? snapshot.MagicDefense);
        occupant.RestoreSuppressionStates(
            snapshot.IsDrained,
            snapshot.IsErasure || snapshot.LegacyIsDisabled,
            snapshot.IsSealbound,
            snapshot.SealboundOwnerTurnStartsRemaining,
            snapshot.HidingRevealed);
        occupant.RestoreHuanShuState(
            snapshot.HuanShuOwnerTurnsRemaining,
            snapshot.HuanShuEligibleAfterTurnNumber);
        occupant.RestoreDemonKingRevivalState(
            snapshot.IsDemonKingRevivalPending,
            snapshot.DemonKingRevivalTurnStartsRemaining,
            ParseRuntimePlayerId(snapshot.DemonKingRevivalCountdownPlayerId, ownerId),
            snapshot.DemonKingRevivalEligibleAfterTurnNumber,
            snapshot.DemonKingRevivalCount);
        occupant.RestoreInvincibleEffects(
            (snapshot.InvincibleEffects ?? Array.Empty<BattleSessionInvincibleEffectSnapshot>())
            .Where(effect => effect != null)
            .Select(effect => new InvincibleEffectState(
                ParseInvincibleDuration(effect.Duration, InvincibleDurationType.Always),
                effect.OwnerTurnsRemaining,
                effect.AppliedTurnNumber,
                ParseRuntimePlayerId(effect.AppliedActivePlayerId, ownerId))));
        occupant.WasSummonedThisTurn = snapshot.WasSummonedThisTurn;
        occupant.HasSummoningSickness = snapshot.HasSummoningSickness;
        occupant.RemainingAttacksThisTurn = snapshot.RemainingAttacksThisTurn;
        return occupant;
    }

    private static DamageType ResolveSnapshotDamageType(
        BattleSessionOccupantSnapshot snapshot,
        OccupantKind kind,
        ICardDefinitionProvider cardDefinitionProvider)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.DamageType) &&
            Enum.TryParse(snapshot.DamageType, ignoreCase: true, out DamageType savedDamageType))
        {
            return savedDamageType;
        }

        if (kind == OccupantKind.Master)
        {
            return DamageType.Physical;
        }

        try
        {
            return cardDefinitionProvider?.GetRequired(snapshot.CardId) switch
            {
                UnitCardDefinition unit => unit.DamageType,
                BuildingCardDefinition building => building.DamageType,
                _ => DamageType.None,
            };
        }
        catch
        {
            return DamageType.None;
        }
    }

    private void RestoreSeatMetadata(BattleSessionPersistenceSnapshot snapshot)
    {
        _seatMetadata.Clear();
        // Participant snapshots survive disconnects, unlike the list of live connections.
        if (snapshot.Participants != null)
        {
            foreach (var participant in snapshot.Participants)
            {
                AddRestoredParticipant(participant);
            }
            return;
        }

        // Legacy snapshots may recover only identities actually stored by the server.
        // Missing disconnected participants must never be reconstructed from a JoinMatch request.
        foreach (var connection in snapshot.Connections ?? Array.Empty<BattleSessionConnectionSnapshot>())
        {
            if (!connection.HasAssignedSeat) continue;
            AddRestoredParticipant(new BattleSessionParticipantSnapshot(
                connection.RuntimeSeatId, connection.OnlineSeatId, connection.AccountId,
                connection.RunId, connection.DeckId, Array.Empty<string>(), new Dictionary<string, int>()));
        }
    }

    private void AddRestoredParticipant(BattleSessionParticipantSnapshot participant)
    {
        if (!Enum.TryParse<PlayerIdDto>(participant.RuntimeSeatId, out var seatId) ||
            !SeatOrder.Contains(seatId) ||
            !Enum.TryParse<OnlineBattleSeatId>(participant.OnlineSeatId, out var onlineSeatId) ||
            onlineSeatId != ToOnlineSeatId(seatId) || _seatMetadata.ContainsKey(seatId))
        {
            throw new InvalidOperationException("Invalid or duplicate participant in server battle snapshot.");
        }
        _seatMetadata.Add(seatId, new BattleSeatMetadata(
            participant.AccountId, participant.RunId, participant.DeckId,
            participant.DeckCardIds ?? Array.Empty<string>(),
            participant.CardUpgradeLevels ?? new Dictionary<string, int>(), participant.AccountId));
    }

    private static ResourceSet RestoreResourceSet(BattleSessionResourceSnapshot? snapshot)
    {
        return snapshot == null
            ? new ResourceSet()
            : new ResourceSet(snapshot.Mana, snapshot.Qi, snapshot.Power, snapshot.Gold);
    }

    private static PlayerId ParseRuntimePlayerId(string? value, PlayerId fallback)
    {
        return Enum.TryParse(value, ignoreCase: true, out PlayerId playerId) ? playerId : fallback;
    }

    private static PlayerIdDto ParseDtoPlayerId(string? value, PlayerIdDto fallback)
    {
        return Enum.TryParse(value, ignoreCase: true, out PlayerIdDto playerId) ? playerId : fallback;
    }

    private static OnlineBattleSeatId ParseOnlineSeatId(string? value, OnlineBattleSeatId fallback)
    {
        return Enum.TryParse(value, ignoreCase: true, out OnlineBattleSeatId seatId) ? seatId : fallback;
    }

    private static PhaseType ParsePhase(string? value, PhaseType fallback)
    {
        return Enum.TryParse(value, ignoreCase: true, out PhaseType phase) ? phase : fallback;
    }

    private static OccupantKind ParseOccupantKind(string? value, OccupantKind fallback)
    {
        return Enum.TryParse(value, ignoreCase: true, out OccupantKind kind) ? kind : fallback;
    }

    private static AttackType ParseAttackType(string? value, AttackType fallback)
    {
        return Enum.TryParse(value, ignoreCase: true, out AttackType attackType) ? attackType : fallback;
    }

    private static DamageType ParseDamageType(string? value, DamageType fallback)
    {
        return Enum.TryParse(value, ignoreCase: true, out DamageType damageType) ? damageType : fallback;
    }

    private static InvincibleDurationType ParseInvincibleDuration(
        string? value,
        InvincibleDurationType fallback)
    {
        return Enum.TryParse(value, ignoreCase: true, out InvincibleDurationType duration) &&
               duration != InvincibleDurationType.None
            ? duration
            : fallback;
    }

    private void ApplyCombatLog(BattleStateViewDto view, BattleClientConnection connection)
    {
        if (view == null || connection == null)
        {
            return;
        }

        var latestSequence = _combatLogSequence;
        var firstAvailableSequence = _combatLogRecords.Count == 0
            ? latestSequence + 1
            : _combatLogRecords[0].Sequence;
        var replaceHistory = connection.LastCombatLogSequenceSent <= 0 ||
                             connection.LastCombatLogSequenceSent > latestSequence ||
                             connection.LastCombatLogSequenceSent < firstAvailableSequence - 1;

        view.CombatLogLatestSequence = latestSequence;
        view.ReplaceCombatLogEntries = replaceHistory;
        view.CombatLogEntries = (replaceHistory
                ? _combatLogRecords
                : _combatLogRecords.Where(record =>
                    record.Sequence > connection.LastCombatLogSequenceSent))
            .TakeLast(MaximumCombatLogEntries)
            .Select(record => ProjectCombatLogForViewer(record, connection))
            .ToList();
        connection.LastCombatLogSequenceSent = latestSequence;
    }

    private static BattleCombatLogEntryDto ProjectCombatLogForViewer(
        BattleCombatLogEntryDto record,
        BattleClientConnection connection)
    {
        var projected = CloneCombatLogEntry(record);
        if (projected.EntryType == BattleCombatLogEntryType.CardGenerated &&
            ToRuntimePlayerId(connection.AssignedSeatId) != projected.SourceOwnerId)
        {
            projected.TargetCardId = string.Empty;
        }

        return projected;
    }

    private static BattleCombatLogEntryDto CloneCombatLogEntry(BattleCombatLogEntryDto record)
    {
        return new BattleCombatLogEntryDto
        {
            Sequence = record.Sequence,
            EntryType = record.EntryType,
            SourceOwnerId = record.SourceOwnerId,
            TargetOwnerId = record.TargetOwnerId,
            SourceCardId = record.SourceCardId ?? string.Empty,
            TargetCardId = record.TargetCardId ?? string.Empty,
            SourceCoord = record.SourceCoord == null
                ? null
                : new Project333.Runtime.Application.Online.TileCoordDto
                {
                    Column = record.SourceCoord.Column,
                    Row = record.SourceCoord.Row
                },
            TargetCoord = record.TargetCoord == null
                ? null
                : new Project333.Runtime.Application.Online.TileCoordDto
                {
                    Column = record.TargetCoord.Column,
                    Row = record.TargetCoord.Row
                },
            SourceHpHistory = record.SourceHpHistory == null
                ? new List<int>()
                : new List<int>(record.SourceHpHistory),
            TargetHpHistory = record.TargetHpHistory == null
                ? new List<int>()
                : new List<int>(record.TargetHpHistory),
            SourceRemoved = record.SourceRemoved,
            TargetRemoved = record.TargetRemoved,
            SourceDamagePrevented = record.SourceDamagePrevented,
            TargetDamagePrevented = record.TargetDamagePrevented,
            HasCounterattack = record.HasCounterattack,
            IsDraw = record.IsDraw,
            Amount = record.Amount,
            AttackBonus = record.AttackBonus,
            HpBonus = record.HpBonus,
            TurnNumber = record.TurnNumber,
            AttackType = record.AttackType,
            DamageType = record.DamageType,
            ValueCause = record.ValueCause,
            ResourceType = record.ResourceType
        };
    }

    private void AppendCombatLogEntries(
        IReadOnlyList<BattleEventDto> battleEvents,
        ServerBattleSnapshot beforeSnapshot,
        ServerBattleSnapshot afterSnapshot)
    {
        if (battleEvents == null || battleEvents.Count == 0)
        {
            return;
        }

        var cardPlayedEvent = battleEvents.FirstOrDefault(candidate =>
            candidate?.EventType == BattleEventType.CardPlayed);
        if (cardPlayedEvent != null)
        {
            AppendCardPlayedCombatLog(cardPlayedEvent);
        }

        var moveEvents = battleEvents
            .Where(candidate => candidate?.EventType == BattleEventType.OccupantMoved)
            .ToList();

        var attackEvent = battleEvents.FirstOrDefault(candidate =>
            candidate?.EventType == BattleEventType.AttackStarted);
        var spellEvent = battleEvents.FirstOrDefault(candidate =>
            candidate?.EventType == BattleEventType.SpellCast);
        var turnEndedEvent = battleEvents.FirstOrDefault(candidate =>
            candidate?.EventType == BattleEventType.TurnEnded);
        var turnStartedEvent = battleEvents.FirstOrDefault(candidate =>
            candidate?.EventType == BattleEventType.TurnStarted);
        var timerExpiredEvent = battleEvents.FirstOrDefault(candidate =>
            candidate?.EventType == BattleEventType.TurnTimerExpired);
        var robotFusionEvent = battleEvents.FirstOrDefault(candidate =>
            candidate?.EventType == BattleEventType.RobotFusionResolved);

        if (robotFusionEvent != null)
        {
            AppendCombatLogRecord(new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.RobotFusion,
                SourceOwnerId = ToRuntimePlayerId(robotFusionEvent.SourceOwnerId),
                TargetOwnerId = ToRuntimePlayerId(robotFusionEvent.TargetOwnerId),
                SourceCardId = RobotFusionRules.CardId,
                TargetCardId = robotFusionEvent.TargetCardId ?? string.Empty,
                TargetCoord = CloneTileCoord(robotFusionEvent.TargetCoord),
                AttackBonus = Math.Max(0, robotFusionEvent.AttackBonus),
                HpBonus = Math.Max(0, robotFusionEvent.HpBonus)
            });
        }

        if (attackEvent != null)
        {
            AppendAttackCombatLogs(attackEvent, battleEvents, beforeSnapshot);
        }
        else if (spellEvent != null)
        {
            AppendSpellCombatLogs(spellEvent, battleEvents, beforeSnapshot);
            AppendResourceChangeCombatLogs();
        }

        if (moveEvents.Count > 0)
        {
            AppendMoveCombatLogs(moveEvents);
        }

        if (turnEndedEvent != null)
        {
            var turnOwnerId = ToRuntimePlayerId(turnEndedEvent.SourceOwnerId);
            AppendCombatLogRecord(new BattleCombatLogEntryDto
            {
                EntryType = timerExpiredEvent != null
                    ? BattleCombatLogEntryType.TurnTimedOut
                    : BattleCombatLogEntryType.TurnEnded,
                SourceOwnerId = turnOwnerId
            });

            AppendValueChangeCombatLogs(
                battleEvents,
                beforeSnapshot,
                candidate => candidate.ValueCause == BattleValueChangeCause.BlueDragon ||
                             candidate.ValueCause == BattleValueChangeCause.RedDragon);
            AppendCardDrawCombatLogs(battleEvents);
            AppendHeroGrowthCombatLogs(beforeSnapshot, afterSnapshot);
        }

        if (turnStartedEvent != null)
        {
            var turnOwnerId = ToRuntimePlayerId(turnStartedEvent.SourceOwnerId);
            var turnNumber = Math.Max(1, turnStartedEvent.Amount);
            AppendCombatLogRecord(new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.TurnStarted,
                SourceOwnerId = turnOwnerId,
                TurnNumber = turnNumber
            });
            AppendResourceChangeCombatLogs();
            AppendRobotFactoryGenerationCombatLogs();
            AppendValueChangeCombatLogs(
                battleEvents,
                beforeSnapshot,
                candidate => candidate.ValueCause == BattleValueChangeCause.DeckExhaustion ||
                             candidate.ValueCause == BattleValueChangeCause.Firewall ||
                             candidate.ValueCause == BattleValueChangeCause.BiochemicalBomb ||
                             candidate.ValueCause == BattleValueChangeCause.Spell);
        }

        if (attackEvent != null || spellEvent != null || turnEndedEvent != null || turnStartedEvent != null)
        {
            AppendValueChangeCombatLogs(
                battleEvents,
                beforeSnapshot,
                candidate => candidate.ValueCause == BattleValueChangeCause.NuclearPowerPlant);
        }

        if (attackEvent == null && spellEvent == null && turnEndedEvent == null && turnStartedEvent == null)
        {
            AppendValueChangeCombatLogs(
                battleEvents,
                beforeSnapshot,
                candidate => candidate.ValueCause != BattleValueChangeCause.NormalAttack &&
                             candidate.ValueCause != BattleValueChangeCause.Counterattack);
        }

        AppendUnaccountedRemovalCombatLogs(battleEvents, beforeSnapshot);
        AppendStatusChangeCombatLogs(beforeSnapshot, afterSnapshot);

        var battleEndedEvent = battleEvents.LastOrDefault(candidate =>
            candidate?.EventType == BattleEventType.BattleEnded);
        if (battleEndedEvent != null)
        {
            var winnerId = ToRuntimePlayerId(battleEndedEvent.TargetOwnerId);
            AppendCombatLogRecord(new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.BattleEnded,
                TargetOwnerId = winnerId,
                IsDraw = battleEndedEvent.IsDraw
            });
        }
    }

    private void AppendCardPlayedCombatLog(BattleEventDto battleEvent)
    {
        var ownerId = ToRuntimePlayerId(battleEvent.SourceOwnerId);
        var isBuilding = false;
        try
        {
            isBuilding = _cardDefinitionProvider?.GetRequired(battleEvent.CardId)?.CardType == CardType.Building;
        }
        catch
        {
            isBuilding = false;
        }

        AppendCombatLogRecord(new BattleCombatLogEntryDto
        {
            EntryType = isBuilding
                ? BattleCombatLogEntryType.BuildingConstructed
                : BattleCombatLogEntryType.UnitSummoned,
            SourceOwnerId = ownerId,
            SourceCardId = battleEvent.CardId ?? string.Empty,
            TargetCoord = CloneTileCoord(battleEvent.TargetCoord)
        });

        if (battleEvent.TargetCoord != null)
        {
            var summonedCoord = new TileCoord(
                battleEvent.TargetCoord.Column,
                battleEvent.TargetCoord.Row);
            var summonedOccupant = _battleFlowController?.CurrentBattleState?
                .GetBoard(ownerId)
                .GetOccupant(summonedCoord);
            if (summonedOccupant?.IsSealbound == true)
            {
                AppendCombatLogRecord(new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.SealboundApplied,
                    TargetOwnerId = ownerId,
                    TargetCardId = summonedOccupant.CardId,
                    TargetCoord = CloneTileCoord(battleEvent.TargetCoord),
                    Amount = summonedOccupant.SealboundOwnerTurnStartsRemaining
                });
            }

            if (summonedOccupant?.IsHiding == true)
            {
                AppendCombatLogRecord(new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.HidingApplied,
                    TargetOwnerId = ownerId,
                    TargetCardId = summonedOccupant.CardId,
                    TargetCoord = CloneTileCoord(battleEvent.TargetCoord)
                });
            }
        }

        if (battleEvent.AttackBonus <= 0 && battleEvent.HpBonus <= 0)
        {
            return;
        }

        AppendCombatLogRecord(new BattleCombatLogEntryDto
        {
            EntryType = BattleCombatLogEntryType.UpgradeApplied,
            SourceOwnerId = ownerId,
            SourceCardId = battleEvent.CardId ?? string.Empty,
            AttackBonus = Math.Max(0, battleEvent.AttackBonus),
            HpBonus = Math.Max(0, battleEvent.HpBonus)
        });
    }

    private void AppendRobotFactoryGenerationCombatLogs()
    {
        var battleState = _battleFlowController?.CurrentBattleState;
        if (battleState == null || battleState.CardGenerationEvents.Count == 0)
        {
            return;
        }

        var generationEvents = battleState.CardGenerationEvents.ToList();
        battleState.ClearCardGenerationEvents();
        foreach (var generationEvent in generationEvents)
        {
            if (generationEvent == null || !generationEvent.AddedToHand)
            {
                continue;
            }

            AppendCombatLogRecord(new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.CardGenerated,
                SourceOwnerId = generationEvent.OwnerId,
                TargetOwnerId = generationEvent.OwnerId,
                SourceCardId = generationEvent.SourceCardId,
                TargetCardId = generationEvent.GeneratedCardId,
                SourceCoord = new Project333.Runtime.Application.Online.TileCoordDto
                {
                    Column = generationEvent.SourceCoord.Column,
                    Row = generationEvent.SourceCoord.Row
                },
                Amount = 1
            });
        }
    }

    private void AppendCardDrawCombatLogs(IReadOnlyList<BattleEventDto> battleEvents)
    {
        var effectDrawGroups = battleEvents
            .Where(candidate => candidate?.EventType == BattleEventType.CardDrawn &&
                                !string.IsNullOrWhiteSpace(candidate.SourceCardId))
            .GroupBy(candidate => new
            {
                candidate!.SourceOwnerId,
                candidate.SourceCardId
            });

        foreach (var group in effectDrawGroups)
        {
            var amount = group.Sum(candidate => Math.Max(0, candidate?.Amount ?? 0));
            if (amount <= 0)
            {
                continue;
            }

            AppendCombatLogRecord(new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.CardDrawn,
                SourceOwnerId = ToRuntimePlayerId(group.Key.SourceOwnerId),
                SourceCardId = group.Key.SourceCardId,
                Amount = amount
            });
        }
    }

    private void AppendMoveCombatLogs(IReadOnlyList<BattleEventDto> moveEvents)
    {
        if (moveEvents.Count >= 2 && AreReciprocalMoves(moveEvents[0], moveEvents[1]))
        {
            var first = moveEvents[0];
            var second = moveEvents[1];
            var ownerId = ToRuntimePlayerId(first.SourceOwnerId);
            AppendCombatLogRecord(new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.OccupantsSwapped,
                SourceOwnerId = ownerId,
                SourceCardId = first.CardId ?? string.Empty,
                TargetCardId = second.CardId ?? string.Empty,
                SourceCoord = CloneTileCoord(first.SourceCoord),
                TargetCoord = CloneTileCoord(second.SourceCoord)
            });
            return;
        }

        foreach (var moveEvent in moveEvents)
        {
            if (moveEvent?.SourceCoord == null || moveEvent.TargetCoord == null)
            {
                continue;
            }

            var ownerId = ToRuntimePlayerId(moveEvent.SourceOwnerId);
            var targetOwnerId = ToRuntimePlayerId(moveEvent.TargetOwnerId);
            if (ownerId != targetOwnerId)
            {
                AppendCombatLogRecord(new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.OccupantControlled,
                    SourceOwnerId = targetOwnerId,
                    TargetOwnerId = ownerId,
                    SourceCardId = moveEvent.CardId ?? string.Empty,
                    SourceCoord = CloneTileCoord(moveEvent.SourceCoord),
                    TargetCoord = CloneTileCoord(moveEvent.TargetCoord)
                });
                continue;
            }

            AppendCombatLogRecord(new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.OccupantMoved,
                SourceOwnerId = ownerId,
                SourceCardId = moveEvent.CardId ?? string.Empty,
                SourceCoord = CloneTileCoord(moveEvent.SourceCoord),
                TargetCoord = CloneTileCoord(moveEvent.TargetCoord)
            });
        }
    }

    private void AppendAttackCombatLogs(
        BattleEventDto attackEvent,
        IReadOnlyList<BattleEventDto> battleEvents,
        ServerBattleSnapshot beforeSnapshot)
    {
        var attackerOwnerId = ToRuntimePlayerId(attackEvent.SourceOwnerId);
        var targetOwnerId = ToRuntimePlayerId(attackEvent.TargetOwnerId);
        var attacker = beforeSnapshot?.FindByRuntimeId(attackEvent.SourceRuntimeId) ??
                       beforeSnapshot?.FindByCoord(attackerOwnerId, ToRuntimeCoord(attackEvent.SourceCoord));
        var declaredTarget = beforeSnapshot?.FindByRuntimeId(attackEvent.TargetRuntimeId) ??
                             beforeSnapshot?.FindByCoord(targetOwnerId, ToRuntimeCoord(attackEvent.TargetCoord));
        if (attacker == null || declaredTarget == null)
        {
            return;
        }

        var huanShuRedirectEvent = battleEvents.FirstOrDefault(candidate =>
            candidate?.EventType == BattleEventType.HuanShuRedirected);
        if (huanShuRedirectEvent != null)
        {
            AppendCombatLogRecord(new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.HuanShuRedirected,
                SourceOwnerId = attackerOwnerId,
                TargetOwnerId = targetOwnerId,
                SourceCardId = attacker.CardId,
                TargetCardId = declaredTarget.CardId,
                SourceCoord = CloneTileCoord(attackEvent.SourceCoord),
                TargetCoord = CloneTileCoord(attackEvent.TargetCoord)
            });
        }

        var normalHits = battleEvents
            .Where(candidate => candidate?.EventType == BattleEventType.DamageApplied &&
                                candidate.ValueCause == BattleValueChangeCause.NormalAttack)
            .ToList();
        var targetHits = normalHits
            .Where(candidate => string.Equals(
                candidate.TargetRuntimeId,
                declaredTarget.RuntimeId,
                StringComparison.Ordinal))
            .ToList();
        var counterHits = battleEvents
            .Where(candidate => candidate?.EventType == BattleEventType.DamageApplied &&
                                candidate.ValueCause == BattleValueChangeCause.Counterattack &&
                                string.Equals(candidate.TargetRuntimeId, attacker.RuntimeId, StringComparison.Ordinal))
            .ToList();

        AppendCombatLogRecord(new BattleCombatLogEntryDto
        {
            EntryType = BattleCombatLogEntryType.Attack,
            SourceOwnerId = attackerOwnerId,
            TargetOwnerId = targetOwnerId,
            SourceCardId = attacker.CardId,
            TargetCardId = declaredTarget.CardId,
            SourceCoord = CloneTileCoord(attackEvent.SourceCoord),
            TargetCoord = CloneTileCoord(attackEvent.TargetCoord),
            SourceHpHistory = counterHits.Count == 0
                ? new List<int>()
                : CreateHpHistory(counterHits, attacker.CurrentHp),
            TargetHpHistory = CreateHpHistory(targetHits, declaredTarget.CurrentHp),
            SourceRemoved = IsRemoved(battleEvents, attacker.RuntimeId),
            TargetRemoved = IsRemoved(battleEvents, declaredTarget.RuntimeId),
            SourceDamagePrevented = counterHits.Count == 0,
            TargetDamagePrevented = targetHits.Count == 0,
            HasCounterattack = counterHits.Count > 0,
            AttackType = attacker.AttackType,
            DamageType = attacker.DamageType
        });

        foreach (var shielderGroup in normalHits
                     .Where(candidate => !string.Equals(candidate.TargetRuntimeId, declaredTarget.RuntimeId, StringComparison.Ordinal))
                     .GroupBy(candidate => candidate.TargetRuntimeId))
        {
            var shielder = beforeSnapshot?.FindByRuntimeId(shielderGroup.Key);
            if (shielder == null)
            {
                continue;
            }

            var shielderHits = shielderGroup.ToList();
            AppendCombatLogRecord(new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.ShielderRedirected,
                SourceOwnerId = shielder.OwnerId,
                TargetOwnerId = declaredTarget.OwnerId,
                SourceCardId = shielder.CardId,
                TargetCardId = declaredTarget.CardId,
                SourceHpHistory = CreateHpHistory(shielderHits, shielder.CurrentHp),
                SourceRemoved = IsRemoved(battleEvents, shielder.RuntimeId),
                SourceDamagePrevented = shielderHits.Count == 0
            });
        }

        AppendValueChangeCombatLogs(
            battleEvents,
            beforeSnapshot,
            candidate => candidate.ValueCause == BattleValueChangeCause.Piercing);
        AppendHealingCombatLogs(battleEvents, beforeSnapshot, BattleValueChangeCause.LifeSteal);
    }

    private void AppendSpellCombatLogs(
        BattleEventDto spellEvent,
        IReadOnlyList<BattleEventDto> battleEvents,
        ServerBattleSnapshot beforeSnapshot)
    {
        var ownerId = ToRuntimePlayerId(spellEvent.SourceOwnerId);
        AppendCombatLogRecord(new BattleCombatLogEntryDto
        {
            EntryType = BattleCombatLogEntryType.SpellCast,
            SourceOwnerId = ownerId,
            TargetOwnerId = ToRuntimePlayerId(spellEvent.TargetOwnerId),
            SourceCardId = spellEvent.CardId ?? string.Empty,
            TargetCoord = CloneTileCoord(spellEvent.TargetCoord)
        });

        if (string.Equals(spellEvent.CardId, "Daehwandan", StringComparison.OrdinalIgnoreCase))
        {
            AppendCombatLogRecord(new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.AttackBuffApplied,
                SourceOwnerId = ownerId,
                TargetOwnerId = ownerId,
                SourceCardId = spellEvent.CardId,
                TargetCardId = "Master",
                AttackBonus = 30
            });
        }
        else if (string.Equals(spellEvent.CardId, "CheonraJimang", StringComparison.OrdinalIgnoreCase))
        {
            var targetOwnerId = ToRuntimePlayerId(spellEvent.TargetOwnerId);
            var target = beforeSnapshot?.FindByCoord(targetOwnerId, ToRuntimeCoord(spellEvent.TargetCoord));
            if (target != null)
            {
                AppendCombatLogRecord(new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.DestructionMarked,
                    SourceOwnerId = ownerId,
                    TargetOwnerId = targetOwnerId,
                    SourceCardId = spellEvent.CardId,
                    TargetCardId = target.CardId
                });
            }
        }

        AppendValueChangeCombatLogs(
            battleEvents,
            beforeSnapshot,
            candidate => candidate.ValueCause == BattleValueChangeCause.Spell);
    }

    private void AppendValueChangeCombatLogs(
        IReadOnlyList<BattleEventDto> battleEvents,
        ServerBattleSnapshot? beforeSnapshot,
        Func<BattleEventDto, bool> predicate)
    {
        foreach (var battleEvent in battleEvents)
        {
            if (battleEvent == null || !predicate(battleEvent))
            {
                continue;
            }

            if (battleEvent.EventType == BattleEventType.DamageApplied)
            {
                AppendDamageCombatLog(battleEvent, beforeSnapshot, battleEvents);
            }
            else if (battleEvent.EventType == BattleEventType.HealingApplied)
            {
                AppendHealingCombatLog(battleEvent, beforeSnapshot);
            }
        }
    }

    private void AppendHealingCombatLogs(
        IReadOnlyList<BattleEventDto> battleEvents,
        ServerBattleSnapshot? beforeSnapshot,
        BattleValueChangeCause cause)
    {
        AppendValueChangeCombatLogs(
            battleEvents,
            beforeSnapshot,
            candidate => candidate.ValueCause == cause);
    }

    private void AppendDamageCombatLog(
        BattleEventDto battleEvent,
        ServerBattleSnapshot? beforeSnapshot,
        IReadOnlyList<BattleEventDto> allEvents)
    {
        if (battleEvent.Amount <= 0 ||
            battleEvent.ValueCause == BattleValueChangeCause.NormalAttack ||
            battleEvent.ValueCause == BattleValueChangeCause.Counterattack)
        {
            return;
        }

        var targetOwnerId = ToRuntimePlayerId(battleEvent.TargetOwnerId);
        var target = beforeSnapshot?.FindByRuntimeId(battleEvent.TargetRuntimeId);
        var targetCardId = target?.CardId ?? battleEvent.TargetCardId;
        var killed = battleEvent.HpAfter <= 0 || IsRemoved(allEvents, battleEvent.TargetRuntimeId);

        AppendCombatLogRecord(new BattleCombatLogEntryDto
        {
            EntryType = BattleCombatLogEntryType.Damage,
            SourceOwnerId = ToRuntimePlayerId(battleEvent.SourceOwnerId),
            TargetOwnerId = targetOwnerId,
            SourceCardId = battleEvent.SourceCardId ?? string.Empty,
            TargetCardId = targetCardId ?? string.Empty,
            Amount = Math.Max(0, battleEvent.Amount),
            DamageType = battleEvent.DamageType,
            ValueCause = battleEvent.ValueCause,
            TargetHpHistory = new List<int>
            {
                Math.Max(0, battleEvent.HpBefore),
                Math.Max(0, battleEvent.HpAfter)
            },
            TargetRemoved = killed
        });
    }

    private void AppendHealingCombatLog(BattleEventDto battleEvent, ServerBattleSnapshot? beforeSnapshot)
    {
        if (battleEvent.Amount <= 0)
        {
            return;
        }

        var targetOwnerId = ToRuntimePlayerId(battleEvent.TargetOwnerId);
        var target = beforeSnapshot?.FindByRuntimeId(battleEvent.TargetRuntimeId);
        AppendCombatLogRecord(new BattleCombatLogEntryDto
        {
            EntryType = BattleCombatLogEntryType.Healing,
            SourceOwnerId = ToRuntimePlayerId(battleEvent.SourceOwnerId),
            TargetOwnerId = targetOwnerId,
            SourceCardId = battleEvent.SourceCardId ?? string.Empty,
            TargetCardId = target?.CardId ?? battleEvent.TargetCardId ?? string.Empty,
            Amount = Math.Max(0, battleEvent.Amount),
            ValueCause = battleEvent.ValueCause,
            TargetHpHistory = new List<int>
            {
                Math.Max(0, battleEvent.HpBefore),
                Math.Max(0, battleEvent.HpAfter)
            }
        });
    }

    private void AppendResourceChangeCombatLogs()
    {
        var battleState = _battleFlowController?.CurrentBattleState;
        if (battleState == null || battleState.ResourceChangeEvents.Count == 0)
        {
            return;
        }

        var resourceChangeEvents = battleState.ResourceChangeEvents.ToList();
        battleState.ClearResourceChangeEvents();
        foreach (var resourceChangeEvent in resourceChangeEvents)
        {
            if (resourceChangeEvent == null)
            {
                continue;
            }

            AppendResourceSetCombatLogs(
                BattleCombatLogEntryType.ResourceSpent,
                resourceChangeEvent.OwnerId,
                resourceChangeEvent.SourceCardId,
                resourceChangeEvent.Spent);
            AppendResourceSetCombatLogs(
                BattleCombatLogEntryType.ResourceGained,
                resourceChangeEvent.OwnerId,
                resourceChangeEvent.SourceCardId,
                resourceChangeEvent.Gained);
        }
    }

    private void AppendResourceSetCombatLogs(
        BattleCombatLogEntryType entryType,
        PlayerId ownerId,
        string sourceCardId,
        ResourceSet resources)
    {
        AppendResourceCombatLog(entryType, ownerId, sourceCardId, BattleCombatLogResourceType.Mana, resources?.Mana ?? 0);
        AppendResourceCombatLog(entryType, ownerId, sourceCardId, BattleCombatLogResourceType.Qi, resources?.Qi ?? 0);
        AppendResourceCombatLog(entryType, ownerId, sourceCardId, BattleCombatLogResourceType.Power, resources?.Power ?? 0);
        AppendResourceCombatLog(entryType, ownerId, sourceCardId, BattleCombatLogResourceType.Gold, resources?.Gold ?? 0);
    }

    private void AppendResourceCombatLog(
        BattleCombatLogEntryType entryType,
        PlayerId ownerId,
        string sourceCardId,
        BattleCombatLogResourceType resourceType,
        int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        AppendCombatLogRecord(new BattleCombatLogEntryDto
        {
            EntryType = entryType,
            SourceOwnerId = ownerId,
            SourceCardId = sourceCardId ?? string.Empty,
            Amount = amount,
            ResourceType = resourceType
        });
    }

    private void AppendUnaccountedRemovalCombatLogs(
        IReadOnlyList<BattleEventDto> battleEvents,
        ServerBattleSnapshot beforeSnapshot)
    {
        var valueChangedRuntimeIds = new HashSet<string>(
            battleEvents
                .Where(candidate => candidate != null &&
                                    (candidate.EventType == BattleEventType.DamageApplied ||
                                     candidate.EventType == BattleEventType.HealingApplied) &&
                                    !string.IsNullOrWhiteSpace(candidate.TargetRuntimeId))
                .Select(candidate => candidate.TargetRuntimeId!),
            StringComparer.Ordinal);

        foreach (var removedEvent in battleEvents.Where(candidate =>
                     candidate?.EventType == BattleEventType.OccupantRemoved))
        {
            if (removedEvent == null ||
                (!string.IsNullOrWhiteSpace(removedEvent.TargetRuntimeId) &&
                 valueChangedRuntimeIds.Contains(removedEvent.TargetRuntimeId)))
            {
                continue;
            }

            var removed = beforeSnapshot?.FindByRuntimeId(removedEvent.TargetRuntimeId ?? removedEvent.RuntimeId);
            if (removed == null)
            {
                continue;
            }

            var isCheonraJimang = beforeSnapshot?.PersistentEffects.Any(effect =>
                effect != null && !effect.IsExpired &&
                string.Equals(effect.EffectId, CheonraJimangEffectId, StringComparison.Ordinal) &&
                string.Equals(effect.TargetRuntimeId, removed.RuntimeId, StringComparison.Ordinal)) == true;
            AppendCombatLogRecord(new BattleCombatLogEntryDto
            {
                EntryType = isCheonraJimang
                    ? BattleCombatLogEntryType.DestroyedByMark
                    : BattleCombatLogEntryType.OccupantRemoved,
                SourceCardId = isCheonraJimang ? "CheonraJimang" : string.Empty,
                TargetOwnerId = removed.OwnerId,
                TargetCardId = removed.CardId
            });
        }
    }

    private void AppendStatusChangeCombatLogs(
        ServerBattleSnapshot beforeSnapshot,
        ServerBattleSnapshot afterSnapshot)
    {
        if (beforeSnapshot == null || afterSnapshot == null)
        {
            return;
        }

        foreach (var before in beforeSnapshot.Occupants)
        {
            var after = afterSnapshot.FindByRuntimeId(before.RuntimeId);
            if (after == null)
            {
                continue;
            }

            if (before.IsDrained != after.IsDrained)
            {
                AppendCombatLogRecord(new BattleCombatLogEntryDto
                {
                    EntryType = after.IsDrained
                        ? BattleCombatLogEntryType.DrainedApplied
                        : BattleCombatLogEntryType.DrainedCleared,
                    TargetOwnerId = before.OwnerId,
                    TargetCardId = before.CardId
                });
            }

            if (before.IsErasure != after.IsErasure)
            {
                AppendCombatLogRecord(new BattleCombatLogEntryDto
                {
                    EntryType = after.IsErasure
                        ? BattleCombatLogEntryType.ErasureApplied
                        : BattleCombatLogEntryType.ErasureCleared,
                    TargetOwnerId = before.OwnerId,
                    TargetCardId = before.CardId
                });
            }

            var demonKingRevivalStarted =
                !before.IsDemonKingRevivalPending && after.IsDemonKingRevivalPending;
            var demonKingRevived =
                before.IsDemonKingRevivalPending &&
                !after.IsDemonKingRevivalPending &&
                after.DemonKingRevivalCount > before.DemonKingRevivalCount;

            if (demonKingRevivalStarted)
            {
                AppendCombatLogRecord(new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.DemonKingRevivalStarted,
                    TargetOwnerId = after.OwnerId,
                    TargetCardId = after.CardId,
                    Amount = after.DemonKingRevivalTurnStartsRemaining
                });
            }
            else if (demonKingRevived)
            {
                AppendCombatLogRecord(new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.DemonKingRevived,
                    TargetOwnerId = after.OwnerId,
                    TargetCardId = after.CardId,
                    AttackBonus = DemonKingRules.RevivalStatGain,
                    HpBonus = DemonKingRules.RevivalStatGain
                });
            }
            else if (before.IsSealbound != after.IsSealbound)
            {
                AppendCombatLogRecord(new BattleCombatLogEntryDto
                {
                    EntryType = after.IsSealbound
                        ? BattleCombatLogEntryType.SealboundApplied
                        : BattleCombatLogEntryType.SealboundReleased,
                    TargetOwnerId = before.OwnerId,
                    TargetCardId = before.CardId,
                    Amount = after.SealboundOwnerTurnStartsRemaining
                });
            }

            if (before.IsHiding != after.IsHiding)
            {
                AppendCombatLogRecord(new BattleCombatLogEntryDto
                {
                    EntryType = after.IsHiding
                        ? BattleCombatLogEntryType.HidingApplied
                        : BattleCombatLogEntryType.HidingRevealed,
                    TargetOwnerId = before.OwnerId,
                    TargetCardId = before.CardId
                });
            }

            if (before.HuanShuOwnerTurnsRemaining <= 0 && after.HuanShuOwnerTurnsRemaining > 0)
            {
                AppendCombatLogRecord(new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.HuanShuApplied,
                    TargetOwnerId = after.OwnerId,
                    TargetCardId = after.CardId,
                    Amount = after.HuanShuOwnerTurnsRemaining
                });
            }
            else if (before.HuanShuOwnerTurnsRemaining > 0 && after.HuanShuOwnerTurnsRemaining <= 0)
            {
                AppendCombatLogRecord(new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.HuanShuCleared,
                    TargetOwnerId = after.OwnerId,
                    TargetCardId = after.CardId
                });
            }
        }
    }

    private void AppendHeroGrowthCombatLogs(
        ServerBattleSnapshot beforeSnapshot,
        ServerBattleSnapshot afterSnapshot)
    {
        if (beforeSnapshot == null || afterSnapshot == null)
        {
            return;
        }

        foreach (var before in beforeSnapshot.Occupants)
        {
            if (!string.Equals(before.CardId, HeroRules.CardId, StringComparison.Ordinal))
            {
                continue;
            }

            var after = afterSnapshot.FindByRuntimeId(before.RuntimeId);
            if (after == null)
            {
                continue;
            }

            var attackBonus = Math.Max(0, after.BaseAttack - before.BaseAttack);
            var hpBonus = Math.Max(0, after.MaxHp - before.MaxHp);
            if (attackBonus == 0 && hpBonus == 0)
            {
                continue;
            }

            AppendCombatLogRecord(new BattleCombatLogEntryDto
            {
                EntryType = BattleCombatLogEntryType.HeroGrowth,
                TargetOwnerId = after.OwnerId,
                TargetCardId = after.CardId,
                AttackBonus = attackBonus,
                HpBonus = hpBonus
            });
        }
    }

    private void AppendCombatLogRecord(BattleCombatLogEntryDto entry)
    {
        if (entry == null || entry.EntryType == BattleCombatLogEntryType.Unknown)
        {
            return;
        }

        entry.Sequence = ++_combatLogSequence;
        _combatLogRecords.Add(entry);
        while (_combatLogRecords.Count > MaximumCombatLogEntries)
        {
            _combatLogRecords.RemoveAt(0);
        }
    }

    private static Project333.Runtime.Application.Online.TileCoordDto? CloneTileCoord(ServerTileCoordDto? coord)
    {
        return coord == null
            ? null
            : new Project333.Runtime.Application.Online.TileCoordDto
            {
                Column = coord.Column,
                Row = coord.Row
            };
    }

    private static TileCoord ToRuntimeCoord(ServerTileCoordDto? coord)
    {
        return coord == null ? default : new TileCoord(coord.Column, coord.Row);
    }

    private static bool AreReciprocalMoves(BattleEventDto first, BattleEventDto second)
    {
        return first?.SourceCoord != null && first.TargetCoord != null &&
               second?.SourceCoord != null && second.TargetCoord != null &&
               first.SourceOwnerId == second.SourceOwnerId &&
               first.SourceCoord.Column == second.TargetCoord.Column &&
               first.SourceCoord.Row == second.TargetCoord.Row &&
               first.TargetCoord.Column == second.SourceCoord.Column &&
               first.TargetCoord.Row == second.SourceCoord.Row;
    }

    private static List<int> CreateHpHistory(
        IReadOnlyList<BattleEventDto> valueEvents,
        int fallbackHp)
    {
        if (valueEvents == null || valueEvents.Count == 0)
        {
            return new List<int>
            {
                Math.Max(0, fallbackHp),
                Math.Max(0, fallbackHp)
            };
        }

        var values = new List<int> { Math.Max(0, valueEvents[0].HpBefore) };
        values.AddRange(valueEvents.Select(valueEvent => Math.Max(0, valueEvent.HpAfter)));
        return values;
    }

    private static bool IsRemoved(IReadOnlyList<BattleEventDto> battleEvents, string runtimeId)
    {
        return !string.IsNullOrWhiteSpace(runtimeId) && battleEvents.Any(candidate =>
            candidate?.EventType == BattleEventType.OccupantRemoved &&
            string.Equals(
                string.IsNullOrWhiteSpace(candidate.TargetRuntimeId) ? candidate.RuntimeId : candidate.TargetRuntimeId,
                runtimeId,
                StringComparison.Ordinal));
    }

    private static ServerBattleSnapshot CaptureBattleSnapshot(BattleState? battleState)
    {
        var snapshot = new ServerBattleSnapshot();
        if (battleState == null)
        {
            return snapshot;
        }

        CaptureBoardSnapshot(snapshot, battleState.PlayerBoard);
        CaptureBoardSnapshot(snapshot, battleState.AIBoard);
        foreach (var effect in battleState.PersistentEffects)
        {
            if (effect == null)
            {
                continue;
            }

            snapshot.PersistentEffects.Add(new ServerPersistentEffectSnapshot(
                effect.SourceCardId,
                effect.OwnerId,
                effect.EffectId,
                effect.TargetRuntimeId,
                effect.IsExpired));
        }

        return snapshot;
    }

    private static void CaptureBoardSnapshot(ServerBattleSnapshot snapshot, BoardState boardState)
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

            snapshot.Occupants.Add(new ServerOccupantSnapshot(
                occupant.RuntimeId,
                occupant.CardId,
                occupant.OwnerId,
                occupant.Position,
                occupant.BaseAttack,
                occupant.MaxHp,
                occupant.CurrentHp,
                occupant.Kind,
                occupant.AttackType,
                occupant.DamageType,
                occupant.IsDrained,
                occupant.IsErasure,
                occupant.IsSealbound,
                occupant.SealboundOwnerTurnStartsRemaining,
                occupant.IsHiding,
                occupant.HuanShuOwnerTurnsRemaining,
                occupant.IsDemonKingRevivalPending,
                occupant.DemonKingRevivalTurnStartsRemaining,
                occupant.DemonKingRevivalCountdownPlayerId,
                occupant.DemonKingRevivalEligibleAfterTurnNumber,
                occupant.DemonKingRevivalCount));
        }
    }

    private sealed class ServerBattleSnapshot
    {
        public List<ServerOccupantSnapshot> Occupants { get; } = new();

        public List<ServerPersistentEffectSnapshot> PersistentEffects { get; } = new();

        public ServerOccupantSnapshot? FindByRuntimeId(string runtimeId)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                return null;
            }

            foreach (var occupant in Occupants)
            {
                if (string.Equals(occupant.RuntimeId, runtimeId, StringComparison.Ordinal))
                {
                    return occupant;
                }
            }

            return null;
        }

        public ServerOccupantSnapshot? FindByCoord(PlayerId ownerId, TileCoord coord)
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

    private sealed class ServerOccupantSnapshot
    {
        public ServerOccupantSnapshot(
            string runtimeId,
            string cardId,
            PlayerId ownerId,
            TileCoord coord,
            int baseAttack,
            int maxHp,
            int currentHp,
            OccupantKind kind,
            AttackType attackType,
            DamageType damageType,
            bool isDrained,
            bool isErasure,
            bool isSealbound,
            int sealboundOwnerTurnStartsRemaining,
            bool isHiding,
            int huanShuOwnerTurnsRemaining,
            bool isDemonKingRevivalPending,
            int demonKingRevivalTurnStartsRemaining,
            PlayerId demonKingRevivalCountdownPlayerId,
            int demonKingRevivalEligibleAfterTurnNumber,
            int demonKingRevivalCount)
        {
            RuntimeId = runtimeId ?? string.Empty;
            CardId = cardId ?? string.Empty;
            OwnerId = ownerId;
            Coord = coord;
            BaseAttack = baseAttack;
            MaxHp = maxHp;
            CurrentHp = currentHp;
            Kind = kind;
            AttackType = attackType;
            DamageType = damageType;
            IsDrained = isDrained;
            IsErasure = isErasure;
            IsSealbound = isSealbound;
            SealboundOwnerTurnStartsRemaining = sealboundOwnerTurnStartsRemaining;
            IsHiding = isHiding;
            HuanShuOwnerTurnsRemaining = huanShuOwnerTurnsRemaining;
            IsDemonKingRevivalPending = isDemonKingRevivalPending;
            DemonKingRevivalTurnStartsRemaining = demonKingRevivalTurnStartsRemaining;
            DemonKingRevivalCountdownPlayerId = demonKingRevivalCountdownPlayerId;
            DemonKingRevivalEligibleAfterTurnNumber = demonKingRevivalEligibleAfterTurnNumber;
            DemonKingRevivalCount = demonKingRevivalCount;
        }

        public string RuntimeId { get; }

        public string CardId { get; }

        public PlayerId OwnerId { get; }

        public TileCoord Coord { get; }

        public int BaseAttack { get; }

        public int MaxHp { get; }

        public int CurrentHp { get; }

        public OccupantKind Kind { get; }

        public AttackType AttackType { get; }

        public DamageType DamageType { get; }

        public bool IsDrained { get; }

        public bool IsErasure { get; }

        public bool IsSealbound { get; }

        public int SealboundOwnerTurnStartsRemaining { get; }

        public bool IsHiding { get; }

        public int HuanShuOwnerTurnsRemaining { get; }

        public bool IsDemonKingRevivalPending { get; }

        public int DemonKingRevivalTurnStartsRemaining { get; }

        public PlayerId DemonKingRevivalCountdownPlayerId { get; }

        public int DemonKingRevivalEligibleAfterTurnNumber { get; }

        public int DemonKingRevivalCount { get; }
    }

    private sealed class ServerPersistentEffectSnapshot
    {
        public ServerPersistentEffectSnapshot(
            string sourceCardId,
            PlayerId ownerId,
            string effectId,
            string targetRuntimeId,
            bool isExpired)
        {
            SourceCardId = sourceCardId ?? string.Empty;
            OwnerId = ownerId;
            EffectId = effectId ?? string.Empty;
            TargetRuntimeId = targetRuntimeId ?? string.Empty;
            IsExpired = isExpired;
        }

        public string SourceCardId { get; }

        public PlayerId OwnerId { get; }

        public string EffectId { get; }

        public string TargetRuntimeId { get; }

        public bool IsExpired { get; }
    }

    private sealed class PendingDisconnectReservation
    {
        public PendingDisconnectReservation(
            PlayerIdDto seatId,
            string reconnectIdentityKey,
            string accountId,
            string playerToken,
            OnlineBattleSeatId onlineSeatId,
            DateTimeOffset reconnectDeadlineUtc)
        {
            SeatId = seatId;
            ReconnectIdentityKey = reconnectIdentityKey ?? string.Empty;
            AccountId = accountId ?? string.Empty;
            PlayerToken = playerToken ?? string.Empty;
            OnlineSeatId = onlineSeatId;
            ReconnectDeadlineUtc = reconnectDeadlineUtc;
        }

        public PlayerIdDto SeatId { get; }

        public string ReconnectIdentityKey { get; }

        public string AccountId { get; }

        public string PlayerToken { get; }

        public OnlineBattleSeatId OnlineSeatId { get; }

        public DateTimeOffset ReconnectDeadlineUtc { get; }
    }

    private sealed class BattleSeatMetadata
    {
        public BattleSeatMetadata(string accountId, string runId, string deckId,
            IEnumerable<string> deckCardIds, IEnumerable<KeyValuePair<string, int>> cardUpgradeLevels, string reconnectIdentityKey)
        {
            AccountId = (accountId ?? string.Empty).Trim();
            RunId = (runId ?? string.Empty).Trim();
            DeckId = (deckId ?? string.Empty).Trim();
            ReconnectIdentityKey = reconnectIdentityKey ?? string.Empty;
            DeckCardIds = Array.AsReadOnly(deckCardIds.ToArray());
            CardUpgradeLevels = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(
                cardUpgradeLevels.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase));
        }

        public string AccountId { get; }
        public string RunId { get; }
        public string DeckId { get; }
        public string ReconnectIdentityKey { get; }
        public IReadOnlyList<string> DeckCardIds { get; }
        public IReadOnlyDictionary<string, int> CardUpgradeLevels { get; }
    }

    private static IReadOnlyList<string> CreatePrototypeDeck(string ownerPrefix)
    {
        var seedCards = new[]
        {
            "CheonraJimang",
            "Microreactor",
            "ElfLongbowScout",
            "ManaWeaver",
            "Shieldbearer",
            "Vampire",
            "Cerberus",
            "A-111",
            "BlueDragon",
            "RedDragon",
            "Gwangma",
            "ManaPond",
            "Shaolin_1st_Disciple"
        };
        var guaranteedOpeningTopCards = new[]
        {
            "GoldMiner",
            "Daehwandan",
            "firebolt",
            "Goblin"
        };

        var deck = new List<string>(33);
        for (var i = 0; i < 33 - guaranteedOpeningTopCards.Length; i++)
        {
            deck.Add(seedCards[i % seedCards.Length]);
        }

        // DeckState draws from the end of the list, so this order makes Goblin draw first.
        deck.AddRange(guaranteedOpeningTopCards);
        return deck;
    }

    private IReadOnlyList<string> ResolvePlayerDeckCardIds()
    {
        return ResolveDeckCardIds(PlayerIdDto.Player, "player");
    }

    private IReadOnlyList<string> ResolveOpponentDeckCardIds()
    {
        return ResolveDeckCardIds(PlayerIdDto.AI, "ai");
    }

    private ICardUpgradeLevelProvider CreateCardUpgradeLevelProvider()
    {
        var provider = new InMemoryCardUpgradeLevelProvider();
        foreach (var pair in _seatMetadata)
        {
            var ownerId = ToRuntimePlayerId(pair.Key);
            foreach (var upgrade in pair.Value.CardUpgradeLevels)
            {
                provider.SetUpgradeLevel(ownerId, upgrade.Key, upgrade.Value);
            }
        }

        return provider;
    }

    private IReadOnlyList<string> ResolveDeckCardIds(PlayerIdDto seatId, string fallbackOwnerPrefix)
    {
        return _seatMetadata.TryGetValue(seatId, out var participant) && participant.DeckCardIds.Count > 0
            ? participant.DeckCardIds
            : CreatePrototypeDeck(fallbackOwnerPrefix);
    }

    private static string FormatConnection(BattleClientConnection connection)
    {
        var identity = string.IsNullOrWhiteSpace(connection.DisplayName)
            ? connection.PlayerToken
            : connection.DisplayName;
        if (string.IsNullOrWhiteSpace(identity))
        {
            identity = string.IsNullOrWhiteSpace(connection.AccountId)
                ? "unknown-token"
                : connection.AccountId;
        }

        var seatLabel = connection.AssignedOnlineSeatId == OnlineBattleSeatId.None
            ? connection.AssignedSeatId.ToString()
            : connection.AssignedOnlineSeatId.ToString();

        return $"{identity}/{seatLabel} ({connection.ConnectionId})";
    }

    private static string GetReconnectIdentityKey(BattleClientConnection connection)
    {
        if (connection == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrWhiteSpace(connection.AccountId)
            ? connection.AccountId
            : connection.PlayerToken ?? string.Empty;
    }

    private void RecordConnectionMetadata(BattleClientConnection connection)
    {
        if (connection == null || !connection.HasAssignedSeat)
        {
            return;
        }

        if (_battleFlowController?.CurrentBattleState != null)
        {
            if (!_seatMetadata.TryGetValue(connection.AssignedSeatId, out var participant) ||
                !string.Equals(participant.ReconnectIdentityKey, GetReconnectIdentityKey(connection), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Battle participant identity cannot change after battle start.");
            }
            connection.AccountId = participant.AccountId;
            connection.RunId = participant.RunId;
            connection.DeckId = participant.DeckId;
            connection.PlayerDeckCardIds.Clear();
            connection.PlayerDeckCardIds.AddRange(participant.DeckCardIds);
            connection.CardUpgradeLevels.Clear();
            foreach (var upgrade in participant.CardUpgradeLevels)
                connection.CardUpgradeLevels[upgrade.Key] = upgrade.Value;
            return;
        }

        _seatMetadata[connection.AssignedSeatId] = new BattleSeatMetadata(
            connection.AccountId, connection.RunId, connection.DeckId,
            connection.PlayerDeckCardIds, connection.CardUpgradeLevels, GetReconnectIdentityKey(connection));
    }

    private BattleSessionConnectionSnapshot CreateConnectionSnapshot(BattleClientConnection connection)
    {
        _seatMetadata.TryGetValue(connection.AssignedSeatId, out var participant);
        return new BattleSessionConnectionSnapshot(
            connection.ConnectionId, participant?.AccountId ?? connection.AccountId, connection.DisplayName,
            connection.AssignedSeatId.ToString(), connection.AssignedOnlineSeatId.ToString(),
            participant?.RunId ?? connection.RunId, participant?.DeckId ?? connection.DeckId,
            connection.HasAssignedSeat, connection.IsOpen);
    }

    private string FormatSeatMetadataSummary()
    {
        if (_seatMetadata.Count == 0)
        {
            return string.Empty;
        }

        var parts = SeatOrder
            .Where(seatId => _seatMetadata.TryGetValue(seatId, out var metadata) &&
                             (!string.IsNullOrWhiteSpace(metadata.RunId) ||
                              !string.IsNullOrWhiteSpace(metadata.DeckId)))
            .Select(seatId =>
            {
                var metadata = _seatMetadata[seatId];
                var runId = string.IsNullOrWhiteSpace(metadata.RunId) ? "-" : metadata.RunId;
                var deckId = string.IsNullOrWhiteSpace(metadata.DeckId) ? "-" : metadata.DeckId;
                return $"{seatId} run={runId} deck={deckId}";
            })
            .ToList();

        return parts.Count == 0 ? string.Empty : $"Metadata: {string.Join("; ", parts)}. ";
    }
}

public sealed record BattleDisconnect(bool Reserved, OnlineBattleEnvelope? Envelope,
    PlayerIdDto SeatId, string IdentityKey, DateTimeOffset DeadlineUtc);

public sealed record PendingReconnectStatusSnapshot(
    string MatchId,
    PlayerIdDto SeatId,
    OnlineBattleSeatId OnlineSeatId,
    int RemainingSeconds,
    DateTimeOffset ReconnectDeadlineUtc);

public sealed record BattleSessionPersistenceSnapshot(
    string MatchId,
    bool UseServerAiOpponent,
    int ConnectionCount,
    bool IsBattleStarted,
    bool IsBattleEnded,
    string Reason,
    DateTimeOffset CreatedAtUtc,
    BattleSessionDomainSnapshot? DomainState,
    BattleStateViewDto? PlayerView,
    BattleStateViewDto? AiView,
    IReadOnlyList<BattleSessionConnectionSnapshot> Connections,
    IReadOnlyList<BattleCombatLogEntryDto>? CombatLogEntries = null,
    IReadOnlyList<BattleSessionParticipantSnapshot>? Participants = null,
    Guid ResultId = default,
    string ResultEndedReason = "normal");

public sealed record BattleSessionParticipantSnapshot(
    string RuntimeSeatId,
    string OnlineSeatId,
    string AccountId,
    string RunId,
    string DeckId,
    IReadOnlyList<string> DeckCardIds,
    IReadOnlyDictionary<string, int> CardUpgradeLevels);

public sealed record BattleSessionConnectionSnapshot(
    string ConnectionId,
    string AccountId,
    string DisplayName,
    string RuntimeSeatId,
    string OnlineSeatId,
    string RunId,
    string DeckId,
    bool HasAssignedSeat,
    bool IsOpen);

public sealed record BattleSessionDomainSnapshot(
    int TurnNumber,
    string ActivePlayerId,
    string Phase,
    bool HasWinner,
    string WinnerId,
    int ActionSequence,
    long TurnTimerVersion,
    string TurnTimerPlayerId,
    DateTimeOffset TurnTimerDeadlineUtc,
    BattleSessionPlayerSnapshot Player,
    BattleSessionPlayerSnapshot AI,
    BattleSessionBoardSnapshot PlayerBoard,
    BattleSessionBoardSnapshot AIBoard,
    IReadOnlyList<BattleSessionPersistentEffectSnapshot> PersistentEffects,
    BattleSessionPendingRobotFusionSnapshot? PendingRobotFusion = null,
    bool IsDraw = false);

public sealed record BattleSessionPendingRobotFusionSnapshot(
    string OwnerId,
    string CardId);

public sealed record BattleSessionPlayerSnapshot(
    string PlayerId,
    BattleSessionResourceSnapshot Resources,
    IReadOnlyList<string> DeckCardIds,
    IReadOnlyList<string> HandCardIds,
    IReadOnlyList<string> DiscardCardIds,
    int FailedDrawCount,
    bool HasUsedMulligan,
    int MaxHandSizeBonus,
    string MasterRuntimeId,
    IReadOnlyList<BattleSessionHandCardSnapshot>? HandCards = null);

public sealed record BattleSessionHandCardSnapshot(
    string RuntimeId,
    string CardId,
    bool IsTemporaryReplicate);

public sealed record BattleSessionBoardSnapshot(
    IReadOnlyList<BattleSessionOccupantSnapshot> Occupants);

public sealed record BattleSessionOccupantSnapshot(
    string RuntimeId,
    string CardId,
    string OwnerId,
    string Kind,
    int Column,
    int Row,
    string AttackType,
    int BaseAttack,
    int MaxHp,
    int CurrentHp,
    bool CanMove,
    BattleSessionResourceSnapshot TurnStartResourceGain,
    int MaxAttacksPerTurn,
    int HitsPerAttack,
    bool HasBerserker,
    bool HasEndure,
    bool HasShielder,
    bool HasLifeSteal,
    bool EndureUsed,
    bool IsDrained,
    bool IsErasure,
    bool HasSummoningSickness,
    int RemainingAttacksThisTurn,
    bool IsScience,
    int SciencePowerUpkeep,
    bool CanAttackAsBuilding,
    string DamageType = "",
    int PhysicalDefense = 0,
    int MagicDefense = 0,
    bool HasRobot = false,
    int? OriginalAttack = null,
    int? OriginalMaxHp = null,
    int? OriginalPhysicalDefense = null,
    int? OriginalMagicDefense = null,
    bool WasSummonedThisTurn = false,
    [property: System.Text.Json.Serialization.JsonPropertyName("IsDisabled")]
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool LegacyIsDisabled = false,
    bool HasRush = false,
    bool IsSealbound = false,
    int SealboundOwnerTurnStartsRemaining = 0,
    bool HasHiding = false,
    bool HidingRevealed = false,
    bool HasFlying = false,
    int SpellPower = 0,
    IReadOnlyList<BattleSessionInvincibleEffectSnapshot>? InvincibleEffects = null,
    int HuanShuOwnerTurnsRemaining = 0,
    int HuanShuEligibleAfterTurnNumber = 0,
    bool HasPiercing = false,
    bool IsDemonKingRevivalPending = false,
    int DemonKingRevivalTurnStartsRemaining = 0,
    string DemonKingRevivalCountdownPlayerId = "",
    int DemonKingRevivalEligibleAfterTurnNumber = 0,
    int DemonKingRevivalCount = 0,
    [property: System.Text.Json.Serialization.JsonPropertyName("HasGuard")]
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool LegacyHasGuard = false);

public sealed record BattleSessionInvincibleEffectSnapshot(
    string Duration,
    int OwnerTurnsRemaining,
    int AppliedTurnNumber,
    string AppliedActivePlayerId);

public sealed record BattleSessionPersistentEffectSnapshot(
    string SourceCardId,
    string OwnerId,
    string EffectId,
    int AppliedTurn,
    string EndConditionText,
    BattleSessionResourceSnapshot TurnStartResourceGain,
    int OwnerTurnStartsRemaining,
    string TargetRuntimeId,
    bool IsExpired,
    int TargetRow = -1,
    int RemainingTriggers = 0,
    int EffectDamage = 0,
    string EffectDamageType = "",
    bool TargetsOwnerBoard = false,
    int CapturedSpellPower = 0,
    int TargetStartColumn = -1);

public sealed record BattleSessionResourceSnapshot(
    int Mana,
    int Qi,
    int Power,
    int Gold);
