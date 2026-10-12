[CmdletBinding()]
param(
    [string]$OutputDirectory = '',
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$resourceDirectory = Join-Path $projectRoot 'Tools\Art\Source\BlueDragon'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $projectRoot 'TempBuild\BlueDragon512Export' }
Add-Type -AssemblyName System.Drawing
if (-not ('BlueDragonSpriteSheetExporter' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'BlueDragonSpriteSheetExporter.cs') -ReferencedAssemblies System.Drawing
}
[BlueDragonSpriteSheetExporter]::Export((Join-Path $resourceDirectory 'DragonAtlas.png'), $OutputDirectory, [bool]$Force)
Get-ChildItem -LiteralPath $OutputDirectory -File | Where-Object { $_.Name -in @('Idle.png', 'Attack.png', 'BeAttacked.png', 'Run.png', 'Death.png') } | Select-Object Name, Length