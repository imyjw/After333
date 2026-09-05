param(
    [string]$BaseUrl = "http://127.0.0.1:7333",
    [int]$TimeoutSeconds = 8,
    [string]$ServerLogDirectory = "C:\Project_333\Logs\Server",
    [string]$AuditLogDirectory = "",
    [string]$PublishedServerPath = "C:\Project_333\Builds\Server\Project333.PvpServer",
    [string]$OutputDirectory = "C:\Project_333\Logs\SmokeReports",
    [string]$TestLabel = "",
    [string]$Notes = "",
    [string]$ExpectedRequiredClientVersion = "0.1.0-dev",
    [switch]$RunReadinessChecks,
    [switch]$AllowDeploymentWarnings,
    [switch]$SkipWebSocket,
    [switch]$SkipLocalLogCopy,
    [switch]$NoZip,
    [switch]$PrintOnly
)

$ErrorActionPreference = "Stop"

function Normalize-HttpUrl {
    param([string]$Url)

    if ([string]::IsNullOrWhiteSpace($Url)) {
        return "http://127.0.0.1:7333"
    }

    return $Url.Trim().TrimEnd("/")
}

function Invoke-JsonGet {
    param(
        [string]$Url,
        [int]$TimeoutSeconds
    )

    return Invoke-RestMethod `
        -Method Get `
        -Uri $Url `
        -TimeoutSec $TimeoutSeconds
}

function Save-JsonFile {
    param(
        [object]$Value,
        [string]$Path
    )

    $Value |
        ConvertTo-Json -Depth 16 |
        Set-Content -LiteralPath $Path -Encoding UTF8
}

function Copy-LatestFile {
    param(
        [string]$Directory,
        [string]$Filter,
        [string]$DestinationDirectory,
        [string]$FriendlyName
    )

    if ([string]::IsNullOrWhiteSpace($Directory) -or -not (Test-Path -LiteralPath $Directory)) {
        Write-Host "[evidence] $FriendlyName directory not found: $Directory"
        return $null
    }

    $file = Get-ChildItem -LiteralPath $Directory -Filter $Filter -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $file) {
        Write-Host "[evidence] No $FriendlyName file matched $Filter in $Directory"
        return $null
    }

    $destinationPath = Join-Path $DestinationDirectory $file.Name
    Copy-Item -LiteralPath $file.FullName -Destination $destinationPath -Force
    Write-Host "[evidence] copied ${FriendlyName}: $($file.FullName)"
    return $destinationPath
}

function Resolve-AuditLogDirectory {
    param(
        [string]$ExplicitDirectory,
        [object]$ServerStatus
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitDirectory)) {
        return $ExplicitDirectory
    }

    if ($null -ne $ServerStatus -and
        $null -ne $ServerStatus.Audit -and
        -not [string]::IsNullOrWhiteSpace($ServerStatus.Audit.LogDirectory)) {
        return $ServerStatus.Audit.LogDirectory
    }

    return "C:\Project_333\Logs\Server\Audit"
}

function Invoke-ToolAndSaveOutput {
    param(
        [string]$ScriptPath,
        [string[]]$Arguments,
        [string]$OutputPath,
        [string]$FriendlyName
    )

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("[evidence] running $FriendlyName") | Out-Null
    $lines.Add("[evidence] script=$ScriptPath") | Out-Null
    $lines.Add("[evidence] args=$($Arguments -join ' ')") | Out-Null
    $lines.Add("") | Out-Null

    $succeeded = $true
    $errorMessage = ""

    try {
        $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $ScriptPath @Arguments 2>&1
        foreach ($line in $output) {
            $lines.Add("$line") | Out-Null
        }

        $exitCode = if ($null -eq $LASTEXITCODE) { 0 } else { $LASTEXITCODE }
        if ($exitCode -ne 0) {
            $succeeded = $false
            $errorMessage = "$FriendlyName exited with code $exitCode."
            $lines.Add("") | Out-Null
            $lines.Add("[evidence] $errorMessage") | Out-Null
        }
    }
    catch {
        $succeeded = $false
        $errorMessage = $_.Exception.Message
        $lines.Add("") | Out-Null
        $lines.Add("[evidence] $FriendlyName failed: $errorMessage") | Out-Null
    }

    Set-Content -LiteralPath $OutputPath -Value $lines -Encoding UTF8
    Write-Host "[evidence] saved ${FriendlyName}: $OutputPath"

    return [pscustomobject]@{
        Name = $FriendlyName
        Succeeded = $succeeded
        OutputPath = $OutputPath
        Error = $errorMessage
    }
}

$BaseUrl = Normalize-HttpUrl $BaseUrl
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$safeLabel = if ([string]::IsNullOrWhiteSpace($TestLabel)) {
    "public_pvp_smoke"
}
else {
    ($TestLabel.Trim() -replace '[^a-zA-Z0-9가-힣._-]', '_')
}

$reportDirectory = Join-Path $OutputDirectory "${safeLabel}_$timestamp"

Write-Host "[evidence] BaseUrl=$BaseUrl"
Write-Host "[evidence] Output=$reportDirectory"
Write-Host "[evidence] RunReadinessChecks=$RunReadinessChecks SkipLocalLogCopy=$SkipLocalLogCopy NoZip=$NoZip"

if ($PrintOnly) {
    Write-Host "[evidence] PrintOnly was set. No files were collected."
    return
}

New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null

$health = $null
$serverStatus = $null
$sessions = $null

try {
    Write-Host "[evidence] GET /health"
    $health = Invoke-JsonGet "$BaseUrl/health" $TimeoutSeconds
    Save-JsonFile $health (Join-Path $reportDirectory "health.json")
}
catch {
    $message = "/health request failed: $($_.Exception.Message)"
    Write-Host "[evidence] warning: $message"
    Set-Content -LiteralPath (Join-Path $reportDirectory "health_error.txt") -Value $message -Encoding UTF8
}

try {
    Write-Host "[evidence] GET /server/status"
    $serverStatus = Invoke-JsonGet "$BaseUrl/server/status" $TimeoutSeconds
    Save-JsonFile $serverStatus (Join-Path $reportDirectory "server_status.json")
}
catch {
    $message = "/server/status request failed: $($_.Exception.Message)"
    Write-Host "[evidence] warning: $message"
    Set-Content -LiteralPath (Join-Path $reportDirectory "server_status_error.txt") -Value $message -Encoding UTF8
}

try {
    Write-Host "[evidence] GET /sessions"
    $sessions = Invoke-JsonGet "$BaseUrl/sessions" $TimeoutSeconds
    Save-JsonFile $sessions (Join-Path $reportDirectory "sessions.json")
}
catch {
    $message = "/sessions request failed: $($_.Exception.Message)"
    Write-Host "[evidence] warning: $message"
    Set-Content -LiteralPath (Join-Path $reportDirectory "sessions_error.txt") -Value $message -Encoding UTF8
}

$resolvedAuditLogDirectory = Resolve-AuditLogDirectory $AuditLogDirectory $serverStatus
$readinessResults = @()
$publishManifestPath = Join-Path $PublishedServerPath "project333_server_publish_manifest.json"
if (Test-Path -LiteralPath $publishManifestPath) {
    Copy-Item -LiteralPath $publishManifestPath -Destination (Join-Path $reportDirectory "project333_server_publish_manifest.json") -Force
    Write-Host "[evidence] copied server publish manifest: $publishManifestPath"
}
else {
    Write-Host "[evidence] server publish manifest not found: $publishManifestPath"
}

if ($RunReadinessChecks) {
    $toolsDirectory = Split-Path -Parent $PSCommandPath
    $commonReadinessArgs = @(
        "-BaseUrl", $BaseUrl,
        "-ExpectedRequiredClientVersion", $ExpectedRequiredClientVersion,
        "-TimeoutSeconds", "$TimeoutSeconds"
    )

    if ($AllowDeploymentWarnings) {
        $commonReadinessArgs += "-AllowDeploymentWarnings"
    }

    if ($SkipWebSocket) {
        $commonReadinessArgs += "-SkipWebSocket"
    }

    $deploymentReadinessScript = Join-Path $toolsDirectory "TestDeploymentReadiness.ps1"
    $readinessResults += Invoke-ToolAndSaveOutput `
        -ScriptPath $deploymentReadinessScript `
        -Arguments $commonReadinessArgs `
        -OutputPath (Join-Path $reportDirectory "deployment_readiness_output.txt") `
        -FriendlyName "deployment readiness"

    $publicSmokeArgs = @($commonReadinessArgs)
    if ($SkipLocalLogCopy) {
        $publicSmokeArgs += "-SkipLocalLogCheck"
    }

    $publicSmokeScript = Join-Path $toolsDirectory "TestPublicPvpSmokeReadiness.ps1"
    $readinessResults += Invoke-ToolAndSaveOutput `
        -ScriptPath $publicSmokeScript `
        -Arguments $publicSmokeArgs `
        -OutputPath (Join-Path $reportDirectory "public_pvp_smoke_readiness_output.txt") `
        -FriendlyName "public PvP smoke readiness"

    Save-JsonFile $readinessResults (Join-Path $reportDirectory "readiness_results.json")
}

if (-not $SkipLocalLogCopy) {
    Copy-LatestFile $ServerLogDirectory "server_*.out.log" $reportDirectory "server stdout log" | Out-Null
    Copy-LatestFile $ServerLogDirectory "server_*.err.log" $reportDirectory "server stderr log" | Out-Null
    Copy-LatestFile $resolvedAuditLogDirectory "audit_*.jsonl" $reportDirectory "audit log" | Out-Null
}
else {
    Write-Host "[evidence] Local log copy skipped."
}

$summary = @"
Project333 Public PvP Smoke Evidence

CollectedAt: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss K")
BaseUrl: $BaseUrl
TestLabel: $safeLabel
ServerLogDirectory: $ServerLogDirectory
AuditLogDirectory: $resolvedAuditLogDirectory
PublishedServerPath: $PublishedServerPath
RunReadinessChecks: $RunReadinessChecks
ExpectedRequiredClientVersion: $ExpectedRequiredClientVersion
AllowDeploymentWarnings: $AllowDeploymentWarnings
SkipWebSocket: $SkipWebSocket

Notes:
$Notes

Manual fields to fill if needed:
Tester A account:
Tester B account:
Match ID:
Observed issue:
Expected result:
Actual result:
"@

Set-Content -LiteralPath (Join-Path $reportDirectory "README.txt") -Value $summary -Encoding UTF8

if (-not $NoZip) {
    $zipPath = "$reportDirectory.zip"
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $reportDirectory "*") -DestinationPath $zipPath -Force
    Write-Host "[evidence] zip=$zipPath"
}

Write-Host "[evidence] collected=$reportDirectory"
