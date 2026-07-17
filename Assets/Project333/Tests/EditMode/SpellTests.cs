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
    public sealed class SpellTests
    {
        [Test]
        public void CastDamageSpell_RemovesCardFromHandAddsItToDiscardAndSpendsCost()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new DamageSpellCardDefinition(
                    cardId: "spell-damage",
                    displayName: "Damage Spell",
                    cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 1),
                    damage: 4),
            });
            const string spellCardId = "spell-damage";
            var target = CreateUnit("target", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 2, 6);

            battleState.Player.Hand.Add(spellCardId);
            battleState.AIBoard.Place(new TileCoord(0, 0), target);

            spellService.CastDamageSpell(
                battleState,
                PlayerId.Player,
                spellCardId,
                PlayerId.AI,
                new TileCoord(0, 0));

            Assert.That(battleState.Player.Hand.Contains(spellCardId), Is.False);
            Assert.That(battleState.Player.Discard.CardIds, Does.Contain(spellCardId));
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(2));
            Assert.That(target.CurrentHp, Is.EqualTo(2));
        }

        [Test]
        public void CastDamageSpell_CanDamageFriendlyOccupant()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new DamageSpellCardDefinition(
                    cardId: "spell-damage",
                    displayName: "Damage Spell",
                    cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 1),
                    damage: 4),
            });
            var target = CreateUnit("friendly-target", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 2, 6);
            battleState.Player.Hand.Add("spell-damage");
            battleState.PlayerBoard.Place(target.Position, target);

            spellService.CastDamageSpell(
                battleState,
                PlayerId.Player,
                "spell-damage",
                PlayerId.Player,
                target.Position);

            Assert.That(target.CurrentHp, Is.EqualTo(2));
        }

        [Test]
        public void CastDamageSpell_CanDefeatOwnMasterAndAwardsOpponentVictory()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new DamageSpellCardDefinition(
                    cardId: "spell-damage",
                    displayName: "Damage Spell",
                    cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 1),
                    damage: 4),
            });
            battleState.Player.Hand.Add("spell-damage");
            battleState.Player.Master.CurrentHp = 4;

            spellService.CastDamageSpell(
                battleState,
                PlayerId.Player,
                "spell-damage",
                PlayerId.Player,
                battleState.Player.Master.Position);

            Assert.That(battleState.IsEnded, Is.True);
            Assert.That(battleState.Result.Winner, Is.EqualTo(PlayerId.AI));
        }

        [Test]
        public void CastDamageSpell_WhenTargetIsProtectedByGuard_DamagesGuardInsteadOfTarget()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new DamageSpellCardDefinition(
                    cardId: "spell-damage",
                    displayName: "Damage Spell",
                    cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 1),
                    damage: 4),
            });
            var guard = CreateUnit("guard", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 0, 6, hasGuard: true);
            var target = CreateUnit("target", PlayerId.AI, new TileCoord(0, 1), AttackType.Melee, 2, 5);

            battleState.Player.Hand.Add("spell-damage");
            battleState.AIBoard.Place(new TileCoord(0, 0), guard);
            battleState.AIBoard.Place(new TileCoord(0, 1), target);

            spellService.CastDamageSpell(
                battleState,
                PlayerId.Player,
                "spell-damage",
                PlayerId.AI,
                new TileCoord(0, 1));

            Assert.That(guard.CurrentHp, Is.EqualTo(2));
            Assert.That(target.CurrentHp, Is.EqualTo(5));
        }

        [Test]
        public void CastDamageSpell_WhenGuardHpIsInsufficient_SpillsRemainingDamageToProtectedTarget()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new DamageSpellCardDefinition(
                    cardId: "spell-damage",
                    displayName: "Damage Spell",
                    cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 1),
                    damage: 10),
            });
            var guard = CreateUnit("guard", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 0, 5, hasGuard: true);
            var target = CreateUnit("target", PlayerId.AI, new TileCoord(0, 1), AttackType.Melee, 2, 10);

            battleState.Player.Hand.Add("spell-damage");
            battleState.AIBoard.Place(new TileCoord(0, 0), guard);
            battleState.AIBoard.Place(new TileCoord(0, 1), target);

            spellService.CastDamageSpell(
                battleState,
                PlayerId.Player,
                "spell-damage",
                PlayerId.AI,
                new TileCoord(0, 1));

            Assert.That(battleState.AIBoard.GetOccupant(new TileCoord(0, 0)), Is.Null);
            Assert.That(target.CurrentHp, Is.EqualTo(5));
        }

        [Test]
        public void CastDamageSpell_RemovesDefeatedTargetImmediately()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new DamageSpellCardDefinition(
                    cardId: "spell-damage",
                    displayName: "Damage Spell",
                    cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 1),
                    damage: 4),
            });
            const string spellCardId = "spell-damage";
            var target = CreateUnit("target", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 2, 4);

            battleState.Player.Hand.Add(spellCardId);
            battleState.AIBoard.Place(new TileCoord(0, 0), target);

            spellService.CastDamageSpell(
                battleState,
                PlayerId.Player,
                spellCardId,
                PlayerId.AI,
                new TileCoord(0, 0));

            Assert.That(battleState.AIBoard.GetOccupant(new TileCoord(0, 0)), Is.Null);
        }

        [Test]
        public void CastDamageSpell_CanDefeatMasterAndEndBattle()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new DamageSpellCardDefinition(
                    cardId: "spell-damage",
                    displayName: "Damage Spell",
                    cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 1),
                    damage: 4),
            });
            const string spellCardId = "spell-damage";

            battleState.Player.Hand.Add(spellCardId);
            battleState.AI.Master.CurrentHp = 4;

            spellService.CastDamageSpell(
                battleState,
                PlayerId.Player,
                spellCardId,
                PlayerId.AI,
                new TileCoord(2, 1));

            Assert.That(battleState.IsEnded, Is.True);
            Assert.That(battleState.Result.Winner, Is.EqualTo(PlayerId.Player));
            Assert.That(battleState.Phase, Is.EqualTo(PhaseType.Ended));
        }

        [Test]
        public void CastDamageSpell_WhenProtectedMasterHasEnoughGuardHealth_DoesNotEndBattle()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new DamageSpellCardDefinition(
                    cardId: "spell-damage",
                    displayName: "Damage Spell",
                    cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 1),
                    damage: 4),
            });
            var guard = CreateUnit("guard", PlayerId.AI, new TileCoord(2, 0), AttackType.Melee, 0, 6, hasGuard: true);

            battleState.Player.Hand.Add("spell-damage");
            battleState.AIBoard.Place(new TileCoord(2, 0), guard);
            battleState.AI.Master.CurrentHp = 4;

            spellService.CastDamageSpell(
                battleState,
                PlayerId.Player,
                "spell-damage",
                PlayerId.AI,
                new TileCoord(2, 1));

            Assert.That(battleState.IsEnded, Is.False);
            Assert.That(guard.CurrentHp, Is.EqualTo(2));
            Assert.That(battleState.AI.Master.CurrentHp, Is.EqualTo(4));
        }

        [Test]
        public void CastDamageSpell_WhenOverflowKillsProtectedMaster_EndsBattle()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new DamageSpellCardDefinition(
                    cardId: "spell-damage",
                    displayName: "Damage Spell",
                    cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 1),
                    damage: 10),
            });
            var guard = CreateUnit("guard", PlayerId.AI, new TileCoord(2, 0), AttackType.Melee, 0, 5, hasGuard: true);

            battleState.Player.Hand.Add("spell-damage");
            battleState.AIBoard.Place(new TileCoord(2, 0), guard);
            battleState.AI.Master.CurrentHp = 4;

            spellService.CastDamageSpell(
                battleState,
                PlayerId.Player,
                "spell-damage",
                PlayerId.AI,
                new TileCoord(2, 1));

            Assert.That(battleState.IsEnded, Is.True);
            Assert.That(battleState.Result.Winner, Is.EqualTo(PlayerId.Player));
        }

        [Test]
        public void CastPersistentResourceSpell_AddsPersistentEffectDiscardsCardAndSpendsCost()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new PersistentResourceSpellCardDefinition(
                    cardId: "spell-persistent",
                    displayName: "Persistent Spell",
                    cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 2),
                    effectId: "effect-gold",
                    turnStartResourceGain: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 1),
                    endConditionText: "Until your next turn ends."),
            });
            const string spellCardId = "spell-persistent";

            battleState.Player.Hand.Add(spellCardId);

            spellService.CastPersistentResourceSpell(
                battleState,
                PlayerId.Player,
                spellCardId);

            Assert.That(battleState.Player.Hand.Contains(spellCardId), Is.False);
            Assert.That(battleState.Player.Discard.CardIds, Does.Contain(spellCardId));
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(1));
            Assert.That(battleState.PersistentEffects.Count, Is.EqualTo(1));
            Assert.That(battleState.PersistentEffects[0].SourceCardId, Is.EqualTo(spellCardId));
            Assert.That(battleState.PersistentEffects[0].OwnerId, Is.EqualTo(PlayerId.Player));
            Assert.That(battleState.PersistentEffects[0].TurnStartResourceGain.Gold, Is.EqualTo(1));
        }

        [Test]
        public void TurnStart_AppliesPersistentResourceEffectFromPersistentZone()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new PersistentResourceSpellCardDefinition(
                    cardId: "spell-persistent",
                    displayName: "Persistent Spell",
                    cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 2),
                    effectId: "effect-gold",
                    turnStartResourceGain: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 1),
                    endConditionText: "Until your next turn ends."),
            });
            var turnStartService = new TurnStartService();
            const string spellCardId = "spell-persistent";
            var startingGold = battleState.Player.Resources.Gold;

            battleState.Player.Hand.Add(spellCardId);

            spellService.CastPersistentResourceSpell(
                battleState,
                PlayerId.Player,
                spellCardId);

            var goldAfterCasting = battleState.Player.Resources.Gold;

            battleState.StartNextTurn(PlayerId.Player);
            turnStartService.ResolveTurnStart(battleState);

            Assert.That(goldAfterCasting, Is.EqualTo(startingGold - 2));
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(goldAfterCasting + 2));
        }

        [Test]
        public void CastDamageSpell_WhenPlayerCannotAffordDefinitionBasedSpell_ThrowsAndKeepsCardInHand()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new DamageSpellCardDefinition(
                    cardId: "expensive-spell",
                    displayName: "Expensive Spell",
                    cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 99),
                    damage: 4),
            });
            var target = CreateUnit("target", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 2, 6);

            battleState.Player.Hand.Add("expensive-spell");
            battleState.AIBoard.Place(new TileCoord(0, 0), target);

            var exception = Assert.Throws<System.InvalidOperationException>(
                () => spellService.CastDamageSpell(
                    battleState,
                    PlayerId.Player,
                    "expensive-spell",
                    PlayerId.AI,
                    new TileCoord(0, 0)));

            Assert.That(exception!.Message, Is.EqualTo("The player cannot afford this card."));
            Assert.That(battleState.Player.Hand.Contains("expensive-spell"), Is.True);
            Assert.That(battleState.Player.Discard.CardIds, Does.Not.Contain("expensive-spell"));
            Assert.That(target.CurrentHp, Is.EqualTo(6));
        }

        [Test]
        public void CastDamageSpell_WhenGoldCanSubstituteForMana_Succeeds()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new DamageSpellCardDefinition(
                    cardId: "gold-sub-spell",
                    displayName: "Gold Substitute Spell",
                    cost: new ResourceSet(mana: 1, qi: 0, power: 0, gold: 1),
                    damage: 4),
            });
            var target = CreateUnit("target", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 2, 6);

            battleState.Player.Hand.Add("gold-sub-spell");
            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 0, gold: 1));
            battleState.AIBoard.Place(new TileCoord(0, 0), target);

            spellService.CastDamageSpell(
                battleState,
                PlayerId.Player,
                "gold-sub-spell",
                PlayerId.AI,
                new TileCoord(0, 0));

            Assert.That(target.CurrentHp, Is.EqualTo(2));
            Assert.That(battleState.Player.Resources.Mana, Is.EqualTo(0));
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(2));
            Assert.That(battleState.Player.Hand.Contains("gold-sub-spell"), Is.False);
        }

        [Test]
        public void TurnStart_PersistentResourceEffectWithLimitedOwnerTurnStarts_ExpiresAfterConfiguredUses()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new PersistentResourceSpellCardDefinition(
                    cardId: "limited-persistent",
                    displayName: "Limited Persistent Spell",
                    cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 0),
                    effectId: "effect-power",
                    turnStartResourceGain: new ResourceSet(mana: 0, qi: 0, power: 1, gold: 0),
                    endConditionText: "For your next two turn starts.",
                    ownerTurnStartsRemaining: 2),
            });
            var turnStartService = new TurnStartService();

            battleState.Player.Hand.Add("limited-persistent");
            spellService.CastPersistentResourceSpell(battleState, PlayerId.Player, "limited-persistent");

            battleState.StartNextTurn(PlayerId.Player);
            turnStartService.ResolveTurnStart(battleState);

            Assert.That(battleState.Player.Resources.Power, Is.EqualTo(1));
            Assert.That(battleState.PersistentEffects[0].IsExpired, Is.False);

            battleState.StartNextTurn(PlayerId.Player);
            turnStartService.ResolveTurnStart(battleState);

            Assert.That(battleState.Player.Resources.Power, Is.EqualTo(2));
            Assert.That(battleState.PersistentEffects[0].IsExpired, Is.True);

            battleState.StartNextTurn(PlayerId.Player);
            turnStartService.ResolveTurnStart(battleState);

            Assert.That(battleState.Player.Resources.Power, Is.EqualTo(2));
        }

        private static SpellService CreateSpellService(IEnumerable<CardDefinition> definitions)
        {
            return new SpellService(new InMemoryCardDefinitionProvider(definitions));
        }

        private static UnitState CreateUnit(
            string id,
            PlayerId ownerId,
            TileCoord coord,
            AttackType attackType,
            int attack,
            int maxHp,
            bool hasGuard = false)
        {
            return new UnitState(
                runtimeId: id,
                cardId: id,
                ownerId: ownerId,
                position: coord,
                attackType: attackType,
                attack: attack,
                maxHp: maxHp,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hasGuard: hasGuard);
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
