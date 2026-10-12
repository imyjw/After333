param([string]$OutputDirectory='TempBuild/StunFreezeVfx_20261001/Export',[string]$StunSource='Source/StunFreezeStatusVfx/OrbitStars-Generated-20261001.png',[string]$FreezeSource='Source/StunFreezeStatusVfx/IceCrystal-Generated-20261001.png',[int]$AlphaThreshold=16)
$ErrorActionPreference='Stop'
$actionVfxTools=if([string]::IsNullOrWhiteSpace($PSScriptRoot)){'C:/Project_333/Tools/Art'}else{$PSScriptRoot}
$actionVfxRoot=[IO.Path]::GetFullPath((Join-Path $actionVfxTools '../..'))
$actionVfxOutput=if([IO.Path]::IsPathRooted($OutputDirectory)){[IO.Path]::GetFullPath($OutputDirectory)}else{[IO.Path]::GetFullPath((Join-Path $actionVfxRoot $OutputDirectory))}
if(($actionVfxOutput-split '[\\/]')-contains'Assets'){throw 'Export requires a location outside Assets.'}
$actionVfxStun=if([IO.Path]::IsPathRooted($StunSource)){$StunSource}else{Join-Path $actionVfxTools $StunSource}
$actionVfxFreeze=if([IO.Path]::IsPathRooted($FreezeSource)){$FreezeSource}else{Join-Path $actionVfxTools $FreezeSource}
$actionVfxBefore=@(Get-FileHash -LiteralPath $actionVfxStun,$actionVfxFreeze -Algorithm SHA256)
if(-not('PackStunFreezeVfx'-as[type])){Add-Type -Path (Join-Path $actionVfxTools 'PackStunFreezeVfx.cs') -ReferencedAssemblies System.Drawing}
$actionVfxReport=[PackStunFreezeVfx]::Run($actionVfxStun,$actionVfxFreeze,$actionVfxOutput,$AlphaThreshold)
foreach($actionVfxInput in $actionVfxBefore){if((Get-FileHash -LiteralPath $actionVfxInput.Path -Algorithm SHA256).Hash-ne$actionVfxInput.Hash){throw 'Generated source changed during export.'}}
$actionVfxReport | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $actionVfxOutput 'packing-report.json') -Encoding UTF8
$actionVfxOutputs=@(Get-ChildItem -LiteralPath $actionVfxOutput -Recurse -File -Filter '*.png' | ForEach-Object{[pscustomobject]@{Path=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
[pscustomobject]@{Sources=$actionVfxBefore;Outputs=$actionVfxOutputs;Report=$actionVfxReport}|ConvertTo-Json -Depth 9|Set-Content -LiteralPath (Join-Path $actionVfxOutput 'export-manifest.json') -Encoding UTF8
Write-Output ('Packed Stun8/Freeze1 full512px cells, fixed native centers and >=16px margins: '+$actionVfxOutput)
