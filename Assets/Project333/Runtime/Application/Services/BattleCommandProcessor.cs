using System;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Application.Services
{
    public sealed class BattleCommandProcessor
    {
        private readonly PlayCardService _playCardService;
        private readonly SpellService _spellService;
        private readonly MoveService _moveService;
        private readonly AttackService _attackService;
        private readonly EndTurnService _endTurnService;

        public BattleCommandProcessor(
            PlayCardService playCardService,
            SpellService spellService,
            MoveService moveService,
            AttackService attackService,
            EndTurnService endTurnService)
        {
            _playCardService = playCardService ?? throw new ArgumentNullException(nameof(playCardService));
            _spellService = spellService ?? throw new ArgumentNullException(nameof(spellService));
            _moveService = moveService ?? throw new ArgumentNullException(nameof(moveService));
            _attackService = attackService ?? throw new ArgumentNullException(nameof(attackService));
            _endTurnService = endTurnService ?? throw new ArgumentNullException(nameof(endTurnService));
        }

        public void Execute(BattleState battleState, PlayerId actorId, IBattleCommand command)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (battleState.IsEnded)
            {
                throw new InvalidOperationException("Commands cannot be executed after the battle has ended.");
            }

            if (battleState.ActivePlayerId != actorId)
            {
                throw new InvalidOperationException("Only the active player can execute commands.");
            }

            battleState.ClearValuePopupEvents();

            switch (command)
            {
                case PlayUnitCardCommand playUnitCardCommand:
                    _playCardService.PlayUnitCard(
                        battleState,
                        actorId,
                        playUnitCardCommand.CardId,
                        playUnitCardCommand.TargetCoord);
                    break;

                case PlayBuildingCardCommand playBuildingCardCommand:
                    _playCardService.PlayBuildingCard(
                        battleState,
                        actorId,
                        playBuildingCardCommand.CardId,
                        playBuildingCardCommand.TargetCoord);
                    break;

                case CastDamageSpellCommand castDamageSpellCommand:
                    _spellService.CastDamageSpell(
                        battleState,
                        actorId,
                        castDamageSpellCommand.CardId,
                        castDamageSpellCommand.TargetOwnerId,
                        castDamageSpellCommand.TargetCoord);
                    break;

                case CastPersistentResourceSpellCommand castPersistentResourceSpellCommand:
                    _spellService.CastPersistentResourceSpell(
                        battleState,
                        actorId,
                        castPersistentResourceSpellCommand.CardId);
                    break;

                case CastScriptedSpellCommand castScriptedSpellCommand:
                    if (castScriptedSpellCommand.HasTarget)
                    {
                        _spellService.CastScriptedSpell(
                            battleState,
                            actorId,
                            castScriptedSpellCommand.CardId,
                            castScriptedSpellCommand.TargetOwnerId,
                            castScriptedSpellCommand.TargetCoord);
                    }
                    else
                    {
                        _spellService.CastScriptedSpell(
                            battleState,
                            actorId,
                            castScriptedSpellCommand.CardId);
                    }

                    break;

                case MoveOccupantCommand moveOccupantCommand:
                    EnsureMainPhase(battleState);
                    _moveService.Move(
                        battleState,
                        actorId,
                        moveOccupantCommand.From,
                        moveOccupantCommand.To);
                    break;

                case AttackCommand attackCommand:
                    _attackService.Attack(
                        battleState,
                        actorId,
                        attackCommand.AttackerCoord,
                        attackCommand.TargetCoord);
                    break;

                case EndTurnCommand:
                    _endTurnService.EndTurn(battleState);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported battle command type '{command.GetType().Name}'.");
            }
        }

        private static void EnsureMainPhase(BattleState battleState)
        {
            if (battleState.Phase != PhaseType.Main)
            {
                throw new InvalidOperationException("This command can only be executed during the main phase.");
            }
        }
    }
}
