using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Presentation.Battle
{
    public static class BattleOutcomeFormatter
    {
        public static string Format(BattleState battleState)
        {
            if (battleState == null || !battleState.IsEnded)
            {
                return string.Empty;
            }

            if (battleState.Result.IsDraw)
            {
                return "Draw";
            }

            return battleState.Result.Winner == PlayerId.Player
                ? "Victory"
                : "Defeat";
        }
    }
}
