param([string]$Source='Source/ManaShield/Formation-Generated-20261001.png',[string]$OutputDirectory='TempBuild/ManaShield_20261001/Export')
$ErrorActionPreference='Stop'
$taskTools=$PSScriptRoot
$taskRoot=[IO.Path]::GetFullPath((Join-Path $taskTools '../..'))
$taskSource=if([IO.Path]::IsPathRooted($Source)){$Source}else{Join-Path $taskTools $Source}
$taskOutput=if([IO.Path]::IsPathRooted($OutputDirectory)){$OutputDirectory}else{Join-Path $taskRoot $OutputDirectory}
$taskBefore=(Get-FileHash -LiteralPath $taskSource -Algorithm SHA256).Hash
Add-Type -Path (Join-Path $taskTools 'PackManaShieldSprites.cs') -ReferencedAssemblies System.Drawing
$taskReport=[PackManaShieldSprites]::Run($taskSource,$taskOutput)
if((Get-FileHash -LiteralPath $taskSource -Algorithm SHA256).Hash-ne$taskBefore){throw 'Generated source changed.'}
[IO.File]::WriteAllText((Join-Path $taskOutput 'packing-report.json'),($taskReport|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
Write-Output ('Packed eight 512px frames, single-frame portrait, contact sheet: '+$taskOutput)
