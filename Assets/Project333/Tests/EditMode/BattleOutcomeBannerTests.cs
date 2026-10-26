using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Battle;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class BattleOutcomeBannerTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _root;
        private BattleBootstrapper _bootstrapper;
        private BattleOutcomeTextView _outcome;
        private BattleFlowController _flow;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("OutcomeTest");
            _root.SetActive(false);
            _bootstrapper = _root.AddComponent<BattleBootstrapper>();
            _outcome = _root.AddComponent<BattleOutcomeTextView>();
            _flow = new BattleFlowController();
            _flow.RestoreBattleState(NewState());
            Set(_bootstrapper, "_battleGateway", new LocalBattleGateway(_flow));
            Set(_bootstrapper, "_sendPlayerActionsToOnlineGateway", false);
            Set(_outcome, "_battleBootstrapper", _bootstrapper);
            _root.SetActive(true);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void LethalAttack_RevealsOnlyAfterPresentationFinishes()
        {
            _outcome.Refresh();
            Assert.That(_root.GetComponentInChildren<BattleOutcomeBannerView>(true), Is.Null);
            var state = _flow.CurrentBattleState;
            state.EndBattle(PlayerId.Player);
            Set(_bootstrapper, "_isBusy", true);
            _outcome.Refresh();
            Assert.That(_root.GetComponentInChildren<BattleOutcomeBannerView>(true), Is.Null);
            Set(_bootstrapper, "_isBusy", false);
            _outcome.Refresh();
            var banner = _root.GetComponentInChildren<BattleOutcomeBannerView>(true);
            Assert.That(banner.gameObject.activeSelf, Is.True);
            Assert.That(banner.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(state.Result.Winner, Is.EqualTo(PlayerId.Player));
            Assert.That(_flow.CurrentBattleState, Is.SameAs(state), "The UI must only observe the result.");
        }

        [Test]
        public void RepeatedRefresh_ReusesBannerAndNewBattleClearsIt()
        {
            _flow.CurrentBattleState.EndBattle(PlayerId.AI);
            _outcome.Refresh();
            var banner = _root.GetComponentInChildren<BattleOutcomeBannerView>(true);
            Set(banner, "_elapsed", 1.25f);
            _outcome.Refresh();
            Assert.That(_root.GetComponentsInChildren<BattleOutcomeBannerView>(true).Length, Is.EqualTo(1));
            Assert.That(Get<float>(banner, "_elapsed"), Is.EqualTo(1.25f));
            var repeatedFinalSnapshot = NewState();
            repeatedFinalSnapshot.EndBattle(PlayerId.AI);
            _flow.RestoreBattleState(repeatedFinalSnapshot);
            _outcome.Refresh();
            Assert.That(Get<float>(banner, "_elapsed"), Is.EqualTo(1.25f), "Equivalent online final snapshots cannot restart the result.");
            _flow.RestoreBattleState(NewState());
            _outcome.Refresh();
            Assert.That(banner.gameObject.activeSelf, Is.False);
            _flow.CurrentBattleState.EndBattleAsDraw();
            _outcome.Refresh();
            var title = banner.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            Assert.That(title.text, Is.EqualTo("무승부"));
            Assert.That(banner.transform.Find("AnimatedBanner/CrownBanner").GetComponent<UnityEngine.UI.Image>().enabled, Is.False);
        }

        [Test]
        public void DraftReturn_WaitsForLethalPresentationThenReadableBannerHold()
        {
            _flow.CurrentBattleState.EndBattle(PlayerId.Player);
            Set(_bootstrapper, "_isBusy", true);
            Set(_bootstrapper, "_returnToDraftSceneDelaySeconds", 0f);
            var sequence = ReturnSequence();
            Assert.That(sequence.MoveNext(), Is.True);
            Assert.That(sequence.Current, Is.Null, "Lethal animation is still running.");
            Set(_bootstrapper, "_isBusy", false);
            Assert.That(sequence.MoveNext(), Is.True);
            var wait = sequence.Current as WaitForSecondsRealtime;
            Assert.That(wait, Is.Not.Null);
            Assert.That(wait.waitTime, Is.GreaterThanOrEqualTo(3.2f));
            // Do not advance into the actual server/result/scene transition path.
        }

        [Test]
        public void DraftReturn_AbandonsAnOldBattleWhileWaitingForPresentation()
        {
            _flow.CurrentBattleState.EndBattle(PlayerId.Player);
            Set(_bootstrapper, "_isBusy", true);
            var sequence = ReturnSequence();
            Assert.That(sequence.MoveNext(), Is.True);
            _flow.RestoreBattleState(NewState());
            Set(_bootstrapper, "_isBusy", false);
            Assert.That(sequence.MoveNext(), Is.False);
            Assert.That(Get<bool>(_bootstrapper, "_hasQueuedDraftSceneReturn"), Is.False);
            Assert.That(_flow.CurrentBattleState.IsEnded, Is.False);
        }

        private IEnumerator ReturnSequence() => (IEnumerator)typeof(BattleBootstrapper)
            .GetMethod("ReturnToDraftSceneAfterDelay", Hidden).Invoke(_bootstrapper, null);

        private static BattleState NewState() => new BattleSetupService().CreateInitialState(
            new BattleSetupRequest(
                BattleDebugDeckFactory.CreateDeck("P", BattleDebugDeckFactory.MinimumDeckSize),
                BattleDebugDeckFactory.CreateDeck("A", BattleDebugDeckFactory.MinimumDeckSize), PlayerId.Player));

        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Hidden).SetValue(owner, value);
        private static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden).GetValue(owner);
    }
}
