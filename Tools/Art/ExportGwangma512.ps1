[CmdletBinding()]
param(
    [string]$OutputDirectory = '',
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$resourceDirectory = Join-Path $projectRoot 'Tools\Art\Source\Gwangma'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $projectRoot 'TempBuild\Gwangma512Export' }
Add-Type -AssemblyName System.Drawing
if (-not ('GwangmaSpriteSheetExporter' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'GwangmaSpriteSheetExporter.cs') -ReferencedAssemblies System.Drawing
}
[GwangmaSpriteSheetExporter]::Export((Join-Path $resourceDirectory 'GwangmaAtlas.png'), $OutputDirectory, [bool]$Force)
Get-ChildItem -LiteralPath $OutputDirectory -File | Where-Object { $_.Name -in @('Idle.png', 'Attack.png', 'BeAttacked.png', 'Run.png', 'Death.png') } | Select-Object Name, Length