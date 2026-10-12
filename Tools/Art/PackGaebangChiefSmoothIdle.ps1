param(
    [string]$Source,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if ([string]::IsNullOrWhiteSpace($Source)) {
    $Source = Join-Path $projectRoot 'Tools/Art/Source/GaebangChief/Idle-Generated-20261002.png'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot 'TempBuild/GaebangSmoothIdle_20261002/Repacked'
}
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'TempBuild')).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $resolvedOutput.StartsWith($allowedRoot,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Output must be inside this project TempBuild. Active Assets cannot be overwritten by this packer.'
}
if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) { throw 'Generated source image is missing.' }
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'PackGaebangChiefSmoothIdle.cs'))) -ReferencedAssemblies System.Drawing,System.Core
$report = [PackSmoothIdle]::Run([IO.Path]::GetFullPath($Source),$resolvedOutput)
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $resolvedOutput 'packing-report.json') -Encoding UTF8
Write-Output ('Packed {0} unique frames: {1}' -f $report.UniqueFrames,$report.Output)
