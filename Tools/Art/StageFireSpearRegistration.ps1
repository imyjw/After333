param([Parameter(Mandatory=$true)][string]$ValidationProject,[string]$ExportDirectory='TempBuild/FireSpearPresentation_20261001/Export')
# Root alone launches/runs Unity. Copy only new/identical approved source bytes, never replace metadata.
$ErrorActionPreference='Stop'
$fireTools=if([string]::IsNullOrWhiteSpace($PSScriptRoot)){'C:/Project_333/Tools/Art'}else{$PSScriptRoot}
$fireRoot=[IO.Path]::GetFullPath((Join-Path $fireTools '../..'))
$fireValidation=[IO.Path]::GetFullPath($ValidationProject)
$fireTemp=[IO.Path]::GetFullPath((Join-Path $fireRoot 'TempBuild'))
if(-not$fireValidation.StartsWith($fireTemp+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or-not(Test-Path -LiteralPath (Join-Path $fireValidation 'ProjectSettings/ProjectVersion.txt'))){throw 'Stage only inside an existing isolated TempBuild Unity project.'}
$fireExport=if([IO.Path]::IsPathRooted($ExportDirectory)){[IO.Path]::GetFullPath($ExportDirectory)}else{[IO.Path]::GetFullPath((Join-Path $fireRoot $ExportDirectory))}
function Copy-FireNewOrIdentical([string]$source,[string]$target){
    if(-not(Test-Path -LiteralPath $source)){throw ('Missing reviewed FireSpear input: '+$source)}
    if(Test-Path -LiteralPath $target){if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash-ne(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash){throw ('Refuse different existing target: '+$target)};return}
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force|Out-Null
    Copy-Item -LiteralPath $source -Destination $target
}
$firePortrait=Join-Path $fireExport 'FireSpearPortrait.png'
if(-not(Test-Path -LiteralPath $firePortrait)){$firePortrait=Join-Path $fireExport 'Resources/Project333/CardArtwork/FireSpear.png'}
Copy-FireNewOrIdentical $firePortrait (Join-Path $fireValidation 'Assets/Project333/Resources/Project333/CardArtwork/FireSpear.png')
Copy-FireNewOrIdentical (Join-Path $fireExport 'Resources/Project333/SpellEffects/FireSpear/FlameSpear.png') (Join-Path $fireValidation 'Assets/Project333/Resources/Project333/SpellEffects/FireSpear/FlameSpear.png')
Copy-FireNewOrIdentical (Join-Path $fireTools 'ImportFireSpearRegistration.cs') (Join-Path $fireValidation 'Assets/Editor/FireSpear/ImportFireSpearRegistration.cs')
Write-Output ('Staged FireSpear portrait,8-frameVFX and native importer only: '+$fireValidation+'; root must sync canonicalJSON/sharedrules first and invoke ImportFireSpearRegistration.Batch.')