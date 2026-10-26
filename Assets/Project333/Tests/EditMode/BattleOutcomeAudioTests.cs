using System.Collections;
using NUnit.Framework;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class BattleOutcomeAudioTests
    {
        [TestCase("Victory")]
        [TestCase("Defeat")]
        public void UserAudio_ImportsAndFitsExistingOutcomeHold(string outcome)
        {
            var clip = Resources.Load<AudioClip>("Project333/Audio/BattleOutcomes/" + outcome);
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.length, Is.GreaterThan(0f));
            Assert.That(clip.length, Is.LessThan(BattleOutcomeBannerView.MinimumDisplaySeconds));
            Assert.That(clip.samples, Is.GreaterThan(0));
            var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
            Assert.That(importer.forceToMono, Is.False, "Keep the supplied audio channels.");
            Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
            Debug.Log(outcome + " outcome audio seconds: " + clip.length);
        }

        [UnityTest]
        public IEnumerator BannerStart_PlaysCorrectSoundOnce_AndHonorsEffectsSettingsAndCleanup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            const string masterKey = "After333.Audio.MasterVolume";
            const string effectsKey = "After333.Audio.EffectsVolume";
            var hadMaster = PlayerPrefs.HasKey(masterKey);
            var hadEffects = PlayerPrefs.HasKey(effectsKey);
            var savedMaster = PlayerPrefs.GetFloat(masterKey);
            var savedEffects = PlayerPrefs.GetFloat(effectsKey);
            var oldMaster = After333AudioSettings.MasterVolume;
            var oldEffects = After333AudioSettings.EffectsVolume;
            var oldListenerVolume = AudioListener.volume;
            var oldTimeScale = Time.timeScale;
            var root = new GameObject("OutcomeAudioPlaybackTest", typeof(RectTransform));
            var listener = new GameObject("OutcomeTestListener", typeof(AudioListener));
            try
            {
                After333AudioSettings.SetMasterVolume(0.5f);
                After333AudioSettings.SetEffectsVolume(0.25f);
                var banner = root.AddComponent<BattleOutcomeBannerView>();
                var cueSettings = new SerializedObject(banner);
                cueSettings.FindProperty("_victorySoundVolume").floatValue = 0.4f;
                cueSettings.FindProperty("_defeatSoundVolume").floatValue = 0.8f;
                cueSettings.ApplyModifiedPropertiesWithoutUndo();
                banner.Show("Draw");
                Assert.That(root.GetComponent<AudioSource>(), Is.Null, "Draw has no victory/defeat cue.");
                banner.Show("Victory");
                var source = root.GetComponent<AudioSource>();
                Assert.That(source, Is.Not.Null);
                Assert.That(source.isPlaying, Is.True);
                Assert.That(source.loop, Is.False);
                Assert.That(source.playOnAwake, Is.False);
                Assert.That(source.spatialBlend, Is.Zero);
                Assert.That(source.volume, Is.EqualTo(0.1f).Within(0.0001f));
                Assert.That(AudioListener.volume, Is.EqualTo(0.5f));
                Assert.That(source.clip, Is.SameAs(Resources.Load<AudioClip>("Project333/Audio/BattleOutcomes/Victory")));

                // Wait for the audio thread to start before seeking; Play and a same-frame seek
                // can otherwise race even when Show never replays the cue.
                var startDeadline = Time.realtimeSinceStartup + 2f;
                while (source.timeSamples == 0 && Time.realtimeSinceStartup < startDeadline) yield return null;
                Assert.That(source.timeSamples, Is.GreaterThan(0), "The real clip must start before testing replay suppression.");
                source.timeSamples = source.clip.samples - source.clip.frequency / 10;
                for (var i = 0; i < 20; i++) banner.Show("Victory");
                var finishDeadline = Time.realtimeSinceStartup + 1f;
                while (source.isPlaying && Time.realtimeSinceStartup < finishDeadline) yield return null;
                Assert.That(source.isPlaying, Is.False, "Repeated final results must not replay the cue.");
                banner.Show("Victory");
                Assert.That(source.isPlaying, Is.False, "A completed cue stays completed on refresh.");

                var victorySource = source;
                banner.Hide();
                banner.Show("Defeat");
                source = System.Array.Find(root.GetComponents<AudioSource>(), candidate =>
                    candidate.clip == Resources.Load<AudioClip>("Project333/Audio/BattleOutcomes/Defeat"));
                var defeatSource = source;
                Assert.That(source, Is.Not.Null);
                Assert.That(source, Is.Not.SameAs(victorySource));
                Assert.That(victorySource.isPlaying, Is.False);
                Assert.That(source.isPlaying, Is.True);
                Assert.That(source.volume, Is.EqualTo(0.2f).Within(0.0001f), "Defeat has an independent base gain.");
                Assert.That(source.clip, Is.SameAs(Resources.Load<AudioClip>("Project333/Audio/BattleOutcomes/Defeat")));
                Assert.That(root.GetComponents<AudioSource>().Length, Is.EqualTo(2), "Reuse one source per cue.");
                After333AudioSettings.SetEffectsVolume(0f);
                Assert.That(source.volume, Is.Zero);
                After333AudioSettings.SetEffectsVolume(0.5f);
                Assert.That(source.volume, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(victorySource.volume, Is.EqualTo(0.2f).Within(0.0001f));
                banner.Show("Draw");
                Assert.That(source.isPlaying, Is.False);
                banner.Show("Victory");
                source = victorySource;
                Assert.That(source.isPlaying, Is.True);
                Assert.That(defeatSource.isPlaying, Is.False);
                root.SetActive(false);
                Assert.That(source.isPlaying, Is.False);
                root.SetActive(true);
                Assert.That(source.isPlaying, Is.False, "Enable alone cannot replay a result.");
                Time.timeScale = 0f;
                banner.Show("Defeat");
                source = defeatSource;
                Assert.That(source.isPlaying, Is.True);
                Assert.That(victorySource.isPlaying, Is.False);
                banner.Hide();
                Assert.That(source.isPlaying, Is.False);
                banner.Show("not-a-result");
                Assert.That(source.isPlaying, Is.False);
                cueSettings.Update();
                cueSettings.FindProperty("_victorySoundVolume").floatValue = 0f;
                cueSettings.ApplyModifiedPropertiesWithoutUndo();
                banner.Show("Victory");
                Assert.That(victorySource.isPlaying, Is.False, "Victory alone can be muted.");
                banner.Show("Defeat");
                Assert.That(defeatSource.isPlaying, Is.True, "Muting victory must not mute defeat.");
                Assert.That(defeatSource.volume, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(root.GetComponents<AudioSource>().Length, Is.EqualTo(2), "Switching results must reuse both sources.");
                banner.Hide();
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(listener);
                Time.timeScale = oldTimeScale;
                After333AudioSettings.SetMasterVolume(oldMaster);
                After333AudioSettings.SetEffectsVolume(oldEffects);
                if (hadMaster) PlayerPrefs.SetFloat(masterKey, savedMaster); else PlayerPrefs.DeleteKey(masterKey);
                if (hadEffects) PlayerPrefs.SetFloat(effectsKey, savedEffects); else PlayerPrefs.DeleteKey(effectsKey);
                AudioListener.volume = oldListenerVolume;
                PlayerPrefs.Save();
            }
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator LeavePlayModeOnFailure()
        {
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
