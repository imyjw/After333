using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Presentation.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class BattleCommonHitAudioTests
    {
        [TestCase("Project333/Audio/RedDragon/FireBreath", .46f, 1)]
        [TestCase("Project333/Audio/Common/UnitHit", 1.124331f, 2)]
        public void ShortEffects_ArePackagedAsExpectedPcmAudio(string path, float duration, int channels)
        {
            var clip = Resources.Load<AudioClip>(path);
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.channels, Is.EqualTo(channels));
            Assert.That(clip.frequency, Is.EqualTo(44100));
            Assert.That(clip.length, Is.EqualTo(duration).Within(.001f));
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as AudioImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.defaultSampleSettings.compressionFormat, Is.EqualTo(AudioCompressionFormat.PCM));
            Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
            clip.LoadAudioData();
            var samples = new float[clip.samples * clip.channels];
            Assert.That(clip.GetData(samples, 0), Is.True);
            double squared = 0; float peak = 0;
            foreach (var sample in samples)
            {
                Assert.That(float.IsNaN(sample), Is.False);
                peak = Mathf.Max(peak, Mathf.Abs(sample)); squared += sample * sample;
            }
            if (channels == 1) // Authored fire breath retains its measured headroom and tapered edges.
            {
                Assert.That(peak, Is.InRange(.5f, .75f));
                Assert.That(Math.Sqrt(squared / samples.Length), Is.InRange(.04d, .3d));
                Assert.That(Mathf.Abs(samples[0]), Is.LessThan(.0001f));
                Assert.That(Mathf.Abs(samples[samples.Length - 1]), Is.LessThan(.0001f));
            }
            else // User-supplied 55.flac is converted losslessly; do not require the old synthetic waveform.
            {
                Assert.That(peak, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
                Assert.That(squared, Is.GreaterThan(0d));
            }
        }

        [UnityTest]
        public IEnumerator DamagedOccupantsShareOneSourceAndHonorListenerVolumeAndDisable()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return CheckReactions();
            yield return new ExitPlayMode();
        }

        private static IEnumerator CheckReactions()
        {
            var oldVolume = AudioListener.volume;
            var oldPause = AudioListener.pause;
            var listener = new GameObject("CommonHitTestListener", typeof(AudioListener));
            try
            {
                AudioListener.volume = 0f;
                AudioListener.pause = false;
                foreach (var cardId in new[] { "BlueDragon", "Gwangma", "RedDragon", "Golem", "Master", "PowerPlant" })
                {
                    var go = new GameObject("CommonHit_" + cardId);
                    go.SetActive(false);
                    try
                    {
                        var view = go.AddComponent<TileTextView>();
                        typeof(TileTextView).GetField("_lastVisualCardId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, cardId);
                        view.PlayBeAttackedAnimation();
                        Assert.That(go.GetComponent<AudioSource>(), Is.Null, "An inactive tile must not emit sound.");
                        go.SetActive(true);
                        var settings = Resources.Load<GameObject>("Project333/Audio/Common/HitSoundSettings");
                        Assert.That(settings, Is.Not.Null, "One shared editable settings prefab must be packaged.");
                        var configured = settings.GetComponent<AudioSource>();
                        var savedClip = configured.clip;
                        try
                        {
                            configured.clip = null;
                            view.PlayBeAttackedAnimation();
                            Assert.That(go.GetComponent<AudioSource>(), Is.Null, "Clearing Audio Clip disables the shared cue.");
                        }
                        finally { configured.clip = savedClip; }
                        view.PlayBeAttackedAnimation();
                        var source = go.GetComponent<AudioSource>();
                        Assert.That(source, Is.Not.Null, cardId + " must receive the shared sound regardless of its own motion asset.");
                        Assert.That(source.ignoreListenerVolume, Is.False);
                        Assert.That(source.ignoreListenerPause, Is.False);
                        Assert.That(source.spatialBlend, Is.Zero);
                        Assert.That(AudioListener.volume, Is.Zero, "Playback must not change the saved master-volume path.");
                        view.PlayBeAttackedAnimation();
                        Assert.That(go.GetComponents<AudioSource>().Length, Is.EqualTo(1), "Repeated damage reuses its source.");
                        if (cardId != "BlueDragon" && cardId != "Gwangma")
                        {
                            source.Stop();
                            view.PlayDeathAnimation();
                            Assert.That(source.isPlaying, Is.False, "Death alone must not create a second common hit.");
                        }
                        view.PlayBeAttackedAnimation();
                        go.SetActive(false);
                        Assert.That(source.isPlaying, Is.False, "Disable/rebind cleanup must stop lingering impacts.");
                    }
                    finally { Object.DestroyImmediate(go); }
                }
                yield return null;
            }
            finally
            {
                Object.DestroyImmediate(listener);
                AudioListener.volume = oldVolume;
                AudioListener.pause = oldPause;
            }
        }

        [UnityTest]
        public IEnumerator SameFrameAreaHitsCoalesceButLaterFramesAndResetCanPlay()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return CheckAreaHits();
            yield return new ExitPlayMode();
        }

        private static IEnumerator CheckAreaHits()
        {
            var root = new GameObject("CommonHitAreaFixture");
            try
            {
                var first = new GameObject("FirstVictim").AddComponent<TileTextView>();
                var second = new GameObject("SecondVictim").AddComponent<TileTextView>();
                var afterReset = new GameObject("NextBattleVictim").AddComponent<TileTextView>();
                first.transform.SetParent(root.transform);
                second.transform.SetParent(root.transform);
                afterReset.transform.SetParent(root.transform);
                first.PlayBeAttackedAnimation();
                Assert.That(first.GetComponent<AudioSource>(), Is.Not.Null);
                second.PlayBeAttackedAnimation();
                Assert.That(second.GetComponent<AudioSource>(), Is.Null,
                    "Simultaneous AoE damage shares one cue instead of stacking identical waveforms.");
                yield return null;
                second.PlayBeAttackedAnimation();
                Assert.That(second.GetComponent<AudioSource>(), Is.Not.Null,
                    "A new frame must allow the next strike to sound.");
                second.ResetCombatPresentation();
                afterReset.PlayBeAttackedAnimation();
                Assert.That(afterReset.GetComponent<AudioSource>(), Is.Not.Null,
                    "Cancel/reset must release the cue owner even in the same frame.");
                afterReset.gameObject.SetActive(false);
                var afterDisable = new GameObject("ReplacementVictim").AddComponent<TileTextView>();
                afterDisable.transform.SetParent(root.transform);
                afterDisable.PlayBeAttackedAnimation();
                Assert.That(afterDisable.GetComponent<AudioSource>(), Is.Not.Null,
                    "A disabled old scene/tile must not suppress the next battle's first sound.");
            }
            finally { Object.DestroyImmediate(root); }
        }
        [UnityTearDown]
        public IEnumerator LeavePlayModeOnFailure()
        {
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}