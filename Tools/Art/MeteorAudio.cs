// Original deterministic Meteor sounds. No recordings, external samples or libraries.
using System;
using System.IO;
using System.Text;
public static class MeteorAudio
{
    public const int Rate=44100;
    private const double Tau=2*Math.PI;
    public sealed class Report
    {
        public string File, Source="Original seeded DSP; no recordings, sample libraries or external service", Format="PCM16 mono";
        public int SampleRate=Rate, Samples, Channels=1, BitsPerSample=16, ClippedSamples, FirstSample, LastSample;
        public uint Seed;
        public double DurationSeconds, PeakDbfs, RmsDbfs, DcOffset, MaxAdjacentStep;
        public double HighpassHz=55, LowpassHz, FadeInMs, FadeOutMs, RecommendedOneShotGain=.30;
        public int FilterOrder=4;
    }
    private sealed class Noise
    {
        private uint state;
        public Noise(uint seed){state=seed;}
        public double Next(){state^=state<<13;state^=state>>17;state^=state<<5;return state/2147483647.5-1;}
    }
    private sealed class Biquad
    {
        private double b0,b1,b2,a1,a2,z1,z2;
        public Biquad(double hz,double q,bool high)
        {
            double w=Tau*hz/Rate,c=Math.Cos(w),a=Math.Sin(w)/(2*q),d=1+a;
            b0=(high?(1+c):(1-c))/2/d;b1=(high?-(1+c):(1-c))/d;b2=b0;a1=-2*c/d;a2=(1-a)/d;
        }
        public double Apply(double x){double y=b0*x+z1;z1=b1*x-a1*y+z2;z2=b2*x-a2*y;return y;}
    }
    private static double Smooth(double x){x=Math.Max(0,Math.Min(1,x));return x*x*(3-2*x);}
    private static double Lowpass(double x,ref double s,double hz){s+=(1-Math.Exp(-Tau*hz/Rate))*(x-s);return s;}
    private static Report GenerateOne(string directory,bool incoming)
    {
        int length=incoming?28665:44100;
        uint seed=incoming?33093081u:33093082u;
        double seconds=length/(double)Rate,cutoff=incoming?1400:1300,fadeIn=incoming?.05:.06,fadeOut=incoming?.09:.22;
        var noise=new Noise(seed);var raw=new double[length];var envs=new double[length];
        var hp1=new Biquad(55,.541196100146197,true);var hp2=new Biquad(55,1.306562964876377,true);
        var lp1=new Biquad(cutoff,.541196100146197,false);var lp2=new Biquad(cutoff,1.306562964876377,false);
        double body=0,fire=0,motion=0,phase=0,sum=0,envSum=0;
        for(int i=0;i<length;i++)
        {
            double t=i/(double)Rate,p=t/seconds;
            double roll=Lowpass(noise.Next(),ref motion,12);
            double low=Lowpass(noise.Next(),ref body,incoming?300:205);
            double flame=Lowpass(noise.Next(),ref fire,incoming?820:690);
            double hz=incoming?82+53*p:58+58*Math.Exp(-t*7);
            phase+=Tau*(hz+4*roll)/Rate;
            double surge=incoming?.85+.15*Math.Sin(Tau*7*t):.78+.22*Math.Sin(Tau*9*t+1.1);
            // Rounded rush with a rising low pulse, then a separate muted impact and falling rumble.
            // Upper noise is deliberately filtered; there is no piercing whistle, crack or hiss layer.
            double x=(2.05*low+.70*flame+.15*Math.Sin(phase)+.055*Math.Sin(phase*1.47))*surge;
            x=lp2.Apply(lp1.Apply(hp2.Apply(hp1.Apply(x))));
            double shape=incoming?.20+.80*Math.Pow(p,1.12):Math.Exp(-t*3.45);
            double env=Smooth(i/(Rate*fadeIn))*Smooth((length-1-i)/(Rate*fadeOut))*shape;
            envs[i]=env;raw[i]=x*env;sum+=raw[i];envSum+=env;
        }
        double dc=sum/envSum,peak=0;
        for(int i=0;i<length;i++){raw[i]-=dc*envs[i];peak=Math.Max(peak,Math.Abs(raw[i]));}
        double scale=Math.Pow(10,-17.0/20)/peak;var pcm=new short[length];
        for(int i=0;i<length;i++)pcm[i]=(short)Math.Round(raw[i]*scale*32767);
        pcm[0]=pcm[length-1]=0;
        int max=0,step=0,clip=0;double total=0,squares=0;
        for(int i=0;i<length;i++){int v=pcm[i];max=Math.Max(max,Math.Abs(v));total+=v;squares+=(double)v*v;if(v==short.MinValue||v==short.MaxValue)clip++;if(i>0)step=Math.Max(step,Math.Abs(v-pcm[i-1]));}
        string file=incoming?"MeteorIncoming.wav":"MeteorImpact.wav";
        var r=new Report{File=file,Seed=seed,Samples=length,DurationSeconds=seconds,LowpassHz=cutoff,FadeInMs=fadeIn*1000,FadeOutMs=fadeOut*1000,FirstSample=pcm[0],LastSample=pcm[length-1],ClippedSamples=clip,
            PeakDbfs=20*Math.Log10(max/32768.0),RmsDbfs=10*Math.Log10(squares/length/(32768.0*32768)),DcOffset=total/length/32768.0,MaxAdjacentStep=step/32768.0};
        if(clip!=0||Math.Abs(r.DcOffset)>.00001||Math.Abs(r.PeakDbfs+17)>.02||r.RmsDbfs> -26||r.RmsDbfs< -35||r.MaxAdjacentStep>.03)
            throw new InvalidOperationException("Meteor audio amplitude/DC validation failed.");
        Directory.CreateDirectory(directory);
        using(var w=new BinaryWriter(System.IO.File.Create(Path.Combine(directory,file))))
        {
            w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+length*2);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(Rate);w.Write(Rate*2);w.Write((short)2);w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data"));w.Write(length*2);foreach(var s in pcm)w.Write(s);
        }
        return r;
    }
    public static Report[] Generate(string directory){return new[]{GenerateOne(directory,true),GenerateOne(directory,false)};}
}