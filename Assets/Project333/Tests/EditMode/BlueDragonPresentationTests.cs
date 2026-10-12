using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Presentation.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class BlueDragonPresentationTests
    {
        private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string ResourceRoot = "Project333/Presentation/BlueDragon/";

        [TestCase("Idle", 4, 12f, true)]
        [TestCase("Attack", 6, 12f, false)]
        [TestCase("BeAttacked", 6, 15f, false)]
        [TestCase("Run", 6, 12f, true)]
        [TestCase("Death", 6, 8f, false)]
        public void ControllerMotion_UsesCompleteFramesAndAuthoredDuration(string stateName, int count, float fps, bool loop)
        {
            var clip = BoardAnimationControllerTestUtility.Clip("BlueDragon", stateName);
            var frames = BoardAnimationControllerTestUtility.Frames(clip);
            Assert.That(frames.Length, Is.EqualTo(count));
            Assert.That(clip.frameRate, Is.EqualTo(fps));
            Assert.That(clip.isLooping, Is.EqualTo(loop));
            AssertValidFrames(frames, AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Unit/BlueDragon/" + stateName + ".png"));
            foreach (var frame in frames) Assert.That(frame.rect.size, Is.EqualTo(new Vector2(512, 512)));
            Assert.That(frames.Distinct().Count(), Is.EqualTo(count));
            Assert.That(clip.length, Is.EqualTo(count / fps).Within(0.0001f));
        }

        [Test]
        public void Controller_HasFiveMotionSheetsAndDefaultsToIdle()
        {
            var sheets = new HashSet<Texture2D>();
            foreach (var state in new[] { "Idle", "Attack", "BeAttacked", "Run", "Death" })
                sheets.Add(BoardAnimationControllerTestUtility.Frames(BoardAnimationControllerTestUtility.Clip("BlueDragon", state))[0].texture);
            Assert.That(sheets.Count, Is.EqualTo(5));
            Assert.That(BoardAnimationControllerTestUtility.Controller("BlueDragon").layers[0].stateMachine.defaultState.name, Is.EqualTo("Idle"));
        }

        [Test]
        public void DrainedControllerClip_HoldsOriginalIdleFirstFrame()
        {
            var idle = BoardAnimationControllerTestUtility.Frames(BoardAnimationControllerTestUtility.Clip("BlueDragon", "Idle"));
            var drained = BoardAnimationControllerTestUtility.Frames(BoardAnimationControllerTestUtility.Clip("BlueDragon", "Drained"));
            Assert.That(drained.Length, Is.EqualTo(1));
            Assert.That(drained[0], Is.SameAs(idle[0]));
        }
        [TestCase("Attack", 6, 1)]
        [TestCase("BeAttacked", 6, 1)]
        [TestCase("Run", 6, 1)]
        [TestCase("Death", 6, 1)]
        [TestCase("WaterAtlas", 4, 3)]
        public void PresentationAtlas_IsPackagedAndPreservesPixelArt(string resourceName, int columns, int rows)
        {
            var texture = rows == 1 ? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Unit/BlueDragon/" + resourceName + ".png") : Resources.Load<Texture2D>(ResourceRoot + resourceName);
            Assert.That(texture, Is.Not.Null, "Missing packaged resource: " + resourceName);
            Assert.That(texture.width % columns, Is.Zero);
            Assert.That(texture.height % rows, Is.Zero);
            Assert.That(texture.width / columns, Is.GreaterThan(0));
            Assert.That(texture.height / rows, Is.GreaterThan(0));
            if (rows == 1)
            {
                Assert.That(texture.width, Is.EqualTo(3072));
                Assert.That(texture.height, Is.EqualTo(512));
            }
            Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point));
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        }

        [Test]
        public void WaterCannon_HasIndependentTravelAndSplashFrames()
        {
            var texture = Resources.Load<Texture2D>(ResourceRoot + "WaterAtlas");
            var projectile = ReadFrames("ProjectileFrames");
            var impact = ReadFrames("ImpactFrames");
            Assert.That(projectile.Count, Is.EqualTo(4));
            Assert.That(impact.Count, Is.EqualTo(8));
            AssertValidFrames(projectile, texture);
            AssertValidFrames(impact, texture);
            Assert.That(projectile.Concat(impact).Select(frame => frame.rect).Distinct().Count(), Is.EqualTo(12));
            Assert.That(ReadFloat("WindupDuration"), Is.GreaterThan(0f));
            Assert.That(ReadFloat("FlightDuration"), Is.GreaterThan(0f));
            Assert.That(ReadFloat("ImpactDuration"), Is.GreaterThan(0f));
        }

        [TestCase("WaterCannonCharge")]
        [TestCase("WaterCannonLaunch")]
        [TestCase("WaterCannonImpact")]
        [TestCase("DragonHurt")]
        [TestCase("DragonDeath")]
        public void Sound_IsPackagedAndPlayable(string soundName)
        {
            var clip = (AudioClip)AssetsType.GetMethod("GetSound", StaticMembers)
                .Invoke(null, new object[] { soundName });
            Assert.That(clip, Is.Not.Null, soundName);
            Assert.That(clip.samples, Is.GreaterThan(0), soundName);
            Assert.That(clip.frequency, Is.GreaterThanOrEqualTo(22050), soundName);
            Assert.That(clip.length, Is.GreaterThan(0.05f), soundName);
        }
        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void OnlineAttack_CapturesBlueDragonIdentityFromItsPresentedOccupant(PlayerId ownerId)
        {
            var root = new GameObject("BlueDragonPresentationTest");
            root.SetActive(false);
            try
            {
                var bootstrapper = root.AddComponent<BattleBootstrapper>();
                var gateway = new LocalBattleGateway(new BattleFlowController());
                var deck = Enumerable.Repeat("unknown", 20).ToArray();
                var state = gateway.StartBattle(new BattleSetupRequest(deck, deck, PlayerId.Player));
                typeof(BattleBootstrapper).GetField("_battleGateway", PrivateInstance).SetValue(bootstrapper, gateway);
                var sourceCoord = new TileCoord(0, 0);
                state.GetBoard(ownerId).Place(sourceCoord, new UnitState(
                    runtimeId: "blue-dragon-presentation-test",
                    cardId: "BlueDragon",
                    ownerId: ownerId,
                    position: sourceCoord,
                    attackType: AttackType.Ranged,
                    attack: 40,
                    maxHp: 50,
                    canMove: true,
                    isScience: false,
                    sciencePowerUpkeep: 0));
                var attack = new BattleEventDto
                {
                    EventType = BattleEventType.AttackStarted,
                    SourceOwnerId = ownerId,
                    SourceCoord = TileCoordDto.FromDomain(sourceCoord),
                    TargetOwnerId = ownerId == PlayerId.Player ? PlayerId.AI : PlayerId.Player,
                    TargetCoord = TileCoordDto.FromDomain(new TileCoord(2, 1)),
                };
                var method = typeof(BattleBootstrapper).GetMethod("BuildOnlineAttackAnimationRequest", PrivateInstance);
                var request = method.Invoke(bootstrapper, new object[] { attack, Array.Empty<BattleEventDto>() });
                Assert.That(request, Is.Not.Null);
                Assert.That(request.GetType().GetProperty("AttackerCardId").GetValue(request), Is.EqualTo("BlueDragon"));
                Assert.That(request.GetType().GetProperty("IsRangedAttacker").GetValue(request), Is.True);
                Assert.That(state.GetBoard(ownerId).GetOccupant(sourceCoord).CurrentHp, Is.EqualTo(50));
                Assert.That(state.GetBoard(attack.TargetOwnerId).GetOccupant(new TileCoord(2, 1)).CurrentHp,
                    Is.EqualTo(state.GetBoard(attack.TargetOwnerId).GetOccupant(new TileCoord(2, 1)).MaxHp));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OnlineImpactEstimate_IncludesCompleteBlueDragonReactionAndDeath(bool died)
        {
            var root = CreatePresentationHarness(false, out var bootstrapper, out var view, out _);
            try
            {
                var requestType = typeof(BattleBootstrapper).GetNestedType("ImpactAnimationRequest", BindingFlags.NonPublic);
                var impactType = typeof(BattleBootstrapper).GetNestedType("AttackAnimationImpact", BindingFlags.NonPublic);
                var request = Activator.CreateInstance(requestType, true);
                var impact = Activator.CreateInstance(impactType,
                    new object[] { PlayerId.Player, new TileCoord(0, 0), true, died });
                ((IList)requestType.GetProperty("Impacts").GetValue(request)).Add(impact);
                var duration = (float)typeof(BattleBootstrapper).GetMethod("EstimateImpactAnimationDuration", PrivateInstance)
                    .Invoke(bootstrapper, new[] { request });
                var visibleDuration = view.GetBeAttackedAnimationDurationSeconds() +
                    (died ? view.GetDeathAnimationDurationSeconds() : 0f);
                Assert.That(duration, Is.GreaterThanOrEqualTo(visibleDuration),
                    "Online snapshot replacement must wait until the full hurt/death sequence is visible.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancelCombat_RestoresPassivePoseAndStopsTravelFromMovingItAgain(bool drained)
        {
            var root = CreatePresentationHarness(drained, out var bootstrapper, out var view, out var image);
            try
            {
                view.RestoreVisualLayout();
                var passivePosition = view.GetVisualWorldPosition();
                view.PlayAttackAnimation();
                var travel = (IEnumerator)typeof(BattleBootstrapper).GetMethod("AnimateVisualTravel", PrivateInstance)
                    .Invoke(bootstrapper, new object[]
                    {
                        view, passivePosition + Vector3.right * 100f,
                        passivePosition + Vector3.right * 300f, 10f
                    });
                Assert.That(travel.MoveNext(), Is.True);
                Assert.That(Vector3.Distance(view.GetVisualWorldPosition(), passivePosition), Is.GreaterThan(1f));

                bootstrapper.CancelCombatPresentation();

                Assert.That(Vector3.Distance(view.GetVisualWorldPosition(), passivePosition), Is.LessThan(0.001f));
                var originalIdle = AssetDatabase.LoadAllAssetsAtPath("Assets/Unit/BlueDragon/Idle.png")
                    .OfType<Sprite>().OrderBy(sprite => sprite.rect.x).First();
                Assert.That(image.sprite, Is.SameAs(originalIdle));
                var sequence = typeof(TileTextView).GetField("_activeSpriteSequence", PrivateInstance).GetValue(view);
                Assert.That(sequence, Is.Not.Null);
                var actualFrames = (IReadOnlyList<Sprite>)sequence.GetType().GetProperty("Frames").GetValue(sequence);
                Assert.That(actualFrames.Count, Is.EqualTo(drained ? 1 : 4));
                Assert.That((bool)sequence.GetType().GetProperty("IsLooping").GetValue(sequence), Is.True);
                Assert.That(travel.MoveNext(), Is.False, "Cancelled travel must stop before writing another position.");
                Assert.That(Vector3.Distance(view.GetVisualWorldPosition(), passivePosition), Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WaterCannon_ResolvesChildCanvasUsedByTheActualBattleTiles()
        {
            var root = CreatePresentationHarness(false, out _, out var view, out var image);
            Component waterCannon = null;
            try
            {
                // Battle_VSlice places TileTextView outside its child UI Canvas.
                var labelObject = new GameObject("TileLabel", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                labelObject.transform.SetParent(image.transform, false);
                SetField(view, "_text", labelObject.GetComponent<TMPro.TextMeshProUGUI>());
                Assert.That(view.GetComponentInParent<Canvas>(), Is.Null);
                var type = typeof(BattleBootstrapper).Assembly.GetType(
                    "Project333.Runtime.Presentation.Battle.BlueDragonWaterCannon");
                waterCannon = root.AddComponent(type);
                var resolved = (bool)type.GetMethod("EnsureOverlay", PrivateInstance)
                    .Invoke(waterCannon, new object[] { view });
                Assert.That(resolved, Is.True,
                    "The projectile overlay must resolve the tile's visual Canvas, even when the TileTextView has no Canvas parent.");
            }
            finally
            {
                if (waterCannon != null)
                {
                    var overlay = waterCannon.GetType().GetField("_root", PrivateInstance).GetValue(waterCannon) as RectTransform;
                    if (overlay != null) Object.DestroyImmediate(overlay.gameObject);
                }
                Object.DestroyImmediate(root);
            }
        }
        private static GameObject CreatePresentationHarness(bool drained, out BattleBootstrapper bootstrapper,
            out TileTextView textView, out Image image)
        {
            var root = new GameObject("BlueDragonPresentationHarness", typeof(RectTransform));
            root.SetActive(false);
            bootstrapper = root.AddComponent<BattleBootstrapper>();
            var screen = root.AddComponent<BattleScreenPresenter>();
            var board = root.AddComponent<BoardPresenter>();
            SetField(bootstrapper, "_battleScreenPresenter", screen);
            SetField(screen, "_boardPresenter", board);
            var tileObject = new GameObject("BlueDragonTile", typeof(RectTransform));
            tileObject.transform.SetParent(root.transform, false);
            var tile = tileObject.AddComponent<TileView>();
            tile.Configure(PlayerId.Player, 0, 0);
            var occupant = new UnitState("blue-dragon-visual-test", "BlueDragon", PlayerId.Player,
                new TileCoord(0, 0), AttackType.Ranged, 40, 50, true, false, 0);
            occupant.IsDrained = drained;
            tile.Present(occupant);
            textView = tileObject.AddComponent<TileTextView>();
            var visual = new GameObject("OccupantVisual", typeof(RectTransform), typeof(Canvas),
                typeof(Image), typeof(Animator), typeof(SpriteRenderer));
            visual.transform.SetParent(tileObject.transform, false);
            image = visual.GetComponent<Image>();
            SetField(textView, "_tileView", tile);
            SetField(textView, "_occupantVisualRoot", visual.GetComponent<RectTransform>());
            SetField(textView, "_occupantVisualImage", image);
            SetField(textView, "_occupantVisualAnimator", visual.GetComponent<Animator>());
            SetField(textView, "_lastVisualController", AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Unit/BlueDragon/BlueDragon.controller"));
            SetField(textView, "_lastVisualCardId", "BlueDragon");
            SetField(textView, "_lastVisualRuntimeId", occupant.RuntimeId);
            SetField(textView, "_isOccupied", true);
            SetField(board, "_playerTileViews", new[] { tile });
            SetField(board, "_aiTileViews", Array.Empty<TileView>());
            return root;
        }

        private static void SetField(object instance, string fieldName, object value)
        {
            instance.GetType().GetField(fieldName, PrivateInstance).SetValue(instance, value);
        }
        private static Type AssetsType
        {
            get
            {
                var type = typeof(BattleBootstrapper).Assembly.GetType(
                    "Project333.Runtime.Presentation.Battle.BlueDragonPresentationAssets");
                Assert.That(type, Is.Not.Null, "BlueDragon presentation resource loader must be included in the runtime assembly.");
                return type;
            }
        }

        private static IReadOnlyList<Sprite> ReadFrames(string memberName)
        {
            var property = AssetsType.GetProperty(memberName, StaticMembers);
            Assert.That(property, Is.Not.Null, memberName);
            return (IReadOnlyList<Sprite>)property.GetValue(null);
        }

        private static float ReadFloat(string fieldName)
        {
            return (float)AssetsType.GetField(fieldName, StaticMembers).GetValue(null);
        }

        private static void AssertValidFrames(IReadOnlyList<Sprite> frames, Texture2D texture)
        {
            Assert.That(texture, Is.Not.Null);
            foreach (var frame in frames)
            {
                Assert.That(frame, Is.Not.Null);
                Assert.That(frame.texture, Is.SameAs(texture));
                Assert.That(frame.rect.width, Is.GreaterThan(0f));
                Assert.That(frame.rect.height, Is.GreaterThan(0f));
                Assert.That(frame.rect.xMin, Is.GreaterThanOrEqualTo(0f));
                Assert.That(frame.rect.yMin, Is.GreaterThanOrEqualTo(0f));
                Assert.That(frame.rect.xMax, Is.LessThanOrEqualTo(texture.width));
                Assert.That(frame.rect.yMax, Is.LessThanOrEqualTo(texture.height));
            }
        }
    }
}