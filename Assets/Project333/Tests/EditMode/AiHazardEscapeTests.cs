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
    public sealed class AiHazardEscapeTests
    {
        [TestCase(25, 1, 1)]
        [TestCase(25, 1, 3)]
        [TestCase(40, 2, 1)]
        [TestCase(40, 2, 3)]
        [TestCase(100, 1, 1)]
        public void Bomb_EscapesToEmptySafeTileAndPreservesHp(int hp, int effects, int depth)
        {
            var state = State();
            var unit = Unit(state, hp);
            for (var i = 0; i < effects; i++) Bomb(state);
            var key = AiBattleStateCopy.PositionKey(state);
            var planner = Planner(depth);
            var command = planner.GetNextCommand(state) as MoveOccupantCommand;
            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(key), "Planning mutated the battle.");
            Assert.That(command, Is.Not.Null);
            Assert.That(command.From, Is.EqualTo(unit.Position));
            Assert.That(command.To.Column, Is.EqualTo(4));
            Assert.That(state.AIBoard.GetOccupant(command.To), Is.Null);
            var other = Planner(depth).GetNextCommand(state) as MoveOccupantCommand;
            Assert.That(other.From, Is.EqualTo(command.From));
            Assert.That(other.To, Is.EqualTo(command.To));
            new MoveService().Move(state, PlayerId.AI, command.From, command.To);
            Assert.That(planner.GetNextCommand(state), Is.TypeOf<EndTurnCommand>(), "Do not shuffle after reaching safety.");
            NextTurn(state);
            Assert.That(unit.IsAlive, Is.True);
            Assert.That(unit.CurrentHp, Is.EqualTo(hp));
        }

        [Test]
        public void Firewall_EscapesAffectedRowUsingCapturedSpellDamage()
        {
            var state = State();
            var unit = Unit(state, 26);
            Firewall(state);
            var staying = AiBattleStateCopy.Create(state);
            NextTurn(staying);
            Assert.That(staying.AIBoard.GetOccupant(unit.Position), Is.Null);
            var move = Planner().GetNextCommand(state) as MoveOccupantCommand;
            Assert.That(move, Is.Not.Null);
            Assert.That(move.To.Row, Is.EqualTo(1));
            new MoveService().Move(state, PlayerId.AI, move.From, move.To);
            NextTurn(state);
            Assert.That(unit.CurrentHp, Is.EqualTo(26));
        }

        [TestCase("expired")]
        [TestCase("enemy-board")]
        [TestCase("invincible")]
        [TestCase("armor")]
        public void HarmlessHazard_DoesNotCauseUnproductiveMovement(string reason)
        {
            var state = State();
            var unit = Unit(state, 25, magicDefense: reason == "armor" ? 100 : 0);
            if (reason == "armor") Firewall(state);
            else
            {
                var effect = Bomb(state, reason == "enemy-board" ? PlayerId.AI : PlayerId.Player);
                if (reason == "expired") effect.Expire();
                if (reason == "invincible") unit.AddInvincibleEffect(InvincibleDurationType.GlobalTurnEnds, 3);
            }
            Assert.That(Planner(3).GetNextCommand(state), Is.TypeOf<EndTurnCommand>());
        }

        [TestCase("immobile")]
        [TestCase("drained")]
        [TestCase("sealbound")]
        [TestCase("building")]
        public void IllegalMovers_AreNeverMoved(string kind)
        {
            var state = State();
            if (kind == "building")
                state.AIBoard.Place(new TileCoord(0,0), new BuildingState("building", "building", PlayerId.AI,
                    new TileCoord(0,0), false, 0, 25, new ResourceSet()));
            else
            {
                var unit = Unit(state, 25, movable: kind != "immobile");
                if (kind == "drained") unit.IsDrained = true;
                if (kind == "sealbound") unit.EnterSealbound(3);
            }
            Bomb(state);
            Assert.That(Planner().GetNextCommand(state), Is.TypeOf<EndTurnCommand>());
        }

        [Test]
        public void NoSafeEmptyTile_DoesNotTradeAnAllyIntoTheHazard()
        {
            var state = State();
            Unit(state, 25);
            Bomb(state);
            var safe = new TileCoord(4,0);
            state.AIBoard.Place(safe, new UnitState("ally", "ally", PlayerId.AI, safe,
                AttackType.Melee, 0, 25, true, false, 0));
            Assert.That(Planner(3).GetNextCommand(state), Is.TypeOf<EndTurnCommand>());
        }

        [Test]
        public void OverlappingAreas_DoNotEscapeIntoAnEquallyLethalEffect()
        {
            var state = State();
            Unit(state, 25);
            Bomb(state);
            Bomb(state, startColumn: BiochemicalBombRules.RightAreaStartColumn);
            Assert.That(Planner(3).GetNextCommand(state), Is.TypeOf<EndTurnCommand>());
        }

        [Test]
        public void Master_EscapesLethalHazard()
        {
            var state = State();
            new MoveService().Move(state, PlayerId.AI, state.AI.Master.Position, new TileCoord(0,0));
            state.AI.Master.CurrentHp = 20;
            Bomb(state);
            var move = Planner().GetNextCommand(state) as MoveOccupantCommand;
            Assert.That(move, Is.Not.Null);
            Assert.That(move.From, Is.EqualTo(state.AI.Master.Position));
            Assert.That(move.To.Column, Is.EqualTo(4));
            new MoveService().Move(state, PlayerId.AI, move.From, move.To);
            NextTurn(state);
            Assert.That(state.IsEnded, Is.False);
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(20));
        }

        [Test]
        public void ImmediateVictory_IsPreferredToEscape()
        {
            var state = State();
            Unit(state, 25);
            Bomb(state);
            state.Player.Master.CurrentHp = 1;
            state.AI.Master.RemainingAttacksThisTurn = 1;
            var attack = Planner().GetNextCommand(state) as AttackCommand;
            Assert.That(attack, Is.Not.Null);
            new AttackService().Attack(state, PlayerId.AI, attack.AttackerCoord, attack.TargetCoord);
            Assert.That(state.Result.Winner, Is.EqualTo(PlayerId.AI));
        }

        private static BattleState State()
        {
            var deck = Enumerable.Repeat("unseen", 20).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.AI));
            state.SetPhase(PhaseType.Main);
            foreach (var player in new[] { state.Player, state.AI })
            {
                foreach (var card in player.Hand.CardIds.ToArray()) player.Hand.Remove(card);
                player.Master.RemainingAttacksThisTurn = 0;
            }
            new MoveService().Move(state, PlayerId.AI, state.AI.Master.Position, new TileCoord(4,1));
            return state;
        }

        private static UnitState Unit(BattleState state, int hp, bool movable = true, int magicDefense = 0)
        {
            var unit = new UnitState("escape-unit", "unit", PlayerId.AI, new TileCoord(0,0),
                AttackType.Melee, 30, hp, movable, false, 0, magicDefense: magicDefense);
            unit.RemainingAttacksThisTurn = 0;
            state.AIBoard.Place(unit.Position, unit);
            return unit;
        }

        private static PersistentEffectState Bomb(BattleState state, PlayerId owner = PlayerId.Player,
            int startColumn = BiochemicalBombRules.LeftAreaStartColumn)
        {
            var effect = new PersistentEffectState(BiochemicalBombRules.CardId, owner, BiochemicalBombRules.EffectId,
                state.TurnNumber, "After four global turn starts", new ResourceSet(), remainingTriggers: 4,
                effectDamage: 25, effectDamageType: DamageType.Fixed, targetStartColumn: startColumn);
            state.PersistentEffects.Add(effect);
            return effect;
        }

        private static void Firewall(BattleState state) => state.PersistentEffects.Add(new PersistentEffectState(
            "firewall", PlayerId.Player, "firewall", state.TurnNumber, "After three global turn starts",
            new ResourceSet(), targetRow: 0, remainingTriggers: 3, effectDamage: 20,
            effectDamageType: DamageType.Magic, capturedSpellPower: 7));

        private static void NextTurn(BattleState state)
        {
            new EndTurnService(new Random(41)).EndTurn(state);
            new TurnStartService(new ScienceUpkeepService()).ResolveTurnStart(state);
        }

        private static AiDecisionService Planner(int depth = 1) => new AiDecisionService(
            new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>()), new TargetingService(),
            ZeroCardUpgradeLevelProvider.Instance, new AiSearchOptions
            { MaxDepth = depth, MaxMilliseconds = 10000, MaxSimulations = 4000 });
    }
}