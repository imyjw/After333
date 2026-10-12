using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Domain.Cards
{
    public static class ManaCannonRules
    {
        public const string CardId = "ManaCannon";
        public const int BaseAttack = 40;
        public const int BaseHealth = 10;
        public const int ManaCost = 2;
        public const int AttackManaCost = 1;
        public const string EffectDescription = "공격할 때마다 마나 1 소모. 부족한 마나는 골드로 대신 지불할 수 있습니다.";

        public static bool RequiresAttackPayment(OccupantState attacker)
        {
            return attacker != null && attacker.Kind == OccupantKind.Unit &&
                string.Equals(attacker.CardId, CardId, StringComparison.Ordinal) && !attacker.EffectsSuppressed;
        }

        public static bool CanPayAttackCost(BattleState state, OccupantState attacker)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return !RequiresAttackPayment(attacker) ||
                state.GetPlayer(attacker.OwnerId).Resources.CanAfford(new ResourceSet(AttackManaCost, 0, 0, 0));
        }

        public static void PayAttackCost(BattleState state, OccupantState attacker)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!RequiresAttackPayment(attacker)) return;
            var resources = state.GetPlayer(attacker.OwnerId).Resources;
            var cost = new ResourceSet(AttackManaCost, 0, 0, 0);
            if (!resources.CanAfford(cost)) throw new InvalidOperationException("자원이 부족합니다.");
            resources.Spend(cost);
        }
    }
}
