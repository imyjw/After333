using System;
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

        public ResourceSetData Cost => _cost;

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

        public bool HasReplicate => _hasReplicate;

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
