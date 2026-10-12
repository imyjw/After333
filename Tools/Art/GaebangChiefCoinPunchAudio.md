# GaebangChief CoinPunch audio — 2026-09-30

Original quiet preparation/action cue for the elderly beggar martial artist's fist attack and coin-eye theme. One effect only: soft hand/cloth air and a restrained coin jostle. There is no low body impact, separate hurt/death cue, or duplicated common UnitHit.

## Export

- Authored WAV: `TempBuild/GaebangChiefPresentation_20260930/Export/Resources/Project333/Units/GaebangChief/Audio/CoinPunch.wav`
- Final resource key: `Project333/Units/GaebangChief/Audio/CoinPunch`
- Reports and hash manifest: `TempBuild/GaebangChiefPresentation_20260930/Export/AudioReports/`
- 0.440 seconds, mono PCM16, 44,100 Hz, 19,404 samples.
- RIFF container: 38,852 bytes; PCM payload: 38,808 bytes.
- Actual decoded peak: -19.99947 dBFS; RMS: -35.84322 dBFS.
- Source: original deterministic DSP. No external samples, recordings, model audio generation or dependency downloads.

The restrained wind sweep reaches its strongest moment around 0.19 seconds. Tiny inharmonic coin jostles start at 0.026, 0.084 and 0.219 seconds. Fade-in/out and a high-pass stage remove boundary clicks and bass rumble. Start once with the punch preparation/swing, before the existing common UnitHit at contact.

Initial one-shot gain recommendation: **0.55**, on a presentation-owned 2D AudioSource obeying the existing `AudioListener.volume` and pause settings. Do not multiply the master gain into the source a second time. Runtime hookup, importer settings and final mix remain owned by the integrating task.

## Verification

- Independent RIFF chunk parser confirmed PCM format, file length and duration.
- Zero clipped samples; DC 0.0000000283.
- First and last 64 samples are zero; both edge-step checks pass.
- Re-generating with the fixed seed produced identical SHA-256:
  `a2c3fc15e7fc1f62fcc467300eeda35a8e5e387a47a212c30e61a6d5d8e378f1`.
- File and waveform checks passed. No device or complete in-game listening check is claimed.

## Reproduce

From `C:/Project_333`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Art/GenerateGaebangChiefCoinPunchAudio.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Art/ValidateGaebangChiefCoinPunchAudio.ps1
```

The generator accepts `-ProjectRoot`, `-OutputDirectory` and `-ReportDirectory`; the validator accepts `-ProjectRoot`, `-AudioFile` and `-ReportDirectory`. Both default to the authoring export paths above. The PowerShell execution-policy override applies only to each launched process. They do not write Assets or Unity metadata, or modify runtime code, other audio, clips, scenes or card data.

If imported into Unity, use PCM, Decompress On Load, Preserve Sample Rate, preload enabled, background loading disabled, forceToMono disabled (already mono) and importer normalization disabled. Preserve existing GUIDs. Native Unity import and production resource placement belong to the integrating task.