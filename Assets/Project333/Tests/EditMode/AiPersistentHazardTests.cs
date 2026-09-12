using System;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class AiPersistentHazardTests
    {
        private static readonly TileCoord HazardTile = new TileCoord(0, 1);
        private static readonly TileCoord SafeTile = new TileCoord(4, 0);

        [Test]
        public void BiochemicalBomb_DoesNotSpendOnProducerThatDiesBeforeProducingResources()
        {
            var state = State();
            AddBomb(state);
            var card = new BuildingCardDefinition("fragile-producer", "Producer", new ResourceSet(0, 0, 0, 1),
                canAttack: false, attack: 0, health: 25, turnStartResourceGain: new ResourceSet(0, 0, 0, 3));
            var provider = GiveCard(state, card);

            Assert.That(Planner(provider).GetNextCommand(state), Is.TypeOf<EndTurnCommand>());

            new PlayCardService(provider).PlayBuildingCard(state, PlayerId.AI, card.CardId, HazardTile);
            ResolveNextTurn(state);
            Assert.That(state.AIBoard.GetOccupant(HazardTile), Is.Null,
                "The shared turn rules must confirm that this producer never reaches an AI income step.");
            Assert.That(state.AI.Resources.Gold, Is.Zero);
        }

        [TestCase(25, false, 1)]
        [TestCase(26, true, 1)]
        [TestCase(25, false, 3)]
        [TestCase(26, true, 3)]
        public void BiochemicalBomb_HighAttackSummonUsesActualSurvivalAtExactDamageBoundary(int hp, bool shouldSummon, int maxDepth)
        {
            var state = State();
            AddBomb(state);
            var card = Card("high-attack", hp);
            var provider = GiveCard(state, card);
            var command = Planner(provider, maxDepth).GetNextCommand(state);

            if (shouldSummon)
            {
                Assert.That(command, Is.TypeOf<PlayUnitCardCommand>());
                Execute(state, provider, command);
            }
            else
            {
                Assert.That(command, Is.TypeOf<EndTurnCommand>());
                new PlayCardService(provider).PlayUnitCard(state, PlayerId.AI, card.CardId, HazardTile);
            }

            ResolveNextTurn(state);
            if (shouldSummon)
                Assert.That(state.AIBoard.GetOccupant(HazardTile).CurrentHp, Is.EqualTo(1));
            else
                Assert.That(state.AIBoard.GetOccupant(HazardTile), Is.Null);
        }

        [Test]
        public void BiochemicalBomb_WhenSafeTileIsAvailable_PlacesFragileUnitOutsideAffectedColumns()
        {
            var state = State(leaveSafeTile: true);
            AddBomb(state);
            var provider = GiveCard(state, Card("safe-placement", 25));

            var command = Planner(provider).GetNextCommand(state);

            Assert.That(command, Is.TypeOf<PlayUnitCardCommand>());
            Assert.That(((PlayUnitCardCommand)command).TargetCoord, Is.EqualTo(SafeTile));
            Execute(state, provider, command);
            ResolveNextTurn(state);
            Assert.That(state.AIBoard.GetOccupant(SafeTile).CurrentHp, Is.EqualTo(25));
        }

        [Test]
        public void BiochemicalBomb_OverlappingEffectsAccountForCombinedLethalDamage()
        {
            var state = State();
            AddBomb(state);
            AddBomb(state);
            var card = Card("stacked-damage", 40);
            var provider = GiveCard(state, card);

            Assert.That(Planner(provider).GetNextCommand(state), Is.TypeOf<EndTurnCommand>());

            new PlayCardService(provider).PlayUnitCard(state, PlayerId.AI, card.CardId, HazardTile);
            ResolveNextTurn(state);
            Assert.That(state.AIBoard.GetOccupant(HazardTile), Is.Null);
        }

        [TestCase(0, false, 0)]
        [TestCase(5, true, 4)]
        public void Firewall_UsesCapturedSpellPowerAndSummonedUnitDefense(int magicDefense, bool shouldSummon, int expectedHp)
        {
            var state = State();
            state.PersistentEffects.Add(new PersistentEffectState("firewall", PlayerId.Player, "firewall",
                state.TurnNumber, "After three global turn starts", new ResourceSet(), targetRow: 1,
                remainingTriggers: 3, effectDamage: 20, effectDamageType: DamageType.Magic, capturedSpellPower: 7));
            var card = Card("firewall-target", 26, magicDefense: magicDefense);
            var provider = GiveCard(state, card);
            var command = Planner(provider).GetNextCommand(state);

            if (shouldSummon)
            {
                Assert.That(command, Is.TypeOf<PlayUnitCardCommand>());
                Execute(state, provider, command);
            }
            else
            {
                Assert.That(command, Is.TypeOf<EndTurnCommand>());
                new PlayCardService(provider).PlayUnitCard(state, PlayerId.AI, card.CardId, HazardTile);
            }

            ResolveNextTurn(state);
            if (shouldSummon)
                Assert.That(state.AIBoard.GetOccupant(HazardTile).CurrentHp, Is.EqualTo(expectedHp));
            else
                Assert.That(state.AIBoard.GetOccupant(HazardTile), Is.Null);
        }

        [Test]
        public void BiochemicalBomb_ExpiredEffectDoesNotPreventProfitableFragileSummon()
        {
            var state = State();
            AddBomb(state).Expire();
            var provider = GiveCard(state, Card("expired-hazard", 25));

            var command = Planner(provider).GetNextCommand(state);

            Assert.That(command, Is.TypeOf<PlayUnitCardCommand>());
            Execute(state, provider, command);
            ResolveNextTurn(state);
            Assert.That(state.AIBoard.GetOccupant(HazardTile).CurrentHp, Is.EqualTo(25));
        }

        [Test]
        public void BiochemicalBomb_RushUnitCanStillBeSummonedForWinningAttackBeforeTheNextTrigger()
        {
            var state = State();
            AddBomb(state);
            state.Player.Master.CurrentHp = 20;
            var provider = GiveCard(state, Card("rush-finisher", 25, rush: true));
            var planner = Planner(provider, maxDepth: 3);

            var summon = planner.GetNextCommand(state);
            Assert.That(summon, Is.TypeOf<PlayUnitCardCommand>());
            Execute(state, provider, summon);

            var attack = planner.GetNextCommand(state);
            Assert.That(attack, Is.TypeOf<AttackCommand>());
            Execute(state, provider, attack);
            Assert.That(state.IsEnded, Is.True);
            Assert.That(state.Result.Winner, Is.EqualTo(PlayerId.AI));
        }

        [Test]
        public void BiochemicalBomb_SummonThenMoveDoesNotEarnProtectionFromAGuardThatWillDie()
        {
            var state = State();
            AddBomb(state);
            state.AI.Master.CurrentHp = 20;
            var guardDestination = new TileCoord(0, 0);
            var summonSource = new TileCoord(1, 1);
            state.AIBoard.Remove(guardDestination);
            state.AIBoard.Remove(summonSource);
            var producer = new BuildingState("vulnerable-producer", "producer", PlayerId.AI, HazardTile,
                canAttack: false, attack: 0, maxHp: 200, turnStartResourceGain: new ResourceSet(0, 0, 0, 1));
            state.AIBoard.Place(HazardTile, producer);
            var card = new UnitCardDefinition("doomed-guard", "Guard", new ResourceSet(0, 0, 0, 1),
                AttackType.Melee, 3, 25, canMove: true, isScience: false, sciencePowerUpkeep: 0);
            var provider = GiveCard(state, card);

            var command = Planner(provider, maxDepth: 3).GetNextCommand(state);

            Assert.That(command, Is.TypeOf<EndTurnCommand>(),
                "Summon -> move into the producer's front tile cannot justify spending a card on transient cover.");
            new PlayCardService(provider).PlayUnitCard(state, PlayerId.AI, card.CardId, summonSource);
            Execute(state, provider, new MoveOccupantCommand(summonSource, guardDestination));
            ResolveNextTurn(state);
            Assert.That(state.AIBoard.GetOccupant(guardDestination), Is.Null);
            Assert.That(state.AIBoard.GetOccupant(HazardTile), Is.SameAs(producer));
            Assert.That(producer.CurrentHp, Is.EqualTo(175));
        }

        [Test]
        public void BiochemicalBomb_EndureAllowsUnitToSurviveAndRemainWorthSummoning()
        {
            var state = State();
            AddBomb(state);
            var provider = GiveCard(state, Card("endure-survivor", 25, endure: true));

            var command = Planner(provider, maxDepth: 3).GetNextCommand(state);

            Assert.That(command, Is.TypeOf<PlayUnitCardCommand>());
            Execute(state, provider, command);
            ResolveNextTurn(state);
            var survivor = state.AIBoard.GetOccupant(HazardTile);
            Assert.That(survivor, Is.Not.Null);
            Assert.That(survivor.CurrentHp, Is.EqualTo(1));
            Assert.That(survivor.EndureUsed, Is.True);
        }

        [TestCase(InvincibleDurationType.Always, true)]
        [TestCase(InvincibleDurationType.UntilTurnEnd, false)]
        public void BiochemicalBomb_InvincibilityMustStillBeActiveWhenTheHazardTriggers(
            InvincibleDurationType duration, bool shouldSummon)
        {
            var state = State();
            AddBomb(state);
            var card = Card("invincible-target", 25, invincible: duration);
            var provider = GiveCard(state, card);
            var command = Planner(provider, maxDepth: 3).GetNextCommand(state);

            if (shouldSummon)
            {
                Assert.That(command, Is.TypeOf<PlayUnitCardCommand>());
                Execute(state, provider, command);
            }
            else
            {
                Assert.That(command, Is.TypeOf<EndTurnCommand>());
                new PlayCardService(provider).PlayUnitCard(state, PlayerId.AI, card.CardId, HazardTile);
            }

            ResolveNextTurn(state);
            if (shouldSummon)
                Assert.That(state.AIBoard.GetOccupant(HazardTile).CurrentHp, Is.EqualTo(25));
            else
                Assert.That(state.AIBoard.GetOccupant(HazardTile), Is.Null);
        }

        [Test]
        public void BiochemicalBomb_DemonKingRetainsValueWhenLethalDamageStartsRevival()
        {
            var state = State();
            AddBomb(state);
            AddBomb(state);
            var card = new UnitCardDefinition(DemonKingRules.CardId, "Demon King",
                new ResourceSet(DemonKingRules.ManaCost, 0, 0, DemonKingRules.GoldCost), AttackType.Melee,
                DemonKingRules.BaseAttack, DemonKingRules.BaseHealth, canMove: true, isScience: false,
                sciencePowerUpkeep: 0, physicalDefense: DemonKingRules.PhysicalDefense,
                magicDefense: DemonKingRules.MagicDefense);
            var provider = GiveCard(state, card);
            // With ample reserves, the pending revival is worth the real listed cost; a removed unit is not.
            state.AI.Resources.Add(new ResourceSet(20, 0, 0, 20));

            var command = Planner(provider, maxDepth: 3).GetNextCommand(state);

            Assert.That(command, Is.TypeOf<PlayUnitCardCommand>());
            Execute(state, provider, command);
            ResolveNextTurn(state);
            var pending = state.AIBoard.GetOccupant(HazardTile);
            Assert.That(pending, Is.Not.Null);
            Assert.That(pending.CurrentHp, Is.LessThanOrEqualTo(0));
            Assert.That(pending.IsDemonKingRevivalPending, Is.True);
        }

        [Test]
        public void HazardPlanning_DoesNotMutateAuthoritativeStateOrConsumeTriggers()
        {
            var state = State();
            var effect = AddBomb(state);
            var provider = GiveCard(state, Card("unchanged", 25));
            var before = AiBattleStateCopy.PositionKey(state);
            var occupants = state.AIBoard.EnumerateOccupants().ToArray();
            var hp = occupants.Select(unit => unit.CurrentHp).ToArray();
            var hand = state.AI.Hand.Cards[0];
            var popupCount = state.ValuePopupEvents.Count;
            var areaEventCount = state.AreaSpellEffectEvents.Count;

            Planner(provider).GetNextCommand(state);

            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(before));
            Assert.That(state.AIBoard.EnumerateOccupants().ToArray(), Is.EqualTo(occupants));
            Assert.That(occupants.Select(unit => unit.CurrentHp).ToArray(), Is.EqualTo(hp));
            Assert.That(state.AI.Hand.Cards[0], Is.SameAs(hand));
            Assert.That(state.PersistentEffects.Single(), Is.SameAs(effect));
            Assert.That(effect.RemainingTriggers, Is.EqualTo(4));
            Assert.That(effect.IsExpired, Is.False);
            Assert.That(state.ValuePopupEvents.Count, Is.EqualTo(popupCount));
            Assert.That(state.AreaSpellEffectEvents.Count, Is.EqualTo(areaEventCount));
            Assert.That(state.ActivePlayerId, Is.EqualTo(PlayerId.AI));
            Assert.That(state.Phase, Is.EqualTo(PhaseType.Main));
        }

        private static BattleState State(bool leaveSafeTile = false)
        {
            var deck = Enumerable.Repeat("unseen", 20).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.AI));
            state.SetPhase(PhaseType.Main);
            foreach (var player in new[] { state.Player, state.AI })
            {
                foreach (var id in player.Hand.CardIds.ToArray()) player.Hand.Remove(id);
                player.Resources.Spend(player.Resources.Clone());
                player.Master.RemainingAttacksThisTurn = 0;
            }
            // Keep the master outside the bomb, with no other combat or movement benefit to obscure the card choice.
            state.AIBoard.Move(state.AI.Master.Position, new TileCoord(4, 1));
            for (var column = 0; column < BoardState.ColumnCount; column++)
            {
                for (var row = 0; row < BoardState.RowCount; row++)
                {
                    var coord = new TileCoord(column, row);
                    if (!state.AIBoard.IsEmpty(coord) || coord == HazardTile || (leaveSafeTile && coord == SafeTile)) continue;
                    var occupant = new UnitState("fixed-" + column + "-" + row, "fixed", PlayerId.AI, coord,
                        AttackType.Melee, 0, 200, canMove: false, isScience: false, sciencePowerUpkeep: 0);
                    occupant.RemainingAttacksThisTurn = 0;
                    state.AIBoard.Place(coord, occupant);
                }
            }
            return state;
        }

        private static UnitCardDefinition Card(string id, int hp, int magicDefense = 0, bool rush = false,
            bool endure = false, InvincibleDurationType invincible = InvincibleDurationType.None)
        {
            // High attack reproduces the phantom immediate material reward even without a producer bonus.
            return new UnitCardDefinition(id, id, new ResourceSet(0, 0, 0, 1), AttackType.Ranged, 200, hp,
                canMove: false, isScience: false, sciencePowerUpkeep: 0, magicDefense: magicDefense, hasRush: rush,
                hasEndure: endure, invincibleDuration: invincible);
        }

        private static ICardDefinitionProvider GiveCard(BattleState state, CardDefinition card)
        {
            state.AI.Hand.Add(card.CardId);
            state.AI.Resources.Add(card.Cost);
            return new InMemoryCardDefinitionProvider(new[] { card });
        }

        private static PersistentEffectState AddBomb(BattleState state)
        {
            var effect = new PersistentEffectState(BiochemicalBombRules.CardId, PlayerId.Player, BiochemicalBombRules.EffectId,
                state.TurnNumber, "After four global turn starts", new ResourceSet(),
                remainingTriggers: BiochemicalBombRules.TriggerCount, effectDamage: BiochemicalBombRules.BaseDamage,
                effectDamageType: DamageType.Fixed, targetStartColumn: BiochemicalBombRules.LeftAreaStartColumn);
            state.PersistentEffects.Add(effect);
            return effect;
        }

        private static AiDecisionService Planner(ICardDefinitionProvider provider, int maxDepth = 1)
        {
            return new AiDecisionService(provider, new TargetingService(), ZeroCardUpgradeLevelProvider.Instance,
                new AiSearchOptions { MaxDepth = maxDepth, MaxMilliseconds = 10000, MaxSimulations = 16000 });
        }

        private static void Execute(BattleState state, ICardDefinitionProvider provider, IBattleCommand command)
        {
            new BattleCommandProcessor(new PlayCardService(provider), new SpellService(provider), new MoveService(),
                new AttackService(new TargetingService(), new Random(41).Next), new EndTurnService(new Random(41)))
                .Execute(state, PlayerId.AI, command);
        }

        private static void ResolveNextTurn(BattleState state)
        {
            new EndTurnService(new Random(41)).EndTurn(state);
            new TurnStartService(new ScienceUpkeepService()).ResolveTurnStart(state);
        }
    }
}
