param([string]$OutputDirectory = 'TempBuild/A212SpriteExport')
$ErrorActionPreference = 'Stop'
$a212ProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$a212Output = if ([IO.Path]::IsPathRooted($OutputDirectory)) { [IO.Path]::GetFullPath($OutputDirectory) } else { [IO.Path]::GetFullPath((Join-Path $a212ProjectRoot $OutputDirectory)) }
$a212Assets = [IO.Path]::GetFullPath((Join-Path $a212ProjectRoot 'Assets'))
if ($a212Output.Equals($a212Assets,[StringComparison]::OrdinalIgnoreCase) -or $a212Output.StartsWith($a212Assets+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Export outside Assets; this tool must not overwrite later artist edits.' }
if (-not ('PackA212Sprites' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'PackA212Sprites.cs') -ReferencedAssemblies System.Drawing }
$a212Sources = @('Attack','Run','Death','SawImpactAtlas') | ForEach-Object { Join-Path $PSScriptRoot ('Source/A-212/'+$_+'-Generated-20260930.png') }
$a212Report = [PackA212Sprites]::Run($a212Sources,$a212Output)
$a212Report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $a212Output 'packing-report.json') -Encoding UTF8
Write-Output ('Exported 3 motion sheets (8x512px) + 4x2 impact atlas to '+$a212Output)
