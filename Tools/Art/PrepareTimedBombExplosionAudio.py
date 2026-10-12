"""Create an original quiet, compact dynamite explosion using deterministic DSP.

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
DEFAULT_OUTPUT = ROOT / "TempBuild/TimedBombExplosionAudioExport"
RATE = 44100
DURATION = 0.960
ATTACK = 0.016
RELEASE = 0.120
PEAK_DBFS = -14.0
SEED = 333093036


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
    # A dry pressure burst with no sharp crack, beeps, timer ticks or long hiss.
    burst = 0.39 * filtered_noise(rng, count, 105, 1350) * np.exp(-time / 0.072)
    soft_air = 0.065 * filtered_noise(rng, count, 260, 1850) * np.exp(-time / 0.040)
    # Shorter and slightly higher than Nuclear: a compact thud, not a long rumble.
    body_frequency = 82.0 + 95.0 * np.exp(-time / 0.038)
    body_phase = 2.0 * math.pi * np.cumsum(body_frequency) / RATE
    body = 0.67 * (np.sin(body_phase) + 0.14 * np.sin(2 * body_phase + 0.18)) * np.exp(-time / 0.145)
    tail_frequency = 74.0 + 22.0 * np.exp(-time / 0.13)
    tail_phase = 2.0 * math.pi * np.cumsum(tail_frequency) / RATE
    rounded_tail = np.sin(tail_phase) + 0.17 * np.sin(2 * tail_phase + 0.30)
    rounded_tail *= 0.44 * np.exp(-time / 0.25)
    pressure_tail = 0.10 * filtered_noise(rng, count, 75, 450) * np.exp(-time / 0.22)
    raw = burst + soft_air + body + rounded_tail + pressure_tail
    # Limit excessive sub-bass and high-frequency edge before the final fades.
    raw = signal.sosfilt(signal.butter(3, 60, btype="highpass", fs=RATE, output="sos"), raw)
    raw = signal.sosfilt(signal.butter(3, 1900, fs=RATE, output="sos"), raw)
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
    path = output / "TimedBombExplosion.wav"
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
        "cue": "TimedBomb quiet dynamite pressure burst and short low body",
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
        "filters_hz": {"highpass": 60, "lowpass": 1900}, "seed": SEED,
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "file_bytes": path.stat().st_size,
        "runtime_one_shot_gain_recommended": 0.30,
        "passed": bool(np.abs(normalized.mean()) < 0.001 and pcm[0] == 0 and pcm[-1] == 0),
        "validation_limit": "File/waveform metrics only; device/in-game listening and subjective comfort are not claimed.",
    }
    (output / "TimedBombExplosionAudioValidation.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    export(parser.parse_args().output)
