param([string]$GeneratedPath, [string]$OutputFolder)
# Mechanical sheet packing only: exact source quarter/half rectangles, uniform scale and padding.
# Default output is validation space; it never overwrites the active Resources sprite sheet.
if([string]::IsNullOrEmpty($GeneratedPath)){$GeneratedPath=Join-Path $PSScriptRoot 'Source\ReactiveArmourVfx\Activation-Generated-20261001.png'}
$taskProjectRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if([string]::IsNullOrEmpty($OutputFolder)){$OutputFolder=Join-Path $taskProjectRoot 'TempBuild\ReactiveArmourVfxPacking'}
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$taskFolder=[IO.Path]::GetFullPath($OutputFolder)
$assetsRoot=[IO.Path]::GetFullPath((Join-Path $taskProjectRoot 'Assets'))+[IO.Path]::DirectorySeparatorChar
if($taskFolder.StartsWith($assetsRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Pack into validation space, inspect it, then deliberately copy the reviewed PNG into Assets.'}
[IO.Directory]::CreateDirectory($taskFolder)|Out-Null
$source=[System.Drawing.Bitmap]::new($GeneratedPath)
$packed=[System.Drawing.Bitmap]::new(2048,1024,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics=[System.Drawing.Graphics]::FromImage($packed)
try {
 $graphics.CompositingMode=[System.Drawing.Drawing2D.CompositingMode]::SourceCopy
 $graphics.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
 $graphics.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
 $graphics.Clear([System.Drawing.Color]::Transparent)
 $cell=[single]($source.Width/4.0)
 if([Math]::Abs($cell-$source.Height/2.0)-gt .0001){throw 'Generated sheet must be a precise4×2 aspectratio.'}
 for($frame=0;$frame-lt8;$frame++){
  $column=$frame%4;$row=[Math]::Floor($frame/4)
  $dest=[System.Drawing.RectangleF]::new([single]($column*512+16),[single]($row*512+16),480,480)
  $src=[System.Drawing.RectangleF]::new([single]($column*$cell),[single]($row*$cell),$cell,$cell)
  $graphics.DrawImage($source,$dest,$src,[System.Drawing.GraphicsUnit]::Pixel)
 }
 $output=Join-Path $taskFolder 'Activation.png'
 $packed.Save($output,[System.Drawing.Imaging.ImageFormat]::Png)
 $frames=@()
 for($frame=0;$frame-lt8;$frame++){
  $column=$frame%4;$row=[Math]::Floor($frame/4)
  $minX=512;$minY=512;$maxX=-1;$maxY=-1;$minBrightX=512;$maxBrightX=-1;$minBrightY=512;$maxBrightY=-1;$zero=0;$semi=0
  for($y=0;$y-lt512;$y++){for($x=0;$x-lt512;$x++){
    $color=$packed.GetPixel($column*512+$x,$row*512+$y)
    if($color.A -eq 0){$zero++}elseif($color.A-lt255){$semi++}
    if($color.A-ge16){$minX=[Math]::Min($minX,$x);$minY=[Math]::Min($minY,$y);$maxX=[Math]::Max($maxX,$x);$maxY=[Math]::Max($maxY,$y)}
    if($color.A-ge128 -and $color.R-ge180 -and $color.G-ge150){
       $minBrightX=[Math]::Min($minBrightX,$x);$minBrightY=[Math]::Min($minBrightY,$y);$maxBrightX=[Math]::Max($maxBrightX,$x);$maxBrightY=[Math]::Max($maxBrightY,$y)
    }
  }}
  if($minX-lt16 -or $minY-lt16 -or $maxX-gt495 -or $maxY-gt495){throw "Frame$frame leavestransparentborder."}
  $frames += [pscustomobject]@{Frame=$frame;Alpha16Bounds=@($minX,$minY,$maxX,$maxY);BrightBounds=@($minBrightX,$minBrightY,$maxBrightX,$maxBrightY);TransparentPixels=$zero;SemitransparentPixels=$semi}
 }
 $report=[pscustomobject]@{SourceWidth=$source.Width;SourceHeight=$source.Height;FinalWidth=2048;FinalHeight=1024;Columns=4;Rows=2;FrameWidth=512;FrameHeight=512;Packing='Uniform nearest-neighbour 480×480 content+16pxborder per512pxcell; exact sourcequarter/halfrectangles';Frames=$frames;SourceSha256=(Get-FileHash -LiteralPath $GeneratedPath -Algorithm SHA256).Hash;FinalSha256=(Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash}
 $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskFolder 'packing.json') -Encoding UTF8
 $report | ConvertTo-Json -Depth 8
}finally{$graphics.Dispose();$packed.Dispose();$source.Dispose()}

