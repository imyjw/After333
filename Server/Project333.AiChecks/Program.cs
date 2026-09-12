using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Project333.PvpServer.BattleSessions;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using ServerEvent = Project333.PvpServer.Messages.BattleEventDto;
using ServerEventType = Project333.PvpServer.Messages.BattleEventType;
using ServerPlayerId = Project333.PvpServer.Messages.PlayerIdDto;

// Offline checks only: no HTTP listener, database, accounts, rewards or production matches.
PveAiDeckChecks.Run();
var provider = PrototypeCardDefinitions.LoadCardDefinitionProvider();
var ids = new[] { "Goblin", "GoldMiner", "ManaWeaver", "firebolt", "ElfLongbowScout", "ManaStone", "A-111",
    "Daehwandan", "RobotFactory", "RobotFusion", "Gu", "HuanShu", "Firewall", "BiochemicalBomb", "TimedBomb", "Hero", "DemonKing" };
foreach (var id in ids) provider.GetRequired(id);
var timings = new ConcurrentBag<double>();
var actions = new ConcurrentDictionary<string, int>();
var wall = Stopwatch.StartNew();
var completed = 0;
var budgets = 0;
Parallel.For(0, 12, new ParallelOptions { MaxDegreeOfParallelism = 4 }, game =>
{
    var random = new Random(100 + game);
    var deck = Enumerable.Range(0, 33).Select(_ => ids[random.Next(ids.Length)]).ToArray();
    var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.AI));
    state.SetPhase(PhaseType.Main);
    state.AI.Resources.Add(new ResourceSet(6, 6, 6, 6));
    for (var col = 0; col < 5; col++)
    {
        var coord = new TileCoord(col, 0);
        state.PlayerBoard.Place(coord, new UnitState($"target-{col}", "Goblin", PlayerId.Player, coord,
            AttackType.Melee, 5 + col * 3, 25, true, false, 0, hasShielder: col == 2));
    }
    var flow = PrototypeCardDefinitions.CreateBattleFlowController(provider);
    flow.RestoreBattleState(state);
    var planner = new AiDecisionService(provider);
    var count = 0;
    while (!state.IsEnded && state.TurnNumber < 17)
    {
        if (++count > 1100) throw new Exception("AI turn did not terminate.");
        if (state.Phase == PhaseType.TurnStart) { flow.ResolveTurnStart(); continue; }
        if (state.ActivePlayerId != PlayerId.AI) { flow.EndTurn(); continue; }
        var before = AiBattleStateCopy.PositionKey(state);
        var command = planner.GetNextCommand(state);
        if (AiBattleStateCopy.PositionKey(state) != before) throw new Exception("Planning mutated the live battle.");
        if (planner.LastSimulationCount > 1200) throw new Exception("Simulation cap exceeded.");
        timings.Add(planner.LastElapsedMilliseconds);
        if (planner.LastBudgetExhausted) Interlocked.Increment(ref budgets);
        flow.ExecuteCommand(PlayerId.AI, command);
        var label = command is IHandCardCommand hand ? $"{command.GetType().Name}:{hand.CardId}" : command.GetType().Name;
        actions.AddOrUpdate(label, 1, (_, n) => n + 1);
    }
    Interlocked.Increment(ref completed);
});

// Exercise the actual server wrapper's forced-end path without opening a socket or creating a match in the DB.
var sampleDeck = Enumerable.Repeat("Goblin", 33).ToArray();
var sample = new BattleSetupService().CreateInitialState(new BattleSetupRequest(sampleDeck, sampleDeck, PlayerId.AI));
sample.SetPhase(PhaseType.Main);
var sampleFlow = PrototypeCardDefinitions.CreateBattleFlowController(provider);
sampleFlow.RestoreBattleState(sample);
var session = new BattleSession("ai-offline-check", true);
Set("_battleFlowController", sampleFlow);
Set("_cardDefinitionProvider", provider);
Set("_aiDecisionService", new AiDecisionService(provider));
var events = session.RunServerAiActionIfNeeded(forceEndTurn: true);
if (events.Count == 0 || (!sample.IsEnded && sample.ActivePlayerId == PlayerId.AI))
    throw new Exception("Server action cap did not end the AI turn.");
if (new BattleSession("pvp-no-ai", false).RunServerAiActionIfNeeded().Count != 0)
    throw new Exception("AI acted in a PvP session.");

var estimatePresentation = typeof(BattleSession).Assembly.GetType("Program")!
    .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
    .Single(method => method.Name.Contains("g__EstimateServerAiPresentationDelay|"));
foreach (var (type, expected) in new[] { (ServerEventType.AttackStarted, 2.25),
    (ServerEventType.OccupantMoved, 1.35), (ServerEventType.CardPlayed, 3.0),
    (ServerEventType.SpellCast, 3.0), (ServerEventType.RobotFusionResolved, 1.1),
    (ServerEventType.TurnStarted, 0.1), (ServerEventType.TurnEnded, 0.1) })
{
    var actionEvents = new[] { new ServerEvent { EventType = type, SourceOwnerId = ServerPlayerId.AI } };
    var estimated = (TimeSpan)estimatePresentation.Invoke(null, new object[] { actionEvents })!;
    if (Math.Abs(estimated.TotalSeconds - expected) > 0.00001)
        throw new Exception($"Incorrect server AI presentation timing for {type}: {estimated}.");
}
Console.WriteLine("PASS: server AI presentation compensation includes action pauses and card pacing.");

var ordered = timings.OrderBy(x => x).ToArray();
Console.WriteLine($"PASS: {completed} synthetic battles, 4 concurrent workers, {ordered.Length} AI decisions, zero illegal commands/state mutations.");
Console.WriteLine($"Decision ms: median={ordered[ordered.Length / 2]:F2}, p95={ordered[(int)((ordered.Length - 1) * .95)]:F2}, max={ordered[^1]:F2}; budget-limited={budgets}; wall={wall.Elapsed.TotalSeconds:F2}s.");
Console.WriteLine("PASS: server forced-end path and PvP exclusion.");
foreach (var item in actions.OrderBy(x => x.Key)) Console.WriteLine($"  {item.Key}: {item.Value}");

void Set(string name, object value) => typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, value);
