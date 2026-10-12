using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Online;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project333.Tests.EditMode
{
    public sealed class MeteorOnlineQueueTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DelayedImpactKeepsFollowingHpSnapshotQueuedUntilTailAndHurtFinish()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyDelayedQueue(endsBattle: false);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DelayedLethalImpactKeepsFollowingBattleEndedSnapshotQueuedUntilReactionsFinish()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyDelayedQueue(endsBattle: true);
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyDelayedQueue(bool endsBattle)
        {
            Assert.That(UnityEngine.Application.isPlaying, Is.True, "The top-level UnityTest must enter Play Mode before driving real coroutines.");
            using (var fixture = new QueueFixture())
            {
                // Awake/LateUpdate must establish the real tile Animator and runtime identity before
                // the synthetic clock hitch; entering Play Mode inside this nested iterator is avoided.
                yield return null;
                yield return null;
                // Use genuine A-301 Hurt playback, with a reaction hold that can be inspected
                // independently from the VFX clock rather than assuming batch-mode frame timing.
                Set(fixture.Bootstrapper, "_attackReactionDuration", 1.7f);
                if (endsBattle) fixture.State.AI.Master.CurrentHp = 20;
                fixture.PrepareCast(endsBattle);
                yield return null;
                var preCastHp = endsBattle ? 20 : 333;
                Assert.That(fixture.After.IsEnded, Is.EqualTo(endsBattle));
                var queue = fixture.BeginQueue();
                try
                {
                    Assert.That(queue.MoveNext(), Is.True);
                    var estimatedWait = queue.Current as WaitForSecondsRealtime;
                    Assert.That(estimatedWait, Is.Not.Null, "The first envelope uses the real animation-duration estimate.");
                    Assert.That(fixture.Bootstrapper.IsMeteorOnlinePresentationPending, Is.True);
                    Assert.That(fixture.Bootstrapper.IsBusy, Is.True, "The stale pre-impact hand cannot submit another action.");
                    fixture.AssertBefore(preCastHp);

                    // Model a clock hitch: the originally estimated deadline has elapsed, but impact only
                    // begins on this late frame and must retain its complete tail. Drive the actual queue
                    // past its timer independently of the still-running BattleBootstrapper coroutine.
                    fixture.SampleLateFlight(estimatedWait.waitTime + 1f);
                    Assert.That(fixture.Remaining, Is.GreaterThan(1.1f));
                    estimatedWait.waitTime = -1f;
                    Assert.That(queue.MoveNext(), Is.True);
                    Assert.That(queue.Current, Is.Null, "An expired estimate must enter the actual-completion wait.");
                    fixture.AssertBefore(preCastHp);

                    var hurtDeadline = Time.realtimeSinceStartup + 8f;
                    while (!fixture.HasImpactFeedback && Time.realtimeSinceStartup < hurtDeadline)
                    {
                        Assert.That(fixture.Bootstrapper.IsMeteorOnlinePresentationPending, Is.True);
                        Assert.That(queue.MoveNext(), Is.True);
                        fixture.AssertBefore(preCastHp);
                        yield return null;
                    }
                    Assert.That(fixture.HasImpactFeedback, Is.True, "The actual online impact must project all A-301 HP bars from 150 to 110. " + fixture.FeedbackDiagnostics);
                    if (endsBattle)
                    {
                        // Advance the real VFX's own unscaled clock to its last frame. The ongoing
                        // reaction/death must still hold the result; watching VFX alone would fail.
                        fixture.CompleteImpactClock();
                        Assert.That(fixture.Remaining, Is.Zero);
                        Assert.That(fixture.Bootstrapper.IsMeteorOnlinePresentationPending, Is.True);
                        Assert.That(fixture.Bootstrapper.IsBusy, Is.True, "Action input stays locked through the remaining reaction.");
                        Assert.That(queue.MoveNext(), Is.True);
                        fixture.AssertBefore(preCastHp);
                    }
                    else
                    {
                        // Keep the nonlethal case fully natural: every rendered tail frame must
                        // retain the old snapshot regardless of wall-clock/frame scheduling.
                        var tailDeadline = Time.unscaledTime + 8f;
                        var stalledFrameDeadline = Time.realtimeSinceStartup + 10f;
                        while (fixture.Remaining > 0f && Time.unscaledTime < tailDeadline && Time.realtimeSinceStartup < stalledFrameDeadline)
                        {
                            Assert.That(fixture.Bootstrapper.IsMeteorOnlinePresentationPending, Is.True, "A still-visible tail must not release the state queue.");
                            Assert.That(fixture.Bootstrapper.IsBusy, Is.True);
                            Assert.That(queue.MoveNext(), Is.True);
                            fixture.AssertBefore(preCastHp);
                            yield return null;
                        }
                        Assert.That(fixture.Remaining, Is.Zero, "The actual VFX unscaled-time deadline must finish.");
                    }

                    var deadline = Time.realtimeSinceStartup + 6f;
                    while (fixture.Bootstrapper.IsMeteorOnlinePresentationPending && Time.realtimeSinceStartup < deadline)
                    {
                        Assert.That(queue.MoveNext(), Is.True);
                        fixture.AssertBefore(preCastHp);
                        yield return null;
                    }
                    Assert.That(fixture.Bootstrapper.IsMeteorOnlinePresentationPending, Is.False, "Tracked reactions must eventually complete without deadlocking the queue.");
                    Assert.That(fixture.Bootstrapper.IsBusy, Is.False, "Completing Meteor releases its presentation input lock.");
                    Assert.That(queue.MoveNext(), Is.False, "Only after complete presentation may the second envelope apply.");
                    Assert.That(Get(fixture.Connection, "_latestStateView"), Is.SameAs(fixture.After));
                    var authoritativeMasterHp = fixture.After.Occupants.Single(occupant =>
                        occupant.OwnerId == PlayerId.AI && occupant.Kind == OccupantKind.Master).CurrentHp;
                    Assert.That(fixture.Connection.CurrentProjectedBattleState.AI.Master.CurrentHp, Is.EqualTo(authoritativeMasterHp),
                        "State projection preserves authoritative overkill HP; only the displayed HP bar clamps to zero.");
                    Assert.That(fixture.Connection.CurrentProjectedBattleState.IsEnded, Is.EqualTo(endsBattle));
                    if (endsBattle) Assert.That(fixture.Connection.CurrentProjectedBattleState.Result.Winner, Is.EqualTo(PlayerId.Player));
                    Assert.That(fixture.Remaining, Is.Zero);
                }
                finally { (queue as IDisposable)?.Dispose(); }
            }
        }

        [UnityTest]
        public IEnumerator CancelClearsBothQueuedStateAndPendingPresentationWithoutOldGenerationReplaying()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var fixture = new QueueFixture())
            {
                fixture.PrepareCast(endsBattle: false);
                var abandoned = fixture.BeginQueue();
                try
                {
                    Assert.That(abandoned.MoveNext(), Is.True);
                    Assert.That(fixture.Bootstrapper.IsMeteorOnlinePresentationPending, Is.True);
                    Assert.That(fixture.Bootstrapper.IsBusy, Is.True);
                    Invoke(fixture.Connection, "StopPresentationQueue");
                    Assert.That(fixture.Bootstrapper.IsMeteorOnlinePresentationPending, Is.False);
                    Assert.That(fixture.Bootstrapper.IsBusy, Is.False, "Cancel releases the abandoned input lock immediately.");
                    Assert.That(fixture.Messages, Is.Empty);
                    Assert.That(fixture.Remaining, Is.Zero);
                    fixture.AssertBefore(333);
                    Assert.That(abandoned.MoveNext(), Is.False);

                    // Start a new generation before the canceled flight's .65-second coroutine wakes.
                    // Its late finally block must not clear the new generation's pending impact.
                    fixture.EnqueuePreparedCast();
                    var resumed = fixture.BeginQueue();
                    try
                    {
                        Assert.That(resumed.MoveNext(), Is.True);
                        Assert.That(fixture.Bootstrapper.IsMeteorOnlinePresentationPending, Is.True);
                        Assert.That(fixture.Bootstrapper.IsBusy, Is.True);
                        fixture.SampleLateFlight(3f);
                        var newImpactStartedAt = Get(fixture.Effect, "_impactStartedAt");
                        var reactionDeadline = Time.realtimeSinceStartup + 8f;
                        while (!fixture.HasStartedHurt && Time.realtimeSinceStartup < reactionDeadline)
                        {
                            Assert.That(fixture.Bootstrapper.IsMeteorOnlinePresentationPending, Is.True);
                            Assert.That((int)Get(fixture.Bootstrapper, "_pendingOnlineMeteorImpacts"), Is.EqualTo(1));
                            fixture.AssertBefore(333);
                            yield return null;
                        }
                        Assert.That(fixture.HasStartedHurt, Is.True);
                        Assert.That(fixture.Bootstrapper.IsMeteorOnlinePresentationPending, Is.True);
                        Assert.That(fixture.Bootstrapper.IsBusy, Is.True);
                        Assert.That(Get(fixture.Effect, "_impactStartedAt"), Is.EqualTo(newImpactStartedAt), "The abandoned generation must not restart the new impact.");
                        Assert.That((int)Get(fixture.Bootstrapper, "_pendingOnlineMeteorImpacts"), Is.EqualTo(1));
                        fixture.AssertBefore(333);
                        Invoke(fixture.Connection, "StopPresentationQueue");
                        Assert.That(fixture.Bootstrapper.IsMeteorOnlinePresentationPending, Is.False);
                        Assert.That(fixture.Bootstrapper.IsBusy, Is.False);
                        Assert.That(resumed.MoveNext(), Is.False);
                    }
                    finally { (resumed as IDisposable)?.Dispose(); }
                    yield return new WaitForSecondsRealtime(.8f);
                    Assert.That(fixture.Bootstrapper.IsMeteorOnlinePresentationPending, Is.False);
                    Assert.That(fixture.Bootstrapper.IsBusy, Is.False);
                    Assert.That(fixture.Remaining, Is.Zero);
                    fixture.AssertBefore(333);
                }
                finally { (abandoned as IDisposable)?.Dispose(); }
            }
            yield return new ExitPlayMode();
        }

        private static object Get(object target, string name) => target.GetType().GetField(name, Hidden).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Hidden).SetValue(target, value);
        private static object Invoke(object target, string name, params object[] arguments) => target.GetType().GetMethod(name, Hidden).Invoke(target, arguments);

        private sealed class QueueFixture : IDisposable
        {
            // Reuse the genuine board/Animator/resource fixture instead of approximating online damage
            // feedback with a mock. This new test file does not change the presentation fixture's owner.
            private readonly object _presentation;
            internal readonly BattleBootstrapper Bootstrapper;
            internal readonly MeteorVfx Effect;
            internal readonly OnlineBattleConnectionTester Connection;
            private readonly LocalBattleGateway _gateway;
            internal BattleState State => _gateway.CurrentBattleState;
            internal Queue<OnlineBattleEnvelope> Messages => (Queue<OnlineBattleEnvelope>)Get(Connection, "_presentationMessages");
            internal BattleStateViewDto Before;
            internal BattleStateViewDto After;
            private List<BattleEventDto> _events;
            internal float Remaining => (float)typeof(MeteorVfx).GetProperty("RemainingDurationSeconds", Hidden).GetValue(Effect);
            internal bool HasStartedHurt
            {
                get
                {
                    var tiles = (TileView[])_presentation.GetType().GetProperty("FeedbackViews", Hidden).GetValue(_presentation);
                    return tiles.Any(tile =>
                    {
                        if (tile.CurrentOccupantCardId != "A-301") return false;
                        var animator = (Animator)Get(tile.GetComponent<TileTextView>(), "_occupantVisualAnimator");
                        return animator != null && animator.runtimeAnimatorController != null && animator.GetCurrentAnimatorStateInfo(0).IsName("BeAttacked");
                    });
                }
            }
            internal bool HasImpactFeedback
            {
                get
                {
                    var targets = FeedbackTargets;
                    return targets.Length == 9 && targets.All(view =>
                        string.Equals((string)Get(view, "_presentedHpRuntimeId"), view.VisualRuntimeId, StringComparison.Ordinal) &&
                        (int)Get(view, "_presentedHp") == 110);
                }
            }
            private TileTextView[] FeedbackTargets => ((TileView[])_presentation.GetType().GetProperty("FeedbackViews", Hidden).GetValue(_presentation))
                .Where(tile => tile.CurrentOccupantCardId == "A-301").Select(tile => tile.GetComponent<TileTextView>()).ToArray();
            internal string FeedbackDiagnostics => "isPlaying=" + UnityEngine.Application.isPlaying +
                ", pending=" + Bootstrapper.IsMeteorOnlinePresentationPending + ", remaining=" + Remaining +
                ", targets=" + string.Join(";", FeedbackTargets.Select(view => view.VisualRuntimeId + ":" +
                    Get(view, "_presentedHpRuntimeId") + ":" + Get(view, "_presentedHp")));

            internal QueueFixture()
            {
                var fixtureType = typeof(MeteorPresentationTests).GetNestedType("Fixture", BindingFlags.NonPublic);
                _presentation = Activator.CreateInstance(fixtureType, Hidden, null, new object[] { true }, null);
                var root = (GameObject)Get(_presentation, "Root");
                Bootstrapper = (BattleBootstrapper)Get(_presentation, "Bootstrapper");
                Effect = (MeteorVfx)Get(_presentation, "Effect");
                _gateway = (LocalBattleGateway)Get(_presentation, "Gateway");
                Invoke(_presentation, "MakeTenFeedbackTargets");
                var host = new GameObject("DormantOnlineQueue");
                host.SetActive(false);
                host.transform.SetParent(root.transform);
                Connection = host.AddComponent<OnlineBattleConnectionTester>();
                Set(Connection, "_battleBootstrapper", Bootstrapper);
                Set(Connection, "_localPlayerId", PlayerId.Player);
                Set(Connection, "_joinUseServerAiOpponent", false);
                Set(Connection, "_hasReceivedFirstStateView", true);
                Set(Connection, "_connectOnStart", false);
            }

            internal void PrepareCast(bool endsBattle)
            {
                State.Player.Hand.Add(MeteorRules.CardId);
                State.Player.Resources.Add(new ResourceSet(7, 0, 0, 0));
                var factory = new BattleStateViewFactory();
                Before = factory.CreateForPlayer(State, "meteor-queue-regression", PlayerId.Player);
                Set(Connection, "_latestStateView", Before);
                Set(Connection, "_currentProjectedBattleState", BattleStateViewProjector.CreateLocalPerspectiveState(Before));
                Bootstrapper.PresentOnlineProjectedState(Connection);
                _gateway.ExecuteCommand(PlayerId.Player, new CastScriptedSpellCommand(MeteorRules.CardId));
                After = factory.CreateForPlayer(State, "meteor-queue-regression", PlayerId.Player);
                _events = new List<BattleEventDto>
                {
                    new BattleEventDto { EventType = BattleEventType.SpellCast, CardId = MeteorRules.CardId,
                        SourceCardId = MeteorRules.CardId, SourceOwnerId = PlayerId.Player, TargetOwnerId = PlayerId.Player }
                };
                foreach (var popup in State.ValuePopupEvents)
                {
                    _events.Add(new BattleEventDto { EventType = BattleEventType.DamageApplied,
                        SourceCardId = popup.SourceCardId, SourceRuntimeId = popup.SourceRuntimeId,
                        SourceOwnerId = popup.SourceOwnerId, TargetOwnerId = popup.OwnerId,
                        TargetCoord = TileCoordDto.FromDomain(popup.Coord), RuntimeId = popup.RuntimeId,
                        TargetRuntimeId = popup.RuntimeId, Amount = popup.Amount, HpBefore = popup.HpBefore,
                        HpAfter = popup.HpAfter, DamageType = popup.DamageType, ValueCause = popup.Cause });
                    if (popup.HpAfter == 0 && popup.HpBefore > 0)
                        _events.Add(new BattleEventDto { EventType = BattleEventType.OccupantRemoved,
                            SourceOwnerId = PlayerId.Player, TargetOwnerId = popup.OwnerId,
                            TargetCoord = TileCoordDto.FromDomain(popup.Coord), TargetRuntimeId = popup.RuntimeId,
                            RuntimeId = popup.RuntimeId });
                }
                if (endsBattle)
                    _events.Add(new BattleEventDto { EventType = BattleEventType.BattleEnded,
                        SourceOwnerId = PlayerId.Player, TargetOwnerId = PlayerId.AI });
                EnqueuePreparedCast();
            }

            internal void EnqueuePreparedCast()
            {
                Messages.Enqueue(new OnlineBattleEnvelope { MessageType = OnlineBattleMessageType.BattleEvents, BattleEvents = _events });
                Messages.Enqueue(new OnlineBattleEnvelope { MessageType = OnlineBattleMessageType.StateView, StateView = After });
            }

            internal IEnumerator BeginQueue() => (IEnumerator)Invoke(Connection, "ProcessPresentationQueue");
            internal void SampleLateFlight(float age) => Invoke(_presentation, "SampleFlight", age);
            internal void CompleteImpactClock() => Invoke(_presentation, "SampleImpact", MeteorVfx.ImpactDurationSeconds + .02f);
            internal void AssertBefore(int masterHp)
            {
                Assert.That(Get(Connection, "_latestStateView"), Is.SameAs(Before));
                Assert.That(Connection.CurrentProjectedBattleState.AI.Master.CurrentHp, Is.EqualTo(masterHp));
                Assert.That(Connection.CurrentProjectedBattleState.IsEnded, Is.False);
            }
            public void Dispose()
            {
                if (Connection != null) Invoke(Connection, "StopPresentationQueue");
                ((IDisposable)_presentation).Dispose();
            }
        }
    }
}
