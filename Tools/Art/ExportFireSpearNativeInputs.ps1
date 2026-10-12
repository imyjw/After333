param([Parameter(Mandatory=$true)][string]$AtlasPath,[Parameter(Mandatory=$true)][string]$PortraitPath,[string]$OutputDirectory='TempBuild/FireSpearPresentation_20261001/NativeInputs')
# No redraw/crop/normalization: preserve the two reviewed whole PNGs exactly outside Assets.
$ErrorActionPreference='Stop'
$fireTools=if([string]::IsNullOrWhiteSpace($PSScriptRoot)){'C:/Project_333/Tools/Art'}else{$PSScriptRoot}
$fireRoot=[IO.Path]::GetFullPath((Join-Path $fireTools '../..'))
$fireOut=if([IO.Path]::IsPathRooted($OutputDirectory)){[IO.Path]::GetFullPath($OutputDirectory)}else{[IO.Path]::GetFullPath((Join-Path $fireRoot $OutputDirectory))}
if(($fireOut-split '[\\/]')-contains'Assets'){throw 'Native input export must remain outside Assets.'}
Add-Type -AssemblyName System.Drawing
$fireInputs=@([pscustomobject]@{Source=[IO.Path]::GetFullPath($AtlasPath);Relative='Resources/Project333/SpellEffects/FireSpear/FlameSpear.png';Width=2048;Height=1024},[pscustomobject]@{Source=[IO.Path]::GetFullPath($PortraitPath);Relative='Resources/Project333/CardArtwork/FireSpear.png';Width=512;Height=512})
$fireRows=@()
foreach($fireInput in $fireInputs){
    $fireImage=[Drawing.Image]::FromFile($fireInput.Source)
    try{if($fireImage.Width-ne$fireInput.Width-or$fireImage.Height-ne$fireInput.Height){throw ('Unexpected whole PNG dimensions: '+$fireInput.Source)}}finally{$fireImage.Dispose()}
    $fireSha=(Get-FileHash -LiteralPath $fireInput.Source -Algorithm SHA256).Hash
    $fireTarget=Join-Path $fireOut $fireInput.Relative
    if(Test-Path -LiteralPath $fireTarget){if((Get-FileHash -LiteralPath $fireTarget -Algorithm SHA256).Hash-ne$fireSha){throw ('Refuse different existing export: '+$fireTarget)}}else{New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($fireTarget)) -Force|Out-Null;Copy-Item -LiteralPath $fireInput.Source -Destination $fireTarget}
    if((Get-FileHash -LiteralPath $fireInput.Source -Algorithm SHA256).Hash-ne$fireSha-or(Get-FileHash -LiteralPath $fireTarget -Algorithm SHA256).Hash-ne$fireSha){throw 'FireSpear source/export bytes changed.'}
    $fireRows+=[pscustomobject]@{Source=$fireInput.Source;Output=$fireTarget;SHA256=$fireSha;Width=$fireInput.Width;Height=$fireInput.Height;ExactBytesPreserved=$true}
}
[pscustomobject]@{Mode='Exact reviewed PNG bytes; no resize/crop/redraw';Inputs=$fireRows}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $fireOut 'native-inputs-manifest.json') -Encoding UTF8
Write-Output ('FireSpear native inputs exported exactly: '+$fireOut)