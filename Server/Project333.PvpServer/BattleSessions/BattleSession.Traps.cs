using Project333.Runtime.Application.Online;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Effects;
using Project333.PvpServer.Messages;

namespace Project333.PvpServer.BattleSessions;

public sealed partial class BattleSession
{
    private void CompleteTrapAwareAttackPresentation(BattleEventDto attackEvent, List<BattleEventDto> events)
    {
        if (_battleFlowController?.CurrentBattleState?.LastAttackResolution?.WasCancelledByTrap == true)
            events.Remove(attackEvent);
        else
            ApplyResolvedAttackTarget(attackEvent, events);

        // Publish the successful trigger before damage and missing-occupant feedback.
        AppendTrapEvents(events);
    }

    private void CompleteTrapAwareSummonPresentation(List<BattleEventDto> events,
        ServerBattleSnapshot beforeSnapshot, PlayerIdDto actorId)
    {
        var state = _battleFlowController?.CurrentBattleState;
        if (state == null) return;
        // A newly summoned unit was not in the pre-command board; include its pre-hit
        // presentation snapshot so lethal trap damage has the ordinary removal cue.
        if (state.LastSummonedOccupant != null &&
            beforeSnapshot.FindByRuntimeId(state.LastSummonedOccupant.RuntimeId) == null)
            beforeSnapshot.Occupants.Add(CreatePresentationOccupantSnapshot(state.LastSummonedOccupant));
        AppendTrapEvents(events);
        AppendValuePopupEvents(events, actorId);
        AppendRemovedOccupantEvents(events, beforeSnapshot, CaptureBattleSnapshot(state));
        AppendBattleEndedEventIfNeeded(events);
    }

    private static bool WasSummonTrapTriggered(IReadOnlyList<BattleEventDto> events, string runtimeId) =>
        !string.IsNullOrWhiteSpace(runtimeId) && events.Any(candidate =>
            candidate?.EventType == BattleEventType.TrapTriggered &&
            string.Equals(candidate.SourceCardId, Project333.Runtime.Infrastructure.Data.SummonTrapCardRules.CardId, StringComparison.Ordinal) &&
            string.Equals(candidate.TargetRuntimeId, runtimeId, StringComparison.Ordinal));
    private void AppendTrapEvents(List<BattleEventDto> events)
    {
        var state = _battleFlowController?.CurrentBattleState;
        if (events == null || state == null) return;
        foreach (var trapEvent in state.TrapEvents)
        {
            if (trapEvent == null) continue;
            var dto = new BattleEventDto
            {
                EventType = trapEvent.Kind == BattleTrapEventKind.Installed
                    ? BattleEventType.TrapInstalled : BattleEventType.TrapTriggered,
                SourceOwnerId = ToDtoPlayerId(trapEvent.OwnerId),
                TrapCount = trapEvent.ArmedTrapCount
            };
            // Installation is generic globally, including the installer's ACK and combat log.
            if (trapEvent.Kind == BattleTrapEventKind.Triggered)
            {
                dto.CardId = dto.SourceCardId = trapEvent.CardId;
                dto.RuntimeId = dto.SourceRuntimeId = trapEvent.RuntimeId;
                dto.TargetOwnerId = ToDtoPlayerId(trapEvent.TargetOwnerId);
                dto.TargetRuntimeId = trapEvent.TargetRuntimeId;
                dto.TargetCardId = trapEvent.TargetCardId;
                if (string.Equals(trapEvent.CardId, Project333.Runtime.Infrastructure.Data.SummonTrapCardRules.CardId, StringComparison.Ordinal))
                    dto.CardSpellDamage = trapEvent.EffectAmount;
                dto.TargetCoord = trapEvent.TargetCoord.HasValue
                    ? ToServerTileCoord(trapEvent.TargetCoord.Value) : null;
            }
            events.Add(dto);
        }
        state.ClearTrapEvents();
    }

    private static bool WasReturnedByTrap(IReadOnlyList<BattleEventDto> events, string runtimeId)
    {
        return !string.IsNullOrWhiteSpace(runtimeId) && events.Any(candidate =>
            candidate?.EventType == BattleEventType.TrapTriggered &&
            string.Equals(candidate.SourceCardId, Project333.Runtime.Infrastructure.Data.TrapReturnCardRules.CardId, StringComparison.Ordinal) &&
            string.Equals(candidate.TargetRuntimeId, runtimeId, StringComparison.Ordinal));
    }

    private static bool WasReflectedByTrap(IReadOnlyList<BattleEventDto> events, string runtimeId)
    {
        return !string.IsNullOrWhiteSpace(runtimeId) && events.Any(candidate =>
            candidate?.EventType == BattleEventType.TrapTriggered &&
            string.Equals(candidate.SourceCardId, Project333.Runtime.Infrastructure.Data.TrapMirrorFieldCardRules.CardId, StringComparison.Ordinal) &&
            string.Equals(candidate.TargetRuntimeId, runtimeId, StringComparison.Ordinal));
    }

    private static bool WasDestructionTrapTriggered(IReadOnlyList<BattleEventDto> events)
    {
        return events.Any(candidate => candidate?.EventType == BattleEventType.TrapTriggered &&
            (string.Equals(candidate.SourceCardId,
                Project333.Runtime.Infrastructure.Data.ExplosiveReactiveArmourCardRules.CardId,
                StringComparison.Ordinal) ||
             string.Equals(candidate.SourceCardId,
                Project333.Runtime.Infrastructure.Data.IronDomeCardRules.CardId,
                StringComparison.Ordinal)));
    }

    private void AppendTrapCombatLogs(IReadOnlyList<BattleEventDto> events)
    {
        foreach (var trapEvent in events.Where(candidate =>
                     candidate?.EventType == BattleEventType.TrapInstalled ||
                     candidate?.EventType == BattleEventType.TrapTriggered))
        {
            bool triggered = trapEvent.EventType == BattleEventType.TrapTriggered;
            var entry = new BattleCombatLogEntryDto
            {
                EntryType = triggered ? BattleCombatLogEntryType.TrapTriggered : BattleCombatLogEntryType.TrapInstalled,
                SourceOwnerId = ToRuntimePlayerId(trapEvent.SourceOwnerId),
                SourceCardId = triggered ? trapEvent.SourceCardId : string.Empty,
                Amount = trapEvent.TrapCount
            };
            if (triggered)
            {
                entry.TargetOwnerId = ToRuntimePlayerId(trapEvent.TargetOwnerId);
                entry.TargetCardId = trapEvent.TargetCardId;
                if (trapEvent.TargetCoord != null) entry.TargetCoord = CloneTileCoord(trapEvent.TargetCoord)!;
            }
            AppendCombatLogRecord(entry);
        }
    }

    private static IReadOnlyList<BattleSessionArmedTrapSnapshot> CreateArmedTrapSnapshots(BattleState state)
    {
        return state.ArmedTraps.Where(trap => trap != null).Select(trap =>
            new BattleSessionArmedTrapSnapshot(
                trap.RuntimeId, trap.OwnerId.ToString(), trap.CardId,
                trap.TriggerType.ToString(), trap.EffectType.ToString(),
                CreateResourceSnapshot(trap.ActivationCost), trap.InstalledTurn,
                trap.UpgradeLevel, trap.IsTemporaryReplicate, trap.EffectAmount)).ToList();
    }

    private static void RestoreArmedTraps(BattleState state, IReadOnlyList<BattleSessionArmedTrapSnapshot>? traps)
    {
        state.ArmedTraps.Clear();
        state.Player.RestoreHiddenTrapCount(0);
        state.AI.RestoreHiddenTrapCount(0);
        foreach (var trap in traps ?? Array.Empty<BattleSessionArmedTrapSnapshot>())
        {
            state.ArmedTraps.Add(new ArmedTrapState(
                trap.RuntimeId, ParseRuntimePlayerId(trap.OwnerId, PlayerId.Player), trap.CardId,
                Enum.Parse<TrapTriggerType>(trap.TriggerType),
                Enum.Parse<TrapEffectType>(trap.EffectType),
                RestoreResourceSet(trap.ActivationCost), trap.InstalledTurn,
                trap.UpgradeLevel, trap.IsTemporaryReplicate, trap.EffectAmount));
        }
        // Reconnect/diagnostic restoration never replays installation or a prior trigger.
        state.ClearTrapEvents();
    }
}

public sealed record BattleSessionArmedTrapSnapshot(
    string RuntimeId, string OwnerId, string CardId,
    string TriggerType, string EffectType, BattleSessionResourceSnapshot ActivationCost,
    int InstalledTurn, int UpgradeLevel, bool IsTemporaryReplicate, int EffectAmount = 0);
