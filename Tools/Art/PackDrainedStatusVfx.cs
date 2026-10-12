using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

// Uniform centered packing only. This does not redraw or independently normalize any frame.
public static class PackDrainedStatusVfx
{
    public sealed class FrameReport
    {
        public int Frame, SourceVisiblePixels, OutputVisiblePixels, SourceComponents;
        public int Left, Top, Right, Bottom, CentralVisiblePixels;
    }
    public sealed class PackReport
    {
        public int SourceWidth, SourceHeight, AlphaThreshold=16, RemovedFaintPixels, SourceVisiblePixels, AssignedVisiblePixels;
        public int Columns=4, Rows=2, CellSize=512, Margin=16;
        public double UniformScale, SourceCellWidth, SourceCellHeight;
        public FrameReport[] Frames;
        public string Anchor="Fixed source grid center -> output (256,256); no per-frame recenter/scale";
    }
    sealed class Dot { public int X,Y; public Color Color; }
    public static PackReport Run(string sourcePath,string outputDirectory)
    {
        var output=Path.GetFullPath(outputDirectory);
        var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath),"../../../.."));
        var assets=Path.GetFullPath(Path.Combine(root,"Assets"));
        if(output.Equals(assets,StringComparison.OrdinalIgnoreCase)||output.StartsWith(assets+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Export must remain outside Assets.");
        using(var input=new Bitmap(sourcePath))
        {
            int w=input.Width,h=input.Height;
            if(w<8||h<4)throw new InvalidOperationException("Require a reviewed 4x2 transparent source.");
            var report=new PackReport{SourceWidth=w,SourceHeight=h,SourceCellWidth=w/4.0,SourceCellHeight=h/2.0};
            var dots=new Dot[w*h]; var seen=new bool[dots.Length];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                Color c=input.GetPixel(x,y);
                if(c.A==0)continue;
                if(c.A<16){report.RemovedFaintPixels++;continue;}
                if(x==0||y==0||x==w-1||y==h-1)throw new InvalidOperationException("Source visible artwork touches image edge; regenerate rather than crop.");
                dots[y*w+x]=new Dot{X=x,Y=y,Color=c};report.SourceVisiblePixels++;
            }
            var frames=Enumerable.Range(0,8).Select(i=>new List<Dot>()).ToArray();
            var componentCounts=new int[8];
            for(int seed=0;seed<dots.Length;seed++)
            {
                if(seen[seed]||dots[seed]==null)continue;
                var component=new List<Dot>();var pending=new Queue<int>();pending.Enqueue(seed);seen[seed]=true;
                double sumX=0,sumY=0;
                while(pending.Count>0)
                {
                    int index=pending.Dequeue();var dot=dots[index];component.Add(dot);sumX+=dot.X+.5;sumY+=dot.Y+.5;
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                    {
                        int nx=dot.X+dx,ny=dot.Y+dy;
                        if(nx<0||ny<0||nx>=w||ny>=h)continue;
                        int ni=ny*w+nx;if(seen[ni]||dots[ni]==null)continue;seen[ni]=true;pending.Enqueue(ni);
                    }
                }
                int col=Math.Min(3,(int)(sumX/component.Count/report.SourceCellWidth));
                int row=Math.Min(1,(int)(sumY/component.Count/report.SourceCellHeight));int frame=row*4+col;
                frames[frame].AddRange(component);componentCounts[frame]++;
            }
            double maxX=0,maxY=0;
            for(int i=0;i<8;i++)
            {
                if(frames[i].Count==0)throw new InvalidOperationException("Empty generated frame "+i);
                double cx=(i%4+.5)*report.SourceCellWidth,cy=(i/4+.5)*report.SourceCellHeight;
                foreach(var dot in frames[i])
                {
                    maxX=Math.Max(maxX,Math.Abs(dot.X+.5-cx)+.5);maxY=Math.Max(maxY,Math.Abs(dot.Y+.5-cy)+.5);
                }
            }
            report.UniformScale=Math.Min(480.0/Math.Max(report.SourceCellWidth,report.SourceCellHeight),Math.Min(239.0/maxX,239.0/maxY));
            Directory.CreateDirectory(output);
            string resource=Path.Combine(output,"Resources/Project333/StatusEffects/Drained");Directory.CreateDirectory(resource);
            using(var atlas=new Bitmap(2048,1024,PixelFormat.Format32bppArgb))
            using(var contact=new Bitmap(2048,1024,PixelFormat.Format32bppArgb))
            using(var graphics=Graphics.FromImage(contact))
            {
                graphics.Clear(Color.FromArgb(255,22,29,38));report.Frames=new FrameReport[8];
                for(int i=0;i<8;i++)
                {
                    double cx=(i%4+.5)*report.SourceCellWidth,cy=(i/4+.5)*report.SourceCellHeight;
                    var transformed=new Dictionary<int,Color>();
                    // Inverse nearest-neighbor sampling retains pixel palette/alpha and a fixed common origin.
                    var lookup=frames[i].ToDictionary(p=>p.Y*w+p.X,p=>p.Color);
                    using(var cell=new Bitmap(512,512,PixelFormat.Format32bppArgb))
                    {
                        var fr=new FrameReport{Frame=i,SourceVisiblePixels=frames[i].Count,SourceComponents=componentCounts[i],Left=512,Top=512,Right=512,Bottom=512};
                        int maxOutputX=-1,maxOutputY=-1;
                        for(int y=0;y<512;y++)for(int x=0;x<512;x++)
                        {
                            int sx=(int)Math.Floor(cx+(x+.5-256)/report.UniformScale),sy=(int)Math.Floor(cy+(y+.5-256)/report.UniformScale);
                            Color c;if(sx<0||sy<0||sx>=w||sy>=h||!lookup.TryGetValue(sy*w+sx,out c))continue;
                            if(x<16||y<16||x>495||y>495)throw new InvalidOperationException("Visible output crosses safe margin "+i);
                            cell.SetPixel(x,y,c);fr.OutputVisiblePixels++;
                            fr.Left=Math.Min(fr.Left,x);fr.Top=Math.Min(fr.Top,y);maxOutputX=Math.Max(maxOutputX,x);maxOutputY=Math.Max(maxOutputY,y);
                            if(x>=208&&x<=303&&y>=208&&y<=303)fr.CentralVisiblePixels++;
                        }
                        if(fr.OutputVisiblePixels==0)throw new InvalidOperationException("Resampled empty frame "+i);
                        fr.Right=511-maxOutputX;fr.Bottom=511-maxOutputY;
                        if(fr.CentralVisiblePixels!=0)throw new InvalidOperationException("Frame "+i+" fills central96px hole; regenerate overlay instead of deleting artwork.");
                        report.Frames[i]=fr;report.AssignedVisiblePixels+=frames[i].Count;
                        using(var ag=Graphics.FromImage(atlas)){ag.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;ag.DrawImageUnscaled(cell,(i%4)*512,(i/4)*512);}
                        graphics.DrawImageUnscaled(cell,(i%4)*512,(i/4)*512);
                    }
                }
                if(report.AssignedVisiblePixels!=report.SourceVisiblePixels)throw new InvalidOperationException("Visible source fragments lost.");
                atlas.Save(Path.Combine(resource,"ElectricArcs.png"),ImageFormat.Png);
                contact.Save(Path.Combine(output,"contact-sheet.png"),ImageFormat.Png);
            }
            return report;
        }
    }
}
