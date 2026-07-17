using System;
using System.Collections.Generic;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Services
{
    public sealed class AiDecisionService
    {
        private static readonly TileCoord[] CoordPriority =
        {
            new TileCoord(2, 1),
            new TileCoord(2, 0),
            new TileCoord(1, 1),
            new TileCoord(1, 0),
            new TileCoord(3, 1),
            new TileCoord(3, 0),
            new TileCoord(0, 1),
            new TileCoord(0, 0),
            new TileCoord(4, 1),
            new TileCoord(4, 0),
        };

        private readonly ICardDefinitionProvider _cardDefinitionProvider;
        private readonly TargetingService _targetingService;
        private readonly ICardUpgradeLevelProvider _cardUpgradeLevelProvider;

        public AiDecisionService(ICardDefinitionProvider cardDefinitionProvider)
            : this(cardDefinitionProvider, new TargetingService(), ZeroCardUpgradeLevelProvider.Instance)
        {
        }

        public AiDecisionService(
            ICardDefinitionProvider cardDefinitionProvider,
            ICardUpgradeLevelProvider cardUpgradeLevelProvider)
            : this(cardDefinitionProvider, new TargetingService(), cardUpgradeLevelProvider)
        {
        }

        public AiDecisionService(
            ICardDefinitionProvider cardDefinitionProvider,
            TargetingService targetingService)
            : this(cardDefinitionProvider, targetingService, ZeroCardUpgradeLevelProvider.Instance)
        {
        }

        public AiDecisionService(
            ICardDefinitionProvider cardDefinitionProvider,
            TargetingService targetingService,
            ICardUpgradeLevelProvider cardUpgradeLevelProvider)
        {
            _cardDefinitionProvider = cardDefinitionProvider ?? throw new ArgumentNullException(nameof(cardDefinitionProvider));
            _targetingService = targetingService ?? throw new ArgumentNullException(nameof(targetingService));
            _cardUpgradeLevelProvider = cardUpgradeLevelProvider ?? ZeroCardUpgradeLevelProvider.Instance;
        }

        public IBattleCommand GetNextCommand(BattleState battleState)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (battleState.ActivePlayerId != PlayerId.AI)
            {
                throw new InvalidOperationException("AI decisions can only be requested during the AI turn.");
            }

            if (battleState.Phase != PhaseType.Main)
            {
                throw new InvalidOperationException("AI main-phase decisions require the Main phase.");
            }

            var attackCommand = GetBestAttackCommand(battleState);
            if (attackCommand != null)
            {
                return attackCommand;
            }

            var damageSpellCommand = GetBestKillDamageSpellCommand(battleState);
            if (damageSpellCommand != null)
            {
                return damageSpellCommand;
            }

            var resourceCardCommand = GetBestResourceCardCommand(battleState);
            if (resourceCardCommand != null)
            {
                return resourceCardCommand;
            }

            var immediateAttackCardCommand = GetBestImmediateAttackCardCommand(battleState);
            if (immediateAttackCardCommand != null)
            {
                return immediateAttackCardCommand;
            }

            var summonCommand = GetBestGeneralSummonCommand(battleState);
            if (summonCommand != null)
            {
                return summonCommand;
            }

            return new EndTurnCommand();
        }

        private AttackCommand GetBestAttackCommand(BattleState battleState)
        {
            AttackCandidate bestCandidate = null;
            var aiBoard = battleState.GetBoard(PlayerId.AI);
            var enemyBoard = battleState.GetBoard(PlayerId.Player);

            foreach (var attackerCoord in CoordPriority)
            {
                var attacker = aiBoard.GetOccupant(attackerCoord);
                if (!CanOccupantAttack(attacker))
                {
                    continue;
                }

                var legalTargets = _targetingService.GetLegalTargets(battleState, PlayerId.AI, attackerCoord);
                foreach (var targetCoord in legalTargets)
                {
                    var defender = enemyBoard.GetOccupant(targetCoord);
                    if (defender == null)
                    {
                        continue;
                    }

                    if (IsProtectedByActiveGuardForNormalAttack(
                            enemyBoard,
                            targetCoord,
                            attacker.AttackType,
                            attacker.HasActiveFlying))
                    {
                        continue;
                    }

                    var candidate = AttackCandidate.From(attackerCoord, attacker, targetCoord, defender);
                    if (bestCandidate == null || CompareAttackCandidates(candidate, bestCandidate) < 0)
                    {
                        bestCandidate = candidate;
                    }
                }
            }

            return bestCandidate == null
                ? null
                : new AttackCommand(bestCandidate.AttackerCoord, bestCandidate.TargetCoord);
        }

        private CastDamageSpellCommand GetBestKillDamageSpellCommand(BattleState battleState)
        {
            SpellCandidate bestCandidate = null;
            var aiPlayer = battleState.GetPlayer(PlayerId.AI);
            var enemyBoard = battleState.GetBoard(PlayerId.Player);

            foreach (var cardId in aiPlayer.Hand.CardIds)
            {
                if (_cardDefinitionProvider.GetRequired(cardId) is not DamageSpellCardDefinition damageSpellDefinition)
                {
                    continue;
                }

                if (!aiPlayer.Resources.CanAfford(damageSpellDefinition.Cost))
                {
                    continue;
                }

                foreach (var targetCoord in CoordPriority)
                {
                    var target = enemyBoard.GetOccupant(targetCoord);
                    if (target == null || target.IsSealbound || target.IsHiding)
                    {
                        continue;
                    }

                    if (IsProtectedByActiveGuard(enemyBoard, targetCoord))
                    {
                        continue;
                    }

                    var cardLevelDamage = CardLevelSpellRules.ApplyDamageBonus(
                        damageSpellDefinition.Damage,
                        _cardUpgradeLevelProvider.GetUpgradeLevel(PlayerId.AI, damageSpellDefinition.CardId));
                    var rawDamage = SpellPowerRules.ApplyCurrent(
                        battleState,
                        PlayerId.AI,
                        cardLevelDamage,
                        damageSpellDefinition.DamageType);
                    var damage = DamageResolutionRules.ResolveIncomingDamage(
                        target,
                        rawDamage,
                        damageSpellDefinition.DamageType);

                    if (damage < target.CurrentHp)
                    {
                        continue;
                    }

                    var candidate = SpellCandidate.From(cardId, targetCoord, target);
                    if (bestCandidate == null || CompareSpellCandidates(candidate, bestCandidate) < 0)
                    {
                        bestCandidate = candidate;
                    }
                }
            }

            return bestCandidate == null
                ? null
                : new CastDamageSpellCommand(bestCandidate.CardId, PlayerId.Player, bestCandidate.TargetCoord);
        }

        private IBattleCommand GetBestResourceCardCommand(BattleState battleState)
        {
            var aiPlayer = battleState.GetPlayer(PlayerId.AI);
            foreach (var cardId in aiPlayer.Hand.CardIds)
            {
                var definition = _cardDefinitionProvider.GetRequired(cardId);
                if (!aiPlayer.Resources.CanAfford(definition.Cost))
                {
                    continue;
                }

                if (!IsResourceProducingCard(definition))
                {
                    continue;
                }

                var summonCommand = TryCreateSummonCommand(battleState, definition);
                if (summonCommand != null)
                {
                    return summonCommand;
                }

                if (definition is PersistentResourceSpellCardDefinition)
                {
                    return new CastPersistentResourceSpellCommand(cardId);
                }
            }

            return null;
        }

        private IBattleCommand GetBestImmediateAttackCardCommand(BattleState battleState)
        {
            var aiPlayer = battleState.GetPlayer(PlayerId.AI);
            foreach (var cardId in aiPlayer.Hand.CardIds)
            {
                var definition = _cardDefinitionProvider.GetRequired(cardId);
                if (!aiPlayer.Resources.CanAfford(definition.Cost))
                {
                    continue;
                }

                if (!CanCardAttackImmediately(definition))
                {
                    continue;
                }

                if (!HasImmediateAttackTarget(battleState, definition))
                {
                    continue;
                }

                var summonCommand = TryCreateSummonCommand(battleState, definition);
                if (summonCommand != null)
                {
                    return summonCommand;
                }
            }

            return null;
        }

        private IBattleCommand GetBestGeneralSummonCommand(BattleState battleState)
        {
            var aiPlayer = battleState.GetPlayer(PlayerId.AI);
            foreach (var cardId in aiPlayer.Hand.CardIds)
            {
                var definition = _cardDefinitionProvider.GetRequired(cardId);
                if (!aiPlayer.Resources.CanAfford(definition.Cost))
                {
                    continue;
                }

                var summonCommand = TryCreateSummonCommand(battleState, definition);
                if (summonCommand != null)
                {
                    return summonCommand;
                }
            }

            return null;
        }

        private static bool IsResourceProducingCard(CardDefinition definition)
        {
            return definition switch
            {
                UnitCardDefinition unitCardDefinition => HasAnyResourceGain(unitCardDefinition.TurnStartResourceGain),
                BuildingCardDefinition buildingCardDefinition => HasAnyResourceGain(buildingCardDefinition.TurnStartResourceGain),
                PersistentResourceSpellCardDefinition persistentResourceSpellCardDefinition => HasAnyResourceGain(persistentResourceSpellCardDefinition.TurnStartResourceGain),
                _ => false,
            };
        }

        private static bool HasAnyResourceGain(ResourceSet resourceSet)
        {
            return resourceSet != null &&
                   (resourceSet.Mana > 0 || resourceSet.Qi > 0 || resourceSet.Power > 0 || resourceSet.Gold > 0);
        }

        private static bool CanCardAttackImmediately(CardDefinition definition)
        {
            return definition switch
            {
                UnitCardDefinition unitCardDefinition =>
                    unitCardDefinition.SealboundOwnerTurnStarts == 0 && unitCardDefinition.HasRush,
                BuildingCardDefinition buildingCardDefinition =>
                    buildingCardDefinition.SealboundOwnerTurnStarts == 0 &&
                    buildingCardDefinition.CanAttack &&
                    buildingCardDefinition.CanAttackOnSummon,
                _ => false,
            };
        }

        private bool HasImmediateAttackTarget(BattleState battleState, CardDefinition definition)
        {
            return GetBestTargetPreferenceForDefinition(battleState, definition) != null;
        }

        private TargetPreference GetBestTargetPreferenceForDefinition(BattleState battleState, CardDefinition definition)
        {
            var enemyBoard = battleState.GetBoard(PlayerId.Player);
            TargetPreference bestPreference = null;

            var attackType = GetAttackType(definition);
            var hasFlying = HasFlying(definition);
            foreach (var targetCoord in GetLegalTargetsForAttackType(
                         battleState,
                         attackType,
                         hasFlying))
            {
                var defender = enemyBoard.GetOccupant(targetCoord);
                if (defender == null)
                {
                    continue;
                }

                var projectedRemainingHp = GetProjectedTargetRemainingHpAfterAttackAction(definition, defender);
                var preference = TargetPreference.FromAttack(targetCoord, defender, projectedRemainingHp <= 0);
                if (bestPreference == null || CompareTargetPreferences(preference, bestPreference) < 0)
                {
                    bestPreference = preference;
                }
            }

            return bestPreference;
        }

        private static IEnumerable<TileCoord> GetLegalTargetsForAttackType(
            BattleState battleState,
            AttackType attackType,
            bool attackerHasFlying)
        {
            var enemyBoard = battleState.GetBoard(PlayerId.Player);

            for (var column = 0; column < BoardState.ColumnCount; column++)
            {
                var frontCoord = new TileCoord(column, 0);
                var backCoord = new TileCoord(column, 1);
                var frontOccupant = enemyBoard.GetOccupant(frontCoord);
                var backOccupant = enemyBoard.GetOccupant(backCoord);

                if (attackType == AttackType.Ranged || attackerHasFlying)
                {
                    if (CanBeTargeted(attackType, attackerHasFlying, frontOccupant))
                    {
                        yield return frontCoord;
                    }

                    if (CanBeTargeted(attackType, attackerHasFlying, backOccupant) &&
                        !IsProtectedByActiveGuardForNormalAttack(
                            enemyBoard,
                            backCoord,
                            attackType,
                            attackerHasFlying))
                    {
                        yield return backCoord;
                    }

                    continue;
                }

                if (CanBeTargeted(attackType, attackerHasFlying, frontOccupant))
                {
                    yield return frontCoord;
                }

                if (CanBeTargeted(attackType, attackerHasFlying, backOccupant) &&
                    !IsFrontRowBlocker(frontOccupant))
                {
                    yield return backCoord;
                }
            }
        }

        private static bool IsProtectedByActiveGuard(BoardState board, TileCoord targetCoord)
        {
            if (board.GetOccupant(targetCoord) == null)
            {
                return false;
            }

            return GuardService.Resolve(board, targetCoord).IsProtected;
        }

        private static bool IsProtectedByActiveGuardForNormalAttack(
            BoardState board,
            TileCoord targetCoord,
            AttackType attackerAttackType,
            bool attackerHasFlying)
        {
            if (board.GetOccupant(targetCoord) == null)
            {
                return false;
            }

            return GuardService.ResolveForNormalAttack(
                board,
                targetCoord,
                attackerAttackType,
                attackerHasFlying).IsProtected;
        }

        private static bool IsFrontRowBlocker(OccupantState frontOccupant)
        {
            if (frontOccupant == null)
            {
                return false;
            }

            return !frontOccupant.DoesNotBlockFrontRow;
        }

        private static bool CanBeTargeted(
            AttackType attackerAttackType,
            bool attackerHasFlying,
            OccupantState occupant)
        {
            return TargetingService.CanBeTargetedByNormalAttack(
                attackerAttackType,
                attackerHasFlying,
                occupant);
        }

        private static AttackType GetAttackType(CardDefinition definition)
        {
            return definition switch
            {
                UnitCardDefinition unitCardDefinition => unitCardDefinition.AttackType,
                BuildingCardDefinition => AttackType.Ranged,
                _ => throw new InvalidOperationException("The specified card does not define an attack type."),
            };
        }

        private static bool HasFlying(CardDefinition definition)
        {
            return definition switch
            {
                UnitCardDefinition unitCardDefinition => unitCardDefinition.HasFlying,
                BuildingCardDefinition buildingCardDefinition => buildingCardDefinition.HasFlying,
                _ => false,
            };
        }

        private static int GetProjectedTargetRemainingHpAfterAttackAction(CardDefinition definition, OccupantState defender)
        {
            return definition switch
            {
                UnitCardDefinition unitCardDefinition => GetProjectedTargetRemainingHpAfterAttackAction(
                    attackPerHit: unitCardDefinition.Attack,
                    damageType: unitCardDefinition.DamageType,
                    attackType: unitCardDefinition.AttackType,
                    hitsPerAttack: unitCardDefinition.HitsPerAttack,
                    defender: defender,
                    defenderCounterAttack: defender.Attack),
                BuildingCardDefinition buildingCardDefinition => GetProjectedTargetRemainingHpAfterAttackAction(
                    attackPerHit: buildingCardDefinition.Attack,
                    damageType: buildingCardDefinition.DamageType,
                    attackType: AttackType.Ranged,
                    hitsPerAttack: 1,
                    defender: defender,
                    defenderCounterAttack: defender.Attack),
                _ => throw new InvalidOperationException("The specified card does not define attack damage."),
            };
        }

        private static int GetProjectedTargetRemainingHpAfterAttackAction(OccupantState attacker, OccupantState defender)
        {
            return GetProjectedTargetRemainingHpAfterAttackAction(
                attackPerHit: attacker.Attack,
                damageType: attacker.DamageType,
                attackType: attacker.AttackType,
                hitsPerAttack: attacker.EffectiveHitsPerAttack,
                defender: defender,
                defenderCounterAttack: defender.Attack);
        }

        private static int GetProjectedTargetRemainingHpAfterAttackAction(
            int attackPerHit,
            DamageType damageType,
            AttackType attackType,
            int hitsPerAttack,
            OccupantState defender,
            int defenderCounterAttack)
        {
            var resolvedHits = hitsPerAttack < 1 ? 1 : hitsPerAttack;
            var projectedTargetHp = defender.CurrentHp;
            var canTriggerEndure = defender.CanTriggerEndure;
            var defenderCanCounterattack =
                attackType == AttackType.Melee &&
                defender.AttackType == AttackType.Melee &&
                !defender.CannotCounterattack &&
                defender.Attack > 0;

            if (defenderCanCounterattack)
            {
                for (var hitIndex = 0; hitIndex < resolvedHits - 1; hitIndex++)
                {
                    projectedTargetHp = ApplyProjectedAttackDamage(
                        projectedTargetHp,
                        attackPerHit,
                        damageType,
                        defender,
                        ref canTriggerEndure);
                }

                projectedTargetHp = ApplyProjectedAttackDamage(
                    projectedTargetHp,
                    attackPerHit,
                    damageType,
                    defender,
                    ref canTriggerEndure);
                return projectedTargetHp;
            }

            for (var hitIndex = 0; hitIndex < resolvedHits; hitIndex++)
            {
                projectedTargetHp = ApplyProjectedAttackDamage(
                    projectedTargetHp,
                    attackPerHit,
                    damageType,
                    defender,
                    ref canTriggerEndure);
            }

            return projectedTargetHp;
        }

        private static int ApplyProjectedAttackDamage(
            int currentHp,
            int damage,
            DamageType damageType,
            OccupantState defender,
            ref bool canTriggerEndure)
        {
            if (defender.IsInvincible)
            {
                return currentHp;
            }

            DamageResolutionRules.ProjectAttackDamageTaken(
                currentHp,
                defender.IsDrained,
                defender.PhysicalDefense,
                defender.MagicDefense,
                canTriggerEndure,
                damage,
                damageType,
                out var remainingHp,
                out var triggeredEndure);
            if (triggeredEndure)
            {
                canTriggerEndure = false;
            }

            return remainingHp;
        }

        private static IBattleCommand TryCreateSummonCommand(BattleState battleState, CardDefinition definition)
        {
            if (definition.CardType != CardType.Unit && definition.CardType != CardType.Building)
            {
                return null;
            }

            var aiBoard = battleState.GetBoard(PlayerId.AI);
            foreach (var coord in CoordPriority)
            {
                if (!aiBoard.IsEmpty(coord))
                {
                    continue;
                }

                return definition.CardType == CardType.Unit
                    ? new PlayUnitCardCommand(definition.CardId, coord)
                    : new PlayBuildingCardCommand(definition.CardId, coord);
            }

            return null;
        }

        private static bool CanOccupantAttack(OccupantState occupant)
        {
            if (occupant == null || occupant.CannotAttack || occupant.HasSummoningSickness || occupant.RemainingAttacksThisTurn <= 0 || occupant.Attack <= 0)
            {
                return false;
            }

            if (occupant is BuildingState buildingState && !buildingState.CanAttackAsBuilding)
            {
                return false;
            }

            return true;
        }

        private static int CompareAttackCandidates(AttackCandidate left, AttackCandidate right)
        {
            var targetComparison = CompareTargetPreferences(left.TargetPreference, right.TargetPreference);
            if (targetComparison != 0)
            {
                return targetComparison;
            }

            var attackerAttackComparison = right.AttackerAttack.CompareTo(left.AttackerAttack);
            if (attackerAttackComparison != 0)
            {
                return attackerAttackComparison;
            }

            return CompareCoordPriority(left.AttackerCoord, right.AttackerCoord);
        }

        private static int CompareSpellCandidates(SpellCandidate left, SpellCandidate right)
        {
            return CompareTargetPreferences(left.TargetPreference, right.TargetPreference);
        }

        private static int CompareTargetPreferences(TargetPreference left, TargetPreference right)
        {
            var categoryComparison = left.Category.CompareTo(right.Category);
            if (categoryComparison != 0)
            {
                return categoryComparison;
            }

            if (left.Category == 2)
            {
                var targetAttackComparison = right.TargetAttack.CompareTo(left.TargetAttack);
                if (targetAttackComparison != 0)
                {
                    return targetAttackComparison;
                }
            }

            if (left.Category == 4)
            {
                var hpComparison = left.TargetHp.CompareTo(right.TargetHp);
                if (hpComparison != 0)
                {
                    return hpComparison;
                }
            }

            return CompareCoordPriority(left.TargetCoord, right.TargetCoord);
        }

        private static int CompareCoordPriority(TileCoord left, TileCoord right)
        {
            return GetCoordPriorityIndex(left).CompareTo(GetCoordPriorityIndex(right));
        }

        private static int GetCoordPriorityIndex(TileCoord coord)
        {
            for (var i = 0; i < CoordPriority.Length; i++)
            {
                if (CoordPriority[i] == coord)
                {
                    return i;
                }
            }

            return int.MaxValue;
        }

        private sealed class AttackCandidate
        {
            private AttackCandidate(TileCoord attackerCoord, int attackerAttack, TargetPreference targetPreference)
            {
                AttackerCoord = attackerCoord;
                AttackerAttack = attackerAttack;
                TargetPreference = targetPreference;
            }

            public TileCoord AttackerCoord { get; }

            public TileCoord TargetCoord => TargetPreference.TargetCoord;

            public int AttackerAttack { get; }

            public TargetPreference TargetPreference { get; }

            public static AttackCandidate From(
                TileCoord attackerCoord,
                OccupantState attacker,
                TileCoord targetCoord,
                OccupantState target)
            {
                var projectedRemainingHp = GetProjectedTargetRemainingHpAfterAttackAction(attacker, target);

                return new AttackCandidate(
                    attackerCoord,
                    attacker.Attack,
                    TargetPreference.FromAttack(targetCoord, target, projectedRemainingHp <= 0));
            }
        }

        private sealed class SpellCandidate
        {
            private SpellCandidate(string cardId, TargetPreference targetPreference)
            {
                CardId = cardId;
                TargetPreference = targetPreference;
            }

            public string CardId { get; }

            public TileCoord TargetCoord => TargetPreference.TargetCoord;

            public TargetPreference TargetPreference { get; }

            public static SpellCandidate From(string cardId, TileCoord targetCoord, OccupantState target)
            {
                return new SpellCandidate(cardId, TargetPreference.FromSpell(targetCoord, target));
            }
        }

        private sealed class TargetPreference
        {
            private TargetPreference(TileCoord targetCoord, int category, int targetHp, int targetAttack)
            {
                TargetCoord = targetCoord;
                Category = category;
                TargetHp = targetHp;
                TargetAttack = targetAttack;
            }

            public TileCoord TargetCoord { get; }

            public int Category { get; }

            public int TargetHp { get; }

            public int TargetAttack { get; }

            public static TargetPreference FromAttack(TileCoord targetCoord, OccupantState target, bool canKill)
            {
                return Create(targetCoord, target, canKill);
            }

            public static TargetPreference FromSpell(TileCoord targetCoord, OccupantState target)
            {
                return Create(targetCoord, target, true);
            }

            private static TargetPreference Create(TileCoord targetCoord, OccupantState target, bool canKill)
            {
                var isMaster = target.Kind == OccupantKind.Master;
                var isResourceProducer = (target.Kind == OccupantKind.Unit || target.Kind == OccupantKind.Building) && HasAnyResourceGain(target.TurnStartResourceGain);

                var category = 4;
                if (isMaster && canKill)
                {
                    category = 0;
                }
                else if (isResourceProducer && canKill)
                {
                    category = 1;
                }
                else if (canKill)
                {
                    category = 2;
                }
                else if (isMaster)
                {
                    category = 3;
                }

                return new TargetPreference(targetCoord, category, target.CurrentHp, target.Attack);
            }
        }
    }
}


