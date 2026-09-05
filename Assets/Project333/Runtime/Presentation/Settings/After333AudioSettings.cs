using System;
using UnityEngine;

namespace Project333.Runtime.Presentation.Settings
{
    public static class After333AudioSettings
    {
        private const string MasterVolumeKey = "After333.Audio.MasterVolume";
        private const string MusicVolumeKey = "After333.Audio.MusicVolume";
        private const float DefaultVolume = 1f;

        public static event Action<float> MusicVolumeChanged;

        public static float MasterVolume { get; private set; } = DefaultVolume;

        public static float MusicVolume { get; private set; } = DefaultVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Reload();
        }

        public static void Reload()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultVolume));
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultVolume));
            ApplyMasterVolume();
            MusicVolumeChanged?.Invoke(MusicVolume);
        }

        public static void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
            ApplyMasterVolume();
        }

        public static void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
            MusicVolumeChanged?.Invoke(MusicVolume);
        }

        public static void Save()
        {
            PlayerPrefs.Save();
        }

        private static void ApplyMasterVolume()
        {
            AudioListener.volume = MasterVolume;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class BackgroundMusicVolumeTarget : MonoBehaviour
    {
        [SerializeField] private AudioSource _audioSource;
        [SerializeField, Range(0f, 1f)] private float _volumeAtFullMusicSetting = 1f;

        private void Awake()
        {
            ResolveAudioSource();
        }

        private void OnEnable()
        {
            ResolveAudioSource();
            After333AudioSettings.MusicVolumeChanged += ApplyMusicVolume;
            ApplyMusicVolume(After333AudioSettings.MusicVolume);
        }

        private void OnDisable()
        {
            After333AudioSettings.MusicVolumeChanged -= ApplyMusicVolume;
        }

        private void OnValidate()
        {
            _volumeAtFullMusicSetting = Mathf.Clamp01(_volumeAtFullMusicSetting);
            ResolveAudioSource();
            ApplyMusicVolume(After333AudioSettings.MusicVolume);
        }

        private void ResolveAudioSource()
        {
            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
            }
        }

        private void ApplyMusicVolume(float musicVolume)
        {
            if (_audioSource != null)
            {
                _audioSource.volume = _volumeAtFullMusicSetting * Mathf.Clamp01(musicVolume);
            }
        }
    }
}
