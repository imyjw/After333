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
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Cards;
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
    public sealed class ReactiveArmourVfxTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticHidden = BindingFlags.Static | BindingFlags.NonPublic;

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            Time.timeScale = 1f;
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        [TestCase(0f, 0)]
        [TestCase(.119f, 0)]
        [TestCase(.121f, 1)]
        [TestCase(.221f, 2)]
        [TestCase(.421f, 4)]
        [TestCase(.819f, 7)]
        public void FrameTimelineShowsBarrierFirstAndAdvancesThroughEveryExplosionFrame(float elapsed, int expected)
        {
            var actual = (int)typeof(ReactiveArmourVfx).GetMethod("ResolveFrameIndex", StaticHidden).Invoke(null, new object[] { elapsed });
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(ReactiveArmourVfx.EngulfDelaySeconds, Is.GreaterThan(ReactiveArmourVfx.BarrierHoldSeconds));
            Assert.That(ReactiveArmourVfx.EngulfDelaySeconds, Is.LessThan(ReactiveArmourVfx.DurationSeconds));
        }

        [TestCase(450f, 0f)]
        [TestCase(-450f, 0f)]
        [TestCase(400f, 310f)]
        [TestCase(-400f, -310f)]
        [TestCase(0f, 600f)]
        [TestCase(0f, -600f)]
        [TestCase(1500f, 600f)]
        [TestCase(-1500f, -600f)]
        public void BarrierStaysBetweenBodiesAndBlastFacesTheAttackerForBothDirectionsAndFarTargets(float dx, float dy)
        {
            var master = new Rect(-50, -60, 100, 120);
            var attacker = new Rect(dx - 40, dy - 70, 80, 140);
            var args = new object[] { master, attacker, Vector2.zero, 0f, 0f };
            var valid = (bool)typeof(ReactiveArmourVfx).GetMethod("TryBuildLayout", StaticHidden).Invoke(null, args);
            Assert.That(valid, Is.True);
            var barrier = (Vector2)args[2]; var angle = (float)args[3]; var size = (float)args[4];
            var axis = (attacker.center - master.center).normalized;
            var distance = Vector2.Distance(master.center, attacker.center);
            var projection = Vector2.Dot(barrier - master.center, axis);
            Assert.That(projection, Is.GreaterThan(0f).And.LessThan(distance));
            Assert.That(Mathf.Abs(Vector2.Dot(barrier - master.center, new Vector2(-axis.y, axis.x))), Is.LessThan(.02f));
            var actualAxis = (Vector2)(Quaternion.Euler(0, 0, angle) * Vector3.right);
            Assert.That(Vector2.Dot(actualAxis, axis), Is.GreaterThan(.999f));
            var blastCenter = barrier + actualAxis * ((370f - 160f) / 512f * size);
            var attackerRadius = attacker.size.magnitude * .5f;
            Assert.That(Vector2.Distance(blastCenter, attacker.center) + attackerRadius, Is.LessThanOrEqualTo(size * .20f + .01f),
                "The full attacker body fits inside the peak explosion, including a distant ranged attacker.");
        }

        [Test]
        public void NativeFramesAreDistinctTransparent512CellsWithStableBarrierPivot()
        {
            var frames = Frames(); Assert.That(frames.Length, Is.EqualTo(8));
            var signatures = new HashSet<string>();
            var rightExtents = new List<int>();
            foreach (var frame in frames)
            {
                Assert.That(frame.rect.size, Is.EqualTo(new Vector2(512, 512)));
                Assert.That(frame.pivot, Is.EqualTo(new Vector2(160, 256)));
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frame));
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(importer.alphaIsTransparency, Is.True);
                Assert.That(importer.mipmapEnabled, Is.False);
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.That(source.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(frame))), Is.True);
                    var pixels = source.GetPixels(Mathf.RoundToInt(frame.rect.x), Mathf.RoundToInt(frame.rect.y), 512, 512);
                    Assert.That(pixels.Count(p => p.a > .05f), Is.GreaterThan(200));
                    Assert.That(pixels.Count(p => p.a < .01f), Is.GreaterThan(30000));
                    rightExtents.Add(pixels.Select((p, index) => p.a >= 16f / 255f ? index % 512 : -1).Max());
                    signatures.Add(Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(
                        pixels.SelectMany(p => new[] { (byte)(p.r * 255), (byte)(p.g * 255), (byte)(p.b * 255), (byte)(p.a * 255) }).ToArray())));
                }
                finally { Object.DestroyImmediate(source); }
            }
            Assert.That(signatures.Count, Is.EqualTo(8), "Eight native cells contain eight different animation poses.");
            Assert.That(rightExtents[0], Is.LessThan(280), "Frame 1 is the barrier before an outgoing explosion.");
            Assert.That(rightExtents[1], Is.GreaterThan(rightExtents[0] + 80));
            Assert.That(rightExtents[4], Is.GreaterThan(470), "The peak reaches and covers the attacker side of the sheet.");
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void LocalSuccessfulTriggerCapturesTheMovedMasterAndExactAttackerBeforeDestruction(PlayerId trapOwner)
        {
            using (var f = new Fixture())
            {
                var attackerOwner = Opposite(trapOwner); var master = f.State.GetPlayer(trapOwner).Master;
                var masterBefore = master.Position; var masterAfter = new TileCoord(3, 0);
                f.State.GetBoard(trapOwner).Move(masterBefore, masterAfter);
                var attacker = f.AddAttacker(attackerOwner, new TileCoord(0, 1), "A-212", AttackType.Melee); f.Arm(trapOwner);
                f.State.SetActivePlayer(attackerOwner);
                var before = Snapshot(f.State);
                f.Gateway.ExecuteCommand(attackerOwner, new AttackCommand(attacker.Position, master.Position));
                var request = Invoke(f.Bootstrapper, "BuildCancelledTrapImpact", before);
                var cue = Property(request, "ReactiveArmourCue");
                Assert.That(cue, Is.Not.Null);
                Assert.That(Property(cue, "MasterOwnerId"), Is.EqualTo(trapOwner));
                Assert.That(Property(cue, "MasterCoord"), Is.EqualTo(masterAfter));
                Assert.That(Property(cue, "MasterRuntimeId"), Is.EqualTo(master.RuntimeId));
                Assert.That(Property(cue, "AttackerOwnerId"), Is.EqualTo(attackerOwner));
                Assert.That(Property(cue, "AttackerCoord"), Is.EqualTo(attacker.Position));
                Assert.That(Property(cue, "AttackerRuntimeId"), Is.EqualTo(attacker.RuntimeId));
                Assert.That(f.State.LastAttackResolution.WasCancelledByTrap, Is.True);
                Assert.That(f.State.GetBoard(attackerOwner).GetOccupant(attacker.Position), Is.Null);
                Assert.That(master.CurrentHp, Is.EqualTo(333)); Assert.That(attacker.CurrentHp, Is.EqualTo(50));
                Assert.That(f.State.ValuePopupEvents, Is.Empty);
                Assert.That(Property(request, "SpellEffectId"), Is.Null.Or.Empty);
                var impacts = (IList)Property(request, "Impacts"); Assert.That(impacts.Count, Is.EqualTo(1));
                Assert.That(Property(impacts[0], "Died"), Is.True); Assert.That(Property(impacts[0], "TookDamage"), Is.False);
                Assert.That((float)Invoke(f.Bootstrapper, "EstimateImpactAnimationDuration", request),
                    Is.GreaterThanOrEqualTo(ReactiveArmourVfx.DurationSeconds));
            }
        }

        [TestCase(PlayerId.Player, PlayerId.Player)]
        [TestCase(PlayerId.Player, PlayerId.AI)]
        [TestCase(PlayerId.AI, PlayerId.Player)]
        [TestCase(PlayerId.AI, PlayerId.AI)]
        public void OnlineConfirmedTriggerProjectsBothSeatsAndRetainsVfxEvenWhenRevivalHasNoRemoval(PlayerId localSeat, PlayerId serverTrapOwner)
        {
            using (var f = new Fixture())
            {
                var owner = serverTrapOwner == localSeat ? PlayerId.Player : PlayerId.AI;
                var attackerOwner = Opposite(owner); var attacker = f.AddAttacker(attackerOwner, new TileCoord(4, 1), DemonKingRules.CardId);
                var host = new GameObject("DormantProjection"); host.SetActive(false); host.transform.SetParent(f.Root.transform);
                var connection = host.AddComponent<OnlineBattleConnectionTester>(); Set(connection, "_localPlayerId", localSeat);
                var original = Trigger(serverTrapOwner, Opposite(serverTrapOwner), attacker.Position, attacker.RuntimeId);
                var projected = (IReadOnlyList<BattleEventDto>)Invoke(connection, "ProjectBattleEventsToLocalPerspective", (object)new[] { original });
                var request = f.OnlineRequest(projected[0], projected); var cue = Property(request, "ReactiveArmourCue");
                Assert.That(cue, Is.Not.Null); Assert.That(Property(cue, "MasterOwnerId"), Is.EqualTo(owner));
                Assert.That(Property(cue, "MasterRuntimeId"), Is.EqualTo(f.State.GetPlayer(owner).Master.RuntimeId));
                Assert.That(Property(cue, "AttackerOwnerId"), Is.EqualTo(attackerOwner));
                Assert.That(Property(cue, "AttackerRuntimeId"), Is.EqualTo(attacker.RuntimeId));
                Assert.That(((IList)Property(request, "Impacts")).Count, Is.Zero, "A revival cue must not invent permanent death.");
                Assert.That(((IList)Property(request, "ValuePopupEvents")).Count, Is.Zero);
                Assert.That((bool)typeof(BattleBootstrapper).GetMethod("HasPresentationImpact", StaticHidden).Invoke(null, new[] { request }), Is.True);
                var groups = (IList)typeof(BattleBootstrapper).GetMethod("BuildOnlineAnimationGroups", StaticHidden).Invoke(null, new object[] { projected });
                Assert.That(groups.Count, Is.EqualTo(1)); Assert.That(((IList)groups[0]).Count, Is.EqualTo(1));
                Assert.That((float)Invoke(f.Bootstrapper, "EstimateOnlineBattleEventAnimationGroupDuration", (object)projected),
                    Is.GreaterThanOrEqualTo(ReactiveArmourVfx.DurationSeconds));
                var restored = f.OnlineRequest(null, Array.Empty<BattleEventDto>());
                Assert.That(Property(restored, "ReactiveArmourCue"), Is.Null);
                Assert.That((float)Invoke(f.Bootstrapper, "EstimateImpactAnimationDuration", restored), Is.Zero);
                Assert.That(f.Bootstrapper.PlayOnlineBattleEventAnimations(Array.Empty<BattleEventDto>()), Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator ActualCanvasRendersBarrierAndEngulfingBlastForEitherOwnerAndDiagonalRangedAttackers()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            foreach (var trapOwner in new[] { PlayerId.Player, PlayerId.AI })
            using (var f = new Fixture(true))
            {
                var attackerOwner = Opposite(trapOwner);
                var attacker = f.AddAttacker(attackerOwner, new TileCoord(trapOwner == PlayerId.Player ? 4 : 0, 0));
                f.CreateBoard(true); yield return null;
                var masterView = f.View(trapOwner, f.State.GetPlayer(trapOwner).Master.Position);
                var attackerView = f.View(attackerOwner, attacker.Position);
                var animator = Get<Animator>(attackerView, "_occupantVisualAnimator"); if (animator != null) animator.speed = 0;
                var effect = f.GetEffect();
                using (var before = f.Render())
                {
                    Assert.That((float)Invoke(effect, "Play", masterView, f.State.GetPlayer(trapOwner).Master.RuntimeId,
                        attackerView, attacker.RuntimeId), Is.EqualTo(ReactiveArmourVfx.DurationSeconds));
                    using (var barrier = f.Render("Barrier-" + trapOwner))
                    {
                        Sample(effect, ReactiveArmourVfx.EngulfDelaySeconds + .001f);
                        using (var peak = f.Render("Explosion-" + trapOwner))
                        {
                            Assert.That(f.DifferentPixels(barrier.Texture, before.Texture), Is.GreaterThan(300));
                            Assert.That(f.DifferentPixels(peak.Texture, barrier.Texture), Is.GreaterThan(2500));
                            Assert.That(f.EffectImage(effect).sprite, Is.SameAs(Frames()[4]));
                        }
                    }
                }
                Assert.That((bool)Property(attackerView, "IsDeathPresentation", Hidden), Is.False);
                Assert.That(attacker.CurrentHp, Is.EqualTo(50)); Assert.That(f.State.GetPlayer(trapOwner).Master.CurrentHp, Is.EqualTo(333));
                Invoke(effect, "Cancel"); AssertHidden(effect, f);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DelayedEngulfHandshakeKeepsPeakVisibleForDeathInsteadOfSkippingTheExplosion()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var f = new Fixture(true))
            {
                var attacker = f.AddAttacker(PlayerId.Player, new TileCoord(0, 1)); f.CreateBoard(); yield return null;
                var effect = f.GetEffect(); var frames = Frames();
                Assert.That((float)Invoke(effect, "Play", f.View(PlayerId.AI, f.State.AI.Master.Position),
                    f.State.AI.Master.RuntimeId, f.View(PlayerId.Player, attacker.Position), attacker.RuntimeId), Is.GreaterThan(0f));
                Set(effect, "_startedAt", Time.unscaledTime - 1f); Invoke(effect, "LateUpdate");
                Assert.That((bool)Property(effect, "IsPlaying", Hidden), Is.True);
                Assert.That(f.EffectImage(effect).sprite, Is.SameAs(frames[3]), "A stalled frame waits before the peak until the death sequence resumes.");
                Invoke(effect, "TryBeginEngulf"); Assert.That(f.EffectImage(effect).sprite, Is.SameAs(frames[4]));
                Assert.That((float)Property(effect, "RemainingDurationSeconds", Hidden), Is.GreaterThanOrEqualTo(.39f));
                Set(effect, "_engulfStartedAt", Time.unscaledTime - .31f); Set(effect, "_endsAt", Time.unscaledTime + .09f); Invoke(effect, "LateUpdate");
                Assert.That(f.EffectImage(effect).sprite, Is.SameAs(frames[7]));
                Assert.That(f.EffectImage(effect).color.a, Is.InRange(.01f, .99f));
                Set(effect, "_endsAt", Time.unscaledTime - .01f); Invoke(effect, "LateUpdate"); AssertHidden(effect, f);
                Assert.That(attacker.CurrentHp, Is.EqualTo(50)); Assert.That(f.State.ValuePopupEvents, Is.Empty);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator NativeBarrierAndBlastAdvanceUnscaledAndAreClearedOnStaleIdentityDisableAndCancel()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var f = new Fixture(true))
            {
                var attacker = f.AddAttacker(PlayerId.Player, new TileCoord(4, 1)); f.CreateBoard(); yield return null;
                var attackerView = f.View(PlayerId.Player, attacker.Position); var masterView = f.View(PlayerId.AI, f.State.AI.Master.Position);
                var effect = f.GetEffect();
                Time.timeScale = 0;
                Assert.That((float)Invoke(effect, "Play", masterView, f.State.AI.Master.RuntimeId, attackerView, attacker.RuntimeId), Is.EqualTo(ReactiveArmourVfx.DurationSeconds));
                var frames = Frames(); Assert.That(f.EffectImage(effect).sprite, Is.SameAs(frames[0]));
                Assert.That(f.EffectImage(effect).raycastTarget, Is.False);
                Assert.That(f.EffectImage(effect).GetComponentInParent<CanvasGroup>().blocksRaycasts, Is.False);
                Assert.That(effect.GetComponents<AudioSource>(), Is.Empty);
                var pivot = f.EffectImage(effect).rectTransform.TransformPoint(Vector3.zero);
                var size = f.EffectImage(effect).rectTransform.sizeDelta;
                Sample(effect, .22f); Assert.That(f.EffectImage(effect).sprite, Is.SameAs(frames[2]));
                Assert.That(f.EffectImage(effect).rectTransform.sizeDelta, Is.EqualTo(size));
                Assert.That(Vector3.Distance(pivot, f.EffectImage(effect).rectTransform.TransformPoint(Vector3.zero)), Is.LessThan(.02f));
                Sample(effect, .77f); Assert.That(f.EffectImage(effect).color.a, Is.InRange(.01f, .99f));
                var tile = f.Tile(PlayerId.Player, attacker.Position);
                var replacement = new UnitState("different-runtime", "A-301", PlayerId.Player, attacker.Position, AttackType.Ranged, 1, 50, true, false, 0);
                tile.Present(replacement);
                Sample(effect, .3f); AssertHidden(effect, f);
                Assert.That((float)Invoke(effect, "Play", masterView, f.State.AI.Master.RuntimeId, attackerView, attacker.RuntimeId), Is.Zero);
                attackerView.SetTile(tile.Title, tile.Content, true); Invoke(attackerView, "LateUpdate");
                tile.Present(attacker); attackerView.SetTile(tile.Title, tile.Content, true); Invoke(attackerView, "LateUpdate");
                Assert.That((float)Invoke(effect, "Play", masterView, "replaced-master", attackerView, attacker.RuntimeId), Is.Zero);
                Invoke(effect, "Play", masterView, f.State.AI.Master.RuntimeId, attackerView, attacker.RuntimeId);
                effect.enabled = false; AssertHidden(effect, f); effect.enabled = true;
                Invoke(effect, "Play", masterView, f.State.AI.Master.RuntimeId, attackerView, attacker.RuntimeId);
                f.Bootstrapper.CancelCombatPresentation(); AssertHidden(effect, f);
                Assert.That(f.Bootstrapper.PlayOnlineBattleEventAnimations(Array.Empty<BattleEventDto>()), Is.Zero);
                AssertHidden(effect, f);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator LocalCancellationShowsBarrierThenEngulfingExplosionBeforeDeathWithoutAttackOrHurt()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var f = new Fixture(true))
            {
                var attacker = f.AddAttacker(PlayerId.Player, new TileCoord(0, 1), "A-212", AttackType.Melee); f.Arm(PlayerId.AI); f.CreateBoard(); yield return null;
                var animator = Get<Animator>(f.View(PlayerId.Player, attacker.Position), "_occupantVisualAnimator");
                Assert.That(f.Bootstrapper.TryAttackWithPlayerOccupant(attacker.Position, f.State.AI.Master.Position, out var message), Is.True, message);
                var effect = f.GetEffect(); var frames = Frames();
                Assert.That(f.EffectImage(effect).sprite, Is.SameAs(frames[0]));
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Death"), Is.False);
                var start = Time.unscaledTime; var sawBlast = false; var sawDeath = false; var sawForbidden = false;
                while (f.Bootstrapper.IsBusy && Time.unscaledTime - start < 4)
                {
                    var animation = animator.GetCurrentAnimatorStateInfo(0);
                    var image = f.EffectImage(effect);
                    if (image != null && image.gameObject.activeInHierarchy && image.sprite != frames[0]) sawBlast = true;
                    if (animation.IsName("Death"))
                    {
                        Assert.That(sawBlast, Is.True); Assert.That(Time.unscaledTime - start, Is.GreaterThanOrEqualTo(ReactiveArmourVfx.EngulfDelaySeconds - .035f));
                        sawDeath = true;
                    }
                    sawForbidden |= animation.IsName("Attack") || animation.IsName("BeAttacked");
                    yield return null;
                }
                Assert.That(f.Bootstrapper.IsBusy, Is.False); Assert.That(sawBlast && sawDeath, Is.True); Assert.That(sawForbidden, Is.False);
                AssertHidden(effect, f); Assert.That(f.State.ValuePopupEvents, Is.Empty);
                Assert.That(f.State.AI.Master.CurrentHp, Is.EqualTo(333)); Assert.That(f.State.PlayerBoard.GetOccupant(attacker.Position), Is.Null);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator OnlineTrapOnlyRevivalUsesTheSameExplosionAndReconnectCancelsItWithoutDeath()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var f = new Fixture(true))
            {
                var attacker = f.AddAttacker(PlayerId.AI, new TileCoord(4, 1), DemonKingRules.CardId); f.CreateBoard(); yield return null;
                var source = Trigger(PlayerId.Player, PlayerId.AI, attacker.Position, attacker.RuntimeId);
                var duration = f.Bootstrapper.PlayOnlineBattleEventAnimations(new[] { source });
                Assert.That(duration, Is.GreaterThanOrEqualTo(ReactiveArmourVfx.DurationSeconds));
                var effect = f.GetEffect(); Assert.That((bool)Property(effect, "IsPlaying", Hidden), Is.True);
                Assert.That(f.Bootstrapper.IsBusy, Is.True);
                Assert.That(f.EffectImage(effect).sprite, Is.SameAs(Frames()[0]));
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(f.EffectImage(effect).sprite, Is.Not.SameAs(Frames()[0]));
                var attackerView = f.View(PlayerId.AI, attacker.Position);
                Assert.That((bool)Property(attackerView, "IsDeathPresentation", Hidden), Is.False);
                f.Bootstrapper.CancelCombatPresentation(); AssertHidden(effect, f); Assert.That(f.Bootstrapper.IsBusy, Is.False);
                Assert.That(f.Bootstrapper.PlayOnlineBattleEventAnimations(null), Is.Zero);
                yield return null; AssertHidden(effect, f);
                Assert.That((bool)Property(attackerView, "IsDeathPresentation", Hidden), Is.False);
                Assert.That(f.State.ValuePopupEvents, Is.Empty); Assert.That(f.State.AI.Master.CurrentHp, Is.EqualTo(333));
            }
            yield return new ExitPlayMode();
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void AffordableMeleeArmourProducesNoTriggerRevealOrVfxCueForRangedMasterAttack(PlayerId owner)
        {
            using (var f = new Fixture())
            {
                var attackerOwner = Opposite(owner);
                var attacker = f.AddAttacker(attackerOwner, new TileCoord(0, 1), "A-301", AttackType.Ranged);
                f.Arm(owner); f.State.SetActivePlayer(attackerOwner);
                var before = Snapshot(f.State);
                f.Gateway.ExecuteCommand(attackerOwner, new AttackCommand(attacker.Position, f.State.GetPlayer(owner).Master.Position));
                Assert.That(f.State.LastAttackResolution.WasCancelledByTrap, Is.False);
                Assert.That(f.State.GetPlayer(owner).Master.CurrentHp, Is.EqualTo(303));
                Assert.That(f.State.GetBoard(attackerOwner).GetOccupant(attacker.Position), Is.SameAs(attacker));
                Assert.That(f.State.GetPlayer(owner).Resources.Power, Is.EqualTo(4));
                Assert.That(f.State.ArmedTraps.Count, Is.EqualTo(1));
                Assert.That(f.State.TrapEvents, Is.Empty);
                Assert.That(Invoke(f.Bootstrapper, "BuildCancelledTrapImpact", before), Is.Null);
                Assert.That((bool)Property(f.GetEffect(), "IsPlaying", Hidden), Is.False);
            }
        }
        private static PlayerId Opposite(PlayerId owner) => owner == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
        private static Sprite[] Frames() => Resources.LoadAll<Sprite>(ReactiveArmourVfx.FramesResourcePath).OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
        private static object Snapshot(BattleState state) => typeof(BattleBootstrapper).GetMethod("CaptureBattleSnapshot", StaticHidden).Invoke(null, new object[] { state });
        private static BattleEventDto Trigger(PlayerId owner, PlayerId attackerOwner, TileCoord coord, string runtimeId) => new BattleEventDto
        {
            EventType = BattleEventType.TrapTriggered, CardId = ExplosiveReactiveArmourCardRules.CardId,
            SourceCardId = ExplosiveReactiveArmourCardRules.CardId, SourceOwnerId = owner, SourceRuntimeId = "consumed-trap",
            RuntimeId = "consumed-trap", TargetOwnerId = attackerOwner, TargetCoord = TileCoordDto.FromDomain(coord),
            TargetRuntimeId = runtimeId, TargetCardId = DemonKingRules.CardId, TrapCount = 0
        };
        private static void Sample(ReactiveArmourVfx effect, float elapsed)
        {
            Set(effect, "_startedAt", Time.unscaledTime - elapsed);
            if (elapsed >= ReactiveArmourVfx.EngulfDelaySeconds)
            {
                Invoke(effect, "TryBeginEngulf");
                Set(effect, "_engulfStartedAt", Time.unscaledTime - (elapsed - ReactiveArmourVfx.EngulfDelaySeconds));
                Set(effect, "_endsAt", Time.unscaledTime + (ReactiveArmourVfx.DurationSeconds - elapsed));
            }
            Invoke(effect, "LateUpdate"); Canvas.ForceUpdateCanvases();
        }
        private static void AssertHidden(ReactiveArmourVfx effect, Fixture f)
        {
            Assert.That((bool)Property(effect, "IsPlaying", Hidden), Is.False);
            var image = f.EffectImage(effect); Assert.That(image == null || !image.gameObject.activeInHierarchy, Is.True);
        }
        private static object Property(object owner, string name, BindingFlags flags = BindingFlags.Instance | BindingFlags.Public) => owner.GetType().GetProperty(name, flags).GetValue(owner);
        private static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Hidden).SetValue(owner, value);
        private static object Invoke(object owner, string name, params object[] arguments) => owner.GetType().GetMethod(name, Hidden).Invoke(owner, arguments);

        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root;
            internal readonly BattleBootstrapper Bootstrapper;
            internal readonly Gateway Gateway;
            internal readonly BattleState State;
            private readonly string _oldToken;
            private BoardPresenter _board;
            private Camera _camera;
            private RenderTexture _render;
            internal Fixture(bool active = false)
            {
                _oldToken = AccountSessionState.SessionToken;
                typeof(AccountSessionState).GetField("<SessionToken>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, "reactive-vfx-no-network");
                var deck = Enumerable.Repeat("Goblin", 33).ToArray();
                State = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.Player)); State.SetPhase(PhaseType.Main);
                Root = new GameObject("ReactiveArmourVfxFixture"); Root.SetActive(false);
                Bootstrapper = Root.AddComponent<BattleBootstrapper>(); Bootstrapper.enabled = false;
                Set(Bootstrapper, "_startOnAwake", false); Set(Bootstrapper, "_autoRunAiTurns", false); Set(Bootstrapper, "_startWithDraftBeforeBattle", false);
                Set(Bootstrapper, "_animateAttackSequences", true); Set(Bootstrapper, "_returnToDraftSceneWhenRunSessionActive", false);
                Set(Bootstrapper, "_opponentPlayedCardNormalDisplaySeconds", .18f); Set(Bootstrapper, "_opponentPlayedCardQueuedDisplaySeconds", .12f);
                Set(Bootstrapper, "_opponentPlayedCardFadeInSeconds", .02f); Set(Bootstrapper, "_opponentPlayedCardFadeOutSeconds", .02f);
                Set(Bootstrapper, "_cardCatalogAsset", AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>("Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset"));
                var provider = JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).CreateProvider();
                Gateway = new Gateway(State, provider); Set(Bootstrapper, "_battleGateway", Gateway); Set(Bootstrapper, "_cardDefinitionProvider", provider);
                if (active)
                {
                    Root.SetActive(true);
                    foreach (var name in new[] { "_opponentPlayedCardRevealCanvasGroup", "_yourTurnBannerCanvasGroup" })
                        if (typeof(BattleBootstrapper).GetField(name, Hidden)?.GetValue(Bootstrapper) is Component component) component.transform.SetParent(Root.transform, false);
                }
            }
            internal UnitState AddAttacker(PlayerId owner, TileCoord coord, string cardId = "A-301", AttackType attackType = AttackType.Ranged)
            {
                var attacker = new UnitState("attacker-" + owner, cardId, owner, coord, attackType, 30, 50, true, false, 0)
                    { WasSummonedThisTurn = false, HasSummoningSickness = false, RemainingAttacksThisTurn = 1 };
                State.GetBoard(owner).Place(coord, attacker); return attacker;
            }
            internal void Arm(PlayerId owner)
            {
                var rule = ExplosiveReactiveArmourCardRules.CreateDefinition(); State.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 4, 0));
                State.ArmedTraps.Add(new ArmedTrapState("armed-reactive", owner, rule.CardId, rule.TriggerType, rule.EffectType, rule.ActivationCost, 1));
            }
            internal object OnlineRequest(BattleEventDto source, IReadOnlyList<BattleEventDto> events) => Invoke(Bootstrapper, "BuildOnlineImpactAnimationRequest", source?.CardId, source, events);
            internal ReactiveArmourVfx GetEffect()
            {
                var effect = Get<ReactiveArmourVfx>(Bootstrapper, "_reactiveArmourVfx");
                if (effect == null) { effect = Root.AddComponent<ReactiveArmourVfx>(); Set(Bootstrapper, "_reactiveArmourVfx", effect); }
                return effect;
            }
            internal Image EffectImage(ReactiveArmourVfx effect)
            {
                return Root.GetComponentsInChildren<Image>(true).FirstOrDefault(image => image.sprite != null && AssetDatabase.GetAssetPath(image.sprite).Contains("/ReactiveArmour/"));
            }
            internal TileView Tile(PlayerId owner, TileCoord coord) => (owner == PlayerId.Player ? _board.PlayerTileViews : _board.AITileViews).Single(t => t.Coord == coord);
            internal TileTextView View(PlayerId owner, TileCoord coord) => Tile(owner, coord).GetComponent<TileTextView>();
            internal void CreateBoard(bool render = false)
            {
                var canvas = new GameObject("ReactiveCanvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
                canvas.transform.SetParent(Root.transform, false); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(2340, 1080);
                if (render)
                {
                    Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
                    var cameraHost = new GameObject("ReactivePreviewCamera", typeof(Camera)); cameraHost.transform.SetParent(Root.transform);
                    _camera = cameraHost.GetComponent<Camera>(); _camera.transform.position = new Vector3(0, 0, -10);
                    _camera.orthographic = true; _camera.orthographicSize = 5.4f; _camera.aspect = 2340f / 1080f;
                    _camera.clearFlags = CameraClearFlags.SolidColor;
                    _camera.backgroundColor = new Color(.035f, .045f, .065f);
                    _render = new RenderTexture(2340, 1080, 24); _render.Create(); _camera.targetTexture = _render;
                    canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = _camera; canvas.transform.localScale = Vector3.one * .01f;
                    // Switching from Overlay retains its screen-position transform. Put the
                    // isolated WorldSpace board at the camera origin before rendering it.
                    canvas.transform.localPosition = Vector3.zero; canvas.transform.localRotation = Quaternion.identity;
                    var viewport = _camera.WorldToViewportPoint(canvas.transform.position);
                    Assert.That(viewport.x, Is.EqualTo(.5f).Within(.001f)); Assert.That(viewport.y, Is.EqualTo(.5f).Within(.001f));
                    Assert.That(viewport.z, Is.GreaterThan(_camera.nearClipPlane));
                }
                var boardHost = new GameObject("DormantBoard"); boardHost.SetActive(false); boardHost.transform.SetParent(Root.transform);
                _board = boardHost.AddComponent<BoardPresenter>(); Set(_board, "_useResponsiveLayout", false);
                var screenHost = new GameObject("DormantScreen"); screenHost.SetActive(false); screenHost.transform.SetParent(Root.transform);
                var screen = screenHost.AddComponent<BattleScreenPresenter>(); Set(screen, "_boardPresenter", _board); Set(Bootstrapper, "_battleScreenPresenter", screen);
                foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
                {
                    var tiles = new TileView[10];
                    for (var index = 0; index < tiles.Length; index++)
                    {
                        var host = new GameObject("Tile-" + owner + index, typeof(RectTransform), typeof(Image)); host.SetActive(false); host.transform.SetParent(canvas.transform, false);
                        var rect = (RectTransform)host.transform; rect.sizeDelta = new Vector2(160, 160);
                        rect.anchoredPosition = new Vector2((owner == PlayerId.Player ? -540 : 320) + (index % 5) * 170, (index / 5) * 170 - 85);
                        var tile = host.AddComponent<TileView>(); tile.Configure(owner, index % 5, index / 5); tiles[index] = tile;
                        var background = new GameObject("TileBackground", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                        background.SetParent(host.transform, false); background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one; background.offsetMin = background.offsetMax = Vector2.zero;
                        var label = new GameObject("TileLabel", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>(); label.transform.SetParent(host.transform, false);
                        var view = host.AddComponent<TileTextView>(); Set(view, "_tileView", tile); Set(view, "_text", label); Set(view, "_battleBootstrapper", Bootstrapper);
                        tile.Present(State.GetBoard(owner).GetOccupant(tile.Coord)); host.SetActive(true); view.SetTile(tile.Title, tile.Content, tile.IsOccupied); view.PlayIdleAnimation(); Invoke(view, "LateUpdate");
                    }
                    Set(_board, owner == PlayerId.Player ? "_playerTileViews" : "_aiTileViews", tiles);
                }
                GetEffect(); Canvas.ForceUpdateCanvases();
            }
            internal CapturedPixels Render(string name = null)
            {
                Assert.That(_render, Is.Not.Null); Canvas.ForceUpdateCanvases(); _camera.Render();
                var previous = RenderTexture.active; var image = new Texture2D(_render.width, _render.height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = _render; image.ReadPixels(new Rect(0, 0, _render.width, _render.height), 0, 0); image.Apply();
                    if (name != null)
                    {
                        var directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Captures/ReactiveArmour_20261001"));
                        Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
                    }
                    return new CapturedPixels(image);
                }
                finally { RenderTexture.active = previous; }
            }
            internal int DifferentPixels(Texture2D next, Texture2D previous)
            {
                var a = next.GetPixels32(); var b = previous.GetPixels32(); return a.Where((pixel, index) => !pixel.Equals(b[index])).Count();
            }
            public void Dispose()
            {
                Object.DestroyImmediate(Root);
                if (_render != null) { _render.Release(); Object.DestroyImmediate(_render); }
                typeof(AccountSessionState).GetField("<SessionToken>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _oldToken);
            }
        }

        private sealed class CapturedPixels : IDisposable
        {
            internal readonly Texture2D Texture;
            internal CapturedPixels(Texture2D texture) { Texture = texture; }
            public void Dispose() => Object.DestroyImmediate(Texture);
        }

        private sealed class Gateway : IBattleGateway
        {
            private readonly ICardDefinitionProvider _provider;
            internal Gateway(BattleState state, ICardDefinitionProvider provider) { CurrentBattleState = state; _provider = provider; }
            public BattleState CurrentBattleState { get; }
            public void ExecuteCommand(PlayerId actor, IBattleCommand command)
            {
                CurrentBattleState.ClearTrapEvents();
                if (command is AttackCommand attack) new AttackService(new TargetingService()).Attack(CurrentBattleState, actor, attack.AttackerCoord, attack.TargetCoord);
                else if (command is InstallTrapCommand install) new TrapService(_provider).Install(CurrentBattleState, actor, install.CardId, install.HandCardRuntimeId);
                else throw new NotSupportedException();
            }
            public BattleState StartBattle(BattleSetupRequest request) => throw new NotSupportedException();
            public void ApplyMulligan(PlayerId owner, IEnumerable<string> cards, IDeckShuffler shuffler) => throw new NotSupportedException();
            public void PassMulligan(PlayerId owner) => throw new NotSupportedException();
            public void ResolveTurnStart() => throw new NotSupportedException();
            public void EndTurn() => throw new NotSupportedException();
        }
    }
}
