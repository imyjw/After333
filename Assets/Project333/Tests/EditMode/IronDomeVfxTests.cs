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
    public sealed class IronDomeVfxTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticHidden = BindingFlags.Static | BindingFlags.NonPublic;

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            Time.timeScale = 1f;
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        [TestCase(450f, 0f)]
        [TestCase(-450f, 0f)]
        [TestCase(400f, 310f)]
        [TestCase(-400f, -310f)]
        [TestCase(0f, 600f)]
        [TestCase(0f, -600f)]
        [TestCase(1500f, 600f)]
        [TestCase(-1500f, -600f)]
        public void MissileMovesFromMasterBodyToTargetBodyAndFacesBothOwnersInEveryDirection(float dx, float dy)
        {
            var master = new Rect(-50, -60, 100, 120);
            var attacker = new Rect(dx - 40, dy - 70, 80, 140);
            foreach (var progress in new[] { 0f, .25f, .5f, 1f })
            {
                var args = new object[] { master, attacker, progress, Vector2.zero, 0f, 0f, 0f };
                Assert.That((bool)typeof(IronDomeMissileVfx).GetMethod("TryBuildLayout", StaticHidden).Invoke(null, args), Is.True);
                var actual = (Vector2)args[3]; var angle = (float)args[4];
                Assert.That(Vector2.Distance(actual, Vector2.Lerp(master.center, attacker.center, progress)), Is.LessThan(.001f));
                var direction = (attacker.center - master.center).normalized;
                Assert.That(Vector2.Dot((Vector2)(Quaternion.Euler(0, 0, angle) * Vector3.right), direction), Is.GreaterThan(.999f));
                Assert.That((float)args[5], Is.GreaterThan(40f));
                Assert.That((float)args[6], Is.EqualTo(140f * 2.6f).Within(.001f));
            }
        }

        [Test]
        public void MissileHasTransparentNativeSingleSpriteAndExplosionReusesAllSixteenTimedBombFrames()
        {
            var missile = Resources.Load<Sprite>(IronDomeMissileVfx.MissileResourcePath);
            Assert.That(missile, Is.Not.Null);
            var path = AssetDatabase.GetAssetPath(missile);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False); Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(missile.rect.size, Is.EqualTo(new Vector2(missile.texture.width, missile.texture.height)));
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(source.LoadImage(File.ReadAllBytes(path)), Is.True);
                var corners = new[] { source.GetPixel(0, 0), source.GetPixel(source.width - 1, 0),
                    source.GetPixel(0, source.height - 1), source.GetPixel(source.width - 1, source.height - 1) };
                Assert.That(corners.All(p => p.a < .01f), Is.True);
            }
            finally { Object.DestroyImmediate(source); }
            var frames = Frames(); Assert.That(frames.Length, Is.EqualTo(IronDomeMissileVfx.ExplosionFrameCount));
            for (var index = 0; index < frames.Length; index++)
            {
                Assert.That(frames[index].name, Is.EqualTo("Explosion_" + index.ToString("00")));
                Assert.That(frames[index].rect.size, Is.EqualTo(new Vector2(512, 512)));
                Assert.That(AssetDatabase.GetAssetPath(frames[index]), Is.EqualTo(
                    "Assets/Project333/Resources/Project333/SpellEffects/TimedBomb/Explosion.png"));
            }
        }

        [UnityTest]
        public IEnumerator ActualCanvasRendersMissileFlightAndSingleBodyCenteredExplosionForBothOwners()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            Time.timeScale = 0f;
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            using (var f = new Fixture(true))
            {
                var attackerOwner = owner == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
                var attacker = f.AddAttacker(attackerOwner, new TileCoord(owner == PlayerId.Player ? 4 : 0, 0));
                f.CreateBoard(true); yield return null;
                var master = f.State.GetPlayer(owner).Master;
                var effect = f.GetEffect();
                using (var before = f.Render())
                {
                    Assert.That((float)Invoke(effect, "Play", f.View(owner, master.Position), master.RuntimeId,
                        f.View(attackerOwner, attacker.Position), attacker.RuntimeId), Is.EqualTo(IronDomeMissileVfx.DurationSeconds));
                    var missile = Get<Image>(effect, "_missile"); var explosion = Get<Image>(effect, "_explosion");
                    var start = missile.rectTransform.anchoredPosition;
                    var masterImage = Get<Image>(f.View(owner, master.Position), "_occupantVisualImage");
                    var nativeBody = ReadOpaqueBounds(masterImage.sprite);
                    var sourceBody = Get<Rect[]>(effect, "_bodyBounds")[0];
                    Assert.That(Vector2.Distance(sourceBody.min, nativeBody.min), Is.LessThan(.003f));
                    Assert.That(Vector2.Distance(sourceBody.max, nativeBody.max), Is.LessThan(.003f));
                    var masterDraw = masterImage.GetPixelAdjustedRect();
                    if (masterImage.preserveAspect)
                    {
                        var spriteSize = masterImage.sprite.rect.size;
                        spriteSize *= Mathf.Min(masterDraw.width / spriteSize.x, masterDraw.height / spriteSize.y);
                        masterDraw = new Rect(masterDraw.center - spriteSize * .5f, spriteSize);
                    }
                    var sourceCenter = masterDraw.min + Vector2.Scale(nativeBody.center, masterDraw.size);
                    var overlayRoot = Get<RectTransform>(effect, "_root");
                    var expectedStart = (Vector2)overlayRoot.InverseTransformPoint(masterImage.rectTransform.TransformPoint(sourceCenter));
                    Assert.That(Vector2.Distance(start, expectedStart), Is.LessThan(.1f),
                        "The missile starts at the actual Master pixels, not the empty center of its 512 cell.");
                    Assert.That(Get<Rect[]>(effect, "_projectedBodies")[0].Contains(start), Is.True);
                    using (var launched = f.Render("Launch-" + owner))
                    {
                        Assert.That(f.DifferentPixels(launched.Texture, before.Texture), Is.GreaterThan(200));
                        Sample(effect, IronDomeMissileVfx.FlightDurationSeconds * .5f);
                        Assert.That(Vector2.Distance(start, missile.rectTransform.anchoredPosition), Is.GreaterThan(50f));
                        Assert.That(missile.gameObject.activeInHierarchy, Is.True);
                        Assert.That(explosion.gameObject.activeInHierarchy, Is.False);
                        using (var flight = f.Render("Flight-" + owner))
                        {
                            Assert.That(f.DifferentPixels(flight.Texture, launched.Texture), Is.GreaterThan(300));
                            Sample(effect, IronDomeMissileVfx.ImpactDelaySeconds + .001f);
                            Assert.That(missile.gameObject.activeInHierarchy, Is.False);
                            Assert.That(explosion.gameObject.activeInHierarchy, Is.True);
                            Assert.That(explosion.sprite, Is.SameAs(Frames()[4]));
                            var bodies = Get<Rect[]>(effect, "_projectedBodies");
                            Assert.That(Vector2.Distance(explosion.rectTransform.anchoredPosition, bodies[1].center), Is.LessThan(.01f));
                            Assert.That(explosion.rectTransform.sizeDelta.x, Is.GreaterThan(Mathf.Max(bodies[1].width, bodies[1].height) * 2f));
                            using (var peak = f.Render("Explosion-" + owner))
                                Assert.That(f.DifferentPixels(peak.Texture, flight.Texture), Is.GreaterThan(2500));
                        }
                    }
                    Assert.That((bool)Property(f.View(attackerOwner, attacker.Position), "IsDeathPresentation", Hidden), Is.False,
                        "The VFX never decides death; only the confirmed gateway cue does.");
                    Assert.That(attacker.CurrentHp, Is.EqualTo(50)); Assert.That(master.CurrentHp, Is.EqualTo(333));
                    var overlay = Get<RectTransform>(effect, "_root");
                    Assert.That(overlay.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                    Assert.That(overlay.GetComponent<CanvasGroup>().interactable, Is.False);
                    Assert.That(missile.raycastTarget || explosion.raycastTarget, Is.False);
                    Invoke(effect, "Cancel"); AssertHidden(effect);
                }
            }
            Time.timeScale = 1f; yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SlowFrameHoldsGrowingBurstUntilConfirmedImpactThenShowsWholeExplosionTail()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            using (var f = new Fixture(true))
            {
                var attacker = f.AddAttacker(PlayerId.AI, new TileCoord(4, 1)); f.CreateBoard(); yield return null;
                var effect = f.GetEffect(); var master = f.State.Player.Master;
                Assert.That((float)Invoke(effect, "Play", f.View(PlayerId.Player, master.Position), master.RuntimeId,
                    f.View(PlayerId.AI, attacker.Position), attacker.RuntimeId), Is.GreaterThan(0f));
                Set(effect, "_startedAt", Time.unscaledTime - 2f); Invoke(effect, "LateUpdate");
                var explosion = Get<Image>(effect, "_explosion");
                Assert.That((bool)Property(effect, "IsPlaying", Hidden), Is.True);
                Assert.That((bool)Property(effect, "HasImpacted", Hidden), Is.False);
                Assert.That(explosion.sprite, Is.SameAs(Frames()[3]));
                Assert.That((bool)Invoke(effect, "TryBeginExplosion"), Is.True);
                Assert.That((bool)Property(effect, "HasImpacted", Hidden), Is.True);
                Assert.That(explosion.sprite, Is.SameAs(Frames()[4]));
                Assert.That((float)Property(effect, "RemainingDurationSeconds", Hidden), Is.GreaterThan(.70f));
                for (var index = 4; index < 16; index++)
                {
                    Set(effect, "_impactStartedAt", Time.unscaledTime - ((index - 4 + .1f) / 16f));
                    Set(effect, "_endsAt", Time.unscaledTime + .1f); Invoke(effect, "LateUpdate");
                    Assert.That(explosion.sprite, Is.SameAs(Frames()[index]));
                }
                effect.enabled = false; AssertHidden(effect);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ReplacementSourceOrTargetCancelsFlightAndImpactWithoutMutatingBattle()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            for (var replacedIndex = 0; replacedIndex < 2; replacedIndex++)
            using (var f = new Fixture(true))
            {
                var attacker = f.AddAttacker(PlayerId.AI, new TileCoord(4, 1)); f.CreateBoard(); yield return null;
                var effect = f.GetEffect(); var master = f.State.Player.Master;
                Assert.That((float)Invoke(effect, "Play", f.View(PlayerId.Player, master.Position), master.RuntimeId,
                    f.View(PlayerId.AI, attacker.Position), attacker.RuntimeId), Is.GreaterThan(0f));
                var owner = replacedIndex == 0 ? PlayerId.Player : PlayerId.AI;
                var coord = replacedIndex == 0 ? master.Position : attacker.Position;
                var replacement = new UnitState("replacement-" + replacedIndex, "A-301", owner, coord, AttackType.Ranged, 10, 20, true, false, 0);
                f.Tile(owner, coord).Present(replacement);
                Set(effect, "_startedAt", Time.unscaledTime - 2f);
                Assert.That((bool)Invoke(effect, "TryBeginExplosion"), Is.False); AssertHidden(effect);
                Assert.That(attacker.CurrentHp, Is.EqualTo(50)); Assert.That(master.CurrentHp, Is.EqualTo(333));
            }
            yield return new ExitPlayMode();
        }

        private static Rect ReadOpaqueBounds(Sprite sprite)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(source.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite))), Is.True);
                var x0 = Mathf.RoundToInt(sprite.rect.x); var y0 = Mathf.RoundToInt(sprite.rect.y);
                var width = Mathf.RoundToInt(sprite.rect.width); var height = Mathf.RoundToInt(sprite.rect.height);
                var pixels = source.GetPixels32(); var minimum = new Vector2(width, height); var maximum = new Vector2(-1, -1);
                for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
                    if (pixels[(y0 + y) * source.width + x0 + x].a >= 16)
                    { minimum = Vector2.Min(minimum, new Vector2(x, y)); maximum = Vector2.Max(maximum, new Vector2(x + 1, y + 1)); }
                Assert.That(maximum.x, Is.GreaterThan(minimum.x));
                return Rect.MinMaxRect(minimum.x / width, minimum.y / height, maximum.x / width, maximum.y / height);
            }
            finally { Object.DestroyImmediate(source); }
        }
        private static Sprite[] Frames() => Resources.LoadAll<Sprite>(TimedBombPresentationVfx.ExplosionResourcePath).OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
        private static void Sample(IronDomeMissileVfx effect, float elapsed)
        {
            Set(effect, "_startedAt", Time.unscaledTime - elapsed);
            if (elapsed >= IronDomeMissileVfx.ImpactDelaySeconds)
            {
                Assert.That((bool)Invoke(effect, "TryBeginExplosion"), Is.True);
                Set(effect, "_impactStartedAt", Time.unscaledTime - (elapsed - IronDomeMissileVfx.ImpactDelaySeconds));
                Set(effect, "_endsAt", Time.unscaledTime + (IronDomeMissileVfx.DurationSeconds - elapsed));
            }
            Invoke(effect, "LateUpdate"); Canvas.ForceUpdateCanvases();
        }
        private static void AssertHidden(IronDomeMissileVfx effect)
        {
            Assert.That((bool)Property(effect, "IsPlaying", Hidden), Is.False);
            var root = Get<RectTransform>(effect, "_root"); Assert.That(root == null || !root.gameObject.activeInHierarchy, Is.True);
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
                typeof(AccountSessionState).GetField("<SessionToken>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, "iron-dome-vfx-no-network");
                var deck = Enumerable.Repeat("Goblin", 33).ToArray();
                State = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.Player)); State.SetPhase(PhaseType.Main);
                Root = new GameObject("IronDomeMissileVfxFixture"); Root.SetActive(false);
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
                var rule = IronDomeCardRules.CreateDefinition(); State.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 4, 0));
                State.ArmedTraps.Add(new ArmedTrapState("armed-iron-dome", owner, rule.CardId, rule.TriggerType, rule.EffectType, rule.ActivationCost, 1));
            }
            internal object OnlineRequest(BattleEventDto source, IReadOnlyList<BattleEventDto> events) => Invoke(Bootstrapper, "BuildOnlineImpactAnimationRequest", source?.CardId, source, events);
            internal IronDomeMissileVfx GetEffect()
            {
                var effect = Get<IronDomeMissileVfx>(Bootstrapper, "_ironDomeMissileVfx");
                if (effect == null) { effect = Root.AddComponent<IronDomeMissileVfx>(); Set(Bootstrapper, "_ironDomeMissileVfx", effect); }
                return effect;
            }
            internal Image EffectImage(IronDomeMissileVfx effect)
            {
                return Root.GetComponentsInChildren<Image>(true).FirstOrDefault(image => image.sprite != null && AssetDatabase.GetAssetPath(image.sprite).Contains("/IronDome/"));
            }
            internal TileView Tile(PlayerId owner, TileCoord coord) => (owner == PlayerId.Player ? _board.PlayerTileViews : _board.AITileViews).Single(t => t.Coord == coord);
            internal TileTextView View(PlayerId owner, TileCoord coord) => Tile(owner, coord).GetComponent<TileTextView>();
            internal void CreateBoard(bool render = false)
            {
                var canvas = new GameObject("IronDomeCanvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
                canvas.transform.SetParent(Root.transform, false); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(2340, 1080);
                if (render)
                {
                    Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
                    var cameraHost = new GameObject("IronDomePreviewCamera", typeof(Camera)); cameraHost.transform.SetParent(Root.transform);
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
                        var directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Captures/IronDomeVfx_20261001"));
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
