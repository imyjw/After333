## Current common hit replacement — 2026-09-28

UnitHit.wav now uses the user-provided independent_nu_ljudbank-hits_and_punches/hits/55.flac. Source retained at Tools/Art/Source/CommonHit/55.flac. Converted to WAV PCM16 stereo/44,100 Hz, 1.124331 seconds, with no gain, trimming, channel mixing or resampling. Decoded PCM MD5 matches source: f3fac118dac18fb9525ef6e21009bfa1. Existing Unity metadata/GUID and runtime gain .52 remain unchanged. All occupants share this sound, including counter damage to the initiating attacker through PlayDamageSound.

The synthesis records below describe the superseded UnitHit only; FireBreath remains synthesized and unchanged. Do not run GenerateRedDragonAndUnitHitAudio.ps1 over the current common hit: it regenerates the old synthetic clip. Source conversion evidence: TempBuild/CommonHit_20260928/conversion-verification.txt.

# RedDragon / common hit sound design — 2026-09-27

Two original procedural effects. No recordings, external samples, paid libraries, model API, or licensing dependencies.

| Clip | Length | Format | Peak | RMS | Runtime one-shot gain |
| --- | --- | --- | --- | --- | --- |
| `Assets/Project333/Resources/Project333/Audio/RedDragon/FireBreath.wav` | 0.460 s | Mono PCM16 / 44,100 Hz | -3.609 dBFS | -15.310 dBFS | 0.45 |
| `Assets/Project333/Resources/Project333/Audio/Common/UnitHit.wav` | 0.180 s | Mono PCM16 / 44,100 Hz | -4.152 dBFS | -21.606 dBFS | 0.52 |

FireBreath layers rounded, band-limited air, low pressure and turbulent flame movement. It has one rapid swell and a short soft tail. UnitHit is a neutral padded contact plus a compact low-mid thud; it contains no voice, metallic ring or element-specific sound. These are synthesized designs; human/device listening quality remains subject to user feedback.

## Reproduce

Run `Tools/Art/GenerateRedDragonAndUnitHitAudio.ps1` in PowerShell with process-scope script execution enabled. Generator: `RedDragonAndUnitHitAudio.cs`. Fixed noise seeds make the exported bytes repeatable. The JSON report records sample counts, peaks/RMS/DC and clipping. Both clips have zero clipped samples and tapered first/last samples.

The generator writes WAVs, not Unity YAML. Import through Unity's AudioImporter API: PCM, DecompressOnLoad, PreserveSampleRate, preloadAudioData true, forceToMono false (the authored WAV is already mono, so skip conversion/normalization), loadInBackground false. Preserve existing GUIDs on regeneration.

## Runtime ownership

- RedDragon breath VFX emits FireBreath once at the attack's breath start; no RedDragon-specific hurt or death sound is added.
- `TileTextView.PlayBeAttackedAnimation` emits the same cached UnitHit for every damaged unit/building/master. Battle presentation calls it only for `TookDamage`, before the death animation if the damage is lethal. It covers direct attacks, counters and spell/effect impact sequences using that shared path.
- A damage presentation can aggregate multiple damage events into one reaction per occupant. Hits on the same frame share one audible cue to prevent coherent waveform stacking in AoE; hits on later frames still play separately. Tile disable/rebind/combat reset releases the shared cue owner, and SubsystemRegistration clears static state even with domain reload disabled. This does not change combat rules.
- WaterCannonImpact, CrossSlashImpact and GwangmaHurt are no longer played. Those source files remain preserved as old authored assets. DragonHurt and DragonDeath were removed from resources and the BlueDragon generator at the user's request on 2026-10-23; the remaining death playback call was also removed.
- Gwangma death audio stays distinct from damage impact sounds; BlueDragon has no dedicated death sound. `PlayDeathAnimation` does not emit UnitHit, so destruction without damage is silent for the hit sound.
- Existing global volume remains `After333AudioSettings -> AudioListener.volume`. Sources retain listener volume/pause behavior, reuse one AudioSource per tile and stop on disable/rebind.

## Verification

`BattleCommonHitAudioTests` covers Unity resource import/data headroom and source lifecycle for BlueDragon, Gwangma, RedDragon, generic unit, master and building. Full task validation results are recorded by the integrating task. Programmatic waveform checks do not claim device listening verification.