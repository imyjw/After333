using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project333.Runtime.Presentation.Settings
{
    public static class After333SoundEffects
    {
        public static void PlayOneShot(AudioSource source, AudioClip clip, float volumeScale = 1f)
        {
            if (!UnityEngine.Application.isPlaying || source == null || clip == null) return;
            RegisterSource(source);
            source.PlayOneShot(clip, volumeScale);
        }

        public static void RegisterSource(AudioSource source)
        {
            if (!UnityEngine.Application.isPlaying || source == null ||
                source.GetComponent<BackgroundMusicVolumeTarget>() != null) return;

            var target = source.GetComponent<SoundEffectVolumeTarget>();
            if (target == null) target = source.gameObject.AddComponent<SoundEffectVolumeTarget>();
            target.Register(source);
        }

        public static void RegisterSceneSources(Scene scene)
        {
            if (!UnityEngine.Application.isPlaying || !scene.IsValid() || !scene.isLoaded) return;
            // Existing sources are bound once at scene load; runtime effects register before playback.
            foreach (var root in scene.GetRootGameObjects())
                foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
                    RegisterSource(source);
        }
    }
}
