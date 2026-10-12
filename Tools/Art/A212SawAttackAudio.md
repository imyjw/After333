# A-212 circular-saw attack sound — 2026-09-30

Current SawAttack uses the user's supplied circular-saw recording, replacing the earlier synthesized cue.

- Asset: `Assets/Project333/Resources/Project333/Units/A-212/Audio/SawAttack.wav`
- Unity resource key: `Project333/Units/A-212/Audio/SawAttack`
- Source copy: `Tools/Art/Source/A-212/Audio/bfcmusic-circular-saw-2-fx-325571.mp3`
- Selected source window: **3.020–3.500 s**, with no pitch or playback-speed change.
- Duration: **0.480 s**; mono PCM16; **44,100 Hz**; **42,380 bytes**.
- Processing: stereo average to mono, remove small DC offset, **20 ms fade in / 45 ms fade out**, baseline peak normalization to -4.00 dBFS, then a broad **-4 dB high shelf at 3,500 Hz** and **-6 dB overall trim** (4 dB quieter than the first softened revision). No post-EQ normalization, pitch/speed change or extra synthesized layers. This preserves the motor buzz while reducing the sharp upper frequencies.
- Current peak: **-13.893 dBFS**; RMS: **-25.159 dBFS**, **9.494 dB lower RMS** than the previous recording-based cue. Existing one-shot gain **0.30** and attack timing remain unchanged.

The cue plays once at visible saw contact for each attack/counter sequence. Existing four-hit sparks and common UnitHit continue independently. Runtime code, animations, VFX and `.meta` remain unchanged. The WAV duration matches the previous cue so its existing guarded audio tail behavior still applies.

## Validation

`A212SawAttackAudioValidation.json` records decoding and processing. `A212SawAttackAudioIndependentValidation.json` independently parses RIFF chunks and PCM with PowerShell, verifying headers, sample count, duration, clipping, DC and boundaries.

- 21,168 samples; first/last samples zero; clipped samples zero.
- Current exported WAV and active copy match SHA-256 **62d1d58d79ed43688b74752e1c780b3f344732c2859784c176d9fac60ea22697**.
- Audio metadata preserved byte-for-byte; GUID **f62caa99c2535aa41b0d5bac5d834035**.

Earlier softening evidence: `TempBuild/A212SawSoftened_20260930/{Prepared,Reproduced,applied.json,before.json}`. Earlier unsoftened recording evidence: `TempBuild/A212SawRecording_20260930/`. This validates the file and waveform; no device/in-game listening check or client build is claimed. The earlier 12 Unity presentation tests preceded this audio replacement and were not rerun for a same-path, same-format, same-duration WAV edit.

## Reproduce and replace

From `C:/Project_333`, using installed Python 3.13 with numpy 2.2.5, soundfile 0.13.1 and libsndfile 1.2.2:

```powershell
& "$env:LOCALAPPDATA/Programs/Python/Python313/python.exe" Tools/Art/PrepareA212SawAttackFromRecording.py
```

The exporter defaults to `TempBuild/A212SawRecordingExport`, outside Unity Assets. Listen to the exported WAV, then copy only `SawAttack.wav` over the active asset above; preserve its existing `.meta`. Validate the active file with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Art/ValidateA212SawAttackAudio.ps1
```

Unity's existing AudioImporter remains PCM / Decompress On Load / Preserve Sample Rate / preload enabled. Unity automatically reimports the changed file. Restart Play Mode after replacing cached audio; rebuild/install the client to update an installed phone app.

The original synthesized WAV and processing report are archived as `Tools/Art/Archive/A212SawAttackSynthesized_20260930.wav` and `A212SawAttackSynthesizedValidation_20260930.json`. `A212SawAttackAudio.cs` and `GenerateA212SawAttackAudio.ps1` remain legacy synthesis tools; their export now defaults outside Assets and does not replace the current recording-based cue.

## Softer revision — 2026-09-30

Per the user's request for a slightly calmer, less unpleasant sound, the upper register is softened with a broad high shelf and the complete cue is turned down. The original 0.480s source window, 20/45ms fades, sample format, pitch and runtime gain remain intact. The previous recording-based WAV is preserved at `Tools/Art/Archive/A212SawAttackRecordedBeforeSoftening_20260930.wav`. File and boundary checks passed; perceived comfort still depends on playback and is not claimed from numerical checks alone.

## Additional volume reduction — 2026-09-30

Per the user's further request, reduced overall level by another 4dB, retaining the EQ, source window, fades and playback timing. Export trim is now -6dB total; runtime one-shot gain remains 0.30. Current evidence: `TempBuild/A212SawQuieter_20260930/`; the immediately preceding WAV is preserved as `before-SawAttack.wav` there.
