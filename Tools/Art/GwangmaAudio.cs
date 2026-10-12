// Original Gwangma sound design: deterministic synthesis, no recordings or samples.
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
public static class GwangmaAudio
{
    public const int Rate = 44100;
    const double Tau = Math.PI * 2;
    sealed class Noise
    {
        uint state;
        public Noise(uint seed) { state = seed; }
        public double Sample() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state / 2147483647.5 - 1; }
    }
    static double Smooth(double x) { x = Math.Max(0, Math.Min(1, x)); return x * x * (3 - 2 * x); }
    static double LP(double x, ref double z, double hz) { z += (1 - Math.Exp(-Tau * hz / Rate)) * (x - z); return z; }
    static double[] Clip(double seconds) { return new double[(int)Math.Round(seconds * Rate)]; }
    // Moving band-limited air, with a quick opening followed by a softer cloth-like tail.
    static void Swish(double[] output, double at, double duration, double gain, double bright, uint seed)
    {
        Noise noise = new Noise(seed);
        int start = (int)(at * Rate), length = (int)(duration * Rate);
        double hi = 0, lo = 0, motion = 0;
        for (int i = 0; i < length && start + i < output.Length; i++)
        {
            double x = i / (double)length, t = i / (double)Rate;
            double opening = Smooth(x / 0.19), tail = Math.Pow(1 - x, 1.7);
            double cutoff = 700 + bright * Math.Pow(Math.Sin(Math.PI * x), 0.65);
            double n = noise.Sample();
            double body = LP(n, ref hi, cutoff) - LP(n, ref lo, 320 + 500 * x);
            double grain = 1 + 0.8 * LP(noise.Sample(), ref motion, 90);
            output[start + i] += gain * opening * tail * body * grain;
        }
    }
    // Inharmonic decaying plate modes: tiny steel resonance, never a pitched game beep.
    static void Metal(double[] output, double at, double duration, double gain, double fundamental)
    {
        double[] modes = { 1, 1.487, 2.137, 2.823, 3.961, 5.173 };
        double[] weights = { 0.42, 0.32, 0.23, 0.17, 0.105, 0.07 };
        int start = (int)(at * Rate), length = (int)(duration * Rate);
        for (int i = 0; i < length && start + i < output.Length; i++)
        {
            double t = i / (double)Rate, x = i / (double)length;
            double sample = 0;
            for (int k = 0; k < modes.Length; k++)
                sample += weights[k] * Math.Sin(Tau * fundamental * modes[k] * t) * Math.Exp(-t * (15 + k * 8));
            output[start + i] += gain * Smooth(t / 0.0015) * Smooth((1 - x) / 0.22) * sample;
        }
    }
    static void Thud(double[] output, double at, double duration, double gain, double pitch, uint seed)
    {
        int start = (int)(at * Rate), length = (int)(duration * Rate);
        Noise noise = new Noise(seed);
        double phase = 0, z = 0;
        for (int i = 0; i < length && start + i < output.Length; i++)
        {
            double t = i / (double)Rate, x = i / (double)length;
            phase += Tau * (pitch * (0.52 + 0.48 * Math.Exp(-t * 32))) / Rate;
            double body = 0.57 * Math.Sin(phase) + 1.4 * LP(noise.Sample(), ref z, 760);
            output[start + i] += gain * Smooth(t / 0.0018) * Math.Exp(-t * 27) * Smooth((1 - x) / 0.2) * body;
        }
    }
    static void Crack(double[] output, double at, double duration, double gain, uint seed)
    {
        int start = (int)(at * Rate), length = (int)(duration * Rate);
        Noise noise = new Noise(seed); double high = 0, low = 0;
        for (int i = 0; i < length && start + i < output.Length; i++)
        {
            double t = i / (double)Rate, n = noise.Sample();
            double snap = LP(n, ref high, 6200) - LP(n, ref low, 750);
            output[start + i] += gain * Smooth(t / 0.0008) * Math.Exp(-t * 115) * Smooth((length - 1 - i) / (Rate * 0.01)) * snap;
        }
    }
    static double[] Draw()
    {
        double[] x = Clip(0.25);
        Swish(x, 0, 0.24, 0.70, 3700, 333221);
        Swish(x, 0.029, 0.17, 0.42, 5500, 333222);
        Metal(x, 0.036, 0.20, 0.25, 1490);
        return x;
    }
    static double[] Swing()
    {
        double[] x = Clip(0.28);
        Swish(x, 0, 0.19, 1.8, 6900, 333223);
        Swish(x, 0.065, 0.20, 1.25, 5100, 333224);
        Metal(x, 0.042, 0.17, 0.052, 2050);
        return x;
    }
    static double[] Impact()
    {
        double[] x = Clip(0.34);
        Crack(x, 0, 0.08, 0.66, 333225);
        Thud(x, 0.001, 0.18, 0.53, 174, 333226);
        Metal(x, 0.004, 0.27, 0.22, 1260);
        // A second short scrape belongs to the crossing blades, not a second game attack.
        Swish(x, 0.016, 0.24, 0.78, 3600, 333227);
        Crack(x, 0.079, 0.066, 0.32, 333236);
        Metal(x, 0.082, 0.23, 0.15, 1670);
        return x;
    }
    static double[] Hurt()
    {
        double[] x = Clip(0.24);
        Thud(x, 0, 0.20, 0.80, 147, 333228);
        Swish(x, 0.006, 0.19, 0.53, 1900, 333229);
        Metal(x, 0.007, 0.17, 0.078, 760);
        return x;
    }
    static double[] Death()
    {
        double[] x = Clip(0.56);
        Swish(x, 0, 0.36, 0.54, 1300, 333230);
        Thud(x, 0.16, 0.30, 0.47, 97, 333231);
        Metal(x, 0.183, 0.32, 0.19, 510);
        Metal(x, 0.26, 0.23, 0.11, 740);
        Swish(x, 0.23, 0.31, 0.23, 1000, 333232);
        return x;
    }
    static double[] Dash()
    {
        double[] x = Clip(0.30);
        Swish(x, 0, 0.285, 1.25, 3100, 333233);
        Thud(x, 0.005, 0.09, 0.15, 110, 333234);
        Thud(x, 0.107, 0.10, 0.11, 139, 333235);
        return x;
    }
    static string Write(string directory, string name, double[] x, double peakTarget, double suggestedGain)
    {
        // Remove DC before applying 2 ms/18 ms sample-edge tapers. No tail beyond clip end.
        double dc = 0, peak = 0;
        for (int i = 0; i < x.Length; i++) dc += x[i];
        dc /= x.Length;
        for (int i = 0; i < x.Length; i++)
        {
            x[i] = (x[i] - dc) * Smooth(i / (Rate * 0.002)) * Smooth((x.Length - 1 - i) / (Rate * 0.018));
            peak = Math.Max(peak, Math.Abs(x[i]));
        }
        double gain = peakTarget / Math.Max(peak, 0.000001), square = 0;
        int clipping = 0;
        using (BinaryWriter w = new BinaryWriter(File.Create(Path.Combine(directory, name + ".wav"))))
        {
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + x.Length * 2);
            w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
            w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2);
            w.Write((short)2); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(x.Length * 2);
            for (int i = 0; i < x.Length; i++)
            {
                double sample = gain * x[i];
                if (Math.Abs(sample) >= 1) clipping++;
                square += sample * sample;
                w.Write((short)Math.Round(sample * 32767));
            }
        }
        if (clipping != 0) throw new InvalidOperationException(name + " clips");
        return "{" + String.Format(CultureInfo.InvariantCulture,
            "\"name\":\"{0}\",\"seconds\":{1:F3},\"samples\":{2},\"peakDbfs\":{3:F2},\"rmsDbfs\":{4:F2},\"clippedSamples\":{5},\"suggestedOneShotGain\":{6:F2}",
            name, x.Length / (double)Rate, x.Length, 20 * Math.Log10(peakTarget), 10 * Math.Log10(square / x.Length), clipping, suggestedGain) + "}";
    }
    public static string Generate(string directory)
    {
        Directory.CreateDirectory(directory);
        var clips = new List<string>();
        clips.Add(Write(directory, "SwordDraw", Draw(), 0.56, 0.22));
        clips.Add(Write(directory, "SwordSwing", Swing(), 0.66, 0.42));
        clips.Add(Write(directory, "CrossSlashImpact", Impact(), 0.70, 0.52));
        clips.Add(Write(directory, "GwangmaHurt", Hurt(), 0.62, 0.32));
        clips.Add(Write(directory, "GwangmaDeath", Death(), 0.64, 0.40));
        clips.Add(Write(directory, "Dash", Dash(), 0.56, 0.23));
        return "{\"format\":\"PCM16 mono\",\"sampleRate\":44100,\"source\":\"Original deterministic synthesis; no recordings or external samples\",\"clips\":[" + String.Join(",", clips.ToArray()) + "]}";
    }
}