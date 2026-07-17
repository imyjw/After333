using System.Collections.Generic;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Application.Gateways
{
    public interface IBattleGateway
    {
        BattleState CurrentBattleState { get; }

        BattleState StartBattle(BattleSetupRequest request);

        void ApplyMulligan(PlayerId playerId, IEnumerable<string> selectedCardIds, IDeckShuffler deckShuffler);

        void PassMulligan(PlayerId playerId);

        void ResolveTurnStart();

        void ExecuteCommand(PlayerId actorId, IBattleCommand command);

        void EndTurn();
    }
}
