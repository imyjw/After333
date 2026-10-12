using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class OrcBerserkerTests
    {
        private static JsonCardDefinitionDatabase Database() => JsonCardDefinitionDatabase.FromJson(File.ReadAllText(
            Path.Combine(Application.dataPath, "Project333/Resources/Project333/Data/cards.json")));

        [Test]
        public void JsonProvider_RegistersTheConfirmedReleasedUnitAndOnlyItsBerserkerTrait()
        {
            var database = Database();
            var record = database.Cards.Single(card => card.Id == OrcBerserkerRules.CardId);
            var definition = (UnitCardDefinition)database.CreateProvider().GetRequired(OrcBerserkerRules.CardId);
            Assert.That(CardDatabaseValidator.Validate(database).Errors, Is.Empty);
            Assert.That(record.DisplayName, Is.EqualTo("오크 광전사"));
            Assert.That(record.DefinitionType, Is.EqualTo(JsonCardDefinitionKind.Unit));
            Assert.That(record.Rarity, Is.EqualTo(CardRarity.Uncommon));
            Assert.That(record.Affiliation, Is.EqualTo(CardAffiliation.Fantasy));
            Assert.That(record.ChargeTileFootprint, Is.EqualTo(ChargeTileFootprint.OneByOne));
            Assert.That(record.IncludeInDraft && record.IncludeInRewards, Is.True);
            Assert.That(record.EffectText, Is.Empty);
            Assert.That(record.SpecialEffectText, Is.EqualTo("Berserker"));
            Assert.That(definition.Cost.Mana, Is.EqualTo(5));
            Assert.That(definition.Cost.Qi + definition.Cost.Power + definition.Cost.Gold, Is.Zero);
            Assert.That(definition.Attack, Is.EqualTo(20));
            Assert.That(definition.Health, Is.EqualTo(50));
            Assert.That(definition.AttackType, Is.EqualTo(AttackType.Melee));
            Assert.That(definition.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(definition.PhysicalDefense + definition.MagicDefense, Is.Zero);
            Assert.That(definition.Races, Is.EqualTo(new[] { UnitRace.Orc }));
            Assert.That(definition.CanMove && definition.HasBerserker, Is.True);
            Assert.That(definition.HasRush || definition.HasReplicate || definition.HasLifeSteal ||
                definition.HasEndure || definition.HasShielder || definition.HasHiding || definition.HasFlying || definition.HasPiercing, Is.False);
            Assert.That(definition.MaxAttacksPerTurn, Is.EqualTo(1));
            Assert.That(definition.HitsPerAttack, Is.EqualTo(1));
            Assert.That(CardUpgradeRules.IsCardUpgradeable(definition), Is.True);
        }

        [TestCase(0, 20, 50)]
        [TestCase(1, 20, 51)]
        [TestCase(2, 20, 52)]
        [TestCase(3, 21, 52)]
        [TestCase(4, 21, 53)]
        [TestCase(5, 21, 54)]
        [TestCase(6, 22, 54)]
        [TestCase(7, 22, 55)]
        [TestCase(8, 22, 56)]
        [TestCase(9, 23, 56)]
        [TestCase(10, 23, 57)]
        [TestCase(11, 23, 58)]
        [TestCase(12, 23, 59)]
        [TestCase(13, 24, 59)]
        public void SharedProviderAndSummon_ApplyEveryConfirmedLevelForEitherOwner(int level, int attack, int hp)
        {
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            {
                var unit = Summon(level, out var state, owner);
                var bonus = CardLevelStatRules.CalculateBonus(OrcBerserkerRules.CardId, level);
                Assert.That(bonus.AttackBonus, Is.EqualTo(attack - 20), "ATK increases only at 3, 6, 9, 13.");
                Assert.That(bonus.HpBonus, Is.EqualTo(hp - 50), "Lv.12 adds HP; Lv.13 adds ATK without adding HP.");
                Assert.That(unit.Attack, Is.EqualTo(attack), owner + " Lv." + level);
                Assert.That(unit.OriginalAttack, Is.EqualTo(attack));
                Assert.That(unit.MaxHp, Is.EqualTo(hp));
                Assert.That(unit.OriginalMaxHp, Is.EqualTo(hp));
                Assert.That(unit.CurrentHp, Is.EqualTo(hp));
                Assert.That(unit.HasBerserker, Is.True);
                Assert.That(unit.Races, Is.EqualTo(new[] { UnitRace.Orc }));
                Assert.That(unit.HasSummoningSickness, Is.True, "Berserker does not grant Rush.");
                Assert.That(unit.PhysicalDefense + unit.MagicDefense, Is.Zero);
                Assert.That(state.GetPlayer(owner).Resources.Mana, Is.EqualTo(2), "The confirmed cost remains 5 at every level.");
                Assert.That(state.GetPlayer(owner).Hand.Cards.Any(card => card.RuntimeId == "orc-selected"), Is.False);
                Assert.That(state.GetPlayer(owner).Hand.Cards.Any(card => card.RuntimeId == "orc-reserve"), Is.True,
                    "Shared summon consumes the selected copy, preserving another copy with the same CardId.");
            }
        }

        [Test]
        public void SharedBerserker_RecalculatesFromActualDamageAndHealing()
        {
            var unit = Summon(0, out _);
            Assert.That(DamageResolutionRules.ApplyEffectDamage(unit, 7, DamageType.Magic), Is.EqualTo(7));
            Assert.That(unit.CurrentHp, Is.EqualTo(43));
            Assert.That(unit.Attack, Is.EqualTo(27));
            unit.Heal(3);
            Assert.That(unit.CurrentHp, Is.EqualTo(46));
            Assert.That(unit.Attack, Is.EqualTo(24), "Berserker follows current missing HP, not cumulative damage received.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MeleeAttackAndCounterattack_UseTheSharedCapturedBerserkerAttack(bool initiatesAttack)
        {
            var unit = Summon(13, out var state);
            unit.HasSummoningSickness = false;
            DamageResolutionRules.ApplyEffectDamage(unit, 7, DamageType.Physical);
            Assert.That(unit.Attack, Is.EqualTo(31));
            var enemy = new UnitState("enemy-melee", "fixture", PlayerId.AI, new TileCoord(0, 0),
                AttackType.Melee, 5, 100, true, false, 0);
            enemy.HasSummoningSickness = false;
            state.AIBoard.Place(enemy.Position, enemy);
            state.SetActivePlayer(initiatesAttack ? PlayerId.Player : PlayerId.AI);
            new AttackService().Attack(state, state.ActivePlayerId, new TileCoord(0, 0), new TileCoord(0, 0));
            Assert.That(enemy.CurrentHp, Is.EqualTo(69), "Exactly one Physical hit uses ATK 31 captured before the exchange.");
            Assert.That(unit.CurrentHp, Is.EqualTo(47));
            Assert.That(unit.Attack, Is.EqualTo(36), "Damage in the simultaneous exchange only affects subsequent actions.");
            var hit = state.ValuePopupEvents.Single(popup => popup.SourceCardId == OrcBerserkerRules.CardId);
            Assert.That(hit.Amount, Is.EqualTo(31));
            Assert.That(hit.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(hit.Cause, Is.EqualTo(initiatesAttack ? BattleValueChangeCause.NormalAttack : BattleValueChangeCause.Counterattack));
        }

        [Test]
        public void Erasure_SuppressesBerserkerButPreservesOrcRaceAndAccountUpgradeStats()
        {
            var unit = Summon(13, out var state);
            unit.ApplyCombatStatBuff("temporary-test-buff", attackBonus: 5, maxHpBonus: 5);
            DamageResolutionRules.ApplyEffectDamage(unit, 7, DamageType.Physical);
            Assert.That(unit.Attack, Is.EqualTo(36));
            new ErasureService().Apply(state, unit);
            Assert.That(unit.IsErasure, Is.True);
            Assert.That(unit.EffectsSuppressed, Is.True);
            Assert.That(unit.BaseAttack, Is.EqualTo(24));
            Assert.That(unit.Attack, Is.EqualTo(24));
            Assert.That(unit.MaxHp, Is.EqualTo(59));
            Assert.That(unit.CurrentHp, Is.EqualTo(57));
            Assert.That(unit.CombatStatBuffs, Is.Empty);
            Assert.That(unit.Races, Is.EqualTo(new[] { UnitRace.Orc }));
            DamageResolutionRules.ApplyEffectDamage(unit, 3, DamageType.Magic);
            Assert.That(unit.Attack, Is.EqualTo(24), "Further damage cannot restore the suppressed trait.");
        }

        [TestCase("cost")]
        [TestCase("faction")]
        [TestCase("race")]
        [TestCase("trait")]
        [TestCase("effect")]
        [TestCase("stats")]
        [TestCase("extraTrait")]
        [TestCase("draft")]
        [TestCase("rewards")]
        public void Validator_RejectsDivergenceFromTheConfirmedCard(string change)
        {
            var database = Database();
            var record = database.Cards.Single(card => card.Id == OrcBerserkerRules.CardId);
            switch (change)
            {
                case "cost": record.Cost.Mana = 4; break;
                case "faction": record.Affiliation = CardAffiliation.Neutral; break;
                case "race": record.Races = new[] { UnitRace.Human }; break;
                case "trait": record.HasBerserker = false; break;
                case "effect": record.EffectText = "unconfirmed effect"; break;
                case "stats": record.Attack = 21; break;
                case "extraTrait": record.HasRush = true; break;
                case "draft": record.IncludeInDraft = false; break;
                case "rewards": record.IncludeInRewards = false; break;
            }
            Assert.That(CardDatabaseValidator.Validate(database).Errors, Has.Some.Contains("OrcBerserker must"));
        }

        private static UnitState Summon(int level, out BattleState state, PlayerId owner = PlayerId.Player)
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner));
            state.SetPhase(PhaseType.Main);
            state.GetPlayer(owner).Resources.Add(new ResourceSet(7, 0, 0, 0));
            state.GetPlayer(owner).Hand.Add(OrcBerserkerRules.CardId, "orc-selected", false);
            state.GetPlayer(owner).Hand.Add(OrcBerserkerRules.CardId, "orc-reserve", false);
            var levels = new InMemoryCardUpgradeLevelProvider();
            levels.SetUpgradeLevel(owner, OrcBerserkerRules.CardId, level);
            return new PlayCardService(Database().CreateProvider(), new SummonService(), levels).PlayUnitCard(
                state, owner, OrcBerserkerRules.CardId, new TileCoord(0, 0), "orc-selected");
        }
    }
}
