param(
    [string]$ProjectRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [string]$OutputDirectory = '',
    [string]$ReportDirectory = ''
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $ProjectRoot 'TempBuild/GaebangChiefPresentation_20260930/Export/Resources/Project333/Units/GaebangChief/Audio'
}
if ([string]::IsNullOrWhiteSpace($ReportDirectory)) {
    $ReportDirectory = Join-Path $ProjectRoot 'TempBuild/GaebangChiefPresentation_20260930/Export/AudioReports'
}
if (-not ('GaebangChiefCoinPunchAudio' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'GaebangChiefCoinPunchAudio.cs')
}
$report = [GaebangChiefCoinPunchAudio]::Generate($OutputDirectory)
[System.IO.Directory]::CreateDirectory($ReportDirectory) | Out-Null
$reportPath = Join-Path $ReportDirectory 'GaebangChiefCoinPunchAudioValidation.json'
$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($reportPath, ($report | ConvertFrom-Json | ConvertTo-Json -Depth 4) + [Environment]::NewLine, $utf8)
# This authoring export does not write Assets, Unity metadata or runtime configuration.
$report | ConvertFrom-Json | Format-List