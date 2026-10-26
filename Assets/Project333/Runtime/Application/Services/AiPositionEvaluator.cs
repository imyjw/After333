#nullable enable
using System;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Domain.Effects;

namespace Project333.Runtime.Application.Services
{
    internal sealed class AiPositionEvaluator
    {
        private const double ProducerSurvivalValue = 50;
        private const double ProducerHpValue = 0.45;
        private const double RangedSurvivalValue = 18;
        private const double RangedHpValue = 0.30;
        private readonly TargetingService _targeting = new TargetingService();
        private readonly AiProducerProtectionPolicy? _protectionPolicy;
        public AiPositionEvaluator() { }
        internal AiPositionEvaluator(AiProducerProtectionPolicy policy) { _protectionPolicy = policy; }
        public double Evaluate(BattleState state) => EvaluateCore(state, null, null);

        // The immediate score must not reward occupants that the real turn transition removes.
        // Presence preserves Demon King revival even when its projected HP is zero.
        public double EvaluateBeforeTurnStart(BattleState state, BattleState projected) =>
            EvaluateCore(state, projected, null);

        // Only forecast continued AI board value. The opponent's unplayed turn is not a known outcome.
        public double EvaluateWithSurvivalForecast(BattleState state, BattleState projected, BoardState futureAiBoard) =>
            EvaluateCore(state, projected, futureAiBoard);

        // Freeze positioning risk at the start of a decision. Losing HP during a simulated
        // action must not add an exposure penalty on top of its explicit damage-band score.
        public double EvaluateForPlanning(BattleState state, int ownMasterExposureHp) =>
            EvaluateCore(state, null, null, ownMasterExposureHp);
        public double EvaluateBeforeTurnStartForPlanning(BattleState state, BattleState projected, int ownMasterExposureHp) =>
            EvaluateCore(state, projected, null, ownMasterExposureHp);
        public double EvaluateWithSurvivalForecastForPlanning(BattleState state, BattleState projected,
            BoardState futureAiBoard, int ownMasterExposureHp) =>
            EvaluateCore(state, projected, futureAiBoard, ownMasterExposureHp);

        public double EvaluateEnemyMasterHpChange(int beforeHp, int afterHp)
        {
            var before = Math.Max(0, beforeHp);
            var after = Math.Max(0, afterHp);
            var weight = before >= 250 ? 0.8 : before >= 150 ? 0.84 : before >= 80 ? 1.2 : 3.0;
            // Actual net HP change only: no reward for blocked damage or overkill.
            // Healing is a negative change, not a second damage reward.
            return (before - after) * weight;
        }

        // Use the same pre-action bands with the opposite sign for our own master.
        public double EvaluateOwnMasterHpChange(int beforeHp, int afterHp) =>
            -EvaluateEnemyMasterHpChange(beforeHp, afterHp);

        // A soft reserve cost, carried along the search path rather than reset by EndTurn.
        public double EvaluateProducerAttack(BattleState before, BattleState after, IBattleCommand command)
        {
            if (command is not AttackCommand attack) return 0;
            var attacker = before.AIBoard.GetOccupant(attack.AttackerCoord);
            if (attacker == null || !IsResourceProducer(attacker)) return 0;
            var killed = false;
            var valuableKill = false;
            foreach (var enemy in before.PlayerBoard.EnumerateOccupants())
            {
                if (!enemy.IsAlive || enemy.Kind == OccupantKind.Master) continue;
                var remaining = after.PlayerBoard.GetOccupant(enemy.Position);
                // A revival seal is still enemy material, not a removed summon.
                if (remaining != null && remaining.RuntimeId == enemy.RuntimeId) continue;
                killed = true;
                valuableKill |= IsResourceProducer(enemy) ||
                    (enemy.Kind == OccupantKind.Unit && enemy.Attack >= 10 && enemy.CurrentHp <= enemy.Attack);
            }
            // Real counterattack losses and destruction chains remain in the board evaluation.
            // Only melee producers reserve attacks that remove no enemy.
            if (!killed) return attacker.AttackType == AttackType.Melee ? -120 : 0;
            return valuableKill ? -8 : -80;
        }

        private double HuanShuDisruptionValue(BattleState state, OccupantState unit)
        {
            if (!unit.IsAlive || unit.Attack <= 0) return 0;
            var attackPower = (double)unit.Attack * unit.EffectiveHitsPerAttack * Math.Min(2, unit.EffectiveMaxAttacksPerTurn);
            // Confusion disrupts attacks; it does not remove HP, production or spell power.
            var value = attackPower * 1.2;
            if (unit.IsSealbound) return value * 0.45;
            if (unit.IsDrained) return value * 0.3;
            if (unit.OwnerId != PlayerId.Player || unit.CannotAttack) return value;
            var master = state.AI.Master;
            if (!_targeting.CanTarget(state, unit.OwnerId, unit.Position, master.Position) ||
                ShielderService.ResolveForNormalAttack(state.AIBoard, master.Position, unit).IsProtected) return value;
            var raw = SpellPowerRules.ApplyCurrent(state, unit.OwnerId, unit.Attack, unit.DamageType);
            var damage = (double)DamageResolutionRules.ResolveIncomingDamage(master, raw, unit.DamageType,
                DamageResolutionRules.ShouldHalveForFlying(unit, master), unit.IsHiding) *
                unit.EffectiveHitsPerAttack * Math.Min(2, unit.EffectiveMaxAttacksPerTurn);
            if (damage <= 0 || master.CurrentHp > damage * 2) return value;
            var candidates = 0;
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
                foreach (var target in state.GetBoard(owner).EnumerateOccupants())
                    if (target.RuntimeId != unit.RuntimeId && target.IsAlive && target.CanBeAffected &&
                        (target.OwnerId == unit.OwnerId || !target.IsHiding)) candidates++;
            // Emergency utility is probabilistic: HuanShu never guarantees master protection.
            var diversionChance = candidates > 1 ? 1.0 - 1.0 / candidates : 0;
            return value + 60 * diversionChance * Math.Min(1, damage / Math.Max(1, master.CurrentHp));
        }
        private double EvaluateCore(BattleState state, BattleState? projected, BoardState? futureAiBoard,
            int? ownMasterExposureHp = null)
        {
            if (state.IsEnded) return state.Result.IsDraw ? 0 : state.Result.Winner == PlayerId.AI ? 1000000 : -1000000;
            var referenceHp = ownMasterExposureHp ?? state.AI.Master.CurrentHp;
            var value = Side(state, PlayerId.AI, projected, futureAiBoard, referenceHp) -
                Side(state, PlayerId.Player, projected, futureAiBoard, referenceHp);

            return value + KnownOwnTrapValue(state);
        }

        private double KnownOwnTrapValue(BattleState state)
        {
            double value = 0;
            foreach (var trap in state.ArmedTraps)
            {
                if (trap == null || trap.OwnerId != PlayerId.AI) continue;
                // An armed free card remains useful even while saving its activation cost.
                // Enemy count is deliberately not assigned an invented trigger or card value.
                value += 8;
                if (trap.TriggerType == TrapTriggerType.EnemyUnitAttacksUnit &&
                    trap.EffectType == TrapEffectType.ReflectAttackerAttackAsMagic)
                {
                    if (state.AI.Resources.CanAfford(trap.ActivationCost))
                        value += Math.Min(20, MirrorFieldThreatValue(state) * 0.25);
                    continue;
                }
                var requiresMelee = trap.TriggerType == TrapTriggerType.EnemyMeleeUnitAttacksMaster &&
                    trap.EffectType == TrapEffectType.DestroyAttacker;
                var requiresRanged = trap.TriggerType == TrapTriggerType.EnemyRangedUnitAttacksMaster &&
                    trap.EffectType == TrapEffectType.DestroyAttacker;
                var capturesMasterAttack = trap.TriggerType == TrapTriggerType.EnemyUnitAttacksMaster &&
                    (trap.EffectType == TrapEffectType.ReturnAttackerToHand ||
                     trap.EffectType == TrapEffectType.DestroyAttacker);
                if ((!requiresMelee && !requiresRanged && !capturesMasterAttack) ||
                    !state.AI.Resources.CanAfford(trap.ActivationCost)) continue;
                double largestThreat = 0;
                var master = state.AI.Master;
                foreach (var unit in state.PlayerBoard.EnumerateOccupants())
                {
                    if (unit.Kind != OccupantKind.Unit || !unit.IsAlive || unit.CannotAttack ||
                        (requiresMelee && unit.AttackType != AttackType.Melee) ||
                        (requiresRanged && unit.AttackType != AttackType.Ranged) ||
                        unit.Attack <= 0 || !_targeting.CanTarget(state, unit.OwnerId, unit.Position, master.Position)) continue;
                    var raw = SpellPowerRules.ApplyCurrent(state, unit.OwnerId, unit.Attack, unit.DamageType);
                    var damage = DamageResolutionRules.ResolveIncomingDamage(master, raw, unit.DamageType,
                        DamageResolutionRules.ShouldHalveForFlying(unit, master), unit.IsHiding);
                    largestThreat = Math.Max(largestThreat, Math.Min(master.CurrentHp, damage));
                }
                // Bounded prevention utility for known own traps; no future attack is assumed.
                // Only the shared service pays, returns or destroys, including revival/death effects.
                value += Math.Min(20, largestThreat * 0.25);
            }
            return value;
        }

        private double MirrorFieldThreatValue(BattleState state)
        {
            double largestThreat = 0;
            foreach (var attacker in state.PlayerBoard.EnumerateOccupants())
            {
                if (attacker.Kind == OccupantKind.Building || !attacker.IsAlive ||
                    attacker.CannotAttack || attacker.Attack <= 0) continue;
                // The reflected packet targets the attacker directly, just as in TrapService.
                var reflectedRaw = SpellPowerRules.ApplyCurrent(state, PlayerId.AI, attacker.Attack, DamageType.Magic);
                var reflected = DamageResolutionRules.ResolveIncomingDamage(attacker, reflectedRaw, DamageType.Magic);
                foreach (var coord in _targeting.GetLegalTargets(state, attacker.OwnerId, attacker.Position))
                {
                    var target = state.AIBoard.GetOccupant(coord);
                    if (target == null || target.Kind == OccupantKind.Building || !target.IsAlive) continue;
                    var incomingRaw = SpellPowerRules.ApplyCurrent(state, attacker.OwnerId, attacker.Attack, attacker.DamageType);
                    var incoming = DamageResolutionRules.ResolveIncomingDamage(target, incomingRaw, attacker.DamageType,
                        DamageResolutionRules.ShouldHalveForFlying(attacker, target), attacker.IsHiding);
                    // Public potential only, bounded like Return; no opponent action is assumed to occur.
                    largestThreat = Math.Max(largestThreat,
                        Math.Min(target.CurrentHp, incoming) + Math.Min(attacker.CurrentHp, reflected));
                }
            }
            return largestThreat;
        }

        private double Side(BattleState state, PlayerId owner, BattleState? projected, BoardState? futureAiBoard,
            int ownMasterExposureHp)
        {
            var projectedAiBoard = projected?.AIBoard;
            var player = state.GetPlayer(owner);
            var board = state.GetBoard(owner);
            // Both masters' HP is scored once per transition using pre-action bands.
            double value = ResourceValue(player.Resources);
            // Permanent exposure also affects units not yet played, with diminishing marginal pressure.
            value -= 18 * Math.Log(1 + board.FalloutStacks);
            foreach (var card in player.Hand.Cards) value += card.IsTemporaryReplicate ? 3 : 7;
            foreach (var unit in board.EnumerateOccupants())
            {
                if (!unit.IsAlive && !unit.IsDemonKingRevivalPending) continue;
                if (!RemainsOnBoard(unit, projectedAiBoard) || !RemainsOnBoard(unit, futureAiBoard)) continue;
                var material = 12 + Math.Max(0, unit.Attack) * 1.35 * unit.EffectiveHitsPerAttack * Math.Min(2, unit.EffectiveMaxAttacksPerTurn)
                    + Math.Max(0, unit.CurrentHp) * 0.55;
                if (unit is MasterState) material = Math.Max(0, unit.Attack - 3) * 1.35;
                // State value, not a per-click reward: only real HP loss/removal earns target priority.
                // A ranged producer uses the higher producer role rather than stacking both bonuses.
                if (IsResourceProducer(unit)) material += ProducerSurvivalValue + Math.Max(0, unit.CurrentHp) * ProducerHpValue;
                else if (IsRangedThreat(unit)) material += RangedSurvivalValue + Math.Max(0, unit.CurrentHp) * RangedHpValue;
                if (unit.IsSealbound) material *= 0.45;
                else if (unit.IsDrained) material *= 0.3;
                if (!unit.EffectsSuppressed)
                {
                    material += GainValue(unit.TurnStartResourceGain) * 3;
                    material += unit.CanTriggerEndure ? 7 : 0;
                    material += unit.HasLifeSteal ? Math.Min(15, unit.Attack * 0.5) : 0;
                    material += unit.HasActiveShielder ? 7 : 0;
                    material += unit.HasActiveFlying ? 6 : 0;
                    material += unit.IsHiding ? 4 : 0;
                    material += unit.HasActivePiercing ? 6 : 0;
                    material += unit.IsInvincible ? 12 : 0;
                    material += unit.EffectiveSpellPower * 4;
                    if (unit.CardId == "RobotFactory" || unit.CardId == "GaebangBranch") material += 14;
                    if (unit.CardId == "PowerPlant") material += player.Resources.Gold > 0 ? 7 : 1;
                    if (unit.CardId == "Hero" || unit.CardId == "DemonKing") material += 18;
                }
                if (unit.IsUnderHuanShu) material -= Math.Min(Math.Max(0, material), HuanShuDisruptionValue(state, unit));
                if (unit.IsFalloutTarget && unit.FalloutStacks > 0 && !unit.IsSealbound && !unit.IsInvincible)
                    material -= Math.Min(material * 0.5, (double)unit.FalloutStacks * NuclearRules.FalloutDamage);
                material += PlasmaShieldProtectionValue(state, unit);
                material += ManaShieldProtectionValue(state, unit);
                value += material;
                value += owner == PlayerId.AI && projectedAiBoard != null
                    ? BackRowProtectionValue(projectedAiBoard.GetOccupant(unit.Position), projectedAiBoard)
                    : BackRowProtectionValue(unit, board);
            }
            foreach (var effect in state.PersistentEffects)
            {
                if (effect.IsExpired || effect.OwnerId != owner) continue;
                value += GainValue(effect.TurnStartResourceGain) * Math.Min(3, Math.Max(1, effect.OwnerTurnStartsRemaining));
                if (effect.EffectDamage <= 0) continue;
                var targetBoard = effect.TargetsOwnerBoard ? board : state.GetOpponentBoard(owner);
                double pressure = 0;
                foreach (var target in targetBoard.EnumerateOccupants())
                {
                    if (!target.CanBeAffected || !target.IsAlive || !RemainsOnBoard(target, projectedAiBoard)) continue;
                    if (effect.TargetRow >= 0 && target.Position.Row != effect.TargetRow) continue;
                    if (effect.TargetStartColumn >= 0 && !BiochemicalBombRules.ContainsColumn(effect.TargetStartColumn, target.Position.Column)) continue;
                    pressure += Math.Min(target.CurrentHp, effect.EffectDamage) * 0.16;
                }
                // Future occupancy is uncertain. Immediate triggers are resolved by the simulator, not estimated here.
                value += (effect.TargetsOwnerBoard ? -1 : 1) * pressure * Math.Min(2, Math.Max(1, effect.RemainingTriggers));
            }
            // Use the resolved board for threat geometry: a doomed guard cannot protect the master.
            var threatState = projected ?? state;
            var enemyId = threatState.GetOpponent(owner).Id;
            double exposure = 0;
            foreach (var enemy in threatState.GetOpponentBoard(owner).EnumerateOccupants())
            {
                if (!enemy.IsAlive || enemy.CannotAttack || enemy.Attack <= 0) continue;
                // Enemy attacks and our guards matter during the imminent enemy main phase.
                // Our future attacks must survive its end effects; keep master HP at the near horizon.
                if (!CanAttackAfterForecast(enemy, futureAiBoard)) continue;
                if (_targeting.CanTarget(threatState, enemyId, enemy.Position, threatState.GetPlayer(owner).Master.Position))
                    exposure += enemy.Attack * enemy.EffectiveHitsPerAttack * Math.Min(2, enemy.EffectiveMaxAttacksPerTurn);
            }
            // A positioning risk heuristic, not a second implementation of damage/armor rules.
            // Keep geometry value without a second HP-dependent damage reward or penalty.
            var exposureHp = owner == PlayerId.AI ? ownMasterExposureHp : 333;
            value -= exposure * (0.08 + 8.0 / (Math.Max(1, exposureHp) + 5));
            return value;
        }

        // A bounded estimate of HP protected from currently visible physical attackers.
        // Only this card's attributed armor contributes, preserving all previous no-shield scores.
        // It is not a prediction of the opponent's hand or a guarantee that it attacks this target.
        private double PlasmaShieldProtectionValue(BattleState state, OccupantState recipient)
        {
            if (!recipient.IsAlive || recipient.IsDrained || recipient.IsSealbound || recipient.IsInvincible) return 0;
            var shieldDefense = 0;
            foreach (var buff in recipient.CombatStatBuffs)
                if (buff.SourceCardId == PlasmaShieldRules.CardId) shieldDefense += buff.PhysicalDefenseBonus;
            if (shieldDefense <= 0) return 0;
            var defenseBeforeShield = Math.Max(0, recipient.PhysicalDefense - shieldDefense);
            double unprotectedDamage = 0, protectedDamage = 0;
            var board = state.GetBoard(recipient.OwnerId);
            foreach (var enemy in state.GetOpponentBoard(recipient.OwnerId).EnumerateOccupants())
            {
                if (!enemy.IsAlive || enemy.CannotAttack || enemy.Attack <= 0 || enemy.DamageType != DamageType.Physical ||
                    enemy.IsUnderHuanShu || (recipient.OwnerId == PlayerId.AI && enemy.IsHiding) ||
                    !_targeting.CanTarget(state, enemy.OwnerId, enemy.Position, recipient.Position) ||
                    ShielderService.ResolveForNormalAttack(board, recipient.Position, enemy).IsProtected) continue;
                var before = DamageResolutionRules.ResolveIncomingDamage(enemy.Attack, DamageType.Physical,
                    false, defenseBeforeShield, recipient.MagicDefense);
                var after = DamageResolutionRules.ResolveIncomingDamage(recipient, enemy.Attack, DamageType.Physical,
                    DamageResolutionRules.ShouldHalveForFlying(enemy, recipient), enemy.IsHiding);
                if (DamageResolutionRules.ShouldHalveForFlying(enemy, recipient)) before /= 2;
                before = DamageResolutionRules.ApplyCriticalMultiplier(before, enemy.IsHiding);
                var packets = enemy.EffectiveHitsPerAttack * Math.Min(2, enemy.EffectiveMaxAttacksPerTurn);
                unprotectedDamage += (double)before * packets;
                protectedDamage += (double)after * packets;
            }
            var hpAtRisk = Math.Max(0, recipient.CurrentHp - (recipient.CanTriggerEndure ? 1 : 0));
            var preventedHp = Math.Min(hpAtRisk, unprotectedDamage) - Math.Min(hpAtRisk, protectedDamage);
            var hpValue = IsResourceProducer(recipient) ? 0.55 + ProducerHpValue :
                IsRangedThreat(recipient) ? 0.55 + RangedHpValue : 0.55;
            return Math.Max(0, preventedHp) * hpValue;
        }

        // A bounded estimate of HP protected from currently visible magic attackers.
        // Only this card's attributed armor contributes, preserving all previous no-shield scores.
        // It is not a prediction of the opponent's hand or a guarantee that it attacks this target.
        private double ManaShieldProtectionValue(BattleState state, OccupantState recipient)
        {
            if (!recipient.IsAlive || recipient.IsDrained || recipient.IsSealbound || recipient.IsInvincible) return 0;
            var shieldDefense = 0;
            foreach (var buff in recipient.CombatStatBuffs)
                if (buff.SourceCardId == ManaShieldRules.CardId) shieldDefense += buff.MagicDefenseBonus;
            if (shieldDefense <= 0) return 0;
            var defenseBeforeShield = Math.Max(0, recipient.MagicDefense - shieldDefense);
            double unprotectedDamage = 0, protectedDamage = 0;
            var board = state.GetBoard(recipient.OwnerId);
            foreach (var enemy in state.GetOpponentBoard(recipient.OwnerId).EnumerateOccupants())
            {
                if (!enemy.IsAlive || enemy.CannotAttack || enemy.Attack <= 0 || enemy.DamageType != DamageType.Magic ||
                    enemy.IsUnderHuanShu || !ManaCannonRules.CanPayAttackCost(state, enemy) || (recipient.OwnerId == PlayerId.AI && enemy.IsHiding) ||
                    !_targeting.CanTarget(state, enemy.OwnerId, enemy.Position, recipient.Position) ||
                    ShielderService.ResolveForNormalAttack(board, recipient.Position, enemy).IsProtected) continue;
                var attackDamage = SpellPowerRules.ApplyCurrent(state, enemy.OwnerId, enemy.Attack, DamageType.Magic);
                var before = DamageResolutionRules.ResolveIncomingDamage(attackDamage, DamageType.Magic,
                    false, recipient.PhysicalDefense, defenseBeforeShield);
                var after = DamageResolutionRules.ResolveIncomingDamage(recipient, attackDamage, DamageType.Magic,
                    DamageResolutionRules.ShouldHalveForFlying(enemy, recipient), enemy.IsHiding);
                if (DamageResolutionRules.ShouldHalveForFlying(enemy, recipient)) before /= 2;
                before = DamageResolutionRules.ApplyCriticalMultiplier(before, enemy.IsHiding);
                var packets = enemy.EffectiveHitsPerAttack * Math.Min(2, enemy.EffectiveMaxAttacksPerTurn);
                unprotectedDamage += (double)before * packets;
                protectedDamage += (double)after * packets;
            }
            var hpAtRisk = Math.Max(0, recipient.CurrentHp - (recipient.CanTriggerEndure ? 1 : 0));
            var preventedHp = Math.Min(hpAtRisk, unprotectedDamage) - Math.Min(hpAtRisk, protectedDamage);
            var hpValue = IsResourceProducer(recipient) ? 0.55 + ProducerHpValue :
                IsRangedThreat(recipient) ? 0.55 + RangedHpValue : 0.55;
            return Math.Max(0, preventedHp) * hpValue;
        }

        private static bool CanAttackAfterForecast(OccupantState unit, BoardState? futureAiBoard)
        {
            if (futureAiBoard == null || unit.OwnerId != PlayerId.AI || unit is MasterState) return true;
            var futureUnit = futureAiBoard.GetOccupant(unit.Position);
            return futureUnit != null && futureUnit.RuntimeId == unit.RuntimeId &&
                futureUnit.IsAlive && !futureUnit.CannotAttack;
        }

        private static bool RemainsOnBoard(OccupantState unit, BoardState? projectedAiBoard)
        {
            if (projectedAiBoard == null || unit.OwnerId != PlayerId.AI || unit is MasterState) return true;
            var projectedUnit = projectedAiBoard.GetOccupant(unit.Position);
            return projectedUnit != null && projectedUnit.RuntimeId == unit.RuntimeId;
        }

        internal static bool IsResourceProducer(OccupantState unit)
        {
            if (unit.Kind == OccupantKind.Master || unit.IsErasure) return false;
            var gain = unit.TurnStartResourceGain;
            return gain.Mana > 0 || gain.Qi > 0 || gain.Power > 0 || gain.Gold > 0 ||
                unit.CardId == PowerPlantRules.CardId;
        }

        internal static bool IsRangedThreat(OccupantState unit)
        {
            return unit.Kind != OccupantKind.Master && unit.AttackType == AttackType.Ranged &&
                unit.Attack > 0 && unit.EffectiveMaxAttacksPerTurn > 0;
        }

        private double BackRowProtectionValue(OccupantState? unit, BoardState board)
        {
            if (unit == null || !unit.IsAlive || unit.IsSealbound || unit.Position.Row != 1) return 0;
            var producer = IsResourceProducer(unit);
            var ranged = IsRangedThreat(unit);
            var wounded = AiMovementPolicy.IsWounded(unit);
            var rearValue = producer ? 6 : ranged ? 5 : wounded ? 3 : 0;
            var front = board.GetOccupant(new TileCoord(unit.Position.Column, 0));
            if (front == null || !front.IsAlive || front.DoesNotBlockFrontRow) return rearValue;
            if (unit.OwnerId == PlayerId.AI && _protectionPolicy != null &&
                _protectionPolicy.TryGetProducerCost(unit, out _))
                return rearValue + _protectionPolicy.CoverValue(unit, front);
            // This is ground-melee cover, not immunity to ranged/Flying/Piercing attacks.
            var coverValue = producer ? (unit.Kind == OccupantKind.Building ? 24 : 20) :
                ranged ? 12 : wounded ? 8 : unit.Kind == OccupantKind.Master ? 3 : 1;
            return rearValue + coverValue;
        }

        private static double ResourceValue(ResourceSet r)
        {
            return Currency(r.Gold, 4.0) + Currency(r.Mana, 3.0) + Currency(r.Qi, 3.0) + Currency(r.Power, 2.0);
        }
        private static double Currency(int amount, double weight) => weight * (Math.Min(12, amount) + Math.Max(0, amount - 12) * 0.2);
        private static double GainValue(ResourceSet r) => r.Mana * 3 + r.Qi * 3 + r.Power * 3 + r.Gold * 3.5;
    }
}
