using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Online;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    // Enter through the same public self-cast action as the hand UI, with the authored scene.
    public sealed class AreaSpellPlaybackLocalCastTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator EarthquakePublicCastShowsAllTenCracksAndResolvesItsPhysicalPacketsExactlyOnce()
        {
            var previousToken = PrepareAuthoredScene();
            try
            {
                yield return new EnterPlayMode();
                yield return null;
                typeof(AccountSessionState).GetProperty("SessionToken")
                    .SetValue(null, "area-spell-public-cast-fixture-no-requests");
                yield return ExerciseAreaSpell(EarthquakeRules.CardId, 10, 30, 3, 0, "_earthquakeVfx");
                yield return new ExitPlayMode();
            }
            finally { RestoreSceneAndToken(previousToken); }
        }

        [UnityTest]
        public IEnumerator NuclearPublicCastShowsTheMushroomCloudAndResolvesImmediateFixedDamageExactlyOnce()
        {
            var previousToken = PrepareAuthoredScene();
            try
            {
                yield return new EnterPlayMode();
                yield return null;
                typeof(AccountSessionState).GetProperty("SessionToken")
                    .SetValue(null, "area-spell-public-cast-fixture-no-requests");
                yield return ExerciseAreaSpell(NuclearRules.CardId, 100, 100, 0, NuclearRules.PowerCost, "_nuclearExplosionVfx");
                yield return new ExitPlayMode();
            }
            finally { RestoreSceneAndToken(previousToken); }
        }

        private static string PrepareAuthoredScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Battle_VSlice.unity", OpenSceneMode.Single);
            var bootstrapper = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<BattleBootstrapper>(true)).Single();
            bootstrapper.enabled = false;
            Set(bootstrapper, "_startOnAwake", false);
            Set(bootstrapper, "_startWithDraftBeforeBattle", false);
            Set(bootstrapper, "_autoRunAiTurns", false);
            Set(bootstrapper, "_returnToDraftSceneWhenRunSessionActive", false);
            foreach (var connection in scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<OnlineBattleConnectionTester>(true)).ToArray())
                Object.DestroyImmediate(connection);
            var authoredBoard = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<BoardPresenter>(true)).Single();
            Assert.That(authoredBoard.transform.root, Is.Not.SameAs(bootstrapper.transform.root),
                "Suspend only the bootstrap host, preserving the authored board's active hierarchy.");
            bootstrapper.gameObject.SetActive(false);
            return AccountSessionState.SessionToken;
        }

        private static void RestoreSceneAndToken(string previousToken)
        {
            var loadedBootstrapper = FindLoadedSceneComponent<BattleBootstrapper>();
            if (loadedBootstrapper != null)
            {
                loadedBootstrapper.CancelCombatPresentation();
                loadedBootstrapper.gameObject.SetActive(true);
            }
            typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null, previousToken);
        }

        private IEnumerator ExerciseAreaSpell(string cardId, int unitDamage, int buildingDamage,
            int goldCost, int powerCost, string effectField)
        {
            Assert.That(UnityEngine.Application.isPlaying, Is.True, "EnterPlayMode must execute in the top-level UnityTest enumerator.");
            var bootstrapper = FindLoadedSceneComponent<BattleBootstrapper>();
            var screen = FindLoadedSceneComponent<BattleScreenPresenter>();
            Assert.That(bootstrapper, Is.Not.Null);
            Assert.That(screen, Is.Not.Null);
            bootstrapper.gameObject.SetActive(true);
            bootstrapper.enabled = true;
            var provider = JsonCardDefinitionDatabase.FromJson(
                File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).CreateProvider();
            var flow = new BattleFlowController(new BattleSetupService(), new MulliganService(), new TurnStartService(),
                new BattleCommandProcessor(new PlayCardService(provider), new SpellService(provider),
                    new MoveService(), new AttackService(new TargetingService()), new EndTurnService()));
            var gateway = new LocalBattleGateway(flow);
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var state = gateway.StartBattle(new BattleSetupRequest(deck, deck, PlayerId.Player));
            state.SetPhase(PhaseType.Main);
            state.SetActivePlayer(PlayerId.Player);
            var selected = state.Player.Hand.Add(cardId);
            var remainingCopy = state.Player.Hand.Add(cardId);
            state.Player.Resources.Add(new ResourceSet(0, 0, powerCost, goldCost));
            var target = new UnitState("area-target-unit", "Goblin", PlayerId.AI,
                new TileCoord(0, 0), AttackType.Melee, 1, 350, true, false, 0);
            var building = new BuildingState("area-target-building", "PowerPlant", PlayerId.AI,
                new TileCoord(1, 0), false, 0, 350);
            var ally = new UnitState("area-unaffected-ally", "Goblin", PlayerId.Player,
                new TileCoord(0, 0), AttackType.Melee, 1, 350, true, false, 0);
            state.AIBoard.Place(target.Position, target);
            state.AIBoard.Place(building.Position, building);
            state.PlayerBoard.Place(ally.Position, ally);
            Set(bootstrapper, "_battleGateway", gateway);
            Set(bootstrapper, "_cardDefinitionProvider", provider);
            Set(bootstrapper, "_sendPlayerActionsToOnlineGateway", false);
            Set(bootstrapper, "_battleScreenPresenter", screen);
            Set(bootstrapper, "_animateAttackSequences", true);
            screen.Bind(bootstrapper);
            screen.Present(state);
            screen.BoardPresenter.ApplyResponsiveLayoutNow();
            Canvas.ForceUpdateCanvases();
            yield return null;

            var ownMasterHp = state.Player.Master.CurrentHp;
            var enemyMasterHp = state.AI.Master.CurrentHp;
            var goldBefore = state.Player.Resources.Gold;
            var powerBefore = state.Player.Resources.Power;
            var handBefore = state.Player.Hand.Count;
            var discardBefore = state.Player.Discard.Count;
            Assert.That((bool)typeof(BattleBootstrapper).GetMethod("ShouldAnimateAttackSequences", Hidden).Invoke(bootstrapper, null), Is.True,
                "The real public-cast path must animate. " + DescribeBoard(screen.BoardPresenter));
            Assert.That(screen.BoardPresenter.gameObject.activeInHierarchy, Is.True);
            Assert.That(bootstrapper.TryCastPlayerPersistentSpell(cardId, selected.RuntimeId, out var message),
                Is.True, message);
            var effect = Get(bootstrapper, effectField);
            Assert.That(effect, Is.Not.Null, "A successful public cast must create its VFX coordinator. " + DescribeBoard(screen.BoardPresenter));
            Assert.That((bool)Get(effect, "_playing"), Is.True,
                "The authored canvases must not reject the public cast. " + DescribeBoard(screen.BoardPresenter));
            Assert.That(bootstrapper.IsBusy, Is.True, "The cast must remain locked during the effect.");
            Assert.That(target.CurrentHp, Is.EqualTo(350 - unitDamage));
            Assert.That(building.CurrentHp, Is.EqualTo(350 - buildingDamage));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(enemyMasterHp - unitDamage));
            Assert.That(ally.CurrentHp, Is.EqualTo(350));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(ownMasterHp));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(goldBefore - goldCost));
            Assert.That(state.Player.Resources.Power, Is.EqualTo(powerBefore - powerCost));
            Assert.That(state.Player.Hand.Contains(cardId, selected.RuntimeId), Is.False);
            Assert.That(state.Player.Hand.Contains(cardId, remainingCopy.RuntimeId), Is.True);
            Assert.That(state.Player.Hand.Count, Is.EqualTo(handBefore - 1));
            Assert.That(state.Player.Discard.Count, Is.EqualTo(discardBefore + 1));

            var images = VisibleImages(effect);
            Assert.That(images.Length, Is.EqualTo(cardId == EarthquakeRules.CardId ? 10 : 1));
            foreach (var image in images)
            {
                Assert.That(image, Is.Not.Null);
                Assert.That(image.gameObject.activeInHierarchy, Is.True);
                Assert.That(image.sprite, Is.Not.Null);
                Assert.That(image.rectTransform.rect.width, Is.GreaterThan(1f));
                Assert.That(image.raycastTarget, Is.False);
            }
            if (cardId == NuclearRules.CardId)
            {
                Assert.That(target.FalloutStacks, Is.EqualTo(1));
                Assert.That(state.AI.Master.FalloutStacks, Is.EqualTo(1));
                Assert.That(building.FalloutStacks, Is.Zero);
                Assert.That(ally.FalloutStacks, Is.Zero);
            }

            var startedAt = Get(effect, "_startedAt");
            Assert.That(bootstrapper.TryCastPlayerPersistentSpell(cardId, selected.RuntimeId, out _), Is.False);
            Assert.That(bootstrapper.TryCastPlayerPersistentSpell(cardId, remainingCopy.RuntimeId, out _), Is.False,
                "A second card must also wait for the already-confirmed cast's presentation.");
            Assert.That(Get(effect, "_startedAt"), Is.EqualTo(startedAt));
            Assert.That(state.Player.Hand.Contains(cardId, remainingCopy.RuntimeId), Is.True);
            Assert.That(target.CurrentHp, Is.EqualTo(350 - unitDamage));
            Assert.That(building.CurrentHp, Is.EqualTo(350 - buildingDamage));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(enemyMasterHp - unitDamage));

            var timeout = Time.unscaledTime + 5f;
            while (bootstrapper.IsBusy && Time.unscaledTime < timeout) yield return null;
            Assert.That(bootstrapper.IsBusy, Is.False);
            Assert.That((bool)Get(effect, "_playing"), Is.False);
            Assert.That(images.All(image => !image.gameObject.activeInHierarchy), Is.True);
            Assert.That(target.CurrentHp, Is.EqualTo(350 - unitDamage), "Completing VFX must not replay damage.");
            Assert.That(building.CurrentHp, Is.EqualTo(350 - buildingDamage));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(enemyMasterHp - unitDamage));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(ownMasterHp));
            Assert.That(state.Player.Discard.Count, Is.EqualTo(discardBefore + 1));
            bootstrapper.CancelCombatPresentation();
            Assert.That(images.All(image => !image.gameObject.activeInHierarchy), Is.True);
        }

        private static string DescribeBoard(BoardPresenter board)
        {
            if (board == null) return "board=null";
            return "playing=" + UnityEngine.Application.isPlaying + " boardActive=" + board.gameObject.activeInHierarchy + " tiles=" +
                string.Join(";", board.AITileViews.Select(tile =>
                {
                    if (tile == null) return "null tile";
                    var anchor = tile.GetComponent<TileTextView>().ActiveEffectButtonAnchor;
                    var canvas = anchor == null ? null : anchor.GetComponentInParent<Canvas>(true);
                    return tile.Coord + " anchor=" + (anchor == null ? "null" : anchor.rect + "/active=" + anchor.gameObject.activeInHierarchy) +
                        " canvas=" + (canvas == null ? "null" : canvas.name + "/active=" + canvas.isActiveAndEnabled + "/root=" + ((RectTransform)canvas.rootCanvas.transform).rect);
                }));
        }

        private static T FindLoadedSceneComponent<T>() where T : Component => Resources.FindObjectsOfTypeAll<T>()
            .SingleOrDefault(component => component.gameObject.scene.IsValid() && component.gameObject.scene.isLoaded);

        private static Image[] VisibleImages(object effect) => effect is EarthquakeVfx
            ? (Image[])Get(effect, "_cracks") : new[] { (Image)Get(effect, "_image") };
        private static object Get(object instance, string name) => instance.GetType().GetField(name, Hidden).GetValue(instance);
        private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Hidden).SetValue(instance, value);
    }
}