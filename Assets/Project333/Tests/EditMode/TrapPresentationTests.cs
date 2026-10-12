using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Cards;
using Project333.Runtime.Presentation.Online;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class TrapPresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTearDown] public IEnumerator ExitAfterFailure() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }

        [TestCase(BattleEventType.TrapInstalled, PlayerId.AI, true)]
        [TestCase(BattleEventType.TrapInstalled, PlayerId.Player, false)]
        [TestCase(BattleEventType.TrapTriggered, PlayerId.AI, true)]
        [TestCase(BattleEventType.TrapTriggered, PlayerId.Player, true)]
        [TestCase(BattleEventType.SpellCast, PlayerId.Player, false)]
        public void RevealRoutingKeepsOwnInstallationPrivateAndTriggerPublic(BattleEventType kind, PlayerId owner, bool expected)
        {
            var entry = new BattleEventDto { EventType = kind, SourceOwnerId = owner,
                CardId = kind == BattleEventType.TrapInstalled ? string.Empty : TrapReturnCardRules.CardId, TrapCount = 1 };
            Assert.That(OpponentPlayedCardRevealRules.ShouldReveal(entry), Is.EqualTo(expected));
            Assert.That(OpponentPlayedCardRevealRules.IsBoardCardPlay(entry), Is.False);
        }

        [Test]
        public void OppositeOnlineSeatRemapsOwnerAndPreservesOnlyPublicCount()
        {
            var root = new GameObject("DormantTrapEventProjection"); root.SetActive(false);
            try
            {
                var tester = root.AddComponent<OnlineBattleConnectionTester>(); Set(tester, "_localPlayerId", PlayerId.AI);
                var result = (List<BattleEventDto>)Call(tester, "ProjectBattleEventsToLocalPerspective", (object)new[]
                {
                    new BattleEventDto { EventType = BattleEventType.TrapInstalled, SourceOwnerId = PlayerId.Player, TargetOwnerId = PlayerId.Player, TrapCount = 3 }
                });
                Assert.That(result[0].SourceOwnerId, Is.EqualTo(PlayerId.AI));
                Assert.That(result[0].TargetOwnerId, Is.EqualTo(PlayerId.AI));
                Assert.That(result[0].TrapCount, Is.EqualTo(3));
                Assert.That(result[0].CardId, Is.Empty);
                Assert.That(result[0].RuntimeId, Is.Empty);
                Assert.That(OpponentPlayedCardRevealRules.ShouldReveal(result[0]), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator GenericInstallationAndActualActivationShareTheNonblockingFifoWithoutDuplicateConsumption()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                Assert.That(Resources.Load<Sprite>("Project333/CardArtwork/TrapHidden"), Is.Not.Null);
                Assert.That(CardArtworkLibrary.TryGetArtwork(TrapReturnCardRules.CardId, out var realArtwork), Is.True);
                var bootstrapper = fixture.Bootstrapper;
                // Malformed hidden payloads must still never enter identity/artwork/stat lookup.
                bootstrapper.QueueOpponentPlayedCardReveals(new[]
                {
                    new BattleEventDto { EventType = BattleEventType.TrapInstalled, SourceOwnerId = PlayerId.AI,
                        CardId = "SecretOriginalCard", SourceCardId = "SecretOriginalCard", CardAttack = 999, CardMaxHp = 999, CardSpellDamage = 999, TrapCount = 1 }
                });
                Assert.That(Field<Image>(bootstrapper, "_opponentPlayedCardRevealImage").sprite,
                    Is.SameAs(Resources.Load<Sprite>("Project333/CardArtwork/TrapHidden")));
                Assert.That(Field<Text>(bootstrapper, "_opponentPlayedCardTrapTitleText").text, Is.EqualTo("트랩"));
                Assert.That(Field<Text>(bootstrapper, "_opponentPlayedCardFallbackText").text, Is.EqualTo("?"));
                Assert.That(Field<Text>(bootstrapper, "_opponentPlayedCardAttackText").gameObject.activeSelf, Is.False);
                Assert.That(Field<Text>(bootstrapper, "_opponentPlayedCardHpText").gameObject.activeSelf, Is.False);
                var group = Field<CanvasGroup>(bootstrapper, "_opponentPlayedCardRevealCanvasGroup");
                Assert.That(group.blocksRaycasts, Is.False); Assert.That(group.interactable, Is.False);
                fixture.State.RecordTrapEvent(new BattleTrapEvent(BattleTrapEventKind.Triggered, PlayerId.Player, 0, TrapReturnCardRules.CardId, "publicly-triggered"));
                Call(bootstrapper, "QueueLocalTrapReveals");
                Assert.That(fixture.State.TrapEvents, Is.Empty);
                var queued = (ICollection)typeof(BattleBootstrapper).GetField("_opponentPlayedCardRevealQueue", Hidden).GetValue(bootstrapper);
                Assert.That(queued.Count, Is.EqualTo(1));
                Call(bootstrapper, "QueueLocalTrapReveals");
                Assert.That(queued.Count, Is.EqualTo(1), "The same transient event must not be replayed by repeated refresh.");
                var deadline = Time.realtimeSinceStartup + 2f;
                while (Field<Image>(bootstrapper, "_opponentPlayedCardRevealImage").sprite != realArtwork && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(Field<Image>(bootstrapper, "_opponentPlayedCardRevealImage").sprite, Is.SameAs(realArtwork));
                Assert.That(Field<Text>(bootstrapper, "_opponentPlayedCardTrapTitleText").gameObject.activeSelf, Is.False);
                Assert.That(group.blocksRaycasts, Is.False);
                // Online state projection restores only counts and must not consume or replay local history.
                fixture.State.RecordTrapEvent(new BattleTrapEvent(BattleTrapEventKind.Installed, PlayerId.AI, 2));
                Set(bootstrapper, "_sendPlayerActionsToOnlineGateway", true);
                Call(bootstrapper, "QueueLocalTrapReveals");
                Assert.That(fixture.State.TrapEvents.Count, Is.EqualTo(1));
                Assert.That(queued.Count, Is.Zero);
                bootstrapper.ClearOpponentPlayedCardReveals();
                Assert.That(group.gameObject.activeSelf, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator MasterMarkerUsesPublicCountAndClearsWhenOccupantChanges()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                fixture.State.AI.RestoreHiddenTrapCount(1); // There are no private opponent trap identities in this state.
                Assert.That(fixture.State.ArmedTraps, Is.Empty);
                var shell = new GameObject("ProjectedMasterTile", typeof(RectTransform)); shell.transform.SetParent(fixture.Root.transform, false);
                shell.SetActive(false);
                var tile = shell.AddComponent<TileView>(); var master = fixture.State.AI.Master;
                tile.Configure(PlayerId.AI, master.Position.Column, master.Position.Row); tile.Present(master);
                var view = shell.AddComponent<TileTextView>(); view.enabled = false;
                var bar = new GameObject("ExistingOccupantStatBar", typeof(RectTransform)); bar.transform.SetParent(shell.transform, false);
                Set(view, "_tileView", tile); Set(view, "_battleBootstrapper", fixture.Bootstrapper);
                Set(view, "_occupantStatRoot", bar.GetComponent<RectTransform>()); Set(view, "_isOccupied", true);
                shell.SetActive(true);
                Call(view, "RefreshTrapStatusMarker");
                var marker = Field<MonoBehaviour>(view, "_trapStatusMarker");
                var question = marker.transform.Find("QuestionGlyph").GetComponent<TMP_Text>();
                var counter = marker.transform.Find("Count").GetComponent<TMP_Text>();
                Assert.That(question.text, Is.EqualTo("?")); Assert.That(question.fontSize, Is.EqualTo(26));
                Assert.That(question.raycastTarget, Is.False);
                Assert.That(counter.gameObject.activeSelf, Is.False);
                fixture.State.AI.RestoreHiddenTrapCount(3); Call(view, "RefreshTrapStatusMarker");
                Assert.That(counter.text, Is.EqualTo("X3")); Assert.That(counter.raycastTarget, Is.False);
                Assert.That(marker.GetComponentsInChildren<Image>(true), Is.Empty, "Master marker is a legible glyph, not the entire portrait shrunk to an icon.");
                fixture.State.AI.RestoreHiddenTrapCount(0); Call(view, "RefreshTrapStatusMarker");
                Assert.That(marker.gameObject.activeSelf, Is.False);
                fixture.State.AI.RestoreHiddenTrapCount(2); Call(view, "RefreshTrapStatusMarker");
                Assert.That(marker.gameObject.activeSelf, Is.True);
                tile.Present(new UnitState("replacement", "UnknownUnit", PlayerId.AI, master.Position,
                    AttackType.Melee, 1, 10, true, false, 0));
                Call(view, "RefreshTrapStatusMarker");
                Assert.That(marker.gameObject.activeSelf, Is.False, "A replacement unit must not inherit the master's trap marker.");
            }
        }

        [UnityTest]
        public IEnumerator OwnMasterInstallIsFreeDuplicateRejectedAndReturnedAttackerSkipsCombatPresentation()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                var bootstrapper = fixture.Bootstrapper; var state = fixture.State;
                state.Player.Hand.Add(TrapReturnCardRules.CardId);
                var goldBefore = state.Player.Resources.Gold;
                Assert.That(bootstrapper.TryUsePlayerCardOnTile(TrapReturnCardRules.CardId, PlayerId.AI, state.AI.Master.Position, out _), Is.False);
                Assert.That(fixture.Gateway.Executions, Is.Zero);
                Assert.That(bootstrapper.TryUsePlayerCardOnTile(TrapReturnCardRules.CardId, PlayerId.Player, state.Player.Master.Position, out var installed), Is.True);
                Assert.That(installed, Is.EqualTo("트랩을 설치했습니다."));
                Assert.That(fixture.Gateway.LastCommand, Is.TypeOf<InstallTrapCommand>());
                Assert.That(state.Player.Resources.Gold, Is.EqualTo(goldBefore));
                state.Player.Hand.Add(TrapReturnCardRules.CardId);
                Assert.That(bootstrapper.TryUsePlayerCardOnTile(TrapReturnCardRules.CardId, PlayerId.Player, state.Player.Master.Position, out var duplicate), Is.False);
                Assert.That(duplicate, Is.EqualTo("이미 설치된 트랩입니다."));
                Assert.That(fixture.Gateway.Executions, Is.EqualTo(1));
                Assert.That(Field<Coroutine>(bootstrapper, "_opponentPlayedCardRevealCoroutine"), Is.Null, "Own installation does not replay a central card.");
                var source = new TileCoord(0, 0);
                var attacker = new UnitState("return-source", "Cerberus", PlayerId.Player, source, AttackType.Ranged, 50, 50, true, false, 0) { HasSummoningSickness = false };
                state.PlayerBoard.Place(source, attacker);
                state.AI.Resources.Add(new ResourceSet(0, 0, 0, 4));
                var trap = TrapReturnCardRules.CreateDefinition();
                state.ArmedTraps.Add(new ArmedTrapState("return-trap", PlayerId.AI, trap.CardId, trap.TriggerType, trap.EffectType, trap.ActivationCost, 1));
                var dormant = new GameObject("DormantBoardPresenter"); dormant.SetActive(false); dormant.transform.SetParent(fixture.Root.transform);
                var screen = dormant.AddComponent<BattleScreenPresenter>(); var board = dormant.AddComponent<BoardPresenter>();
                Set(screen, "_boardPresenter", board); Set(bootstrapper, "_battleScreenPresenter", screen); Set(bootstrapper, "_animateAttackSequences", true);
                var masterHp = state.AI.Master.CurrentHp;
                Assert.That(bootstrapper.TryAttackWithPlayerOccupant(source, state.AI.Master.Position, out var result), Is.True, result);
                Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
                Assert.That(result, Is.EqualTo("트랩이 발동해 공격이 취소되었습니다."));
                Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(masterHp));
                Assert.That(state.PlayerBoard.GetOccupant(source), Is.Null);
                Assert.That(Field<Coroutine>(bootstrapper, "_presentationSequenceCoroutine"), Is.Null, "Accepted trap cancellation must never enter melee/laser/hit/death motion.");
                Assert.That(bootstrapper.IsBusy, Is.False);
                Assert.That(fixture.Root.GetComponentsInChildren<AudioSource>(true).Any(audio => audio.isPlaying), Is.False);
            }
        }

        [Test]
        public void TrapInputTargetsOnlyOwnMasterAtZeroGoldAndSuppressesDuplicateHints()
        {
            using (var fixture = new Fixture())
            {
                var state = fixture.State;
                state.Player.Resources.Spend(state.Player.Resources.Clone());
                Assert.That(state.Player.Resources.CanAfford(TrapReturnCardRules.CreateDefinition().ActivationCost), Is.False);
                state.Player.Hand.Add(TrapReturnCardRules.CardId);
                var input = fixture.Root.AddComponent<BattlePlayerInputController>(); input.enabled = false;
                var board = fixture.Root.AddComponent<BoardPresenter>();
                var ownMaster = InputTile(fixture.Root.transform, PlayerId.Player, state.Player.Master.Position);
                var enemyMaster = InputTile(fixture.Root.transform, PlayerId.AI, state.AI.Master.Position);
                var ownUnit = InputTile(fixture.Root.transform, PlayerId.Player, new TileCoord(0, 0));
                state.PlayerBoard.Place(ownUnit.Coord, new UnitState("ordinary-unit", "Cerberus", PlayerId.Player, ownUnit.Coord, AttackType.Melee, 1, 10, true, false, 0));
                var empty = InputTile(fixture.Root.transform, PlayerId.Player, new TileCoord(2, 0));
                Set(board, "_playerTileViews", new[] { ownMaster, ownUnit, empty }); Set(board, "_aiTileViews", new[] { enemyMaster });
                Set(input, "_boardPresenter", board); Set(input, "_battleBootstrapper", fixture.Bootstrapper);
                Set(input, "_selectedCardId", TrapReturnCardRules.CardId); Set(input, "_selectedSlotIndex", 0);
                input.RebindViews(); input.RefreshHighlights();
                Assert.That(ownMaster.HighlightState, Is.EqualTo(BattleHighlightState.SpellTarget));
                Assert.That(enemyMaster.HighlightState, Is.EqualTo(BattleHighlightState.None));
                Assert.That(ownUnit.HighlightState, Is.EqualTo(BattleHighlightState.None));
                Assert.That(empty.HighlightState, Is.EqualTo(BattleHighlightState.None));
                Assert.That(Call(input, "CanCardCastWithoutTarget", state, TrapReturnCardRules.CardId), Is.False);
                Assert.That(Call(input, "CanSelectedCardTargetOccupant", state, state.Player.Master), Is.True);
                ownMaster.NotifyClicked();
                Assert.That(fixture.Gateway.LastCommand, Is.TypeOf<InstallTrapCommand>(), "Actual master click must install, rather than selecting the master as a unit.");
                Assert.That(input.SelectedCardId, Is.Empty);
                Assert.That(state.Player.Resources.Gold, Is.Zero);
                state.Player.Hand.Add(TrapReturnCardRules.CardId);
                Set(input, "_selectedCardId", TrapReturnCardRules.CardId); Set(input, "_selectedSlotIndex", 0);
                input.RefreshHighlights();
                Assert.That(ownMaster.HighlightState, Is.EqualTo(BattleHighlightState.None));
                Assert.That(Call(input, "CanDragCardInCurrentState", state, TrapReturnCardRules.CardId), Is.False);
                Assert.That(Call(input, "CanSelectedCardTargetOccupant", state, state.Player.Master), Is.False);
                Assert.That(Call(input, "BuildCardDragFailureMessage", state, TrapReturnCardRules.CardId, false), Is.EqualTo("이미 설치된 트랩입니다."));
            }
        }

        [Test]
        public void TrapInputKeepsInspectionSelectionButClearsTargetHintsOutsideMainPhase()
        {
            using (var fixture = new Fixture())
            {
                var state = fixture.State; state.Player.Hand.Add(TrapReturnCardRules.CardId);
                var input = fixture.Root.AddComponent<BattlePlayerInputController>(); input.enabled = false;
                var board = fixture.Root.AddComponent<BoardPresenter>();
                var master = InputTile(fixture.Root.transform, PlayerId.Player, state.Player.Master.Position);
                Set(board, "_playerTileViews", new[] { master }); Set(board, "_aiTileViews", Array.Empty<TileView>());
                Set(input, "_boardPresenter", board); Set(input, "_battleBootstrapper", fixture.Bootstrapper);
                Set(input, "_selectedCardId", TrapReturnCardRules.CardId); Set(input, "_selectedSlotIndex", 0);
                state.SetPhase(PhaseType.TurnEnd);
                input.RefreshHighlights();
                Assert.That(input.SelectedCardId, Is.EqualTo(TrapReturnCardRules.CardId));
                Assert.That(master.HighlightState, Is.EqualTo(BattleHighlightState.None));
                Assert.That(Call(input, "CanCardUseOnTile", state, TrapReturnCardRules.CardId, master), Is.False);
                Assert.That(Call(input, "CanDragCardInCurrentState", state, TrapReturnCardRules.CardId), Is.False);
            }
        }

        [Test]
        public void TrapKeywordDescribesFreeInstallAndConditionalPrivateOneShotActivation()
        {
            var tooltip = OccupantSpecialEffectTooltipCatalog.Build(TrapReturnCardRules.CreateDefinition()).Single(entry => entry.Title == "트랩");
            Assert.That(tooltip.Description, Does.Contain("무료로 설치").And.Contain("발동 비용").And.Contain("한 번").And.Contain("비용이 부족").And.Contain("상대에게 종류가 공개되지").And.Contain("같은 트랩"));
        }

        [UnityTest]
        public IEnumerator MirrorCancellationShowsOnlyAttackerHurtAndActualDeath()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            foreach (var hp in new[] { 100, 10 })
            using (var fixture = new Fixture())
            {
                var state = fixture.State;
                var source = new TileCoord(0, 0);
                var attacker = new UnitState("mirror-source-" + hp, "A-301", PlayerId.Player, source,
                    AttackType.Ranged, 30, hp, true, false, 0) { HasSummoningSickness = false };
                state.PlayerBoard.Place(source, attacker);
                var protectedCoord = new TileCoord(1, 0);
                var protectedUnit = new UnitState("mirror-protected", "A-301", PlayerId.AI, protectedCoord,
                    AttackType.Ranged, 10, 100, true, false, 0);
                state.AIBoard.Place(protectedCoord, protectedUnit);
                var trap = TrapMirrorFieldCardRules.CreateDefinition();
                state.AI.Resources.Add(new ResourceSet(2, 0, 0, 0));
                state.ArmedTraps.Add(new ArmedTrapState("mirror-armed-" + hp, PlayerId.AI, trap.CardId,
                    trap.TriggerType, trap.EffectType, trap.ActivationCost, 1));
                var sourceView = SetupMirrorBoard(fixture, source);
                var sourceAnimator = Field<Animator>(sourceView, "_occupantVisualAnimator");
                Assert.That(sourceAnimator.runtimeAnimatorController, Is.Not.Null);
                Set(fixture.Bootstrapper, "_valuePopupCascadeDelay", .005f);
                Assert.That(fixture.Bootstrapper.TryAttackWithPlayerOccupant(source, protectedCoord, out var message), Is.True, message);
                Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
                Assert.That(protectedUnit.CurrentHp, Is.EqualTo(100));
                Assert.That(state.ValuePopupEvents.Single().SourceCardId, Is.EqualTo(TrapMirrorFieldCardRules.CardId));
                Assert.That(fixture.Bootstrapper.IsBusy, Is.True, "Real reflection feedback is kept, while the original attack motion is canceled.");
                var deadline = Time.unscaledTime + 3f;
                var sawHurt = false; var sawDeath = false; var sawAttack = false;
                while (fixture.Bootstrapper.IsBusy && Time.unscaledTime < deadline)
                {
                    var current = sourceAnimator.GetCurrentAnimatorStateInfo(0);
                    sawHurt |= current.IsName("BeAttacked");
                    sawDeath |= current.IsName("Death");
                    sawAttack |= current.IsName("Attack");
                    yield return null;
                }
                Assert.That(fixture.Bootstrapper.IsBusy, Is.False);
                Assert.That(sawHurt, Is.True, "Reflection damage must play the common hurt reaction.");
                Assert.That(sawDeath, Is.EqualTo(hp == 10));
                Assert.That(sawAttack, Is.False, "The canceled laser/normal attack must never be played.");
                Assert.That(state.Player.Hand.CardIds, Does.Not.Contain("A-301"));
                Assert.That(state.AI.Discard.CardIds, Is.EqualTo(new[] { TrapMirrorFieldCardRules.CardId }));
                Assert.That(hp == 10 ? state.PlayerBoard.GetOccupant(source) == null : attacker.CurrentHp == 70, Is.True);
            }
            yield return new ExitPlayMode();
        }

        [Test]
        public void MirrorOnlineFeedbackUsesRecordedTargetIdentityAndNoAttackSequence()
        {
            using (var fixture = new Fixture())
            {
                var coord = new TileCoord(0, 0);
                var events = new[]
                {
                    new BattleEventDto { EventType = BattleEventType.TrapTriggered, CardId = TrapMirrorFieldCardRules.CardId,
                        SourceOwnerId = PlayerId.AI, TargetOwnerId = PlayerId.Player, TargetCoord = TileCoordDto.FromDomain(coord),
                        TargetRuntimeId = "online-mirror-source", TrapCount = 0 },
                    new BattleEventDto { EventType = BattleEventType.DamageApplied, SourceCardId = TrapMirrorFieldCardRules.CardId,
                        SourceOwnerId = PlayerId.AI, TargetOwnerId = PlayerId.Player, TargetCoord = TileCoordDto.FromDomain(coord),
                        RuntimeId = "online-mirror-source", TargetRuntimeId = "online-mirror-source", Amount = 30, HpBefore = 10, HpAfter = 0, DamageType = DamageType.Magic },
                    new BattleEventDto { EventType = BattleEventType.OccupantRemoved, TargetOwnerId = PlayerId.Player,
                        TargetCoord = TileCoordDto.FromDomain(coord), TargetRuntimeId = "online-mirror-source" }
                };
                var request = Call(fixture.Bootstrapper, "BuildOnlineImpactAnimationRequest", TrapMirrorFieldCardRules.CardId, events[0], events);
                Assert.That((bool)request.GetType().GetProperty("GuardAreaRuntimeIds").GetValue(request), Is.True);
                var impacts = (System.Collections.IList)request.GetType().GetProperty("Impacts").GetValue(request);
                Assert.That(impacts.Count, Is.EqualTo(1));
                Assert.That((float)Call(fixture.Bootstrapper, "EstimateOnlineBattleEventAnimationGroupDuration", (object)events), Is.GreaterThan(0f));
                Assert.That(events.Any(e => e.EventType == BattleEventType.AttackStarted), Is.False);
                Assert.That(OpponentPlayedCardRevealRules.ShouldReveal(events[0]), Is.True);
            }
        }

        [TestCase("A-212", true)]
        [TestCase(DemonKingRules.CardId, false)]
        public void ReactiveArmourLocalCancellationUsesMembershipForDeathAndNeverInventsAHurtCue(string cardId, bool destroyed)
        {
            using (var fixture = new Fixture())
            {
                var state = fixture.State;
                var attacker = new UnitState("reactive-local-" + cardId, cardId, PlayerId.Player, new TileCoord(0, 0),
                    AttackType.Melee, 30, 50, true, false, 0) { HasSummoningSickness = false };
                attacker.WasSummonedThisTurn = false; attacker.RemainingAttacksThisTurn = 1;
                state.PlayerBoard.Place(attacker.Position, attacker);
                ArmReactive(fixture);
                var before = typeof(BattleBootstrapper).GetMethod("CaptureBattleSnapshot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { state });
                fixture.Gateway.ExecuteCommand(PlayerId.Player, new AttackCommand(attacker.Position, state.AI.Master.Position));
                var request = Call(fixture.Bootstrapper, "BuildCancelledTrapImpact", before);
                Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
                Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(333));
                Assert.That(state.ValuePopupEvents, Is.Empty);
                Assert.That(Property<bool>(request, "GuardAreaRuntimeIds"), Is.True);
                var impacts = Property<IList>(request, "Impacts");
                Assert.That(impacts.Count, Is.EqualTo(destroyed ? 1 : 0));
                if (destroyed)
                {
                    Assert.That(Property<bool>(impacts[0], "TookDamage"), Is.False);
                    Assert.That(Property<bool>(impacts[0], "Died"), Is.True);
                    Assert.That(Property<IDictionary>(request, "AreaImpactRuntimeIds")[impacts[0]], Is.EqualTo(attacker.RuntimeId));
                    Assert.That(attacker.CurrentHp, Is.EqualTo(50), "Direct destruction does not invent an HP damage packet.");
                }
                else
                {
                    Assert.That(state.PlayerBoard.GetOccupant(attacker.Position), Is.SameAs(attacker));
                    Assert.That(attacker.IsDemonKingRevivalPending && attacker.IsSealbound, Is.True);
                }
                Assert.That(OpponentPlayedCardRevealRules.ShouldReveal(new BattleEventDto
                    { EventType = BattleEventType.TrapTriggered, CardId = ExplosiveReactiveArmourCardRules.CardId, SourceOwnerId = PlayerId.AI }), Is.True);
            }
        }

        [Test]
        public void ReactiveArmourOnlineRemovalMergesOnceAndKeepsOnlyRealChainDamage()
        {
            using (var fixture = new Fixture())
            {
                var source = TileCoordDto.FromDomain(new TileCoord(0, 0));
                var chain = TileCoordDto.FromDomain(new TileCoord(1, 0));
                var reveal = new BattleEventDto { EventType = BattleEventType.TrapTriggered,
                    CardId = ExplosiveReactiveArmourCardRules.CardId, SourceCardId = ExplosiveReactiveArmourCardRules.CardId,
                    SourceOwnerId = PlayerId.AI, TargetOwnerId = PlayerId.Player, TargetCoord = source,
                    TargetRuntimeId = "reactive-online-attacker", TrapCount = 0 };
                var removal = new BattleEventDto { EventType = BattleEventType.OccupantRemoved,
                    TargetOwnerId = PlayerId.Player, TargetCoord = source, TargetRuntimeId = reveal.TargetRuntimeId };
                var events = new[] { reveal, removal, removal,
                    new BattleEventDto { EventType = BattleEventType.DamageApplied, SourceCardId = NuclearPowerPlantRules.CardId,
                        SourceRuntimeId = "death-effect-source", SourceOwnerId = PlayerId.Player,
                        TargetOwnerId = PlayerId.AI, TargetCoord = chain, TargetRuntimeId = "chain-damaged-unit",
                        RuntimeId = "chain-damaged-unit", Amount = 5, HpBefore = 5, HpAfter = 0, DamageType = DamageType.Physical },
                    new BattleEventDto { EventType = BattleEventType.OccupantRemoved, TargetOwnerId = PlayerId.AI,
                        TargetCoord = chain, TargetRuntimeId = "chain-damaged-unit" } };
                var request = Call(fixture.Bootstrapper, "BuildOnlineImpactAnimationRequest",
                    ExplosiveReactiveArmourCardRules.CardId, reveal, events);
                Assert.That(Property<bool>(request, "GuardAreaRuntimeIds"), Is.True);
                Assert.That(Property<string>(request, "SpellEffectId"), Is.Null.Or.Empty);
                var impacts = Property<IList>(request, "Impacts"); var identity = Property<IDictionary>(request, "AreaImpactRuntimeIds");
                Assert.That(impacts.Count, Is.EqualTo(2), "Each removed runtime has one death reaction.");
                foreach (var impact in impacts)
                {
                    Assert.That(Property<bool>(impact, "Died"), Is.True);
                    Assert.That(Property<bool>(impact, "TookDamage"), Is.EqualTo((string)identity[impact] == "chain-damaged-unit"));
                }
                Assert.That(Property<IList>(request, "ValuePopupEvents").Count, Is.EqualTo(1));
                Assert.That((float)Call(fixture.Bootstrapper, "EstimateOnlineBattleEventAnimationGroupDuration", (object)events), Is.GreaterThan(0f));
                Assert.That(events.Any(e => e.EventType == BattleEventType.AttackStarted), Is.False);
                var noDamage = new[] { reveal, removal };
                request = Call(fixture.Bootstrapper, "BuildOnlineImpactAnimationRequest", ExplosiveReactiveArmourCardRules.CardId, reveal, noDamage);
                Assert.That(Property<IList>(request, "ValuePopupEvents"), Is.Empty);
                Assert.That(Property<bool>(Property<IList>(request, "Impacts")[0], "TookDamage"), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ReactiveArmourCanceledAttackPlaysDeathWithoutAttackOrHurt()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                var state = fixture.State; var source = new TileCoord(0, 0);
                var handBefore = state.Player.Hand.Count;
                var attacker = new UnitState("reactive-animated-source", "A-212", PlayerId.Player, source,
                    AttackType.Melee, 30, 50, true, false, 0) { HasSummoningSickness = false };
                attacker.WasSummonedThisTurn = false; attacker.RemainingAttacksThisTurn = 1;
                state.PlayerBoard.Place(source, attacker); ArmReactive(fixture);
                var sourceView = SetupMirrorBoard(fixture, source);
                var animator = Field<Animator>(sourceView, "_occupantVisualAnimator");
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
                Assert.That(fixture.Bootstrapper.TryAttackWithPlayerOccupant(source, state.AI.Master.Position, out var message), Is.True, message);
                Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
                Assert.That(state.ValuePopupEvents, Is.Empty); Assert.That(fixture.Bootstrapper.IsBusy, Is.True);
                var sawDeath = false; var sawHurt = false; var sawAttack = false;
                var deadline = Time.unscaledTime + 3f;
                while (fixture.Bootstrapper.IsBusy && Time.unscaledTime < deadline)
                {
                    var current = animator.GetCurrentAnimatorStateInfo(0);
                    sawDeath |= current.IsName("Death"); sawHurt |= current.IsName("BeAttacked"); sawAttack |= current.IsName("Attack");
                    yield return null;
                }
                Assert.That(fixture.Bootstrapper.IsBusy, Is.False);
                Assert.That(sawDeath, Is.True); Assert.That(sawHurt || sawAttack, Is.False);
                Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(333));
                Assert.That(state.PlayerBoard.GetOccupant(source), Is.Null);
                Assert.That(state.Player.Hand.Count, Is.EqualTo(handBefore), "Direct destruction must not add a returned card to the existing hand.");
                Assert.That(state.AI.Discard.CardIds, Is.EqualTo(new[] { ExplosiveReactiveArmourCardRules.CardId }));
            }
            yield return new ExitPlayMode();
        }

        [TestCase(PlayerId.Player, "A-301", true)]
        [TestCase(PlayerId.AI, "A-301", true)]
        [TestCase(PlayerId.Player, DemonKingRules.CardId, false)]
        [TestCase(PlayerId.AI, DemonKingRules.CardId, false)]
        public void IronDomeLocalCancellationRetainsActualDeathAndRevivalWithoutHurtOrArmourCue(
            PlayerId trapOwner, string cardId, bool destroyed)
        {
            using (var fixture = new Fixture())
            {
                var state = fixture.State;
                var attackerOwner = trapOwner == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
                var attacker = new UnitState("iron-local-" + trapOwner + cardId, cardId, attackerOwner,
                    new TileCoord(0, 0), AttackType.Ranged, 30, 50, true, false, 0)
                    { WasSummonedThisTurn = false, HasSummoningSickness = false, RemainingAttacksThisTurn = 1 };
                state.GetBoard(attackerOwner).Place(attacker.Position, attacker);
                state.SetActivePlayer(attackerOwner);
                var master = state.GetPlayer(trapOwner).Master;
                state.GetBoard(trapOwner).Move(master.Position, new TileCoord(3, 0));
                ArmIronDome(fixture, trapOwner);
                var before = typeof(BattleBootstrapper).GetMethod("CaptureBattleSnapshot", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { state });
                fixture.Gateway.ExecuteCommand(attackerOwner, new AttackCommand(attacker.Position, state.GetPlayer(trapOwner).Master.Position));
                var request = Call(fixture.Bootstrapper, "BuildCancelledTrapImpact", before);
                Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
                Assert.That(state.GetPlayer(trapOwner).Master.CurrentHp, Is.EqualTo(333));
                Assert.That(state.ValuePopupEvents, Is.Empty);
                Assert.That(request, Is.Not.Null);
                Assert.That(Property<bool>(request, "GuardAreaRuntimeIds"), Is.True);
                Assert.That(Property<object>(request, "ReactiveArmourCue"), Is.Null, "Iron Dome must not borrow the reactive armour barrier/explosion.");
                var cue = Property<object>(request, "IronDomeCue");
                AssertIronDomeCue(cue, trapOwner, master, attacker);
                Assert.That((float)Call(fixture.Bootstrapper, "EstimateImpactAnimationDuration", request),
                    Is.GreaterThanOrEqualTo(IronDomeMissileVfx.DurationSeconds));
                Assert.That(Property<string>(request, "SpellEffectId"), Is.Null.Or.Empty);
                var impacts = Property<IList>(request, "Impacts");
                Assert.That(impacts.Count, Is.EqualTo(destroyed ? 1 : 0));
                if (destroyed)
                {
                    Assert.That(Property<bool>(impacts[0], "TookDamage"), Is.False);
                    Assert.That(Property<bool>(impacts[0], "Died"), Is.True);
                    Assert.That(Property<IDictionary>(request, "AreaImpactRuntimeIds")[impacts[0]], Is.EqualTo(attacker.RuntimeId));
                    Assert.That(attacker.CurrentHp, Is.EqualTo(50), "Destruction has no invented HP damage.");
                    Assert.That(state.GetBoard(attackerOwner).GetOccupant(attacker.Position), Is.Null);
                }
                else
                {
                    Assert.That(state.GetBoard(attackerOwner).GetOccupant(attacker.Position), Is.SameAs(attacker));
                    Assert.That(attacker.IsDemonKingRevivalPending && attacker.IsSealbound, Is.True);
                }
            }
        }

        [TestCase(PlayerId.Player, PlayerId.Player)]
        [TestCase(PlayerId.Player, PlayerId.AI)]
        [TestCase(PlayerId.AI, PlayerId.Player)]
        [TestCase(PlayerId.AI, PlayerId.AI)]
        public void IronDomeOnlineConfirmedRemovalProjectsBothSeatsAndKeepsOnlyRealChainDamage(PlayerId localSeat, PlayerId serverTrapOwner)
        {
            using (var fixture = new Fixture())
            {
                var serverAttackerOwner = serverTrapOwner == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
                var localAttackerOwner = serverAttackerOwner == localSeat ? PlayerId.Player : PlayerId.AI;
                var localTrapOwner = localAttackerOwner == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
                var host = new GameObject("DormantIronDomeProjection"); host.SetActive(false); host.transform.SetParent(fixture.Root.transform);
                var connection = host.AddComponent<OnlineBattleConnectionTester>(); Set(connection, "_localPlayerId", localSeat);
                var source = TileCoordDto.FromDomain(new TileCoord(0, 0));
                var chain = TileCoordDto.FromDomain(new TileCoord(1, 0));
                var attacker = new UnitState("iron-online-attacker", "A-301", localAttackerOwner, source.ToDomain(),
                    AttackType.Ranged, 30, 50, true, false, 0);
                fixture.State.GetBoard(localAttackerOwner).Place(attacker.Position, attacker);
                var reveal = new BattleEventDto { EventType = BattleEventType.TrapTriggered, CardId = IronDomeCardRules.CardId,
                    SourceCardId = IronDomeCardRules.CardId, SourceOwnerId = serverTrapOwner, TargetOwnerId = serverAttackerOwner,
                    TargetCoord = source, TargetRuntimeId = "iron-online-attacker", TrapCount = 0 };
                var removal = new BattleEventDto { EventType = BattleEventType.OccupantRemoved,
                    TargetOwnerId = serverAttackerOwner, TargetCoord = source, TargetRuntimeId = reveal.TargetRuntimeId };
                var original = new[] { reveal, removal, removal,
                    new BattleEventDto { EventType = BattleEventType.DamageApplied, SourceCardId = NuclearPowerPlantRules.CardId,
                        SourceRuntimeId = "death-effect-source", SourceOwnerId = serverAttackerOwner,
                        TargetOwnerId = serverTrapOwner, TargetCoord = chain, TargetRuntimeId = "iron-chain-damaged",
                        RuntimeId = "iron-chain-damaged", Amount = 5, HpBefore = 5, HpAfter = 0, DamageType = DamageType.Physical },
                    new BattleEventDto { EventType = BattleEventType.OccupantRemoved, TargetOwnerId = serverTrapOwner,
                        TargetCoord = chain, TargetRuntimeId = "iron-chain-damaged" } };
                var projected = (IReadOnlyList<BattleEventDto>)Call(connection, "ProjectBattleEventsToLocalPerspective", (object)original);
                var request = Call(fixture.Bootstrapper, "BuildOnlineImpactAnimationRequest", IronDomeCardRules.CardId, projected[0], projected);
                Assert.That(Property<bool>(request, "GuardAreaRuntimeIds"), Is.True);
                Assert.That(Property<object>(request, "ReactiveArmourCue"), Is.Null);
                AssertIronDomeCue(Property<object>(request, "IronDomeCue"), localTrapOwner,
                    fixture.State.GetPlayer(localTrapOwner).Master, attacker);
                Assert.That(Property<string>(request, "SpellEffectId"), Is.Null.Or.Empty);
                var impacts = Property<IList>(request, "Impacts"); var identities = Property<IDictionary>(request, "AreaImpactRuntimeIds");
                Assert.That(impacts.Count, Is.EqualTo(2), "Repeated removal events retain a single death for the exact runtime.");
                foreach (var impact in impacts)
                {
                    var isChain = (string)identities[impact] == "iron-chain-damaged";
                    Assert.That(Property<bool>(impact, "Died"), Is.True);
                    Assert.That(Property<bool>(impact, "TookDamage"), Is.EqualTo(isChain));
                    Assert.That(Property<PlayerId>(impact, "OwnerId"), Is.EqualTo(isChain ? localTrapOwner : localAttackerOwner));
                }
                Assert.That(Property<IList>(request, "ValuePopupEvents").Count, Is.EqualTo(1));
                Assert.That(projected[0].SourceOwnerId, Is.EqualTo(localTrapOwner));
                Assert.That(OpponentPlayedCardRevealRules.ShouldReveal(projected[0]), Is.True);
                Assert.That(projected.Any(entry => entry.EventType == BattleEventType.AttackStarted), Is.False);
                Assert.That((float)Call(fixture.Bootstrapper, "EstimateOnlineBattleEventAnimationGroupDuration", (object)projected), Is.GreaterThan(0f));
                var groups = (IList)typeof(BattleBootstrapper).GetMethod("BuildOnlineAnimationGroups", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { projected });
                Assert.That(groups.Count, Is.EqualTo(1)); Assert.That(((IList)groups[0]).Count, Is.EqualTo(projected.Count));
                var revivalOnly = new[] { projected[0] };
                var revival = Call(fixture.Bootstrapper, "BuildOnlineImpactAnimationRequest", IronDomeCardRules.CardId, projected[0], revivalOnly);
                Assert.That(Property<IList>(revival, "Impacts"), Is.Empty, "A confirmed revival without removal must not invent permanent death.");
                Assert.That(Property<IList>(revival, "ValuePopupEvents"), Is.Empty);
                Assert.That(Property<object>(revival, "ReactiveArmourCue"), Is.Null);
                AssertIronDomeCue(Property<object>(revival, "IronDomeCue"), localTrapOwner,
                    fixture.State.GetPlayer(localTrapOwner).Master, attacker);
                Assert.That((float)Call(fixture.Bootstrapper, "EstimateOnlineBattleEventAnimationGroupDuration", (object)revivalOnly),
                    Is.GreaterThanOrEqualTo(IronDomeMissileVfx.DurationSeconds), "A confirmed revival still plays the missile and blast.");
                Assert.That(fixture.Bootstrapper.PlayOnlineBattleEventAnimations(Array.Empty<BattleEventDto>()), Is.Zero, "Restored state must not replay activation.");
            }
        }

        [UnityTest]
        public IEnumerator IronDomeOnlineDeathRejectsAReplacementOccupantAtTheSameTile()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                var source = new TileCoord(0, 0);
                var attacker = new UnitState("iron-old-visual", "A-301", PlayerId.Player, source, AttackType.Ranged, 30, 50, true, false, 0);
                fixture.State.PlayerBoard.Place(source, attacker);
                var view = SetupMirrorBoard(fixture, source);
                yield return null;
                Assert.That(view.isActiveAndEnabled, Is.True);
                Assert.That(view.VisualRuntimeId, Is.EqualTo(attacker.RuntimeId), "The identity check requires a real bound runtime visual.");
                var reveal = new BattleEventDto { EventType = BattleEventType.TrapTriggered, CardId = IronDomeCardRules.CardId,
                    SourceOwnerId = PlayerId.AI, TargetOwnerId = PlayerId.Player, TargetCoord = TileCoordDto.FromDomain(source),
                    TargetRuntimeId = attacker.RuntimeId, TrapCount = 0 };
                var removal = new BattleEventDto { EventType = BattleEventType.OccupantRemoved, TargetOwnerId = PlayerId.Player,
                    TargetCoord = reveal.TargetCoord, TargetRuntimeId = attacker.RuntimeId };
                var request = Call(fixture.Bootstrapper, "BuildOnlineImpactAnimationRequest", IronDomeCardRules.CardId, reveal, new[] { reveal, removal });
                var impact = Property<IList>(request, "Impacts")[0];
                var matches = typeof(BattleBootstrapper).GetMethod("MatchesAreaImpactIdentity", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(matches.Invoke(null, new[] { request, impact, view }), Is.True);
                var tile = Field<TileView>(view, "_tileView");
                var replacement = new UnitState("iron-new-visual", "A-301", PlayerId.Player, source, AttackType.Ranged, 30, 50, true, false, 0);
                tile.Present(replacement); view.SetTile(tile.Title, tile.Content, true);
                Assert.That(view.VisualRuntimeId, Is.EqualTo(replacement.RuntimeId));
                Assert.That(matches.Invoke(null, new[] { request, impact, view }), Is.False, "The old confirmed death must not animate a new unit.");
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator IronDomeCanceledRangedAttackFliesFromEitherMasterThenExplodesBeforeActualDeath()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            foreach (var trapOwner in new[] { PlayerId.Player, PlayerId.AI })
            using (var fixture = new Fixture())
            {
                var state = fixture.State; var source = new TileCoord(0, 0); var attackerOwner = Opposite(trapOwner);
                state.SetActivePlayer(attackerOwner);
                var handBefore = state.GetPlayer(attackerOwner).Hand.Count;
                var attacker = new UnitState("iron-animated-source-" + trapOwner, "A-301", attackerOwner, source,
                    AttackType.Ranged, 30, 50, true, false, 0)
                    { WasSummonedThisTurn = false, HasSummoningSickness = false, RemainingAttacksThisTurn = 1 };
                state.GetBoard(attackerOwner).Place(source, attacker); ArmIronDome(fixture, trapOwner);
                var sourceView = SetupMirrorBoard(fixture, source, attackerOwner);
                var animator = Field<Animator>(sourceView, "_occupantVisualAnimator");
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
                yield return null;
                var started = Time.unscaledTime;
                if (attackerOwner == PlayerId.Player)
                    Assert.That(fixture.Bootstrapper.TryAttackWithPlayerOccupant(source, state.AI.Master.Position, out var message), Is.True, message);
                else
                {
                    var before = Capture(state);
                    fixture.Gateway.ExecuteCommand(attackerOwner, new AttackCommand(source, state.GetPlayer(trapOwner).Master.Position));
                    var request = Call(fixture.Bootstrapper, "BuildCancelledTrapImpact", before);
                    Call(fixture.Bootstrapper, "StartPresentationSequence",
                        (IEnumerator)Call(fixture.Bootstrapper, "PlayerSpellImpactSequence", request));
                }
                Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
                Assert.That(state.ValuePopupEvents, Is.Empty); Assert.That(fixture.Bootstrapper.IsBusy, Is.True);
                var effect = Field<IronDomeMissileVfx>(fixture.Bootstrapper, "_ironDomeMissileVfx");
                Assert.That(effect, Is.Not.Null); Assert.That(Property<bool>(effect, "IsPlaying"), Is.True);
                Assert.That(Property<bool>(effect, "HasImpacted"), Is.False);
                Assert.That(Property<bool>(sourceView, "IsDeathPresentation"), Is.False, "Keep the confirmed ranged body during the launch.");
                var sawDeath = false; var sawHurt = false; var sawAttack = false; var sawBlast = false; var sawMissile = false; var sawImpact = false;
                var deadline = Time.unscaledTime + 4f;
                while (fixture.Bootstrapper.IsBusy && Time.unscaledTime < deadline)
                {
                    var impacted = Property<bool>(effect, "HasImpacted");
                    // Normal completion clears the effect's current flags before the parent sequence finishes.
                    sawImpact |= impacted;
                    sawMissile |= fixture.Root.GetComponentsInChildren<Image>(true).Any(image => image.isActiveAndEnabled &&
                        image.sprite != null && UnityEditor.AssetDatabase.GetAssetPath(image.sprite).Contains("/Vfx/IronDome/"));
                    sawBlast |= fixture.Root.GetComponentsInChildren<Image>(true).Any(image => image.isActiveAndEnabled &&
                        image.sprite != null && UnityEditor.AssetDatabase.GetAssetPath(image.sprite).Contains("/TimedBomb/Explosion"));
                    var current = animator.GetCurrentAnimatorStateInfo(0);
                    sawDeath |= current.IsName("Death"); sawHurt |= current.IsName("BeAttacked"); sawAttack |= current.IsName("Attack");
                    if (!sawImpact) Assert.That(Property<bool>(sourceView, "IsDeathPresentation"), Is.False);
                    if (current.IsName("Death"))
                    {
                        Assert.That(sawImpact && sawBlast, Is.True, "Actual Death follows the missile explosion, never the launch.");
                        Assert.That(Time.unscaledTime - started, Is.GreaterThanOrEqualTo(IronDomeMissileVfx.ImpactDelaySeconds - .04f));
                    }
                    Assert.That(Field<ReactiveArmourVfx>(fixture.Bootstrapper, "_reactiveArmourVfx"), Is.Null);
                    yield return null;
                }
                Assert.That(fixture.Bootstrapper.IsBusy, Is.False);
                Assert.That(sawMissile && sawBlast && sawDeath, Is.True); Assert.That(sawHurt || sawAttack, Is.False);
                Assert.That(Property<bool>(effect, "IsPlaying"), Is.False);
                Assert.That(state.GetPlayer(trapOwner).Master.CurrentHp, Is.EqualTo(333));
                Assert.That(state.GetBoard(attackerOwner).GetOccupant(source), Is.Null);
                Assert.That(state.GetPlayer(attackerOwner).Hand.Count, Is.EqualTo(handBefore));
                Assert.That(state.GetPlayer(trapOwner).Discard.CardIds, Is.EqualTo(new[] { IronDomeCardRules.CardId }));
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator IronDomeOnlineQueueKeepsFollowingStateDeferredThroughMissileExplosionAndDeath()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                var attacker = AddIronAttacker(fixture, PlayerId.Player);
                var view = SetupMirrorBoard(fixture, attacker.Position);
                var connection = CreateDormantConnection(fixture); yield return null;
                var reveal = IronTrigger(PlayerId.AI, attacker);
                var removal = new BattleEventDto { EventType = BattleEventType.OccupantRemoved, TargetOwnerId = attacker.OwnerId,
                    TargetCoord = reveal.TargetCoord, TargetRuntimeId = attacker.RuntimeId };
                var postResolution = new BattleStateViewFactory().CreateForPlayer(fixture.State, "iron-animation-only", PlayerId.Player);
                postResolution.Occupants.RemoveAll(entry => entry.RuntimeId == attacker.RuntimeId);
                Call(connection, "QueuePresentationEnvelope", new OnlineBattleEnvelope
                    { MessageType = OnlineBattleMessageType.BattleEvents, BattleEvents = new List<BattleEventDto> { reveal, removal } });
                Call(connection, "QueuePresentationEnvelope", new OnlineBattleEnvelope
                    { MessageType = OnlineBattleMessageType.StateView, StateView = postResolution });
                Assert.That(fixture.Bootstrapper.IsIronDomeOnlinePresentationPending && fixture.Bootstrapper.IsBusy, Is.True);
                Assert.That(connection.LatestStateView, Is.Null);
                Assert.That(Property<bool>(view, "IsDeathPresentation"), Is.False);
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(connection.LatestStateView, Is.Null, "A following authoritative view must not erase the body in flight.");
                var effect = Field<IronDomeMissileVfx>(fixture.Bootstrapper, "_ironDomeMissileVfx");
                var sawDeath = false; var deadline = Time.unscaledTime + 4f;
                while (fixture.Bootstrapper.IsIronDomeOnlinePresentationPending && Time.unscaledTime < deadline)
                {
                    Assert.That(connection.LatestStateView, Is.Null);
                    if (Property<bool>(effect, "HasImpacted")) sawDeath |= Property<bool>(view, "IsDeathPresentation");
                    yield return null;
                }
                while (connection.LatestStateView == null && Time.unscaledTime < deadline) yield return null;
                Assert.That(sawDeath, Is.True); Assert.That(fixture.Bootstrapper.IsIronDomeOnlinePresentationPending, Is.False);
                Assert.That(connection.LatestStateView, Is.SameAs(postResolution));
                Assert.That(connection.CurrentProjectedBattleState.PlayerBoard.GetOccupant(attacker.Position), Is.Null);
                Assert.That(fixture.State.PlayerBoard.GetOccupant(attacker.Position), Is.SameAs(attacker), "Presentation must not mutate gateway truth.");
                Assert.That(fixture.State.ValuePopupEvents, Is.Empty);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator IronDomeOnlineTrapOnlyRevivalStillExplodesButNeverInventsPermanentDeath()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                var attacker = AddIronAttacker(fixture, PlayerId.AI, DemonKingRules.CardId);
                var view = SetupMirrorBoard(fixture, attacker.Position, PlayerId.AI); yield return null;
                Assert.That(fixture.Bootstrapper.PlayOnlineBattleEventAnimations(new[] { IronTrigger(PlayerId.Player, attacker) }),
                    Is.GreaterThanOrEqualTo(IronDomeMissileVfx.DurationSeconds));
                var effect = Field<IronDomeMissileVfx>(fixture.Bootstrapper, "_ironDomeMissileVfx");
                var sawImpact = false; var deadline = Time.unscaledTime + 4f;
                while (fixture.Bootstrapper.IsIronDomeOnlinePresentationPending && Time.unscaledTime < deadline)
                {
                    sawImpact |= Property<bool>(effect, "HasImpacted");
                    Assert.That(Property<bool>(view, "IsDeathPresentation"), Is.False);
                    yield return null;
                }
                Assert.That(sawImpact, Is.True); Assert.That(fixture.Bootstrapper.IsBusy, Is.False);
                Assert.That(fixture.State.AIBoard.GetOccupant(attacker.Position), Is.SameAs(attacker));
                Assert.That(fixture.State.ValuePopupEvents, Is.Empty);
                Assert.That(fixture.Bootstrapper.PlayOnlineBattleEventAnimations(Array.Empty<BattleEventDto>()), Is.Zero);
                Assert.That(Property<bool>(effect, "IsPlaying"), Is.False, "Restoring state alone does not replay the missile.");
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator IronDomeCanceledOnlineFlightCannotKillAReplacementOrResumeAfterReconnect()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            foreach (var replaceOccupant in new[] { true, false })
            using (var fixture = new Fixture())
            {
                var attacker = AddIronAttacker(fixture, PlayerId.Player);
                var view = SetupMirrorBoard(fixture, attacker.Position); yield return null;
                var reveal = IronTrigger(PlayerId.AI, attacker);
                var removal = new BattleEventDto { EventType = BattleEventType.OccupantRemoved, TargetOwnerId = attacker.OwnerId,
                    TargetCoord = reveal.TargetCoord, TargetRuntimeId = attacker.RuntimeId };
                fixture.Bootstrapper.PlayOnlineBattleEventAnimations(new[] { reveal, removal });
                var effect = Field<IronDomeMissileVfx>(fixture.Bootstrapper, "_ironDomeMissileVfx");
                yield return new WaitForSecondsRealtime(.1f);
                if (replaceOccupant)
                {
                    var tile = Field<TileView>(view, "_tileView");
                    tile.Present(new UnitState("replacement-after-iron-launch", "A-301", PlayerId.Player, attacker.Position,
                        AttackType.Ranged, 30, 50, true, false, 0));
                    view.SetTile(tile.Title, tile.Content, true);
                }
                else fixture.Bootstrapper.CancelCombatPresentation();
                var deadline = Time.unscaledTime + 3f;
                while (fixture.Bootstrapper.IsIronDomeOnlinePresentationPending && Time.unscaledTime < deadline)
                {
                    Assert.That(Property<bool>(view, "IsDeathPresentation"), Is.False);
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(IronDomeMissileVfx.ImpactDelaySeconds + .05f);
                Assert.That(fixture.Bootstrapper.IsIronDomeOnlinePresentationPending, Is.False);
                Assert.That(Property<bool>(effect, "IsPlaying"), Is.False);
                Assert.That(Property<bool>(view, "IsDeathPresentation"), Is.False);
                Assert.That(fixture.Bootstrapper.PlayOnlineBattleEventAnimations(Array.Empty<BattleEventDto>()), Is.Zero);
                Assert.That(fixture.State.PlayerBoard.GetOccupant(attacker.Position), Is.SameAs(attacker));
            }
            yield return new ExitPlayMode();
        }

        private static UnitState AddIronAttacker(Fixture fixture, PlayerId owner, string cardId = "A-301")
        {
            var attacker = new UnitState("iron-flight-" + owner, cardId, owner, new TileCoord(0, 0),
                AttackType.Ranged, 30, 50, true, false, 0)
                { WasSummonedThisTurn = false, HasSummoningSickness = false, RemainingAttacksThisTurn = 1 };
            fixture.State.GetBoard(owner).Place(attacker.Position, attacker); return attacker;
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-the-confirmed-attacker")]
        public void IronDomeOnlineMissileRequiresTheConfirmedAttackerIdentity(string runtimeId)
        {
            using (var fixture = new Fixture())
            {
                var attacker = AddIronAttacker(fixture, PlayerId.Player);
                var entry = IronTrigger(PlayerId.AI, attacker); entry.TargetRuntimeId = runtimeId;
                var request = Call(fixture.Bootstrapper, "BuildOnlineImpactAnimationRequest", IronDomeCardRules.CardId,
                    entry, new[] { entry });
                Assert.That(Property<object>(request, "IronDomeCue"), Is.Null);
                Assert.That(Property<IList>(request, "Impacts"), Is.Empty);
                Assert.That((float)Call(fixture.Bootstrapper, "EstimateImpactAnimationDuration", request), Is.Zero);
            }
        }

        [Test]
        public void IronDomeInstallationAndStateRestorationNeverLaunchAMissile()
        {
            using (var fixture = new Fixture())
            {
                var attacker = AddIronAttacker(fixture, PlayerId.Player);
                var install = IronTrigger(PlayerId.AI, attacker); install.EventType = BattleEventType.TrapInstalled;
                var request = Call(fixture.Bootstrapper, "BuildOnlineImpactAnimationRequest", IronDomeCardRules.CardId,
                    install, new[] { install });
                Assert.That(Property<object>(request, "IronDomeCue"), Is.Null);
                Assert.That((float)Call(fixture.Bootstrapper, "EstimateImpactAnimationDuration", request), Is.Zero);
                ArmIronDome(fixture, PlayerId.AI);
                Assert.That(fixture.Bootstrapper.PlayOnlineBattleEventAnimations(Array.Empty<BattleEventDto>()), Is.Zero);
                Assert.That(Field<IronDomeMissileVfx>(fixture.Bootstrapper, "_ironDomeMissileVfx"), Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator SummonTrapLocalNewBodyReceivesRealHurtAndDeathWithoutMutatingTruth()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            foreach (var hp in new[] { 10, 50 })
            using (var fixture = new Fixture())
            {
                var coord = new TileCoord(0, 0);
                var view = SetupMirrorBoard(fixture, coord);
                var before = Capture(fixture.State);
                var goldBefore = fixture.State.Player.Resources.Gold;
                var unit = new UnitState("new-summon-" + hp, "Goblin", PlayerId.Player, coord, AttackType.Melee, 10, hp, true, false, 0);
                fixture.State.PlayerBoard.Place(coord, unit);
                fixture.State.RecordSummonedOccupant(unit);
                ArmSummon(fixture, PlayerId.AI);
                Assert.That(TrapService.TryResolveAcceptedUnitSummon(fixture.State, unit), Is.True);
                Assert.That(fixture.State.LastSummonedOccupant.CurrentHp, Is.EqualTo(hp));
                var request = Call(fixture.Bootstrapper, "BuildLocalSummonTrapImpact", before);
                var impacts = Property<IList>(request, "Impacts");
                Assert.That(impacts.Count, Is.EqualTo(1));
                Assert.That(Property<bool>(impacts[0], "TookDamage"), Is.True);
                Assert.That(Property<bool>(impacts[0], "Died"), Is.EqualTo(hp == 10));
                var sequence = (IEnumerator)Call(fixture.Bootstrapper, "PlayImpactAnimationSequence", request);
                fixture.Bootstrapper.StartCoroutine(sequence);
                Assert.That(view.VisualRuntimeId, Is.EqualTo(unit.RuntimeId));
                Assert.That(fixture.State.Player.Resources.Gold, Is.EqualTo(goldBefore));
                Assert.That(unit.CurrentHp, Is.EqualTo(hp - 20));
                Assert.That(fixture.State.ValuePopupEvents.Single().Amount, Is.EqualTo(Math.Min(hp, 20)));
                if (hp == 10) Assert.That(fixture.State.PlayerBoard.GetOccupant(coord), Is.Null);
                var deadline = Time.unscaledTime + 3f; var sawDeath = false;
                while (Time.unscaledTime < deadline)
                {
                    sawDeath |= Property<bool>(view, "IsDeathPresentation");
                    if (sawDeath || hp == 50) break;
                    yield return null;
                }
                Assert.That(sawDeath, Is.EqualTo(hp == 10));
                fixture.Bootstrapper.CancelCombatPresentation();
                Assert.That(view.VisualRuntimeId, Is.EqualTo(hp == 10 ? string.Empty : unit.RuntimeId));
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SummonTrapOnlineDeadNewBodyWaitsForDeathInBothPerspectivesAndCancelsCleanly()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            using (var fixture = new Fixture())
            {
                var coord = new TileCoord(0, 0); var view = SetupMirrorBoard(fixture, coord, owner); yield return null;
                var entries = SummonEntries(owner, coord);
                Assert.That(fixture.Bootstrapper.PlayOnlineBattleEventAnimations(entries), Is.GreaterThan(0f));
                Assert.That(view.VisualRuntimeId, Is.EqualTo("new-online-summon"));
                Assert.That(fixture.Bootstrapper.IsSummonTrapOnlinePresentationPending, Is.True);
                Assert.That(fixture.State.GetBoard(owner).GetOccupant(coord), Is.Null, "Cosmetic bodies never enter battle truth.");
                var sawDeath = false; var deadline = Time.unscaledTime + 4f;
                while (fixture.Bootstrapper.IsSummonTrapOnlinePresentationPending && Time.unscaledTime < deadline)
                {
                    sawDeath |= Property<bool>(view, "IsDeathPresentation");
                    yield return null;
                }
                Assert.That(sawDeath, Is.True); Assert.That(fixture.Bootstrapper.IsSummonTrapOnlinePresentationPending, Is.False);
                fixture.Bootstrapper.CancelCombatPresentation();
                Assert.That(view.VisualRuntimeId, Is.Empty);
                Assert.That(fixture.Bootstrapper.PlayOnlineBattleEventAnimations(Array.Empty<BattleEventDto>()), Is.Zero);
                Assert.That(view.VisualRuntimeId, Is.Empty, "Restoring only state must not replay a consumed trap.");
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SummonTrapCannotDamageAReplacedVisualAndServerAiFinalStateCannotEraseTheNewBodyEarly()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                var coord = new TileCoord(0, 0); var view = SetupMirrorBoard(fixture, coord, PlayerId.AI); yield return null;
                var replacement = new UnitState("newer-authoritative-unit", "Goblin", PlayerId.AI, coord, AttackType.Melee, 10, 40, true, false, 0);
                fixture.State.AIBoard.Place(coord, replacement);
                var tile = view.GetComponent<TileView>(); tile.Present(replacement); view.SetTile(tile.Title, tile.Content, true);
                fixture.Bootstrapper.PlayOnlineBattleEventAnimations(SummonEntries(PlayerId.AI, coord));
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(view.VisualRuntimeId, Is.EqualTo(replacement.RuntimeId));
                Assert.That(Property<bool>(view, "IsDeathPresentation"), Is.False);
                Assert.That(replacement.CurrentHp, Is.EqualTo(40));
                var connection = CreateDormantConnection(fixture); Set(connection, "_joinUseServerAiOpponent", true);
                var envelope = new OnlineBattleEnvelope { MessageType = OnlineBattleMessageType.BattleEvents, BattleEvents = SummonEntries(PlayerId.AI, coord).ToList() };
                Assert.That((bool)Call(connection, "ShouldPresentFollowingStateBeforeServerAiCardReveal", envelope), Is.False);
                envelope.BattleEvents.RemoveAll(e => e.EventType != BattleEventType.CardPlayed);
                Assert.That((bool)Call(connection, "ShouldPresentFollowingStateBeforeServerAiCardReveal", envelope), Is.True);
                fixture.Bootstrapper.CancelCombatPresentation();
            }
            yield return new ExitPlayMode();
        }

        [Test]
        public void SummonTrapLocalVisualPreservesGoldBasedAttackWithoutChangingAuthoritativeBaseAttack()
        {
            using (var fixture = new Fixture())
            {
                var coord = new TileCoord(0, 0);
                var before = Capture(fixture.State);
                var unit = new UnitState("summoned-gold-chief", GaebangChiefRules.CardId, PlayerId.Player,
                    coord, AttackType.Melee, 10, 60, true, false, 0);
                fixture.State.PlayerBoard.Place(coord, unit);
                var expectedAttack = unit.Attack;
                Assert.That(expectedAttack, Is.GreaterThan(unit.BaseAttack));
                fixture.State.RecordSummonedOccupant(unit);
                ArmSummon(fixture, PlayerId.AI);
                Assert.That(TrapService.TryResolveAcceptedUnitSummon(fixture.State, unit), Is.True);
                var request = Call(fixture.Bootstrapper, "BuildLocalSummonTrapImpact", before);
                var visual = Property<OccupantState>(request, "SummonedVisual");
                Assert.That(visual.Attack, Is.EqualTo(expectedAttack));
                Assert.That(fixture.State.LastSummonedAttack, Is.EqualTo(expectedAttack));
                Assert.That(unit.BaseAttack, Is.EqualTo(10));
                Assert.That(unit.Attack, Is.EqualTo(expectedAttack));
                Assert.That(visual, Is.Not.SameAs(unit));
                Assert.That(visual, Is.Not.SameAs(fixture.State.LastSummonedOccupant));
                Assert.That(unit.CurrentHp, Is.EqualTo(40));
            }
        }
        [TestCase(0)]
        [TestCase(2)]
        public void SummonTrapOnlineVisualKeepsCapturedAttackAndNativeInitialProtection(int sealTurns)
        {
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            using (var fixture = new Fixture())
            {
                var definition = new UnitCardDefinition("summon-initial-flags", "fixture", new ResourceSet(),
                    AttackType.Ranged, 17, 50, true, false, 0, maxAttacksPerTurn: 2, hitsPerAttack: 2,
                    hasBerserker: true, hasEndure: true, hasShielder: true, hasLifeSteal: true,
                    hasRush: true, sealboundOwnerTurnStarts: sealTurns, hasHiding: true, hasFlying: true,
                    spellPower: 3, invincibleDuration: InvincibleDurationType.Always, hasPiercing: true);
                Set(fixture.Bootstrapper, "_cardDefinitionProvider", new InMemoryCardDefinitionProvider(new CardDefinition[] { definition }));
                var coord = new TileCoord(0, 0);
                var trigger = new BattleEventDto { EventType = BattleEventType.TrapTriggered, CardId = SummonTrapCardRules.CardId,
                    SourceOwnerId = Opposite(owner), TargetOwnerId = owner, TargetCoord = TileCoordDto.FromDomain(coord),
                    TargetRuntimeId = "new-special-summon" };
                var events = new[]
                {
                    new BattleEventDto { EventType = BattleEventType.CardPlayed, CardId = definition.CardId,
                        SourceOwnerId = owner, RuntimeId = trigger.TargetRuntimeId, CardAttack = 29, CardMaxHp = 55 },
                    trigger,
                    new BattleEventDto { EventType = BattleEventType.InvinciblePrevented,
                        TargetRuntimeId = trigger.TargetRuntimeId, HpBefore = 55, HpAfter = 55 }
                };
                var visual = (OccupantState)Call(fixture.Bootstrapper, "CreateOnlineSummonTrapVisual", trigger, events);
                Assert.That(visual.Attack, Is.EqualTo(29));
                Assert.That(visual.MaxHp, Is.EqualTo(55)); Assert.That(visual.CurrentHp, Is.EqualTo(55));
                Assert.That(visual.OwnerId, Is.EqualTo(owner)); Assert.That(visual.RuntimeId, Is.EqualTo(trigger.TargetRuntimeId));
                Assert.That(visual.HasHiding && visual.HasFlying && visual.HasRush && visual.HasBerserker &&
                    visual.HasEndure && visual.HasShielder && visual.HasLifeSteal && visual.HasPiercing, Is.True);
                Assert.That(visual.SpellPower, Is.EqualTo(3)); Assert.That(visual.HitsPerAttack, Is.EqualTo(2));
                Assert.That(visual.IsSealbound, Is.EqualTo(sealTurns > 0));
                Assert.That(visual.SealboundOwnerTurnStartsRemaining, Is.EqualTo(sealTurns));
                Assert.That(visual.InvincibleEffects.Count, Is.EqualTo(1));
                Assert.That(visual.IsInvincible, Is.EqualTo(sealTurns == 0));
                Assert.That(visual.IsHiding, Is.EqualTo(sealTurns == 0));
                Assert.That(visual.WasSummonedThisTurn, Is.True);
                Assert.That(visual.HasSummoningSickness, Is.EqualTo(sealTurns > 0));
                Assert.That(fixture.State.GetBoard(owner).GetOccupant(coord), Is.Null,
                    "The temporary view must never place a unit into online battle truth.");
            }
        }
        private static void ArmSummon(Fixture fixture, PlayerId owner)
        {
            var definition = SummonTrapCardRules.CreateDefinition();
            fixture.State.GetPlayer(owner).Resources.Add(definition.ActivationCost);
            fixture.State.ArmedTraps.Add(new ArmedTrapState("summon-private", owner, definition.CardId,
                definition.TriggerType, definition.EffectType, definition.ActivationCost, 1, effectAmount: 20));
        }

        private static BattleEventDto[] SummonEntries(PlayerId owner, TileCoord coord) => new[]
        {
            new BattleEventDto { EventType = BattleEventType.CardPlayed, SourceOwnerId = owner, TargetOwnerId = owner,
                TargetCoord = TileCoordDto.FromDomain(coord), RuntimeId = "new-online-summon", CardId = "Goblin", CardAttack = 10, CardMaxHp = 10 },
            new BattleEventDto { EventType = BattleEventType.TrapTriggered, SourceOwnerId = Opposite(owner), CardId = SummonTrapCardRules.CardId,
                TargetOwnerId = owner, TargetCoord = TileCoordDto.FromDomain(coord), TargetRuntimeId = "new-online-summon", TargetCardId = "Goblin", CardSpellDamage = 20 },
            new BattleEventDto { EventType = BattleEventType.DamageApplied, SourceOwnerId = Opposite(owner), SourceCardId = SummonTrapCardRules.CardId,
                TargetOwnerId = owner, TargetCoord = TileCoordDto.FromDomain(coord), TargetRuntimeId = "new-online-summon", Amount = 10, HpBefore = 10, HpAfter = 0, DamageType = DamageType.Physical },
            new BattleEventDto { EventType = BattleEventType.OccupantRemoved, TargetOwnerId = owner,
                TargetCoord = TileCoordDto.FromDomain(coord), TargetRuntimeId = "new-online-summon" }
        };

        private static BattleEventDto IronTrigger(PlayerId trapOwner, UnitState attacker) => new BattleEventDto
        {
            EventType = BattleEventType.TrapTriggered, CardId = IronDomeCardRules.CardId, SourceCardId = IronDomeCardRules.CardId,
            SourceOwnerId = trapOwner, TargetOwnerId = attacker.OwnerId, TargetCoord = TileCoordDto.FromDomain(attacker.Position),
            TargetRuntimeId = attacker.RuntimeId, TargetCardId = attacker.CardId, TrapCount = 0
        };

        private static OnlineBattleConnectionTester CreateDormantConnection(Fixture fixture)
        {
            var host = new GameObject("IronAnimationQueueWithoutTransport"); host.SetActive(false); host.transform.SetParent(fixture.Root.transform);
            var connection = host.AddComponent<OnlineBattleConnectionTester>();
            Set(connection, "_connectOnStart", false); Set(connection, "_battleBootstrapper", fixture.Bootstrapper);
            Set(connection, "_joinUseServerAiOpponent", false); Set(connection, "_localPlayerId", PlayerId.Player);
            host.SetActive(true); connection.enabled = false; return connection;
        }

        private static void AssertIronDomeCue(object cue, PlayerId trapOwner, UnitState master, UnitState attacker)
        {
            Assert.That(cue, Is.Not.Null);
            Assert.That(Property<PlayerId>(cue, "MasterOwnerId"), Is.EqualTo(trapOwner));
            Assert.That(Property<TileCoord>(cue, "MasterCoord"), Is.EqualTo(master.Position));
            Assert.That(Property<string>(cue, "MasterRuntimeId"), Is.EqualTo(master.RuntimeId));
            Assert.That(Property<PlayerId>(cue, "AttackerOwnerId"), Is.EqualTo(attacker.OwnerId));
            Assert.That(Property<TileCoord>(cue, "AttackerCoord"), Is.EqualTo(attacker.Position));
            Assert.That(Property<string>(cue, "AttackerRuntimeId"), Is.EqualTo(attacker.RuntimeId));
        }

        private static PlayerId Opposite(PlayerId owner) => owner == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
        private static object Capture(BattleState state) => typeof(BattleBootstrapper)
            .GetMethod("CaptureBattleSnapshot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { state });

        private static void ArmIronDome(Fixture fixture, PlayerId owner)
        {
            var definition = IronDomeCardRules.CreateDefinition();
            fixture.State.GetPlayer(owner).Resources.Add(definition.ActivationCost);
            fixture.State.ArmedTraps.Add(new ArmedTrapState("iron-private", owner, definition.CardId,
                definition.TriggerType, definition.EffectType, definition.ActivationCost, 1));
        }


        private static void ArmReactive(Fixture fixture)
        {
            var definition = ExplosiveReactiveArmourCardRules.CreateDefinition();
            fixture.State.AI.Resources.Add(new ResourceSet(0, 0, 4, 0));
            fixture.State.ArmedTraps.Add(new ArmedTrapState("reactive-private", PlayerId.AI, definition.CardId,
                definition.TriggerType, definition.EffectType, definition.ActivationCost, 1));
        }

        private static T Property<T>(object owner, string name) => (T)owner.GetType()
            .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(owner);

        private static TileTextView SetupMirrorBoard(Fixture fixture, TileCoord source, PlayerId sourceOwner = PlayerId.Player)
        {
            var canvas = new GameObject("MirrorFeedbackCanvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(fixture.Root.transform, false); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(2340, 1080);
            var boardHost = new GameObject("MirrorBoard"); boardHost.transform.SetParent(fixture.Root.transform);
            var board = boardHost.AddComponent<BoardPresenter>(); board.enabled = false; Set(board, "_useResponsiveLayout", false);
            var screenHost = new GameObject("DormantMirrorScreen"); screenHost.SetActive(false); screenHost.transform.SetParent(fixture.Root.transform);
            var screen = screenHost.AddComponent<BattleScreenPresenter>(); Set(screen, "_boardPresenter", board);
            Set(fixture.Bootstrapper, "_battleScreenPresenter", screen); Set(fixture.Bootstrapper, "_animateAttackSequences", true);
            Set(fixture.Bootstrapper, "_cardCatalogAsset", UnityEditor.AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(
                "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset"));
            TileTextView sourceView = null;
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            {
                var tiles = new TileView[10];
                for (var index = 0; index < tiles.Length; index++)
                {
                    var host = new GameObject("MirrorTile" + owner + index, typeof(RectTransform), typeof(Image));
                    host.SetActive(false); host.transform.SetParent(canvas.transform, false);
                    var rect = (RectTransform)host.transform; rect.sizeDelta = new Vector2(160, 160);
                    rect.anchoredPosition = new Vector2((owner == PlayerId.Player ? -900 : 100) + (index % 5) * 180, (index / 5) * 180 - 90);
                    var tile = host.AddComponent<TileView>(); tile.Configure(owner, index % 5, index / 5); tiles[index] = tile;
                    var background = new GameObject("TileBackground", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                    background.SetParent(host.transform, false); background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one;
                    background.offsetMin = background.offsetMax = Vector2.zero;
                    var label = new GameObject("TileLabel", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                    label.transform.SetParent(host.transform, false);
                    var view = host.AddComponent<TileTextView>(); Set(view, "_tileView", tile); Set(view, "_text", label);
                    Set(view, "_battleBootstrapper", fixture.Bootstrapper);
                    tile.Present(fixture.State.GetBoard(owner).GetOccupant(tile.Coord));
                    host.SetActive(true); view.SetTile(tile.Title, tile.Content, true); view.PlayIdleAnimation();
                    if (owner == sourceOwner && tile.Coord == source) sourceView = view;
                }
                Set(board, owner == PlayerId.Player ? "_playerTileViews" : "_aiTileViews", tiles);
            }
            return sourceView;
        }

        private static TileView InputTile(Transform parent, PlayerId owner, TileCoord coord)
        {
            var root = new GameObject("InputTile" + owner + coord); root.transform.SetParent(parent, false);
            var tile = root.AddComponent<TileView>(); tile.Configure(owner, coord.Column, coord.Row); return tile;
        }
        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root;
            internal readonly BattleBootstrapper Bootstrapper;
            internal readonly BattleState State;
            internal readonly Gateway Gateway;
            private readonly string _originalSession;
            internal Fixture()
            {
                _originalSession = AccountSessionState.SessionToken;
                SetSession("trap-ui-fixture-no-requests");
                var deck = Enumerable.Range(0, 12).Select(i => "fixture-" + i).ToArray();
                State = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.Player));
                State.SetPhase(PhaseType.Main);
                Root = new GameObject("TrapUiFixture"); Root.SetActive(false);
                Bootstrapper = Root.AddComponent<BattleBootstrapper>(); Bootstrapper.enabled = false;
                Set(Bootstrapper, "_startOnAwake", false); Set(Bootstrapper, "_startWithDraftBeforeBattle", false);
                Set(Bootstrapper, "_autoRunAiTurns", false);
                Set(Bootstrapper, "_opponentPlayedCardNormalDisplaySeconds", .18f);
                Set(Bootstrapper, "_opponentPlayedCardQueuedDisplaySeconds", .12f);
                Set(Bootstrapper, "_opponentPlayedCardFadeInSeconds", .02f);
                Set(Bootstrapper, "_opponentPlayedCardFadeOutSeconds", .02f);
                Root.SetActive(true); // No authentication request, draft/start call, remote tester or server is used.
                var provider = new InMemoryCardDefinitionProvider(new CardDefinition[] { TrapReturnCardRules.CreateDefinition(), TrapMirrorFieldCardRules.CreateDefinition(), ExplosiveReactiveArmourCardRules.CreateDefinition(), IronDomeCardRules.CreateDefinition(), SummonTrapCardRules.CreateDefinition(), new UnitCardDefinition("Goblin", "고블린", new ResourceSet(), AttackType.Melee, 10, 10, true, false, 0) });
                Gateway = new Gateway(State, provider);
                Set(Bootstrapper, "_cardDefinitionProvider", provider); Set(Bootstrapper, "_battleGateway", Gateway);
                foreach (var name in new[] { "_opponentPlayedCardRevealCanvasGroup", "_yourTurnBannerCanvasGroup" })
                {
                    var field = typeof(BattleBootstrapper).GetField(name, Hidden);
                    if (field?.GetValue(Bootstrapper) is Component component) component.transform.SetParent(Root.transform, false);
                }
            }
            public void Dispose() { Object.DestroyImmediate(Root); SetSession(_originalSession); }
        }

        private sealed class Gateway : IBattleGateway
        {
            internal int Executions;
            internal IBattleCommand LastCommand;
            private readonly TrapService _traps;
            public Gateway(BattleState state, ICardDefinitionProvider cards) { CurrentBattleState = state; _traps = new TrapService(cards); }
            public BattleState CurrentBattleState { get; }
            public void ExecuteCommand(PlayerId actor, IBattleCommand command)
            {
                Executions++; LastCommand = command; CurrentBattleState.ClearTrapEvents();
                if (command is InstallTrapCommand install) _traps.Install(CurrentBattleState, actor, install.CardId, install.HandCardRuntimeId);
                else if (command is AttackCommand attack) new AttackService().Attack(CurrentBattleState, actor, attack.AttackerCoord, attack.TargetCoord);
                else throw new InvalidOperationException("Unexpected fixture command.");
            }
            public BattleState StartBattle(BattleSetupRequest request) => throw new NotSupportedException();
            public void ApplyMulligan(PlayerId owner, IEnumerable<string> cards, IDeckShuffler shuffler) => throw new NotSupportedException();
            public void PassMulligan(PlayerId owner) => throw new NotSupportedException();
            public void ResolveTurnStart() => throw new NotSupportedException();
            public void EndTurn() => throw new NotSupportedException();
        }
        private static void SetSession(string value) => typeof(AccountSessionState).GetField("<SessionToken>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Hidden).SetValue(owner, value);
        private static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Hidden).Invoke(owner, args);
    }
}
