param(
    [string]$ProjectPath = "C:\Project_333\Server\Project333.PvpServer\Project333.PvpServer.csproj",
    [string]$ListenUrl = "http://0.0.0.0:7333",
    [string]$DatabaseConnection = $env:PROJECT333_DB_CONNECTION,
    [string]$RequiredClientVersion = "0.1.0-dev",
    [string]$RecommendedClientVersion = "",
    [int]$SessionTokenDays = 7,
    [int]$RefreshTokenDays = 30,
    [int]$RateLimitWindowSeconds = 60,
    [int]$AuthRateLimitPerWindow = 12,
    [int]$AccountRateLimitPerWindow = 120,
    [int]$BattleConnectRateLimitPerWindow = 20,
    [string]$GoogleClientIds = $env:PROJECT333_GOOGLE_CLIENT_IDS,
    [string]$GoogleDesktopClientId = $env:PROJECT333_GOOGLE_DESKTOP_CLIENT_ID,
    [string]$GoogleDesktopClientSecret = $env:PROJECT333_GOOGLE_DESKTOP_CLIENT_SECRET,
    [switch]$EnableStartupRecovery,
    [switch]$VerboseTransportLogs,
    [switch]$PrintOnly
)

$ErrorActionPreference = "Stop"

function Set-Project333Env {
    param(
        [string]$Name,
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        Remove-Item -Path "Env:$Name" -ErrorAction SilentlyContinue
        return
    }

    Set-Item -Path "Env:$Name" -Value $Value
}

function Format-MaskedConnectionString {
    param([string]$ConnectionString)

    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        return "<not configured>"
    }

    return ($ConnectionString -replace '(?i)(Password|Pwd)=([^;]*)', '$1=****')
}

function Get-Project333LanAddresses {
    $addresses = [System.Net.Dns]::GetHostAddresses([System.Net.Dns]::GetHostName())
    foreach ($address in $addresses) {
        if ($address.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork) {
            continue
        }

        $text = $address.ToString()
        if ($text.StartsWith("127.")) {
            continue
        }

        if ($text.StartsWith("169.254.")) {
            continue
        }

        $text
    }
}

function Get-Project333ListenPort {
    param([string]$Url)

    try {
        $uri = [System.Uri]$Url
        if ($uri.Port -gt 0) {
            return $uri.Port
        }
    }
    catch {
    }

    return 7333
}

function Write-Project333LanHints {
    param([string]$ListenUrl)

    $port = Get-Project333ListenPort $ListenUrl
    $addresses = @(Get-Project333LanAddresses)

    Write-Host "  LAN hints:"
    if ($addresses.Count -eq 0) {
        Write-Host "    No LAN IPv4 address was detected. Check Windows network settings."
        return
    }

    foreach ($address in $addresses) {
        $baseUrl = "http://${address}:$port"
        Write-Host "    Client Server URL: $baseUrl"
        Write-Host "    Endpoint test: powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestServerEndpoint.ps1 -BaseUrl '$baseUrl'"
    }

    Write-Host "    If another PC cannot connect, allow TCP port $port through Windows Defender Firewall on this server PC."
}

if (-not (Test-Path -LiteralPath $ProjectPath)) {
    throw "Project file was not found: $ProjectPath"
}

if ([string]::IsNullOrWhiteSpace($DatabaseConnection)) {
    throw "RunServer_Test.ps1 requires PROJECT333_DB_CONNECTION or -DatabaseConnection. Test/LAN PvP should not run without PostgreSQL."
}

if ($SessionTokenDays -lt 1 -or $RefreshTokenDays -lt 1) {
    throw "SessionTokenDays and RefreshTokenDays must both be at least 1."
}

if ($RateLimitWindowSeconds -lt 1 -or
    $AuthRateLimitPerWindow -lt 1 -or
    $AccountRateLimitPerWindow -lt 1 -or
    $BattleConnectRateLimitPerWindow -lt 1) {
    throw "Rate-limit window and permit values must all be at least 1."
}

Set-Project333Env "PROJECT333_PVP_SERVER_URL" $ListenUrl
Set-Project333Env "PROJECT333_PUBLIC_SERVER_URL" ""
Set-Project333Env "PROJECT333_DB_CONNECTION" $DatabaseConnection
Set-Project333Env "PROJECT333_GOOGLE_CLIENT_IDS" $GoogleClientIds
Set-Project333Env "PROJECT333_GOOGLE_DESKTOP_CLIENT_ID" $GoogleDesktopClientId
Set-Project333Env "PROJECT333_GOOGLE_DESKTOP_CLIENT_SECRET" $GoogleDesktopClientSecret
Set-Project333Env "PROJECT333_REQUIRED_CLIENT_VERSION" $RequiredClientVersion
Set-Project333Env "PROJECT333_RECOMMENDED_CLIENT_VERSION" $RecommendedClientVersion
Set-Project333Env "PROJECT333_DEV_MIN_TICKETS" ""
Set-Project333Env "PROJECT333_SESSION_TOKEN_DAYS" "$SessionTokenDays"
Set-Project333Env "PROJECT333_REFRESH_TOKEN_DAYS" "$RefreshTokenDays"
Set-Project333Env "PROJECT333_ENABLE_PVP_STARTUP_RECOVERY" $(if ($EnableStartupRecovery) { "1" } else { "0" })
Set-Project333Env "PROJECT333_VERBOSE_TRANSPORT_LOGS" $(if ($VerboseTransportLogs) { "1" } else { "0" })
Set-Project333Env "PROJECT333_RATE_LIMIT_ENABLED" "1"
Set-Project333Env "PROJECT333_RATE_LIMIT_WINDOW_SECONDS" "$RateLimitWindowSeconds"
Set-Project333Env "PROJECT333_AUTH_RATE_LIMIT_PER_WINDOW" "$AuthRateLimitPerWindow"
Set-Project333Env "PROJECT333_ACCOUNT_RATE_LIMIT_PER_WINDOW" "$AccountRateLimitPerWindow"
Set-Project333Env "PROJECT333_BATTLE_CONNECT_RATE_LIMIT_PER_WINDOW" "$BattleConnectRateLimitPerWindow"

Write-Host "[After333] Test server settings"
Write-Host "  ListenUrl: $env:PROJECT333_PVP_SERVER_URL"
Write-Host "  PublicUrl: <same as ListenUrl>"
Write-Host "  Database:  $(Format-MaskedConnectionString $env:PROJECT333_DB_CONNECTION)"
Write-Host "  Client:    required=$env:PROJECT333_REQUIRED_CLIENT_VERSION recommended=$env:PROJECT333_RECOMMENDED_CLIENT_VERSION"
Write-Host "  Tickets:   devMinimum=<disabled>"
Write-Host "  Auth:      session=${env:PROJECT333_SESSION_TOKEN_DAYS}d refresh=${env:PROJECT333_REFRESH_TOKEN_DAYS}d"
Write-Host "  Recovery:  startup=$env:PROJECT333_ENABLE_PVP_STARTUP_RECOVERY verboseTransport=$env:PROJECT333_VERBOSE_TRANSPORT_LOGS"
Write-Host "  RateLimits: enabled=$env:PROJECT333_RATE_LIMIT_ENABLED window=${env:PROJECT333_RATE_LIMIT_WINDOW_SECONDS}s auth=$env:PROJECT333_AUTH_RATE_LIMIT_PER_WINDOW account=$env:PROJECT333_ACCOUNT_RATE_LIMIT_PER_WINDOW battleConnect=$env:PROJECT333_BATTLE_CONNECT_RATE_LIMIT_PER_WINDOW"
Write-Project333LanHints $env:PROJECT333_PVP_SERVER_URL

if ($PrintOnly) {
    Write-Host "[After333] PrintOnly was set. Server was not started."
    return
}

dotnet run --project $ProjectPath
