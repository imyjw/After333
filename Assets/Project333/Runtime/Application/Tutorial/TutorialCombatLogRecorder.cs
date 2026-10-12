using System.Collections.Generic;
using System.Linq;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Application.Tutorial
{
    // Read-only projection of the authored local lesson into the same records as online combat.
    // It never pays resources, resolves damage or clears the shared presentation events.
    public sealed class TutorialCombatLogRecorder
    {
        private Dictionary<string, OccupantState> _before;
        private PhaseType _phase;
        private PlayerId _owner;
        private int _turn;
        private bool _ended;
        private BattleAttackResolution _attack;
        private readonly HashSet<BattleValuePopupEvent> _values = new HashSet<BattleValuePopupEvent>();
        private readonly HashSet<BattleResourceChangeEvent> _resources = new HashSet<BattleResourceChangeEvent>();
        public TutorialCombatLogRecorder(BattleState battle) { Rebase(battle); }
        private static Dictionary<string, OccupantState> Occupants(BattleState b) =>
            b.PlayerBoard.EnumerateOccupants().Concat(b.AIBoard.EnumerateOccupants())
                .ToDictionary(o => o.RuntimeId, o => o.CloneDetached());
        public void Rebase(BattleState b)
        {
            _before = Occupants(b); _phase = b.Phase; _owner = b.ActivePlayerId; _turn = b.TurnNumber;
            _ended = b.IsEnded; _attack = b.LastAttackResolution;
            _values.Clear(); _values.UnionWith(b.ValuePopupEvents);
            _resources.Clear(); _resources.UnionWith(b.ResourceChangeEvents);
        }
        public IReadOnlyList<BattleCombatLogEntryDto> Capture(BattleState b)
        {
            var result = new List<BattleCombatLogEntryDto>();
            var after = Occupants(b);
            var values = b.ValuePopupEvents.Where(v => _values.Add(v)).ToList();
            var resources = b.ResourceChangeEvents.Where(v => _resources.Add(v)).ToList();
            if (_phase == PhaseType.Main && b.TurnNumber != _turn)
                result.Add(new BattleCombatLogEntryDto { EntryType = BattleCombatLogEntryType.TurnEnded, SourceOwnerId = _owner });
            var turnStarted = b.Phase == PhaseType.Main && (_phase != PhaseType.Main || b.TurnNumber != _turn);
            if (turnStarted)
                result.Add(new BattleCombatLogEntryDto { EntryType = BattleCombatLogEntryType.TurnStarted, SourceOwnerId = b.ActivePlayerId, TurnNumber = b.TurnNumber });
            if (turnStarted) foreach (var resource in resources)
            {
                AddResources(result, resource, resource.Spent, BattleCombatLogEntryType.ResourceSpent);
                AddResources(result, resource, resource.Gained, BattleCombatLogEntryType.ResourceGained);
            }
            var loggedMoves = new HashSet<string>();
            foreach (var o in after.Values)
            {
                if (!_before.TryGetValue(o.RuntimeId, out var previous))
                    result.Add(new BattleCombatLogEntryDto { EntryType = o.Kind == OccupantKind.Building ? BattleCombatLogEntryType.BuildingConstructed : BattleCombatLogEntryType.UnitSummoned,
                        SourceOwnerId = o.OwnerId, SourceCardId = o.CardId, TargetCoord = Coord(o.Position) });
                else if (!previous.Position.Equals(o.Position) && loggedMoves.Add(o.RuntimeId))
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
            }
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
                    SourceHpHistory = counters.Count == 0 ? new List<int>() : History(counters, attacker.CurrentHp),
                    TargetHpHistory = History(hits, target.CurrentHp), HasCounterattack = counters.Count > 0,
                    SourceRemoved = Removed(after, attacker.RuntimeId), TargetRemoved = Removed(after, target.RuntimeId),
                    SourceDamagePrevented = counters.Any(v => v.IsInvinciblePrevented), TargetDamagePrevented = hits.Any(v => v.IsInvinciblePrevented) });
            }
            // The lesson's targeted damage spell uses the normal SpellCast + damage records.
            foreach (var spell in values.Where(v => v.Cause == BattleValueChangeCause.Spell && !string.IsNullOrEmpty(v.SourceCardId))
                         .GroupBy(v => new { v.SourceCardId, v.SourceOwnerId }))
                result.Add(new BattleCombatLogEntryDto { EntryType = BattleCombatLogEntryType.SpellCast,
                    SourceOwnerId = spell.Key.SourceOwnerId, SourceCardId = spell.Key.SourceCardId });
            foreach (var value in values)
            {
                if (value.Cause == BattleValueChangeCause.NormalAttack || value.Cause == BattleValueChangeCause.Counterattack || value.Amount <= 0) continue;
                var affected = _before.TryGetValue(value.RuntimeId, out var old) ? old :
                    after.TryGetValue(value.RuntimeId, out var current) ? current : null;
                result.Add(new BattleCombatLogEntryDto { EntryType = value.IsHealing ? BattleCombatLogEntryType.Healing : BattleCombatLogEntryType.Damage,
                    SourceOwnerId = value.SourceOwnerId, TargetOwnerId = value.OwnerId,
                    SourceCardId = value.SourceCardId, TargetCardId = affected?.CardId ?? "Master",
                    TargetCoord = Coord(value.Coord), Amount = value.Amount, DamageType = value.DamageType, ValueCause = value.Cause,
                    TargetHpHistory = new List<int> { value.HpBefore, value.HpAfter },
                    TargetRemoved = Removed(after, value.RuntimeId), TargetDamagePrevented = value.IsInvinciblePrevented });
            }
            if (!turnStarted) foreach (var resource in resources)
            {
                AddResources(result, resource, resource.Spent, BattleCombatLogEntryType.ResourceSpent);
                AddResources(result, resource, resource.Gained, BattleCombatLogEntryType.ResourceGained);
            }
            if (!_ended && b.IsEnded)
                result.Add(new BattleCombatLogEntryDto { EntryType = BattleCombatLogEntryType.BattleEnded,
                    TargetOwnerId = b.Result.Winner, IsDraw = b.Result.IsDraw });
            _before = after; _phase = b.Phase; _owner = b.ActivePlayerId; _turn = b.TurnNumber; _ended = b.IsEnded; _attack = attack;
            return result;
        }
        private static TileCoordDto Coord(TileCoord c) => new TileCoordDto { Column = c.Column, Row = c.Row };
        private static bool Removed(Dictionary<string, OccupantState> occupants, string id) =>
            !occupants.TryGetValue(id, out var occupant) || !occupant.IsAlive;
        private static List<int> History(List<BattleValuePopupEvent> hits, int initial)
        { var history = new List<int> { initial }; foreach (var hit in hits) history.Add(hit.HpAfter); return history; }
        private static void AddResources(List<BattleCombatLogEntryDto> records, BattleResourceChangeEvent source, ResourceSet resources, BattleCombatLogEntryType type)
        {
            var amounts = new[] { resources.Mana, resources.Qi, resources.Power, resources.Gold };
            var types = new[] { BattleCombatLogResourceType.Mana, BattleCombatLogResourceType.Qi, BattleCombatLogResourceType.Power, BattleCombatLogResourceType.Gold };
            for (var i = 0; i < amounts.Length; i++) if (amounts[i] > 0)
                records.Add(new BattleCombatLogEntryDto { EntryType = type, SourceOwnerId = source.OwnerId,
                    SourceCardId = source.SourceCardId, Amount = amounts[i], ResourceType = types[i] });
        }
    }
}
