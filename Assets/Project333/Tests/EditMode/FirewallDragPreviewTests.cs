using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Hand;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class FirewallDragPreviewTests
    {
        private GameObject _root;
        private BattlePlayerInputController _input;
        private BattleBootstrapper _bootstrapper;
        private HandCardView _cardView;
        private BattleState _state;
        private TileView[] _own;
        private TileView[] _enemy;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("FirewallDragPreviewTest");
            _input = _root.AddComponent<BattlePlayerInputController>();
            _bootstrapper = _root.AddComponent<BattleBootstrapper>();
            var board = _root.AddComponent<BoardPresenter>();
            _own = MakeTiles(PlayerId.Player);
            _enemy = MakeTiles(PlayerId.AI);
            Set(board, "_playerTileViews", _own);
            Set(board, "_aiTileViews", _enemy);

            var flow = new BattleFlowController();
            var deck = Enumerable.Repeat("sample", 33).ToArray();
            flow.StartBattle(new BattleSetupRequest(deck, deck, PlayerId.Player));
            flow.PassMulligan(PlayerId.Player);
            flow.ResolveTurnStart();
            Set(_bootstrapper, "_battleFlowController", flow);
            Set(_bootstrapper, "_battleGateway", new LocalBattleGateway(flow));
            Set(_input, "_battleBootstrapper", _bootstrapper);
            Set(_input, "_boardPresenter", board);
            _state = flow.CurrentBattleState;
            _state.Player.Resources.Add(new ResourceSet(20, 0, 0, 0));

            var hand = Child("Hand").AddComponent<HandPresenter>();
            _cardView = Child("FirewallCard").AddComponent<HandCardView>();
            _cardView.Configure(0);
            Set(hand, "_handCardViews", new[] { _cardView });
            hand.SetExpanded(true);
            Set(_input, "_handPresenter", hand);
            _input.RebindViews();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void ActualHandDrag_HighlightsTheWholeSelectedRowIncludingEmptyTiles(
            [Values("native", "json")] string definitionSource,
            [Values(PlayerId.Player, PlayerId.AI)] PlayerId owner,
            [Values(0, 1)] int row,
            [Values(0, 2, 4)] int column)
        {
            BeginDrag(FirewallDefinition(definitionSource));
            Hover(Tile(owner, column, row));

            AssertRow(owner, row);
            Assert.That(_state.PersistentEffects, Is.Empty, "Hovering is presentation only; it does not cast.");
            Assert.That(_state.Player.Hand.Contains("Firewall"), Is.True);
        }

        [Test]
        public void MovingTheDragAcrossRowsAndFieldsClearsThePreviousRow()
        {
            BeginDrag(FirewallDefinition("native"));
            Hover(Tile(PlayerId.Player, 0, 0));
            AssertRow(PlayerId.Player, 0);
            Hover(Tile(PlayerId.Player, 4, 1));
            AssertRow(PlayerId.Player, 1);
            Hover(Tile(PlayerId.AI, 2, 0));
            AssertRow(PlayerId.AI, 0);
        }

        [TestCase("offboard")]
        [TestCase("end")]
        [TestCase("busy")]
        [TestCase("opponent")]
        [TestCase("resources")]
        [TestCase("removed")]
        public void DragPreviewDisappearsWhenThereIsNoCurrentLegalDestination(string reason)
        {
            BeginDrag(FirewallDefinition("native"));
            Hover(Tile(PlayerId.AI, 4, 0));
            AssertRow(PlayerId.AI, 0);
            var outside = new Vector2(-10000, -10000);
            switch (reason)
            {
                case "offboard": _cardView.NotifyDragged(outside); break;
                case "end": _cardView.NotifyDragEnded(outside); break;
                case "busy": Set(_bootstrapper, "_isBusy", true); break;
                case "opponent": _state.StartNextTurn(PlayerId.AI); break;
                case "resources":
                    _state.Player.Resources.Spend(_state.Player.Resources.Clone());
                    _cardView.NotifyDragged(ScreenPoint(Tile(PlayerId.AI, 4, 0)));
                    break;
                case "removed":
                    _state.Player.Hand.Remove("Firewall");
                    _cardView.NotifyDragged(ScreenPoint(Tile(PlayerId.AI, 4, 0)));
                    break;
            }
            _input.RefreshHighlights();

            Assert.That(_own.Concat(_enemy).All(tile => tile.HighlightState == BattleHighlightState.None), Is.True);
            Assert.That(_state.PersistentEffects, Is.Empty);
        }

        private void BeginDrag(CardDefinition definition)
        {
            Set(_bootstrapper, "_cardDefinitionProvider", new InMemoryCardDefinitionProvider(new[] { definition }));
            _state.Player.Hand.Add(definition.CardId);
            _cardView.Present(definition.CardId);
            _cardView.NotifyDragStarted(Vector2.zero);
        }

        private void Hover(TileView tile)
        {
            _cardView.NotifyDragged(ScreenPoint(tile));
            _input.RefreshHighlights();
        }

        private void AssertRow(PlayerId owner, int row)
        {
            var highlighted = _own.Concat(_enemy)
                .Where(tile => tile.HighlightState == BattleHighlightState.DragHoverTarget).ToArray();
            Assert.That(highlighted.Length, Is.EqualTo(5));
            Assert.That(highlighted.All(tile => tile.OwnerId == owner && tile.Coord.Row == row), Is.True);
            Assert.That(highlighted.Select(tile => tile.Coord.Column), Is.EquivalentTo(Enumerable.Range(0, 5)));
            Assert.That(_own.Concat(_enemy).Except(highlighted)
                .All(tile => tile.HighlightState == BattleHighlightState.None), Is.True);
        }

        private static CardDefinition FirewallDefinition(string source)
        {
            if (source == "json")
            {
                var json = Resources.Load<TextAsset>("Project333/Data/cards");
                Assert.That(json, Is.Not.Null);
                return JsonCardDefinitionDatabase.FromJson(json.text).CreateProvider().GetRequired("Firewall");
            }
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>(
                "Assets/Project333/ScriptableObjects/StarterTen/Cards/Firewall.asset");
            Assert.That(asset, Is.Not.Null);
            return asset.ToDefinition();
        }

        private TileView[] MakeTiles(PlayerId owner)
        {
            return Enumerable.Range(0, 10).Select(index =>
            {
                var go = Child("Tile-" + owner + "-" + index, typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.sizeDelta = new Vector2(80, 80);
                rect.position = new Vector3(100 + index % 5 * 100,
                    100 + index / 5 * 100 + (owner == PlayerId.Player ? 0 : 400), 0);
                var tile = go.AddComponent<TileView>();
                tile.Configure(owner, index % 5, index / 5);
                var textView = go.AddComponent<TileTextView>();
                Set(textView, "_interactionSurfaceRoot", rect);
                return tile;
            }).ToArray();
        }

        private GameObject Child(string name, params Type[] components)
        {
            var child = new GameObject(name, components);
            child.transform.SetParent(_root.transform, false);
            return child;
        }

        private TileView Tile(PlayerId owner, int column, int row) =>
            (owner == PlayerId.Player ? _own : _enemy).Single(tile => tile.Coord == new TileCoord(column, row));

        private static Vector2 ScreenPoint(TileView tile) =>
            RectTransformUtility.WorldToScreenPoint(null, tile.transform.position);

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
