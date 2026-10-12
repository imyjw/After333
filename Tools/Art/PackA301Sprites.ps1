param([string]$OutputDirectory = 'TempBuild/A301SpriteExport')
$ErrorActionPreference = 'Stop'
$a301ProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$a301Output = if ([IO.Path]::IsPathRooted($OutputDirectory)) { [IO.Path]::GetFullPath($OutputDirectory) } else { [IO.Path]::GetFullPath((Join-Path $a301ProjectRoot $OutputDirectory)) }
$a301Assets = [IO.Path]::GetFullPath((Join-Path $a301ProjectRoot 'Assets'))
if ($a301Output.Equals($a301Assets,[StringComparison]::OrdinalIgnoreCase) -or $a301Output.StartsWith($a301Assets+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Export outside Assets; this tool must not overwrite later artist edits.' }
if (-not ('PackA301Sprites' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'PackA301Sprites.cs') -ReferencedAssemblies System.Drawing }
$a301Sources = @('Idle','Attack','Death') | ForEach-Object { Join-Path $PSScriptRoot ('Source/A-301/'+$_+'-Generated-20260930.png') }
# Preserve a small flying bob relative to the fixed lowest hover envelope, including continuity
# at the last/first Idle frames. Lift values are an authored registration of the source motion;
# they intentionally differ, instead of setting each individual pose's bottom to 496.
$a301Lifts = [int[][]]@([int[]]@(0,2,2,2,8,6,1,0),[int[]]@(0,1,2,2,4,3,2,0),[int[]]@(16,14,12,8,4,0,0,0))
$a301Report = [PackA301Sprites]::Run($a301Sources,$a301Output,$a301Lifts)
$a301Report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $a301Output 'packing-report.json') -Encoding UTF8
Write-Output ('Exported Idle/Attack/Death as 8x512px with a shared scale to '+$a301Output)
