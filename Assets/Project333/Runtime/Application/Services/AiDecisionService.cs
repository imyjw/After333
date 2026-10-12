#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
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
        private const double ScoreTieEpsilon = 0.0001;
        private readonly AiActionGenerator _generator;
        private readonly AiBattleSimulation _simulation;
        private readonly AiPositionEvaluator _evaluator;
        private readonly AiProducerProtectionPolicy _protectionPolicy;
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
            _protectionPolicy = new AiProducerProtectionPolicy(provider);
            _evaluator = new AiPositionEvaluator(_protectionPolicy);
            _generator = new AiActionGenerator(provider, targeting, _protectionPolicy);
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
                var root = new Node { States = new[] { observation, observation }, Depth = 0,
                    MasterExposureHp = observation.AI.Master.CurrentHp };
                var end = new AiAction(-1, new EndTurnCommand());
                var best = Expand(root, end, watch);
                if (best == null) return new EndTurnCommand();
                var frontier = new List<Node> { root };
                for (var depth = 1; depth <= _options.MaxDepth && frontier.Count > 0; depth++)
                {
                    var next = new List<Node>();
                    var seen = new Dictionary<string, Node>();
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
                            if (_visited.Contains(AiBattleStateCopy.PositionKey(node.States[0]))) continue;
                            if (seen.TryGetValue(key, out var previous))
                            {
                                if (!IsPreferred(node, previous)) continue;
                                next.Remove(previous);
                            }
                            seen[key] = node;
                            if (IsPreferred(node, best)) best = node;
                            if (!node.States[0].IsEnded && node.States[0].ActivePlayerId == PlayerId.AI) next.Add(node);
                        }
                    }
                    LastCompletedDepth = depth;
                    // Resolve otherwise equal attacks by target HP, then retain stable insertion order.
                    for (var i = 1; i < next.Count; i++)
                    {
                        var item = next[i];
                        var j = i - 1;
                        while (j >= 0 && IsPreferred(item, next[j])) { next[j + 1] = next[j]; j--; }
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
                Depth = parent.Depth + action.Commands.Length, Cost = parent.Cost + action.Commands.Length * 0.25,
                Focus = parent.Focus, ProtectionFocus = parent.ProtectionFocus, MasterExposureHp = parent.MasterExposureHp };
            if (parent.First == null && action.Commands[0] is MoveOccupantCommand firstMove)
                node.ProtectionFocus = _protectionPolicy.CreateFocus(parent.States[0], firstMove);
            var protectionFocusIsValid = node.ProtectionFocus != null;
            var firstFocus = parent.First == null ? CreateAttackFocus(parent.States[0], action.Commands[0]) : null;
            var focusIsValid = firstFocus != null;
            double total = 0, worst = double.MaxValue;
            for (var sample = 0; sample < 2; sample++)
            {
                double movementScore = 0;
                var escapeOnly = false;
                if (action.Commands[0] is MoveOccupantCommand move)
                {
                    movementScore = AiMovementPolicy.ScoreUsingCardCosts(parent.States[sample], move, _protectionPolicy);
                    escapeOnly = movementScore <= 0;
                    if (escapeOnly && !AiMovementPolicy.IsHazardEscapeCandidate(parent.States[sample], move)) return null;
                }
                if (!HasBudget(watch)) return null;
                LastSimulationCount++;
                var after = _simulation.Apply(parent.States[sample], action, sample);
                if (after == null) return null;
                node.States[sample] = after;
                node.ActionPreferenceScores[sample] = parent.ActionPreferenceScores[sample] +
                    _evaluator.EvaluateProducerAttack(parent.States[sample], after, action.Commands[0]);
                node.EnemyMasterHpScores[sample] = parent.EnemyMasterHpScores[sample] +
                    _evaluator.EvaluateEnemyMasterHpChange(parent.States[sample].Player.Master.CurrentHp, after.Player.Master.CurrentHp);
                node.OwnMasterHpScores[sample] = parent.OwnMasterHpScores[sample] +
                    _evaluator.EvaluateOwnMasterHpChange(parent.States[sample].AI.Master.CurrentHp, after.AI.Master.CurrentHp);
                if (firstFocus != null)
                {
                    var targetAfter = after.PlayerBoard.GetOccupant(firstFocus.Target);
                    focusIsValid &= targetAfter == null ||
                        (targetAfter.RuntimeId == firstFocus.TargetRuntimeId && targetAfter.CurrentHp < firstFocus.Hp);
                }
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
                        ? _evaluator.EvaluateBeforeTurnStartForPlanning(immediate, projected, node.MasterExposureHp) * 0.25 +
                          _evaluator.EvaluateForPlanning(projected, node.MasterExposureHp) * 0.75
                        // Remove doomed future material from both terms, without treating a hypothetical
                        // opponent pass (or its terminal result) as a guaranteed attack or victory.
                        : _evaluator.EvaluateWithSurvivalForecastForPlanning(immediate, projected,
                            survivalForecast.AIBoard, node.MasterExposureHp) * 0.25 +
                          _evaluator.EvaluateWithSurvivalForecastForPlanning(projected, projected,
                            survivalForecast.AIBoard, node.MasterExposureHp) * 0.75;
                if (!after.IsEnded && !projected.IsEnded)
                {
                    if (action.Commands[0] is MoveOccupantCommand protectionMove)
                    {
                        movementScore = survivalForecast == null
                            ? AiMovementPolicy.ScoreForPlanningBeforeTurnStart(parent.States[sample], protectionMove, projected, _protectionPolicy)
                            : AiMovementPolicy.ScoreForPlanningWithSurvivalForecast(parent.States[sample], protectionMove,
                                projected, survivalForecast.AIBoard, _protectionPolicy);
                        // Escape has no flat bonus: keep movement/cover costs and compare resolved board value.
                        // Known-producer cover is already in state value; retain its -4 move cost.
                        if (!escapeOnly && _protectionPolicy.CreateFocus(parent.States[sample], protectionMove) == null)
                            movementScore = Math.Max(0, movementScore);
                    }
                    // Only actual path changes are carried to children. A projected end turn
                    // must not be credited repeatedly at every search depth.
                    var immediateMasterScore = action.Commands[0] is EndTurnCommand
                        ? parent.EnemyMasterHpScores[sample] : node.EnemyMasterHpScores[sample];
                    var projectedMasterScore = node.EnemyMasterHpScores[sample] +
                        _evaluator.EvaluateEnemyMasterHpChange(after.Player.Master.CurrentHp, projected.Player.Master.CurrentHp);
                    var immediateOwnMasterScore = action.Commands[0] is EndTurnCommand
                        ? parent.OwnMasterHpScores[sample] : node.OwnMasterHpScores[sample];
                    var projectedOwnMasterScore = node.OwnMasterHpScores[sample] +
                        _evaluator.EvaluateOwnMasterHpChange(after.AI.Master.CurrentHp, projected.AI.Master.CurrentHp);
                    score += (immediateMasterScore + immediateOwnMasterScore) * 0.25 +
                        (projectedMasterScore + projectedOwnMasterScore) * 0.75;
                    score += movementScore + node.ActionPreferenceScores[sample];
                }
                if (node.ProtectionFocus != null)
                    protectionFocusIsValid &= AiProducerProtectionPolicy.IsValidFocus(node.ProtectionFocus,
                        projected.AIBoard, survivalForecast?.AIBoard);
                if (after.IsEnded || projected.IsEnded)
                {
                    protectionFocusIsValid = false;
                    focusIsValid = false;
                    node.Focus = null;
                }
                total += score;
                worst = Math.Min(worst, score);
            }
            if (parent.First == null) node.Focus = focusIsValid ? firstFocus : null;
            if (!protectionFocusIsValid) node.ProtectionFocus = null;
            // Do not choose an optimistic random outcome and present it as a guaranteed kill.
            node.Score = total * 0.375 + worst * 0.25 - node.Cost;
            return node;
        }
        private static bool IsPreferred(Node candidate, Node incumbent)
        {
            if (candidate.Score > incumbent.Score + ScoreTieEpsilon) return true;
            if (Math.Abs(candidate.Score - incumbent.Score) > ScoreTieEpsilon) return false;
            var protection = candidate.ProtectionFocus;
            var priorProtection = incumbent.ProtectionFocus;
            if (protection != null && priorProtection != null)
            {
                if (protection.Cost != priorProtection.Cost) return protection.Cost > priorProtection.Cost;
                if (protection.GuardHp != priorProtection.GuardHp) return protection.GuardHp > priorProtection.GuardHp;
            }
            var focus = candidate.Focus;
            var previous = incumbent.Focus;
            return focus != null && previous != null && focus.Attacker.Equals(previous.Attacker) &&
                focus.EquivalenceKey == previous.EquivalenceKey && focus.Hp < previous.Hp;
        }

        private static AttackFocus? CreateAttackFocus(BattleState before, IBattleCommand command)
        {
            if (command is not AttackCommand attack) return null;
            var attacker = before.AIBoard.GetOccupant(attack.AttackerCoord);
            var target = before.PlayerBoard.GetOccupant(attack.TargetCoord);
            if (attacker == null || attacker.IsUnderHuanShu || target == null ||
                target.Kind != OccupantKind.Unit || !target.IsAlive) return null;

            var key = new StringBuilder(AiBattleStateCopy.FocusTargetKey(target));
            for (var index = 0; index < before.PersistentEffects.Count; index++)
            {
                var effect = before.PersistentEffects[index];
                if (effect.IsExpired) continue;
                // Delayed destruction is not interchangeable with an unmarked target.
                if (effect.TargetRuntimeId == target.RuntimeId) return null;
                if (effect.EffectDamage <= 0) continue;
                var affectedOwner = effect.TargetsOwnerBoard ? effect.OwnerId : before.GetOpponent(effect.OwnerId).Id;
                if (affectedOwner != target.OwnerId ||
                    (effect.TargetRow >= 0 && effect.TargetRow != target.Position.Row) ||
                    (effect.TargetStartColumn >= 0 &&
                     !BiochemicalBombRules.ContainsColumn(effect.TargetStartColumn, target.Position.Column))) continue;
                key.Append("|area:").Append(index);
            }
            return new AttackFocus
            {
                Attacker = attack.AttackerCoord, Target = attack.TargetCoord,
                TargetRuntimeId = target.RuntimeId, Hp = target.CurrentHp, EquivalenceKey = key.ToString()
            };
        }

        private sealed class AttackFocus
        {
            public TileCoord Attacker;
            public TileCoord Target;
            public string TargetRuntimeId = string.Empty;
            public string EquivalenceKey = string.Empty;
            public int Hp;
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
            public double[] ActionPreferenceScores = new double[2];
            public double[] EnemyMasterHpScores = new double[2];
            public double[] OwnMasterHpScores = new double[2];
            public int MasterExposureHp;
            public IBattleCommand? First;
            public int Depth;
            public double Cost;
            public double Score;
            public AttackFocus? Focus;
            public AiProducerProtectionFocus? ProtectionFocus;
        }
    }
}
