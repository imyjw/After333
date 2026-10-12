using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Presentation.Battle;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class StunFreezeVfxPresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string StarName = "StunOrbitStars", IceName = "FreezeIceCrystal";
        private const string StarResource = "Project333/StatusEffects/Stun/OrbitStars";
        private const string IceResource = "Project333/StatusEffects/Freeze/IceCrystal";
        private static readonly string[] NativeShapes = { "Gwangma", "BlueDragon", "A-301", "ManaCannon" };
        private static readonly Dictionary<Sprite, Rect> AlphaRects = new Dictionary<Sprite, Rect>();
        private static readonly Dictionary<Sprite, Vector2Int> SourceSizes = new Dictionary<Sprite, Vector2Int>();
        private static Type EffectType => typeof(TileTextView).Assembly.GetType(
            "Project333.Runtime.Presentation.Battle.ActionDebuffStatusVfx", true);

        [TearDown] public void Cleanup() { AlphaRects.Clear(); SourceSizes.Clear(); BoardAnimationControllerTestUtility.Cleanup(); }
        [UnityTearDown] public IEnumerator LeavePlayMode()
        {
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }


        [UnityTest]
        public IEnumerator NativeBodyFreezesImmediatelyAtItsExactPoseAndResumesItsOriginalCustomSpeed()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                foreach (var card in NativeShapes)
                {
                    var unit = Unit(PlayerId.Player, card, "native-pose-" + card);
                    fixture.Present(fixture.Left, unit, true);
                    yield return null; yield return null;
                    var animator = AnimatorOf(fixture.Left);
                    animator.speed = .73f; fixture.Left.PlayIdleAnimation(); animator.Update(.21f); Sync(fixture.Left);
                    var before = new NativePose(fixture.Left);
                    Assert.That(before.Time, Is.GreaterThan(0f));
                    unit.ApplyFreeze(1); fixture.Left.Refresh();
                    Assert.That(animator.speed, Is.Zero, "The first Refresh pauses the native Animator immediately.");
                    AssertPose(fixture.Left, before);
                    var truth = fixture.Truth();
                    yield return new WaitForSecondsRealtime(.24f);
                    AssertPose(fixture.Left, before);
                    Assert.That(fixture.Truth(), Is.EqualTo(truth));
                    unit.ResolveActionDebuffsTurnEnd(); fixture.Left.Refresh();
                    Assert.That(unit.IsFrozen, Is.False);
                    Assert.That(animator.speed, Is.EqualTo(.73f), "Restore the original speed rather than assigning a global default.");
                    AssertPose(fixture.Left, before);
                    yield return new WaitForSecondsRealtime(.15f);
                    Assert.That(AnimatorOf(fixture.Left).GetCurrentAnimatorStateInfo(0).normalizedTime,
                        Is.GreaterThan(before.Time), "Playback resumes from the held position without rewinding.");
                }
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PassiveDrainedChangesDuringFreezeApplyAfterReleaseWithoutChangingTheHeldPose()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            foreach (var startsDrained in new[] { false, true })
            {
                var fixture = new Fixture();
                try
                {
                    yield return null; yield return null;
                    fixture.LeftUnit.IsDrained = startsDrained; fixture.Left.Refresh();
                    var animator = AnimatorOf(fixture.Left); animator.speed = .77f;
                    animator.Update(.23f); Sync(fixture.Left);
                    AssertNativeState(fixture.Left, startsDrained ? "Drained" : "Idle");
                    fixture.LeftUnit.ApplyFreeze(1); fixture.Left.Refresh();
                    var pose = new NativePose(fixture.Left); var truth = fixture.Truth();
                    fixture.LeftUnit.IsDrained = !startsDrained;
                    fixture.Left.ResetCombatPresentation(); fixture.Left.Refresh();
                    AssertPose(fixture.Left, pose); Assert.That(animator.speed, Is.Zero);
                    yield return new WaitForSecondsRealtime(.18f);
                    AssertPose(fixture.Left, pose);
                    fixture.LeftUnit.ResolveActionDebuffsTurnEnd(); fixture.Left.Refresh();
                    Assert.That(fixture.LeftUnit.IsFrozen, Is.False);
                    AssertNativeState(fixture.Left, startsDrained ? "Idle" : "Drained");
                    Assert.That(animator.speed, Is.EqualTo(.77f));
                    Assert.That(Visual(fixture.Left).sprite, Is.SameAs(BoardAnimationControllerTestUtility.Frames(
                        BoardAnimationControllerTestUtility.Clip("ManaCannon", startsDrained ? "Idle" : "Drained"))[0]),
                        "After release, the newly current passive state is sampled immediately.");
                    Assert.That(fixture.Truth(), Is.EqualTo(truth));
                }
                finally { fixture.Dispose(); }
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CommonNativeClipSamplerKeepsItsFrameAndFractionalTimeThenContinues()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                var unit = Unit(PlayerId.Player, "Gwangma", "sampled-pose");
                fixture.Present(fixture.Left, unit, true); yield return null; yield return null;
                var animator = AnimatorOf(fixture.Left); animator.speed = .67f; animator.enabled = false;
                fixture.Left.PlayRunAnimation();
                Assert.That(Get(fixture.Left, "_activeSpriteSequence"), Is.Not.Null,
                    "Use the shared sampler of the actual native controller while its Animator is disabled.");
                yield return new WaitForSecondsRealtime(.19f);
                unit.ApplyFreeze(2); fixture.Left.Refresh();
                var index = (int)Get(fixture.Left, "_activeSequenceFrameIndex");
                var timer = (float)Get(fixture.Left, "_activeSequenceFrameTimer");
                var sprite = Visual(fixture.Left).sprite;
                yield return new WaitForSecondsRealtime(.24f);
                Assert.That((int)Get(fixture.Left, "_activeSequenceFrameIndex"), Is.EqualTo(index));
                Assert.That((float)Get(fixture.Left, "_activeSequenceFrameTimer"), Is.EqualTo(timer).Within(.000001f));
                Assert.That(Visual(fixture.Left).sprite, Is.SameAs(sprite));
                unit.ClearActionDebuffs(); fixture.Left.Refresh();
                Assert.That(animator.enabled, Is.False, "Clearing Freeze must preserve an explicitly disabled Animator.");
                Assert.That(animator.speed, Is.EqualTo(.67f));
                Assert.That((int)Get(fixture.Left, "_activeSequenceFrameIndex"), Is.EqualTo(index));
                Assert.That((float)Get(fixture.Left, "_activeSequenceFrameTimer"), Is.EqualTo(timer).Within(.000001f));
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That((int)Get(fixture.Left, "_activeSequenceFrameIndex") != index ||
                    Mathf.Abs((float)Get(fixture.Left, "_activeSequenceFrameTimer") - timer) > .000001f, Is.True,
                    "The sampler advances from the saved frame and fractional time after Freeze clears.");
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SameRuntimeIdWireSnapshotsPreserveFrozenPoseAndFreshOccupantsStartTheirOwnPose()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                yield return null; yield return null;
                var animator = AnimatorOf(fixture.Left); animator.speed = .61f;
                fixture.Left.PlayIdleAnimation(); animator.Update(.23f); Sync(fixture.Left);
                fixture.LeftUnit.ApplyStun(3); fixture.LeftUnit.ApplyFreeze(3); fixture.Left.Refresh();
                var frozenPose = new NativePose(fixture.Left);
                yield return WaitVisible(fixture.Left, StarName); yield return new WaitForSecondsRealtime(.2f);
                var star = Overlay(fixture.Left, StarName); var ice = Overlay(fixture.Left, IceName);
                var effect = Effect(fixture.Left);
                var starStart = (float)Get(effect, "_stunStartedAt");
                var iceStart = (float)Get(effect, "_freezeStartedAt");
                for (var i = 0; i < 3; i++)
                {
                    var wire = new BattleStateViewFactory().CreateForPlayer(fixture.Authority, "freeze-snapshot", PlayerId.Player);
                    var restored = BattleStateViewProjector.CreateLocalPerspectiveState(wire);
                    var snapshot = restored.PlayerBoard.GetOccupant(new TileCoord(0, 0));
                    fixture.Present(fixture.Left, snapshot);
                    yield return null; yield return null;
                    Assert.That(fixture.Left.GetComponent<TileView>().CurrentOccupant, Is.SameAs(snapshot));
                    Assert.That(fixture.Left.VisualRuntimeId, Is.EqualTo(snapshot.RuntimeId));
                    AssertPose(fixture.Left, frozenPose);
                    Assert.That(animator.speed, Is.Zero);
                    Assert.That(Overlay(fixture.Left, StarName), Is.SameAs(star));
                    Assert.That(Overlay(fixture.Left, IceName), Is.SameAs(ice));
                    Assert.That(Get(effect, "_boundOccupant"), Is.SameAs(snapshot));
                    Assert.That((float)Get(effect, "_stunStartedAt"), Is.EqualTo(starStart));
                    Assert.That((float)Get(effect, "_freezeStartedAt"), Is.EqualTo(iceStart));
                }
                fixture.LeftUnit.ClearActionDebuffs(); fixture.Left.Refresh();
                Assert.That(animator.speed, Is.EqualTo(.61f)); AssertPose(fixture.Left, frozenPose);
                var fresh = Unit(PlayerId.Player, "ManaCannon", "new-frozen-occupant"); fresh.ApplyFreeze(2);
                fixture.Present(fixture.Left, fresh); yield return null; yield return null;
                Assert.That(fixture.Left.VisualRuntimeId, Is.EqualTo(fresh.RuntimeId));
                Assert.That(fixture.Left.GetComponent<TileView>().CurrentOccupant, Is.SameAs(fresh));
                Assert.That(animator.speed, Is.Zero); AssertNativeState(fixture.Left, "Idle");
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.LessThan(.01f),
                    "A new occupant binds its own initial pose instead of inheriting an unrelated frozen phase.");
                Assert.That(Visual(fixture.Left).sprite, Is.SameAs(BoardAnimationControllerTestUtility.Frames(
                    BoardAnimationControllerTestUtility.Clip("ManaCannon", "Idle"))[0]));
                Assert.That(Overlays(fixture.Left, StarName).Length, Is.EqualTo(1));
                Assert.That(Overlays(fixture.Left, IceName).Length, Is.EqualTo(1));
                Assert.That((float)Get(Effect(fixture.Left), "_freezeStartedAt"), Is.GreaterThan(iceStart),
                    "A different occupant starts its own independent crystal clock.");
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator FrozenBodyIgnoresNonDeathMotionRequestsAndDeathRestoresPlayback()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                yield return null; yield return null;
                var animator = AnimatorOf(fixture.Left); animator.speed = .79f;
                fixture.Left.PlayRunAnimation(); animator.Update(.19f); Sync(fixture.Left);
                fixture.LeftUnit.ApplyStun(2); fixture.LeftUnit.ApplyFreeze(2); fixture.Left.Refresh();
                var pose = new NativePose(fixture.Left); var hp = fixture.LeftUnit.CurrentHp;
                foreach (var state in new[] { "Idle", "Attack", "Run", "BeAttacked", "Drained" })
                {
                    Invoke(fixture.Left, "PlayAnimationState", state);
                    AssertPose(fixture.Left, pose); Assert.That(animator.speed, Is.Zero, state);
                }
                fixture.Left.PlayDeathAnimation(); fixture.Left.Refresh();
                Assert.That(animator.speed, Is.EqualTo(.79f)); AssertNativeState(fixture.Left, "Death");
                Assert.That(Visible(Overlay(fixture.Left, StarName)) || Visible(Overlay(fixture.Left, IceName)), Is.False);
                var deathTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThan(deathTime));
                Assert.That(fixture.LeftUnit.CurrentHp, Is.EqualTo(hp), "A presentation cannot apply authoritative death damage.");
                fixture.Capture("stun-freeze-death-no-overlays.png");
                fixture.LeftUnit.ClearActionDebuffs(); fixture.Left.ResetCombatPresentation();
                yield return null; yield return null;
                AssertNativeState(fixture.Left, "Idle"); Assert.That(animator.speed, Is.EqualTo(.79f));
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator StunFreezeAndDrainedVisualClocksContinueIndependentlyWhileTheBodyAndGameClockArePaused()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture(); var oldScale = Time.timeScale;
            try
            {
                yield return null; yield return null;
                var animator = AnimatorOf(fixture.Left); animator.speed = .64f;
                fixture.LeftUnit.IsDrained = true; fixture.Left.Refresh();
                fixture.LeftUnit.ApplyStun(4); fixture.LeftUnit.ApplyFreeze(4); fixture.Left.Refresh();
                Time.timeScale = 0f;
                yield return WaitVisible(fixture.Left, StarName); yield return WaitVisible(fixture.Left, IceName);
                var pose = new NativePose(fixture.Left); var truth = fixture.Truth();
                var stars = new HashSet<Sprite>(); var arcs = new HashSet<Sprite>(); var iceAlphas = new List<float>();
                var stopAt = Time.realtimeSinceStartup + .5f;
                while (Time.realtimeSinceStartup < stopAt)
                {
                    AssertPose(fixture.Left, pose); Assert.That(animator.speed, Is.Zero);
                    var star = Overlay(fixture.Left, StarName); var ice = Overlay(fixture.Left, IceName);
                    if (Visible(star)) stars.Add(star.sprite);
                    if (Visible(ice)) iceAlphas.Add(ice.color.a);
                    var arc = Overlay(fixture.Left, "DrainedElectricArcs"); if (Visible(arc)) arcs.Add(arc.sprite);
                    yield return null;
                }
                Assert.That(stars.Count, Is.GreaterThan(1), "The orbital stars use their own unscaled clock.");
                Assert.That(arcs.Count, Is.GreaterThan(1), "Existing Drained electricity keeps its independent unscaled clock.");
                Assert.That(iceAlphas.Count, Is.GreaterThan(1));
                Assert.That(iceAlphas.Max() - iceAlphas.Min(), Is.GreaterThan(.005f), "Ice has a subtle pulse while its body pose is held.");
                Assert.That(iceAlphas.All(a => a > 0f && a < .8f), Is.True);
                Assert.That(fixture.Truth(), Is.EqualTo(truth));
                Assert.That(fixture.Root.GetComponentsInChildren<AudioSource>(true).Length, Is.Zero, "These status visuals add no sound.");
                fixture.Capture("stun-freeze-drained-paused.png");
            }
            finally { Time.timeScale = oldScale; fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ClearingOneDebuffLeavesTheOtherVisibleAndDoesNotRestartItsClock()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                fixture.LeftUnit.ApplyStun(3); fixture.LeftUnit.ApplyFreeze(2); fixture.Left.Refresh();
                yield return WaitVisible(fixture.Left, StarName); yield return new WaitForSecondsRealtime(.25f);
                var star = Overlay(fixture.Left, StarName); var ice = Overlay(fixture.Left, IceName);
                var continuingStar = star.sprite;
                fixture.LeftUnit.RestoreActionDebuffs(3, 0); fixture.Left.Refresh();
                Assert.That(Visible(star), Is.True); Assert.That(star.sprite, Is.SameAs(continuingStar));
                Assert.That(Visible(ice), Is.False); Assert.That(AnimatorOf(fixture.Left).speed, Is.EqualTo(1f));
                var stunOnlyTime = AnimatorOf(fixture.Left).GetCurrentAnimatorStateInfo(0).normalizedTime;
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(AnimatorOf(fixture.Left).GetCurrentAnimatorStateInfo(0).normalizedTime,
                    Is.GreaterThan(stunOnlyTime), "Stun stars alone do not freeze the body.");
                fixture.LeftUnit.ApplyFreeze(2); fixture.Left.Refresh();
                Assert.That(Visible(ice), Is.True);
                var iceAlpha = ice.color.a; var iceSprite = ice.sprite;
                fixture.LeftUnit.RestoreActionDebuffs(0, 2); fixture.Left.Refresh();
                Assert.That(Visible(star), Is.False); Assert.That(Visible(ice), Is.True);
                Assert.That(ice.sprite, Is.SameAs(iceSprite)); Assert.That(ice.color.a, Is.EqualTo(iceAlpha).Within(.000001f));
                Assert.That(AnimatorOf(fixture.Left).speed, Is.Zero);
                fixture.LeftUnit.ClearActionDebuffs(); fixture.Left.Refresh();
                Assert.That(Visible(star) || Visible(ice), Is.False); Assert.That(AnimatorOf(fixture.Left).speed, Is.EqualTo(1f));
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DisableDestroyAndReactivationRestoreSpeedAndLeaveOnlyOneReusableOverlaySet()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                yield return null; yield return null;
                var animator = AnimatorOf(fixture.Left); animator.speed = .82f;
                fixture.LeftUnit.ApplyStun(3); fixture.LeftUnit.ApplyFreeze(3); fixture.Left.Refresh();
                yield return WaitVisible(fixture.Left, StarName);
                var stars = Overlay(fixture.Left, StarName); var ice = Overlay(fixture.Left, IceName);
                fixture.Left.enabled = false; yield return null;
                Assert.That(animator.speed, Is.EqualTo(.82f));
                Assert.That(Visible(stars) || Visible(ice), Is.False);
                fixture.Left.Refresh(); Assert.That(animator.speed, Is.EqualTo(.82f));
                fixture.Left.enabled = true; yield return WaitVisible(fixture.Left, IceName);
                Assert.That(animator.speed, Is.Zero); Assert.That(Overlay(fixture.Left, StarName), Is.SameAs(stars));
                fixture.Left.gameObject.SetActive(false); yield return null;
                Assert.That(animator.speed, Is.EqualTo(.82f));
                Assert.That(Visible(stars) || Visible(ice), Is.False);
                fixture.Left.gameObject.SetActive(true); yield return WaitVisible(fixture.Left, StarName);
                Assert.That(animator.speed, Is.Zero);
                var effect = Effect(fixture.Left); effect.enabled = false;
                yield return null; yield return null;
                Assert.That(Visible(stars) || Visible(ice), Is.False);
                effect.enabled = true; yield return WaitVisible(fixture.Left, IceName);
                Object.Destroy(effect); yield return null; yield return null;
                yield return WaitVisible(fixture.Left, StarName);
                Assert.That(stars == null && ice == null, Is.True, "Destroying the visual component cleans its old Images.");
                Assert.That(Overlays(fixture.Left, StarName).Length, Is.EqualTo(1));
                Assert.That(Overlays(fixture.Left, IceName).Length, Is.EqualTo(1));
                var currentStars = Overlay(fixture.Left, StarName); var currentIce = Overlay(fixture.Left, IceName);
                Object.Destroy(fixture.Left); yield return null; yield return null;
                Assert.That(animator.speed, Is.EqualTo(.82f));
                Assert.That(currentStars == null && currentIce == null, Is.True);
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator HiddenEnemyDeadAndClearedTilesHideOverlaysWhileSealboundKeepsExistingDebuffs()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                var own = Unit(PlayerId.Player, "ManaCannon", "own-hidden", true);
                var enemy = Unit(PlayerId.AI, "GaebangChief", "enemy-hidden", true);
                own.ApplyStun(3); own.ApplyFreeze(3); enemy.ApplyStun(3); enemy.ApplyFreeze(3);
                fixture.Present(fixture.Left, own); fixture.Present(fixture.Right, enemy);
                yield return WaitVisible(fixture.Left, StarName);
                Assert.That(own.IsHiding && enemy.IsHiding, Is.True, "Presentation cannot reveal an authoritative hidden unit.");
                Assert.That(Visible(Overlay(fixture.Right, StarName)) || Visible(Overlay(fixture.Right, IceName)), Is.False);
                enemy.RevealHiding(); yield return WaitVisible(fixture.Right, IceName);
                Assert.That(Visible(Overlay(fixture.Right, StarName)), Is.True);
                fixture.Capture("stun-freeze-hidden-owner-visibility.png");
                enemy.EnterSealbound(2); yield return null; yield return null;
                Assert.That(Visible(Overlay(fixture.Right, StarName)) && Visible(Overlay(fixture.Right, IceName)), Is.True,
                    "Sealbound blocks new debuffs, but existing Stun and Freeze retain their presentation.");
                Assert.That(AnimatorOf(fixture.Right).speed, Is.Zero);
                enemy.ResolveSealboundOwnerTurnStart(); enemy.ResolveSealboundOwnerTurnStart();
                yield return WaitVisible(fixture.Right, IceName);
                enemy.CurrentHp = 0; yield return null; yield return null;
                Assert.That(Visible(Overlay(fixture.Right, StarName)) || Visible(Overlay(fixture.Right, IceName)), Is.False);
                fixture.Present(fixture.Left, null); yield return null; yield return null;
                Assert.That(Visible(Overlay(fixture.Left, StarName)) || Visible(Overlay(fixture.Left, IceName)), Is.False);
                Assert.That(AnimatorOf(fixture.Left).speed, Is.GreaterThan(0f), "A cleared tile releases its Animator pause.");
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator NativeShapesForBothOwnersKeepStarsAboveTheHeadAndCrystalAroundTheVisibleBody()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var fixture = new Fixture();
            try
            {
                foreach (var card in NativeShapes)
                {
                    var own = Unit(PlayerId.Player, card, "shape-own-" + card);
                    var enemy = Unit(PlayerId.AI, card, "shape-enemy-" + card);
                    own.ApplyStun(3); own.ApplyFreeze(3); enemy.ApplyStun(3); enemy.ApplyFreeze(3);
                    fixture.Present(fixture.Left, own, true); fixture.Present(fixture.Right, enemy, true);
                    yield return WaitVisible(fixture.Left, StarName); yield return WaitVisible(fixture.Right, IceName);
                    fixture.Capture("stun-freeze-body-" + card.ToLowerInvariant() + ".png");
                    foreach (var view in new[] { fixture.Left, fixture.Right })
                    {
                        Assert.That(AnimatorOf(view).runtimeAnimatorController,
                            Is.SameAs(BoardAnimationControllerTestUtility.Controller(card)));
                        WriteLayoutEvidence(view);
                        AssertStatusPlacement(view);
                        Assert.That(Overlay(view, StarName).raycastTarget || Overlay(view, IceName).raycastTarget, Is.False);
                        Assert.That(AnimatorOf(view).speed, Is.Zero);
                    }
                    var viewToMove = fixture.Left; var star = Overlay(viewToMove, StarName); var ice = Overlay(viewToMove, IceName);
                    var starOffset = star.rectTransform.position - Visual(viewToMove).rectTransform.position;
                    viewToMove.SetVisualWorldPosition(viewToMove.GetVisualWorldPosition() + new Vector3(17, -8, 0));
                    yield return null; yield return null;
                    Assert.That(Vector3.Distance(star.rectTransform.position - Visual(viewToMove).rectTransform.position, starOffset), Is.LessThan(.03f));
                    var oldIceSize = ice.rectTransform.rect.size;
                    Visual(viewToMove).rectTransform.sizeDelta *= .7f; yield return null; yield return null;
                    Assert.That(ice.rectTransform.rect.width, Is.LessThan(oldIceSize.x * .85f));
                    Assert.That(ice.rectTransform.rect.height, Is.LessThan(oldIceSize.y * .85f));
                    AssertStatusPlacement(viewToMove);
                }
            }
            finally { fixture.Dispose(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AlreadyFrozenWireRestorationStartsPausedForBothLocalPerspectivesWithoutChangingTruth()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var original = CreateState();
            foreach (var unit in original.PlayerBoard.EnumerateOccupants().Concat(original.AIBoard.EnumerateOccupants()))
            { unit.ApplyStun(3); unit.ApplyFreeze(3); }
            foreach (var viewer in new[] { PlayerId.Player, PlayerId.AI })
            {
                var wire = new BattleStateViewFactory().CreateForPlayer(original, "frozen-restore", viewer);
                var restored = BattleStateViewProjector.CreateLocalPerspectiveState(wire);
                var fixture = new Fixture(restored);
                try
                {
                    Assert.That(AnimatorOf(fixture.Left).speed, Is.Zero); Assert.That(AnimatorOf(fixture.Right).speed, Is.Zero);
                    var truth = fixture.Truth();
                    yield return WaitVisible(fixture.Left, StarName); yield return WaitVisible(fixture.Right, IceName);
                    var left = new NativePose(fixture.Left); var right = new NativePose(fixture.Right);
                    yield return new WaitForSecondsRealtime(.22f);
                    AssertPose(fixture.Left, left); AssertPose(fixture.Right, right);
                    Assert.That(fixture.LeftUnit.OwnerId, Is.EqualTo(PlayerId.Player));
                    Assert.That(fixture.RightUnit.OwnerId, Is.EqualTo(PlayerId.AI));
                    Assert.That(fixture.Truth(), Is.EqualTo(truth));
                    fixture.Capture(viewer == PlayerId.Player ? "stun-freeze-restored-player.png" : "stun-freeze-restored-ai.png");
                }
                finally { fixture.Dispose(); }
            }
            yield return new ExitPlayMode();
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root = new GameObject("StunFreezeVfxFixture");
            internal readonly BattleState Authority;
            internal readonly TileTextView Left, Right;
            internal UnitState LeftUnit => (UnitState)Left.GetComponent<TileView>().CurrentOccupant;
            internal UnitState RightUnit => (UnitState)Right.GetComponent<TileView>().CurrentOccupant;
            private readonly Camera _camera; private readonly RenderTexture _render;

            internal Fixture(BattleState state = null)
            {
                Authority = state ?? CreateState();
                var cameraObject = new GameObject("StunFreezeVfxCamera", typeof(Camera), typeof(AudioListener));
                cameraObject.transform.SetParent(Root.transform);
                _camera = cameraObject.GetComponent<Camera>(); _camera.transform.position = new Vector3(0, 0, -10);
                _camera.orthographic = true; _camera.orthographicSize = 5; _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(.035f, .045f, .06f);
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    _render = new RenderTexture(1000, 560, 24); _render.Create(); _camera.targetTexture = _render;
                }
                Left = CreateTile(Root.transform, _camera, Authority.PlayerBoard.GetOccupant(new TileCoord(0, 0)), -220);
                Right = CreateTile(Root.transform, _camera, Authority.AIBoard.GetOccupant(new TileCoord(0, 0)), 220);
                Canvas.ForceUpdateCanvases();
            }
            
            internal void Present(TileTextView view, OccupantState occupant, bool rebindCard = false)
            {
                view.GetComponent<TileView>().Present(occupant);
                if (rebindCard && occupant != null) BoardAnimationControllerTestUtility.Bind(view, occupant.CardId);
                // No SetTile/Refresh here: the real LateUpdate must observe the live occupant.
            }
            internal string Truth() => string.Join("|", new[] {
                LeftUnit?.CurrentHp.ToString(), LeftUnit?.MaxHp.ToString(), LeftUnit?.RemainingAttacksThisTurn.ToString(),
                RightUnit?.CurrentHp.ToString(), RightUnit?.MaxHp.ToString(), RightUnit?.RemainingAttacksThisTurn.ToString(),
                ResourceTruth(Authority.Player.Resources), ResourceTruth(Authority.AI.Resources) });
            internal void Capture(string filename)
            {
                if (_render == null) return;
                Canvas.ForceUpdateCanvases(); _camera.Render();
                var old = RenderTexture.active; var texture = new Texture2D(_render.width, _render.height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = _render; texture.ReadPixels(new Rect(0, 0, _render.width, _render.height), 0, 0); texture.Apply();
                    var directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Captures/StunFreezeVfx_20261001"));
                    Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, filename), texture.EncodeToPNG());
                }
                finally { RenderTexture.active = old; Object.DestroyImmediate(texture); }
            }
            public void Dispose()
            {
                Object.DestroyImmediate(Root);
                if (_render != null) { _render.Release(); Object.DestroyImmediate(_render); }
            }
        }

        private static TileTextView CreateTile(Transform fixture, Camera camera, OccupantState occupant, float x)
        {
            Assert.That(occupant, Is.Not.Null);
            var shell = new GameObject("StunFreezeTile-" + occupant.OwnerId); shell.SetActive(false); shell.transform.SetParent(fixture);
            var tile = shell.AddComponent<TileView>(); tile.Configure(occupant.OwnerId, 0, 0); tile.Present(occupant);
            var view = shell.AddComponent<TileTextView>();
            var canvasObject = new GameObject("TileCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(shell.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            var label = new GameObject("TileText", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(canvasObject.transform, false);
            var text = label.GetComponent<TextMeshProUGUI>(); text.enabled = false;
            text.rectTransform.anchoredPosition = new Vector2(x, -30); text.rectTransform.sizeDelta = new Vector2(96, 96);
            var visual = new GameObject("OccupantVisual", typeof(RectTransform), typeof(Image), typeof(Animator));
            visual.transform.SetParent(canvasObject.transform, false);
            Set(view, "_tileView", tile); Set(view, "_text", text); Set(view, "_showOccupantStats", false);
            Set(view, "_showHuanShuStatusIcon", false); Set(view, "_useTileImageClickSurface", false);
            Set(view, "_occupantVisualRoot", visual.GetComponent<RectTransform>());
            Set(view, "_occupantVisualImage", visual.GetComponent<Image>()); Set(view, "_occupantVisualAnimator", visual.GetComponent<Animator>());
            Set(view, "_occupantVisualSize", new Vector2(340, 340)); Set(view, "_occupantVisualAnchoredPosition", new Vector2(x, 0));
            BoardAnimationControllerTestUtility.Bind(view, occupant.CardId);
            shell.SetActive(true); view.SetTile(occupant.OwnerId.ToString(), occupant.CardId, true);
            return view;
        }

        private static BattleState CreateState()
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.Player));
            state.Player.Resources.Add(new ResourceSet(2, 3, 4, 5)); state.AI.Resources.Add(new ResourceSet(3, 2, 5, 4));
            state.PlayerBoard.Place(new TileCoord(0, 0), Unit(PlayerId.Player, "ManaCannon", "drained-player"));
            state.AIBoard.Place(new TileCoord(0, 0), Unit(PlayerId.AI, "GaebangChief", "drained-ai"));
            return state;
        }
        private static UnitState Unit(PlayerId owner, string card, string runtimeId, bool hiding = false) =>
            new UnitState(runtimeId, card, owner, new TileCoord(0, 0), card == "ManaCannon" ? AttackType.Ranged : AttackType.Melee,
                card == "ManaCannon" ? 50 : 10, 100, true, false, 0, damageType: card == "ManaCannon" ? DamageType.Magic : DamageType.Physical,
                hasHiding: hiding);
        private static string ResourceTruth(ResourceSet r) => r.Mana + "," + r.Qi + "," + r.Power + "," + r.Gold;

        private readonly struct NativePose
        {
            internal readonly int State;
            internal readonly float Time;
            internal readonly Sprite ImageSprite, RendererSprite;
            internal NativePose(TileTextView view)
            {
                var state = AnimatorOf(view).GetCurrentAnimatorStateInfo(0);
                State = state.fullPathHash; Time = state.normalizedTime; ImageSprite = Visual(view).sprite;
                RendererSprite = ((SpriteRenderer)Get(view, "_occupantVisualSpriteRenderer"))?.sprite;
            }
        }
        private static void AssertPose(TileTextView view, NativePose expected)
        {
            var actual = new NativePose(view);
            Assert.That(actual.State, Is.EqualTo(expected.State), "A frozen body keeps its native state.");
            Assert.That(actual.Time, Is.EqualTo(expected.Time).Within(.000001f), "A frozen body keeps its exact native playback position.");
            Assert.That(actual.ImageSprite, Is.SameAs(expected.ImageSprite));
            Assert.That(actual.RendererSprite, Is.SameAs(expected.RendererSprite));
        }
        private static void AssertNativeState(TileTextView view, string state) =>
            Assert.That(AnimatorOf(view).GetCurrentAnimatorStateInfo(0).IsName(state), Is.True, state);
        private static Animator AnimatorOf(TileTextView view) => Visual(view).GetComponent<Animator>();
        private static Image Visual(TileTextView view) => view.GetComponentsInChildren<Image>(true).Single(i => i.name == "OccupantVisual");
        private static Image[] Overlays(TileTextView view, string name) => view.GetComponentsInChildren<Image>(true).Where(i => i.name == name).ToArray();
        private static Image Overlay(TileTextView view, string name) => Overlays(view, name).SingleOrDefault();
        private static MonoBehaviour Effect(TileTextView view) => view.GetComponents<MonoBehaviour>().Single(c => c != null && c.GetType() == EffectType);
        private static bool Visible(Image image) => image != null && image.enabled && image.gameObject.activeInHierarchy && image.sprite != null && image.color.a > .01f;
        private static IEnumerator WaitVisible(TileTextView view, string name)
        {
            yield return null; yield return null;
            var deadline = Time.realtimeSinceStartup + 3f;
            while (!Visible(Overlay(view, name)) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Visible(Overlay(view, name)), Is.True, "Expected visible native status " + name);
        }
        private static object Get(object target, string field) => target.GetType().GetField(field, Hidden).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);
        private static void Invoke(object target, string method, params object[] arguments) =>
            target.GetType().GetMethod(method, Hidden).Invoke(target, arguments);
        private static void Sync(TileTextView view) => Invoke(view, "SyncAnimatedVisualSprite", false);

        private static Rect VisibleDrawRect(Image image)
        {
            var sprite = image.sprite; Assert.That(sprite, Is.Not.Null);
            if (!AlphaRects.TryGetValue(sprite, out var pixels))
            {
                // Decode a read-only copy of the current PNG. Never change protected unit import settings.
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.That(texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite))), Is.True);
                    var colors = texture.GetPixels32(); var cell = sprite.rect;
                    SourceSizes[sprite] = new Vector2Int(texture.width, texture.height);
                    var scaleX = texture.width / (double)sprite.texture.width;
                    var scaleY = texture.height / (double)sprite.texture.height;
                    var x0 = Math.Max(0, (int)Math.Round(cell.xMin * scaleX));
                    var y0 = Math.Max(0, (int)Math.Round(cell.yMin * scaleY));
                    var x1 = Math.Min(texture.width, (int)Math.Round(cell.xMax * scaleX));
                    var y1 = Math.Min(texture.height, (int)Math.Round(cell.yMax * scaleY));
                    var width = x1 - x0; var height = y1 - y0;
                    Assert.That(width, Is.GreaterThan(0)); Assert.That(height, Is.GreaterThan(0));
                    var minX = width; var minY = height; var maxX = -1; var maxY = -1;
                    for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                    {
                        if (colors[(y0 + y) * texture.width + x0 + x].a < 16) continue;
                        minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                    }
                    Assert.That(maxX, Is.GreaterThanOrEqualTo(minX), "Visible native alpha is required.");
                    pixels = new Rect(minX / (float)width, minY / (float)height,
                        (maxX + 1 - minX) / (float)width, (maxY + 1 - minY) / (float)height);
                    AlphaRects[sprite] = pixels;
                }
                finally { Object.DestroyImmediate(texture); }
            }
            var draw = image.GetPixelAdjustedRect();
            if (image.preserveAspect)
            {
                var scale = Mathf.Min(draw.width / sprite.rect.width, draw.height / sprite.rect.height);
                var size = sprite.rect.size * scale; draw = new Rect(draw.center - size * .5f, size);
            }
            return new Rect(draw.min + Vector2.Scale(pixels.min, draw.size), Vector2.Scale(pixels.size, draw.size));
        }
        private static Rect InBodySpace(Image overlay, Image body)
        {
            var rect = VisibleDrawRect(overlay); var points = new[] {
                new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax),
                new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax) };
            var locals = points.Select(p => body.rectTransform.InverseTransformPoint(overlay.rectTransform.TransformPoint(p))).ToArray();
            return Rect.MinMaxRect(locals.Min(p => p.x), locals.Min(p => p.y), locals.Max(p => p.x), locals.Max(p => p.y));
        }
        [Serializable]
        private sealed class LayoutEvidence
        {
            public string CardId, Owner, VisualKey, SpriteName, BodyShader;
            public Vector2Int OriginalPngSize, ImportedTextureSize;
            public Rect ImportedSpriteRect, OriginalNormalizedAlpha, ManifestNormalizedAlpha;
            public Rect BodyVisibleRect, StarsVisibleRect, IceVisibleRect;
            public float BodyAnimatorSpeed;
        }
        private static void WriteLayoutEvidence(TileTextView view)
        {
            var body = Visual(view); var sprite = body.sprite;
            var visibleBody = VisibleDrawRect(body);
            var key = (string)Get(view, "_lastVisualControllerName");
            Assert.That(key, Is.EqualTo(AnimatorOf(view).runtimeAnimatorController.name),
                "The cached visual key must correspond to the actual native controller.");
            var asset = Resources.Load<StatusSpriteBoundsAsset>("Project333/StatusEffects/BodyBounds");
            var entry = asset.Entries.FirstOrDefault(e => e.VisualKey == key &&
                e.TextureName == sprite.texture.name && e.SpriteName == sprite.name && e.SpriteRect == sprite.rect);
            Assert.That(entry, Is.Not.Null, "Current displayed native sprite has authored bounds.");
            var occupant = view.GetComponent<TileView>().CurrentOccupant;
            var evidence = new LayoutEvidence
            {
                CardId = occupant.CardId, Owner = occupant.OwnerId.ToString(), VisualKey = key,
                SpriteName = sprite.name, BodyShader = body.material == null ? null : body.material.shader.name,
                OriginalPngSize = SourceSizes[sprite],
                ImportedTextureSize = new Vector2Int(sprite.texture.width, sprite.texture.height),
                ImportedSpriteRect = sprite.rect, OriginalNormalizedAlpha = AlphaRects[sprite],
                ManifestNormalizedAlpha = entry.NormalizedBounds, BodyVisibleRect = visibleBody,
                StarsVisibleRect = InBodySpace(Overlay(view, StarName), body),
                IceVisibleRect = InBodySpace(Overlay(view, IceName), body),
                BodyAnimatorSpeed = AnimatorOf(view).speed
            };
            var directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                "../Captures/StunFreezeVfx_20261001"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "layout-" + occupant.CardId.ToLowerInvariant() + "-" +
                occupant.OwnerId.ToString().ToLowerInvariant() + ".json"), JsonUtility.ToJson(evidence, true));
        }

        private static void AssertStatusPlacement(TileTextView view)
        {
            var body = Visual(view); var visibleBody = VisibleDrawRect(body);
            var stars = InBodySpace(Overlay(view, StarName), body); var ice = InBodySpace(Overlay(view, IceName), body);
            Assert.That(stars.yMin, Is.GreaterThan(visibleBody.yMax - visibleBody.height * .1f), "Visible stars orbit above the visible head, rather than the transparent cell top or torso.");
            Assert.That(Mathf.Abs(stars.center.x - visibleBody.center.x), Is.LessThan(visibleBody.width * .4f));
            Assert.That(stars.width, Is.InRange(visibleBody.width * .15f, visibleBody.width * 1.5f));
            Assert.That(ice.width, Is.InRange(visibleBody.width * .75f, visibleBody.width * 1.5f));
            Assert.That(ice.height, Is.InRange(visibleBody.height * .75f, visibleBody.height * 1.5f));
            Assert.That(Mathf.Abs(ice.center.x - visibleBody.center.x), Is.LessThan(visibleBody.width * .1f));
            Assert.That(Mathf.Abs(ice.center.y - visibleBody.center.y), Is.LessThan(visibleBody.height * .1f));
            Assert.That(ice.yMin, Is.GreaterThan(visibleBody.yMin - visibleBody.height * .15f), "Ice encloses the body without a large effect below the feet.");
        }
    }
}

