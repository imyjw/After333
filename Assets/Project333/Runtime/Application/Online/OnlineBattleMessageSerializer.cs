using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Project333.Runtime.Application.Online
{
    public static class OnlineBattleMessageSerializer
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None,
            Converters = { new StringEnumConverter() }
        };

        public static string SerializeClientCommand(ClientBattleCommandMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return SerializeEnvelope(new OnlineBattleEnvelope
            {
                MessageType = OnlineBattleMessageType.ClientCommand,
                MatchId = message.MatchId,
                ClientCommand = message
            });
        }

        public static string SerializeJoinMatch(string matchId, string playerToken)
        {
            return SerializeJoinMatch(matchId, playerToken, null);
        }

        public static string SerializeJoinMatch(
            string matchId,
            string playerToken,
            IReadOnlyList<string> playerDeckCardIds)
        {
            return SerializeJoinMatch(
                matchId,
                playerToken,
                playerDeckCardIds,
                useServerAiOpponent: true,
                useMatchmakingQueue: false);
        }

        public static string SerializeJoinMatch(
            string matchId,
            string playerToken,
            IReadOnlyList<string> playerDeckCardIds,
            bool useServerAiOpponent,
            bool useMatchmakingQueue)
        {
            return SerializeJoinMatch(
                matchId,
                playerToken,
                playerDeckCardIds,
                useServerAiOpponent,
                useMatchmakingQueue,
                runId: null,
                deckId: null,
                sessionToken: null,
                accountId: null);
        }

        public static string SerializeJoinMatch(
            string matchId,
            string playerToken,
            IReadOnlyList<string> playerDeckCardIds,
            bool useServerAiOpponent,
            bool useMatchmakingQueue,
            string runId,
            string deckId,
            string sessionToken = null,
            string accountId = null,
            bool isReconnectAttempt = false,
            string previousConnectionId = null)
        {
            var envelope = new OnlineBattleEnvelope
            {
                MessageType = OnlineBattleMessageType.JoinMatch,
                MatchId = matchId ?? string.Empty,
                PlayerToken = playerToken ?? string.Empty,
                SessionToken = sessionToken ?? string.Empty,
                AccountId = accountId ?? string.Empty,
                RunId = runId ?? string.Empty,
                DeckId = deckId ?? string.Empty,
                UseServerAiOpponent = useServerAiOpponent,
                UseMatchmakingQueue = useMatchmakingQueue,
                IsReconnectAttempt = isReconnectAttempt,
                PreviousConnectionId = previousConnectionId ?? string.Empty
            };

            if (playerDeckCardIds != null)
            {
                for (var i = 0; i < playerDeckCardIds.Count; i++)
                {
                    var cardId = playerDeckCardIds[i];
                    if (!string.IsNullOrWhiteSpace(cardId))
                    {
                        envelope.PlayerDeckCardIds.Add(cardId);
                    }
                }
            }

            return SerializeEnvelope(envelope);
        }

        public static string SerializeEnvelope(OnlineBattleEnvelope envelope)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            return JsonConvert.SerializeObject(envelope, Settings);
        }

        public static OnlineBattleEnvelope DeserializeEnvelope(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("JSON payload must not be empty.", nameof(json));
            }

            return JsonConvert.DeserializeObject<OnlineBattleEnvelope>(json, Settings);
        }
    }
}
