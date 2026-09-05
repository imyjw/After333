using Project333.Runtime.Application.Commands;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Application.Services
{
    public static class CombatLogFormatter
    {
        public static string FormatBattleStarted(PlayerId firstPlayerId)
        {
            return $"Battle started. {firstPlayerId} acts first.";
        }

        public static string FormatMulliganPassed(PlayerId playerId)
        {
            return $"{playerId} passed mulligan.";
        }

        public static string FormatTurnStartResolved(PlayerId playerId, int turnNumber)
        {
            return $"{playerId} resolved turn start for turn {turnNumber}.";
        }

        public static string FormatEndTurn(PlayerId playerId)
        {
            return $"{playerId} ended their turn.";
        }

        public static string FormatCommand(PlayerId actorId, IBattleCommand command)
        {
            return command switch
            {
                PlayUnitCardCommand playUnitCardCommand => $"{actorId} played '{playUnitCardCommand.CardId}' to {playUnitCardCommand.TargetCoord}.",
                PlayBuildingCardCommand playBuildingCardCommand => $"{actorId} played '{playBuildingCardCommand.CardId}' to {playBuildingCardCommand.TargetCoord}.",
                CastDamageSpellCommand castDamageSpellCommand => $"{actorId} cast '{castDamageSpellCommand.CardId}' on {castDamageSpellCommand.TargetOwnerId} {castDamageSpellCommand.TargetCoord}.",
                CastPersistentResourceSpellCommand castPersistentResourceSpellCommand => $"{actorId} cast persistent spell '{castPersistentResourceSpellCommand.CardId}'.",
                MoveOccupantCommand moveOccupantCommand => $"{actorId} moved from {moveOccupantCommand.From} to {moveOccupantCommand.To}.",
                AttackCommand attackCommand => $"{actorId} attacked from {attackCommand.AttackerCoord} to {attackCommand.TargetCoord}.",
                EndTurnCommand => FormatEndTurn(actorId),
                _ => $"{actorId} executed {command.GetType().Name}."
            };
        }

        public static string FormatSwap(TileCoord first, TileCoord second)
        {
            return $"Swapped occupants at {first} and {second}.";
        }
    }
}
