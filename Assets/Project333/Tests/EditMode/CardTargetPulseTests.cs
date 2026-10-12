using System;
using System.IO;
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
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class CardTargetPulseTests
    {
        private GameObject _root;
        private BattlePlayerInputController _input;
        private BattleBootstrapper _bootstrapper;
        private BattleState _state;
        private TileView[] _own, _enemy;

        [SetUp] public void SetUp()
        {
            _root = new GameObject("TargetPulseTest");
            _input = _root.AddComponent<BattlePlayerInputController>();
            _bootstrapper = _root.AddComponent<BattleBootstrapper>();
            var board = _root.AddComponent<BoardPresenter>();
            _own = Tiles(PlayerId.Player); _enemy = Tiles(PlayerId.AI);
            Set(board, "_playerTileViews", _own); Set(board, "_aiTileViews", _enemy);
            var flow = new BattleFlowController();
            var deck = Enumerable.Repeat("sample", 33).ToArray();
            flow.StartBattle(new BattleSetupRequest(deck, deck, PlayerId.Player));
            flow.PassMulligan(PlayerId.Player); flow.ResolveTurnStart();
            Set(_bootstrapper, "_battleFlowController", flow);
            Set(_bootstrapper, "_battleGateway", new LocalBattleGateway(flow));
            Set(_input, "_battleBootstrapper", _bootstrapper); Set(_input, "_boardPresenter", board);
            _state = _bootstrapper.CurrentBattleState;
        }

        [TearDown] public void TearDown() { Object.DestroyImmediate(_root); }

        [TestCase(false)] [TestCase(true)]
        public void AffordableSummon_OnlyEmptyFriendlyTiles_AndGoldSubstitution(bool building)
        {
            var cost = new ResourceSet(3, 0, 0, 0);
            _state.Player.Resources.Add(new ResourceSet(0, 0, 0, 3));
            CardDefinition card = building
                ? new BuildingCardDefinition("card", "Building", cost, false, 0, 10)
                : new UnitCardDefinition("card", "Unit", cost, AttackType.Melee, 2, 10, true, false, 0);
            Select(card);
            Assert.That(_own.Count(t => t.HighlightState == BattleHighlightState.Playable), Is.EqualTo(9));
            Assert.That(_own.Single(t => t.Coord == new TileCoord(2, 1)).HighlightState, Is.EqualTo(BattleHighlightState.None));
            Assert.That(_enemy.All(t => t.HighlightState == BattleHighlightState.None), Is.True);
        }

        [TestCase("resources")] [TestCase("deselect")] [TestCase("removed")]
        [TestCase("busy")] [TestCase("opponent")] [TestCase("phase")]
        public void StaleHintsDisappear(string reason)
        {
            var card = new UnitCardDefinition("card", "Unit", new ResourceSet(0, 0, 0, 1), AttackType.Melee, 2, 10, true, false, 0);
            _state.Player.Resources.Add(new ResourceSet(0, 0, 0, 1));
            Select(card);
            Assert.That(_own.Any(t => t.HighlightState == BattleHighlightState.Playable), Is.True);
            switch (reason)
            {
                case "resources": _state.Player.Resources.Spend(_state.Player.Resources.Clone()); break;
                case "deselect": Set(_input, "_selectedCardId", string.Empty); break;
                case "removed": _state.Player.Hand.Remove("card"); break;
                case "busy": Set(_bootstrapper, "_isBusy", true); break;
                case "opponent": _state.StartNextTurn(PlayerId.AI); break;
                case "phase": _state.SetPhase(PhaseType.TurnEnd); break;
            }
            _input.RefreshHighlights();
            Assert.That(_own.Concat(_enemy).All(t => t.HighlightState == BattleHighlightState.None), Is.True);
        }

        [Test]
        public void DamageSpell_HiddenEnemyAndSealedTargetsExcluded_FriendlyHiddenAllowed()
        {
            AddUnit(PlayerId.AI, 0, true);
            AddUnit(PlayerId.Player, 0, true);
            var sealedUnit = AddUnit(PlayerId.AI, 1, false);
            sealedUnit.EnterSealbound(2);
            Select(new DamageSpellCardDefinition("card", "Damage", new ResourceSet(), 4));
            Assert.That(_own[0].HighlightState, Is.EqualTo(BattleHighlightState.SpellTarget));
            Assert.That(_enemy[0].HighlightState, Is.EqualTo(BattleHighlightState.None));
            Assert.That(_enemy[1].HighlightState, Is.EqualTo(BattleHighlightState.None));
            Assert.That(_own.Concat(_enemy).Count(t => t.HighlightState == BattleHighlightState.SpellTarget), Is.EqualTo(3));
        }

        [TestCase("firewall", 20)] [TestCase("biochemical_bomb", 10)]
        [TestCase("magic_amplification_circle", 0)] [TestCase("nuclear", 0)] [TestCase("earthquake", 10)] [TestCase("meteor", 10)]
        public void SpellHints_MatchTargetableArea_WithoutInventingTargetsForUntargetedSpells(string effect, int count)
        {
            Select(new ScriptedSpellCardDefinition("card", "Spell", new ResourceSet(), effect));
            Assert.That(_own.Concat(_enemy).Count(t => t.HighlightState == BattleHighlightState.SpellTarget), Is.EqualTo(count));
        }

        [Test]
        public void RealScene_TileTextViewCreatesAndHidesNonBlockingPulse()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Battle_VSlice.unity");
            try
            {
                var tile = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TileView>(true)).First();
                var view = tile.GetComponent<TileTextView>();
                tile.SetHighlight(BattleHighlightState.Playable); Call(view, "RefreshTileBackgroundHighlight");
                var pulse = view.ActiveEffectButtonAnchor.GetComponentInChildren<TileTargetPulse>();
                Assert.That(pulse, Is.Not.Null);
                var image = pulse.GetComponent<Image>();
                Assert.That(image.enabled, Is.True); Assert.That(image.raycastTarget, Is.False);
                tile.SetHighlight(BattleHighlightState.None); Call(view, "RefreshTileBackgroundHighlight");
                Assert.That(image.enabled, Is.False);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void WhitePulse_RenderReturnsToOriginalAndPreservesArtwork()
        {
            var root = new GameObject("PulseRender", typeof(RectTransform), typeof(Canvas));
            var camObject = new GameObject("Camera", typeof(Camera));
            var camera = camObject.GetComponent<Camera>();
            var rt = new RenderTexture(800, 400, 24);
            var previous = RenderTexture.active;
            Texture2D shot = null;
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f,.08f,.12f);
                camera.transform.position = new Vector3(0,0,-10); camera.orthographic = true; camera.targetTexture = rt;
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project333/Art/Tile/타일3.png");
                Assert.That(sprite, Is.Not.Null);
                for (int i = 0; i < 2; i++)
                {
                    var tile = new GameObject("Tile", typeof(RectTransform), typeof(Image)); tile.transform.SetParent(root.transform, false);
                    var rect = (RectTransform)tile.transform; rect.sizeDelta = new Vector2(320,220); rect.anchoredPosition = new Vector2(i == 0 ? -190 : 190, 0);
                    var image = tile.GetComponent<Image>(); image.sprite = sprite;
                    var pulse = TileTargetPulse.Create(image);
                    pulse.Refresh(image, true, .6f);
                    Assert.That(pulse.GetComponent<Image>().color.a, Is.EqualTo(.65f).Within(.001));
                    pulse.Refresh(image, true, i == 0 ? 1.2f : .6f);
                    Assert.That(image.sprite, Is.SameAs(sprite)); Assert.That(image.color, Is.EqualTo(Color.white));
                }
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
                shot = new Texture2D(800,400,TextureFormat.RGBA32,false); shot.ReadPixels(new Rect(0,0,800,400),0,0); shot.Apply();
                var normal = shot.GetPixel(210,200); var peak = shot.GetPixel(590,200);
                Assert.That(peak.r, Is.GreaterThan(normal.r + .2f));
                Assert.That(peak.g, Is.GreaterThan(normal.g + .2f));
                var path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../TempBuild/CardTargetPulse_20260929"));
                Directory.CreateDirectory(path); File.WriteAllBytes(Path.Combine(path, "preview.png"), shot.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; Object.DestroyImmediate(root); Object.DestroyImmediate(camObject); Object.DestroyImmediate(rt); if (shot != null) Object.DestroyImmediate(shot); }
        }

        private void Select(CardDefinition card)
        {
            Set(_bootstrapper, "_cardDefinitionProvider", new InMemoryCardDefinitionProvider(new[] { card }));
            _state.Player.Hand.Add(card.CardId); Set(_input, "_selectedCardId", card.CardId); _input.RefreshHighlights();
        }
        private UnitState AddUnit(PlayerId owner, int column, bool hiding)
        {
            var unit = new UnitState("unit"+owner+column, "unit", owner, new TileCoord(column,0), AttackType.Melee, 2, 10, true, false, 0, hasHiding: hiding);
            (owner == PlayerId.Player ? _state.PlayerBoard : _state.AIBoard).Place(unit.Position, unit); return unit;
        }
        private TileView[] Tiles(PlayerId owner) => Enumerable.Range(0,10).Select(i =>
        {
            var go = new GameObject("Tile"); go.transform.SetParent(_root.transform);
            var tile = go.AddComponent<TileView>(); tile.Configure(owner, i % 5, i / 5); return tile;
        }).ToArray();
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target,value);
        private static void Call(object target, string method) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target,null);
    }
}
