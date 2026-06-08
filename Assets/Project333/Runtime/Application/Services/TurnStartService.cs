using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Application.Services
{
    public sealed class TurnStartService
    {
        private const int BaseDrawCount = 1;
        private const int BaseGoldGain = 1;
        private const int FailedDrawDamageStep = 3;
        private const string CheonraJimangEffectId = "cheonra_jimang";

        private readonly ScienceUpkeepService _scienceUpkeepService;

        public TurnStartService()
            : this(new ScienceUpkeepService())
        {
        }

        public TurnStartService(ScienceUpkeepService scienceUpkeepService)
        {
            _scienceUpkeepService = scienceUpkeepService;
        }

        public void ResolveTurnStart(BattleState battleState)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (battleState.Phase != PhaseType.TurnStart)
            {
                throw new InvalidOperationException("Turn start can only be resolved during the TurnStart phase.");
            }

            if (battleState.IsEnded)
            {
                return;
            }

            battleState.ClearValuePopupEvents();

            ResolveScriptedTurnStartEffects(battleState);
            if (battleState.IsEnded)
            {
                return;
            }

            var activePlayer = battleState.GetPlayer(battleState.ActivePlayerId);

            RefreshTurnStartState(battleState);
            GainBaseResources(activePlayer);
            GainOccupantResources(battleState);
            ResolveBaseDraw(activePlayer, battleState);

            if (!battleState.IsEnded)
            {
                _scienceUpkeepService.ResolveUpkeep(battleState);
            }

            if (!battleState.IsEnded)
            {
                battleState.SetPhase(PhaseType.Main);
            }
        }

        private static void ResolveScriptedTurnStartEffects(BattleState battleState)
        {
            var snapshot = new List<PersistentEffectState>(battleState.PersistentEffects);
            foreach (var persistentEffect in snapshot)
            {
                if (persistentEffect.IsExpired || persistentEffect.OwnerId != battleState.ActivePlayerId)
                {
                    continue;
                }

                if (!string.Equals(persistentEffect.EffectId, CheonraJimangEffectId, StringComparison.Ordinal))
                {
                    continue;
                }

                ResolveCheonraJimang(battleState, persistentEffect);
            }
        }

        private static void ResolveCheonraJimang(BattleState battleState, PersistentEffectState persistentEffect)
        {
            if (!string.IsNullOrWhiteSpace(persistentEffect.TargetRuntimeId) &&
                TryFindOccupantByRuntimeId(battleState.PlayerBoard, persistentEffect.TargetRuntimeId, out var playerTarget))
            {
                battleState.PlayerBoard.Remove(playerTarget.Position);
            }
            else if (!string.IsNullOrWhiteSpace(persistentEffect.TargetRuntimeId) &&
                     TryFindOccupantByRuntimeId(battleState.AIBoard, persistentEffect.TargetRuntimeId, out var aiTarget))
            {
                battleState.AIBoard.Remove(aiTarget.Position);
            }

            persistentEffect.Expire();
        }

        private static bool TryFindOccupantByRuntimeId(BoardState boardState, string runtimeId, out OccupantState occupant)
        {
            foreach (var candidate in boardState.EnumerateOccupants())
            {
                if (candidate.RuntimeId == runtimeId)
                {
                    occupant = candidate;
                    return true;
                }
            }

            occupant = null;
            return false;
        }

        private static void RefreshTurnStartState(BattleState battleState)
        {
            var activeBoard = battleState.GetBoard(battleState.ActivePlayerId);

            foreach (var occupant in activeBoard.EnumerateOccupants())
            {
                occupant.HasSummoningSickness = false;
                occupant.RemainingAttacksThisTurn = occupant.MaxAttacksPerTurn;
            }
        }

        private static void GainBaseResources(PlayerState playerState)
        {
            playerState.Resources.Add(new ResourceSet(
                mana: 0,
                qi: 0,
                power: 0,
                gold: BaseGoldGain));
        }

        private static void GainOccupantResources(BattleState battleState)
        {
            var activePlayer = battleState.GetPlayer(battleState.ActivePlayerId);
            var activeBoard = battleState.GetBoard(battleState.ActivePlayerId);

            foreach (var occupant in activeBoard.EnumerateOccupants())
            {
                if (occupant.IsDisabled)
                {
                    continue;
                }

                if (!HasAnyResourceGain(occupant.TurnStartResourceGain))
                {
                    continue;
                }

                activePlayer.Resources.Add(occupant.TurnStartResourceGain.Clone());
            }

            foreach (var persistentEffect in battleState.PersistentEffects)
            {
                if (persistentEffect.IsExpired || persistentEffect.OwnerId != battleState.ActivePlayerId)
                {
                    continue;
                }

                if (HasAnyResourceGain(persistentEffect.TurnStartResourceGain))
                {
                    activePlayer.Resources.Add(persistentEffect.TurnStartResourceGain.Clone());
                }

                persistentEffect.ResolveOwnerTurnStart();
            }
        }

        private static void ResolveBaseDraw(PlayerState playerState, BattleState battleState)
        {
            for (var i = 0; i < BaseDrawCount; i++)
            {
                if (!playerState.Deck.TryDraw(out var cardId))
                {
                    ApplyFailedDrawDamage(playerState, battleState);
                    continue;
                }

                if (playerState.Hand.Count >= playerState.MaxHandSize)
                {
                    continue;
                }

                playerState.Hand.Add(cardId);
            }
        }

        private static void ApplyFailedDrawDamage(PlayerState playerState, BattleState battleState)
        {
            var failedDrawCount = playerState.IncrementFailedDrawCount();
            var damage = failedDrawCount * FailedDrawDamageStep;

            playerState.Master.CurrentHp -= damage;
            BattleValuePopupRecorder.RecordDamage(battleState, playerState.Master, damage);

            if (playerState.Master.CurrentHp <= 0)
            {
                battleState.EndBattle(battleState.GetOpponent(playerState.Id).Id);
            }
        }

        private static bool HasAnyResourceGain(ResourceSet resourceSet)
        {
            return resourceSet.Mana > 0 ||
                   resourceSet.Qi > 0 ||
                   resourceSet.Power > 0 ||
                   resourceSet.Gold > 0;
        }
    }
}
