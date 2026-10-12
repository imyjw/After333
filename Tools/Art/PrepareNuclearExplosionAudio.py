# Legacy DSP exporter. Current audio uses PrepareNuclearExplosionFromRecording.py.
"""Create an original rounded Nuclear explosion cue using deterministic DSP.

The default export is outside Unity Assets. This tool refuses output underneath
Assets and never replaces the active WAV or creates Unity metadata. Review the
export before copying it into the game. Requires numpy and scipy.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import wave

import numpy as np
from scipy import signal


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_OUTPUT = ROOT / "TempBuild/NuclearExplosionAudioExport"
RATE = 44100
DURATION = 1.680
ATTACK = 0.020
RELEASE = 0.180
PEAK_DBFS = -12.0
SEED = 3330930


def db(value):
    return float(20 * math.log10(max(float(value), 1e-15)))


def filtered_noise(rng, count, low, high):
    samples = signal.sosfilt(
        signal.butter(3, [low, high], btype="bandpass", fs=RATE, output="sos"),
        rng.normal(0.0, 1.0, count))
    return samples / max(float(np.sqrt(np.mean(samples ** 2))), 1e-12)


def synthesize():
    count = round(DURATION * RATE)
    time = np.arange(count, dtype=np.float64) / RATE
    rng = np.random.default_rng(SEED)
    # A compact, band-limited pressure burst instead of a sharp crack or long hiss.
    burst = 0.34 * filtered_noise(rng, count, 90, 1050) * np.exp(-time / 0.125)
    soft_air = 0.09 * filtered_noise(rng, count, 180, 1650) * np.exp(-time / 0.075)
    # Continuous pitched body drops into a restrained rounded low rumble.
    body_frequency = 83.0 + 125.0 * np.exp(-time / 0.055)
    body_phase = 2.0 * math.pi * np.cumsum(body_frequency) / RATE
    body = 0.66 * (np.sin(body_phase) + 0.18 * np.sin(2 * body_phase + 0.18)) * np.exp(-time / 0.230)
    tail_frequency = 58.0 + 37.0 * np.exp(-time / 0.33)
    tail_phase = 2.0 * math.pi * np.cumsum(tail_frequency) / RATE
    rounded_tail = (np.sin(tail_phase) + 0.24 * np.sin(2 * tail_phase + 0.30) +
                    0.06 * np.sin(3 * tail_phase + 0.10))
    rounded_tail *= 0.68 * np.exp(-time / 0.48) * (0.92 + 0.08 * np.cos(2 * math.pi * 3.1 * time))
    pressure_tail = 0.12 * filtered_noise(rng, count, 55, 310) * np.exp(-time / 0.48)
    raw = burst + soft_air + body + rounded_tail + pressure_tail
    # Limit excessive sub-bass and high-frequency edge before the final fades.
    raw = signal.sosfilt(signal.butter(3, 48, btype="highpass", fs=RATE, output="sos"), raw)
    raw = signal.sosfilt(signal.butter(3, 2100, fs=RATE, output="sos"), raw)
    fade = np.ones(count)
    attack_count = round(ATTACK * RATE)
    release_count = round(RELEASE * RATE)
    fade[:attack_count] = 0.5 - 0.5 * np.cos(np.linspace(0, math.pi, attack_count))
    fade[-release_count:] = 0.5 + 0.5 * np.cos(np.linspace(0, math.pi, release_count))
    clip = raw * fade
    # Remove residual mean through the same envelope, keeping both endpoints zero.
    clip -= (clip.sum() / fade.sum()) * fade
    clip *= 10 ** (PEAK_DBFS / 20) / float(np.abs(clip).max())
    clip[0] = clip[-1] = 0
    if not np.isfinite(clip).all():
        raise ValueError("Non-finite synthesized sample")
    pcm = np.rint(clip * 32768).astype("<i2")
    pcm[0] = pcm[-1] = 0
    if ((pcm == -32768) | (pcm == 32767)).any():
        raise ValueError("Clipped PCM sample")
    return pcm


def export(output):
    output = Path(output).resolve()
    assets = (ROOT / "Assets").resolve()
    if output == assets or assets in output.parents:
        raise ValueError("Export outside Assets; do not overwrite current artist audio automatically.")
    output.mkdir(parents=True, exist_ok=True)
    pcm = synthesize()
    path = output / "NuclearExplosion.wav"
    with wave.open(str(path), "wb") as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(RATE)
        writer.writeframes(pcm.tobytes())
    normalized = pcm.astype(np.float64) / 32768
    spectrum = np.abs(np.fft.rfft(normalized)) ** 2
    bins = np.fft.rfftfreq(len(pcm), 1 / RATE)
    window = round(0.1 * RATE)
    window_rms = [db(np.sqrt(np.mean(normalized[start:start + window] ** 2)))
                  for start in range(0, len(pcm), window)]
    report = {
        "cue": "Nuclear rounded explosion with low rumble tail",
        "source": "Original deterministic DSP; no external recording, third-party sample or model used.",
        "samples": len(pcm), "duration_seconds": len(pcm) / RATE,
        "channels": 1, "sample_rate": RATE, "format": "WAV PCM16",
        "attack_seconds": ATTACK, "release_seconds": RELEASE,
        "peak_dbfs": db(np.abs(normalized).max()),
        "rms_dbfs": db(np.sqrt(np.mean(normalized ** 2))),
        "dc_average": float(normalized.mean()),
        "clipped_samples": int(((pcm == -32768) | (pcm == 32767)).sum()),
        "first_sample": int(pcm[0]), "last_sample": int(pcm[-1]),
        "max_sample_step": float(np.abs(np.diff(normalized)).max()),
        "spectral_energy_below_45hz_ratio": float(spectrum[bins < 45].sum() / spectrum.sum()),
        "spectral_energy_above_4khz_ratio": float(spectrum[bins > 4000].sum() / spectrum.sum()),
        "window_seconds": 0.1, "window_rms_dbfs": window_rms,
        "filters_hz": {"highpass": 48, "lowpass": 2100}, "seed": SEED,
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "file_bytes": path.stat().st_size,
        "runtime_one_shot_gain_recommended": 0.30,
        "passed": bool(np.abs(normalized.mean()) < 0.001 and pcm[0] == 0 and pcm[-1] == 0),
        "validation_limit": "File/waveform metrics only; device/in-game listening and subjective comfort are not claimed.",
    }
    (output / "NuclearExplosionAudioValidation.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    export(parser.parse_args().output)
