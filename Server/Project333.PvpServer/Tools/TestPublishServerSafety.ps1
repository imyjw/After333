param(
    [string]$ReferenceOutput = (Join-Path $PSScriptRoot '..\..\..\Builds\Server\Releases\20260910-automatic-operation-recovery')
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$publisher = Join-Path $PSScriptRoot 'PublishServer_Release.ps1'
$testRoot = Join-Path $root ('Builds\Server\publish-safety-check-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testRoot)
$fixtureSource = Join-Path $testRoot 'fixture-source'
[void][IO.Directory]::CreateDirectory((Join-Path $fixtureSource 'Persistence\Migrations'))
Set-Content -LiteralPath (Join-Path $fixtureSource 'Fixture.csproj') -Value '<Project />'
Set-Content -LiteralPath (Join-Path $fixtureSource 'Persistence\Migrations\0001_fixture.sql') -Value 'SELECT 1;'
$project = Join-Path $fixtureSource 'Fixture.csproj'
$global:PublishCheckState = @{ Mode = 'success'; Calls = 0; Reference = $ReferenceOutput; Source = $fixtureSource }
$passes = 0
Add-Type -TypeDefinition 'using System.Runtime.InteropServices; using Microsoft.Win32.SafeHandles; public static class PublishDirectoryLock { [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern SafeFileHandle CreateFile(string name, uint access, uint share, System.IntPtr security, uint creation, uint flags, System.IntPtr template); }'

function Require([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Expect-Failure([scriptblock]$Action, [string]$Message) {
    try { & $Action | Out-Null }
    catch { return $_.Exception.Message }
    throw "Expected failure: $Message"
}
function Passed([string]$Message) { $script:passes++; Write-Host "PASS: $Message" }
function Invoke-FixturePublish([string]$Output, [switch]$Replace, [switch]$Preview) {
    & $publisher -ProjectPath $project -OutputPath $Output -CleanOutput:$Replace -PrintOnly:$Preview
}
function Folder-Hash([string]$Folder) {
    return ((Get-ChildItem -LiteralPath $Folder -File -Recurse -Force | Sort-Object FullName | ForEach-Object {
        $_.FullName.Substring($Folder.Length) + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash
    }) -join "`n")
}
# Exercise the real script with a fake compiler boundary. No server or DB is started.
function global:dotnet {
    if ($args[0] -eq '--version') { $global:LASTEXITCODE = 0; return 'synthetic-sdk' }
    if ($args[0] -ne 'publish') { throw 'Unexpected dotnet command.' }
    $state = $global:PublishCheckState
    $state.Calls++
    $stage = [string]$args[[Array]::IndexOf($args, '-o') + 1]
    $state.Stage = $stage
    foreach ($name in @('Project333.PvpServer.dll', 'Project333.PvpServer.deps.json', 'Project333.PvpServer.runtimeconfig.json', 'Data\cards.json', 'Data\pve_ai_decks.json')) {
        $destination = Join-Path $stage $name
        [void][IO.Directory]::CreateDirectory((Split-Path $destination))
        if ($state.Mode -eq 'missing' -and $name -eq 'Project333.PvpServer.runtimeconfig.json') { continue }
        Copy-Item -LiteralPath (Join-Path $state.Reference $name) -Destination $destination
    }
    [void][IO.Directory]::CreateDirectory((Join-Path $stage 'Persistence\Migrations'))
    if ($state.Mode -ne 'migration') {
        Copy-Item -LiteralPath (Join-Path $state.Source 'Persistence\Migrations\0001_fixture.sql') -Destination (Join-Path $stage 'Persistence\Migrations\0001_fixture.sql')
    }
    if ($state.Mode -eq 'json') { Set-Content -LiteralPath (Join-Path $stage 'Data\cards.json') -Value '{broken' }
    if ($state.Mode -eq 'dll') { Set-Content -LiteralPath (Join-Path $stage 'Project333.PvpServer.dll') -Value 'not an assembly' }
    $hiddenFile = Join-Path $stage '.fixture-hidden'
    [IO.File]::WriteAllText($hiddenFile, 'hidden artifact')
    [IO.File]::SetAttributes($hiddenFile, [IO.FileAttributes]::Hidden)
    if ($state.Mode -eq 'promotion') {
        $state.DirectoryLock = [PublishDirectoryLock]::CreateFile($stage, 2147483648, 3, [IntPtr]::Zero, 3, 0x02000000, [IntPtr]::Zero)
        Require (-not $state.DirectoryLock.IsInvalid) 'Could not lock staging directory for promotion failure test.'
    }
    $global:LASTEXITCODE = if ($state.Mode -eq 'failed') { 42 } else { 0 }
}
try {
    foreach ($bad in @($root, (Join-Path $root 'Builds\Server'), ($root + '-neighbor'), (Join-Path $root 'Assets\publish-output'), (Join-Path $root 'Builds\Server\..\outside'))) {
        $errorText = Expect-Failure { Invoke-FixturePublish $bad -Replace } 'unsafe path'
        Require ($errorText -like '*strictly below*') "Wrong path rejection: $errorText"
    }
    Require ($global:PublishCheckState.Calls -eq 0) 'Unsafe paths reached compiler.'
    Passed 'root, source, sibling-prefix and traversal output paths rejected before publishing'

    $previewParent = Join-Path $testRoot 'preview-parent'
    Invoke-FixturePublish (Join-Path $previewParent 'release') -Preview
    Require (-not (Test-Path -LiteralPath $previewParent)) 'PrintOnly wrote a directory.'
    Require ($global:PublishCheckState.Calls -eq 0) 'PrintOnly invoked compiler.'
    Passed 'PrintOnly performs no build or filesystem mutation'

    $target = Join-Path $testRoot 'release'
    $global:PublishCheckState.Mode = 'failed'
    $errorText = Expect-Failure { Invoke-FixturePublish $target } 'native failure'
    Require ($errorText -like '*exit code 42*') "Native failure code was lost: $errorText"
    Require (-not (Test-Path -LiteralPath $target)) 'Failed first publish promoted output.'
    Require (-not (Test-Path -LiteralPath (Join-Path $global:PublishCheckState.Stage 'project333_server_publish_manifest.json'))) 'Failed build got a success manifest.'
    Passed 'nonzero publish exit cannot create an output or success manifest even when DLL exists'

    $global:PublishCheckState.Mode = 'success'
    Invoke-FixturePublish $target
    $manifest = Get-Content -LiteralPath (Join-Path $target 'project333_server_publish_manifest.json') -Raw | ConvertFrom-Json
    Require ($manifest.SchemaVersion -eq 2 -and $manifest.BuildId -and $manifest.PublishExitCode -eq 0) 'Missing build identity.'
    Require ($manifest.PublishedFiles.Count -eq 7) 'Manifest does not cover all artifacts.'
    foreach ($file in $manifest.PublishedFiles) {
        Require ($file.Path.StartsWith($target + '\')) 'Manifest contains a staging path.'
        Require ((Get-FileHash -LiteralPath $file.Path).Hash -eq $file.Sha256) 'Artifact hash mismatch.'
    }
    Require ($manifest.Files.CardsJson -and $manifest.Files.ServerDll) 'Legacy manifest fields missing.'
    Passed 'successful build promotes fresh artifacts and records hashes/build id with final paths'

    $before = Folder-Hash $target
    foreach ($mode in @('failed', 'missing', 'json', 'dll', 'migration')) {
        $global:PublishCheckState.Mode = $mode
        $null = Expect-Failure { Invoke-FixturePublish $target -Replace } "invalid build $mode"
        Require ((Folder-Hash $target) -eq $before) "Existing output changed after $mode failure."
    }
    Passed 'old output and manifest survive compiler failure, missing file/migration, invalid JSON and invalid DLL'

    $global:PublishCheckState.Mode = 'success'
    $beforeCalls = $global:PublishCheckState.Calls
    $null = Expect-Failure { Invoke-FixturePublish $target } 'replacement without CleanOutput'
    Require ($global:PublishCheckState.Calls -eq $beforeCalls) 'Implicit replacement reached compiler.'
    Set-Content -LiteralPath (Join-Path $target 'stale-file.txt') -Value 'old-only'
    $oldWithStale = Folder-Hash $target
    Invoke-FixturePublish $target -Replace
    Require (-not (Test-Path -LiteralPath (Join-Path $target 'stale-file.txt'))) 'Old artifact leaked into fresh publish.'
    $backups = @(Get-ChildItem -LiteralPath $testRoot -Directory -Force | Where-Object Name -like '.release.backup-*')
    Require ($backups.Count -eq 1 -and (Folder-Hash $backups[0].FullName) -eq $oldWithStale) 'Previous output backup was not preserved exactly.'
    Passed 'explicit replacement retains exact previous build and excludes stale files'

    $before = Folder-Hash $target
    $global:PublishCheckState.Mode = 'promotion'
    try { $null = Expect-Failure { Invoke-FixturePublish $target -Replace } 'promotion rename failure' }
    finally { if ($global:PublishCheckState.DirectoryLock) { $global:PublishCheckState.DirectoryLock.Dispose() } }
    Require ((Folder-Hash $target) -eq $before) 'Failed promotion did not restore original output.'
    $remainingBackups = @(Get-ChildItem -LiteralPath $testRoot -Directory -Force | Where-Object Name -like '.release.backup-*')
    Require ($remainingBackups.Count -eq 1) 'Rollback left original output only in a backup path.'
    $global:PublishCheckState.Mode = 'success'
    Passed 'failed staging rename rolls the previous build back to its original output path'
    $other = Join-Path $testRoot 'not-a-release'
    [void][IO.Directory]::CreateDirectory($other)
    Set-Content -LiteralPath (Join-Path $other 'important.txt') -Value 'keep'
    $null = Expect-Failure { Invoke-FixturePublish $other -Replace } 'nonpublish folder'
    Require ((Get-Content -LiteralPath (Join-Path $other 'important.txt')) -eq 'keep') 'Nonpublish folder changed.'
    Passed 'nonempty unrelated output directory is not replaced'

    $lockPath = Join-Path $testRoot '.release.publish.lock'
    $lock = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $beforeCalls = $global:PublishCheckState.Calls
    try { $null = Expect-Failure { Invoke-FixturePublish $target -Replace } 'concurrent publish' }
    finally { $lock.Dispose() }
    Require ($global:PublishCheckState.Calls -eq $beforeCalls) 'Concurrent publish reached compiler.'
    Passed 'same-output lock prevents concurrent publication'

    $link = Join-Path $testRoot 'linked-output'
    New-Item -ItemType Junction -Path $link -Target $other | Out-Null
    $errorText = Expect-Failure { Invoke-FixturePublish (Join-Path $link 'child') -Replace } 'junction escape'
    Require ($errorText -like '*link/junction*') "Junction was not rejected: $errorText"
    Require (-not (Test-Path -LiteralPath (Join-Path $other 'child'))) 'Publish traversed junction.'
    Passed 'junction ancestors are rejected before output creation'

    Write-Host "PASS: $passes publish safety groups; fixtures retained at $testRoot"
}
finally {
    Remove-Item -LiteralPath Function:\dotnet
    Remove-Variable PublishCheckState -Scope Global
}