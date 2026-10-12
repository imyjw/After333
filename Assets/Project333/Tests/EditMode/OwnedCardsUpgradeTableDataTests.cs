using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.OwnedCards;
using UnityEditor;

namespace Project333.Tests.EditMode
{
    public sealed class OwnedCardsUpgradeTableDataTests
    {
        private const string DatabasePath = "Assets/Project333/Resources/Project333/Data/cards.json";
        private const string CatalogPath = "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";

        [Test]
        public void StandardUnitShowsEachLevelsIncrementInsteadOfCumulativeOrBaseValues()
        {
            var table = OwnedCardsUpgradeTableData.Build(Unit("Golem"));
            Assert.That(table.Title, Is.EqualTo("레벨별 강화 증가량"));
            Assert.That(table.IsUpgradeable, Is.True);
            Assert.That(table.EmptyMessage, Is.Empty);
            Assert.That(table.ColumnHeaders, Is.EqualTo(new[] { "레벨", "HP", "ATK", "효과" }));
            Assert.That(table.Rows.Select(row => row.Level), Is.EqualTo(Enumerable.Range(1, 13)));
            for (var level = 1; level <= 13; level++)
            {
                var attackLevel = level == 3 || level == 6 || level == 9 || level == 13;
                var row = table.Rows[level - 1];
                Assert.That(row.AttackIncrease, Is.EqualTo(attackLevel ? 1 : 0), "Lv." + level);
                Assert.That(row.HpIncrease, Is.EqualTo(attackLevel ? 0 : 1), "Lv." + level);
                Assert.That(row.AttackText, Is.EqualTo(attackLevel ? "+1" : "—"));
                Assert.That(row.HpText, Is.EqualTo(attackLevel ? "—" : "+1"));
                Assert.That(row.EffectText, Is.EqualTo("—"));
            }
            Assert.That(table.Rows.Sum(row => row.AttackIncrease), Is.EqualTo(4));
            Assert.That(table.Rows.Sum(row => row.HpIncrease), Is.EqualTo(9));
        }

        [TestCase("A-212")]
        [TestCase("Cerberus")]
        public void OnlyLevelThirteenAttackScheduleRetainsTwelveHpIncrements(string cardId)
        {
            var table = OwnedCardsUpgradeTableData.Build(Unit(cardId));
            for (var index = 0; index < 12; index++)
            {
                Assert.That(table.Rows[index].HpIncrease, Is.EqualTo(1));
                Assert.That(table.Rows[index].AttackIncrease, Is.Zero);
            }
            Assert.That(table.Rows[12].HpIncrease, Is.Zero);
            Assert.That(table.Rows[12].AttackIncrease, Is.EqualTo(1));
            Assert.That(table.Rows.Sum(row => row.HpIncrease), Is.EqualTo(12));
            Assert.That(table.Rows.Sum(row => row.AttackIncrease), Is.EqualTo(1));
        }

        [TestCase("Hero")]
        [TestCase("DemonKing")]
        public void TripleAttackLevelThirteenIsDisplayedAsPlusThree(string cardId)
        {
            var table = OwnedCardsUpgradeTableData.Build(Unit(cardId));
            Assert.That(table.Rows[12].AttackIncrease, Is.EqualTo(3));
            Assert.That(table.Rows[12].AttackText, Is.EqualTo("+3"));
            Assert.That(table.Rows[12].HpText, Is.EqualTo("—"));
            Assert.That(table.Rows.Sum(row => row.AttackIncrease), Is.EqualTo(6));
            Assert.That(table.Rows.Sum(row => row.HpIncrease), Is.EqualTo(9));
        }

        [TestCase("ManaPond")]
        [TestCase("RobotFactory")]
        [TestCase("GaebangBranch")]
        [TestCase("Inn")]
        [TestCase("MerchantCaravan")]
        [TestCase("PowerPlant")]
        [TestCase("NuclearPowerPlant")]
        public void HpOnlyBuildingsNeverShowAnAttackOrIncomeIncrease(string cardId)
        {
            var building = new BuildingCardDefinition(cardId, cardId,
                new ResourceSet(5, 0, 0, 0), false, 0, 20,
                new ResourceSet(1, 2, 3, 4), spellPower: 20);
            var table = OwnedCardsUpgradeTableData.Build(building);
            Assert.That(table.Rows.Count, Is.EqualTo(13));
            foreach (var row in table.Rows)
            {
                Assert.That(row.HpText, Is.EqualTo("+1"));
                Assert.That(row.AttackText, Is.EqualTo("—"));
                Assert.That(row.EffectText, Is.EqualTo("—"));
            }
            Assert.That(table.Rows.Sum(row => row.HpIncrease), Is.EqualTo(13));
            Assert.That(table.ColumnHeaders.Count, Is.EqualTo(4), "No cost, production or spell-power columns.");
            Assert.That(building.SpellPower, Is.EqualTo(20));
            Assert.That(building.TurnStartResourceGain.Gold, Is.EqualTo(4));
        }

        [TestCase("firebolt", "마법 피해")]
        [TestCase("Firewall", "마법 피해")]
        [TestCase("BiochemicalBomb", "고정 피해")]
        [TestCase("Nuclear", "고정 피해")]
        [TestCase("Earthquake", "물리 피해")]
        [TestCase("Meteor", "마법 피해")]
        [TestCase("FireSpear", "마법 피해")]
        [TestCase("TimedBomb", "물리 피해")]
        public void DamageSpellsShowBaseDamageIncreaseOnly(string cardId, string label)
        {
            var definition = Definitions().Single(card => card.CardId == cardId);
            var table = OwnedCardsUpgradeTableData.Build(definition);
            Assert.That(table.IsUpgradeable, Is.True);
            Assert.That(table.EffectLabel, Is.EqualTo(label));
            Assert.That(table.ColumnHeaders[3], Is.EqualTo(label));
            Assert.That(table.Rows.Count, Is.EqualTo(13));
            foreach (var row in table.Rows)
            {
                Assert.That(row.EffectIncrease, Is.EqualTo(1), "Damage per hit/tick, not trigger count or building multiplier.");
                Assert.That(row.EffectText, Is.EqualTo("+1"));
                Assert.That(row.HpText, Is.EqualTo("—"));
                Assert.That(row.AttackText, Is.EqualTo("—"));
            }
            Assert.That(table.Rows.Sum(row => row.EffectIncrease), Is.EqualTo(13));
        }

        [TestCase("PlasmaShield", "plasma_shield")]
        [TestCase("ManaShield", "mana_shield")]
        public void ShieldsDisplayNonUpgradeableInsteadOfDefenseGainRows(string cardId, string effectId)
        {
            var spell = new ScriptedSpellCardDefinition(cardId, cardId,
                new ResourceSet(1, 0, 0, 0), effectId, effectAmount: 10);
            var table = OwnedCardsUpgradeTableData.Build(spell);
            Assert.That(table.IsUpgradeable, Is.False);
            Assert.That(table.EmptyMessage, Is.EqualTo("강화 불가"));
            Assert.That(table.Rows, Is.Empty);
            Assert.That(spell.EffectAmount, Is.EqualTo(10));
            Assert.That(spell.Damage, Is.Zero);
        }

        [Test]
        public void EveryIneligibleSpellClearlyHasNoUpgradeRows()
        {
            var ineligible = Definitions().Where(card => !CardUpgradeRules.IsCardUpgradeable(card)).ToArray();
            Assert.That(ineligible.Length, Is.EqualTo(16));
            foreach (var definition in ineligible)
            {
                var table = OwnedCardsUpgradeTableData.Build(definition);
                Assert.That(table.IsUpgradeable, Is.False, definition.CardId);
                Assert.That(table.EmptyMessage, Is.EqualTo("강화 불가"), definition.CardId);
                Assert.That(table.Rows, Is.Empty, definition.CardId);
            }
        }

        [Test]
        public void UpgradeEligibilityDoesNotInventUnsupportedEffectOrDefinitionRules()
        {
            var unsupported = new CardDefinition[]
            {
                new ScriptedSpellCardDefinition("FireSpear", "Unknown scripted effect",
                    new ResourceSet(), "unimplemented_effect", 40, DamageType.Magic),
                new PersistentResourceSpellCardDefinition("Firewall", "Unknown resource growth",
                    new ResourceSet(), "resource_effect", new ResourceSet(5, 0, 0, 0), "never"),
                new UnsupportedDefinition()
            };
            foreach (var definition in unsupported)
            {
                var table = OwnedCardsUpgradeTableData.Build(definition);
                Assert.That(table.IsUpgradeable, Is.True, definition.CardId);
                Assert.That(table.EmptyMessage, Is.EqualTo("강화 정보 없음"));
                Assert.That(table.Rows, Is.Empty);
            }
            var missing = OwnedCardsUpgradeTableData.Build(null);
            Assert.That(missing.IsUpgradeable, Is.False);
            Assert.That(missing.Rows, Is.Empty);
            Assert.That(missing.EmptyMessage, Is.EqualTo("강화 정보를 확인할 수 없습니다."));
        }

        [Test]
        public void FullTableAndHeadersAreImmutableAndDoNotDependOnPreviouslyOpenedLevel()
        {
            var definition = Unit("Golem");
            var first = OwnedCardsUpgradeTableData.Build(definition);
            var second = OwnedCardsUpgradeTableData.Build(definition);
            Assert.That(second.Rows.Select(row => row.Level), Is.EqualTo(Enumerable.Range(1, 13)));
            Assert.That(second.Rows.Select(row => row.HpIncrease), Is.EqualTo(first.Rows.Select(row => row.HpIncrease)));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<OwnedCardUpgradeRow>)first.Rows).RemoveAt(0));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<string>)first.ColumnHeaders)[0] = "changed");
        }

        [Test]
        public void AllCanonicalDefinitionsMatchEveryActualLevelAndFinalTotal()
        {
            var definitions = Definitions();
            Assert.That(definitions.Count, Is.EqualTo(60));
            Assert.That(definitions.Count(CardUpgradeRules.IsCardUpgradeable), Is.EqualTo(44));
            foreach (var definition in definitions)
                AssertAgainstActualRules(definition);
        }

        [Test]
        public void AllNativeDefinitionsMatchTheActualRulesAndCanonicalIncrementTables()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Cards.Count, Is.EqualTo(54));
            var canonical = Definitions().ToDictionary(card => card.CardId, StringComparer.Ordinal);
            foreach (var asset in catalog.Cards)
            {
                Assert.That(asset, Is.Not.Null);
                var native = asset.ToDefinition();
                AssertAgainstActualRules(native);
                var nativeTable = OwnedCardsUpgradeTableData.Build(native);
                var canonicalTable = OwnedCardsUpgradeTableData.Build(canonical[asset.CardId]);
                Assert.That(nativeTable.IsUpgradeable, Is.EqualTo(canonicalTable.IsUpgradeable), asset.CardId);
                Assert.That(nativeTable.EffectLabel, Is.EqualTo(canonicalTable.EffectLabel), asset.CardId);
                Assert.That(nativeTable.Rows.Select(row => row.HpIncrease),
                    Is.EqualTo(canonicalTable.Rows.Select(row => row.HpIncrease)), asset.CardId);
                Assert.That(nativeTable.Rows.Select(row => row.AttackIncrease),
                    Is.EqualTo(canonicalTable.Rows.Select(row => row.AttackIncrease)), asset.CardId);
                Assert.That(nativeTable.Rows.Select(row => row.EffectIncrease),
                    Is.EqualTo(canonicalTable.Rows.Select(row => row.EffectIncrease)), asset.CardId);
            }
        }

        [Test]
        public void BuildingTheTableDoesNotMutatePrintedCostProductionTraitsOrBaseStats()
        {
            var definition = new UnitCardDefinition("OrcChieftain", "A valid supplied definition",
                new ResourceSet(5, 4, 3, 2), AttackType.Melee, 50, 70, true, false, 0,
                turnStartResourceGain: new ResourceSet(1, 2, 3, 4), physicalDefense: 7,
                magicDefense: 9, hasBerserker: true, spellPower: 6);
            OwnedCardsUpgradeTableData.Build(definition);
            OwnedCardsUpgradeTableData.Build(definition);
            Assert.That(new[] { definition.Cost.Mana, definition.Cost.Qi, definition.Cost.Power, definition.Cost.Gold },
                Is.EqualTo(new[] { 5, 4, 3, 2 }));
            Assert.That(new[] { definition.TurnStartResourceGain.Mana, definition.TurnStartResourceGain.Qi,
                definition.TurnStartResourceGain.Power, definition.TurnStartResourceGain.Gold },
                Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(new[] { definition.Attack, definition.Health, definition.PhysicalDefense,
                definition.MagicDefense, definition.SpellPower }, Is.EqualTo(new[] { 50, 70, 7, 9, 6 }));
            Assert.That(definition.HasBerserker, Is.True);
        }

        private static void AssertAgainstActualRules(CardDefinition definition)
        {
            var table = OwnedCardsUpgradeTableData.Build(definition);
            var eligible = CardUpgradeRules.IsCardUpgradeable(definition);
            Assert.That(table.IsUpgradeable, Is.EqualTo(eligible), definition.CardId);
            if (!eligible)
            {
                Assert.That(table.Rows, Is.Empty, definition.CardId);
                return;
            }

            Assert.That(table.Rows.Count, Is.EqualTo(13), definition.CardId);
            Assert.That(table.EmptyMessage, Is.Empty, definition.CardId);
            var previous = ActualValues(definition, 0);
            var baseValues = previous;
            for (var level = 1; level <= 13; level++)
            {
                var actual = ActualValues(definition, level);
                var row = table.Rows[level - 1];
                Assert.That(row.Level, Is.EqualTo(level), definition.CardId);
                Assert.That(new[] { row.HpIncrease, row.AttackIncrease, row.EffectIncrease },
                    Is.EqualTo(new[] { actual[0] - previous[0], actual[1] - previous[1], actual[2] - previous[2] }),
                    definition.CardId + " Lv." + level);
                previous = actual;
            }
            Assert.That(new[] { table.Rows.Sum(row => row.HpIncrease), table.Rows.Sum(row => row.AttackIncrease),
                table.Rows.Sum(row => row.EffectIncrease) },
                Is.EqualTo(new[] { previous[0] - baseValues[0], previous[1] - baseValues[1], previous[2] - baseValues[2] }),
                definition.CardId + " Lv.13 totals");
        }

        private static int[] ActualValues(CardDefinition definition, int level)
        {
            if (definition is UnitCardDefinition || definition is BuildingCardDefinition)
            {
                Assert.That(CardStatDisplay.TryCreate(definition, level, out var stats), Is.True);
                return new[] { stats.Hp, stats.Attack, 0 };
            }
            if (definition is ScriptedSpellCardDefinition daehwandan && daehwandan.EffectId == DaehwandanCardRules.EffectId)
                return new[] { 0, 0, DaehwandanCardRules.GetAttackBonus(daehwandan.EffectAmount, level) };
            if (definition is ScriptedSpellCardDefinition shield &&
                shield.EffectId == PlasmaShieldRules.EffectId)
                return new[] { 0, 0, PlasmaShieldRules.GetDefenseBonus(shield.EffectAmount, level) };
            if (definition is ScriptedSpellCardDefinition manaShield &&
                manaShield.EffectId == ManaShieldRules.EffectId)
                return new[] { 0, 0, ManaShieldRules.GetDefenseBonus(manaShield.EffectAmount, level) };
            Assert.That(CardStatDisplay.TryGetSpellDamage(definition, level, 0, out var damage), Is.True,
                definition.CardId);
            return new[] { 0, 0, damage };
        }

        private static IReadOnlyList<CardDefinition> Definitions()
        {
            return JsonCardDefinitionDatabase.FromJson(File.ReadAllText(DatabasePath)).ToDefinitions();
        }

        private static UnitCardDefinition Unit(string cardId)
        {
            return new UnitCardDefinition(cardId, cardId, new ResourceSet(0, 0, 0, 7),
                AttackType.Melee, 30, 50, true, false, 0);
        }

        private sealed class UnsupportedDefinition : CardDefinition
        {
            internal UnsupportedDefinition()
                : base("future-unit", "Future unit definition", CardType.Unit, new ResourceSet()) { }
        }
    }
}
