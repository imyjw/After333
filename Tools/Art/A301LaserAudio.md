# A-301 soft laser attack sound — 2026-09-30

Original deterministic DSP cue for A-301's laser attack. The design is a rounded,
descending electronic chirp with a very faint air layer. It uses no source recording
or third-party sample. Only this attack cue was created; common UnitHit and other
characters' audio remain unchanged.

- Active asset: `Assets/Project333/Resources/Project333/Units/A-301/Audio/LaserShot.wav`
- Unity resource key: `Project333/Units/A-301/Audio/LaserShot`
- Duration: **0.260 s**; **11,466 samples**; mono PCM16; **44,100 Hz**.
- File size: **22,976 bytes**.
- Level: peak **-14.000 dBFS**; RMS **-25.011 dBFS**. Recommended one-shot gain: **0.30**.
- Fades: **16 ms raised-cosine attack / 50 ms raised-cosine release**.
- Tone: continuous descending oscillator `180 + 720 × exp(-t / 0.045)` Hz,
  a quiet rounded second harmonic and low body layer, plus very faint filtered air.
  A 2,200 Hz low-pass limits the sharp upper register; no click or loud hiss layer.
- SHA-256: `3cfff82da0af3e250f287386d77769dde4a42388127a036ce35d7d063d882b87`.

The level deliberately matches the quieter A-212 revision. Perceived comfort still
depends on playback; numerical analysis does not establish a listening judgment.

## Validation

`A301LaserAudioValidation.json` records the synthesis parameters and waveform
measurements. `A301LaserAudioIndependentValidation.json` uses a separate PowerShell
RIFF/PCM parser, without the numpy/scipy synthesis helper.

- Mono PCM16 / 44,100 Hz headers, byte rate, block alignment and complete RIFF size passed.
- First and last PCM samples zero; clipped samples zero; fade boundary checks passed.
- DC average **0.0000739012**, below the 0.001 limit.
- Spectral energy above 4 kHz: approximately **0.00000222%** of total.
- A second synthesis export reproduced the WAV byte-for-byte; the active copy has the same hash.
- Export into Unity Assets is rejected; the default export folder is outside Assets.

Evidence: `TempBuild/A301Presentation_20260930/Audio/{Prepared,Reproduced}` and the
independent validation record in that Audio folder. Checks concern file and waveform
properties; no device/in-game listening check or client build is claimed here.

## Reproduce and replace

From `C:/Project_333`, using installed Python 3.13 with numpy and scipy:

```powershell
& "$env:LOCALAPPDATA/Programs/Python/Python313/python.exe" Tools/Art/PrepareA301LaserAudio.py
```

The exporter defaults to `TempBuild/A301LaserAudioExport`, outside Unity Assets,
and refuses any destination beneath Assets. Listen to that export before replacing
`LaserShot.wav` at the active path; preserve its `.meta` once Unity has imported it.
The script never creates Unity metadata or automatically overwrites the active audio.

Validate the active WAV after replacement:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Art/ValidateA301LaserAudio.ps1
```

New metadata and AudioImporter settings are created by the parent Unity import step:
PCM / Decompress On Load / Preserve Sample Rate / preload enabled. Restart Play Mode
after replacing a cached clip, and rebuild/install the client to update a phone app.
No per-unit hit or death audio was added.
