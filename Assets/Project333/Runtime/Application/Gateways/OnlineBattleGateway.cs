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
        private int? _endedTurnNumber;
        private long? _pendingEndTurnSequence;
        public bool IsAwaitingNextTurn => _endedTurnNumber.HasValue;

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
            if (view != null && view.MatchId == MatchId &&
                (view.IsEnded || (_endedTurnNumber.HasValue && view.TurnNumber > _endedTurnNumber.Value &&
                 view.ActivePlayerId == LocalPlayerId && view.Phase == PhaseType.Main)))
                ClearEndTurnLock();
        }

        public void SetLocalPlayerId(PlayerId localPlayerId)
        {
            LocalPlayerId = localPlayerId;
        }

        public void SetMatchId(string matchId)
        {
            if (!string.IsNullOrWhiteSpace(matchId))
            {
                if (MatchId != matchId) { ClearEndTurnLock(); CurrentView = null; }
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

                case InstallTrapCommand installTrapCommand:
                    return CreateCardMessage(serverActorId, OnlineBattleCommandType.InstallTrap,
                        installTrapCommand.CardId, installTrapCommand.HandCardRuntimeId);

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

                case ActivateEffectCommand active:
                    var activeMessage = CreateBaseMessage(serverActorId, OnlineBattleCommandType.ActivateEffect);
                    activeMessage.SourceCoord = TileCoordDto.FromDomain(active.SourceCoord);
                    activeMessage.OccupantRuntimeId = active.OccupantRuntimeId;
                    return activeMessage;

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
                ExpectedTurnNumber = CurrentView?.TurnNumber,
                CommandType = commandType
            };
        }

        private void Send(ClientBattleCommandMessage message)
        {
            var isGameplay = message.CommandType != OnlineBattleCommandType.Surrender &&
                message.CommandType != OnlineBattleCommandType.ApplyMulligan &&
                message.CommandType != OnlineBattleCommandType.PassMulligan;
            if (isGameplay && IsAwaitingNextTurn)
                throw new InvalidOperationException("턴 종료를 요청했습니다. 다음 내 턴을 기다려 주세요.");
            if (isGameplay && CurrentView != null &&
                (CurrentView.IsEnded || CurrentView.ActivePlayerId != LocalPlayerId || CurrentView.Phase != PhaseType.Main))
                throw new InvalidOperationException("내 행동 턴이 아닙니다.");
            var endTurn = message.CommandType == OnlineBattleCommandType.EndTurn;
            if (endTurn)
            {
                if (CurrentView == null || CurrentView.HasPendingRobotFusion)
                    throw new InvalidOperationException("현재 전투 상태를 확인하거나 로봇 합체 선택을 완료해 주세요.");
                _endedTurnNumber = CurrentView.TurnNumber;
                _pendingEndTurnSequence = message.Sequence;
            }
            try { _messageSender.Send(message); }
            catch
            {
                // Synchronous enqueue failure can retry. Asynchronous failures recover through
                // reconnect and a fresh authoritative view, never a timed EndTurn retry.
                if (endTurn) RejectCommand(message.Sequence);
                throw;
            }
        }

        public void RejectCommand(long? sequence)
        {
            // An unrelated earlier error must not unlock a submitted EndTurn.
            if (sequence.HasValue && sequence == _pendingEndTurnSequence) ClearEndTurnLock();
        }

        private void ClearEndTurnLock()
        {
            _endedTurnNumber = null;
            _pendingEndTurnSequence = null;
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
