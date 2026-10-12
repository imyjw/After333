param([string]$OutputDirectory='TempBuild/DrainedStatusVfx_20261001/Export')
$ErrorActionPreference='Stop'
$drainedTools=if([string]::IsNullOrWhiteSpace($PSScriptRoot)){'C:/Project_333/Tools/Art'}else{$PSScriptRoot}
$drainedRoot=[IO.Path]::GetFullPath((Join-Path $drainedTools '../..'))
$drainedOutput=if([IO.Path]::IsPathRooted($OutputDirectory)){[IO.Path]::GetFullPath($OutputDirectory)}else{[IO.Path]::GetFullPath((Join-Path $drainedRoot $OutputDirectory))}
$drainedAssets=[IO.Path]::GetFullPath((Join-Path $drainedRoot 'Assets'))
if($drainedOutput.Equals($drainedAssets,[StringComparison]::OrdinalIgnoreCase)-or$drainedOutput.StartsWith($drainedAssets+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Export must remain outside Assets.'}
$drainedSource=Join-Path $drainedTools 'Source/DrainedStatusVfx/ElectricArcs-Generated-20261001.png'
$drainedHash=(Get-FileHash -LiteralPath $drainedSource -Algorithm SHA256).Hash
if(-not('PackDrainedStatusVfx'-as[type])){Add-Type -Path (Join-Path $drainedTools 'PackDrainedStatusVfx.cs') -ReferencedAssemblies System.Drawing}
$drainedReport=[PackDrainedStatusVfx]::Run($drainedSource,$drainedOutput)
if((Get-FileHash -LiteralPath $drainedSource -Algorithm SHA256).Hash-ne$drainedHash){throw 'Source image changed.'}
$drainedReport|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $drainedOutput 'packing-report.json') -Encoding UTF8
$drainedFiles=Get-ChildItem -LiteralPath $drainedOutput -Filter '*.png' -Recurse|ForEach-Object{[pscustomobject]@{Path=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}}
[pscustomobject]@{Source=$drainedSource;SourceSHA256=$drainedHash;Outputs=$drainedFiles;Report=$drainedReport}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $drainedOutput 'export-manifest.json') -Encoding UTF8
Write-Output ('Packed8 fixed-center512px Drained frames with one scale '+$drainedReport.UniformScale+'; all margins>=16; center96px transparent: '+$drainedOutput)
