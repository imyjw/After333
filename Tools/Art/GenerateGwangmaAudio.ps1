param(
    [string]$ProjectRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
)
$ErrorActionPreference = 'Stop'
# Deterministic original sound design; no recordings, third-party libraries or downloads.
# Mono PCM one-shots are preloaded; existing Unity GUIDs are preserved on regeneration.
if (-not ('GwangmaAudio' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'GwangmaAudio.cs')
}
$audioPath = Join-Path $ProjectRoot 'Assets/Project333/Resources/Project333/Audio/Gwangma'
[System.IO.Directory]::CreateDirectory($audioPath) | Out-Null
$report = [GwangmaAudio]::Generate($audioPath)
$audioTemplate = @'
fileFormatVersion: 2
guid: {GUID}
AudioImporter:
  externalObjects: {}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 0
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {}
  forceToMono: 1
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData: Original procedural Gwangma audio
  assetBundleName:
  assetBundleVariant:
'@
$folderTemplate = @'
fileFormatVersion: 2
guid: {GUID}
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
'@
$utf8 = New-Object System.Text.UTF8Encoding($false)
foreach ($dir in @((Split-Path -Parent $audioPath), $audioPath)) {
    $meta = $dir + '.meta'
    if (-not (Test-Path -LiteralPath $meta)) {
        [System.IO.File]::WriteAllText($meta, $folderTemplate.Replace('{GUID}', [guid]::NewGuid().ToString('N')) + [Environment]::NewLine, $utf8)
    }
}
foreach ($file in Get-ChildItem -LiteralPath $audioPath -Filter '*.wav') {
    $meta = $file.FullName + '.meta'
    if (-not (Test-Path -LiteralPath $meta)) {
        [System.IO.File]::WriteAllText($meta, $audioTemplate.Replace('{GUID}', [guid]::NewGuid().ToString('N')) + [Environment]::NewLine, $utf8)
    }
}
$reportPath = Join-Path $PSScriptRoot 'GwangmaAudioValidation.json'
[System.IO.File]::WriteAllText($reportPath, ($report | ConvertFrom-Json | ConvertTo-Json -Depth 4) + [Environment]::NewLine, $utf8)
$report | ConvertFrom-Json | Select-Object -ExpandProperty clips | Format-Table -AutoSize
