using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Presentation.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class BoardAnimatorControllerPlaybackTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        [UnityTest]
        public IEnumerator AssignedControllerDeterminesBothCharactersFramesAndDuration()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            foreach (var card in new[] { "BlueDragon", "Gwangma" })
            {
                GameObject root = null; AnimationClip replacement = null; AnimatorOverrideController controller = null;
                try
                {
                    var idle = BoardAnimationControllerTestUtility.Frames(BoardAnimationControllerTestUtility.Clip(card, "Idle"));
                    replacement = new AnimationClip { name = "Attack", frameRate = 2f };
                    AnimationUtility.SetObjectReferenceCurve(replacement,
                        EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"),
                        new[] { new ObjectReferenceKeyframe { time = 0, value = idle[0] },
                            new ObjectReferenceKeyframe { time = .5f, value = idle[1] } });
                    controller = new AnimatorOverrideController(BoardAnimationControllerTestUtility.Controller(card));
                    controller["Attack"] = replacement;
                    root = new GameObject("AssignedController_" + card); root.SetActive(false);
                    var tile = root.AddComponent<TileView>(); tile.Configure(PlayerId.Player, 0, 0);
                    tile.Present(new UnitState("controller-test", card, PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 33, 33, true, false, 0));
                    var view = root.AddComponent<TileTextView>();
                    var visual = new GameObject("OccupantVisual", typeof(RectTransform), typeof(Image), typeof(Animator), typeof(SpriteRenderer));
                    visual.transform.SetParent(root.transform, false);
                    Set(view, "_tileView", tile); Set(view, "_showOccupantStats", false); Set(view, "_showHuanShuStatusIcon", false);
                    Set(view, "_useTileImageClickSurface", false);
                    Set(view, "_occupantVisualRoot", visual.GetComponent<RectTransform>());
                    Set(view, "_occupantVisualImage", visual.GetComponent<Image>());
                    Set(view, "_occupantVisualAnimator", visual.GetComponent<Animator>());
                    Set(view, "_occupantVisualSpriteRenderer", visual.GetComponent<SpriteRenderer>());
                    BoardAnimationControllerTestUtility.Bind(view, card, controller);
                    view.SetTile(card, card, true); root.SetActive(true); yield return null;
                    var animator = visual.GetComponent<Animator>();
                    Assert.That(animator.runtimeAnimatorController, Is.SameAs(controller), "Use the controller registered on the card.");
                    var visualRect = visual.GetComponent<RectTransform>();
                    var idleSize = visualRect.sizeDelta;
                    var idlePosition = visualRect.anchoredPosition;
                    foreach (var state in new[] { "Attack", "BeAttacked", "Run", "Death", "Drained", "Idle", "Attack", "Idle" })
                    {
                        typeof(TileTextView).GetMethod("PlayAnimationState", Hidden).Invoke(view, new object[] { state });
                        animator.Update(0);
                        Assert.That(Vector2.Distance(visualRect.sizeDelta, idleSize), Is.LessThan(.001f), card + "/" + state + " must not resize at transition.");
                        Assert.That(Vector2.Distance(visualRect.anchoredPosition, idlePosition), Is.LessThan(.001f), card + "/" + state + " must not move the ground line.");
                        typeof(TileTextView).GetMethod("ApplyVisualLayout", Hidden).Invoke(view, null);
                        Assert.That(Vector2.Distance(visualRect.sizeDelta, idleSize), Is.LessThan(.001f), card + "/" + state + " must retain size after layout refresh.");
                        Assert.That(Vector2.Distance(visualRect.anchoredPosition, idlePosition), Is.LessThan(.001f), card + "/" + state + " must retain its ground line after layout refresh.");
                    }
                    view.PlayAttackAnimation(); animator.Update(0);
                    Sync(view);
                    Assert.That(visual.GetComponent<Image>().sprite, Is.SameAs(idle[0]), "A changed Attack clip must override any former generated attack frames.");
                    Assert.That(view.GetAttackAnimationDurationSeconds(), Is.EqualTo(replacement.length).Within(.0001f));
                    Assert.That(view.GetAttackAnimationDurationSeconds(), Is.GreaterThan(.5f), "Read the edited clip duration instead of the old hard-coded duration.");
                    animator.Update(.6f); Sync(view);
                    Assert.That(visual.GetComponent<Image>().sprite, Is.SameAs(idle[1]));
                    Assert.That(typeof(TileTextView).GetField("_activeSpriteSequence", Hidden).GetValue(view), Is.Null);
                    Assert.That(animator.enabled, Is.True);
                    if (animator.HasState(0, Animator.StringToHash("Drained")))
                    {
                        view.PlayDrainedAnimation(); animator.Update(1.2f); Sync(view);
                        Assert.That(visual.GetComponent<Image>().sprite, Is.SameAs(BoardAnimationControllerTestUtility.Frames(BoardAnimationControllerTestUtility.Clip(card, "Drained"))[0]), "Drained stays on the controller's authored held pose.");
                    }
                    view.PlayIdleAnimation(); animator.Update(0); Sync(view);
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
                }
                finally
                {
                    if (root != null) Object.DestroyImmediate(root);
                    if (controller != null) Object.DestroyImmediate(controller);
                    if (replacement != null) Object.DestroyImmediate(replacement);
                    BoardAnimationControllerTestUtility.Cleanup();
                }
            }
            yield return new ExitPlayMode();
        }
        [UnityTearDown]
        public IEnumerator ExitAfterFailure()
        {
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }
        private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Hidden).SetValue(instance, value);
        private static void Sync(TileTextView view) => typeof(TileTextView).GetMethod("SyncAnimatedVisualSprite", Hidden).Invoke(view, new object[] { true });
    }
}