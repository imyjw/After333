using System;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Gateways;
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

        public void RunAiTurn(BattleFlowController battleFlowController, Action<string> logEntryCallback = null,
            Func<string, string> cardNameResolver = null)
        {
            if (battleFlowController == null)
            {
                throw new ArgumentNullException(nameof(battleFlowController));
            }

            RunAiTurnCore(
                () => battleFlowController.CurrentBattleState,
                battleFlowController.ResolveTurnStart,
                battleFlowController.EndTurn,
                battleFlowController.ExecuteCommand,
                logEntryCallback,
                cardNameResolver);
        }

        public void RunAiTurn(IBattleGateway battleGateway, Action<string> logEntryCallback = null,
            Func<string, string> cardNameResolver = null)
        {
            if (battleGateway == null)
            {
                throw new ArgumentNullException(nameof(battleGateway));
            }

            RunAiTurnCore(
                () => battleGateway.CurrentBattleState,
                battleGateway.ResolveTurnStart,
                battleGateway.EndTurn,
                battleGateway.ExecuteCommand,
                logEntryCallback,
                cardNameResolver);
        }

        private void RunAiTurnCore(
            Func<BattleState> getBattleState,
            Action resolveTurnStart,
            Action endTurn,
            Action<PlayerId, IBattleCommand> executeCommand,
            Action<string> logEntryCallback,
            Func<string, string> cardNameResolver)
        {
            var actionCount = 0;
            while (true)
            {
                var battleState = getBattleState();
                if (battleState == null || battleState.IsEnded || battleState.ActivePlayerId != PlayerId.AI)
                {
                    return;
                }

                if (battleState.Phase == PhaseType.TurnStart)
                {
                    resolveTurnStart();
                    var resolvedBattleState = getBattleState();
                    if (resolvedBattleState != null)
                    {
                        logEntryCallback?.Invoke(CombatLogFormatter.FormatTurnStartResolved(PlayerId.AI, resolvedBattleState.TurnNumber));
                    }

                    continue;
                }

                if (battleState.Phase != PhaseType.Main)
                {
                    return;
                }

                var command = ++actionCount >= 64 ? new EndTurnCommand() : _aiDecisionService.GetNextCommand(battleState);
                if (command is EndTurnCommand)
                {
                    logEntryCallback?.Invoke(CombatLogFormatter.FormatEndTurn(PlayerId.AI));
                    endTurn();
                    return;
                }

                var commandLog = logEntryCallback == null ? null :
                    CombatLogFormatter.FormatCommand(PlayerId.AI, command, battleState, cardNameResolver);
                executeCommand(PlayerId.AI, command);
                logEntryCallback?.Invoke(commandLog);
            }
        }
    }
}
