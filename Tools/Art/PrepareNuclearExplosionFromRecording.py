"""Prepare the user-provided Nuclear explosion recording for the existing spell.

Exports outside Assets. Review then copy only NuclearExplosion.wav to the existing
Resources path and preserve its metadata. Requires numpy and python-soundfile.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import wave
import numpy as np
import soundfile as sf

# Installed under Tools/Art; --project-root also supports reviewed temporary use.
DEFAULT_ROOT = Path(__file__).resolve().parents[2]
RATE = 44100
START = 0.0
DURATION = 1.680
ATTACK = .020
RELEASE = .180
MAX_PEAK_DBFS = -12.0
TARGET_RMS_DBFS = -29.298

def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def db(value):
    return float(20 * math.log10(max(float(value), 1e-15)))

def prepare(root, source, output):
    root, source, output = Path(root).resolve(), Path(source).resolve(), Path(output).resolve()
    assets = root / 'Assets'
    if output == assets or assets in output.parents:
        raise ValueError('Export outside Assets; do not overwrite current audio automatically.')
    decoded, rate = sf.read(str(source), dtype='float64', always_2d=True)
    if rate != RATE or decoded.shape[1] != 2 or not np.isfinite(decoded).all():
        raise ValueError('Expected the reviewed finite 44,100 Hz stereo recording.')
    start, count = round(START * rate), round(DURATION * rate)
    if start + count > len(decoded):
        raise ValueError('Selected recording window is incomplete.')
    clip = decoded[start:start+count].mean(axis=1).copy()
    clip -= clip.mean()
    fade = np.ones(count)
    attack, release = round(ATTACK*rate), round(RELEASE*rate)
    fade[:attack] = .5 - .5*np.cos(np.linspace(0, math.pi, attack))
    fade[-release:] = .5 + .5*np.cos(np.linspace(0, math.pi, release))
    clip *= fade
    clip -= (clip.sum()/fade.sum()) * fade
    peak, rms = float(np.abs(clip).max()), float(np.sqrt(np.mean(clip**2)))
    if rms < 1e-8:
        raise ValueError('Unexpectedly silent recording window.')
    gain = min(10**(MAX_PEAK_DBFS/20)/peak, 10**(TARGET_RMS_DBFS/20)/rms)
    pcm = np.rint(clip*gain*32768).astype('<i2')
    pcm[0] = pcm[-1] = 0
    normalized = pcm.astype(np.float64)/32768
    if ((pcm == -32768) | (pcm == 32767)).any():
        raise ValueError('Clipped PCM sample.')
    output.mkdir(parents=True, exist_ok=True)
    wav = output / 'NuclearExplosion.wav'
    with wave.open(str(wav), 'wb') as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(rate)
        writer.writeframes(pcm.tobytes())
    report = {
        'source':str(source), 'source_sha256':digest(source),
        'source_sample_rate':rate, 'source_channels':decoded.shape[1],
        'source_duration_seconds':len(decoded)/rate,
        'selected_start_seconds':start/rate, 'selected_end_seconds':(start+count)/rate,
        'duration_seconds':count/rate, 'samples':count, 'channels':1,
        'sample_rate':rate, 'format':'WAV PCM16',
        'fade_in_seconds':attack/rate, 'fade_out_seconds':release/rate,
        'normalization_gain':gain, 'normalization_db':db(gain),
        'max_peak_dbfs':MAX_PEAK_DBFS, 'target_rms_dbfs':TARGET_RMS_DBFS,
        'peak_dbfs':db(np.abs(normalized).max()),
        'rms_dbfs':db(np.sqrt(np.mean(normalized**2))),
        'dc_offset':float(normalized.mean()),
        'clipped_samples':0, 'first_sample':int(pcm[0]), 'last_sample':int(pcm[-1]),
        'sha256':digest(wav), 'file_bytes':wav.stat().st_size,
        'decoder':'python-soundfile '+sf.__version__,
        'libsndfile':sf.__libsndfile_version__, 'numpy':np.__version__,
        'processing':'First 1.680 seconds, stereo average to mono, remove DC, 20ms/180ms raised-cosine fades; target previous -29.298dBFS RMS with -12dBFS peak ceiling. No additional layers, EQ, pitch/speed change or resampling.',
        'passed':True, 'listening':'File/waveform checks only; no device/in-game listening claimed.'
    }
    (output/'NuclearExplosionRecordingValidation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,ensure_ascii=False,indent=2))

if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root',type=Path,default=DEFAULT_ROOT)
    parser.add_argument('--source',type=Path)
    parser.add_argument('--output',type=Path)
    args=parser.parse_args()
    source=args.source or args.project_root/'Tools/Art/Source/Nuclear/Audio/dragon-studio-nuclear-explosion-386181.mp3'
    output=args.output or args.project_root/'TempBuild/NuclearExplosionRecordingExport'
    prepare(args.project_root,source,output)
