param(
    [string]$SourceDirectory = 'C:/Users/youndonghyeok/.codex/generated_images/01a08439-297d-79c0-a9dc-a0153e47e32b',
    [string]$OutputDirectory = 'C:/Project_333/TempBuild/RedDragonPresentation_20260927/Art',
    [string]$ArchiveDirectory = 'C:/Project_333/Tools/Art/Source/RedDragon'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'PackRedDragonSprites.cs') -ReferencedAssemblies System.Drawing,System.Core
$names = @('Idle','Attack','BeAttacked','Run','Death')
$generated = @('exec-a3b62f0f-a63c-46ce-a7be-d3c70542f6c4.png','exec-9b090a84-cec3-4c1f-bde8-e670a4a296ca.png','exec-badc5c13-2c00-425e-bfb8-61e7355a7687.png','exec-266c8bb3-22bc-4856-ab73-627c45cee055.png','exec-e7999c93-b1f9-4c2f-870c-540275a75b06.png')
$sources = [string[]]($generated | ForEach-Object { Join-Path $SourceDirectory $_ })
$report = [PackRedDragonSprites]::Run($sources,[string[]]$names,$OutputDirectory)
[PackRedDragonSprites]::PackFire((Join-Path $SourceDirectory 'exec-fbd4de07-bb5f-424e-8ede-14976d564756.png'),(Join-Path $OutputDirectory 'FireAtlas.png'))
New-Item -ItemType Directory -Path $ArchiveDirectory -Force | Out-Null
for($i=0;$i -lt $sources.Length;$i++) {
    $destination = Join-Path $ArchiveDirectory ($names[$i]+'-Generated-20260927.png')
    if(Test-Path -LiteralPath $destination) {
        if((Get-FileHash -LiteralPath $destination).Hash -ne (Get-FileHash -LiteralPath $sources[$i]).Hash) { throw "Archive exists with different content: $destination" }
    } else { Copy-Item -LiteralPath $sources[$i] -Destination $destination }
}
$fireSource=Join-Path $SourceDirectory 'exec-fbd4de07-bb5f-424e-8ede-14976d564756.png'
$fireArchive=Join-Path $ArchiveDirectory 'FireAtlas-Generated-20260927.png'
if(!(Test-Path -LiteralPath $fireArchive)){Copy-Item -LiteralPath $fireSource -Destination $fireArchive}
elseif((Get-FileHash -LiteralPath $fireArchive).Hash -ne (Get-FileHash -LiteralPath $fireSource).Hash){throw 'Different FireAtlas source already archived.'}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'alpha-bounds-report.json') -Encoding UTF8
$report | Select-Object UniformScale,SourceFrameAnchorX,Preview | ConvertTo-Json -Compress