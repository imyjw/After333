param([string]$GeneratedSource,[string]$OutputDirectory='TempBuild/EarthquakePresentation_20260930/Packed')
$ErrorActionPreference='Stop'
$earthquakeProjectRoot=if($PSScriptRoot){[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))}else{[IO.Path]::GetFullPath('C:/Project_333')}
if(-not $GeneratedSource){$GeneratedSource=Join-Path $earthquakeProjectRoot 'Tools/Art/Source/Earthquake/Cracks-Generated-20260930.png'}
$earthquakeOutput=if([IO.Path]::IsPathRooted($OutputDirectory)){[IO.Path]::GetFullPath($OutputDirectory)}else{[IO.Path]::GetFullPath((Join-Path $earthquakeProjectRoot $OutputDirectory))}
if($earthquakeOutput -match '(?i)[\\/]Assets([\\/]|$)'){throw 'Export outside Assets; install only a reviewed result.'}
Add-Type -AssemblyName System.Drawing
if(-not ('EarthquakeSpritePack' -as [type])){
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class EarthquakeSpritePack {
 public sealed class FrameReport {
  public int Index,MinX=512,MinY=512,MaxX=-1,MaxY=-1,AnyMinX=512,AnyMinY=512,AnyMaxX=-1,AnyMaxY=-1;
  public int VisiblePixels,NonzeroAlphaPixels,ClearPixels;
  public double SourceCellX,SourceCellY,SourceCellWidth,SourceCellHeight;
 }
 public sealed class Report {
  public int SourceWidth,SourceHeight,CellSize=512,AtlasSize=2048,Columns=4,Rows=4,FrameCount=16,FramesPerSecond=16,CanvasMargin=16;
  public double SharedScale,PivotX=.5,PivotY=.5;
  public string Method="Resample the entire 4x4 source grid with one shared nearest-neighbor scale; put each complete 480px cell in a 512px canvas at16,16. Preserve alpha and phase geometry; no crop, redraw or framewise normalization.";
  public FrameReport[] Frames;
 }
 public static Report Run(string source,string output){
  int w,h;byte[] d;
  using(var b=new Bitmap(source)){
   w=b.Width;h=b.Height;if(w!=h||w<1024)throw new InvalidOperationException("Require a square 4x4 generated atlas.");
   d=new byte[w*h*4];var z=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
   for(int y=0;y<h;y++)Marshal.Copy(IntPtr.Add(z.Scan0,y*z.Stride),d,y*w*4,w*4);b.UnlockBits(z);
  }
  double scale=1920.0/w;var pixels=new byte[2048*2048*4];var frames=new FrameReport[16];
  for(int f=0;f<16;f++){
   var frame=new FrameReport{Index=f,SourceCellX=f%4*w/4.0,SourceCellY=f/4*h/4.0,SourceCellWidth=w/4.0,SourceCellHeight=h/4.0};
   for(int y=0;y<480;y++)for(int x=0;x<480;x++){
    int sx=Math.Min(w-1,(int)((f%4*480+x+.5)/scale)),sy=Math.Min(h-1,(int)((f/4*480+y+.5)/scale));
    int ox=x+16,oy=y+16;int a=d[(sy*w+sx)*4+3];
    Array.Copy(d,(sy*w+sx)*4,pixels,((f/4*512+oy)*2048+f%4*512+ox)*4,4);
    if(a==0){frame.ClearPixels++;continue;}
    frame.NonzeroAlphaPixels++;frame.AnyMinX=Math.Min(frame.AnyMinX,ox);frame.AnyMaxX=Math.Max(frame.AnyMaxX,ox);frame.AnyMinY=Math.Min(frame.AnyMinY,oy);frame.AnyMaxY=Math.Max(frame.AnyMaxY,oy);
    if(a<16)continue;
    frame.VisiblePixels++;frame.MinX=Math.Min(frame.MinX,ox);frame.MaxX=Math.Max(frame.MaxX,ox);frame.MinY=Math.Min(frame.MinY,oy);frame.MaxY=Math.Max(frame.MaxY,oy);
   }
   frame.ClearPixels+=512*512-480*480;
   if(frame.VisiblePixels==0||frame.AnyMinX<16||frame.AnyMaxX>495||frame.AnyMinY<16||frame.AnyMaxY>495)throw new InvalidOperationException("Empty/overflow frame "+f);
   frames[f]=frame;
  }
  Directory.CreateDirectory(output);
  using(var b=new Bitmap(2048,2048,PixelFormat.Format32bppArgb)){
   var z=b.LockBits(new Rectangle(0,0,2048,2048),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
   for(int y=0;y<2048;y++)Marshal.Copy(pixels,y*2048*4,IntPtr.Add(z.Scan0,y*z.Stride),2048*4);b.UnlockBits(z);b.Save(Path.Combine(output,"Cracks.png"),ImageFormat.Png);
  }
  return new Report{SourceWidth=w,SourceHeight=h,SharedScale=scale,Frames=frames};
 }
}
"@
}
$earthquakeReport=[EarthquakeSpritePack]::Run([IO.Path]::GetFullPath($GeneratedSource),$earthquakeOutput)
$earthquakeReport | Add-Member -NotePropertyName SourceSha256 -NotePropertyValue (Get-FileHash -LiteralPath $GeneratedSource -Algorithm SHA256).Hash
$earthquakeReport | Add-Member -NotePropertyName OutputSha256 -NotePropertyValue (Get-FileHash -LiteralPath (Join-Path $earthquakeOutput 'Cracks.png') -Algorithm SHA256).Hash
$earthquakeReport | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $earthquakeOutput 'packing-report.json') -Encoding UTF8
Write-Output ('Exported 16x512px Cracks: '+$earthquakeOutput+'; shared scale '+$earthquakeReport.SharedScale+'; full cells with 16px border.')