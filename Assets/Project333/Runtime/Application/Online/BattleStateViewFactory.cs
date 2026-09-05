using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Online
{
    public sealed class BattleStateViewFactory
    {
        public BattleStateViewDto CreateForPlayer(BattleState battleState, string matchId, PlayerId viewerId)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            var viewer = battleState.GetPlayer(viewerId);
            var opponent = battleState.GetOpponent(viewerId);

            var view = new BattleStateViewDto
            {
                MatchId = matchId ?? string.Empty,
                ViewerId = viewerId,
                TurnNumber = battleState.TurnNumber,
                ActivePlayerId = battleState.ActivePlayerId,
                Phase = battleState.Phase,
                IsEnded = battleState.IsEnded,
                HasWinner = battleState.Result.HasWinner,
                IsDraw = battleState.Result.IsDraw,
                WinnerId = battleState.Result.Winner,
                HasPendingRobotFusion = battleState.PendingRobotFusion != null,
                PendingRobotFusionOwnerId = battleState.PendingRobotFusion?.OwnerId ?? PlayerId.Player,
                PendingRobotFusionCardId = battleState.PendingRobotFusion?.CardId ?? string.Empty,
                Player = CreatePlayerView(viewer, true),
                Opponent = CreatePlayerView(opponent, false)
            };

            AddBoardOccupants(view, battleState.GetBoard(viewerId));
            AddBoardOccupants(view, battleState.GetOpponentBoard(viewerId));
            AddPersistentEffects(view, battleState);
            return view;
        }

        private static BattlePlayerViewDto CreatePlayerView(PlayerState playerState, bool showHand)
        {
            var view = new BattlePlayerViewDto
            {
                PlayerId = playerState.Id,
                Resources = ResourceSetDto.FromDomain(playerState.Resources),
                DeckCount = playerState.Deck.Count,
                HandCount = playerState.Hand.Count,
                DiscardCount = playerState.Discard.Count,
                MaxHandSize = playerState.MaxHandSize,
                HasUsedMulligan = playerState.HasUsedMulligan
            };

            if (showHand)
            {
                view.VisibleHandCardIds.AddRange(playerState.Hand.CardIds);
                foreach (var handCard in playerState.Hand.Cards)
                {
                    view.VisibleHandCards.Add(new HandCardViewDto
                    {
                        RuntimeId = handCard.RuntimeId,
                        CardId = handCard.CardId,
                        IsTemporaryReplicate = handCard.IsTemporaryReplicate
                    });
                }
            }

            return view;
        }

        private static void AddBoardOccupants(BattleStateViewDto view, BoardState boardState)
        {
            foreach (var occupant in boardState.EnumerateOccupants())
            {
                view.Occupants.Add(CreateOccupantView(occupant));
            }
        }

        private static BoardOccupantViewDto CreateOccupantView(OccupantState occupant)
        {
            var view = new BoardOccupantViewDto
            {
                OwnerId = occupant.OwnerId,
                Coord = TileCoordDto.FromDomain(occupant.Position),
                RuntimeId = occupant.RuntimeId,
                CardId = occupant.CardId,
                Kind = occupant.Kind,
                AttackType = occupant.AttackType,
                DamageType = occupant.DamageType,
                Attack = occupant.Attack,
                BaseAttack = occupant.BaseAttack,
                HasBaseAttack = true,
                OriginalAttack = occupant.OriginalAttack,
                MaxAttacksPerTurn = occupant.MaxAttacksPerTurn,
                HitsPerAttack = occupant.HitsPerAttack,
                HasBerserker = occupant.HasBerserker,
                SciencePowerUpkeep = GetSciencePowerUpkeep(occupant),
                PhysicalDefense = occupant.PhysicalDefense,
                MagicDefense = occupant.MagicDefense,
                OriginalPhysicalDefense = occupant.OriginalPhysicalDefense,
                OriginalMagicDefense = occupant.OriginalMagicDefense,
                CurrentHp = occupant.CurrentHp,
                MaxHp = occupant.MaxHp,
                OriginalMaxHp = occupant.OriginalMaxHp,
                HasOriginalCombatStats = true,
                CanMove = occupant.CanMove,
                IsDrained = occupant.IsDrained,
                IsErasure = occupant.IsErasure,
                IsSealbound = occupant.IsSealbound,
                SealboundOwnerTurnStartsRemaining = occupant.SealboundOwnerTurnStartsRemaining,
                IsDemonKingRevivalPending = occupant.IsDemonKingRevivalPending,
                DemonKingRevivalTurnStartsRemaining = occupant.DemonKingRevivalTurnStartsRemaining,
                DemonKingRevivalCountdownPlayerId = occupant.DemonKingRevivalCountdownPlayerId,
                DemonKingRevivalEligibleAfterTurnNumber = occupant.DemonKingRevivalEligibleAfterTurnNumber,
                DemonKingRevivalCount = occupant.DemonKingRevivalCount,
                HuanShuOwnerTurnsRemaining = occupant.HuanShuOwnerTurnsRemaining,
                HuanShuEligibleAfterTurnNumber = occupant.HuanShuEligibleAfterTurnNumber,
                HasRush = occupant.HasRush,
                HasHiding = occupant.HasHiding,
                HidingRevealed = occupant.HidingRevealed,
                HasFlying = occupant.HasFlying,
                HasPiercing = occupant.HasPiercing,
                SpellPower = occupant.SpellPower,
                HasShielder = occupant.HasShielder,
                HasEndure = occupant.HasEndure,
                HasLifeSteal = occupant.HasLifeSteal,
                HasRobot = occupant is UnitState unit && unit.HasRobot,
                EndureUsed = occupant.EndureUsed,
                WasSummonedThisTurn = occupant.WasSummonedThisTurn,
                HasSummoningSickness = occupant.HasSummoningSickness,
                RemainingAttacksThisTurn = occupant.RemainingAttacksThisTurn
            };

            foreach (var effect in occupant.InvincibleEffects)
            {
                view.InvincibleEffects.Add(new InvincibleEffectViewDto
                {
                    Duration = effect.Duration,
                    OwnerTurnsRemaining = effect.OwnerTurnsRemaining,
                    AppliedTurnNumber = effect.AppliedTurnNumber,
                    AppliedActivePlayerId = effect.AppliedActivePlayerId
                });
            }

            return view;
        }

        private static int GetSciencePowerUpkeep(OccupantState occupant)
        {
            return occupant switch
            {
                UnitState unit => unit.SciencePowerUpkeep,
                BuildingState building => building.SciencePowerUpkeep,
                _ => 0,
            };
        }

        private static void AddPersistentEffects(BattleStateViewDto view, BattleState battleState)
        {
            foreach (var persistentEffect in battleState.PersistentEffects)
            {
                if (persistentEffect == null)
                {
                    continue;
                }

                view.PersistentEffects.Add(new PersistentEffectViewDto
                {
                    SourceCardId = persistentEffect.SourceCardId,
                    OwnerId = persistentEffect.OwnerId,
                    EffectId = persistentEffect.EffectId,
                    AppliedTurn = persistentEffect.AppliedTurn,
                    EndConditionText = persistentEffect.EndConditionText,
                    TurnStartResourceGain = ResourceSetDto.FromDomain(persistentEffect.TurnStartResourceGain),
                    OwnerTurnStartsRemaining = persistentEffect.OwnerTurnStartsRemaining,
                    TargetRuntimeId = persistentEffect.TargetRuntimeId ?? string.Empty,
                    TargetRow = persistentEffect.TargetRow,
                    RemainingTriggers = persistentEffect.RemainingTriggers,
                    EffectDamage = persistentEffect.EffectDamage,
                    EffectDamageType = persistentEffect.EffectDamageType,
                    TargetsOwnerBoard = persistentEffect.TargetsOwnerBoard,
                    CapturedSpellPower = persistentEffect.CapturedSpellPower,
                    TargetStartColumn = persistentEffect.TargetStartColumn,
                    IsExpired = persistentEffect.IsExpired
                });
            }
        }
    }
}
