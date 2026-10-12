// Original After333 A-212 circular-saw attack. Deterministic synthesis, no external samples.
using System;
using System.Globalization;
using System.IO;
using System.Text;

public static class A212SawAttackAudio
{
    const int Rate = 44100;
    const double Duration = .48;
    const double Tau = Math.PI * 2;
    sealed class Noise
    {
        uint value;
        public Noise(uint seed) { value = seed; }
        public double Next() { value ^= value << 13; value ^= value >> 17; value ^= value << 5; return value / 2147483647.5 - 1; }
    }
    sealed class Resonance
    {
        readonly double feedback, previousGain, inputGain;
        double previous, previousPrevious;
        public Resonance(double frequency, double bandwidth)
        {
            double r = Math.Exp(-Math.PI * bandwidth / Rate);
            feedback = 2 * r * Math.Cos(Tau * frequency / Rate);
            previousGain = r * r;
            inputGain = 1 - r;
        }
        public double Next(double x)
        {
            double y = inputGain * x + feedback * previous - previousGain * previousPrevious;
            previousPrevious = previous; previous = y;
            return y;
        }
    }
    static double Smooth(double x)
    {
        x = Math.Max(0, Math.Min(1, x)); return x * x * (3 - 2 * x);
    }
    static double LowPass(double x, ref double state, double cutoff)
    {
        state += (1 - Math.Exp(-Tau * cutoff / Rate)) * (x - state); return state;
    }
    static double Motor(double phase)
    {
        // The dense but band-limited harmonic stack is an electric motor under load,
        // rather than an engine's exhaust pulses. Two slightly detuned motors beat.
        double value = .62 * Math.Sin(phase) + .25 * Math.Sin(phase * 2) + .19 * Math.Sin(phase * 3);
        for (int h = 4; h <= 26; h++)
            value += .42 * Math.Sin(phase * h) / Math.Pow(h, .90) * Math.Exp(-h / 28.0);
        return value;
    }
    static double[] SawAttack()
    {
        double[] samples = new double[(int)Math.Round(Rate * Duration)];
        var noise = new Noise(2122026093);
        var resonanceA = new Resonance(1320, 160);
        var resonanceB = new Resonance(2280, 210);
        var resonanceC = new Resonance(3670, 280);
        double motorPhaseA = 0, motorPhaseB = .67, toothPhase = 0;
        double gritLow = 0, gritHigh = 0, movement = 0, fineLow = 0, finalLow = 0, dcLow = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double t = i / (double)Rate;
            double spinup = Smooth(t / .065);
            double load = Smooth((t - .055) / .023) * (1 - Smooth((t - .335) / .035));
            double rundown = Smooth((t - .35) / .13);
            double variation = LowPass(noise.Next(), ref movement, 58);
            double speed = 121 + 61 * spinup - 27 * load - 69 * rundown;
            double flutter = 1 + .006 * Math.Sin(Tau * 37 * t) + .016 * variation;
            motorPhaseA += Tau * speed * flutter / Rate;
            motorPhaseB += Tau * (speed * 1.086 + 2.2) * (1 - .009 * variation) / Rate;
            toothPhase += Tau * (speed * 8.1 + 60) / Rate;
            double motor = .32 * Motor(motorPhaseA) + .29 * Motor(motorPhaseB);
            // Circular-blade tooth passes, not a conventional sword whoosh.
            double teeth = .105 * Math.Sin(toothPhase) + .055 * Math.Sin(toothPhase * 2.03);
            double n = noise.Next();
            double gritty = LowPass(n, ref gritLow, 6600) - LowPass(n, ref gritHigh, 540);
            double fine = LowPass(noise.Next(), ref fineLow, 4300);
            double chatter = .63 + .23 * Math.Sin(Tau * 103 * t) + .14 * Math.Sin(Tau * 167 * t + .8);
            double scratch = gritty * (1.04 * load + .12 * spinup) * chatter;
            double steel = .65 * resonanceA.Next(gritty * load)
                         + .42 * resonanceB.Next(gritty * load)
                         + .24 * resonanceC.Next(gritty * load);
            double envelope = Smooth(t / .005) * (1 - .45 * rundown)
                            * Smooth((Duration - t) / .054);
            double dry = motor * (.64 + .26 * load) + teeth * (.36 + .64 * load)
                       + 1.50 * scratch + .65 * steel + .035 * fine * spinup;
            double driven = Math.Tanh(dry * 1.35) * envelope;
            driven = LowPass(driven, ref finalLow, 7500);
            double highpassed = driven - LowPass(driven, ref dcLow, 38);
            samples[i] = highpassed * Smooth(t / .003) * Smooth((Duration - t) / .020);
        }
        return samples;
    }
    public static string Generate(string audioDirectory)
    {
        Directory.CreateDirectory(audioDirectory);
        string path = Path.Combine(audioDirectory, "SawAttack.wav");
        double[] samples = SawAttack();
        double average = 0;
        foreach (double x in samples) average += x;
        average /= samples.Length;
        double peak = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = (samples[i] - average) * Smooth(i / (Rate * .003))
                       * Smooth((samples.Length - 1 - i) / (Rate * .020));
            peak = Math.Max(peak, Math.Abs(samples[i]));
        }
        double normalization = .64 / peak, sum = 0, dc = 0, maxStep = 0;
        int clipped = 0; double previous = 0;
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(Rate); writer.Write(Rate * 2);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
            for (int i = 0; i < samples.Length; i++)
            {
                double value = samples[i] * normalization;
                if (Math.Abs(value) >= 1) clipped++;
                short pcm = (short)Math.Round(value * 32767);
                writer.Write(pcm);
                double decoded = pcm / 32768.0;
                sum += decoded * decoded; dc += decoded;
                maxStep = Math.Max(maxStep, Math.Abs(decoded - previous)); previous = decoded;
            }
        }
        if (clipped != 0) throw new InvalidOperationException("Clipped A-212 saw attack.");
        return "{" + String.Format(CultureInfo.InvariantCulture,
            "\"name\":\"SawAttack\",\"format\":\"PCM16 mono\",\"sampleRate\":{0},\"seconds\":{1:F3},\"samples\":{2},\"peakDbfs\":{3:F4},\"rmsDbfs\":{4:F4},\"dcOffset\":{5:F8},\"clippedSamples\":{6},\"maxAdjacentSampleStep\":{7:F6},\"firstSample\":0,\"lastSample\":0,\"suggestedOneShotGain\":0.30,\"source\":\"Original deterministic synthesis; no recordings or external samples\"",
            Rate, samples.Length / (double)Rate, samples.Length, 20 * Math.Log10(.64),
            10 * Math.Log10(sum / samples.Length), dc / samples.Length, clipped, maxStep) + "}";
    }
}