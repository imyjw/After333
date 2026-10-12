// Original After333 sound design. Deterministic synthesis; no samples or recordings.
using System;
using System.Globalization;
using System.IO;
using System.Text;
public static class RedDragonAndUnitHitAudio
{
    const int Rate = 44100;
    const double Tau = Math.PI * 2;
    sealed class Noise
    {
        uint value;
        public Noise(uint seed) { value = seed; }
        public double Next() { value ^= value << 13; value ^= value >> 17; value ^= value << 5; return value / 2147483647.5 - 1; }
    }
    static double Smooth(double x) { x = Math.Max(0, Math.Min(1, x)); return x * x * (3 - 2 * x); }
    static double LowPass(double x, ref double state, double cutoff)
    {
        state += (1 - Math.Exp(-Tau * cutoff / Rate)) * (x - state); return state;
    }
    static double[] FireBreath()
    {
        double[] samples = new double[(int)(Rate * .46)];
        var noise = new Noise(333927001);
        double bass = 0, treble = 0, bassLow = 0, wind = 0, windLow = 0, movement = 0, phase = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double t = i / (double)Rate, u = t / .46;
            // Air pressure builds rapidly, then decays into ember-like soft turbulence.
            double attack = Smooth(t / .026);
            double release = Math.Pow(1 - u, .88) * Smooth((.46 - t) / .092);
            double breath = LowPass(noise.Next(), ref wind, 1350 + 850 * Math.Exp(-t * 7));
            breath -= LowPass(breath, ref windLow, 240);
            double n = noise.Next();
            double body = LowPass(n, ref bass, 450) - LowPass(n, ref bassLow, 55);
            double flicker = .92 + .44 * LowPass(noise.Next(), ref movement, 45) + .08 * Math.Sin(t * Tau * 27);
            phase += Tau * (95 + 48 * Math.Exp(-t * 15)) / Rate;
            double pressure = (.55 * Math.Sin(phase) + .17 * Math.Sin(2.07 * phase) + .07 * Math.Sin(3.16 * phase)) * Math.Exp(-t * 10);
            // A restrained ignition transient, rounded below 4 kHz instead of raw hiss.
            double ignition = LowPass(noise.Next(), ref treble, 3800) * Math.Exp(-t * 72);
            samples[i] = Math.Tanh((2.5 * breath * flicker + 1.1 * body + .32 * pressure + .34 * ignition) * attack * release);
        }
        return samples;
    }
    static double[] UnitHit()
    {
        double[] samples = new double[(int)(Rate * .18)];
        var noise = new Noise(333927002);
        double body = 0, contact = 0, contactLow = 0, phase = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double t = i / (double)Rate;
            phase += Tau * (124 + 100 * Math.Exp(-t * 46)) / Rate;
            // A short neutral padded strike: no sword ring, voice, wet impact, or element.
            double thunk = (.74 * Math.Sin(phase) + .15 * Math.Sin(phase * 2.18)) * Math.Exp(-t * 36);
            double bodyNoise = LowPass(noise.Next(), ref body, 680) * Math.Exp(-t * 43);
            double n = noise.Next();
            double tap = (LowPass(n, ref contact, 3300) - LowPass(n, ref contactLow, 500)) * Math.Exp(-t * 115);
            samples[i] = (thunk + 1.7 * bodyNoise + .85 * tap) * Smooth(t / .0016) * Smooth((.18 - t) / .038);
        }
        return samples;
    }
    static string Write(string path, double[] samples, double targetPeak, double gain)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        double dc = 0, peak = 0, sum = 0;
        foreach (double x in samples) dc += x;
        dc /= samples.Length;
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = (samples[i] - dc) * Smooth(i / (Rate * .0015)) * Smooth((samples.Length - 1 - i) / (Rate * .012));
            peak = Math.Max(peak, Math.Abs(samples[i]));
        }
        double normalize = targetPeak / peak;
        int clipped = 0; double finalDc = 0;
        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + samples.Length * 2);
            w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
            w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2);
            w.Write((short)2); w.Write((short)16); w.Write(Encoding.ASCII.GetBytes("data")); w.Write(samples.Length * 2);
            foreach (double x in samples)
            {
                double v = x * normalize;
                if (Math.Abs(v) >= 1) clipped++;
                short pcm = (short)Math.Round(v * 32767);
                w.Write(pcm); sum += v * v; finalDc += v;
            }
        }
        if (clipped != 0) throw new InvalidOperationException("Clipping: " + path);
        return "{" + String.Format(CultureInfo.InvariantCulture,
            "\"name\":\"{0}\",\"seconds\":{1:F3},\"samples\":{2},\"peakDbfs\":{3:F3},\"rmsDbfs\":{4:F3},\"dcOffset\":{5:F7},\"clippedSamples\":{6},\"suggestedOneShotGain\":{7:F2}",
            Path.GetFileNameWithoutExtension(path), samples.Length / (double)Rate, samples.Length, 20 * Math.Log10(targetPeak), 10 * Math.Log10(sum / samples.Length), finalDc / samples.Length, clipped, gain) + "}";
    }
    public static string Generate(string resourcesAudioRoot)
    {
        string fire = Write(Path.Combine(resourcesAudioRoot, "RedDragon/FireBreath.wav"), FireBreath(), .66, .45);
        string hit = Write(Path.Combine(resourcesAudioRoot, "Common/UnitHit.wav"), UnitHit(), .62, .52);
        return "{\"format\":\"PCM16 mono\",\"sampleRate\":44100,\"source\":\"Original deterministic synthesis; no recordings or external samples\",\"clips\":[" + fire + "," + hit + "]}";
    }
}