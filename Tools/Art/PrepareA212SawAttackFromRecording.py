"""Prepare the user-provided circular-saw recording, without synthesizing extra layers.

Default export is outside Unity Assets. Review then copy only SawAttack.wav into its
existing Resources path; preserve .meta. Requires installed numpy and soundfile.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_SOURCE = ROOT / 'Tools/Art/Source/A-212/Audio/bfcmusic-circular-saw-2-fx-325571.mp3'
DEFAULT_OUTPUT = ROOT / 'TempBuild/A212SawRecordingExport'

def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def soften_saw(clip, rate):
    # Broad RBJ high shelf: preserve lower motor buzz while reducing sharp treble.
    shelf_db, corner_hz, trim_db = -4.0, 3500.0, -6.0
    a = 10 ** (shelf_db / 40)
    omega = 2 * math.pi * corner_hz / rate
    c, s = math.cos(omega), math.sin(omega)
    alpha = s / 2 * math.sqrt(2)  # shelf slope S=1
    term = 2 * math.sqrt(a) * alpha
    b = np.array([a*((a+1)+(a-1)*c+term),
                  -2*a*((a-1)+(a+1)*c),
                  a*((a+1)+(a-1)*c-term)])
    d = np.array([(a+1)-(a-1)*c+term,
                  2*((a-1)-(a+1)*c),
                  (a+1)-(a-1)*c-term])
    b, d = b/d[0], d/d[0]
    # Direct-form II transposed; zero initial state and no timing/sample-count change.
    result = np.empty_like(clip)
    z1 = z2 = 0.0
    for i, x in enumerate(clip):
        y = b[0]*x + z1
        z1, z2 = b[1]*x - d[1]*y + z2, b[2]*x - d[2]*y
        result[i] = y
    result *= 10 ** (trim_db / 20)
    if not np.isfinite(result).all():
        raise ValueError('Non-finite audio after EQ.')
    return result

def prepare(source, output):
    source, output = Path(source).resolve(), Path(output).resolve()
    assets = (ROOT / 'Assets').resolve()
    if output == assets or assets in output.parents:
        raise ValueError('Export outside Assets; do not overwrite later artist edits automatically.')
    decoded, rate = sf.read(str(source), dtype='float64', always_2d=True)
    if rate != 44100 or decoded.shape[1] != 2 or not np.isfinite(decoded).all():
        raise ValueError('Expected the reviewed 44,100 Hz stereo recording.')
    start_s, duration_s = 3.020, .480
    start, length = round(start_s * rate), round(duration_s * rate)
    if start + length > len(decoded):
        raise ValueError('Selected recording window is incomplete.')
    clip = decoded[start:start+length].mean(axis=1).copy()
    dc_removed = float(clip.mean())
    clip -= dc_removed
    attack, release = round(.020 * rate), round(.045 * rate)
    # Raised-cosine fades have zero slope at both boundaries; no pitch/speed change.
    clip[:attack] *= .5 - .5 * np.cos(np.linspace(0, math.pi, attack))
    clip[-release:] *= .5 + .5 * np.cos(np.linspace(0, math.pi, release))
    peak = float(np.abs(clip).max())
    if peak < .01:
        raise ValueError('Unexpectedly quiet source window.')
    gain = 10 ** (-4.0 / 20) / peak
    clip *= gain
    baseline_rms = float(np.sqrt(np.mean(clip**2)))
    clip = soften_saw(clip, rate)
    clip[0] = clip[-1] = 0
    output.mkdir(parents=True, exist_ok=True)
    wave_path = output / 'SawAttack.wav'
    sf.write(str(wave_path), clip, rate, format='WAV', subtype='PCM_16')
    result, check_rate = sf.read(str(wave_path), dtype='int16')
    if check_rate != rate or result.ndim != 1 or len(result) != length:
        raise ValueError('PCM export format mismatch.')
    clipped = int(((result == -32768) | (result == 32767)).sum())
    if clipped or result[0] != 0 or result[-1] != 0:
        raise ValueError('Clipped sample or nonzero audio boundary.')
    normalized = result.astype(np.float64) / 32768
    report = {
        'source': str(source), 'source_sha256': digest(source),
        'source_sample_rate': rate, 'source_channels': decoded.shape[1],
        'source_decoded_frames': len(decoded), 'source_duration_seconds': len(decoded)/rate,
        'selected_start_seconds': start/rate, 'selected_end_seconds': (start+length)/rate,
        'duration_seconds': len(result)/rate, 'samples': len(result),
        'channels': 1, 'sample_rate': rate, 'format': 'WAV PCM16',
        'fade_in_seconds': attack/rate, 'fade_out_seconds': release/rate,
        'removed_dc': dc_removed, 'pre_eq_normalization_gain': gain,
        'high_shelf_db': -4.0, 'high_shelf_corner_hz': 3500.0, 'overall_trim_db': -6.0,
        'baseline_rms_dbfs': float(20*np.log10(baseline_rms)),
        'rms_change_db': float(20*np.log10(np.sqrt(np.mean(normalized**2))/baseline_rms)),
        'peak_dbfs': float(20*np.log10(np.abs(normalized).max())),
        'rms_dbfs': float(20*np.log10(np.sqrt(np.mean(normalized**2)))),
        'clipped_samples': clipped, 'first_sample': int(result[0]), 'last_sample': int(result[-1]),
        'sha256': digest(wave_path), 'file_bytes': wave_path.stat().st_size,
        'decoder': 'python-soundfile '+sf.__version__, 'libsndfile': sf.__libsndfile_version__,
        'numpy': np.__version__, 'passed': True,
        'processing': 'Stereo average to mono, remove DC, 20ms/45ms cosine fades, baseline peak -4dBFS, broad -4dB high shelf at 3500Hz then -6dB overall trim; no post-EQ normalization, added layers, pitch shift, time stretch or resampling.',
        'listening': 'File/waveform validation only; device/in-game listening not claimed.'
    }
    (output/'A212SawRecordingValidation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,ensure_ascii=False,indent=2))

if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source',type=Path,default=DEFAULT_SOURCE)
    parser.add_argument('--output',type=Path,default=DEFAULT_OUTPUT)
    args=parser.parse_args()
    prepare(args.source,args.output)
