using System;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class SummonTrapCombatTests
    {
        private const string Id = "SummonTrap";
        private static readonly TileCoord MasterCoord = new TileCoord(2, 1);
        private static readonly TileCoord TargetCoord = new TileCoord(0, 1);
        private static readonly int[] Levels = Enumerable.Range(0, 14).ToArray();

        [Test]
        public void NewEnumsAppendWithoutReinterpretingExistingInstalledRules()
        {
            Assert.That((int)TrapTriggerType.EnemyUnitSummoned, Is.EqualTo(5));
            Assert.That((int)TrapTriggerType.EnemyRangedUnitAttacksMaster, Is.EqualTo(4));
            Assert.That((int)TrapEffectType.DamageSummonedUnitPhysical, Is.EqualTo(4));
            Assert.That((int)TrapEffectType.DestroyAttacker, Is.EqualTo(3));
        }

        [Test]
        public void InstallCapturesEveryLevelDamageAndDoesNotReReadLaterAccountUpgrade()
        {
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            foreach (var level in Levels)
            {
                var state = State(owner);
                var levels = new InMemoryCardUpgradeLevelProvider();
                levels.SetUpgradeLevel(owner, Id, level);
                var definition = Definition();
                var provider = new InMemoryCardDefinitionProvider(new CardDefinition[] { definition, Target() });
                var installed = Install(state, owner, definition, levels: levels);
                Assert.That(installed.EffectAmount, Is.EqualTo(20 + level));
                Assert.That(installed.UpgradeLevel, Is.EqualTo(level));
                levels.SetUpgradeLevel(owner, Id, 13 - level);
                state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 0, 1));
                var unit = Play(state, Opponent(owner), provider);
                Assert.That(unit.CurrentHp, Is.EqualTo(100 - 20 - level), "Wrong level at " + owner + "/" + level);
                Assert.That(state.TrapEvents.Single().EffectAmount, Is.EqualTo(20 + level));
                Assert.That(state.TrapEvents.Single().UpgradeLevel, Is.EqualTo(level));
                Assert.That(state.GetPlayer(owner).Resources.Gold, Is.Zero);
                Assert.That(state.GetPlayer(owner).Discard.CardIds, Is.EqualTo(new[] { Id }));
            }
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void InstalledFreeAndDuplicateRejectedWithoutConsumingSecondCard(PlayerId owner)
        {
            var state = State(owner);
            var definition = Definition();
            var installed = Install(state, owner, definition, clearEvents: false);
            var second = state.GetPlayer(owner).Hand.Add(Id, "duplicate", false);
            Assert.Throws<InvalidOperationException>(() => new TrapService(
                new InMemoryCardDefinitionProvider(new[] { definition })).Install(state, owner, Id, second.RuntimeId));
            Assert.That(state.GetPlayer(owner).Hand.Cards.Single().RuntimeId, Is.EqualTo("duplicate"));
            Assert.That(state.GetPlayer(owner).Resources.Gold, Is.Zero);
            Assert.That(state.GetPlayer(owner).Discard.Count, Is.Zero);
            Assert.That(installed.EffectAmount, Is.EqualTo(20));
            Assert.That(state.TrapEvents.Single().CardId, Is.Empty);
            Assert.That(state.TrapEvents.Single().EffectAmount, Is.Zero);
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void PhysicalDefenseAppliedWithNoSpellPowerMagicDefenseOrShielderTransfer(PlayerId owner)
        {
            var state = State(owner);
            Install(state, owner, Definition());
            state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 0, 1));
            var power = new UnitState("power", "PowerFixture", owner, new TileCoord(1, 0), AttackType.Ranged,
                1, 100, true, false, 0, spellPower: 50);
            state.GetBoard(owner).Place(power.Position, power);
            var enemy = Opponent(owner);
            var shield = new UnitState("shield", "ShieldFixture", enemy, new TileCoord(0, 0), AttackType.Melee,
                1, 100, true, false, 0, hasShielder: true);
            state.GetBoard(enemy).Place(shield.Position, shield);
            var target = Target(physicalDefense: 7, magicDefense: 99, hiding: true, flying: true);
            var unit = Play(state, enemy, new InMemoryCardDefinitionProvider(new CardDefinition[] { Definition(), target }));
            Assert.That(unit.CurrentHp, Is.EqualTo(87));
            Assert.That(shield.CurrentHp, Is.EqualTo(100));
            Assert.That(unit.IsHiding, Is.False);
            var damage = state.ValuePopupEvents.Single();
            Assert.That(damage.Amount, Is.EqualTo(13));
            Assert.That(damage.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(damage.SourceOwnerId, Is.EqualTo(owner));
            Assert.That(damage.SourceCardId, Is.EqualTo(Id));
            Assert.That(damage.RuntimeId, Is.EqualTo(unit.RuntimeId));
            Assert.That(state.LastAttackResolution, Is.Null);
            Assert.That(state.LastSummonedOccupant.CurrentHp, Is.EqualTo(100));
            Assert.That(state.LastSummonedOccupant.IsHiding, Is.True);
            Assert.That(state.GetPlayer(enemy).Resources.Mana, Is.EqualTo(8), "Accepted summon cost is retained.");
        }

        [TestCase(PlayerId.Player, InvincibleDurationType.Always)]
        [TestCase(PlayerId.AI, InvincibleDurationType.Always)]
        [TestCase(PlayerId.Player, InvincibleDurationType.SummonTurn)]
        [TestCase(PlayerId.AI, InvincibleDurationType.SummonTurn)]
        [TestCase(PlayerId.Player, InvincibleDurationType.OwnerTurnOnly)]
        [TestCase(PlayerId.AI, InvincibleDurationType.OwnerTurnOnly)]
        public void NativeInvincibleInitializedBeforeTrapAndStillConsumesItsOneActivation(PlayerId owner, InvincibleDurationType duration)
        {
            var state = State(owner); Install(state, owner, Definition());
            state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 0, 1));
            var unit = Play(state, Opponent(owner), Provider(Target(invincible: duration, hiding: true)));
            Assert.That(unit.CurrentHp, Is.EqualTo(100));
            Assert.That(unit.IsInvincible, Is.True);
            Assert.That(unit.IsHiding, Is.True);
            Assert.That(state.ArmedTraps, Is.Empty);
            Assert.That(state.TrapEvents.Count, Is.EqualTo(1));
            Assert.That(state.ValuePopupEvents.Single().IsInvinciblePrevented, Is.True);
            Assert.That(state.GetPlayer(owner).Resources.Gold, Is.Zero);
        }

        [TestCase(30, 0)]
        [TestCase(0, 2)]
        public void FullyProtectedSummonConsumesTrapAndPreservesHiding(int defense, int seal)
        {
            var state = State(PlayerId.Player); Install(state, PlayerId.Player, Definition());
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 1));
            var unit = Play(state, PlayerId.AI, Provider(Target(physicalDefense: defense, seal: seal, hiding: true)));
            Assert.That(unit.CurrentHp, Is.EqualTo(100));
            Assert.That(unit.HasHiding, Is.True);
            Assert.That(unit.HidingRevealed, Is.False);
            Assert.That(unit.IsHiding, Is.EqualTo(seal == 0), "Sealbound takes priority while sealed; the trap must not permanently reveal Hiding.");
            Assert.That(state.ArmedTraps, Is.Empty);
            Assert.That(state.TrapEvents.Count, Is.EqualTo(1));
            Assert.That(state.ValuePopupEvents, Is.Empty);
            for (var i = 0; i < seal; i++) unit.ResolveSealboundOwnerTurnStart();
            Assert.That(unit.IsHiding, Is.True, "After Sealbound release the preserved Hiding becomes active.");
            Assert.That(unit.CurrentHp, Is.EqualTo(100));
            Assert.That(state.TrapEvents.Count, Is.EqualTo(1), "Release must not replay the consumed summon trap.");
        }

        [Test]
        public void EndurePreventsDeathButActualDamageAndSuccessfulActivationRemain()
        {
            var state = State(PlayerId.Player); Install(state, PlayerId.Player, Definition(damage: 100));
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 1));
            var unit = Play(state, PlayerId.AI, Provider(Target(hp: 10, endure: true)));
            Assert.That(unit.CurrentHp, Is.EqualTo(1));
            Assert.That(unit.EndureUsed, Is.True);
            Assert.That(state.ValuePopupEvents.Single().Amount, Is.EqualTo(9));
            Assert.That(state.AIBoard.GetOccupant(TargetCoord), Is.SameAs(unit));
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void UnaffordableTrapRemainsAndLaterSummonPaysAndTriggersOnce(PlayerId owner)
        {
            var state = State(owner); Install(state, owner, Definition());
            var first = Play(state, Opponent(owner), Provider(Target()), new TileCoord(0, 0));
            Assert.That(first.CurrentHp, Is.EqualTo(100));
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(state.TrapEvents, Is.Empty);
            state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 0, 1));
            var second = Play(state, Opponent(owner), Provider(Target()));
            Assert.That(second.CurrentHp, Is.EqualTo(80));
            Assert.That(state.ArmedTraps, Is.Empty);
            Assert.That(state.TrapEvents.Single().TargetRuntimeId, Is.EqualTo(second.RuntimeId));
        }

        [Test]
        public void FifoSkipsOtherConditionsAndUnaffordableThenTriggersOnlyFirstAffordable()
        {
            var state = State(PlayerId.Player);
            Install(state, PlayerId.Player, TrapReturnCardRules.CreateDefinition());
            Install(state, PlayerId.Player, Definition("ExpensiveSummon", cost: 5));
            Install(state, PlayerId.Player, Definition());
            Install(state, PlayerId.Player, Definition("LaterSummon"));
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 2));
            Play(state, PlayerId.AI, Provider(Target()));
            Assert.That(state.TrapEvents.Single().CardId, Is.EqualTo(Id));
            Assert.That(state.ArmedTraps.Select(t => t.CardId), Is.EqualTo(new[] { "TrapReturn", "ExpensiveSummon", "LaterSummon" }));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(1));
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void OwnUnitsAndBuildingsDoNotTrigger(PlayerId owner)
        {
            var state = State(owner); Install(state, owner, Definition());
            state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 0, 1));
            Play(state, owner, Provider(Target()));
            var enemy = Opponent(owner); state.SetActivePlayer(enemy);
            var building = new BuildingCardDefinition("BuildingFixture", "building", new ResourceSet(), false, 0, 100, new ResourceSet());
            var hand = state.GetPlayer(enemy).Hand.Add(building.CardId);
            new PlayCardService(new InMemoryCardDefinitionProvider(new[] { building })).PlayBuildingCard(state, enemy,
                building.CardId, new TileCoord(1, 0), hand.RuntimeId);
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(state.GetPlayer(owner).Resources.Gold, Is.EqualTo(1));
            Assert.That(state.TrapEvents, Is.Empty);
            Assert.That(state.LastSummonedOccupant, Is.Null);
            Assert.That(TrapService.TryResolveAcceptedUnitSummon(state, state.GetPlayer(enemy).Master), Is.False);
        }

        [Test]
        public void FailedSummonDoesNotConsumeResourcesHandOrTrap()
        {
            var state = State(PlayerId.Player); Install(state, PlayerId.Player, Definition());
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 1)); state.SetActivePlayer(PlayerId.AI);
            var target = Target(); var provider = Provider(target); var hand = state.AI.Hand.Add(target.CardId);
            var service = new PlayCardService(provider);
            Assert.Throws<InvalidOperationException>(() => service.PlayUnitCard(state, PlayerId.AI, target.CardId, TargetCoord, hand.RuntimeId));
            state.AI.Resources.Add(new ResourceSet(10, 0, 0, 0));
            Assert.Throws<InvalidOperationException>(() => service.PlayUnitCard(state, PlayerId.AI, target.CardId, MasterCoord, hand.RuntimeId));
            Assert.That(state.AI.Hand.Contains(target.CardId, hand.RuntimeId), Is.True);
            Assert.That(state.AI.Resources.Mana, Is.EqualTo(10));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(1));
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(state.TrapEvents, Is.Empty);
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void LethalTrapResolvesRemovalButRetainsDetachedInitialSummonForPresentation(PlayerId owner)
        {
            var state = State(owner); Install(state, owner, Definition());
            state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 0, 1));
            var unit = Play(state, Opponent(owner), Provider(Target(hp: 3)));
            Assert.That(state.GetBoard(unit.OwnerId).GetOccupant(TargetCoord), Is.Null);
            Assert.That(state.ValuePopupEvents.Single().Amount, Is.EqualTo(3));
            Assert.That(state.LastSummonedOccupant.RuntimeId, Is.EqualTo(unit.RuntimeId));
            Assert.That(state.LastSummonedOccupant.CurrentHp, Is.EqualTo(3));
            Assert.That(state.LastSummonedOccupant, Is.Not.SameAs(unit));
            Assert.That(state.LastSummonedOccupant.IsAlive, Is.True);
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void DemonKingRemainsSealedForNormalRevivalAndTrapDoesNotReactivateOnRevival(PlayerId owner)
        {
            var state = State(owner); Install(state, owner, Definition());
            state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 0, 1));
            var unit = Play(state, Opponent(owner), Provider(Target("DemonKing", hp: 10)));
            Assert.That(unit.IsDemonKingRevivalPending, Is.True);
            Assert.That(state.GetBoard(unit.OwnerId).GetOccupant(TargetCoord), Is.SameAs(unit));
            Assert.That(state.TrapEvents.Count, Is.EqualTo(1));
            Assert.That(state.ValuePopupEvents.Single().Amount, Is.EqualTo(10));
            var count = unit.DemonKingRevivalTurnStartsRemaining;
            for (var i = 0; i < count; i++) unit.ResolveDemonKingRevivalTurnStart(unit.OwnerId, 6 + i);
            Assert.That(unit.IsDemonKingRevivalPending, Is.False);
            Assert.That(state.TrapEvents.Count, Is.EqualTo(1));
        }

        [Test]
        public void OrcAuraInitializedBeforeDamageThenWithdrawnWithItsDeathChain()
        {
            var state = State(PlayerId.Player); Install(state, PlayerId.Player, Definition());
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 1));
            var existing = new UnitState("orc", "OrcFixture", PlayerId.AI, new TileCoord(1, 0),
                AttackType.Melee, 2, 3, true, false, 0, races: new[] { UnitRace.Orc });
            state.AIBoard.Place(existing.Position, existing);
            var chieftain = Target("OrcChieftain", hp: 10, orc: true);
            var unit = Play(state, PlayerId.AI, Provider(chieftain));
            Assert.That(state.LastSummonedOccupant.MaxHp, Is.EqualTo(20));
            Assert.That(state.ValuePopupEvents.Single().Amount, Is.EqualTo(20));
            Assert.That(state.AIBoard.GetOccupant(TargetCoord), Is.Null);
            Assert.That(existing.MaxHp, Is.EqualTo(3));
            Assert.That(existing.CurrentHp, Is.EqualTo(3));
            Assert.That(existing.BaseAttack, Is.EqualTo(2));
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void PrivateCapturedAmountSurvivesWireAndReconnectAndEnemyGetsOnlyCount(PlayerId owner)
        {
            var state = State(owner); var levels = new InMemoryCardUpgradeLevelProvider();
            levels.SetUpgradeLevel(owner, Id, 13); Install(state, owner, Definition(), levels: levels);
            foreach (var viewer in new[] { owner, Opponent(owner) })
            {
                var view = new BattleStateViewFactory().CreateForPlayer(state, "summon-private", viewer);
                var text = OnlineBattleMessageSerializer.SerializeEnvelope(new OnlineBattleEnvelope
                    { MessageType = OnlineBattleMessageType.StateView, StateView = view });
                var restoredView = OnlineBattleMessageSerializer.DeserializeEnvelope(text).StateView;
                var projected = BattleStateViewProjector.CreateLocalPerspectiveState(restoredView);
                Assert.That(projected.TrapEvents, Is.Empty);
                Assert.That(projected.LastSummonedOccupant, Is.Null);
                if (viewer == owner)
                {
                    Assert.That(projected.ArmedTraps.Single().EffectAmount, Is.EqualTo(33));
                    Assert.That(projected.ArmedTraps.Single().UpgradeLevel, Is.EqualTo(13));
                    Assert.That(projected.ArmedTraps.Single().TriggerType, Is.EqualTo(TrapTriggerType.EnemyUnitSummoned));
                    Assert.That(projected.ArmedTraps.Single().ActivationCost.Gold, Is.EqualTo(1));
                }
                else
                {
                    Assert.That(text, Does.Not.Contain(Id));
                    Assert.That(projected.ArmedTraps, Is.Empty);
                    Assert.That(projected.GetArmedTrapCount(PlayerId.AI), Is.EqualTo(1));
                    Assert.That(view.Opponent.VisibleArmedTraps, Is.Empty);
                }
            }
        }

        [Test]
        public void AiFullSimulationPreservesOwnAmountAndObservationCannotLearnEnemyTrap()
        {
            var state = State(PlayerId.Player); Install(state, PlayerId.Player, Definition());
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 1));
            var full = AiBattleStateCopy.Create(state);
            var unit = Play(full, PlayerId.AI, Provider(Target()));
            Assert.That(unit.CurrentHp, Is.EqualTo(80));
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(state.AIBoard.GetOccupant(TargetCoord), Is.Null);
            var observation = AiBattleStateCopy.Create(state, true);
            Assert.That(observation.ArmedTraps, Is.Empty);
            Assert.That(observation.GetArmedTrapCount(PlayerId.Player), Is.EqualTo(1));
            Assert.That(Play(observation, PlayerId.AI, Provider(Target())).CurrentHp, Is.EqualTo(100));
            var own = State(PlayerId.AI); Install(own, PlayerId.AI, Definition());
            var ownCopy = AiBattleStateCopy.Create(own, true);
            Assert.That(ownCopy.ArmedTraps.Single().EffectAmount, Is.EqualTo(20));
            var key = AiBattleStateCopy.PositionKey(ownCopy);
            var trap = ownCopy.ArmedTraps.Single();
            ownCopy.ArmedTraps[0] = new ArmedTrapState(trap.RuntimeId, trap.OwnerId, trap.CardId,
                trap.TriggerType, trap.EffectType, trap.ActivationCost, trap.InstalledTurn, effectAmount: 21);
            Assert.That(AiBattleStateCopy.PositionKey(ownCopy), Is.Not.EqualTo(key));
        }

        [Test]
        public void SubsequentCommandClearsTransientInitialSummonAndNoMoveOrAttackRetriggers()
        {
            var state = State(PlayerId.Player); Install(state, PlayerId.Player, Definition());
            var provider = Provider(Target());
            var unit = Play(state, PlayerId.AI, provider);
            Assert.That(state.LastSummonedOccupant, Is.Not.Null);
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 1));
            var processor = new BattleCommandProcessor(new PlayCardService(provider), new SpellService(provider),
                new MoveService(), new AttackService(), new EndTurnService());
            processor.Execute(state, PlayerId.AI, new MoveOccupantCommand(TargetCoord, new TileCoord(1, 1)));
            Assert.That(state.LastSummonedOccupant, Is.Null);
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(unit.CurrentHp, Is.EqualTo(100));
            unit.HasSummoningSickness = false;
            processor.Execute(state, PlayerId.AI, new AttackCommand(unit.Position, MasterCoord));
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(state.TrapEvents, Is.Empty);
        }

        private static TrapSpellCardDefinition Definition(string id = Id, int damage = 20, int cost = 1) =>
            new TrapSpellCardDefinition(id, "서먼 트랩", new ResourceSet(0, 0, 0, cost),
                TrapTriggerType.EnemyUnitSummoned, TrapEffectType.DamageSummonedUnitPhysical, damage: damage);
        private static UnitCardDefinition Target(string id = "TargetFixture", int hp = 100,
            int physicalDefense = 0, int magicDefense = 0, bool hiding = false, bool flying = false,
            int seal = 0, bool endure = false, InvincibleDurationType invincible = InvincibleDurationType.None, bool orc = false) =>
            new UnitCardDefinition(id, "target", new ResourceSet(2, 0, 0, 0), AttackType.Ranged,
                1, hp, true, false, 0, hasEndure: endure, physicalDefense: physicalDefense, magicDefense: magicDefense,
                races: orc ? new[] { UnitRace.Orc } : Array.Empty<UnitRace>(),
                sealboundOwnerTurnStarts: seal, hasHiding: hiding, hasFlying: flying, invincibleDuration: invincible);
        private static InMemoryCardDefinitionProvider Provider(UnitCardDefinition target) =>
            new InMemoryCardDefinitionProvider(new CardDefinition[] { Definition(), target });
        private static ArmedTrapState Install(BattleState state, PlayerId owner, TrapSpellCardDefinition definition,
            ICardUpgradeLevelProvider levels = null, bool clearEvents = true)
        {
            state.SetActivePlayer(owner); state.ClearTrapEvents();
            var hand = state.GetPlayer(owner).Hand.Add(definition.CardId);
            var trap = new TrapService(new InMemoryCardDefinitionProvider(new[] { definition }), levels)
                .Install(state, owner, definition.CardId, hand.RuntimeId);
            if (clearEvents) state.ClearTrapEvents();
            return trap;
        }
        private static UnitState Play(BattleState state, PlayerId owner, ICardDefinitionProvider provider, TileCoord? coord = null)
        {
            var definition = provider.GetAll().OfType<UnitCardDefinition>().Single();
            state.SetActivePlayer(owner); state.ClearTrapEvents(); state.ClearValuePopupEvents();
            state.GetPlayer(owner).Resources.Add(new ResourceSet(10, 0, 0, 0));
            var hand = state.GetPlayer(owner).Hand.Add(definition.CardId);
            return new PlayCardService(provider).PlayUnitCard(state, owner, definition.CardId, coord ?? TargetCoord, hand.RuntimeId);
        }
        private static PlayerId Opponent(PlayerId id) => id == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
        private static BattleState State(PlayerId owner)
        {
            var pMaster = new MasterState("master-player", PlayerId.Player, MasterCoord, 3, 333);
            var aMaster = new MasterState("master-ai", PlayerId.AI, MasterCoord, 3, 333);
            var p = new PlayerState(PlayerId.Player, new ResourceSet(), new DeckState(Array.Empty<string>()),
                new HandState(), new DiscardState(), pMaster);
            var a = new PlayerState(PlayerId.AI, new ResourceSet(), new DeckState(Array.Empty<string>()),
                new HandState(), new DiscardState(), aMaster);
            var pBoard = new BoardState(); pBoard.Place(MasterCoord, pMaster);
            var aBoard = new BoardState(); aBoard.Place(MasterCoord, aMaster);
            var state = new BattleState(p, a, pBoard, aBoard); state.RestoreRuntimeState(5, owner, PhaseType.Main);
            return state;
        }
    }
}
