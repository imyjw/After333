using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Presentation.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class BiochemicalGasVfxTests
    {
        private GameObject _root;
        private TileView[] _player, _enemy;
        private BiochemicalGasVfx _vfx;
        private BattleState _state;
        private BoardPresenter _board;
        [SetUp] public void SetUp()
        {
            _root = new GameObject("GasPreview", typeof(RectTransform), typeof(Canvas));
            _root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _board = _root.AddComponent<BoardPresenter>();
            typeof(BoardPresenter).GetField("_useResponsiveLayout", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_board, false);
            _vfx = _root.AddComponent<BiochemicalGasVfx>();
            _player = Tiles(PlayerId.Player); _enemy = Tiles(PlayerId.AI);
            typeof(BoardPresenter).GetField("_playerTileViews", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_board, _player);
            typeof(BoardPresenter).GetField("_aiTileViews", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_board, _enemy);
            var deck = Enumerable.Repeat("unknown", 33).ToArray();
            _state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.Player));
            _state.SetPhase(PhaseType.Main);
        }
        [TearDown] public void TearDown() { if (_root != null) UnityEngine.Object.DestroyImmediate(_root); }
        private TileView[] Tiles(PlayerId owner)
        {
            var tiles = new TileView[10];
            for (var col = 0; col < 5; col++) for (var row = 0; row < 2; row++)
            {
                var go = new GameObject(owner + "-" + col + "-" + row, typeof(RectTransform), typeof(Image), typeof(TileView));
                go.transform.SetParent(_root.transform, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(-540 + (int)owner * 600 + col * 106, 60 - row * 132);
                rect.sizeDelta = new Vector2(98, 118);
                go.GetComponent<Image>().color = owner == PlayerId.Player ? new Color(.20f,.30f,.39f) : new Color(.36f,.22f,.24f);
                var tile = go.GetComponent<TileView>(); tile.Configure(owner, col, row);
                tiles[col * 2 + row] = tile;
            }
            return tiles;
        }
        private PersistentEffectState Add(int start, PlayerId owner = PlayerId.Player)
        {
            var effect = new PersistentEffectState("BiochemicalBomb", owner, BiochemicalBombRules.EffectId,
                1, "4 starts", new ResourceSet(), remainingTriggers: 4, effectDamage: 25,
                effectDamageType: DamageType.Fixed, targetStartColumn: start);
            _state.PersistentEffects.Add(effect); return effect;
        }
        private void Frame(float time, float delta = 1)
        {
            Canvas.ForceUpdateCanvases();
            typeof(BiochemicalGasVfx).GetMethod("Animate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_vfx, new object[] { time, delta });
            Canvas.ForceUpdateCanvases();
        }
        [TestCase(0)] [TestCase(1)]
        public void BoardIntegration_ShowsExactlyEightTiles_AndNoRaycastBlockers(int start)
        {
            Add(start); _board.Present(_state); Frame(1);
            Assert.That(_player.Count(t => t.GetComponentInChildren<RawImage>() != null), Is.Zero);
            Assert.That(_enemy.Count(t => t.GetComponentInChildren<RawImage>() != null), Is.EqualTo(8));
            foreach (var tile in _enemy)
            {
                Assert.That(BiochemicalGasVfx.CountAt(_state, tile, out _), Is.EqualTo(tile.Coord.Column >= start && tile.Coord.Column < start + 4 ? 1 : 0));
                foreach (var image in tile.GetComponentsInChildren<RawImage>())
                { Assert.That(image.raycastTarget, Is.False); Assert.That(image.texture, Is.Not.Null); Assert.That(image.color.a, Is.GreaterThan(0)); }
            }
            Assert.That(_root.GetComponentsInChildren<GraphicRaycaster>(), Is.Empty);
            Assert.That(_root.GetComponentsInChildren<CanvasGroup>().All(g => !g.blocksRaycasts && !g.interactable), Is.True);
            var first = _enemy.First(t => t.GetComponentInChildren<RawImage>() != null).GetComponentInChildren<RawImage>();
            var previous = first.rectTransform.anchoredPosition; Frame(3);
            Assert.That(first.rectTransform.anchoredPosition, Is.Not.EqualTo(previous));
        }
        [Test]
        public void OverlapIsBounded_ExpirationAndDisableCleanUp()
        {
            var left = Add(0); var right = Add(1);
            for (var i = 0; i < 10; i++) { _board.Present(_state); Frame(i); }
            Assert.That(_root.GetComponentsInChildren<RawImage>().Length, Is.EqualTo(20));
            Assert.That(BiochemicalGasVfx.CountAt(_state, _enemy[2], out _), Is.EqualTo(2));
            left.Expire(); _board.Present(_state); Frame(12);
            Assert.That(_root.GetComponentsInChildren<RawImage>().Length, Is.EqualTo(16));
            right.Expire(); _board.Present(_state); Frame(13);
            Assert.That(_root.GetComponentsInChildren<RawImage>(), Is.Empty);
            Add(0); _board.Present(_state); Frame(14); _vfx.enabled = false;
            // EditMode does not dispatch the runtime OnDisable callback.
            typeof(BiochemicalGasVfx).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_vfx, null);
            Assert.That(_root.GetComponentsInChildren<RawImage>(), Is.Empty);
        }
        [TestCase(PlayerId.Player)] [TestCase(PlayerId.AI)]
        public void ReconnectProjection_RestoresTheCorrectLocalSide(PlayerId viewer)
        {
            Add(1);
            var view = new BattleStateViewFactory().CreateForPlayer(_state, "gas-test", viewer);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(view);
            _board.Present(projected); Frame(2);
            var affected = viewer == PlayerId.Player ? _enemy : _player;
            var other = viewer == PlayerId.Player ? _player : _enemy;
            Assert.That(affected.Count(t => t.GetComponentInChildren<RawImage>() != null), Is.EqualTo(8));
            Assert.That(other.Count(t => t.GetComponentInChildren<RawImage>() != null), Is.Zero);
            _board.Present(null); Frame(3);
            Assert.That(_root.GetComponentsInChildren<RawImage>(), Is.Empty);
        }
        [Test]
        public void RealBattleScene_UsesTileBackgroundAnchors()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Battle_VSlice.unity");
            try
            {
                var board = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BoardPresenter>(true)).First();
                board.RebuildTileRegistry();
                Add(1);
                board.Present(_state);
                foreach (var tile in board.AITileViews)
                {
                    var anchor = tile.GetComponent<TileTextView>().ActiveEffectButtonAnchor;
                    Assert.That(anchor, Is.Not.Null);
                    var gas = anchor.Find("BiochemicalGas");
                    Assert.That(gas != null, Is.EqualTo(tile.Coord.Column >= 1));
                    if (gas == null) continue;
                    Assert.That(gas.GetComponentsInChildren<RawImage>().Length, Is.EqualTo(2));
                    Assert.That(gas.GetComponent<Canvas>().sortingOrder, Is.LessThan(250));
                    Assert.That(gas.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                }
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void RenderPreview()
        {
            Add(0); _board.Present(_state);
            var cameraObject = new GameObject("GasPreviewCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            var rt = new RenderTexture(1280, 720, 24);
            var previous = RenderTexture.active;
            Texture2D capture = null;
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f,.05f,.075f);
                camera.transform.position = new Vector3(0,0,-10); camera.orthographic = true;
                camera.targetTexture = rt;
                var canvas = _root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                Frame(2);
                camera.Render(); RenderTexture.active = rt;
                capture = new Texture2D(1280,720,TextureFormat.RGBA32,false);
                capture.ReadPixels(new Rect(0,0,1280,720),0,0); capture.Apply();
                var output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../TempBuild/BiochemicalGas_20260929"));
                Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output,"preview.png"),capture.EncodeToPNG());
                Assert.That(Resources.Load<Texture2D>(BiochemicalGasVfx.TextureResourcePath).width, Is.LessThanOrEqualTo(512));
            }
            finally { RenderTexture.active = previous; if(capture!=null) UnityEngine.Object.DestroyImmediate(capture);
                camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(cameraObject); }
        }
    }
}
