// Deterministic import preparation of reviewed generated poses. Does not regenerate artwork.
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections.Generic;
public static class PackA212Sprites
{
 public sealed class Box { public int X,Y,W,H; }
 public sealed class Frame { public string Motion; public int Index,BottomMargin; public Box Source,Output; }
 public sealed class Report { public Frame[] Frames; public double[] Scales; public string Preview; public int RemovedLowAlphaPixels; }
 sealed class Blob { public List<int> Pixels=new List<int>(); public int MinX=int.MaxValue,MinY=int.MaxValue,MaxX=-1,MaxY=-1; public double Cx {get{return (MinX+MaxX)*.5;}} public double Cy {get{return (MinY+MaxY)*.5;}} public void Add(int p,int w){Pixels.Add(p);int x=p%w,y=p/w;MinX=Math.Min(MinX,x);MinY=Math.Min(MinY,y);MaxX=Math.Max(MaxX,x);MaxY=Math.Max(MaxY,y);} }
 sealed class Sheet { public byte[] Data; public int W,H,Discarded; public Blob[] Poses; }
 public static Report Run(string[] paths,string output)
 {
  var names=new[]{"Attack","Run","Death","SawImpactAtlas"};
  if(paths.Length!=4)throw new ArgumentException("Four reviewed source sheets required");
  var sheets=paths.Select((p,i)=>Read(p,i==3)).ToArray();
  var frames=new List<Frame>();var scales=new List<double>();Directory.CreateDirectory(output);
  for(int m=0;m<4;m++) {
   var s=sheets[m];
   // First full-height pose matches the existing Idle's ~295px height; all frames of one motion share this scale.
   double scale=m==3?1:295.0/(s.Poses[0].MaxY-s.Poses[0].MinY+1);
   double maxWidth=s.Poses.Max(p=>p.MaxX-p.MinX+1),maxHeight=s.Poses.Max(p=>p.MaxY-p.MinY+1);
   scale=Math.Min(scale,Math.Min(488.0/maxWidth,478.0/maxHeight));scales.Add(scale);
   int w=m==3?2048:4096,h=m==3?1024:512; var packed=new byte[w*h*4];
   for(int f=0;f<8;f++) {
    var pose=s.Poses[f];int pw=pose.MaxX-pose.MinX+1,ph=pose.MaxY-pose.MinY+1;
    int ow=(int)Math.Round(pw*scale),oh=(int)Math.Round(ph*scale),cx=m==3?f%4:f,cy=m==3?f/4:0;
    int x0=256-ow/2,y0=m==3?256-oh/2:496-oh;
    if(x0<8||x0+ow>504||y0<8||y0+oh>504)throw new InvalidOperationException("Complete pose won't fit 512-cell: "+names[m]+" "+f);
    var isolated=new byte[pw*ph*4];foreach(int p in pose.Pixels)Array.Copy(s.Data,p*4,isolated,((p/s.W-pose.MinY)*pw+p%s.W-pose.MinX)*4,4);
    for(int y=0;y<oh;y++)for(int x=0;x<ow;x++){
     int sx=Math.Min(pw-1,(int)((x+.5)*pw/ow)),sy=Math.Min(ph-1,(int)((y+.5)*ph/oh));
     Array.Copy(isolated,(sy*pw+sx)*4,packed,((cy*512+y0+y)*w+cx*512+x0+x)*4,4);
    }
    var box=Bounds(packed,w,cx*512,cy*512,512);int margin=512-box.Y-box.H;
    if(m!=3&&(margin<14||margin>18))throw new InvalidOperationException("Bad baseline margin "+margin);
    frames.Add(new Frame{Motion=names[m],Index=f,BottomMargin=margin,Source=new Box{X=pose.MinX,Y=pose.MinY,W=pw,H=ph},Output=box});
   }
   Save(Path.Combine(output,names[m]+".png"),w,h,packed);
  }
  var preview=Path.Combine(output,"contact-sheet.png");
  using(var result=new Bitmap(2048,4*278,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(result))using(var font=new Font(FontFamily.GenericSansSerif,11,FontStyle.Bold)){
   g.Clear(Color.FromArgb(24,30,39));g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
   for(int m=0;m<4;m++)using(var b=new Bitmap(Path.Combine(output,names[m]+".png"))){
    g.DrawString(names[m],font,Brushes.White,4,m*278+2);
    for(int f=0;f<8;f++){int sx=m==3?(f%4)*512:f*512,sy=m==3?(f/4)*512:0;g.DrawImage(b,new Rectangle(f*256,m*278+20,256,256),new Rectangle(sx,sy,512,512),GraphicsUnit.Pixel);g.DrawLine(Pens.DimGray,f*256,m*278+268,(f+1)*256,m*278+268);}
   }result.Save(preview,ImageFormat.Png);
  }
  return new Report{Frames=frames.ToArray(),Scales=scales.ToArray(),Preview=preview,RemovedLowAlphaPixels=sheets.Sum(s=>s.Discarded)};
 }
 static Sheet Read(string path,bool vfx)
 {
  var s=new Sheet();using(var b=new Bitmap(path)){s.W=b.Width;s.H=b.Height;s.Data=new byte[s.W*s.H*4];var bits=b.LockBits(new Rectangle(0,0,s.W,s.H),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);for(int y=0;y<s.H;y++)Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),s.Data,y*s.W*4,s.W*4);b.UnlockBits(bits);}
  // Omit generator's nearly invisible haze; keep alpha>=16 and original visible edge alpha.
  for(int p=0;p<s.W*s.H;p++)if(s.Data[p*4+3]<16){if(s.Data[p*4+3]>0)s.Discarded++;s.Data[p*4+3]=0;}
  var blobs=new List<Blob>();var visited=new bool[s.W*s.H];var q=new int[s.W*s.H];
  for(int p=0;p<s.W*s.H;p++)if(!visited[p]&&s.Data[p*4+3]>=16){
   var b=new Blob();int a=0,z=0;q[z++]=p;visited[p]=true;
   while(a<z){int n=q[a++];b.Add(n,s.W);int x=n%s.W,y=n/s.W;for(int yy=Math.Max(0,y-1);yy<=Math.Min(s.H-1,y+1);yy++)for(int xx=Math.Max(0,x-1);xx<=Math.Min(s.W-1,x+1);xx++){int v=yy*s.W+xx;if(!visited[v]&&s.Data[v*4+3]>=16){visited[v]=true;q[z++]=v;}}}
   blobs.Add(b);
  }
  var major=blobs.OrderByDescending(b=>b.Pixels.Count).Take(8).ToArray();
  if(major.Any(b=>b.Pixels.Count<512))throw new InvalidOperationException("Could not find eight complete poses: "+path);
  var top=major.OrderBy(b=>b.Cy).Take(4).OrderBy(b=>b.Cx).ToArray();var bottom=major.OrderBy(b=>b.Cy).Skip(4).OrderBy(b=>b.Cx).ToArray();
  s.Poses=top.Concat(bottom).ToArray();
  if(s.Poses.Take(4).Any(b=>b.Cy>s.H*.55)||s.Poses.Skip(4).Any(b=>b.Cy<s.H*.45))throw new InvalidOperationException("Ambiguous source rows");
  foreach(var b in blobs.Except(major)){
   // Attach short saw arcs/sparks to their closest complete pose. Ignore isolated single-pixel noise.
   if(b.Pixels.Count<4){foreach(int p in b.Pixels)s.Data[p*4+3]=0;continue;}
   int index=Enumerable.Range(0,8).OrderBy(i=>Math.Pow(b.Cx-s.Poses[i].Cx,2)+Math.Pow(b.Cy-s.Poses[i].Cy,2)).First();
   var pose=s.Poses[index];if(Math.Abs(b.Cy-pose.Cy)>s.H*.35||Math.Abs(b.Cx-pose.Cx)>s.W*.17)throw new InvalidOperationException("Ambiguous detached artwork");
   foreach(int p in b.Pixels)pose.Add(p,s.W);
  }
  if(s.Poses.Any(b=>b.MinX==0||b.MaxX==s.W-1||b.MinY==0||b.MaxY==s.H-1))throw new InvalidOperationException("Visible source touches outer edge; regenerate rather than crop: "+path);
  return s;
 }
 static Box Bounds(byte[] d,int w,int x0,int y0,int n){int a=n,b=n,c=-1,e=-1;for(int y=0;y<n;y++)for(int x=0;x<n;x++)if(d[((y+y0)*w+x+x0)*4+3]>=16){a=Math.Min(a,x);b=Math.Min(b,y);c=Math.Max(c,x);e=Math.Max(e,y);}return new Box{X=a,Y=b,W=c-a+1,H=e-b+1};}
 static void Save(string path,int w,int h,byte[] data){using(var b=new Bitmap(w,h,PixelFormat.Format32bppArgb)){var bits=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);for(int y=0;y<h;y++)Marshal.Copy(data,y*w*4,IntPtr.Add(bits.Scan0,y*bits.Stride),w*4);b.UnlockBits(bits);b.Save(path,ImageFormat.Png);}}
}
