using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;

namespace Project333.Tests.EditMode
{
    public sealed class TrapOnlineStateTests
    {
        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void Factory_OnlyViewerReceivesPrivateIdentityCostAndOrder(PlayerId viewer)
        {
            var state = CreateState();
            var opponent = state.GetOpponent(viewer).Id;
            state.ArmedTraps.Add(Trap("own-first", viewer, "OwnSecret", 4, 2));
            state.ArmedTraps.Add(Trap("enemy-only", opponent, "EnemySecret", 7, 3));
            state.ArmedTraps.Add(Trap("own-second", viewer, "OwnOtherSecret", 2, 4));
            var view = new BattleStateViewFactory().CreateForPlayer(state, "trap-private", viewer);

            Assert.That(view.Player.TrapCount, Is.EqualTo(2));
            Assert.That(view.Opponent.TrapCount, Is.EqualTo(1));
            Assert.That(view.Player.VisibleArmedTraps.Select(t => t.RuntimeId),
                Is.EqualTo(new[] { "own-first", "own-second" }));
            Assert.That(view.Player.VisibleArmedTraps[0].ActivationCost.Gold, Is.EqualTo(4));
            Assert.That(view.Opponent.VisibleArmedTraps, Is.Empty);
            var json = OnlineBattleMessageSerializer.SerializeEnvelope(new OnlineBattleEnvelope
                { MessageType = OnlineBattleMessageType.StateView, StateView = view });
            Assert.That(json, Does.Contain("OwnSecret"));
            Assert.That(json, Does.Not.Contain("EnemySecret"));
            Assert.That(json, Does.Not.Contain("enemy-only"));
            Assert.That(state.TrapEvents, Is.Empty);
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void Projector_RestoresOwnedCapturedRulesAndEnemyCountWithoutReplaying(PlayerId viewer)
        {
            var state = CreateState();
            state.ArmedTraps.Add(Trap("viewer-trap", viewer, "CapturedSecret", 4, 6));
            state.ArmedTraps.Add(Trap("opponent-trap", state.GetOpponent(viewer).Id, "OtherSecret", 9, 2));
            state.RecordTrapEvent(new BattleTrapEvent(BattleTrapEventKind.Installed, viewer, 1));
            var wire = new BattleStateViewFactory().CreateForPlayer(state, "restore-traps", viewer);
            var json = OnlineBattleMessageSerializer.SerializeEnvelope(new OnlineBattleEnvelope
                { MessageType = OnlineBattleMessageType.StateView, StateView = wire });
            var restoredWire = OnlineBattleMessageSerializer.DeserializeEnvelope(json).StateView;
            var restored = BattleStateViewProjector.CreateLocalPerspectiveState(restoredWire);

            Assert.That(restored.ArmedTraps.Count, Is.EqualTo(1));
            var trap = restored.ArmedTraps[0];
            Assert.That(trap.OwnerId, Is.EqualTo(PlayerId.Player));
            Assert.That(trap.RuntimeId, Is.EqualTo("viewer-trap"));
            Assert.That(trap.CardId, Is.EqualTo("CapturedSecret"));
            Assert.That(trap.ActivationCost.Gold, Is.EqualTo(4));
            Assert.That(trap.TriggerType, Is.EqualTo(TrapTriggerType.EnemyUnitAttacksMaster));
            Assert.That(trap.EffectType, Is.EqualTo(TrapEffectType.ReturnAttackerToHand));
            Assert.That(trap.InstalledTurn, Is.EqualTo(6));
            Assert.That(restored.Player.HiddenTrapCount, Is.Zero);
            Assert.That(restored.AI.HiddenTrapCount, Is.EqualTo(1));
            Assert.That(restored.GetArmedTrapCount(PlayerId.Player), Is.EqualTo(1));
            Assert.That(restored.GetArmedTrapCount(PlayerId.AI), Is.EqualTo(1));
            Assert.That(restored.TrapEvents, Is.Empty);
            Assert.That(state.TrapEvents.Count, Is.EqualTo(1), "Projection must not mutate authority.");
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void Projector_IgnoresOpponentPrivateListAndMisownedEntries(PlayerId viewer)
        {
            var wire = new BattleStateViewFactory().CreateForPlayer(CreateState(), "malformed", viewer);
            wire.Opponent.TrapCount = 3;
            var other = viewer == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
            var hostile = new ArmedTrapViewDto { OwnerId = other, RuntimeId = "forged", CardId = "Secret",
                TriggerType = TrapTriggerType.EnemyUnitAttacksMaster, EffectType = TrapEffectType.ReturnAttackerToHand,
                ActivationCost = new ResourceSetDto { Gold = 4 } };
            wire.Opponent.VisibleArmedTraps.Add(hostile);
            wire.Player.VisibleArmedTraps.Add(hostile);
            var restored = BattleStateViewProjector.CreateLocalPerspectiveState(wire);
            Assert.That(restored.ArmedTraps, Is.Empty);
            Assert.That(restored.GetArmedTrapCount(PlayerId.AI), Is.EqualTo(3));
            Assert.That(restored.GetArmedTrapCount(PlayerId.Player), Is.Zero);
        }

        [Test]
        public void FactoryAndProjector_DoNotAliasActivationCostOrPrivateLists()
        {
            var state = CreateState();
            state.ArmedTraps.Add(Trap("cost-copy", PlayerId.Player, "Secret", 4, 1));
            var view = new BattleStateViewFactory().CreateForPlayer(state, "copy", PlayerId.Player);
            var restored = BattleStateViewProjector.CreateLocalPerspectiveState(view);
            view.Player.VisibleArmedTraps[0].ActivationCost.Gold = 999;
            view.Player.VisibleArmedTraps.Clear();
            Assert.That(restored.ArmedTraps[0].ActivationCost.Gold, Is.EqualTo(4));
            Assert.That(state.ArmedTraps[0].ActivationCost.Gold, Is.EqualTo(4));
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
        }

        [Test]
        public void LegacyView_WithNullPrivateListAndNegativeCountRestoresNoTraps()
        {
            var wire = new BattleStateViewFactory().CreateForPlayer(CreateState(), "old", PlayerId.Player);
            wire.Player.VisibleArmedTraps = null;
            wire.Opponent.VisibleArmedTraps = null;
            wire.Opponent.TrapCount = -2;
            var restored = BattleStateViewProjector.CreateLocalPerspectiveState(wire);
            Assert.That(restored.ArmedTraps, Is.Empty);
            Assert.That(restored.AI.HiddenTrapCount, Is.Zero);
            Assert.That(restored.TrapEvents, Is.Empty);
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void InstallGateway_SendsExactHandInstanceAndDoesNotChangeTruth(PlayerId seat)
        {
            var sender = new Sender();
            var gateway = new OnlineBattleGateway("trap-intent", "token", seat, sender);
            var state = CreateState();
            state.SetPhase(PhaseType.Main);
            state.SetActivePlayer(seat);
            var view = new BattleStateViewFactory().CreateForPlayer(state, "trap-intent", seat);
            gateway.ApplyServerView(view);
            gateway.ExecuteCommand(PlayerId.Player, new InstallTrapCommand("TrapReturn", "exact-hand-instance"));
            Assert.That(sender.Messages.Count, Is.EqualTo(1));
            var message = sender.Messages[0];
            Assert.That(message.CommandType, Is.EqualTo(OnlineBattleCommandType.InstallTrap));
            Assert.That(message.ActorId, Is.EqualTo(seat));
            Assert.That(message.CardId, Is.EqualTo("TrapReturn"));
            Assert.That(message.HandCardRuntimeId, Is.EqualTo("exact-hand-instance"));
            Assert.That(message.HasTarget, Is.False);
            Assert.That(gateway.CurrentView, Is.SameAs(view));
            Assert.That(state.ArmedTraps, Is.Empty);
            Assert.That(state.TrapEvents, Is.Empty);
        }

        private static ArmedTrapState Trap(string id, PlayerId owner, string cardId, int gold, int turn)
            => new ArmedTrapState(id, owner, cardId, TrapTriggerType.EnemyUnitAttacksMaster,
                TrapEffectType.ReturnAttackerToHand, new ResourceSet(0, 0, 0, gold), turn);

        private static BattleState CreateState()
            => new BattleSetupService().CreateInitialState(new BattleSetupRequest(
                Enumerable.Repeat("Goblin", 33).ToArray(), Enumerable.Repeat("Goblin", 33).ToArray(), PlayerId.Player));

        private sealed class Sender : IOnlineBattleMessageSender
        {
            public readonly List<ClientBattleCommandMessage> Messages = new List<ClientBattleCommandMessage>();
            public void Send(ClientBattleCommandMessage message) => Messages.Add(message);
        }
    }
}
