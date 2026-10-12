using System;

namespace Project333.Runtime.Domain.Cards
{
    public static class OrcChieftainRules
    {
        public const string CardId = "OrcChieftain";
        public const string DisplayName = "오크 족장";
        public const int BaseAttack = 50;
        public const int BaseHealth = 70;
        public const int ManaCost = 7;
        public const int AuraAttackBonus = 10;
        public const int AuraHealthBonus = 10;

        public static bool IsActiveSource(OccupantState occupant) =>
            occupant is UnitState && occupant.Kind == OccupantKind.Unit &&
            !string.IsNullOrWhiteSpace(occupant.RuntimeId) &&
            string.Equals(occupant.CardId, CardId, StringComparison.Ordinal) &&
            occupant.IsAlive && !occupant.EffectsSuppressed;

        public static bool IsRecipient(OccupantState occupant) =>
            occupant is UnitState unit && occupant.Kind == OccupantKind.Unit && unit.HasRace(UnitRace.Orc);

        internal static bool IsAura(CombatStatBuffState buff) =>
            buff != null && string.Equals(buff.SourceCardId, CardId, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(buff.SourceRuntimeId);
    }
}
