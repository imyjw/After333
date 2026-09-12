using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Messages;

static class BattleInputChecks
{
    public static async Task Run(Action<string> pass, CancellationToken ct)
    {
        var korean = Encoding.UTF8.GetBytes("{\"text\":\"한글 재접속 검증\"}");
        var socket = new InputSocket(korean, fragmentSize: 1);
        Require(await Read(socket) == Encoding.UTF8.GetString(korean), "UTF-8 characters split across fragments must survive.");
        var maximum = new byte[BattleMessageReader.MaxMessageBytes]; Array.Fill(maximum, (byte)' ');
        Require((await Read(new InputSocket(maximum, 997)))!.Length == maximum.Length, "Exact byte limit must be accepted across arbitrary fragments.");
        var normalJoin = JsonSerializer.Serialize(new OnlineBattleEnvelope
        {
            MatchId = "pvp-check", SessionToken = new string('t', 512), AccountId = Guid.NewGuid().ToString(),
            RunId = Guid.NewGuid().ToString(), DeckId = Guid.NewGuid().ToString(),
            PlayerDeckCardIds = Enumerable.Repeat("NuclearPowerPlant", 33).ToList(),
        });
        Require(Encoding.UTF8.GetByteCount(normalJoin) < BattleMessageReader.MaxMessageBytes &&
            await Read(new InputSocket(Encoding.UTF8.GetBytes(normalJoin), 100)) == normalJoin, "Normal 33-card join must fit and round-trip.");
        pass("valid fragmented Korean UTF-8, exact 64 KiB boundary and a normal 33-card join are accepted");

        socket = new InputSocket(new byte[BattleMessageReader.MaxMessageBytes + 1], 8192);
        await Rejected(socket, WebSocketCloseStatus.MessageTooBig);
        Require(socket.Receives == 9, "Fragment accumulation must reject the first byte above the limit.");
        await Rejected(new InputSocket(korean, 1, WebSocketMessageType.Binary), WebSocketCloseStatus.InvalidMessageType);
        await Rejected(new InputSocket(new byte[] { 0xc3, 0x28 }, 1), WebSocketCloseStatus.InvalidPayloadData);
        await Rejected(new InputSocket(new byte[] { 0xe3, 0x81 }, 1), WebSocketCloseStatus.InvalidPayloadData);
        pass("fragmented oversized input, binary input and invalid/truncated UTF-8 are rejected with protocol close statuses");

        socket = new InputSocket(korean, 1) { CloseInstead = true };
        Require(await Read(socket) == null && socket.CloseCount == 1 && socket.AbortCount == 1, "Remote close must discard partial input and use transport cleanup.");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        try { await BattleMessageReader.ReadTextAsync(new BattleClientConnection("cancel", new InputSocket(korean, 1)), canceled.Token); throw new Exception("Canceled receive accepted"); }
        catch (OperationCanceledException) { }
        pass("remote close and receive cancellation preserve the common connection cleanup contract");

        Task<string?> Read(InputSocket input) => BattleMessageReader.ReadTextAsync(new BattleClientConnection("input-check", input), ct);
        async Task Rejected(InputSocket input, WebSocketCloseStatus expected)
        {
            try { await Read(input); } catch (BattleMessageException ex) { Require(ex.CloseStatus == expected, "Wrong protocol close status"); return; }
            throw new InvalidOperationException("Input should have been rejected.");
        }
    }

    public static async Task ExpectTooLargeClose(ClientWebSocket socket, CancellationToken ct)
    {
        // Exceeds the limit across many continuation frames, not just one big frame.
        var block = Encoding.UTF8.GetBytes(new string(' ', 8192));
        for (var i = 0; i < 8; i++) await socket.SendAsync(new ArraySegment<byte>(block), WebSocketMessageType.Text, false, ct);
        await socket.SendAsync(new ArraySegment<byte>(new byte[] { (byte)' ' }), WebSocketMessageType.Text, true, ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(8));
        var buffer = new byte[32768];
        for (var i = 0; i < 30; i++)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
            if (result.MessageType != WebSocketMessageType.Close) continue;
            Require(result.CloseStatus == WebSocketCloseStatus.MessageTooBig, "Oversized real message must close with 1009.");
            return;
        }
        throw new InvalidOperationException("Oversized message was not closed.");
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    sealed class InputSocket(byte[] bytes, int fragmentSize, WebSocketMessageType type = WebSocketMessageType.Text) : WebSocket
    {
        int offset;
        public int Receives, CloseCount, AbortCount;
        public bool CloseInstead;
        WebSocketState state = WebSocketState.Open;
        public override WebSocketState State => state;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;
        public override void Abort() { AbortCount++; state = WebSocketState.Aborted; }
        public override void Dispose() { }
        public override Task CloseAsync(WebSocketCloseStatus status, string? reason, CancellationToken ct) => throw new NotSupportedException();
        public override Task CloseOutputAsync(WebSocketCloseStatus status, string? reason, CancellationToken ct) { CloseCount++; return Task.CompletedTask; }
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool end, CancellationToken ct) => throw new NotSupportedException();
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Receives++;
            if (CloseInstead && offset > 0) { state = WebSocketState.CloseReceived; return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true)); }
            var count = Math.Min(Math.Min(fragmentSize, buffer.Count), bytes.Length - offset);
            Array.Copy(bytes, offset, buffer.Array!, buffer.Offset, count); offset += count;
            return Task.FromResult(new WebSocketReceiveResult(count, type, offset == bytes.Length));
        }
    }
}
