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
        private readonly Func<int, int> _randomIndexSelector;

        public AttackService()
            : this(new TargetingService(), null)
        {
        }

        public AttackService(TargetingService targetingService)
            : this(targetingService, null)
        {
        }

        public AttackService(
            TargetingService targetingService,
            Func<int, int> randomIndexSelector)
        {
            _targetingService = targetingService ?? throw new ArgumentNullException(nameof(targetingService));
            if (randomIndexSelector != null)
            {
                _randomIndexSelector = randomIndexSelector;
                return;
            }

            var random = new Random();
            _randomIndexSelector = random.Next;
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
            var declaredDefenderOwnerId = battleState.GetOpponent(attackerOwnerId).Id;
            var declaredDefenderBoard = battleState.GetBoard(declaredDefenderOwnerId);

            var attacker = attackerBoard.GetOccupant(attackerCoord);
            if (attacker == null)
            {
                throw new InvalidOperationException("Attacker tile is empty.");
            }

            var declaredDefender = declaredDefenderBoard.GetOccupant(targetCoord);
            if (declaredDefender == null)
            {
                throw new InvalidOperationException("Defender tile is empty.");
            }

            ValidateAttacker(attacker, attackerOwnerId);

            if (!_targetingService.CanTarget(battleState, attackerOwnerId, attackerCoord, targetCoord))
            {
                throw new InvalidOperationException("Target is not legal for this attacker.");
            }

            var resolvedTarget = ResolveAttackTarget(
                battleState,
                attacker,
                declaredDefenderOwnerId,
                targetCoord,
                declaredDefender);
            var defenderBoard = battleState.GetBoard(resolvedTarget.OwnerId);
            var defender = resolvedTarget.Occupant;
            battleState.RecordAttackResolution(new BattleAttackResolution(
                attackerOwnerId,
                attackerCoord,
                attacker.RuntimeId,
                attacker.CardId,
                declaredDefenderOwnerId,
                targetCoord,
                resolvedTarget.OwnerId,
                resolvedTarget.Coord,
                defender.RuntimeId,
                defender.CardId,
                resolvedTarget.WasHuanShuRedirected));

            // A legal attack declaration reveals Hiding before any hit or counterattack resolves.
            attacker.RevealHiding();
            ResolveCombat(
                battleState,
                defenderBoard,
                resolvedTarget.Coord,
                attacker,
                defender);

            attacker.RemainingAttacksThisTurn -= 1;
            OccupantDestructionService.ResolveDefeatedOccupants(battleState);
        }

        private ResolvedAttackTarget ResolveAttackTarget(
            BattleState battleState,
            OccupantState attacker,
            PlayerId declaredDefenderOwnerId,
            TileCoord declaredTargetCoord,
            OccupantState declaredDefender)
        {
            if (!attacker.IsUnderHuanShu)
            {
                return new ResolvedAttackTarget(
                    declaredDefenderOwnerId,
                    declaredTargetCoord,
                    declaredDefender,
                    wasHuanShuRedirected: false);
            }

            var candidates = new List<ResolvedAttackTarget>();
            AddHuanShuCandidates(
                battleState,
                PlayerId.Player,
                attacker,
                declaredDefenderOwnerId,
                declaredTargetCoord,
                candidates);
            AddHuanShuCandidates(
                battleState,
                PlayerId.AI,
                attacker,
                declaredDefenderOwnerId,
                declaredTargetCoord,
                candidates);

            if (candidates.Count == 0)
            {
                throw new InvalidOperationException("HuanShu attack has no legal random target.");
            }

            var selectedIndex = _randomIndexSelector(candidates.Count);
            if (selectedIndex < 0 || selectedIndex >= candidates.Count)
            {
                throw new InvalidOperationException("HuanShu random target selector returned an invalid index.");
            }

            return candidates[selectedIndex];
        }

        private static void AddHuanShuCandidates(
            BattleState battleState,
            PlayerId boardOwnerId,
            OccupantState attacker,
            PlayerId declaredDefenderOwnerId,
            TileCoord declaredTargetCoord,
            ICollection<ResolvedAttackTarget> candidates)
        {
            var board = battleState.GetBoard(boardOwnerId);
            foreach (var candidate in board.EnumerateOccupants())
            {
                if (!CanBeHuanShuRandomTarget(attacker, candidate))
                {
                    continue;
                }

                var wasRedirected = boardOwnerId != declaredDefenderOwnerId ||
                                    candidate.Position != declaredTargetCoord;
                candidates.Add(new ResolvedAttackTarget(
                    boardOwnerId,
                    candidate.Position,
                    candidate,
                    wasRedirected));
            }
        }

        private static bool CanBeHuanShuRandomTarget(
            OccupantState attacker,
            OccupantState candidate)
        {
            if (candidate == null ||
                ReferenceEquals(attacker, candidate) ||
                !candidate.IsAlive ||
                !candidate.CanBeAffected)
            {
                return false;
            }

            if (candidate.OwnerId != attacker.OwnerId && candidate.IsHiding)
            {
                return false;
            }

            return true;
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
            var initialShielderInfo = ShielderService.ResolveForNormalAttack(
                defenderBoard,
                declaredTargetCoord,
                attacker);
            var counterattacker = initialShielderInfo.IsProtected
                ? initialShielderInfo.Shielder
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
            var shielderInfo = ShielderService.ResolveForNormalAttack(
                defenderBoard,
                declaredTargetCoord,
                attacker);
            if (shielderInfo.IsProtected)
            {
                affectedDefenders.Add(shielderInfo.Shielder);
                var shielderDamage = ResolveDeclaredTargetDamage(
                    attacker, shielderInfo.OriginalTarget, attackerDamagePerHit, attackerDamageType);
                var shielderHpBefore = Math.Max(0, shielderInfo.Shielder.CurrentHp);
                var resolvedShielderDamage = DamageResolutionRules.ResolveIncomingDamage(
                    shielderInfo.Shielder,
                    shielderDamage,
                    attackerDamageType);
                var absorbedDamage = DamageResolutionRules.ApplyAttackDamage(
                    shielderInfo.Shielder,
                    shielderDamage,
                    attackerDamageType);
                BattleValuePopupRecorder.RecordDamage(
                    battleState,
                    shielderInfo.Shielder,
                    absorbedDamage,
                    BattleValueChangeCause.NormalAttack,
                    attackerDamageType,
                    attacker.OwnerId,
                    attacker.RuntimeId,
                    attacker.CardId);
                ApplyAttackHitEffects(battleState, attacker, absorbedDamage);
                var remainingDamage = Math.Max(0, resolvedShielderDamage - shielderHpBefore);
                if (remainingDamage > 0)
                {
                    affectedDefenders.Add(shielderInfo.OriginalTarget);
                    var overflowDamage = DamageResolutionRules.ApplyResolvedAttackDamage(
                        shielderInfo.OriginalTarget,
                        remainingDamage);
                    BattleValuePopupRecorder.RecordDamage(
                        battleState,
                        shielderInfo.OriginalTarget,
                        overflowDamage,
                        BattleValueChangeCause.NormalAttack,
                        attackerDamageType,
                        attacker.OwnerId,
                        attacker.RuntimeId,
                        attacker.CardId);
                    ApplyAttackHitEffects(battleState, attacker, overflowDamage);
                }

                var piercingDamage = ApplyPiercingDamageWithoutHitEffects(
                    battleState,
                    defenderBoard,
                    declaredTargetCoord,
                    attacker,
                    attackerDamagePerHit,
                    attackerDamageType,
                    affectedDefenders);
                ApplyAttackHitEffects(battleState, attacker, piercingDamage);

                return;
            }

            affectedDefenders.Add(shielderInfo.OriginalTarget);
            ApplyDirectAttackDamageAndEffects(
                battleState,
                shielderInfo.OriginalTarget,
                attacker,
                attackerDamagePerHit,
                attackerDamageType);
            var directPiercingDamage = ApplyPiercingDamageWithoutHitEffects(
                battleState,
                defenderBoard,
                declaredTargetCoord,
                attacker,
                attackerDamagePerHit,
                attackerDamageType,
                affectedDefenders);
            ApplyAttackHitEffects(battleState, attacker, directPiercingDamage);
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
                attackerDamageType,
                DamageResolutionRules.ShouldHalveForFlying(attacker, defender));
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
            var damageDealtByAttacker = ApplyAttackDamagePacketWithoutHitEffects(
                battleState,
                defenderBoard,
                declaredTargetCoord,
                attacker,
                attackerDamagePerHit,
                attackerDamageType,
                affectedDefenders);
            damageDealtByAttacker += ApplyPiercingDamageWithoutHitEffects(
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

        private static int ApplyPiercingDamageWithoutHitEffects(
            BattleState battleState,
            BoardState defenderBoard,
            TileCoord resolvedTargetCoord,
            OccupantState attacker,
            int attackerDamagePerHit,
            DamageType attackerDamageType,
            ISet<OccupantState> affectedDefenders)
        {
            if (attacker == null ||
                !attacker.HasActivePiercing ||
                defenderBoard == null ||
                (resolvedTargetCoord.Row != 0 && resolvedTargetCoord.Row != 1))
            {
                return 0;
            }

            var oppositeRowCoord = new TileCoord(
                resolvedTargetCoord.Column,
                resolvedTargetCoord.Row == 0 ? 1 : 0);
            var oppositeRowOccupant = defenderBoard.GetOccupant(oppositeRowCoord);
            if (oppositeRowOccupant == null || !oppositeRowOccupant.IsAlive || oppositeRowOccupant.IsSealbound)
            {
                return 0;
            }

            // Reuse attack redirection without declaring another attack or triggering another counterattack.
            return ApplyAttackDamagePacketWithoutHitEffects(
                battleState,
                defenderBoard,
                oppositeRowCoord,
                attacker,
                attackerDamagePerHit,
                attackerDamageType,
                affectedDefenders,
                BattleValueChangeCause.Piercing);
        }

        private static int ApplyAttackDamagePacketWithoutHitEffects(
            BattleState battleState,
            BoardState defenderBoard,
            TileCoord declaredTargetCoord,
            OccupantState attacker,
            int attackerDamagePerHit,
            DamageType attackerDamageType,
            ISet<OccupantState> affectedDefenders,
            BattleValueChangeCause damageCause = BattleValueChangeCause.NormalAttack)
        {
            var shielderInfo = ShielderService.ResolveForNormalAttack(
                defenderBoard,
                declaredTargetCoord,
                attacker);
            if (!shielderInfo.IsProtected)
            {
                affectedDefenders.Add(shielderInfo.OriginalTarget);
                var directDamage = DamageResolutionRules.ApplyAttackDamage(
                    shielderInfo.OriginalTarget,
                    attackerDamagePerHit,
                    attackerDamageType,
                    DamageResolutionRules.ShouldHalveForFlying(attacker, shielderInfo.OriginalTarget));
                BattleValuePopupRecorder.RecordDamage(
                    battleState,
                    shielderInfo.OriginalTarget,
                    directDamage,
                    damageCause,
                    attackerDamageType,
                    attacker.OwnerId,
                    attacker.RuntimeId,
                    attacker.CardId);
                return directDamage;
            }

            affectedDefenders.Add(shielderInfo.Shielder);
            var shielderDamage = ResolveDeclaredTargetDamage(
                attacker, shielderInfo.OriginalTarget, attackerDamagePerHit, attackerDamageType);
            var shielderHpBefore = Math.Max(0, shielderInfo.Shielder.CurrentHp);
            var resolvedShielderDamage = DamageResolutionRules.ResolveIncomingDamage(
                shielderInfo.Shielder,
                shielderDamage,
                attackerDamageType);
            var absorbedDamage = DamageResolutionRules.ApplyAttackDamage(
                shielderInfo.Shielder,
                shielderDamage,
                attackerDamageType);
            BattleValuePopupRecorder.RecordDamage(
                battleState,
                shielderInfo.Shielder,
                absorbedDamage,
                damageCause,
                attackerDamageType,
                attacker.OwnerId,
                attacker.RuntimeId,
                attacker.CardId);

            var totalDamage = absorbedDamage;
            var remainingDamage = Math.Max(0, resolvedShielderDamage - shielderHpBefore);
            if (remainingDamage <= 0)
            {
                return totalDamage;
            }

            affectedDefenders.Add(shielderInfo.OriginalTarget);
            var overflowDamage = DamageResolutionRules.ApplyResolvedAttackDamage(
                shielderInfo.OriginalTarget,
                remainingDamage);
            BattleValuePopupRecorder.RecordDamage(
                battleState,
                shielderInfo.OriginalTarget,
                overflowDamage,
                damageCause,
                attackerDamageType,
                attacker.OwnerId,
                attacker.RuntimeId,
                attacker.CardId);
            return totalDamage + overflowDamage;
        }

        private static int ResolveDeclaredTargetDamage(
            OccupantState attacker,
            OccupantState target,
            int rawDamage,
            DamageType damageType)
        {
            // Redirect the target's resolved packet; the Shielder's Flying flag never reduces it again.
            return DamageResolutionRules.ResolveIncomingDamage(
                target, rawDamage, damageType, DamageResolutionRules.ShouldHalveForFlying(attacker, target));
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

        private readonly struct ResolvedAttackTarget
        {
            public ResolvedAttackTarget(
                PlayerId ownerId,
                TileCoord coord,
                OccupantState occupant,
                bool wasHuanShuRedirected)
            {
                OwnerId = ownerId;
                Coord = coord;
                Occupant = occupant;
                WasHuanShuRedirected = wasHuanShuRedirected;
            }

            public PlayerId OwnerId { get; }
            public TileCoord Coord { get; }
            public OccupantState Occupant { get; }
            public bool WasHuanShuRedirected { get; }
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
