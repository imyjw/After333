#nullable enable
using System;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Services
{
    internal sealed class AiBattleSimulation
    {
        private readonly ICardDefinitionProvider _provider;
        private readonly ICardUpgradeLevelProvider _levels;
        public AiBattleSimulation(ICardDefinitionProvider provider, ICardUpgradeLevelProvider levels)
        { _provider = provider; _levels = levels; }

        public BattleState? Apply(BattleState source, AiAction action, int sample)
        {
            var copy = AiBattleStateCopy.Create(source);
            // Independent scenario RNG. Never consume or clone the authoritative battle's random stream.
            var random = ScenarioRandom(source, sample);
            var processor = new BattleCommandProcessor(
                new PlayCardService(_provider, new SummonService(), _levels),
                new SpellService(_provider, _levels), new MoveService(),
                new AttackService(new TargetingService(), random.Next), new EndTurnService(random));
            try
            {
                foreach (var command in action.Commands)
                {
                    processor.Execute(copy, PlayerId.AI, RemapScenarioHandCard(copy, command, action.HandIndex));
                    if (copy.IsEnded) break;
                }
                if (!copy.IsEnded && copy.Phase == PhaseType.TurnStart)
                    new TurnStartService(new ScienceUpkeepService(), new RobotFactoryService(_provider, random)).ResolveTurnStart(copy);
                return copy;
            }
            catch (Exception error) when (error is InvalidOperationException || error is ArgumentException)
            {
                // The shared rules, not the candidate generator, are the final legality authority.
                return null;
            }
        }

        public bool HasOpponentTurnEndDamageRisk(BattleState state)
        {
            if (state.IsEnded || state.ActivePlayerId != PlayerId.Player || state.Phase != PhaseType.Main) return false;
            // Check after upkeep and turn-start hazards: a previously drained dragon can reactivate,
            // while a dragon removed by that transition can no longer trigger its end-turn effect.
            foreach (var occupant in state.PlayerBoard.EnumerateOccupants())
                if (occupant.CardId == "RedDragon" && occupant.IsAlive && !occupant.EffectsSuppressed) return true;
            return false;
        }

        public BattleState ProjectOpponentTurnEnd(BattleState source, int sample)
        {
            var copy = AiBattleStateCopy.Create(source);
            // Resolve public automatic effects with the real rules, including defenses and destruction chains.
            // The opponent's unknown main-phase actions are absent; this is a survival forecast only.
            new EndTurnService(ScenarioRandom(source, sample)).EndTurn(copy);
            // Stop before our next draw/resource step, so hypothetical extra income cannot fund a play.
            return copy;
        }

        private static Random ScenarioRandom(BattleState source, int sample) =>
            new Random(7919 + sample * 104729 + source.TurnNumber * 31);
        private static IBattleCommand RemapScenarioHandCard(BattleState state, IBattleCommand command, int handIndex)
        {
            if (handIndex < 0 || command is not IHandCardCommand hand || string.IsNullOrEmpty(hand.HandCardRuntimeId) ||
                state.AI.Hand.Contains(hand.CardId, hand.HandCardRuntimeId)) return command;
            // A newly replicated card has a different GUID in each isolated random scenario.
            if (handIndex >= state.AI.Hand.Count || state.AI.Hand.Cards[handIndex].CardId != hand.CardId)
                throw new InvalidOperationException("The planned hand card is unavailable in this scenario.");
            var id = state.AI.Hand.Cards[handIndex].RuntimeId;
            return command switch
            {
                PlayUnitCardCommand c => new PlayUnitCardCommand(c.CardId, c.TargetCoord, id),
                PlayBuildingCardCommand c => new PlayBuildingCardCommand(c.CardId, c.TargetCoord, id),
                CastDamageSpellCommand c => new CastDamageSpellCommand(c.CardId, c.TargetOwnerId, c.TargetCoord, id),
                CastPersistentResourceSpellCommand c => new CastPersistentResourceSpellCommand(c.CardId, id),
                CastScriptedSpellCommand c when c.HasTarget => new CastScriptedSpellCommand(c.CardId, c.TargetOwnerId, c.TargetCoord, id),
                CastScriptedSpellCommand c => new CastScriptedSpellCommand(c.CardId, id),
                _ => command
            };
        }
    }
}
