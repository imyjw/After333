"""Create A-301's original soft laser cue with deterministic DSP.

Export defaults outside Unity Assets. Review the WAV, then copy LaserShot.wav
to its Resources path. The exporter refuses destinations anywhere under Assets
and never creates or changes Unity metadata. Requires numpy and scipy.
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
DEFAULT_OUTPUT = ROOT / "TempBuild/A301LaserAudioExport"
RATE = 44100
DURATION = 0.260
ATTACK = 0.016
RELEASE = 0.050
DECAY = 0.092
PEAK_DBFS = -14.0
SEED = 3010930


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def db(value):
    return float(20 * math.log10(max(float(value), 1e-15)))


def synthesize():
    count = round(DURATION * RATE)
    time = np.arange(count, dtype=np.float64) / RATE
    # Continuous frequency sweep: no oscillator reset, hard transient or high hiss.
    frequency = 180.0 + 720.0 * np.exp(-time / 0.045)
    phase = 2.0 * math.pi * np.cumsum(frequency) / RATE
    rounded = np.sin(phase) + 0.12 * np.sin(2.0 * phase + 0.25)
    body = 0.16 * np.sin(phase * 0.5 + 0.4)
    rng = np.random.default_rng(SEED)
    airflow = signal.sosfilt(signal.butter(2, [180, 1400], fs=RATE,
                                          btype="bandpass", output="sos"),
                            rng.normal(0, 1, count))
    airflow /= max(float(np.sqrt(np.mean(airflow ** 2))), 1e-12)
    airflow *= 0.008 * np.exp(-time / 0.045)
    raw = rounded + body + airflow
    # Low pass keeps the upper register gentle; the air layer remains very faint.
    raw = signal.sosfilt(signal.butter(3, 2200, fs=RATE, output="sos"), raw)
    raw -= raw.mean()
    envelope = np.exp(-time / DECAY)
    attack_count = round(ATTACK * RATE)
    release_count = round(RELEASE * RATE)
    envelope[:attack_count] *= 0.5 - 0.5 * np.cos(np.linspace(0, math.pi, attack_count))
    envelope[-release_count:] *= 0.5 + 0.5 * np.cos(np.linspace(0, math.pi, release_count))
    clip = raw * envelope
    clip *= 10 ** (PEAK_DBFS / 20) / float(np.abs(clip).max())
    clip[0] = clip[-1] = 0
    if not np.isfinite(clip).all():
        raise ValueError("Non-finite synthesized audio")
    pcm = np.rint(clip * 32768).astype("<i2")
    pcm[0] = pcm[-1] = 0
    if ((pcm == -32768) | (pcm == 32767)).any():
        raise ValueError("Clipped PCM")
    return pcm


def export(output):
    output = Path(output).resolve()
    assets = (ROOT / "Assets").resolve()
    if output == assets or assets in output.parents:
        raise ValueError("Export outside Assets; do not overwrite artist edits automatically.")
    pcm = synthesize()
    output.mkdir(parents=True, exist_ok=True)
    path = output / "LaserShot.wav"
    with wave.open(str(path), "wb") as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(RATE)
        writer.writeframes(pcm.tobytes())
    normalized = pcm.astype(np.float64) / 32768
    spectrum = np.abs(np.fft.rfft(normalized)) ** 2
    bins = np.fft.rfftfreq(len(pcm), 1 / RATE)
    report = {
        "cue": "A-301 soft descending laser shot",
        "source": "Original deterministic DSP; no recording, third-party sample or external model used.",
        "samples": len(pcm), "duration_seconds": len(pcm) / RATE,
        "channels": 1, "sample_rate": RATE, "format": "WAV PCM16",
        "attack_seconds": round(ATTACK * RATE) / RATE,
        "release_seconds": round(RELEASE * RATE) / RATE,
        "decay_seconds": DECAY,
        "frequency_hz": "180 + 720 * exp(-t / 0.045), continuous phase",
        "harmonic_2_gain": 0.12, "subtone_gain": 0.16,
        "air_layer_gain": 0.008, "air_band_hz": [180, 1400],
        "low_pass_hz": 2200, "seed": SEED,
        "peak_dbfs": db(np.abs(normalized).max()),
        "rms_dbfs": db(np.sqrt(np.mean(normalized ** 2))),
        "dc_average": float(normalized.mean()),
        "clipped_samples": int(((pcm == -32768) | (pcm == 32767)).sum()),
        "first_sample": int(pcm[0]), "last_sample": int(pcm[-1]),
        "max_sample_step": float(np.abs(np.diff(normalized)).max()),
        "spectral_energy_above_4khz_ratio": float(spectrum[bins > 4000].sum() / spectrum.sum()),
        "sha256": digest(path), "file_bytes": path.stat().st_size,
        "runtime_one_shot_gain_recommended": 0.30,
        "passed": True,
        "validation_limit": "File/waveform measurements only; perceived comfort and device/game listening are not claimed.",
    }
    (output / "A301LaserAudioValidation.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    export(parser.parse_args().output)
