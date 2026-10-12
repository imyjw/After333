param([Parameter(Mandatory=$true)][string]$AtlasPath,[Parameter(Mandatory=$true)][string]$PortraitPath,[string]$OutputDirectory='TempBuild/PlasmaShield_20261001/NativeInputs')
# No redraw/crop/normalization: preserve the two reviewed whole PNGs exactly outside Assets.
$ErrorActionPreference='Stop'
$shieldTools=if([string]::IsNullOrWhiteSpace($PSScriptRoot)){'C:/Project_333/Tools/Art'}else{$PSScriptRoot}
$shieldRoot=[IO.Path]::GetFullPath((Join-Path $shieldTools '../..'))
$shieldOut=if([IO.Path]::IsPathRooted($OutputDirectory)){[IO.Path]::GetFullPath($OutputDirectory)}else{[IO.Path]::GetFullPath((Join-Path $shieldRoot $OutputDirectory))}
if(($shieldOut-split '[\\/]')-contains'Assets'){throw 'Native input export must remain outside Assets.'}
Add-Type -AssemblyName System.Drawing
$shieldInputs=@([pscustomobject]@{Source=[IO.Path]::GetFullPath($AtlasPath);Relative='Resources/Project333/SpellEffects/PlasmaShield/Formation.png';Width=2048;Height=1024},[pscustomobject]@{Source=[IO.Path]::GetFullPath($PortraitPath);Relative='Resources/Project333/CardArtwork/PlasmaShield.png';Width=512;Height=512})
$shieldRows=@()
foreach($shieldInput in $shieldInputs){
    $shieldImage=[Drawing.Image]::FromFile($shieldInput.Source)
    try{if($shieldImage.Width-ne$shieldInput.Width-or$shieldImage.Height-ne$shieldInput.Height){throw ('Unexpected whole PNG dimensions: '+$shieldInput.Source)}}finally{$shieldImage.Dispose()}
    $shieldSha=(Get-FileHash -LiteralPath $shieldInput.Source -Algorithm SHA256).Hash
    $shieldTarget=Join-Path $shieldOut $shieldInput.Relative
    if(Test-Path -LiteralPath $shieldTarget){if((Get-FileHash -LiteralPath $shieldTarget -Algorithm SHA256).Hash-ne$shieldSha){throw ('Refuse different existing export: '+$shieldTarget)}}else{New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($shieldTarget)) -Force|Out-Null;Copy-Item -LiteralPath $shieldInput.Source -Destination $shieldTarget}
    if((Get-FileHash -LiteralPath $shieldInput.Source -Algorithm SHA256).Hash-ne$shieldSha-or(Get-FileHash -LiteralPath $shieldTarget -Algorithm SHA256).Hash-ne$shieldSha){throw 'PlasmaShield source/export bytes changed.'}
    $shieldRows+=[pscustomobject]@{Source=$shieldInput.Source;Output=$shieldTarget;SHA256=$shieldSha;Width=$shieldInput.Width;Height=$shieldInput.Height;ExactBytesPreserved=$true}
}
[pscustomobject]@{Mode='Exact reviewed PNG bytes; no resize/crop/redraw';Inputs=$shieldRows}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $shieldOut 'native-inputs-manifest.json') -Encoding UTF8
Write-Output ('PlasmaShield native inputs exported exactly: '+$shieldOut)