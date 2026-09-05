using System;
using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Application.Services
{
    public static class BattleOutcomeResolver
    {
        public static void ResolveMasterDefeat(BattleState battleState)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (battleState.IsEnded)
            {
                return;
            }

            var playerMasterHp = battleState.Player.Master.CurrentHp;
            var aiMasterHp = battleState.AI.Master.CurrentHp;
            var playerMasterDefeated = playerMasterHp <= 0;
            var aiMasterDefeated = aiMasterHp <= 0;
            if (!playerMasterDefeated && !aiMasterDefeated)
            {
                return;
            }

            if (playerMasterDefeated && aiMasterDefeated)
            {
                if (playerMasterHp == aiMasterHp)
                {
                    battleState.EndBattleAsDraw();
                    return;
                }

                battleState.EndBattle(playerMasterHp > aiMasterHp
                    ? PlayerId.Player
                    : PlayerId.AI);
                return;
            }

            battleState.EndBattle(playerMasterDefeated ? PlayerId.AI : PlayerId.Player);
        }
    }
}
