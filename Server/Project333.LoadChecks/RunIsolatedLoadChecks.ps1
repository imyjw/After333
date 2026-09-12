param(
    [Parameter(Mandatory = $true)]
    [string]$ServerOutput,
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin'
)

$ErrorActionPreference = 'Stop'
$serverOutputPath = (Resolve-Path -LiteralPath $ServerOutput).Path
$serverDll = Join-Path $serverOutputPath 'Project333.PvpServer.dll'
if (-not (Test-Path -LiteralPath $serverDll -PathType Leaf)) { throw 'A freshly built server output is required.' }
foreach ($tool in @('initdb.exe', 'pg_ctl.exe', 'createdb.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $PostgresBin $tool) -PathType Leaf)) { throw "PostgreSQL tool missing: $tool" }
}

# Every execution owns a fresh directory and cluster. There is no live-DB/URL argument,
# no drop/delete command, and cleanup stops only processes started by this execution.
$tempPrefix = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Temp')).TrimEnd('\') + '\'
$runRoot = [IO.Path]::GetFullPath((Join-Path $tempPrefix ('after333-run-authority-check-' + [guid]::NewGuid().ToString('N'))))
if (-not $runRoot.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $runRoot)) {
    throw 'The isolated output directory must be a new child of the OS temporary directory.'
}
$pgData = Join-Path $runRoot 'PgData'
$dbPort = 15439
$httpPort = 17339
foreach ($port in @($dbPort, $httpPort)) {
    $probe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $port)
    try { $probe.Start() } catch { throw "Isolated test port $port is already in use. No existing process was touched." }
    finally { $probe.Stop() }
}
New-Item -ItemType Directory -Path $runRoot | Out-Null
Write-Output "Isolated artifacts: $runRoot"
Write-Output "Database: fresh PgData under that directory, loopback:$dbPort; HTTP: loopback:$httpPort"

$environment = @{
    PROJECT333_DB_CONNECTION = "Host=127.0.0.1;Port=$dbPort;Database=after333_run_authority;Username=after333_authority_test;Timeout=5"
    PROJECT333_PVP_SERVER_URL = "http://127.0.0.1:$httpPort"
    PROJECT333_PUBLIC_SERVER_URL = "http://127.0.0.1:$httpPort"
    PROJECT333_REQUIRED_CLIENT_VERSION = '0.1.0-dev'
    PROJECT333_ENABLE_PVP_STARTUP_RECOVERY = '0'
    PROJECT333_NEW_ACCOUNT_TICKETS = '2'
    PROJECT333_NEW_ACCOUNT_RESOURCE_GOLD = '0'
    PROJECT333_DEV_MIN_TICKETS = '-1'
    PROJECT333_DRAFT_RUN_TICKET_COST = '1'
    PROJECT333_RUN_REWARD_RESOURCE_GOLD_BASE = '0'
    PROJECT333_RUN_REWARD_RESOURCE_GOLD_PER_WIN = '3'
    PROJECT333_RUN_REWARD_RESOURCE_GOLD_PER_LOSS = '0'
    PROJECT333_RUN_REWARD_TICKETS_BASE = '0'
    PROJECT333_RUN_REWARD_TICKETS_PER_WIN = '0'
    PROJECT333_RUN_REWARD_TICKETS_PER_LOSS = '0'
    PROJECT333_RUN_REWARD_CARD_REWARDS = '{}'
    PROJECT333_RUN_REWARD_PACK_REWARDS = '{}'
    PROJECT333_AUDIT_LOG_DIR = (Join-Path $runRoot 'Audit')
    PROJECT333_ACCOUNT_RATE_LIMIT_PER_WINDOW = '100000'
    PROJECT333_AUTH_RATE_LIMIT_PER_WINDOW = '100000'
    PROJECT333_BATTLE_CONNECT_RATE_LIMIT_PER_WINDOW = '100000'
    PROJECT333_BATTLE_AUTH_TIMEOUT_SECONDS = '5'
    AFTER333_AUTHORITY_TEST_DATA = $pgData
    PROJECT333_BATTLE_RESULT_OUTBOX_DIR = (Join-Path $runRoot 'ServerResultOutbox')
    ASPNETCORE_ENVIRONMENT = 'Production'
    DOTNET_ENVIRONMENT = 'Production'
    TEMP = $tempPrefix.TrimEnd('\')
    TMP = $tempPrefix.TrimEnd('\')
}
$previousLoadPid=[Environment]::GetEnvironmentVariable('AFTER333_LOAD_SERVER_PID','Process')
$savedEnvironment = @{}
foreach ($key in $environment.Keys) {
    $savedEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
    [Environment]::SetEnvironmentVariable($key, $environment[$key], 'Process')
}
$server = $null
$pgStarted = $false
$pg = Join-Path $PostgresBin 'pg_ctl.exe'

function Wait-TestServer([Diagnostics.Process]$Process) {
    for ($i = 0; $i -lt 50; $i++) {
        if ($Process.HasExited) { throw 'Isolated HTTP server exited during startup. See its output logs.' }
        try {
            $health = Invoke-RestMethod -Uri "http://127.0.0.1:$httpPort/health" -TimeoutSec 1
            if ($health.Status -eq 'Ok') { return }
        } catch { }
        Start-Sleep -Milliseconds 200
    }
    throw 'Isolated HTTP server did not become ready.'
}


try {
    $harnessArtifacts = Join-Path $runRoot 'HarnessArtifacts'
    & dotnet build (Join-Path $PSScriptRoot 'LoadChecks.csproj') --configuration Release `
        --artifacts-path $harnessArtifacts --configfile (Join-Path $PSScriptRoot 'offline.NuGet.Config') `
        '-p:NuGetAudit=false' "-p:ServerOutput=$serverOutputPath" --ignore-failed-sources `
        *> (Join-Path $runRoot 'harness-build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Harness build failed. See harness-build.log.' }

    & (Join-Path $PostgresBin 'initdb.exe') -D $pgData -U after333_authority_test `
        --auth-local=trust --auth-host=trust --encoding=UTF8 --no-locale `
        *> (Join-Path $runRoot 'initdb.log')
    if ($LASTEXITCODE -ne 0) { throw 'Fresh synthetic PostgreSQL initialization failed.' }
    # Direct file redirection avoids PowerShell waiting for inherited native pipe handles.
    $pgStart = Start-Process -FilePath $pg -ArgumentList @('-D', ('"' + $pgData + '"'),
        '-l', ('"' + (Join-Path $runRoot 'postgres.log') + '"'), '-o', ('"-h 127.0.0.1 -p ' + $dbPort + '"'),
        '-w', '-t', '15', 'start') -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $runRoot 'postgres-start.log') `
        -RedirectStandardError (Join-Path $runRoot 'postgres-start.err.log')
    # Start-Process -Wait waits for the PostgreSQL child as well; wait for pg_ctl only.
    $null = $pgStart.Handle
    if (-not $pgStart.WaitForExit(20000)) { throw 'Test pg_ctl did not return within 20 seconds.' }
    $pgStart.Refresh()
    if ($pgStart.ExitCode -ne 0) { throw 'Fresh synthetic PostgreSQL startup failed.' }
    $pgStarted = $true
    & (Join-Path $PostgresBin 'createdb.exe') -h 127.0.0.1 -p $dbPort -U after333_authority_test -w after333_run_authority `
        *> (Join-Path $runRoot 'createdb.log')
    if ($LASTEXITCODE -ne 0) { throw 'Synthetic database creation failed.' }

    $server = Start-Process dotnet -ArgumentList @('"' + $serverDll + '"') -WorkingDirectory $serverOutputPath `
        -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runRoot 'server.out.log') `
        -RedirectStandardError (Join-Path $runRoot 'server.err.log')
    Wait-TestServer $server
    $env:AFTER333_LOAD_SERVER_PID=[string]$server.Id
    $harnessDll = Join-Path $harnessArtifacts 'bin\LoadChecks\release\LoadChecks.dll'
    $harness = Start-Process dotnet -ArgumentList @('"' + $harnessDll + '"') -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $runRoot 'checks.log') -RedirectStandardError (Join-Path $runRoot 'checks.err.log')
    $null = $harness.Handle
    $harnessTimer = [Diagnostics.Stopwatch]::StartNew()
    while (-not $harness.WaitForExit(20000)) {
        if ($harnessTimer.Elapsed.TotalSeconds -ge 480) {
            Stop-Process -Id $harness.Id
            throw 'Run authority checks exceeded the bounded execution time.'
        }
    }
    $harness.Refresh()
    if ($harness.ExitCode -ne 0) { throw 'Run authority checks failed. See checks.log and checks.err.log.' }
    Get-Content -LiteralPath (Join-Path $runRoot 'checks.log')

} finally {
    if ($null -ne $server -and -not $server.HasExited) { Stop-Process -Id $server.Id; $server.WaitForExit() }
    if ($pgStarted) {
        $pgStop = Start-Process -FilePath $pg -ArgumentList @('-D', ('"' + $pgData + '"'), '-m', 'fast', '-w', '-t', '15', 'stop') `
            -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runRoot 'postgres-stop.log') `
            -RedirectStandardError (Join-Path $runRoot 'postgres-stop.err.log')
        $null = $pgStop.Handle
        if (-not $pgStop.WaitForExit(20000)) { Write-Warning 'Synthetic pg_ctl stop exceeded 20 seconds.' }
        $pgStop.Refresh()
        if ($pgStop.HasExited -and $pgStop.ExitCode -ne 0) { Write-Warning 'Synthetic PostgreSQL did not stop cleanly; inspect postgres-stop.log.' }
    }
    foreach ($key in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($key, $savedEnvironment[$key], 'Process')
    }
    [Environment]::SetEnvironmentVariable('AFTER333_LOAD_SERVER_PID',$previousLoadPid,'Process')
    Write-Output "Preserved test artifacts: $runRoot"
}
