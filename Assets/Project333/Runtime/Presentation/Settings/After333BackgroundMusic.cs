using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project333.Runtime.Presentation.Settings
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class After333BackgroundMusic : MonoBehaviour
    {
        private const string LobbyResource = "Project333/Audio/BGM/After333_Lobby";
        private const string BattleResource = "Project333/Audio/BGM/After333_Battle";
        private const float FullMusicVolume = 0.48f;
        private const float FadeOutSeconds = 0.35f;
        private const float FadeInSeconds = 0.65f;

        private static After333BackgroundMusic s_instance;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private AudioSource _source;
        private AudioListener _fallbackListener;
        private BackgroundMusicVolumeTarget _volumeTarget;
        private string _requestedResource;
        private Coroutine _transition;
        private float _fadeGain;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            s_instance = null;
        }

        public static void EnsureForScene(Scene scene)
        {
            if (!UnityEngine.Application.isPlaying || !scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            if (s_instance != null)
            {
                s_instance.RefreshAudioListener();
            }

            var resource = ResourceForScene(scene.name);
            if (resource == null)
            {
                return;
            }

            if (s_instance == null)
            {
                // Include disabled objects so a temporarily disabled player is never duplicated.
                s_instance = FindFirstObjectByType<After333BackgroundMusic>(FindObjectsInactive.Include);
                if (s_instance == null)
                {
                    var host = new GameObject("After333BackgroundMusic");
                    s_instance = host.AddComponent<After333BackgroundMusic>();
                }
            }

            s_instance.RefreshAudioListener();
            s_instance.RequestTrack(resource);
        }

        private static string ResourceForScene(string sceneName)
        {
            switch (sceneName)
            {
                case "GameStart_VSlice":
                case "Draft_VSlice":
                case "DeckBuilding_VSlice":
                case "OwnedCards_VSlice":
                case "Shop_VSlice":
                    return LobbyResource;
                case "Battle_VSlice":
                    return BattleResource;
                default:
                    return null;
            }
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                enabled = false;
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
            _fallbackListener = gameObject.AddComponent<AudioListener>();
            RefreshAudioListener();
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.dopplerLevel = 0f;
            _source.volume = 0f;
            _volumeTarget = GetComponent<BackgroundMusicVolumeTarget>();
            if (_volumeTarget == null)
            {
                _volumeTarget = gameObject.AddComponent<BackgroundMusicVolumeTarget>();
            }

            _volumeTarget.SetVolumeAtFullMusicSetting(FullMusicVolume);
            SetFadeGain(0f);
        }

        private void OnEnable()
        {
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            RefreshAudioListener();
            if (_source != null && _requestedResource != null)
            {
                BeginTransition();
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            if (_fallbackListener != null)
            {
                _fallbackListener.enabled = false;
            }

            CancelTransition();
            if (_source != null)
            {
                _source.Pause();
                SetFadeGain(0f);
            }
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            RefreshAudioListener();
        }

        private void RefreshAudioListener()
        {
            if (_fallbackListener == null)
            {
                return;
            }

            var otherListenerExists = false;
            var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != _fallbackListener && listeners[i].isActiveAndEnabled)
                {
                    otherListenerExists = true;
                    break;
                }
            }

            _fallbackListener.enabled = isActiveAndEnabled && !otherListenerExists;
        }

        private void RequestTrack(string resource)
        {
            if (_requestedResource == resource)
            {
                return;
            }

            _requestedResource = resource;
            if (isActiveAndEnabled && _source != null)
            {
                BeginTransition();
            }
        }

        private void BeginTransition()
        {
            if (!_clips.TryGetValue(_requestedResource, out var clip))
            {
                clip = Resources.Load<AudioClip>(_requestedResource);
                _clips.Add(_requestedResource, clip);
                if (clip == null)
                {
                    Debug.LogWarning("After333 background music is missing: " + _requestedResource, this);
                }
            }

            if (clip == null)
            {
                return;
            }

            CancelTransition();
            _transition = StartCoroutine(TransitionTo(clip));
        }

        private void CancelTransition()
        {
            if (_transition != null)
            {
                StopCoroutine(_transition);
                _transition = null;
            }
        }

        private IEnumerator TransitionTo(AudioClip clip)
        {
            if (_source.clip != clip)
            {
                if (_source.clip != null)
                {
                    yield return FadeTo(0f, FadeOutSeconds);
                }

                _source.Stop();
                _source.clip = clip;
                SetFadeGain(0f);
                _source.Play();
            }
            else
            {
                _source.UnPause();
            }

            yield return FadeTo(1f, FadeInSeconds);
            _transition = null;
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            var start = _fadeGain;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetFadeGain(Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            SetFadeGain(target);
        }

        private void SetFadeGain(float gain)
        {
            _fadeGain = Mathf.Clamp01(gain);
            if (_volumeTarget != null)
            {
                _volumeTarget.SetFadeGain(_fadeGain);
            }
        }
    }
}
