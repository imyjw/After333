// Packs reviewed imagegen pixels only; does not redraw, crop visible art or edit source PNGs.
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections.Generic;

public static class PackGaebangChiefSprites
{
    public sealed class Box { public int X,Y,W,H; }
    public sealed class Frame { public string Motion; public int Index,SourcePixels,BottomMargin; public Box Source,Output; public double Scale,SourceAnchorX; }
    public sealed class Report { public int CellSize=512,AlphaThreshold=16; public double CharacterScale,CoinScale; public int[] RemovedLowAlphaPixels; public Frame[] Frames; public string Preview; }
    sealed class Blob {
        public List<int> Pixels=new List<int>(); public int X=int.MaxValue,Y=int.MaxValue,R=-1,D=-1;
        public double Cx {get{return(X+R)*.5;}} public double Cy {get{return(Y+D)*.5;}}
        public Box Bounds {get{return new Box{X=X,Y=Y,W=R-X+1,H=D-Y+1};}}
        public void Add(int p,int w){Pixels.Add(p);int x=p%w,y=p/w;X=Math.Min(X,x);Y=Math.Min(Y,y);R=Math.Max(R,x);D=Math.Max(D,y);}
    }
    sealed class Sheet { public int W,H,Haze; public byte[] Data; public Blob[] Poses; }
    public static Report Run(string[] paths,string output)
    {
        if(paths.Length!=5)throw new ArgumentException("Idle,Attack,Run,Death,CoinImpactAtlas sources required.");
        var names=new[]{"Idle","Attack","Run","Death"};
        var sheets=paths.Take(4).Select(ReadCharacter).ToArray();
        // One physical scale across all 32 poses, including lying death poses and extended attack arms.
        double scale=340.0/sheets[0].Poses[0].Bounds.H;
        var frames=new List<Frame>();Directory.CreateDirectory(output);
        string unit=Path.Combine(output,"Unit","GaebangChief");Directory.CreateDirectory(unit);
        for(int m=0;m<4;m++)
        {
            var s=sheets[m];var destination=new byte[4096*512*4];
            for(int f=0;f<8;f++)
            {
                var p=s.Poses[f];var box=p.Bounds;
                int ow=(int)Math.Round(box.W*scale),oh=(int)Math.Round(box.H*scale);
                // Source grid X registration preserves authored torso shifts instead of centering an extended fist.
                double anchor=(f%4+.5)*s.W/4.0;
                int x0=(int)Math.Round(256+(box.X-anchor)*scale),y0=496-oh;
                if(x0<8||x0+ow>504||y0<8||y0+oh>496)
                    throw new InvalidOperationException("Complete pose does not fit; regenerate rather than crop: "+names[m]+" "+f);
                var isolated=new byte[box.W*box.H*4];
                foreach(int v in p.Pixels)Array.Copy(s.Data,v*4,isolated,((v/s.W-box.Y)*box.W+v%s.W-box.X)*4,4);
                Resample(isolated,box.W,box.H,destination,4096,f*512+x0,y0,ow,oh);
                var b=Bounds(destination,4096,f*512,0,512);
                int margin=512-b.Y-b.H;
                if(margin!=16)throw new InvalidOperationException("Foot/collapse contact baseline must retain 16px: "+names[m]+" "+f+" "+margin);
                frames.Add(new Frame{Motion=names[m],Index=f,SourcePixels=p.Pixels.Count,Source=box,Output=b,BottomMargin=margin,Scale=scale,SourceAnchorX=anchor});
            }
            Save(Path.Combine(unit,names[m]+".png"),4096,512,destination);
        }
        var coin=Read(paths[4]);double coinScale=480.0/Math.Max(coin.W/4.0,coin.H/2.0);
        var atlas=new byte[2048*1024*4];int sourceVisible=0;
        for(int f=0;f<8;f++)
        {
            int c=f%4,r=f/4,x=(int)Math.Round(c*coin.W/4.0),y=(int)Math.Round(r*coin.H/2.0);
            int w=(int)Math.Round((c+1)*coin.W/4.0)-x,h=(int)Math.Round((r+1)*coin.H/2.0)-y;
            var cell=new byte[w*h*4];int n=0;
            for(int yy=0;yy<h;yy++)for(int xx=0;xx<w;xx++)
            {
                int p=((y+yy)*coin.W+x+xx)*4;
                if(coin.Data[p+3]>=16)
                {
                    if(xx<1||xx>=w-1||yy<1||yy>=h-1)throw new InvalidOperationException("Coin visible art crosses a source cell; regenerate instead of cropping "+f);
                    n++;
                }
                Array.Copy(coin.Data,p,cell,(yy*w+xx)*4,4);
            }
            int ow=(int)Math.Round(w*coinScale),oh=(int)Math.Round(h*coinScale);
            int x0=(512-ow)/2,y0=(512-oh)/2;
            Resample(cell,w,h,atlas,2048,c*512+x0,r*512+y0,ow,oh);
            var b=Bounds(atlas,2048,c*512,r*512,512);
            if(b.W<=0||b.X<16||b.Y<16||b.X+b.W>496||b.Y+b.H>496)throw new InvalidOperationException("Coin full-cell padding failed "+f);
            sourceVisible+=n;
            frames.Add(new Frame{Motion="CoinImpactAtlas",Index=f,SourcePixels=n,Source=new Box{X=x,Y=y,W=w,H=h},Output=b,BottomMargin=512-b.Y-b.H,Scale=coinScale,SourceAnchorX=x+w*.5});
        }
        if(sourceVisible!=Enumerable.Range(0,coin.W*coin.H).Count(p=>coin.Data[p*4+3]>=16))throw new InvalidOperationException("Coin source pixels were dropped.");
        string resources=Path.Combine(output,"Resources","Project333","Units","GaebangChief");Directory.CreateDirectory(resources);
        Save(Path.Combine(resources,"CoinImpactAtlas.png"),2048,1024,atlas);
        string preview=Path.Combine(output,"contact-sheet.png");
        using(var b=new Bitmap(2048,5*278,PixelFormat.Format32bppArgb))
        using(var g=Graphics.FromImage(b))
        using(var font=new Font(FontFamily.GenericSansSerif,11,FontStyle.Bold))
        {
            g.Clear(Color.FromArgb(25,31,42));g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
            for(int m=0;m<5;m++)
            {
                string path=m<4?Path.Combine(unit,names[m]+".png"):Path.Combine(resources,"CoinImpactAtlas.png");
                using(var source=new Bitmap(path))
                {
                    g.DrawString(m<4?names[m]:"CoinImpactAtlas",font,Brushes.White,4,m*278+2);
                    for(int f=0;f<8;f++)
                    {
                        var rect=m<4?new Rectangle(f*512,0,512,512):new Rectangle(f%4*512,f/4*512,512,512);
                        g.DrawImage(source,new Rectangle(f*256,m*278+20,256,256),rect,GraphicsUnit.Pixel);
                        g.DrawLine(Pens.DimGray,f*256,m*278+268,(f+1)*256,m*278+268);
                    }
                }
            }
            b.Save(preview,ImageFormat.Png);
        }
        return new Report{CharacterScale=scale,CoinScale=coinScale,RemovedLowAlphaPixels=sheets.Select(s=>s.Haze).Concat(new[]{coin.Haze}).ToArray(),Frames=frames.ToArray(),Preview=preview};
    }
    static Sheet ReadCharacter(string path)
    {
        var s=Read(path);var seen=new bool[s.W*s.H];var queue=new int[s.W*s.H];var blobs=new List<Blob>();
        for(int p=0;p<s.W*s.H;p++)if(!seen[p]&&s.Data[p*4+3]>=16)
        {
            var b=new Blob();int a=0,z=0;queue[z++]=p;seen[p]=true;
            while(a<z)
            {
                int v=queue[a++],x=v%s.W,y=v/s.W;b.Add(v,s.W);
                for(int yy=Math.Max(0,y-1);yy<=Math.Min(s.H-1,y+1);yy++)
                for(int xx=Math.Max(0,x-1);xx<=Math.Min(s.W-1,x+1);xx++)
                {int n=yy*s.W+xx;if(!seen[n]&&s.Data[n*4+3]>=16){seen[n]=true;queue[z++]=n;}}
            }
            blobs.Add(b);
        }
        var major=blobs.OrderByDescending(b=>b.Pixels.Count).Take(8).ToArray();
        if(major.Length!=8||major.Any(b=>b.Pixels.Count<10000))throw new InvalidOperationException("Eight separately connected full poses required: "+path);
        s.Poses=major.OrderBy(b=>b.Cy).Take(4).OrderBy(b=>b.Cx).Concat(major.OrderBy(b=>b.Cy).Skip(4).OrderBy(b=>b.Cx)).ToArray();
        for(int i=0;i<8;i++)
            if(Math.Abs(s.Poses[i].Cx-(i%4+.5)*s.W/4)>s.W*.15||
                (i<4&&s.Poses[i].Cy>s.H*.55)||(i>=4&&s.Poses[i].Cy<s.H*.45))
                throw new InvalidOperationException("Ambiguous authored pose order: "+path);
        // Retain every visible detached fragment, even a single pixel; assign to nearest full body bounding box.
        foreach(var b in blobs.Except(major))
        {
            var ordered=Enumerable.Range(0,8).OrderBy(i=>Distance(b,major[i])).ToArray();
            int closest=ordered[0];double d=Distance(b,major[closest]),next=Distance(b,major[ordered[1]]);
            if(d>Math.Pow(s.W*.12,2)||(b.Pixels.Count>=16&&next<d*1.2))
                throw new InvalidOperationException("Ambiguous detached artwork: "+path);
            foreach(int p in b.Pixels)major[closest].Add(p,s.W);
        }
        if(s.Poses.Sum(p=>p.Pixels.Count)!=Enumerable.Range(0,s.W*s.H).Count(p=>s.Data[p*4+3]>=16))
            throw new InvalidOperationException("Visible character source pixels were lost.");
        if(s.Poses.Any(p=>p.X<1||p.Y<1||p.R>=s.W-1||p.D>=s.H-1))
            throw new InvalidOperationException("Source figure is cropped at outer edge: "+path);
        return s;
    }
    static double Distance(Blob a,Blob b)
    {
        double dx=Math.Max(0,Math.Max(b.X-a.Cx,a.Cx-b.R)),dy=Math.Max(0,Math.Max(b.Y-a.Cy,a.Cy-b.D));
        return dx*dx+dy*dy;
    }
    static Sheet Read(string path)
    {
        var s=new Sheet();
        using(var b=new Bitmap(path))
        {
            s.W=b.Width;s.H=b.Height;s.Data=new byte[s.W*s.H*4];
            var bits=b.LockBits(new Rectangle(0,0,s.W,s.H),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            for(int y=0;y<s.H;y++)Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),s.Data,y*s.W*4,s.W*4);
            b.UnlockBits(bits);
        }
        for(int p=0;p<s.W*s.H;p++)if(s.Data[p*4+3]<16){if(s.Data[p*4+3]>0)s.Haze++;for(int c=0;c<4;c++)s.Data[p*4+c]=0;}
        return s;
    }
    static void Resample(byte[] src,int sw,int sh,byte[] dst,int dw,int x0,int y0,int w,int h)
    {
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {int sx=Math.Min(sw-1,(int)((x+.5)*sw/w)),sy=Math.Min(sh-1,(int)((y+.5)*sh/h));Array.Copy(src,(sy*sw+sx)*4,dst,((y+y0)*dw+x+x0)*4,4);}
    }
    static Box Bounds(byte[] d,int w,int x0,int y0,int size)
    {
        int l=size,t=size,r=-1,b=-1;
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)if(d[((y+y0)*w+x+x0)*4+3]>=16){l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);b=Math.Max(b,y);}
        return new Box{X=l,Y=t,W=r-l+1,H=b-t+1};
    }
    static void Save(string path,int w,int h,byte[] data)
    {
        using(var b=new Bitmap(w,h,PixelFormat.Format32bppArgb))
        {
            var bits=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            for(int y=0;y<h;y++)Marshal.Copy(data,y*w*4,IntPtr.Add(bits.Scan0,y*bits.Stride),w*4);
            b.UnlockBits(bits);b.Save(path,ImageFormat.Png);
        }
    }
}
