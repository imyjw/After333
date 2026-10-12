// Original deterministic Earthquake rumble. No recording, sample library or external dependency.
using System;
using System.IO;
using System.Text;
public static class EarthquakeRumbleAudio
{
    public const int Rate = 44100;
    private const double Tau = Math.PI * 2;
    public sealed class Report
    {
        public string Source = "Original seeded DSP; no recordings, samples or external libraries", Format = "PCM16 mono";
        public int SampleRate=Rate, Samples, Channels=1, BitsPerSample=16, ClippedSamples, FirstSample, LastSample;
        public double DurationSeconds, PeakDbfs, RmsDbfs, DcOffset, MaxAdjacentStep;
        public double HighpassHz=50, LowpassHz=1200, FadeInMs=60, FadeOutMs=200, RecommendedOneShotGain=.30;
        public int FilterOrder=4;
    }
    private sealed class Noise
    {
        private uint state = 33093017;
        public double Next() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state / 2147483647.5 - 1; }
    }
    private sealed class Biquad
    {
        private double b0,b1,b2,a1,a2,z1,z2;
        public Biquad(double hz,double q,bool highpass)
        {
            double w=Tau*hz/Rate,c=Math.Cos(w),s=Math.Sin(w),alpha=s/(2*q),a0=1+alpha;
            b0=(highpass?(1+c):(1-c))/2/a0;
            b1=(highpass?-(1+c):(1-c))/a0;b2=b0;a1=-2*c/a0;a2=(1-alpha)/a0;
        }
        public double Apply(double x) { double y=b0*x+z1;z1=b1*x-a1*y+z2;z2=b2*x-a2*y;return y; }
    }
    private static double Smooth(double x) { x=Math.Max(0,Math.Min(1,x));return x*x*(3-2*x); }
    private static double Lowpass(double x,ref double state,double hz)
    { state+=(1-Math.Exp(-Tau*hz/Rate))*(x-state);return state; }
    public static Report Generate(string directory)
    {
        const int length=47628;var raw=new double[length];var envelope=new double[length];var noise=new Noise();
        var hp1=new Biquad(50,.541196100146197,true);var hp2=new Biquad(50,1.306562964876377,true);
        var lp1=new Biquad(1200,.541196100146197,false);var lp2=new Biquad(1200,1.306562964876377,false);
        double bodyState=0,stoneState=0,motionState=0,phase=0,sum=0,envSum=0;
        // Broad low earth motion with rounded rolling surges. There is no sharp crack or hiss layer.
        for(int i=0;i<length;i++)
        {
            double t=i/(double)Rate;
            double motion=Lowpass(noise.Next(),ref motionState,9);
            double surge=.72+.24*Math.Sin(Tau*6.2*t+.7*Math.Sin(Tau*1.1*t));
            double body=2.1*Lowpass(noise.Next(),ref bodyState,240);
            double stone=.48*Lowpass(noise.Next(),ref stoneState,720);
            phase+=Tau*(73+7*Math.Sin(Tau*2.9*t)+3*motion)/Rate;
            double vibration=.13*Math.Sin(phase)+.075*Math.Sin(Tau*112.7*t+.4*Math.Sin(Tau*3.2*t));
            double x=(body+stone+vibration)*surge;
            x=lp2.Apply(lp1.Apply(hp2.Apply(hp1.Apply(x))));
            double env=Smooth(i/(Rate*.06))*Smooth((length-1-i)/(Rate*.20));
            envelope[i]=env;raw[i]=x*env;sum+=raw[i];envSum+=env;
        }
        // Weighted DC removal retains the exact zero endpoints and both authored tapers.
        double dc=sum/envSum,peak=0;
        for(int i=0;i<length;i++){raw[i]-=dc*envelope[i];peak=Math.Max(peak,Math.Abs(raw[i]));}
        double normalization=Math.Pow(10,-17.0/20)/peak;var pcm=new short[length];
        for(int i=0;i<length;i++)pcm[i]=(short)Math.Round(raw[i]*normalization*32767);
        pcm[0]=pcm[length-1]=0;
        int max=0,clipped=0,maxStep=0;double total=0,squares=0;
        for(int i=0;i<length;i++){
            int v=pcm[i];max=Math.Max(max,Math.Abs(v));total+=v;squares+=(double)v*v;
            if(v==short.MinValue||v==short.MaxValue)clipped++;
            if(i>0)maxStep=Math.Max(maxStep,Math.Abs(v-pcm[i-1]));
        }
        var report=new Report{Samples=length,DurationSeconds=length/(double)Rate,FirstSample=pcm[0],LastSample=pcm[length-1],
            PeakDbfs=20*Math.Log10(max/32768.0),RmsDbfs=10*Math.Log10(squares/length/(32768.0*32768)),
            DcOffset=total/length/32768.0,MaxAdjacentStep=maxStep/32768.0,ClippedSamples=clipped};
        if(clipped!=0||Math.Abs(report.DcOffset)>.00001||Math.Abs(report.PeakDbfs+17)>.02)
            throw new InvalidOperationException("Earthquake audio amplitude/DC validation failed.");
        Directory.CreateDirectory(directory);
        using(var w=new BinaryWriter(File.Create(Path.Combine(directory,"EarthquakeRumble.wav")))){
            w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+length*2);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(Rate);w.Write(Rate*2);w.Write((short)2);w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data"));w.Write(length*2);foreach(var s in pcm)w.Write(s);
        }
        return report;
    }
}