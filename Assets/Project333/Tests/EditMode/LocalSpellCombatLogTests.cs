using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Tests.EditMode
{
    public sealed class LocalSpellCombatLogTests
    {
        [TestCase(PlasmaShieldRules.CardId)]
        [TestCase(ManaShieldRules.CardId)]
        public void ShieldLogsIdentifyBothActorsBothFieldsAndUnitsOrBuildings(string cardId)
        {
            foreach (var actor in new[] { PlayerId.Player, PlayerId.AI })
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            foreach (var building in new[] { false, true })
            {
                var state = CreateState();
                var coord = new TileCoord(1, 0);
                OccupantState target = building
                    ? new BuildingState("target", "Mine", owner, coord, false, 0, 30)
                    : new UnitState("target", "OrcWarrior", owner, coord, AttackType.Melee, 20, 40, true, false, 0);
                state.GetBoard(owner).Place(coord, target);
                var text = CombatLogFormatter.FormatCommand(actor,
                    new CastScriptedSpellCommand(cardId, owner, coord), state, Name);
                var subject = actor == PlayerId.Player ? "플레이어가" : "상대방이";
                var possessive = owner == PlayerId.Player ? "플레이어의" : "상대방의";
                Assert.That(text, Is.EqualTo($"{subject} {possessive} {Name(target.CardId)}(1,0)에게 {Name(cardId)} 마법카드를 사용했다."));
                state.GetBoard(owner).Remove(coord);
                Assert.That(text, Does.Contain(Name(target.CardId)), "Captured logs retain the original target after removal.");
            }
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void TargetlessSpellDoesNotInventAMasterRecipient(PlayerId actor)
        {
            var text = CombatLogFormatter.FormatCommand(actor,
                new CastScriptedSpellCommand("Daehwandan"), CreateState(), Name);
            Assert.That(text, Is.EqualTo((actor == PlayerId.Player ? "플레이어가" : "상대방이") + " 대환단 마법카드를 사용했다."));
        }

        [TestCase("Firewall")]
        [TestCase("FireSpear")]
        public void RowSpellsDoNotNameAnArbitraryOccupantAsTheOnlyTarget(string cardId)
        {
            var state = CreateState();
            var coord = new TileCoord(1, 0);
            state.AIBoard.Place(coord, new UnitState("target", "OrcWarrior", PlayerId.AI, coord, AttackType.Melee, 20, 40, true, false, 0));
            Assert.That(CombatLogFormatter.FormatCommand(PlayerId.Player,
                new CastScriptedSpellCommand(cardId, PlayerId.AI, coord), state, Name),
                Is.EqualTo($"플레이어가 {Name(cardId)} 마법카드를 사용했다."));
        }

        [Test]
        public void MissingTargetOrContextFallsBackWithoutThrowing()
        {
            var command = new CastScriptedSpellCommand(ManaShieldRules.CardId, PlayerId.AI, new TileCoord(1, 0));
            var expected = "플레이어가 마나쉴드 마법카드를 사용했다.";
            Assert.That(CombatLogFormatter.FormatCommand(PlayerId.Player, command, CreateState(), Name), Is.EqualTo(expected));
            Assert.That(CombatLogFormatter.FormatCommand(PlayerId.Player, command, null, Name), Is.EqualTo(expected));
            Assert.That(CombatLogFormatter.FormatCommand(PlayerId.Player, command), Does.Contain(ManaShieldRules.CardId));
        }

        private static BattleState CreateState()
        {
            var deck = Enumerable.Range(0, 10).Select(i => "Card" + i).ToArray();
            return new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.Player));
        }

        private static string Name(string id) => id switch
        {
            PlasmaShieldRules.CardId => "플라즈마 쉴드",
            ManaShieldRules.CardId => "마나쉴드",
            "OrcWarrior" => "오크 전사",
            "Mine" => "금광",
            "Daehwandan" => "대환단",
            "Firewall" => "파이어월",
            "FireSpear" => "파이어스피어",
            _ => id
        };
    }
}
