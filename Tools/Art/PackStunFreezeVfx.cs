using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

// Packs existing generated pixels only. One scale and fixed grid origins preserve every phase.
public static class PackStunFreezeVfx
{
    public sealed class FrameReport
    {
        public int Frame, SourceVisiblePixels, OutputVisiblePixels, SourceComponents;
        public int Left, Top, Right, Bottom, CentralVisiblePixels, CentralOpaquePixels, Inner64VisiblePixels;
        public double CentralMeanAlpha;
    }
    public sealed class SheetReport
    {
        public string Name, SourcePath, ResourcePath;
        public int SourceWidth, SourceHeight, SourceTransparentPixels, RemovedFaintPixels, SourceVisiblePixels, AssignedVisiblePixels;
        public int Columns, Rows, FrameCount, AlphaThreshold, CellSize=512, Margin=16;
        public double SourceCellWidth, SourceCellHeight, UniformScale;
        public string Anchor="Fixed generated grid center -> (256,256), one uniform scale, no frame recentering";
        public FrameReport[] Frames;
    }
    public sealed class PackReport { public SheetReport Stun, Freeze; }
    sealed class Dot { public int X,Y; public Color Color; }
    public static PackReport Run(string stunSourcePath,string freezeSourcePath,string outputDirectory,int alphaThreshold=16)
    {
        if(alphaThreshold<1||alphaThreshold>32)throw new ArgumentOutOfRangeException("alphaThreshold");
        var output=Path.GetFullPath(outputDirectory);
        if(output.Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Any(p=>p.Equals("Assets",StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Safe export requires an output directory outside every Assets folder.");
        Directory.CreateDirectory(output);
        return new PackReport {
            Stun=Pack(stunSourcePath,output,"OrbitStars","Stun",4,2,alphaThreshold,true),
            Freeze=Pack(freezeSourcePath,output,"IceCrystal","Freeze",1,1,alphaThreshold,false)
        };
    }
    static SheetReport Pack(string sourcePath,string output,string name,string status,int columns,int rows,int threshold,bool emptyCenter)
    {
        using(var input=new Bitmap(sourcePath))
        {
            int w=input.Width,h=input.Height,count=columns*rows;
            if(w<columns*16||h<rows*16)throw new InvalidOperationException("Source resolution is too small: "+sourcePath);
            var report=new SheetReport {Name=name,SourcePath=Path.GetFullPath(sourcePath),SourceWidth=w,SourceHeight=h,
                Columns=columns,Rows=rows,FrameCount=count,AlphaThreshold=threshold,SourceCellWidth=w/(double)columns,SourceCellHeight=h/(double)rows,
                ResourcePath="Resources/Project333/StatusEffects/"+status+"/"+name+".png"};
            var dots=new Dot[w*h];var seen=new bool[dots.Length];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                var c=input.GetPixel(x,y);
                if(c.A==0){report.SourceTransparentPixels++;continue;}
                if(c.A<threshold){report.RemovedFaintPixels++;continue;}
                if(x==0||y==0||x==w-1||y==h-1)throw new InvalidOperationException(name+" artwork touches the canvas edge; request a framing revision instead of cropping.");
                dots[y*w+x]=new Dot {X=x,Y=y,Color=c};report.SourceVisiblePixels++;
            }
            if(report.SourceTransparentPixels<w*h/5)throw new InvalidOperationException(name+" source lacks genuine transparent background; request regenerated alpha.");
            var frames=Enumerable.Range(0,count).Select(i=>new List<Dot>()).ToArray();var components=new int[count];
            for(int seed=0;seed<dots.Length;seed++)
            {
                if(seen[seed]||dots[seed]==null)continue;
                var component=new List<Dot>();var pending=new Queue<int>();pending.Enqueue(seed);seen[seed]=true;
                double sx=0,sy=0;int minX=w,minY=h,maxX=0,maxY=0;
                while(pending.Count>0)
                {
                    int index=pending.Dequeue();var dot=dots[index];component.Add(dot);sx+=dot.X+.5;sy+=dot.Y+.5;
                    minX=Math.Min(minX,dot.X);minY=Math.Min(minY,dot.Y);maxX=Math.Max(maxX,dot.X);maxY=Math.Max(maxY,dot.Y);
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                    {
                        int nx=dot.X+dx,ny=dot.Y+dy;if(nx<0||ny<0||nx>=w||ny>=h)continue;
                        int next=ny*w+nx;if(seen[next]||dots[next]==null)continue;seen[next]=true;pending.Enqueue(next);
                    }
                }
                int col=Math.Min(columns-1,(int)(sx/component.Count/report.SourceCellWidth));
                int row=Math.Min(rows-1,(int)(sy/component.Count/report.SourceCellHeight));
                // Whole connected components must belong to one cell; never split or delete crossing artwork.
                if(minX<col*report.SourceCellWidth||maxX+1>(col+1)*report.SourceCellWidth||
                   minY<row*report.SourceCellHeight||maxY+1>(row+1)*report.SourceCellHeight)
                    throw new InvalidOperationException(name+" visible connected art crosses source grid cell "+(row*columns+col)+"; request separated frames.");
                int frame=row*columns+col;frames[frame].AddRange(component);components[frame]++;
            }
            double widest=0,tallest=0;
            for(int i=0;i<count;i++)
            {
                if(frames[i].Count==0)throw new InvalidOperationException(name+" source has empty frame "+i);
                double cx=(i%columns+.5)*report.SourceCellWidth,cy=(i/columns+.5)*report.SourceCellHeight;
                foreach(var dot in frames[i])
                {
                    widest=Math.Max(widest,Math.Abs(dot.X+.5-cx)+.5);
                    tallest=Math.Max(tallest,Math.Abs(dot.Y+.5-cy)+.5);
                }
            }
            report.UniformScale=Math.Min(480.0/Math.Max(report.SourceCellWidth,report.SourceCellHeight),Math.Min(239.0/widest,239.0/tallest));
            var target=Path.Combine(output,report.ResourcePath);Directory.CreateDirectory(Path.GetDirectoryName(target));
            using(var atlas=new Bitmap(columns*512,rows*512,PixelFormat.Format32bppArgb))
            using(var contact=new Bitmap(columns*512,rows*512,PixelFormat.Format32bppArgb))
            using(var graphics=Graphics.FromImage(contact))
            {
                graphics.Clear(Color.FromArgb(255,22,29,38));report.Frames=new FrameReport[count];
                for(int i=0;i<count;i++)
                {
                    double cx=(i%columns+.5)*report.SourceCellWidth,cy=(i/columns+.5)*report.SourceCellHeight;
                    var lookup=frames[i].ToDictionary(p=>p.Y*w+p.X,p=>p.Color);
                    using(var cell=new Bitmap(512,512,PixelFormat.Format32bppArgb))
                    {
                        var fr=new FrameReport {Frame=i,SourceVisiblePixels=frames[i].Count,SourceComponents=components[i],Left=512,Top=512,Right=512,Bottom=512};
                        int maxX=-1,maxY=-1;long centerAlpha=0;
                        for(int y=0;y<512;y++)for(int x=0;x<512;x++)
                        {
                            int sourceX=(int)Math.Floor(cx+(x+.5-256)/report.UniformScale),sourceY=(int)Math.Floor(cy+(y+.5-256)/report.UniformScale);
                            Color c;if(sourceX<0||sourceY<0||sourceX>=w||sourceY>=h||!lookup.TryGetValue(sourceY*w+sourceX,out c))continue;
                            if(x<16||y<16||x>495||y>495)throw new InvalidOperationException(name+" output crosses 16px margin in frame "+i);
                            cell.SetPixel(x,y,c);fr.OutputVisiblePixels++;fr.Left=Math.Min(fr.Left,x);fr.Top=Math.Min(fr.Top,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);
                            if(x>=208&&x<=303&&y>=208&&y<=303){fr.CentralVisiblePixels++;centerAlpha+=c.A;if(c.A>=192)fr.CentralOpaquePixels++;}
                            if(x>=224&&x<=287&&y>=224&&y<=287)fr.Inner64VisiblePixels++;
                        }
                        if(fr.OutputVisiblePixels==0)throw new InvalidOperationException(name+" frame vanished in resampling "+i);
                        fr.Right=511-maxX;fr.Bottom=511-maxY;fr.CentralMeanAlpha=centerAlpha/(96.0*96.0);
                        if(emptyCenter&&fr.Inner64VisiblePixels!=0)throw new InvalidOperationException(name+" covers central64px; preserve pixels and request composition revision.");


                        report.Frames[i]=fr;report.AssignedVisiblePixels+=frames[i].Count;
                        using(var ag=Graphics.FromImage(atlas)){ag.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;ag.DrawImageUnscaled(cell,i%columns*512,i/columns*512);}
                        graphics.DrawImageUnscaled(cell,i%columns*512,i/columns*512);
                    }
                }
                if(report.AssignedVisiblePixels!=report.SourceVisiblePixels)throw new InvalidOperationException(name+" lost original visible art pixels.");
                atlas.Save(target,ImageFormat.Png);contact.Save(Path.Combine(output,name+"-contact-sheet.png"),ImageFormat.Png);
            }
            return report;
        }
    }
}
