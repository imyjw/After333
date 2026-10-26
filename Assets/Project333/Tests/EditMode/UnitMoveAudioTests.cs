using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Tutorial;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project333.Tests.EditMode
{
    public sealed class UnitMoveAudioTests
    {
        private static void Set(object owner, string field, object value) =>
            owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);

        [Test]
        public void AttachedMp3_ImportsAsPlayableMonoClip()
        {
            const string path = "Assets/Project333/Resources/Project333/Audio/Common/UnitMove.mp3";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.length, Is.GreaterThan(0f));
            Assert.That(clip.samples, Is.GreaterThan(0));
            Assert.That(((AudioImporter)AssetImporter.GetAtPath(path)).forceToMono, Is.True);
        }

        [UnityTest]
        public IEnumerator AcceptedMoves_PlaySound_WhileRejectedMovesAndNonMoveUpdatesStaySilent()
        {
            yield return new EnterPlayMode();
            TutorialRuntimeIsolation.RequestEntry(); // Prevent accounts, server access and auto battle startup.
            var root = new GameObject("MoveAudioTest");
            var savedVolume = AudioListener.volume;
            try
            {
                AudioListener.volume = 0f;
                root.AddComponent<AudioListener>();
                var screen = root.AddComponent<BattleScreenPresenter>();
                var controller = root.AddComponent<BattleBootstrapper>();
                Set(controller, "_startOnAwake", false);
                Set(controller, "_autoRunAiTurns", false);
                Set(controller, "_sendPlayerActionsToOnlineGateway", false);
                Set(controller, "_battleScreenPresenter", screen);
                Set(controller, "_animateOnlineMoveEvents", false);
                var audio = root.GetComponent<AudioSource>();
                Assert.That(audio, Is.Not.Null);
                Assert.That(audio.isPlaying, Is.False);

                controller.PlayOnlineBattleEventAnimations(new[] { new BattleEventDto { EventType = BattleEventType.StateChanged } });
                Assert.That(audio.isPlaying, Is.False);
                controller.PlayOnlineBattleEventAnimations(new[] { Move(PlayerId.Player, 1, 0, 1, 0), new BattleEventDto { EventType = BattleEventType.OccupantMoved } });
                Assert.That(audio.isPlaying, Is.False, "Malformed and zero-distance events are silent.");

                foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
                {
                    controller.PlayOnlineBattleEventAnimations(new[] { Move(owner, 1, 0, 2, 0) });
                    Assert.That(audio.isPlaying, Is.True, "Both viewer sides play accepted movement even with visual travel disabled.");
                    audio.Stop();
                }
                controller.PlayOnlineBattleEventAnimations(new[] { Move(PlayerId.Player, 1, 0, 2, 0), Move(PlayerId.Player, 2, 0, 1, 0) });
                Assert.That(audio.isPlaying, Is.True, "Swap event batch emits the movement cue.");
                Assert.That(root.GetComponents<AudioSource>().Length, Is.EqualTo(1), "Movement reuses the card-draw source.");
                audio.Stop();
                yield return null;
                Assert.That(audio.isPlaying, Is.False, "Later animation groups must not restart the swap cue.");

                var state = LocalState();
                var flow = new BattleFlowController();
                flow.RestoreBattleState(state);
                Set(controller, "_battleGateway", new LocalBattleGateway(flow));
                Assert.That(controller.TryMovePlayerOccupant(new TileCoord(0, 0), new TileCoord(0, 1), out _), Is.False);
                Assert.That(audio.isPlaying, Is.False, "Rejected local commands do not emit the cue.");
                Assert.That(controller.TryMovePlayerOccupant(new TileCoord(2, 1), new TileCoord(2, 0), out var message), Is.True, message);
                Assert.That(audio.isPlaying, Is.True, "Successful local/tutorial movement emits the cue after acceptance.");
                Assert.That(state.PlayerBoard.GetOccupant(new TileCoord(2, 0)), Is.Not.Null);
                audio.Stop();

                state = LocalState();
                state.PlayerBoard.Place(new TileCoord(1, 1), new UnitState("ally", "Goblin", PlayerId.Player,
                    new TileCoord(1, 1), AttackType.Melee, 1, 5, true, false, 0));
                flow.RestoreBattleState(state);
                Assert.That(controller.TryMovePlayerOccupant(new TileCoord(2, 1), new TileCoord(1, 1), out message), Is.True, message);
                Assert.That(audio.isPlaying, Is.True);
                Assert.That(state.PlayerBoard.GetOccupant(new TileCoord(2, 1)).RuntimeId, Is.EqualTo("ally"));
                Assert.That(audio.ignoreListenerVolume, Is.False);
                Assert.That(audio.ignoreListenerPause, Is.False);
                Assert.That(audio.spatialBlend, Is.Zero);
                Assert.That(AudioListener.volume, Is.Zero, "Sound playback must not change master volume.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                TutorialRuntimeIsolation.CancelEntry();
                AudioListener.volume = savedVolume;
            }
            yield return new ExitPlayMode();
        }

        private static BattleState LocalState()
        {
            var deck = Enumerable.Repeat("Fireball", 33).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.Player));
            new MulliganService().PassMulligan(state, PlayerId.Player);
            state.SetPhase(PhaseType.Main);
            return state;
        }

        private static BattleEventDto Move(PlayerId owner, int fromX, int fromY, int toX, int toY) => new BattleEventDto
        {
            EventType = BattleEventType.OccupantMoved,
            SourceOwnerId = owner, TargetOwnerId = owner,
            SourceCoord = new TileCoordDto { Column = fromX, Row = fromY },
            TargetCoord = new TileCoordDto { Column = toX, Row = toY }
        };

        [UnityTearDown]
        public IEnumerator LeavePlayModeOnFailure()
        {
            TutorialRuntimeIsolation.CancelEntry();
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
