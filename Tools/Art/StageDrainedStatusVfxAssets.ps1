param([Parameter(Mandatory=$true)][string]$ValidationProject,[string]$ExportDirectory='TempBuild/DrainedStatusVfx_20261001/Export')
# Stages only a reviewed new status atlas and native importer; never operates any Editor.
$ErrorActionPreference='Stop'
$drainedTools=if([string]::IsNullOrWhiteSpace($PSScriptRoot)){'C:/Project_333/Tools/Art'}else{$PSScriptRoot}
$drainedRoot=[IO.Path]::GetFullPath((Join-Path $drainedTools '../..'))
$drainedValidation=[IO.Path]::GetFullPath($ValidationProject)
$drainedTemp=[IO.Path]::GetFullPath((Join-Path $drainedRoot 'TempBuild'))
if(-not$drainedValidation.StartsWith($drainedTemp+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or-not(Test-Path -LiteralPath (Join-Path $drainedValidation 'ProjectSettings/ProjectVersion.txt'))){throw 'Stage only into an existing isolated TempBuild Unity project.'}
$drainedExport=if([IO.Path]::IsPathRooted($ExportDirectory)){[IO.Path]::GetFullPath($ExportDirectory)}else{[IO.Path]::GetFullPath((Join-Path $drainedRoot $ExportDirectory))}
function Copy-DrainedNewOrIdentical([string]$source,[string]$target){
    if(-not(Test-Path -LiteralPath $source)){throw ('Missing reviewed input '+$source)}
    if(Test-Path -LiteralPath $target){if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash-ne(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash){throw ('Refuse to overwrite different existing file '+$target)};return}
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force|Out-Null
    Copy-Item -LiteralPath $source -Destination $target
}
$drainedMedia='Resources/Project333/StatusEffects/Drained/ElectricArcs.png'
Copy-DrainedNewOrIdentical (Join-Path $drainedExport $drainedMedia) (Join-Path $drainedValidation ('Assets/Project333/'+$drainedMedia))
Copy-DrainedNewOrIdentical (Join-Path $drainedTools 'ImportDrainedStatusVfxAssets.cs') (Join-Path $drainedValidation 'Assets/Editor/DrainedStatusVfx/ImportDrainedStatusVfxAssets.cs')
Write-Output ('Staged status atlas/native importer only: '+$drainedValidation+'; root owns Refresh and ImportDrainedStatusVfxAssets.Run() twice.')
