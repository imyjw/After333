using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    public sealed class EndTurnService
    {
        private const string BlueDragonCardId = "BlueDragon";
        private const string RedDragonCardId = "RedDragon";
        private const int BlueDragonHealAmount = 33;
        private const int RedDragonDamageAmount = 66;

        public void EndTurn(BattleState battleState)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (battleState.IsEnded)
            {
                return;
            }

            if (battleState.Phase != PhaseType.Main)
            {
                throw new InvalidOperationException("Turns can only end during the Main phase.");
            }

            battleState.GetPlayer(battleState.ActivePlayerId).Hand.RemoveTemporaryReplicates();

            ResolveEndTurnOccupantEffects(battleState);
            if (battleState.IsEnded)
            {
                return;
            }

            var endingPlayerId = battleState.ActivePlayerId;
            battleState.ResolveInvincibleTurnEnd(endingPlayerId);
            battleState.SetPhase(PhaseType.TurnEnd);
            battleState.StartNextTurn(GetNextPlayerId(endingPlayerId));
        }

        private static void ResolveEndTurnOccupantEffects(BattleState battleState)
        {
            var activePlayerId = battleState.ActivePlayerId;
            var activeBoard = battleState.GetBoard(activePlayerId);
            var effectSources = new List<OccupantState>(activeBoard.EnumerateOccupants());

            foreach (var occupant in effectSources)
            {
                if (battleState.IsEnded)
                {
                    return;
                }

                if (occupant == null ||
                    occupant.EffectsSuppressed ||
                    activeBoard.GetOccupant(occupant.Position) != occupant)
                {
                    continue;
                }

                switch (occupant.CardId)
                {
                    case BlueDragonCardId:
                        ResolveBlueDragonEffect(battleState, activePlayerId, occupant);
                        break;

                    case RedDragonCardId:
                        ResolveRedDragonEffect(battleState, activePlayerId, occupant);
                        break;

                    case GaebangBranchRules.CardId:
                        ResolveGaebangBranchEffect(battleState, activePlayerId);
                        break;
                }
            }
        }

        private static void ResolveGaebangBranchEffect(BattleState battleState, PlayerId ownerId)
        {
            var owner = battleState.GetPlayer(ownerId);
            if (owner.Resources.Gold != 0)
            {
                return;
            }

            BattleDrawService.DrawCards(owner, battleState, GaebangBranchRules.DrawCount);
        }

        private static void ResolveBlueDragonEffect(
            BattleState battleState,
            PlayerId ownerId,
            OccupantState source)
        {
            var alliedBoard = battleState.GetBoard(ownerId);
            foreach (var occupant in alliedBoard.EnumerateOccupants())
            {
                if (occupant.Kind == OccupantKind.Building ||
                    occupant.CurrentHp <= 0 ||
                    occupant.IsSealbound)
                {
                    continue;
                }

                BattleValuePopupRecorder.HealAndRecord(
                    battleState,
                    occupant,
                    BlueDragonHealAmount,
                    BattleValueChangeCause.BlueDragon,
                    ownerId,
                    source?.RuntimeId,
                    BlueDragonCardId);
            }
        }

        private static void ResolveRedDragonEffect(
            BattleState battleState,
            PlayerId ownerId,
            OccupantState source)
        {
            var enemyBoard = battleState.GetOpponentBoard(ownerId);
            var damagedOccupants = new List<OccupantState>(enemyBoard.EnumerateOccupants());

            foreach (var occupant in damagedOccupants)
            {
                if (enemyBoard.GetOccupant(occupant.Position) != occupant)
                {
                    continue;
                }

                if (occupant.IsSealbound)
                {
                    continue;
                }

                var actualDamage = DamageResolutionRules.ApplyEffectDamage(
                    occupant,
                    RedDragonDamageAmount,
                    DamageType.Physical);
                BattleValuePopupRecorder.RecordDamage(
                    battleState,
                    occupant,
                    actualDamage,
                    BattleValueChangeCause.RedDragon,
                    DamageType.Physical,
                    ownerId,
                    source?.RuntimeId,
                    RedDragonCardId);
            }

            foreach (var occupant in damagedOccupants)
            {
                if (occupant.Kind != OccupantKind.Master &&
                    occupant.CurrentHp <= 0 &&
                    enemyBoard.GetOccupant(occupant.Position) == occupant)
                {
                    enemyBoard.Remove(occupant.Position);
                }
            }

            var enemyMaster = battleState.GetOpponent(ownerId).Master;
            if (enemyMaster.CurrentHp <= 0)
            {
                battleState.EndBattle(ownerId);
            }
        }

        private static PlayerId GetNextPlayerId(PlayerId currentPlayerId)
        {
            return currentPlayerId == PlayerId.Player
                ? PlayerId.AI
                : PlayerId.Player;
        }
    }
}
