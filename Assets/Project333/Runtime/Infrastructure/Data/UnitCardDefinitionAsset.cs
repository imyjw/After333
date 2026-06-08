using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Cards;
using UnityEngine;

namespace Project333.Runtime.Infrastructure.Data
{
    [CreateAssetMenu(menuName = "Project333/Cards/Unit Card", fileName = "UnitCardDefinition")]
    public sealed class UnitCardDefinitionAsset : CardDefinitionAsset
    {
        [Header("Combat")]
        [SerializeField] private AttackType _attackType = AttackType.Melee;
        [SerializeField] private int _attack;
        [SerializeField] private int _health = 1;
        [Min(1)]
        [SerializeField] private int _hitsPerAttack = 1;
        [SerializeField] private bool _hasBerserker;
        [SerializeField] private bool _hasEndure;
        [SerializeField] private bool _hasGuard;
        [Header("Board")]
        [SerializeField] private bool _canMove = true;
        [Header("Faction Rules")]
        [SerializeField] private bool _isScience;
        [SerializeField] private int _sciencePowerUpkeep;
        [Header("Slice Effects")]
        [SerializeField] private ResourceSetData _turnStartResourceGain;
        [SerializeField] private int _maxAttacksPerTurn = 1;
        [SerializeField] private bool _canAttackOnSummon;

        public override string SpecialEffectText
        {
            get
            {
                var lines = new List<string>();
                var explicitText = RawSpecialEffectText?.Trim();
                if (!string.IsNullOrWhiteSpace(explicitText))
                {
                    lines.Add(explicitText);
                }

                var powerUpkeepText = GetSciencePowerUpkeepSpecialEffectText();
                if (!string.IsNullOrWhiteSpace(powerUpkeepText) &&
                    !lines.Exists(line => string.Equals(line, powerUpkeepText, StringComparison.Ordinal)))
                {
                    lines.Add(powerUpkeepText);
                }

                return string.Join(Environment.NewLine, lines);
            }
        }

        public override CardDefinition ToDefinition()
        {
            return new UnitCardDefinition(
                cardId: CardId,
                displayName: DisplayName,
                cost: Cost.ToRuntime(),
                attackType: _attackType,
                attack: _attack,
                health: _health,
                canMove: _canMove,
                isScience: _isScience,
                sciencePowerUpkeep: _sciencePowerUpkeep,
                turnStartResourceGain: _turnStartResourceGain.ToRuntime(),
                maxAttacksPerTurn: _maxAttacksPerTurn,
                canAttackOnSummon: _canAttackOnSummon,
                hitsPerAttack: _hitsPerAttack,
                hasBerserker: _hasBerserker,
                hasEndure: _hasEndure,
                hasGuard: _hasGuard);
        }

        public void ConfigureForTests(
            AttackType attackType,
            int attack,
            int health,
            bool canMove,
            bool isScience,
            int sciencePowerUpkeep,
            ResourceSetData turnStartResourceGain,
            int maxAttacksPerTurn,
            bool canAttackOnSummon,
            int hitsPerAttack = 1,
            bool hasBerserker = false,
            bool hasEndure = false,
            bool hasGuard = false)
        {
            _attackType = attackType;
            _attack = attack;
            _health = health;
            _canMove = canMove;
            _isScience = isScience;
            _sciencePowerUpkeep = sciencePowerUpkeep;
            _turnStartResourceGain = turnStartResourceGain;
            _maxAttacksPerTurn = maxAttacksPerTurn;
            _canAttackOnSummon = canAttackOnSummon;
            _hitsPerAttack = hitsPerAttack < 1 ? 1 : hitsPerAttack;
            _hasBerserker = hasBerserker;
            _hasEndure = hasEndure;
            _hasGuard = hasGuard;
        }

        private string GetSciencePowerUpkeepSpecialEffectText()
        {
            return _sciencePowerUpkeep > 0
                ? $"전력 -{_sciencePowerUpkeep}"
                : string.Empty;
        }
    }
}
