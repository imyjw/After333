using System;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

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

        public static string FormatCommand(PlayerId actorId, IBattleCommand command,
            BattleState battleState = null, Func<string, string> cardNameResolver = null)
        {
            return command switch
            {
                PlayUnitCardCommand playUnitCardCommand => $"{actorId} played '{playUnitCardCommand.CardId}' to {playUnitCardCommand.TargetCoord}.",
                PlayBuildingCardCommand playBuildingCardCommand => $"{actorId} played '{playBuildingCardCommand.CardId}' to {playBuildingCardCommand.TargetCoord}.",
                CastDamageSpellCommand castDamageSpellCommand => $"{actorId} cast '{castDamageSpellCommand.CardId}' on {castDamageSpellCommand.TargetOwnerId} {castDamageSpellCommand.TargetCoord}.",
                CastScriptedSpellCommand scriptedSpellCommand => FormatScriptedSpell(actorId,
                    scriptedSpellCommand, battleState, cardNameResolver),
                CastPersistentResourceSpellCommand castPersistentResourceSpellCommand => $"{actorId} cast persistent spell '{castPersistentResourceSpellCommand.CardId}'.",
                MoveOccupantCommand moveOccupantCommand => $"{actorId} moved from {moveOccupantCommand.From} to {moveOccupantCommand.To}.",
                AttackCommand attackCommand => $"{actorId} attacked from {attackCommand.AttackerCoord} to {attackCommand.TargetCoord}.",
                ActivateEffectCommand active => FormatActiveEffect(actorId,
                    battleState?.GetBoard(actorId).GetOccupant(active.SourceCoord)?.CardId,
                    active.SourceCoord, cardNameResolver),
                InstallTrapCommand => $"{actorId} 트랩을 설치",
                EndTurnCommand => FormatEndTurn(actorId),
                _ => $"{actorId} executed {command.GetType().Name}."
            };
        }

        public static string FormatActiveEffect(PlayerId ownerId, string cardId,
            TileCoord? sourceCoord, Func<string, string> cardNameResolver = null)
        {
            var owner = ownerId == PlayerId.Player ? "플레이어의" : "상대방의";
            var name = string.IsNullOrWhiteSpace(cardId) ? "소환물" : ResolveCardName(cardId, cardNameResolver);
            var coord = sourceCoord.HasValue ? sourceCoord.Value.ToString() : string.Empty;
            var activation = $"{owner} {name}{coord} 액티브 발동";
            return cardId == PowerPlantRules.CardId
                ? $"{activation}: 골드 {PowerPlantRules.TriggerGoldCost} 소모, 전력 {PowerPlantRules.PowerGain} 획득"
                : activation;
        }

        private static string FormatScriptedSpell(PlayerId actorId, CastScriptedSpellCommand command,
            BattleState battleState, Func<string, string> cardNameResolver)
        {
            var actor = actorId == PlayerId.Player ? "플레이어가" : "상대방이";
            var sourceName = ResolveCardName(command.CardId, cardNameResolver);
            // A row/area spell's selected tile is not an individual recipient.
            if (command.HasTarget && battleState != null &&
                (command.CardId == PlasmaShieldRules.CardId || command.CardId == ManaShieldRules.CardId))
            {
                var board = battleState.GetBoard(command.TargetOwnerId);
                var target = board.IsInside(command.TargetCoord) ? board.GetOccupant(command.TargetCoord) : null;
                if (target != null)
                {
                    var owner = command.TargetOwnerId == PlayerId.Player ? "플레이어의" : "상대방의";
                    var targetName = ResolveCardName(target.CardId, cardNameResolver);
                    return $"{actor} {owner} {targetName}({command.TargetCoord.Column},{command.TargetCoord.Row})에게 " +
                           $"{sourceName} 마법카드를 사용했다.";
                }
            }

            return $"{actor} {sourceName} 마법카드를 사용했다.";
        }

        private static string ResolveCardName(string cardId, Func<string, string> resolver)
        {
            if (string.Equals(cardId, "Master", StringComparison.OrdinalIgnoreCase)) return "마스터";
            var name = resolver?.Invoke(cardId);
            return string.IsNullOrWhiteSpace(name) ? cardId : name;
        }

        public static string FormatSwap(TileCoord first, TileCoord second)
        {
            return $"Swapped occupants at {first} and {second}.";
        }
    }
}
