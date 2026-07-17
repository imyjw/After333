param(
    [string]$ProjectPath = "C:\Project_333\Server\Project333.PvpServer\Project333.PvpServer.csproj",
    [string]$OutputPath = "C:\Project_333\Builds\Server\Project333.PvpServer",
    [string]$Configuration = "Release",
    [string]$ClientVersion = "0.1.0-dev",
    [string]$Notes = "",
    [switch]$CleanOutput,
    [switch]$PrintOnly
)

$ErrorActionPreference = "Stop"

function Get-Project333FileManifest {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    $file = Get-Item -LiteralPath $Path
    return [pscustomobject]@{
        Path = $file.FullName
        Length = $file.Length
        LastWriteTimeUtc = $file.LastWriteTimeUtc.ToString("O")
        Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
}

function Invoke-Project333Git {
    param(
        [string]$ProjectRoot,
        [string[]]$Arguments
    )

    try {
        $output = & git -C $ProjectRoot @Arguments 2>$null
        if ($LASTEXITCODE -ne 0) {
            return ""
        }

        return (($output | Out-String).Trim())
    }
    catch {
        return ""
    }
}

function Test-Project333GitDirty {
    param([string]$ProjectRoot)

    try {
        & git -C $ProjectRoot diff --quiet 2>$null
        if ($LASTEXITCODE -ne 0) {
            return $true
        }

        & git -C $ProjectRoot diff --cached --quiet 2>$null
        return ($LASTEXITCODE -ne 0)
    }
    catch {
        return $false
    }
}

function New-Project333PublishManifest {
    param(
        [string]$ProjectRoot,
        [string]$ProjectPath,
        [string]$OutputPath,
        [string]$Configuration,
        [string]$ClientVersion,
        [string]$Notes
    )

    $publishedDll = Join-Path $OutputPath "Project333.PvpServer.dll"
    $cardsJson = Join-Path $OutputPath "Data\cards.json"
    $assemblyVersion = ""
    if (Test-Path -LiteralPath $publishedDll) {
        try {
            $assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($publishedDll).Version.ToString()
        }
        catch {
            $assemblyVersion = ""
        }
    }

    $gitCommit = Invoke-Project333Git $ProjectRoot @("rev-parse", "HEAD")
    $gitBranch = Invoke-Project333Git $ProjectRoot @("branch", "--show-current")
    $gitDirty = Test-Project333GitDirty $ProjectRoot

    return [pscustomobject]@{
        SchemaVersion = 1
        CreatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
        ProjectRoot = $ProjectRoot
        ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath)
        OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
        Configuration = $Configuration
        ClientVersion = $ClientVersion
        Notes = $Notes
        ServerAssemblyVersion = $assemblyVersion
        Git = [pscustomobject]@{
            Branch = $gitBranch
            Commit = $gitCommit
            HasUncommittedChanges = $gitDirty
        }
        Files = [pscustomobject]@{
            ServerDll = Get-Project333FileManifest $publishedDll
            CardsJson = Get-Project333FileManifest $cardsJson
        }
    }
}

if (-not (Test-Path -LiteralPath $ProjectPath)) {
    throw "Project file was not found: $ProjectPath"
}

if ($CleanOutput -and (Test-Path -LiteralPath $OutputPath)) {
    $resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
    $projectRoot = [System.IO.Path]::GetFullPath("C:\Project_333")
    if (-not $resolvedOutputPath.StartsWith($projectRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean output outside project root: $resolvedOutputPath"
    }

    Write-Host "[After333] Cleaning publish output: $resolvedOutputPath"
    if (-not $PrintOnly) {
        Remove-Item -LiteralPath $resolvedOutputPath -Recurse -Force
    }
}

$publishCommand = @(
    "dotnet",
    "publish",
    "`"$ProjectPath`"",
    "-c",
    $Configuration,
    "-o",
    "`"$OutputPath`""
) -join " "

Write-Host "[After333] Server publish settings"
Write-Host "  Project:       $ProjectPath"
Write-Host "  Configuration: $Configuration"
Write-Host "  Output:        $OutputPath"
Write-Host "  ClientVersion: $ClientVersion"
Write-Host "  Command:       $publishCommand"

if ($PrintOnly) {
    Write-Host "[After333] PrintOnly was set. Publish was not started."
    return
}

dotnet publish $ProjectPath -c $Configuration -o $OutputPath

$publishedDll = Join-Path $OutputPath "Project333.PvpServer.dll"
if (-not (Test-Path -LiteralPath $publishedDll)) {
    throw "Publish completed but server DLL was not found: $publishedDll"
}

Write-Host "[After333] Publish complete."
Write-Host "  Server DLL: $publishedDll"
$projectRoot = [System.IO.Path]::GetFullPath("C:\Project_333")
$manifest = New-Project333PublishManifest `
    -ProjectRoot $projectRoot `
    -ProjectPath $ProjectPath `
    -OutputPath $OutputPath `
    -Configuration $Configuration `
    -ClientVersion $ClientVersion `
    -Notes $Notes
$manifestPath = Join-Path $OutputPath "project333_server_publish_manifest.json"
$manifest | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Write-Host "  Manifest:   $manifestPath"
Write-Host "  Next step:  powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunPublishedServer_Production.ps1 -PublishedServerPath '$OutputPath' -DatabaseConnection '<connection-string>' -RequiredClientVersion '$ClientVersion'"
