using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Services
{
    public sealed class RobotFactoryService
    {
        public const string CardId = "RobotFactory";

        private static readonly TileCoord[] TriggerOrder =
        {
            new TileCoord(0, 0),
            new TileCoord(0, 1),
            new TileCoord(1, 0),
            new TileCoord(1, 1),
            new TileCoord(2, 0),
            new TileCoord(2, 1),
            new TileCoord(3, 0),
            new TileCoord(3, 1),
            new TileCoord(4, 0),
            new TileCoord(4, 1),
        };

        private readonly List<UnitCardDefinition> _eligibleRobots;
        private readonly Random _random;

        public RobotFactoryService(ICardDefinitionProvider cardDefinitionProvider)
            : this(cardDefinitionProvider, new Random())
        {
        }

        public RobotFactoryService(ICardDefinitionProvider cardDefinitionProvider, Random random)
        {
            if (cardDefinitionProvider == null)
            {
                throw new ArgumentNullException(nameof(cardDefinitionProvider));
            }

            _random = random ?? throw new ArgumentNullException(nameof(random));
            _eligibleRobots = new List<UnitCardDefinition>();

            foreach (var definition in cardDefinitionProvider.GetAll())
            {
                if (definition is UnitCardDefinition unit &&
                    unit.HasRobot &&
                    unit.IncludeInDraft)
                {
                    _eligibleRobots.Add(unit);
                }
            }

            _eligibleRobots.Sort((left, right) =>
                string.Compare(left.CardId, right.CardId, StringComparison.Ordinal));
        }

        public void Resolve(BattleState battleState)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (_eligibleRobots.Count == 0)
            {
                return;
            }

            var owner = battleState.GetPlayer(battleState.ActivePlayerId);
            var board = battleState.GetBoard(battleState.ActivePlayerId);
            foreach (var coord in TriggerOrder)
            {
                if (!(board.GetOccupant(coord) is BuildingState factory) ||
                    !string.Equals(factory.CardId, CardId, StringComparison.Ordinal) ||
                    !factory.IsAlive ||
                    factory.EffectsSuppressed)
                {
                    continue;
                }

                var generatedCard = _eligibleRobots[_random.Next(_eligibleRobots.Count)];
                var addedToHand = owner.Hand.Count < owner.MaxHandSize;
                if (addedToHand)
                {
                    owner.Hand.Add(generatedCard.CardId);
                }

                battleState.RecordCardGeneration(new BattleCardGenerationEvent(
                    owner.Id,
                    factory.RuntimeId,
                    factory.CardId,
                    factory.Position,
                    generatedCard.CardId,
                    addedToHand));
            }
        }
    }
}
