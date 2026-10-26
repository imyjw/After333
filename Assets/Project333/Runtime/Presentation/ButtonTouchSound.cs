using Project333.Runtime.Presentation.Settings;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Project333.Runtime.Presentation
{
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class ButtonTouchSound : MonoBehaviour, IPointerDownHandler
    {
        private static AudioSource _source;
        private static AudioClip _clip;
        private UnityEngine.UI.Button _button;

        private void Awake()
        {
            _button = GetComponent<UnityEngine.UI.Button>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left ||
                !_button.IsActive() ||
                !_button.IsInteractable())
            {
                return;
            }

            if (_clip == null)
            {
                _clip = Resources.Load<AudioClip>(
                    "Project333/Audio/UI/ButtonClick");
            }

            if (_clip == null) return;

            if (_source == null)
            {
                var soundObject = new GameObject("ButtonTouchSoundPlayer");
                DontDestroyOnLoad(soundObject);

                _source = soundObject.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
            }

            After333SoundEffects.PlayOneShot(_source, _clip, 0.5f);
        }
    }
}