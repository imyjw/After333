using System.Net.WebSockets;
using System.Reflection;
using System.Text.Json.Nodes;
using Project333.PvpServer.BattleSessions;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;

internal static class PveAiDeckChecks
{
    public static void Run()
    {
        var database = PrototypeCardDefinitions.LoadCardDefinitionDatabase();
        var catalog = PveAiDeckCatalog.Default;
        var path = Path.Combine(AppContext.BaseDirectory, "Data", PveAiDeckCatalog.FileName);
        var json = File.ReadAllText(path);
        Check(catalog.Decks.Count == 10, "Ten decks required.");
        var expected = new Dictionary<string, int>
        {
            ["Legendary"] = 1, ["Unique"] = 5, ["Rare"] = 9, ["Uncommon"] = 11, ["Common"] = 7
        };
        var definitions = database.Cards.ToDictionary(card => card.Id);
        var provider = PrototypeCardDefinitions.LoadCardDefinitionProvider();
        for (var index = 0; index < catalog.Decks.Count; index++)
        {
            var selected = catalog.SelectDeck(count =>
            {
                Check(count == 10, "Selector must receive all ten decks.");
                return index;
            });
            Check(ReferenceEquals(selected, catalog.Decks[index]), "Wrong selected deck.");
            Check(selected.CardIds.Count == 33, "Wrong total.");
            foreach (var group in selected.CardIds.GroupBy(id => id))
            {
                Check(definitions[group.Key].IncludeInDraft, "Unreleased card included.");
                Check(group.Count() <= 3, "Copy cap exceeded.");
            }
            foreach (var pair in expected)
                Check(selected.CardIds.Count(id => definitions[id].Rarity.ToString() == pair.Key) == pair.Value,
                    $"Wrong rarity quota: {selected.Id}/{pair.Key}.");

            // Run each real fixed deck through setup and a legal AI command, not just JSON validation.
            var flow = PrototypeCardDefinitions.CreateBattleFlowController(provider);
            flow.StartBattle(new BattleSetupRequest(selected.CardIds, selected.CardIds, PlayerId.AI));
            var state = flow.CurrentBattleState!;
            Check(Signature(state.AI) == Signature(selected.CardIds), "Setup lost cards.");
            state.SetPhase(PhaseType.Main);
            state.AI.Resources.Add(new Project333.Runtime.Domain.Resources.ResourceSet(6, 6, 6, 6));
            var planner = new AiDecisionService(provider);
            var before = AiBattleStateCopy.PositionKey(state);
            var command = planner.GetNextCommand(state);
            Check(AiBattleStateCopy.PositionKey(state) == before, "Planning mutated state.");
            flow.ExecuteCommand(PlayerId.AI, command);
        }
        for (var i = 0; i < 100; i++)
            Check(catalog.Decks.Contains(catalog.SelectDeck()), "Random selection escaped catalog.");
        ExpectInvalid(() => catalog.SelectDeck(_ => -1));
        ExpectInvalid(() => catalog.SelectDeck(count => count));

        Reject(root => root["schemaVersion"] = 2);
        Reject(root => root["decks"]!.AsArray().RemoveAt(9));
        Reject(root => root["decks"]![1]!["id"] = root["decks"]![0]!["id"]!.GetValue<string>());
        Reject(root => root["decks"]![1]!["cards"] = root["decks"]![0]!["cards"]!.DeepClone());
        Reject(root => root["decks"]![0]!["cards"]![0]!["cardId"] = "missing-card");
        Reject(root => root["decks"]![0]!["cards"]![0]!["cardId"] = "DemonKing");
        Reject(root => root["decks"]![0]!["cards"]![0]!["count"] = 2);
        Reject(root => root["decks"]![0]!["cards"]![1]!["count"] = 4);
        Reject(root => root["decks"]![0]!["cards"]![1]!["count"] = 0);
        Reject(root => root["decks"]![0]!["cards"]![1]!["count"] =
            root["decks"]![0]!["cards"]![1]!["count"]!.GetValue<int>() == 1 ? 2 : 1);
        Reject(root =>
        {
            var entries = root["decks"]![0]!["cards"]!.AsArray();
            var common = entries.First(entry =>
                definitions[entry!["cardId"]!.GetValue<string>()].Rarity.ToString() == "Common")!;
            var unique = entries.First(entry =>
                definitions[entry!["cardId"]!.GetValue<string>()].Rarity.ToString() == "Unique" &&
                entry["count"]!.GetValue<int>() < 3)!;
            unique["count"] = unique["count"]!.GetValue<int>() + 1;
            var commonCount = common["count"]!.GetValue<int>() - 1;
            if (commonCount == 0) entries.Remove(common);
            else common["count"] = commonCount;
        });
        Reject(root => root["decks"]![0]!["cards"]!.AsArray().Add(
            root["decks"]![0]!["cards"]![1]!.DeepClone()));

        using var socketA = new ClientWebSocket();
        using var socketB = new ClientWebSocket();
        // These sockets are never connected: session setup needs only connection metadata.
        var player = new BattleClientConnection("deck-check-player", socketA) { PlayerToken = "player" };
        player.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin", 33));
        var pve = new BattleSession("deck-check-pve", true);
        Check(pve.TryAddConnection(player, out _), "PvE join failed.");
        Check(pve.TryStartBattleIfReady(out var message), "PvE start failed.");
        var current = Flow(pve).CurrentBattleState!;
        var chosen = catalog.Decks.Single(deck => message.Contains($"AI deck: {deck.Id}."));
        Check(Signature(current.AI) == Signature(chosen.CardIds), "Server did not use selected AI deck.");
        Check(Signature(current.Player) == Signature(player.PlayerDeckCardIds), "Player deck changed.");
        Check(!pve.TryStartBattleIfReady(out _), "Running battle restarted.");
        Check(ReferenceEquals(current, Flow(pve).CurrentBattleState), "Running state replaced.");
        Check(Signature(current.AI) == Signature(chosen.CardIds), "Running AI deck rerolled.");

        var pvp = new BattleSession("deck-check-pvp", false);
        var opponent = new BattleClientConnection("deck-check-opponent", socketB) { PlayerToken = "opponent" };
        opponent.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Skeleton", 33));
        var pvpPlayer = new BattleClientConnection("deck-check-pvp-player", socketA) { PlayerToken = "pvp-player" };
        pvpPlayer.PlayerDeckCardIds.AddRange(player.PlayerDeckCardIds);
        Check(pvp.TryAddConnection(pvpPlayer, out _), "PvP player join failed.");
        Check(!pvp.TryStartBattleIfReady(out _), "PvP started before opponent joined.");
        Check(pvp.TryAddConnection(opponent, out _), "PvP opponent join failed.");
        Check(pvp.TryStartBattleIfReady(out message), "PvP start failed.");
        Check(!message.Contains("AI deck:"), "AI deck selected for PvP.");
        Check(Signature(Flow(pvp).CurrentBattleState!.AI) == Signature(opponent.PlayerDeckCardIds),
            "PvP opponent deck replaced.");
        Console.WriteLine("PASS: 10 fixed decks, quotas/copy caps, all selector indexes, 14 invalid inputs, 10 legal AI decisions, PvE selection/no restart, PvP deck preservation.");

        void Reject(Action<JsonNode> mutate)
        {
            var root = JsonNode.Parse(json)!;
            mutate(root);
            ExpectInvalid(() => PveAiDeckCatalog.FromJson(root.ToJsonString(), database));
        }
    }

    private static BattleFlowController Flow(BattleSession session) =>
        (BattleFlowController)typeof(BattleSession).GetField("_battleFlowController",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
    private static string Signature(PlayerState player) => Signature(player.Hand.CardIds.Concat(player.Deck.CardIds));
    private static string Signature(IEnumerable<string> ids) => string.Join("|", ids.OrderBy(id => id, StringComparer.Ordinal));
    private static void Check(bool passed, string message)
    {
        if (!passed) throw new Exception(message);
    }
    private static void ExpectInvalid(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new Exception("Invalid deck input was accepted.");
    }
}
