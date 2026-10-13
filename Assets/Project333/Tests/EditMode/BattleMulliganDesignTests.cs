using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Hand;
using Project333.Runtime.Presentation.Online;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    // Exercise the authored overlay without starting an account, network connection or real battle.
    public sealed class BattleMulliganDesignTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (Application.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void AuthoredOverlayUsesRequestedLabelsAndFiveLargeOpeningCards()
        {
            var fixture = BattleMulliganDesignTestScene.Load();
            var state = BattleMulliganDesignTestScene.State();
            var before = BattleMulliganDesignTestScene.TruthSnapshot(state);
            fixture.Presenter.Present(state);
            Canvas.ForceUpdateCanvases();

            Assert.That(fixture.Text("TitleText").text, Is.EqualTo("시작 핸드 카드"));
            Assert.That(fixture.Text("InstructionText").text, Is.EqualTo("교환할 카드 선택"));
            Assert.That(fixture.Confirm.GetComponentInChildren<Text>(true).text, Is.EqualTo("확인"));
            Assert.That(fixture.Text("TurnOrderBadge").text, Does.Contain("선공"));
            Assert.That(fixture.ActiveCards.Length, Is.EqualTo(5));
            foreach (var unused in fixture.Buttons.Where(c => !c.gameObject.activeSelf))
                AssertCardVisual(unused, keep: false, replace: false);
            foreach (var card in fixture.ActiveCards)
            {
                Assert.That(card.gameObject.activeSelf, Is.True);
                Assert.That(((RectTransform)card.transform).rect.height, Is.GreaterThan(300));
                Assert.That(card.interactable, Is.True);
                AssertCardVisual(card, keep: true, replace: false);
            }
            Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(before));
        }

        [Test]
        public void TogglingOneDuplicateSelectsOnlyThatSlotAndCanBeUndone()
        {
            var fixture = BattleMulliganDesignTestScene.Load();
            var state = BattleMulliganDesignTestScene.State("Goblin", "Goblin", "SkeletonArcher", "ManaPond", "Goblin");
            var before = BattleMulliganDesignTestScene.TruthSnapshot(state);
            fixture.Presenter.Present(state);

            fixture.Presenter.ToggleCardFromUi(1);
            Assert.That(fixture.Presenter.SelectedCardCount, Is.EqualTo(1));
            AssertCardVisual(fixture.Buttons[0], keep: true, replace: false);
            AssertCardVisual(fixture.Buttons[1], keep: false, replace: true);
            AssertCardVisual(fixture.Buttons[2], keep: true, replace: false);
            fixture.Presenter.ToggleCardFromUi(1);
            Assert.That(fixture.Presenter.SelectedCardCount, Is.Zero);
            foreach (var card in fixture.ActiveCards) AssertCardVisual(card, keep: true, replace: false);
            Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(before));
        }

        [Test]
        public void AllCardsCanBeMarkedAndZeroSelectionsStillAllowsConfirmation()
        {
            var fixture = BattleMulliganDesignTestScene.Load();
            fixture.Presenter.Present(BattleMulliganDesignTestScene.State());
            Assert.That(fixture.Confirm.interactable, Is.True);
            for (var i = 0; i < 5; i++) fixture.Presenter.ToggleCardFromUi(i);
            Assert.That(fixture.Presenter.SelectedCardCount, Is.EqualTo(5));
            foreach (var card in fixture.ActiveCards) AssertCardVisual(card, keep: false, replace: true);
            for (var i = 0; i < 5; i++) fixture.Presenter.ToggleCardFromUi(i);
            Assert.That(fixture.Confirm.interactable, Is.True);
            Assert.That(fixture.Presenter.SelectedCardCount, Is.Zero);
        }

        [Test]
        public void FiveCardHandExpandsAuthoredCapacityAndBindsFifthRealButtonOnlyOnce()
        {
            var fixture = BattleMulliganDesignTestScene.Load();
            var authored = fixture.Buttons.ToArray();
            var state = BattleMulliganDesignTestScene.State();
            var before = BattleMulliganDesignTestScene.TruthSnapshot(state);
            fixture.Presenter.Present(state);
            Assert.That(fixture.ActiveCards.Length, Is.EqualTo(5));
            Assert.That(fixture.Buttons.Take(authored.Length), Is.EqualTo(authored),
                "Expanding the opening hand must retain the authored slots and their references.");
            var fifth = fixture.Buttons[4];
            var artwork = fifth.transform.Find("CardArtwork").GetComponent<Image>();
            Assert.That(artwork, Is.Not.Null);
            Assert.That(artwork.sprite, Is.Not.Null, "The fifth card must render its own artwork.");
            Assert.That(artwork, Is.Not.SameAs(fixture.Buttons[0].transform.Find("CardArtwork").GetComponent<Image>()));
            foreach (var name in new[] { "AttackValueText", "HpValueText" })
            {
                var text = fifth.GetComponentsInChildren<Text>(true).Single(t => t.name == name);
                Assert.That(text.font, Is.Not.Null, "Expanded slots must retain their authored stat fonts.");
            }
            var allButtons = fixture.Buttons.ToArray();
            var childCount = fixture.Presenter.GetComponentsInChildren<Transform>(true).Length;
            for (var repeat = 0; repeat < 5; repeat++) fixture.Presenter.Present(state);
            Assert.That(fixture.Buttons, Is.EqualTo(allButtons));
            Assert.That(fixture.Presenter.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(childCount));

            fifth.onClick.Invoke();
            Assert.That(fixture.Presenter.SelectedCardCount, Is.EqualTo(1),
                "The fifth native button must have one listener bound to the fifth slot.");
            for (var index = 0; index < 5; index++)
                AssertCardVisual(fixture.Buttons[index], keep: index != 4, replace: index == 4);
            fixture.Presenter.Present(state);
            fifth.onClick.Invoke();
            Assert.That(fixture.Presenter.SelectedCardCount, Is.Zero, "A second click must undo selection exactly once.");
            Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(before));
        }

        [Test]
        public void ConfirmedReconnectShowsCleanAuthoritativeHandAndWaitingMessage()
        {
            var fixture = BattleMulliganDesignTestScene.Load();
            var state = BattleMulliganDesignTestScene.State();
            state.Player.MarkMulliganUsed();
            var before = BattleMulliganDesignTestScene.TruthSnapshot(state);
            fixture.Presenter.Present(state);
            Assert.That(fixture.Presenter.IsDealingOpeningHand, Is.False);
            Assert.That(fixture.Text("StatusText").text, Is.EqualTo("상대의 멀리건을 기다리는 중..."));
            foreach (var card in fixture.ActiveCards)
            {
                Assert.That(card.interactable, Is.False);
                AssertCardVisual(card, keep: false, replace: false);
            }
            fixture.Presenter.ToggleCardFromUi(0);
            fixture.Presenter.ConfirmSelectionFromUi();
            Assert.That(fixture.Presenter.SelectedCardCount, Is.Zero);
            Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(before));
        }

        [Test]
        public void ReconnectAfterMulliganDoesNotShowStaleOverlay()
        {
            var fixture = BattleMulliganDesignTestScene.Load();
            var state = BattleMulliganDesignTestScene.State();
            state.Player.MarkMulliganUsed();
            state.AI.MarkMulliganUsed();
            state.SetPhase(PhaseType.Main);
            fixture.Presenter.Present(state);
            Assert.That(fixture.Presenter.IsSessionActive, Is.False);
            Assert.That(fixture.Presenter.IsDealingOpeningHand, Is.False);
            Assert.That(fixture.Presenter.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ApplyingDesignRepeatedlyPreservesSerializedCardsStatsAndSelection()
        {
            var fixture = BattleMulliganDesignTestScene.Load();
            fixture.Presenter.Present(BattleMulliganDesignTestScene.State());
            fixture.Presenter.ToggleCardFromUi(0);
            var artwork = fixture.Buttons.Select(c => c.transform.Find("CardArtwork").GetComponent<Image>()).ToArray();
            var statTexts = fixture.Buttons.SelectMany(c => c.GetComponentsInChildren<Text>(true))
                .Where(t => t.name == "AttackValueText" || t.name == "HpValueText").ToArray();
            var fonts = statTexts.Select(t => t.font).ToArray();
            var count = fixture.Presenter.GetComponentsInChildren<Transform>(true).Length;
            fixture.Presenter.ApplyPresentationDesign();
            fixture.Presenter.ApplyPresentationDesign();
            Assert.That(fixture.Presenter.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            Assert.That(fixture.Buttons.Select(c => c.transform.Find("CardArtwork").GetComponent<Image>()), Is.EqualTo(artwork));
            Assert.That(statTexts.Select(t => t.font), Is.EqualTo(fonts));
            AssertCardVisual(fixture.Buttons[0], keep: false, replace: true);
            Assert.That(fixture.Presenter.GetComponentsInChildren<Transform>(true).Count(t => t.name == "InstructionText"), Is.EqualTo(1));
            foreach (var card in fixture.ActiveCards)
            {
                Assert.That(card.GetComponentsInChildren<Transform>(true).Count(t => t.name == "KeepAura"), Is.EqualTo(1));
                Assert.That(card.GetComponentsInChildren<Transform>(true).Count(t => t.name == "ReplaceCross"), Is.EqualTo(1));
                Assert.That(card.GetComponentsInChildren<Transform>(true).Count(t => t.name == "ReplaceLabel"), Is.EqualTo(1));
                foreach (var graphic in card.GetComponentsInChildren<Graphic>(true).Where(g => g.gameObject != card.gameObject))
                    Assert.That(graphic.raycastTarget, Is.False, "Decoration must not steal card clicks: " + graphic.name);
            }
        }

        [UnityTest]
        public IEnumerator EntranceRunsOnceFromOwnDeckInUnscaledTimeWithoutChangingBattleTruth()
        {
            BattleMulliganDesignTestScene.Load();
            yield return new EnterPlayMode();
            var fixture = BattleMulliganDesignTestScene.Current();
            var state = BattleMulliganDesignTestScene.State();
            var before = BattleMulliganDesignTestScene.TruthSnapshot(state);
            var source = fixture.Deck.GetComponentsInChildren<Image>(true).Single(i => i.name == "PlayerDeckCardBack_02");
            fixture.Presenter.ConfigureDrawSource(source.rectTransform, source.sprite);
            Time.timeScale = 0f;
            fixture.Presenter.Present(state);
            Canvas.ForceUpdateCanvases();
            Assert.That(fixture.Presenter.IsDealingOpeningHand, Is.True);
            Assert.That(fixture.Confirm.interactable, Is.False);
            fixture.Presenter.ToggleCardFromUi(0);
            fixture.Presenter.ConfirmSelectionFromUi();
            Assert.That(fixture.Presenter.SelectedCardCount, Is.Zero);
            var ghosts = BattleMulliganDesignTestScene.Ghosts(fixture.Presenter);
            Assert.That(ghosts.Length, Is.EqualTo(5));
            var firstGhost = ghosts.OrderBy(g => g.name).First();
            var sourceCanvas = source.GetComponentInParent<Canvas>().rootCanvas;
            var targetCanvas = firstGhost.GetComponentInParent<Canvas>().rootCanvas;
            var sourceScreen = RectTransformUtility.WorldToScreenPoint(
                sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : sourceCanvas.worldCamera,
                source.rectTransform.TransformPoint(source.rectTransform.rect.center));
            var ghostScreen = RectTransformUtility.WorldToScreenPoint(
                targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera,
                ((RectTransform)firstGhost).TransformPoint(((RectTransform)firstGhost).rect.center));
            Assert.That(Vector2.Distance(sourceScreen, ghostScreen), Is.LessThan(2f),
                "The first frame must originate near the authored bottom-right deck.");
            for (var i = 0; i < 5; i++) fixture.Presenter.Present(state);
            Assert.That(BattleMulliganDesignTestScene.Ghosts(fixture.Presenter).Length, Is.EqualTo(5));
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(fixture.Presenter.IsDealingOpeningHand, Is.False, "Server countdown must not be paused by game timeScale.");
            Assert.That(BattleMulliganDesignTestScene.Ghosts(fixture.Presenter).Length, Is.Zero);
            Assert.That(fixture.Confirm.interactable, Is.True);
            foreach (var card in fixture.ActiveCards)
            {
                Assert.That(card.interactable, Is.True);
                Assert.That(card.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(.001f));
                AssertCardVisual(card, keep: true, replace: false);
            }
            fixture.Buttons[0].onClick.Invoke();
            Assert.That(fixture.Presenter.SelectedCardCount, Is.EqualTo(1), "Settled card button must bind the real click listener.");
            fixture.Presenter.Present(state);
            Assert.That(fixture.Presenter.IsDealingOpeningHand, Is.False, "Refresh must not deal the same hand again.");
            Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(before));
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator TimeoutDuringEntranceCancelsFlightAndShowsCleanKeptHand()
        {
            BattleMulliganDesignTestScene.Load();
            yield return new EnterPlayMode();
            var fixture = BattleMulliganDesignTestScene.Current();
            var state = BattleMulliganDesignTestScene.State();
            var opening = state.Player.Hand.CardIds.ToArray();
            fixture.Presenter.Present(state);
            Assert.That(fixture.Presenter.IsDealingOpeningHand, Is.True);
            state.Player.MarkMulliganUsed();
            state.AI.MarkMulliganUsed();
            state.SetPhase(PhaseType.Main);
            var before = BattleMulliganDesignTestScene.TruthSnapshot(state);
            fixture.Presenter.Present(state);
            yield return null;
            Assert.That(fixture.Presenter.IsDealingOpeningHand, Is.False);
            Assert.That(BattleMulliganDesignTestScene.Ghosts(fixture.Presenter), Is.Empty);
            Assert.That(fixture.Presenter.IsSessionActive, Is.True);
            Assert.That(fixture.Confirm.gameObject.activeSelf, Is.False);
            fixture.Presenter.ApplyPresentationDesign();
            Assert.That(fixture.Confirm.gameObject.activeSelf, Is.False, "Layout refresh must keep final confirmation hidden.");
            Assert.That(fixture.Text("InstructionText").text, Is.EqualTo("전투 준비 완료"));
            foreach (var card in fixture.ActiveCards)
            {
                Assert.That(card.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(.001f));
                AssertCardVisual(card, keep: false, replace: false);
            }
            Assert.That((IEnumerable<string>)BattleMulliganDesignTestScene.Get(fixture.Presenter, "_displayCardIds"), Is.EqualTo(opening));
            Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(before));
            yield return new WaitForSecondsRealtime(3.2f);
            Assert.That(fixture.Presenter.IsSessionActive, Is.False);
            Assert.That(fixture.Presenter.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ConfirmationDuringEntranceCancelsFlightAndLocksWaitingCards()
        {
            BattleMulliganDesignTestScene.Load();
            yield return new EnterPlayMode();
            var fixture = BattleMulliganDesignTestScene.Current();
            var state = BattleMulliganDesignTestScene.State();
            fixture.Presenter.Present(state);
            state.Player.MarkMulliganUsed();
            var before = BattleMulliganDesignTestScene.TruthSnapshot(state);
            fixture.Presenter.Present(state);
            yield return null;
            Assert.That(fixture.Presenter.IsDealingOpeningHand, Is.False);
            Assert.That(BattleMulliganDesignTestScene.Ghosts(fixture.Presenter), Is.Empty);
            Assert.That(fixture.Text("StatusText").text, Is.EqualTo("상대의 멀리건을 기다리는 중..."));
            foreach (var card in fixture.ActiveCards)
            {
                Assert.That(card.interactable, Is.False);
                Assert.That(card.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(.001f));
                AssertCardVisual(card, keep: false, replace: false);
            }
            Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator HidingOrRelayoutDuringEntranceRemovesGhostsAndNextSessionCanDealAgain()
        {
            BattleMulliganDesignTestScene.Load();
            yield return new EnterPlayMode();
            var fixture = BattleMulliganDesignTestScene.Current();
            var state = BattleMulliganDesignTestScene.State();
            fixture.Presenter.Present(state);
            yield return null;
            fixture.Presenter.Present(null);
            yield return null;
            Assert.That(fixture.Presenter.IsSessionActive, Is.False);
            Assert.That(fixture.Presenter.IsDealingOpeningHand, Is.False);
            Assert.That(BattleMulliganDesignTestScene.Ghosts(fixture.Presenter), Is.Empty);
            fixture.Presenter.Present(state);
            Assert.That(fixture.Presenter.IsDealingOpeningHand, Is.True);
            fixture.Presenter.ApplyPresentationDesign();
            Assert.That(fixture.Presenter.IsDealingOpeningHand, Is.False, "Forced relayout must settle the hand.");
            Assert.That(fixture.Confirm.interactable, Is.True);
            Assert.That(fixture.Text("StatusText").text, Does.Not.Contain("뽑는 중"), "Canceling a deal must restore selection guidance.");
            fixture.Presenter.Present(null);
            yield return null;
            fixture.Presenter.Present(state);
            Assert.That(fixture.Presenter.IsDealingOpeningHand, Is.True);
            fixture.Presenter.gameObject.SetActive(false);
            yield return null;
            Assert.That(fixture.Presenter.IsDealingOpeningHand, Is.False);
            Assert.That(BattleMulliganDesignTestScene.Ghosts(fixture.Presenter), Is.Empty);
            foreach (var card in fixture.ActiveCards)
                Assert.That(card.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(.001f));
        }

        [UnityTest]
        public IEnumerator ServerTimeoutAfterLocalSelectionKeepsOpeningCardsAndClearsReplacementMarks()
        {
            BattleMulliganDesignTestScene.Load();
            yield return new EnterPlayMode();
            var fixture = BattleMulliganDesignTestScene.Current();
            var state = BattleMulliganDesignTestScene.State();
            var opening = state.Player.Hand.CardIds.ToArray();
            fixture.Presenter.Present(state);
            yield return new WaitForSecondsRealtime(1.2f);
            fixture.Presenter.ToggleCardFromUi(0);
            fixture.Presenter.ToggleCardFromUi(2);
            Assert.That(fixture.Presenter.SelectedCardCount, Is.EqualTo(2));
            AssertCardVisual(fixture.Buttons[0], keep: false, replace: true);
            state.Player.MarkMulliganUsed();
            state.AI.MarkMulliganUsed();
            state.SetPhase(PhaseType.Main);
            var before = BattleMulliganDesignTestScene.TruthSnapshot(state);
            fixture.Presenter.Present(state);
            foreach (var card in fixture.ActiveCards) AssertCardVisual(card, keep: false, replace: false);
            Assert.That((IEnumerable<string>)BattleMulliganDesignTestScene.Get(fixture.Presenter, "_displayCardIds"), Is.EqualTo(opening));
            Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(before));
        }
        internal static void AssertCardVisual(Button card, bool keep, bool replace)
        {
            var aura = card.transform.Find("KeepAura");
            var cross = card.transform.Find("ReplaceCross");
            var label = card.transform.Find("ReplaceLabel");
            Assert.That(aura, Is.Not.Null);
            Assert.That(cross, Is.Not.Null);
            Assert.That(label, Is.Not.Null);
            Assert.That(aura.gameObject.activeSelf, Is.EqualTo(keep), "Keep aura " + card.name);
            Assert.That(cross.gameObject.activeSelf, Is.EqualTo(replace), "Replacement cross " + card.name);
            Assert.That(label.gameObject.activeSelf, Is.EqualTo(replace), "Replacement label " + card.name);
            Assert.That(label.GetComponent<Text>().text, Is.EqualTo("교체"));
            foreach (var image in cross.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "ReplaceMark")
                {
                    Assert.That(image.sprite != null, Is.True, "Use the authored red mark.");
                    Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(image.sprite)), Is.EqualTo("1058e767d60e8fc4e80c70c01b709ebb"), "Compare persistent sprite identity across native import/domain reloads.");
                    Assert.That(image.color, Is.EqualTo(Color.white), "Preserve the red artwork colors.");
                }
                else Assert.That(image.color.r, Is.GreaterThan(image.color.g * 2), "Fallback cross must be red.");
            }
        }
    }

    internal sealed class BattleMulliganDesignTestScene
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        public BattleMulliganOverlayPresenter Presenter;
        public PlayerDeckPresenter Deck;
        public Button[] Buttons => (Button[])Get(Presenter, "_cardButtons");
        public Button Confirm;
        public Button[] ActiveCards => Buttons.Where(c => c != null && c.gameObject.activeSelf).ToArray();

        public static BattleMulliganDesignTestScene Load(bool showMulliganTimer = false)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Battle_VSlice.unity", OpenSceneMode.Single);
            foreach (var bootstrapper in Find<BattleBootstrapper>(scene).ToArray())
            {
                bootstrapper.enabled = false;
                Set(bootstrapper, "_startOnAwake", false);
                Set(bootstrapper, "_startWithDraftBeforeBattle", false);
                Set(bootstrapper, "_autoRunAiTurns", false);
                if (showMulliganTimer)
                {
                    // Apply only the real countdown presentation to its authored UI. No timer coroutine or gateway is started.
                    bootstrapper.GetType().GetMethod("ApplyOnlineTurnTimerValue", Hidden)
                        .Invoke(bootstrapper, new object[] { true, false, true, 20, 33 });
                    ((CanvasGroup)Get(bootstrapper, "_onlineTurnTimerCanvasGroup")).gameObject.SetActive(true);
                }
                Object.DestroyImmediate(bootstrapper);
            }
            foreach (var connection in Find<OnlineBattleConnectionTester>(scene).ToArray())
                Object.DestroyImmediate(connection);
            foreach (var input in Find<BattlePlayerInputController>(scene)) input.enabled = false;
            var fixture = Current(scene);
            fixture.Presenter.Bind(null);
            fixture.Presenter.ApplyPresentationDesign();
            return fixture;
        }

        public static BattleMulliganDesignTestScene Current() => Current(SceneManager.GetActiveScene());
        private static BattleMulliganDesignTestScene Current(Scene scene)
        {
            var presenter = Find<BattleMulliganOverlayPresenter>(scene).Single();
            var deck = Find<PlayerDeckPresenter>(scene).Single(p => !(p is OpponentDeckPresenter));
            return new BattleMulliganDesignTestScene
            {
                Presenter = presenter,
                Deck = deck,
                Confirm = (Button)Get(presenter, "_confirmButton")
            };
        }

        private static IEnumerable<T> Find<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));

        public Text Text(string name) => Presenter.GetComponentsInChildren<Text>(true).Single(t => t.name == name);
        public static Transform[] Ghosts(BattleMulliganOverlayPresenter presenter) =>
            presenter.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("DrawingCard_", StringComparison.Ordinal)).ToArray();

        public static BattleState State(params string[] opening)
        {
            var cards = opening.Length == 0 ? new[] { "Goblin", "SkeletonArcher", "ManaPond", "Goblin", "ManaPond" } : opening;
            // Deck.Draw removes from the end, so reverse makes the desired opening order explicit.
            var deck = new[] { "Goblin", "ManaPond", "SkeletonArcher", "Goblin", "ManaPond", "SkeletonArcher" }
                .Concat(cards.Reverse()).ToArray();
            return new BattleSetupService(new NoOpShuffler()).CreateInitialState(
                new BattleSetupRequest(deck, deck, PlayerId.Player, aiMulliganEnabled: true));
        }

        public static string TruthSnapshot(BattleState state) => string.Join("|",
            state.Phase, state.ActivePlayerId, state.TurnNumber, state.Player.HasUsedMulligan, state.AI.HasUsedMulligan,
            string.Join(",", state.Player.Hand.Cards.Select(c => c.RuntimeId + ":" + c.CardId)),
            string.Join(",", state.Player.Deck.CardIds), string.Join(",", state.AI.Hand.CardIds),
            string.Join(",", state.AI.Deck.CardIds), state.Player.Resources.Gold, state.Player.Resources.Mana,
            state.Player.Resources.Qi, state.Player.Resources.Power, state.CardDrawEvents.Count, state.ResourceChangeEvents.Count);

        public static object Get(object instance, string name) =>
            instance.GetType().GetField(name, Hidden).GetValue(instance);
        public static void Set(object instance, string name, object value) =>
            instance.GetType().GetField(name, Hidden).SetValue(instance, value);

        private sealed class NoOpShuffler : IDeckShuffler
        {
            public void Shuffle(DeckState deck) { }
        }
    }
}