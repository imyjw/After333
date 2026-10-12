# Gwangma original sword-combat sound design

Created on 2026-09-26 for the Gwangma animation and X-shaped sword-slash presentation. The sounds are original deterministic synthesis: filtered air/cloth noise, low impact transients and short inharmonic steel resonances. No recordings, downloaded samples, soundfonts, imitation voices or external services are used. These are synthesized effects, not recorded sword or Foley performances.

## Assets and suggested synchronization

Resource prefix: `Project333/Audio/Gwangma/`.

| Clip | Length | Intended cue | Initial one-shot gain |
| --- | ---: | --- | ---: |
| SwordDraw | 0.25 s | Start of the raised/crossed-blade anticipation | 0.22 |
| SwordSwing | 0.28 s | Blades begin the diagonal crossing cut | 0.42 |
| CrossSlashImpact | 0.34 s | X slash reaches the target's shared hit anchor | 0.52 |
| GwangmaHurt | 0.24 s | Gwangma hurt response | 0.32 |
| GwangmaDeath | 0.56 s | Gwangma collapse; cloth fall and metal settling | 0.40 |
| Dash | 0.30 s | Begin advancing toward the target | 0.23 |

The swing contains two close air swishes for the two blades of one crossing motion. The impact contains a short body transient and a restrained steel shimmer. These clips do not bake in a complete multi-hit attack or counterattack; the presentation must trigger each strike according to its actual visual timeline. Keep draw and dash quieter than the impact. Do not loop dash once per animation frame or play hurt audio once per flash frame.

Use the shared battle 2D AudioSource/bounded presentation audio pool and cache clips. Preserve `After333AudioSettings` master volume through `AudioListener.volume`; do not multiply that master gain into the one-shot a second time or route SFX through a music-volume component. Cancellation, reconnect and scene cleanup should stop the presentation-owned voices. No gameplay results depend on these clips.

## Format and validation

All six WAVs are mono PCM16 at 44.1 kHz. Their total duration is 1.97 seconds, with 173,754 bytes of PCM payload (about 170 KiB; roughly 339 KiB decoded float samples). Short SFX use Decompress On Load, PCM and preload enabled; audio normalization and background loading are disabled. This avoids streaming startup latency for very short action cues.

Sample-edge fades are 2 ms in and 18 ms out. There is no long reverb tail. Generated peaks are between -5.04 and -3.10 dBFS before the one-shot gain. `GwangmaAudioValidation.json` records synthesis measurements. `GwangmaAudioIndependentValidation.json` independently parses the actual RIFF chunks and sample payloads: all six files have the expected format and duration, zero clipped samples, zero first/last samples, absolute DC below 0.00029 and unique Unity GUIDs. This validates the files and import settings, not perceptual balance in the complete game mix; final mix judgment requires in-game listening.

## Regeneration

From `C:/Project_333`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Art/GenerateGwangmaAudio.ps1
```

The execution-policy override is limited to the new PowerShell process. The generator compiles `GwangmaAudio.cs`, recreates only this set of WAV files, writes its measurement report and preserves existing Unity GUIDs. It does not build the app, control the Editor, change scenes or overwrite the original `Assets/Unit/Gwangma` sprites.
