using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

public static class PackSmoothIdle {
  const int Cell=512,Size=2048,Threshold=16;
  public sealed class Bounds { public int X,Y,W,H; }
  public sealed class FrameReport { public int Frame,BottomMargin,EdgePixels,SourceVisiblePixels,RepresentedSourceVisiblePixels,RemovedLowAlphaPixels,OutputVisiblePixels; public Bounds Output,Feet; public double AlphaCenterX,AlphaCenterY,FootCenterX,FootCenterY; public string Hash; }
  public sealed class Difference { public int From,To,ChangedVisiblePixels; public double MeanPremultipliedDifference,VisibleUnionDifference; }
  public sealed class Report { public int Width=Size,Height=Size,CellSize=Cell,FrameCount=16,AlphaThreshold=Threshold,UniqueFrames; public double Scale,OffsetX,OffsetY; public string Input,Output,Preview,Gif,Placement; public FrameReport[] Frames; public Difference[] AdjacentAndLoopDifferences; }
  static byte[] Read(string path,out int w,out int h) {
    using(var b=new Bitmap(path)){w=b.Width;h=b.Height;var d=new byte[w*h*4];var l=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);for(int y=0;y<h;y++)Marshal.Copy(IntPtr.Add(l.Scan0,y*l.Stride),d,y*w*4,w*4);b.UnlockBits(l);return d;}
  }
  static Bitmap BitmapOf(byte[] d,int w,int h){var b=new Bitmap(w,h,PixelFormat.Format32bppArgb);var l=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);for(int y=0;y<h;y++)Marshal.Copy(d,y*w*4,IntPtr.Add(l.Scan0,y*l.Stride),w*4);b.UnlockBits(l);return b;}
  static Bounds Box(byte[] d,int w,int x0,int y0,int cw,int ch,int minY=0) {
    int left=cw,top=ch,right=-1,bottom=-1;for(int y=minY;y<ch;y++)for(int x=0;x<cw;x++)if(d[((y+y0)*w+x+x0)*4+3]>=Threshold){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}return new Bounds{X=left,Y=top,W=right-left+1,H=bottom-top+1};
  }
  public static Report Run(string input,string scratch) {
    int sw,sh;var src=Read(input,out sw,out sh);if(sw!=1254||sh!=1254)throw new Exception("Unexpected fractional-grid source size");
    var first=Box(src,sw,0,0,(int)Math.Round(sw/4.0),(int)Math.Round(sh/4.0));
    double scale=340.0/first.H;
    // One continuous grid origin, scale, and translation for all poses. Pixel centres map consistently; no frame-specific pose normalization.
    double offsetX=278.5-((first.X+(first.W-1)*.5)+.5)*scale;
    double offsetY=496.0-(first.Y+first.H)*scale;
    var dst=new byte[Size*Size*4];var frames=new List<FrameReport>();var cells=new List<byte[]>();
    for(int f=0;f<16;f++) {
      int col=f%4,row=f/4;
      double originX=col*sw/4.0,originY=row*sh/4.0;
      int x0=(int)Math.Round(originX),y0=(int)Math.Round(originY),x1=(int)Math.Round((col+1)*sw/4.0),y1=(int)Math.Round((row+1)*sh/4.0);
      int low=0,visible=0;for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++){int a=src[(y*sw+x)*4+3];if(a>0&&a<Threshold)low++;if(a>=Threshold)visible++;}
      var cell=new byte[Cell*Cell*4];var represented=new bool[sw*sh];int representedCount=0;
      for(int y=0;y<Cell;y++)for(int x=0;x<Cell;x++) {
        int sx=(int)Math.Floor(originX+(x+.5-offsetX)/scale),sy=(int)Math.Floor(originY+(y+.5-offsetY)/scale);
        if(sx<x0||sx>=x1||sy<y0||sy>=y1)continue;
        int p=(sy*sw+sx)*4;if(src[p+3]<Threshold)continue;if(!represented[sy*sw+sx]){represented[sy*sw+sx]=true;representedCount++;}
        Array.Copy(src,p,cell,(y*Cell+x)*4,4);
        Array.Copy(src,p,dst,(((row*Cell+y)*Size+col*Cell+x)*4),4);
      }
      var box=Box(cell,Cell,0,0,Cell,Cell);var feet=Box(cell,Cell,0,0,Cell,Cell,box.Y+box.H-12);
      if(box.X<1||box.Y<1||box.X+box.W>=Cell||box.Y+box.H>=Cell)throw new Exception("Visible artwork does not fit complete cell "+f);
      int edge=0,n=0;double ax=0,ay=0,aa=0,fx=0,fy=0,fa=0;
      for(int y=0;y<Cell;y++)for(int x=0;x<Cell;x++){int a=cell[(y*Cell+x)*4+3];if(a>=Threshold){n++;ax+=x*a;ay+=y*a;aa+=a;if(x==0||x==Cell-1||y==0||y==Cell-1)edge++;if(y>=box.Y+box.H-12){fx+=x*a;fy+=y*a;fa+=a;}}}
      string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(cell)).Replace("-","").ToLowerInvariant();
      if(representedCount!=visible)throw new Exception("Visible source pixel not represented "+f);
      frames.Add(new FrameReport{Frame=f,SourceVisiblePixels=visible,RepresentedSourceVisiblePixels=representedCount,RemovedLowAlphaPixels=low,Output=box,Feet=feet,BottomMargin=Cell-box.Y-box.H,OutputVisiblePixels=n,EdgePixels=edge,AlphaCenterX=ax/aa,AlphaCenterY=ay/aa,FootCenterX=fx/fa,FootCenterY=fy/fa,Hash=hash});cells.Add(cell);
    }
    // All above-threshold source pixels lie within transformed target bounds. Upscaling represents every source pixel at least once.
    for(int f=0;f<16;f++){int c=f%4,r=f/4;double ox=c*sw/4.0,oy=r*sh/4.0;int x0=(int)Math.Round(ox),y0=(int)Math.Round(oy),x1=(int)Math.Round((c+1)*sw/4.0),y1=(int)Math.Round((r+1)*sh/4.0);for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)if(src[(y*sw+x)*4+3]>=Threshold){double tx=offsetX+(x-ox+.5)*scale,ty=offsetY+(y-oy+.5)*scale;if(tx<1||tx>=511||ty<1||ty>=511)throw new Exception("Visible source pixel would be cropped "+f);}}
    string dir=Path.Combine(scratch,"Candidate","Assets","Unit","GaebangChief");Directory.CreateDirectory(dir);string output=Path.Combine(dir,"Idle.png");using(var sheet=BitmapOf(dst,Size,Size))sheet.Save(output,ImageFormat.Png);
    var differences=new List<Difference>();for(int f=0;f<16;f++)differences.Add(Compare(cells[f],cells[(f+1)%16],f,(f+1)%16));
    string preview=Path.Combine(scratch,"contact-sheet.png"),gif=Path.Combine(scratch,"idle-preview-8fps.gif");
    using(var p=new Bitmap(1024,1104,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(p))using(var font=new Font(FontFamily.GenericSansSerif,11,FontStyle.Bold)){g.Clear(Color.FromArgb(25,31,42));g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;for(int f=0;f<16;f++){int xx=f%4*256,yy=f/4*276;g.DrawString("Idle_"+f,font,Brushes.White,xx+4,yy+2);using(var b=BitmapOf(cells[f],Cell,Cell))g.DrawImage(b,new Rectangle(xx,yy+20,256,256),0,0,512,512,GraphicsUnit.Pixel);g.DrawLine(Pens.DimGray,xx,yy+268,xx+256,yy+268);}p.Save(preview,ImageFormat.Png);}
    WriteGif(cells,gif,12); // Alternating 0.12/0.13 sec approximates 8 fps exactly over a 2 second loop.
    return new Report{Input=input,Output=output,Preview=preview,Gif=gif,Scale=scale,OffsetX=offsetX,OffsetY=offsetY,UniqueFrames=frames.Select(f=>f.Hash).Distinct().Count(),Frames=frames.ToArray(),AdjacentAndLoopDifferences=differences.ToArray(),Placement="One shared scale 340/229; common center registration x=278.5 and first contact bottom=495. Continuous 313.5px source grid origins preserve relative pose motion. alpha1..15 transparency noise removed, matching existing packer. All alpha>=16 artwork retained; no per-frame centering, geometric deformation, redraw or blending."};
  }
  static Difference Compare(byte[] a,byte[] b,int from,int to){double sum=0,unionSum=0;int union=0,changed=0;for(int p=0;p<Cell*Cell;p++){int q=p*4;bool visible=a[q+3]>=Threshold||b[q+3]>=Threshold;double error=0;for(int c=0;c<3;c++)error+=Math.Abs(a[q+c]*a[q+3]/255.0-b[q+c]*b[q+3]/255.0);error+=Math.Abs(a[q+3]-b[q+3]);sum+=error;if(visible){union++;unionSum+=error;if(error>.01)changed++;}}return new Difference{From=from,To=to,ChangedVisiblePixels=changed,MeanPremultipliedDifference=sum/(Cell*Cell*4*255.0),VisibleUnionDifference=unionSum/(union*4*255.0)};}
  static void WriteGif(List<byte[]> cells,string path,int delay){using(var stream=File.Create(path))using(var w=new BinaryWriter(stream)){w.Write(System.Text.Encoding.ASCII.GetBytes("GIF89a"));w.Write((ushort)Cell);w.Write((ushort)Cell);w.Write((byte)0);w.Write((byte)0);w.Write((byte)0);w.Write(new byte[]{0x21,0xff,11});w.Write(System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));w.Write(new byte[]{3,1,0,0,0});int frameIndex=0;foreach(var cell in cells){byte[] bytes;using(var b=BitmapOf(cell,Cell,Cell))using(var opaque=new Bitmap(Cell,Cell,PixelFormat.Format24bppRgb))using(var g=Graphics.FromImage(opaque))using(var ms=new MemoryStream()){g.Clear(Color.FromArgb(25,31,42));g.DrawImageUnscaled(b,0,0);opaque.Save(ms,ImageFormat.Gif);bytes=ms.ToArray();}int colorBits=bytes[10]&7;int colors=1<<(colorBits+1);byte[] palette=new byte[colors*3];Array.Copy(bytes,13,palette,0,palette.Length);int cursor=13+palette.Length;while(bytes[cursor]!=0x2c){if(bytes[cursor]!=0x21)throw new Exception("Unexpected GIF block");cursor+=2;while(bytes[cursor]!=0){cursor+=1+bytes[cursor];}cursor++;}int packed=bytes[cursor+9];int data=cursor+10;if((packed&128)!=0){colorBits=packed&7;colors=1<<(colorBits+1);palette=new byte[colors*3];Array.Copy(bytes,data,palette,0,palette.Length);data+=palette.Length;}w.Write(new byte[]{0x21,0xf9,4,0});w.Write((ushort)(delay+(frameIndex++%2)));w.Write((byte)0);w.Write((byte)0);w.Write((byte)0x2c);w.Write((ushort)0);w.Write((ushort)0);w.Write((ushort)Cell);w.Write((ushort)Cell);w.Write((byte)((packed&0x40)|0x80|colorBits));w.Write(palette);w.Write(bytes[data++]);while(bytes[data]!=0){int n=bytes[data++];w.Write((byte)n);w.Write(bytes,data,n);data+=n;}w.Write((byte)0);}w.Write((byte)0x3b);}}
}

