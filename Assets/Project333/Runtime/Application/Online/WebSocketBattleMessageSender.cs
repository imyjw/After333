using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Project333.Runtime.Application.Online
{
    public sealed class WebSocketBattleMessageSender : IOnlineBattleMessageSender, IDisposable
    {
        private const int ReceiveBufferSize = 8192;
        private const string ClientVersionHeaderName = "X-Project333-Client-Version";
        private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(5);

        private readonly Uri _serverUri;
        private readonly string _clientVersion;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private ClientWebSocket _socket;
        private CancellationTokenSource _connectionCancellation;
        private Task _receiveTask;
        private int _connectionClosedSignaled;
        private int _intentionalDisconnect;

        public WebSocketBattleMessageSender(string serverUrl, string clientVersion = null)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                throw new ArgumentException("Server URL must not be empty.", nameof(serverUrl));
            }

            _serverUri = new Uri(serverUrl, UriKind.Absolute);
            _clientVersion = string.IsNullOrWhiteSpace(clientVersion) ? string.Empty : clientVersion.Trim();
        }

        public bool IsConnected => _socket != null && _socket.State == WebSocketState.Open;

        public event Action<OnlineBattleEnvelope> MessageReceived;

        public event Action<string> ErrorReceived;

        public event Action<string> ConnectionClosed;

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (IsConnected)
            {
                return;
            }

            DisposeSocket();

            Interlocked.Exchange(ref _connectionClosedSignaled, 0);
            Interlocked.Exchange(ref _intentionalDisconnect, 0);
            _connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _socket = new ClientWebSocket();
            _socket.Options.KeepAliveInterval = KeepAliveInterval;
            if (!string.IsNullOrWhiteSpace(_clientVersion))
            {
                _socket.Options.SetRequestHeader(ClientVersionHeaderName, _clientVersion);
            }

            await _socket.ConnectAsync(_serverUri, _connectionCancellation.Token);
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_connectionCancellation.Token));
        }

        public void Send(ClientBattleCommandMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (!IsConnected)
            {
                throw new InvalidOperationException("Cannot send an online battle message before the WebSocket is connected.");
            }

            var json = OnlineBattleMessageSerializer.SerializeClientCommand(message);
            _ = SendJsonAsync(json);
        }

        public void SendJoinMatch(string matchId, string playerToken)
        {
            SendJoinMatch(matchId, playerToken, null);
        }

        public void SendJoinMatch(
            string matchId,
            string playerToken,
            IReadOnlyList<string> playerDeckCardIds)
        {
            SendJoinMatch(
                matchId,
                playerToken,
                playerDeckCardIds,
                useServerAiOpponent: true,
                useMatchmakingQueue: false);
        }

        public void SendJoinMatch(
            string matchId,
            string playerToken,
            IReadOnlyList<string> playerDeckCardIds,
            bool useServerAiOpponent,
            bool useMatchmakingQueue)
        {
            SendJoinMatch(
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

        public void SendJoinMatch(
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
            if (!IsConnected)
            {
                throw new InvalidOperationException("Cannot join an online battle match before the WebSocket is connected.");
            }

            var json = OnlineBattleMessageSerializer.SerializeJoinMatch(
                matchId,
                playerToken,
                playerDeckCardIds,
                useServerAiOpponent,
                useMatchmakingQueue,
                runId,
                deckId,
                sessionToken,
                accountId,
                isReconnectAttempt,
                previousConnectionId);
            _ = SendJsonAsync(json);
        }

        public void SendKeepAlive(string matchId, string playerToken, string accountId)
        {
            if (!IsConnected)
            {
                return;
            }

            var json = OnlineBattleMessageSerializer.SerializeEnvelope(new OnlineBattleEnvelope
            {
                MessageType = OnlineBattleMessageType.KeepAlive,
                MatchId = matchId ?? string.Empty,
                PlayerToken = playerToken ?? string.Empty,
                AccountId = accountId ?? string.Empty
            });
            _ = SendJsonAsync(json);
        }

        public async Task DisconnectAsync()
        {
            if (_socket == null)
            {
                return;
            }

            try
            {
                Interlocked.Exchange(ref _intentionalDisconnect, 1);
                _connectionCancellation?.Cancel();

                if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                ErrorReceived?.Invoke(ex.Message);
            }
            finally
            {
                DisposeSocket();
            }
        }

        public void Dispose()
        {
            DisposeSocket();
        }

        private async Task SendJsonAsync(string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            await _sendLock.WaitAsync();
            try
            {
                if (!IsConnected)
                {
                    throw new InvalidOperationException("Cannot send an online battle message after the WebSocket disconnected.");
                }

                await _socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _connectionCancellation.Token);
            }
            catch (Exception ex)
            {
                ErrorReceived?.Invoke(ex.Message);
                SignalConnectionClosed(ex.Message);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[ReceiveBufferSize];

            while (!cancellationToken.IsCancellationRequested && _socket != null)
            {
                try
                {
                    using (var messageStream = new MemoryStream())
                    {
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                SignalConnectionClosed("The server closed the WebSocket connection.");
                                return;
                            }

                            messageStream.Write(buffer, 0, result.Count);
                        }
                        while (!result.EndOfMessage);

                        if (result.MessageType != WebSocketMessageType.Text)
                        {
                            continue;
                        }

                        var json = Encoding.UTF8.GetString(messageStream.ToArray());
                        var envelope = OnlineBattleMessageSerializer.DeserializeEnvelope(json);
                        if (envelope != null)
                        {
                            MessageReceived?.Invoke(envelope);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    ErrorReceived?.Invoke(ex.Message);
                    SignalConnectionClosed(ex.Message);
                    return;
                }
            }
        }

        private void SignalConnectionClosed(string reason)
        {
            if (Volatile.Read(ref _intentionalDisconnect) != 0)
            {
                return;
            }

            if (Interlocked.Exchange(ref _connectionClosedSignaled, 1) != 0)
            {
                return;
            }

            ConnectionClosed?.Invoke(string.IsNullOrWhiteSpace(reason)
                ? "The WebSocket connection was closed."
                : reason);
        }

        private void DisposeSocket()
        {
            _connectionCancellation?.Cancel();
            _connectionCancellation?.Dispose();
            _connectionCancellation = null;

            _socket?.Dispose();
            _socket = null;
            _receiveTask = null;
        }
    }
}
