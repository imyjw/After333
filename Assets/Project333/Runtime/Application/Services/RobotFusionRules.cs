using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    public static class RobotFusionRules
    {
        public const string CardId = "RobotFusion";
        public const string EffectId = "robot_fusion";
        public const int MinimumRobotCount = 2;

        public static int CountLivingRobots(BoardState board)
        {
            if (board == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var occupant in board.EnumerateOccupants())
            {
                if (occupant is UnitState { HasActiveRobot: true, IsAlive: true })
                {
                    count += 1;
                }
            }

            return count;
        }

        public static bool CanBegin(BattleState battleState, PlayerId ownerId)
        {
            return battleState != null &&
                   CountLivingRobots(battleState.GetBoard(ownerId)) >= MinimumRobotCount;
        }

        public static RobotFusionResult Resolve(
            BattleState battleState,
            PlayerId ownerId,
            IReadOnlyList<TileCoord> selectedCoords)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (selectedCoords == null || selectedCoords.Count < MinimumRobotCount)
            {
                throw new InvalidOperationException("Robot Fusion requires at least two living Robot units.");
            }

            var board = battleState.GetBoard(ownerId);
            var seenCoords = new HashSet<TileCoord>();
            var selectedRobots = new List<UnitState>(selectedCoords.Count);
            for (var index = 0; index < selectedCoords.Count; index += 1)
            {
                var coord = selectedCoords[index];
                if (!seenCoords.Add(coord))
                {
                    throw new InvalidOperationException("Robot Fusion cannot select the same Robot more than once.");
                }

                if (board.GetOccupant(coord) is not UnitState { HasActiveRobot: true, IsAlive: true } robot)
                {
                    throw new InvalidOperationException(
                        $"Robot Fusion target {coord} must be a living Robot unit on the caster's field.");
                }

                selectedRobots.Add(robot);
            }

            var survivor = selectedRobots[0];
            for (var index = 1; index < selectedRobots.Count; index += 1)
            {
                if (selectedRobots[index].SciencePowerUpkeep > survivor.SciencePowerUpkeep)
                {
                    survivor = selectedRobots[index];
                }
            }

            var absorbedRuntimeIds = new List<string>(selectedRobots.Count - 1);
            var attackBonus = 0;
            var hpBonus = 0;
            for (var index = 0; index < selectedRobots.Count; index += 1)
            {
                var robot = selectedRobots[index];
                if (ReferenceEquals(robot, survivor))
                {
                    continue;
                }

                attackBonus += Math.Max(0, robot.Attack);
                hpBonus += Math.Max(0, robot.CurrentHp);
                absorbedRuntimeIds.Add(robot.RuntimeId);
            }

            survivor.IncreaseBaseAttack(attackBonus);
            survivor.IncreaseMaxHpAndCurrentHp(hpBonus);

            for (var index = 0; index < selectedRobots.Count; index += 1)
            {
                var robot = selectedRobots[index];
                if (!ReferenceEquals(robot, survivor))
                {
                    board.Remove(robot.Position);
                }
            }

            return new RobotFusionResult(
                survivor.RuntimeId,
                survivor.CardId,
                survivor.Position,
                attackBonus,
                hpBonus,
                absorbedRuntimeIds);
        }
    }

    public sealed class RobotFusionResult
    {
        public RobotFusionResult(
            string survivorRuntimeId,
            string survivorCardId,
            TileCoord survivorCoord,
            int attackBonus,
            int hpBonus,
            IReadOnlyList<string> absorbedRuntimeIds)
        {
            SurvivorRuntimeId = survivorRuntimeId ?? string.Empty;
            SurvivorCardId = survivorCardId ?? string.Empty;
            SurvivorCoord = survivorCoord;
            AttackBonus = attackBonus;
            HpBonus = hpBonus;
            AbsorbedRuntimeIds = absorbedRuntimeIds ?? Array.Empty<string>();
        }

        public string SurvivorRuntimeId { get; }
        public string SurvivorCardId { get; }
        public TileCoord SurvivorCoord { get; }
        public int AttackBonus { get; }
        public int HpBonus { get; }
        public IReadOnlyList<string> AbsorbedRuntimeIds { get; }
    }
}
