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
        [SerializeField] private string _attribute = "Neutral";
        [SerializeField] private CardAffiliation _affiliation = CardAffiliation.Neutral;
        [SerializeField] private ChargeTileFootprint _chargeTileFootprint = ChargeTileFootprint.OneByOne;
        [Header("Rules Text")]
        [TextArea(2, 4)]
        [SerializeField] private string _effectText;
        [TextArea(2, 4)]
        [SerializeField] private string _specialEffectText;
        [Header("Board Visual")]
        [SerializeField] private Sprite _boardSprite;
        [SerializeField] private RuntimeAnimatorController _boardAnimatorController;

        public string CardId => _cardId;

        public string DisplayName => _displayName;

        public CardRarity Rarity => _rarity;

        public ResourceSetData Cost => _cost;

        public string Attribute => _attribute;

        public CardAffiliation Affiliation => _affiliation;

        public ChargeTileFootprint ChargeTileFootprint => _chargeTileFootprint;

        public string EffectText => _effectText;

        public virtual string SpecialEffectText => _specialEffectText;

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
            string attribute,
            CardAffiliation affiliation,
            ChargeTileFootprint chargeTileFootprint,
            string effectText,
            string specialEffectText)
        {
            _rarity = rarity;
            _attribute = string.IsNullOrWhiteSpace(attribute) ? "Neutral" : attribute;
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
    }
}
