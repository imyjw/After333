using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

// Uniform, fixed-grid resampling of generated pixels; no pose recentering or artwork cropping.
public static class PackManaShieldSprites
{
    public sealed class FrameReport
    {
        public int Frame, VisiblePixels, Left, Top, Right, Bottom;
    }
    public sealed class Report
    {
        public int SourceWidth, SourceHeight, Frames = 8, CellSize = 512;
        public double UniformScale;
        public string Source, Atlas, Portrait, ContactSheet;
        public FrameReport[] FrameBounds;
    }
    public static Report Run(string inputPath, string outputDirectory)
    {
        var output = Path.GetFullPath(outputDirectory);
        if(output.Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Any(p=>p.Equals("Assets",StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Export outside Assets before native import.");
        Directory.CreateDirectory(output);
        using(var source = new Bitmap(inputPath))
        {
            double sw = source.Width/4.0, sh = source.Height/2.0;
            var result = new Report { Source = Path.GetFullPath(inputPath), SourceWidth = source.Width, SourceHeight = source.Height,
                UniformScale = 464.0/Math.Max(sw,sh), FrameBounds = new FrameReport[8],
                Atlas = Path.Combine(output,"Resources/Project333/SpellEffects/ManaShield/Formation.png"),
                Portrait = Path.Combine(output,"ManaShieldPortrait.png"), ContactSheet = Path.Combine(output,"ManaShieldContactSheet.png") };
            Directory.CreateDirectory(Path.GetDirectoryName(result.Atlas));
            for(int y=0;y<source.Height;y++) for(int x=0;x<source.Width;x++)
            {
                if(source.GetPixel(x,y).A < 16) continue;
                double cellX=x%sw, cellY=y%sh;
                if(cellX<2 || cellY<2 || cellX>sw-3 || cellY>sh-3)
                    throw new InvalidOperationException("Visible art touches a source cell edge; regenerate safe framing rather than crop.");
            }
            using(var atlas = new Bitmap(2048,1024,PixelFormat.Format32bppArgb))
            using(var contact = new Bitmap(2048,1024,PixelFormat.Format32bppArgb))
            using(var preview = Graphics.FromImage(contact))
            {
                preview.Clear(Color.FromArgb(255,22,29,38));
                for(int i=0;i<8;i++)
                {
                    var info = new FrameReport { Frame=i, Left=512, Top=512, Right=512, Bottom=512 };
                    int maxX=-1,maxY=-1;
                    double cx=(i%4+.5)*sw,cy=(i/4+.5)*sh;
                    using(var cell = new Bitmap(512,512,PixelFormat.Format32bppArgb))
                    {
                        for(int y=0;y<512;y++) for(int x=0;x<512;x++)
                        {
                            int sx=(int)Math.Floor(cx+(x+.5-256)/result.UniformScale),sy=(int)Math.Floor(cy+(y+.5-256)/result.UniformScale);
                            if(sx < i%4*sw || sx >= (i%4+1)*sw || sy < i/4*sh || sy >= (i/4+1)*sh) continue;
                            var color=source.GetPixel(sx,sy);
                            if(color.A==0)continue;
                            if(x<16 || y<16 || x>495 || y>495)
                                throw new InvalidOperationException("Nonzero alpha crosses output safety margin in frame "+i);
                            cell.SetPixel(x,y,color);
                            atlas.SetPixel(i%4*512+x,i/4*512+y,color);
                            if(color.A<16)continue;
                            info.VisiblePixels++;info.Left=Math.Min(info.Left,x);info.Top=Math.Min(info.Top,y);
                            maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);
                        }
                        info.Right=511-maxX;info.Bottom=511-maxY;
                        if(info.VisiblePixels==0 || Math.Min(Math.Min(info.Left,info.Top),Math.Min(info.Right,info.Bottom))<16)
                            throw new InvalidOperationException("Invalid 512px frame or insufficient safe margin: "+i);
                        preview.DrawImageUnscaled(cell,i%4*512,i/4*512);
                        if(i==4) cell.Save(result.Portrait,ImageFormat.Png);
                        
                    }
                    result.FrameBounds[i]=info;
                }
                atlas.Save(result.Atlas,ImageFormat.Png);contact.Save(result.ContactSheet,ImageFormat.Png);
            }
return result;
        }
    }
}
