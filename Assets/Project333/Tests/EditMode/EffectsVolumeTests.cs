using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class EffectsVolumeTests
    {
        private const string EffectsKey = "After333.Audio.EffectsVolume";

        [Test]
        public void OldPreferences_DefaultEffectsToFull_WithoutChangingExistingVolumes()
        {
            var snapshot = new SettingsSnapshot();
            try
            {
                After333AudioSettings.SetMasterVolume(0.7f);
                After333AudioSettings.SetMusicVolume(0.4f);
                PlayerPrefs.DeleteKey(EffectsKey);
                After333AudioSettings.Reload();
                Assert.That(After333AudioSettings.EffectsVolume, Is.EqualTo(1f));
                Assert.That(After333AudioSettings.MasterVolume, Is.EqualTo(0.7f));
                Assert.That(After333AudioSettings.MusicVolume, Is.EqualTo(0.4f));
                Assert.That(AudioListener.volume, Is.EqualTo(0.7f));
            }
            finally { snapshot.Restore(); }
        }

        [Test]
        public void EffectsVolume_ClampsAndSurvivesSaveReload_IndependentlyOfMusicAndMaster()
        {
            var snapshot = new SettingsSnapshot();
            try
            {
                After333AudioSettings.SetMasterVolume(0.6f);
                After333AudioSettings.SetMusicVolume(0.3f);
                After333AudioSettings.SetEffectsVolume(-1f);
                Assert.That(After333AudioSettings.EffectsVolume, Is.Zero);
                After333AudioSettings.SetEffectsVolume(2f);
                Assert.That(After333AudioSettings.EffectsVolume, Is.EqualTo(1f));
                After333AudioSettings.SetEffectsVolume(0.27f);
                After333AudioSettings.Save();
                After333AudioSettings.Reload();
                Assert.That(After333AudioSettings.EffectsVolume, Is.EqualTo(0.27f));
                Assert.That(PlayerPrefs.GetFloat(EffectsKey), Is.EqualTo(0.27f));
                Assert.That(After333AudioSettings.MasterVolume, Is.EqualTo(0.6f));
                Assert.That(After333AudioSettings.MusicVolume, Is.EqualTo(0.3f));
                Assert.That(AudioListener.volume, Is.EqualTo(0.6f));
            }
            finally { snapshot.Restore(); }
        }

        [TestCase("GameStart_VSlice")]
        [TestCase("Draft_VSlice")]
        [TestCase("DeckBuilding_VSlice")]
        [TestCase("Battle_VSlice")]
        [TestCase("OwnedCards_VSlice")]
        [TestCase("Shop_VSlice")]
        public void Settings_InEachSupportedScene_AddsOneThirdRowAndUpgradesExistingPanel(string sceneName)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = sceneName;
            try
            {
                var controller = GlobalSettingsMenuController.EnsureForScene(scene);
                Assert.That(controller, Is.Not.Null);
                var panel = (RectTransform)controller.transform.Find("SafeAreaRoot/ModalRoot/AudioPanel");
                var master = panel.Find("MasterVolumeSlider").GetComponent<Slider>();
                var music = panel.Find("MusicVolumeSlider").GetComponent<Slider>();
                var effects = panel.Find("EffectsVolumeSlider").GetComponent<Slider>();
                Assert.That(panel.Find("EffectsVolumeLabel").GetComponent<Text>().text, Is.EqualTo("효과음 볼륨"));
                Assert.That(effects.minValue, Is.Zero);
                Assert.That(effects.maxValue, Is.EqualTo(1f));
                Assert.That(effects.wholeNumbers, Is.False);
                panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 560f);
                controller.EnsureEditableHierarchy();
                Assert.That(panel.rect.height, Is.GreaterThanOrEqualTo(720f));
                Assert.That(panel.GetComponentsInChildren<Slider>(true).Length, Is.EqualTo(3));
                Assert.That(panel.Find("MasterVolumeSlider").GetComponent<Slider>(), Is.SameAs(master));
                Assert.That(panel.Find("MusicVolumeSlider").GetComponent<Slider>(), Is.SameAs(music));
                Assert.That(panel.Find("EffectsVolumeSlider").GetComponent<Slider>(), Is.SameAs(effects));
                Assert.That(panel.gameObject.activeSelf, Is.False);

                var rowBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panel, effects.transform);
                var backBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panel, panel.Find("BackButton"));
                Assert.That(rowBounds.min.y, Is.GreaterThan(backBounds.max.y), "The third slider must not overlap Back.");
                Assert.That(rowBounds.min.y, Is.GreaterThan(panel.rect.yMin));
                Assert.That(rowBounds.max.y, Is.LessThan(panel.rect.yMax));
            }
            finally
            {
                foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);
            }
        }

        [UnityTest]
        public IEnumerator Effects_ChangeLive_ReuseSources_KeepMusicIndependent_AndBindActualUiSounds()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var snapshot = new SettingsSnapshot();
            var root = new GameObject("EffectsVolumePlaybackTest");
            var objects = new List<GameObject>();
            var clip = AudioClip.Create("EffectsVolumeTestClip", 44100 * 4, 1, 44100, false);
            try
            {
                root.AddComponent<AudioListener>();
                After333AudioSettings.SetMasterVolume(0.7f);
                After333AudioSettings.SetMusicVolume(0.4f);
                After333AudioSettings.SetEffectsVolume(0.5f);

                var first = root.AddComponent<AudioSource>();
                first.playOnAwake = false;
                first.volume = 0.6f;
                var second = root.AddComponent<AudioSource>();
                second.playOnAwake = false;
                second.volume = 0.8f;
                After333SoundEffects.PlayOneShot(first, clip, 0.25f);
                After333SoundEffects.PlayOneShot(second, clip, 0.3f);
                yield return null;
                Assert.That(first.isPlaying, Is.True);
                Assert.That(first.volume, Is.EqualTo(0.3f).Within(0.0001f));
                Assert.That(second.volume, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(root.GetComponents<SoundEffectVolumeTarget>().Length, Is.EqualTo(1));
                for (var i = 0; i < 8; i++) After333SoundEffects.PlayOneShot(first, clip, 0.25f);
                Assert.That(first.volume, Is.EqualTo(0.3f).Within(0.0001f), "Repeated effects must not compound the gain.");

                var musicObject = new GameObject("IndependentMusicSource");
                objects.Add(musicObject);
                var music = musicObject.AddComponent<AudioSource>();
                var musicTarget = musicObject.AddComponent<BackgroundMusicVolumeTarget>();
                musicTarget.SetVolumeAtFullMusicSetting(0.48f);
                musicTarget.SetFadeGain(0.5f);
                After333SoundEffects.RegisterSceneSources(root.scene);
                Assert.That(musicObject.GetComponent<SoundEffectVolumeTarget>(), Is.Null);
                Assert.That(music.volume, Is.EqualTo(0.096f).Within(0.0001f));
                After333AudioSettings.SetEffectsVolume(0f);
                Assert.That(first.volume, Is.Zero, "Already-playing one-shots must mute immediately.");
                Assert.That(second.volume, Is.Zero);
                Assert.That(music.volume, Is.EqualTo(0.096f).Within(0.0001f));
                Assert.That(AudioListener.volume, Is.EqualTo(0.7f));
                After333AudioSettings.SetMusicVolume(0f);
                After333AudioSettings.SetEffectsVolume(0.25f);
                Assert.That(first.volume, Is.EqualTo(0.15f).Within(0.0001f));
                Assert.That(music.volume, Is.Zero);
                root.SetActive(false);
                After333AudioSettings.SetEffectsVolume(1f);
                root.SetActive(true);
                Assert.That(first.volume, Is.EqualTo(0.6f));
                Assert.That(second.volume, Is.EqualTo(0.8f));

                // Actual settings buttons and slider events, not direct handler invocation.
                var menuObject = new GameObject("EffectsVolumeMenuTest", typeof(RectTransform));
                objects.Add(menuObject);
                var controller = menuObject.AddComponent<GlobalSettingsMenuController>();
                var modal = menuObject.transform.Find("SafeAreaRoot/ModalRoot");
                menuObject.transform.Find("SafeAreaRoot/GearButton").GetComponent<Button>().onClick.Invoke();
                modal.Find("MainPanel/MainButtonStack/SettingsButton").GetComponent<Button>().onClick.Invoke();
                var panel = modal.Find("AudioPanel");
                Assert.That(panel.gameObject.activeInHierarchy, Is.True);
                var slider = panel.Find("EffectsVolumeSlider").GetComponent<Slider>();
                slider.value = 0.23f;
                Assert.That(After333AudioSettings.EffectsVolume, Is.EqualTo(0.23f));
                Assert.That(panel.Find("EffectsVolumeValueText").GetComponent<Text>().text, Is.EqualTo("23%"));
                Assert.That(first.volume, Is.EqualTo(0.138f).Within(0.0001f));
                panel.Find("BackButton").GetComponent<Button>().onClick.Invoke();
                modal.Find("MainPanel/MainButtonStack/SettingsButton").GetComponent<Button>().onClick.Invoke();
                Assert.That(slider.value, Is.EqualTo(0.23f));

                var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
                foreach (var soundName in new[] { "ButtonTouchSound", "CardTouchSound", "GoldSound" })
                {
                    var buttonObject = new GameObject(soundName + "TestButton", typeof(RectTransform), typeof(Button));
                    objects.Add(buttonObject);
                    IPointerDownHandler sound;
                    switch (soundName)
                    {
                        case "ButtonTouchSound": sound = buttonObject.AddComponent<ButtonTouchSound>(); break;
                        case "CardTouchSound": sound = buttonObject.AddComponent<CardTouchSound>(); break;
                        default: sound = buttonObject.AddComponent<GoldSound>(); break;
                    }
                    sound.OnPointerDown(pointer);
                    var sourceObject = GameObject.Find(soundName + "Player");
                    Assert.That(sourceObject, Is.Not.Null, soundName);
                    objects.Add(sourceObject);
                    var source = sourceObject.GetComponent<AudioSource>();
                    Assert.That(source.volume, Is.EqualTo(0.23f).Within(0.0001f));
                    Assert.That(source.isPlaying, Is.True);
                    After333AudioSettings.SetEffectsVolume(0f);
                    Assert.That(source.volume, Is.Zero);
                    After333AudioSettings.SetEffectsVolume(0.23f);
                    sound.OnPointerDown(pointer);
                    Assert.That(sourceObject.GetComponents<AudioSource>().Length, Is.EqualTo(1));
                }
            }
            finally
            {
                foreach (var go in objects) if (go != null) Object.DestroyImmediate(go);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(clip);
                snapshot.Restore();
            }
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator LeavePlayModeOnFailure()
        {
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        private sealed class SettingsSnapshot
        {
            private readonly string[] _keys = { "After333.Audio.MasterVolume", "After333.Audio.MusicVolume", EffectsKey };
            private readonly bool[] _present = new bool[3];
            private readonly float[] _values = new float[3];
            private readonly float _master = After333AudioSettings.MasterVolume;
            private readonly float _music = After333AudioSettings.MusicVolume;
            private readonly float _effects = After333AudioSettings.EffectsVolume;
            private readonly float _listener = AudioListener.volume;

            public SettingsSnapshot()
            {
                for (var i = 0; i < _keys.Length; i++)
                {
                    _present[i] = PlayerPrefs.HasKey(_keys[i]);
                    _values[i] = PlayerPrefs.GetFloat(_keys[i]);
                }
            }

            public void Restore()
            {
                After333AudioSettings.SetMasterVolume(_master);
                After333AudioSettings.SetMusicVolume(_music);
                After333AudioSettings.SetEffectsVolume(_effects);
                for (var i = 0; i < _keys.Length; i++)
                {
                    if (_present[i]) PlayerPrefs.SetFloat(_keys[i], _values[i]);
                    else PlayerPrefs.DeleteKey(_keys[i]);
                }
                AudioListener.volume = _listener;
                PlayerPrefs.Save();
            }
        }
    }
}
