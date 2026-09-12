using System.IO;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class CardDatabaseValidatorTests
    {
        [Test]
        public void Validate_CurrentCardsJson_HasNoErrors()
        {
            var path = Path.Combine(Application.dataPath, "Project333/Resources/Project333/Data/cards.json");

            var result = CardDatabaseValidator.ValidateJson(File.ReadAllText(path));

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
        }

        [Test]
        public void CurrentCardsJson_ManaPondUsesNerfedHealth()
        {
            var path = Path.Combine(Application.dataPath, "Project333/Resources/Project333/Data/cards.json");
            var provider = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).CreateProvider();
            var manaPond = provider.GetRequired("ManaPond") as BuildingCardDefinition;

            Assert.That(manaPond, Is.Not.Null);
            Assert.That(manaPond.Health, Is.EqualTo(20));
        }

        [Test]
        public void CurrentCardsJson_ElfLongbowScoutIsUncommonWithPiercing()
        {
            var path = Path.Combine(Application.dataPath, "Project333/Resources/Project333/Data/cards.json");
            var database = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path));
            var record = database.Cards.Find(card => card.Id == "ElfLongbowScout");
            var definition = database.CreateProvider().GetRequired("ElfLongbowScout") as UnitCardDefinition;

            Assert.That(record, Is.Not.Null);
            Assert.That(record.Rarity, Is.EqualTo(CardRarity.Uncommon));
            Assert.That(record.HasPiercing, Is.True);
            Assert.That(record.SpecialEffectText, Does.Contain("관통"));
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.HasPiercing, Is.True);
        }

        [Test]
        public void CurrentCardsJson_UsesLockedDamageTypesAndStartingDefenses()
        {
            var path = Path.Combine(Application.dataPath, "Project333/Resources/Project333/Data/cards.json");
            var provider = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).CreateProvider();

            AssertUnit(provider, "A-111", DamageType.Physical);
            AssertUnit(provider, "A-301", DamageType.Physical);
            AssertUnit(provider, "Cerberus", DamageType.Physical);
            AssertUnit(provider, "ElfLongbowScout", DamageType.Physical);
            AssertUnit(provider, "Goblin", DamageType.Physical);
            AssertUnit(provider, "Golem", DamageType.Physical, expectedPhysicalDefense: 1);
            AssertUnit(provider, "Skeleton", DamageType.Physical);
            AssertUnit(provider, "Zombie", DamageType.Physical);
            AssertUnit(provider, "OrcWarrior", DamageType.Physical);
            AssertUnit(provider, WerewolfRules.CardId, DamageType.Physical);
            AssertUnit(provider, "RedDragon", DamageType.Magic);
            AssertUnit(provider, "Shaolin_1st_Disciple", DamageType.Physical);
            AssertUnit(provider, "Vampire", DamageType.Physical);
            Assert.That(
                ((UnitCardDefinition)provider.GetRequired("Vampire")).Attack,
                Is.EqualTo(15));
            AssertUnit(provider, "Gwangma", DamageType.Physical);
            AssertUnit(provider, "GoldMiner", DamageType.Physical);
            AssertUnit(provider, "Shieldbearer", DamageType.Physical);
            AssertUnit(provider, "ManaWeaver", DamageType.Magic);
            AssertUnit(provider, "BlueDragon", DamageType.Magic);
            AssertUnit(
                provider,
                DemonKingRules.CardId,
                DamageType.Magic,
                expectedPhysicalDefense: DemonKingRules.PhysicalDefense,
                expectedMagicDefense: DemonKingRules.MagicDefense);

            var a111 = provider.GetRequired("A-111") as UnitCardDefinition;
            Assert.That(a111, Is.Not.Null);
            Assert.That(a111.HasRobot, Is.True);
            Assert.That(a111.HasRush, Is.True);

            var a111Record = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == "A-111");
            Assert.That(a111Record, Is.Not.Null);
            Assert.That(a111Record.HasRobot, Is.True);
            Assert.That(a111Record.HasRush, Is.True);
            Assert.That(a111Record.SpecialEffectText, Does.Contain("로봇"));
            Assert.That(a111Record.SpecialEffectText, Does.Contain("속공"));

            var a301 = provider.GetRequired("A-301") as UnitCardDefinition;
            Assert.That(a301, Is.Not.Null);
            Assert.That(a301.Cost.Power, Is.EqualTo(2));
            Assert.That(a301.AttackType, Is.EqualTo(AttackType.Ranged));
            Assert.That(a301.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(a301.Attack, Is.EqualTo(33));
            Assert.That(a301.Health, Is.EqualTo(3));
            Assert.That(a301.CanMove, Is.True);
            Assert.That(a301.IsScience, Is.True);
            Assert.That(a301.SciencePowerUpkeep, Is.EqualTo(1));
            Assert.That(a301.HasFlying, Is.True);
            Assert.That(a301.HasRobot, Is.True);
            Assert.That(a301.HasRush, Is.False);
            Assert.That(a301.HasReplicate, Is.True);

            var a301Record = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == "A-301");
            Assert.That(a301Record, Is.Not.Null);
            Assert.That(a301Record.Rarity, Is.EqualTo(CardRarity.Common));
            Assert.That(a301Record.Affiliation, Is.EqualTo(CardAffiliation.ScienceCivilization));
            Assert.That(a301Record.IncludeInDraft, Is.True);
            Assert.That(a301Record.IncludeInRewards, Is.True);
            Assert.That(a301Record.HasRush, Is.False);
            Assert.That(a301Record.HasReplicate, Is.True);
            Assert.That(a301Record.SpecialEffectText, Does.Contain("비행"));
            Assert.That(a301Record.SpecialEffectText, Does.Contain("로봇"));
            Assert.That(a301Record.SpecialEffectText, Does.Contain("전력 -1"));
            Assert.That(a301Record.SpecialEffectText, Does.Contain("복제"));
            Assert.That(a301Record.SpecialEffectText, Does.Not.Contain("속공"));

            AssertSimpleFantasyUnit(provider, path, "Skeleton", 10, 10);
            AssertSimpleFantasyUnit(provider, path, "Zombie", 15, 1);
            AssertSimpleFantasyUnit(provider, path, "OrcWarrior", 25, 40, expectedManaCost: 3);

            var werewolf = provider.GetRequired(WerewolfRules.CardId) as UnitCardDefinition;
            Assert.That(werewolf, Is.Not.Null);
            Assert.That(werewolf.Cost.Mana, Is.EqualTo(WerewolfRules.ManaCost));
            Assert.That(werewolf.AttackType, Is.EqualTo(AttackType.Melee));
            Assert.That(werewolf.Attack, Is.EqualTo(WerewolfRules.BaseAttack));
            Assert.That(werewolf.Health, Is.EqualTo(WerewolfRules.BaseHealth));
            Assert.That(werewolf.CanMove, Is.True);
            Assert.That(werewolf.HasReplicate, Is.True);

            var werewolfRecord = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == WerewolfRules.CardId);
            Assert.That(werewolfRecord, Is.Not.Null);
            Assert.That(werewolfRecord.Rarity, Is.EqualTo(CardRarity.Rare));
            Assert.That(werewolfRecord.Affiliation, Is.EqualTo(CardAffiliation.Fantasy));
            Assert.That(werewolfRecord.IncludeInDraft, Is.True);
            Assert.That(werewolfRecord.IncludeInRewards, Is.True);
            Assert.That(werewolfRecord.PhysicalDefense, Is.Zero);
            Assert.That(werewolfRecord.MagicDefense, Is.Zero);
            Assert.That(werewolfRecord.SpecialEffectText, Does.Contain("복제"));

            var demonKing = provider.GetRequired(DemonKingRules.CardId) as UnitCardDefinition;
            Assert.That(demonKing, Is.Not.Null);
            Assert.That(demonKing.Cost.Mana, Is.EqualTo(DemonKingRules.ManaCost));
            Assert.That(demonKing.Cost.Gold, Is.EqualTo(DemonKingRules.GoldCost));
            Assert.That(demonKing.AttackType, Is.EqualTo(AttackType.Melee));
            Assert.That(demonKing.DamageType, Is.EqualTo(DamageType.Magic));
            Assert.That(demonKing.Attack, Is.EqualTo(DemonKingRules.BaseAttack));
            Assert.That(demonKing.Health, Is.EqualTo(DemonKingRules.BaseHealth));
            Assert.That(demonKing.CanMove, Is.True);

            var demonKingRecord = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == DemonKingRules.CardId);
            Assert.That(demonKingRecord, Is.Not.Null);
            Assert.That(demonKingRecord.Rarity, Is.EqualTo(CardRarity.Legendary));
            Assert.That(demonKingRecord.Affiliation, Is.EqualTo(CardAffiliation.Fantasy));
            Assert.That(demonKingRecord.IncludeInDraft, Is.False);
            Assert.That(demonKingRecord.IncludeInRewards, Is.False);
            Assert.That(demonKingRecord.EffectText, Does.Contain("사망"));
            Assert.That(demonKingRecord.EffectText, Does.Contain("3"));
            Assert.That(demonKingRecord.EffectText, Does.Contain("33"));

            var hero = provider.GetRequired(HeroRules.CardId) as UnitCardDefinition;
            Assert.That(hero, Is.Not.Null);
            Assert.That(hero.Cost.Mana, Is.EqualTo(HeroRules.ManaCost));
            Assert.That(hero.AttackType, Is.EqualTo(AttackType.Melee));
            Assert.That(hero.DamageType, Is.EqualTo(DamageType.Fixed));
            Assert.That(hero.Attack, Is.EqualTo(HeroRules.BaseAttack));
            Assert.That(hero.Health, Is.EqualTo(HeroRules.BaseHealth));
            Assert.That(hero.PhysicalDefense, Is.EqualTo(HeroRules.PhysicalDefense));
            Assert.That(hero.MagicDefense, Is.EqualTo(HeroRules.MagicDefense));
            Assert.That(hero.CanMove, Is.True);
            Assert.That(hero.InvincibleDuration, Is.EqualTo(InvincibleDurationType.GlobalTurnEnds));
            Assert.That(hero.InvincibleOwnerTurns, Is.EqualTo(HeroRules.InvincibleTurnEnds));

            var heroRecord = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == HeroRules.CardId);
            Assert.That(heroRecord, Is.Not.Null);
            Assert.That(heroRecord.Rarity, Is.EqualTo(CardRarity.Unique));
            Assert.That(heroRecord.Affiliation, Is.EqualTo(CardAffiliation.Fantasy));
            Assert.That(heroRecord.IncludeInDraft, Is.False);
            Assert.That(heroRecord.IncludeInRewards, Is.False);
            Assert.That(heroRecord.EffectText, Does.Contain("13"));

            var blueDragon = provider.GetRequired("BlueDragon") as UnitCardDefinition;
            Assert.That(blueDragon, Is.Not.Null);
            Assert.That(blueDragon.Attack, Is.EqualTo(40));
            Assert.That(blueDragon.Health, Is.EqualTo(50));
            Assert.That(blueDragon.HasFlying, Is.True);

            var redDragon = provider.GetRequired("RedDragon") as UnitCardDefinition;
            Assert.That(redDragon, Is.Not.Null);
            Assert.That(redDragon.Attack, Is.EqualTo(40));
            Assert.That(redDragon.Health, Is.EqualTo(40));
            Assert.That(redDragon.HasFlying, Is.True);

            var goldMiner = provider.GetRequired("GoldMiner") as UnitCardDefinition;
            Assert.That(goldMiner, Is.Not.Null);
            Assert.That(goldMiner.Cost.Gold, Is.EqualTo(2));

            var daehwandan = provider.GetRequired("Daehwandan") as ScriptedSpellCardDefinition;
            Assert.That(daehwandan, Is.Not.Null);
            Assert.That(daehwandan.Cost.Qi, Is.EqualTo(5));
            Assert.That(daehwandan.Cost.Gold, Is.Zero);

            var snowGinseng = provider.GetRequired(TenThousandYearSnowGinsengRules.CardId)
                as PersistentResourceSpellCardDefinition;
            Assert.That(snowGinseng, Is.Not.Null);
            Assert.That(snowGinseng.Cost.Gold, Is.EqualTo(TenThousandYearSnowGinsengRules.GoldCost));
            Assert.That(snowGinseng.EffectId, Is.EqualTo(TenThousandYearSnowGinsengRules.EffectId));
            Assert.That(
                snowGinseng.TurnStartResourceGain.Qi,
                Is.EqualTo(TenThousandYearSnowGinsengRules.TurnStartQiGain));
            Assert.That(
                snowGinseng.OwnerTurnStartsRemaining,
                Is.EqualTo(TenThousandYearSnowGinsengRules.OwnerTurnStarts));

            var snowGinsengRecord = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == TenThousandYearSnowGinsengRules.CardId);
            Assert.That(snowGinsengRecord, Is.Not.Null);
            Assert.That(snowGinsengRecord.Rarity, Is.EqualTo(CardRarity.Unique));
            Assert.That(snowGinsengRecord.Affiliation, Is.EqualTo(CardAffiliation.Murim));
            Assert.That(snowGinsengRecord.IncludeInDraft, Is.True);
            Assert.That(snowGinsengRecord.IncludeInRewards, Is.False);

            var gaebangBranch = provider.GetRequired("GaebangBranch") as BuildingCardDefinition;
            Assert.That(gaebangBranch, Is.Not.Null);
            Assert.That(gaebangBranch.DisplayName, Is.EqualTo("개방 분타"));
            Assert.That(gaebangBranch.Cost.Gold, Is.EqualTo(2));
            Assert.That(gaebangBranch.Attack, Is.Zero);
            Assert.That(gaebangBranch.Health, Is.EqualTo(20));
            Assert.That(gaebangBranch.CanAttack, Is.False);
            Assert.That(gaebangBranch.DamageType, Is.EqualTo(DamageType.None));

            var gaebangBranchRecord = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == "GaebangBranch");
            Assert.That(gaebangBranchRecord, Is.Not.Null);
            Assert.That(gaebangBranchRecord.Rarity, Is.EqualTo(CardRarity.Common));
            Assert.That(gaebangBranchRecord.Affiliation, Is.EqualTo(CardAffiliation.Murim));
            Assert.That(gaebangBranchRecord.IncludeInDraft, Is.True);
            Assert.That(gaebangBranchRecord.IncludeInRewards, Is.True);

            var merchantCaravan = provider.GetRequired(MerchantCaravanRules.CardId) as BuildingCardDefinition;
            Assert.That(merchantCaravan, Is.Not.Null);
            Assert.That(merchantCaravan.Cost.Gold, Is.EqualTo(MerchantCaravanRules.GoldCost));
            Assert.That(merchantCaravan.Attack, Is.Zero);
            Assert.That(merchantCaravan.Health, Is.EqualTo(MerchantCaravanRules.BaseHealth));
            Assert.That(merchantCaravan.CanAttack, Is.False);
            Assert.That(merchantCaravan.DamageType, Is.EqualTo(DamageType.None));
            Assert.That(merchantCaravan.PhysicalDefense, Is.Zero);
            Assert.That(merchantCaravan.MagicDefense, Is.Zero);
            Assert.That(
                merchantCaravan.TurnStartResourceGain.Gold,
                Is.EqualTo(MerchantCaravanRules.TurnStartGoldGain));

            var merchantCaravanRecord = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == MerchantCaravanRules.CardId);
            Assert.That(merchantCaravanRecord, Is.Not.Null);
            Assert.That(merchantCaravanRecord.Rarity, Is.EqualTo(CardRarity.Uncommon));
            Assert.That(merchantCaravanRecord.Affiliation, Is.EqualTo(CardAffiliation.Murim));
            Assert.That(merchantCaravanRecord.IncludeInDraft, Is.True);
            Assert.That(merchantCaravanRecord.IncludeInRewards, Is.True);

            var inn = provider.GetRequired(InnRules.CardId) as BuildingCardDefinition;
            Assert.That(inn, Is.Not.Null);
            Assert.That(inn.Cost.Qi, Is.EqualTo(InnRules.QiCost));
            Assert.That(inn.Attack, Is.Zero);
            Assert.That(inn.Health, Is.EqualTo(InnRules.BaseHealth));
            Assert.That(inn.CanAttack, Is.False);
            Assert.That(inn.DamageType, Is.EqualTo(DamageType.None));
            Assert.That(inn.PhysicalDefense, Is.Zero);
            Assert.That(inn.MagicDefense, Is.Zero);
            Assert.That(inn.TurnStartResourceGain.Gold, Is.EqualTo(InnRules.TurnStartGoldGain));

            var innRecord = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == InnRules.CardId);
            Assert.That(innRecord, Is.Not.Null);
            Assert.That(innRecord.Rarity, Is.EqualTo(CardRarity.Common));
            Assert.That(innRecord.Affiliation, Is.EqualTo(CardAffiliation.Murim));
            Assert.That(innRecord.IncludeInDraft, Is.False);
            Assert.That(innRecord.IncludeInRewards, Is.False);

            var powerPlant = provider.GetRequired(PowerPlantRules.CardId) as BuildingCardDefinition;
            Assert.That(powerPlant, Is.Not.Null);
            Assert.That(powerPlant.Cost.Power, Is.EqualTo(PowerPlantRules.PlayPowerCost));
            Assert.That(powerPlant.Cost.Gold, Is.Zero);
            Assert.That(powerPlant.Attack, Is.Zero);
            Assert.That(powerPlant.Health, Is.EqualTo(PowerPlantRules.BaseHealth));
            Assert.That(powerPlant.CanAttack, Is.False);
            Assert.That(powerPlant.DamageType, Is.EqualTo(DamageType.None));
            Assert.That(powerPlant.PhysicalDefense, Is.Zero);
            Assert.That(powerPlant.MagicDefense, Is.Zero);
            Assert.That(powerPlant.SciencePowerUpkeep, Is.Zero);

            var powerPlantRecord = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == PowerPlantRules.CardId);
            Assert.That(powerPlantRecord, Is.Not.Null);
            Assert.That(powerPlantRecord.Rarity, Is.EqualTo(CardRarity.Common));
            Assert.That(powerPlantRecord.Affiliation, Is.EqualTo(CardAffiliation.ScienceCivilization));
            Assert.That(powerPlantRecord.IncludeInDraft, Is.True);
            Assert.That(powerPlantRecord.IncludeInRewards, Is.True);

            var nuclearPowerPlant = provider.GetRequired(NuclearPowerPlantRules.CardId) as BuildingCardDefinition;
            Assert.That(nuclearPowerPlant, Is.Not.Null);
            Assert.That(nuclearPowerPlant.Cost.Power, Is.EqualTo(NuclearPowerPlantRules.PowerCost));
            Assert.That(nuclearPowerPlant.Attack, Is.Zero);
            Assert.That(nuclearPowerPlant.Health, Is.EqualTo(NuclearPowerPlantRules.BaseHealth));
            Assert.That(nuclearPowerPlant.CanAttack, Is.False);
            Assert.That(nuclearPowerPlant.DamageType, Is.EqualTo(DamageType.None));
            Assert.That(nuclearPowerPlant.PhysicalDefense, Is.Zero);
            Assert.That(nuclearPowerPlant.MagicDefense, Is.Zero);
            Assert.That(nuclearPowerPlant.SciencePowerUpkeep, Is.Zero);
            Assert.That(
                nuclearPowerPlant.TurnStartResourceGain.Power,
                Is.EqualTo(NuclearPowerPlantRules.TurnStartPowerGain));

            var nuclearPowerPlantRecord = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == NuclearPowerPlantRules.CardId);
            Assert.That(nuclearPowerPlantRecord, Is.Not.Null);
            Assert.That(nuclearPowerPlantRecord.Rarity, Is.EqualTo(CardRarity.Rare));
            Assert.That(
                nuclearPowerPlantRecord.Affiliation,
                Is.EqualTo(CardAffiliation.ScienceCivilization));
            Assert.That(nuclearPowerPlantRecord.IncludeInDraft, Is.True);
            Assert.That(nuclearPowerPlantRecord.IncludeInRewards, Is.True);

            var firebolt = provider.GetRequired("firebolt") as DamageSpellCardDefinition;
            Assert.That(firebolt, Is.Not.Null);
            Assert.That(firebolt.Damage, Is.EqualTo(10));
            Assert.That(firebolt.DamageType, Is.EqualTo(DamageType.Magic));

            var firewall = provider.GetRequired("Firewall") as ScriptedSpellCardDefinition;
            Assert.That(firewall, Is.Not.Null);
            Assert.That(firewall.Damage, Is.EqualTo(33));
            Assert.That(firewall.DamageType, Is.EqualTo(DamageType.Magic));
            Assert.That(firewall.TriggerCount, Is.EqualTo(2));

            var firewallRecord = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == "Firewall");
            Assert.That(firewallRecord, Is.Not.Null);
            Assert.That(firewallRecord.IncludeInDraft, Is.False);
            Assert.That(firewallRecord.IncludeInRewards, Is.False);

            var timedBomb = provider.GetRequired(TimedBombRules.CardId) as ScriptedSpellCardDefinition;
            Assert.That(timedBomb, Is.Not.Null);
            Assert.That(timedBomb.Cost.Power, Is.EqualTo(TimedBombRules.PowerCost));
            Assert.That(timedBomb.Cost.Gold, Is.EqualTo(TimedBombRules.GoldCost));
            Assert.That(timedBomb.EffectId, Is.EqualTo(TimedBombRules.EffectId));
            Assert.That(timedBomb.Damage, Is.EqualTo(TimedBombRules.BaseDamage));
            Assert.That(timedBomb.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(timedBomb.TriggerCount, Is.EqualTo(TimedBombRules.TurnStartsUntilDetonation));

            var timedBombRecord = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == TimedBombRules.CardId);
            Assert.That(timedBombRecord, Is.Not.Null);
            Assert.That(timedBombRecord.Rarity, Is.EqualTo(CardRarity.Common));
            Assert.That(timedBombRecord.Affiliation, Is.EqualTo(CardAffiliation.ScienceCivilization));
            Assert.That(timedBombRecord.IncludeInDraft, Is.True);
            Assert.That(timedBombRecord.IncludeInRewards, Is.True);

            var biochemicalBomb = provider.GetRequired(BiochemicalBombRules.CardId) as ScriptedSpellCardDefinition;
            Assert.That(biochemicalBomb, Is.Not.Null);
            Assert.That(biochemicalBomb.Cost.Power, Is.EqualTo(BiochemicalBombRules.PowerCost));
            Assert.That(biochemicalBomb.Cost.Gold, Is.EqualTo(BiochemicalBombRules.GoldCost));
            Assert.That(biochemicalBomb.EffectId, Is.EqualTo(BiochemicalBombRules.EffectId));
            Assert.That(biochemicalBomb.Damage, Is.EqualTo(BiochemicalBombRules.BaseDamage));
            Assert.That(biochemicalBomb.DamageType, Is.EqualTo(DamageType.Fixed));
            Assert.That(biochemicalBomb.TriggerCount, Is.EqualTo(BiochemicalBombRules.TriggerCount));

            var biochemicalBombRecord = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == BiochemicalBombRules.CardId);
            Assert.That(biochemicalBombRecord, Is.Not.Null);
            Assert.That(biochemicalBombRecord.Rarity, Is.EqualTo(CardRarity.Uncommon));
            Assert.That(biochemicalBombRecord.Affiliation, Is.EqualTo(CardAffiliation.ScienceCivilization));
            Assert.That(biochemicalBombRecord.IncludeInDraft, Is.True);
            Assert.That(biochemicalBombRecord.IncludeInRewards, Is.True);

            var powerBank = provider.GetRequired(PowerBankRules.CardId) as ScriptedSpellCardDefinition;
            Assert.That(powerBank, Is.Not.Null);
            Assert.That(powerBank.Cost.Gold, Is.EqualTo(PowerBankRules.GoldCost));
            Assert.That(powerBank.EffectId, Is.EqualTo(PowerBankRules.EffectId));
            Assert.That(powerBank.Damage, Is.Zero);
            Assert.That(powerBank.DamageType, Is.EqualTo(DamageType.None));

            var powerBankRecord = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == PowerBankRules.CardId);
            Assert.That(powerBankRecord, Is.Not.Null);
            Assert.That(powerBankRecord.Rarity, Is.EqualTo(CardRarity.Common));
            Assert.That(powerBankRecord.Affiliation, Is.EqualTo(CardAffiliation.ScienceCivilization));
            Assert.That(powerBankRecord.IncludeInDraft, Is.True);
            Assert.That(powerBankRecord.IncludeInRewards, Is.False);

            var manaStone = provider.GetRequired(ManaStoneRules.CardId) as ScriptedSpellCardDefinition;
            Assert.That(manaStone, Is.Not.Null);
            Assert.That(ManaStoneRules.GoldCost, Is.EqualTo(2));
            Assert.That(ManaStoneRules.ManaGain, Is.EqualTo(3));
            Assert.That(manaStone.Cost.Gold, Is.EqualTo(ManaStoneRules.GoldCost));
            Assert.That(manaStone.EffectId, Is.EqualTo(ManaStoneRules.EffectId));
            Assert.That(manaStone.Damage, Is.Zero);
            Assert.That(manaStone.DamageType, Is.EqualTo(DamageType.None));

            var manaStoneRecord = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == ManaStoneRules.CardId);
            Assert.That(manaStoneRecord, Is.Not.Null);
            Assert.That(manaStoneRecord.Rarity, Is.EqualTo(CardRarity.Common));
            Assert.That(manaStoneRecord.Affiliation, Is.EqualTo(CardAffiliation.Fantasy));
            Assert.That(manaStoneRecord.IncludeInDraft, Is.True);
            Assert.That(manaStoneRecord.IncludeInRewards, Is.False);

            var manaStoneBundle = provider.GetRequired(ManaStoneBundleRules.CardId) as ScriptedSpellCardDefinition;
            Assert.That(manaStoneBundle, Is.Not.Null);
            Assert.That(ManaStoneBundleRules.GoldCost, Is.EqualTo(5));
            Assert.That(manaStoneBundle.Cost.Gold, Is.EqualTo(ManaStoneBundleRules.GoldCost));
            Assert.That(manaStoneBundle.EffectId, Is.EqualTo(ManaStoneBundleRules.EffectId));
            Assert.That(manaStoneBundle.Damage, Is.Zero);
            Assert.That(manaStoneBundle.DamageType, Is.EqualTo(DamageType.None));

            var manaStoneBundleRecord = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == ManaStoneBundleRules.CardId);
            Assert.That(manaStoneBundleRecord, Is.Not.Null);
            Assert.That(manaStoneBundleRecord.Rarity, Is.EqualTo(CardRarity.Uncommon));
            Assert.That(manaStoneBundleRecord.Affiliation, Is.EqualTo(CardAffiliation.Fantasy));
            Assert.That(manaStoneBundleRecord.IncludeInDraft, Is.True);
            Assert.That(manaStoneBundleRecord.IncludeInRewards, Is.False);

            var robotFusion = provider.GetRequired("RobotFusion") as ScriptedSpellCardDefinition;
            Assert.That(robotFusion, Is.Not.Null);
            Assert.That(robotFusion.EffectId, Is.EqualTo(RobotFusionRules.EffectId));
            Assert.That(robotFusion.Cost.Power, Is.EqualTo(3));

            var robotFusionRecord = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == "RobotFusion");
            Assert.That(robotFusionRecord, Is.Not.Null);
            Assert.That(robotFusionRecord.IncludeInDraft, Is.False);
            Assert.That(robotFusionRecord.IncludeInRewards, Is.False);

            var gu = provider.GetRequired(GuRules.CardId) as ScriptedSpellCardDefinition;
            Assert.That(gu, Is.Not.Null);
            Assert.That(gu.EffectId, Is.EqualTo(GuRules.EffectId));
            Assert.That(gu.Cost.Qi, Is.EqualTo(GuRules.QiCost));
            Assert.That(gu.Damage, Is.Zero);
            Assert.That(gu.DamageType, Is.EqualTo(DamageType.None));

            var guRecord = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == GuRules.CardId);
            Assert.That(guRecord, Is.Not.Null);
            Assert.That(guRecord.Rarity, Is.EqualTo(CardRarity.Unique));
            Assert.That(guRecord.Affiliation, Is.EqualTo(CardAffiliation.Murim));
            Assert.That(guRecord.IncludeInDraft, Is.True);
            Assert.That(guRecord.IncludeInRewards, Is.False);

            var huanShu = provider.GetRequired(HuanShuRules.CardId) as ScriptedSpellCardDefinition;
            Assert.That(huanShu, Is.Not.Null);
            Assert.That(huanShu.EffectId, Is.EqualTo(HuanShuRules.EffectId));
            Assert.That(huanShu.Cost.Qi, Is.EqualTo(HuanShuRules.QiCost));
            Assert.That(huanShu.Damage, Is.Zero);
            Assert.That(huanShu.DamageType, Is.EqualTo(DamageType.None));

            var huanShuRecord = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == HuanShuRules.CardId);
            Assert.That(huanShuRecord, Is.Not.Null);
            Assert.That(huanShuRecord.Rarity, Is.EqualTo(CardRarity.Uncommon));
            Assert.That(huanShuRecord.Affiliation, Is.EqualTo(CardAffiliation.Murim));
            Assert.That(huanShuRecord.IncludeInDraft, Is.True);
            Assert.That(huanShuRecord.IncludeInRewards, Is.False);
            Assert.That(huanShuRecord.EffectText, Does.Contain("영구"));
        }

        [Test]
        public void ValidateJson_RemovedAttributeField_Fails()
        {
            const string json =
                "{\"schemaVersion\":1,\"cards\":[{\"id\":\"Goblin\",\"displayName\":\"Goblin\",\"definitionType\":\"Unit\",\"rarity\":\"Common\",\"attribute\":\"Neutral\",\"affiliation\":\"Fantasy\",\"chargeTileFootprint\":\"OneByOne\",\"health\":1,\"attack\":0}]}";

            var result = CardDatabaseValidator.ValidateJson(json);

            Assert.That(result.Errors, Has.Some.Contains("contains removed field 'attribute'"));
        }

        [Test]
        public void Validate_DuplicateCardId_Fails()
        {
            var database = CreateDatabase(
                CreateUnitRecord("duplicate"),
                CreateUnitRecord("duplicate"));

            var result = CardDatabaseValidator.Validate(database);

            Assert.That(result.Errors, Has.Some.Contains("Duplicate card id 'duplicate'."));
        }

        [Test]
        public void Validate_LifeStealTextWithoutFlag_Fails()
        {
            var card = CreateUnitRecord("vampire-like");
            card.SpecialEffectText = "LifeSteal";
            card.HasLifeSteal = false;
            var database = CreateDatabase(card);

            var result = CardDatabaseValidator.Validate(database);

            Assert.That(result.Errors, Has.Some.Contains("hasLifeSteal is false"));
        }

        [Test]
        public void Validate_KoreanLifeStealTextWithFlag_Passes()
        {
            var card = CreateUnitRecord("vampire-like");
            card.SpecialEffectText = "흡혈: 공격 시 입힌 피해만큼 체력 회복";
            card.HasLifeSteal = true;

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(card));

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
        }

        [Test]
        public void Validate_KoreanShielderTextWithFlag_Passes()
        {
            var card = CreateUnitRecord("shielder-unit");
            card.SpecialEffectText = "쉴더";
            card.HasShielder = true;

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(card));

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
        }

        [Test]
        public void Validate_ShielderTextWithoutFlag_Fails()
        {
            var card = CreateUnitRecord("shielder-text-without-flag");
            card.SpecialEffectText = "Shielder";

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("hasShielder is false"));
        }

        [Test]
        public void Validate_LegacyGuardText_FailsWithRenameGuidance()
        {
            var card = CreateUnitRecord("legacy-guard-unit");
            card.SpecialEffectText = "Guard";
            card.HasShielder = true;

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("Use Shielder/쉴더 instead"));
        }

        [TestCase("hasShielder")]
        [TestCase("hasGuard")]
        public void FromJson_CurrentAndLegacyShielderFlags_MapToHasShielder(string propertyName)
        {
            var json =
                "{\"schemaVersion\":1,\"cards\":[{" +
                "\"id\":\"shielder-json\",\"displayName\":\"Shielder\"," +
                "\"definitionType\":\"Unit\",\"specialEffectText\":\"쉴더\"," +
                "\"" + propertyName + "\":true}]}";

            var database = JsonCardDefinitionDatabase.FromJson(json);
            var record = database.Cards[0];
            var definition = (UnitCardDefinition)record.ToDefinition();

            Assert.That(record.HasShielder, Is.True);
            Assert.That(definition.HasShielder, Is.True);
        }

        [Test]
        public void Validate_UnsupportedScriptedEffect_Fails()
        {
            var database = CreateDatabase(new JsonCardDefinitionRecord
            {
                Id = "unknown-spell",
                DisplayName = "Unknown Spell",
                DefinitionType = JsonCardDefinitionKind.ScriptedSpell,
                ChargeTileFootprint = ChargeTileFootprint.None,
                EffectId = "not_implemented_yet",
            });

            var result = CardDatabaseValidator.Validate(database);

            Assert.That(result.Errors, Has.Some.Contains("unsupported effectId 'not_implemented_yet'"));
        }

        [Test]
        public void Validate_GuWithWrongCost_FailsWithoutRejectingReleasedAvailability()
        {
            var gu = new JsonCardDefinitionRecord
            {
                Id = GuRules.CardId,
                DisplayName = "고독",
                DefinitionType = JsonCardDefinitionKind.ScriptedSpell,
                Rarity = CardRarity.Unique,
                IncludeInDraft = true,
                IncludeInRewards = true,
                Affiliation = CardAffiliation.Murim,
                ChargeTileFootprint = ChargeTileFootprint.None,
                Cost = new JsonResourceSet { Qi = 9 },
                EffectText = "상대방 유닛 하나를 복종시킨다.",
                EffectId = GuRules.EffectId,
                DamageType = DamageType.None,
            };

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(gu));

            Assert.That(result.Errors, Has.Some.Contains("cost exactly 10 qi"));
            Assert.That(result.Errors, Has.None.Contains("disabled in draft and rewards"));
        }

        [Test]
        public void Validate_HuanShuWithWrongRules_Fails()
        {
            var huanShu = new JsonCardDefinitionRecord
            {
                Id = HuanShuRules.CardId,
                DisplayName = "환술",
                DefinitionType = JsonCardDefinitionKind.ScriptedSpell,
                Rarity = CardRarity.Rare,
                IncludeInDraft = true,
                IncludeInRewards = true,
                Affiliation = CardAffiliation.Fantasy,
                ChargeTileFootprint = ChargeTileFootprint.None,
                Cost = new JsonResourceSet { Qi = 2 },
                EffectText = "대상에게 환술을 부여한다.",
                EffectId = HuanShuRules.EffectId,
                DamageType = DamageType.None,
            };

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(huanShu));

            Assert.That(result.Errors, Has.Some.Contains("Uncommon Murim ScriptedSpell"));
            Assert.That(result.Errors, Has.Some.Contains("cost exactly 3 qi"));
            Assert.That(result.Errors, Has.None.Contains("disabled in draft and rewards"));
            Assert.That(result.Errors, Has.Some.Contains("permanent random attack targeting effect"));
        }

        [Test]
        public void Validate_TenThousandYearSnowGinsengWithWrongRules_Fails()
        {
            var snowGinseng = new JsonCardDefinitionRecord
            {
                Id = TenThousandYearSnowGinsengRules.CardId,
                DisplayName = "영약: 만년설삼",
                DefinitionType = JsonCardDefinitionKind.PersistentResourceSpell,
                Rarity = CardRarity.Unique,
                IncludeInDraft = true,
                IncludeInRewards = true,
                Affiliation = CardAffiliation.Murim,
                ChargeTileFootprint = ChargeTileFootprint.None,
                Cost = new JsonResourceSet { Gold = 3 },
                EffectText = "다음 턴 시작 시 기 +2",
                EffectId = "wrong_snow_ginseng_effect",
                DamageType = DamageType.None,
                TurnStartResourceGain = new JsonResourceSet { Qi = 2 },
                OwnerTurnStartsRemaining = 2,
                EndConditionText = "다음 2번의 내 턴 시작 후 종료됩니다.",
            };

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(snowGinseng));

            Assert.That(result.Errors, Has.Some.Contains("cost exactly 4 gold"));
            Assert.That(result.Errors, Has.Some.Contains("next 3 owner turn starts"));
            Assert.That(result.Errors, Has.None.Contains("disabled in draft"));
            Assert.That(result.Errors, Has.Some.Contains("must not appear in random rewards"));
        }

        [Test]
        public void Validate_FirewallWithoutTriggerCount_Fails()
        {
            var firewall = new JsonCardDefinitionRecord
            {
                Id = "Firewall",
                DisplayName = "파이어월",
                DefinitionType = JsonCardDefinitionKind.ScriptedSpell,
                ChargeTileFootprint = ChargeTileFootprint.None,
                EffectId = "firewall",
                Damage = 33,
                DamageType = DamageType.Magic,
                TriggerCount = 0,
                IncludeInDraft = false,
            };

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(firewall));

            Assert.That(result.Errors, Has.Some.Contains("triggerCount greater than 0"));
        }

        [Test]
        public void Validate_TimedBombWithWrongCountdown_Fails()
        {
            var timedBomb = new JsonCardDefinitionRecord
            {
                Id = TimedBombRules.CardId,
                DisplayName = "시한폭탄",
                DefinitionType = JsonCardDefinitionKind.ScriptedSpell,
                Rarity = CardRarity.Common,
                IncludeInDraft = false,
                IncludeInRewards = false,
                Affiliation = CardAffiliation.ScienceCivilization,
                ChargeTileFootprint = ChargeTileFootprint.None,
                Cost = new JsonResourceSet
                {
                    Power = TimedBombRules.PowerCost,
                    Gold = TimedBombRules.GoldCost,
                },
                EffectId = TimedBombRules.EffectId,
                Damage = TimedBombRules.BaseDamage,
                DamageType = DamageType.Physical,
                TriggerCount = 2,
            };

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(timedBomb));

            Assert.That(result.Errors, Has.Some.Contains("triggerCount 3"));
        }

        [Test]
        public void Validate_BiochemicalBombWithWrongDamageAndDuration_Fails()
        {
            var biochemicalBomb = new JsonCardDefinitionRecord
            {
                Id = BiochemicalBombRules.CardId,
                DisplayName = "생화학폭탄",
                DefinitionType = JsonCardDefinitionKind.ScriptedSpell,
                Rarity = CardRarity.Uncommon,
                IncludeInDraft = false,
                IncludeInRewards = false,
                Affiliation = CardAffiliation.ScienceCivilization,
                ChargeTileFootprint = ChargeTileFootprint.None,
                Cost = new JsonResourceSet
                {
                    Power = BiochemicalBombRules.PowerCost,
                    Gold = BiochemicalBombRules.GoldCost,
                },
                EffectId = BiochemicalBombRules.EffectId,
                Damage = 40,
                DamageType = DamageType.Physical,
                TriggerCount = 3,
            };

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(biochemicalBomb));

            Assert.That(result.Errors, Has.Some.Contains("25 fixed damage for 4 global turn starts"));
            Assert.That(result.Errors, Has.Some.Contains("Fixed damageType"));
            Assert.That(result.Errors, Has.Some.Contains("triggerCount 4"));
        }

        [Test]
        public void Validate_PowerBankWithWrongPowerText_Fails()
        {
            var powerBank = new JsonCardDefinitionRecord
            {
                Id = PowerBankRules.CardId,
                DisplayName = "보조배터리",
                DefinitionType = JsonCardDefinitionKind.ScriptedSpell,
                Rarity = CardRarity.Common,
                IncludeInDraft = false,
                IncludeInRewards = false,
                Affiliation = CardAffiliation.ScienceCivilization,
                ChargeTileFootprint = ChargeTileFootprint.None,
                Cost = new JsonResourceSet { Gold = PowerBankRules.GoldCost },
                EffectText = "사용 시 전력 획득",
                EffectId = PowerBankRules.EffectId,
                Damage = 0,
                DamageType = DamageType.None,
            };

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(powerBank));

            Assert.That(result.Errors, Has.Some.Contains("immediate power +6"));
        }

        [Test]
        public void Validate_PowerPlantThatCanAttack_Fails()
        {
            var powerPlant = new JsonCardDefinitionRecord
            {
                Id = PowerPlantRules.CardId,
                DisplayName = "발전소",
                DefinitionType = JsonCardDefinitionKind.Building,
                Rarity = CardRarity.Common,
                IncludeInDraft = false,
                IncludeInRewards = false,
                Affiliation = CardAffiliation.ScienceCivilization,
                ChargeTileFootprint = ChargeTileFootprint.OneByOne,
                Cost = new JsonResourceSet { Power = PowerPlantRules.PlayPowerCost },
                EffectText = "내 턴 시작 시 골드 1을 지불하고 전력 2를 얻는다.",
                Attack = 0,
                Health = PowerPlantRules.BaseHealth,
                PhysicalDefense = 0,
                MagicDefense = 0,
                CanAttack = true,
                DamageType = DamageType.None,
                SciencePowerUpkeep = 0,
            };

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(powerPlant));

            Assert.That(result.Errors, Has.Some.Contains("PowerPlant must use ATK 0"));
        }

        [Test]
        public void Validate_NuclearPowerPlantWithWrongDestructionText_Fails()
        {
            var nuclearPowerPlant = new JsonCardDefinitionRecord
            {
                Id = NuclearPowerPlantRules.CardId,
                DisplayName = "원자력 발전소",
                DefinitionType = JsonCardDefinitionKind.Building,
                Rarity = CardRarity.Rare,
                IncludeInDraft = false,
                IncludeInRewards = false,
                Affiliation = CardAffiliation.ScienceCivilization,
                ChargeTileFootprint = ChargeTileFootprint.OneByOne,
                Cost = new JsonResourceSet { Power = NuclearPowerPlantRules.PowerCost },
                EffectText = "내 턴 시작 시 전력 +3",
                Attack = 0,
                Health = NuclearPowerPlantRules.BaseHealth,
                PhysicalDefense = 0,
                MagicDefense = 0,
                CanAttack = false,
                DamageType = DamageType.None,
                IsScience = true,
                SciencePowerUpkeep = 0,
                TurnStartResourceGain = new JsonResourceSet
                {
                    Power = NuclearPowerPlantRules.TurnStartPowerGain,
                },
            };

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(nuclearPowerPlant));

            Assert.That(result.Errors, Has.Some.Contains("30 physical destruction damage"));
        }

        [Test]
        public void Validate_MerchantCaravanWithWrongGoldGain_Fails()
        {
            var merchantCaravan = new JsonCardDefinitionRecord
            {
                Id = MerchantCaravanRules.CardId,
                DisplayName = "상단",
                DefinitionType = JsonCardDefinitionKind.Building,
                Rarity = CardRarity.Uncommon,
                IncludeInDraft = false,
                IncludeInRewards = false,
                Affiliation = CardAffiliation.Murim,
                ChargeTileFootprint = ChargeTileFootprint.OneByOne,
                Cost = new JsonResourceSet { Gold = MerchantCaravanRules.GoldCost },
                EffectText = "내 턴 시작 시 골드 +2",
                Attack = 0,
                Health = MerchantCaravanRules.BaseHealth,
                PhysicalDefense = 0,
                MagicDefense = 0,
                CanAttack = false,
                DamageType = DamageType.None,
                TurnStartResourceGain = new JsonResourceSet { Gold = 2 },
            };

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(merchantCaravan));

            Assert.That(result.Errors, Has.Some.Contains("MerchantCaravan must grant exactly 3 gold"));
        }

        [Test]
        public void Validate_InnWithWrongGoldGain_Fails()
        {
            var inn = new JsonCardDefinitionRecord
            {
                Id = InnRules.CardId,
                DisplayName = "객잔",
                DefinitionType = JsonCardDefinitionKind.Building,
                Rarity = CardRarity.Common,
                IncludeInDraft = false,
                IncludeInRewards = false,
                Affiliation = CardAffiliation.Murim,
                ChargeTileFootprint = ChargeTileFootprint.OneByOne,
                Cost = new JsonResourceSet { Qi = InnRules.QiCost },
                EffectText = "내 턴 시작 시 골드 +2",
                Attack = 0,
                Health = InnRules.BaseHealth,
                PhysicalDefense = 0,
                MagicDefense = 0,
                CanAttack = false,
                DamageType = DamageType.None,
                TurnStartResourceGain = new JsonResourceSet { Gold = 2 },
            };

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(inn));

            Assert.That(result.Errors, Has.Some.Contains("Inn must grant exactly 1 gold"));
        }

        [Test]
        public void Validate_ManaStoneWithWrongManaText_Fails()
        {
            var manaStone = new JsonCardDefinitionRecord
            {
                Id = ManaStoneRules.CardId,
                DisplayName = "마정석",
                DefinitionType = JsonCardDefinitionKind.ScriptedSpell,
                Rarity = CardRarity.Common,
                IncludeInDraft = false,
                IncludeInRewards = false,
                Affiliation = CardAffiliation.Fantasy,
                ChargeTileFootprint = ChargeTileFootprint.None,
                Cost = new JsonResourceSet { Gold = ManaStoneRules.GoldCost },
                EffectText = "사용 시 마나 획득",
                EffectId = ManaStoneRules.EffectId,
                Damage = 0,
                DamageType = DamageType.None,
            };

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(manaStone));

            Assert.That(result.Errors, Is.EquivalentTo(new[]
            {
                "ManaStone effectText must describe its immediate mana +3 effect."
            }));
        }

        [Test]
        public void Validate_ManaStoneBundleWithWrongManaText_Fails()
        {
            var manaStoneBundle = new JsonCardDefinitionRecord
            {
                Id = ManaStoneBundleRules.CardId,
                DisplayName = "마정석 꾸러미",
                DefinitionType = JsonCardDefinitionKind.ScriptedSpell,
                Rarity = CardRarity.Uncommon,
                IncludeInDraft = false,
                IncludeInRewards = false,
                Affiliation = CardAffiliation.Fantasy,
                ChargeTileFootprint = ChargeTileFootprint.None,
                Cost = new JsonResourceSet { Gold = ManaStoneBundleRules.GoldCost },
                EffectText = "사용 시 마나 획득",
                EffectId = ManaStoneBundleRules.EffectId,
                Damage = 0,
                DamageType = DamageType.None,
            };

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(manaStoneBundle));

            Assert.That(result.Errors, Has.Some.Contains("immediate mana +9"));
        }

        [Test]
        public void Validate_NegativeDefense_Fails()
        {
            var card = CreateUnitRecord("invalid-defense");
            card.PhysicalDefense = -1;

            var result = CardDatabaseValidator.Validate(CreateDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("negative physicalDefense"));
        }

        [Test]
        public void Validate_AttackingUnitWithoutDamageType_Fails()
        {
            var card = CreateUnitRecord("missing-damage-type");
            card.Attack = 1;
            card.DamageType = DamageType.None;

            var result = CardDatabaseValidator.Validate(CreateDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("must define a damageType"));
        }

        [Test]
        public void Validate_RobotFlagWithoutRobotText_Fails()
        {
            var card = CreateUnitRecord("robot-without-text");
            card.HasRobot = true;

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("has Robot"));
        }

        [Test]
        public void Validate_RobotTextWithoutRobotFlag_Fails()
        {
            var card = CreateUnitRecord("robot-text-without-flag");
            card.SpecialEffectText = "로봇";

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("hasRobot is false"));
        }

        [Test]
        public void Validate_KoreanRushTextWithFlag_PassesAndCreatesRushDefinition()
        {
            var card = CreateUnitRecord("rush-unit");
            card.HasRush = true;
            card.SpecialEffectText = "속공: 소환한 턴에 바로 공격 가능";

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(card));
            var definition = card.ToDefinition() as UnitCardDefinition;

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.HasRush, Is.True);
        }

        [Test]
        public void Validate_KoreanSealboundTextWithCount_PassesAndCreatesDefinition()
        {
            var card = CreateUnitRecord("sealbound-unit");
            card.SealboundOwnerTurnStarts = 2;
            card.SpecialEffectText = "봉인 2";

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(card));
            var definition = card.ToDefinition() as UnitCardDefinition;

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.SealboundOwnerTurnStarts, Is.EqualTo(2));
        }

        [Test]
        public void Validate_KoreanHidingTextWithFlag_PassesAndCreatesDefinition()
        {
            var card = CreateUnitRecord("hiding-unit");
            card.HasHiding = true;
            card.SpecialEffectText = "은신: 공격 전까지 적 일반공격과 단일 대상 마법의 대상이 되지 않음";

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(card));
            var definition = card.ToDefinition() as UnitCardDefinition;

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.HasHiding, Is.True);
        }

        [Test]
        public void Validate_HidingFlagOnSpell_Fails()
        {
            var card = new JsonCardDefinitionRecord
            {
                Id = "invalid-hiding-spell",
                DisplayName = "Invalid Hiding Spell",
                DefinitionType = JsonCardDefinitionKind.DamageSpell,
                ChargeTileFootprint = ChargeTileFootprint.None,
                Damage = 1,
                DamageType = DamageType.Magic,
                HasHiding = true,
                SpecialEffectText = "은신",
            };

            var result = CardDatabaseValidator.Validate(CreateDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("hasHiding can only be used by Unit cards"));
        }

        [Test]
        public void Validate_SealboundCountOnSpell_Fails()
        {
            var card = new JsonCardDefinitionRecord
            {
                Id = "invalid-sealbound-spell",
                DisplayName = "Invalid Sealbound Spell",
                DefinitionType = JsonCardDefinitionKind.DamageSpell,
                ChargeTileFootprint = ChargeTileFootprint.None,
                Damage = 1,
                DamageType = DamageType.Magic,
                SealboundOwnerTurnStarts = 1,
                SpecialEffectText = "봉인 1",
            };

            var result = CardDatabaseValidator.Validate(CreateDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("can only be used by Unit or Building"));
        }

        [Test]
        public void Validate_KoreanReplicateTextWithFlag_ZeroCostPassesAndCreatesDefinition()
        {
            var card = CreateUnitRecord("zero-cost-replicate");
            card.HasReplicate = true;
            card.SpecialEffectText = "복제: 사용하면 그 턴에만 유지되는 사본을 생성";

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(card));
            var definition = card.ToDefinition();

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            Assert.That(definition.HasReplicate, Is.True);
            Assert.That(definition.Cost.Mana, Is.Zero);
            Assert.That(definition.Cost.Qi, Is.Zero);
            Assert.That(definition.Cost.Power, Is.Zero);
            Assert.That(definition.Cost.Gold, Is.Zero);
        }

        [Test]
        public void Validate_ReplicateFlagWithoutReplicateText_Fails()
        {
            var card = CreateUnitRecord("replicate-without-text");
            card.HasReplicate = true;

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("has Replicate"));
        }

        [Test]
        public void Validate_ReplicateTextWithoutReplicateFlag_Fails()
        {
            var card = CreateUnitRecord("replicate-text-without-flag");
            card.SpecialEffectText = "복제";

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("hasReplicate is false"));
        }

        [Test]
        public void Validate_RushFlagWithoutRushText_Fails()
        {
            var card = CreateUnitRecord("rush-without-text");
            card.HasRush = true;

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("has Rush"));
        }

        [Test]
        public void Validate_RushTextWithoutRushFlag_Fails()
        {
            var card = CreateUnitRecord("rush-text-without-flag");
            card.SpecialEffectText = "속공";

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("hasRush is false"));
        }

        [Test]
        public void Validate_NonUnitWithRushFlag_Fails()
        {
            var card = new JsonCardDefinitionRecord
            {
                Id = "rush-spell",
                DisplayName = "Rush Spell",
                DefinitionType = JsonCardDefinitionKind.DamageSpell,
                ChargeTileFootprint = ChargeTileFootprint.None,
                Damage = 1,
                DamageType = DamageType.Magic,
                HasRush = true,
            };

            var result = CardDatabaseValidator.Validate(CreateDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("only be used by Unit cards"));
        }

        [Test]
        public void Validate_KoreanFlyingTextWithFlag_PassesAndCreatesDefinition()
        {
            var card = CreateUnitRecord("flying-unit");
            card.HasFlying = true;
            card.SpecialEffectText = "비행: 근접 일반공격의 대상이 되지 않고 전열차단을 하지 않음";
            var database = CreateDraftCompatibleDatabase(card);

            var result = CardDatabaseValidator.Validate(database);
            var definition = database.CreateProvider().GetRequired(card.Id) as UnitCardDefinition;

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.HasFlying, Is.True);
        }

        [Test]
        public void Validate_FlyingBuilding_PassesAndCreatesDefinition()
        {
            var card = new JsonCardDefinitionRecord
            {
                Id = "flying-building",
                DisplayName = "Flying Building",
                DefinitionType = JsonCardDefinitionKind.Building,
                ChargeTileFootprint = ChargeTileFootprint.OneByOne,
                Health = 10,
                Attack = 0,
                HitsPerAttack = 1,
                HasFlying = true,
                SpecialEffectText = "Flying",
            };
            var database = CreateDraftCompatibleDatabase(card);

            var result = CardDatabaseValidator.Validate(database);
            var definition = database.CreateProvider().GetRequired(card.Id) as BuildingCardDefinition;

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.HasFlying, Is.True);
        }

        [Test]
        public void Validate_FlyingFlagOnSpell_Fails()
        {
            var card = new JsonCardDefinitionRecord
            {
                Id = "flying-spell",
                DisplayName = "Flying Spell",
                DefinitionType = JsonCardDefinitionKind.DamageSpell,
                ChargeTileFootprint = ChargeTileFootprint.None,
                Damage = 1,
                DamageType = DamageType.Magic,
                HasFlying = true,
                SpecialEffectText = "비행",
            };

            var result = CardDatabaseValidator.Validate(CreateDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("hasFlying can only be used by Unit or Building cards"));
        }

        [Test]
        public void Validate_FlyingTextWithoutFlag_Fails()
        {
            var card = CreateUnitRecord("flying-text-without-flag");
            card.SpecialEffectText = "비행";

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("hasFlying is false"));
        }

        [Test]
        public void Validate_KoreanPiercingTextWithFlag_PassesAndCreatesDefinition()
        {
            var card = CreateUnitRecord("piercing-unit");
            card.HasPiercing = true;
            card.SpecialEffectText = "관통";
            var database = CreateDraftCompatibleDatabase(card);

            var result = CardDatabaseValidator.Validate(database);
            var definition = database.CreateProvider().GetRequired(card.Id) as UnitCardDefinition;

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.HasPiercing, Is.True);
        }

        [Test]
        public void Validate_AttackCapablePiercingBuilding_PassesAndCreatesDefinition()
        {
            var card = new JsonCardDefinitionRecord
            {
                Id = "piercing-building",
                DisplayName = "Piercing Building",
                DefinitionType = JsonCardDefinitionKind.Building,
                ChargeTileFootprint = ChargeTileFootprint.OneByOne,
                Health = 10,
                Attack = 2,
                CanAttack = true,
                HitsPerAttack = 1,
                HasPiercing = true,
                SpecialEffectText = "Piercing",
            };
            var database = CreateDraftCompatibleDatabase(card);

            var result = CardDatabaseValidator.Validate(database);
            var definition = database.CreateProvider().GetRequired(card.Id) as BuildingCardDefinition;

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.HasPiercing, Is.True);
        }

        [Test]
        public void Validate_PiercingFlagOnSpell_Fails()
        {
            var card = new JsonCardDefinitionRecord
            {
                Id = "piercing-spell",
                DisplayName = "Piercing Spell",
                DefinitionType = JsonCardDefinitionKind.DamageSpell,
                ChargeTileFootprint = ChargeTileFootprint.None,
                Damage = 1,
                DamageType = DamageType.Magic,
                HasPiercing = true,
                SpecialEffectText = "관통",
            };

            var result = CardDatabaseValidator.Validate(CreateDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("hasPiercing can only be used by Unit or Building cards"));
        }

        [Test]
        public void Validate_PiercingBuildingWithoutAttackCapability_Fails()
        {
            var card = new JsonCardDefinitionRecord
            {
                Id = "non-attacking-piercing-building",
                DisplayName = "Non-attacking Piercing Building",
                DefinitionType = JsonCardDefinitionKind.Building,
                ChargeTileFootprint = ChargeTileFootprint.OneByOne,
                Health = 10,
                HasPiercing = true,
                SpecialEffectText = "관통",
            };

            var result = CardDatabaseValidator.Validate(CreateDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("requires an attack-capable Building"));
        }

        [Test]
        public void Validate_PiercingTextWithoutFlag_Fails()
        {
            var card = CreateUnitRecord("piercing-text-without-flag");
            card.SpecialEffectText = "관통";

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("hasPiercing is false"));
        }

        [Test]
        public void Validate_OwnerTurnsInvincibleWithKoreanText_PassesAndCreatesDefinition()
        {
            var card = CreateUnitRecord("invincible-unit");
            card.InvincibleDuration = InvincibleDurationType.OwnerTurns;
            card.InvincibleOwnerTurns = 2;
            card.SpecialEffectText = "내 2턴 동안 무적";
            var database = CreateDraftCompatibleDatabase(card);

            var result = CardDatabaseValidator.Validate(database);
            var definition = database.CreateProvider().GetRequired(card.Id) as UnitCardDefinition;

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.InvincibleDuration, Is.EqualTo(InvincibleDurationType.OwnerTurns));
            Assert.That(definition.InvincibleOwnerTurns, Is.EqualTo(2));
        }

        [Test]
        public void Validate_InvincibleTextWithoutDuration_Fails()
        {
            var card = CreateUnitRecord("invincible-text-without-duration");
            card.SpecialEffectText = "소환된 턴 동안 무적";

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("invincibleDuration is None"));
        }

        [Test]
        public void Validate_OwnerTurnsInvincibleWithoutPositiveCount_Fails()
        {
            var card = CreateUnitRecord("invincible-without-count");
            card.InvincibleDuration = InvincibleDurationType.OwnerTurns;
            card.SpecialEffectText = "무적";

            var result = CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(card));

            Assert.That(result.Errors, Has.Some.Contains("requires positive invincibleOwnerTurns"));
        }

        private static JsonCardDefinitionDatabase CreateDatabase(params JsonCardDefinitionRecord[] cards)
        {
            var database = new JsonCardDefinitionDatabase();
            database.Cards.AddRange(cards);
            return database;
        }

        private static JsonCardDefinitionDatabase CreateDraftCompatibleDatabase(JsonCardDefinitionRecord testedCard)
        {
            var database = CreateDatabase(testedCard);
            for (var index = 0; index < 3; index += 1)
            {
                var legendary = CreateUnitRecord($"legendary-filler-{index}");
                legendary.Rarity = CardRarity.Legendary;
                database.Cards.Add(legendary);
            }

            // The tested card can be excluded from the draft pool.
            for (var index = 0; index < 13; index += 1)
            {
                database.Cards.Add(CreateUnitRecord($"non-legendary-filler-{index}"));
            }

            return database;
        }

        private static JsonCardDefinitionRecord CreateUnitRecord(string id)
        {
            return new JsonCardDefinitionRecord
            {
                Id = id,
                DisplayName = id,
                DefinitionType = JsonCardDefinitionKind.Unit,
                ChargeTileFootprint = ChargeTileFootprint.OneByOne,
                Health = 1,
                Attack = 0,
                MaxAttacksPerTurn = 1,
                HitsPerAttack = 1,
            };
        }

        private static void AssertUnit(
            ICardDefinitionProvider provider,
            string cardId,
            DamageType expectedDamageType,
            int expectedPhysicalDefense = 0,
            int expectedMagicDefense = 0)
        {
            var unit = provider.GetRequired(cardId) as UnitCardDefinition;
            Assert.That(unit, Is.Not.Null, $"Expected '{cardId}' to be a unit definition.");
            Assert.That(unit.DamageType, Is.EqualTo(expectedDamageType), cardId);
            Assert.That(unit.PhysicalDefense, Is.EqualTo(expectedPhysicalDefense), cardId);
            Assert.That(unit.MagicDefense, Is.EqualTo(expectedMagicDefense), cardId);
        }

        private static void AssertSimpleFantasyUnit(
            ICardDefinitionProvider provider,
            string cardsJsonPath,
            string cardId,
            int expectedAttack,
            int expectedHealth,
            int expectedManaCost = 1)
        {
            var unit = provider.GetRequired(cardId) as UnitCardDefinition;
            Assert.That(unit, Is.Not.Null);
            Assert.That(unit.Cost.Mana, Is.EqualTo(expectedManaCost), cardId);
            Assert.That(unit.AttackType, Is.EqualTo(AttackType.Melee), cardId);
            Assert.That(unit.DamageType, Is.EqualTo(DamageType.Physical), cardId);
            Assert.That(unit.Attack, Is.EqualTo(expectedAttack), cardId);
            Assert.That(unit.Health, Is.EqualTo(expectedHealth), cardId);
            Assert.That(unit.CanMove, Is.True, cardId);

            var record = JsonCardDefinitionDatabase
                .FromJson(File.ReadAllText(cardsJsonPath))
                .Cards
                .Find(card => card.Id == cardId);
            Assert.That(record, Is.Not.Null);
            Assert.That(record.Rarity, Is.EqualTo(CardRarity.Common), cardId);
            Assert.That(record.Affiliation, Is.EqualTo(CardAffiliation.Fantasy), cardId);
            Assert.That(record.IncludeInDraft, Is.True, cardId);
            Assert.That(record.IncludeInRewards, Is.True, cardId);
            Assert.That(record.EffectText, Is.Empty, cardId);
            Assert.That(record.SpecialEffectText, Is.Empty, cardId);
        }
    }
}
