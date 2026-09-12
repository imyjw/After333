param(
    [string]$BaseUrl = "http://127.0.0.1:7333",
    [string]$ClientVersion = "0.1.0-dev"
)

$ErrorActionPreference = "Stop"
$uri = [UriBuilder]::new($BaseUrl.TrimEnd('/') + '/battle')
$uri.Scheme = if ($uri.Scheme -eq 'https') { 'wss' } else { 'ws' }

foreach ($kind in @('JoinMatch', 'ClientCommand')) {
    $socket = [System.Net.WebSockets.ClientWebSocket]::new()
    $socket.Options.SetRequestHeader('X-Project333-Client-Version', $ClientVersion)
    $timeout = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(10))
    try {
        $socket.ConnectAsync($uri.Uri, $timeout.Token).GetAwaiter().GetResult() | Out-Null
        $buffer = New-Object byte[] 65536
        # Consume the initial server greeting before testing the request.
        do {
            $greeting = $socket.ReceiveAsync([ArraySegment[byte]]::new($buffer), $timeout.Token).GetAwaiter().GetResult()
        } until ($greeting.EndOfMessage)

        $matchId = 'auth-check-' + [guid]::NewGuid().ToString('N')
        $payload = if ($kind -eq 'JoinMatch') {
            @{MessageType='JoinMatch'; MatchId=$matchId; PlayerToken='auth-check'; UseServerAiOpponent=$false}
        } else {
            @{MessageType='ClientCommand'; ClientCommand=@{MatchId=$matchId; PlayerToken='auth-check'; ActorId='Player'; Sequence=1; CommandType='EndTurn'}}
        }
        $bytes = [Text.Encoding]::UTF8.GetBytes(($payload | ConvertTo-Json -Depth 5 -Compress))
        $socket.SendAsync([ArraySegment[byte]]::new($bytes), [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $timeout.Token).GetAwaiter().GetResult() | Out-Null
        $message = [IO.MemoryStream]::new()
        try {
            do {
                $received = $socket.ReceiveAsync([ArraySegment[byte]]::new($buffer), $timeout.Token).GetAwaiter().GetResult()
                if ($received.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) { throw 'Server closed before rejecting the request.' }
                $message.Write($buffer, 0, $received.Count)
            } until ($received.EndOfMessage)
            $response = [Text.Encoding]::UTF8.GetString($message.ToArray()) | ConvertFrom-Json
        } finally { $message.Dispose() }

        $expected = if ($kind -eq 'JoinMatch') { 'missing_session_token' } else { 'join_required' }
        if ($response.MessageType -ne 'Error' -or $response.Error.Code -ne $expected -or $response.HasAssignedSeat) {
            throw "$kind must be rejected with $expected without assigning a seat. Actual error: $($response.Error.Code)"
        }
        Write-Host "[battle-auth] $kind rejected: $expected"
    } finally {
        $socket.Abort()
        $socket.Dispose()
        $timeout.Dispose()
    }
}
Write-Host '[battle-auth] Unauthenticated battle access checks passed.'
