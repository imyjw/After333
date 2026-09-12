using System;
using System.IO;
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
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class AiRedDragonHazardTests
    {
        private static readonly TileCoord SummonTile = new TileCoord(0, 1);
        private static readonly TileCoord DragonTile = new TileCoord(4, 0);

        [TestCase(33, false)]
        [TestCase(34, true)]
        public void EnemyRedDragon_SummonMustSurviveEnemyTurnEndBeforeItsFirstOrdinaryAttack(int hp, bool shouldSummon)
        {
            var state = State();
            AddDragon(state);
            var card = Card("ordinary-attacker", hp);
            var provider = GiveCard(state, card);

            var command = Planner(provider).GetNextCommand(state);

            AssertSummonDecisionAndResolve(state, provider, card, command, shouldSummon);
            if (shouldSummon)
            {
                Assert.That(state.AIBoard.GetOccupant(SummonTile).CurrentHp, Is.EqualTo(1));
                Assert.That(state.AIBoard.GetOccupant(SummonTile).HasSummoningSickness, Is.False);
            }
            else
                Assert.That(state.AIBoard.GetOccupant(SummonTile), Is.Null);
        }

        [Test]
        public void EnemyRedDragon_HoldsActualElfLongbowScoutThatWouldDieBeforeItsFirstAttack()
        {
            var state = State();
            AddDragon(state);
            var jsonPath = Path.Combine(Application.dataPath, "Project333", "Resources", "Project333", "Data", "cards.json");
            var provider = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(jsonPath)).CreateProvider();
            var card = (UnitCardDefinition)provider.GetRequired("ElfLongbowScout");
            state.AI.Hand.Add(card.CardId);
            state.AI.Resources.Add(card.Cost);

            var command = Planner(provider).GetNextCommand(state);

            Assert.That(command, Is.TypeOf<EndTurnCommand>());
            var scout = new PlayCardService(provider).PlayUnitCard(state, PlayerId.AI, card.CardId, SummonTile);
            Assert.That(scout.HasSummoningSickness, Is.True);
            ResolveUntilNextAiMain(state);
            Assert.That(state.AIBoard.GetOccupant(SummonTile), Is.Null);
        }

        [TestCase(100, 0, false)]
        [TestCase(0, 1, true)]
        public void EnemyRedDragon_UsesMagicDefenseAndDoesNotAddSpellPowerToItsTriggeredDamage(
            int physicalDefense, int magicDefense, bool shouldSummon)
        {
            var state = State();
            AddDragon(state, spellPower: 10);
            var card = Card("armored-attacker", 33, physicalDefense: physicalDefense, magicDefense: magicDefense);
            var provider = GiveCard(state, card);

            var command = Planner(provider).GetNextCommand(state);

            AssertSummonDecisionAndResolve(state, provider, card, command, shouldSummon);
            if (shouldSummon)
                Assert.That(state.AIBoard.GetOccupant(SummonTile).CurrentHp, Is.EqualTo(1));
            else
                Assert.That(state.AIBoard.GetOccupant(SummonTile), Is.Null);
        }

        [Test]
        public void EnemyRedDragon_MultipleSourcesCombineBeforeTheSummonedUnitCanAct()
        {
            var state = State();
            AddDragon(state);
            AddDragon(state, coord: new TileCoord(0, 0));
            var card = Card("two-dragon-target", 50);
            var provider = GiveCard(state, card);

            var command = Planner(provider).GetNextCommand(state);

            AssertSummonDecisionAndResolve(state, provider, card, command, shouldSummon: false);
            Assert.That(state.AIBoard.GetOccupant(SummonTile), Is.Null);
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(333 - 66));
        }

        [Test]
        public void EnemyRedDragon_ErasedSourceDoesNotPreventProfitableFragileSummon()
        {
            var state = State();
            AddDragon(state).ApplyErasure();
            var card = Card("suppressed-source", 33);
            var provider = GiveCard(state, card);

            var command = Planner(provider).GetNextCommand(state);

            AssertSummonDecisionAndResolve(state, provider, card, command, shouldSummon: true);
            Assert.That(state.AIBoard.GetOccupant(SummonTile).CurrentHp, Is.EqualTo(33));
        }

        [Test]
        public void EnemyRedDragon_CurrentlyDrainedSourceCanReactivateBeforeItsEndTurnDamage()
        {
            var state = State();
            // Synthetic upkeep-bearing source exercises existing upkeep/state transitions without changing card data.
            var dragon = AddDragon(state, sciencePowerUpkeep: 1);
            dragon.IsDrained = true;
            state.Player.Resources.Add(new ResourceSet(0, 0, 1, 0));
            var card = Card("reactivated-source", 33);
            var provider = GiveCard(state, card);

            var command = Planner(provider).GetNextCommand(state);

            Assert.That(command, Is.TypeOf<EndTurnCommand>());
            Assert.That(dragon.IsDrained, Is.True, "Forecasting must not pay the authoritative source's upkeep.");
            new PlayCardService(provider).PlayUnitCard(state, PlayerId.AI, card.CardId, SummonTile);
            ResolveUntilNextAiMain(state);
            Assert.That(dragon.IsDrained, Is.False);
            Assert.That(state.Player.Resources.Power, Is.Zero);
            Assert.That(state.AIBoard.GetOccupant(SummonTile), Is.Null);
        }

        [Test]
        public void EnemyRedDragon_SourceRemovedByEnemyTurnStartDamageDoesNotForbidFragileSummon()
        {
            var state = State();
            var dragon = AddDragon(state);
            var firewall = new PersistentEffectState("Firewall", PlayerId.AI, "firewall", state.TurnNumber,
                "After one global turn start", new ResourceSet(), targetRow: 0, remainingTriggers: 1,
                effectDamage: 40, effectDamageType: DamageType.Magic);
            state.PersistentEffects.Add(firewall);
            var card = Card("source-dies-at-turn-start", 33);
            var provider = GiveCard(state, card);

            var command = Planner(provider).GetNextCommand(state);

            Assert.That(command, Is.TypeOf<PlayUnitCardCommand>());
            Assert.That(state.PlayerBoard.GetOccupant(DragonTile), Is.SameAs(dragon));
            Assert.That(firewall.RemainingTriggers, Is.EqualTo(1));
            Execute(state, provider, command);
            ResolveUntilNextAiMain(state);
            Assert.That(state.PlayerBoard.GetOccupant(DragonTile), Is.Null);
            Assert.That(firewall.IsExpired, Is.True);
            Assert.That(state.AIBoard.GetOccupant(SummonTile).CurrentHp, Is.EqualTo(33));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(333));
        }

        [Test]
        public void EnemyRedDragon_KillingSourceMakesFragileFollowupSummonViable()
        {
            var state = State();
            AddDragon(state);
            var unit = Card("after-dragon-removal", 33);
            var spell = new DamageSpellCardDefinition("remove-dragon", "Remove dragon", new ResourceSet(0, 0, 0, 1), 40);
            var provider = new InMemoryCardDefinitionProvider(new CardDefinition[] { unit, spell });
            state.AI.Hand.Add(spell.CardId);
            state.AI.Hand.Add(unit.CardId);
            state.AI.Resources.Add(new ResourceSet(0, 0, 0, 2));
            var planner = Planner(provider);

            var usedRemovalSpell = false;
            var summonedUnit = false;
            for (var action = 0; action < 3 && !(usedRemovalSpell && summonedUnit); action++)
            {
                var command = planner.GetNextCommand(state);
                Assert.That(command, Is.Not.TypeOf<EndTurnCommand>(),
                    "The AI must finish removing the dragon and summoning the unit before yielding the turn.");
                if (command is CastDamageSpellCommand removal)
                {
                    Assert.That(removal.CardId, Is.EqualTo(spell.CardId));
                    Assert.That(removal.TargetOwnerId, Is.EqualTo(PlayerId.Player));
                    Assert.That(removal.TargetCoord, Is.EqualTo(DragonTile));
                    usedRemovalSpell = true;
                }
                if (command is PlayUnitCardCommand summon)
                {
                    Assert.That(summon.CardId, Is.EqualTo(unit.CardId));
                    summonedUnit = true;
                }
                Execute(state, provider, command);
            }

            Assert.That(usedRemovalSpell, Is.True);
            Assert.That(summonedUnit, Is.True);
            Assert.That(state.PlayerBoard.GetOccupant(DragonTile), Is.Null);
            Assert.That(state.AIBoard.GetOccupant(SummonTile).CardId, Is.EqualTo(unit.CardId));
            Assert.That(state.AI.Hand.Count, Is.Zero, "Both cards must actually have been used.");
            Assert.That(state.ActivePlayerId, Is.EqualTo(PlayerId.AI));
            Assert.That(state.Phase, Is.EqualTo(PhaseType.Main));
            ResolveUntilNextAiMain(state);
            Assert.That(state.AIBoard.GetOccupant(SummonTile).CurrentHp, Is.EqualTo(33));
        }

        [Test]
        public void EnemyRedDragon_RushUnitCanWinBeforeTheEnemyEndTurnTrigger()
        {
            var state = State();
            AddDragon(state);
            state.Player.Master.CurrentHp = 20;
            var provider = GiveCard(state, Card("rush-finisher", 33, rush: true));
            var planner = Planner(provider);

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
        public void EnemyRedDragon_DoomedBlueDragonCanStillProvideWorthwhileOwnTurnEndHealing()
        {
            var state = State();
            AddDragon(state);
            var army = state.AIBoard.EnumerateOccupants().Where(unit => unit.Kind != OccupantKind.Master).ToArray();
            foreach (var unit in army) unit.CurrentHp = 100;
            // Reduced-HP fixture isolates a real BlueDragon end-turn heal before the enemy's lethal trigger.
            var card = new UnitCardDefinition("BlueDragon", "Blue Dragon", new ResourceSet(0, 0, 0, 1),
                AttackType.Ranged, 0, 20, canMove: false, isScience: false, sciencePowerUpkeep: 0);
            var provider = GiveCard(state, card);

            var command = Planner(provider).GetNextCommand(state);

            Assert.That(command, Is.TypeOf<PlayUnitCardCommand>(),
                "Losing this summon later must not erase the healing it provides to the existing army first.");
            Execute(state, provider, command);
            ResolveUntilNextAiMain(state);
            Assert.That(state.AIBoard.GetOccupant(SummonTile), Is.Null);
            Assert.That(army.Select(unit => unit.CurrentHp), Is.All.EqualTo(100));
        }

        [Test]
        public void EnemyRedDragon_ProjectionDoesNotConsumeTheRealTurnOrChangeBattleState()
        {
            var state = State();
            var dragon = AddDragon(state, spellPower: 10);
            var provider = GiveCard(state, Card("unchanged", 33));
            var before = AiBattleStateCopy.PositionKey(state);
            var hand = state.AI.Hand.Cards[0];
            var popupCount = state.ValuePopupEvents.Count;
            var resourceCount = state.ResourceChangeEvents.Count;
            var drawCount = state.CardDrawEvents.Count;
            var turn = state.TurnNumber;

            Planner(provider).GetNextCommand(state);

            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(before));
            Assert.That(state.AI.Hand.Cards[0], Is.SameAs(hand));
            Assert.That(state.PlayerBoard.GetOccupant(DragonTile), Is.SameAs(dragon));
            Assert.That(dragon.CurrentHp, Is.EqualTo(40));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.ValuePopupEvents.Count, Is.EqualTo(popupCount));
            Assert.That(state.ResourceChangeEvents.Count, Is.EqualTo(resourceCount));
            Assert.That(state.CardDrawEvents.Count, Is.EqualTo(drawCount));
            Assert.That(state.TurnNumber, Is.EqualTo(turn));
            Assert.That(state.ActivePlayerId, Is.EqualTo(PlayerId.AI));
            Assert.That(state.Phase, Is.EqualTo(PhaseType.Main));
        }

        private static BattleState State()
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
            for (var column = 0; column < BoardState.ColumnCount; column++)
            {
                for (var row = 0; row < BoardState.RowCount; row++)
                {
                    var coord = new TileCoord(column, row);
                    if (coord == SummonTile || !state.AIBoard.IsEmpty(coord)) continue;
                    var fixedUnit = new UnitState("fixed-" + column + "-" + row, "fixed", PlayerId.AI, coord,
                        AttackType.Melee, 0, 200, canMove: false, isScience: false, sciencePowerUpkeep: 0);
                    fixedUnit.RemainingAttacksThisTurn = 0;
                    state.AIBoard.Place(coord, fixedUnit);
                }
            }
            return state;
        }

        private static UnitState AddDragon(BattleState state, int spellPower = 0, TileCoord? coord = null,
            int sciencePowerUpkeep = 0)
        {
            var tile = coord ?? DragonTile;
            var dragon = new UnitState("red-dragon-" + tile.Column + "-" + tile.Row, "RedDragon", PlayerId.Player,
                tile, AttackType.Melee, 0, 40, canMove: false, isScience: sciencePowerUpkeep > 0,
                sciencePowerUpkeep: sciencePowerUpkeep,
                damageType: DamageType.Magic, spellPower: spellPower);
            dragon.RemainingAttacksThisTurn = 0;
            state.PlayerBoard.Place(tile, dragon);
            return dragon;
        }

        private static UnitCardDefinition Card(string id, int hp, int physicalDefense = 0, int magicDefense = 0,
            bool rush = false)
        {
            return new UnitCardDefinition(id, id, new ResourceSet(0, 0, 0, 1), AttackType.Ranged, 200, hp,
                canMove: false, isScience: false, sciencePowerUpkeep: 0, physicalDefense: physicalDefense,
                magicDefense: magicDefense, hasRush: rush);
        }

        private static ICardDefinitionProvider GiveCard(BattleState state, CardDefinition card)
        {
            state.AI.Hand.Add(card.CardId);
            state.AI.Resources.Add(card.Cost);
            return new InMemoryCardDefinitionProvider(new[] { card });
        }

        private static AiDecisionService Planner(ICardDefinitionProvider provider)
        {
            return new AiDecisionService(provider, new TargetingService(), ZeroCardUpgradeLevelProvider.Instance,
                new AiSearchOptions { MaxDepth = 3, MaxMilliseconds = 10000, MaxSimulations = 16000 });
        }

        private static void AssertSummonDecisionAndResolve(BattleState state, ICardDefinitionProvider provider,
            UnitCardDefinition card, IBattleCommand command, bool shouldSummon)
        {
            if (shouldSummon)
            {
                Assert.That(command, Is.TypeOf<PlayUnitCardCommand>());
                Execute(state, provider, command);
            }
            else
            {
                Assert.That(command, Is.TypeOf<EndTurnCommand>());
                new PlayCardService(provider).PlayUnitCard(state, PlayerId.AI, card.CardId, SummonTile);
            }
            ResolveUntilNextAiMain(state);
        }

        private static void Execute(BattleState state, ICardDefinitionProvider provider, IBattleCommand command)
        {
            new BattleCommandProcessor(new PlayCardService(provider), new SpellService(provider), new MoveService(),
                new AttackService(new TargetingService(), new System.Random(41).Next), new EndTurnService(new System.Random(41)))
                .Execute(state, PlayerId.AI, command);
        }

        private static void ResolveUntilNextAiMain(BattleState state)
        {
            var endTurn = new EndTurnService(new System.Random(41));
            var turnStart = new TurnStartService(new ScienceUpkeepService());
            endTurn.EndTurn(state);
            turnStart.ResolveTurnStart(state);
            Assert.That(state.ActivePlayerId, Is.EqualTo(PlayerId.Player));
            endTurn.EndTurn(state);
            turnStart.ResolveTurnStart(state);
            Assert.That(state.ActivePlayerId, Is.EqualTo(PlayerId.AI));
            Assert.That(state.Phase, Is.EqualTo(PhaseType.Main));
        }
    }
}
