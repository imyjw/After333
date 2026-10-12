// After333 original scores and deterministic offline instrument synthesis.
// No recordings, soundfonts, third-party code, external samples, services or packages.
// Render: powershell -NoProfile -File Tools/Audio/GenerateAfter333Music.ps1
using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;

public static class After333Music
{
    const int Rate = 44100, TableSize = 16384;
    const double Tau = 2 * Math.PI;
    static readonly double[] Sine = MakeWave(false), StringWave = MakeWave(true);
    enum Instrument { Strings, Flute, Zither, Bell, Bass, Spiccato, Horn, Taiko, Snare, Shaker, Gong, Piano }
    sealed class Note
    {
        public Instrument Voice; public int Pitch; public double Beat, Length, Gain, Pan;
        public Note(Instrument voice, int pitch, double beat, double length, double gain, double pan)
        { Voice = voice; Pitch = pitch; Beat = beat; Length = length; Gain = gain; Pan = pan; }
    }
    sealed class Chord
    {
        public int Bass; public int[] Notes;
        public Chord(int bass, params int[] notes) { Bass = bass; Notes = notes; }
    }
    sealed class Score
    {
        public string File, Title; public int Tempo, Bars; public List<Note> Notes = new List<Note>();
        public Score(string file, string title, int tempo, int bars) { File=file; Title=title; Tempo=tempo; Bars=bars; }
        public void Add(Instrument voice, int pitch, double beat, double length, double gain, double pan)
        { Notes.Add(new Note(voice,pitch,beat,length,gain,pan)); }
    }
    sealed class Noise
    {
        uint state; public Noise(uint seed) { state=seed; }
        public double Next() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state/4294967295.0; }
        public double Sample() { return 2*Next()-1; }
    }
    static double[] MakeWave(bool strings)
    {
        double[] wave = new double[TableSize+1];
        for(int i=0;i<TableSize;i++) {
            double p=Tau*i/TableSize;
            if(!strings) wave[i]=Math.Sin(p);
            else for(int h=1;h<=12;h++) wave[i]+=Math.Sin(p*h+0.08*h)/Math.Pow(h,1.72);
        }
        wave[TableSize]=wave[0]; return wave;
    }
    static double Wave(double[] table, double phase)
    {
        phase-=Math.Floor(phase); double x=phase*TableSize; int ix=(int)x;
        return table[ix]+(table[ix+1]-table[ix])*(x-ix);
    }
    static double Sin(double phase) { return Wave(Sine,phase); }
    static double Smooth(double x) { x=Math.Max(0,Math.Min(1,x)); return x*x*(3-2*x); }
    static double Frequency(int pitch) { return 440*Math.Pow(2,(pitch-69)/12.0); }
    static int Pitch(string s)
    {
        if(s=="R") return -1;
        int value = s[0]=='C'?0:s[0]=='D'?2:s[0]=='E'?4:s[0]=='F'?5:s[0]=='G'?7:s[0]=='A'?9:11;
        if(s.Length==3) value+=s[1]=='#'?1:-1;
        return 12*(1+(s[s.Length-1]-'0'))+value;
    }
    static void Phrase(Score score, Instrument voice, double bar, double gain, double pan, string text)
    {
        double at=bar*4; string[] tokens=text.Split(new char[]{' ','|','\r','\n'},StringSplitOptions.RemoveEmptyEntries);
        for(int i=0;i<tokens.Length;i++) {
            string[] pair=tokens[i].Split(':'); int pitch=Pitch(pair[0]);
            double len=double.Parse(pair[1],CultureInfo.InvariantCulture);
            if(pitch>=0) score.Add(voice,pitch,at+0.018*Math.Sin(i*2.7),len*0.89,gain*(0.91+0.08*Math.Sin(i*1.9)),pan+0.04*Math.Sin(i));
            at+=len;
        }
    }
    static readonly Chord[] Harmony = {
        new Chord(38,62,65,69,76), // Dm9
        new Chord(45,60,62,65,69), // Dm7/A
        new Chord(34,62,65,69,70), // Bbmaj7
        new Chord(33,60,65,69,72), // F/A, low A bass
        new Chord(43,62,65,67,69), // Gm9
        new Chord(41,62,65,69,74), // Dm/F
        new Chord(36,60,64,67,74), // Cadd9
        new Chord(45,61,64,67,69), // A7
        new Chord(41,60,65,69,76), // Fmaj7
        new Chord(40,60,64,67,74), // C/E
        new Chord(43,58,62,65,69), // Gm9 low
        new Chord(45,62,64,67,69)  // A7sus4
    };

    static Score Sanctuary()
    {
        // Revised user direction: a bright, still, unobtrusive piano room in F major.
        // Forty bars at 72 BPM, with a two-bar harmonic breath instead of a driving pulse.
        Score s=new Score("After333_Lobby","Sunlit Garden",72,40);
        Chord[] chords={
            new Chord(41,53,57,62), // F6: open, settled tonic
            new Chord(46,53,58,62), // Bb major
            new Chord(41,53,57,60), // F
            new Chord(40,55,60,64), // C/E
            new Chord(46,53,58,60), // Bbadd9
            new Chord(45,53,57,60), // F/A
            new Chord(43,53,58,62), // Gm7
            new Chord(48,55,60,64), // C, no dominant seventh or sharp leading tension
            new Chord(50,53,57,60), // Dm7 passing colour
            new Chord(41,53,58,62)  // Bb/F, plagal return
        };
        int[] progression={0,1, 2,3,4,5, 6,7,2,4, 8,6,7,5, 4,2,6,7, 9,0};
        for(int cell=0;cell<progression.Length;cell++) {
            Chord c=chords[progression[cell]];
            double at=cell*8, breath=0.96+0.035*Math.Sin(cell*0.83);
            // A low, open piano chord every two bars; individual keys are softly spread.
            s.Add(Instrument.Piano,c.Bass,at+0.02,3.4,0.056*breath,-0.23);
            for(int v=0;v<3;v++) {
                s.Add(Instrument.Piano,c.Notes[v],at+0.12+v*0.045,2.9,0.028*breath,-0.13+v*0.11);
                // Barely audible air, below the piano. No percussion, bells or high flute.
                s.Add(Instrument.Strings,c.Notes[v],at+0.30+v*0.05,7.3,0.0023,-0.52+v*0.52);
            }
            // Occasional soft chord replies, never a continuous arpeggio or rhythmic ostinato.
            if(cell%3!=1) {
                s.Add(Instrument.Piano,c.Notes[1],at+4.62,1.8,0.017,-0.05);
                s.Add(Instrument.Piano,c.Notes[2],at+4.69,1.8,0.018,0.12);
            }
        }
        // Intro: a few middle-register notes arrive after the chord has settled.
        Phrase(s,Instrument.Piano,0,0.054,0.12,
            "R:2 C4:2 | F4:2 R:2 | R:1 D4:2 F4:1 | A4:2 R:2");
        // A: the principal rising third is followed by a descending answer and long breaths.
        Phrase(s,Instrument.Piano,4,0.074,0.12,
            "R:0.5 A4:1.5 G4:1 F4:1 | C4:2 R:2 | G4:2 E4:1 C4:1 | R:2 E4:2");
        Phrase(s,Instrument.Piano,8,0.073,0.10,
            "F4:1.5 G4:0.5 A4:2 | D5:1 C5:1 R:2 | C5:1 A4:1 G4:2 | F4:2 R:2");
        // A answer: more space at the start, stepwise motion and a quiet tonic landing.
        Phrase(s,Instrument.Piano,12,0.070,0.12,
            "R:1 G4:2 F4:1 | D4:2 R:2 | E4:1 G4:1 C5:2 | G4:1 E4:1 R:2");
        Phrase(s,Instrument.Piano,16,0.073,0.11,
            "A4:2 G4:1 F4:1 | C4:1 F4:1 R:2 | D4:1 F4:1 A4:2 | G4:1 F4:1 R:2");
        // B: a slightly lower, reflective phrase; the overall level does not swell.
        Phrase(s,Instrument.Piano,20,0.068,0.09,
            "F4:2 A4:1 C5:1 | A4:2 R:2 | Bb4:1 A4:1 G4:2 | F4:1 D4:1 R:2");
        Phrase(s,Instrument.Piano,24,0.070,0.12,
            "E4:2 G4:1 C5:1 | G4:2 E4:1 R:1 | F4:2 A4:1 G4:1 | C4:2 R:2");
        // A return: familiar outline, new ending, no extra layers or louder percussion.
        Phrase(s,Instrument.Piano,28,0.073,0.12,
            "R:0.5 F4:1.5 G4:1 A4:1 | D5:1 C5:1 R:2 | A4:2 G4:1 F4:1 | C4:2 R:2");
        Phrase(s,Instrument.Piano,32,0.070,0.10,
            "D4:1 F4:1 G4:2 | Bb4:1 A4:1 R:2 | G4:2 E4:1 C4:1 | E4:2 R:2");
        // Plagal coda and a quiet F-major ending; the opening can return without a cadence hit.
        Phrase(s,Instrument.Piano,36,0.067,0.10,
            "F4:2 A4:1 G4:1 | F4:2 D4:1 R:1 | C4:2 F4:2 | A4:1 G4:1 F4:1 R:1");
        return s;
    }

    static Score Convergence()
    {
        Score s=new Score("After333_Battle","Convergence of Three",128,64);
        int[] cycleA={0,0,2,6,4,5,11,7,0,5,2,3,4,6,11,7};
        int[] cycleB={8,9,2,5,4,6,11,7,2,3,4,5,2,6,11,7};
        int[] middle={0,5,2,6,4,5,11,7};
        for(int bar=0;bar<64;bar++) {
            int ci=bar<16?cycleA[bar]:bar<32?cycleB[bar-16]:bar<40?middle[bar-32]:bar<56?cycleA[bar-40]:cycleB[bar-48];
            Chord c=Harmony[ci]; double at=bar*4;
            double energy=bar<4?0.68:bar<16?0.90:bar<32?1.03:bar<40?0.48:bar<56?1.10:0.98;
            bool quiet=bar>=32&&bar<40;
            for(int v=0;v<4;v++) s.Add(Instrument.Strings,c.Notes[v]-12,at+v*0.022,3.72,0.031*energy,(-0.72+v*0.48));
            s.Add(Instrument.Bass,c.Bass,at,quiet?3.6:1.72,0.13*energy,0);
            if(!quiet) {
                s.Add(Instrument.Bass,c.Bass,at+2,1.5,0.100*energy,0);
                if(bar%4==3) s.Add(Instrument.Bass,bar%16==15?37:c.Bass+7,at+3.5,0.38,0.075,0);
            }
            // 3+3+2 eighth-note accents echo the threefold theme without constant high percussion.
            int[] pattern=bar%2==0?new int[]{0,2,1,0,3,2,1,2}:new int[]{0,1,2,1,3,2,0,1};
            if(!quiet) for(int n=0;n<8;n++) {
                double accent=n==0||n==3||n==6?1.0:0.72;
                s.Add(Instrument.Spiccato,c.Notes[pattern[n]]-12,at+n*0.5+(n%2)*0.012,0.36,0.09*energy*accent,n%2==0?-0.43:0.39);
                if(bar>=16 && n%2==1) s.Add(Instrument.Zither,c.Notes[pattern[n]],at+n*0.5+0.02,0.5,0.031*energy,0.31);
            } else {
                s.Add(Instrument.Zither,c.Notes[0],at+0.5,1.6,0.068,-0.36);
                s.Add(Instrument.Zither,c.Notes[2]+12,at+2.5,1.1,0.049,0.34);
            }
            s.Add(Instrument.Taiko,38,at,1.15,0.19*energy,-0.08);
            if(!quiet) {
                s.Add(Instrument.Taiko,45,at+1.5,0.8,0.072*energy,0.20);
                s.Add(Instrument.Taiko,38,at+2,1.1,0.132*energy,-0.08);
                if(bar%2==1) s.Add(Instrument.Taiko,48,at+3.5,0.62,0.067*energy,-0.24);
                if(bar>=4) {
                    s.Add(Instrument.Snare,0,at+1,0.32,0.040*energy,0.05);
                    s.Add(Instrument.Snare,0,at+3,0.36,0.051*energy,0.05);
                }
                for(int n=0;n<8;n++) s.Add(Instrument.Shaker,0,at+0.25+n*0.5,0.10,0.023*energy*(n%2==0?0.55:1),0.42);
            } else if(bar%2==1) s.Add(Instrument.Taiko,45,at+2.75,0.7,0.048,0.24);
            if(bar%8==7&&!quiet) {
                // Lift into the next phrase; the last bar deliberately backs off to the first.
                double lift=bar==63?0.54:1;
                for(int n=0;n<4;n++) s.Add(Instrument.Taiko,new int[]{50,48,45,43}[n],at+3+n*0.25,0.6,0.048*lift*(1+n*0.12),0.33-n*0.18);
                if(bar!=63) s.Add(Instrument.Snare,0,at+3.75,0.3,0.037,-0.09);
            }
            if(bar%8==0) s.Add(Instrument.Gong,c.Notes[0]-12,at,4,quiet?0.025:0.041,-0.38);
            if(bar%4==2) s.Add(Instrument.Bell,c.Notes[2]+12,at+2.5,1.3,0.017,0.42);
        }
        Phrase(s,Instrument.Horn,0,0.124,-0.10,
            "D4:0.75 F4:0.75 A4:1.5 R:1 | D5:1 A4:1 F4:1 E4:1 | F4:1 D4:1 F4:0.5 A4:1 R:0.5 | G4:1 E4:1 C4:1 R:1");
        Phrase(s,Instrument.Flute,4,0.145,0.15,
            "D5:0.75 G5:0.75 A5:0.5 Bb5:1 A5:1 | A5:1 F5:1 E5:0.5 D5:1 R:0.5 | E5:1 G5:0.5 A5:1.5 D6:1 | C#6:1 A5:1 E5:1 R:1");
        Phrase(s,Instrument.Horn,8,0.127,-0.10,
            "D4:0.75 F4:0.75 A4:1.5 C5:1 | A4:1 F4:1 E4:1 D4:1 | F4:0.5 A4:1 D5:1.5 C5:1 | A4:2 R:2");
        Phrase(s,Instrument.Flute,12,0.147,0.13,
            "G5:0.75 A5:0.75 Bb5:1 A5:0.5 G5:1 | E5:1 G5:0.5 D6:1.5 C6:1 | A5:1 G5:1 E5:1 D5:1 | C#5:1 E5:1 A5:1 R:1");
        Phrase(s,Instrument.Horn,16,0.127,-0.10,
            "A4:1 C5:1 E5:1 D5:1 | C5:1 G4:1 E4:1 R:1 | F4:1 A4:1 D5:1 C5:1 | A4:1 F4:1 D4:1 R:1");
        Phrase(s,Instrument.Flute,20,0.143,0.15,
            "G5:0.5 A5:0.5 Bb5:1 D6:1 Bb5:1 | G5:1 E5:1 D5:1 C5:1 | E5:0.75 G5:0.75 A5:1.5 D6:1 | C#6:1 A5:1 E5:1 R:1");
        Phrase(s,Instrument.Horn,24,0.135,-0.10,
            "F4:0.75 A4:0.75 D5:1.5 C5:1 | A4:1 C5:1 F5:1 E5:1 | D5:1 Bb4:1 G4:1 A4:1 | F4:1 A4:1 D5:1 R:1");
        Phrase(s,Instrument.Flute,28,0.15,0.12,
            "D6:1 C6:0.5 A5:1.5 F5:1 | G5:1 E5:1 D5:1 C5:1 | E5:1 G5:0.5 A5:1.5 D6:1 | C#6:1 A5:1 E5:1 R:1");
        Phrase(s,Instrument.Flute,32,0.113,0.04,
            "D5:2 F5:1 A5:1 | C6:2 A5:1 R:1 | F5:1 A5:1 D6:2 | G5:2 E5:1 R:1");
        Phrase(s,Instrument.Flute,36,0.113,0.04,
            "G5:1 A5:1 Bb5:1 A5:1 | F5:2 E5:1 D5:1 | E5:1 G5:1 A5:2 | E5:1 C#5:1 A4:1 R:1");
        Phrase(s,Instrument.Horn,40,0.144,-0.12,
            "D4:0.75 F4:0.75 A4:1.5 D5:1 | C5:1 A4:1 F4:1 E4:1 | F4:1 A4:0.5 D5:1.5 C5:1 | G4:1 E4:1 C4:1 R:1");
        Phrase(s,Instrument.Flute,44,0.151,0.13,
            "D5:0.75 G5:0.75 A5:0.5 Bb5:1 A5:1 | C6:1 A5:1 F5:1 E5:1 | E5:1 G5:0.5 A5:1.5 D6:1 | C#6:1 A5:1 E5:1 R:1");
        Phrase(s,Instrument.Horn,48,0.14,-0.12,
            "D4:0.75 F4:0.75 A4:1.5 C5:1 | D5:1 C5:1 A4:1 F4:1 | F4:0.5 A4:1 D5:1.5 C5:1 | A4:2 R:2");
        Phrase(s,Instrument.Flute,52,0.154,0.13,
            "G5:0.75 A5:0.75 Bb5:1 A5:0.5 G5:1 | E5:1 G5:0.5 D6:1.5 C6:1 | A5:1 G5:1 E5:1 D5:1 | C#5:1 E5:1 A5:1 R:1");
        Phrase(s,Instrument.Horn,56,0.13,-0.10,
            "F4:0.75 A4:0.75 D5:1.5 C5:1 | A4:1 C5:1 F5:1 E5:1 | D5:1 Bb4:1 G4:1 A4:1 | F4:1 A4:1 D5:1 R:1");
        Phrase(s,Instrument.Flute,60,0.143,0.12,
            "D6:1 C6:0.5 A5:1.5 F5:1 | G5:1 E5:1 D5:1 C5:1 | E5:1 G5:1 A5:1 D6:1 | C#6:1 A5:1 E5:1 R:1");
        // Warm inner countermelodies only at high points, not constant doubled leads.
        Phrase(s,Instrument.Strings,24,0.032,0.30,"D4:4 | C4:4 | Bb3:2 D4:2 | A3:4");
        Phrase(s,Instrument.Strings,48,0.032,0.31,"F4:3 E4:1 | D4:4 | D4:2 F4:2 | C4:4");
        return s;
    }

    static void RenderPiano(Note note, int serial, double beatSeconds, float[] left, float[] right, float[] send)
    {
        // Felt-piano-like additive strings: no FM bell, sample playback or hard transient.
        // The upper hammer-excited partials decay faster than the lower string body.
        double hz=Frequency(note.Pitch), held=note.Length*beatSeconds, release=1.65;
        double seconds=held+release, fundamentalLoss=0.43+hz/1500;
        int count=(int)(seconds*Rate), index=(int)Math.Round(note.Beat*beatSeconds*Rate);
        while(index<0) index+=left.Length; index%=left.Length;
        double[] weights={1.0,0.35,0.23,0.105,0.055,0.027,0.013};
        double[] phases=new double[weights.Length],steps=new double[weights.Length],levels=new double[weights.Length],losses=new double[weights.Length];
        for(int h=0;h<weights.Length;h++) {
            int partial=h+1;
            phases[h]=0.015*(serial%11)+0.035*h;
            steps[h]=hz*partial*Math.Sqrt(1+0.000035*partial*partial)/Rate;
            levels[h]=weights[h];
            losses[h]=Math.Exp(-fundamentalLoss*(1+0.23*h*h)/Rate);
        }
        double pan=Math.Max(-0.7,Math.Min(0.7,note.Pan+(note.Pitch-60)*0.004));
        double gl=Math.Cos((pan+1)*Math.PI/4)*note.Gain, gr=Math.Sin((pan+1)*Math.PI/4)*note.Gain;
        double partner=0.073,partnerStep=hz*1.0007/Rate,lowL=0,lowR=0,hammer=0;
        Noise noise=new Noise((uint)(333726+serial*367));
        double hammerDecay=1,hammerLoss=Math.Exp(-85.0/Rate);
        double partnerLevel=0.13,partnerLoss=Math.Exp(-fundamentalLoss/Rate);
        for(int i=0;i<count;i++) {
            double t=i/(double)Rate, env=Smooth(t/0.017);
            if(t>held) env*=Smooth((seconds-t)/release);
            double value=0;
            for(int h=0;h<weights.Length;h++) {
                phases[h]+=steps[h]; value+=levels[h]*Sin(phases[h]); levels[h]*=losses[h];
            }
            partner+=partnerStep;
            hammer+=0.055*(noise.Sample()-hammer);
            double touch=0.06*hammer*hammerDecay;
            double a=value+partnerLevel*Sin(partner)+touch;
            double b=value+partnerLevel*Sin(partner+0.018)+touch;
            // A soft soundboard roll-off keeps the attack warm even on the highest melody note.
            lowL+=0.235*(a-lowL); lowR+=0.235*(b-lowR);
            a=lowL*env*gl; b=lowR*env*gr;
            left[index]+=(float)a; right[index]+=(float)b;
            send[index]+=(float)((a+b)*0.5*0.29);
            if(++index==left.Length) index=0;
            hammerDecay*=hammerLoss; partnerLevel*=partnerLoss;
        }
    }

    static void RenderNote(Note note, int serial, double beatSeconds, float[] left, float[] right, float[] send)
    {
        if(note.Voice==Instrument.Piano) { RenderPiano(note,serial,beatSeconds,left,right,send); return; }
        Instrument voice=note.Voice;
        bool percussion=voice==Instrument.Taiko||voice==Instrument.Snare||voice==Instrument.Shaker||voice==Instrument.Gong;
        double duration=note.Length*beatSeconds;
        double attack=voice==Instrument.Strings?0.24:voice==Instrument.Flute?0.065:voice==Instrument.Horn?0.058:voice==Instrument.Bass?0.025:0.003;
        double release=voice==Instrument.Strings?0.74:voice==Instrument.Flute?0.16:voice==Instrument.Horn?0.19:voice==Instrument.Zither?1.25:voice==Instrument.Bell?1.8:voice==Instrument.Bass?0.12:voice==Instrument.Gong?1.8:0.09;
        double seconds=duration+release;
        int count=(int)(seconds*Rate), index=(int)Math.Round(note.Beat*beatSeconds*Rate);
        while(index<0) index+=left.Length; index%=left.Length;
        double hz=Frequency(note.Pitch), step=hz/Rate;
        double p=0.037*(serial%17), q=p+0.231, r=p+0.713, drift=serial*0.137;
        Noise noise=new Noise((uint)(0xA333123+serial*7919)); double nl=0, nh=0, lp=0, lp2=0;
        double pan=Math.Max(-0.9,Math.Min(0.9,note.Pan));
        double gl=Math.Cos((pan+1)*Math.PI/4)*note.Gain, gr=Math.Sin((pan+1)*Math.PI/4)*note.Gain;
        double wet=voice==Instrument.Strings?0.38:voice==Instrument.Flute?0.42:voice==Instrument.Zither?0.35:voice==Instrument.Bass?0.08:percussion?0.16:0.30;
        double decay=1, decayRate=Math.Exp(-(voice==Instrument.Zither?2.6:voice==Instrument.Bell?1.65:voice==Instrument.Spiccato?9.0:voice==Instrument.Taiko?5.4:voice==Instrument.Snare?16:voice==Instrument.Shaker?29:voice==Instrument.Gong?1.3:0)/Rate);
        double bright=1, brightRate=Math.Exp(-(voice==Instrument.Zither?9.0:voice==Instrument.Spiccato?14:voice==Instrument.Bell?2.6:12)/Rate);
        for(int i=0;i<count;i++) {
            double t=i/(double)Rate;
            double envelope=Smooth(t/attack)*Smooth((seconds-t)/release);
            if(t>duration) envelope*=Smooth(1-(t-duration)/release);
            double a=0, b=0;
            double random=noise.Sample(); nl+=0.07*(random-nl); nh+=0.002*(random-nh);
            if(voice==Instrument.Strings) {
                double vibrato=0.00065*Sin(t*4.73+drift), swell=0.90+0.10*Sin(t*0.39+drift);
                p+=step*(1+vibrato); q+=step*(0.9978+0.0005*Sin(t*0.29+drift)); r+=step*(1.0026+0.0006*Sin(t*0.31+drift));
                a=(0.48*Wave(StringWave,p)+0.30*Wave(StringWave,q)+0.22*Wave(StringWave,r)+0.055*nl)*swell;
                b=(0.48*Wave(StringWave,p+0.001)+0.22*Wave(StringWave,q+0.037)+0.30*Wave(StringWave,r-0.043)+0.04*nl)*swell;
                lp+=0.16*(a-lp); lp2+=0.16*(b-lp2); a=lp; b=lp2;
            } else if(voice==Instrument.Flute) {
                double vibrato=0.0019*Smooth((t-0.1)/0.4)*Sin(t*4.86+drift);
                p+=step*(1+vibrato+0.0028*nl);
                double breath=(nl-nh)*0.22, air=0.86+0.07*Sin(t*5.1+drift);
                a=(Sin(p)+0.24*Sin(2*p+0.05)+0.085*Sin(3*p)+0.023*Sin(4*p))*0.77*air+breath;
                a+=0.07*bright*(nl-nh); b=a;
            } else if(voice==Instrument.Zither||voice==Instrument.Spiccato) {
                p+=step*(1+0.003*bright);
                double body=Sin(p)+0.36*Sin(p*2.002)+0.20*bright*Sin(p*3.004)+0.13*bright*Sin(p*4.012)+0.085*bright*Sin(p*6.02);
                if(voice==Instrument.Spiccato) body=0.67*body+0.25*Wave(StringWave,p)+0.10*bright*nl;
                a=body*decay*0.83; b=a;
            } else if(voice==Instrument.Bell) {
                p+=step;
                a=(Sin(p+0.21*bright*Sin(p*2))+0.18*Sin(p*2.003)+0.09*bright*Sin(p*3.997))*decay*0.62; b=a;
            } else if(voice==Instrument.Bass) {
                p+=step; a=0.78*Sin(p)+0.18*Sin(p*2)+0.05*Sin(p*3); b=a;
            } else if(voice==Instrument.Horn) {
                p+=step*(1+0.0007*Sin(t*4.1+drift)); q+=step*1.0013;
                double bloom=Smooth(t/0.15)*(0.80+0.14*Sin(t*0.7+drift));
                a=0.77*Sin(p)+bloom*(0.26*Sin(2*p)+0.16*Sin(3*p)+0.07*Sin(4*p))+0.11*Sin(q);
                lp+=0.12*(a-lp); a=lp; b=0.97*a+0.02*Sin(q+0.04);
            } else if(voice==Instrument.Taiko) {
                double baseHz=note.Pitch==38?57:note.Pitch==45?84:Frequency(note.Pitch)*0.63;
                p+=(baseHz+38*bright)/Rate;
                a=(0.86*Sin(p)+0.24*Sin(p*1.51)+0.09*Sin(p*2.13))*decay+0.20*bright*bright*(nl-nh);
                b=a;
            } else if(voice==Instrument.Snare) {
                p+=182.0/Rate; a=(0.23*Sin(p)+0.13*Sin(p*1.72)+2.7*(nl-nh))*decay; b=a;
            } else if(voice==Instrument.Shaker) {
                lp+=0.38*(random-lp); a=(lp-nl)*decay*0.69; b=a;
            } else {
                p+=step; a=(Sin(p)+0.37*Sin(p*1.493)+0.20*Sin(p*2.027)+0.09*Sin(p*2.711))*decay*0.56; b=a;
            }
            a*=envelope; b*=envelope;
            left[index]+=(float)(a*gl); right[index]+=(float)(b*gr);
            send[index]+=(float)((a*gl+b*gr)*0.5*wet);
            if(++index==left.Length) index=0;
            decay*=decayRate; bright*=brightRate;
            // Keep continuous phases for inharmonic plucks and membrane partials.
        }
    }

    static void Reverb(float[] left,float[] right,float[] send)
    {
        // Four damped combs per side, warmed by a complete cyclic pass. The long loop
        // is many RT60s; the following pass is periodic to floating-point precision.
        int[] dl={1557,1617,1491,1422}, dr={1580,1640,1514,1445};
        for(int ch=0;ch<2;ch++) {
            float[] target=ch==0?left:right; int[] delays=ch==0?dl:dr;
            for(int d=0;d<4;d++) {
                int size=(int)(delays[d]*1.67); float[] ring=new float[size]; int cursor=0;
                double filter=0, feedback=0.84;
                for(int pass=0;pass<2;pass++) for(int i=0;i<send.Length;i++) {
                    double delayed=ring[cursor]; filter=0.68*filter+0.32*delayed;
                    ring[cursor]=(float)(send[i]+feedback*filter);
                    if(pass==1) target[i]+=(float)(delayed*0.17);
                    if(++cursor==size) cursor=0;
                }
            }
        }
        // Sparse cross-field early reflections with exact circular indexing.
        double[] times={0.039,0.071,0.109,0.163,0.227,0.311};
        for(int d=0;d<times.Length;d++) {
            int from=left.Length-(int)(times[d]*Rate); double gain=0.29*Math.Pow(0.72,d);
            for(int i=0;i<left.Length;i++) {
                float v=(float)(send[from]*gain);
                left[i]+=d%2==0?v:v*0.41f; right[i]+=d%2==1?v:v*0.41f;
                if(++from==left.Length) from=0;
            }
        }
    }

    static void Master(float[] left,float[] right,double peakTarget)
    {
        // A circular two-pass 24 Hz high-pass and 8 kHz low-pass remove DC/harshness.
        for(int ch=0;ch<2;ch++) {
            float[] data=ch==0?left:right;
            double prev=0,hp=0,low=0,highCoeff=Math.Exp(-Tau*24/Rate),lowCoeff=1-Math.Exp(-Tau*8000/Rate);
            for(int pass=0;pass<2;pass++) for(int i=0;i<data.Length;i++) {
                double x=data[i], y=highCoeff*(hp+x-prev); prev=x; hp=y;
                low+=lowCoeff*(y-low);
                if(pass==1) data[i]=(float)low;
            }
            // Micro seam correction only (2.9 ms each side), no audible whole-song fades.
            // Wrapped synthesis already preserves all note and reverb tails.
            double delta=data[0]-data[data.Length-1];
            for(int i=0;i<128;i++) {
                double weight=1-Smooth(i/127.0);
                data[i]-=(float)(delta*0.5*weight);
                data[data.Length-1-i]+=(float)(delta*0.5*weight);
            }
        }
        double peak=0;
        for(int i=0;i<left.Length;i++) peak=Math.Max(peak,Math.Max(Math.Abs(left[i]),Math.Abs(right[i])));
        double gain=peakTarget/peak;
        for(int i=0;i<left.Length;i++) { left[i]=(float)(left[i]*gain); right[i]=(float)(right[i]*gain); }
    }

    static string Render(Score score,string directory)
    {
        double beatSeconds=60.0/score.Tempo;
        int frames=(int)Math.Round(score.Bars*4*beatSeconds*Rate);
        float[] left=new float[frames],right=new float[frames],send=new float[frames];
        for(int i=0;i<score.Notes.Count;i++) RenderNote(score.Notes[i],i,beatSeconds,left,right,send);
        Reverb(left,right,send);
        Master(left,right,score.File=="After333_Lobby"?0.354813389:0.708); // Lobby -9 dBFS; battle unchanged.
        string path=Path.Combine(directory,score.File+".wav");
        using(BinaryWriter w=new BinaryWriter(File.Create(path))) {
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36+frames*4);
            w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)2);
            w.Write(Rate); w.Write(Rate*4); w.Write((short)4); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(frames*4);
            Noise dither=new Noise(333202609);
            for(int i=0;i<frames;i++) {
                w.Write((short)Math.Round(left[i]*32767+(dither.Next()-dither.Next())*0.5));
                w.Write((short)Math.Round(right[i]*32767+(dither.Next()-dither.Next())*0.5));
            }
        }
        return "{\"file\":\""+score.File+".wav\",\"title\":\""+score.Title+"\",\"bpm\":"+score.Tempo+",\"bars\":"+score.Bars+",\"notes\":"+score.Notes.Count+",\"frames\":"+frames+"}";
    }

    public static string Generate(string directory) { return Generate(directory,"All"); }

    public static string Generate(string directory,string track)
    {
        if(track!="All"&&track!="Lobby"&&track!="Battle") throw new ArgumentException("Track must be All, Lobby or Battle.");
        Directory.CreateDirectory(directory);
        List<string> clips=new List<string>();
        if(track=="All"||track=="Lobby") clips.Add(Render(Sanctuary(),directory));
        GC.Collect();
        if(track=="All"||track=="Battle") clips.Add(Render(Convergence(),directory));
        return "{\"generator\":\"After333Music.cs\",\"sampleRate\":44100,\"channels\":2,\"bits\":16,\"clips\":["+string.Join(",",clips.ToArray())+"]}";
    }
}
