using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Cards;
using Project333.Runtime.Presentation.Online;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class OpponentCardRevealQueueTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void OpponentRobotFusionResolvedIsAPlayedCardReveal()
        {
            Assert.That(OpponentPlayedCardRevealRules.ShouldReveal(new BattleEventDto
            {
                EventType = BattleEventType.RobotFusionResolved, SourceOwnerId = PlayerId.AI,
                CardId = "RobotFusion", SourceCardId = "RobotFusion", TargetCardId = "A-111"
            }), Is.True, "The server emits a fusion event instead of SpellCast for this card.");
        }

        [TestCase(PlayerId.Player, "RobotFusion", false)]
        [TestCase(PlayerId.AI, "", false)]
        [TestCase(PlayerId.AI, "RobotFusion", true)]
        public void FusionRevealRetainsPublicIdentityAndOpponentOwnerChecks(PlayerId source, string id, bool expected)
        {
            Assert.That(OpponentPlayedCardRevealRules.ShouldReveal(new BattleEventDto
            {
                EventType = BattleEventType.RobotFusionResolved, SourceOwnerId = source, CardId = id
            }), Is.EqualTo(expected));
        }

        [UnityTest]
        public IEnumerator InstantMessagesDoNotPreventLaterHuanShuFromEitherSeat()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            foreach (var serverAi in new[] { false, true })
            foreach (var seat in new[] { PlayerId.Player, PlayerId.AI })
            using (var f = new Fixture(seat))
            {
                Set(f.Connection, "_joinUseServerAiOpponent", serverAi);
                // An empty keepalive has no animation and may complete synchronously.
                f.Enqueue(new OnlineBattleEnvelope { MessageType = OnlineBattleMessageType.KeepAlive });
                yield return null;
                Assert.That(Field<Coroutine>(f.Connection, "_presentationQueueCoroutine"), Is.Null,
                    "A completed zero-duration queue must not leave a stale running handle.");
                f.Enqueue(f.Card("HuanShu"));
                yield return null;
                yield return null;
                Assert.That(f.Image.sprite, Is.SameAs(CardArt("HuanShu")));
                Assert.That(f.Group.gameObject.activeSelf, Is.True);
                Assert.That(f.Group.alpha, Is.GreaterThan(0f));
                Assert.That(f.Group.blocksRaycasts, Is.False);
                Assert.That(f.Group.interactable, Is.False);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ClearingRevealsDropsOnlyPastHistoryAndAllowsTheNextCard()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var f = new Fixture(PlayerId.Player))
            {
                f.Bootstrapper.QueueOpponentPlayedCardReveals(new[]
                {
                    new BattleEventDto { EventType = BattleEventType.SpellCast, SourceOwnerId = PlayerId.AI, CardId = "HuanShu" },
                    new BattleEventDto { EventType = BattleEventType.SpellCast, SourceOwnerId = PlayerId.AI, CardId = "RobotFusion" }
                });
                yield return null;
                f.Bootstrapper.ClearOpponentPlayedCardReveals();
                Assert.That(f.Group.gameObject.activeSelf, Is.False);
                Assert.That(Field<Coroutine>(f.Bootstrapper, "_opponentPlayedCardRevealCoroutine"), Is.Null);
                Assert.That(((ICollection)Field<object>(f.Bootstrapper, "_opponentPlayedCardRevealQueue")).Count, Is.Zero);
                f.Bootstrapper.QueueOpponentPlayedCardReveals(new[]
                {
                    new BattleEventDto { EventType = BattleEventType.SpellCast, SourceOwnerId = PlayerId.AI, CardId = "Daehwandan" }
                });
                yield return null;
                yield return null;
                Assert.That(f.Image.sprite, Is.SameAs(CardArt("Daehwandan")));
                Assert.That(f.Group.alpha, Is.GreaterThan(0f));
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RapidMixedOpponentCardsRevealEveryCardInFifoOrder()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var f = new Fixture(PlayerId.Player))
            {
                var ids = new[] { "Goblin", "HuanShu", "ManaPond", "Daehwandan", "RobotFusion", "HuanShu" };
                var events = new List<BattleEventDto>();
                for (var i = 0; i < ids.Length; i++)
                    events.Add(new BattleEventDto
                    {
                        EventType = ids[i] == "Goblin" || ids[i] == "ManaPond" ? BattleEventType.CardPlayed :
                            ids[i] == "RobotFusion" ? BattleEventType.RobotFusionResolved : BattleEventType.SpellCast,
                        SourceOwnerId = PlayerId.AI, CardId = ids[i]
                    });
                f.Bootstrapper.QueueOpponentPlayedCardReveals(events);
                var seen = new List<string>();
                var deadline = Time.realtimeSinceStartup + 5f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    var shownId = Field<Text>(f.Bootstrapper, "_opponentPlayedCardFallbackText").text;
                    if (f.Group.gameObject.activeSelf && f.Group.alpha > 0f &&
                        (seen.Count == 0 || seen[seen.Count - 1] != shownId))
                    {
                        seen.Add(shownId);
                        if (shownId == "RobotFusion")
                        {
                            Assert.That(f.Image.sprite, Is.Null, "This JSON-only card has no authored art yet.");
                            Assert.That(Field<Text>(f.Bootstrapper, "_opponentPlayedCardFallbackText").gameObject.activeSelf, Is.True);
                        }
                        else Assert.That(f.Image.sprite, Is.SameAs(CardArt(shownId)));
                    }
                    if (Field<Coroutine>(f.Bootstrapper, "_opponentPlayedCardRevealCoroutine") == null) break;
                    yield return null;
                }
                Assert.That(seen.Count, Is.EqualTo(ids.Length));
                CollectionAssert.AreEqual(ids, seen);
                Assert.That(f.Group.gameObject.activeSelf, Is.False);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator FinalResultCannotCoverAnOpponentHuanShuStillWaitingInTheRevealQueue()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var f = new Fixture(PlayerId.Player))
            {
                var state = EndedState(f.Bootstrapper);
                var outcome = f.Root.AddComponent<BattleOutcomeTextView>();
                outcome.enabled = false;
                Set(outcome, "_battleBootstrapper", f.Bootstrapper);
                f.Bootstrapper.QueueOpponentPlayedCardReveals(new[]
                {
                    new BattleEventDto { EventType = BattleEventType.SpellCast, SourceOwnerId = PlayerId.AI, CardId = "Daehwandan" },
                    new BattleEventDto { EventType = BattleEventType.SpellCast, SourceOwnerId = PlayerId.AI, CardId = "HuanShu" }
                });
                Assert.That(f.Bootstrapper.IsBusy, Is.False, "A reveal must not block battle actions.");
                outcome.Refresh();
                Assert.That(f.Root.GetComponentInChildren<BattleOutcomeBannerView>(true), Is.Null,
                    "The centered result must wait until all already accepted opponent card reveals finish.");
                var sawHuanShu = false;
                var deadline = Time.realtimeSinceStartup + 3f;
                while (Field<Coroutine>(f.Bootstrapper, "_opponentPlayedCardRevealCoroutine") != null && Time.realtimeSinceStartup < deadline)
                {
                    outcome.Refresh();
                    Assert.That(f.Root.GetComponentInChildren<BattleOutcomeBannerView>(true), Is.Null);
                    if (f.Group.alpha > 0f && f.Image.sprite == CardArt("HuanShu")) sawHuanShu = true;
                    yield return null;
                }
                Assert.That(sawHuanShu, Is.True);
                outcome.Refresh();
                Assert.That(f.Root.GetComponentInChildren<BattleOutcomeBannerView>(true), Is.Not.Null);
                Assert.That(state.IsEnded, Is.True, "Waiting is presentation-only.");
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DraftReturnHoldStartsAfterAllQueuedOpponentCardsHaveFinished()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var f = new Fixture(PlayerId.Player))
            {
                EndedState(f.Bootstrapper);
                f.Bootstrapper.QueueOpponentPlayedCardReveals(new[]
                {
                    new BattleEventDto { EventType = BattleEventType.SpellCast, SourceOwnerId = PlayerId.AI, CardId = "HuanShu" }
                });
                var returning = (IEnumerator)Call(f.Bootstrapper, "ReturnToDraftSceneAfterDelay");
                try
                {
                    Assert.That(returning.MoveNext(), Is.True);
                    Assert.That(returning.Current, Is.Null, "Do not begin the result timer while a reveal is pending.");
                    var deadline = Time.realtimeSinceStartup + 3f;
                    while (Field<Coroutine>(f.Bootstrapper, "_opponentPlayedCardRevealCoroutine") != null && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    Assert.That(returning.MoveNext(), Is.True);
                    Assert.That(returning.Current, Is.TypeOf<WaitForSecondsRealtime>());
                    Assert.That(((WaitForSecondsRealtime)returning.Current).waitTime, Is.GreaterThanOrEqualTo(BattleOutcomeBannerView.MinimumDisplaySeconds));
                    // Do not advance into the actual run/scene-return path.
                }
                finally { (returning as IDisposable)?.Dispose(); }
            }
            yield return new ExitPlayMode();
        }

        private static BattleState EndedState(BattleBootstrapper bootstrapper)
        {
            var cards = new[] { "Goblin", "Goblin", "Goblin", "Goblin", "Goblin", "Goblin" };
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(cards, cards, PlayerId.Player));
            state.SetPhase(PhaseType.Main);
            state.EndBattle(PlayerId.AI);
            var flow = new BattleFlowController();
            flow.RestoreBattleState(state);
            Set(bootstrapper, "_battleGateway", new LocalBattleGateway(flow));
            return state;
        }

        private static Sprite CardArt(string id)
        {
            Assert.That(CardArtworkLibrary.TryGetArtwork(id, out var sprite), Is.True, id + " artwork must exist.");
            return sprite;
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root;
            internal readonly BattleBootstrapper Bootstrapper;
            internal readonly OnlineBattleConnectionTester Connection;
            private readonly string _originalToken;
            private readonly PlayerId _seat;
            internal CanvasGroup Group => Field<CanvasGroup>(Bootstrapper, "_opponentPlayedCardRevealCanvasGroup");
            internal Image Image => Field<Image>(Bootstrapper, "_opponentPlayedCardRevealImage");

            internal Fixture(PlayerId seat)
            {
                _seat = seat;
                _originalToken = AccountSessionState.SessionToken;
                SetSession("opponent-reveal-fixture-no-http");
                Root = new GameObject("OpponentRevealFixture");
                Root.SetActive(false);
                Bootstrapper = Root.AddComponent<BattleBootstrapper>();
                Bootstrapper.enabled = false;
                Set(Bootstrapper, "_startOnAwake", false);
                Set(Bootstrapper, "_startWithDraftBeforeBattle", false);
                Set(Bootstrapper, "_autoRunAiTurns", false);
                Set(Bootstrapper, "_opponentPlayedCardNormalDisplaySeconds", .3f);
                Set(Bootstrapper, "_opponentPlayedCardQueuedDisplaySeconds", .2f);
                Set(Bootstrapper, "_opponentPlayedCardFadeInSeconds", .02f);
                Set(Bootstrapper, "_opponentPlayedCardFadeOutSeconds", .02f);
                Connection = Root.AddComponent<OnlineBattleConnectionTester>();
                Connection.enabled = false;
                Set(Connection, "_connectOnStart", false);
                Set(Connection, "_automaticallyReconnectDuringBattle", false);
                Set(Connection, "_battleBootstrapper", Bootstrapper);
                Set(Connection, "_joinUseServerAiOpponent", false);
                Set(Connection, "_localPlayerId", seat);
                Set(Bootstrapper, "_onlineConnectionTester", Connection);
                Root.SetActive(true);
                foreach (var field in new[] { "_opponentPlayedCardRevealCanvasGroup", "_yourTurnBannerCanvasGroup" })
                {
                    var component = Field<CanvasGroup>(Bootstrapper, field);
                    if (component != null) component.transform.SetParent(Root.transform, false);
                }
            }

            internal void Enqueue(OnlineBattleEnvelope envelope) => Call(Connection, "QueuePresentationEnvelope", envelope);
            internal OnlineBattleEnvelope Card(string id) => new OnlineBattleEnvelope
            {
                MessageType = OnlineBattleMessageType.BattleEvents,
                BattleEvents = new List<BattleEventDto> { new BattleEventDto
                {
                    EventType = BattleEventType.SpellCast, CardId = id,
                    SourceOwnerId = _seat == PlayerId.Player ? PlayerId.AI : PlayerId.Player
                } }
            };
            public void Dispose()
            {
                Bootstrapper.ClearOpponentPlayedCardReveals();
                Object.DestroyImmediate(Root);
                SetSession(_originalToken);
            }
        }

        private static void SetSession(string value) => typeof(AccountSessionState)
            .GetField("<SessionToken>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Hidden).SetValue(owner, value);
        private static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Hidden).Invoke(owner, args);
    }
}
