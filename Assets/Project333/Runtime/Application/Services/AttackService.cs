using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    public sealed class AttackService
    {
        private readonly TargetingService _targetingService;

        public AttackService()
            : this(new TargetingService())
        {
        }

        public AttackService(TargetingService targetingService)
        {
            _targetingService = targetingService;
        }

        public void Attack(BattleState battleState, PlayerId attackerOwnerId, TileCoord attackerCoord, TileCoord targetCoord)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (battleState.Phase != PhaseType.Main)
            {
                throw new InvalidOperationException("Attacks can only be declared during the main phase.");
            }

            if (battleState.ActivePlayerId != attackerOwnerId)
            {
                throw new InvalidOperationException("Only the active player can attack.");
            }

            var attackerBoard = battleState.GetBoard(attackerOwnerId);
            var defenderBoard = battleState.GetOpponentBoard(attackerOwnerId);

            var attacker = attackerBoard.GetOccupant(attackerCoord);
            if (attacker == null)
            {
                throw new InvalidOperationException("Attacker tile is empty.");
            }

            var defender = defenderBoard.GetOccupant(targetCoord);
            if (defender == null)
            {
                throw new InvalidOperationException("Defender tile is empty.");
            }

            ValidateAttacker(attacker, attackerOwnerId);

            if (!_targetingService.CanTarget(battleState, attackerOwnerId, attackerCoord, targetCoord))
            {
                throw new InvalidOperationException("Target is not legal for this attacker.");
            }

            // A legal attack declaration reveals Hiding before any hit or counterattack resolves.
            attacker.RevealHiding();
            var combatResult = ResolveCombat(battleState, defenderBoard, targetCoord, attacker, defender);

            attacker.RemainingAttacksThisTurn -= 1;

            ResolveMasterDefeat(battleState, attacker, combatResult.AffectedDefenders);
            RemoveIfDefeated(attackerBoard, attackerCoord, attacker);
            RemoveDefeatedOccupants(defenderBoard, combatResult.AffectedDefenders);
        }

        private static void ValidateAttacker(OccupantState attacker, PlayerId attackerOwnerId)
        {
            if (attacker.OwnerId != attackerOwnerId)
            {
                throw new InvalidOperationException("Cannot attack with an enemy occupant.");
            }

            if (attacker.CannotAttack)
            {
                throw new InvalidOperationException("This occupant cannot attack while drained.");
            }

            if (attacker.HasSummoningSickness)
            {
                throw new InvalidOperationException("This occupant cannot attack on the turn it was summoned.");
            }

            if (attacker.RemainingAttacksThisTurn <= 0)
            {
                throw new InvalidOperationException("This occupant has no attacks remaining this turn.");
            }

            if (attacker.Attack <= 0)
            {
                throw new InvalidOperationException("Occupants with 0 attack cannot attack.");
            }

            if (attacker is BuildingState buildingState && !buildingState.CanAttackAsBuilding)
            {
                throw new InvalidOperationException("This building cannot attack.");
            }
        }

        private static CombatResolutionResult ResolveCombat(
            BattleState battleState,
            BoardState defenderBoard,
            TileCoord declaredTargetCoord,
            OccupantState attacker,
            OccupantState declaredDefender)
        {
            var affectedDefenders = new HashSet<OccupantState>();
            var hitsPerAttack = attacker.EffectiveHitsPerAttack;
            var attackerDamagePerHit = DamageResolutionRules.GetDamageAgainst(attacker, declaredDefender);
            var attackerDamageType = attacker.DamageType;
            var initialGuardInfo = GuardService.ResolveForNormalAttack(
                defenderBoard,
                declaredTargetCoord,
                attacker);
            var counterattacker = initialGuardInfo.IsProtected
                ? initialGuardInfo.Guard
                : declaredDefender;
            var defenderCounterDamage = DamageResolutionRules.GetDamageAgainst(counterattacker, attacker);
            var defenderCounterDamageType = counterattacker.DamageType;
            var defenderCanCounterattack =
                attacker.AttackType == AttackType.Melee &&
                counterattacker.AttackType == AttackType.Melee &&
                !counterattacker.CannotCounterattack &&
                defenderCounterDamage > 0;

            if (defenderCanCounterattack)
            {
                for (var hitIndex = 0; hitIndex < hitsPerAttack - 1; hitIndex++)
                {
                    ApplyAttackDamageToDeclaredTarget(
                        battleState,
                        defenderBoard,
                        declaredTargetCoord,
                        attacker,
                        attackerDamagePerHit,
                        attackerDamageType,
                        affectedDefenders);
                }

                ApplyFinalMeleeHitWithCounterattack(
                    battleState,
                    defenderBoard,
                    declaredTargetCoord,
                    attacker,
                    counterattacker,
                    attackerDamagePerHit,
                    attackerDamageType,
                    defenderCounterDamage,
                    defenderCounterDamageType,
                    affectedDefenders);
                return new CombatResolutionResult(affectedDefenders);
            }

            for (var hitIndex = 0; hitIndex < hitsPerAttack; hitIndex++)
            {
                ApplyAttackDamageToDeclaredTarget(
                    battleState,
                    defenderBoard,
                    declaredTargetCoord,
                    attacker,
                    attackerDamagePerHit,
                    attackerDamageType,
                    affectedDefenders);
            }

            return new CombatResolutionResult(affectedDefenders);
        }

        private static void ApplyAttackDamageToDeclaredTarget(
            BattleState battleState,
            BoardState defenderBoard,
            TileCoord declaredTargetCoord,
            OccupantState attacker,
            int attackerDamagePerHit,
            DamageType attackerDamageType,
            ISet<OccupantState> affectedDefenders)
        {
            var guardInfo = GuardService.ResolveForNormalAttack(
                defenderBoard,
                declaredTargetCoord,
                attacker);
            if (guardInfo.IsProtected)
            {
                affectedDefenders.Add(guardInfo.Guard);
                var guardDamage = attackerDamagePerHit;
                var guardHpBefore = Math.Max(0, guardInfo.Guard.CurrentHp);
                var resolvedGuardDamage = DamageResolutionRules.ResolveIncomingDamage(
                    guardInfo.Guard,
                    guardDamage,
                    attackerDamageType);
                var absorbedDamage = DamageResolutionRules.ApplyAttackDamage(
                    guardInfo.Guard,
                    guardDamage,
                    attackerDamageType);
                BattleValuePopupRecorder.RecordDamage(
                    battleState,
                    guardInfo.Guard,
                    absorbedDamage,
                    BattleValueChangeCause.NormalAttack,
                    attackerDamageType,
                    attacker.OwnerId,
                    attacker.RuntimeId,
                    attacker.CardId);
                ApplyAttackHitEffects(battleState, attacker, absorbedDamage);
                var remainingDamage = Math.Max(0, resolvedGuardDamage - guardHpBefore);
                if (remainingDamage > 0)
                {
                    affectedDefenders.Add(guardInfo.OriginalTarget);
                    var overflowDamage = DamageResolutionRules.ApplyAttackDamage(
                        guardInfo.OriginalTarget,
                        remainingDamage,
                        attackerDamageType);
                    BattleValuePopupRecorder.RecordDamage(
                        battleState,
                        guardInfo.OriginalTarget,
                        overflowDamage,
                        BattleValueChangeCause.NormalAttack,
                        attackerDamageType,
                        attacker.OwnerId,
                        attacker.RuntimeId,
                        attacker.CardId);
                    ApplyAttackHitEffects(battleState, attacker, overflowDamage);
                }

                return;
            }

            affectedDefenders.Add(guardInfo.OriginalTarget);
            ApplyDirectAttackDamageAndEffects(
                battleState,
                guardInfo.OriginalTarget,
                attacker,
                attackerDamagePerHit,
                attackerDamageType);
        }

        private static void ApplyDirectAttackDamageAndEffects(
            BattleState battleState,
            OccupantState defender,
            OccupantState attacker,
            int attackerDamagePerHit,
            DamageType attackerDamageType)
        {
            var actualDamage = DamageResolutionRules.ApplyAttackDamage(
                defender,
                attackerDamagePerHit,
                attackerDamageType);
            BattleValuePopupRecorder.RecordDamage(
                battleState,
                defender,
                actualDamage,
                BattleValueChangeCause.NormalAttack,
                attackerDamageType,
                attacker.OwnerId,
                attacker.RuntimeId,
                attacker.CardId);
            ApplyAttackHitEffects(battleState, attacker, actualDamage);
        }

        private static void ApplyFinalMeleeHitWithCounterattack(
            BattleState battleState,
            BoardState defenderBoard,
            TileCoord declaredTargetCoord,
            OccupantState attacker,
            OccupantState counterattacker,
            int attackerDamagePerHit,
            DamageType attackerDamageType,
            int defenderCounterDamage,
            DamageType defenderCounterDamageType,
            ISet<OccupantState> affectedDefenders)
        {
            var damageDealtByAttacker = ApplyAttackDamageToDeclaredTargetWithoutHitEffects(
                battleState,
                defenderBoard,
                declaredTargetCoord,
                attacker,
                attackerDamagePerHit,
                attackerDamageType,
                affectedDefenders);
            var counterattackDamage = DamageResolutionRules.ApplyAttackDamage(
                attacker,
                defenderCounterDamage,
                defenderCounterDamageType);
            BattleValuePopupRecorder.RecordDamage(
                battleState,
                attacker,
                counterattackDamage,
                BattleValueChangeCause.Counterattack,
                defenderCounterDamageType,
                counterattacker.OwnerId,
                counterattacker.RuntimeId,
                counterattacker.CardId);

            ApplyAttackHitEffectsIfAlive(battleState, attacker, damageDealtByAttacker);
            ApplyAttackHitEffectsIfAlive(battleState, counterattacker, counterattackDamage);
        }

        private static int ApplyAttackDamageToDeclaredTargetWithoutHitEffects(
            BattleState battleState,
            BoardState defenderBoard,
            TileCoord declaredTargetCoord,
            OccupantState attacker,
            int attackerDamagePerHit,
            DamageType attackerDamageType,
            ISet<OccupantState> affectedDefenders)
        {
            var guardInfo = GuardService.ResolveForNormalAttack(
                defenderBoard,
                declaredTargetCoord,
                attacker);
            if (!guardInfo.IsProtected)
            {
                affectedDefenders.Add(guardInfo.OriginalTarget);
                var directDamage = DamageResolutionRules.ApplyAttackDamage(
                    guardInfo.OriginalTarget,
                    attackerDamagePerHit,
                    attackerDamageType);
                BattleValuePopupRecorder.RecordDamage(
                    battleState,
                    guardInfo.OriginalTarget,
                    directDamage,
                    BattleValueChangeCause.NormalAttack,
                    attackerDamageType,
                    attacker.OwnerId,
                    attacker.RuntimeId,
                    attacker.CardId);
                return directDamage;
            }

            affectedDefenders.Add(guardInfo.Guard);
            var guardHpBefore = Math.Max(0, guardInfo.Guard.CurrentHp);
            var resolvedGuardDamage = DamageResolutionRules.ResolveIncomingDamage(
                guardInfo.Guard,
                attackerDamagePerHit,
                attackerDamageType);
            var absorbedDamage = DamageResolutionRules.ApplyAttackDamage(
                guardInfo.Guard,
                attackerDamagePerHit,
                attackerDamageType);
            BattleValuePopupRecorder.RecordDamage(
                battleState,
                guardInfo.Guard,
                absorbedDamage,
                BattleValueChangeCause.NormalAttack,
                attackerDamageType,
                attacker.OwnerId,
                attacker.RuntimeId,
                attacker.CardId);

            var totalDamage = absorbedDamage;
            var remainingDamage = Math.Max(0, resolvedGuardDamage - guardHpBefore);
            if (remainingDamage <= 0)
            {
                return totalDamage;
            }

            affectedDefenders.Add(guardInfo.OriginalTarget);
            var overflowDamage = DamageResolutionRules.ApplyAttackDamage(
                guardInfo.OriginalTarget,
                remainingDamage,
                attackerDamageType);
            BattleValuePopupRecorder.RecordDamage(
                battleState,
                guardInfo.OriginalTarget,
                overflowDamage,
                BattleValueChangeCause.NormalAttack,
                attackerDamageType,
                attacker.OwnerId,
                attacker.RuntimeId,
                attacker.CardId);
            return totalDamage + overflowDamage;
        }

        private static void ApplyAttackHitEffectsIfAlive(BattleState battleState, OccupantState attacker, int actualDamageDealt)
        {
            if (attacker == null || attacker.CurrentHp <= 0)
            {
                return;
            }

            ApplyAttackHitEffects(battleState, attacker, actualDamageDealt);
        }

        private static void ApplyAttackHitEffects(BattleState battleState, OccupantState attacker, int actualDamageDealt)
        {
            if (attacker == null || actualDamageDealt <= 0)
            {
                return;
            }

            if (attacker.HasLifeSteal && !attacker.EffectsSuppressed)
            {
                BattleValuePopupRecorder.HealAndRecord(
                    battleState,
                    attacker,
                    actualDamageDealt,
                    BattleValueChangeCause.LifeSteal,
                    attacker.OwnerId,
                    attacker.RuntimeId,
                    attacker.CardId);
            }
        }

        private static void RemoveDefeatedOccupants(BoardState board, IEnumerable<OccupantState> occupants)
        {
            foreach (var occupant in occupants)
            {
                if (occupant == null || occupant.Kind == OccupantKind.Master || occupant.CurrentHp > 0)
                {
                    continue;
                }

                if (board.GetOccupant(occupant.Position) == occupant)
                {
                    board.Remove(occupant.Position);
                }
            }
        }

        private static void RemoveIfDefeated(BoardState board, TileCoord coord, OccupantState occupant)
        {
            if (occupant.CurrentHp <= 0 && board.GetOccupant(coord) == occupant)
            {
                board.Remove(coord);
            }
        }

        private static void ResolveMasterDefeat(BattleState battleState, OccupantState attacker, IEnumerable<OccupantState> defenders)
        {
            var attackerMasterDefeated = attacker.Kind == OccupantKind.Master && attacker.CurrentHp <= 0;
            var defenderMasterDefeated = false;
            foreach (var defender in defenders)
            {
                if (defender != null && defender.Kind == OccupantKind.Master && defender.CurrentHp <= 0)
                {
                    defenderMasterDefeated = true;
                    break;
                }
            }

            if (attackerMasterDefeated && defenderMasterDefeated)
            {
                throw new InvalidOperationException(
                    "Simultaneous Master defeat is not defined by the current rules.");
            }

            if (defenderMasterDefeated)
            {
                battleState.EndBattle(attacker.OwnerId);
                return;
            }

            if (attackerMasterDefeated)
            {
                battleState.EndBattle(battleState.GetOpponent(attacker.OwnerId).Id);
            }
        }

        private sealed class CombatResolutionResult
        {
            public CombatResolutionResult(IReadOnlyCollection<OccupantState> affectedDefenders)
            {
                AffectedDefenders = affectedDefenders;
            }

            public IReadOnlyCollection<OccupantState> AffectedDefenders { get; }
        }
    }
}
