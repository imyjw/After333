using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Hand;
using Project333.Runtime.Presentation.Online;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class OccupantStatIncreasePresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            Time.timeScale = 1f;
            if (UnityEngine.Application.isPlaying)
            {
                yield return new ExitPlayMode();
            }
        }

        [UnityTest]
        public IEnumerator TwoStageQiEffectRipplesAtFeetThenRisesAlongBodyWithoutCoveringMasterOrStatBar()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            using (var f = new Fixture())
            {
                f.ConfigureMasterTiles();
                yield return null;

                Assert.That(OccupantStatIncreaseVfx.DurationSeconds, Is.InRange(0.5f, 0.7f));

                foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
                foreach (var kind in new[] { OccupantStatIncreaseKind.Attack, OccupantStatIncreaseKind.MaxHealth, OccupantStatIncreaseKind.Both })
                {
                    var masterTile = f.MasterTile(owner);
                    var view = masterTile.GetComponent<TileTextView>();
                    var master = masterTile.CurrentOccupant;
                    master.ApplyCombatStatBuff("color-proof",
                        attackBonus: (kind & OccupantStatIncreaseKind.Attack) != 0 ? 30 : 0,
                        maxHpBonus: (kind & OccupantStatIncreaseKind.MaxHealth) != 0 ? 10 : 0);
                    masterTile.Present(master);
                    view.SetTile(masterTile.Title, masterTile.Content, true);
                    view.PlayIdleAnimation();
                    Invoke(view, "LateUpdate");
                    yield return null;
                    Invoke(view, "LateUpdate");

                    var animator = (Animator)Get(view, "_occupantVisualAnimator");
                    if (animator != null) animator.speed = 0f;

                    f.Effect.Cancel();
                    using (var baseline = f.RenderTileCrop(owner, owner == PlayerId.Player ? kind + "-phase-0-baseline" : null))
                    {
                        var playedDuration = f.Effect.Play(view, masterTile.CurrentOccupantRuntimeId, kind, f.Board);
                        Assert.That(playedDuration, Is.EqualTo(OccupantStatIncreaseVfx.DurationSeconds));
                        Assert.That(f.Effect.IsPlayingFor(owner), Is.True);

                        var overlayCanvas = f.Effect.GetOverlayCanvas(owner);
                        var statCanvas = (Canvas)Get(view, "_occupantStatCanvas");
                        Assert.That(overlayCanvas, Is.Not.Null);
                        Assert.That(overlayCanvas.overrideSorting, Is.True);
                        Assert.That(overlayCanvas.sortingOrder, Is.EqualTo(OccupantStatIncreaseVfx.OverlaySortingOrder));
                        Assert.That(statCanvas, Is.Not.Null);
                        Assert.That(overlayCanvas.sortingOrder, Is.LessThan(statCanvas.sortingOrder),
                            "Daehwandan Qi overlay must render strictly below OccupantStatBar (4950).");

                        var graphic = f.Effect.GetGraphic(owner);
                        Assert.That(graphic.raycastTarget, Is.False);
                        Assert.That(overlayCanvas.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);

                        // Stage 1 (t = 0.08s): Foot circular ripple starts first and expands outward; rising Qi has not started yet.
                        f.Effect.Sample(owner, 0.08f);
                        var earlyRippleRadius = f.Effect.GetRippleHorizontalRadius(owner);
                        var earlyRippleAlpha = f.Effect.GetRippleAlpha(owner);
                        var earlyRisingAlpha = f.Effect.GetRisingQiAlpha(owner);
                        Assert.That(earlyRippleAlpha, Is.GreaterThan(0.25f).And.LessThanOrEqualTo(OccupantStatIncreaseVfx.MaximumRippleAlpha));
                        Assert.That(earlyRisingAlpha, Is.EqualTo(0f).Within(0.001f));

                        using (var frame1 = f.RenderTileCrop(owner, owner == PlayerId.Player ? kind + "-phase-1-foot-ripple" : null))
                        {
                            Assert.That(f.CountDifferentPixels(frame1, baseline), Is.GreaterThan(60));
                            Assert.That(f.CountPaletteChangedPixels(frame1, baseline, kind), Is.GreaterThan(40),
                                "Foot ripple must render in the selected stat palette.");

                            // Stage 1 -> Stage 2 overlap (t = 0.22s): Ripple expands wider while rising Qi begins ascending from lower body.
                            f.Effect.Sample(owner, 0.22f);
                            var midRippleRadius = f.Effect.GetRippleHorizontalRadius(owner);
                            var midRippleAlpha = f.Effect.GetRippleAlpha(owner);
                            var midRisingAlpha = f.Effect.GetRisingQiAlpha(owner);
                            var midRisingCenterY = f.Effect.GetRisingQiCenterY(owner);
                            Assert.That(midRippleRadius, Is.GreaterThan(earlyRippleRadius), "Foot ripple must expand outward.");
                            Assert.That(midRippleAlpha, Is.GreaterThan(0.10f));
                            Assert.That(midRisingAlpha, Is.GreaterThan(0.20f).And.LessThanOrEqualTo(OccupantStatIncreaseVfx.MaximumRisingQiAlpha));

                            using (var frame2 = f.RenderTileCrop(owner, owner == PlayerId.Player ? kind + "-phase-2-ripple-and-rising-qi" : null))
                            {
                                Assert.That(f.CountDifferentPixels(frame2, baseline), Is.GreaterThan(90));
                                Assert.That(f.CountDifferentPixels(frame2, frame1), Is.GreaterThan(40));
                                Assert.That(f.CountPaletteChangedPixels(frame2, baseline, kind), Is.GreaterThan(60),
                                    "Ripple and rising Qi must render in the selected stat palette.");

                                // Stage 2 peak/upper ascent (t = 0.42s): Foot ripple has finished, Qi rises higher along the body and fades out.
                                f.Effect.Sample(owner, 0.42f);
                                var lateRippleAlpha = f.Effect.GetRippleAlpha(owner);
                                var lateRisingAlpha = f.Effect.GetRisingQiAlpha(owner);
                                var lateRisingCenterY = f.Effect.GetRisingQiCenterY(owner);
                                Assert.That(lateRippleAlpha, Is.EqualTo(0f).Within(0.001f));
                                Assert.That(lateRisingAlpha, Is.GreaterThan(0.10f).And.LessThanOrEqualTo(OccupantStatIncreaseVfx.MaximumRisingQiAlpha));
                                Assert.That(lateRisingCenterY, Is.GreaterThan(midRisingCenterY), "Qi energy must rise upward along the Master's body.");

                                using (var frame3 = f.RenderTileCrop(owner, owner == PlayerId.Player ? kind + "-phase-3-rising-qi-peak" : null))
                                {
                                    Assert.That(f.CountDifferentPixels(frame3, baseline), Is.GreaterThan(50));
                                    Assert.That(f.CountPaletteChangedPixels(frame3, baseline, kind), Is.GreaterThan(35),
                                        "Rising Qi peak must render in the selected stat palette.");

                                    // Completion (t = 0.63s): Effect finishes and cleans up.
                                    f.Effect.Sample(owner, OccupantStatIncreaseVfx.DurationSeconds + 0.01f);
                                    Assert.That(f.Effect.IsPlayingFor(owner), Is.False);

                                    using (var frame4 = f.RenderTileCrop(owner, owner == PlayerId.Player ? kind + "-phase-4-complete" : null))
                                    {
                                        if (owner == PlayerId.Player)
                                        {
                                            f.WriteComparisonStrip(kind + "-stat-increase-proof", baseline, frame1, frame2, frame3, frame4);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator LocalPlayerCastKeepsBlueHandBreakupPlaysMasterVfxOnceAndNeverBlocksNextAction()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            using (var f = new Fixture())
            {
                f.ConfigureMasterTiles();
                f.ConfigureHandPresenter();
                yield return null;

                // 1. Rejected cast (0 Qi, 0 Gold): neither hand breakup nor Master VFX may play.
                f.State.Player.Resources.Spend(f.State.Player.Resources.Clone());
                f.State.Player.Hand.Add(DaehwandanCardRules.CardId, "rejected-pill", false);
                f.Hand.Present(f.State.Player.Hand);
                yield return null;

                Assert.That(f.Bootstrapper.TryCastPlayerPersistentSpell(DaehwandanCardRules.CardId, "rejected-pill", out _), Is.False);
                Assert.That(f.Effect.IsPlayingFor(PlayerId.Player), Is.False);
                Assert.That(f.ActiveHandBreakups(), Is.Empty);

                // 2. Cancelled drag preparation: neither hand breakup nor Master VFX may play.
                f.Hand.PrepareSpellCardUse(DaehwandanCardRules.CardId, "rejected-pill");
                f.Bootstrapper.CancelPendingSpellCardUse();
                yield return null;
                Assert.That(f.Effect.IsPlayingFor(PlayerId.Player), Is.False);
                Assert.That(f.AllHandBreakups(), Is.Empty);

                // 3. Successful cast: plays both blue hand breakup AND Master Qi VFX, increases Master ATK, and keeps IsBusy == false.
                var initialAttack = f.State.Player.Master.Attack;
                f.State.Player.Resources.Add(new ResourceSet(0, 5, 0, 0));
                Assert.That(f.Bootstrapper.TryCastPlayerPersistentSpell(DaehwandanCardRules.CardId, "rejected-pill", out _), Is.True);
                Assert.That(f.State.Player.Master.Attack, Is.EqualTo(initialAttack + 30));
                Assert.That(f.ActiveHandBreakups().Length, Is.EqualTo(1), "Existing blue shard hand breakup must remain active.");
                Assert.That(f.Effect.IsPlayingFor(PlayerId.Player), Is.True, "Master Qi VFX must start on successful Daehwandan cast.");
                Assert.That(f.Effect.IsPlayingFor(PlayerId.AI), Is.False);
                Assert.That(f.Bootstrapper.IsBusy, Is.False, "Daehwandan VFX must never block the next action or turn progression.");

                // 4. Moving the Master while the 0.62s VFX is still playing is allowed immediately and the VFX follows the Master.
                var moveCommand = new MoveOccupantCommand(new TileCoord(2, 1), new TileCoord(1, 1));
                f.Gateway.ExecuteCommand(PlayerId.Player, moveCommand);
                Invoke(f.Bootstrapper, "RefreshPresenter");
                yield return null;
                f.Effect.Sample(PlayerId.Player, 0.25f);
                Assert.That(f.Effect.IsPlayingFor(PlayerId.Player), Is.True, "Master Qi VFX follows the Master when moved during playback.");
            }

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator OnlineAndAiCastsApplyToCasterMasterFromBothSeatsWithoutHoldingAnimationQueue()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            using (var f = new Fixture())
            {
                f.ConfigureMasterTiles();
                var connection = f.Root.AddComponent<OnlineBattleConnectionTester>();
                yield return null;

                foreach (var localSeat in new[] { PlayerId.Player, PlayerId.AI })
                {
                    Set(connection, "_localPlayerId", localSeat);
                    var remoteOpponent = localSeat == PlayerId.Player ? PlayerId.AI : PlayerId.Player;

                    // Opponent casts Daehwandan -> projected to PlayerId.AI Master.
                    f.Effect.Cancel();
                    Invoke(f.Bootstrapper, "RefreshPresenter");
                    f.State.AI.Master.ApplyCombatStatBuff(DaehwandanCardRules.CardId, attackBonus: 30);
                    var opponentEvents = new List<BattleEventDto>
                    {
                        new BattleEventDto
                        {
                            EventType = BattleEventType.SpellCast,
                            SourceOwnerId = remoteOpponent,
                            TargetOwnerId = remoteOpponent,
                            CardId = DaehwandanCardRules.CardId,
                            SourceCardId = DaehwandanCardRules.CardId
                        }
                    };
                    var projectedOpponent = (IReadOnlyList<BattleEventDto>)Invoke(
                        connection,
                        "ProjectBattleEventsToLocalPerspective",
                        (object)opponentEvents);
                    var holdDuration = f.Bootstrapper.PlayOnlineBattleEventAnimations(projectedOpponent);
                    Invoke(f.Bootstrapper, "RefreshPresenter");
                    f.Bootstrapper.ClearOpponentPlayedCardReveals();

                    Assert.That(holdDuration, Is.EqualTo(0f), "Online Daehwandan event must not hold the animation queue.");
                    Assert.That(f.Bootstrapper.IsBusy, Is.False);
                    Assert.That(f.Effect.IsPlayingFor(PlayerId.AI), Is.True, "Opponent cast must play VFX on opponent Master.");
                    Assert.That(f.Effect.IsPlayingFor(PlayerId.Player), Is.False);

                    // Local player casts Daehwandan -> projected to PlayerId.Player Master.
                    var selfEvents = new List<BattleEventDto>
                    {
                        new BattleEventDto
                        {
                            EventType = BattleEventType.SpellCast,
                            SourceOwnerId = localSeat,
                            TargetOwnerId = localSeat,
                            CardId = DaehwandanCardRules.CardId,
                            SourceCardId = DaehwandanCardRules.CardId
                        }
                    };
                    var projectedSelf = (IReadOnlyList<BattleEventDto>)Invoke(
                        connection,
                        "ProjectBattleEventsToLocalPerspective",
                        (object)selfEvents);
                    f.State.Player.Master.ApplyCombatStatBuff(DaehwandanCardRules.CardId, attackBonus: 30);
                    Assert.That(f.Bootstrapper.PlayOnlineBattleEventAnimations(projectedSelf), Is.EqualTo(0f));
                    Invoke(f.Bootstrapper, "RefreshPresenter");
                    Assert.That(f.Effect.IsPlayingFor(PlayerId.Player), Is.True, "Own cast must play VFX on player Master.");
                    Assert.That(f.Effect.IsPlayingFor(PlayerId.AI), Is.True, "Both Masters can animate independently if cast back-to-back.");

                    // Bare snapshot / empty events must not restart or trigger VFX.
                    f.Effect.Cancel();
                    Assert.That(f.Bootstrapper.PlayOnlineBattleEventAnimations(Array.Empty<BattleEventDto>()), Is.EqualTo(0f));
                    Assert.That(f.Effect.IsPlaying, Is.False);
                }

                // Local AI turn execution triggers VFX from its confirmed stat change.
                f.Effect.Cancel();
                Invoke(f.Bootstrapper, "RefreshPresenter");
                var attackBeforeAi = f.State.AI.Master.Attack;
                f.State.RestoreRuntimeState(2, PlayerId.AI, PhaseType.Main);
                foreach (var card in f.State.AI.Hand.Cards.ToArray())
                {
                    f.State.AI.Hand.Remove(card.CardId, card.RuntimeId);
                }

                f.State.AI.Resources.Spend(f.State.AI.Resources.Clone());
                f.State.AI.Hand.Add(DaehwandanCardRules.CardId);
                f.State.AI.Resources.Add(new ResourceSet(0, 5, 0, 0));
                f.State.AI.Master.RemainingAttacksThisTurn = 0;

                var ai = new AiDecisionService(
                    f.Provider,
                    new TargetingService(),
                    ZeroCardUpgradeLevelProvider.Instance,
                    new AiSearchOptions { MaxDepth = 1, BeamWidth = 4, MaxSimulations = 200, MaxMilliseconds = 500 });
                Set(f.Bootstrapper, "_aiDecisionService", ai);
                var sequence = (IEnumerator)Invoke(f.Bootstrapper, "RunAiTurnSequence");
                Assert.That(sequence.MoveNext(), Is.True);
                Assert.That(f.State.AI.Master.Attack, Is.EqualTo(attackBeforeAi + 30));
                Assert.That(f.Effect.IsPlayingFor(PlayerId.AI), Is.True);

                // CancelCombatPresentation immediately cleans up active VFX.
                f.Bootstrapper.CancelCombatPresentation();
                Assert.That(f.Effect.IsPlaying, Is.False);
            }

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SingleAttackHealthAndBothBuffsUseRedGreenYellowForUnitsBuildingsAndMasters()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var f = new Fixture())
            {
                foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
                {
                    var unit = AddUnit(f, owner, "unit-" + owner, new TileCoord(0, 0));
                    var building = new BuildingState("building-" + owner, "PowerPlant", owner, new TileCoord(1, 0), false, 0, 40);
                    f.State.GetBoard(owner).Place(building.Position, building);
                    f.ConfigureMasterTiles();
                    yield return null;
                    unit.ApplyCombatStatBuff("future-single-attack", attackBonus: 10);
                    building.ApplyCombatStatBuff("future-max-health", maxHpBonus: 10);
                    f.State.GetPlayer(owner).Master.ApplyCombatStatBuff("future-both", attackBonus: 5, maxHpBonus: 5);
                    Invoke(f.Bootstrapper, "RefreshPresenter");
                    AssertCue(f, unit, OccupantStatIncreaseKind.Attack);
                    AssertCue(f, building, OccupantStatIncreaseKind.MaxHealth);
                    AssertCue(f, f.State.GetPlayer(owner).Master, OccupantStatIncreaseKind.Both);
                    Assert.That(unit.Attack, Is.EqualTo(20));
                    Assert.That(building.MaxHp, Is.EqualTo(50));
                    Assert.That(f.Bootstrapper.IsBusy, Is.False);
                }
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator BoardWideMaxHealthBuffAnimatesEveryAffectedIdentityAndRepeatedRefreshDoesNotRestart()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var f = new Fixture())
            {
                var units = Enumerable.Range(0, 5).Select(i => AddUnit(f, PlayerId.Player, "area-" + i, new TileCoord(i, 0))).ToArray();
                f.ConfigureMasterTiles();
                yield return null;
                foreach (var unit in units) unit.ApplyCombatStatBuff("future-area-health", maxHpBonus: 10);
                Invoke(f.Bootstrapper, "RefreshPresenter");
                foreach (var unit in units)
                {
                    AssertCue(f, unit, OccupantStatIncreaseKind.MaxHealth);
                    Assert.That(unit.MaxHp, Is.EqualTo(50));
                }
                Assert.That(f.Effect.IsPlayingFor(PlayerId.AI), Is.False);
                Assert.That(f.Effect.IsPlayingFor(PlayerId.Player, f.State.Player.Master.RuntimeId), Is.False);
                f.Effect.Sample(PlayerId.Player, 0.3f);
                Invoke(f.Bootstrapper, "RefreshPresenter");
                Assert.That(f.Effect.GetElapsedSeconds(PlayerId.Player), Is.GreaterThanOrEqualTo(0.29f));
                f.Effect.Sample(PlayerId.Player, 0.63f);
                Invoke(f.Bootstrapper, "RefreshPresenter");
                Assert.That(f.Effect.IsPlaying, Is.False);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator HealingDamageInitialSpawnAndReconnectNeverProduceIncreaseCues()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var f = new Fixture())
            {
                var unit = AddUnit(f, PlayerId.Player, "healing-unit", new TileCoord(0, 0), true);
                unit.ApplyCombatStatBuff("preexisting-upgrade", attackBonus: 10, maxHpBonus: 10);
                f.ConfigureMasterTiles();
                yield return null;
                Assert.That(f.Effect.IsPlaying, Is.False, "Initial stats on a new occupant are not a live increase.");
                unit.CurrentHp -= 12;
                Invoke(f.Bootstrapper, "RefreshPresenter");
                Assert.That(f.Effect.IsPlaying, Is.False, "Berserker's HP-derived Attack is not an applied ATK buff.");
                unit.Heal(12);
                Invoke(f.Bootstrapper, "RefreshPresenter");
                Assert.That(f.Effect.IsPlaying, Is.False, "Healing CurrentHp is not increasing MaxHp.");
                f.Bootstrapper.CancelCombatPresentation();
                unit.ApplyCombatStatBuff("during-disconnect", attackBonus: 10);
                Invoke(f.Bootstrapper, "RefreshPresenter");
                Assert.That(f.Effect.IsPlaying, Is.False, "Reconnect snapshot establishes a baseline without replay.");
                unit.ApplyCombatStatBuff("new-after-reconnect", maxHpBonus: 10);
                Invoke(f.Bootstrapper, "RefreshPresenter");
                AssertCue(f, unit, OccupantStatIncreaseKind.MaxHealth);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator FreshOnlineStateViewsKeepIdentityAndClassifyBothViewerSeats()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var f = new Fixture())
            {
                var unit = AddUnit(f, PlayerId.Player, "online-buffed-unit", new TileCoord(0, 0));
                f.ConfigureMasterTiles();
                yield return null;
                foreach (var viewer in new[] { PlayerId.Player, PlayerId.AI })
                {
                    f.Bootstrapper.CancelCombatPresentation();
                    var factory = new BattleStateViewFactory();
                    f.Screen.Present(BattleStateViewProjector.CreateLocalPerspectiveState(factory.CreateForPlayer(f.State, "stat-vfx", viewer)));
                    Assert.That(f.Effect.IsPlaying, Is.False);
                    unit.ApplyCombatStatBuff("future-targeted-buff", attackBonus: 5, maxHpBonus: 5);
                    f.Screen.Present(BattleStateViewProjector.CreateLocalPerspectiveState(factory.CreateForPlayer(f.State, "stat-vfx", viewer)));
                    var projectedOwner = viewer == PlayerId.Player ? PlayerId.Player : PlayerId.AI;
                    Assert.That(f.Effect.GetIncreaseKind(projectedOwner, unit.RuntimeId), Is.EqualTo(OccupantStatIncreaseKind.Both));
                    f.Effect.Sample(projectedOwner, 0.3f);
                    f.Screen.Present(BattleStateViewProjector.CreateLocalPerspectiveState(factory.CreateForPlayer(f.State, "stat-vfx", viewer)));
                    Assert.That(f.Effect.GetElapsedSeconds(projectedOwner), Is.GreaterThanOrEqualTo(0.29f));
                }
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator BuffedOccupantFollowsMoveSwapAndCancelsAtDeathDisableAndRealTimeExpiry()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var f = new Fixture())
            {
                var unit = AddUnit(f, PlayerId.Player, "moving-buffed-unit", new TileCoord(0, 0));
                var other = AddUnit(f, PlayerId.Player, "swap-partner", new TileCoord(1, 0));
                f.ConfigureMasterTiles();
                yield return null;
                unit.ApplyCombatStatBuff("attack", attackBonus: 10);
                Invoke(f.Bootstrapper, "RefreshPresenter");
                var before = f.Effect.GetBodyRect(PlayerId.Player).center;
                f.State.PlayerBoard.Move(unit.Position, new TileCoord(4, 0));
                Invoke(f.Bootstrapper, "RefreshPresenter");
                f.Effect.Sample(PlayerId.Player, 0.2f);
                Assert.That(f.Effect.IsPlayingFor(PlayerId.Player, unit.RuntimeId), Is.True);
                Assert.That(Vector2.Distance(before, f.Effect.GetBodyRect(PlayerId.Player).center), Is.GreaterThan(100f));
                f.State.PlayerBoard.Swap(unit.Position, other.Position);
                Invoke(f.Bootstrapper, "RefreshPresenter");
                f.Effect.Sample(PlayerId.Player, 0.3f);
                Assert.That(f.Effect.GetTargetRuntimeId(PlayerId.Player), Is.EqualTo(unit.RuntimeId));
                unit.CurrentHp = 0;
                Invoke(f.Bootstrapper, "RefreshPresenter");
                f.Effect.Sample(PlayerId.Player, 0.4f);
                Assert.That(f.Effect.IsPlaying, Is.False);
                other.ApplyCombatStatBuff("health", maxHpBonus: 10);
                Invoke(f.Bootstrapper, "RefreshPresenter");
                AssertCue(f, other, OccupantStatIncreaseKind.MaxHealth);
                f.Effect.enabled = false;
                Assert.That(f.Effect.IsPlaying, Is.False);
                f.Effect.enabled = true;
                Invoke(f.Bootstrapper, "RefreshPresenter");
                other.ApplyCombatStatBuff("attack", attackBonus: 10);
                Invoke(f.Bootstrapper, "RefreshPresenter");
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(0.72f);
                Assert.That(f.Effect.IsPlaying, Is.False, "LateUpdate expiry must finish while scaled time is paused.");
                Time.timeScale = 1f;
            }
            yield return new ExitPlayMode();
        }

        private static UnitState AddUnit(Fixture f, PlayerId owner, string runtimeId, TileCoord coord, bool berserker = false)
        {
            var unit = new UnitState(runtimeId, "Goblin", owner, coord, AttackType.Melee, 10, 40, true, false, 0, hasBerserker: berserker);
            f.State.GetBoard(owner).Place(coord, unit);
            return unit;
        }

        private static void AssertCue(Fixture f, OccupantState occupant, OccupantStatIncreaseKind kind)
        {
            Assert.That(f.Effect.GetIncreaseKind(occupant.OwnerId, occupant.RuntimeId), Is.EqualTo(kind), occupant.RuntimeId);
            var graphic = f.Effect.GetGraphic(occupant.OwnerId, occupant.RuntimeId);
            Assert.That(graphic, Is.Not.Null);
            var color = graphic.PrimaryColor;
            if (kind == OccupantStatIncreaseKind.Attack) Assert.That(color.r, Is.GreaterThan(color.g * 2f));
            if (kind == OccupantStatIncreaseKind.MaxHealth) Assert.That(color.g, Is.GreaterThan(color.r * 2f));
            if (kind == OccupantStatIncreaseKind.Both)
            {
                Assert.That(color.r, Is.GreaterThan(0.7f)); Assert.That(color.g, Is.GreaterThan(0.6f));
                Assert.That(color.b, Is.LessThan(0.2f));
            }
        }

        private static object Invoke(object instance, string name, params object[] args) =>
            instance.GetType().GetMethod(name, Hidden).Invoke(instance, args);

        private static object Get(object instance, string name) =>
            instance.GetType().GetField(name, Hidden).GetValue(instance);

        private static void Set(object instance, string name, object value) =>
            instance.GetType().GetField(name, Hidden).SetValue(instance, value);

        private sealed class CapturedPixels : IDisposable
        {
            internal readonly Texture2D Texture;
            internal CapturedPixels(Texture2D texture) { Texture = texture; }
            public static implicit operator Texture2D(CapturedPixels value) => value.Texture;
            public void Dispose() => Object.DestroyImmediate(Texture);
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root = new GameObject("StatIncreasePresentationFixture");
            internal readonly OccupantStatIncreaseVfx Effect;
            internal readonly BattleBootstrapper Bootstrapper;
            internal readonly BattleScreenPresenter Screen;
            internal readonly BoardPresenter Board;
            internal readonly LocalBattleGateway Gateway;
            internal readonly ICardDefinitionProvider Provider;
            internal readonly RectTransform CanvasRect;
            internal HandPresenter Hand;
            private readonly Camera _camera;
            private readonly RenderTexture _render;
            private readonly string _oldToken;

            internal BattleState State => Gateway.CurrentBattleState;
            internal IEnumerable<TileView> AllTiles => Board.PlayerTileViews.Concat(Board.AITileViews);

            internal Fixture()
            {
                var cameraHost = new GameObject("PreviewCamera", typeof(Camera));
                cameraHost.transform.SetParent(Root.transform);
                _camera = cameraHost.GetComponent<Camera>();
                _camera.transform.position = new Vector3(0f, 0f, -10f);
                _camera.orthographic = true;
                _camera.orthographicSize = 5.4f;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0.05f, 0.07f, 0.10f);
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    _render = new RenderTexture(2340, 1080, 24);
                    _render.Create();
                    _camera.targetTexture = _render;
                }

                var canvas = new GameObject("BattleCanvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
                canvas.transform.SetParent(Root.transform, false);
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = _camera;
                CanvasRect = (RectTransform)canvas.transform;
                CanvasRect.localScale = Vector3.one * 0.01f;

                var boardHost = new GameObject("Board");
                boardHost.SetActive(false);
                boardHost.transform.SetParent(Root.transform);
                Board = boardHost.AddComponent<BoardPresenter>();
                Set(Board, "_useResponsiveLayout", false);
                boardHost.SetActive(true);
                Set(Board, "_playerTileViews", MakeTiles(PlayerId.Player));
                Set(Board, "_aiTileViews", MakeTiles(PlayerId.AI));

                var host = new GameObject("Coordinator");
                host.SetActive(false);
                host.transform.SetParent(Root.transform);
                Bootstrapper = host.AddComponent<BattleBootstrapper>();
                Effect = host.AddComponent<OccupantStatIncreaseVfx>();
                Set(Bootstrapper, "_startOnAwake", false);
                Set(Bootstrapper, "_autoRunAiTurns", false);
                Set(Bootstrapper, "_returnToDraftSceneWhenRunSessionActive", false);
                Set(Bootstrapper, "_cardCatalogAsset",
                    AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(
                        "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset"));

                var screenHost = new GameObject("Screen");
                screenHost.SetActive(false);
                screenHost.transform.SetParent(Root.transform);
                Screen = screenHost.AddComponent<BattleScreenPresenter>();
                Set(Screen, "_boardPresenter", Board);
                Set(Screen, "_statIncreaseVfx", Effect);
                Set(Bootstrapper, "_battleScreenPresenter", Screen);

                Provider = JsonCardDefinitionDatabase
                    .FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json"))
                    .CreateProvider();
                var flow = new BattleFlowController(
                    new BattleSetupService(),
                    new MulliganService(),
                    new TurnStartService(),
                    new BattleCommandProcessor(
                        new PlayCardService(Provider),
                        new SpellService(Provider),
                        new MoveService(),
                        new AttackService(new TargetingService()),
                        new EndTurnService()));
                Gateway = new LocalBattleGateway(flow);
                var deck = Enumerable.Repeat("Goblin", 33).ToArray();
                Gateway.StartBattle(new BattleSetupRequest(deck, deck, PlayerId.Player));
                State.SetPhase(PhaseType.Main);
                Set(Bootstrapper, "_battleGateway", Gateway);
                Set(Bootstrapper, "_cardDefinitionProvider", Provider);
                _oldToken = AccountSessionState.SessionToken;
                typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null, "synthetic-daehwandan-session");
                screenHost.SetActive(true);
                host.SetActive(true);
                Layout(new Vector2(2340f, 1080f));
            }

            private TileView[] MakeTiles(PlayerId owner)
            {
                var tiles = new TileView[10];
                for (var i = 0; i < tiles.Length; i++)
                {
                    var tile = new GameObject(owner + "Tile" + i, typeof(RectTransform), typeof(Image), typeof(TileView))
                        .GetComponent<TileView>();
                    tile.transform.SetParent(CanvasRect, false);
                    tile.Configure(owner, i % 5, i / 5);
                    tile.GetComponent<Image>().color = new Color(0.11f, 0.17f, 0.22f);
                    tiles[i] = tile;
                }

                return tiles;
            }

            internal void Layout(Vector2 size)
            {
                CanvasRect.sizeDelta = size;
                var layout = BoardResponsiveLayoutCalculator.Calculate(
                    (int)size.x,
                    (int)size.y,
                    new Rect(0f, 0f, size.x, size.y),
                    0.9f,
                    24f,
                    48f,
                    new Vector2(8f, 8f),
                    1f,
                    170f);
                foreach (var tile in AllTiles)
                {
                    var rect = (RectTransform)tile.transform;
                    rect.sizeDelta = layout.TileSize;
                    rect.anchoredPosition = layout.GetTileCenter(tile.OwnerId, tile.Coord);
                }

                Canvas.ForceUpdateCanvases();
            }

            internal void ConfigureMasterTiles()
            {
                foreach (var tile in AllTiles)
                {
                    if (tile.GetComponent<TileTextView>() != null) continue;
                    tile.gameObject.SetActive(false);
                    var background = new GameObject("TileBackground", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    background.transform.SetParent(tile.transform, false);
                    background.raycastTarget = false;
                    background.color = new Color(0.11f, 0.17f, 0.22f);
                    background.rectTransform.anchorMin = Vector2.zero;
                    background.rectTransform.anchorMax = Vector2.one;
                    background.rectTransform.offsetMin = Vector2.zero;
                    background.rectTransform.offsetMax = Vector2.zero;

                    var label = new GameObject("TileLabel", typeof(RectTransform), typeof(TextMeshProUGUI))
                        .GetComponent<TextMeshProUGUI>();
                    label.transform.SetParent(tile.transform, false);
                    label.rectTransform.sizeDelta = ((RectTransform)tile.transform).sizeDelta;

                    var view = tile.gameObject.AddComponent<TileTextView>();
                    Set(view, "_tileView", tile);
                    // Native scenes wire this event to SetTile; reproduce that binding.
                    ((TileView.TileChangedEvent)Get(tile, "_onTileChanged")).AddListener(view.SetTile);
                    Set(view, "_text", label);
                    Set(view, "_battleBootstrapper", Bootstrapper);
                    Set(view, "_tileBackgroundNormalColor", new Color(0.11f, 0.17f, 0.22f, 1f));
                    tile.gameObject.SetActive(true);
                }

                Invoke(Bootstrapper, "RefreshPresenter");
                foreach (var tile in AllTiles)
                {
                    var view = tile.GetComponent<TileTextView>();
                    if (tile.CurrentOccupant != null)
                    {
                        view.SetTile(tile.Title, tile.Content, true);
                        view.PlayIdleAnimation();
                    }

                    Invoke(view, "LateUpdate");
                }
            }

            internal void ConfigureHandPresenter()
            {
                var slots = new GameObject("LogicalSlots", typeof(RectTransform));
                slots.transform.SetParent(CanvasRect, false);
                var layout = new GameObject("RuntimeHandRoot", typeof(RectTransform));
                layout.transform.SetParent(CanvasRect, false);
                var rect = layout.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(0f, 440f);
                Hand = CanvasRect.gameObject.AddComponent<HandPresenter>();
                Set(Hand, "_handRoot", slots.transform);
                Set(Hand, "_runtimeCanvas", CanvasRect.GetComponent<Canvas>());
                Set(Hand, "_runtimeCanvasRect", CanvasRect);
                Set(Hand, "_runtimeHandRoot", rect);
                Set(Screen, "_handPresenter", Hand);
                Hand.SetExpanded(true);
            }

            internal TileView MasterTile(PlayerId owner) =>
                AllTiles.Single(t => t.OwnerId == owner && t.CurrentOccupant?.Kind == OccupantKind.Master);

            internal SpellCardUseVfx[] AllHandBreakups() =>
                Root.GetComponentsInChildren<SpellCardUseVfx>(true);

            internal SpellCardUseVfx[] ActiveHandBreakups() =>
                AllHandBreakups().Where(v => v.IsPlaying && v.gameObject.activeInHierarchy).ToArray();

            internal CapturedPixels RenderTileCrop(PlayerId owner, string saveName = null)
            {
                Assert.That(_render, Is.Not.Null);
                Canvas.ForceUpdateCanvases();
                _camera.Render();

                var tile = MasterTile(owner);
                var screenCenter = _camera.WorldToScreenPoint(tile.transform.position);
                var cropWidth = 280;
                var cropHeight = 320;
                var x0 = Mathf.Clamp(Mathf.RoundToInt(screenCenter.x) - cropWidth / 2, 0, _render.width - cropWidth);
                var y0 = Mathf.Clamp(Mathf.RoundToInt(screenCenter.y) - cropHeight / 2 - 12, 0, _render.height - cropHeight);

                var previous = RenderTexture.active;
                var full = new Texture2D(_render.width, _render.height, TextureFormat.RGB24, false);
                var crop = new Texture2D(cropWidth, cropHeight, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = _render;
                    full.ReadPixels(new Rect(0f, 0f, _render.width, _render.height), 0, 0);
                    full.Apply();
                    crop.SetPixels(full.GetPixels(x0, y0, cropWidth, cropHeight));
                    crop.Apply();

                    if (!string.IsNullOrEmpty(saveName))
                    {
                        var dir = GetCaptureDirectory();
                        Directory.CreateDirectory(dir);
                        File.WriteAllBytes(Path.Combine(dir, saveName + ".png"), crop.EncodeToPNG());
                    }

                    return new CapturedPixels(crop);
                }
                finally
                {
                    RenderTexture.active = previous;
                    Object.DestroyImmediate(full);
                }
            }

            internal void WriteComparisonStrip(string saveName, params Texture2D[] frames)
            {
                if (frames == null || frames.Length == 0) return;
                var gap = 12;
                var pad = 16;
                var w = frames[0].width;
                var h = frames[0].height;
                var totalWidth = pad * 2 + frames.Length * w + (frames.Length - 1) * gap;
                var totalHeight = pad * 2 + h;
                var strip = new Texture2D(totalWidth, totalHeight, TextureFormat.RGB24, false);
                try
                {
                    var bg = new Color32(10, 14, 20, 255);
                    var fill = Enumerable.Repeat(bg, totalWidth * totalHeight).ToArray();
                    strip.SetPixels32(fill);
                    for (var i = 0; i < frames.Length; i++)
                    {
                        strip.SetPixels(pad + i * (w + gap), pad, w, h, frames[i].GetPixels());
                    }

                    strip.Apply();
                    var dir = GetCaptureDirectory();
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir, saveName + ".png"), strip.EncodeToPNG());
                }
                finally
                {
                    Object.DestroyImmediate(strip);
                }
            }

            private static string GetCaptureDirectory() =>
                Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Captures/StatIncrease_20261026"));

            internal int CountDifferentPixels(Texture2D a, Texture2D b)
            {
                var pa = a.GetPixels32();
                var pb = b.GetPixels32();
                var count = 0;
                for (var i = 0; i < pa.Length; i++)
                {
                    if (!pa[i].Equals(pb[i])) count++;
                }

                return count;
            }

            internal int CountPaletteChangedPixels(Texture2D current, Texture2D baseline, OccupantStatIncreaseKind kind)
            {
                var pc = current.GetPixels32();
                var pb = baseline.GetPixels32();
                var count = 0;
                for (var i = 0; i < pc.Length; i++)
                {
                    var dr = pc[i].r - pb[i].r;
                    var dg = pc[i].g - pb[i].g;
                    var db = pc[i].b - pb[i].b;
                    var matches = kind == OccupantStatIncreaseKind.Attack
                        ? dr >= 8 && dr > dg + 4 && dr > db + 4
                        : kind == OccupantStatIncreaseKind.MaxHealth
                            ? dg >= 8 && dg > dr + 4 && dg > db + 4
                            : dr >= 8 && dg >= 8 && Math.Min(dr, dg) > db + 4;
                    if (matches)
                    {
                        count++;
                    }
                }

                return count;
            }

            public void Dispose()
            {
                typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null, _oldToken);
                Object.DestroyImmediate(Root);
                if (_render != null)
                {
                    _render.Release();
                    Object.DestroyImmediate(_render);
                }
            }
        }
    }
}
