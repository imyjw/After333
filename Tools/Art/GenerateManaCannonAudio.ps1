param(
    [string]$ProjectRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [string]$OutputDirectory = '',
    [string]$ReportDirectory = ''
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $ProjectRoot 'TempBuild/ManaCannonPresentation_20261001/Export/Resources/Project333/Units/ManaCannon/Audio'
}
if ([string]::IsNullOrWhiteSpace($ReportDirectory)) {
    $ReportDirectory = Join-Path $ProjectRoot 'TempBuild/ManaCannonPresentation_20261001/Export/AudioReports'
}
$manaAssets = [IO.Path]::GetFullPath((Join-Path $ProjectRoot 'Assets')).TrimEnd('\','/')
foreach ($manaDirectory in @($OutputDirectory, $ReportDirectory)) {
    $manaResolved = [IO.Path]::GetFullPath($manaDirectory).TrimEnd('\','/')
    if ($manaResolved.Equals($manaAssets, [StringComparison]::OrdinalIgnoreCase) -or $manaResolved.StartsWith($manaAssets + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Authoring tool refuses output into project Assets.'
    }
}
$manaReportPath = Join-Path $ReportDirectory 'ManaCannonAudioGeneratorValidation.json'
if (Test-Path -LiteralPath $manaReportPath) { throw 'Choose a fresh report directory; existing report is preserved.' }
if (-not ('ManaCannonAudio' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'ManaCannonAudio.cs')
}
$manaReport = [ManaCannonAudio]::Generate($OutputDirectory) | ConvertFrom-Json
[IO.Directory]::CreateDirectory($ReportDirectory) | Out-Null
[IO.File]::WriteAllText($manaReportPath, ($manaReport | ConvertTo-Json -Depth 5) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
$manaReport | ConvertTo-Json -Depth 5
