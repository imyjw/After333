using System;
using System.Collections.Generic;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Application.Gateways
{
    public sealed class OnlineBattleGateway : IBattleGateway
    {
        private readonly IOnlineBattleMessageSender _messageSender;
        private long _nextSequence = 1;

        public OnlineBattleGateway(
            string matchId,
            string playerToken,
            PlayerId localPlayerId,
            IOnlineBattleMessageSender messageSender,
            string sessionToken = "",
            string accountId = "")
        {
            MatchId = matchId ?? string.Empty;
            PlayerToken = playerToken ?? string.Empty;
            SessionToken = sessionToken ?? string.Empty;
            AccountId = accountId ?? string.Empty;
            LocalPlayerId = localPlayerId;
            _messageSender = messageSender ?? throw new ArgumentNullException(nameof(messageSender));
        }

        public string MatchId { get; private set; }

        public string PlayerToken { get; }

        public string SessionToken { get; }

        public string AccountId { get; }

        public PlayerId LocalPlayerId { get; private set; }

        public BattleStateViewDto CurrentView { get; private set; }

        public BattleState CurrentBattleState => null;

        public BattleState StartBattle(BattleSetupRequest request)
        {
            throw new InvalidOperationException("Online battles are started by the server after matchmaking or room entry.");
        }

        public void ApplyServerView(BattleStateViewDto view)
        {
            CurrentView = view;
        }

        public void SetLocalPlayerId(PlayerId localPlayerId)
        {
            LocalPlayerId = localPlayerId;
        }

        public void SetMatchId(string matchId)
        {
            if (!string.IsNullOrWhiteSpace(matchId))
            {
                MatchId = matchId;
            }
        }

        public void ApplyMulligan(PlayerId playerId, IEnumerable<string> selectedCardIds, IDeckShuffler deckShuffler)
        {
            var message = CreateBaseMessage(ToServerOwnerId(playerId), OnlineBattleCommandType.ApplyMulligan);
            if (selectedCardIds != null)
            {
                message.SelectedCardIds.AddRange(selectedCardIds);
            }

            Send(message);
        }

        public void PassMulligan(PlayerId playerId)
        {
            Send(CreateBaseMessage(ToServerOwnerId(playerId), OnlineBattleCommandType.PassMulligan));
        }

        public void ResolveTurnStart()
        {
            throw new InvalidOperationException("Online turn-start resolution is server-authoritative.");
        }

        public void ExecuteCommand(PlayerId actorId, IBattleCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            Send(CreateCommandMessage(actorId, command));
        }

        public void EndTurn()
        {
            Send(CreateBaseMessage(LocalPlayerId, OnlineBattleCommandType.EndTurn));
        }

        public void SendSurrender()
        {
            Send(CreateBaseMessage(LocalPlayerId, OnlineBattleCommandType.Surrender));
        }

        private ClientBattleCommandMessage CreateCommandMessage(PlayerId actorId, IBattleCommand command)
        {
            var serverActorId = ToServerOwnerId(actorId);

            switch (command)
            {
                case PlayUnitCardCommand playUnitCardCommand:
                    return CreateCardTargetMessage(
                        serverActorId,
                        OnlineBattleCommandType.PlayUnitCard,
                        playUnitCardCommand.CardId,
                        playUnitCardCommand.HandCardRuntimeId,
                        default,
                        playUnitCardCommand.TargetCoord,
                        false);

                case PlayBuildingCardCommand playBuildingCardCommand:
                    return CreateCardTargetMessage(
                        serverActorId,
                        OnlineBattleCommandType.PlayBuildingCard,
                        playBuildingCardCommand.CardId,
                        playBuildingCardCommand.HandCardRuntimeId,
                        default,
                        playBuildingCardCommand.TargetCoord,
                        false);

                case CastDamageSpellCommand castDamageSpellCommand:
                    return CreateCardTargetMessage(
                        serverActorId,
                        OnlineBattleCommandType.CastDamageSpell,
                        castDamageSpellCommand.CardId,
                        castDamageSpellCommand.HandCardRuntimeId,
                        ToServerOwnerId(castDamageSpellCommand.TargetOwnerId),
                        castDamageSpellCommand.TargetCoord,
                        true);

                case CastPersistentResourceSpellCommand castPersistentResourceSpellCommand:
                    return CreateCardMessage(
                        serverActorId,
                        OnlineBattleCommandType.CastPersistentResourceSpell,
                        castPersistentResourceSpellCommand.CardId,
                        castPersistentResourceSpellCommand.HandCardRuntimeId);

                case CastScriptedSpellCommand castScriptedSpellCommand:
                    if (castScriptedSpellCommand.HasMultipleTargets)
                    {
                        var multiTargetMessage = CreateCardMessage(
                            serverActorId,
                            OnlineBattleCommandType.CastScriptedSpell,
                            castScriptedSpellCommand.CardId,
                            castScriptedSpellCommand.HandCardRuntimeId);
                        foreach (var targetCoord in castScriptedSpellCommand.TargetCoords)
                        {
                            multiTargetMessage.SelectedTargetCoords.Add(TileCoordDto.FromDomain(targetCoord));
                        }

                        return multiTargetMessage;
                    }

                    if (!castScriptedSpellCommand.HasTarget)
                    {
                        return CreateCardMessage(
                            serverActorId,
                            OnlineBattleCommandType.CastScriptedSpell,
                            castScriptedSpellCommand.CardId,
                            castScriptedSpellCommand.HandCardRuntimeId);
                    }

                    return CreateCardTargetMessage(
                        serverActorId,
                        OnlineBattleCommandType.CastScriptedSpell,
                        castScriptedSpellCommand.CardId,
                        castScriptedSpellCommand.HandCardRuntimeId,
                        ToServerOwnerId(castScriptedSpellCommand.TargetOwnerId),
                        castScriptedSpellCommand.TargetCoord,
                        true);

                case MoveOccupantCommand moveOccupantCommand:
                    var moveMessage = CreateBaseMessage(serverActorId, OnlineBattleCommandType.MoveOccupant);
                    moveMessage.SourceCoord = TileCoordDto.FromDomain(moveOccupantCommand.From);
                    moveMessage.DestinationCoord = TileCoordDto.FromDomain(moveOccupantCommand.To);
                    return moveMessage;

                case AttackCommand attackCommand:
                    var attackMessage = CreateBaseMessage(serverActorId, OnlineBattleCommandType.Attack);
                    attackMessage.SourceCoord = TileCoordDto.FromDomain(attackCommand.AttackerCoord);
                    attackMessage.TargetCoord = TileCoordDto.FromDomain(attackCommand.TargetCoord);
                    attackMessage.HasTarget = true;
                    return attackMessage;

                case EndTurnCommand:
                    return CreateBaseMessage(serverActorId, OnlineBattleCommandType.EndTurn);

                default:
                    throw new InvalidOperationException($"Unsupported online command type '{command.GetType().Name}'.");
            }
        }

        private ClientBattleCommandMessage CreateCardMessage(
            PlayerId actorId,
            OnlineBattleCommandType commandType,
            string cardId,
            string handCardRuntimeId)
        {
            var message = CreateBaseMessage(actorId, commandType);
            message.CardId = cardId ?? string.Empty;
            message.HandCardRuntimeId = handCardRuntimeId ?? string.Empty;
            return message;
        }

        private ClientBattleCommandMessage CreateCardTargetMessage(
            PlayerId actorId,
            OnlineBattleCommandType commandType,
            string cardId,
            string handCardRuntimeId,
            PlayerId targetOwnerId,
            TileCoord targetCoord,
            bool hasTarget)
        {
            var message = CreateCardMessage(actorId, commandType, cardId, handCardRuntimeId);
            message.TargetOwnerId = targetOwnerId;
            message.TargetCoord = TileCoordDto.FromDomain(targetCoord);
            message.HasTarget = hasTarget;
            return message;
        }

        private ClientBattleCommandMessage CreateBaseMessage(PlayerId actorId, OnlineBattleCommandType commandType)
        {
            return new ClientBattleCommandMessage
            {
                MatchId = MatchId,
                PlayerToken = PlayerToken,
                SessionToken = SessionToken,
                AccountId = AccountId,
                Sequence = _nextSequence++,
                ActorId = actorId,
                CommandType = commandType
            };
        }

        private void Send(ClientBattleCommandMessage message)
        {
            _messageSender.Send(message);
        }

        private PlayerId ToServerOwnerId(PlayerId localPerspectiveOwnerId)
        {
            return localPerspectiveOwnerId == PlayerId.Player
                ? LocalPlayerId
                : GetOpponentId(LocalPlayerId);
        }

        private static PlayerId GetOpponentId(PlayerId playerId)
        {
            return playerId == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
        }
    }
}
