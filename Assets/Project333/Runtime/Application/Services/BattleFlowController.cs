using System;
using System.Collections.Generic;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Services
{
    public sealed class BattleFlowController
    {
        private readonly BattleSetupService _battleSetupService;
        private readonly MulliganService _mulliganService;
        private readonly TurnStartService _turnStartService;
        private readonly BattleCommandProcessor _battleCommandProcessor;

        public BattleFlowController()
            : this(
                new BattleSetupService(),
                new MulliganService(),
                new TurnStartService(),
                CreateDefaultCommandProcessor())
        {
        }

        public BattleFlowController(
            BattleSetupService battleSetupService,
            MulliganService mulliganService,
            TurnStartService turnStartService,
            BattleCommandProcessor battleCommandProcessor)
        {
            _battleSetupService = battleSetupService ?? throw new ArgumentNullException(nameof(battleSetupService));
            _mulliganService = mulliganService ?? throw new ArgumentNullException(nameof(mulliganService));
            _turnStartService = turnStartService ?? throw new ArgumentNullException(nameof(turnStartService));
            _battleCommandProcessor = battleCommandProcessor ?? throw new ArgumentNullException(nameof(battleCommandProcessor));
        }

        public BattleState CurrentBattleState { get; private set; }

        public BattleState StartBattle(BattleSetupRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            CurrentBattleState = _battleSetupService.CreateInitialState(request);
            return CurrentBattleState;
        }

        public void ApplyMulligan(
            PlayerId playerId,
            IEnumerable<string> selectedCardIds,
            IDeckShuffler deckShuffler)
        {
            EnsureBattleStarted();
            _mulliganService.ApplyMulligan(CurrentBattleState, playerId, selectedCardIds, deckShuffler);
        }

        public void PassMulligan(PlayerId playerId)
        {
            EnsureBattleStarted();
            _mulliganService.PassMulligan(CurrentBattleState, playerId);
        }

        public void ResolveTurnStart()
        {
            EnsureBattleStarted();
            _turnStartService.ResolveTurnStart(CurrentBattleState);
        }

        public void ExecuteCommand(PlayerId actorId, IBattleCommand command)
        {
            EnsureBattleStarted();
            _battleCommandProcessor.Execute(CurrentBattleState, actorId, command);
        }

        public void EndTurn()
        {
            EnsureBattleStarted();
            ExecuteCommand(CurrentBattleState.ActivePlayerId, new EndTurnCommand());
        }

        private void EnsureBattleStarted()
        {
            if (CurrentBattleState == null)
            {
                throw new InvalidOperationException("Battle flow cannot continue before a battle has been started.");
            }
        }

        private static BattleCommandProcessor CreateDefaultCommandProcessor()
        {
            var provider = new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>());

            return new BattleCommandProcessor(
                new PlayCardService(provider),
                new SpellService(provider),
                new MoveService(),
                new AttackService(),
                new EndTurnService());
        }
    }
}
