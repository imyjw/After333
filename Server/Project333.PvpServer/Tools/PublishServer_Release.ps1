param(
    [string]$ProjectPath = (Join-Path $PSScriptRoot '..\Project333.PvpServer.csproj'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\..\..\Builds\Server\Project333.PvpServer'),
    [string]$Configuration = 'Release',
    [string]$ClientVersion = '0.1.0-dev',
    [string]$Notes = '',
    [switch]$CleanOutput,
    [switch]$PrintOnly
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$outputRoot = Join-Path $projectRoot 'Builds\Server'

function Assert-PublishPath {
    param([string]$Path)
    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    $prefix = $outputRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Publish output must be strictly below $outputRoot : $fullPath"
    }
    # Reject Win32 aliases and alternate streams; all filesystem operations use literal paths.
    foreach ($part in $fullPath.Substring([IO.Path]::GetPathRoot($fullPath).Length).Split([char[]]'\/')) {
        if ($part.EndsWith('.') -or $part.EndsWith(' ') -or $part.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) {
            throw "Invalid publish path segment: $part"
        }
    }
    $cursor = $fullPath
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Publish paths cannot pass through a link/junction: $cursor"
            }
            if (-not $item.PSIsContainer) { throw "Publish path is not a directory: $cursor" }
        }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
    }
    return $fullPath
}

function Get-PublishGit {
    param([string[]]$Arguments)
    try {
        $value = & git -C $projectRoot @Arguments 2>$null
        if ($LASTEXITCODE -ne 0) { return $null }
        return (($value | Out-String).Trim())
    }
    catch { return $null }
}

function Get-PublishFile {
    param([string]$FilePath, [string]$StagePath, [string]$FinalPath)
    $file = Get-Item -LiteralPath $FilePath -Force
    $relative = $file.FullName.Substring($StagePath.Length + 1)
    return [pscustomobject]@{
        Path = Join-Path $FinalPath $relative
        RelativePath = $relative.Replace('\', '/')
        Length = $file.Length
        LastWriteTimeUtc = $file.LastWriteTimeUtc.ToString('O')
        Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
}

function Assert-PublishArtifacts {
    param([string]$StagePath)
    $required = @('Project333.PvpServer.dll', 'Project333.PvpServer.deps.json',
        'Project333.PvpServer.runtimeconfig.json', 'Data\cards.json', 'Data\pve_ai_decks.json')
    foreach ($relative in $required) {
        $path = Join-Path $StagePath $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
            throw "Required published file is missing or empty: $relative"
        }
    }
    # Do not traverse generated links while hashing the artifact tree.
    $pending = [Collections.Generic.Queue[string]]::new()
    $pending.Enqueue($StagePath)
    while ($pending.Count -gt 0) {
        foreach ($item in Get-ChildItem -LiteralPath $pending.Dequeue() -Force) {
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Published artifacts cannot contain links: $($item.FullName)"
            }
            if ($item.PSIsContainer) { $pending.Enqueue($item.FullName) }
        }
    }
    [void][Reflection.AssemblyName]::GetAssemblyName((Join-Path $StagePath 'Project333.PvpServer.dll'))
    foreach ($relative in $required | Where-Object { $_.EndsWith('.json') }) {
        $parsed = Get-Content -LiteralPath (Join-Path $StagePath $relative) -Raw | ConvertFrom-Json
        if ($null -eq $parsed) { throw "Published JSON is null: $relative" }
    }
    $sourceMigrations = @(Get-ChildItem -LiteralPath (Join-Path (Split-Path $ProjectPath) 'Persistence\Migrations') -Filter '*.sql' -File)
    if ($sourceMigrations.Count -eq 0) { throw 'No source migrations were found.' }
    foreach ($migration in $sourceMigrations) {
        $published = Join-Path $StagePath "Persistence\Migrations\$($migration.Name)"
        if (-not (Test-Path -LiteralPath $published -PathType Leaf) -or
            (Get-FileHash -LiteralPath $published).Hash -ne (Get-FileHash -LiteralPath $migration.FullName).Hash) {
            throw "Published migration missing or changed: $($migration.Name)"
        }
    }
}

$ProjectPath = [IO.Path]::GetFullPath($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ProjectPath))
if (-not (Test-Path -LiteralPath $ProjectPath -PathType Leaf)) { throw "Project file was not found: $ProjectPath" }
$OutputPath = Assert-PublishPath $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
$parentPath = Split-Path $OutputPath
$leaf = Split-Path $OutputPath -Leaf
if ((Test-Path -LiteralPath $OutputPath) -and -not $CleanOutput) {
    throw 'Output already exists. Use a new release directory or -CleanOutput to replace it with a retained backup.'
}
if ($PrintOnly) {
    Write-Host "[After333] Project: $ProjectPath"
    Write-Host "[After333] Output: $OutputPath; configuration=$Configuration; client=$ClientVersion"
    Write-Host '[After333] PrintOnly: no build, directory creation, cleanup, or manifest writes.'
    return
}

# FileShare.None prevents two invocations from publishing into the same output.
# Keep the empty lock file after releasing its handle to avoid an unlink/reopen race.
[void][IO.Directory]::CreateDirectory($parentPath)
$null = Assert-PublishPath $OutputPath
$lockPath = Join-Path $parentPath ".$leaf.publish.lock"
$publishLock = $null
$stagePath = $null
$backupPath = $null
try {
    $publishLock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $null = Assert-PublishPath $OutputPath
    if (Test-Path -LiteralPath $OutputPath) {
        if (-not $CleanOutput) { throw 'Output appeared while acquiring the publish lock.' }
        $contents = @(Get-ChildItem -LiteralPath $OutputPath -Force)
        if ($contents.Count -gt 0 -and -not (Test-Path -LiteralPath (Join-Path $OutputPath 'Project333.PvpServer.dll') -PathType Leaf)) {
            throw 'Refusing to replace a nonempty directory that is not a server publish output.'
        }
    }
    $buildId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N')
    $stagePath = Assert-PublishPath (Join-Path $parentPath ".$leaf.staging-$buildId")
    [void][IO.Directory]::CreateDirectory($stagePath)
    $gitStatus = Get-PublishGit @('status', '--porcelain', '--untracked-files=normal')
    $gitInfo = [pscustomobject]@{
        Branch = Get-PublishGit @('branch', '--show-current')
        Commit = Get-PublishGit @('rev-parse', 'HEAD')
        HasUncommittedChanges = if ($null -eq $gitStatus) { $null } else { -not [string]::IsNullOrWhiteSpace($gitStatus) }
    }
    $sdk = & dotnet --version
    if ($LASTEXITCODE -ne 0) { throw "dotnet --version failed: $LASTEXITCODE" }
    Write-Host "[After333] Building $buildId into $stagePath"
    & dotnet publish $ProjectPath -c $Configuration -o $stagePath
    $publishExitCode = $LASTEXITCODE
    if ($publishExitCode -ne 0) { throw "dotnet publish failed with exit code $publishExitCode. Existing output is unchanged." }
    $null = Assert-PublishPath $stagePath
    Assert-PublishArtifacts $stagePath
    $files = @(Get-ChildItem -LiteralPath $stagePath -File -Recurse -Force | Sort-Object FullName | ForEach-Object {
        Get-PublishFile $_.FullName $stagePath $OutputPath
    })
    $manifest = [pscustomobject]@{
        SchemaVersion = 2
        BuildId = $buildId
        CreatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        ProjectRoot = $projectRoot
        ProjectPath = $ProjectPath
        OutputPath = $OutputPath
        Configuration = $Configuration
        ClientVersion = $ClientVersion
        Notes = $Notes
        DotNetSdkVersion = ($sdk | Out-String).Trim()
        PublishExitCode = $publishExitCode
        ServerAssemblyVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $stagePath 'Project333.PvpServer.dll')).Version.ToString()
        Git = $gitInfo
        Files = [pscustomobject]@{
            ServerDll = $files | Where-Object { $_.RelativePath -eq 'Project333.PvpServer.dll' }
            CardsJson = $files | Where-Object { $_.RelativePath -eq 'Data/cards.json' }
        }
        PublishedFiles = $files
    }
    $manifest | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath (Join-Path $stagePath 'project333_server_publish_manifest.json') -Encoding UTF8

    # Renames stay on the same volume. Never delete the last working build.
    $null = Assert-PublishPath $stagePath
    $null = Assert-PublishPath $OutputPath
    if (Test-Path -LiteralPath $OutputPath) {
        $backupPath = Assert-PublishPath (Join-Path $parentPath ".$leaf.backup-$buildId")
        [IO.Directory]::Move($OutputPath, $backupPath)
    }
    try { [IO.Directory]::Move($stagePath, $OutputPath) }
    catch {
        if ($backupPath -and -not (Test-Path -LiteralPath $OutputPath)) {
            $null = Assert-PublishPath $backupPath
            $null = Assert-PublishPath $OutputPath
            [IO.Directory]::Move($backupPath, $OutputPath)
        }
        throw
    }
    Write-Host "[After333] Publish complete: $OutputPath"
    Write-Host "[After333] BuildId: $buildId; manifest covers $($files.Count) files."
    if ($backupPath) { Write-Host "[After333] Previous build retained: $backupPath" }
    Write-Host '[After333] The running server has not been started or restarted.'
}
catch {
    if ($stagePath -and (Test-Path -LiteralPath $stagePath)) {
        Write-Warning "Unpromoted build retained for diagnosis: $stagePath"
    }
    throw
}
finally { if ($publishLock) { $publishLock.Dispose() } }