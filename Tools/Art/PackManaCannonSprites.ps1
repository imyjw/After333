param([string]$OutputDirectory='TempBuild/ManaCannonPresentation_20261001/Export')
$ErrorActionPreference='Stop'
$cannonToolRoot=if([string]::IsNullOrWhiteSpace($PSScriptRoot)){'C:/Project_333/Tools/Art'}else{$PSScriptRoot}
$cannonRoot=[IO.Path]::GetFullPath((Join-Path $cannonToolRoot '../..'))
$cannonOutput=if([IO.Path]::IsPathRooted($OutputDirectory)){[IO.Path]::GetFullPath($OutputDirectory)}else{[IO.Path]::GetFullPath((Join-Path $cannonRoot $OutputDirectory))}
$cannonAssets=[IO.Path]::GetFullPath((Join-Path $cannonRoot 'Assets'))
if($cannonOutput.Equals($cannonAssets,[StringComparison]::OrdinalIgnoreCase)-or $cannonOutput.StartsWith($cannonAssets+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Export must remain outside Assets.'}
if(-not('PackManaCannonSprites'-as[type])){Add-Type -Path (Join-Path $cannonToolRoot 'PackManaCannonSprites.cs') -ReferencedAssemblies System.Drawing}
$cannonSources=@('Idle','Attack','Death','ProjectileAtlas','ExplosionAtlas')|ForEach-Object{Join-Path $cannonToolRoot ('Source/ManaCannon/'+$_+'-Generated-20261001.png')}
$cannonBefore=$cannonSources|ForEach-Object{[pscustomobject]@{Path=$_;SHA256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash}}
$cannonReport=[PackManaCannonSprites]::Run([string[]]$cannonSources,$cannonOutput)
$cannonReport|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $cannonOutput 'packing-report.json') -Encoding UTF8
foreach($entry in $cannonBefore){if((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash-ne$entry.SHA256){throw ('Source changed: '+$entry.Path)}}
$cannonFiles=Get-ChildItem -LiteralPath $cannonOutput -Recurse -Filter '*.png'|ForEach-Object{[pscustomobject]@{Path=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}}
[pscustomobject]@{Sources=$cannonBefore;Outputs=$cannonFiles;Report=$cannonReport}|ConvertTo-Json -Depth 9|Set-Content -LiteralPath (Join-Path $cannonOutput 'export-manifest.json') -Encoding UTF8
Write-Output ('Complete sprites: '+$cannonOutput+'; uniform character scale='+$cannonReport.CharacterScale+'; 24 contact baselines=16px; 2x8 complete padded VFX frames.')

