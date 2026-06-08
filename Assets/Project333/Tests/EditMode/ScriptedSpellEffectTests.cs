using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class ScriptedSpellEffectTests
    {
        [Test]
        public void CastScriptedSpell_Daehwandan_BuffsMasterAndAddsPersistentQiGain()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new ScriptedSpellCardDefinition(
                    cardId: "Daehwandan",
                    displayName: "대환단",
                    cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 3),
                    effectId: "daehwandan"),
            });

            battleState.Player.Hand.Add("Daehwandan");

            spellService.CastScriptedSpell(battleState, PlayerId.Player, "Daehwandan");

            Assert.That(battleState.Player.Hand.Contains("Daehwandan"), Is.False);
            Assert.That(battleState.Player.Discard.CardIds, Does.Contain("Daehwandan"));
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(0));
            Assert.That(battleState.Player.Master.Attack, Is.EqualTo(33));
            Assert.That(battleState.PersistentEffects.Count, Is.EqualTo(1));
            Assert.That(battleState.PersistentEffects[0].TurnStartResourceGain.Qi, Is.EqualTo(3));
            Assert.That(battleState.PersistentEffects[0].OwnerTurnStartsRemaining, Is.EqualTo(3));
        }

        [Test]
        public void ResolveTurnStart_CheonraJimang_DestroysMarkedUnitBeforeResourceGain()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new ScriptedSpellCardDefinition(
                    cardId: "CheonraJimang",
                    displayName: "천라지망",
                    cost: new ResourceSet(mana: 0, qi: 5, power: 0, gold: 0),
                    effectId: "cheonra_jimang"),
            });
            var turnStartService = new TurnStartService();
            var markedUnit = new UnitState(
                runtimeId: "marked-unit",
                cardId: "GoldMiner",
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 3,
                maxHp: 15,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                turnStartResourceGain: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 1));

            battleState.PlayerBoard.Place(new TileCoord(0, 0), markedUnit);
            battleState.Player.Hand.Add("CheonraJimang");
            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 5, power: 0, gold: 0));
            var startingGold = battleState.Player.Resources.Gold;

            spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                "CheonraJimang",
                PlayerId.Player,
                new TileCoord(0, 0));

            battleState.SetPhase(PhaseType.TurnStart);
            turnStartService.ResolveTurnStart(battleState);

            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(0, 0)), Is.Null);
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(startingGold + 1));
            Assert.That(battleState.PersistentEffects[0].IsExpired, Is.True);
        }

        private static SpellService CreateSpellService(IEnumerable<CardDefinition> definitions)
        {
            return new SpellService(new InMemoryCardDefinitionProvider(definitions));
        }

        private static BattleState CreateBattleState()
        {
            var setupService = new BattleSetupService();
            var mulliganService = new MulliganService();
            var battleState = setupService.CreateInitialState(new BattleSetupRequest(CreateDeck("P"), CreateDeck("A"), PlayerId.Player));
            mulliganService.PassMulligan(battleState, PlayerId.Player);
            battleState.SetPhase(PhaseType.Main);
            return battleState;
        }

        private static IReadOnlyList<string> CreateDeck(string prefix)
        {
            return new List<string>
            {
                prefix + "-00",
                prefix + "-01",
                prefix + "-02",
                prefix + "-03",
                prefix + "-04",
                prefix + "-05",
                prefix + "-06",
                prefix + "-07",
                prefix + "-08",
                prefix + "-09",
            };
        }
    }
}
