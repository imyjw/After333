using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Application.Services
{
    public sealed class EndTurnService
    {
        private const string BlueDragonCardId = "BlueDragon";
        private const string RedDragonCardId = "RedDragon";
        private const int BlueDragonHealAmount = 33;
        private const int RedDragonDamageAmount = 20;
        public const string RedDragonFlamesEffectId = "redDragonEndFlames";

        public EndTurnService()
        {
        }

        public EndTurnService(Random random)
        {
            // Retain the seeded-constructor contract used by AI simulations; turn end has no random effects.
            if (random == null) throw new ArgumentNullException(nameof(random));
        }

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
                throw new InvalidOperationException("자신의 턴에만 턴을 종료할 수 있습니다.");
            }

            battleState.GetPlayer(battleState.ActivePlayerId).Hand.RemoveTemporaryReplicates();

            // A local presenter may retain the preceding turn's consumed cue. Each
            // accepted EndTurn creates a new ordered set; never infer it from a snapshot.
            battleState.AreaSpellEffectEvents.RemoveAll(effect => effect.EffectId == RedDragonFlamesEffectId);
            var endingPlayerId = battleState.ActivePlayerId;
            ResolveEndTurnOccupantEffects(battleState);
            // Turn-end Magic effects still benefit; the next turn's damage must not.
            foreach (var effect in battleState.PersistentEffects)
            {
                if (effect.OwnerId == endingPlayerId &&
                    string.Equals(effect.EffectId, MagicAmplificationCircleRules.EffectId, StringComparison.Ordinal))
                    effect.Expire();
            }
            if (battleState.IsEnded)
            {
                return;
            }

            battleState.ResolveInvincibleTurnEnd(endingPlayerId);
            foreach (var occupant in battleState.PlayerBoard.EnumerateOccupants())
                occupant.ResolveActionDebuffsTurnEnd();
            foreach (var occupant in battleState.AIBoard.EnumerateOccupants())
                occupant.ResolveActionDebuffsTurnEnd();
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

            BattleDrawService.DrawCards(
                owner,
                battleState,
                GaebangBranchRules.DrawCount,
                GaebangBranchRules.CardId);
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
            var damage = SpellPowerRules.ApplyCurrent(battleState, ownerId, RedDragonDamageAmount, DamageType.Magic);
            var targetCoords = new List<TileCoord>(BoardState.ColumnCount * BoardState.RowCount);
            for (var row = 0; row < BoardState.RowCount; row++)
            for (var column = 0; column < BoardState.ColumnCount; column++)
                targetCoords.Add(new TileCoord(column, row));
            // Presentation-only activation precedes the existing damage packets,
            // including empty or fully protected fields and separate Dragon sources.
            battleState.RecordAreaSpellEffect(new BattleAreaSpellEffectEvent(
                RedDragonFlamesEffectId, RedDragonCardId, ownerId,
                battleState.GetOpponent(ownerId).Id, targetCoords, battleState.ValuePopupEvents.Count));

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
                    damage,
                    DamageType.Magic);
                BattleValuePopupRecorder.RecordDamage(
                    battleState,
                    occupant,
                    actualDamage,
                    BattleValueChangeCause.RedDragon,
                    DamageType.Magic,
                    ownerId,
                    source?.RuntimeId,
                    RedDragonCardId);
            }

            OccupantDestructionService.ResolveDefeatedOccupants(battleState);
        }

        private static PlayerId GetNextPlayerId(PlayerId currentPlayerId)
        {
            return currentPlayerId == PlayerId.Player
                ? PlayerId.AI
                : PlayerId.Player;
        }

    }
}
