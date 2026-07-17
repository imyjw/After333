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
            TimedBombRules.EffectId,
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

            if (card.InvincibleDuration == InvincibleDurationType.OwnerTurns &&
                card.InvincibleOwnerTurns <= 0)
            {
                result.AddError(
                    $"Card '{id}' OwnerTurns Invincible requires positive invincibleOwnerTurns.");
            }

            if (card.InvincibleDuration != InvincibleDurationType.OwnerTurns &&
                card.InvincibleOwnerTurns != 0)
            {
                result.AddError(
                    $"Card '{id}' invincibleOwnerTurns must be 0 unless invincibleDuration is OwnerTurns.");
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

            if (string.Equals(card.Id, TimedBombRules.CardId, StringComparison.Ordinal))
            {
                ValidateTimedBomb(card, result);
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
                result.AddError("TimedBomb must cost exactly 3 power and 1 gold.");
            }

            if (!string.Equals(card.EffectId, TimedBombRules.EffectId, StringComparison.Ordinal) ||
                card.Damage != TimedBombRules.BaseDamage ||
                card.DamageType != DamageType.Physical ||
                card.TriggerCount != TimedBombRules.TurnStartsUntilDetonation)
            {
                result.AddError("TimedBomb must detonate after 3 turn starts for 33 physical damage.");
            }

            if (card.IncludeInDraft || card.IncludeInRewards)
            {
                result.AddError("TimedBomb must stay disabled in draft and rewards until its art is ready.");
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

            if (card.IncludeInDraft || card.IncludeInRewards)
            {
                result.AddError("GaebangBranch must stay disabled in draft and rewards until its art is ready.");
            }

            if (string.IsNullOrWhiteSpace(card.EffectText) ||
                !card.EffectText.Contains("골드") ||
                !card.EffectText.Contains("2"))
            {
                result.AddError("GaebangBranch effectText must describe its zero-gold two-card draw effect.");
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
        }

        private static void ValidateSpecialEffectText(JsonCardDefinitionRecord card, CardDatabaseValidationResult result, string id)
        {
            var text = card.SpecialEffectText ?? string.Empty;
            var hasRush = card.DefinitionType == JsonCardDefinitionKind.Unit &&
                          (card.HasRush || card.CanAttackOnSummon);
            RequireTextWhenFlagged(card.HasBerserker, text, "Berserker", result, id);
            RequireTextWhenFlagged(card.HasEndure, text, "Endure", result, id);
            RequireTextWhenFlagged(card.HasGuard, text, "Guard", result, id);
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
                result.AddWarning($"Card '{id}' has hitsPerAttack {card.HitsPerAttack}; confirm this is intended.");
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

            if (ContainsToken(text, "Guard") && !card.HasGuard)
            {
                result.AddError($"Card '{id}' specialEffectText contains Guard but hasGuard is false.");
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
