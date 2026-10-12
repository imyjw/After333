param(
    [Parameter(Mandatory=$true)][string]$ValidationProject,
    [string]$ExportDirectory='TempBuild/ManaCannonPresentation_20261001/Export'
)
# Stages only this new card's media and native importer. Never launches/stops an Editor.
$ErrorActionPreference='Stop'
$cannonToolRoot=if([string]::IsNullOrWhiteSpace($PSScriptRoot)){'C:/Project_333/Tools/Art'}else{$PSScriptRoot}
$cannonRoot=[IO.Path]::GetFullPath((Join-Path $cannonToolRoot '../..'))
$cannonValidation=[IO.Path]::GetFullPath($ValidationProject)
$cannonTemp=[IO.Path]::GetFullPath((Join-Path $cannonRoot 'TempBuild'))
if(-not $cannonValidation.StartsWith($cannonTemp+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Use an isolated validation project below C:/Project_333/TempBuild.'}
if(-not(Test-Path -LiteralPath (Join-Path $cannonValidation 'ProjectSettings/ProjectVersion.txt'))){throw 'Existing isolated Unity project required.'}
$cannonExport=if([IO.Path]::IsPathRooted($ExportDirectory)){[IO.Path]::GetFullPath($ExportDirectory)}else{[IO.Path]::GetFullPath((Join-Path $cannonRoot $ExportDirectory))}
function Copy-CannonMedia([string]$from,[string]$relative){
    $target=[IO.Path]::GetFullPath((Join-Path $cannonValidation $relative))
    if(-not $target.StartsWith($cannonValidation+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Stage target escaped isolated project.'}
    if(-not(Test-Path -LiteralPath $from)){throw ('Missing reviewed export '+$from)}
    $sha=(Get-FileHash -LiteralPath $from -Algorithm SHA256).Hash
    if(Test-Path -LiteralPath $target){if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash-ne$sha){throw ('Refusing to overwrite different staged art/source '+$target)}}
    else{New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force|Out-Null;Copy-Item -LiteralPath $from -Destination $target}
    [pscustomobject]@{Path=$relative;SHA256=$sha}
}
$cannonPortrait=Join-Path $cannonRoot 'Assets/Project333/Resources/Project333/CardArtwork/ManaCannon.png'
if(-not(Test-Path -LiteralPath $cannonPortrait)){$cannonPortrait=Join-Path $cannonExport 'Resources/Project333/CardArtwork/ManaCannon.png'}
$cannonStaged=@(
    foreach($motion in @('Idle','Attack','Death')){Copy-CannonMedia (Join-Path $cannonExport ('Unit/ManaCannon/'+$motion+'.png')) ('Assets/Unit/ManaCannon/'+$motion+'.png')}
    foreach($atlas in @('ProjectileAtlas','ExplosionAtlas')){Copy-CannonMedia (Join-Path $cannonExport ('Resources/Project333/Units/ManaCannon/'+$atlas+'.png')) ('Assets/Project333/Resources/Project333/Units/ManaCannon/'+$atlas+'.png')}
    foreach($sound in @('ManaShot','ManaExplosion')){Copy-CannonMedia (Join-Path $cannonExport ('Resources/Project333/Units/ManaCannon/Audio/'+$sound+'.wav')) ('Assets/Project333/Resources/Project333/Units/ManaCannon/Audio/'+$sound+'.wav')}
    Copy-CannonMedia $cannonPortrait 'Assets/Project333/Resources/Project333/CardArtwork/ManaCannon.png'
    Copy-CannonMedia (Join-Path $cannonToolRoot 'ImportManaCannonPresentationAssets.cs') 'Assets/Editor/ManaCannonPresentation/ImportManaCannonPresentationAssets.cs'
)
$cannonStaged|ConvertTo-Json -Depth 5
Write-Output 'Root: after native synchronous Refresh/compile, eval ImportManaCannonPresentationAssets.Run(); repeat Run() to validate retained GUID/fileIDs and no duplicate catalog entry. This wrapper did not operate any Editor.'