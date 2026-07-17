using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;

namespace Project333.Tests.EditMode
{
    public sealed class OnlineBattleGatewayTests
    {
        [Test]
        public void CreateForPlayer_HidesOpponentHandCardIds()
        {
            var battleState = new BattleSetupService().CreateInitialState(CreateRequest());
            var factory = new BattleStateViewFactory();

            var playerView = factory.CreateForPlayer(battleState, "match-1", PlayerId.Player);

            Assert.That(playerView.Player.VisibleHandCardIds.Count, Is.EqualTo(battleState.Player.Hand.Count));
            Assert.That(playerView.Player.VisibleHandCards.Count, Is.EqualTo(battleState.Player.Hand.Count));
            Assert.That(playerView.Opponent.HandCount, Is.EqualTo(battleState.AI.Hand.Count));
            Assert.That(playerView.Opponent.VisibleHandCardIds, Is.Empty);
            Assert.That(playerView.Opponent.VisibleHandCards, Is.Empty);
            Assert.That(playerView.Occupants.Count, Is.EqualTo(2));
        }

        [Test]
        public void CreateAndProjectStateView_PreservesPendingRobotFusionForLocalViewer()
        {
            var battleState = new BattleSetupService().CreateInitialState(CreateRequest());
            battleState.BeginRobotFusion(PlayerId.AI, RobotFusionRules.CardId);
            var factory = new BattleStateViewFactory();

            var aiView = factory.CreateForPlayer(battleState, "match-1", PlayerId.AI);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(aiView);

            Assert.That(aiView.HasPendingRobotFusion, Is.True);
            Assert.That(aiView.PendingRobotFusionOwnerId, Is.EqualTo(PlayerId.AI));
            Assert.That(projected.PendingRobotFusion, Is.Not.Null);
            Assert.That(projected.PendingRobotFusion.OwnerId, Is.EqualTo(PlayerId.Player));
            Assert.That(projected.PendingRobotFusion.CardId, Is.EqualTo(RobotFusionRules.CardId));
        }

        [Test]
        public void ExecuteCommand_Attack_SendsOnlineCommandMessage()
        {
            var sender = new RecordingSender();
            var gateway = new OnlineBattleGateway("match-1", "token-a", PlayerId.Player, sender);

            gateway.ExecuteCommand(PlayerId.Player, new AttackCommand(new TileCoord(2, 1), new TileCoord(2, 0)));

            Assert.That(sender.Messages.Count, Is.EqualTo(1));
            var message = sender.Messages[0];
            Assert.That(message.MatchId, Is.EqualTo("match-1"));
            Assert.That(message.PlayerToken, Is.EqualTo("token-a"));
            Assert.That(message.Sequence, Is.EqualTo(1));
            Assert.That(message.ActorId, Is.EqualTo(PlayerId.Player));
            Assert.That(message.CommandType, Is.EqualTo(OnlineBattleCommandType.Attack));
            Assert.That(message.SourceCoord.Column, Is.EqualTo(2));
            Assert.That(message.SourceCoord.Row, Is.EqualTo(1));
            Assert.That(message.TargetCoord.Column, Is.EqualTo(2));
            Assert.That(message.TargetCoord.Row, Is.EqualTo(0));
            Assert.That(message.HasTarget, Is.True);
        }

        [Test]
        public void ExecuteCommand_PlayCard_PreservesSelectedHandRuntimeId()
        {
            var sender = new RecordingSender();
            var gateway = new OnlineBattleGateway("match-1", "token-a", PlayerId.Player, sender);

            gateway.ExecuteCommand(
                PlayerId.Player,
                new PlayUnitCardCommand(
                    "replicate-unit",
                    new TileCoord(0, 0),
                    "hand-runtime-123"));

            Assert.That(sender.Messages, Has.Count.EqualTo(1));
            Assert.That(sender.Messages[0].CardId, Is.EqualTo("replicate-unit"));
            Assert.That(sender.Messages[0].HandCardRuntimeId, Is.EqualTo("hand-runtime-123"));
        }

        [Test]
        public void SerializeClientCommand_WrapsMessageInEnvelope()
        {
            var message = new ClientBattleCommandMessage
            {
                MatchId = "match-1",
                PlayerToken = "token-a",
                Sequence = 3,
                ActorId = PlayerId.Player,
                CommandType = OnlineBattleCommandType.EndTurn
            };

            var json = OnlineBattleMessageSerializer.SerializeClientCommand(message);
            var envelope = OnlineBattleMessageSerializer.DeserializeEnvelope(json);

            Assert.That(envelope.MessageType, Is.EqualTo(OnlineBattleMessageType.ClientCommand));
            Assert.That(envelope.MatchId, Is.EqualTo("match-1"));
            Assert.That(envelope.ClientCommand, Is.Not.Null);
            Assert.That(envelope.ClientCommand.CommandType, Is.EqualTo(OnlineBattleCommandType.EndTurn));
            Assert.That(envelope.ClientCommand.Sequence, Is.EqualTo(3));
        }

        [Test]
        public void SetLocalPlayerId_ChangesEndTurnActor()
        {
            var sender = new RecordingSender();
            var gateway = new OnlineBattleGateway("match-1", "token-b", PlayerId.Player, sender);

            gateway.SetLocalPlayerId(PlayerId.AI);
            gateway.EndTurn();

            Assert.That(sender.Messages.Count, Is.EqualTo(1));
            Assert.That(sender.Messages[0].ActorId, Is.EqualTo(PlayerId.AI));
        }

        [Test]
        public void SendSurrender_SendsSurrenderForLocalSeat()
        {
            var sender = new RecordingSender();
            var gateway = new OnlineBattleGateway("match-1", "token-b", PlayerId.AI, sender);

            gateway.SendSurrender();

            Assert.That(sender.Messages.Count, Is.EqualTo(1));
            Assert.That(sender.Messages[0].ActorId, Is.EqualTo(PlayerId.AI));
            Assert.That(sender.Messages[0].CommandType, Is.EqualTo(OnlineBattleCommandType.Surrender));
        }

        [Test]
        public void ExecuteCommand_WhenLocalSeatIsAi_MapsLocalPlayerActorToServerSeat()
        {
            var sender = new RecordingSender();
            var gateway = new OnlineBattleGateway("match-1", "token-b", PlayerId.AI, sender);

            gateway.ExecuteCommand(PlayerId.Player, new PlayUnitCardCommand("Goblin", new TileCoord(0, 0)));

            Assert.That(sender.Messages.Count, Is.EqualTo(1));
            Assert.That(sender.Messages[0].ActorId, Is.EqualTo(PlayerId.AI));
            Assert.That(sender.Messages[0].CommandType, Is.EqualTo(OnlineBattleCommandType.PlayUnitCard));
        }

        [Test]
        public void ExecuteCommand_WhenLocalSeatIsAi_MapsLocalOpponentSpellTargetToServerOpponent()
        {
            var sender = new RecordingSender();
            var gateway = new OnlineBattleGateway("match-1", "token-b", PlayerId.AI, sender);

            gateway.ExecuteCommand(
                PlayerId.Player,
                new CastDamageSpellCommand("firebolt", PlayerId.AI, new TileCoord(2, 1)));

            Assert.That(sender.Messages.Count, Is.EqualTo(1));
            var message = sender.Messages[0];
            Assert.That(message.ActorId, Is.EqualTo(PlayerId.AI));
            Assert.That(message.TargetOwnerId, Is.EqualTo(PlayerId.Player));
            Assert.That(message.TargetCoord.Column, Is.EqualTo(2));
            Assert.That(message.TargetCoord.Row, Is.EqualTo(1));
        }

        [Test]
        public void ExecuteCommand_WhenLocalSeatIsAi_MapsFriendlySpellTargetToServerOwnSeat()
        {
            var sender = new RecordingSender();
            var gateway = new OnlineBattleGateway("match-1", "token-b", PlayerId.AI, sender);

            gateway.ExecuteCommand(
                PlayerId.Player,
                new CastDamageSpellCommand("firebolt", PlayerId.Player, new TileCoord(0, 0)));

            Assert.That(sender.Messages.Count, Is.EqualTo(1));
            var message = sender.Messages[0];
            Assert.That(message.ActorId, Is.EqualTo(PlayerId.AI));
            Assert.That(message.TargetOwnerId, Is.EqualTo(PlayerId.AI));
            Assert.That(message.TargetCoord.Column, Is.Zero);
            Assert.That(message.TargetCoord.Row, Is.Zero);
        }

        [Test]
        public void ExecuteCommand_CastFireboltEnemyMaster_SendsDamageSpellCommand()
        {
            var sender = new RecordingSender();
            var gateway = new OnlineBattleGateway("match-1", "token-a", PlayerId.Player, sender);

            gateway.ExecuteCommand(
                PlayerId.Player,
                new CastDamageSpellCommand("firebolt", PlayerId.AI, new TileCoord(2, 1)));

            Assert.That(sender.Messages.Count, Is.EqualTo(1));
            var message = sender.Messages[0];
            Assert.That(message.MatchId, Is.EqualTo("match-1"));
            Assert.That(message.PlayerToken, Is.EqualTo("token-a"));
            Assert.That(message.ActorId, Is.EqualTo(PlayerId.Player));
            Assert.That(message.CommandType, Is.EqualTo(OnlineBattleCommandType.CastDamageSpell));
            Assert.That(message.CardId, Is.EqualTo("firebolt"));
            Assert.That(message.TargetOwnerId, Is.EqualTo(PlayerId.AI));
            Assert.That(message.TargetCoord.Column, Is.EqualTo(2));
            Assert.That(message.TargetCoord.Row, Is.EqualTo(1));
            Assert.That(message.HasTarget, Is.True);
        }

        [Test]
        public void ExecuteCommand_RobotFusion_PreservesOrderedTargetCoordinates()
        {
            var sender = new RecordingSender();
            var gateway = new OnlineBattleGateway("match-1", "token-a", PlayerId.Player, sender);

            gateway.ExecuteCommand(
                PlayerId.Player,
                new CastScriptedSpellCommand(
                    RobotFusionRules.CardId,
                    new[] { new TileCoord(4, 1), new TileCoord(0, 0), new TileCoord(2, 0) }));

            Assert.That(sender.Messages.Count, Is.EqualTo(1));
            var message = sender.Messages[0];
            Assert.That(message.CommandType, Is.EqualTo(OnlineBattleCommandType.CastScriptedSpell));
            Assert.That(message.CardId, Is.EqualTo(RobotFusionRules.CardId));
            Assert.That(message.HasTarget, Is.False);
            Assert.That(message.SelectedTargetCoords.Count, Is.EqualTo(3));
            Assert.That(message.SelectedTargetCoords[0].Column, Is.EqualTo(4));
            Assert.That(message.SelectedTargetCoords[0].Row, Is.EqualTo(1));
            Assert.That(message.SelectedTargetCoords[1].Column, Is.Zero);
            Assert.That(message.SelectedTargetCoords[1].Row, Is.Zero);
            Assert.That(message.SelectedTargetCoords[2].Column, Is.EqualTo(2));
            Assert.That(message.SelectedTargetCoords[2].Row, Is.Zero);
        }

        [Test]
        public void SerializeJoinMatch_WrapsMessageInEnvelope()
        {
            var json = OnlineBattleMessageSerializer.SerializeJoinMatch("match-1", "token-a");
            var envelope = OnlineBattleMessageSerializer.DeserializeEnvelope(json);

            Assert.That(envelope.MessageType, Is.EqualTo(OnlineBattleMessageType.JoinMatch));
            Assert.That(envelope.MatchId, Is.EqualTo("match-1"));
            Assert.That(envelope.PlayerToken, Is.EqualTo("token-a"));
        }

        [Test]
        public void SerializeJoinMatch_WhenUsingMatchmakingQueue_SendsPvpJoinRequestWithDeck()
        {
            var deck = new[] { "Goblin", "firebolt" };

            var json = OnlineBattleMessageSerializer.SerializeJoinMatch(
                "pvp-matchmaking",
                "token-a",
                deck,
                useServerAiOpponent: false,
                useMatchmakingQueue: true);
            var envelope = OnlineBattleMessageSerializer.DeserializeEnvelope(json);

            Assert.That(envelope.MessageType, Is.EqualTo(OnlineBattleMessageType.JoinMatch));
            Assert.That(envelope.UseServerAiOpponent, Is.False);
            Assert.That(envelope.UseMatchmakingQueue, Is.True);
            Assert.That(envelope.PlayerDeckCardIds, Is.EqualTo(deck));
        }

        [Test]
        public void SerializeJoinMatch_WhenAutomaticallyReconnecting_IncludesPreviousConnection()
        {
            var json = OnlineBattleMessageSerializer.SerializeJoinMatch(
                "pvp-matchmaking",
                "token-a",
                playerDeckCardIds: null,
                useServerAiOpponent: false,
                useMatchmakingQueue: true,
                runId: "run-1",
                deckId: "deck-1",
                sessionToken: "session-a",
                accountId: "account-a",
                isReconnectAttempt: true,
                previousConnectionId: "connection-old");
            var envelope = OnlineBattleMessageSerializer.DeserializeEnvelope(json);

            Assert.That(envelope.IsReconnectAttempt, Is.True);
            Assert.That(envelope.PreviousConnectionId, Is.EqualTo("connection-old"));
        }

        private static BattleSetupRequest CreateRequest()
        {
            return new BattleSetupRequest(
                playerDeckCardIds: CreateDeck("P"),
                aiDeckCardIds: CreateDeck("A"),
                firstPlayerId: PlayerId.Player);
        }

        private static IReadOnlyList<string> CreateDeck(string prefix)
        {
            return new List<string>
            {
                prefix + "-00",
                prefix + "-01",
                prefix + "-02",
                prefix + "-03",
                prefix + "-04",
                prefix + "-05",
                prefix + "-06",
                prefix + "-07",
                prefix + "-08",
                prefix + "-09",
            };
        }

        private sealed class RecordingSender : IOnlineBattleMessageSender
        {
            public List<ClientBattleCommandMessage> Messages { get; } = new List<ClientBattleCommandMessage>();

            public void Send(ClientBattleCommandMessage message)
            {
                Messages.Add(message);
            }
        }
    }
}
