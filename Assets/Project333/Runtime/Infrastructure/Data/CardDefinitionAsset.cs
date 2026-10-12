using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Project333.Runtime.Infrastructure.Data
{
    public abstract class CardDefinitionAsset : ScriptableObject
    {
        [Header("Core")]
        [SerializeField] private string _cardId;
        [SerializeField] private string _displayName;
        [SerializeField] private CardRarity _rarity = CardRarity.Common;
        [SerializeField] private ResourceSetData _cost;
        [Header("Theme")]
        [SerializeField] private CardAffiliation _affiliation = CardAffiliation.Neutral;
        [SerializeField] private ChargeTileFootprint _chargeTileFootprint = ChargeTileFootprint.OneByOne;
        [Header("Rules Text")]
        [TextArea(2, 4)]
        [SerializeField] private string _effectText;
        [TextArea(2, 4)]
        [SerializeField] private string _specialEffectText;
        [SerializeField] private bool _hasReplicate;
        [Header("Board Visual")]
        [SerializeField] private Sprite _boardSprite;
        [SerializeField] private RuntimeAnimatorController _boardAnimatorController;

        public string CardId => _cardId;

        public string DisplayName => _displayName;

        public CardRarity Rarity => _rarity;

        public virtual ResourceSetData Cost => _cost;

        public CardAffiliation Affiliation => _affiliation;

        public ChargeTileFootprint ChargeTileFootprint => _chargeTileFootprint;

        public string EffectText => _effectText;

        public virtual string SpecialEffectText
        {
            get
            {
                var text = _specialEffectText?.Trim() ?? string.Empty;
                if (!_hasReplicate ||
                    text.IndexOf("Replicate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.Contains("복제"))
                {
                    return text;
                }

                return string.IsNullOrWhiteSpace(text)
                    ? "복제"
                    : $"{text}{Environment.NewLine}복제";
            }
        }

        protected static string NormalizePowerConsumptionSpecialEffectText(string text, int amount)
        {
            text = text?.Trim() ?? string.Empty;
            if (amount <= 0)
            {
                return text;
            }

            var lines = new List<string>();
            var hasPowerConsumption = false;
            foreach (var segment in text.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var label = segment.Trim();
                // Old native assets retain their serialized values; the typed amount is authoritative.
                if (Regex.IsMatch(label, @"^전력\s*(?:소모\s*:\s*|-)\s*[0-9]+\s*$"))
                {
                    if (!hasPowerConsumption)
                    {
                        lines.Add($"전력 소모:{amount}");
                        hasPowerConsumption = true;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(label))
                {
                    lines.Add(label);
                }
            }

            if (!hasPowerConsumption)
            {
                lines.Add($"전력 소모:{amount}");
            }

            return string.Join(Environment.NewLine, lines);
        }
        public bool HasReplicate => _hasReplicate;

        public virtual bool HasTrap => false;

        protected string RawSpecialEffectText => _specialEffectText;

        public Sprite BoardSprite => _boardSprite;

        public RuntimeAnimatorController BoardAnimatorController => _boardAnimatorController;

        public abstract CardDefinition ToDefinition();

        public void ConfigureBaseForTests(string cardId, string displayName, ResourceSetData cost)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                throw new ArgumentException("Card id is required.", nameof(cardId));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Display name is required.", nameof(displayName));
            }

            _cardId = cardId;
            _displayName = displayName;
            _cost = cost;
        }

        public void ConfigureMetadataForTests(
            CardRarity rarity,
            CardAffiliation affiliation,
            ChargeTileFootprint chargeTileFootprint,
            string effectText,
            string specialEffectText)
        {
            _rarity = rarity;
            _affiliation = affiliation;
            _chargeTileFootprint = chargeTileFootprint;
            _effectText = effectText ?? string.Empty;
            _specialEffectText = specialEffectText ?? string.Empty;
        }

        public void ConfigureBoardVisualsForTests(Sprite boardSprite, RuntimeAnimatorController boardAnimatorController)
        {
            _boardSprite = boardSprite;
            _boardAnimatorController = boardAnimatorController;
        }

        public void ConfigureReplicateForTests(bool hasReplicate)
        {
            _hasReplicate = hasReplicate;
        }
    }
}
