using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Application.Online
{
    public static class BattleStateViewProjector
    {
        public static BattleState CreateLocalPerspectiveState(BattleStateViewDto view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            var playerBoard = new BoardState();
            var aiBoard = new BoardState();

            var playerMaster = CreateFallbackMaster(PlayerId.Player);
            var aiMaster = CreateFallbackMaster(PlayerId.AI);

            if (view.Occupants != null)
            {
                foreach (var occupantView in view.Occupants)
                {
                    if (occupantView?.Coord == null)
                    {
                        continue;
                    }

                    var localOwnerId = ToLocalOwnerId(view, occupantView.OwnerId);
                    var occupant = CreateOccupant(view, occupantView, localOwnerId);
                    var board = localOwnerId == PlayerId.Player ? playerBoard : aiBoard;
                    board.Place(occupant.Position, occupant);

                    if (occupant.Kind == OccupantKind.Master)
                    {
                        if (localOwnerId == PlayerId.Player)
                        {
                            playerMaster = (MasterState)occupant;
                        }
                        else
                        {
                            aiMaster = (MasterState)occupant;
                        }
                    }
                }
            }

            EnsureMasterPlaced(playerBoard, playerMaster);
            EnsureMasterPlaced(aiBoard, aiMaster);

            var playerState = CreatePlayerState(PlayerId.Player, view.Player, playerMaster, showHand: true);
            var aiState = CreatePlayerState(PlayerId.AI, view.Opponent, aiMaster, showHand: false);

            var projectedState = new BattleState(playerState, aiState, playerBoard, aiBoard);
            ApplyTurnState(projectedState, view);
            ApplyPersistentEffects(projectedState, view);
            if (view.HasPendingRobotFusion && !string.IsNullOrWhiteSpace(view.PendingRobotFusionCardId))
            {
                projectedState.RestorePendingRobotFusion(
                    ToLocalOwnerId(view, view.PendingRobotFusionOwnerId),
                    view.PendingRobotFusionCardId);
            }

            if (view.IsEnded && view.HasWinner)
            {
                projectedState.EndBattle(ToLocalOwnerId(view, view.WinnerId));
            }

            return projectedState;
        }

        private static PlayerState CreatePlayerState(
            PlayerId id,
            BattlePlayerViewDto view,
            MasterState master,
            bool showHand)
        {
            var hand = new HandState();
            if (showHand && view?.VisibleHandCards != null && view.VisibleHandCards.Count > 0)
            {
                foreach (var card in view.VisibleHandCards)
                {
                    if (card == null || string.IsNullOrWhiteSpace(card.CardId))
                    {
                        continue;
                    }

                    hand.Add(
                        card.CardId,
                        string.IsNullOrWhiteSpace(card.RuntimeId)
                            ? Guid.NewGuid().ToString("N")
                            : card.RuntimeId,
                        card.IsTemporaryReplicate);
                }
            }
            else if (showHand && view?.VisibleHandCardIds != null)
            {
                foreach (var cardId in view.VisibleHandCardIds)
                {
                    hand.Add(cardId);
                }
            }
            else
            {
                AddPlaceholderCards(hand, view?.HandCount ?? 0, "hidden");
            }

            var discard = new DiscardState();
            AddPlaceholderCards(discard, view?.DiscardCount ?? 0, "discard");

            var playerState = new PlayerState(
                id,
                ToResourceSet(view?.Resources),
                new DeckState(CreatePlaceholderCardIds(view?.DeckCount ?? 0, "deck")),
                hand,
                discard,
                master);

            if (view != null)
            {
                if (view.HasUsedMulligan)
                {
                    playerState.MarkMulliganUsed();
                }

                var maxHandBonus = Math.Max(0, view.MaxHandSize - PlayerState.BaseMaxHandSize);
                playerState.IncreaseMaxHandSizeBonus(maxHandBonus);
            }

            return playerState;
        }

        private static OccupantState CreateOccupant(
            BattleStateViewDto stateView,
            BoardOccupantViewDto view,
            PlayerId localOwnerId)
        {
            var coord = view.Coord.ToDomain();
            OccupantState occupant;

            switch (view.Kind)
            {
                case OccupantKind.Master:
                    occupant = new MasterState(
                        string.IsNullOrWhiteSpace(view.RuntimeId) ? $"{localOwnerId}-master" : view.RuntimeId,
                        localOwnerId,
                        coord,
                        view.Attack,
                        view.MaxHp,
                        view.PhysicalDefense,
                        view.MagicDefense,
                        view.HasFlying,
                        view.SpellPower);
                    break;

                case OccupantKind.Building:
                    occupant = new BuildingState(
                        view.RuntimeId,
                        view.CardId,
                        localOwnerId,
                        coord,
                        canAttack: view.Attack > 0 || view.RemainingAttacksThisTurn > 0,
                        attack: view.Attack,
                        maxHp: view.MaxHp,
                        damageType: view.DamageType,
                        physicalDefense: view.PhysicalDefense,
                        magicDefense: view.MagicDefense,
                        hasFlying: view.HasFlying,
                        spellPower: view.SpellPower);
                    break;

                default:
                    occupant = new UnitState(
                        view.RuntimeId,
                        view.CardId,
                        localOwnerId,
                        coord,
                        view.AttackType,
                        view.Attack,
                        view.MaxHp,
                        view.CanMove,
                        isScience: false,
                        sciencePowerUpkeep: 0,
                        maxAttacksPerTurn: Math.Max(1, view.RemainingAttacksThisTurn),
                        hasEndure: view.HasEndure,
                        hasGuard: view.HasGuard,
                        hasLifeSteal: view.HasLifeSteal,
                        damageType: view.DamageType,
                        physicalDefense: view.PhysicalDefense,
                        magicDefense: view.MagicDefense,
                        hasRobot: view.HasRobot,
                        hasRush: view.HasRush,
                        hasHiding: view.HasHiding,
                        hasFlying: view.HasFlying,
                        spellPower: view.SpellPower);
                    break;
            }

            occupant.CurrentHp = view.CurrentHp;
            if (view.HasOriginalCombatStats)
            {
                occupant.RestoreOriginalCombatStats(
                    view.OriginalAttack,
                    view.OriginalMaxHp,
                    view.OriginalPhysicalDefense,
                    view.OriginalMagicDefense);
            }

            occupant.RestoreSuppressionStates(
                view.IsDrained,
                view.IsErasure,
                view.IsSealbound,
                view.SealboundOwnerTurnStartsRemaining,
                view.HidingRevealed);
            var invincibleEffects = new List<InvincibleEffectState>();
            if (view.InvincibleEffects != null)
            {
                foreach (var effectView in view.InvincibleEffects)
                {
                    if (effectView == null || effectView.Duration == InvincibleDurationType.None)
                    {
                        continue;
                    }

                    invincibleEffects.Add(new InvincibleEffectState(
                        effectView.Duration,
                        effectView.OwnerTurnsRemaining,
                        effectView.AppliedTurnNumber,
                        ToLocalOwnerId(stateView, effectView.AppliedActivePlayerId)));
                }
            }

            occupant.RestoreInvincibleEffects(invincibleEffects);
            occupant.WasSummonedThisTurn = view.WasSummonedThisTurn;
            occupant.HasSummoningSickness = view.HasSummoningSickness;
            occupant.RemainingAttacksThisTurn = view.RemainingAttacksThisTurn;
            occupant.EndureUsed = view.EndureUsed;
            return occupant;
        }

        private static void ApplyTurnState(BattleState battleState, BattleStateViewDto view)
        {
            var activePlayerId = ToLocalOwnerId(view, view.ActivePlayerId);
            for (var i = 0; i < view.TurnNumber; i++)
            {
                battleState.StartNextTurn(activePlayerId);
            }

            battleState.SetActivePlayer(activePlayerId);
            battleState.SetPhase(view.Phase);
        }

        private static void ApplyPersistentEffects(BattleState battleState, BattleStateViewDto view)
        {
            if (view.PersistentEffects == null)
            {
                return;
            }

            foreach (var effectView in view.PersistentEffects)
            {
                if (effectView == null)
                {
                    continue;
                }

                var persistentEffect = new PersistentEffectState(
                    sourceCardId: effectView.SourceCardId,
                    ownerId: ToLocalOwnerId(view, effectView.OwnerId),
                    effectId: effectView.EffectId,
                    appliedTurn: effectView.AppliedTurn,
                    endConditionText: effectView.EndConditionText,
                    turnStartResourceGain: ToResourceSet(effectView.TurnStartResourceGain),
                    ownerTurnStartsRemaining: effectView.OwnerTurnStartsRemaining,
                    targetRuntimeId: effectView.TargetRuntimeId,
                    targetRow: effectView.TargetRow,
                    remainingTriggers: effectView.RemainingTriggers,
                    effectDamage: effectView.EffectDamage,
                    effectDamageType: effectView.EffectDamageType,
                    targetsOwnerBoard: effectView.TargetsOwnerBoard,
                    capturedSpellPower: effectView.CapturedSpellPower);

                if (effectView.IsExpired)
                {
                    persistentEffect.Expire();
                }

                battleState.PersistentEffects.Add(persistentEffect);
            }
        }

        private static PlayerId ToLocalOwnerId(BattleStateViewDto view, PlayerId remoteOwnerId)
        {
            return remoteOwnerId == view.ViewerId ? PlayerId.Player : PlayerId.AI;
        }

        private static ResourceSet ToResourceSet(ResourceSetDto resources)
        {
            return resources == null
                ? new ResourceSet()
                : new ResourceSet(resources.Mana, resources.Qi, resources.Power, resources.Gold);
        }

        private static MasterState CreateFallbackMaster(PlayerId ownerId)
        {
            return new MasterState(
                $"{ownerId}-projected-master",
                ownerId,
                new TileCoord(2, 1),
                attack: 3,
                maxHp: 333);
        }

        private static void EnsureMasterPlaced(BoardState board, MasterState master)
        {
            if (board.GetOccupant(master.Position) == null)
            {
                board.Place(master.Position, master);
            }
        }

        private static void AddPlaceholderCards(HandState hand, int count, string prefix)
        {
            foreach (var cardId in CreatePlaceholderCardIds(count, prefix))
            {
                hand.Add(cardId);
            }
        }

        private static void AddPlaceholderCards(DiscardState discard, int count, string prefix)
        {
            foreach (var cardId in CreatePlaceholderCardIds(count, prefix))
            {
                discard.Add(cardId);
            }
        }

        private static string[] CreatePlaceholderCardIds(int count, string prefix)
        {
            if (count <= 0)
            {
                return Array.Empty<string>();
            }

            var cardIds = new string[count];
            for (var i = 0; i < cardIds.Length; i++)
            {
                cardIds[i] = $"{prefix}_{i:00}";
            }

            return cardIds;
        }
    }
}
