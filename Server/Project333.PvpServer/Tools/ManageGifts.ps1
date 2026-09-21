[CmdletBinding()]
param(
    [ValidateSet('Accounts','Prepare','Preview','Send')][string]$Mode = 'Accounts',
    [string]$Search = '',
    [switch]$AllUsers,
    [Guid[]]$AccountIds = @(),
    [string]$Title = '',
    [string]$Message = '',
    [int]$Tickets = 0,
    [long]$ResourceGold = 0,
    [string]$Reason = '',
    [string]$ExpiresAt = '',
    [string]$RequestFile = '',
    [Guid]$RequestId = [Guid]::Empty,
    [string]$SettingsFile = '',
    [switch]$UseEnvironmentDatabase
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$project = Join-Path $projectRoot 'Server\Project333.GiftAdmin\Project333.GiftAdmin.csproj'
$artifacts = Join-Path $projectRoot 'Builds\Tools\GiftAdminArtifacts'
$private = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'After333\Admin'
New-Item -ItemType Directory -Force -Path $private | Out-Null
$buildLog = Join-Path $private 'gift-admin-build.log'
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
& $dotnet build $project -c Release --artifacts-path $artifacts --nologo -v quiet *> $buildLog
if ($LASTEXITCODE -ne 0) { throw "Administrator tool build failed. See $buildLog" }
$dll = Join-Path $artifacts 'bin\Project333.GiftAdmin\release\Project333.GiftAdmin.dll'
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw 'Built administrator tool was not found.' }
$priorConnection = [Environment]::GetEnvironmentVariable('PROJECT333_DB_CONNECTION','Process')
$priorOutputEncoding = $OutputEncoding
$priorConsoleEncoding = [Console]::OutputEncoding
try {
    if (-not $UseEnvironmentDatabase) {
        if ([string]::IsNullOrWhiteSpace($SettingsFile)) {
            $SettingsFile = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'After333\Server\DeploymentSettings-20260909.clixml'
        }
        if (-not (Test-Path -LiteralPath $SettingsFile -PathType Leaf)) { throw 'Saved server settings were not found. Specify -SettingsFile or use -UseEnvironmentDatabase with the server DB environment variable.' }
        $vault = Import-Clixml -LiteralPath $SettingsFile
        if ($vault -isnot [Collections.IDictionary] -or $vault['PROJECT333_DB_CONNECTION'] -isnot [Security.SecureString]) { throw 'Saved settings must contain a DPAPI-encrypted database connection.' }
        $connection = [Net.NetworkCredential]::new('', $vault['PROJECT333_DB_CONNECTION']).Password
        [Environment]::SetEnvironmentVariable('PROJECT333_DB_CONNECTION',$connection,'Process')
    }
    if ([string]::IsNullOrWhiteSpace($env:PROJECT333_DB_CONNECTION)) { throw 'The database connection is not configured.' }
    $OutputEncoding = [Text.UTF8Encoding]::new($false)
    [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
    $arguments = @($Mode.ToLowerInvariant())
    $inputJson = '{}'
    switch ($Mode) {
        'Accounts' { $inputJson = @{Search=$Search} | ConvertTo-Json -Compress }
        'Prepare' {
            if ($AllUsers.IsPresent -eq ($AccountIds.Count -gt 0)) { throw 'Choose exactly one: -AllUsers or -AccountIds.' }
            if ($RequestId -eq [Guid]::Empty) { $RequestId = [Guid]::NewGuid() }
            if ([string]::IsNullOrWhiteSpace($RequestFile)) {
                $RequestFile = Join-Path $private ('GiftRequests\gift-'+$RequestId.ToString('D')+'.json')
            }
            $expiry = $null
            if (-not [string]::IsNullOrWhiteSpace($ExpiresAt)) {
                $expiry = [DateTimeOffset]::Parse($ExpiresAt,[Globalization.CultureInfo]::InvariantCulture).ToUniversalTime().ToString('O')
            }
            $inputJson = [ordered]@{RequestId=$RequestId.ToString('D');Title=$Title;Message=$Message;Tickets=$Tickets;ResourceGold=$ResourceGold;AllExistingAccounts=$AllUsers.IsPresent;AccountIds=@($AccountIds|ForEach-Object {$_.ToString('D')});ExpiresAtUtc=$expiry;Reason=$Reason} | ConvertTo-Json -Depth 5 -Compress
            $arguments += [IO.Path]::GetFullPath($RequestFile)
        }
        default {
            if ([string]::IsNullOrWhiteSpace($RequestFile)) { throw '-RequestFile is required for Preview and Send.' }
            $arguments += (Resolve-Path -LiteralPath $RequestFile).Path
        }
    }
    $output = $inputJson | & $dotnet $dll @arguments
    $code = $LASTEXITCODE
    $output | Write-Output
    if ($code -ne 0) { throw 'Administrator operation did not complete. Keep the same request file for a retry.' }
} finally {
    [Environment]::SetEnvironmentVariable('PROJECT333_DB_CONNECTION',$priorConnection,'Process')
    $OutputEncoding = $priorOutputEncoding
    [Console]::OutputEncoding = $priorConsoleEncoding
    $connection = $null
    $vault = $null
}