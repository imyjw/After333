using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Cards;
using UnityEngine;
using UnityEngine.Serialization;

namespace Project333.Runtime.Infrastructure.Data
{
    [CreateAssetMenu(menuName = "Project333/Cards/Unit Card", fileName = "UnitCardDefinition")]
    public sealed class UnitCardDefinitionAsset : CardDefinitionAsset
    {
        [Header("Combat")]
        [SerializeField] private AttackType _attackType = AttackType.Melee;
        [SerializeField] private DamageType _damageType = DamageType.Physical;
        [SerializeField] private int _attack;
        [SerializeField] private int _health = 1;
        [Min(0)]
        [SerializeField] private int _physicalDefense;
        [Min(0)]
        [SerializeField] private int _magicDefense;
        [Min(1)]
        [SerializeField] private int _hitsPerAttack = 1;
        [SerializeField] private bool _hasBerserker;
        [SerializeField] private bool _hasEndure;
        [InspectorName("Has Shielder (쉴더)")]
        [FormerlySerializedAs("_hasGuard")]
        [SerializeField] private bool _hasShielder;
        [SerializeField] private bool _hasLifeSteal;
        [SerializeField] private bool _hasRobot;
        [Header("Board")]
        [SerializeField] private bool _canMove = true;
        [Min(0)]
        [InspectorName("Sealbound Owner Turn Starts (봉인)")]
        [SerializeField] private int _sealboundOwnerTurnStarts;
        [InspectorName("Has Hiding (은신)")]
        [SerializeField] private bool _hasHiding;
        [InspectorName("Has Flying (비행)")]
        [SerializeField] private bool _hasFlying;
        [InspectorName("Has Piercing (관통)")]
        [SerializeField] private bool _hasPiercing;
        [Min(0)]
        [InspectorName("Spell Power (주문력)")]
        [SerializeField] private int _spellPower;
        [InspectorName("Invincible Duration (무적)")]
        [SerializeField] private InvincibleDurationType _invincibleDuration;
        [Min(0)]
        [InspectorName("Invincible Owner Turns (내 n턴)")]
        [SerializeField] private int _invincibleOwnerTurns;
        [Header("Faction Rules")]
        [SerializeField] private bool _isScience;
        [SerializeField] private int _sciencePowerUpkeep;
        [Header("Slice Effects")]
        [SerializeField] private ResourceSetData _turnStartResourceGain;
        [SerializeField] private int _maxAttacksPerTurn = 1;
        [InspectorName("Has Rush (속공)")]
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

                if (_hasRobot && !lines.Exists(ContainsRobotSpecialEffectText))
                {
                    lines.Add("로봇");
                }

                if (_canAttackOnSummon && !lines.Exists(ContainsRushSpecialEffectText))
                {
                    lines.Add("속공");
                }

                if (HasReplicate && !lines.Exists(ContainsReplicateSpecialEffectText))
                {
                    lines.Add("복제");
                }

                if (_sealboundOwnerTurnStarts > 0 && !lines.Exists(ContainsSealboundSpecialEffectText))
                {
                    lines.Add($"봉인 {_sealboundOwnerTurnStarts}");
                }

                if (_hasHiding && !lines.Exists(ContainsHidingSpecialEffectText))
                {
                    lines.Add("은신");
                }

                if (_hasFlying && !lines.Exists(ContainsFlyingSpecialEffectText))
                {
                    lines.Add("비행");
                }

                if (_hasPiercing && !lines.Exists(ContainsPiercingSpecialEffectText))
                {
                    lines.Add("관통");
                }

                if (_spellPower > 0 && !lines.Exists(ContainsSpellPowerSpecialEffectText))
                {
                    lines.Add($"주문력 +{_spellPower}");
                }

                if (_invincibleDuration != InvincibleDurationType.None &&
                    !lines.Exists(ContainsInvincibleSpecialEffectText))
                {
                    lines.Add(GetInvincibleSpecialEffectText());
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
                hasShielder: _hasShielder,
                hasLifeSteal: _hasLifeSteal,
                damageType: _damageType,
                physicalDefense: _physicalDefense,
                magicDefense: _magicDefense,
                hasRobot: _hasRobot,
                hasReplicate: HasReplicate,
                sealboundOwnerTurnStarts: _sealboundOwnerTurnStarts,
                hasHiding: _hasHiding,
                hasFlying: _hasFlying,
                spellPower: _spellPower,
                invincibleDuration: _invincibleDuration,
                invincibleOwnerTurns: _invincibleOwnerTurns,
                hasPiercing: _hasPiercing);
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
            bool hasShielder = false,
            bool hasLifeSteal = false,
            DamageType damageType = DamageType.Physical,
            int physicalDefense = 0,
            int magicDefense = 0,
            bool hasRobot = false,
            int sealboundOwnerTurnStarts = 0,
            bool hasHiding = false,
            bool hasFlying = false,
            int spellPower = 0,
            InvincibleDurationType invincibleDuration = InvincibleDurationType.None,
            int invincibleOwnerTurns = 0,
            bool hasPiercing = false)
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
            _hasShielder = hasShielder;
            _hasLifeSteal = hasLifeSteal;
            _damageType = damageType;
            _physicalDefense = physicalDefense;
            _magicDefense = magicDefense;
            _hasRobot = hasRobot;
            _sealboundOwnerTurnStarts = sealboundOwnerTurnStarts;
            _hasHiding = hasHiding;
            _hasFlying = hasFlying;
            _spellPower = spellPower;
            _invincibleDuration = invincibleDuration;
            _invincibleOwnerTurns = invincibleOwnerTurns;
            _hasPiercing = hasPiercing;
        }

        private string GetSciencePowerUpkeepSpecialEffectText()
        {
            return _sciencePowerUpkeep > 0
                ? $"전력 -{_sciencePowerUpkeep}"
                : string.Empty;
        }

        private static bool ContainsRobotSpecialEffectText(string text)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                   (text.IndexOf("로봇", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf("Robot", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool ContainsRushSpecialEffectText(string text)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                   (text.IndexOf("속공", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf("Rush", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool ContainsReplicateSpecialEffectText(string text)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                   (text.IndexOf("Replicate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.Contains("복제"));
        }

        private static bool ContainsSealboundSpecialEffectText(string text)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                   (text.IndexOf("Sealbound", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.Contains("봉인"));
        }

        private static bool ContainsHidingSpecialEffectText(string text)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                   (text.IndexOf("Hiding", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.Contains("은신"));
        }

        private static bool ContainsFlyingSpecialEffectText(string text)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                   (text.IndexOf("Flying", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.Contains("비행"));
        }

        private static bool ContainsPiercingSpecialEffectText(string text)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                   (text.IndexOf("Piercing", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.Contains("관통"));
        }

        private static bool ContainsSpellPowerSpecialEffectText(string text)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                   (text.IndexOf("SpellPower", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("Spell Power", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.Contains("주문력"));
        }

        private static bool ContainsInvincibleSpecialEffectText(string text)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                   (text.IndexOf("Invincible", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.Contains("무적"));
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
                InvincibleDurationType.GlobalTurnEnds => $"{_invincibleOwnerTurns}번의 턴 종료 동안 무적",
                _ => string.Empty,
            };
        }
    }
}
