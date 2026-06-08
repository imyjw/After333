using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    public sealed class AttackService
    {
        private const string VampireCardId = "Vampire";

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

            if (attacker.IsDisabled)
            {
                throw new InvalidOperationException("Disabled occupants cannot attack.");
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
            var hitsPerAttack = attacker.HitsPerAttack < 1 ? 1 : attacker.HitsPerAttack;
            var defenderCanCounterattack =
                attacker.AttackType == AttackType.Melee &&
                declaredDefender.AttackType == AttackType.Melee &&
                !declaredDefender.IsDisabled &&
                declaredDefender.Attack > 0;

            if (defenderCanCounterattack)
            {
                affectedDefenders.Add(declaredDefender);
                for (var hitIndex = 0; hitIndex < hitsPerAttack - 1; hitIndex++)
                {
                    ApplyDirectAttackDamageAndEffects(battleState, declaredDefender, attacker);
                }

                ApplyFinalMeleeHitWithCounterattack(battleState, attacker, declaredDefender);
                return new CombatResolutionResult(affectedDefenders);
            }

            for (var hitIndex = 0; hitIndex < hitsPerAttack; hitIndex++)
            {
                ApplyAttackDamageToDeclaredTarget(battleState, defenderBoard, declaredTargetCoord, attacker, affectedDefenders);
            }

            return new CombatResolutionResult(affectedDefenders);
        }

        private static void ApplyAttackDamageToDeclaredTarget(
            BattleState battleState,
            BoardState defenderBoard,
            TileCoord declaredTargetCoord,
            OccupantState attacker,
            ISet<OccupantState> affectedDefenders)
        {
            var guardInfo = GuardService.Resolve(defenderBoard, declaredTargetCoord);
            if (guardInfo.IsProtected)
            {
                affectedDefenders.Add(guardInfo.Guard);
                var guardDamage = DamageResolutionRules.GetDamageAgainst(attacker, guardInfo.Guard);
                var absorbedDamage = DamageResolutionRules.ApplyAttackDamage(guardInfo.Guard, guardDamage);
                BattleValuePopupRecorder.RecordDamage(battleState, guardInfo.Guard, absorbedDamage);
                ApplyAttackHitEffects(battleState, attacker, absorbedDamage);
                var remainingDamage = Math.Max(0, guardDamage - absorbedDamage);
                if (remainingDamage > 0)
                {
                    affectedDefenders.Add(guardInfo.OriginalTarget);
                    var overflowDamage = DamageResolutionRules.ApplyAttackDamage(guardInfo.OriginalTarget, remainingDamage);
                    BattleValuePopupRecorder.RecordDamage(battleState, guardInfo.OriginalTarget, overflowDamage);
                    ApplyAttackHitEffects(battleState, attacker, overflowDamage);
                }

                return;
            }

            affectedDefenders.Add(guardInfo.OriginalTarget);
            ApplyDirectAttackDamageAndEffects(battleState, guardInfo.OriginalTarget, attacker);
        }

        private static void ApplyDirectAttackDamageAndEffects(BattleState battleState, OccupantState defender, OccupantState attacker)
        {
            var damage = DamageResolutionRules.GetDamageAgainst(attacker, defender);
            var actualDamage = DamageResolutionRules.ApplyAttackDamage(defender, damage);
            BattleValuePopupRecorder.RecordDamage(battleState, defender, actualDamage);
            ApplyAttackHitEffects(battleState, attacker, actualDamage);
        }

        private static void ApplyFinalMeleeHitWithCounterattack(BattleState battleState, OccupantState attacker, OccupantState defender)
        {
            var attackerDamage = DamageResolutionRules.GetDamageAgainst(attacker, defender);
            var defenderDamage = DamageResolutionRules.GetDamageAgainst(defender, attacker);

            var damageDealtByAttacker = DamageResolutionRules.ApplyAttackDamage(defender, attackerDamage);
            BattleValuePopupRecorder.RecordDamage(battleState, defender, damageDealtByAttacker);
            ApplyAttackHitEffects(battleState, attacker, damageDealtByAttacker);
            var counterattackDamage = DamageResolutionRules.ApplyAttackDamage(attacker, defenderDamage);
            BattleValuePopupRecorder.RecordDamage(battleState, attacker, counterattackDamage);
        }

        private static void ApplyAttackHitEffects(BattleState battleState, OccupantState attacker, int actualDamageDealt)
        {
            if (attacker == null || actualDamageDealt <= 0)
            {
                return;
            }

            if (string.Equals(attacker.CardId, VampireCardId, StringComparison.Ordinal))
            {
                BattleValuePopupRecorder.HealAndRecord(battleState, attacker, actualDamageDealt);
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
