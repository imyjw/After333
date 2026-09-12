using System.Net.WebSockets;
using System.Text;

namespace Project333.PvpServer.BattleSessions;

public static class BattleMessageReader
{
    // Limit the complete client message, including all continuation frames.
    // Server StateViews are outbound and are not subject to this input limit.
    public const int MaxMessageBytes = 64 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static async Task<string?> ReadTextAsync(BattleClientConnection connection, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();
        while (true)
        {
            var result = await connection.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                if (connection.Socket.State == WebSocketState.CloseReceived)
                    connection.MarkPeerCloseReceived();
                await connection.AcknowledgeCloseAsync(cancellationToken);
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Text)
                throw new BattleMessageException(WebSocketCloseStatus.InvalidMessageType, "Only text messages are supported.");
            if (stream.Length > MaxMessageBytes - result.Count)
                throw new BattleMessageException(WebSocketCloseStatus.MessageTooBig, "Client message exceeds 65536 bytes.");

            stream.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage) continue;
            try { return StrictUtf8.GetString(stream.GetBuffer(), 0, checked((int)stream.Length)); }
            catch (DecoderFallbackException)
            {
                throw new BattleMessageException(WebSocketCloseStatus.InvalidPayloadData, "Text message must contain valid UTF-8.");
            }
        }
    }
}

public sealed class BattleMessageException : Exception
{
    public BattleMessageException(WebSocketCloseStatus closeStatus, string message) : base(message) => CloseStatus = closeStatus;
    public WebSocketCloseStatus CloseStatus { get; }
}
