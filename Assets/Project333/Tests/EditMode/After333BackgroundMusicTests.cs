using System.Collections;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Presentation.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class After333BackgroundMusicTests
    {
        private const string ResourceRoot = "Project333/Audio/BGM/";

        [TestCase("After333_Lobby")]
        [TestCase("After333_Battle")]
        public void Music_IsPackagedAsStereoStreamingAudio(string trackName)
        {
            var clip = Resources.Load<AudioClip>(ResourceRoot + trackName);
            Assert.That(clip, Is.Not.Null, trackName);
            Assert.That(clip.channels, Is.EqualTo(2));
            Assert.That(clip.frequency, Is.GreaterThanOrEqualTo(44100));
            Assert.That(clip.length, Is.GreaterThan(90f));
            Assert.That(clip.samples, Is.GreaterThan(0));
            Assert.That(clip.loadType, Is.EqualTo(AudioClipLoadType.Streaming));
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as AudioImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.forceToMono, Is.False);
            Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming));
        }

        [UnityTest]
        public IEnumerator Music_PersistsThroughScenesAndHonorsVolumePauseAndListenerOwnership()
        {
            // Named empty scenes exercise music without account clients, battle gateways or a server.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return RunPlayback();
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator LeavePlayModeOnFailure()
        {
            if (UnityEngine.Application.isPlaying)
            {
                yield return new ExitPlayMode();
            }
        }

        private static IEnumerator RunPlayback()
        {
            var settings = new AudioSettingsSnapshot();
            var previousTimeScale = Time.timeScale;
            try
            {
                After333BackgroundMusic.EnsureForScene(SceneManager.GetActiveScene());
                Assert.That(MusicPlayers(), Is.Empty, "Unrelated Editor/test scenes must not start music.");
                After333AudioSettings.SetMasterVolume(0.7f);
                After333AudioSettings.SetMusicVolume(1f);

                var lobby = SceneManager.CreateScene("GameStart_VSlice");
                SceneManager.SetActiveScene(lobby);
                After333BackgroundMusic.EnsureForScene(lobby);
                var player = MusicPlayers().Single();
                var source = player.GetComponent<AudioSource>();
                var fallbackListener = player.GetComponent<AudioListener>();
                Assert.That(fallbackListener, Is.Not.Null);
                Assert.That(fallbackListener.enabled, Is.True, "Menu scenes without a camera listener still need audible music.");
                yield return new WaitForSecondsRealtime(0.8f);
                Assert.That(source.clip, Is.SameAs(Resources.Load<AudioClip>(ResourceRoot + "After333_Lobby")));
                Assert.That(source.loop, Is.True);
                Assert.That(source.spatialBlend, Is.Zero);
                Assert.That(source.volume, Is.EqualTo(0.48f).Within(0.001f));
                Assert.That(AudioListener.volume, Is.EqualTo(0.7f).Within(0.001f));
                var lobbyClip = source.clip;

                var menuNames = new[] { "Draft_VSlice", "DeckBuilding_VSlice", "OwnedCards_VSlice", "Shop_VSlice" };
                var lastMenu = lobby;
                foreach (var sceneName in menuNames)
                {
                    var menu = SceneManager.CreateScene(sceneName);
                    SceneManager.SetActiveScene(menu);
                    var cursor = source.timeSamples;
                    After333BackgroundMusic.EnsureForScene(menu);
                    Assert.That(MusicPlayers().Single(), Is.SameAs(player));
                    Assert.That(source.clip, Is.SameAs(lobbyClip));
                    Assert.That(source.timeSamples, Is.GreaterThanOrEqualTo(cursor), "Menu navigation must not restart the loop.");
                    yield return SceneManager.UnloadSceneAsync(lastMenu);
                    Assert.That(player, Is.Not.Null, "Music survives unloading the scene that created it.");
                    lastMenu = menu;
                }

                var duplicate = new GameObject("DuplicateMusicPlayer").AddComponent<After333BackgroundMusic>();
                yield return null;
                Assert.That(duplicate == null, Is.True);
                Assert.That(MusicPlayers().Single(), Is.SameAs(player));

                var battle = SceneManager.CreateScene("Battle_VSlice");
                SceneManager.SetActiveScene(battle);
                var battleListener = new GameObject("BattleCameraListener").AddComponent<AudioListener>();
                SceneManager.MoveGameObjectToScene(battleListener.gameObject, battle);
                Time.timeScale = 0f;
                After333BackgroundMusic.EnsureForScene(battle);
                Assert.That(fallbackListener.enabled, Is.False, "Use the existing battle listener, never two listeners.");
                Assert.That(battleListener.enabled, Is.True);
                yield return new WaitForSecondsRealtime(0.15f);
                After333AudioSettings.SetMusicVolume(0.25f);
                Assert.That(source.volume, Is.LessThanOrEqualTo(0.12f + 0.001f), "Volume changes apply during the fade.");
                yield return new WaitForSecondsRealtime(1.1f);
                Assert.That(source.clip, Is.SameAs(Resources.Load<AudioClip>(ResourceRoot + "After333_Battle")));
                Assert.That(source.volume, Is.EqualTo(0.12f).Within(0.001f), "Transitions complete even while game time is paused.");
                Assert.That(MusicPlayers().Single(), Is.SameAs(player));

                After333AudioSettings.SetMusicVolume(0f);
                Assert.That(source.volume, Is.Zero);
                After333AudioSettings.SetMasterVolume(0f);
                Assert.That(AudioListener.volume, Is.Zero);
                After333AudioSettings.SetMusicVolume(0.4f);
                After333AudioSettings.SetMasterVolume(0.7f);
                After333AudioSettings.Reload();
                Assert.That(source.volume, Is.EqualTo(0.192f).Within(0.001f));
                Assert.That(AudioListener.volume, Is.EqualTo(0.7f).Within(0.001f));

                player.gameObject.SetActive(false);
                After333BackgroundMusic.EnsureForScene(lastMenu);
                Assert.That(MusicPlayers().Single(), Is.SameAs(player), "Inactive players must not be duplicated.");
                Assert.That(source.isPlaying, Is.False);
                Assert.That(fallbackListener.enabled, Is.False);
                SceneManager.SetActiveScene(lastMenu);
                yield return SceneManager.UnloadSceneAsync(battle);
                player.gameObject.SetActive(true);
                yield return new WaitForSecondsRealtime(1.15f);
                Assert.That(source.clip, Is.SameAs(lobbyClip));
                Assert.That(source.volume, Is.EqualTo(0.192f).Within(0.001f));
                Assert.That(fallbackListener.enabled, Is.True, "Returning to a menu restores the fallback listener.");

                player.enabled = false;
                After333BackgroundMusic.EnsureForScene(lastMenu);
                Assert.That(MusicPlayers().Single(), Is.SameAs(player), "Disabled players must not be duplicated.");
                Assert.That(source.isPlaying, Is.False);
                Assert.That(source.volume, Is.Zero);
                player.enabled = true;
                yield return new WaitForSecondsRealtime(0.8f);
                Assert.That(source.volume, Is.EqualTo(0.192f).Within(0.001f));
                Assert.That(fallbackListener.enabled, Is.True);
            }
            finally
            {
                foreach (var player in MusicPlayers())
                {
                    Object.DestroyImmediate(player.gameObject);
                }

                Time.timeScale = previousTimeScale;
                settings.Restore();
            }
        }

        private static After333BackgroundMusic[] MusicPlayers()
        {
            return Object.FindObjectsByType<After333BackgroundMusic>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private sealed class AudioSettingsSnapshot
        {
            private const string MasterKey = "After333.Audio.MasterVolume";
            private const string MusicKey = "After333.Audio.MusicVolume";
            private readonly bool _hadMaster = PlayerPrefs.HasKey(MasterKey);
            private readonly bool _hadMusic = PlayerPrefs.HasKey(MusicKey);
            private readonly float _savedMaster = PlayerPrefs.GetFloat(MasterKey);
            private readonly float _savedMusic = PlayerPrefs.GetFloat(MusicKey);
            private readonly float _runtimeMaster = After333AudioSettings.MasterVolume;
            private readonly float _runtimeMusic = After333AudioSettings.MusicVolume;
            private readonly float _listenerVolume = AudioListener.volume;

            public void Restore()
            {
                After333AudioSettings.SetMasterVolume(_runtimeMaster);
                After333AudioSettings.SetMusicVolume(_runtimeMusic);
                RestorePreference(MasterKey, _hadMaster, _savedMaster);
                RestorePreference(MusicKey, _hadMusic, _savedMusic);
                AudioListener.volume = _listenerVolume;
                PlayerPrefs.Save();
            }

            private static void RestorePreference(string key, bool existed, float value)
            {
                if (existed) PlayerPrefs.SetFloat(key, value);
                else PlayerPrefs.DeleteKey(key);
            }
        }
    }
}
