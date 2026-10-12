// Deterministic production-sheet assembly, not artwork regeneration.
// Finds whole alpha-connected poses before packing; never cuts at nominal source-cell boundaries.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

public static class PackRedDragonSprites
{
    public sealed class Bounds { public int X, Y, Width, Height; }
    public sealed class FrameReport { public string Motion; public int Frame; public Bounds Source, Output; public int OpaquePixels, BottomMargin; }
    public sealed class Report { public double UniformScale, SourceFrameAnchorX; public int DiscardedInvisibleFragmentPixels,SourceVisiblePixels,AssignedVisiblePixels; public string[] SheetPaths; public FrameReport[] Frames; public string Preview; }
    private sealed class Component { public readonly List<int> Pixels = new List<int>(); public int MinX=int.MaxValue,MinY=int.MaxValue,MaxX=-1,MaxY=-1; public void Add(int p,int w){Pixels.Add(p);int x=p%w,y=p/w;MinX=Math.Min(MinX,x);MinY=Math.Min(MinY,y);MaxX=Math.Max(MaxX,x);MaxY=Math.Max(MaxY,y);} }
    private sealed class Source { public string Name,Path; public int Width,Height; public byte[] Data; public List<Component> Poses; public int DiscardedInvisibleFragmentPixels,VisiblePixels; }

    public static Report Run(string[] paths,string[] motions,string outputFolder)
    {
        if(paths.Length!=5 || motions.Length!=5) throw new ArgumentException("Provide exactly five motion sheets.");
        var sources = paths.Select((p,i)=>Read(p,motions[i])).ToArray();
        double minRelativeX=double.MaxValue,maxRelativeX=double.MinValue,maxHeight=0;
        foreach(var source in sources) for(int f=0;f<6;f++)
        {
            var pose=source.Poses[f];int originX=(f%3)*512;
            minRelativeX=Math.Min(minRelativeX,pose.MinX-originX);
            maxRelativeX=Math.Max(maxRelativeX,pose.MaxX-originX);
            maxHeight=Math.Max(maxHeight,pose.MaxY-pose.MinY+1);
        }
        double scale=Math.Min(1,Math.Min(480/(maxRelativeX-minRelativeX+1),470/maxHeight));
        double anchor=(minRelativeX+maxRelativeX+1)*.5;
        var reports=new List<FrameReport>();var outputs=new List<string>();
        // Source validation is complete before any output is published.
        Directory.CreateDirectory(outputFolder);
        foreach(var source in sources)
        {
            var result=new byte[3072*512*4];
            for(int f=0;f<6;f++)
            {
                var pose=source.Poses[f]; int width=pose.MaxX-pose.MinX+1,height=pose.MaxY-pose.MinY+1;
                var isolated=new byte[width*height*4];
                foreach(int p in pose.Pixels) Array.Copy(source.Data,p*4,isolated,((p/source.Width-pose.MinY)*width+p%source.Width-pose.MinX)*4,4);
                int targetW=Math.Max(1,(int)Math.Round(width*scale)),targetH=Math.Max(1,(int)Math.Round(height*scale));
                int x0=(int)Math.Round(256+(pose.MinX-(f%3)*512-anchor)*scale),y0=496-targetH;
                if(x0<1 || x0+targetW>511 || y0<1) throw new InvalidOperationException("Global scale does not fit complete pose: "+source.Name+" "+f);
                for(int y=0;y<targetH;y++)for(int x=0;x<targetW;x++)
                {
                    int sx=Math.Min(width-1,(int)((x+.5)*width/targetW)),sy=Math.Min(height-1,(int)((y+.5)*height/targetH));
                    int from=(sy*width+sx)*4,to=((y0+y)*3072+f*512+x0+x)*4;
                    Array.Copy(isolated,from,result,to,4);
                }
                Bounds output=AlphaBounds(result,3072,f*512,512);
                int margin=512-output.Y-output.Height;
                if(margin<8||margin>24) throw new InvalidOperationException("Ground margin out of tolerance: "+source.Name+" "+f+" margin="+margin);
                reports.Add(new FrameReport{Motion=source.Name,Frame=f,Source=new Bounds{X=pose.MinX,Y=pose.MinY,Width=width,Height=height},Output=output,OpaquePixels=pose.Pixels.Count,BottomMargin=margin});
            }
            string destination=Path.Combine(outputFolder,source.Name+".png");Save(destination,3072,512,result);outputs.Add(destination);
        }
        string preview=Path.Combine(outputFolder,"contact-sheet.png");
        using(var image=new Bitmap(1536,5*276,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(image))
        using(var font=new Font(FontFamily.GenericSansSerif,11,FontStyle.Bold))
        {
            g.Clear(Color.FromArgb(26,32,41));g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
            for(int row=0;row<5;row++)using(var sheet=new Bitmap(outputs[row]))
            {
                for(int f=0;f<6;f++)
                {
                    g.DrawImage(sheet,new Rectangle(f*256,row*276+20,256,256),new Rectangle(f*512,0,512,512),GraphicsUnit.Pixel);
                    g.DrawLine(Pens.DimGray,f*256,row*276+268,(f+1)*256,row*276+268);
                }
                g.DrawString(motions[row],font,Brushes.White,5,row*276+2);
            }
            image.Save(preview,ImageFormat.Png);
        }
        return new Report{UniformScale=scale,SourceFrameAnchorX=anchor,DiscardedInvisibleFragmentPixels=sources.Sum(s=>s.DiscardedInvisibleFragmentPixels),SourceVisiblePixels=sources.Sum(s=>s.VisiblePixels),AssignedVisiblePixels=sources.Sum(s=>s.Poses.Sum(p=>p.Pixels.Count(n=>s.Data[n*4+3]>=16))),Frames=reports.ToArray(),SheetPaths=outputs.ToArray(),Preview=preview};
    }

    private static Source Read(string path,string name)
    {
        int w,h; byte[] data;
        using(var bitmap=new Bitmap(path))
        {
            w=bitmap.Width;h=bitmap.Height;if(w!=1536||h!=1024)throw new InvalidOperationException("Expected3x2source512cells: "+path);
            var bits=bitmap.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try{data=new byte[w*h*4];for(int y=0;y<h;y++)Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),data,y*w*4,w*4);}finally{bitmap.UnlockBits(bits);}
        }
        var visited=new byte[w*h];var components=new List<Component>();var queue=new int[w*h];
        for(int p=0;p<w*h;p++)if(visited[p]==0 && data[p*4+3]>=16)
        {
            var component=new Component();int first=0,last=0;queue[last++]=p;visited[p]=1;
            while(first<last){int point=queue[first++];component.Add(point,w);int x=point%w,y=point/w;
                for(int yy=Math.Max(0,y-1);yy<=Math.Min(h-1,y+1);yy++)for(int xx=Math.Max(0,x-1);xx<=Math.Min(w-1,x+1);xx++)
                {int n=yy*w+xx;if(visited[n]==0 && data[n*4+3]>=16){visited[n]=1;queue[last++]=n;}}
            }
            components.Add(component);
        }
        int maxCount=components.Count==0?0:components.Max(c=>c.Pixels.Count);
        var major=components.Where(c=>c.Pixels.Count>=Math.Max(512,maxCount*.06)).OrderByDescending(c=>c.Pixels.Count).ToList();
        if(major.Count!=6)throw new InvalidOperationException(name+": expected6majorposes, found"+major.Count+"; largest component pixelcounts="+string.Join(",",components.OrderByDescending(c=>c.Pixels.Count).Take(12).Select(c=>c.Pixels.Count.ToString()).ToArray()));
        // Sort rows by spatial center, then columns by spatial center, not source-cell crops.
        var byRow=major.OrderBy(c=>(c.MinY+c.MaxY)*.5).ToArray();
        var ordered=byRow.Take(3).OrderBy(c=>(c.MinX+c.MaxX)*.5).Concat(byRow.Skip(3).OrderBy(c=>(c.MinX+c.MaxX)*.5)).ToList();
        if(ordered.Take(3).Any(c=>(c.MinY+c.MaxY)*.5>=512) || ordered.Skip(3).Any(c=>(c.MinY+c.MaxY)*.5<512))
            throw new InvalidOperationException(name+": ambiguous source row order.");
        // Grow six visible cores with their nonzero antialias fringe (up to two pixels).
        // Distant alpha<16 generator haze is omitted; it is not part of the visible pose.
        var labels=new byte[w*h];var fringeDistance=new byte[w*h];int head=0,tail=0;
        for(int i=0;i<6;i++)foreach(int p in ordered[i].Pixels){labels[p]=(byte)(i+1);queue[tail++]=p;}
        while(head<tail)
        {
            int p=queue[head++],x=p%w,y=p/w;byte label=labels[p];
            for(int yy=Math.Max(0,y-1);yy<=Math.Min(h-1,y+1);yy++)for(int xx=Math.Max(0,x-1);xx<=Math.Min(w-1,x+1);xx++)
            {int n=yy*w+xx;if(labels[n]==0 && data[n*4+3]>0 && (data[n*4+3]>=16 || fringeDistance[p]<2)){labels[n]=label;fringeDistance[n]=data[n*4+3]>=16?(byte)0:(byte)(fringeDistance[p]+1);ordered[label-1].Add(n,w);queue[tail++]=n;}}
        }
        // Detached tiny flames/sparks retain original coordinates relative to the nearest complete pose.
        var fragments=new List<Component>();
        for(int p=0;p<w*h;p++)if(labels[p]==0 && data[p*4+3]>0)
        {
            var c=new Component();int first=0,last=0;queue[last++]=p;labels[p]=255;
            while(first<last){int q=queue[first++];c.Add(q,w);int x=q%w,y=q/w;
                for(int yy=Math.Max(0,y-1);yy<=Math.Min(h-1,y+1);yy++)for(int xx=Math.Max(0,x-1);xx<=Math.Min(w-1,x+1);xx++)
                {int n=yy*w+xx;if(labels[n]==0 && data[n*4+3]>0){labels[n]=255;queue[last++]=n;}}
            }
            fragments.Add(c);
        }
        int discardedInvisible=0;
        foreach(var c in fragments)
        {
            if(!c.Pixels.Any(p=>data[p*4+3]>=16)){discardedInvisible+=c.Pixels.Count;continue;}
            double cx=(c.MinX+c.MaxX)*.5,cy=(c.MinY+c.MaxY)*.5;
            var nearest=ordered.OrderBy(m=>Distance(cx,cy,m)).First();foreach(int p in c.Pixels)nearest.Add(p,w);
        }
        int visible=Enumerable.Range(0,w*h).Count(p=>data[p*4+3]>=16);
        if(ordered.Sum(c=>c.Pixels.Count(p=>data[p*4+3]>=16))!=visible)throw new InvalidOperationException(name+": visible source pixels were lost or assigned twice.");
        foreach(var c in ordered)if(c.MinX==0||c.MaxX==w-1||c.MinY==0||c.MaxY==h-1)
            throw new InvalidOperationException(name+": artwork touches outer source boundary; regenerate instead of clipping.");
        return new Source{Name=name,Path=path,Width=w,Height=h,Data=data,Poses=ordered,DiscardedInvisibleFragmentPixels=discardedInvisible,VisiblePixels=visible};
    }
    private static double Distance(double x,double y,Component c)
    {
        double dx=Math.Max(0,Math.Max(c.MinX-x,x-c.MaxX)),dy=Math.Max(0,Math.Max(c.MinY-y,y-c.MaxY));
        double centerX=x-(c.MinX+c.MaxX)*.5,centerY=y-(c.MinY+c.MaxY)*.5;
        return (dx*dx+dy*dy)*1000000+centerX*centerX+centerY*centerY;
    }
    private static Bounds AlphaBounds(byte[] data,int w,int offset,int cell)
    {
        int minX=cell,minY=cell,maxX=-1,maxY=-1;
        for(int y=0;y<cell;y++)for(int x=0;x<cell;x++)if(data[(y*w+offset+x)*4+3]>=16){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
        return new Bounds{X=minX,Y=minY,Width=maxX-minX+1,Height=maxY-minY+1};
    }
    private static void Save(string path,int width,int height,byte[] data)
    {
        using(var bitmap=new Bitmap(width,height,PixelFormat.Format32bppArgb))
        {
            var bits=bitmap.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try{for(int y=0;y<height;y++)Marshal.Copy(data,y*width*4,IntPtr.Add(bits.Scan0,y*bits.Stride),width*4);}finally{bitmap.UnlockBits(bits);}
            bitmap.Save(path,ImageFormat.Png);
        }
    }
    public static void PackFire(string path,string destination)
    {
        using(var source=new Bitmap(path))using(var output=new Bitmap(3072,512,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(output))
        {
            if(source.Width!=1536||source.Height!=1024)throw new InvalidOperationException("Fire source must be3x2 512pxcells.");
            g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            for(int i=0;i<6;i++)g.DrawImage(source,new Rectangle(i*512,0,512,512),new Rectangle((i%3)*512,(i/3)*512,512,512),GraphicsUnit.Pixel);
            output.Save(destination,ImageFormat.Png);
        }
    }
}