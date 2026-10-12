using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Hand;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class CardStatDisplayTests
    {
        [TestCase(0, 10)]
        [TestCase(5, 15)]
        [TestCase(13, 23)]
        [TestCase(-1, 10)]
        [TestCase(14, 10)]
        public void DamageSpell_UsesSpellUpgradeRuleAndHidesHp(int level, int expected)
        {
            var spell = new DamageSpellCardDefinition("firebolt", "firebolt", new ResourceSet(), 10);
            Assert.That(CardStatDisplay.TryCreate(spell, level, out var stats), Is.True);
            Assert.That(stats.Attack, Is.EqualTo(expected));
            Assert.That(stats.HasHp, Is.False);
        }

        [TestCase("Firewall", "firewall", 33, DamageType.Magic, 41)]
        [TestCase("TimedBomb", "timed_bomb", 33, DamageType.Physical, 36)]
        [TestCase("BiochemicalBomb", "biochemical_bomb", 25, DamageType.Fixed, 28)]
        public void PersistentDamage_IsPerTriggerNotDurationTotal(string id, string effect,
            int damage, DamageType type, int expected)
        {
            var spell = new ScriptedSpellCardDefinition(id, id, new ResourceSet(), effect,
                damage, type, triggerCount: 4);
            Assert.That(CardStatDisplay.TryCreate(spell, 3, out var stats, spellPower: 5), Is.True);
            Assert.That(stats.Attack, Is.EqualTo(expected));
            Assert.That(stats.HasHp, Is.False);
        }

        [TestCase(DamageType.Magic, 18)]
        [TestCase(DamageType.Physical, 13)]
        [TestCase(DamageType.Fixed, 13)]
        public void OnlyMagicDamage_AddsSpellPower(DamageType type, int expected)
        {
            var spell = new DamageSpellCardDefinition("spell", "Spell", new ResourceSet(), 10, type);
            CardStatDisplay.TryCreate(spell, 3, out var stats, spellPower: 5);
            Assert.That(stats.Attack, Is.EqualTo(expected));
        }

        [Test]
        public void ResourceSpell_DoesNotDisplayZeroDamageEvenWithSpellPower()
        {
            var spell = new ScriptedSpellCardDefinition("ManaStone", "ManaStone", new ResourceSet(), "mana_stone");
            Assert.That(CardStatDisplay.TryCreate(spell, 5, out _, spellPower: 9), Is.False);
            Assert.That(CardStatDisplay.TryCreate(null, 0, out _), Is.False);
        }

        [Test]
        public void Nuclear_EveryLevelDisplaysImmediateDamageWithoutChangingFalloutOrAddingSpellPower()
        {
            var record = JsonCardDefinitionDatabase.FromJson(Resources.Load<TextAsset>("Project333/Data/cards").text)
                .Cards.Find(card => card.Id == NuclearRules.CardId);
            var definition = record.ToDefinition();
            for (var level = 0; level <= 13; level++)
            {
                Assert.That(CardStatDisplay.TryCreate(definition, level, out var stats, spellPower: 999), Is.True);
                Assert.That(stats.Attack, Is.EqualTo(100 + level), $"Nuclear Lv.{level}");
                Assert.That(stats.HasHp, Is.False);
                var text = CardStatDisplay.GetEffectText(definition, record.EffectText, level);
                Assert.That(text, Does.Contain("고정 데미지 " + (100 + level) + "을 입힌다."));
                Assert.That(text, Does.Contain("사용 당시 상대 전장에 있는 유닛(마스터 포함)에 영구 낙진 +1중첩.").And.Contain("중첩당 고정 피해 10."));
                if (level > 0) Assert.That(text, Does.Not.Contain("고정 데미지 100을"));
            }
            var resourceSpell = new ScriptedSpellCardDefinition("PowerBank", "Power Bank", new ResourceSet(), "power_bank");
            Assert.That(CardStatDisplay.GetEffectText(resourceSpell, "고정 데미지 100을 입힌다.", 13),
                Is.EqualTo("고정 데미지 100을 입힌다."), "Only Nuclear uses the immediate-hit description projection.");
        }

        [TestCase("전력 -2", "전력 소모:2")]
        [TestCase("전력-2", "전력 소모:2")]
        [TestCase("전력소모: 2", "전력 소모:2")]
        [TestCase("전력 소모: 2", "전력 소모:2")]
        [TestCase("전력 -20", "전력 소모:2")]
        [TestCase("비행\n전력 -2\n복제", "비행\n전력 소모:2\n복제")]
        [TestCase("4연타, 전력 -2", "4연타, 전력 소모:2")]
        public void PowerConsumer_RemoteLegacyLabelsUseTypedAmountWithoutChangingOtherEffects(string text, string expected)
        {
            var unit = new UnitCardDefinition("power-consumer", "Power consumer", new ResourceSet(),
                AttackType.Melee, 10, 20, true, true, 2);
            Assert.That(CardStatDisplay.GetEffectText(unit, text, 13), Is.EqualTo(expected));
            Assert.That(unit.SciencePowerUpkeep, Is.EqualTo(2));
        }

        [Test]
        public void BuildingPowerConsumer_RemoteLegacyLabelUsesTypedAmount()
        {
            var building = new BuildingCardDefinition("RobotFactory", "Robot factory", new ResourceSet(),
                false, 0, 10, sciencePowerUpkeep: 1);
            Assert.That(CardStatDisplay.GetEffectText(building, "전력 -1", 13), Is.EqualTo("전력 소모:1"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("내 턴 시작 시 전력 -2 비용을 지불한다.")]
        [TestCase("속공")]
        public void PowerConsumer_OrdinaryEffectSentencesAndEmptyTextAreUnchanged(string text)
        {
            var unit = new UnitCardDefinition("power-consumer", "Power consumer", new ResourceSet(),
                AttackType.Melee, 10, 20, true, true, 2);
            Assert.That(CardStatDisplay.GetEffectText(unit, text, 13), Is.EqualTo(text));
        }

        [Test]
        public void RobotFusion_RemoteLegacyReferenceUsesNewNameOnly()
        {
            var definition = new ScriptedSpellCardDefinition(RobotFusionRules.CardId, "로봇 합체",
                new ResourceSet(), RobotFusionRules.EffectId);
            Assert.That(CardStatDisplay.GetEffectText(definition, "전력 유지비가 가장 높은 로봇이 남는다.", 0),
                Is.EqualTo("전력 소모가 가장 높은 로봇이 남는다."));
        }

        [Test]
        public void SpellPowerCostAndOrdinaryUnitWithoutPowerConsumptionAreUnchanged()
        {
            const string text = "전력 -2";
            var spell = new ScriptedSpellCardDefinition("PowerBank", "PowerBank", new ResourceSet(), "power_bank");
            var unit = new UnitCardDefinition("unit", "Unit", new ResourceSet(),
                AttackType.Melee, 10, 20, true, false, 0);
            Assert.That(CardStatDisplay.GetEffectText(spell, text, 0), Is.EqualTo(text));
            Assert.That(CardStatDisplay.GetEffectText(unit, text, 0), Is.EqualTo(text));
        }
        [Test]
        public void UnitsAndBuildings_KeepExistingStatsAndHp()
        {
            var unit = new UnitCardDefinition("unit", "Unit", new ResourceSet(),
                AttackType.Melee, 20, 30, true, false, 0);
            CardStatDisplay.TryCreate(unit, 5, out var unitStats, spellPower: 9);
            Assert.That(unitStats.Attack, Is.EqualTo(21));
            Assert.That(unitStats.Hp, Is.EqualTo(34));
            Assert.That(unitStats.HasHp, Is.True);

            var building = new BuildingCardDefinition("PowerPlant", "PowerPlant", new ResourceSet(), false, 0, 30);
            CardStatDisplay.TryCreate(building, 5, out var buildingStats);
            Assert.That(buildingStats.Attack, Is.Zero);
            Assert.That(buildingStats.Hp, Is.EqualTo(35));
            Assert.That(buildingStats.HasHp, Is.True);
        }

        [Test]
        public void SpellCastEvent_PreservesCardDamageSeparatelyFromActualHpLoss()
        {
            var source = new BattleEventDto { CardSpellDamage = 18, Amount = 2 };
            var envelope = new OnlineBattleEnvelope();
            envelope.BattleEvents.Add(source);
            var json = OnlineBattleMessageSerializer.SerializeEnvelope(envelope);
            var restored = OnlineBattleMessageSerializer.DeserializeEnvelope(json).BattleEvents[0];
            Assert.That(restored.CardSpellDamage, Is.EqualTo(18));
            Assert.That(restored.Amount, Is.EqualTo(2));
            source.CardSpellDamage = null;
            var legacyJson = OnlineBattleMessageSerializer.SerializeEnvelope(envelope);
            Assert.That(legacyJson, Does.Not.Contain(nameof(BattleEventDto.CardSpellDamage)));
            Assert.That(OnlineBattleMessageSerializer.DeserializeEnvelope(legacyJson)
                .BattleEvents[0].CardSpellDamage, Is.Null);
        }

        [Test]
        public void HandCardView_RefreshesAndClearsSpellPowerOnReuse()
        {
            var root = new GameObject("HandSlot");
            try
            {
                var view = root.AddComponent<HandCardView>();
                view.Present("firebolt", "card", false, spellPower: 5);
                Assert.That(view.SpellPower, Is.EqualTo(5));
                view.Present("firebolt", "card", false, spellPower: 1);
                Assert.That(view.SpellPower, Is.EqualTo(1));
                view.Present("Goblin");
                Assert.That(view.SpellPower, Is.Zero);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MulliganSlot_ReusedAfterDamageSpell_RestoresHpVisibility()
        {
            var root = new GameObject("MulliganStats");
            root.SetActive(false);
            try
            {
                var presenter = root.AddComponent<BattleMulliganOverlayPresenter>();
                var attackObject = new GameObject("AttackValueText", typeof(RectTransform), typeof(Text));
                var hpObject = new GameObject("HpValueText", typeof(RectTransform), typeof(Text));
                attackObject.transform.SetParent(root.transform);
                hpObject.transform.SetParent(root.transform);
                var attack = attackObject.GetComponent<Text>();
                var hp = hpObject.GetComponent<Text>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var type = typeof(BattleMulliganOverlayPresenter);
                type.GetField("_attackValueTexts", flags).SetValue(presenter, new[] { attack });
                type.GetField("_hpValueTexts", flags).SetValue(presenter, new[] { hp });
                var show = type.GetMethod("SetCardStatVisible", flags);
                show.Invoke(presenter, new object[] { 0, true, false });
                Assert.That(attack.enabled, Is.True);
                Assert.That(hp.enabled, Is.False);
                show.Invoke(presenter, new object[] { 0, true, true });
                Assert.That(hp.enabled, Is.True);
                show.Invoke(presenter, new object[] { 0, false, false });
                Assert.That(attack.enabled, Is.False);
                Assert.That(hp.enabled, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
