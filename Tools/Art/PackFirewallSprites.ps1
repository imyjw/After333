param([string]$GeneratedSource,[string]$OutputDirectory='TempBuild/FirewallSpriteExport')
$ErrorActionPreference='Stop'
$firewallProjectRoot=if($PSScriptRoot){[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))}else{[IO.Path]::GetFullPath('C:/Project_333')}
if(-not $GeneratedSource){$GeneratedSource=Join-Path $firewallProjectRoot 'Tools/Art/Source/Firewall/FlameTile-Generated-20261006.png'}
$firewallSource=[IO.Path]::GetFullPath($GeneratedSource)
$firewallOutput=if([IO.Path]::IsPathRooted($OutputDirectory)){[IO.Path]::GetFullPath($OutputDirectory)}else{[IO.Path]::GetFullPath((Join-Path $firewallProjectRoot $OutputDirectory))}
if($firewallOutput -match '(?i)[\\/]Assets([\\/]|$)'){throw 'Export outside Assets; install only a reviewed result and preserve later artist edits.'}
if($firewallSource.StartsWith($firewallOutput+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Keep the preserved source outside the export folder.'}
Add-Type -AssemblyName System.Drawing
if(-not ('FirewallSpritePack' -as [type])){
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Generic;
public static class FirewallSpritePack {
 public sealed class Bounds { public int MinX,MinY,MaxX,MaxY; public int Width {get{return MaxX-MinX+1;}} public int Height {get{return MaxY-MinY+1;}} }
 public sealed class FrameReport {
  public int Index,SourceCellX,SourceCellY,SourceCellWidth,SourceCellHeight,RemovedLowAlphaPixels,VisibleSourcePixels;
  public Bounds SourceBounds,OutputBounds;
  public double SharedScale;
  public int DestinationX,DestinationY,ScaledWidth,ScaledHeight,BottomMargin,SourceBrightCoreY,SourceBrightCorePixels,OutputBrightCoreY;
  public int BrightCoreAboveVisibleBottom;
 }
 public sealed class Report {
  public int SourceWidth,SourceHeight,CellSize=512,AtlasSize=2048,Columns=4,Rows=4,FrameCount=16,AlphaCutoff=16,VisibleBottomTopOrigin=495;
  public int RemovedLowAlphaPixels;
  public double SharedScale;
  public string Method="One shared nearest-neighbor scale; translate each complete frame to visible bottom y495; preserve phase height and all alpha>=16 detached embers.";
  public FrameReport[] Frames;
 }
 static byte[] Read(string path,out int w,out int h){
  using(var b=new Bitmap(path)){
   w=b.Width;h=b.Height;var d=new byte[w*h*4];var z=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
   for(int y=0;y<h;y++)Marshal.Copy(IntPtr.Add(z.Scan0,y*z.Stride),d,y*w*4,w*4);b.UnlockBits(z);return d;
  }
 }
 static void Save(string path,int w,int h,byte[] d){
  using(var b=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
   var z=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
   for(int y=0;y<h;y++)Marshal.Copy(d,y*w*4,IntPtr.Add(z.Scan0,y*z.Stride),w*4);b.UnlockBits(z);b.Save(path,ImageFormat.Png);
  }
 }
 public static Report Run(string source,string output){
  int w,h;var data=Read(source,out w,out h);
  if(w!=h||w<1024)throw new InvalidOperationException("Require a square 4x4 generated atlas.");
  var frames=new List<FrameReport>();int removed=0;
  for(int f=0;f<16;f++){
   int x0=(int)Math.Round(f%4*w/4.0),x1=(int)Math.Round((f%4+1)*w/4.0),y0=(int)Math.Round(f/4*h/4.0),y1=(int)Math.Round((f/4+1)*h/4.0);
   var frame=new FrameReport{Index=f,SourceCellX=x0,SourceCellY=y0,SourceCellWidth=x1-x0,SourceCellHeight=y1-y0};
   var bounds=new Bounds{MinX=x1-x0,MinY=y1-y0,MaxX=-1,MaxY=-1};int bestBright=-1;
   for(int y=y0;y<y1;y++){
    int bright=0;
    for(int x=x0;x<x1;x++){
     int p=(y*w+x)*4,a=data[p+3];
     if(a<16){if(a>0){frame.RemovedLowAlphaPixels++;removed++;}data[p]=data[p+1]=data[p+2]=data[p+3]=0;continue;}
     frame.VisibleSourcePixels++;
     bounds.MinX=Math.Min(bounds.MinX,x-x0);bounds.MaxX=Math.Max(bounds.MaxX,x-x0);bounds.MinY=Math.Min(bounds.MinY,y-y0);bounds.MaxY=Math.Max(bounds.MaxY,y-y0);
     if(x==x0||x==x1-1||y==y0||y==y1-1)throw new InvalidOperationException("Visible source touches cell boundary "+f+"; regenerate rather than crop.");
     if(data[p+2]>200&&data[p+1]>170&&data[p]<180&&a>=64)bright++;
    }
    if(bright>bestBright){bestBright=bright;frame.SourceBrightCoreY=y-y0;frame.SourceBrightCorePixels=bright;}
   }
   if(frame.VisibleSourcePixels==0)throw new InvalidOperationException("Empty frame "+f);
   frame.SourceBounds=bounds;frames.Add(frame);
  }
  double scale=Math.Min(480.0/frames.Max(f=>f.SourceBounds.Width),480.0/frames.Max(f=>f.SourceBounds.Height));
  var pixels=new byte[2048*2048*4];
  foreach(var f in frames){
   var b=f.SourceBounds;int ow=(int)Math.Round(b.Width*scale),oh=(int)Math.Round(b.Height*scale);
   int dx=256-ow/2,dy=496-oh;
   if(dx<16||dx+ow>496||dy<16)throw new InvalidOperationException("Complete frame cannot fit "+f.Index);
   for(int y=0;y<oh;y++)for(int x=0;x<ow;x++){
    int sx=Math.Min(b.Width-1,(int)((x+.5)/scale)),sy=Math.Min(b.Height-1,(int)((y+.5)/scale));
    int src=((f.SourceCellY+b.MinY+sy)*w+f.SourceCellX+b.MinX+sx)*4;
    int dest=((f.Index/4*512+dy+y)*2048+f.Index%4*512+dx+x)*4;
    Array.Copy(data,src,pixels,dest,4);
   }
   var actual=new Bounds{MinX=512,MinY=512,MaxX=-1,MaxY=-1};
   for(int y=0;y<512;y++)for(int x=0;x<512;x++){
    if(pixels[((f.Index/4*512+y)*2048+f.Index%4*512+x)*4+3]<16)continue;
    actual.MinX=Math.Min(actual.MinX,x);actual.MaxX=Math.Max(actual.MaxX,x);actual.MinY=Math.Min(actual.MinY,y);actual.MaxY=Math.Max(actual.MaxY,y);
   }
   if(actual.MinX<16||actual.MaxX>495||actual.MinY<16||actual.MaxY!=495)throw new InvalidOperationException("Containment or floor mismatch "+f.Index);
   f.SharedScale=scale;f.DestinationX=dx;f.DestinationY=dy;f.ScaledWidth=ow;f.ScaledHeight=oh;f.OutputBounds=actual;f.BottomMargin=511-actual.MaxY;
   f.OutputBrightCoreY=dy+(int)Math.Round((f.SourceBrightCoreY-b.MinY)*scale);
   f.BrightCoreAboveVisibleBottom=495-f.OutputBrightCoreY;
  }
  Directory.CreateDirectory(output);Save(Path.Combine(output,"FlameWall.png"),2048,2048,pixels);
  return new Report{SourceWidth=w,SourceHeight=h,SharedScale=scale,RemovedLowAlphaPixels=removed,Frames=frames.ToArray()};
 }
}
"@
}
$firewallReport=[FirewallSpritePack]::Run($firewallSource,$firewallOutput)
$firewallReport | Add-Member -NotePropertyName SourceSha256 -NotePropertyValue (Get-FileHash -LiteralPath $firewallSource -Algorithm SHA256).Hash
$firewallReport | Add-Member -NotePropertyName OutputSha256 -NotePropertyValue (Get-FileHash -LiteralPath (Join-Path $firewallOutput 'FlameWall.png') -Algorithm SHA256).Hash
$firewallReport | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $firewallOutput 'packing-report.json') -Encoding UTF8
Write-Output ('Exported 16 512px FlameWall cells outside Assets: '+$firewallOutput)
Write-Output ('Shared scale '+$firewallReport.SharedScale+'; alpha cutoff16; visible bottom495; all bottom margins16.')