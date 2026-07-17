using System;
using System.Collections.Generic;
#if UNITY_5_3_OR_NEWER
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
#else
using System.Text.Json;
using System.Text.Json.Serialization;
#endif
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Infrastructure.Data
{
    public sealed class JsonCardDefinitionDatabase
    {
#if UNITY_5_3_OR_NEWER
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Converters = { new StringEnumConverter() }
        };
#else
        private static readonly JsonSerializerOptions Settings = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            Converters = { new JsonStringEnumConverter() }
        };
#endif

        public int SchemaVersion { get; set; } = 1;

        public List<JsonCardDefinitionRecord> Cards { get; set; } = new List<JsonCardDefinitionRecord>();

        public static JsonCardDefinitionDatabase FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Card definition JSON must not be empty.", nameof(json));
            }

#if UNITY_5_3_OR_NEWER
            var database = JsonConvert.DeserializeObject<JsonCardDefinitionDatabase>(json, Settings);
#else
            var database = JsonSerializer.Deserialize<JsonCardDefinitionDatabase>(json, Settings);
#endif
            if (database == null)
            {
                throw new InvalidOperationException("Card definition JSON could not be parsed.");
            }

            database.Cards ??= new List<JsonCardDefinitionRecord>();
            return database;
        }

        public IReadOnlyList<CardDefinition> ToDefinitions()
        {
            var definitions = new List<CardDefinition>(Cards.Count);

            foreach (var card in Cards)
            {
                if (card == null)
                {
                    continue;
                }

                definitions.Add(card.ToDefinition());
            }

            return definitions;
        }

        public ICardDefinitionProvider CreateProvider()
        {
            return new InMemoryCardDefinitionProvider(ToDefinitions());
        }
    }

    public enum JsonCardDefinitionKind
    {
        Unit = 0,
        Building = 1,
        DamageSpell = 2,
        PersistentResourceSpell = 3,
        ScriptedSpell = 4,
    }

    public sealed class JsonCardDefinitionRecord
    {
        public string Id { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public JsonCardDefinitionKind DefinitionType { get; set; }

        public CardRarity Rarity { get; set; } = CardRarity.Common;

        public bool IncludeInDraft { get; set; } = true;

        public bool IncludeInRewards { get; set; } = true;

        public CardAffiliation Affiliation { get; set; } = CardAffiliation.Neutral;

        public ChargeTileFootprint ChargeTileFootprint { get; set; } = ChargeTileFootprint.OneByOne;

        public string EffectText { get; set; } = string.Empty;

        public string SpecialEffectText { get; set; } = string.Empty;

        public JsonResourceSet Cost { get; set; } = new JsonResourceSet();

        public AttackType AttackType { get; set; } = AttackType.Melee;

        public DamageType DamageType { get; set; } = DamageType.Physical;

        public int Attack { get; set; }

        public int Health { get; set; } = 1;

        public int PhysicalDefense { get; set; }

        public int MagicDefense { get; set; }

        public bool CanMove { get; set; } = true;

        public bool IsScience { get; set; }

        public int SciencePowerUpkeep { get; set; }

        public JsonResourceSet TurnStartResourceGain { get; set; } = new JsonResourceSet();

        public int MaxAttacksPerTurn { get; set; } = 1;

        public bool CanAttackOnSummon { get; set; }

        public bool HasRush { get; set; }

        public bool HasReplicate { get; set; }

        public int HitsPerAttack { get; set; } = 1;

        public bool HasBerserker { get; set; }

        public bool HasEndure { get; set; }

        public bool HasGuard { get; set; }

        public bool HasLifeSteal { get; set; }

        public bool HasRobot { get; set; }

        public int SealboundOwnerTurnStarts { get; set; }

        public bool HasHiding { get; set; }

        public bool HasFlying { get; set; }

        public int SpellPower { get; set; }

        public InvincibleDurationType InvincibleDuration { get; set; } = InvincibleDurationType.None;

        public int InvincibleOwnerTurns { get; set; }

        public bool CanAttack { get; set; }

        public int Damage { get; set; } = 1;

        public string EffectId { get; set; } = string.Empty;

        public string EndConditionText { get; set; } = string.Empty;

        public int OwnerTurnStartsRemaining { get; set; }

        public int TriggerCount { get; set; }

        public CardDefinition ToDefinition()
        {
            ValidateCoreFields();

            switch (DefinitionType)
            {
                case JsonCardDefinitionKind.Unit:
                    return new UnitCardDefinition(
                        Id,
                        DisplayName,
                        ToCost(),
                        AttackType,
                        Attack,
                        Health,
                        CanMove,
                        IsScience,
                        SciencePowerUpkeep,
                        ToTurnStartResourceGain(),
                        MaxAttacksPerTurn,
                        CanAttackOnSummon,
                        HitsPerAttack,
                        HasBerserker,
                        HasEndure,
                        HasGuard,
                        HasLifeSteal,
                        DamageType,
                        PhysicalDefense,
                        MagicDefense,
                        HasRobot,
                        IncludeInDraft,
                        HasRush || CanAttackOnSummon,
                        HasReplicate,
                        SealboundOwnerTurnStarts,
                        HasHiding,
                        HasFlying,
                        SpellPower,
                        InvincibleDuration,
                        InvincibleOwnerTurns);

                case JsonCardDefinitionKind.Building:
                    return new BuildingCardDefinition(
                        Id,
                        DisplayName,
                        ToCost(),
                        CanAttack,
                        Attack,
                        Health,
                        ToTurnStartResourceGain(),
                        CanAttackOnSummon,
                        DamageType,
                        PhysicalDefense,
                        MagicDefense,
                        SciencePowerUpkeep,
                        IncludeInDraft,
                        HasReplicate,
                        SealboundOwnerTurnStarts,
                        HasFlying,
                        SpellPower,
                        InvincibleDuration,
                        InvincibleOwnerTurns);

                case JsonCardDefinitionKind.DamageSpell:
                    return new DamageSpellCardDefinition(
                        Id,
                        DisplayName,
                        ToCost(),
                        Damage,
                        DamageType,
                        HasReplicate);

                case JsonCardDefinitionKind.PersistentResourceSpell:
                    return new PersistentResourceSpellCardDefinition(
                        Id,
                        DisplayName,
                        ToCost(),
                        RequireEffectId(),
                        ToTurnStartResourceGain(),
                        EndConditionText,
                        OwnerTurnStartsRemaining,
                        HasReplicate);

                case JsonCardDefinitionKind.ScriptedSpell:
                    return new ScriptedSpellCardDefinition(
                        Id,
                        DisplayName,
                        ToCost(),
                        RequireEffectId(),
                        Damage,
                        DamageType,
                        TriggerCount,
                        HasReplicate);

                default:
                    throw new InvalidOperationException($"Unsupported JSON card definition type '{DefinitionType}'.");
            }
        }

        private void ValidateCoreFields()
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                throw new InvalidOperationException("JSON card definition is missing an id.");
            }

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                throw new InvalidOperationException($"JSON card definition '{Id}' is missing a display name.");
            }
        }

        private string RequireEffectId()
        {
            if (string.IsNullOrWhiteSpace(EffectId))
            {
                throw new InvalidOperationException($"JSON card definition '{Id}' is missing an effect id.");
            }

            return EffectId;
        }

        private ResourceSet ToCost()
        {
            return (Cost ?? new JsonResourceSet()).ToRuntime();
        }

        private ResourceSet ToTurnStartResourceGain()
        {
            return (TurnStartResourceGain ?? new JsonResourceSet()).ToRuntime();
        }
    }

    public sealed class JsonResourceSet
    {
        public int Mana { get; set; }

        public int Qi { get; set; }

        public int Power { get; set; }

        public int Gold { get; set; }

        public ResourceSet ToRuntime()
        {
            return new ResourceSet(Mana, Qi, Power, Gold);
        }
    }
}
