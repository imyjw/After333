param([string]$BombSource,[string]$ExplosionSource,[string]$OutputDirectory='C:/Project_333/TempBuild/TimedBombPresentation_20260930/Packed')
$ErrorActionPreference='Stop'
if(-not $BombSource -or -not $ExplosionSource){throw 'Pass both preserved source PNGs.'}
$taskOutput=[IO.Path]::GetFullPath($OutputDirectory)
if($taskOutput -match '(?i)[\\/]Assets([\\/]|$)'){throw 'Export outside Assets; install only a reviewed result.'}
[IO.Directory]::CreateDirectory($taskOutput) | Out-Null
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
public static class TimedBombSpritePack {
  public static string Run(string bombSource,string explosionSource,string folder) {
    using(var input=new Bitmap(bombSource))
    using(var output=new Bitmap(512,512,PixelFormat.Format32bppArgb)) {
      if(input.Width!=713 || input.Height!=374)throw new Exception("Original timer reference dimensions changed; inspect artwork before packing.");
      using(var g=Graphics.FromImage(output)) {
        g.Clear(Color.Transparent);g.CompositingMode=CompositingMode.SourceCopy;
        g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
        g.DrawImage(input,new Rectangle(16,130,480,252),new Rectangle(0,0,713,374),GraphicsUnit.Pixel);
      }
      output.Save(Path.Combine(folder,"BombBody.png"),ImageFormat.Png);
    }
    using(var input=new Bitmap(explosionSource))
    using(var output=new Bitmap(2048,2048,PixelFormat.Format32bppArgb)) {
      if(input.Width!=input.Height || input.Width<1024)throw new Exception("Expected generated square 4x4 atlas.");
      using(var g=Graphics.FromImage(output)) {
        g.Clear(Color.Transparent);g.CompositingMode=CompositingMode.SourceCopy;
        g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
        for(int f=0;f<16;f++) {
          int col=f%4,row=f/4;
          int x0=(int)Math.Round(col*input.Width/4.0),x1=(int)Math.Round((col+1)*input.Width/4.0);
          int y0=(int)Math.Round(row*input.Height/4.0),y1=(int)Math.Round((row+1)*input.Height/4.0);
          g.DrawImage(input,new Rectangle(col*512+16,row*512+16,480,480),new Rectangle(x0,y0,x1-x0,y1-y0),GraphicsUnit.Pixel);
        }
      }
      var report=new StringBuilder("{\"frameSize\":512,\"atlasSize\":2048,\"columns\":4,\"rows\":4,\"frames\":[");
      for(int f=0;f<16;f++) {
        int minX=512,minY=512,maxX=-1,maxY=-1;long visible=0,transparent=0;
        for(int y=0;y<512;y++)for(int x=0;x<512;x++) {
          byte alpha=output.GetPixel(f%4*512+x,f/4*512+y).A;
          if(alpha==0)transparent++;
          if(alpha<16)continue;
          visible++;minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);
          if(x<16 || x>495 || y<16 || y>495)throw new Exception("Frame overflow: "+f);
        }
        if(visible==0 || transparent==0)throw new Exception("Empty or opaque cell: "+f);
        if(f>0)report.Append(',');
        report.Append("{\"index\":"+f+",\"boundsTopLeft\":["+minX+","+minY+","+maxX+","+maxY+"],\"visiblePixels\":"+visible+",\"transparentPixels\":"+transparent+"}");
      }
      report.Append("]}");
      output.Save(Path.Combine(folder,"Explosion.png"),ImageFormat.Png);
      File.WriteAllText(Path.Combine(folder,"TimedBombSpriteBounds.json"),report.ToString(),new UTF8Encoding(false));
      return "BombBody512px: preserved original713x374 in (16,130,480,252). Explosion:16 chronological512px transparent cells, all artwork contained.";
    }
  }
}
'@
[TimedBombSpritePack]::Run($BombSource,$ExplosionSource,$taskOutput)
Get-FileHash -LiteralPath "$taskOutput/BombBody.png","$taskOutput/Explosion.png" -Algorithm SHA256
