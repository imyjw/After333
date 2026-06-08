using System;
using System.Collections;
using System.Collections.Generic;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Draft;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class BattleBootstrapper : MonoBehaviour, IDraftOverlayHost
    {
        [System.Serializable]
        public sealed class CombatLogChangedEvent : UnityEvent<string>
        {
        }

        private const int MaxCombatLogEntries = 10;
        private const string BusyMessage = "Battle is still resolving animations.";
        private const string MasterCardAssetResourcePath = "Project333/SpecialCards/MasterCard";
        private const string FireboltSpellEffectId = "firebolt";
        private const string FireboltSpellEffectResourcePath = "Project333/SpellEffects/Firebolt-Sheet";
        private const int FireboltSpellEffectFrameCount = 7;
        private const float FireboltSpellEffectFramesPerSecond = 14f;
        private static readonly Vector2 FireboltSpellEffectSize = new Vector2(220f, 220f);
        private static IReadOnlyList<Sprite> s_fireboltSpellEffectFrames;

        [SerializeField] private BattleScreenPresenter _battleScreenPresenter;
        [SerializeField] private CardDefinitionCatalogAsset _cardCatalogAsset;
        [SerializeField] private DeckDefinitionAsset _playerDeckAsset;
        [SerializeField] private DeckDefinitionAsset _aiDeckAsset;
        [SerializeField] private string[] _playerDeckCardIds = Array.Empty<string>();
        [SerializeField] private string[] _aiDeckCardIds = Array.Empty<string>();
        [SerializeField] private bool _useGeneratedDebugDecksWhenEmpty = true;
        [Min(BattleDebugDeckFactory.MinimumDeckSize)]
        [SerializeField] private int _generatedDeckSize = BattleDebugDeckFactory.MinimumDeckSize;
        [SerializeField] private PlayerId _firstPlayerId = PlayerId.Player;
        [SerializeField] private bool _startOnAwake = true;
        [SerializeField] private bool _autoPassPlayerMulligan = true;
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
        [SerializeField] private string _combatLogText = "No actions yet.";
        [TextArea(2, 6)]
        [SerializeField] private string _lastInteractionStatus = "No interaction yet.";

        private BattleFlowController _battleFlowController;
        private ICardDefinitionProvider _cardDefinitionProvider;
        private AiDecisionService _aiDecisionService;
        private AiTurnRunner _aiTurnRunner;
        private DraftSessionService _draftSessionService;
        private Coroutine _presentationSequenceCoroutine;
        private readonly List<string> _combatLogEntries = new List<string>();
        private IReadOnlyList<string> _runtimePlayerDeckCardIdsOverride = Array.Empty<string>();
        private IReadOnlyList<string> _runtimeAIDeckCardIdsOverride = Array.Empty<string>();
        private bool _isBusy;
        private bool _hasQueuedDraftSceneReturn;

        public BattleState CurrentBattleState => _battleFlowController?.CurrentBattleState;

        public string CombatLogText => _combatLogText;

        public string LastInteractionStatus => _lastInteractionStatus;

        public bool IsBusy => _isBusy;

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

        public void BeginDraft()
        {
            CancelPresentationSequence();
            _battleScreenPresenter?.SetBattleUiVisible(false);
            _runtimePlayerDeckCardIdsOverride = Array.Empty<string>();
            _runtimeAIDeckCardIdsOverride = Array.Empty<string>();
            _draftSessionService = new DraftSessionService(_cardCatalogAsset, CreateDraftRandom());
            var validation = _draftSessionService.ValidateCatalog();

            _battleFlowController = null;
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

        private void Awake()
        {
            if (_battleScreenPresenter != null)
            {
                _battleScreenPresenter.Bind(this);
            }

            if (_draftOverlayPresenter != null || _startWithDraftBeforeBattle)
            {
                ResolveDraftOverlayPresenter();
            }

            if (_startOnAwake)
            {
                if (DraftRunSessionState.TryConsumePendingBattleStart(out var draftedDeckCardIds))
                {
                    StartBattleWithDeckCardIds(draftedDeckCardIds);
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

        public void StartBattle()
        {
            if (_startWithDraftBeforeBattle && (_runtimePlayerDeckCardIdsOverride == null || _runtimePlayerDeckCardIdsOverride.Count == 0))
            {
                BeginDraft();
                return;
            }

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
            _battleFlowController.StartBattle(new BattleSetupRequest(
                playerDeckCardIds: resolvedConfig.PlayerDeckCardIds,
                aiDeckCardIds: resolvedConfig.AIDeckCardIds,
                firstPlayerId: _firstPlayerId));

            AddCombatLogEntry(CombatLogFormatter.FormatBattleStarted(_firstPlayerId));

            if (_autoPassPlayerMulligan && CurrentBattleState.Phase == PhaseType.Mulligan)
            {
                _battleFlowController.PassMulligan(PlayerId.Player);
                AddCombatLogEntry(CombatLogFormatter.FormatMulliganPassed(PlayerId.Player));
            }

            if (_autoResolveTurnStartAfterAutoPass && CurrentBattleState.Phase == PhaseType.TurnStart)
            {
                _battleFlowController.ResolveTurnStart();
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
            if (!TryBeginImmediateInteraction())
            {
                return;
            }

            EnsureBattleStarted();
            _battleFlowController.PassMulligan(PlayerId.Player);
            AddCombatLogEntry(CombatLogFormatter.FormatMulliganPassed(PlayerId.Player));

            if (_autoResolveTurnStartAfterAutoPass && CurrentBattleState.Phase == PhaseType.TurnStart)
            {
                _battleFlowController.ResolveTurnStart();
                AddCombatLogEntry(CombatLogFormatter.FormatTurnStartResolved(CurrentBattleState.ActivePlayerId, CurrentBattleState.TurnNumber));
            }

            RefreshPresenter();
            MaybeRunAiTurn();
        }

        public void ResolveTurnStart()
        {
            if (!TryBeginImmediateInteraction())
            {
                return;
            }

            EnsureBattleStarted();
            var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
            _battleFlowController.ResolveTurnStart();
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
            var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
            AddCombatLogEntry(CombatLogFormatter.FormatEndTurn(CurrentBattleState.ActivePlayerId));
            _battleFlowController.EndTurn();
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

        public bool TryMovePlayerOccupant(TileCoord fromCoord, TileCoord toCoord, out string message)
        {
            EnsureBattleStarted();

            if (IsBusy)
            {
                message = BusyMessage;
                _lastInteractionStatus = message;
                return false;
            }

            try
            {
                var isSwap = CurrentBattleState.PlayerBoard.GetOccupant(toCoord) != null;
                _battleFlowController.ExecuteCommand(PlayerId.Player, new MoveOccupantCommand(fromCoord, toCoord));
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

            try
            {
                var attackCommand = new AttackCommand(attackerCoord, targetCoord);
                if (!ShouldAnimateAttackSequences())
                {
                    _battleFlowController.ExecuteCommand(PlayerId.Player, attackCommand);
                    message = $"Attacked from {attackerCoord} to {targetCoord}.";
                    _lastInteractionStatus = message;
                    AddCombatLogEntry(message);
                    RefreshPresenter();
                    MaybeRunAiTurn();
                    return true;
                }

                var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                var attackerIsRanged = IsRangedAttacker(CurrentBattleState, PlayerId.Player, attackerCoord);
                var guardInfo = GuardService.Resolve(CurrentBattleState.GetOpponentBoard(PlayerId.Player), targetCoord);
                _battleFlowController.ExecuteCommand(PlayerId.Player, attackCommand);
                var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                var valuePopupEvents = CloneValuePopupEvents(CurrentBattleState);
                var defenderCounterattacks = CanDefenderCounterattack(CurrentBattleState, PlayerId.Player, attackerCoord, PlayerId.AI, guardInfo);
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
            EnsureBattleStarted();

            if (IsBusy)
            {
                message = BusyMessage;
                _lastInteractionStatus = message;
                return false;
            }

            if (string.IsNullOrWhiteSpace(cardId))
            {
                message = "No card is selected.";
                _lastInteractionStatus = message;
                return false;
            }

            try
            {
                var definition = _cardDefinitionProvider.GetRequired(cardId);

                switch (definition.CardType)
                {
                    case CardType.Unit:
                        if (targetOwnerId != PlayerId.Player)
                        {
                            message = $"Card '{cardId}' must be placed on a player field tile.";
                            _lastInteractionStatus = message;
                            return false;
                        }

                        _battleFlowController.ExecuteCommand(PlayerId.Player, new PlayUnitCardCommand(cardId, targetCoord));
                        break;

                    case CardType.Building:
                        if (targetOwnerId != PlayerId.Player)
                        {
                            message = $"Card '{cardId}' must be placed on a player field tile.";
                            _lastInteractionStatus = message;
                            return false;
                        }

                        _battleFlowController.ExecuteCommand(PlayerId.Player, new PlayBuildingCardCommand(cardId, targetCoord));
                        break;

                    case CardType.Spell:
                        if (definition is DamageSpellCardDefinition)
                        {
                            if (!ShouldAnimateAttackSequences())
                            {
                                _battleFlowController.ExecuteCommand(PlayerId.Player, new CastDamageSpellCommand(cardId, targetOwnerId, targetCoord));
                                break;
                            }

                            var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                            _battleFlowController.ExecuteCommand(PlayerId.Player, new CastDamageSpellCommand(cardId, targetOwnerId, targetCoord));
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
                            if (string.Equals(scriptedSpellDefinition.EffectId, "cheonra_jimang", StringComparison.Ordinal))
                            {
                                _battleFlowController.ExecuteCommand(PlayerId.Player, new CastScriptedSpellCommand(cardId, targetOwnerId, targetCoord));
                                break;
                            }

                            message = $"Card '{cardId}' is a self-cast scripted spell. Use the cast selected spell button.";
                            _lastInteractionStatus = message;
                            return false;
                        }

                        if (definition is PersistentResourceSpellCardDefinition)
                        {
                            message = $"Card '{cardId}' is a persistent spell. Use the cast selected spell button.";
                            _lastInteractionStatus = message;
                            return false;
                        }

                        message = $"Card '{cardId}' uses an unsupported spell type in the current slice.";
                        _lastInteractionStatus = message;
                        return false;

                    default:
                        message = $"Card '{cardId}' cannot be used on a board tile in the current slice.";
                        _lastInteractionStatus = message;
                        return false;
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
                message = ex.Message;
                _lastInteractionStatus = message;
                Debug.LogWarning($"Play card interaction failed for '{cardId}' at {targetCoord}: {ex.Message}");
                return false;
            }
        }

        public bool TryCastPlayerPersistentSpell(string cardId, out string message)
        {
            EnsureBattleStarted();

            if (IsBusy)
            {
                message = BusyMessage;
                _lastInteractionStatus = message;
                return false;
            }

            if (string.IsNullOrWhiteSpace(cardId))
            {
                message = "No card is selected.";
                _lastInteractionStatus = message;
                return false;
            }

            try
            {
                var definition = _cardDefinitionProvider.GetRequired(cardId);
                if (definition is PersistentResourceSpellCardDefinition)
                {
                    _battleFlowController.ExecuteCommand(PlayerId.Player, new CastPersistentResourceSpellCommand(cardId));
                    message = $"Cast persistent spell '{cardId}'.";
                }
                else if (definition is ScriptedSpellCardDefinition scriptedSpellDefinition &&
                         string.Equals(scriptedSpellDefinition.EffectId, "daehwandan", StringComparison.Ordinal))
                {
                    _battleFlowController.ExecuteCommand(PlayerId.Player, new CastScriptedSpellCommand(cardId));
                    message = $"Cast spell '{cardId}'.";
                }
                else
                {
                    message = $"Card '{cardId}' is not a self-cast spell.";
                    _lastInteractionStatus = message;
                    return false;
                }

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
                Debug.LogWarning($"Cast persistent spell interaction failed for '{cardId}': {ex.Message}");
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

            _aiTurnRunner.RunAiTurn(_battleFlowController, AddCombatLogEntry);

            if (_autoResolvePlayerTurnStartAfterAi &&
                CurrentBattleState != null &&
                !CurrentBattleState.IsEnded &&
                CurrentBattleState.ActivePlayerId == PlayerId.Player &&
                CurrentBattleState.Phase == PhaseType.TurnStart)
            {
                _battleFlowController.ResolveTurnStart();
                AddCombatLogEntry(CombatLogFormatter.FormatTurnStartResolved(PlayerId.Player, CurrentBattleState.TurnNumber));
            }

            RefreshPresenter();
        }

        private void ClearCombatLog()
        {
            _combatLogEntries.Clear();
            _combatLogText = "No actions yet.";
            _onCombatLogChanged.Invoke(_combatLogText);
        }

        private void AddCombatLogEntry(string entry)
        {
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
            if (_battleFlowController == null || CurrentBattleState == null)
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
                _runtimeAIDeckCardIdsOverride);

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
                new TurnStartService(),
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
                   DraftRunSessionState.HasDraftedDeckReady &&
                   CurrentBattleState != null &&
                   CurrentBattleState.IsEnded &&
                   !string.IsNullOrWhiteSpace(DraftRunSessionState.DraftSceneName);
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

            DraftRunSessionState.RecordBattleResult(CurrentBattleState.Result.Winner == PlayerId.Player);
            SceneManager.LoadScene(DraftRunSessionState.DraftSceneName);
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
                    _battleFlowController.ResolveTurnStart();
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
                    _battleFlowController.EndTurn();
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
                    var guardInfo = GuardService.Resolve(CurrentBattleState.GetOpponentBoard(PlayerId.AI), attackCommand.TargetCoord);
                    _battleFlowController.ExecuteCommand(PlayerId.AI, attackCommand);
                    AddCombatLogEntry(CombatLogFormatter.FormatCommand(PlayerId.AI, attackCommand));
                    var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    var valuePopupEvents = CloneValuePopupEvents(CurrentBattleState);
                    var defenderCounterattacks = CanDefenderCounterattack(CurrentBattleState, PlayerId.AI, attackCommand.AttackerCoord, PlayerId.Player, guardInfo);
                    var animationRequest = BuildAttackAnimationRequest(beforeSnapshot, afterSnapshot, PlayerId.AI, attackCommand.AttackerCoord, PlayerId.Player, attackCommand.TargetCoord, guardInfo, attackerIsRanged, defenderCounterattacks, valuePopupEvents);
                    yield return PlayAttackAnimationSequence(animationRequest);
                    RefreshPresenter();
                    yield return null;
                    continue;
                }

                if (command is CastDamageSpellCommand castDamageSpellCommand)
                {
                    var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    _battleFlowController.ExecuteCommand(PlayerId.AI, castDamageSpellCommand);
                    AddCombatLogEntry(CombatLogFormatter.FormatCommand(PlayerId.AI, castDamageSpellCommand));
                    var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    var impactRequest = BuildImpactAnimationRequest(beforeSnapshot, afterSnapshot, CloneValuePopupEvents(CurrentBattleState), castDamageSpellCommand.CardId);
                    yield return PlayImpactAnimationSequence(impactRequest);
                    RefreshPresenter();
                    yield return null;
                    continue;
                }

                _battleFlowController.ExecuteCommand(PlayerId.AI, command);
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
                _battleFlowController.ResolveTurnStart();
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
                : GetRangedAttackNudgeTarget(attackerStartPosition, attackDestination, _rangedAttackNudgeDistance);

            attackerView.RestoreVisualLayout();
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
                if (valuePopupEvent == null || valuePopupEvent.Amount <= 0)
                {
                    continue;
                }

                clonedEvents.Add(new BattleValuePopupEvent(
                    valuePopupEvent.RuntimeId,
                    valuePopupEvent.OwnerId,
                    valuePopupEvent.Coord,
                    valuePopupEvent.IsHealing,
                    valuePopupEvent.Amount));
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
                if (valuePopupEvent == null || valuePopupEvent.Amount <= 0)
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
                targetView.QueueFloatingValuePopup(valuePopupEvent.Amount, valuePopupEvent.IsHealing, scheduledDelay, delayStepIndex);
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
                   !defender.IsDisabled;
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
    }
}
