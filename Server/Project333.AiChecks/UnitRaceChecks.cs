using System.Reflection;
using System.Text.Json;
using Project333.PvpServer.BattleSessions;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;

internal static class UnitRaceChecks
{
    public static void Run()
    {
        var provider = PrototypeCardDefinitions.LoadCardDefinitionProvider();
        var capture = typeof(BattleSession).GetMethod("CreateOccupantSnapshot", BindingFlags.NonPublic | BindingFlags.Static)!;
        var restore = typeof(BattleSession).GetMethod("RestoreOccupant", BindingFlags.NonPublic | BindingFlags.Static)!;
        BattleSessionOccupantSnapshot Capture(OccupantState unit) =>
            (BattleSessionOccupantSnapshot)capture.Invoke(null, new object[] { unit })!;
        OccupantState Restore(BattleSessionOccupantSnapshot snapshot) =>
            (OccupantState)restore.Invoke(null, new object[] { snapshot, PlayerId.Player, provider })!;
        void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

        foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
        {
            var unit = new UnitState("race-check", "A-111", owner, new TileCoord(0, 0),
                AttackType.Melee, 13, 13, true, true, 1,
                races: new[] { UnitRace.Human, UnitRace.Robot });
            unit.ApplyErasure();
            var json = JsonSerializer.Serialize(Capture(unit));
            Require(!json.Contains("\"HasRobot\""), "New snapshot must not write the legacy Robot flag.");
            var restored = (UnitState)Restore(JsonSerializer.Deserialize<BattleSessionOccupantSnapshot>(json)!);
            Require(restored.Races.SequenceEqual(unit.Races) && restored.IsErasure && restored.OwnerId == owner,
                "Server JSON snapshot must preserve all races, Erasure and owner.");
            Require(RobotFusionRules.IsEligibleRobot(restored), "Restored erased Robot must be eligible for fusion.");

            var legacy = Capture(unit) with { Races = null, HasRobot = true };
            var legacyJson = JsonSerializer.Serialize(legacy);
            Require(!legacyJson.Contains("\"Races\""), "Absent race list must be omitted.");
            Require(((UnitState)Restore(JsonSerializer.Deserialize<BattleSessionOccupantSnapshot>(legacyJson)!))
                .HasRace(UnitRace.Robot), "Old Robot snapshot must migrate when races are absent.");
            var explicitEmpty = legacy with { Races = Array.Empty<UnitRace>() };
            Require(!((UnitState)Restore(explicitEmpty)).HasRace(UnitRace.Robot),
                "Explicit no-race value must override the legacy flag.");

            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.Player));
            state.GetBoard(owner).Place(unit.Position, unit);
            var view = new BattleStateViewFactory().CreateForPlayer(state, "race-check", owner);
            var viewJson = JsonSerializer.Serialize(view);
            var received = JsonSerializer.Deserialize<BattleStateViewDto>(viewJson)!;
            var viewed = received.Occupants.Single(o => o.RuntimeId == unit.RuntimeId);
            Require(viewed.Races.SequenceEqual(unit.Races) && viewed.IsErasure, "Server wire view must preserve races.");
            Require(!viewJson.Contains("\"HasRobot\""), "New wire view must not write the legacy flag.");
        }

        var building = new BuildingState("factory-check", "RobotFactory", PlayerId.Player,
            new TileCoord(0, 0), false, 0, 50);
        var buildingJson = JsonSerializer.Serialize(Capture(building));
        Require(!buildingJson.Contains("\"Races\"") && !buildingJson.Contains("\"HasRobot\""),
            "Buildings must not write unit race data.");
        var restoredBuilding = Restore(JsonSerializer.Deserialize<BattleSessionOccupantSnapshot>(buildingJson)!);
        Require(restoredBuilding is BuildingState, "Building restore must remain a Building.");

        Console.WriteLine("PASS Unit races: server JSON snapshots and wire views, both seats, Erasure, legacy input, empty precedence, buildings.");
    }
}
