// Original After333 ManaCannon cues: azure charge/launch and airy crystal dispersion.
// Deterministic synthesis only; no recordings, external samples or Unity dependencies.
using System;
using System.Globalization;
using System.IO;
using System.Text;

public static class ManaCannonAudio
{
    const int Rate = 44100;
    const double Tau = Math.PI * 2;
    sealed class Noise
    {
        uint state;
        public Noise(uint seed) { state = seed; }
        public double Next()
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return state / 2147483647.5 - 1;
        }
    }
    static double Smooth(double x)
    {
        x = Math.Max(0, Math.Min(1, x));
        return x * x * (3 - 2 * x);
    }
    static double LP(double x, ref double state, double hz)
    {
        state += (1 - Math.Exp(-Tau * hz / Rate)) * (x - state);
        return state;
    }
    static double Crystal(double time, double onset, double strength, double frequency, double decay)
    {
        double t = time - onset;
        if (t <= 0) return 0;
        double env = Smooth(t / .004) * Math.Exp(-t * decay);
        return strength * env * (
            .74 * Math.Sin(Tau * (frequency * t - frequency * .05 * t * t))
          + .22 * Math.Sin(Tau * frequency * 1.493 * t) * Math.Exp(-t * 14)
          + .07 * Math.Sin(Tau * frequency * 2.017 * t) * Math.Exp(-t * 26));
    }
    static double[] Shot()
    {
        const double duration = .40;
        var result = new double[(int)Math.Round(Rate * duration)];
        var noise = new Noise(333100101u);
        double breathLP = 0, breathLow = 0, releaseLP = 0, releaseLow = 0;
        double flicker = 0, outputLP = 0, outputLow = 0, chargePhase = 0, releasePhase = 0;
        for (int i = 0; i < result.Length; i++)
        {
            double t = i / (double)Rate;
            double rise = Smooth(t / .285);
            chargePhase += Tau * (345 + 730 * rise * rise) / Rate;
            double chargeEnv = Smooth(t / .024) * Smooth((.325 - t) / .042)
                             * (.26 + .74 * rise);
            double charge = chargeEnv * (
                .108 * Math.Sin(chargePhase)
              + .038 * Math.Sin(chargePhase * 1.501 + .4)
              + .014 * Math.Sin(chargePhase * 2.007 + .8));
            double n = noise.Next();
            double breath = LP(n, ref breathLP, 1500 + 2600 * rise)
                          - LP(n, ref breathLow, 450);
            charge += .090 * breath * chargeEnv;

            double launchTime = Math.Max(0, t - .300);
            double launchEnv = t > .300
                ? Smooth(launchTime / .010) * Math.Exp(-launchTime * 19)
                  * Smooth((duration - t) / .045) : 0;
            double releaseNoise = noise.Next();
            double release = LP(releaseNoise, ref releaseLP, 6200 - 1800 * Smooth(launchTime / .15))
                           - LP(releaseNoise, ref releaseLow, 680);
            releasePhase += Tau * (1400 + 2100 * Math.Exp(-launchTime * 24)) / Rate;
            double mod = .93 + .12 * LP(noise.Next(), ref flicker, 60);
            double launch = launchEnv * (.64 * release * mod + .032 * Math.Sin(releasePhase));
            double glints = Crystal(t, .034, .014, 1740, 37)
                          + Crystal(t, .112, .013, 2130, 39)
                          + Crystal(t, .190, .012, 2640, 44)
                          + Crystal(t, .303, .024, 1980, 34);
            double mix = .72 * charge + launch + glints;
            mix = LP(mix, ref outputLP, 6700);
            result[i] = mix - LP(mix, ref outputLow, 190);
        }
        return result;
    }
    static double[] Explosion()
    {
        const double duration = .35;
        var result = new double[(int)Math.Round(Rate * duration)];
        var noise = new Noise(333100102u);
        double airLP = 0, airLow = 0, dustLP = 0, dustLow = 0, outputLP = 0, outputLow = 0;
        double shimmerPhase = 0;
        for (int i = 0; i < result.Length; i++)
        {
            double t = i / (double)Rate;
            double bloom = Smooth(t / .011) * Math.Exp(-t * 16)
                         * Smooth((duration - t) / .090);
            double n = noise.Next();
            double air = LP(n, ref airLP, 5350 - 2100 * Smooth(t / .20))
                       - LP(n, ref airLow, 650);
            double dustNoise = noise.Next();
            double dust = LP(dustNoise, ref dustLP, 7200)
                        - LP(dustNoise, ref dustLow, 2350);
            // No sub-bass pulse or blunt physical thud; the blast disperses as air and crystals.
            double spread = .56 * air * bloom + .055 * dust * bloom;
            double crystals = Crystal(t, .002, .058, 828, 21)
                            + Crystal(t, .003, .041, 1276, 26)
                            + Crystal(t, .007, .028, 1771, 30)
                            + Crystal(t, .012, .019, 2347, 35)
                            + Crystal(t, .060, .014, 1515, 34)
                            + Crystal(t, .111, .009, 2048, 40);
            shimmerPhase += Tau * (760 + 660 * Math.Exp(-t * 19)) / Rate;
            double shimmer = .025 * bloom * Math.Sin(shimmerPhase);
            double mix = spread + crystals + shimmer;
            mix = LP(mix, ref outputLP, 6600);
            result[i] = mix - LP(mix, ref outputLow, 280);
        }
        return result;
    }
    static string WriteCue(string directory, string name, double[] samples, double peakDb, double oneShotGain)
    {
        string path = Path.Combine(directory, name + ".wav");
        double mean = 0, peak = 0;
        foreach (double x in samples) mean += x;
        mean /= samples.Length;
        for (int i = 0; i < samples.Length; i++)
        {
            // Keep a 64-sample zero guard at each seam, then fade smoothly.
            double fade = Smooth((i - 64) / (Rate * .010))
                        * Smooth((samples.Length - 65 - i) / (Rate * .038));
            samples[i] = (samples[i] - mean) * fade;
            peak = Math.Max(peak, Math.Abs(samples[i]));
        }
        double targetPeak = Math.Pow(10, peakDb / 20), gain = targetPeak / peak;
        double squares = 0, dc = 0, actualPeak = 0;
        int clipped = 0;
        using (var writer = new BinaryWriter(new FileStream(path, FileMode.CreateNew)))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(Rate); writer.Write(Rate * 2);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
            foreach (double x in samples)
            {
                double value = x * gain;
                if (Math.Abs(value) >= 1) clipped++;
                short pcm = (short)Math.Round(value * 32767);
                writer.Write(pcm);
                double v = pcm / 32768.0;
                actualPeak = Math.Max(actualPeak, Math.Abs(v)); squares += v * v; dc += v;
            }
        }
        if (clipped != 0) throw new InvalidOperationException("Clipped " + name);
        return "{" + String.Format(CultureInfo.InvariantCulture,
            "\"name\":\"{0}\",\"format\":\"PCM16 mono\",\"sampleRate\":{1},\"seconds\":{2:F3},\"samples\":{3},\"peakDbfs\":{4:F8},\"rmsDbfs\":{5:F8},\"dcOffset\":{6:F12},\"clippedSamples\":{7},\"firstSample\":0,\"lastSample\":0,\"suggestedOneShotGain\":{8:F2},\"source\":\"Original deterministic synthesis; no recordings or external samples\"",
            name, Rate, samples.Length / (double)Rate, samples.Length, 20 * Math.Log10(actualPeak),
            10 * Math.Log10(squares / samples.Length), dc / samples.Length, clipped, oneShotGain) + "}";
    }
    public static string Generate(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        foreach (string name in new string[] { "ManaShot", "ManaExplosion" })
            if (File.Exists(Path.Combine(outputDirectory, name + ".wav")))
                throw new IOException("Refusing to overwrite existing " + name + ".wav; choose a fresh export directory.");
        string shot = WriteCue(outputDirectory, "ManaShot", Shot(), -20, .45);
        string explosion = WriteCue(outputDirectory, "ManaExplosion", Explosion(), -19, .40);
        return "{\"sampleRate\":44100,\"clips\":[" + shot + "," + explosion + "]}";
    }
}
