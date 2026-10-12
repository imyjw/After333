#nullable enable
using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Services
{
    public sealed class TrapService
    {
        private readonly ICardDefinitionProvider _cards;
        private readonly ICardUpgradeLevelProvider _levels;

        public TrapService(ICardDefinitionProvider cards, ICardUpgradeLevelProvider? levels = null)
        {
            _cards = cards ?? throw new ArgumentNullException(nameof(cards));
            _levels = levels ?? ZeroCardUpgradeLevelProvider.Instance;
        }

        public ArmedTrapState Install(BattleState state, PlayerId ownerId, string cardId,
            string? handCardRuntimeId = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (ownerId != PlayerId.Player && ownerId != PlayerId.AI) throw new ArgumentOutOfRangeException(nameof(ownerId));
            if (state.IsEnded) throw new InvalidOperationException("전투가 종료되었습니다.");
            if (state.Phase != PhaseType.Main) throw new InvalidOperationException("자신의 행동 턴에만 행동할 수 있습니다.");
            if (state.ActivePlayerId != ownerId) throw new InvalidOperationException("자신의 턴에만 행동할 수 있습니다.");
            if (string.IsNullOrWhiteSpace(cardId)) throw new ArgumentException("Trap card id is required.", nameof(cardId));

            var owner = state.GetPlayer(ownerId);
            if (owner.Master == null || !owner.Master.IsAlive) throw new InvalidOperationException("마스터가 살아있지 않습니다.");
            var handCard = owner.Hand.GetCardForPlay(cardId, handCardRuntimeId ?? string.Empty);
            if (handCard == null) throw new InvalidOperationException("해당 카드가 핸드에 없습니다.");
            var definition = _cards.GetRequired(cardId) as TrapSpellCardDefinition;
            if (definition == null || !definition.HasTrap ||
                !string.Equals(definition.CardId, handCard.CardId, StringComparison.Ordinal))
                throw new InvalidOperationException("트랩 카드가 아닙니다.");
            foreach (var existing in state.ArmedTraps)
                if (existing != null && existing.OwnerId == ownerId &&
                    string.Equals(existing.CardId, cardId, StringComparison.Ordinal))
                    throw new InvalidOperationException("같은 트랩이 이미 설치되어 있습니다.");

            var level = CardUpgradeRules.IsCardUpgradeable(definition)
                ? _levels.GetUpgradeLevel(ownerId, cardId) : 0;
            if (level < 0 || level > CardUpgradeRules.MaxLevel) level = 0;
            var trap = new ArmedTrapState(handCard.RuntimeId, ownerId, definition.CardId,
                definition.TriggerType, definition.EffectType, definition.ActivationCost,
                state.TurnNumber, level, handCard.IsTemporaryReplicate,
                definition.EffectType == TrapEffectType.DamageSummonedUnitPhysical
                    ? CardLevelSpellRules.ApplyDamageBonus(definition.Damage, level) : 0);

            // Validate and capture before touching the hand. Installation never pays activation cost.
            if (!owner.Hand.Remove(cardId, handCard.RuntimeId))
                throw new InvalidOperationException("해당 카드가 핸드에 없습니다.");
            state.ArmedTraps.Add(trap);
            state.RecordTrapEvent(new BattleTrapEvent(BattleTrapEventKind.Installed,
                ownerId, state.GetArmedTrapCount(ownerId)));
            return trap;
        }

        /// <summary>Called once after accepted attack validation and HuanShu resolution, before Shielder/combat.</summary>
        public static bool TryResolveAcceptedAttack(BattleState state, OccupantState attacker,
            PlayerId targetOwnerId, TileCoord targetCoord, OccupantState target)
        {
            if (state == null || state.IsEnded || attacker == null || target == null ||
                !attacker.IsAlive || !IsUnitOrMaster(attacker) ||
                attacker.OwnerId == targetOwnerId || !target.IsAlive ||
                !ReferenceEquals(state.GetBoard(targetOwnerId).GetOccupant(targetCoord), target) ||
                !ReferenceEquals(state.GetBoard(attacker.OwnerId).GetOccupant(attacker.Position), attacker))
                return false;

            for (var index = 0; index < state.ArmedTraps.Count; index++)
            {
                var trap = state.ArmedTraps[index];
                if (trap == null || trap.OwnerId != targetOwnerId ||
                    !MatchesAcceptedAttack(state, trap, attacker, target))
                    continue;
                var owner = state.GetPlayer(trap.OwnerId);
                var cost = trap.ActivationCost;
                if (!owner.Resources.CanAfford(cost)) continue;

                // Capture effective ATK before payment, HP loss, or any resulting aura/death change.
                var reflectedDamage = trap.EffectType == TrapEffectType.ReflectAttackerAttackAsMagic
                    ? SpellPowerRules.ApplyCurrent(state, trap.OwnerId, Math.Max(0, attacker.Attack), DamageType.Magic)
                    : 0;
                var attackerCoord = attacker.Position;
                owner.Resources.Spend(cost);
                state.ArmedTraps.RemoveAt(index);
                owner.Discard.Add(trap.CardId);
                var addedToHand = false;
                if (trap.EffectType == TrapEffectType.ReturnAttackerToHand)
                {
                    // A return is not destruction: no death/revival/kill reward is resolved for this unit.
                    var returningOwner = state.GetPlayer(attacker.OwnerId);
                    state.GetBoard(attacker.OwnerId).Remove(attackerCoord);
                    addedToHand = returningOwner.Hand.Count < returningOwner.MaxHandSize;
                    if (addedToHand) returningOwner.Hand.Add(attacker.CardId);
                }
                else if (trap.EffectType == TrapEffectType.DestroyAttacker)
                {
                    // Direct destruction is not an HP-damage packet: preserve the ordinary
                    // destruction queue, aura withdrawal, last words and Demon King revival.
                    OccupantDestructionService.DestroyOccupant(state, attacker.OwnerId, attacker);
                }
                else if (trap.EffectType == TrapEffectType.ReflectAttackerAttackAsMagic)
                {
                    // Reflection hits the attacking unit itself; it is not a new targeted spell
                    // or normal attack, so Shielder transfer, Flying reduction and critical do not apply.
                    var actualDamage = DamageResolutionRules.ApplyEffectDamage(attacker, reflectedDamage, DamageType.Magic);
                    BattleValuePopupRecorder.RecordDamage(state, attacker, actualDamage,
                        BattleValueChangeCause.Spell, DamageType.Magic, trap.OwnerId,
                        trap.RuntimeId, trap.CardId);
                }
                state.RecordTrapEvent(new BattleTrapEvent(BattleTrapEventKind.Triggered,
                    trap.OwnerId, state.GetArmedTrapCount(trap.OwnerId), trap.CardId,
                    trap.RuntimeId, trap.UpgradeLevel, attacker.OwnerId, attacker.RuntimeId,
                    attacker.CardId, attackerCoord, addedToHand));
                return true;
            }
            return false;
        }

        /// <summary>After successful hand summon initialization, before the command finishes.</summary>
        public static bool TryResolveAcceptedUnitSummon(BattleState state, OccupantState summoned)
        {
            if (state == null || state.IsEnded || summoned == null ||
                summoned.Kind != OccupantKind.Unit || !summoned.IsAlive ||
                !ReferenceEquals(state.GetBoard(summoned.OwnerId).GetOccupant(summoned.Position), summoned))
                return false;

            var ownerId = state.GetOpponent(summoned.OwnerId).Id;
            for (var index = 0; index < state.ArmedTraps.Count; index++)
            {
                var trap = state.ArmedTraps[index];
                if (trap == null || trap.OwnerId != ownerId ||
                    trap.TriggerType != TrapTriggerType.EnemyUnitSummoned ||
                    trap.EffectType != TrapEffectType.DamageSummonedUnitPhysical)
                    continue;
                var owner = state.GetPlayer(ownerId);
                if (!owner.Resources.CanAfford(trap.ActivationCost)) continue;

                owner.Resources.Spend(trap.ActivationCost);
                state.ArmedTraps.RemoveAt(index);
                owner.Discard.Add(trap.CardId);
                // This is the unit just summoned, not a newly selected spell target.
                // Common effect damage supplies physical defense, Endure, Invincible,
                // Sealbound, Hiding reveal and Drained amplification without Shielder transfer.
                var damage = DamageResolutionRules.ApplyEffectDamage(
                    summoned, trap.EffectAmount, DamageType.Physical);
                BattleValuePopupRecorder.RecordDamage(state, summoned, damage,
                    BattleValueChangeCause.Spell, DamageType.Physical, ownerId,
                    trap.RuntimeId, trap.CardId);
                state.RecordTrapEvent(new BattleTrapEvent(BattleTrapEventKind.Triggered,
                    ownerId, state.GetArmedTrapCount(ownerId), trap.CardId,
                    trap.RuntimeId, trap.UpgradeLevel, summoned.OwnerId, summoned.RuntimeId,
                    summoned.CardId, summoned.Position, effectAmount: trap.EffectAmount));
                OccupantDestructionService.ResolveDefeatedOccupants(state);
                return true;
            }
            return false;
        }
        private static bool MatchesAcceptedAttack(BattleState state, ArmedTrapState trap,
            OccupantState attacker, OccupantState target)
        {
            if (trap.TriggerType == TrapTriggerType.EnemyRangedUnitAttacksMaster &&
                trap.EffectType == TrapEffectType.DestroyAttacker)
                return attacker.Kind == OccupantKind.Unit && attacker.AttackType == AttackType.Ranged &&
                    target.Kind == OccupantKind.Master &&
                    ReferenceEquals(state.GetPlayer(trap.OwnerId).Master, target);
            if (trap.TriggerType == TrapTriggerType.EnemyMeleeUnitAttacksMaster &&
                trap.EffectType == TrapEffectType.DestroyAttacker)
                return attacker.Kind == OccupantKind.Unit && attacker.AttackType == AttackType.Melee &&
                    target.Kind == OccupantKind.Master &&
                    ReferenceEquals(state.GetPlayer(trap.OwnerId).Master, target);

            // Existing installations retain their captured trigger and activation cost.
            // Do not reinterpret the original any-Unit trigger when restoring an armed trap.
            if (trap.TriggerType == TrapTriggerType.EnemyUnitAttacksMaster &&
                (trap.EffectType == TrapEffectType.ReturnAttackerToHand ||
                 trap.EffectType == TrapEffectType.DestroyAttacker))
                return attacker.Kind == OccupantKind.Unit && target.Kind == OccupantKind.Master &&
                    ReferenceEquals(state.GetPlayer(trap.OwnerId).Master, target);

            return trap.TriggerType == TrapTriggerType.EnemyUnitAttacksUnit &&
                trap.EffectType == TrapEffectType.ReflectAttackerAttackAsMagic &&
                IsUnitOrMaster(attacker) && IsUnitOrMaster(target);
        }

        private static bool IsUnitOrMaster(OccupantState occupant) =>
            occupant.Kind == OccupantKind.Unit || occupant.Kind == OccupantKind.Master;
    }
}
