using System;
using System.Collections.Generic;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Services
{
    internal sealed class AiAction
    {
        public readonly IBattleCommand[] Commands;
        public readonly int Priority;
        public int HandIndex = -1;
        public AiAction(int priority, params IBattleCommand[] commands) { Priority = priority; Commands = commands; }
    }

    internal sealed class AiActionGenerator
    {
        internal static readonly TileCoord[] Coordinates = {
            new TileCoord(2,1), new TileCoord(2,0), new TileCoord(1,1), new TileCoord(1,0),
            new TileCoord(3,1), new TileCoord(3,0), new TileCoord(0,1), new TileCoord(0,0),
            new TileCoord(4,1), new TileCoord(4,0)
        };
        private readonly Dictionary<string, CardDefinition> _cards = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
        private readonly TargetingService _targeting;
        public AiActionGenerator(ICardDefinitionProvider provider, TargetingService targeting)
        {
            _targeting = targeting;
            foreach (var card in provider.GetAll()) _cards[card.CardId] = card;
        }

        public List<AiAction> Generate(BattleState state)
        {
            var actions = new List<AiAction>();
            if (state.IsEnded || state.ActivePlayerId != PlayerId.AI || state.Phase != PhaseType.Main) return actions;
            if (state.PendingRobotFusion != null)
            {
                AddFusion(actions, state, state.PendingRobotFusion.CardId, string.Empty, false);
                actions.Add(new AiAction(-1, new EndTurnCommand()));
                return actions;
            }
            foreach (var source in Coordinates)
            {
                var unit = state.AIBoard.GetOccupant(source);
                if (unit == null || !unit.IsAlive || unit.CannotAttack || unit.HasSummoningSickness ||
                    unit.RemainingAttacksThisTurn <= 0 || unit.Attack <= 0) continue;
                foreach (var target in Coordinates)
                    if (_targeting.CanTarget(state, PlayerId.AI, source, target))
                        actions.Add(new AiAction(100, new AttackCommand(source, target)));
            }
            // Round-robin placements/targets prevents one hand card from consuming the entire search budget.
            var handGroups = new List<List<AiAction>>();
            var seenCards = new HashSet<string>();
            for (var handIndex = 0; handIndex < state.AI.Hand.Count; handIndex++)
            {
                var hand = state.AI.Hand.Cards[handIndex];
                if (!_cards.TryGetValue(hand.CardId, out var definition) ||
                    !state.AI.Resources.CanAfford(definition.Cost) ||
                    !seenCards.Add(hand.CardId + ":" + hand.IsTemporaryReplicate)) continue;
                var group = new List<AiAction>();
                foreach (var coord in Coordinates)
                {
                    if (state.AIBoard.GetOccupant(coord) != null) continue;
                    if (definition is UnitCardDefinition)
                        group.Add(new AiAction(70, new PlayUnitCardCommand(hand.CardId, coord, hand.RuntimeId)));
                    else if (definition is BuildingCardDefinition)
                        group.Add(new AiAction(70, new PlayBuildingCardCommand(hand.CardId, coord, hand.RuntimeId)));
                }
                if (definition is DamageSpellCardDefinition)
                {
                    foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
                        foreach (var coord in Coordinates)
                            if (state.GetBoard(owner).GetOccupant(coord) is OccupantState o && o.CanBeAffected)
                                group.Add(new AiAction(90, new CastDamageSpellCommand(hand.CardId, owner, coord, hand.RuntimeId)));
                }
                else if (definition is PersistentResourceSpellCardDefinition)
                    group.Add(new AiAction(80, new CastPersistentResourceSpellCommand(hand.CardId, hand.RuntimeId)));
                else if (definition is ScriptedSpellCardDefinition script)
                    AddScript(group, state, script, hand.RuntimeId);
                foreach (var action in group) action.HandIndex = handIndex;
                handGroups.Add(group);
            }
            for (var index = 0; index < 100; index++)
            {
                var added = false;
                foreach (var group in handGroups)
                    if (index < group.Count) { actions.Add(group[index]); added = true; }
                if (!added) break;
            }
            foreach (var source in Coordinates)
            {
                var unit = state.AIBoard.GetOccupant(source);
                if (unit == null || !unit.IsAlive || !unit.CanMove || unit.CannotMoveDueToState) continue;
                foreach (var target in Coordinates)
                {
                    if (source == target) continue;
                    var occupant = state.AIBoard.GetOccupant(target);
                    if (occupant != null && (!occupant.CanMove || !occupant.IsAlive || occupant.CannotMoveDueToState)) continue;
                    var move = new MoveOccupantCommand(source, target);
                    var escapesHazard = AiMovementPolicy.IsHazardEscapeCandidate(state, move);
                    if (AiMovementPolicy.Score(state, move) <= 0 && !escapesHazard) continue;
                    // Inspect threatened occupants before ordinary plays exhaust the search budget.
                    actions.Add(new AiAction(escapesHazard ? 96 : state.AI.Master.CurrentHp < 40 ? 95 : 20,
                        move));
                }
            }
            actions.Add(new AiAction(-1, new EndTurnCommand()));
            // Stable ordering is part of reproducibility; never sort on generated runtime GUIDs.
            var indexed = new List<KeyValuePair<int, AiAction>>();
            for (var i = 0; i < actions.Count; i++) indexed.Add(new KeyValuePair<int, AiAction>(i, actions[i]));
            indexed.Sort((a, b) => a.Value.Priority == b.Value.Priority ? a.Key.CompareTo(b.Key) : b.Value.Priority.CompareTo(a.Value.Priority));
            actions.Clear();
            foreach (var item in indexed) actions.Add(item.Value);
            PrioritizeTacticalTargets(state, actions);
            return actions;
        }

        private static void PrioritizeTacticalTargets(BattleState state, List<AiAction> actions)
        {
            // Inspect master damage and destruction chains before ordinary actions consume the
            // search budget. Interleave attacks and spells so neither can crowd the other out.
            // This is search order only; the shared simulation still decides whether an action helps.
            var groups = new[]
            {
                new List<AiAction>(), new List<AiAction>(),
                new List<AiAction>(), new List<AiAction>()
            };
            var ordinary = new List<AiAction>();
            foreach (var action in actions)
            {
                var group = TacticalTargetGroup(state, action.Commands[0]);
                if (group >= 0) groups[group].Add(action);
                else ordinary.Add(action);
            }
            actions.Clear();
            for (var index = 0; ; index++)
            {
                var added = false;
                foreach (var group in groups)
                    if (index < group.Count) { actions.Add(group[index]); added = true; }
                if (!added) break;
            }
            actions.AddRange(ordinary);
        }

        private static int TacticalTargetGroup(BattleState state, IBattleCommand command)
        {
            if (command is AttackCommand attack)
            {
                var target = state.PlayerBoard.GetOccupant(attack.TargetCoord);
                if (target is MasterState) return 0;
                if (OccupantDestructionService.HasActiveDestructionEffect(target)) return 1;
            }
            else if (command is CastDamageSpellCommand spell)
            {
                var target = state.GetBoard(spell.TargetOwnerId).GetOccupant(spell.TargetCoord);
                if (spell.TargetOwnerId == PlayerId.Player && target is MasterState) return 2;
                if (OccupantDestructionService.HasActiveDestructionEffect(target)) return 3;
            }
            return -1;
        }

        private static void AddScript(List<AiAction> actions, BattleState state, ScriptedSpellCardDefinition card, string runtimeId)
        {
            if (card.EffectId == RobotFusionRules.EffectId)
            {
                if (RobotFusionRules.CanBegin(state, PlayerId.AI)) AddFusion(actions, state, card.CardId, runtimeId, true);
            }
            else if (card.EffectId == "firewall")
            {
                foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
                    for (var row = 0; row < 2; row++)
                        actions.Add(new AiAction(85, new CastScriptedSpellCommand(card.CardId, owner, new TileCoord(0, row), runtimeId)));
            }
            else if (card.EffectId == BiochemicalBombRules.EffectId)
            {
                foreach (var column in new[] { 0, 1 })
                    actions.Add(new AiAction(85, new CastScriptedSpellCommand(card.CardId, PlayerId.Player, new TileCoord(column, 0), runtimeId)));
            }
            else if (card.EffectId == GuRules.EffectId || card.EffectId == HuanShuRules.EffectId || card.EffectId == "cheonra_jimang")
            {
                foreach (var coord in Coordinates)
                    if (state.PlayerBoard.GetOccupant(coord) != null)
                        actions.Add(new AiAction(90, new CastScriptedSpellCommand(card.CardId, PlayerId.Player, coord, runtimeId)));
            }
            else
                actions.Add(new AiAction(80, new CastScriptedSpellCommand(card.CardId, runtimeId)));
        }

        private static void AddFusion(List<AiAction> actions, BattleState state, string cardId, string runtimeId, bool begin)
        {
            var robots = new List<TileCoord>();
            foreach (var coord in Coordinates)
                if (state.AIBoard.GetOccupant(coord) is UnitState { HasActiveRobot: true, IsAlive: true }) robots.Add(coord);
            // Ordered pairs cover equal-upkeep survivor choices; whole groups cover larger fusions without factorial search.
            foreach (var first in robots)
            {
                foreach (var second in robots)
                    if (first != second) Add(new[] { first, second });
                if (robots.Count > 2)
                {
                    var all = new List<TileCoord> { first };
                    foreach (var other in robots) if (other != first) all.Add(other);
                    Add(all);
                }
            }
            void Add(IReadOnlyList<TileCoord> coordinates)
            {
                var finish = new CastScriptedSpellCommand(cardId, coordinates);
                actions.Add(begin ? new AiAction(85, new CastScriptedSpellCommand(cardId, runtimeId), finish) : new AiAction(85, finish));
            }
        }
    }
}
