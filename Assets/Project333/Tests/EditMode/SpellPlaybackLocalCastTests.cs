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
    // Exercise the player's public cast entry, not a request pre-filled by the fixture.
    public sealed class SpellPlaybackLocalCastTests
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
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RealLocalGatewaySelectedColumnCastPlaysOnceOnTheAuthoredBattleCanvases()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Battle_VSlice.unity", OpenSceneMode.Single);
            var bootstrapper = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleBootstrapper>(true)).Single();
            bootstrapper.enabled = false;
            Set(bootstrapper, "_startOnAwake", false);
            Set(bootstrapper, "_startWithDraftBeforeBattle", false);
            Set(bootstrapper, "_autoRunAiTurns", false);
            Set(bootstrapper, "_returnToDraftSceneWhenRunSessionActive", false);
            foreach (var connection in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OnlineBattleConnectionTester>(true)).ToArray())
                Object.DestroyImmediate(connection);
            _previousToken = AccountSessionState.SessionToken;
            _hasPreviousToken = true;
            typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null, "spell-local-cast-fixture-no-requests");
            yield return new EnterPlayMode();

            bootstrapper = Object.FindFirstObjectByType<BattleBootstrapper>();
            var screen = Object.FindFirstObjectByType<BattleScreenPresenter>();
            Assert.That(bootstrapper, Is.Not.Null);
            Assert.That(screen, Is.Not.Null);
            var provider = JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).CreateProvider();
            var flow = new BattleFlowController(new BattleSetupService(), new MulliganService(), new TurnStartService(),
                new BattleCommandProcessor(new PlayCardService(provider), new SpellService(provider), new MoveService(), new AttackService(new TargetingService()), new EndTurnService()));
            var gateway = new LocalBattleGateway(flow);
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var state = gateway.StartBattle(new BattleSetupRequest(deck, deck, PlayerId.Player));
            state.SetPhase(PhaseType.Main);
            state.SetActivePlayer(PlayerId.Player);
            var selected = state.Player.Hand.Add(FireSpearRules.CardId);
            state.Player.Resources.Add(new ResourceSet(3, 0, 0, 0));
            var front = new UnitState("local-column-front", "A-301", PlayerId.AI, new TileCoord(2, 0), AttackType.Ranged, 1, 100, true, false, 0);
            state.AIBoard.Place(front.Position, front);
            Set(bootstrapper, "_battleGateway", gateway);
            Set(bootstrapper, "_cardDefinitionProvider", provider);
            Set(bootstrapper, "_sendPlayerActionsToOnlineGateway", false);
            Set(bootstrapper, "_battleScreenPresenter", screen);
            screen.Bind(bootstrapper);
            screen.Present(state);
            screen.BoardPresenter.ApplyResponsiveLayoutNow();
            Canvas.ForceUpdateCanvases();
            yield return null;

            var playerHp = state.Player.Master.CurrentHp;
            var masterHp = state.AI.Master.CurrentHp;
            var discardBefore = state.Player.Discard.Count;
            var handBefore = state.Player.Hand.Count;
            Assert.That(bootstrapper.TryUsePlayerCardOnTile(FireSpearRules.CardId, selected.RuntimeId, PlayerId.AI, new TileCoord(2, 1), out var message), Is.True, message);
            var effect = (FireSpearVfx)Get(bootstrapper, "_fireSpearVfx");
            Assert.That(effect, Is.Not.Null, "The successful public cast must create its VFX coordinator.");
            Assert.That((bool)Get(effect, "_playing"), Is.True, "A confirmed cast must not be silently rejected by the authored canvases.");
            Assert.That(Get(effect, "_caster"), Is.EqualTo(PlayerId.Player));
            Assert.That(Get(effect, "_targetOwner"), Is.EqualTo(PlayerId.AI));
            Assert.That(Get(effect, "_column"), Is.EqualTo(2));
            var image = (Image)Get(effect, "_image");
            Assert.That(image.gameObject.activeInHierarchy && image.sprite != null, Is.True);
            Assert.That(front.CurrentHp, Is.EqualTo(85));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(masterHp - 15));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(playerHp));
            Assert.That(state.Player.Resources.Mana, Is.Zero);
            Assert.That(state.Player.Hand.Contains(FireSpearRules.CardId, selected.RuntimeId), Is.False);
            Assert.That(state.Player.Hand.Count, Is.EqualTo(handBefore - 1));
            Assert.That(state.Player.Discard.Count, Is.EqualTo(discardBefore + 1));
            var startedAt = Get(effect, "_startedAt");
            Assert.That(bootstrapper.TryUsePlayerCardOnTile(FireSpearRules.CardId, selected.RuntimeId, PlayerId.AI, new TileCoord(2, 1), out _), Is.False);
            Assert.That(Get(effect, "_startedAt"), Is.EqualTo(startedAt));
            Assert.That(front.CurrentHp, Is.EqualTo(85));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(masterHp - 15));
            var timeout = Time.unscaledTime + 4f;
            while (bootstrapper.IsBusy && Time.unscaledTime < timeout) yield return null;
            Assert.That(bootstrapper.IsBusy, Is.False);
            Assert.That((bool)Get(effect, "_playing"), Is.False);
            Assert.That(image.gameObject.activeInHierarchy, Is.False);
            Assert.That(front.CurrentHp, Is.EqualTo(85), "Playback completion never repeats authoritative damage.");
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(masterHp - 15));
            Assert.That(state.Player.Discard.Count, Is.EqualTo(discardBefore + 1));
            yield return new ExitPlayMode();
        }

        private static object Get(object instance, string name) => instance.GetType().GetField(name, Hidden).GetValue(instance);
        private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Hidden).SetValue(instance, value);
    }
}
