// Deterministic packing of reviewed imagegen artwork. No painting or procedural sprite redraw.
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections.Generic;

public static class PackA301Sprites
{
    public sealed class Box { public int X, Y, W, H; }
    public sealed class Point { public double X, Y; public int Pixels; }
    public sealed class Frame { public string Motion; public int Index, BottomMargin; public Box Source, Output; public Point SourceLens, SourceEmitter, OutputEmitter; public double Scale; public int Lift; }
    public sealed class Inspection { public int W,H,RemovedLowAlphaPixels; public Frame[] Frames; }
    public sealed class Report { public int CellSize=512; public int FrameCount=8; public double SharedScale; public int[] RemovedLowAlphaPixels; public Frame[] Frames; public string Preview; }
    sealed class Blob {
        public List<int> Pixels = new List<int>(); public int MinX=int.MaxValue, MinY=int.MaxValue, MaxX=-1, MaxY=-1;
        public double Cx { get { return (MinX+MaxX)*.5; } } public double Cy { get { return (MinY+MaxY)*.5; } }
        public void Add(int p,int w) { Pixels.Add(p); int x=p%w,y=p/w; MinX=Math.Min(MinX,x); MinY=Math.Min(MinY,y); MaxX=Math.Max(MaxX,x); MaxY=Math.Max(MaxY,y); }
        public Box Box {get {return new Box{X=MinX,Y=MinY,W=MaxX-MinX+1,H=MaxY-MinY+1};}}
    }
    sealed class Sheet { public byte[] Data; public int W,H,Discarded; public Blob[] Poses; }

    public static Inspection Inspect(string path)
    {
        var s=Read(path); return new Inspection{W=s.W,H=s.H,RemovedLowAlphaPixels=s.Discarded,
            Frames=s.Poses.Select((p,i)=>new Frame{Index=i,Source=p.Box,SourceLens=ColorCenter(s,p,false),SourceEmitter=ColorCenter(s,p,true)}).ToArray()};
    }

    // Every character frame uses the same scale. Hover is an intentional authored lift array relative
    // to the fixed low-hover baseline at y=496, rather than independently clamping all poses to it.
    // Review lifts after source inspection; they must retain motion between the generated Idle poses.
    public static Report Run(string[] paths,string output,int[][] lifts)
    {
        string[] names={"Idle","Attack","Death"};
        if(paths.Length!=3||lifts.Length!=3||lifts.Any(a=>a.Length!=8)) throw new ArgumentException("Three reviewed sheets and three eight-frame lift arrays required.");
        var sheets=paths.Select(Read).ToArray();
        double scale=295.0/sheets[0].Poses[0].Box.H;
        double maxW=sheets.Max(s=>s.Poses.Max(p=>p.Box.W)),maxH=sheets.Max(s=>s.Poses.Max(p=>p.Box.H));
        scale=Math.Min(scale,Math.Min(488.0/maxW,(478.0-lifts.Max(a=>a.Max()))/maxH));
        if(scale<=0)throw new InvalidOperationException("Invalid shared scale");
        var frames=new List<Frame>(); Directory.CreateDirectory(output);
        for(int m=0;m<3;m++)
        {
            var s=sheets[m]; var pixels=new byte[4096*512*4];
            // Stable lens registration prevents short rotor projections/local muzzle bloom from
            // shifting the main body sideways. Death uses complete-pose center where lens is damaged.
            var first=s.Poses[0]; var firstLens=ColorCenter(s,first,false);
            double targetLens=256+(firstLens.X-first.Cx)*scale;
            for(int f=0;f<8;f++)
            {
                var p=s.Poses[f]; int pw=p.Box.W,ph=p.Box.H,ow=(int)Math.Round(pw*scale),oh=(int)Math.Round(ph*scale);
                var lens=ColorCenter(s,p,false); var emitter=ColorCenter(s,p,true);
                int x0=lens.Pixels>=4&&m!=2?(int)Math.Round(targetLens-(lens.X-p.MinX)*scale):256-ow/2;
                int y0=496-lifts[m][f]-oh;
                if(x0<8||x0+ow>504||y0<8||y0+oh>498)throw new InvalidOperationException("Complete artwork cannot fit: "+names[m]+" #"+f+"; recompose rather than crop.");
                var isolated=new byte[pw*ph*4]; foreach(int v in p.Pixels) Array.Copy(s.Data,v*4,isolated,((v/s.W-p.MinY)*pw+v%s.W-p.MinX)*4,4);
                for(int y=0;y<oh;y++)for(int x=0;x<ow;x++)
                {
                    int sx=Math.Min(pw-1,(int)((x+.5)*pw/ow)),sy=Math.Min(ph-1,(int)((y+.5)*ph/oh));
                    Array.Copy(isolated,(sy*pw+sx)*4,pixels,((y0+y)*4096+f*512+x0+x)*4,4);
                }
                var bounds=Bounds(pixels,4096,f*512,0,512);
                int margin=512-bounds.Y-bounds.H;
                if(margin<14||margin>40)throw new InvalidOperationException("Bad baseline "+names[m]+" #"+f+" = "+margin);
                frames.Add(new Frame{Motion=names[m],Index=f,BottomMargin=margin,Source=p.Box,Output=bounds,Scale=scale,Lift=lifts[m][f],
                    SourceLens=lens,SourceEmitter=emitter,OutputEmitter=new Point{X=x0+(emitter.X-p.MinX)*scale,Y=y0+(emitter.Y-p.MinY)*scale,Pixels=emitter.Pixels}});
            }
            Save(Path.Combine(output,names[m]+".png"),4096,512,pixels);
        }
        string preview=Path.Combine(output,"contact-sheet.png");
        using(var b=new Bitmap(2048,3*278,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(b))using(var font=new Font(FontFamily.GenericSansSerif,11,FontStyle.Bold))
        {
            g.Clear(Color.FromArgb(24,30,39));g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
            for(int m=0;m<3;m++)using(var source=new Bitmap(Path.Combine(output,names[m]+".png")))
            {
                g.DrawString(names[m],font,Brushes.White,4,m*278+2);
                for(int f=0;f<8;f++){g.DrawImage(source,new Rectangle(f*256,m*278+20,256,256),new Rectangle(f*512,0,512,512),GraphicsUnit.Pixel);g.DrawLine(Pens.DimGray,f*256,m*278+268,(f+1)*256,m*278+268);}
            }
            b.Save(preview,ImageFormat.Png);
        }
        return new Report{SharedScale=scale,Frames=frames.ToArray(),RemovedLowAlphaPixels=sheets.Select(s=>s.Discarded).ToArray(),Preview=preview};
    }

    static Sheet Read(string path)
    {
        var s=new Sheet(); using(var bitmap=new Bitmap(path))
        {
            s.W=bitmap.Width;s.H=bitmap.Height;s.Data=new byte[s.W*s.H*4];var bits=bitmap.LockBits(new Rectangle(0,0,s.W,s.H),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            for(int y=0;y<s.H;y++) Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),s.Data,y*s.W*4,s.W*4);bitmap.UnlockBits(bits);
        }
        // Remove only virtually invisible alpha haze, retaining the generated visible artwork.
        for(int p=0;p<s.W*s.H;p++)if(s.Data[p*4+3]<16){if(s.Data[p*4+3]>0)s.Discarded++;s.Data[p*4+3]=0;}
        var visited=new bool[s.W*s.H];var queue=new int[s.W*s.H];var blobs=new List<Blob>();
        for(int p=0;p<s.W*s.H;p++)if(!visited[p]&&s.Data[p*4+3]>=16)
        {
            var blob=new Blob();int a=0,z=0;queue[z++]=p;visited[p]=true;
            while(a<z){int n=queue[a++];blob.Add(n,s.W);int x=n%s.W,y=n/s.W;for(int yy=Math.Max(0,y-1);yy<=Math.Min(s.H-1,y+1);yy++)for(int xx=Math.Max(0,x-1);xx<=Math.Min(s.W-1,x+1);xx++){int v=yy*s.W+xx;if(!visited[v]&&s.Data[v*4+3]>=16){visited[v]=true;queue[z++]=v;}}}
            blobs.Add(blob);
        }
        var major=blobs.OrderByDescending(b=>b.Pixels.Count).Take(8).ToArray();
        if(major.Length!=8||major.Any(b=>b.Pixels.Count<256))throw new InvalidOperationException("Could not locate eight complete poses: "+path);
        s.Poses=major.OrderBy(b=>b.Cy).Take(4).OrderBy(b=>b.Cx).Concat(major.OrderBy(b=>b.Cy).Skip(4).OrderBy(b=>b.Cx)).ToArray();
        if(s.Poses.Take(4).Any(p=>p.Cy>s.H*.6)||s.Poses.Skip(4).Any(p=>p.Cy<s.H*.4))throw new InvalidOperationException("Ambiguous source pose rows "+path);
        foreach(var blob in blobs.Except(major))
        {
            if(blob.Pixels.Count<4){foreach(int p in blob.Pixels)s.Data[p*4+3]=0;continue;}
            int index=Enumerable.Range(0,8).OrderBy(i=>Math.Pow(blob.Cx-s.Poses[i].Cx,2)+Math.Pow(blob.Cy-s.Poses[i].Cy,2)).First();var p0=s.Poses[index];
            if(Math.Abs(blob.Cx-p0.Cx)>s.W*.2||Math.Abs(blob.Cy-p0.Cy)>s.H*.35)throw new InvalidOperationException("Ambiguous detached artwork: "+path);
            foreach(int p in blob.Pixels)p0.Add(p,s.W);
        }
        if(s.Poses.Any(p=>p.MinX<1||p.MinY<1||p.MaxX>=s.W-1||p.MaxY>=s.H-1))throw new InvalidOperationException("Source touches outer edge; regenerate rather than crop: "+path);
        return s;
    }

    static Point ColorCenter(Sheet s,Blob p,bool cyan)
    {
        double x=0,y=0;int n=0;
        foreach(int v in p.Pixels)
        {
            int b=s.Data[v*4],g=s.Data[v*4+1],r=s.Data[v*4+2],a=s.Data[v*4+3];
            int py=v/s.W;
            bool match=cyan?b>120&&g>120&&r<100&&Math.Abs(b-g)<90&&py>p.MinY+p.Box.H*.55:
                b>110&&g<190&&b>r*1.5&&b>g*1.12&&py>p.MinY+p.Box.H*.25&&py<p.MinY+p.Box.H*.84;
            if(a>=64&&match){x+=v%s.W;y+=py;n++;}
        }
        return new Point{X=n>0?x/n:p.Cx,Y=n>0?y/n:p.Cy,Pixels=n};
    }
    static Box Bounds(byte[] d,int w,int x0,int y0,int n){int a=n,b=n,c=-1,e=-1;for(int y=0;y<n;y++)for(int x=0;x<n;x++)if(d[((y+y0)*w+x+x0)*4+3]>=16){a=Math.Min(a,x);b=Math.Min(b,y);c=Math.Max(c,x);e=Math.Max(e,y);}return new Box{X=a,Y=b,W=c-a+1,H=e-b+1};}
    static void Save(string path,int w,int h,byte[] data){using(var b=new Bitmap(w,h,PixelFormat.Format32bppArgb)){var bits=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);for(int y=0;y<h;y++)Marshal.Copy(data,y*w*4,IntPtr.Add(bits.Scan0,y*bits.Stride),w*4);b.UnlockBits(bits);b.Save(path,ImageFormat.Png);}}
}
