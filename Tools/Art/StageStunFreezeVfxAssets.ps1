param([Parameter(Mandatory=$true)][string]$ValidationProject,[string]$ExportDirectory='TempBuild/StunFreezeVfx_20261001/Export')
# Stages only approved new textures and importer. Root alone operates the isolated Editor.
$ErrorActionPreference='Stop'
$actionVfxTools=if([string]::IsNullOrWhiteSpace($PSScriptRoot)){'C:/Project_333/Tools/Art'}else{$PSScriptRoot}
$actionVfxRoot=[IO.Path]::GetFullPath((Join-Path $actionVfxTools '../..'))
$actionVfxValidation=[IO.Path]::GetFullPath($ValidationProject)
$actionVfxTemp=[IO.Path]::GetFullPath((Join-Path $actionVfxRoot 'TempBuild'))
if(-not$actionVfxValidation.StartsWith($actionVfxTemp+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or-not(Test-Path -LiteralPath (Join-Path $actionVfxValidation 'ProjectSettings/ProjectVersion.txt'))){throw 'Stage only in an existing isolated TempBuild Unity project.'}
$actionVfxExport=if([IO.Path]::IsPathRooted($ExportDirectory)){[IO.Path]::GetFullPath($ExportDirectory)}else{[IO.Path]::GetFullPath((Join-Path $actionVfxRoot $ExportDirectory))}
function Copy-ActionVfxNewOrIdentical([string]$source,[string]$target){
    if(-not(Test-Path -LiteralPath $source)){throw ('Missing reviewed input '+$source)}
    if(Test-Path -LiteralPath $target){if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash-ne(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash){throw ('Refuse to overwrite different existing file '+$target)};return}
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force|Out-Null
    Copy-Item -LiteralPath $source -Destination $target
}
foreach($actionVfxMedia in @('Resources/Project333/StatusEffects/Stun/OrbitStars.png','Resources/Project333/StatusEffects/Freeze/IceCrystal.png')){
    Copy-ActionVfxNewOrIdentical (Join-Path $actionVfxExport $actionVfxMedia) (Join-Path $actionVfxValidation ('Assets/Project333/'+$actionVfxMedia))
}
Copy-ActionVfxNewOrIdentical (Join-Path $actionVfxTools 'ImportStunFreezeVfxAssets.cs') (Join-Path $actionVfxValidation 'Assets/Editor/StunFreezeVfx/ImportStunFreezeVfxAssets.cs')
Copy-ActionVfxNewOrIdentical (Join-Path $actionVfxRoot 'Assets/Project333/Editor/StatusSpriteBoundsEditor.cs') (Join-Path $actionVfxValidation 'Assets/Project333/Editor/StatusSpriteBoundsEditor.cs')
Write-Output ('Staged new Stun8/Freeze1 textures and native importer only: '+$actionVfxValidation+'; root owns Refresh/catalog/eval and Run twice.')
