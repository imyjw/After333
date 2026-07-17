param(
    [Parameter(Mandatory = $true)]
    [string]$Domain,
    [Parameter(Mandatory = $true)]
    [string]$AdminEmail,
    [string]$PublishedServerPath = "C:\Project_333\Builds\Server\Project333.PvpServer",
    [string]$OutputPath = "C:\Project_333\Builds\Staging\Project333.PvpServer",
    [string]$ProjectRoot = "C:\Project_333",
    [string]$RequiredClientVersion = "0.1.0-dev",
    [switch]$AllowMissingPublishManifest,
    [switch]$Overwrite,
    [switch]$PrintOnly
)

$ErrorActionPreference = "Stop"

function Resolve-Project333FullPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "A non-empty path is required."
    }

    return [System.IO.Path]::GetFullPath($Path)
}

function Test-Project333ChildPath {
    param(
        [string]$ParentPath,
        [string]$ChildPath
    )

    $parent = (Resolve-Project333FullPath $ParentPath).TrimEnd('\') + '\'
    $child = Resolve-Project333FullPath $ChildPath
    return $child.StartsWith($parent, [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-Project333Domain {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "Domain is required."
    }

    $normalized = $Value.Trim().TrimEnd('.')
    if ($normalized.Contains("://") -or $normalized.Contains("/") -or $normalized.Contains(":")) {
        throw "Domain must be a DNS host name without scheme, port, or path. Example: staging.project333.example"
    }

    if ([System.Uri]::CheckHostName($normalized) -ne [System.UriHostNameType]::Dns) {
        throw "Domain is not a valid DNS host name: $normalized"
    }

    return $normalized
}

function Assert-Project333Email {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value) -or -not $Value.Contains("@")) {
        throw "AdminEmail must be a valid certificate-notification email address."
    }

    return $Value.Trim()
}

$Domain = Assert-Project333Domain $Domain
$AdminEmail = Assert-Project333Email $AdminEmail
$ProjectRoot = Resolve-Project333FullPath $ProjectRoot
$PublishedServerPath = Resolve-Project333FullPath $PublishedServerPath
$OutputPath = Resolve-Project333FullPath $OutputPath

if ([string]::IsNullOrWhiteSpace($RequiredClientVersion)) {
    throw "RequiredClientVersion is required."
}

$serverDll = Join-Path $PublishedServerPath "Project333.PvpServer.dll"
if (-not (Test-Path -LiteralPath $serverDll)) {
    throw "Published server DLL was not found: $serverDll. Run PublishServer_Release.ps1 first."
}

$sourcePublishManifestPath = Join-Path $PublishedServerPath "project333_server_publish_manifest.json"
$sourcePublishManifest = $null
if (-not (Test-Path -LiteralPath $sourcePublishManifestPath)) {
    if (-not $AllowMissingPublishManifest) {
        throw "Published server manifest was not found: $sourcePublishManifestPath. Republish with PublishServer_Release.ps1, or use -AllowMissingPublishManifest only for a legacy local validation."
    }

    Write-Warning "Published server manifest is missing. This bundle cannot prove its source version."
}
else {
    try {
        $sourcePublishManifest = Get-Content -LiteralPath $sourcePublishManifestPath -Raw | ConvertFrom-Json
    }
    catch {
        throw "Published server manifest could not be read: $($_.Exception.Message)"
    }

    if ($sourcePublishManifest.ClientVersion -ne $RequiredClientVersion) {
        throw "Published server client version '$($sourcePublishManifest.ClientVersion)' does not match required bundle version '$RequiredClientVersion'. Republish with the intended client version."
    }
}

$caddyTemplatePath = Join-Path $ProjectRoot "Server\Project333.PvpServer\Deploy\Caddyfile.example"
if (-not (Test-Path -LiteralPath $caddyTemplatePath)) {
    throw "Caddy template was not found: $caddyTemplatePath"
}

$sourceToolsPath = Join-Path $ProjectRoot "Server\Project333.PvpServer\Tools"
$toolNames = @(
    "RunPublishedServer_Watchdog.ps1",
    "TestDeploymentReadiness.ps1",
    "TestServerEndpoint.ps1",
    "CollectPublicPvpSmokeEvidence.ps1",
    "BackupPostgres_Project333.ps1",
    "RestorePostgres_Project333.ps1"
)

foreach ($toolName in $toolNames) {
    $toolPath = Join-Path $sourceToolsPath $toolName
    if (-not (Test-Path -LiteralPath $toolPath)) {
        throw "Required deployment tool was not found: $toolPath"
    }
}

$publicBaseUrl = "https://$Domain"
$battleWebSocketUrl = "wss://$Domain/battle"

Write-Host "[After333] Staging deployment bundle"
Write-Host "  SourceServer:  $PublishedServerPath"
Write-Host "  Output:        $OutputPath"
Write-Host "  Domain:        $Domain"
Write-Host "  PublicBaseUrl: $publicBaseUrl"
Write-Host "  BattleSocket:  $battleWebSocketUrl"
Write-Host "  ClientVersion: $RequiredClientVersion"
Write-Host "  AdminEmail:    $AdminEmail"
Write-Host "  IncludesDbSecret: false"
Write-Host "  PublishManifest: $(if ($null -eq $sourcePublishManifest) { '<missing; legacy override>' } else { $sourcePublishManifestPath })"

if ($PrintOnly) {
    Write-Host "[After333] PrintOnly was set. No files were created."
    return
}

if (Test-Path -LiteralPath $OutputPath) {
    if (-not $Overwrite) {
        throw "Output path already exists: $OutputPath. Use -Overwrite to replace a bundle under the project Builds directory."
    }

    $buildsRoot = Join-Path $ProjectRoot "Builds"
    if (-not (Test-Project333ChildPath $buildsRoot $OutputPath)) {
        throw "Refusing to overwrite a directory outside the project Builds directory: $OutputPath"
    }

    Remove-Item -LiteralPath $OutputPath -Recurse -Force
}

$serverOutputPath = Join-Path $OutputPath "Server"
$toolsOutputPath = Join-Path $OutputPath "Tools"
$logsOutputPath = Join-Path $OutputPath "Logs"

New-Item -ItemType Directory -Force -Path $serverOutputPath | Out-Null
New-Item -ItemType Directory -Force -Path $toolsOutputPath | Out-Null
New-Item -ItemType Directory -Force -Path $logsOutputPath | Out-Null

Copy-Item -Path (Join-Path $PublishedServerPath "*") -Destination $serverOutputPath -Recurse -Force
foreach ($toolName in $toolNames) {
    Copy-Item -LiteralPath (Join-Path $sourceToolsPath $toolName) -Destination (Join-Path $toolsOutputPath $toolName) -Force
}

$caddyfile = (Get-Content -LiteralPath $caddyTemplatePath -Raw)
$caddyfile = $caddyfile.Replace("project333.example.com", $Domain)
$caddyfile = $caddyfile.Replace("admin@example.com", $AdminEmail)
$caddyfilePath = Join-Path $OutputPath "Caddyfile"
$caddyfile | Set-Content -LiteralPath $caddyfilePath -Encoding UTF8

$startScriptLines = @(
    'param(',
    '    [string]$DatabaseConnection = "",',
    '    [switch]$RestartOnCleanExit,',
    '    [switch]$PrintOnly',
    ')',
    '',
    '$ErrorActionPreference = "Stop"',
    '$BundleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path',
    '$Runner = Join-Path $BundleRoot "Tools\RunPublishedServer_Watchdog.ps1"',
    'if ([string]::IsNullOrWhiteSpace($DatabaseConnection)) {',
    '    $DatabaseConnection = $env:PROJECT333_DB_CONNECTION',
    '}',
    '',
    'if ([string]::IsNullOrWhiteSpace($DatabaseConnection)) {',
    '    $secureConnection = Read-Host "PostgreSQL connection string" -AsSecureString',
    '    $connectionPointer = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureConnection)',
    '    try {',
    '        $DatabaseConnection = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($connectionPointer)',
    '    }',
    '    finally {',
    '        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($connectionPointer)',
    '    }',
    '}',
    '',
    'if ([string]::IsNullOrWhiteSpace($DatabaseConnection)) {',
    '    throw "A PostgreSQL connection string is required."',
    '}',
    '',
    '$parameters = @{',
    '    PublishedServerPath = Join-Path $BundleRoot "Server"',
    '    ListenUrl = "http://127.0.0.1:7333"',
    "    PublicServerUrl = `"$publicBaseUrl`"",
    '    DatabaseConnection = $DatabaseConnection',
    "    RequiredClientVersion = `"$RequiredClientVersion`"",
    '    LogDirectory = Join-Path $BundleRoot "Logs"',
    '    LogRetentionDays = 30',
    '    RestartDelaySeconds = 3',
    '    RestartOnCleanExit = $RestartOnCleanExit',
    '    PrintOnly = $PrintOnly',
    '}',
    '',
    '& $Runner @parameters'
)
$startScriptPath = Join-Path $OutputPath "StartStagingServer.ps1"
$startScriptLines | Set-Content -LiteralPath $startScriptPath -Encoding UTF8

$verifyScriptLines = @(
    'param(',
    '    [switch]$SkipWebSocket,',
    '    [switch]$PrintOnly',
    ')',
    '',
    '$ErrorActionPreference = "Stop"',
    '$BundleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path',
    '$ReadinessTool = Join-Path $BundleRoot "Tools\TestDeploymentReadiness.ps1"',
    '$parameters = @{',
    "    BaseUrl = `"$publicBaseUrl`"",
    "    BattleWebSocketUrl = `"$battleWebSocketUrl`"",
    "    ExpectedPublicUrl = `"$publicBaseUrl`"",
    "    ExpectedRequiredClientVersion = `"$RequiredClientVersion`"",
    '    RequireStartupRecovery = $true',
    '    SkipWebSocket = $SkipWebSocket',
    '    PrintOnly = $PrintOnly',
    '}',
    '',
    '& $ReadinessTool @parameters'
)
$verifyScriptPath = Join-Path $OutputPath "VerifyStagingDeployment.ps1"
$verifyScriptLines | Set-Content -LiteralPath $verifyScriptPath -Encoding UTF8

$readmeLines = @(
    '# After333 Staging Deployment Bundle',
    '',
    "Created: $([DateTimeOffset]::UtcNow.ToString('O'))",
    "Public URL: $publicBaseUrl",
    "Battle WebSocket: $battleWebSocketUrl",
    "Required client version: $RequiredClientVersion",
    '',
    'This bundle does not contain a PostgreSQL password or connection string.',
    '',
    '## Host prerequisites',
    '',
    '- .NET 8 runtime',
    '- Caddy',
    '- Reachable PostgreSQL database',
    '- DNS A/AAAA record for the domain pointing to this host',
    '- Inbound TCP 80 and 443 allowed',
    '- TCP 7333 kept private',
    '',
    '## Start order',
    '',
    '1. Open PowerShell in this bundle directory.',
    '2. Start Project333 in the first PowerShell window:',
    '',
    '```powershell',
    '.\StartStagingServer.ps1',
    '```',
    '',
    'The script securely prompts for the PostgreSQL connection string so the password is not written into PowerShell command history.',
    'For unattended hosting, provide `PROJECT333_DB_CONNECTION` through the host secret/environment manager before starting the script.',
    '',
    '3. Start Caddy in the second PowerShell window:',
    '',
    '```powershell',
    'caddy run --config .\Caddyfile',
    '```',
    '',
    '4. Verify the public HTTPS/WSS endpoint:',
    '',
    '```powershell',
    '.\VerifyStagingDeployment.ps1',
    '```',
    '',
    '5. Set the Unity client ServerSettingsPanel URL to:',
    '',
    '```text',
    $publicBaseUrl,
    '```',
    '',
    '## Safety',
    '',
    '- Do not save the real database connection string in this bundle.',
    '- Prefer the secure prompt or a host secret manager instead of a command-line password.',
    '- Do not expose port 7333 to the public internet.',
    '- Back up PostgreSQL before schema changes or external tests.',
    '- Keep smoke-test commands disabled for staging/public tests.'
)
$readmePath = Join-Path $OutputPath "README.md"
$readmeLines | Set-Content -LiteralPath $readmePath -Encoding UTF8

$bundleManifest = [pscustomobject]@{
    SchemaVersion = 1
    CreatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    Domain = $Domain
    PublicBaseUrl = $publicBaseUrl
    BattleWebSocketUrl = $battleWebSocketUrl
    RequiredClientVersion = $RequiredClientVersion
    AdminEmail = $AdminEmail
    ContainsDatabaseSecret = $false
    PublishedServerManifest = $sourcePublishManifest
    Files = [pscustomobject]@{
        Server = "Server"
        Caddyfile = "Caddyfile"
        StartScript = "StartStagingServer.ps1"
        VerifyScript = "VerifyStagingDeployment.ps1"
        Readme = "README.md"
        Tools = $toolNames
    }
}
$bundleManifestPath = Join-Path $OutputPath "project333_staging_bundle_manifest.json"
$bundleManifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $bundleManifestPath -Encoding UTF8

Write-Host "[After333] Staging bundle created."
Write-Host "  Bundle:       $OutputPath"
Write-Host "  Caddyfile:    $caddyfilePath"
Write-Host "  Start:        $startScriptPath"
Write-Host "  Verify:       $verifyScriptPath"
Write-Host "  Manifest:     $bundleManifestPath"
Write-Host "  Next: copy this bundle to the staging host, then follow README.md."
