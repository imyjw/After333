// Original After333 GaebangChief punch preparation/action cue.
// Quiet fist/cloth air and delicate coin jingle; no body impact, samples or recordings.
using System;
using System.Globalization;
using System.IO;
using System.Text;
public static class GaebangChiefCoinPunchAudio
{
    const int Rate = 44100;
    const double Duration = .44;
    const double Tau = Math.PI * 2;
    sealed class Noise
    {
        uint state;
        public Noise(uint seed) { state = seed; }
        public double Next() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state / 2147483647.5 - 1; }
    }
    static double Smooth(double x)
    {
        x = Math.Max(0, Math.Min(1, x)); return x * x * (3 - 2 * x);
    }
    static double LP(double x, ref double state, double hz)
    {
        state += (1 - Math.Exp(-Tau * hz / Rate)) * (x - state); return state;
    }
    static double Coin(double time, double onset, double strength, double detune)
    {
        double t = time - onset;
        if (t <= 0) return 0;
        // Inharmonic, short and soft: a small coin jostle rather than a large bell.
        double e = Math.Exp(-t * 38) * Smooth(t / .0018);
        return strength * e * (
            .63 * Math.Sin(Tau * (2380 * detune) * t) * Math.Exp(-t * 8)
          + .35 * Math.Sin(Tau * (3867 * detune) * t) * Math.Exp(-t * 17)
          + .15 * Math.Sin(Tau * (5688 * detune) * t) * Math.Exp(-t * 27)
          + .07 * Math.Sin(Tau * (7115 * detune) * t) * Math.Exp(-t * 50));
    }
    static double[] Synthesize()
    {
        var noise = new Noise(3332120931u);
        double[] samples = new double[(int)Math.Round(Rate * Duration)];
        double airLP = 0, airLow = 0, clothLP = 0, clothLow = 0, flutter = 0, outputLP = 0, outputDC = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double t = i / (double)Rate;
            double sweep = Math.Exp(-Math.Pow((t - .173) / .076, 2));
            double anticipation = .18 * Math.Exp(-Math.Pow((t - .053) / .027, 2));
            double n = noise.Next();
            double air = LP(n, ref airLP, 1900 + 1850 * sweep) - LP(n, ref airLow, 260);
            double cloth = LP(noise.Next(), ref clothLP, 1100) - LP(n, ref clothLow, 190);
            double mod = .94 + .16 * LP(noise.Next(), ref flutter, 45);
            double swish = (.65 * air + .24 * cloth) * (sweep + anticipation) * mod;
            double coins = Coin(t, .026, .071, 1)
                         + Coin(t, .084, .045, .957)
                         + Coin(t, .219, .032, 1.033);
            double envelope = Smooth(t / .007) * Smooth((Duration - t) / .048);
            double mix = (swish + coins) * envelope;
            mix = LP(mix, ref outputLP, 6900);
            samples[i] = (mix - LP(mix, ref outputDC, 125)) * Smooth(t / .006)
                       * Smooth((Duration - t) / .026);
        }
        return samples;
    }
    public static string Generate(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        string path = Path.Combine(outputDirectory, "CoinPunch.wav");
        double[] samples = Synthesize();
        double mean = 0, peak = 0;
        foreach (double x in samples) mean += x;
        mean /= samples.Length;
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = (samples[i] - mean) * Smooth(i / (Rate * .006))
                       * Smooth((samples.Length - 1 - i) / (Rate * .026));
            peak = Math.Max(peak, Math.Abs(samples[i]));
        }
        const double targetPeak = .10;
        double gain = targetPeak / peak, squares = 0, dc = 0;
        int clipped = 0;
        using (var writer = new BinaryWriter(File.Create(path)))
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
                double v = pcm / 32768.0; squares += v * v; dc += v;
            }
        }
        if (clipped != 0) throw new InvalidOperationException("Clipped CoinPunch.");
        return "{" + String.Format(CultureInfo.InvariantCulture,
            "\"name\":\"CoinPunch\",\"format\":\"PCM16 mono\",\"sampleRate\":{0},\"seconds\":{1:F3},\"samples\":{2},\"peakDbfs\":{3:F4},\"rmsDbfs\":{4:F4},\"dcOffset\":{5:F9},\"clippedSamples\":{6},\"firstSample\":0,\"lastSample\":0,\"suggestedOneShotGain\":0.55,\"source\":\"Original deterministic synthesis; no recordings or external samples\"",
            Rate, samples.Length / (double)Rate, samples.Length, 20 * Math.Log10(targetPeak),
            10 * Math.Log10(squares / samples.Length), dc / samples.Length, clipped) + "}";
    }
}