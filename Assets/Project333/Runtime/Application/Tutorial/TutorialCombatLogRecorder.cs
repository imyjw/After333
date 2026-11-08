using System;
using System.Collections.Generic;
using System.Linq;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Tutorial
{
    // Read-only projection of the authored local lesson into the same records as online combat.
    // It never pays resources, resolves damage or clears the shared presentation events.
    public sealed class TutorialCombatLogRecorder
    {
        private Dictionary<string, OccupantState> _before;
        private Dictionary<string, BattleCombatLogCardSnapshotDto> _previewBefore;
        private static Dictionary<string, BattleCombatLogCardSnapshotDto> Previews(BattleState b) =>
            b.PlayerBoard.EnumerateOccupants().Concat(b.AIBoard.EnumerateOccupants())
                .ToDictionary(o => o.RuntimeId, BattleCombatLogCardSnapshotDto.Capture);
        private PhaseType _phase;
        private PlayerId _owner;
        private int _turn;
        private bool _ended;
        private BattleAttackResolution _attack;
        private OccupantState _lastSummoned;
        private readonly HashSet<BattleValuePopupEvent> _values = new HashSet<BattleValuePopupEvent>();
        private readonly HashSet<BattleResourceChangeEvent> _resources = new HashSet<BattleResourceChangeEvent>();
        private readonly HashSet<BattleTrapEvent> _traps = new HashSet<BattleTrapEvent>();
        private readonly HashSet<PersistentEffectState> _persistentEffects = new HashSet<PersistentEffectState>();
        private readonly HashSet<BattleCardDrawEvent> _draws = new HashSet<BattleCardDrawEvent>();
        private readonly HashSet<BattleCardGenerationEvent> _generations = new HashSet<BattleCardGenerationEvent>();
        private readonly ICardDefinitionProvider _cardDefinitions;
        public TutorialCombatLogRecorder(BattleState battle, ICardDefinitionProvider cardDefinitions = null)
        {
            _cardDefinitions = cardDefinitions;
            Rebase(battle);
        }
        private static Dictionary<string, OccupantState> Occupants(BattleState b) =>
            b.PlayerBoard.EnumerateOccupants().Concat(b.AIBoard.EnumerateOccupants())
                .ToDictionary(o => o.RuntimeId, o => o.CloneDetached());
        public void Rebase(BattleState b)
        {
            _before = Occupants(b); _previewBefore = Previews(b); _phase = b.Phase; _owner = b.ActivePlayerId; _turn = b.TurnNumber;
            _ended = b.IsEnded; _attack = b.LastAttackResolution; _lastSummoned = b.LastSummonedOccupant;
            _values.Clear(); _values.UnionWith(b.ValuePopupEvents);
            _resources.Clear(); _resources.UnionWith(b.ResourceChangeEvents);
            _traps.Clear(); _traps.UnionWith(b.TrapEvents);
            _persistentEffects.Clear(); _persistentEffects.UnionWith(b.PersistentEffects);
            _draws.Clear(); _draws.UnionWith(b.CardDrawEvents);
            _generations.Clear(); _generations.UnionWith(b.CardGenerationEvents);
        }
        public IReadOnlyList<BattleCombatLogEntryDto> Capture(BattleState b)
        {
            var result = new List<BattleCombatLogEntryDto>();
            var after = Occupants(b);
            var previewAfter = Previews(b);
            var values = b.ValuePopupEvents.Where(v => _values.Add(v)).ToList();
            var resources = b.ResourceChangeEvents.Where(v => _resources.Add(v)).ToList();
            var traps = b.TrapEvents.Where(t => _traps.Add(t)).ToList();
            var newEffects = b.PersistentEffects.Where(e => e != null && _persistentEffects.Add(e)).ToList();
            var draws = b.CardDrawEvents.Where(d => d != null && _draws.Add(d)).ToList();
            var generations = b.CardGenerationEvents.Where(g => g != null && _generations.Add(g)).ToList();
            foreach (var trap in traps)
            {
                if (trap == null) continue;
                var triggered = trap.Kind == BattleTrapEventKind.Triggered;
                var trapEntry = new BattleCombatLogEntryDto
                {
                    EntryType = triggered ? BattleCombatLogEntryType.TrapTriggered : BattleCombatLogEntryType.TrapInstalled,
                    SourceOwnerId = trap.OwnerId,
                    SourceCardId = triggered ? trap.CardId : string.Empty,
                    Amount = trap.ArmedTrapCount
                };
                if (triggered)
                {
                    trapEntry.TargetOwnerId = trap.TargetOwnerId;
                    trapEntry.TargetCardId = trap.TargetCardId;
                    if (trap.TargetCoord.HasValue) trapEntry.TargetCoord = Coord(trap.TargetCoord.Value);
                    trapEntry.SourcePreview = BattleCombatLogCardSnapshotDto.CreateSpellCard(trap.CardId, trap.OwnerId, trap.RuntimeId);
                    var trapTarget = BuildTrapTargetPreview(b, trap, values, after);
                    if (trapTarget != null)
                    {
                        trapEntry.TargetPreview = trapTarget;
                        trapEntry.TargetPreviews = new List<BattleCombatLogCardSnapshotDto> { trapTarget.Clone() };
                    }
                }
                result.Add(trapEntry);
            }
            if (_phase == PhaseType.Main && b.TurnNumber != _turn)
                result.Add(new BattleCombatLogEntryDto { EntryType = BattleCombatLogEntryType.TurnEnded, SourceOwnerId = _owner });
            var turnStarted = b.Phase == PhaseType.Main && (_phase != PhaseType.Main || b.TurnNumber != _turn);
            if (turnStarted)
                result.Add(new BattleCombatLogEntryDto { EntryType = BattleCombatLogEntryType.TurnStarted, SourceOwnerId = b.ActivePlayerId, TurnNumber = b.TurnNumber });
            if (turnStarted) foreach (var resource in resources)
            {
                AddResources(result, resource, resource.Spent, BattleCombatLogEntryType.ResourceSpent, ResourceSourcePreview(b, resource));
                AddResources(result, resource, resource.Gained, BattleCombatLogEntryType.ResourceGained, ResourceSourcePreview(b, resource));
            }
            if (turnStarted) AddGenerations(result, b, generations);
            var loggedMoves = new HashSet<string>();
            foreach (var o in after.Values)
            {
                if (!_before.TryGetValue(o.RuntimeId, out var previous))
                {
                    result.Add(new BattleCombatLogEntryDto { EntryType = o.Kind == OccupantKind.Building ? BattleCombatLogEntryType.BuildingConstructed : BattleCombatLogEntryType.UnitSummoned,
                        SourceOwnerId = o.OwnerId, SourceCardId = o.CardId, TargetCoord = Coord(o.Position),
                        SourcePreview = BattleCombatLogCardSnapshotDto.CaptureOriginal(o) });
                    if (o.IsSealbound && !o.IsDemonKingRevivalPending)
                        AddStatusLog(result, BattleCombatLogEntryType.SealboundApplied, o, previewAfter[o.RuntimeId], o.SealboundOwnerTurnStartsRemaining);
                    if (o.IsHiding)
                        AddStatusLog(result, BattleCombatLogEntryType.HidingApplied, o, previewAfter[o.RuntimeId]);
                }
                else
                {
                    if (!previous.Position.Equals(o.Position) && loggedMoves.Add(o.RuntimeId))
                    {
                        var partner = after.Values.FirstOrDefault(other => other.RuntimeId != o.RuntimeId && other.OwnerId == o.OwnerId &&
                            other.Position.Equals(previous.Position) && _before.TryGetValue(other.RuntimeId, out var oldPartner) &&
                            oldPartner.Position.Equals(o.Position));
                        if (partner != null)
                        {
                            loggedMoves.Add(partner.RuntimeId);
                            result.Add(new BattleCombatLogEntryDto { EntryType = BattleCombatLogEntryType.OccupantsSwapped,
                                SourceOwnerId = o.OwnerId, SourceCardId = o.CardId, TargetCardId = partner.CardId,
                                SourceCoord = Coord(previous.Position), TargetCoord = Coord(o.Position) });
                        }
                        else result.Add(new BattleCombatLogEntryDto { EntryType = BattleCombatLogEntryType.OccupantMoved,
                            SourceOwnerId = o.OwnerId, SourceCardId = o.CardId, SourceCoord = Coord(previous.Position), TargetCoord = Coord(o.Position) });
                    }
                    if (o.LastActiveEffectTurnNumber != previous.LastActiveEffectTurnNumber && o.LastActiveEffectTurnNumber >= 0 &&
                        _previewBefore.TryGetValue(o.RuntimeId, out var activeBefore))
                        result.Add(new BattleCombatLogEntryDto { EntryType = BattleCombatLogEntryType.ActiveEffectActivated,
                            SourceOwnerId = o.OwnerId, SourceCardId = o.CardId, SourceCoord = Coord(o.Position),
                            SourcePreview = activeBefore.Clone() });
                }
            }
            var lastSummoned = b.LastSummonedOccupant;
            if (lastSummoned != null && !ReferenceEquals(lastSummoned, _lastSummoned) &&
                !_before.ContainsKey(lastSummoned.RuntimeId) && !after.ContainsKey(lastSummoned.RuntimeId))
                result.Add(new BattleCombatLogEntryDto { EntryType = lastSummoned.Kind == OccupantKind.Building ? BattleCombatLogEntryType.BuildingConstructed : BattleCombatLogEntryType.UnitSummoned,
                    SourceOwnerId = lastSummoned.OwnerId, SourceCardId = lastSummoned.CardId, TargetCoord = Coord(lastSummoned.Position),
                    SourcePreview = BattleCombatLogCardSnapshotDto.CaptureOriginal(lastSummoned) });
            var attack = b.LastAttackResolution;
            if (attack != null && !ReferenceEquals(attack, _attack) && !attack.WasCancelledByTrap &&
                _before.TryGetValue(attack.AttackerRuntimeId, out var attacker) &&
                _before.TryGetValue(attack.ResolvedTargetRuntimeId, out var target))
            {
                var hits = values.Where(v => v.RuntimeId == target.RuntimeId && v.Cause == BattleValueChangeCause.NormalAttack).ToList();
                var counters = values.Where(v => v.RuntimeId == attacker.RuntimeId && v.Cause == BattleValueChangeCause.Counterattack).ToList();
                result.Add(new BattleCombatLogEntryDto { EntryType = BattleCombatLogEntryType.Attack,
                    SourceOwnerId = attacker.OwnerId, TargetOwnerId = target.OwnerId,
                    SourceCardId = attacker.CardId, TargetCardId = target.CardId,
                    SourceCoord = Coord(attacker.Position), TargetCoord = Coord(target.Position),
                    AttackType = attacker.AttackType, DamageType = attacker.DamageType,
                    SourcePreview = _previewBefore[attacker.RuntimeId].WithOutcome(counters.Sum(v => v.Amount),
                        Removed(after, attacker.RuntimeId) || counters.Any(v => v.HpAfter <= 0)),
                    TargetPreview = _previewBefore[target.RuntimeId].WithOutcome(hits.Sum(v => v.Amount),
                        Removed(after, target.RuntimeId) || hits.Any(v => v.HpAfter <= 0)),
                    SourceHpHistory = counters.Count == 0 ? new List<int>() : History(counters, attacker.CurrentHp),
                    TargetHpHistory = History(hits, target.CurrentHp), HasCounterattack = counters.Count > 0,
                    SourceRemoved = Removed(after, attacker.RuntimeId), TargetRemoved = Removed(after, target.RuntimeId),
                    SourceDamagePrevented = counters.Any(v => v.IsInvinciblePrevented), TargetDamagePrevented = hits.Any(v => v.IsInvinciblePrevented) });
            }
            // The lesson's targeted damage spell uses the normal SpellCast + damage records.
            foreach (var spell in values.Where(v => (v.Cause == BattleValueChangeCause.Spell || (!turnStarted && v.Cause == BattleValueChangeCause.Firewall)) && !string.IsNullOrEmpty(v.SourceCardId))
                         .GroupBy(v => new { v.SourceCardId, v.SourceOwnerId }))
            {
                var spellEvents = spell.ToList();
                var spellTargets = BuildBatchTargets(spellEvents, after);
                result.Add(new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.SpellCast,
                    SourceOwnerId = spell.Key.SourceOwnerId,
                    TargetOwnerId = spellTargets.Count > 0 ? spellTargets[0].OwnerId : spell.Key.SourceOwnerId,
                    SourceCardId = spell.Key.SourceCardId,
                    SourcePreview = BattleCombatLogCardSnapshotDto.CreateSpellCard(spell.Key.SourceCardId, spell.Key.SourceOwnerId),
                    TargetPreview = spellTargets.Count > 0 ? spellTargets[0].Clone() : null,
                    TargetPreviews = spellTargets.Select(t => t.Clone()).ToList()
                });
            }
            foreach (var effect in newEffects)
            {
                if (string.IsNullOrEmpty(effect.SourceCardId) ||
                    result.Any(r => r.EntryType == BattleCombatLogEntryType.SpellCast && r.SourceCardId == effect.SourceCardId && r.SourceOwnerId == effect.OwnerId))
                    continue;
                BattleCombatLogCardSnapshotDto targetPreview = null;
                if (!string.IsNullOrEmpty(effect.TargetRuntimeId) && _previewBefore.TryGetValue(effect.TargetRuntimeId, out var marked))
                {
                    targetPreview = marked.Clone().WithOutcome(0, false);
                }
                result.Add(new BattleCombatLogEntryDto
                {
                    EntryType = BattleCombatLogEntryType.SpellCast,
                    SourceOwnerId = effect.OwnerId,
                    TargetOwnerId = targetPreview != null ? targetPreview.OwnerId : effect.OwnerId,
                    SourceCardId = effect.SourceCardId,
                    SourcePreview = BattleCombatLogCardSnapshotDto.CreateSpellCard(effect.SourceCardId, effect.OwnerId),
                    TargetPreview = targetPreview?.Clone(),
                    TargetPreviews = targetPreview != null ? new List<BattleCombatLogCardSnapshotDto> { targetPreview.Clone() } : new List<BattleCombatLogCardSnapshotDto>()
                });
            }
            var nonAttackValues = values.Where(v => v.Cause != BattleValueChangeCause.NormalAttack && v.Cause != BattleValueChangeCause.Counterattack && v.Amount > 0).ToList();
            foreach (var batch in nonAttackValues.GroupBy(v => new { v.IsHealing, v.Cause, v.SourceOwnerId, SourceCardId = v.SourceCardId ?? string.Empty, SourceRuntimeId = v.SourceRuntimeId ?? string.Empty }))
            {
                var groupList = batch.ToList();
                var batchTargets = BuildBatchTargets(groupList, after);
                var sourcePreview = BuildEffectSourcePreview(batch.Key.SourceOwnerId, batch.Key.SourceCardId, batch.Key.SourceRuntimeId, batch.Key.Cause, groupList[0]);
                foreach (var value in groupList)
                {
                    var affected = _before.TryGetValue(value.RuntimeId, out var old) ? old :
                        after.TryGetValue(value.RuntimeId, out var current) ? current : null;
                    var singleTarget = BuildSingleTarget(value, after);
                    result.Add(new BattleCombatLogEntryDto
                    {
                        EntryType = value.IsHealing ? BattleCombatLogEntryType.Healing : BattleCombatLogEntryType.Damage,
                        SourceOwnerId = value.SourceOwnerId,
                        TargetOwnerId = value.OwnerId,
                        SourceCardId = value.SourceCardId,
                        TargetCardId = affected?.CardId ?? "Master",
                        TargetCoord = Coord(value.Coord),
                        Amount = value.Amount,
                        DamageType = value.DamageType,
                        ValueCause = value.Cause,
                        TargetHpHistory = new List<int> { value.HpBefore, value.HpAfter },
                        TargetRemoved = Removed(after, value.RuntimeId),
                        TargetDamagePrevented = value.IsInvinciblePrevented,
                        SourcePreview = sourcePreview?.Clone(),
                        TargetPreview = singleTarget,
                        TargetPreviews = batchTargets.Select(t => t.Clone()).ToList()
                    });
                }
            }
            if (!turnStarted) foreach (var resource in resources)
            {
                AddResources(result, resource, resource.Spent, BattleCombatLogEntryType.ResourceSpent, ResourceSourcePreview(b, resource));
                AddResources(result, resource, resource.Gained, BattleCombatLogEntryType.ResourceGained, ResourceSourcePreview(b, resource));
            }
            if (!turnStarted) AddGenerations(result, b, generations);
            AddDraws(result, b, draws);
            foreach (var o in after.Values)
            {
                if (!_before.TryGetValue(o.RuntimeId, out var previous) || !previewAfter.TryGetValue(o.RuntimeId, out var currentPreview))
                    continue;
                if (!previous.IsDrained && o.IsDrained) AddStatusLog(result, BattleCombatLogEntryType.DrainedApplied, o, currentPreview);
                if (previous.IsDrained && !o.IsDrained) AddStatusLog(result, BattleCombatLogEntryType.DrainedCleared, o, currentPreview);
                if (!previous.IsErasure && o.IsErasure) AddStatusLog(result, BattleCombatLogEntryType.ErasureApplied, o, currentPreview);
                if (previous.IsErasure && !o.IsErasure) AddStatusLog(result, BattleCombatLogEntryType.ErasureCleared, o, currentPreview);
                if (!previous.IsStunned && o.IsStunned) AddStatusLog(result, BattleCombatLogEntryType.StunApplied, o, currentPreview);
                if (previous.IsStunned && !o.IsStunned) AddStatusLog(result, BattleCombatLogEntryType.StunCleared, o, currentPreview);
                if (!previous.IsFrozen && o.IsFrozen) AddStatusLog(result, BattleCombatLogEntryType.FreezeApplied, o, currentPreview);
                if (previous.IsFrozen && !o.IsFrozen) AddStatusLog(result, BattleCombatLogEntryType.FreezeCleared, o, currentPreview);
                if (!previous.IsSealbound && o.IsSealbound && !o.IsDemonKingRevivalPending)
                    AddStatusLog(result, BattleCombatLogEntryType.SealboundApplied, o, currentPreview, o.SealboundOwnerTurnStartsRemaining);
                if (previous.IsSealbound && !previous.IsDemonKingRevivalPending && !o.IsSealbound)
                    AddStatusLog(result, BattleCombatLogEntryType.SealboundReleased, o, currentPreview);
                if (!previous.IsHiding && o.IsHiding) AddStatusLog(result, BattleCombatLogEntryType.HidingApplied, o, currentPreview);
                if (previous.IsHiding && !o.IsHiding) AddStatusLog(result, BattleCombatLogEntryType.HidingRevealed, o, currentPreview);
                if (!previous.IsUnderHuanShu && o.IsUnderHuanShu) AddStatusLog(result, BattleCombatLogEntryType.HuanShuApplied, o, currentPreview);
                if (previous.IsUnderHuanShu && !o.IsUnderHuanShu) AddStatusLog(result, BattleCombatLogEntryType.HuanShuCleared, o, currentPreview);
            }
            if (!_ended && b.IsEnded)
                result.Add(new BattleCombatLogEntryDto { EntryType = BattleCombatLogEntryType.BattleEnded,
                    TargetOwnerId = b.Result.Winner, IsDraw = b.Result.IsDraw });
            _before = after; _previewBefore = previewAfter; _phase = b.Phase; _owner = b.ActivePlayerId; _turn = b.TurnNumber; _ended = b.IsEnded; _attack = attack; _lastSummoned = lastSummoned;
            return result;
        }
        private BattleCombatLogCardSnapshotDto BuildTrapTargetPreview(
            BattleState b,
            BattleTrapEvent trap,
            List<BattleValuePopupEvent> values,
            Dictionary<string, OccupantState> after)
        {
            BattleCombatLogCardSnapshotDto preview = null;
            if (!string.IsNullOrEmpty(trap.TargetRuntimeId) && _previewBefore.TryGetValue(trap.TargetRuntimeId, out var beforePreview))
            {
                preview = beforePreview.Clone();
            }
            else if (b.LastSummonedOccupant != null && b.LastSummonedOccupant.RuntimeId == trap.TargetRuntimeId)
            {
                preview = BattleCombatLogCardSnapshotDto.Capture(b.LastSummonedOccupant);
                if (b.LastSummonedAttack > 0) preview.Attack = b.LastSummonedAttack;
            }
            if (preview == null) return null;
            preview.OwnerId = trap.TargetOwnerId;
            var hits = values.Where(v => v.RuntimeId == trap.TargetRuntimeId && !v.IsHealing).ToList();
            if (hits.Count > 0 && hits[0].HpBefore > 0) preview.Hp = hits[0].HpBefore;
            if (string.Equals(trap.CardId, "TrapReturn", StringComparison.Ordinal))
            {
                return preview.WithOutcome(0, false);
            }
            var died = Removed(after, trap.TargetRuntimeId) || hits.Any(v => v.HpAfter <= 0);
            if (after.TryGetValue(trap.TargetRuntimeId, out var rem) && rem.IsDemonKingRevivalPending) died = false;
            return preview.WithOutcome(hits.Sum(v => Math.Max(0, v.Amount)), died);
        }
        private List<BattleCombatLogCardSnapshotDto> BuildBatchTargets(
            List<BattleValuePopupEvent> groupEvents,
            Dictionary<string, OccupantState> after)
        {
            var list = new List<BattleCombatLogCardSnapshotDto>();
            var indexByRuntimeId = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var v in groupEvents)
            {
                var key = !string.IsNullOrEmpty(v.RuntimeId) ? v.RuntimeId : $"{v.OwnerId}:{v.Coord.Column},{v.Coord.Row}";
                if (!indexByRuntimeId.TryGetValue(key, out var idx))
                {
                    var single = BuildSingleTarget(v, after);
                    if (single == null) continue;
                    indexByRuntimeId[key] = list.Count;
                    list.Add(single);
                }
                else
                {
                    var existing = list[idx];
                    if (v.IsHealing) existing.HealAmount += Math.Max(0, v.Amount);
                    else
                    {
                        existing.DamageTaken += Math.Max(0, v.Amount);
                        if (Removed(after, v.RuntimeId) || v.HpAfter <= 0) existing.Died = true;
                    }
                }
            }
            return list;
        }
        private BattleCombatLogCardSnapshotDto BuildSingleTarget(
            BattleValuePopupEvent v,
            Dictionary<string, OccupantState> after)
        {
            BattleCombatLogCardSnapshotDto preview = null;
            if (!string.IsNullOrEmpty(v.RuntimeId) && _previewBefore.TryGetValue(v.RuntimeId, out var beforePreview))
            {
                preview = beforePreview.Clone();
            }
            else if (!string.IsNullOrEmpty(v.RuntimeId) && after.TryGetValue(v.RuntimeId, out var current))
            {
                preview = BattleCombatLogCardSnapshotDto.Capture(current);
            }
            if (preview == null) return null;
            preview.OwnerId = v.OwnerId;
            if (v.HpBefore > 0) preview.Hp = v.HpBefore;
            if (v.IsHealing) return preview.WithHealOutcome(Math.Max(0, v.Amount));
            if (v.IsInvinciblePrevented) return preview.WithOutcome(0, false);
            var died = Removed(after, v.RuntimeId) || v.HpAfter <= 0;
            if (after.TryGetValue(v.RuntimeId, out var rem) && rem.IsDemonKingRevivalPending) died = false;
            return preview.WithOutcome(Math.Max(0, v.Amount), died);
        }
        private BattleCombatLogCardSnapshotDto BuildEffectSourcePreview(
            PlayerId sourceOwnerId,
            string sourceCardId,
            string sourceRuntimeId,
            BattleValueChangeCause cause,
            BattleValuePopupEvent sample)
        {
            if (!string.IsNullOrEmpty(sourceRuntimeId) && _previewBefore.TryGetValue(sourceRuntimeId, out var byId))
            {
                var copy = byId.Clone();
                copy.OwnerId = sourceOwnerId;
                if (cause == BattleValueChangeCause.LifeSteal && sample != null && sample.RuntimeId == sourceRuntimeId && sample.HpBefore > 0)
                    copy.Hp = sample.HpBefore;
                return copy;
            }
            if (cause == BattleValueChangeCause.DeckExhaustion || string.Equals(sourceCardId, "Master", StringComparison.OrdinalIgnoreCase))
            {
                var master = _before.Values.FirstOrDefault(o => o.OwnerId == sourceOwnerId && o.Kind == OccupantKind.Master);
                if (master != null && _previewBefore.TryGetValue(master.RuntimeId, out var mp))
                {
                    var copy = mp.Clone(); copy.OwnerId = sourceOwnerId; return copy;
                }
            }
            if (cause == BattleValueChangeCause.BlueDragon || cause == BattleValueChangeCause.RedDragon ||
                cause == BattleValueChangeCause.NuclearPowerPlant || cause == BattleValueChangeCause.Piercing ||
                cause == BattleValueChangeCause.LifeSteal)
            {
                var occ = _before.Values.FirstOrDefault(o => o.OwnerId == sourceOwnerId && string.Equals(o.CardId, sourceCardId, StringComparison.Ordinal));
                if (occ != null && _previewBefore.TryGetValue(occ.RuntimeId, out var op))
                {
                    var copy = op.Clone(); copy.OwnerId = sourceOwnerId; return copy;
                }
            }
            return BattleCombatLogCardSnapshotDto.CreateSpellCard(sourceCardId, sourceOwnerId, sourceRuntimeId);
        }
        private static void AddStatusLog(List<BattleCombatLogEntryDto> records, BattleCombatLogEntryType type, OccupantState o, BattleCombatLogCardSnapshotDto preview, int amount = 0) =>
            records.Add(new BattleCombatLogEntryDto { EntryType = type, TargetOwnerId = o.OwnerId, TargetCardId = o.CardId,
                TargetCoord = Coord(o.Position), Amount = amount, TargetPreview = preview.Clone() });
        private static TileCoordDto Coord(TileCoord c) => new TileCoordDto { Column = c.Column, Row = c.Row };
        private static bool Removed(Dictionary<string, OccupantState> occupants, string id) =>
            !occupants.TryGetValue(id, out var occupant) || !occupant.IsAlive;
        private static List<int> History(List<BattleValuePopupEvent> hits, int initial)
        { var history = new List<int> { initial }; foreach (var hit in hits) history.Add(hit.HpAfter); return history; }
        private static void AddResources(List<BattleCombatLogEntryDto> records, BattleResourceChangeEvent source, ResourceSet resources, BattleCombatLogEntryType type, BattleCombatLogCardSnapshotDto sourcePreview)
        {
            var amounts = new[] { resources.Mana, resources.Qi, resources.Power, resources.Gold };
            var types = new[] { BattleCombatLogResourceType.Mana, BattleCombatLogResourceType.Qi, BattleCombatLogResourceType.Power, BattleCombatLogResourceType.Gold };
            for (var i = 0; i < amounts.Length; i++) if (amounts[i] > 0)
                records.Add(new BattleCombatLogEntryDto { EntryType = type, SourceOwnerId = source.OwnerId,
                    SourceCardId = source.SourceCardId, Amount = amounts[i], ResourceType = types[i], SourcePreview = sourcePreview?.Clone() });
        }
        // Local/tutorial logs show only the local player's private cards; the AI side gets card backs.
        private void AddDraws(List<BattleCombatLogEntryDto> records, BattleState b, List<BattleCardDrawEvent> draws)
        {
            foreach (var group in draws.Where(d => !string.IsNullOrWhiteSpace(d.SourceCardId)).GroupBy(d => new { d.OwnerId, d.SourceCardId }))
            {
                var targets = group.Select(d => PrivateCardPreview(d.DrawnCardId, group.Key.OwnerId)).ToList();
                records.Add(new BattleCombatLogEntryDto { EntryType = BattleCombatLogEntryType.CardDrawn,
                    SourceOwnerId = group.Key.OwnerId, TargetOwnerId = group.Key.OwnerId, SourceCardId = group.Key.SourceCardId,
                    Amount = targets.Count, SourcePreview = CardOrOccupantPreview(b, group.Key.OwnerId, group.Key.SourceCardId, null),
                    TargetPreview = targets.FirstOrDefault()?.Clone(), TargetPreviews = targets });
            }
        }
        private void AddGenerations(List<BattleCombatLogEntryDto> records, BattleState b, List<BattleCardGenerationEvent> generations)
        {
            foreach (var g in generations.Where(g => g.AddedToHand))
            {
                var target = PrivateCardPreview(g.GeneratedCardId, g.OwnerId);
                records.Add(new BattleCombatLogEntryDto { EntryType = BattleCombatLogEntryType.CardGenerated,
                    SourceOwnerId = g.OwnerId, TargetOwnerId = g.OwnerId, SourceCardId = g.SourceCardId,
                    TargetCardId = g.OwnerId == PlayerId.Player ? g.GeneratedCardId : string.Empty,
                    SourceCoord = Coord(g.SourceCoord), Amount = 1,
                    SourcePreview = CardOrOccupantPreview(b, g.OwnerId, g.SourceCardId, g.SourceCoord),
                    TargetPreview = target, TargetPreviews = new List<BattleCombatLogCardSnapshotDto> { target.Clone() } });
            }
        }
        private BattleCombatLogCardSnapshotDto ResourceSourcePreview(BattleState b, BattleResourceChangeEvent resource) =>
            string.IsNullOrWhiteSpace(resource.SourceCardId) ? null : CardOrOccupantPreview(b, resource.OwnerId, resource.SourceCardId, null);
        private BattleCombatLogCardSnapshotDto CardOrOccupantPreview(BattleState b, PlayerId owner, string cardId, TileCoord? coord)
        {
            var occupant = coord.HasValue
                ? _before.Values.FirstOrDefault(o => o.OwnerId == owner && o.Position.Equals(coord.Value) && o.CardId == cardId)
                : null;
            occupant ??= _before.Values.FirstOrDefault(o => o.OwnerId == owner && string.Equals(o.CardId, cardId, StringComparison.Ordinal));
            if (occupant != null && _previewBefore.TryGetValue(occupant.RuntimeId, out var before))
            {
                var copy = before.Clone(); copy.OwnerId = owner; return copy;
            }
            var current = b.GetBoard(owner).EnumerateOccupants().FirstOrDefault(o => string.Equals(o.CardId, cardId, StringComparison.Ordinal));
            if (current != null)
            {
                var copy = BattleCombatLogCardSnapshotDto.Capture(current); copy.OwnerId = owner; return copy;
            }
            return HandCardPreview(cardId, owner);
        }
        private BattleCombatLogCardSnapshotDto PrivateCardPreview(string cardId, PlayerId owner) =>
            owner == PlayerId.Player && !string.IsNullOrWhiteSpace(cardId)
                ? HandCardPreview(cardId, owner)
                : BattleCombatLogCardSnapshotDto.CreateCardBack(owner);
        private BattleCombatLogCardSnapshotDto HandCardPreview(string cardId, PlayerId owner)
        {
            CardDefinition definition = null;
            try { definition = string.IsNullOrWhiteSpace(cardId) ? null : _cardDefinitions?.GetRequired(cardId); }
            catch (Exception) { definition = null; }
            if (definition is UnitCardDefinition unit)
                return BattleCombatLogCardSnapshotDto.CreateHandCard(unit.CardId, unit.Attack, unit.Health, ownerId: owner);
            if (definition is BuildingCardDefinition building)
                return BattleCombatLogCardSnapshotDto.CreateHandCard(building.CardId, building.Attack, building.Health, ownerId: owner);
            return BattleCombatLogCardSnapshotDto.CreateSpellCard(cardId ?? string.Empty, owner);
        }
    }
}
