// Original deterministic procedural BlueDragon sound design. No external samples.
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
public static class BlueDragonAudioSynthesizer
{
    public const int Rate = 44100;
    const double Tau = Math.PI * 2;
    sealed class Noise
    {
        uint s;
        public Noise(uint seed) { s = seed; }
        public double Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s / 4294967295.0; }
        public double Sample() { return 2 * Next() - 1; }
    }
    static double Smooth(double x) { x = Math.Max(0, Math.Min(1, x)); return x * x * (3 - 2 * x); }
    static double LP(double sample, ref double state, double frequency)
    { state += (1 - Math.Exp(-Tau * frequency / Rate)) * (sample - state); return state; }
    static void Bubbles(double[] dst, Noise noise, int count, double start, double end, double amplitude, double baseHz)
    {
        for (int b = 0; b < count; b++)
        {
            double at = start + (end - start) * noise.Next();
            double hz = baseHz * (0.6 + noise.Next() * 1.4), decay = 0.014 + 0.042 * noise.Next();
            double amp = amplitude * (0.35 + 0.65 * noise.Next()) * (1 - 0.5 * at / (dst.Length / (double)Rate));
            double phase = 0;
            int offset = (int)(at * Rate);
            for (int i = 0; i < (int)(decay * 7 * Rate) && offset + i < dst.Length; i++)
            {
                double t = i / (double)Rate;
                phase += Tau * hz * (1 + 0.35 * (1 - Math.Exp(-t / 0.018))) / Rate;
                double env = Smooth(t / 0.0015) * Math.Exp(-t / decay);
                dst[offset + i] += amp * env * (Math.Sin(phase) + 0.19 * Math.Sin(2.07 * phase));
            }
        }
    }
    static void Thump(double[] dst, double at, double seconds, double amplitude, double high, double low)
    {
        int offset = (int)(at * Rate);
        double phase = 0;
        for (int i = 0; i < (int)(seconds * Rate) && offset + i < dst.Length; i++)
        {
            double t = i / (double)Rate;
            phase += Tau * (low + (high - low) * Math.Exp(-t * 23)) / Rate;
            dst[offset + i] += amplitude * Smooth(t / 0.004) * Math.Exp(-t * 15) * Math.Sin(phase);
        }
    }
    static void Space(double[] dst, double amount)
    {
        double[] dry = (double[])dst.Clone(), delays = { 0.037, 0.061, 0.097, 0.143 };
        for (int k = 0; k < delays.Length; k++)
        {
            int offset = (int)(delays[k] * Rate);
            double low = 0;
            for (int i = offset; i < dst.Length; i++)
                dst[i] += amount / (k + 1) * LP(dry[i - offset], ref low, 2900 - k * 450);
        }
    }
    // Turbulent pressure + resonant bubbles; each stage has a different envelope.
    static double[] Water(int kind)
    {
        double seconds = kind == 0 ? 0.38 : kind == 1 ? 0.58 : 0.82;
        double[] dst = new double[(int)Math.Round(seconds * Rate)];
        Noise noise = new Noise((uint)(333101 + kind));
        double low = 0, bottom = 0, texture = 0, phase = 0;
        for (int i = 0; i < dst.Length; i++)
        {
            double t = i / (double)Rate, x = t / seconds;
            double attack = kind == 0 ? 0.06 : kind == 1 ? 0.007 : 0.0017;
            double env = Smooth(t / attack) * (kind == 0 ? Math.Pow(1 - x, 0.6) : Math.Exp(-t * (kind == 1 ? 7.7 : 7.3)));
            double cutoff = kind == 0 ? 550 + 1900 * x : kind == 1 ? 1150 + 2100 * Math.Exp(-t * 6) : 2400 + 2500 * Math.Exp(-t * 35);
            double n = noise.Sample();
            double wash = LP(n, ref low, cutoff), sub = LP(n, ref bottom, kind == 0 ? 70 : kind == 1 ? 100 : 180);
            double mod = LP(noise.Sample(), ref texture, kind == 2 ? 53 : 27);
            phase += Tau * (130 + 130 * x * x) / Rate;
            double body = kind == 0 ? 0.48 * (wash - sub) + 0.04 * Math.Sin(phase) : (wash - sub) * (kind == 1 ? 1.1 + 1.3 * mod : 0.82 + 1.9 * mod);
            dst[i] = env * body;
        }
        if (kind == 0) Bubbles(dst, noise, 26, 0.015, 0.30, 0.08, 570);
        if (kind == 1) { Thump(dst, 0, 0.30, 0.42, 185, 66); Bubbles(dst, noise, 38, 0.025, 0.37, 0.10, 640); }
        if (kind == 2) { Thump(dst, 0.003, 0.34, 0.73, 205, 61); Bubbles(dst, noise, 58, 0.025, 0.61, 0.14, 870); Bubbles(dst, noise, 13, 0.005, 0.25, 0.15, 290); }
        Space(dst, 0.1 + kind * 0.03);
        return dst;
    }
    // Nonverbal fantasy creature texture, made from FM harmonics and filtered noise.
    static double[] Creature(bool death)
    {
        double seconds = death ? 1.12 : 0.34;
        double[] dst = new double[(int)Math.Round(seconds * Rate)];
        Noise noise = new Noise(death ? 333105U : 333104U);
        double phase = 0, breath = 0, bottom = 0, modulation = 0;
        for (int i = 0; i < dst.Length; i++)
        {
            double t = i / (double)Rate;
            double env = Smooth(t / (death ? 0.022 : 0.007)) * Math.Pow(1 - t / seconds, death ? 1.5 : 1.3);
            double jitter = LP(noise.Sample(), ref modulation, 36);
            double hz = death ? 69 + 117 * Math.Exp(-t * 3.5) : 92 + 84 * Math.Exp(-t * 13);
            phase += Tau * hz * (1 + jitter * 0.25 + 0.035 * Math.Sin(t * 56)) / Rate;
            double harmonics = Math.Sin(phase) + 0.55 * Math.Sin(phase * 2) + 0.29 * Math.Sin(phase * 3) + 0.14 * Math.Sin(phase * 5);
            double noiseBody = LP(noise.Sample(), ref breath, death ? 980 - 550 * t / seconds : 1500);
            double low = LP(noiseBody, ref bottom, 110);
            double flutter = 0.68 + 0.32 * Math.Sin(t * (death ? 107 : 152));
            dst[i] = env * (0.21 * harmonics * flutter + 0.52 * (noiseBody - low));
        }
        if (death) { Thump(dst, 0.39, 0.30, 0.28, 120, 49); Bubbles(dst, noise, 19, 0.50, 0.90, 0.037, 440); }
        else Thump(dst, 0, 0.13, 0.24, 176, 78);
        Space(dst, death ? 0.12 : 0.07);
        return dst;
    }
    static string Write(string dir, string name, double[] data, double peakTarget, double suggestedGain)
    {
        double mean = 0, peak = 0;
        for (int i = 0; i < data.Length; i++) mean += data[i];
        mean /= data.Length;
        for (int i = 0; i < data.Length; i++)
        {
            double taper = Smooth(i / (Rate * 0.002)) * Smooth((data.Length - 1 - i) / (Rate * 0.035));
            data[i] = Math.Tanh((data[i] - mean) * 1.2) * taper;
            peak = Math.Max(peak, Math.Abs(data[i]));
        }
        double gain = peakTarget / Math.Max(0.00001, peak), square = 0;
        int clipCount = 0;
        using (BinaryWriter writer = new BinaryWriter(File.Create(Path.Combine(dir, name + ".wav"))))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + data.Length * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(Rate); writer.Write(Rate * 2);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(data.Length * 2);
            for (int i = 0; i < data.Length; i++)
            {
                double sample = data[i] * gain;
                square += sample * sample;
                if (Math.Abs(sample) >= 1) clipCount++;
                writer.Write((short)Math.Round(sample * 32767));
            }
        }
        if (clipCount != 0) throw new InvalidOperationException(name + " contains clipped samples");
        return "{" + string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "\"name\":\"{0}\",\"seconds\":{1:F3},\"samples\":{2},\"peakDbfs\":{3:F2},\"rmsDbfs\":{4:F2},\"clippedSamples\":{5},\"suggestedOneShotGain\":{6:F2}",
            name, data.Length / (double)Rate, data.Length, 20 * Math.Log10(peakTarget), 10 * Math.Log10(square / data.Length), clipCount, suggestedGain) + "}";
    }
    public static string Generate(string dir)
    {
        Directory.CreateDirectory(dir);
        List<string> reports = new List<string>();
        reports.Add(Write(dir, "WaterCannonCharge", Water(0), 0.63, 0.26));
        reports.Add(Write(dir, "WaterCannonLaunch", Water(1), 0.78, 0.55));
        reports.Add(Write(dir, "WaterCannonImpact", Water(2), 0.82, 0.65));
        return "{\"format\":\"PCM16 mono\",\"sampleRate\":44100,\"source\":\"Original deterministic synthesis; no recordings\",\"clips\":[" + string.Join(",", reports.ToArray()) + "]}";
    }
}
