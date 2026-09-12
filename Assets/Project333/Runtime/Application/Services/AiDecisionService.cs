#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Services
{
    public sealed class AiSearchOptions
    {
        public int MaxDepth { get; set; } = 3;
        public int BeamWidth { get; set; } = 5;
        public int MaxSimulations { get; set; } = 1200;
        public int MaxMilliseconds { get; set; } = 80;
    }

    public sealed class AiDecisionService
    {
        private readonly AiActionGenerator _generator;
        private readonly AiBattleSimulation _simulation;
        private readonly AiPositionEvaluator _evaluator = new AiPositionEvaluator();
        private readonly AiSearchOptions _options;
        private readonly HashSet<string> _visited = new HashSet<string>();
        private int _turn = -1;
        private int _decisions;
        private double _turnSearchMilliseconds;
        public int LastSimulationCount { get; private set; }
        public int LastCompletedDepth { get; private set; }
        public double LastElapsedMilliseconds { get; private set; }
        public bool LastBudgetExhausted { get; private set; }

        public AiDecisionService(ICardDefinitionProvider provider) : this(provider, new TargetingService(), ZeroCardUpgradeLevelProvider.Instance) { }
        public AiDecisionService(ICardDefinitionProvider provider, ICardUpgradeLevelProvider levels) : this(provider, new TargetingService(), levels) { }
        public AiDecisionService(ICardDefinitionProvider provider, TargetingService targeting) : this(provider, targeting, ZeroCardUpgradeLevelProvider.Instance) { }
        public AiDecisionService(ICardDefinitionProvider provider, TargetingService targeting, ICardUpgradeLevelProvider levels)
            : this(provider, targeting, levels, new AiSearchOptions()) { }

        public AiDecisionService(ICardDefinitionProvider provider, TargetingService targeting, ICardUpgradeLevelProvider levels, AiSearchOptions options)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (targeting == null) throw new ArgumentNullException(nameof(targeting));
            if (options == null || options.MaxDepth < 1 || options.MaxDepth > 3 || options.BeamWidth < 1 ||
                options.MaxSimulations < 4 || options.MaxMilliseconds < 1) throw new ArgumentOutOfRangeException(nameof(options));
            _options = new AiSearchOptions { MaxDepth = options.MaxDepth, BeamWidth = Math.Min(12, options.BeamWidth),
                MaxSimulations = options.MaxSimulations, MaxMilliseconds = options.MaxMilliseconds };
            _generator = new AiActionGenerator(provider, targeting);
            _simulation = new AiBattleSimulation(provider, levels ?? ZeroCardUpgradeLevelProvider.Instance);
        }

        public IBattleCommand GetNextCommand(BattleState battleState)
        {
            if (battleState == null) throw new ArgumentNullException(nameof(battleState));
            if (battleState.ActivePlayerId != PlayerId.AI || battleState.Phase != PhaseType.Main || battleState.IsEnded)
                throw new InvalidOperationException("AI decisions require a live AI main phase.");
            var watch = Stopwatch.StartNew();
            LastSimulationCount = 0;
            LastCompletedDepth = 0;
            LastBudgetExhausted = false;
            try
            {
                if (_turn != battleState.TurnNumber) { _turn = battleState.TurnNumber; _decisions = 0; _turnSearchMilliseconds = 0; _visited.Clear(); }
                if (++_decisions >= 64 || _turnSearchMilliseconds >= 500) return new EndTurnCommand();
                var observation = AiBattleStateCopy.Create(battleState, true);
                _visited.Add(AiBattleStateCopy.PositionKey(observation));
                var root = new Node { States = new[] { observation, observation }, Depth = 0 };
                var end = new AiAction(-1, new EndTurnCommand());
                var best = Expand(root, end, watch);
                if (best == null) return new EndTurnCommand();
                var frontier = new List<Node> { root };
                for (var depth = 1; depth <= _options.MaxDepth && frontier.Count > 0; depth++)
                {
                    var next = new List<Node>();
                    var seen = new HashSet<string>();
                    var fraction = depth == _options.MaxDepth ? 1.0 : depth == 1 ? 0.5 : 0.8;
                    foreach (var parent in frontier)
                    {
                        if (parent.States[0].IsEnded || parent.States[0].ActivePlayerId != PlayerId.AI) continue;
                        foreach (var action in _generator.Generate(parent.States[0]))
                        {
                            if (LastSimulationCount >= _options.MaxSimulations * fraction ||
                                watch.Elapsed.TotalMilliseconds >= _options.MaxMilliseconds * fraction) break;
                            if (parent.Depth + action.Commands.Length > _options.MaxDepth) continue;
                            var node = Expand(parent, action, watch);
                            if (node == null) continue;
                            var key = AiBattleStateCopy.PositionKey(node.States[0]) + "|" + AiBattleStateCopy.PositionKey(node.States[1]);
                            if (!seen.Add(key) || _visited.Contains(AiBattleStateCopy.PositionKey(node.States[0]))) continue;
                            if (node.Score > best.Score + 0.0001) best = node;
                            if (!node.States[0].IsEnded && node.States[0].ActivePlayerId == PlayerId.AI) next.Add(node);
                        }
                    }
                    LastCompletedDepth = depth;
                    // Stable insertion-order tie breaks, independent of newly generated card/occupant GUIDs.
                    for (var i = 1; i < next.Count; i++)
                    {
                        var item = next[i];
                        var j = i - 1;
                        while (j >= 0 && next[j].Score < item.Score) { next[j + 1] = next[j]; j--; }
                        next[j + 1] = item;
                    }
                    frontier = DiverseBeam(next);
                }
                return best.First ?? new EndTurnCommand();
            }
            finally
            {
                LastElapsedMilliseconds = watch.Elapsed.TotalMilliseconds;
                _turnSearchMilliseconds += LastElapsedMilliseconds;
                LastBudgetExhausted = LastSimulationCount >= _options.MaxSimulations ||
                    LastElapsedMilliseconds >= _options.MaxMilliseconds;
            }
        }

        private Node? Expand(Node parent, AiAction action, Stopwatch watch)
        {
            var node = new Node { First = parent.First ?? action.Commands[0], States = new BattleState[2],
                Depth = parent.Depth + action.Commands.Length, Cost = parent.Cost + action.Commands.Length * 0.25 };
            double total = 0, worst = double.MaxValue;
            for (var sample = 0; sample < 2; sample++)
            {
                double movementScore = 0;
                var escapeOnly = false;
                if (action.Commands[0] is MoveOccupantCommand move)
                {
                    movementScore = AiMovementPolicy.Score(parent.States[sample], move);
                    escapeOnly = movementScore <= 0;
                    if (escapeOnly && !AiMovementPolicy.IsHazardEscapeCandidate(parent.States[sample], move)) return null;
                }
                if (!HasBudget(watch)) return null;
                LastSimulationCount++;
                var after = _simulation.Apply(parent.States[sample], action, sample);
                if (after == null) return null;
                node.States[sample] = after;
                var projected = after;
                if (!after.IsEnded && after.ActivePlayerId == PlayerId.AI)
                {
                    if (!HasBudget(watch)) return null;
                    LastSimulationCount++;
                    projected = _simulation.Apply(after, new AiAction(-1, new EndTurnCommand()), sample);
                    if (projected == null) return null;
                }
                if (escapeOnly && action.Commands[0] is MoveOccupantCommand escape)
                {
                    if (!HasBudget(watch)) return null;
                    LastSimulationCount++;
                    var staying = _simulation.Apply(parent.States[sample], new AiAction(-1, new EndTurnCommand()), sample);
                    if (staying == null || !AiMovementPolicy.ImprovesEscapeSurvival(
                        parent.States[sample], escape, staying, projected)) return null;
                }
                BattleState? survivalForecast = null;
                if (_simulation.HasOpponentTurnEndDamageRisk(projected))
                {
                    if (!HasBudget(watch)) return null;
                    LastSimulationCount++;
                    survivalForecast = _simulation.ProjectOpponentTurnEnd(projected, sample);
                }
                // Compare every candidate at the same horizons. EndTurn already contains the
                // transition; its immediate term must use the state before that command.
                var immediate = action.Commands[0] is EndTurnCommand ? parent.States[sample] : after;
                var score = after.IsEnded ? _evaluator.Evaluate(after) :
                    projected.IsEnded ? _evaluator.Evaluate(projected) * 0.9 :
                    survivalForecast == null
                        ? _evaluator.EvaluateBeforeTurnStart(immediate, projected) * 0.25 + _evaluator.Evaluate(projected) * 0.75
                        // Remove doomed future material from both terms, without treating a hypothetical
                        // opponent pass (or its terminal result) as a guaranteed attack or victory.
                        : _evaluator.EvaluateWithSurvivalForecast(immediate, projected, survivalForecast.AIBoard) * 0.25 +
                          _evaluator.EvaluateWithSurvivalForecast(projected, projected, survivalForecast.AIBoard) * 0.75;
                if (!after.IsEnded && !projected.IsEnded)
                {
                    if (action.Commands[0] is MoveOccupantCommand protectionMove)
                    {
                        movementScore = survivalForecast == null
                            ? AiMovementPolicy.ScoreBeforeTurnStart(parent.States[sample], protectionMove, projected)
                            : AiMovementPolicy.ScoreWithSurvivalForecast(parent.States[sample], protectionMove,
                                projected, survivalForecast.AIBoard);
                        // Escape has no flat bonus: keep movement/cover costs and compare resolved board value.
                        if (!escapeOnly) movementScore = Math.Max(0, movementScore);
                    }
                    score += movementScore;
                }
                total += score;
                worst = Math.Min(worst, score);
            }
            // Do not choose an optimistic random outcome and present it as a guaranteed kill.
            node.Score = total * 0.375 + worst * 0.25 - node.Cost;
            return node;
        }
        private bool HasBudget(Stopwatch watch) => LastSimulationCount < _options.MaxSimulations &&
            watch.Elapsed.TotalMilliseconds < _options.MaxMilliseconds;

        private List<Node> DiverseBeam(List<Node> sorted)
        {
            var result = new List<Node>();
            var groups = new HashSet<string>();
            foreach (var node in sorted)
            {
                var group = node.First is IHandCardCommand hand ? "card:" + hand.CardId : node.First!.GetType().Name;
                if (groups.Add(group)) result.Add(node);
                if (result.Count >= _options.BeamWidth) return result;
            }
            foreach (var node in sorted)
            {
                if (!result.Contains(node)) result.Add(node);
                if (result.Count >= _options.BeamWidth) break;
            }
            return result;
        }
        private sealed class Node
        {
            public BattleState[] States = Array.Empty<BattleState>();
            public IBattleCommand? First;
            public int Depth;
            public double Cost;
            public double Score;
        }
    }
}
