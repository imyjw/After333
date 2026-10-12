param(
    [string]$ProjectRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [ValidateSet('All', 'Lobby', 'Battle')]
    [string]$Track = 'All'
)
$ErrorActionPreference = 'Stop'
# Original deterministic instrumental scores; every sound is synthesized locally.
if (-not ('After333Music' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'After333Music.cs')
}
$audioPath = Join-Path $ProjectRoot 'Assets/Project333/Resources/Project333/Audio/BGM'
[IO.Directory]::CreateDirectory($audioPath) | Out-Null
$report = [After333Music]::Generate($audioPath, $Track)
$generatedReport = $report | ConvertFrom-Json
$audioTemplate = @'
fileFormatVersion: 2
guid: {GUID}
AudioImporter:
  externalObjects: {}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 2
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.65
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {}
  forceToMono: 0
  normalize: 0
  loadInBackground: 1
  ambisonic: 0
  3D: 0
  userData: Original After333 instrumental score; Tools/Audio/After333Music.md
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
$utf8 = New-Object Text.UTF8Encoding($false)
foreach ($dir in @((Split-Path -Parent $audioPath), $audioPath)) {
    if (-not (Test-Path -LiteralPath ($dir + '.meta'))) {
        [IO.File]::WriteAllText($dir + '.meta', $folderTemplate.Replace('{GUID}', [guid]::NewGuid().ToString('N')) + [Environment]::NewLine, $utf8)
    }
}
foreach ($name in @($generatedReport.clips | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_.file) })) {
    $meta = Join-Path $audioPath ($name + '.wav.meta')
    $guid = if (Test-Path -LiteralPath $meta) { [regex]::Match([IO.File]::ReadAllText($meta), '(?m)^guid:\s*([a-f0-9]{32})').Groups[1].Value } else { [guid]::NewGuid().ToString('N') }
    if ([string]::IsNullOrEmpty($guid)) { throw "Invalid existing audio GUID: $meta" }
    [IO.File]::WriteAllText($meta, $audioTemplate.Replace('{GUID}', $guid) + [Environment]::NewLine, $utf8)
}
$reportPath = Join-Path $PSScriptRoot 'After333MusicRender.json'
if ($Track -ne 'All' -and (Test-Path -LiteralPath $reportPath)) {
    $previous = [IO.File]::ReadAllText($reportPath) | ConvertFrom-Json
    $updatedNames = @($generatedReport.clips | ForEach-Object { $_.file })
    $preserved = @($previous.clips | Where-Object { $_.file -notin $updatedNames })
    $generatedReport.clips = @(@($generatedReport.clips) + $preserved | Sort-Object file -Descending)
}
[IO.File]::WriteAllText($reportPath, ($generatedReport | ConvertTo-Json -Depth 4) + [Environment]::NewLine, $utf8)
$report | ConvertFrom-Json | Select-Object -ExpandProperty clips | Format-Table -AutoSize
