param([Parameter(Mandatory=$true)][string]$ValidationProject,[string]$ExportDirectory='TempBuild/PlasmaShield_20261001/Export')
# Root alone launches/runs Unity. Copy only new/identical approved source bytes, never replace metadata.
$ErrorActionPreference='Stop'
$shieldTools=if([string]::IsNullOrWhiteSpace($PSScriptRoot)){'C:/Project_333/Tools/Art'}else{$PSScriptRoot}
$shieldRoot=[IO.Path]::GetFullPath((Join-Path $shieldTools '../..'))
$shieldValidation=[IO.Path]::GetFullPath($ValidationProject)
$shieldTemp=[IO.Path]::GetFullPath((Join-Path $shieldRoot 'TempBuild'))
if(-not$shieldValidation.StartsWith($shieldTemp+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or-not(Test-Path -LiteralPath (Join-Path $shieldValidation 'ProjectSettings/ProjectVersion.txt'))){throw 'Stage only inside an existing isolated TempBuild Unity project.'}
$shieldExport=if([IO.Path]::IsPathRooted($ExportDirectory)){[IO.Path]::GetFullPath($ExportDirectory)}else{[IO.Path]::GetFullPath((Join-Path $shieldRoot $ExportDirectory))}
function Copy-ShieldNewOrIdentical([string]$source,[string]$target){
    if(-not(Test-Path -LiteralPath $source)){throw ('Missing reviewed PlasmaShield input: '+$source)}
    if(Test-Path -LiteralPath $target){if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash-ne(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash){throw ('Refuse different existing target: '+$target)};return}
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force|Out-Null
    Copy-Item -LiteralPath $source -Destination $target
}
$shieldPortrait=Join-Path $shieldExport 'PlasmaShieldPortrait.png'
if(-not(Test-Path -LiteralPath $shieldPortrait)){$shieldPortrait=Join-Path $shieldExport 'Resources/Project333/CardArtwork/PlasmaShield.png'}
Copy-ShieldNewOrIdentical $shieldPortrait (Join-Path $shieldValidation 'Assets/Project333/Resources/Project333/CardArtwork/PlasmaShield.png')
Copy-ShieldNewOrIdentical (Join-Path $shieldExport 'Resources/Project333/SpellEffects/PlasmaShield/Formation.png') (Join-Path $shieldValidation 'Assets/Project333/Resources/Project333/SpellEffects/PlasmaShield/Formation.png')
Copy-ShieldNewOrIdentical (Join-Path $shieldTools 'ImportPlasmaShieldRegistration.cs') (Join-Path $shieldValidation 'Assets/Editor/PlasmaShield/ImportPlasmaShieldRegistration.cs')
Write-Output ('Staged PlasmaShield portrait,8-frameVFX and native importer only: '+$shieldValidation+'; root must sync canonicalJSON/sharedrules first and invoke ImportPlasmaShieldRegistration.Batch.')