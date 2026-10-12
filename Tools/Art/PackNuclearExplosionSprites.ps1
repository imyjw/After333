param([string]$Source,[string]$OutputDirectory='C:/Project_333/TempBuild/NuclearPresentation_20260930/Packed')
$ErrorActionPreference='Stop'
if(-not $Source){throw 'Pass the original built-in image generation PNG.'}
$taskOut=[IO.Path]::GetFullPath($OutputDirectory)
if($taskOut -match '(?i)[\\/]Assets([\\/]|$)'){throw 'Export outside Assets; explicitly install a reviewed result afterwards.'}
[IO.Directory]::CreateDirectory($taskOut) | Out-Null
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
public static class NuclearSheetPack {
  public static string Run(string source,string folder) {
    using(var input=new Bitmap(source))
    using(var output=new Bitmap(2048,2048,PixelFormat.Format32bppArgb)) {
      if(input.Width!=input.Height || input.Width<1024)throw new Exception("Expected a square 4x4 generated atlas.");
      using(var graphics=Graphics.FromImage(output)) {
        graphics.Clear(Color.Transparent);
        graphics.CompositingMode=CompositingMode.SourceCopy;
        graphics.InterpolationMode=InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode=PixelOffsetMode.Half;
        for(int frame=0;frame<16;frame++) {
          int column=frame%4,row=frame/4;
          int x0=(int)Math.Round(column*input.Width/4.0),x1=(int)Math.Round((column+1)*input.Width/4.0);
          int y0=(int)Math.Round(row*input.Height/4.0),y1=(int)Math.Round((row+1)*input.Height/4.0);
          graphics.DrawImage(input,new Rectangle(column*512+16,row*512+16,480,480),
            new Rectangle(x0,y0,x1-x0,y1-y0),GraphicsUnit.Pixel);
        }
      }
      var json=new StringBuilder();
      json.Append("{\"width\":2048,\"height\":2048,\"columns\":4,\"rows\":4,\"frameSize\":512,\"frames\":[");
      for(int f=0;f<16;f++) {
        int minX=512,minY=512,maxX=-1,maxY=-1;long opaque=0,clear=0;
        for(int y=0;y<512;y++)for(int x=0;x<512;x++) {
          var p=output.GetPixel(f%4*512+x,f/4*512+y);
          if(p.A==0)clear++;
          if(p.A<16)continue;
          opaque++; minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
        }
        if(opaque==0||minX<16||maxX>495||minY<16||maxY>495||clear==0)throw new Exception("Empty, opaque background or overflow frame "+f);
        if(f>0)json.Append(',');
        json.Append("{\"index\":"+f+",\"boundsTopLeft\":["+minX+","+minY+","+maxX+","+maxY+"],\"bottomClearance\":"+(511-maxY)+",\"visiblePixels\":"+opaque+",\"transparentPixels\":"+clear+"}");
      }
      json.Append("]}");
      output.Save(Path.Combine(folder,"MushroomExplosion.png"),ImageFormat.Png);
      File.WriteAllText(Path.Combine(folder,"MushroomExplosionBounds.json"),json.ToString(),new UTF8Encoding(false));
      return "Packed 16 chronological 512x512 cells with preserved alpha and 16px outer containment; no per-frame recentering.";
    }
  }
}
'@ -ReferencedAssemblies System.Drawing
[NuclearSheetPack]::Run([IO.Path]::GetFullPath($Source),$taskOut)
