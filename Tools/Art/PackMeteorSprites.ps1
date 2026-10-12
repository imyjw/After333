param([string]$FlightSource,[string]$ImpactSource,[string]$OutputDirectory='TempBuild/MeteorPresentation_20260930/Packed',[ValidateSet('All','Flight','Impact')][string]$Asset='All')
$ErrorActionPreference='Stop'
$meteorProjectRoot=if($PSScriptRoot){[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))}else{[IO.Path]::GetFullPath('C:/Project_333')}
if(-not $FlightSource){$FlightSource=Join-Path $meteorProjectRoot 'Tools/Art/Source/Meteor/Flight-Generated-20260930.png'}
if(-not $ImpactSource){$ImpactSource=Join-Path $meteorProjectRoot 'Tools/Art/Source/Meteor/Impact-Generated-20260930.png'}
$meteorOutput=if([IO.Path]::IsPathRooted($OutputDirectory)){[IO.Path]::GetFullPath($OutputDirectory)}else{[IO.Path]::GetFullPath((Join-Path $meteorProjectRoot $OutputDirectory))}
if($meteorOutput -match '(?i)[\\/]Assets([\\/]|$)'){throw 'Export outside Assets; install only reviewed output and preserve artist edits.'}
Add-Type -AssemblyName System.Drawing
if(-not ('MeteorSpritePack' -as [type])){
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class MeteorSpritePack
{
 public sealed class FrameReport
 {
  public int Index,MinX=512,MinY=512,MaxX=-1,MaxY=-1,AnyMinX=512,AnyMinY=512,AnyMaxX=-1,AnyMaxY=-1;
  public int VisiblePixels,NonzeroAlphaPixels,SourceTransparentPixels,SourceBoundaryVisiblePixels;
  public double SourceCellX,SourceCellY,SourceCellWidth,SourceCellHeight;
 }
 public sealed class Report
 {
  public string Asset,Method="One uniform nearest-neighbor scale for the complete source grid; each whole480px cell is placed at16,16 in a full512px canvas. Preserve source alpha and phase geometry; no redraw, crop, alpha cleanup or independent normalization.";
  public int SourceWidth,SourceHeight,CellSize=512,Columns,Rows,FrameCount,FramesPerSecond,CanvasMargin=16,OutputWidth,OutputHeight;
  public double SharedScale,PivotX=.5,PivotY=.5;
  public FrameReport[] Frames;
 }
 public static Report Run(string source,string output,string asset,int grid,int fps)
 {
  int w,h;byte[] d;
  using(var b=new Bitmap(source))
  {
   w=b.Width;h=b.Height;if(w!=h||w<grid*128)throw new InvalidOperationException("Require a square "+grid+"x"+grid+" source.");
   d=new byte[w*h*4];var z=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
   for(int y=0;y<h;y++)Marshal.Copy(IntPtr.Add(z.Scan0,y*z.Stride),d,y*w*4,w*4);b.UnlockBits(z);
  }
  int outputSize=grid*512,frames=grid*grid;double scale=grid*480.0/w;
  var pixels=new byte[outputSize*outputSize*4];var reports=new FrameReport[frames];
  for(int f=0;f<frames;f++)
  {
   var frame=new FrameReport{Index=f,SourceCellX=f%grid*w/(double)grid,SourceCellY=f/grid*h/(double)grid,SourceCellWidth=w/(double)grid,SourceCellHeight=h/(double)grid};
   for(int y=0;y<480;y++)for(int x=0;x<480;x++)
   {
    int sx=Math.Min(w-1,(int)((f%grid*480+x+.5)/scale)),sy=Math.Min(h-1,(int)((f/grid*480+y+.5)/scale));
    int ox=x+16,oy=y+16,a=d[(sy*w+sx)*4+3];
    Array.Copy(d,(sy*w+sx)*4,pixels,((f/grid*512+oy)*outputSize+f%grid*512+ox)*4,4);
    if(a==0){frame.SourceTransparentPixels++;continue;}
    frame.NonzeroAlphaPixels++;frame.AnyMinX=Math.Min(frame.AnyMinX,ox);frame.AnyMaxX=Math.Max(frame.AnyMaxX,ox);frame.AnyMinY=Math.Min(frame.AnyMinY,oy);frame.AnyMaxY=Math.Max(frame.AnyMaxY,oy);
    if(a<16)continue;if(x==0||x==479||y==0||y==479)frame.SourceBoundaryVisiblePixels++;frame.VisiblePixels++;frame.MinX=Math.Min(frame.MinX,ox);frame.MaxX=Math.Max(frame.MaxX,ox);frame.MinY=Math.Min(frame.MinY,oy);frame.MaxY=Math.Max(frame.MaxY,oy);
   }
   if(frame.SourceTransparentPixels==0)throw new InvalidOperationException("Opaque source cell "+f);
   if(frame.SourceBoundaryVisiblePixels>0)throw new InvalidOperationException("Visible source art touches cell boundary "+f+"; regenerate safe separate cells, do not crop or hide it by adding output margins.");
   if(frame.NonzeroAlphaPixels==0&&(grid==1||f<frames-1))throw new InvalidOperationException("Empty source cell "+f);
   if(frame.NonzeroAlphaPixels>0&&(frame.AnyMinX<16||frame.AnyMaxX>495||frame.AnyMinY<16||frame.AnyMaxY>495))throw new InvalidOperationException("Overflow cell "+f);
   reports[f]=frame;
  }
  Directory.CreateDirectory(output);
  using(var b=new Bitmap(outputSize,outputSize,PixelFormat.Format32bppArgb))
  {
   var z=b.LockBits(new Rectangle(0,0,outputSize,outputSize),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
   for(int y=0;y<outputSize;y++)Marshal.Copy(pixels,y*outputSize*4,IntPtr.Add(z.Scan0,y*z.Stride),outputSize*4);b.UnlockBits(z);b.Save(Path.Combine(output,asset+".png"),ImageFormat.Png);
  }
  return new Report{Asset=asset,SourceWidth=w,SourceHeight=h,Columns=grid,Rows=grid,FrameCount=frames,FramesPerSecond=fps,SharedScale=scale,OutputWidth=outputSize,OutputHeight=outputSize,Frames=reports};
 }
}
"@
}
$meteorReports=@()
if($Asset -ne 'Impact'){$meteorReports += [MeteorSpritePack]::Run([IO.Path]::GetFullPath($FlightSource),$meteorOutput,'Flight',1,0)}
if($Asset -ne 'Flight'){$meteorReports += [MeteorSpritePack]::Run([IO.Path]::GetFullPath($ImpactSource),$meteorOutput,'Impact',4,16)}
foreach($r in $meteorReports){$source=if($r.Asset -eq 'Flight'){$FlightSource}else{$ImpactSource};$r|Add-Member -NotePropertyName SourceSha256 -NotePropertyValue (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash;$r|Add-Member -NotePropertyName OutputSha256 -NotePropertyValue (Get-FileHash -LiteralPath (Join-Path $meteorOutput ($r.Asset+'.png')) -Algorithm SHA256).Hash}
$meteorReports|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $meteorOutput 'packing-report.json') -Encoding UTF8
$meteorReports|Select-Object Asset,FrameCount,SharedScale,OutputSha256|ConvertTo-Json