using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    internal static class BattleValuePopupRecorder
    {
        public static void RecordDamage(BattleState battleState, OccupantState occupant, int actualDamage)
        {
            if (battleState == null || occupant == null || actualDamage <= 0)
            {
                return;
            }

            battleState.ValuePopupEvents.Add(new BattleValuePopupEvent(
                occupant.RuntimeId,
                occupant.OwnerId,
                occupant.Position,
                isHealing: false,
                amount: actualDamage));
        }

        public static int HealAndRecord(BattleState battleState, OccupantState occupant, int requestedAmount)
        {
            if (battleState == null || occupant == null || requestedAmount <= 0)
            {
                return 0;
            }

            var previousHp = occupant.CurrentHp;
            occupant.Heal(requestedAmount);
            var actualHealedAmount = occupant.CurrentHp - previousHp;
            if (actualHealedAmount > 0)
            {
                battleState.ValuePopupEvents.Add(new BattleValuePopupEvent(
                    occupant.RuntimeId,
                    occupant.OwnerId,
                    occupant.Position,
                    isHealing: true,
                    amount: actualHealedAmount));
            }

            return actualHealedAmount;
        }
    }
}
