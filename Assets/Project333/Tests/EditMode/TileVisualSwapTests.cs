using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class TileVisualSwapTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(false)]
        [TestCase(true)]
        public void MasterElfSwap_PreservesIncomingArtworkAcrossRepeatedSwaps(bool onlineProjection)
        {
            using (var f = new Fixture(onlineProjection))
            {
                f.Present();
                f.AssertArtwork();
                for (var n = 0; n < 6; n++)
                {
                    new MoveService().Move(f.State, PlayerId.AI, f.Front, f.Back);
                    f.Present();
                    f.AssertArtwork();
                    f.Present();
                    f.AssertArtwork();
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MasterTileClearedBeforeElfEnters_UsesElfArtwork(bool onlineProjection)
        {
            using (var f = new Fixture(onlineProjection))
            {
                f.Present();
                new MoveService().Move(f.State, PlayerId.AI, f.Back, f.Empty);
                f.Present();
                new MoveService().Move(f.State, PlayerId.AI, f.Front, f.Back);
                f.Present();
                f.AssertArtwork();
            }
        }

        [UnityTest]
        public IEnumerator MasterElfSwap_StaysCorrectWhileNativeAnimatorAdvances()
        {
            using (var f = new Fixture(true))
            {
                f.Present();
                for (var n = 0; n < 6; n++)
                {
                    new MoveService().Move(f.State, PlayerId.AI, f.Front, f.Back);
                    f.Present();
                    for (var frame = 0; frame < 4; frame++)
                    {
                        yield return null;
                        f.AssertArtwork();
                    }
                }
            }
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly TileCoord Front = new TileCoord(4, 0), Back = new TileCoord(4, 1), Empty = new TileCoord(3, 0);
            internal readonly BattleState State;
            private readonly bool _online;
            private readonly GameObject _root;
            private readonly CardDefinitionCatalogAsset _catalog;
            private readonly CardDefinitionAsset _elf, _master;
            private readonly List<TileTextView> _views = new List<TileTextView>();
            internal Fixture(bool online)
            {
                _online = online;
                State = new BattleSetupService().CreateInitialState(new BattleSetupRequest(
                    Enumerable.Range(0, 33).Select(i => "P"+i).ToArray(), Enumerable.Range(0, 33).Select(i => "A"+i).ToArray(), PlayerId.Player));
                new MoveService().Move(State, PlayerId.AI, new TileCoord(2, 1), Back);
                var elf = new UnitState("elf-swap-regression", "ElfElder", PlayerId.AI, Front, AttackType.Ranged, 30, 22, true, false, 0);
                State.AIBoard.Place(Front, elf);
                _elf = AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>("Assets/Project333/ScriptableObjects/StarterTen/Cards/ElfElder.asset");
                _master = AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>("Assets/Project333/Resources/Project333/SpecialCards/MasterCard.asset");
                Assert.That(_elf, Is.Not.Null); Assert.That(_master, Is.Not.Null);
                _catalog = ScriptableObject.CreateInstance<CardDefinitionCatalogAsset>();
                _catalog.ConfigureForTests(new[] { _elf, _master });
                _root = new GameObject("SwapFixture");
                var host = new GameObject("InactiveCatalogHost"); host.SetActive(false); host.transform.SetParent(_root.transform);
                var bootstrapper = host.AddComponent<BattleBootstrapper>(); Set(bootstrapper, "_cardCatalogAsset", _catalog);
                foreach (var coord in new[] { Front, Back, Empty })
                {
                    var shell = new GameObject("Tile-"+coord); shell.SetActive(false); shell.transform.SetParent(_root.transform);
                    var tile = shell.AddComponent<TileView>(); tile.Configure(PlayerId.AI, coord.Column, coord.Row);
                    var view = shell.AddComponent<TileTextView>();
                    var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)); canvas.transform.SetParent(shell.transform, false);
                    canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                    var label = new GameObject("TileText", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(canvas.transform, false);
                    var visual = new GameObject("OccupantVisual", typeof(RectTransform), typeof(Image), typeof(Animator), typeof(SpriteRenderer));
                    visual.transform.SetParent(canvas.transform, false);
                    Set(view, "_tileView", tile); Set(view, "_text", label.GetComponent<TMP_Text>());
                    Set(view, "_battleBootstrapper", bootstrapper); Set(view, "_showOccupantStats", false);
                    Set(view, "_showHuanShuStatusIcon", false); Set(view, "_useTileImageClickSurface", false);
                    Set(view, "_occupantVisualRoot", visual.GetComponent<RectTransform>());
                    Set(view, "_occupantVisualImage", visual.GetComponent<Image>()); Set(view, "_occupantVisualAnimator", visual.GetComponent<Animator>());
                    Set(view, "_occupantVisualSpriteRenderer", visual.GetComponent<SpriteRenderer>());
                    shell.SetActive(true); _views.Add(view);
                }
            }
            internal void Present()
            {
                var displayed = _online ? BattleStateViewProjector.CreateLocalPerspectiveState(new BattleStateViewFactory().CreateForPlayer(State, "swap-fixture", PlayerId.Player)) : State;
                foreach (var view in _views)
                {
                    var tile = view.GetComponent<TileView>(); var occupant = displayed.AIBoard.GetOccupant(tile.Coord);
                    tile.Present(occupant); view.SetTile(tile.Title, tile.Content, tile.IsOccupied);
                    typeof(TileTextView).GetMethod("LateUpdate", Hidden).Invoke(view, null);
                }
            }
            internal void AssertArtwork()
            {
                foreach (var view in _views)
                {
                    var occupant = view.GetComponent<TileView>().CurrentOccupant;
                    if (occupant == null) continue;
                    var image = (Image)typeof(TileTextView).GetField("_occupantVisualImage", Hidden).GetValue(view);
                    Assert.That(view.VisualRuntimeId, Is.EqualTo(occupant.RuntimeId));
                    if (occupant.CardId == "ElfElder")
                        Assert.That(image.sprite, Is.EqualTo(_elf.BoardSprite),
                            "Expected elf: " + AssetDatabase.GetAssetPath(_elf.BoardSprite) +
                            "; rendered: " + AssetDatabase.GetAssetPath(image.sprite) +
                            "; runtime: " + occupant.RuntimeId);
                    else
                        Assert.That(image.sprite, Is.Not.EqualTo(_elf.BoardSprite), "Master keeps its artwork");
                }
            }
            public void Dispose() { Object.DestroyImmediate(_root); Object.DestroyImmediate(_catalog); }
        }
        private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Hidden).SetValue(instance, value);
    }
}
