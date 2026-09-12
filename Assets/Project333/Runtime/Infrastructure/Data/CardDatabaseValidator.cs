using System;
using System.Collections.Generic;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Cards;
#if UNITY_5_3_OR_NEWER
using Newtonsoft.Json.Linq;
#else
using System.Text.Json;
#endif

namespace Project333.Runtime.Infrastructure.Data
{
    public static class CardDatabaseValidator
    {
        private const string RemovedAttributeFieldName = "attribute";

        private static readonly HashSet<string> SupportedScriptedEffectIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "cheonra_jimang",
            "daehwandan",
            "firewall",
            "robot_fusion",
            BiochemicalBombRules.EffectId,
            TimedBombRules.EffectId,
            PowerBankRules.EffectId,
            ManaStoneRules.EffectId,
            ManaStoneBundleRules.EffectId,
            GuRules.EffectId,
            HuanShuRules.EffectId,
        };

        public static CardDatabaseValidationResult ValidateJson(string json)
        {
            var result = new CardDatabaseValidationResult();
            if (string.IsNullOrWhiteSpace(json))
            {
                result.AddError("Card database JSON is empty.");
                return result;
            }

            try
            {
                ValidateRawJson(json, result);
            }
            catch (Exception exception)
            {
                result.AddError($"Card database JSON could not be parsed: {exception.Message}");
                return result;
            }

            try
            {
                Merge(result, Validate(JsonCardDefinitionDatabase.FromJson(json)));
            }
            catch (Exception exception)
            {
                result.AddError($"Card database JSON could not be converted to card definitions: {exception.Message}");
            }

            return result;
        }

        public static CardDatabaseValidationResult Validate(JsonCardDefinitionDatabase database)
        {
            var result = new CardDatabaseValidationResult();
            if (database == null)
            {
                result.AddError("Card database is null.");
                return result;
            }

            if (database.SchemaVersion != 1)
            {
                result.AddError($"Unsupported schemaVersion '{database.SchemaVersion}'. Expected 1.");
            }

            if (database.Cards == null || database.Cards.Count == 0)
            {
                result.AddError("Card database must contain at least one card.");
                return result;
            }

            ValidateDuplicateIds(database.Cards, result);
            foreach (var card in database.Cards)
            {
                ValidateCard(card, result);
            }

            ValidateRobotFactoryPool(database.Cards, result);
            ValidateDraftPoolShape(database.Cards, result);
            return result;
        }

        private static void Merge(CardDatabaseValidationResult target, CardDatabaseValidationResult source)
        {
            foreach (var error in source.Errors)
            {
                target.AddError(error);
            }

            foreach (var warning in source.Warnings)
            {
                target.AddWarning(warning);
            }
        }

#if UNITY_5_3_OR_NEWER
        private static void ValidateRawJson(string json, CardDatabaseValidationResult result)
        {
            var root = JObject.Parse(json);
            if (root["cards"] is not JArray cards)
            {
                return;
            }

            for (var index = 0; index < cards.Count; index += 1)
            {
                if (cards[index] is not JObject card)
                {
                    continue;
                }

                var cardId = ResolveRawCardId(card["id"], index);
                foreach (var property in card.Properties())
                {
                    ValidateRawPropertyName(property.Name, cardId, result);
                }
            }
        }

        private static string ResolveRawCardId(JToken idToken, int index)
        {
            var id = idToken?.ToString();
            return string.IsNullOrWhiteSpace(id) ? $"#{index}" : id;
        }
#else
        private static void ValidateRawJson(string json, CardDatabaseValidationResult result)
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("cards", out var cards) ||
                cards.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var index = 0;
            foreach (var card in cards.EnumerateArray())
            {
                if (card.ValueKind != JsonValueKind.Object)
                {
                    index += 1;
                    continue;
                }

                var id = card.TryGetProperty("id", out var idProperty) && idProperty.ValueKind == JsonValueKind.String
                    ? idProperty.GetString()
                    : null;
                var cardId = string.IsNullOrWhiteSpace(id) ? $"#{index}" : id;
                foreach (var property in card.EnumerateObject())
                {
                    ValidateRawPropertyName(property.Name, cardId, result);
                }

                index += 1;
            }
        }
#endif

        private static void ValidateRawPropertyName(string propertyName, string cardId, CardDatabaseValidationResult result)
        {
            if (string.Equals(propertyName, RemovedAttributeFieldName, StringComparison.OrdinalIgnoreCase))
            {
                result.AddError($"Card '{cardId}' contains removed field '{RemovedAttributeFieldName}'. Remove it from cards.json.");
            }
        }

        private static void ValidateDuplicateIds(IReadOnlyList<JsonCardDefinitionRecord> cards, CardDatabaseValidationResult result)
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var card in cards)
            {
                if (card == null || string.IsNullOrWhiteSpace(card.Id))
                {
                    continue;
                }

                if (!seenIds.Add(card.Id))
                {
                    result.AddError($"Duplicate card id '{card.Id}'.");
                }
            }
        }

        private static void ValidateCard(JsonCardDefinitionRecord card, CardDatabaseValidationResult result)
        {
            if (card == null)
            {
                result.AddError("Card database contains a null card record.");
                return;
            }

            var id = string.IsNullOrWhiteSpace(card.Id) ? "<missing id>" : card.Id;
            if (string.IsNullOrWhiteSpace(card.Id))
            {
                result.AddError("Card record is missing id.");
            }

            if (string.IsNullOrWhiteSpace(card.DisplayName))
            {
                result.AddError($"Card '{id}' is missing displayName.");
            }

            ValidateResourceSet(card.Cost, result, id, "cost", allowPositiveOnly: false);
            ValidateResourceSet(card.TurnStartResourceGain, result, id, "turnStartResourceGain", allowPositiveOnly: true);

            if (card.HasRush && card.DefinitionType != JsonCardDefinitionKind.Unit)
            {
                result.AddError($"Card '{id}' hasRush can only be used by Unit cards.");
            }

            if (card.HasHiding && card.DefinitionType != JsonCardDefinitionKind.Unit)
            {
                result.AddError($"Card '{id}' hasHiding can only be used by Unit cards.");
            }

            if (card.HasFlying &&
                card.DefinitionType != JsonCardDefinitionKind.Unit &&
                card.DefinitionType != JsonCardDefinitionKind.Building)
            {
                result.AddError($"Card '{id}' hasFlying can only be used by Unit or Building cards.");
            }

            if (card.HasPiercing &&
                card.DefinitionType != JsonCardDefinitionKind.Unit &&
                card.DefinitionType != JsonCardDefinitionKind.Building)
            {
                result.AddError($"Card '{id}' hasPiercing can only be used by Unit or Building cards.");
            }

            if (card.HasPiercing &&
                card.DefinitionType == JsonCardDefinitionKind.Building &&
                !card.CanAttack)
            {
                result.AddError($"Card '{id}' hasPiercing requires an attack-capable Building.");
            }

            if (card.SpellPower < 0)
            {
                result.AddError($"Card '{id}' cannot have negative spellPower.");
            }

            if (card.SpellPower > 0 &&
                card.DefinitionType != JsonCardDefinitionKind.Unit &&
                card.DefinitionType != JsonCardDefinitionKind.Building)
            {
                result.AddError($"Card '{id}' spellPower can only be used by Unit or Building cards.");
            }

            if (!Enum.IsDefined(typeof(InvincibleDurationType), card.InvincibleDuration))
            {
                result.AddError($"Card '{id}' has an invalid invincibleDuration.");
            }

            if (card.InvincibleDuration != InvincibleDurationType.None &&
                card.DefinitionType != JsonCardDefinitionKind.Unit &&
                card.DefinitionType != JsonCardDefinitionKind.Building)
            {
                result.AddError(
                    $"Card '{id}' invincibleDuration can only be used by Unit or Building cards.");
            }

            var countedInvincible =
                card.InvincibleDuration == InvincibleDurationType.OwnerTurns ||
                card.InvincibleDuration == InvincibleDurationType.GlobalTurnEnds;
            if (countedInvincible && card.InvincibleOwnerTurns <= 0)
            {
                result.AddError(
                    $"Card '{id}' {card.InvincibleDuration} Invincible requires positive invincibleOwnerTurns.");
            }

            if (!countedInvincible && card.InvincibleOwnerTurns != 0)
            {
                result.AddError(
                    $"Card '{id}' invincibleOwnerTurns must be 0 unless invincibleDuration uses a turn count.");
            }

            if (card.SealboundOwnerTurnStarts < 0)
            {
                result.AddError($"Card '{id}' cannot have negative sealboundOwnerTurnStarts.");
            }

            if (card.SealboundOwnerTurnStarts > 0 &&
                card.DefinitionType != JsonCardDefinitionKind.Unit &&
                card.DefinitionType != JsonCardDefinitionKind.Building)
            {
                result.AddError(
                    $"Card '{id}' sealboundOwnerTurnStarts can only be used by Unit or Building cards.");
            }

            switch (card.DefinitionType)
            {
                case JsonCardDefinitionKind.Unit:
                    ValidateUnit(card, result, id);
                    break;

                case JsonCardDefinitionKind.Building:
                    ValidateBuilding(card, result, id);
                    break;

                case JsonCardDefinitionKind.DamageSpell:
                    ValidateDamageSpell(card, result, id);
                    break;

                case JsonCardDefinitionKind.PersistentResourceSpell:
                    ValidatePersistentResourceSpell(card, result, id);
                    break;

                case JsonCardDefinitionKind.ScriptedSpell:
                    ValidateScriptedSpell(card, result, id);
                    break;

                default:
                    result.AddError($"Card '{id}' uses unsupported definitionType '{card.DefinitionType}'.");
                    break;
            }

            ValidateReplicateText(card, result, id);

            if (string.Equals(card.Id, RobotFactoryService.CardId, StringComparison.Ordinal))
            {
                ValidateRobotFactory(card, result);
            }

            if (string.Equals(card.Id, GaebangBranchRules.CardId, StringComparison.Ordinal))
            {
                ValidateGaebangBranch(card, result);
            }

            if (string.Equals(card.Id, MerchantCaravanRules.CardId, StringComparison.Ordinal))
            {
                ValidateMerchantCaravan(card, result);
            }

            if (string.Equals(card.Id, InnRules.CardId, StringComparison.Ordinal))
            {
                ValidateInn(card, result);
            }

            if (string.Equals(card.Id, PowerPlantRules.CardId, StringComparison.Ordinal))
            {
                ValidatePowerPlant(card, result);
            }

            if (string.Equals(card.Id, NuclearPowerPlantRules.CardId, StringComparison.Ordinal))
            {
                ValidateNuclearPowerPlant(card, result);
            }

            if (string.Equals(card.Id, TimedBombRules.CardId, StringComparison.Ordinal))
            {
                ValidateTimedBomb(card, result);
            }

            if (string.Equals(card.Id, BiochemicalBombRules.CardId, StringComparison.Ordinal))
            {
                ValidateBiochemicalBomb(card, result);
            }

            if (string.Equals(card.Id, PowerBankRules.CardId, StringComparison.Ordinal))
            {
                ValidatePowerBank(card, result);
            }

            if (string.Equals(card.Id, ManaStoneRules.CardId, StringComparison.Ordinal))
            {
                ValidateManaStone(card, result);
            }

            if (string.Equals(card.Id, ManaStoneBundleRules.CardId, StringComparison.Ordinal))
            {
                ValidateManaStoneBundle(card, result);
            }

            if (string.Equals(
                    card.Id,
                    TenThousandYearSnowGinsengRules.CardId,
                    StringComparison.Ordinal))
            {
                ValidateTenThousandYearSnowGinseng(card, result);
            }

            if (string.Equals(card.Id, GuRules.CardId, StringComparison.Ordinal))
            {
                ValidateGu(card, result);
            }

            if (string.Equals(card.Id, HuanShuRules.CardId, StringComparison.Ordinal))
            {
                ValidateHuanShu(card, result);
            }

            if (string.Equals(card.Id, WerewolfRules.CardId, StringComparison.Ordinal))
            {
                ValidateWerewolf(card, result);
            }

            if (string.Equals(card.Id, DemonKingRules.CardId, StringComparison.Ordinal))
            {
                ValidateDemonKing(card, result);
            }

            if (string.Equals(card.Id, HeroRules.CardId, StringComparison.Ordinal))
            {
                ValidateHero(card, result);
            }
        }

        private static void ValidateHero(
            JsonCardDefinitionRecord card,
            CardDatabaseValidationResult result)
        {
            if (card.DefinitionType != JsonCardDefinitionKind.Unit ||
                card.Rarity != CardRarity.Unique ||
                card.Affiliation != CardAffiliation.Fantasy ||
                card.ChargeTileFootprint != ChargeTileFootprint.OneByOne)
            {
                result.AddError("Hero must be a Unique 1x1 Fantasy Unit.");
            }

            if (card.Cost == null ||
                card.Cost.Mana != HeroRules.ManaCost ||
                card.Cost.Qi != 0 || card.Cost.Power != 0 || card.Cost.Gold != 0)
            {
                result.AddError("Hero must cost exactly 3 mana.");
            }

            if (card.AttackType != AttackType.Melee ||
                card.DamageType != DamageType.Fixed ||
                card.Attack != HeroRules.BaseAttack ||
                card.Health != HeroRules.BaseHealth ||
                card.PhysicalDefense != HeroRules.PhysicalDefense ||
                card.MagicDefense != HeroRules.MagicDefense ||
                !card.CanMove)
            {
                result.AddError("Hero must use melee Fixed ATK 3, HP 3, DEF 0/0, and be movable.");
            }

            if (card.InvincibleDuration != InvincibleDurationType.GlobalTurnEnds ||
                card.InvincibleOwnerTurns != HeroRules.InvincibleTurnEnds)
            {
                result.AddError("Hero must gain Invincible for exactly three global turn endings when summoned.");
            }

            if (card.IncludeInDraft || card.IncludeInRewards)
            {
                result.AddError("Hero must stay disabled in draft and rewards until its art is ready.");
            }

            if (string.IsNullOrWhiteSpace(card.EffectText) ||
                !card.EffectText.Contains(HeroRules.InvincibleTurnEnds.ToString()) ||
                !card.EffectText.Contains(HeroRules.GrowthAmount.ToString()) ||
                !card.EffectText.Contains("ATK") ||
                !card.EffectText.Contains("HP"))
            {
                result.AddError("Hero effectText must describe three global turn endings of Invincible and random ATK/HP +13 growth.");
            }
        }

        private static void ValidateDemonKing(
            JsonCardDefinitionRecord card,
            CardDatabaseValidationResult result)
        {
            if (card.DefinitionType != JsonCardDefinitionKind.Unit ||
                card.Rarity != CardRarity.Legendary ||
                card.Affiliation != CardAffiliation.Fantasy ||
                card.ChargeTileFootprint != ChargeTileFootprint.OneByOne)
            {
                result.AddError("DemonKing must be a Legendary 1x1 Fantasy Unit.");
            }

            if (card.Cost == null ||
                card.Cost.Mana != DemonKingRules.ManaCost ||
                card.Cost.Gold != DemonKingRules.GoldCost ||
                card.Cost.Qi != 0 || card.Cost.Power != 0)
            {
                result.AddError("DemonKing must cost exactly 3 mana and 3 gold.");
            }

            if (card.AttackType != AttackType.Melee ||
                card.DamageType != DamageType.Magic ||
                card.Attack != DemonKingRules.BaseAttack ||
                card.Health != DemonKingRules.BaseHealth ||
                card.PhysicalDefense != DemonKingRules.PhysicalDefense ||
                card.MagicDefense != DemonKingRules.MagicDefense ||
                !card.CanMove)
            {
                result.AddError("DemonKing must use melee Magic ATK 33, HP 33, DEF 3/3, and be movable.");
            }

            if (card.IncludeInDraft || card.IncludeInRewards)
            {
                result.AddError("DemonKing must stay disabled in draft and rewards until its art is ready.");
            }

            if (string.IsNullOrWhiteSpace(card.EffectText) ||
                !card.EffectText.Contains("사망") ||
                !card.EffectText.Contains("봉인") ||
                !card.EffectText.Contains(DemonKingRules.RevivalTurnStarts.ToString()) ||
                !card.EffectText.Contains(DemonKingRules.RevivalStatGain.ToString()))
            {
                result.AddError("DemonKing effectText must describe its repeating three-turn sealed revival and +33/+33 growth.");
            }
        }

        private static void ValidateWerewolf(
            JsonCardDefinitionRecord card,
            CardDatabaseValidationResult result)
        {
            if (card.DefinitionType != JsonCardDefinitionKind.Unit ||
                card.Rarity != CardRarity.Rare ||
                card.Affiliation != CardAffiliation.Fantasy ||
                card.ChargeTileFootprint != ChargeTileFootprint.OneByOne)
            {
                result.AddError("Werewolf must be a Rare 1x1 Fantasy Unit.");
            }

            if (card.Cost == null ||
                card.Cost.Mana != WerewolfRules.ManaCost ||
                card.Cost.Qi != 0 || card.Cost.Power != 0 || card.Cost.Gold != 0)
            {
                result.AddError("Werewolf must cost exactly 3 mana.");
            }

            if (card.AttackType != AttackType.Melee ||
                card.DamageType != DamageType.Physical ||
                card.Attack != WerewolfRules.BaseAttack ||
                card.Health != WerewolfRules.BaseHealth ||
                card.PhysicalDefense != 0 || card.MagicDefense != 0 ||
                !card.CanMove)
            {
                result.AddError("Werewolf must use melee Physical ATK 20, HP 30, DEF 0/0, and be movable.");
            }

            if (!card.HasReplicate)
            {
                result.AddError("Werewolf must have Replicate.");
            }


            if (string.IsNullOrWhiteSpace(card.EffectText) ||
                !card.EffectText.Contains("웨어울프") ||
                !card.EffectText.Contains(WerewolfRules.AttackPerOtherWerewolf.ToString()))
            {
                result.AddError("Werewolf effectText must describe ATK +10 for each other allied Werewolf.");
            }
        }

        private static void ValidateHuanShu(
            JsonCardDefinitionRecord card,
            CardDatabaseValidationResult result)
        {
            if (card.DefinitionType != JsonCardDefinitionKind.ScriptedSpell ||
                card.Rarity != CardRarity.Uncommon ||
                card.Affiliation != CardAffiliation.Murim ||
                card.ChargeTileFootprint != ChargeTileFootprint.None)
            {
                result.AddError("HuanShu must be an Uncommon Murim ScriptedSpell with no footprint.");
            }

            if (card.Cost == null ||
                card.Cost.Qi != HuanShuRules.QiCost ||
                card.Cost.Mana != 0 || card.Cost.Power != 0 || card.Cost.Gold != 0)
            {
                result.AddError("HuanShu must cost exactly 3 qi.");
            }

            if (!string.Equals(card.EffectId, HuanShuRules.EffectId, StringComparison.Ordinal) ||
                card.Damage != 0 || card.DamageType != DamageType.None)
            {
                result.AddError("HuanShu must use the huan_shu non-damage scripted effect.");
            }


            if (string.IsNullOrWhiteSpace(card.EffectText) ||
                !card.EffectText.Contains("환술") ||
                !card.EffectText.Contains("영구") ||
                !card.EffectText.Contains("무작위"))
            {
                result.AddError("HuanShu effectText must describe its permanent random attack targeting effect.");
            }
        }

        private static void ValidateTenThousandYearSnowGinseng(
            JsonCardDefinitionRecord card,
            CardDatabaseValidationResult result)
        {
            if (card.DefinitionType != JsonCardDefinitionKind.PersistentResourceSpell ||
                card.Rarity != CardRarity.Unique ||
                card.Affiliation != CardAffiliation.Murim ||
                card.ChargeTileFootprint != ChargeTileFootprint.None)
            {
                result.AddError(
                    "TenThousandYearSnowGinseng must be a Unique Murim PersistentResourceSpell with no footprint.");
            }

            if (card.Cost == null ||
                card.Cost.Gold != TenThousandYearSnowGinsengRules.GoldCost ||
                card.Cost.Mana != 0 || card.Cost.Qi != 0 || card.Cost.Power != 0)
            {
                result.AddError("TenThousandYearSnowGinseng must cost exactly 4 gold.");
            }

            if (!string.Equals(
                    card.EffectId,
                    TenThousandYearSnowGinsengRules.EffectId,
                    StringComparison.Ordinal) ||
                card.Damage != 0 ||
                card.DamageType != DamageType.None)
            {
                result.AddError(
                    "TenThousandYearSnowGinseng must use its non-damage persistent resource effect.");
            }

            if (card.TurnStartResourceGain == null ||
                card.TurnStartResourceGain.Qi != TenThousandYearSnowGinsengRules.TurnStartQiGain ||
                card.TurnStartResourceGain.Mana != 0 ||
                card.TurnStartResourceGain.Power != 0 ||
                card.TurnStartResourceGain.Gold != 0 ||
                card.OwnerTurnStartsRemaining != TenThousandYearSnowGinsengRules.OwnerTurnStarts)
            {
                result.AddError(
                    "TenThousandYearSnowGinseng must grant exactly 3 qi at the next 3 owner turn starts.");
            }


            if (card.IncludeInRewards)
            {
                result.AddError(
                    "TenThousandYearSnowGinseng is non-upgradeable and must not appear in random rewards.");
            }

            if (string.IsNullOrWhiteSpace(card.EffectText) ||
                !card.EffectText.Contains("기") ||
                !card.EffectText.Contains(TenThousandYearSnowGinsengRules.TurnStartQiGain.ToString()) ||
                !card.EffectText.Contains(TenThousandYearSnowGinsengRules.OwnerTurnStarts.ToString()))
            {
                result.AddError(
                    "TenThousandYearSnowGinseng effectText must describe qi +3 for the next 3 owner turn starts.");
            }
        }

        private static void ValidateGu(
            JsonCardDefinitionRecord card,
            CardDatabaseValidationResult result)
        {
            if (card.DefinitionType != JsonCardDefinitionKind.ScriptedSpell ||
                card.Rarity != CardRarity.Unique ||
                card.Affiliation != CardAffiliation.Murim ||
                card.ChargeTileFootprint != ChargeTileFootprint.None)
            {
                result.AddError("Gu must be a Unique Murim ScriptedSpell with no footprint.");
            }

            if (card.Cost == null ||
                card.Cost.Qi != GuRules.QiCost ||
                card.Cost.Mana != 0 || card.Cost.Power != 0 || card.Cost.Gold != 0)
            {
                result.AddError("Gu must cost exactly 10 qi.");
            }

            if (!string.Equals(card.EffectId, GuRules.EffectId, StringComparison.Ordinal) ||
                card.Damage != 0 || card.DamageType != DamageType.None)
            {
                result.AddError("Gu must use the gu non-damage scripted effect.");
            }


            if (string.IsNullOrWhiteSpace(card.EffectText) ||
                !card.EffectText.Contains("상대방") ||
                !card.EffectText.Contains("유닛") ||
                !card.EffectText.Contains("복종"))
            {
                result.AddError("Gu effectText must describe controlling one enemy Unit.");
            }
        }

        private static void ValidateManaStoneBundle(
            JsonCardDefinitionRecord card,
            CardDatabaseValidationResult result)
        {
            if (card.DefinitionType != JsonCardDefinitionKind.ScriptedSpell ||
                card.Rarity != CardRarity.Uncommon ||
                card.Affiliation != CardAffiliation.Fantasy ||
                card.ChargeTileFootprint != ChargeTileFootprint.None)
            {
                result.AddError("ManaStoneBundle must be an Uncommon Fantasy ScriptedSpell with no footprint.");
            }

            if (card.Cost == null ||
                card.Cost.Gold != ManaStoneBundleRules.GoldCost ||
                card.Cost.Mana != 0 || card.Cost.Qi != 0 || card.Cost.Power != 0)
            {
                result.AddError("ManaStoneBundle must cost exactly 5 gold.");
            }

            if (!string.Equals(card.EffectId, ManaStoneBundleRules.EffectId, StringComparison.Ordinal) ||
                card.Damage != 0 || card.DamageType != DamageType.None)
            {
                result.AddError("ManaStoneBundle must use the mana_stone_bundle non-damage scripted effect.");
            }

            if (string.IsNullOrWhiteSpace(card.EffectText) ||
                !card.EffectText.Contains("마나") ||
                !card.EffectText.Contains(ManaStoneBundleRules.ManaGain.ToString()))
            {
                result.AddError("ManaStoneBundle effectText must describe its immediate mana +9 effect.");
            }
        }

        private static void ValidateManaStone(
            JsonCardDefinitionRecord card,
            CardDatabaseValidationResult result)
        {
            if (card.DefinitionType != JsonCardDefinitionKind.ScriptedSpell ||
                card.Rarity != CardRarity.Common ||
                card.Affiliation != CardAffiliation.Fantasy ||
                card.ChargeTileFootprint != ChargeTileFootprint.None)
            {
                result.AddError("ManaStone must be a Common Fantasy ScriptedSpell with no footprint.");
            }

            if (card.Cost == null ||
                card.Cost.Gold != ManaStoneRules.GoldCost ||
                card.Cost.Mana != 0 || card.Cost.Qi != 0 || card.Cost.Power != 0)
            {
                result.AddError("ManaStone must cost exactly 2 gold.");
            }

            if (!string.Equals(card.EffectId, ManaStoneRules.EffectId, StringComparison.Ordinal) ||
                card.Damage != 0 || card.DamageType != DamageType.None)
            {
                result.AddError("ManaStone must use the mana_stone non-damage scripted effect.");
            }


            if (string.IsNullOrWhiteSpace(card.EffectText) ||
                !card.EffectText.Contains("마나") ||
                !card.EffectText.Contains(ManaStoneRules.ManaGain.ToString()))
            {
                result.AddError("ManaStone effectText must describe its immediate mana +3 effect.");
            }
        }

        private static void ValidatePowerBank(
            JsonCardDefinitionRecord card,
            CardDatabaseValidationResult result)
        {
            if (card.DefinitionType != JsonCardDefinitionKind.ScriptedSpell ||
                card.Rarity != CardRarity.Common ||
                card.Affiliation != CardAffiliation.ScienceCivilization ||
                card.ChargeTileFootprint != ChargeTileFootprint.None)
            {
                result.AddError("PowerBank must be a Common Science Civilization ScriptedSpell with no footprint.");
            }

            if (card.Cost == null ||
                card.Cost.Gold != PowerBankRules.GoldCost ||
                card.Cost.Mana != 0 || card.Cost.Qi != 0 || card.Cost.Power != 0)
            {
                result.AddError("PowerBank must cost exactly 3 gold.");
            }

            if (!string.Equals(card.EffectId, PowerBankRules.EffectId, StringComparison.Ordinal) ||
                card.Damage != 0 || card.DamageType != DamageType.None)
            {
                result.AddError("PowerBank must use the power_bank non-damage scripted effect.");
            }


            if (string.IsNullOrWhiteSpace(card.EffectText) ||
                !card.EffectText.Contains("전력") ||
                !card.EffectText.Contains(PowerBankRules.PowerGain.ToString()))
            {
                result.AddError("PowerBank effectText must describe its immediate power +6 effect.");
            }
        }

        private static void ValidateTimedBomb(
            JsonCardDefinitionRecord card,
            CardDatabaseValidationResult result)
        {
            if (card.DefinitionType != JsonCardDefinitionKind.ScriptedSpell ||
                card.Rarity != CardRarity.Common ||
                card.Affiliation != CardAffiliation.ScienceCivilization ||
                card.ChargeTileFootprint != ChargeTileFootprint.None)
            {
                result.AddError("TimedBomb must be a Common Science Civilization ScriptedSpell with no footprint.");
            }

            if (card.Cost == null ||
                card.Cost.Gold != TimedBombRules.GoldCost ||
                card.Cost.Power != TimedBombRules.PowerCost ||
                card.Cost.Mana != 0 || card.Cost.Qi != 0)
            {
                result.AddError("TimedBomb must cost exactly 3 power with no additional gold cost.");
            }

            if (!string.Equals(card.EffectId, TimedBombRules.EffectId, StringComparison.Ordinal) ||
                card.Damage != TimedBombRules.BaseDamage ||
                card.DamageType != DamageType.Physical ||
                card.TriggerCount != TimedBombRules.TurnStartsUntilDetonation)
            {
                result.AddError("TimedBomb must detonate after 3 turn starts for 33 physical damage.");
            }

        }

        private static void ValidateBiochemicalBomb(
            JsonCardDefinitionRecord card,
            CardDatabaseValidationResult result)
        {
            if (card.DefinitionType != JsonCardDefinitionKind.ScriptedSpell ||
                card.Rarity != CardRarity.Uncommon ||
                card.Affiliation != CardAffiliation.ScienceCivilization ||
                card.ChargeTileFootprint != ChargeTileFootprint.None)
            {
                result.AddError("BiochemicalBomb must be an Uncommon Science Civilization ScriptedSpell with no footprint.");
            }

            if (card.Cost == null ||
                card.Cost.Power != BiochemicalBombRules.PowerCost ||
                card.Cost.Gold != BiochemicalBombRules.GoldCost ||
                card.Cost.Mana != 0 || card.Cost.Qi != 0)
            {
                result.AddError("BiochemicalBomb must cost exactly 4 power and 1 gold.");
            }

            if (!string.Equals(card.EffectId, BiochemicalBombRules.EffectId, StringComparison.Ordinal) ||
                card.Damage != BiochemicalBombRules.BaseDamage ||
                card.DamageType != DamageType.Fixed ||
                card.TriggerCount != BiochemicalBombRules.TriggerCount)
            {
                result.AddError("BiochemicalBomb must deal 25 fixed damage for 4 global turn starts.");
            }

        }

        private static void ValidateGaebangBranch(
            JsonCardDefinitionRecord card,
            CardDatabaseValidationResult result)
        {
            if (card.DefinitionType != JsonCardDefinitionKind.Building ||
                card.Rarity != CardRarity.Common ||
                card.Affiliation != CardAffiliation.Murim ||
                card.ChargeTileFootprint != ChargeTileFootprint.OneByOne)
            {
                result.AddError("GaebangBranch must be a Common 1x1 Murim Building.");
            }

            if (card.Cost == null ||
                card.Cost.Gold != GaebangBranchRules.GoldCost ||
                card.Cost.Mana != 0 || card.Cost.Qi != 0 || card.Cost.Power != 0)
            {
                result.AddError("GaebangBranch must cost exactly 2 gold.");
            }

            if (card.Attack != 0 ||
                card.Health != GaebangBranchRules.BaseHealth ||
                card.CanAttack ||
                card.DamageType != DamageType.None ||
                card.PhysicalDefense != 0 || card.MagicDefense != 0)
            {
                result.AddError("GaebangBranch must use ATK 0, HP 20, DEF 0/0, no attack, and DamageType None.");
            }


            if (string.IsNullOrWhiteSpace(card.EffectText) ||
                !card.EffectText.Contains("골드") ||
                !card.EffectText.Contains("2"))
            {
                result.AddError("GaebangBranch effectText must describe its zero-gold two-card draw effect.");
            }
        }

        private static void ValidateMerchantCaravan(
            JsonCardDefinitionRecord card,
            CardDatabaseValidationResult result)
        {
            if (card.DefinitionType != JsonCardDefinitionKind.Building ||
                card.Rarity != CardRarity.Uncommon ||
                card.Affiliation != CardAffiliation.Murim ||
                card.ChargeTileFootprint != ChargeTileFootprint.OneByOne)
            {
                result.AddError("MerchantCaravan must be an Uncommon 1x1 Murim Building.");
            }

            if (card.Cost == null ||
                card.Cost.Gold != MerchantCaravanRules.GoldCost ||
                card.Cost.Mana != 0 || card.Cost.Qi != 0 || card.Cost.Power != 0)
            {
                result.AddError("MerchantCaravan must cost exactly 4 gold.");
            }

            if (card.Attack != 0 ||
                card.Health != MerchantCaravanRules.BaseHealth ||
                card.CanAttack ||
                card.DamageType != DamageType.None ||
                card.PhysicalDefense != 0 || card.MagicDefense != 0)
            {
                result.AddError($"MerchantCaravan must use ATK 0, HP {MerchantCaravanRules.BaseHealth}, DEF 0/0, no attack, and DamageType None.");
            }

            if (card.IsScience || card.SciencePowerUpkeep != 0)
            {
                result.AddError("MerchantCaravan must not use science power upkeep.");
            }

            if (card.TurnStartResourceGain == null ||
                card.TurnStartResourceGain.Gold != MerchantCaravanRules.TurnStartGoldGain ||
                card.TurnStartResourceGain.Mana != 0 ||
                card.TurnStartResourceGain.Qi != 0 ||
                card.TurnStartResourceGain.Power != 0)
            {
                result.AddError("MerchantCaravan must grant exactly 3 gold at its owner's turn start.");
            }


            if (string.IsNullOrWhiteSpace(card.EffectText) ||
                !card.EffectText.Contains("턴 시작") ||
                !card.EffectText.Contains("골드") ||
                !card.EffectText.Contains(MerchantCaravanRules.TurnStartGoldGain.ToString()))
            {
                result.AddError("MerchantCaravan effectText must describe its turn-start gold +3 effect.");
            }
        }

        private static void ValidateInn(
            JsonCardDefinitionRecord card,
            CardDatabaseValidationResult result)
        {
            if (card.DefinitionType != JsonCardDefinitionKind.Building ||
                card.Rarity != CardRarity.Common ||
                card.Affiliation != CardAffiliation.Murim ||
                card.ChargeTileFootprint != ChargeTileFootprint.OneByOne)
            {
                result.AddError("Inn must be a Common 1x1 Murim Building.");
            }

            if (card.Cost == null ||
                card.Cost.Qi != InnRules.QiCost ||
                card.Cost.Mana != 0 || card.Cost.Power != 0 || card.Cost.Gold != 0)
            {
                result.AddError("Inn must cost exactly 2 qi.");
            }

            if (card.Attack != 0 ||
                card.Health != InnRules.BaseHealth ||
                card.CanAttack ||
                card.DamageType != DamageType.None ||
                card.PhysicalDefense != 0 || card.MagicDefense != 0)
            {
                result.AddError("Inn must use ATK 0, HP 20, DEF 0/0, no attack, and DamageType None.");
            }

            if (card.IsScience || card.SciencePowerUpkeep != 0)
            {
                result.AddError("Inn must not use science power upkeep.");
            }

            if (card.TurnStartResourceGain == null ||
                card.TurnStartResourceGain.Gold != InnRules.TurnStartGoldGain ||
                card.TurnStartResourceGain.Mana != 0 ||
                card.TurnStartResourceGain.Qi != 0 ||
                card.TurnStartResourceGain.Power != 0)
            {
                result.AddError("Inn must grant exactly 1 gold at its owner's turn start.");
            }

            if (card.IncludeInDraft || card.IncludeInRewards)
            {
                result.AddError("Inn must stay disabled in draft and rewards until its art is ready.");
            }

            if (string.IsNullOrWhiteSpace(card.EffectText) ||
                !card.EffectText.Contains("턴 시작") ||
                !card.EffectText.Contains("골드") ||
                !card.EffectText.Contains(InnRules.TurnStartGoldGain.ToString()))
            {
                result.AddError("Inn effectText must describe its turn-start gold +1 effect.");
            }
        }

        private static void ValidatePowerPlant(
            JsonCardDefinitionRecord card,
            CardDatabaseValidationResult result)
        {
            if (card.DefinitionType != JsonCardDefinitionKind.Building ||
                card.Rarity != CardRarity.Common ||
                card.Affiliation != CardAffiliation.ScienceCivilization ||
                card.ChargeTileFootprint != ChargeTileFootprint.OneByOne)
            {
                result.AddError("PowerPlant must be a Common 1x1 Science Civilization building.");
            }

            if (card.Cost == null ||
                card.Cost.Power != PowerPlantRules.PlayPowerCost ||
                card.Cost.Mana != 0 || card.Cost.Qi != 0 || card.Cost.Gold != 0)
            {
                result.AddError("PowerPlant must cost exactly 1 power.");
            }

            if (card.Attack != 0 ||
                card.Health != PowerPlantRules.BaseHealth ||
                card.CanAttack ||
                card.DamageType != DamageType.None ||
                card.PhysicalDefense != 0 || card.MagicDefense != 0)
            {
                result.AddError("PowerPlant must use ATK 0, HP 30, DEF 0/0, no attack, and DamageType None.");
            }

            if (card.SciencePowerUpkeep != 0)
            {
                result.AddError("PowerPlant must not have science power upkeep.");
            }

            if (card.TurnStartResourceGain != null &&
                (card.TurnStartResourceGain.Mana != 0 ||
                 card.TurnStartResourceGain.Qi != 0 ||
                 card.TurnStartResourceGain.Power != 0 ||
                 card.TurnStartResourceGain.Gold != 0))
            {
                result.AddError("PowerPlant must use its conditional gold-to-power effect instead of turnStartResourceGain.");
            }


            if (string.IsNullOrWhiteSpace(card.EffectText) ||
                !card.EffectText.Contains("골드") ||
                !card.EffectText.Contains(PowerPlantRules.TriggerGoldCost.ToString()) ||
                !card.EffectText.Contains("전력") ||
                !card.EffectText.Contains(PowerPlantRules.PowerGain.ToString()))
            {
                result.AddError("PowerPlant effectText must describe paying 1 gold to gain 2 power at turn start.");
            }
        }

        private static void ValidateNuclearPowerPlant(
            JsonCardDefinitionRecord card,
            CardDatabaseValidationResult result)
        {
            if (card.DefinitionType != JsonCardDefinitionKind.Building ||
                card.Rarity != CardRarity.Rare ||
                card.Affiliation != CardAffiliation.ScienceCivilization ||
                card.ChargeTileFootprint != ChargeTileFootprint.OneByOne)
            {
                result.AddError("NuclearPowerPlant must be a Rare 1x1 Science Civilization building.");
            }

            if (card.Cost == null ||
                card.Cost.Power != NuclearPowerPlantRules.PowerCost ||
                card.Cost.Mana != 0 || card.Cost.Qi != 0 || card.Cost.Gold != 0)
            {
                result.AddError("NuclearPowerPlant must cost exactly 5 power.");
            }

            if (card.Attack != 0 ||
                card.Health != NuclearPowerPlantRules.BaseHealth ||
                card.CanAttack ||
                card.DamageType != DamageType.None ||
                card.PhysicalDefense != 0 || card.MagicDefense != 0)
            {
                result.AddError("NuclearPowerPlant must use ATK 0, HP 30, DEF 0/0, no attack, and DamageType None.");
            }

            if (!card.IsScience || card.SciencePowerUpkeep != 0)
            {
                result.AddError("NuclearPowerPlant must be a science building without power upkeep.");
            }

            if (card.TurnStartResourceGain == null ||
                card.TurnStartResourceGain.Power != NuclearPowerPlantRules.TurnStartPowerGain ||
                card.TurnStartResourceGain.Mana != 0 ||
                card.TurnStartResourceGain.Qi != 0 ||
                card.TurnStartResourceGain.Gold != 0)
            {
                result.AddError("NuclearPowerPlant must grant exactly 3 power at its owner's turn start.");
            }


            if (string.IsNullOrWhiteSpace(card.EffectText) ||
                !card.EffectText.Contains("턴 시작") ||
                !card.EffectText.Contains("전력") ||
                !card.EffectText.Contains(NuclearPowerPlantRules.TurnStartPowerGain.ToString()) ||
                !card.EffectText.Contains("파괴") ||
                !card.EffectText.Contains(NuclearPowerPlantRules.DestructionDamage.ToString()) ||
                !card.EffectText.Contains("물리"))
            {
                result.AddError("NuclearPowerPlant effectText must describe power +3 and its 30 physical destruction damage.");
            }
        }

        private static void ValidateRobotFactory(
            JsonCardDefinitionRecord card,
            CardDatabaseValidationResult result)
        {
            if (card.DefinitionType != JsonCardDefinitionKind.Building ||
                card.Rarity != CardRarity.Rare ||
                card.Affiliation != CardAffiliation.ScienceCivilization ||
                card.ChargeTileFootprint != ChargeTileFootprint.OneByOne)
            {
                result.AddError("RobotFactory must be a Rare 1x1 Science Civilization building.");
            }

            if (card.Cost == null || card.Cost.Power != 1 ||
                card.Cost.Mana != 0 || card.Cost.Qi != 0 || card.Cost.Gold != 0)
            {
                result.AddError("RobotFactory must cost exactly 1 power.");
            }

            if (card.Attack != 0 || card.Health != 50 || card.CanAttack ||
                card.DamageType != DamageType.None ||
                card.PhysicalDefense != 0 || card.MagicDefense != 0)
            {
                result.AddError("RobotFactory must use ATK 0, HP 50, DEF 0/0, no attack, and DamageType None.");
            }

            if (card.SciencePowerUpkeep != 1)
            {
                result.AddError("RobotFactory must use sciencePowerUpkeep 1.");
            }

            if (card.HasRobot)
            {
                result.AddError("RobotFactory itself must not have the Robot tag.");
            }

            if (card.IncludeInDraft || card.IncludeInRewards)
            {
                result.AddError("RobotFactory must stay disabled in draft and rewards until its art is ready.");
            }
        }

        private static void ValidateRobotFactoryPool(
            IReadOnlyList<JsonCardDefinitionRecord> cards,
            CardDatabaseValidationResult result)
        {
            var hasRobotFactory = false;
            var eligibleRobotCount = 0;
            foreach (var card in cards)
            {
                if (card == null)
                {
                    continue;
                }

                if (string.Equals(card.Id, RobotFactoryService.CardId, StringComparison.Ordinal))
                {
                    hasRobotFactory = true;
                }

                if (card.DefinitionType == JsonCardDefinitionKind.Unit &&
                    card.HasRobot &&
                    card.IncludeInDraft)
                {
                    eligibleRobotCount += 1;
                }
            }

            if (hasRobotFactory && eligibleRobotCount == 0)
            {
                result.AddError("RobotFactory requires at least one draft-enabled Robot unit candidate.");
            }
        }

        private static void ValidateUnit(JsonCardDefinitionRecord card, CardDatabaseValidationResult result, string id)
        {
            if (card.ChargeTileFootprint != ChargeTileFootprint.OneByOne)
            {
                result.AddError($"Unit card '{id}' must use OneByOne footprint until multi-tile rules are implemented.");
            }

            if (card.Health <= 0)
            {
                result.AddError($"Unit card '{id}' must have health greater than 0.");
            }

            if (card.Attack < 0)
            {
                result.AddError($"Unit card '{id}' cannot have negative attack.");
            }

            if (card.Attack > 0 && card.DamageType == DamageType.None)
            {
                result.AddError($"Unit card '{id}' with attack greater than 0 must define a damageType.");
            }

            if (card.MaxAttacksPerTurn < 1)
            {
                result.AddError($"Unit card '{id}' must have maxAttacksPerTurn >= 1.");
            }

            if (card.HitsPerAttack < 1)
            {
                result.AddError($"Unit card '{id}' must have hitsPerAttack >= 1.");
            }

            if (card.SciencePowerUpkeep < 0)
            {
                result.AddError($"Unit card '{id}' cannot have negative sciencePowerUpkeep.");
            }

            ValidateDefenses(card, result, id);
            ValidateSpecialEffectText(card, result, id);
        }

        private static void ValidateBuilding(JsonCardDefinitionRecord card, CardDatabaseValidationResult result, string id)
        {
            if (card.ChargeTileFootprint != ChargeTileFootprint.OneByOne)
            {
                result.AddError($"Building card '{id}' must use OneByOne footprint until multi-tile rules are implemented.");
            }

            if (card.Health <= 0)
            {
                result.AddError($"Building card '{id}' must have health greater than 0.");
            }

            if (card.Attack < 0)
            {
                result.AddError($"Building card '{id}' cannot have negative attack.");
            }

            if (card.CanAttack && card.Attack <= 0)
            {
                result.AddError($"Attack-capable building card '{id}' must have attack greater than 0.");
            }

            if (card.CanAttack && card.DamageType == DamageType.None)
            {
                result.AddError($"Attack-capable building card '{id}' must define a damageType.");
            }

            if (card.SciencePowerUpkeep < 0)
            {
                result.AddError($"Building card '{id}' cannot have negative sciencePowerUpkeep.");
            }

            if (card.SciencePowerUpkeep > 0 &&
                !string.Equals(card.Id, RobotFactoryService.CardId, StringComparison.Ordinal))
            {
                result.AddError(
                    $"Building card '{id}' uses power upkeep without an explicitly implemented building-upkeep rule.");
            }

            ValidateDefenses(card, result, id);
            ValidateSpecialEffectText(card, result, id);
        }

        private static void ValidateDamageSpell(JsonCardDefinitionRecord card, CardDatabaseValidationResult result, string id)
        {
            if (card.ChargeTileFootprint != ChargeTileFootprint.None)
            {
                result.AddError($"Damage spell card '{id}' must use None footprint.");
            }

            if (card.Damage <= 0)
            {
                result.AddError($"Damage spell card '{id}' must have damage greater than 0.");
            }

            if (card.DamageType == DamageType.None)
            {
                result.AddError($"Damage spell card '{id}' must define a damageType.");
            }
        }

        private static void ValidateDefenses(
            JsonCardDefinitionRecord card,
            CardDatabaseValidationResult result,
            string id)
        {
            if (card.PhysicalDefense < 0)
            {
                result.AddError($"Card '{id}' cannot have negative physicalDefense.");
            }

            if (card.MagicDefense < 0)
            {
                result.AddError($"Card '{id}' cannot have negative magicDefense.");
            }
        }

        private static void ValidatePersistentResourceSpell(JsonCardDefinitionRecord card, CardDatabaseValidationResult result, string id)
        {
            if (card.ChargeTileFootprint != ChargeTileFootprint.None)
            {
                result.AddError($"Persistent resource spell card '{id}' must use None footprint.");
            }

            if (string.IsNullOrWhiteSpace(card.EffectId))
            {
                result.AddError($"Persistent resource spell card '{id}' must define effectId.");
            }

            if (string.IsNullOrWhiteSpace(card.EndConditionText))
            {
                result.AddError($"Persistent resource spell card '{id}' must define endConditionText.");
            }

            if (card.OwnerTurnStartsRemaining <= 0)
            {
                result.AddError($"Persistent resource spell card '{id}' must have ownerTurnStartsRemaining > 0.");
            }

            if (!HasAnyPositiveResource(card.TurnStartResourceGain))
            {
                result.AddError($"Persistent resource spell card '{id}' must grant at least one turn-start resource.");
            }
        }

        private static void ValidateScriptedSpell(JsonCardDefinitionRecord card, CardDatabaseValidationResult result, string id)
        {
            if (card.ChargeTileFootprint != ChargeTileFootprint.None)
            {
                result.AddError($"Scripted spell card '{id}' must use None footprint.");
            }

            if (string.IsNullOrWhiteSpace(card.EffectId))
            {
                result.AddError($"Scripted spell card '{id}' must define effectId.");
                return;
            }

            if (!SupportedScriptedEffectIds.Contains(card.EffectId))
            {
                result.AddError($"Scripted spell card '{id}' uses unsupported effectId '{card.EffectId}'.");
            }

            if (string.Equals(card.EffectId, "firewall", StringComparison.Ordinal))
            {
                if (card.Damage <= 0)
                {
                    result.AddError($"Firewall card '{id}' must have damage greater than 0.");
                }

                if (card.DamageType == DamageType.None)
                {
                    result.AddError($"Firewall card '{id}' must define a damageType.");
                }

                if (card.TriggerCount <= 0)
                {
                    result.AddError($"Firewall card '{id}' must have triggerCount greater than 0.");
                }
            }

            if (string.Equals(card.EffectId, RobotFusionRules.EffectId, StringComparison.Ordinal))
            {
                if (!string.Equals(card.Id, RobotFusionRules.CardId, StringComparison.Ordinal))
                {
                    result.AddError(
                        $"Robot Fusion effect must use card id '{RobotFusionRules.CardId}'.");
                }

                if (card.Cost == null || card.Cost.Power != 3 ||
                    card.Cost.Mana != 0 || card.Cost.Qi != 0 || card.Cost.Gold != 0)
                {
                    result.AddError("Robot Fusion must cost exactly 3 power.");
                }

                if (card.IncludeInDraft || card.IncludeInRewards)
                {
                    result.AddError("Robot Fusion must stay disabled in draft and rewards until its art is ready.");
                }
            }

            if (string.Equals(card.EffectId, TimedBombRules.EffectId, StringComparison.Ordinal))
            {
                if (card.Damage <= 0)
                {
                    result.AddError($"Timed Bomb card '{id}' must have damage greater than 0.");
                }

                if (card.DamageType != DamageType.Physical)
                {
                    result.AddError($"Timed Bomb card '{id}' must use Physical damageType.");
                }

                if (card.TriggerCount != TimedBombRules.TurnStartsUntilDetonation)
                {
                    result.AddError($"Timed Bomb card '{id}' must use triggerCount {TimedBombRules.TurnStartsUntilDetonation}.");
                }
            }

            if (string.Equals(card.EffectId, BiochemicalBombRules.EffectId, StringComparison.Ordinal))
            {
                if (card.Damage <= 0)
                {
                    result.AddError($"Biochemical Bomb card '{id}' must have damage greater than 0.");
                }

                if (card.DamageType != DamageType.Fixed)
                {
                    result.AddError($"Biochemical Bomb card '{id}' must use Fixed damageType.");
                }

                if (card.TriggerCount != BiochemicalBombRules.TriggerCount)
                {
                    result.AddError($"Biochemical Bomb card '{id}' must use triggerCount {BiochemicalBombRules.TriggerCount}.");
                }
            }

            if (string.Equals(card.EffectId, GuRules.EffectId, StringComparison.Ordinal) &&
                !string.Equals(card.Id, GuRules.CardId, StringComparison.Ordinal))
            {
                result.AddError($"Gu effect must use card id '{GuRules.CardId}'.");
            }

            if (string.Equals(card.EffectId, HuanShuRules.EffectId, StringComparison.Ordinal) &&
                !string.Equals(card.Id, HuanShuRules.CardId, StringComparison.Ordinal))
            {
                result.AddError($"HuanShu effect must use card id '{HuanShuRules.CardId}'.");
            }
        }

        private static void ValidateSpecialEffectText(JsonCardDefinitionRecord card, CardDatabaseValidationResult result, string id)
        {
            var text = card.SpecialEffectText ?? string.Empty;
            var hasRush = card.DefinitionType == JsonCardDefinitionKind.Unit &&
                          (card.HasRush || card.CanAttackOnSummon);
            var containsShielder = ContainsToken(text, "Shielder") || text.Contains("쉴더");
            var containsLegacyGuard = ContainsToken(text, "Guard") || text.Contains("가드");
            RequireTextWhenFlagged(card.HasBerserker, text, "Berserker", result, id);
            RequireTextWhenFlagged(card.HasEndure, text, "Endure", result, id);
            if (containsLegacyGuard)
            {
                result.AddError(
                    $"Card '{id}' specialEffectText uses the legacy name Guard/가드. Use Shielder/쉴더 instead.");
            }

            if (card.HasShielder && !containsShielder && !containsLegacyGuard)
            {
                result.AddError(
                    $"Card '{id}' has Shielder but specialEffectText does not contain 'Shielder' or '쉴더'.");
            }

            if (card.HasLifeSteal &&
                !ContainsToken(text, "LifeSteal") &&
                !text.Contains("흡혈"))
            {
                result.AddError($"Card '{id}' has LifeSteal but specialEffectText does not contain 'LifeSteal' or '흡혈'.");
            }

            if (card.HasRobot &&
                !ContainsToken(text, "Robot") &&
                !text.Contains("로봇"))
            {
                result.AddError($"Card '{id}' has Robot but specialEffectText does not contain 'Robot' or '로봇'.");
            }

            if (hasRush &&
                !ContainsToken(text, "Rush") &&
                !text.Contains("속공"))
            {
                result.AddError($"Card '{id}' has Rush but specialEffectText does not contain 'Rush' or '속공'.");
            }

            if (card.HitsPerAttack == 2)
            {
                RequireTextContains(text, "Double Attack", result, id, "hitsPerAttack 2");
            }
            else if (card.HitsPerAttack == 3)
            {
                RequireTextContains(text, "Triple Attack", result, id, "hitsPerAttack 3");
            }
            else if (card.HitsPerAttack > 3)
            {
                RequireTextContains(text, $"{card.HitsPerAttack}연타", result, id, $"hitsPerAttack {card.HitsPerAttack}");
            }

            if (card.SciencePowerUpkeep > 0)
            {
                RequireTextContains(text, $"전력 -{card.SciencePowerUpkeep}", result, id, "sciencePowerUpkeep");
            }

            if (ContainsToken(text, "Berserker") && !card.HasBerserker)
            {
                result.AddError($"Card '{id}' specialEffectText contains Berserker but hasBerserker is false.");
            }

            if (ContainsToken(text, "Endure") && !card.HasEndure)
            {
                result.AddError($"Card '{id}' specialEffectText contains Endure but hasEndure is false.");
            }

            if (containsShielder && !card.HasShielder)
            {
                result.AddError($"Card '{id}' specialEffectText contains Shielder/쉴더 but hasShielder is false.");
            }

            if ((ContainsToken(text, "LifeSteal") || text.Contains("흡혈")) && !card.HasLifeSteal)
            {
                result.AddError($"Card '{id}' specialEffectText contains LifeSteal/흡혈 but hasLifeSteal is false.");
            }


            if ((ContainsToken(text, "Robot") || text.Contains("로봇")) && !card.HasRobot)
            {
                result.AddError($"Card '{id}' specialEffectText contains Robot/로봇 but hasRobot is false.");
            }


            if ((ContainsToken(text, "Rush") || text.Contains("속공")) && !hasRush)
            {
                result.AddError($"Card '{id}' specialEffectText contains Rush/속공 but hasRush is false.");
            }

            var containsHiding = ContainsToken(text, "Hiding") || text.Contains("은신");
            if (card.HasHiding && !containsHiding)
            {
                result.AddError(
                    $"Card '{id}' has Hiding but specialEffectText does not contain 'Hiding' or '은신'.");
            }

            if (containsHiding && !card.HasHiding)
            {
                result.AddError(
                    $"Card '{id}' specialEffectText contains Hiding/은신 but hasHiding is false.");
            }

            var containsFlying = ContainsToken(text, "Flying") || text.Contains("비행");
            if (card.HasFlying && !containsFlying)
            {
                result.AddError(
                    $"Card '{id}' has Flying but specialEffectText does not contain 'Flying' or '비행'.");
            }

            if (containsFlying && !card.HasFlying)
            {
                result.AddError(
                    $"Card '{id}' specialEffectText contains Flying/비행 but hasFlying is false.");
            }

            var containsPiercing = ContainsToken(text, "Piercing") || text.Contains("관통");
            if (card.HasPiercing && !containsPiercing)
            {
                result.AddError(
                    $"Card '{id}' has Piercing but specialEffectText does not contain 'Piercing' or '관통'.");
            }

            if (containsPiercing && !card.HasPiercing)
            {
                result.AddError(
                    $"Card '{id}' specialEffectText contains Piercing/관통 but hasPiercing is false.");
            }

            var containsSpellPower = ContainsToken(text, "SpellPower") ||
                                     text.IndexOf("Spell Power", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     text.Contains("주문력");
            if (card.SpellPower > 0 && !containsSpellPower)
            {
                result.AddError(
                    $"Card '{id}' has SpellPower but specialEffectText does not contain 'SpellPower' or '주문력'.");
            }

            if (containsSpellPower && card.SpellPower <= 0)
            {
                result.AddError(
                    $"Card '{id}' specialEffectText contains SpellPower/주문력 but spellPower is 0.");
            }

            var containsInvincible = ContainsToken(text, "Invincible") || text.Contains("무적");
            if (card.InvincibleDuration != InvincibleDurationType.None && !containsInvincible)
            {
                result.AddError(
                    $"Card '{id}' has Invincible but specialEffectText does not contain 'Invincible' or '무적'.");
            }

            if (containsInvincible && card.InvincibleDuration == InvincibleDurationType.None)
            {
                result.AddError(
                    $"Card '{id}' specialEffectText contains Invincible/무적 but invincibleDuration is None.");
            }

            var containsSealbound = ContainsToken(text, "Sealbound") || text.Contains("봉인");
            if (card.SealboundOwnerTurnStarts > 0 && !containsSealbound)
            {
                result.AddError(
                    $"Card '{id}' has Sealbound but specialEffectText does not contain 'Sealbound' or '봉인'.");
            }

            if (containsSealbound && card.SealboundOwnerTurnStarts <= 0)
            {
                result.AddError(
                    $"Card '{id}' specialEffectText contains Sealbound/봉인 but sealboundOwnerTurnStarts is 0.");
            }
        }

        private static void ValidateReplicateText(
            JsonCardDefinitionRecord card,
            CardDatabaseValidationResult result,
            string id)
        {
            var text = card.SpecialEffectText ?? string.Empty;
            var containsReplicate = ContainsToken(text, "Replicate") || text.Contains("복제");
            if (card.HasReplicate && !containsReplicate)
            {
                result.AddError($"Card '{id}' has Replicate but specialEffectText does not contain 'Replicate' or '복제'.");
            }

            if (containsReplicate && !card.HasReplicate)
            {
                result.AddError($"Card '{id}' specialEffectText contains Replicate/복제 but hasReplicate is false.");
            }
        }

        private static void RequireTextWhenFlagged(
            bool isFlagged,
            string text,
            string token,
            CardDatabaseValidationResult result,
            string id)
        {
            if (isFlagged)
            {
                RequireTextContains(text, token, result, id, token);
            }
        }

        private static void RequireTextContains(
            string text,
            string token,
            CardDatabaseValidationResult result,
            string id,
            string source)
        {
            if (!ContainsToken(text, token))
            {
                result.AddError($"Card '{id}' has {source} but specialEffectText does not contain '{token}'.");
            }
        }

        private static bool ContainsToken(string text, string token)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                   text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ValidateResourceSet(
            JsonResourceSet resourceSet,
            CardDatabaseValidationResult result,
            string id,
            string fieldName,
            bool allowPositiveOnly)
        {
            var resources = resourceSet ?? new JsonResourceSet();
            if (resources.Mana < 0 || resources.Qi < 0 || resources.Power < 0 || resources.Gold < 0)
            {
                result.AddError($"Card '{id}' has negative values in {fieldName}.");
            }

            if (allowPositiveOnly &&
                resources.Mana == 0 &&
                resources.Qi == 0 &&
                resources.Power == 0 &&
                resources.Gold == 0)
            {
                return;
            }
        }

        private static bool HasAnyPositiveResource(JsonResourceSet resourceSet)
        {
            var resources = resourceSet ?? new JsonResourceSet();
            return resources.Mana > 0 || resources.Qi > 0 || resources.Power > 0 || resources.Gold > 0;
        }

        private static void ValidateDraftPoolShape(IReadOnlyList<JsonCardDefinitionRecord> cards, CardDatabaseValidationResult result)
        {
            var legendaryCount = 0;
            var nonLegendaryCount = 0;
            foreach (var card in cards)
            {
                if (card == null ||
                    string.IsNullOrWhiteSpace(card.Id) ||
                    !card.IncludeInDraft)
                {
                    continue;
                }

                if (card.Rarity == CardRarity.Legendary)
                {
                    legendaryCount += 1;
                }
                else
                {
                    nonLegendaryCount += 1;
                }
            }

            if (legendaryCount < 3)
            {
                result.AddError($"Draft pool requires at least 3 legendary cards, but found {legendaryCount}.");
            }

            if (nonLegendaryCount < 13)
            {
                result.AddError($"Draft pool requires at least 13 non-legendary cards, but found {nonLegendaryCount}.");
            }
        }
    }
}
