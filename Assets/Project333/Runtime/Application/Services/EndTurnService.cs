using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    public sealed class EndTurnService
    {
        private const string BlueDragonCardId = "BlueDragon";
        private const string RedDragonCardId = "RedDragon";
        private const int BlueDragonHealAmount = 33;
        private const int RedDragonDamageAmount = 33;
        private readonly Random _random;

        public EndTurnService()
            : this(CreateRandom())
        {
        }

        public EndTurnService(Random random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
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
                throw new InvalidOperationException("Turns can only end during the Main phase.");
            }

            battleState.GetPlayer(battleState.ActivePlayerId).Hand.RemoveTemporaryReplicates();

            ResolveEndTurnOccupantEffects(battleState);
            if (battleState.IsEnded)
            {
                return;
            }

            ResolveHeroGrowthEffects(battleState);

            var endingPlayerId = battleState.ActivePlayerId;
            battleState.ResolveInvincibleTurnEnd(endingPlayerId);
            battleState.SetPhase(PhaseType.TurnEnd);
            battleState.StartNextTurn(GetNextPlayerId(endingPlayerId));
        }

        private void ResolveHeroGrowthEffects(BattleState battleState)
        {
            ResolveHeroGrowthEffects(battleState.PlayerBoard);
            ResolveHeroGrowthEffects(battleState.AIBoard);
        }

        private void ResolveHeroGrowthEffects(Project333.Runtime.Domain.Board.BoardState board)
        {
            var occupants = new List<OccupantState>(board.EnumerateOccupants());
            foreach (var occupant in occupants)
            {
                if (!HeroRules.IsHero(occupant) ||
                    !occupant.IsAlive ||
                    occupant.EffectsSuppressed ||
                    board.GetOccupant(occupant.Position) != occupant)
                {
                    continue;
                }

                if (_random.Next(2) == 0)
                {
                    occupant.IncreaseBaseAttack(HeroRules.GrowthAmount);
                }
                else
                {
                    occupant.IncreaseMaxHpAndCurrentHp(HeroRules.GrowthAmount);
                }
            }
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

        private static Random CreateRandom()
        {
            var seedBytes = new byte[sizeof(int)];
            using (var randomNumberGenerator = RandomNumberGenerator.Create())
            {
                randomNumberGenerator.GetBytes(seedBytes);
            }

            return new Random(BitConverter.ToInt32(seedBytes, 0));
        }
    }
}
