using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    // Actual authored UI and real confirm listeners; passive gateway records intent but never changes truth or sends network.
    public sealed class BattleMulliganReplacementTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (Application.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ConfirmHidesOnlySelectedSlotsBeforeSendingAndDoesNotDrawCards(int count)
        {
            var fixture = BattleMulliganDesignTestScene.Load();
            var state = BattleMulliganDesignTestScene.State("Goblin", "Goblin", "ManaPond");
            var driver = BattleMulliganReplacementTestDriver.Bind(fixture, state);
            fixture.Presenter.Present(state);
            var slots = Slots(count);
            foreach (var slot in slots) fixture.Presenter.ToggleCardFromUi(slot);
            var before = BattleMulliganDesignTestScene.TruthSnapshot(state);
            fixture.Presenter.ConfirmSelectionFromUi();
            Assert.That(driver.Gateway.ConfirmCount, Is.EqualTo(1));
            Assert.That(driver.Gateway.SubmittedCardIds, Is.EqualTo(slots.Select(i => state.Player.Hand.CardIds[i])));
            for (var slot = 0; slot < 3; slot++)
            {
                Assert.That(driver.Gateway.AlphasAtSubmit[slot], Is.EqualTo(slots.Contains(slot) ? 0f : 1f), "Hide before submission: slot " + slot);
                Assert.That(Alpha(fixture, slot), Is.EqualTo(slots.Contains(slot) ? 0f : 1f));
                Assert.That(fixture.Buttons[slot].interactable, Is.False);
            }
            Assert.That(fixture.Presenter.IsDealingReplacementHand, Is.False, "No speculative cards before authoritative response.");
            Assert.That(BattleMulliganDesignTestScene.Ghosts(fixture.Presenter), Is.Empty);
            fixture.Presenter.Present(state);
            Assert.That(driver.Gateway.ConfirmCount, Is.EqualTo(1));
            Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(before));
            Assert.That(driver.Owner.activeInHierarchy, Is.False);
        }

        [Test]
        public void RejectedSubmissionRestoresAllCardsSelectionAndRetryWithoutChangingTruth()
        {
            var fixture = BattleMulliganDesignTestScene.Load();
            var state = BattleMulliganDesignTestScene.State();
            var driver = BattleMulliganReplacementTestDriver.Bind(fixture, state);
            fixture.Presenter.Present(state);
            fixture.Presenter.ToggleCardFromUi(0);
            fixture.Presenter.ToggleCardFromUi(2);
            var before = BattleMulliganDesignTestScene.TruthSnapshot(state);
            BattleMulliganDesignTestScene.Set(driver.Bootstrapper, "_isBusy", true);
            fixture.Presenter.ConfirmSelectionFromUi();
            Assert.That(driver.Gateway.ConfirmCount, Is.Zero, "The bootstrapper rejected submission before dispatch.");
            Assert.That(BattleMulliganDesignTestScene.Get(fixture.Presenter, "_confirmationSent"), Is.False);
            Assert.That(fixture.Presenter.SelectedCardCount, Is.EqualTo(2));
            foreach (var card in fixture.ActiveCards)
            {
                Assert.That(card.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
                Assert.That(card.interactable, Is.True);
            }
            BattleMulliganDesignTests.AssertCardVisual(fixture.Buttons[0], false, true);
            BattleMulliganDesignTests.AssertCardVisual(fixture.Buttons[1], true, false);
            BattleMulliganDesignTests.AssertCardVisual(fixture.Buttons[2], false, true);
            Assert.That(fixture.Confirm.interactable, Is.True);
            Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(before));
            BattleMulliganDesignTestScene.Set(driver.Bootstrapper, "_isBusy", false);
            fixture.Presenter.ConfirmSelectionFromUi();
            Assert.That(driver.Gateway.ConfirmCount, Is.EqualTo(1), "A rejected submit must allow retry.");
            Assert.That(driver.Gateway.AlphasAtSubmit, Is.EqualTo(new[] { 0f, 1f, 0f }));
            Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator ZeroOneTwoThreeReplacementsFlyOnlyToSelectedSlotsAndKeepDuplicateInPlace()
        {
            BattleMulliganDesignTestScene.Load();
            yield return new EnterPlayMode();
            var fixture = BattleMulliganDesignTestScene.Current();
            Time.timeScale = 0f;
            for (var count = 0; count <= 3; count++)
            {
                fixture.Presenter.Present(null);
                yield return null;
                var state = BattleMulliganDesignTestScene.State("Goblin", "Goblin", "ManaPond");
                var driver = BattleMulliganReplacementTestDriver.Bind(fixture, state);
                BattleMulliganDesignTestScene.Set(fixture.Presenter, "_dealOpeningHand", false);
                fixture.Presenter.Present(state);
                var slots = Slots(count);
                var opening = state.Player.Hand.CardIds.ToArray();
                var positions = fixture.Buttons.Take(3).Select(c => ((RectTransform)c.transform).anchoredPosition).ToArray();
                foreach (var slot in slots) fixture.Buttons[slot].onClick.Invoke();
                fixture.Confirm.onClick.Invoke();
                Assert.That(driver.Gateway.ConfirmCount, Is.EqualTo(1), "Real confirm button listener must submit once.");
                var before = BattleMulliganDesignTestScene.TruthSnapshot(state);
                fixture.Presenter.Present(state);
                Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(before));
                for (var slot = 0; slot < 3; slot++) Assert.That(Alpha(fixture, slot), Is.EqualTo(slots.Contains(slot) ? 0f : 1f));
                BattleMulliganReplacementTestDriver.ApplyAuthoritativeReplacement(state, slots);
                var expected = BattleMulliganOverlayPresenter.BuildConfirmedCardLayout(opening, slots, state.Player.Hand.CardIds);
                before = BattleMulliganDesignTestScene.TruthSnapshot(state);
                fixture.Presenter.Present(state);
                Assert.That(fixture.Presenter.IsDealingReplacementHand, Is.EqualTo(count > 0));
                if (count > 0) Assert.That(fixture.Text("StatusText").text, Is.Empty);
                Assert.That(fixture.Presenter.IsDealingOpeningHand, Is.False);
                Assert.That(Display(fixture), Is.EqualTo(expected));
                var ghosts = BattleMulliganDesignTestScene.Ghosts(fixture.Presenter);
                Assert.That(ghosts.Length, Is.EqualTo(count));
                if (count > 0)
                {
                    Assert.That((int[])BattleMulliganDesignTestScene.Get(fixture.Presenter, "_dealSlotIndices"), Is.EqualTo(slots));
                    var source = fixture.Deck.GetComponentsInChildren<Image>(true).Single(i => i.name == "PlayerDeckCardBack_02");
                    var first = ghosts.OrderBy(g => g.name).First();
                    Assert.That(Vector2.Distance(ScreenPoint(source.rectTransform), ScreenPoint((RectTransform)first)), Is.LessThan(2f));
                }
                for (var slot = 0; slot < 3; slot++)
                {
                    Assert.That(((RectTransform)fixture.Buttons[slot].transform).anchoredPosition, Is.EqualTo(positions[slot]), "Kept slots must not reflow.");
                    if (!slots.Contains(slot)) Assert.That(Alpha(fixture, slot), Is.EqualTo(1f));
                    BattleMulliganDesignTests.AssertCardVisual(fixture.Buttons[slot], false, false);
                }
                var ids = ghosts.Select(g => g.GetInstanceID()).ToArray();
                for (var repeat = 0; repeat < 5; repeat++) fixture.Presenter.Present(state);
                Assert.That(BattleMulliganDesignTestScene.Ghosts(fixture.Presenter).Select(g => g.GetInstanceID()), Is.EqualTo(ids), "Repeated authoritative views must not restart replacement.");
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.That(fixture.Presenter.IsDealingReplacementHand, Is.False, "Replacement must finish while timeScale is zero.");
                Assert.That(BattleMulliganDesignTestScene.Ghosts(fixture.Presenter), Is.Empty);
                foreach (var card in fixture.ActiveCards) Assert.That(card.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
                Assert.That(Display(fixture), Is.EqualTo(expected));
                Assert.That(fixture.Text("StatusText").text, Is.EqualTo("상대의 멀리건을 기다리는 중..."));
                fixture.Confirm.onClick.Invoke();
                Assert.That(driver.Gateway.ConfirmCount, Is.EqualTo(1));
                Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(before));
                UnityEngine.Object.Destroy(driver.Owner);
            }
        }

        [UnityTest]
        public IEnumerator MainResponseStartsFinalThreeSecondsOnlyAfterReplacementArrives()
        {
            BattleMulliganDesignTestScene.Load();
            yield return new EnterPlayMode();
            var fixture = BattleMulliganDesignTestScene.Current();
            var state = BattleMulliganDesignTestScene.State();
            BattleMulliganReplacementTestDriver.Bind(fixture, state);
            BattleMulliganDesignTestScene.Set(fixture.Presenter, "_dealOpeningHand", false);
            BattleMulliganDesignTestScene.Set(fixture.Presenter, "_openingCardFlightSeconds", .8f);
            Time.timeScale = 0f;
            fixture.Presenter.Present(state);
            fixture.Buttons[1].onClick.Invoke();
            fixture.Confirm.onClick.Invoke();
            BattleMulliganReplacementTestDriver.ApplyAuthoritativeReplacement(state, new[] { 1 }, main: true);
            var before = BattleMulliganDesignTestScene.TruthSnapshot(state);
            fixture.Presenter.Present(state);
            Assert.That(fixture.Presenter.IsDealingReplacementHand, Is.True);
            Assert.That(fixture.Text("StatusText").text, Is.Empty, "Replacement animation must not show a progress notice.");
            Assert.That(BattleMulliganDesignTestScene.Get(fixture.Presenter, "_finalResultPending"), Is.True);
            Assert.That(BattleMulliganDesignTestScene.Get(fixture.Presenter, "_resultSequence"), Is.Null);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(fixture.Presenter.IsDealingReplacementHand, Is.True);
            Assert.That(fixture.Text("StatusText").text, Is.Empty, "Replacement animation must not show a progress notice.");
            Assert.That(BattleMulliganDesignTestScene.Get(fixture.Presenter, "_isShowingFinalResult"), Is.False);
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(fixture.Presenter.IsDealingReplacementHand, Is.False);
            Assert.That(BattleMulliganDesignTestScene.Get(fixture.Presenter, "_isShowingFinalResult"), Is.True);
            Assert.That(fixture.Confirm.gameObject.activeSelf, Is.False);
            Assert.That(fixture.Text("InstructionText").text, Is.EqualTo("전투 준비 완료"));
            yield return new WaitForSecondsRealtime(2.6f);
            Assert.That(fixture.Presenter.IsSessionActive, Is.True, "Result must remain for three seconds after arrival.");
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(fixture.Presenter.IsSessionActive, Is.False);
            Assert.That(fixture.Presenter.gameObject.activeSelf, Is.False);
            Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator MainArrivingMidflightAndRepeatedViewsPreserveReplacementFlight()
        {
            BattleMulliganDesignTestScene.Load();
            yield return new EnterPlayMode();
            var fixture = BattleMulliganDesignTestScene.Current();
            var state = BattleMulliganDesignTestScene.State();
            BattleMulliganReplacementTestDriver.Bind(fixture, state);
            BattleMulliganDesignTestScene.Set(fixture.Presenter, "_dealOpeningHand", false);
            BattleMulliganDesignTestScene.Set(fixture.Presenter, "_openingCardFlightSeconds", .8f);
            fixture.Presenter.Present(state);
            fixture.Buttons[0].onClick.Invoke(); fixture.Buttons[2].onClick.Invoke();
            fixture.Confirm.onClick.Invoke();
            BattleMulliganReplacementTestDriver.ApplyAuthoritativeReplacement(state, new[] { 0, 2 });
            fixture.Presenter.Present(state);
            var ids = BattleMulliganDesignTestScene.Ghosts(fixture.Presenter).Select(g => g.GetInstanceID()).ToArray();
            yield return new WaitForSecondsRealtime(.2f);
            state.AI.MarkMulliganUsed(); state.SetPhase(PhaseType.Main);
            var before = BattleMulliganDesignTestScene.TruthSnapshot(state);
            for (var repeat = 0; repeat < 5; repeat++) fixture.Presenter.Present(state);
            Assert.That(fixture.Presenter.IsDealingReplacementHand, Is.True);
            Assert.That(fixture.Text("StatusText").text, Is.Empty, "Replacement animation must not show a progress notice.");
            Assert.That(BattleMulliganDesignTestScene.Ghosts(fixture.Presenter).Select(g => g.GetInstanceID()), Is.EqualTo(ids));
            Assert.That(BattleMulliganDesignTestScene.Get(fixture.Presenter, "_resultSequence"), Is.Null);
            Assert.That(BattleMulliganDesignTestScene.Get(fixture.Presenter, "_finalResultPending"), Is.True);
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(fixture.Presenter.IsDealingReplacementHand, Is.False);
            Assert.That(BattleMulliganDesignTestScene.Get(fixture.Presenter, "_isShowingFinalResult"), Is.True);
            Assert.That(BattleMulliganDesignTestScene.Ghosts(fixture.Presenter), Is.Empty);
            Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator ResizeDisableAndNullPresentationCleanFlightAndDoNotReplayConfirmedHand()
        {
            BattleMulliganDesignTestScene.Load();
            yield return new EnterPlayMode();
            var fixture = BattleMulliganDesignTestScene.Current();
            for (var action = 0; action < 3; action++)
            {
                fixture.Presenter.Present(null); yield return null;
                var state = BattleMulliganDesignTestScene.State();
                BattleMulliganReplacementTestDriver.Bind(fixture, state);
                BattleMulliganDesignTestScene.Set(fixture.Presenter, "_dealOpeningHand", false);
                fixture.Presenter.Present(state);
                fixture.Buttons[0].onClick.Invoke(); fixture.Buttons[2].onClick.Invoke();
                fixture.Confirm.onClick.Invoke();
                BattleMulliganReplacementTestDriver.ApplyAuthoritativeReplacement(state, new[] { 0, 2 });
                fixture.Presenter.Present(state);
                Assert.That(fixture.Presenter.IsDealingReplacementHand, Is.True);
            Assert.That(fixture.Text("StatusText").text, Is.Empty, "Replacement animation must not show a progress notice.");
                var before = BattleMulliganDesignTestScene.TruthSnapshot(state);
                if (action == 0) fixture.Presenter.ApplyPresentationDesign();
                else if (action == 1) fixture.Presenter.gameObject.SetActive(false);
                else fixture.Presenter.Present(null);
                yield return null;
                Assert.That(fixture.Presenter.IsDealingReplacementHand, Is.False);
                Assert.That(BattleMulliganDesignTestScene.Ghosts(fixture.Presenter), Is.Empty);
                foreach (var card in fixture.ActiveCards) Assert.That(card.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
                fixture.Presenter.Present(state);
                Assert.That(fixture.Presenter.IsDealingReplacementHand, Is.False, "A snapped/confirmed hand must not replay on a view refresh.");
                Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(before));
            }
        }

        [UnityTest]
        public IEnumerator TimeoutWithoutConfirmKeepsSelectionAndConfirmedReconnectSkipsAllReplacementFlight()
        {
            BattleMulliganDesignTestScene.Load();
            yield return new EnterPlayMode();
            var fixture = BattleMulliganDesignTestScene.Current();
            BattleMulliganDesignTestScene.Set(fixture.Presenter, "_dealOpeningHand", false);
            var state = BattleMulliganDesignTestScene.State();
            fixture.Presenter.Present(state);
            var opening = state.Player.Hand.CardIds.ToArray();
            fixture.Buttons[0].onClick.Invoke(); fixture.Buttons[2].onClick.Invoke();
            state.Player.MarkMulliganUsed(); state.AI.MarkMulliganUsed(); state.SetPhase(PhaseType.Main);
            var before = BattleMulliganDesignTestScene.TruthSnapshot(state);
            fixture.Presenter.Present(state);
            Assert.That(fixture.Presenter.IsDealingReplacementHand, Is.False);
            Assert.That(BattleMulliganDesignTestScene.Ghosts(fixture.Presenter), Is.Empty);
            Assert.That(Display(fixture), Is.EqualTo(opening));
            foreach (var card in fixture.ActiveCards) BattleMulliganDesignTests.AssertCardVisual(card, false, false);
            Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(before));
            fixture.Presenter.Present(null); yield return null;
            state = BattleMulliganDesignTestScene.State();
            BattleMulliganReplacementTestDriver.ApplyAuthoritativeReplacement(state, new[] { 1 });
            before = BattleMulliganDesignTestScene.TruthSnapshot(state);
            fixture.Presenter.Present(state);
            Assert.That(fixture.Presenter.IsDealingOpeningHand, Is.False);
            Assert.That(fixture.Presenter.IsDealingReplacementHand, Is.False);
            Assert.That(BattleMulliganDesignTestScene.Ghosts(fixture.Presenter), Is.Empty);
            Assert.That(Display(fixture), Is.EqualTo(state.Player.Hand.CardIds));
            Assert.That(fixture.Text("StatusText").text, Is.EqualTo("상대의 멀리건을 기다리는 중..."));
            Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(before));
        }

        [Test]
        public void GeneratedReplacementMarkIsNativePointSpriteAndDisplayedWithWhiteTint()
        {
            var sprite = Resources.Load<Sprite>("Project333/UI/MulliganReplaceMark");
            Assert.That(sprite, Is.Not.Null);
            var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(sprite));
            Assert.That(importer.textureType, Is.EqualTo(UnityEditor.TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(UnityEditor.SpriteImportMode.Single));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.textureCompression, Is.EqualTo(UnityEditor.TextureImporterCompression.Uncompressed));
            Assert.That(importer.maxTextureSize, Is.EqualTo(2048));
            Assert.That(sprite.rect.width, Is.EqualTo(1024));
            Assert.That(sprite.rect.height, Is.EqualTo(1536));
            var fixture = BattleMulliganDesignTestScene.Load();
            fixture.Presenter.Present(BattleMulliganDesignTestScene.State());
            fixture.Presenter.ToggleCardFromUi(1);
            var mark = fixture.Buttons[1].transform.Find("ReplaceCross/ReplaceMark").GetComponent<Image>();
            Assert.That(mark.sprite != null, Is.True);
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(mark.sprite)), Is.EqualTo(UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(sprite))));
            Assert.That(mark.color, Is.EqualTo(Color.white));
            Assert.That(mark.preserveAspect, Is.True);
            Assert.That(mark.raycastTarget, Is.False);
            foreach (var fallback in fixture.Buttons[1].transform.Find("ReplaceCross").GetComponentsInChildren<Image>(true).Where(i => i != mark))
                Assert.That(fallback.gameObject.activeSelf, Is.False, "Generated art must replace fallback strokes.");
        }

        [UnityTest]
        public IEnumerator SynchronousSubmitRefreshShowsAuthoritativeFlightForMulliganAndMainResponses()
        {
            BattleMulliganDesignTestScene.Load();
            yield return new EnterPlayMode();
            var fixture = BattleMulliganDesignTestScene.Current();
            for (var main = 0; main < 2; main++)
            {
                fixture.Presenter.Present(null); yield return null;
                var state = BattleMulliganDesignTestScene.State();
                var driver = BattleMulliganReplacementTestDriver.Bind(fixture, state, routePresentation: true);
                BattleMulliganDesignTestScene.Set(fixture.Presenter, "_dealOpeningHand", false);
                fixture.Presenter.Present(state);
                fixture.Buttons[0].onClick.Invoke(); fixture.Buttons[2].onClick.Invoke();
                string authoritativeSnapshot = null;
                driver.Gateway.AuthoritativeResponseAtSubmit = () =>
                {
                    BattleMulliganReplacementTestDriver.ApplyAuthoritativeReplacement(state, new[] { 0, 2 }, main: main == 1);
                    authoritativeSnapshot = BattleMulliganDesignTestScene.TruthSnapshot(state);
                };
                fixture.Confirm.onClick.Invoke(); // Real SubmitPlayerMulligan -> RefreshPresenter -> BattleScreenPresenter.Present.
                Assert.That(driver.Gateway.ConfirmCount, Is.EqualTo(1));
                Assert.That(driver.Gateway.AlphasAtSubmit, Is.EqualTo(new[] { 0f, 1f, 0f }));
                Assert.That(fixture.Presenter.IsDealingReplacementHand, Is.True);
            Assert.That(fixture.Text("StatusText").text, Is.Empty, "Replacement animation must not show a progress notice.");
                Assert.That((int[])BattleMulliganDesignTestScene.Get(fixture.Presenter, "_dealSlotIndices"), Is.EqualTo(new[] { 0, 2 }));
                Assert.That(BattleMulliganDesignTestScene.Get(fixture.Presenter, "_finalResultPending"), Is.EqualTo(main == 1));
                Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(authoritativeSnapshot));
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.That(fixture.Presenter.IsDealingReplacementHand, Is.False);
                Assert.That(BattleMulliganDesignTestScene.Get(fixture.Presenter, "_isShowingFinalResult"), Is.EqualTo(main == 1));
                Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(authoritativeSnapshot));
            }
        }

        [UnityTest]
        public IEnumerator PendingSelectedSlotsRemainEmptyAfterLayoutRefreshDisableAndReenable()
        {
            BattleMulliganDesignTestScene.Load();
            yield return new EnterPlayMode();
            var fixture = BattleMulliganDesignTestScene.Current();
            var state = BattleMulliganDesignTestScene.State();
            BattleMulliganReplacementTestDriver.Bind(fixture, state);
            BattleMulliganDesignTestScene.Set(fixture.Presenter, "_dealOpeningHand", false);
            fixture.Presenter.Present(state);
            fixture.Buttons[0].onClick.Invoke(); fixture.Buttons[2].onClick.Invoke();
            var before = BattleMulliganDesignTestScene.TruthSnapshot(state);
            fixture.Confirm.onClick.Invoke();
            fixture.Presenter.ApplyPresentationDesign();
            Assert.That(new[] { Alpha(fixture, 0), Alpha(fixture, 1), Alpha(fixture, 2) }, Is.EqualTo(new[] { 0f, 1f, 0f }));
            fixture.Presenter.gameObject.SetActive(false);
            yield return null;
            fixture.Presenter.Present(state);
            Assert.That(new[] { Alpha(fixture, 0), Alpha(fixture, 1), Alpha(fixture, 2) }, Is.EqualTo(new[] { 0f, 1f, 0f }));
            Assert.That(fixture.Presenter.IsDealingReplacementHand, Is.False);
            Assert.That(BattleMulliganDesignTestScene.Ghosts(fixture.Presenter), Is.Empty);
            Assert.That(fixture.Confirm.interactable, Is.False);
            Assert.That(BattleMulliganDesignTestScene.TruthSnapshot(state), Is.EqualTo(before));
        }
        private static int[] Slots(int count) => count == 0 ? Array.Empty<int>() : count == 1 ? new[] { 1 } : count == 2 ? new[] { 0, 2 } : new[] { 0, 1, 2 };
        private static float Alpha(BattleMulliganDesignTestScene fixture, int slot) => fixture.Buttons[slot].GetComponent<CanvasGroup>().alpha;
        private static IEnumerable<string> Display(BattleMulliganDesignTestScene fixture) => (IEnumerable<string>)BattleMulliganDesignTestScene.Get(fixture.Presenter, "_displayCardIds");
        private static Vector2 ScreenPoint(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            return RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center));
        }
    }

    internal sealed class BattleMulliganReplacementTestDriver
    {
        public GameObject Owner;
        public BattleBootstrapper Bootstrapper;
        public PassiveMulliganGateway Gateway;

        public static BattleMulliganReplacementTestDriver Bind(BattleMulliganDesignTestScene fixture, BattleState state, bool routePresentation = false)
        {
            var owner = new GameObject("InactiveReadOnlyMulliganDriver");
            owner.SetActive(false);
            var bootstrapper = owner.AddComponent<BattleBootstrapper>();
            bootstrapper.enabled = false;
            var gateway = new PassiveMulliganGateway(state, fixture);
            BattleMulliganDesignTestScene.Set(bootstrapper, "_battleGateway", gateway);
            BattleMulliganDesignTestScene.Set(bootstrapper, "_startOnAwake", false);
            BattleMulliganDesignTestScene.Set(bootstrapper, "_autoRunAiTurns", false);
            BattleMulliganDesignTestScene.Set(bootstrapper, "_sendPlayerActionsToOnlineGateway", false);
            BattleMulliganDesignTestScene.Set(bootstrapper, "_autoResolveTurnStartAfterAutoPass", false);
            BattleMulliganDesignTestScene.Set(bootstrapper, "_cardDefinitionProvider", JsonCardDefinitionDatabase.FromJson(Resources.Load<TextAsset>("Project333/Data/cards").text).CreateProvider());
            if (routePresentation)
            {
                var screen = owner.AddComponent<BattleScreenPresenter>();
                screen.enabled = false;
                BattleMulliganDesignTestScene.Set(screen, "_mulliganOverlayPresenter", fixture.Presenter);
                BattleMulliganDesignTestScene.Set(screen, "_playerDeckPresenter", fixture.Deck);
                BattleMulliganDesignTestScene.Set(bootstrapper, "_battleScreenPresenter", screen);
            }
            fixture.Presenter.Bind(bootstrapper);
            var source = fixture.Deck.GetComponentsInChildren<Image>(true).Single(i => i.name == "PlayerDeckCardBack_02");
            fixture.Presenter.ConfigureDrawSource(source.rectTransform, source.sprite);
            return new BattleMulliganReplacementTestDriver { Owner = owner, Bootstrapper = bootstrapper, Gateway = gateway };
        }

        // Represents a detached authoritative response. The production presenter never performs this mutation.
        public static void ApplyAuthoritativeReplacement(BattleState state, IReadOnlyList<int> slots, bool main = false)
        {
            var opening = state.Player.Hand.Cards.ToArray();
            foreach (var slot in slots) state.Player.Hand.Remove(opening[slot].CardId, opening[slot].RuntimeId);
            var replacements = new[] { "SkeletonArcher", "ManaPond", "Goblin" };
            for (var index = 0; index < slots.Count; index++) state.Player.Hand.Add(replacements[index]);
            state.Player.MarkMulliganUsed();
            if (main)
            {
                state.AI.MarkMulliganUsed();
                state.Player.Hand.Add("Cerberus"); // An unrelated normal turn-start draw must never become a fourth mulligan slot.
                state.SetPhase(PhaseType.Main);
            }
        }

        internal sealed class PassiveMulliganGateway : IBattleGateway
        {
            private readonly BattleMulliganDesignTestScene _fixture;
            public BattleState CurrentBattleState { get; }
            public int ConfirmCount;
            public Action AuthoritativeResponseAtSubmit;
            public string[] SubmittedCardIds = Array.Empty<string>();
            public float[] AlphasAtSubmit = Array.Empty<float>();
            public PassiveMulliganGateway(BattleState state, BattleMulliganDesignTestScene fixture) { CurrentBattleState = state; _fixture = fixture; }
            private void Record(IEnumerable<string> cards)
            {
                ConfirmCount++;
                SubmittedCardIds = cards.ToArray();
                AlphasAtSubmit = _fixture.Buttons.Take(3).Select(c => c.GetComponent<CanvasGroup>().alpha).ToArray();
                AuthoritativeResponseAtSubmit?.Invoke();
            }
            public void ApplyMulligan(PlayerId playerId, IEnumerable<string> cards, IDeckShuffler shuffler) => Record(cards);
            public void PassMulligan(PlayerId playerId) => Record(Array.Empty<string>());
            public BattleState StartBattle(BattleSetupRequest request) => throw new InvalidOperationException("Test must never start a real battle.");
            public void ResolveTurnStart() => throw new InvalidOperationException("Test must never resolve gameplay.");
            public void ExecuteCommand(PlayerId actor, IBattleCommand command) => throw new InvalidOperationException("Test must never execute gameplay.");
            public void EndTurn() => throw new InvalidOperationException("Test must never end a real turn.");
        }
    }
}