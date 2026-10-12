using System;
using System.Text;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;

namespace Project333.Runtime.Application.Services
{
    public static class AiBattleStateCopy
    {
        public const string UnknownCardId = "__ai_unknown_card__";

        public static BattleState Create(BattleState source, bool hidePrivateZones = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var playerMaster = (MasterState)source.Player.Master.CloneDetached();
            var aiMaster = (MasterState)source.AI.Master.CloneDetached();
            var result = new BattleState(
                CopyPlayer(source.Player, playerMaster, hidePrivateZones),
                CopyPlayer(source.AI, aiMaster, hidePrivateZones),
                CopyBoard(source.PlayerBoard, source.Player.Master, playerMaster),
                CopyBoard(source.AIBoard, source.AI.Master, aiMaster));
            foreach (var effect in source.PersistentEffects)
            {
                var copy = new PersistentEffectState(effect.SourceCardId, effect.OwnerId, effect.EffectId,
                    effect.AppliedTurn, effect.EndConditionText, effect.TurnStartResourceGain.Clone(),
                    effect.OwnerTurnStartsRemaining, effect.TargetRuntimeId, effect.TargetRow,
                    effect.RemainingTriggers, effect.EffectDamage, effect.EffectDamageType,
                    effect.TargetsOwnerBoard, effect.CapturedSpellPower, effect.TargetStartColumn);
                copy.RestoreRuntimeState(effect.OwnerTurnStartsRemaining, effect.IsExpired, effect.RemainingTriggers);
                result.PersistentEffects.Add(copy);
            }
            // Observation copies retain only the AI's own secret identities. Repeated search
            // copies preserve already-hidden counts without turning them into synthetic cards.
            foreach (var trap in source.ArmedTraps)
            {
                if (trap == null || hidePrivateZones && trap.OwnerId != PlayerId.AI) continue;
                result.ArmedTraps.Add(new ArmedTrapState(trap.RuntimeId, trap.OwnerId, trap.CardId,
                    trap.TriggerType, trap.EffectType, trap.ActivationCost, trap.InstalledTurn,
                    trap.UpgradeLevel, trap.IsTemporaryReplicate, trap.EffectAmount));
            }
            result.Player.RestoreHiddenTrapCount(hidePrivateZones
                ? source.GetArmedTrapCount(PlayerId.Player) : source.Player.HiddenTrapCount);
            result.AI.RestoreHiddenTrapCount(source.AI.HiddenTrapCount);
            result.RestoreRuntimeState(source.TurnNumber, source.ActivePlayerId, source.Phase);
            result.Counters.Restore(source.Counters.ActionSequence);
            if (source.PendingRobotFusion != null)
                result.RestorePendingRobotFusion(source.PendingRobotFusion.OwnerId, source.PendingRobotFusion.CardId);
            if (source.Result.IsDraw) result.EndBattleAsDraw();
            else if (source.Result.HasWinner) result.EndBattle(source.Result.Winner);
            return result;
        }

        private static PlayerState CopyPlayer(PlayerState source, MasterState master, bool hide)
        {
            var deck = new string[source.Deck.Count];
            for (var i = 0; i < deck.Length; i++)
                deck[i] = hide ? UnknownCardId : source.Deck.CardIds[i];
            var hand = new HandState();
            for (var i = 0; i < source.Hand.Count; i++)
            {
                var card = source.Hand.Cards[i];
                var hidden = hide && source.Id == PlayerId.Player;
                hand.Add(hidden ? UnknownCardId : card.CardId,
                    hidden ? "hidden-" + i : card.RuntimeId, !hidden && card.IsTemporaryReplicate);
            }
            var discard = new DiscardState();
            foreach (var card in source.Discard.CardIds) discard.Add(card);
            var result = new PlayerState(source.Id, source.Resources.Clone(), new DeckState(deck), hand, discard, master);
            result.RestoreRuntimeState(source.FailedDrawCount, source.HasUsedMulligan, source.MaxHandSizeBonus);
            return result;
        }

        private static BoardState CopyBoard(BoardState source, MasterState originalMaster, MasterState master)
        {
            var board = new BoardState();
            board.RestoreFalloutStacks(source.FalloutStacks);
            foreach (var occupant in source.EnumerateOccupants())
                board.Place(occupant.Position, ReferenceEquals(occupant, originalMaster) ? master : occupant.CloneDetached(),
                    synchronizeAuras: false);
            board.RefreshOrcChieftainAuras();
            return board;
        }

        // Excludes hand/summon GUIDs, event queues and hidden identities; persistent target references remain significant.
        public static string PositionKey(BattleState state)
        {
            var key = new StringBuilder();
            key.Append(state.TurnNumber).Append('/').Append(state.ActivePlayerId).Append('/').Append(state.Phase)
                .Append('/').Append(state.Result.HasResult).Append('/').Append(state.Result.Winner)
                .Append('/').Append(state.Result.IsDraw).Append('/').Append(state.PendingRobotFusion?.CardId);
            foreach (var id in new[] { PlayerId.AI, PlayerId.Player })
            {
                var player = state.GetPlayer(id);
                key.Append('|').Append(id).Append(':').Append(player.Master.CurrentHp).Append(':')
                    .Append(player.Resources.Mana).Append(',').Append(player.Resources.Qi).Append(',')
                    .Append(player.Resources.Power).Append(',').Append(player.Resources.Gold).Append(':')
                    .Append(player.Deck.Count).Append(',').Append(player.FailedDrawCount).Append(',').Append(player.MaxHandSize);
                key.Append(':').Append(state.GetBoard(id).FalloutStacks);
                key.Append(':').Append(player.Hand.Count);
                if (id == PlayerId.AI)
                    foreach (var card in player.Hand.Cards)
                        key.Append(';').Append(card.CardId).Append(',').Append(card.IsTemporaryReplicate);
                foreach (var o in state.GetBoard(id).EnumerateOccupants())
                {
                    AppendOccupantKey(key, o, includePositionAndHp: true);
                }
            }
            // Only public enemy counts enter the key. Own installation order and captured
            // rules are significant, whereas random hand/runtime GUIDs are not.
            key.Append("|enemy-traps:").Append(state.GetArmedTrapCount(PlayerId.Player));
            foreach (var trap in state.ArmedTraps)
                if (trap != null && trap.OwnerId == PlayerId.AI)
                {
                    var cost = trap.ActivationCost;
                    key.Append("|own-trap:").Append(trap.CardId).Append(',').Append(trap.TriggerType)
                        .Append(',').Append(trap.EffectType).Append(',').Append(trap.InstalledTurn)
                        .Append(',').Append(trap.UpgradeLevel).Append(',').Append(trap.IsTemporaryReplicate).Append(',').Append(trap.EffectAmount)
                        .Append(',').Append(cost.Mana).Append(',').Append(cost.Qi)
                        .Append(',').Append(cost.Power).Append(',').Append(cost.Gold);
                }
            foreach (var effect in state.PersistentEffects)
                key.Append('|').Append(effect.EffectId).Append(',').Append(effect.OwnerId).Append(',')
                    .Append(effect.TargetRow).Append(',').Append(effect.TargetStartColumn).Append(',')
                    .Append(effect.TargetRuntimeId).Append(',').Append(effect.RemainingTriggers).Append(',')
                    .Append(effect.OwnerTurnStartsRemaining).Append(',').Append(effect.TargetsOwnerBoard).Append(',')
                    .Append(effect.EffectDamage).Append(',').Append(effect.EffectDamageType).Append(',').Append(effect.IsExpired)
                    .Append(',').Append(effect.AppliedTurn).Append(',').Append(effect.CapturedSpellPower)
                    .Append(',').Append(effect.TurnStartResourceGain.Mana).Append(',').Append(effect.TurnStartResourceGain.Qi)
                    .Append(',').Append(effect.TurnStartResourceGain.Power).Append(',').Append(effect.TurnStartResourceGain.Gold);
            return key.ToString();
        }

        // Share the search-state fields so focus targeting cannot silently ignore a new tracked status.
        internal static string FocusTargetKey(OccupantState occupant)
        {
            var key = new StringBuilder();
            AppendOccupantKey(key, occupant, includePositionAndHp: false);
            key.Append(':').Append(occupant.Kind).Append(',').Append(occupant.Attack)
                .Append(',').Append(occupant.IsInvincible).Append(',').Append(occupant.HuanShuOwnerTurnsRemaining)
                .Append(',').Append(occupant.IsDemonKingRevivalPending);
            return key.ToString();
        }

        private static void AppendOccupantKey(StringBuilder key, OccupantState o, bool includePositionAndHp)
        {
            if (includePositionAndHp) key.Append(';').Append(o.Position).Append(':');
            key.Append(o.CardId).Append(',').Append(o.BaseAttack);
            if (includePositionAndHp) key.Append(',').Append(o.CurrentHp);
            key.Append(',').Append(o.MaxHp).Append(',').Append(o.RemainingAttacksThisTurn)
                .Append(',').Append(o.HasSummoningSickness).Append(',').Append(o.WasSummonedThisTurn)
                .Append(',').Append(o.IsDrained).Append(',').Append(o.IsErasure).Append(',').Append(o.IsSealbound)
                .Append(',').Append(o.StunTurnEndsRemaining).Append(',').Append(o.FreezeTurnEndsRemaining)
                .Append(',').Append(o.SealboundOwnerTurnStartsRemaining).Append(',').Append(o.EndureUsed)
                .Append(',').Append(o.HidingRevealed).Append(',').Append(o.IsUnderHuanShu)
                .Append(',').Append(o.HuanShuEligibleAfterTurnNumber).Append(',').Append(o.PhysicalDefense)
                .Append(',').Append(o.MagicDefense).Append(',').Append(o.DemonKingRevivalCount)
                .Append(',').Append(o.DemonKingRevivalTurnStartsRemaining).Append(',').Append(o.OwnerId)
                .Append(',').Append(o.AttackType).Append(',').Append(o.DamageType).Append(',').Append(o.CanMove)
                .Append(',').Append(o.MaxAttacksPerTurn).Append(',').Append(o.HitsPerAttack)
                .Append(',').Append(o.HasBerserker).Append(',').Append(o.HasEndure).Append(',').Append(o.HasShielder)
                .Append(',').Append(o.HasLifeSteal).Append(',').Append(o.HasRush).Append(',').Append(o.HasHiding)
                .Append(',').Append(o.HasFlying).Append(',').Append(o.HasPiercing).Append(',').Append(o.SpellPower)
                .Append(',').Append(o.OriginalAttack).Append(',').Append(o.OriginalMaxHp)
                .Append(',').Append(o.OriginalPhysicalDefense).Append(',').Append(o.OriginalMagicDefense)
                .Append(',').Append(o.DemonKingRevivalCountdownPlayerId).Append(',').Append(o.DemonKingRevivalEligibleAfterTurnNumber)
                .Append(',').Append(o.FalloutPlayerStacks).Append(',').Append(o.FalloutAiStacks)
                .Append(',').Append(o.LastActiveEffectTurnNumber)
                .Append(',').Append(o.TurnStartResourceGain.Mana).Append(',').Append(o.TurnStartResourceGain.Qi)
                .Append(',').Append(o.TurnStartResourceGain.Power).Append(',').Append(o.TurnStartResourceGain.Gold);
            if (o is UnitState u) key.Append(',').Append(string.Join("|", u.Races)).Append(',').Append(u.SciencePowerUpkeep);
            if (o is BuildingState b) key.Append(',').Append(b.SciencePowerUpkeep).Append(',').Append(b.CanAttackAsBuilding);
            foreach (var buff in o.CombatStatBuffs)
                key.Append('~').Append(buff.SourceCardId).Append(',').Append(buff.SourceRuntimeId).Append(',').Append(buff.AttackBonus)
                    .Append(',').Append(buff.MaxHpBonus).Append(',').Append(buff.PhysicalDefenseBonus)
                    .Append(',').Append(buff.MagicDefenseBonus);
            foreach (var runtimeId in o.ErasedOrcChieftainSourceRuntimeIds)
                key.Append("~erased-chief,").Append(runtimeId);
            foreach (var effect in o.InvincibleEffects)
                key.Append('!').Append(effect.Duration).Append(',').Append(effect.OwnerTurnsRemaining)
                    .Append(',').Append(effect.AppliedTurnNumber).Append(',').Append(effect.AppliedActivePlayerId);
        }
    }
}
