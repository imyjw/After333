> Historical record (superseded 2026-10-23). Current music uses user-provided After333_Lobby.mp3 and BattleBGM2.wav; see [current music documentation](../../docs/background_music.md). The scores, measurements, previews and generators below describe the old synthesized tracks. Do not run the old generator against current Assets: it would overwrite the battle track and recreate a duplicate lobby WAV.

# After333 original background music

Updated 2026-09-26. The user rejected the first lobby track and clarified the direction: **bright, calm, quiet background music**. The lobby composition was replaced in full with **Sunlit Garden / 햇살 정원**, a slow F-major piano piece. The battle audio and its Unity metadata were preserved byte-for-byte.

Both pieces are original scores and original offline procedural sound designs. No recordings, commercial compositions, soundfonts, external samples, downloaded media, AI music service or third-party DSP package is used. Instrument labels describe synthesized timbres, not acoustic recordings. Numerical verification does not establish subjective musical quality; no human listening review is claimed.

## Current deliverables

| Runtime source file | Title | Score | Source duration |
| --- | --- | --- | --- |
| Assets/Project333/Resources/Project333/Audio/BGM/After333_Lobby.wav | Sunlit Garden / 햇살 정원 | F major, 72 BPM, 4/4, 40 bars | 5,880,000 frames / 44,100 = 133.333333 s |
| Assets/Project333/Resources/Project333/Audio/BGM/After333_Battle.wav | Convergence of Three | D minor, 128 BPM, 4/4, 64 bars | 5,292,000 frames / 44,100 = 120 s |

## Sunlit Garden score and sound

- Bars 1–4: a few middle-register piano notes over open F6 and Bb harmony.
- Bars 5–12: a rising-third phrase, stepwise answers, and full beats of silence between ideas.
- Bars 13–20: a quieter answer and tonic landing; slow inversions keep the bass moving gently.
- Bars 21–28: a reflective middle phrase over passing Dm7/Gm7, returning through C and F/A.
- Bars 29–36: the familiar main contour returns with different endings; no additional loud layers.
- Bars 37–40: a soft Bb/F-to-F6 plagal return, ending on F and leaving room for the opening.

Harmony changes every two bars. Each harmony has a low piano note, a softly spread inner chord, and occasional quiet chord replies. There is no continuous arpeggio or rhythmic ostinato. The melody remains in C4–D5, with most notes below C5. The score contains 192 piano notes and 60 very quiet pad notes: 252 events total, down from 812 in the rejected version.

The new dedicated piano renderer uses slightly inharmonic strings, a weak detuned partner string, a 17 ms smooth attack, a very quiet filtered hammer component, and faster decay for each higher partial. A soft soundboard low-pass reduces the upper attack. There is no FM bell algorithm in this piano voice. The sustaining pad is mixed far below the piano; the lobby contains no flute, bells, horn, bass-synth, drums, snare, shaker or gong.

Note durations, pauses and gentle variations in gain/panning are written explicitly in After333Music.cs. The composition is not generated from a random melody or an endlessly repeating arpeggio.

## Unchanged battle score

Convergence of Three retains the D–F–A identity, 3+3+2 eighth-note accents, horn-like calls, flute replies, string-like spiccato, low membrane drums and restrained snare/shaker.

- Bars 1–16: theme A and call/response.
- Bars 17–32: relative-major B section and eight-bar transitions.
- Bars 33–40: reduced middle section, with continuous ostinato and snare removed.
- Bars 41–56: expanded A reprise.
- Bars 57–64: condensed B/turnaround, with the final fill reduced into the loop.

It contains 2,075 note events and remains mastered to about -3 dBFS peak.

## Loop and import treatment

Both sources are stereo PCM16 at 44.1 kHz. Note tails wrap around the full score. Separate damped comb reverberators and early reflections use cyclic warm-up; no whole-track fade-out or silent end padding is added. Circular 24 Hz high-pass / 8 kHz low-pass and a 128-sample local seam correction precede shared stereo gain adjustment and triangular dither. Channels are not normalized independently.

The lobby uses a -9 dBFS peak target, substantially quieter than the previous version; the battle's original -3 dBFS target is unchanged. Unity imports the source as stereo streaming Vorbis, quality 0.65, background loading, no preload, and no importer normalization. Runtime source gain remains the integration layer's responsibility.

## Reproduction

Generate the current lobby only, preserving battle files:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Audio/GenerateAfter333Music.ps1 -Track Lobby

Other supported values are Battle and All (the default). The C# renderer is compiled with the system .NET runtime and needs no Unity assemblies or external library. Seeds are fixed. The generator writes metadata only for rendered tracks, preserves existing GUIDs, and merges the selected track's render record into After333MusicRender.json without dropping the other track's record.

The current lobby entry point is Sanctuary(); its output/title are Sunlit Garden and After333_Lobby.wav. The internal method name is retained to avoid unnecessary generator churn.

## Independent source measurements (2026-09-26)

Measured by the separately written InspectAfter333Music.mjs PCM reader:

| Measure | Revised lobby | Unchanged battle |
| --- | --- | --- |
| Stereo peak | -9.00 dBFS | -3.00 dBFS |
| Overall stereo RMS | -24.97 dBFS | -17.10 dBFS |
| One-second RMS range | -46.25 to -20.75 dBFS | -22.17 to -15.09 dBFS |
| Clipped frames | 0 | 0 |
| First/last sample difference | 0 | 1 / 32,768 |
| L-R difference RMS | 0.02205 | 0.05872 |
| Channel correlation | 0.92403 | 0.91154 |

The lobby's quietest windows are written pauses and decaying tails. Its DC magnitude is below 0.000000013. PCM format, musical duration, stereo, headroom, DC and loop-boundary checks passed. Compressed import and runtime playback are verified separately by integration checks, and these signal measurements do not substitute for listening.

The 20-second lobby excerpt is docs/audio/After333_Lobby_Preview.wav. Preview fades belong only to excerpts, not the seamless game loops.

Battle preservation SHA256, identical before and after the lobby-only render:

- After333_Battle.wav: BA64056F38B739298796BF3BA8D3E6230B7080728474F2432B93D0441F5BB60B
- After333_Battle.wav.meta: F34ADFBCC3C69BFC5E0AD1D0D9908D60454B5CBD7E8EB4C6C5AB6559956D1EC3

This music-only revision does not alter gameplay, runtime code, scenes, networking, economy or server behavior. Updated audio reaches installed clients through the user's next client build.
