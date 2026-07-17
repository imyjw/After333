using System;
using Project333.Runtime.Domain.Cards;
using UnityEngine;

namespace Project333.Runtime.Infrastructure.Data
{
    [CreateAssetMenu(menuName = "Project333/Cards/Building Card", fileName = "BuildingCardDefinition")]
    public sealed class BuildingCardDefinitionAsset : CardDefinitionAsset
    {
        [Header("Combat")]
        [SerializeField] private bool _canAttack;
        [SerializeField] private DamageType _damageType = DamageType.Physical;
        [SerializeField] private int _attack;
        [SerializeField] private int _health = 1;
        [Min(0)]
        [SerializeField] private int _physicalDefense;
        [Min(0)]
        [SerializeField] private int _magicDefense;
        [Header("Slice Effects")]
        [SerializeField] private ResourceSetData _turnStartResourceGain;
        [Min(0)]
        [SerializeField] private int _sciencePowerUpkeep;
        [SerializeField] private bool _canAttackOnSummon;
        [Min(0)]
        [InspectorName("Sealbound Owner Turn Starts (봉인)")]
        [SerializeField] private int _sealboundOwnerTurnStarts;
        [InspectorName("Has Flying (비행)")]
        [SerializeField] private bool _hasFlying;
        [Min(0)]
        [InspectorName("Spell Power (주문력)")]
        [SerializeField] private int _spellPower;
        [InspectorName("Invincible Duration (무적)")]
        [SerializeField] private InvincibleDurationType _invincibleDuration;
        [Min(0)]
        [InspectorName("Invincible Owner Turns (내 n턴)")]
        [SerializeField] private int _invincibleOwnerTurns;

        public override string SpecialEffectText
        {
            get
            {
                var text = base.SpecialEffectText;
                if (_sealboundOwnerTurnStarts > 0 &&
                    text.IndexOf("Sealbound", StringComparison.OrdinalIgnoreCase) < 0 &&
                    !text.Contains("봉인"))
                {
                    text = string.IsNullOrWhiteSpace(text)
                        ? $"봉인 {_sealboundOwnerTurnStarts}"
                        : $"{text}{Environment.NewLine}봉인 {_sealboundOwnerTurnStarts}";
                }

                if (_hasFlying &&
                    text.IndexOf("Flying", StringComparison.OrdinalIgnoreCase) < 0 &&
                    !text.Contains("비행"))
                {
                    text = string.IsNullOrWhiteSpace(text)
                        ? "비행"
                        : $"{text}{Environment.NewLine}비행";
                }

                if (_spellPower > 0 &&
                    text.IndexOf("SpellPower", StringComparison.OrdinalIgnoreCase) < 0 &&
                    text.IndexOf("Spell Power", StringComparison.OrdinalIgnoreCase) < 0 &&
                    !text.Contains("주문력"))
                {
                    text = string.IsNullOrWhiteSpace(text)
                        ? $"주문력 +{_spellPower}"
                        : $"{text}{Environment.NewLine}주문력 +{_spellPower}";
                }

                if (_invincibleDuration != InvincibleDurationType.None &&
                    text.IndexOf("Invincible", StringComparison.OrdinalIgnoreCase) < 0 &&
                    !text.Contains("무적"))
                {
                    var invincibleText = GetInvincibleSpecialEffectText();
                    text = string.IsNullOrWhiteSpace(text)
                        ? invincibleText
                        : $"{text}{Environment.NewLine}{invincibleText}";
                }

                return text;
            }
        }

        public override CardDefinition ToDefinition()
        {
            return new BuildingCardDefinition(
                cardId: CardId,
                displayName: DisplayName,
                cost: Cost.ToRuntime(),
                canAttack: _canAttack,
                attack: _attack,
                health: _health,
                turnStartResourceGain: _turnStartResourceGain.ToRuntime(),
                canAttackOnSummon: _canAttackOnSummon,
                damageType: _damageType,
                physicalDefense: _physicalDefense,
                magicDefense: _magicDefense,
                sciencePowerUpkeep: _sciencePowerUpkeep,
                hasReplicate: HasReplicate,
                sealboundOwnerTurnStarts: _sealboundOwnerTurnStarts,
                hasFlying: _hasFlying,
                spellPower: _spellPower,
                invincibleDuration: _invincibleDuration,
                invincibleOwnerTurns: _invincibleOwnerTurns);
        }

        public void ConfigureForTests(
            bool canAttack,
            int attack,
            int health,
            ResourceSetData turnStartResourceGain,
            bool canAttackOnSummon,
            DamageType damageType = DamageType.Physical,
            int physicalDefense = 0,
            int magicDefense = 0,
            int sciencePowerUpkeep = 0,
            int sealboundOwnerTurnStarts = 0,
            bool hasFlying = false,
            int spellPower = 0,
            InvincibleDurationType invincibleDuration = InvincibleDurationType.None,
            int invincibleOwnerTurns = 0)
        {
            _canAttack = canAttack;
            _attack = attack;
            _health = health;
            _turnStartResourceGain = turnStartResourceGain;
            _canAttackOnSummon = canAttackOnSummon;
            _damageType = damageType;
            _physicalDefense = physicalDefense;
            _magicDefense = magicDefense;
            _sciencePowerUpkeep = sciencePowerUpkeep;
            _sealboundOwnerTurnStarts = sealboundOwnerTurnStarts;
            _hasFlying = hasFlying;
            _spellPower = spellPower;
            _invincibleDuration = invincibleDuration;
            _invincibleOwnerTurns = invincibleOwnerTurns;
        }

        private string GetInvincibleSpecialEffectText()
        {
            return _invincibleDuration switch
            {
                InvincibleDurationType.Always => "무적",
                InvincibleDurationType.SummonTurn => "소환된 턴 동안 무적",
                InvincibleDurationType.OwnerTurnOnly => "내 턴에만 무적",
                InvincibleDurationType.OpponentTurnOnly => "상대 턴에만 무적",
                InvincibleDurationType.UntilTurnEnd => "턴 종료까지 무적",
                InvincibleDurationType.OwnerTurns => $"내 {_invincibleOwnerTurns}턴 동안 무적",
                _ => string.Empty,
            };
        }
    }
}
