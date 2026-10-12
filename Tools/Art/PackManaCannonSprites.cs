// Deterministic packing of reviewed imagegen pixels. Does not redraw or crop visible art.
// Export remains outside Assets; native metadata and primary media installation belong to root.
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections.Generic;

public static class PackManaCannonSprites
{
    public sealed class Box { public int X,Y,W,H; }
    public sealed class Point { public double X,Y; public int Pixels; }
    public sealed class Frame { public string Motion; public int Index,SourcePixels,BottomMargin; public Box Source,Output; public double Scale,SourceAnchorX; public Point SourceOrigin,OutputOrigin; }
    public sealed class Report { public int CellSize=512,AlphaThreshold=16; public double CharacterScale; public double[] VfxScales; public int[] RemovedLowAlphaPixels; public Frame[] Frames; public string Preview,Portrait; }
    sealed class Blob {
        public List<int> Pixels=new List<int>();public int X=int.MaxValue,Y=int.MaxValue,R=-1,D=-1;
        public double Cx{get{return(X+R)*.5;}}public double Cy{get{return(Y+D)*.5;}}
        public Box Bounds{get{return new Box{X=X,Y=Y,W=R-X+1,H=D-Y+1};}}
        public void Add(int p,int w){Pixels.Add(p);int x=p%w,y=p/w;X=Math.Min(X,x);Y=Math.Min(Y,y);R=Math.Max(R,x);D=Math.Max(D,y);}
    }
    sealed class Sheet { public int W,H,Haze;public byte[] Data;public Blob[] Poses; }
    public static Report Run(string[] paths,string output)
    {
        if(paths.Length!=5)throw new ArgumentException("Idle,Attack,Death,ProjectileAtlas,ExplosionAtlas sources required.");
        var names=new[]{"Idle","Attack","Death"};
        var sheets=paths.Take(3).Select(ReadUnit).ToArray();
        if(sheets.Any(s=>s.W!=sheets[0].W||s.H!=sheets[0].H))throw new InvalidOperationException("Character source resolutions differ; review normalized source registration first.");
        // Target idle width 380px (~280px high). Fit the complete 24-pose envelope once, never per-pose resizing.
        double scale=380.0/sheets[0].Poses[0].Bounds.W;
        foreach(var sheet in sheets)for(int f=0;f<8;f++)
        {
            var p=sheet.Poses[f];double anchor=(f%4+.5)*sheet.W/4.0;
            double left=anchor-p.X,right=p.R+1-anchor;
            if(left>0)scale=Math.Min(scale,239.0/left);
            if(right>0)scale=Math.Min(scale,239.0/right);
            scale=Math.Min(scale,479.0/p.Bounds.H);
        }
        if(scale<=0)throw new InvalidOperationException("Invalid shared scale.");
        var frames=new List<Frame>();Directory.CreateDirectory(output);
        string unit=Path.Combine(output,"Unit","ManaCannon");Directory.CreateDirectory(unit);
        string artwork=Path.Combine(output,"Resources","Project333","CardArtwork");Directory.CreateDirectory(artwork);
        string portrait=Path.Combine(artwork,"ManaCannon.png");
        for(int m=0;m<3;m++)
        {
            var s=sheets[m];var destination=new byte[4096*512*4];
            for(int f=0;f<8;f++)
            {
                var p=s.Poses[f];var box=p.Bounds;
                int ow=(int)Math.Round(box.W*scale),oh=(int)Math.Round(box.H*scale);
                double anchor=(f%4+.5)*s.W/4.0;
                int x0=(int)Math.Round(256+(box.X-anchor)*scale),y0=496-oh;
                if(x0<16||x0+ow>496||y0<16||y0+oh>496)throw new InvalidOperationException("Complete cannon pose does not fit; regenerate rather than crop: "+names[m]+" "+f);
                var isolated=new byte[box.W*box.H*4];
                foreach(int v in p.Pixels)Array.Copy(s.Data,v*4,isolated,((v/s.W-box.Y)*box.W+v%s.W-box.X)*4,4);
                Resample(isolated,box.W,box.H,destination,4096,f*512+x0,y0,ow,oh);
                var b=Bounds(destination,4096,f*512,0,512);
                int margin=512-b.Y-b.H;
                if(margin!=16)throw new InvalidOperationException("Contact/collapse floor must retain 16px: "+names[m]+" "+f+" "+margin);
                frames.Add(new Frame{Motion=names[m],Index=f,SourcePixels=p.Pixels.Count,Source=box,Output=b,BottomMargin=margin,Scale=scale,SourceAnchorX=anchor});
            }
            Save(Path.Combine(unit,names[m]+".png"),4096,512,destination);
            if(m==0)
            {
                // The full completed Idle_0 cell is the dedicated portrait, never the user's multi-pose example strip.
                var firstCell=new byte[512*512*4];
                for(int y=0;y<512;y++)Array.Copy(destination,y*4096*4,firstCell,y*512*4,512*4);
                Save(portrait,512,512,firstCell);
            }
        }
        var effects=new[]{ReadEffect(paths[3]),ReadEffect(paths[4])};var effectNames=new[]{"ProjectileAtlas","ExplosionAtlas"};var vfxScales=new double[2];
        string resources=Path.Combine(output,"Resources","Project333","Units","ManaCannon");Directory.CreateDirectory(resources);
        for(int e=0;e<2;e++)
        {
            var s=effects[e];var origins=new Point[8];
            for(int f=0;f<(e==0?8:5);f++)origins[f]=FindCore(s,s.Poses[f],f,e==0);
            // The final fading explosion frames have no central white flash. Retain the preceding row's authored origin.
            if(e==1)for(int f=5;f<8;f++)
                origins[f]=new Point{X=origins[4].X+(f%4)*s.W/4.0,Y=origins[4].Y,Pixels=0};
            double initial=480.0/Math.Max(s.W/4.0,s.H/2.0),effectScale=initial;
            double targetX=e==1?256:16+(origins[0].X)*initial;
            double targetY=e==1?256:16+(origins[0].Y)*initial;
            foreach(int f in Enumerable.Range(0,8))
            {
                var p=s.Poses[f];var origin=origins[f];
                double left=origin.X-p.X,right=p.R+1-origin.X,top=origin.Y-p.Y,bottom=p.D+1-origin.Y;
                if(left>0)effectScale=Math.Min(effectScale,(targetX-17)/left);
                if(right>0)effectScale=Math.Min(effectScale,(495-targetX)/right);
                if(top>0)effectScale=Math.Min(effectScale,(targetY-17)/top);
                if(bottom>0)effectScale=Math.Min(effectScale,(495-targetY)/bottom);
            }
            vfxScales[e]=effectScale;var atlas=new byte[2048*1024*4];
            for(int f=0;f<8;f++)
            {
                var p=s.Poses[f];var box=p.Bounds;var origin=origins[f];
                int ow=(int)Math.Round(box.W*effectScale),oh=(int)Math.Round(box.H*effectScale);
                int x0=(int)Math.Round(targetX+(box.X-origin.X)*effectScale),y0=(int)Math.Round(targetY+(box.Y-origin.Y)*effectScale);
                if(x0<16||y0<16||x0+ow>496||y0+oh>496)throw new InvalidOperationException("Registered VFX envelope cannot fit "+effectNames[e]+" "+f);
                var isolated=new byte[box.W*box.H*4];
                foreach(int v in p.Pixels)Array.Copy(s.Data,v*4,isolated,((v/s.W-box.Y)*box.W+v%s.W-box.X)*4,4);
                Resample(isolated,box.W,box.H,atlas,2048,f%4*512+x0,f/4*512+y0,ow,oh);
                var b=Bounds(atlas,2048,f%4*512,f/4*512,512);
                if(b.W<=0||b.X<16||b.Y<16||b.X+b.W>496||b.Y+b.H>496)throw new InvalidOperationException("Full-cell VFX padding failed "+effectNames[e]+" "+f);
                frames.Add(new Frame{Motion=effectNames[e],Index=f,SourcePixels=p.Pixels.Count,Source=box,Output=b,BottomMargin=512-b.Y-b.H,Scale=effectScale,
                    SourceAnchorX=origin.X,SourceOrigin=origin,OutputOrigin=new Point{X=x0+(origin.X-box.X)*effectScale,Y=y0+(origin.Y-box.Y)*effectScale,Pixels=origin.Pixels}});
            }
            Save(Path.Combine(resources,effectNames[e]+".png"),2048,1024,atlas);
        }
        string preview=Path.Combine(output,"contact-sheet.png");
        using(var b=new Bitmap(2048,5*278,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(b))using(var font=new Font(FontFamily.GenericSansSerif,11,FontStyle.Bold))
        {
            g.Clear(Color.FromArgb(25,31,42));g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
            for(int m=0;m<5;m++)using(var source=new Bitmap(m<3?Path.Combine(unit,names[m]+".png"):Path.Combine(resources,effectNames[m-3]+".png")))
            {
                g.DrawString(m<3?names[m]:effectNames[m-3],font,Brushes.White,4,m*278+2);
                for(int f=0;f<8;f++)
                {
                    var rect=m<3?new Rectangle(f*512,0,512,512):new Rectangle(f%4*512,f/4*512,512,512);
                    g.DrawImage(source,new Rectangle(f*256,m*278+20,256,256),rect,GraphicsUnit.Pixel);
                    g.DrawLine(Pens.DimGray,f*256,m*278+268,(f+1)*256,m*278+268);
                }
            }
            b.Save(preview,ImageFormat.Png);
        }
        return new Report{CharacterScale=scale,VfxScales=vfxScales,RemovedLowAlphaPixels=sheets.Select(s=>s.Haze).Concat(effects.Select(s=>s.Haze)).ToArray(),Frames=frames.ToArray(),Preview=preview,Portrait=portrait};
    }
    static Sheet ReadUnit(string path)
    {
        var s=Read(path);var frames=new Blob[8];bool separated=true;
        for(int f=0;f<8;f++)
        {
            int c=f%4,r=f/4,x0=(int)Math.Round(c*s.W/4.0),y0=(int)Math.Round(r*s.H/2.0);
            int x1=(int)Math.Round((c+1)*s.W/4.0),y1=(int)Math.Round((r+1)*s.H/2.0);
            var b=new Blob();
            for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)if(s.Data[(y*s.W+x)*4+3]>=16)
            {b.Add(y*s.W+x,s.W);if(x==x0||x==x1-1||y==y0||y==y1-1)separated=false;}
            if(b.Pixels.Count<128)throw new InvalidOperationException("Empty/unidentifiable cannon pose "+path+" "+f);
            frames[f]=b;
        }
        // Full grid regions preserve separated death rubble and detached muzzle sparks.
        // Only when a whole pose crosses an internal grid line, use the reviewed connected-body extraction.
        if(!separated)return ReadConnectedCharacter(path);
        s.Poses=frames;return s;
    }
    static Point FindCore(Sheet s,Blob p,int f,bool projectile)
    {
        double x0=f%4*s.W/4.0,y0=f/4*s.H/2.0,cx=x0+s.W/8.0,cy=y0+s.H/4.0;
        double x=0,y=0;int n=0;
        foreach(int v in p.Pixels)
        {
            int sx=v%s.W,sy=v/s.W,a=s.Data[v*4+3],r=s.Data[v*4+2],g=s.Data[v*4+1],b=s.Data[v*4];
            bool region=projectile?sx>x0+s.W/4.0*.72:Math.Pow(sx-cx,2)+Math.Pow(sy-cy,2)<Math.Pow(s.W/4.0*.16,2);
            if(region&&a>=192&&r>=235&&g>=240&&b>=240){x+=sx;y+=sy;n++;}
        }
        if(n<24)throw new InvalidOperationException("Cannot locate stable white core "+(projectile?"projectile":"explosion")+" "+f);
        return new Point{X=x/n,Y=y/n,Pixels=n};
    }
    static Sheet ReadEffect(string path)
    {
        var s=Read(path);var blobs=Components(s);var seeds=new Blob[8];s.Poses=Enumerable.Range(0,8).Select(i=>new Blob()).ToArray();
        for(int f=0;f<8;f++)
        {
            seeds[f]=blobs.Where(b=>NominalCell(s,b)==f).OrderByDescending(b=>b.Pixels.Count).FirstOrDefault();
            if(seeds[f]==null)throw new InvalidOperationException("Missing VFX source frame "+path+" "+f);
        }
        foreach(var blob in blobs)
        {
            // Assign the complete component to its authored cell by its centroid; never split it at a grid boundary.
            // A nearest-bounding-box rule would incorrectly steal late fading particles into an earlier, larger ring.
            if(blob.Bounds.W>s.W/4.0*1.1||blob.Bounds.H>s.H/2.0*1.1)throw new InvalidOperationException("Merged VFX frames require generation revision "+path);
            int index=NominalCell(s,blob);
            foreach(int pixel in blob.Pixels)s.Poses[index].Add(pixel,s.W);
        }
        if(s.Poses.Sum(p=>p.Pixels.Count)!=Enumerable.Range(0,s.W*s.H).Count(p=>s.Data[p*4+3]>=16)||
            s.Poses.Any(p=>p.X<1||p.Y<1||p.R>=s.W-1||p.D>=s.H-1))throw new InvalidOperationException("Visible VFX pixels lost or cropped source "+path);
        return s;
    }
    static int NominalCell(Sheet s,Blob b)
    {
        return Enumerable.Range(0,8).OrderBy(i=>Math.Pow(b.Cx-(i%4+.5)*s.W/4,2)+Math.Pow(b.Cy-(i/4+.5)*s.H/2,2)).First();
    }
    static List<Blob> Components(Sheet s)
    {
        var seen=new bool[s.W*s.H];var queue=new int[s.W*s.H];var blobs=new List<Blob>();
        for(int p=0;p<s.W*s.H;p++)if(!seen[p]&&s.Data[p*4+3]>=16)
        {
            var b=new Blob();int a=0,z=0;queue[z++]=p;seen[p]=true;
            while(a<z)
            {
                int v=queue[a++],x=v%s.W,y=v/s.W;b.Add(v,s.W);
                for(int yy=Math.Max(0,y-1);yy<=Math.Min(s.H-1,y+1);yy++)for(int xx=Math.Max(0,x-1);xx<=Math.Min(s.W-1,x+1);xx++)
                {int n=yy*s.W+xx;if(!seen[n]&&s.Data[n*4+3]>=16){seen[n]=true;queue[z++]=n;}}
            }
            blobs.Add(b);
        }
        return blobs;
    }
    static Sheet ReadConnectedCharacter(string path)
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
        if(major.Length!=8||major.Any(b=>b.Pixels.Count<512))throw new InvalidOperationException("Eight separately connected full poses required: "+path);
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

