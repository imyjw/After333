using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Online;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class AiActionPacingTests
    {
        private static BattleEventDto Event(BattleEventType type, PlayerId owner = PlayerId.AI) =>
            new BattleEventDto { EventType = type, SourceOwnerId = owner, CardId = "Goblin" };

        [TestCase(BattleEventType.AttackStarted, 1)]
        [TestCase(BattleEventType.OccupantMoved, 1)]
        [TestCase(BattleEventType.RobotFusionResolved, 1)]
        [TestCase(BattleEventType.CardPlayed, 3)]
        [TestCase(BattleEventType.SpellCast, 3)]
        [TestCase(BattleEventType.TurnStarted, 0)]
        [TestCase(BattleEventType.TurnEnded, 0)]
        [TestCase(BattleEventType.DamageApplied, 0)]
        [TestCase(BattleEventType.CardDrawn, 0)]
        [TestCase(BattleEventType.ReconnectGraceCancelled, 0)]
        public void PostActionPause_OnlyPacesDeliberateActions(BattleEventType type, double expected)
        {
            Assert.That(AiActionPacing.GetPostActionPauseSeconds(true, new[] { Event(type) }, 0), Is.EqualTo(expected));
        }

        [TestCase(0, 3)]
        [TestCase(0.5, 2.5)]
        [TestCase(2, 1)]
        [TestCase(3, 1)]
        [TestCase(5, 1)]
        public void CardPlay_PreservesThreeSecondMinimumAndOneSecondPause(double animationSeconds, double expected)
        {
            Assert.That(AiActionPacing.GetPostActionPauseSeconds(true,
                new[] { Event(BattleEventType.SpellCast) }, animationSeconds), Is.EqualTo(expected));
        }

        [TestCase(false, PlayerId.AI)]
        [TestCase(true, PlayerId.Player)]
        public void PvpAndPlayerActions_DoNotPause(bool serverAi, PlayerId owner)
        {
            Assert.That(AiActionPacing.GetPostActionPauseSeconds(serverAi,
                new[] { Event(BattleEventType.AttackStarted, owner) }, 2), Is.Zero);
        }

        [TestCase(BattleEventType.TurnEnded)]
        [TestCase(BattleEventType.BattleEnded)]
        public void TerminalEvents_DoNotDelayPlayerTurnOrResult(BattleEventType terminal)
        {
            Assert.That(AiActionPacing.GetPostActionPauseSeconds(true,
                new[] { Event(BattleEventType.AttackStarted), Event(terminal) }, 2), Is.Zero);
        }

        [Test]
        public void MultiHitAndCounterattack_PauseOnceForTheWholeAction()
        {
            var events = new[] { Event(BattleEventType.AttackStarted), Event(BattleEventType.DamageApplied),
                Event(BattleEventType.DamageApplied), Event(BattleEventType.DamageApplied, PlayerId.Player), null };
            Assert.That(AiActionPacing.GetPostActionPauseSeconds(true, events, 2), Is.EqualTo(1));
            Assert.That(AiActionPacing.GetPostActionPauseSeconds(true, null, 2), Is.Zero);
            Assert.That(AiActionPacing.GetPostActionPauseSeconds(true, new BattleEventDto[0], 2), Is.Zero);
        }

        [TestCase(BattleEventType.OccupantMoved, 1)]
        [TestCase(BattleEventType.SpellCast, 3)]
        [TestCase(BattleEventType.CardPlayed, 3)]
        public void PresentationQueue_AppliesFollowingStateBeforePause(BattleEventType type, float expectedPause)
        {
            var go = new GameObject("AiPacingTest");
            try
            {
                var tester = go.AddComponent<OnlineBattleConnectionTester>();
                tester.ConfigureForLocalTest("ws://unused", "test", "test", PlayerId.Player, false);
                var deck = Enumerable.Repeat("unknown", 20).ToArray();
                var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.AI));
                state.SetPhase(PhaseType.Main);
                state.Player.Master.CurrentHp = 123;
                var view = new BattleStateViewFactory().CreateForPlayer(state, "test", PlayerId.Player);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var queue = (Queue<OnlineBattleEnvelope>)typeof(OnlineBattleConnectionTester)
                    .GetField("_presentationMessages", flags).GetValue(tester);
                queue.Enqueue(new OnlineBattleEnvelope { MessageType = OnlineBattleMessageType.BattleEvents,
                    BattleEvents = new List<BattleEventDto> { Event(type) } });
                queue.Enqueue(new OnlineBattleEnvelope { MessageType = OnlineBattleMessageType.StateView, StateView = view });
                var sequence = (IEnumerator)typeof(OnlineBattleConnectionTester)
                    .GetMethod("ProcessPresentationQueue", flags).Invoke(tester, null);
                WaitForSecondsRealtime pause = null;
                for (var step = 0; step < 10 && sequence.MoveNext(); step++)
                {
                    pause = sequence.Current as WaitForSecondsRealtime;
                    if (pause != null) break;
                }
                Assert.That(pause, Is.Not.Null);
                Assert.That(pause.waitTime, Is.EqualTo(expectedPause));
                Assert.That(tester.LatestStateView, Is.SameAs(view));
                Assert.That(tester.CurrentProjectedBattleState.Player.Master.CurrentHp, Is.EqualTo(123));
                Assert.That(sequence.MoveNext(), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
