using System;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Services
{
    public sealed class BattleCommandProcessor
    {
        private readonly PlayCardService _playCardService;
        private readonly SpellService _spellService;
        private readonly MoveService _moveService;
        private readonly AttackService _attackService;
        private readonly EndTurnService _endTurnService;
        private readonly ReplicateService _replicateService;

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
            _replicateService = new ReplicateService(_playCardService.CardDefinitionProvider);
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

            var handCardCommand = command as IHandCardCommand;
            var actorHand = battleState.GetPlayer(actorId).Hand;
            var cardSelectedForPlay = handCardCommand == null
                ? null
                : actorHand.GetCardForPlay(
                    handCardCommand.CardId,
                    handCardCommand.HandCardRuntimeId);

            var pendingRobotFusion = battleState.PendingRobotFusion;
            var resolvesPendingRobotFusion = command is CastScriptedSpellCommand pendingFusionCommand &&
                                             pendingFusionCommand.HasMultipleTargets &&
                                             string.Equals(
                                                 pendingFusionCommand.CardId,
                                                 pendingRobotFusion?.CardId,
                                                 StringComparison.Ordinal);
            if (pendingRobotFusion != null &&
                command is not EndTurnCommand &&
                !resolvesPendingRobotFusion)
            {
                throw new InvalidOperationException(
                    "Complete the pending Robot Fusion selection before performing another action.");
            }

            switch (command)
            {
                case PlayUnitCardCommand playUnitCardCommand:
                    _playCardService.PlayUnitCard(
                        battleState,
                        actorId,
                        playUnitCardCommand.CardId,
                        playUnitCardCommand.TargetCoord,
                        playUnitCardCommand.HandCardRuntimeId);
                    break;

                case PlayBuildingCardCommand playBuildingCardCommand:
                    _playCardService.PlayBuildingCard(
                        battleState,
                        actorId,
                        playBuildingCardCommand.CardId,
                        playBuildingCardCommand.TargetCoord,
                        playBuildingCardCommand.HandCardRuntimeId);
                    break;

                case CastDamageSpellCommand castDamageSpellCommand:
                    _spellService.CastDamageSpell(
                        battleState,
                        actorId,
                        castDamageSpellCommand.CardId,
                        castDamageSpellCommand.TargetOwnerId,
                        castDamageSpellCommand.TargetCoord,
                        castDamageSpellCommand.HandCardRuntimeId);
                    break;

                case CastPersistentResourceSpellCommand castPersistentResourceSpellCommand:
                    _spellService.CastPersistentResourceSpell(
                        battleState,
                        actorId,
                        castPersistentResourceSpellCommand.CardId,
                        castPersistentResourceSpellCommand.HandCardRuntimeId);
                    break;

                case CastScriptedSpellCommand castScriptedSpellCommand:
                    if (castScriptedSpellCommand.HasMultipleTargets)
                    {
                        _spellService.CastScriptedSpell(
                            battleState,
                            actorId,
                            castScriptedSpellCommand.CardId,
                            castScriptedSpellCommand.TargetCoords);
                    }
                    else if (castScriptedSpellCommand.HasTarget)
                    {
                        _spellService.CastScriptedSpell(
                            battleState,
                            actorId,
                            castScriptedSpellCommand.CardId,
                            castScriptedSpellCommand.TargetOwnerId,
                            castScriptedSpellCommand.TargetCoord,
                            castScriptedSpellCommand.HandCardRuntimeId);
                    }
                    else
                    {
                        _spellService.CastScriptedSpell(
                            battleState,
                            actorId,
                            castScriptedSpellCommand.CardId,
                            castScriptedSpellCommand.HandCardRuntimeId);
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
                    battleState.CancelPendingRobotFusion();
                    _endTurnService.EndTurn(battleState);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported battle command type '{command.GetType().Name}'.");
            }

            if (handCardCommand != null &&
                cardSelectedForPlay != null &&
                !actorHand.Contains(cardSelectedForPlay.CardId, cardSelectedForPlay.RuntimeId))
            {
                _replicateService.TryCreateTemporaryCopy(
                    battleState,
                    actorId,
                    handCardCommand.CardId);
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

    public sealed class ReplicateService
    {
        private readonly ICardDefinitionProvider _cardDefinitionProvider;

        public ReplicateService(ICardDefinitionProvider cardDefinitionProvider)
        {
            _cardDefinitionProvider = cardDefinitionProvider ??
                                      throw new ArgumentNullException(nameof(cardDefinitionProvider));
        }

        public HandCardState TryCreateTemporaryCopy(
            BattleState battleState,
            PlayerId ownerId,
            string cardId)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            var definition = _cardDefinitionProvider.GetRequired(cardId);
            if (!definition.HasReplicate)
            {
                return null;
            }

            var owner = battleState.GetPlayer(ownerId);
            if (owner.Hand.Count >= owner.MaxHandSize)
            {
                return null;
            }

            return owner.Hand.AddTemporaryReplicate(definition.CardId);
        }
    }
}
