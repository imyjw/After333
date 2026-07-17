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
        public void CurrentCardsJson_UsesLockedDamageTypesAndZeroStartingDefenses()
        {
            var path = Path.Combine(Application.dataPath, "Project333/Resources/Project333/Data/cards.json");
            var provider = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).CreateProvider();

            AssertUnit(provider, "A-111", DamageType.Physical);
            AssertUnit(provider, "Cerberus", DamageType.Physical);
            AssertUnit(provider, "ElfLongbowScout", DamageType.Physical);
            AssertUnit(provider, "Goblin", DamageType.Physical);
            AssertUnit(provider, "Golem", DamageType.Physical);
            AssertUnit(provider, "RedDragon", DamageType.Physical);
            AssertUnit(provider, "Shaolin_1st_Disciple", DamageType.Physical);
            AssertUnit(provider, "Vampire", DamageType.Physical);
            AssertUnit(provider, "Gwangma", DamageType.Physical);
            AssertUnit(provider, "GoldMiner", DamageType.Physical);
            AssertUnit(provider, "Shieldbearer", DamageType.Physical);
            AssertUnit(provider, "ManaWeaver", DamageType.Magic);
            AssertUnit(provider, "BlueDragon", DamageType.Magic);

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

            var blueDragon = provider.GetRequired("BlueDragon") as UnitCardDefinition;
            Assert.That(blueDragon, Is.Not.Null);
            Assert.That(blueDragon.Attack, Is.EqualTo(40));
            Assert.That(blueDragon.Health, Is.EqualTo(70));
            Assert.That(blueDragon.HasFlying, Is.True);

            var redDragon = provider.GetRequired("RedDragon") as UnitCardDefinition;
            Assert.That(redDragon, Is.Not.Null);
            Assert.That(redDragon.Attack, Is.EqualTo(50));
            Assert.That(redDragon.Health, Is.EqualTo(50));
            Assert.That(redDragon.HasFlying, Is.True);

            var gaebangBranch = provider.GetRequired("GaebangBranch") as BuildingCardDefinition;
            Assert.That(gaebangBranch, Is.Not.Null);
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
            Assert.That(gaebangBranchRecord.IncludeInDraft, Is.False);
            Assert.That(gaebangBranchRecord.IncludeInRewards, Is.False);

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
            Assert.That(timedBombRecord.IncludeInDraft, Is.False);
            Assert.That(timedBombRecord.IncludeInRewards, Is.False);

            var robotFusion = provider.GetRequired("RobotFusion") as ScriptedSpellCardDefinition;
            Assert.That(robotFusion, Is.Not.Null);
            Assert.That(robotFusion.EffectId, Is.EqualTo(RobotFusionRules.EffectId));
            Assert.That(robotFusion.Cost.Power, Is.EqualTo(3));

            var robotFusionRecord = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path)).Cards
                .Find(card => card.Id == "RobotFusion");
            Assert.That(robotFusionRecord, Is.Not.Null);
            Assert.That(robotFusionRecord.IncludeInDraft, Is.False);
            Assert.That(robotFusionRecord.IncludeInRewards, Is.False);
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

            for (var index = 0; database.Cards.Count < 16; index += 1)
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
            DamageType expectedDamageType)
        {
            var unit = provider.GetRequired(cardId) as UnitCardDefinition;
            Assert.That(unit, Is.Not.Null, $"Expected '{cardId}' to be a unit definition.");
            Assert.That(unit.DamageType, Is.EqualTo(expectedDamageType), cardId);
            Assert.That(unit.PhysicalDefense, Is.Zero, cardId);
            Assert.That(unit.MagicDefense, Is.Zero, cardId);
        }
    }
}
