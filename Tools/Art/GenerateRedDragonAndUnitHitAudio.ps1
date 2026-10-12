param([string]$ProjectRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$ErrorActionPreference = 'Stop'
# Writes audio only. Unity creates metadata; importer settings are applied through its API.
if (-not ('RedDragonAndUnitHitAudio' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'RedDragonAndUnitHitAudio.cs')
}
$report = [RedDragonAndUnitHitAudio]::Generate((Join-Path $ProjectRoot 'Assets/Project333/Resources/Project333/Audio'))
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'RedDragonAndUnitHitAudioValidation.json'),
    ($report | ConvertFrom-Json | ConvertTo-Json -Depth 4) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
$report | ConvertFrom-Json | Select-Object -ExpandProperty clips | Format-Table -AutoSize