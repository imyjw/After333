param([string]$OutputDirectory='TempBuild/GaebangChiefPresentation_20260930/Export')
$ErrorActionPreference='Stop'
$chiefToolRoot=if([string]::IsNullOrWhiteSpace($PSScriptRoot)){'C:/Project_333/Tools/Art'}else{$PSScriptRoot}
$chiefRoot=[IO.Path]::GetFullPath((Join-Path $chiefToolRoot '../..'))
$chiefOutput=if([IO.Path]::IsPathRooted($OutputDirectory)){[IO.Path]::GetFullPath($OutputDirectory)}else{[IO.Path]::GetFullPath((Join-Path $chiefRoot $OutputDirectory))}
$chiefAssets=[IO.Path]::GetFullPath((Join-Path $chiefRoot 'Assets'))
if($chiefOutput.Equals($chiefAssets,[StringComparison]::OrdinalIgnoreCase)-or $chiefOutput.StartsWith($chiefAssets+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Export must remain outside Assets.'}
if(-not('PackGaebangChiefSprites'-as[type])){Add-Type -Path (Join-Path $chiefToolRoot 'PackGaebangChiefSprites.cs') -ReferencedAssemblies System.Drawing}
$chiefSources=@('Idle','Attack','Run','Death','CoinImpactAtlas')|ForEach-Object{Join-Path $chiefToolRoot ('Source/GaebangChief/'+$_+'-Generated-20260930.png')}
$chiefBefore=$chiefSources|ForEach-Object{[pscustomobject]@{Path=$_;SHA256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash}}
$chiefReport=[PackGaebangChiefSprites]::Run([string[]]$chiefSources,$chiefOutput)
$chiefReport|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $chiefOutput 'packing-report.json') -Encoding UTF8
foreach($entry in $chiefBefore){if((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash-ne$entry.SHA256){throw ('Source changed: '+$entry.Path)}}
$chiefFiles=Get-ChildItem -LiteralPath $chiefOutput -Recurse -Filter '*.png'|ForEach-Object{[pscustomobject]@{Path=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}}
[pscustomobject]@{Sources=$chiefBefore;Outputs=$chiefFiles;Report=$chiefReport}|ConvertTo-Json -Depth 9|Set-Content -LiteralPath (Join-Path $chiefOutput 'export-manifest.json') -Encoding UTF8
Write-Output ('Complete sprites: '+$chiefOutput+'; uniform character scale='+$chiefReport.CharacterScale+'; 32 contact baselines=16px; 8 complete padded coin frames.')
