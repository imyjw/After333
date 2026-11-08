using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;

namespace Project333.Runtime.Application.Services
{
    public static class FirewallEffectService
    {
        // Cast uses the existing SpellCast VFX cue. Only later ticks record an
        // area event and consume one of the two persistent triggers.
        public static void Apply(BattleState battleState, PersistentEffectState effect, bool isTurnStart)
        {
            var targetPlayerId = effect.TargetsOwnerBoard
                ? effect.OwnerId
                : battleState.GetOpponent(effect.OwnerId).Id;
            var targetBoard = battleState.GetBoard(targetPlayerId);
            var resolvedEffectDamage = SpellPowerRules.ApplyCurrent(battleState, effect.OwnerId, effect.EffectDamage, effect.EffectDamageType);
            var targets = new List<OccupantState>();
            var targetCoords = new List<TileCoord>(BoardState.ColumnCount);
            for (var column = 0; column < BoardState.ColumnCount; column++)
            {
                targetCoords.Add(new TileCoord(column, effect.TargetRow));
            }

            if (isTurnStart) battleState.RecordAreaSpellEffect(new BattleAreaSpellEffectEvent(
                FirewallRules.EffectId,
                effect.SourceCardId,
                effect.OwnerId,
                targetPlayerId,
                targetCoords,
                battleState.ValuePopupEvents.Count));

            foreach (var occupant in targetBoard.EnumerateOccupants())
            {
                if (occupant.Position.Row == effect.TargetRow)
                {
                    targets.Add(occupant);
                }
            }

            foreach (var target in targets)
            {
                if (target.IsSealbound)
                {
                    continue;
                }

                var actualDamage = DamageResolutionRules.ApplyEffectDamage(
                    target,
                    resolvedEffectDamage,
                    effect.EffectDamageType);
                BattleValuePopupRecorder.RecordDamage(
                    battleState,
                    target,
                    actualDamage,
                    BattleValueChangeCause.Firewall,
                    effect.EffectDamageType,
                    effect.OwnerId,
                    sourceCardId: effect.SourceCardId);
            }

            if (isTurnStart) effect.ResolveTrigger();
            OccupantDestructionService.ResolveDefeatedOccupants(battleState);
        }
    }
}
