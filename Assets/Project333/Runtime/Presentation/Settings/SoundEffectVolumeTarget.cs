using System.Collections.Generic;
using UnityEngine;

namespace Project333.Runtime.Presentation.Settings
{
    [DisallowMultipleComponent]
    public sealed class SoundEffectVolumeTarget : MonoBehaviour
    {
        private readonly List<SourceVolume> _sources = new List<SourceVolume>(2);

        internal void Register(AudioSource source)
        {
            for (var i = 0; i < _sources.Count; i++)
                if (_sources[i].Source == source) return;

            // Capture the original Inspector/source gain once, never the already-scaled gain.
            _sources.Add(new SourceVolume(source, source.volume));
            ApplyVolume(After333AudioSettings.EffectsVolume);
        }

        private void OnEnable()
        {
            After333AudioSettings.EffectsVolumeChanged += ApplyVolume;
            ApplyVolume(After333AudioSettings.EffectsVolume);
        }

        private void OnDisable()
        {
            After333AudioSettings.EffectsVolumeChanged -= ApplyVolume;
        }

        private void OnDestroy()
        {
            // Removing only this component must not leave a source permanently attenuated.
            foreach (var entry in _sources)
                if (entry.Source != null) entry.Source.volume = entry.FullVolume;
        }

        private void ApplyVolume(float volume)
        {
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                var entry = _sources[i];
                if (entry.Source == null)
                {
                    _sources.RemoveAt(i);
                    continue;
                }
                entry.Source.volume = entry.FullVolume * volume;
            }
        }

        private readonly struct SourceVolume
        {
            public readonly AudioSource Source;
            public readonly float FullVolume;

            public SourceVolume(AudioSource source, float fullVolume)
            {
                Source = source;
                FullVolume = fullVolume;
            }
        }
    }
}
