using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Online;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    // The authored battle has independent tile canvases, unlike the single-canvas VFX fixture.
    public sealed class MeteorActualBattleSceneTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private string _previousToken;
        private bool _hasPreviousToken;

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (_hasPreviousToken)
            {
                typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null, _previousToken);
                _hasPreviousToken = false;
            }
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AuthoredTileCanvasesShowMeteorFlightAndImpactForBothFields()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Battle_VSlice.unity", OpenSceneMode.Single);
            var bootstrapper = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleBootstrapper>(true)).Single();
            bootstrapper.enabled = false;
            Set(bootstrapper, "_startOnAwake", false);
            Set(bootstrapper, "_startWithDraftBeforeBattle", false);
            Set(bootstrapper, "_autoRunAiTurns", false);
            foreach (var connection in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OnlineBattleConnectionTester>(true)).ToArray())
                Object.DestroyImmediate(connection);
            _previousToken = AccountSessionState.SessionToken;
            _hasPreviousToken = true;
            typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null, "meteor-actual-scene-fixture-no-requests");
            yield return new EnterPlayMode();
            var board = Object.FindFirstObjectByType<BoardPresenter>();
            Assert.That(board, Is.Not.Null);
            board.ApplyResponsiveLayoutNow();
            Canvas.ForceUpdateCanvases();
            yield return null;
            var effect = new GameObject("ActualSceneMeteorFixture").AddComponent<MeteorVfx>();
            try
            {
                var anchors = board.AITileViews.Select(t => t.GetComponent<TileTextView>().ActiveEffectButtonAnchor).ToArray();
                Assert.That(anchors.All(a => a != null && a.gameObject.activeInHierarchy), Is.True);
                Assert.That(anchors.Select(a => a.GetComponentInParent<Canvas>().rootCanvas).Distinct().Count(), Is.GreaterThan(1),
                    "Retain the authored independent tile canvases; do not replace them with a shared synthetic canvas.");
                foreach (var caster in new[] { PlayerId.Player, PlayerId.AI })
                {
                    var duration = (float)Call(effect, "Play", board, caster);
                    var effectRoot = (RectTransform)Get(effect, "_root");
                    Assert.That(duration, Is.EqualTo(MeteorVfx.DurationSeconds), "The actual battle canvas must allow the effect to start. caster=" + caster + " root=" + (effectRoot == null ? "null" : effectRoot.rect.ToString()) + " anchors=" + string.Join(";", anchors.Select(a => a.rect + " world=" + a.position)));
                    var root = (RectTransform)Get(effect, "_root");
                    var targetTiles = caster == PlayerId.Player ? board.AITileViews : board.PlayerTileViews;
                    var fieldCanvas = targetTiles[0].GetComponent<TileTextView>().ActiveEffectButtonAnchor.GetComponentInParent<Canvas>(true).rootCanvas;
                    Assert.That(root.parent, Is.SameAs(fieldCanvas.transform), "Field VFX must use the tile background viewport rather than a unit-sized sorting Canvas.");
                    Assert.That(root.rect.width, Is.EqualTo(((RectTransform)fieldCanvas.transform).rect.width).Within(.01f));
                    var flight = (Image)Get(effect, "_flightImage");
                    var impact = (Image)Get(effect, "_impactImage");
                    Assert.That(root.gameObject.activeInHierarchy && flight.gameObject.activeInHierarchy, Is.True);
                    Assert.That(flight.sprite, Is.Not.Null);
                    Assert.That(root.GetComponent<Canvas>().sortingOrder, Is.EqualTo(4908));
                    Set(effect, "_startedAt", Time.unscaledTime - MeteorVfx.ImpactDelaySeconds - .01f);
                    Assert.That((bool)Call(effect, "TryBeginImpact"), Is.True);
                    Assert.That(impact.gameObject.activeInHierarchy, Is.True);
                    Assert.That(flight.gameObject.activeInHierarchy, Is.False);
                    Assert.That(impact.sprite.name, Does.StartWith("Impact_"));
                    Assert.That(impact.rectTransform.rect.width, Is.GreaterThan(anchors[0].rect.width * 1.75f));
                    Assert.That((float)Get(effect, "_endsAt") - Time.unscaledTime, Is.GreaterThan(1f));
                    Call(effect, "Cancel");
                    Assert.That(root.gameObject.activeInHierarchy, Is.False);
                }
            }
            finally { Object.Destroy(effect.gameObject); }
            yield return new ExitPlayMode();
        }

        private static object Get(object instance, string name) => instance.GetType().GetField(name, Hidden).GetValue(instance);
        private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Hidden).SetValue(instance, value);
        private static object Call(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, Hidden).Invoke(instance, args);
    }
}
