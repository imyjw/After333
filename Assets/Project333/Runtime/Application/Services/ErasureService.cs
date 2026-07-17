using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    /// <summary>
    /// Applies Erasure to an occupant and removes effects attached to that occupant.
    /// </summary>
    public sealed class ErasureService
    {
        public void Apply(BattleState battleState, OccupantState target)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (target.IsSealbound)
            {
                return;
            }

            target.ApplyErasure();

            foreach (var effect in battleState.PersistentEffects)
            {
                if (effect == null ||
                    effect.IsExpired ||
                    string.IsNullOrWhiteSpace(effect.TargetRuntimeId) ||
                    !string.Equals(effect.TargetRuntimeId, target.RuntimeId, StringComparison.Ordinal))
                {
                    continue;
                }

                effect.Expire();
            }
        }
    }
}
