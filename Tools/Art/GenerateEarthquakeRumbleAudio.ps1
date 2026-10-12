param([string]$OutputDirectory='TempBuild/EarthquakePresentation_20260930/Audio',[string]$SynthesizerPath)
$ErrorActionPreference='Stop'
$earthquakeProjectRoot=if($PSScriptRoot){[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))}else{[IO.Path]::GetFullPath('C:/Project_333')}
if(-not $SynthesizerPath){$SynthesizerPath=Join-Path $earthquakeProjectRoot 'Tools/Art/EarthquakeRumbleAudio.cs'}
$earthquakeAudioOutput=if([IO.Path]::IsPathRooted($OutputDirectory)){[IO.Path]::GetFullPath($OutputDirectory)}else{[IO.Path]::GetFullPath((Join-Path $earthquakeProjectRoot $OutputDirectory))}
if($earthquakeAudioOutput -match '(?i)[\\/]Assets([\\/]|$)'){throw 'Export outside Assets; do not overwrite artist assets or metadata.'}
if(-not ('EarthquakeRumbleAudio' -as [type])){Add-Type -Path $SynthesizerPath}
$earthquakeAudioReport=[EarthquakeRumbleAudio]::Generate($earthquakeAudioOutput)
$earthquakeAudioReport | Add-Member -NotePropertyName OutputSha256 -NotePropertyValue (Get-FileHash -LiteralPath (Join-Path $earthquakeAudioOutput 'EarthquakeRumble.wav') -Algorithm SHA256).Hash
$earthquakeAudioReport | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $earthquakeAudioOutput 'EarthquakeRumbleAudioValidation.json') -Encoding UTF8
$earthquakeAudioReport | ConvertTo-Json -Depth 5