param(
    [Parameter(Mandatory=$true)][string]$ValidationProject,
    [string]$ExportDirectory='TempBuild/GaebangChiefPresentation_20260930/Export'
)
# Stages only this new card's media and native importer. Never launches/stops an Editor.
$ErrorActionPreference='Stop'
$chiefToolRoot=if([string]::IsNullOrWhiteSpace($PSScriptRoot)){'C:/Project_333/Tools/Art'}else{$PSScriptRoot}
$chiefRoot=[IO.Path]::GetFullPath((Join-Path $chiefToolRoot '../..'))
$chiefValidation=[IO.Path]::GetFullPath($ValidationProject)
$chiefTemp=[IO.Path]::GetFullPath((Join-Path $chiefRoot 'TempBuild'))
if(-not $chiefValidation.StartsWith($chiefTemp+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Use an isolated validation project below C:/Project_333/TempBuild.'}
if(-not(Test-Path -LiteralPath (Join-Path $chiefValidation 'ProjectSettings/ProjectVersion.txt'))){throw 'Existing isolated Unity project required.'}
$chiefExport=if([IO.Path]::IsPathRooted($ExportDirectory)){[IO.Path]::GetFullPath($ExportDirectory)}else{[IO.Path]::GetFullPath((Join-Path $chiefRoot $ExportDirectory))}
function Copy-ChiefMedia([string]$from,[string]$relative){
    $target=[IO.Path]::GetFullPath((Join-Path $chiefValidation $relative))
    if(-not $target.StartsWith($chiefValidation+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Stage target escaped isolated project.'}
    if(-not(Test-Path -LiteralPath $from)){throw ('Missing reviewed export '+$from)}
    $sha=(Get-FileHash -LiteralPath $from -Algorithm SHA256).Hash
    if(Test-Path -LiteralPath $target){if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash-ne$sha){throw ('Refusing to overwrite different staged art/source '+$target)}}
    else{New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force|Out-Null;Copy-Item -LiteralPath $from -Destination $target}
    [pscustomobject]@{Path=$relative;SHA256=$sha}
}
$chiefStaged=@(
    foreach($motion in @('Idle','Attack','Run','Death')){Copy-ChiefMedia (Join-Path $chiefExport ('Unit/GaebangChief/'+$motion+'.png')) ('Assets/Unit/GaebangChief/'+$motion+'.png')}
    Copy-ChiefMedia (Join-Path $chiefExport 'Resources/Project333/Units/GaebangChief/CoinImpactAtlas.png') 'Assets/Project333/Resources/Project333/Units/GaebangChief/CoinImpactAtlas.png'
    Copy-ChiefMedia (Join-Path $chiefExport 'Resources/Project333/Units/GaebangChief/Audio/CoinPunch.wav') 'Assets/Project333/Resources/Project333/Units/GaebangChief/Audio/CoinPunch.wav'
    Copy-ChiefMedia (Join-Path $chiefRoot 'Assets/Project333/Resources/Project333/CardArtwork/GaebangChief.png') 'Assets/Project333/Resources/Project333/CardArtwork/GaebangChief.png'
    Copy-ChiefMedia (Join-Path $chiefToolRoot 'ImportGaebangChiefPresentationAssets.cs') 'Assets/Editor/GaebangChiefPresentation/ImportGaebangChiefPresentationAssets.cs'
)
$chiefStaged|ConvertTo-Json -Depth 5
Write-Output 'Root: after native synchronous Refresh/compile, eval ImportGaebangChiefPresentationAssets.Run(); repeat Run() to validate retained GUID/fileIDs and no duplicate catalog entry. This wrapper did not operate any Editor.'
