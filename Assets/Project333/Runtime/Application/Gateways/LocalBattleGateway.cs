using System;
using System.Collections.Generic;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Application.Gateways
{
    public sealed class LocalBattleGateway : IBattleGateway
    {
        private readonly BattleFlowController _battleFlowController;

        public LocalBattleGateway(BattleFlowController battleFlowController)
        {
            _battleFlowController = battleFlowController ?? throw new ArgumentNullException(nameof(battleFlowController));
        }

        public BattleFlowController BattleFlowController => _battleFlowController;

        public BattleState CurrentBattleState => _battleFlowController.CurrentBattleState;

        public BattleState StartBattle(BattleSetupRequest request)
        {
            return _battleFlowController.StartBattle(request);
        }

        public void ApplyMulligan(PlayerId playerId, IEnumerable<string> selectedCardIds, IDeckShuffler deckShuffler)
        {
            _battleFlowController.ApplyMulligan(playerId, selectedCardIds, deckShuffler);
        }

        public void PassMulligan(PlayerId playerId)
        {
            _battleFlowController.PassMulligan(playerId);
        }

        public void ResolveTurnStart()
        {
            _battleFlowController.ResolveTurnStart();
        }

        public void ExecuteCommand(PlayerId actorId, IBattleCommand command)
        {
            _battleFlowController.ExecuteCommand(actorId, command);
        }

        public void EndTurn()
        {
            _battleFlowController.EndTurn();
        }
    }
}
