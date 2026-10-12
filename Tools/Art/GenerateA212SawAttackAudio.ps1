# Legacy synthesized cue exporter. Current game audio uses PrepareA212SawAttackFromRecording.py.
param(
    [string]$ProjectRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [string]$OutputDirectory = ''
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $ProjectRoot 'TempBuild/A212SynthesizedAudio'
}
$outputPath = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar)
$assetsPath = [IO.Path]::GetFullPath((Join-Path $ProjectRoot 'Assets')).TrimEnd([IO.Path]::DirectorySeparatorChar)
if ($outputPath.Equals($assetsPath, [StringComparison]::OrdinalIgnoreCase) -or $outputPath.StartsWith($assetsPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Export outside Assets to preserve current artist edits and recording-based audio.'
}
if (-not ('A212SawAttackAudio' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'A212SawAttackAudio.cs')
}
$report = [A212SawAttackAudio]::Generate($outputPath)
$utf8 = New-Object System.Text.UTF8Encoding($false)
$reportPath = Join-Path $outputPath 'A212SawAttackSynthesizedValidation.json'
[IO.File]::WriteAllText($reportPath, ($report | ConvertFrom-Json | ConvertTo-Json -Depth 4) + [Environment]::NewLine, $utf8)
# This exporter does not write Unity metadata or replace the active SawAttack.wav.
$report | ConvertFrom-Json | Format-List
