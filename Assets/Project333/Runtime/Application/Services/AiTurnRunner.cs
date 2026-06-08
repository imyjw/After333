using System;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Application.Services
{
    public sealed class AiTurnRunner
    {
        private readonly AiDecisionService _aiDecisionService;

        public AiTurnRunner(AiDecisionService aiDecisionService)
        {
            _aiDecisionService = aiDecisionService ?? throw new ArgumentNullException(nameof(aiDecisionService));
        }

        public void RunAiTurn(BattleFlowController battleFlowController, Action<string> logEntryCallback = null)
        {
            if (battleFlowController == null)
            {
                throw new ArgumentNullException(nameof(battleFlowController));
            }

            while (true)
            {
                var battleState = battleFlowController.CurrentBattleState;
                if (battleState == null || battleState.IsEnded || battleState.ActivePlayerId != PlayerId.AI)
                {
                    return;
                }

                if (battleState.Phase == PhaseType.TurnStart)
                {
                    battleFlowController.ResolveTurnStart();
                    logEntryCallback?.Invoke(CombatLogFormatter.FormatTurnStartResolved(PlayerId.AI, battleFlowController.CurrentBattleState.TurnNumber));
                    continue;
                }

                if (battleState.Phase != PhaseType.Main)
                {
                    return;
                }

                var command = _aiDecisionService.GetNextCommand(battleState);
                if (command is EndTurnCommand)
                {
                    logEntryCallback?.Invoke(CombatLogFormatter.FormatEndTurn(PlayerId.AI));
                    battleFlowController.EndTurn();
                    return;
                }

                battleFlowController.ExecuteCommand(PlayerId.AI, command);
                logEntryCallback?.Invoke(CombatLogFormatter.FormatCommand(PlayerId.AI, command));
            }
        }
    }
}
