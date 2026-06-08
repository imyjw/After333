using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Application.Services
{
    public sealed class BattleSetupService
    {
        private static readonly TileCoord MasterStartCoord = new TileCoord(2, 1);

        private const int StartingMana = 0;
        private const int StartingQi = 0;
        private const int StartingPower = 0;
        private const int StartingGold = 3;

        private const int MasterAttack = 3;
        private const int MasterHp = 333;

        private const int FirstPlayerOpeningHand = 3;
        private const int SecondPlayerOpeningHand = 4;

        public BattleState CreateInitialState(BattleSetupRequest request)
        {
            var playerBoard = new BoardState();
            var aiBoard = new BoardState();

            var playerMaster = new MasterState(
                runtimeId: "player-master",
                ownerId: PlayerId.Player,
                position: MasterStartCoord,
                attack: MasterAttack,
                maxHp: MasterHp);

            var aiMaster = new MasterState(
                runtimeId: "ai-master",
                ownerId: PlayerId.AI,
                position: MasterStartCoord,
                attack: MasterAttack,
                maxHp: MasterHp);

            playerBoard.Place(MasterStartCoord, playerMaster);
            aiBoard.Place(MasterStartCoord, aiMaster);

            var player = new PlayerState(
                id: PlayerId.Player,
                resources: CreateStartingResources(),
                deck: new DeckState(request.PlayerDeckCardIds),
                hand: new HandState(),
                discard: new DiscardState(),
                master: playerMaster);

            var ai = new PlayerState(
                id: PlayerId.AI,
                resources: CreateStartingResources(),
                deck: new DeckState(request.AIDeckCardIds),
                hand: new HandState(),
                discard: new DiscardState(),
                master: aiMaster);

            // Current slice AI never performs a mulligan.
            ai.MarkMulliganUsed();

            var battleState = new BattleState(
                player: player,
                ai: ai,
                playerBoard: playerBoard,
                aiBoard: aiBoard);

            DrawOpeningHands(player, ai, request.FirstPlayerId);

            battleState.SetActivePlayer(request.FirstPlayerId);
            battleState.SetPhase(PhaseType.Mulligan);

            return battleState;
        }

        private static ResourceSet CreateStartingResources()
        {
            return new ResourceSet(
                mana: StartingMana,
                qi: StartingQi,
                power: StartingPower,
                gold: StartingGold);
        }

        private static void DrawOpeningHands(PlayerState player, PlayerState ai, PlayerId firstPlayerId)
        {
            var playerOpeningHand = firstPlayerId == PlayerId.Player
                ? FirstPlayerOpeningHand
                : SecondPlayerOpeningHand;

            var aiOpeningHand = firstPlayerId == PlayerId.AI
                ? FirstPlayerOpeningHand
                : SecondPlayerOpeningHand;

            DrawExactCards(player, playerOpeningHand);
            DrawExactCards(ai, aiOpeningHand);
        }

        private static void DrawExactCards(PlayerState playerState, int drawCount)
        {
            for (var i = 0; i < drawCount; i++)
            {
                if (!playerState.Deck.TryDraw(out var cardId))
                {
                    throw new InvalidOperationException(
                        "Deck does not contain enough cards to create the opening hand.");
                }

                playerState.Hand.Add(cardId);
            }
        }
    }
}
