using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Presentation.Battle;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class OccupantAttackReadyIndicatorTests
    {
        [UnityTearDown]
        public IEnumerator ExitPlay()
        {
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DotTracksRealBoardBindingAttackTurnSwapAndRemoval()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                fixture.Present(); yield return null;
                var dot = fixture.Dot;
                Assert.That(dot, Is.Not.Null);
                Assert.That(dot.gameObject.activeInHierarchy, Is.True);
                Assert.That(dot.raycastTarget, Is.False);
                Assert.That(dot.color.g, Is.GreaterThan(dot.color.r));
                Assert.That(dot.sprite.texture.GetPixel(4, 4).a, Is.GreaterThan(.9f));
                Assert.That(dot.sprite.texture.GetPixel(0, 0).a, Is.LessThan(.1f));
                var enemyDot = fixture.EnemyView.GetComponentsInChildren<Image>(true).Single(i => i.name == "AttackReadyIndicator");
                Assert.That(enemyDot.gameObject.activeSelf, Is.False);
                var background = dot.transform.parent.GetComponent<Image>();
                var normalBarColor = background.color;
                fixture.Unit.IsDrained = true;
                fixture.Present(); yield return null;
                Assert.That(dot.gameObject.activeSelf, Is.False);
                Assert.That(background.color, Is.Not.EqualTo(normalBarColor));
                fixture.Unit.IsDrained = false;
                fixture.Present(); yield return null;
                Assert.That(background.color, Is.EqualTo(normalBarColor));
                Assert.That(dot.gameObject.activeSelf, Is.True);

                new AttackService().Attack(fixture.State, PlayerId.Player, fixture.Unit.Position, fixture.State.AI.Master.Position);
                fixture.Present(); yield return null;
                Assert.That(fixture.Dot, Is.SameAs(dot));
                Assert.That(dot.gameObject.activeInHierarchy, Is.True, "Second attack remains.");
                new AttackService().Attack(fixture.State, PlayerId.Player, fixture.Unit.Position, fixture.State.AI.Master.Position);
                fixture.Present(); yield return null;
                Assert.That(dot.gameObject.activeSelf, Is.False);

                fixture.Unit.RemainingAttacksThisTurn = 2;
                fixture.State.SetActivePlayer(PlayerId.AI);
                fixture.Present(); yield return null;
                Assert.That(dot.gameObject.activeSelf, Is.False);
                fixture.State.SetActivePlayer(PlayerId.Player);
                fixture.Present(); yield return null;
                Assert.That(dot.gameObject.activeSelf, Is.True);

                var replacement = new UnitState("new-summon", "Goblin", PlayerId.Player,
                    fixture.Unit.Position, AttackType.Melee, 3, 10, true, false, 0);
                fixture.View.GetComponent<TileView>().Present(replacement);
                yield return null;
                Assert.That(dot.gameObject.activeSelf, Is.False, "A newly bound summon must not inherit readiness.");
                fixture.Board.Present(null); yield return null;
                Assert.That(dot.gameObject.activeInHierarchy, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator LargerBarFontAndDotSettingsRemainInsideTheBarAndDoNotShiftAfterUse()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var fixture = new Fixture())
            {
                Set(fixture.View, "_occupantStatBarSize", new Vector2(150, 34));
                Set(fixture.View, "_occupantStatFontSize", 24);
                Set(fixture.View, "_occupantStatIconSize", new Vector2(20, 20));
                Set(fixture.View, "_attackReadyIndicatorSize", 10f);
                fixture.Present(); yield return null;
                var dot = fixture.Dot;
                var bar = (RectTransform)dot.transform.parent;
                Assert.That(bar.sizeDelta, Is.EqualTo(new Vector2(150, 34)));
                Assert.That(dot.rectTransform.sizeDelta, Is.EqualTo(new Vector2(10, 10)));
                var text = bar.Find("AttackText").GetComponent<TMP_Text>();
                Assert.That(text.fontSize, Is.EqualTo(24));
                Assert.That(text.rectTransform.sizeDelta.y, Is.GreaterThanOrEqualTo(30));
                AssertCenteredStatGroup(bar, -8f);
                var fixedDotPosition = dot.rectTransform.anchoredPosition;
                fixture.Unit.CurrentHp = 3;
                fixture.Present(); yield return null;
                AssertCenteredStatGroup(bar, -8f);
                Assert.That(dot.rectTransform.anchoredPosition, Is.EqualTo(fixedDotPosition));
                var position = text.rectTransform.anchoredPosition;
                var corners = new Vector3[4]; dot.rectTransform.GetWorldCorners(corners);
                foreach (var world in corners)
                    Assert.That(bar.rect.Contains(bar.InverseTransformPoint(world)), Is.True);
                Assert.That(bar.GetComponent<Canvas>().sortingOrder, Is.EqualTo(4950));
                fixture.Unit.RemainingAttacksThisTurn = 0;
                fixture.Present(); yield return null;
                Assert.That(text.rectTransform.anchoredPosition, Is.EqualTo(position));
                Assert.That(dot.gameObject.activeSelf, Is.False);
                Set(fixture.View, "_showAttackReadyIndicator", false);
                fixture.Unit.RemainingAttacksThisTurn = 1;
                fixture.Present(); yield return null;
                Assert.That(dot.gameObject.activeSelf, Is.False);
            }
        }

        private static void AssertCenteredStatGroup(RectTransform bar, float expectedCenter)
        {
            var heart = bar.Find("HeartIcon").GetComponent<RectTransform>();
            var hp = bar.Find("HpText").GetComponent<TMP_Text>();
            var attack = bar.Find("AttackText").GetComponent<TMP_Text>();
            Assert.That(hp.alignment, Is.EqualTo(TextAlignmentOptions.Center));
            Assert.That(attack.alignment, Is.EqualTo(TextAlignmentOptions.Center));
            var left = heart.anchoredPosition.x - heart.rect.width * heart.localScale.x * 0.5f;
            var right = attack.rectTransform.anchoredPosition.x +
                attack.rectTransform.rect.width * attack.rectTransform.localScale.x * 0.5f;
            Assert.That((left + right) * 0.5f, Is.EqualTo(expectedCenter).Within(0.01f),
                "Both icon/value pairs must be centered together in the space left of the fixed 16px dot lane.");
            Assert.That(left, Is.GreaterThanOrEqualTo(bar.rect.xMin + 3.9f));
            Assert.That(right, Is.LessThanOrEqualTo(bar.rect.xMax - 19.9f));
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly BattleState State;
            internal readonly UnitState Unit;
            internal readonly BoardPresenter Board;
            internal readonly TileTextView View, EnemyView;
            private readonly GameObject _root;
            internal Image Dot => View.GetComponentsInChildren<Image>(true).Single(i => i.name == "AttackReadyIndicator");
            internal Fixture()
            {
                var cards = Enumerable.Range(0, 33).Select(i => "test-" + i).ToArray();
                State = new BattleSetupService().CreateInitialState(new BattleSetupRequest(cards, cards, PlayerId.Player));
                State.RestoreRuntimeState(5, PlayerId.Player, PhaseType.Main);
                Unit = new UnitState("ready", "Archer", PlayerId.Player, new TileCoord(0, 0),
                    AttackType.Ranged, 3, 50, true, false, 0, maxAttacksPerTurn: 2) { HasSummoningSickness = false };
                State.PlayerBoard.Place(Unit.Position, Unit);
                var enemy = new UnitState("enemy", "Archer", PlayerId.AI, new TileCoord(0, 0),
                    AttackType.Ranged, 3, 50, true, false, 0) { HasSummoningSickness = false };
                State.AIBoard.Place(enemy.Position, enemy);
                _root = new GameObject("AttackReadyFixture"); _root.SetActive(false);
                var ownRoot = new GameObject("PlayerBoardRoot"); ownRoot.transform.SetParent(_root.transform);
                var enemyRoot = new GameObject("AIBoardRoot"); enemyRoot.transform.SetParent(_root.transform);
                View = CreateTile(ownRoot.transform, PlayerId.Player);
                EnemyView = CreateTile(enemyRoot.transform, PlayerId.AI);
                Board = _root.AddComponent<BoardPresenter>();
                Set(Board, "_playerBoardRoot", ownRoot.transform); Set(Board, "_aiBoardRoot", enemyRoot.transform);
                Set(Board, "_useResponsiveLayout", false);
                _root.SetActive(true);
            }
            private TileTextView CreateTile(Transform parent, PlayerId owner)
            {
                var shell = new GameObject("Tile"); shell.transform.SetParent(parent);
                var tile = shell.AddComponent<TileView>(); tile.Configure(owner, 0, 0);
                var view = shell.AddComponent<TileTextView>();
                var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
                canvas.transform.SetParent(shell.transform, false);
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var text = new GameObject("TileText", typeof(RectTransform), typeof(TextMeshProUGUI));
                text.transform.SetParent(canvas.transform, false);
                Set(view, "_tileView", tile); Set(view, "_text", text.GetComponent<TMP_Text>());
                Set(view, "_showHuanShuStatusIcon", false);
                var changed = (TileView.TileChangedEvent)typeof(TileView).GetField("_onTileChanged", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tile);
                changed.AddListener(view.SetTile);
                return view;
            }
            internal void Present() => Board.Present(State);
            public void Dispose() => Object.DestroyImmediate(_root);
        }

        private static void Set(object instance, string name, object value) =>
            instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
    }
}
