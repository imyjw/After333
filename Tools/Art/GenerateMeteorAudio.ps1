param([string]$OutputDirectory='TempBuild/MeteorPresentation_20260930/Audio',[string]$SynthesizerPath)
$ErrorActionPreference='Stop'
$meteorProjectRoot=if($PSScriptRoot){[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))}else{[IO.Path]::GetFullPath('C:/Project_333')}
if(-not $SynthesizerPath){$SynthesizerPath=Join-Path $meteorProjectRoot 'Tools/Art/MeteorAudio.cs'}
$meteorOutput=if([IO.Path]::IsPathRooted($OutputDirectory)){[IO.Path]::GetFullPath($OutputDirectory)}else{[IO.Path]::GetFullPath((Join-Path $meteorProjectRoot $OutputDirectory))}
if($meteorOutput -match '(?i)[\\/]Assets([\\/]|$)'){throw 'Export outside Assets; install only reviewed audio and preserve native metadata.'}
if(-not ('MeteorAudio' -as [type])){Add-Type -Path $SynthesizerPath}
$meteorReports=[MeteorAudio]::Generate($meteorOutput)
foreach($r in $meteorReports){$r|Add-Member -NotePropertyName OutputSha256 -NotePropertyValue (Get-FileHash -LiteralPath (Join-Path $meteorOutput $r.File) -Algorithm SHA256).Hash}
$meteorReports|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $meteorOutput 'MeteorAudioSynthesisReport.json') -Encoding UTF8
$meteorReports|ConvertTo-Json -Depth 6