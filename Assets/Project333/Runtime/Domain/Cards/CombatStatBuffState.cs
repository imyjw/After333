using System;

namespace Project333.Runtime.Domain.Cards
{
    /// <summary>Attribution for applied combat stat bonuses; the occupant stats remain authoritative.</summary>
    public sealed class CombatStatBuffState
    {
        public CombatStatBuffState(
            string sourceCardId,
            int attackBonus = 0,
            int maxHpBonus = 0,
            int physicalDefenseBonus = 0,
            int magicDefenseBonus = 0,
            string sourceRuntimeId = "")
        {
            if (string.IsNullOrWhiteSpace(sourceCardId))
                throw new ArgumentException("A combat stat buff requires its source CardId.", nameof(sourceCardId));
            if (attackBonus < 0) throw new ArgumentOutOfRangeException(nameof(attackBonus));
            if (maxHpBonus < 0) throw new ArgumentOutOfRangeException(nameof(maxHpBonus));
            if (physicalDefenseBonus < 0) throw new ArgumentOutOfRangeException(nameof(physicalDefenseBonus));
            if (magicDefenseBonus < 0) throw new ArgumentOutOfRangeException(nameof(magicDefenseBonus));
            SourceCardId = sourceCardId;
            SourceRuntimeId = sourceRuntimeId ?? string.Empty;
            AttackBonus = attackBonus;
            MaxHpBonus = maxHpBonus;
            PhysicalDefenseBonus = physicalDefenseBonus;
            MagicDefenseBonus = magicDefenseBonus;
        }

        public string SourceCardId { get; }
        // Empty for permanent gains; a live source's identity distinguishes stacked auras.
        public string SourceRuntimeId { get; }
        public int AttackBonus { get; }
        public int MaxHpBonus { get; }
        public int PhysicalDefenseBonus { get; }
        public int MagicDefenseBonus { get; }

        public CombatStatBuffState Clone() => new CombatStatBuffState(
            SourceCardId, AttackBonus, MaxHpBonus, PhysicalDefenseBonus, MagicDefenseBonus, SourceRuntimeId);
    }
}
