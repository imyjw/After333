using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;

namespace Project333.Tests.EditMode
{
    public sealed class UnitRaceTests
    {
        private const string DataPath = "Assets/Project333/Resources/Project333/Data/cards.json";

        [TestCase("A-111", UnitRace.Robot)]
        [TestCase("A-212", UnitRace.Robot)]
        [TestCase("A-301", UnitRace.Robot)]
        [TestCase("Goblin", UnitRace.Goblin)]
        [TestCase("Gwangma", UnitRace.Human)]
        [TestCase("GaebangChief", UnitRace.Human)]
        [TestCase("GoldMiner", UnitRace.Human)]
        [TestCase("ManaWeaver", UnitRace.Human)]
        [TestCase("Shieldbearer", UnitRace.Human)]
        [TestCase("Shaolin_1st_Disciple", UnitRace.Human)]
        [TestCase("ElfLongbowScout", UnitRace.Elf)]
        [TestCase("ElfElder", UnitRace.Elf)]
        [TestCase("Hero", UnitRace.Human)]
        [TestCase("RedDragon", UnitRace.Dragon)]
        [TestCase("BlueDragon", UnitRace.Dragon)]
        [TestCase("Vampire", UnitRace.Vampire)]
        [TestCase("Skeleton", UnitRace.Undead)]
        [TestCase("SkeletonArcher", UnitRace.Undead)]
        [TestCase("Zombie", UnitRace.Undead)]
        [TestCase("OrcWarrior", UnitRace.Orc)]
        [TestCase("OrcBerserker", UnitRace.Orc)]
        [TestCase("OrcChieftain", UnitRace.Orc)]
        [TestCase("Werewolf", UnitRace.Beast)]
        [TestCase("Cerberus", UnitRace.Beast)]
        public void ConfirmedCard_HasExactlyTheAssignedRace(string id, UnitRace race)
        {
            var definition = (UnitCardDefinition)JsonCardDefinitionDatabase.FromJson(File.ReadAllText(DataPath))
                .CreateProvider().GetRequired(id);
            Assert.That(definition.Races, Is.EqualTo(new[] { race }));
        }

        [TestCase("Golem")]
        [TestCase("DemonKing")]
        [TestCase("ManaCannon")]
        public void ConfirmedRacelessCard_HasEmptyRaceCollection(string id)
        {
            var definition = (UnitCardDefinition)JsonCardDefinitionDatabase.FromJson(File.ReadAllText(DataPath))
                .CreateProvider().GetRequired(id);
            Assert.That(definition.Races, Is.Empty);
        }

        [Test]
        public void AuthoringData_OnlyUnitsDeclareRaces_AndAllExistingUnitAssetsMatch()
        {
            var json = File.ReadAllText(DataPath);
            var records = JsonCardDefinitionDatabase.FromJson(json).Cards.ToDictionary(c => c.Id);
            var units = 0;
            foreach (var card in JObject.Parse(json)["cards"].Cast<JObject>())
            {
                Assert.That(card.Property("hasRobot"), Is.Null);
                if ((string)card["definitionType"] == "Unit")
                {
                    units++;
                    Assert.That(card["races"], Is.TypeOf<JArray>());
                    Assert.That((string)card["specialEffectText"] ?? string.Empty, Does.Not.Contain("로봇"));
                }
                else Assert.That(card.Property("races"), Is.Null);
            }
            Assert.That(units, Is.EqualTo(27));
            var verified = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:UnitCardDefinitionAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnitCardDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (!records.TryGetValue(asset.CardId, out var record)) continue; // SampleBattle fixtures.
                var definition = (UnitCardDefinition)asset.ToDefinition();
                Assert.That(definition.Races, Is.EqualTo(record.Races), asset.CardId);
                Assert.That(asset.SpecialEffectText, Does.Not.Contain("로봇"), asset.CardId);
                verified++;
            }
            Assert.That(verified, Is.EqualTo(25)); // Hero/DemonKing are JSON-only unreleased cards.
        }

        [Test]
        public void MultipleRaces_AreCopiedAndRetainedThroughErasureAndAiSimulation()
        {
            var input = new[] { UnitRace.Human, UnitRace.Robot };
            var unit = Unit(input);
            input[0] = UnitRace.Beast;
            unit.ApplyErasure();
            Assert.That(unit.HasRace(UnitRace.Human), Is.True);
            Assert.That(unit.HasRace(UnitRace.Robot), Is.True);
            Assert.That(unit.HasRace(UnitRace.Beast), Is.False);
            var state = State();
            state.PlayerBoard.Place(unit.Position, unit);
            var copied = (UnitState)AiBattleStateCopy.Create(state).PlayerBoard.GetOccupant(unit.Position);
            Assert.That(copied.Races, Is.EqualTo(unit.Races));
            Assert.That(copied.IsErasure, Is.True);
            Assert.That(RobotFusionRules.IsEligibleRobot(copied), Is.True);
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void OnlineRoundTrip_PreservesMultipleRacesAndErasureForEitherSeat(PlayerId owner)
        {
            var state = State();
            var unit = Unit(new[] { UnitRace.Human, UnitRace.Robot }, owner);
            unit.ApplyErasure();
            state.GetBoard(owner).Place(unit.Position, unit);
            var view = new BattleStateViewFactory().CreateForPlayer(state, "race-test", owner);
            var json = JsonConvert.SerializeObject(view);
            Assert.That(json, Does.Not.Contain("\"HasRobot\""));
            var received = JsonConvert.DeserializeObject<BattleStateViewDto>(json);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(received);
            var restored = (UnitState)projected.PlayerBoard.GetOccupant(unit.Position);
            Assert.That(restored.Races, Is.EqualTo(unit.Races));
            Assert.That(restored.IsErasure, Is.True);
            Assert.That(RobotFusionRules.IsEligibleRobot(restored), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyView_UsesRobotFlagOnlyWhenRaceCollectionIsAbsent(bool explicitEmptyRaces)
        {
            var state = State();
            var unit = Unit(Array.Empty<UnitRace>());
            state.PlayerBoard.Place(unit.Position, unit);
            var view = new BattleStateViewFactory().CreateForPlayer(state, "legacy-race", PlayerId.Player);
            var record = view.Occupants.Single(o => o.RuntimeId == unit.RuntimeId);
            record.Races = explicitEmptyRaces ? Array.Empty<UnitRace>() : null;
            record.HasRobot = true;
            var restored = (UnitState)BattleStateViewProjector.CreateLocalPerspectiveState(view)
                .PlayerBoard.GetOccupant(unit.Position);
            Assert.That(restored.HasRace(UnitRace.Robot), Is.EqualTo(!explicitEmptyRaces));
        }

        [Test]
        public void Fusion_AcceptsErasedRobotAndRetainsOnlySurvivorRaces()
        {
            var state = State();
            var survivor = Unit(new[] { UnitRace.Robot });
            var donor = new UnitState("donor", "donor", PlayerId.Player, new TileCoord(1, 0),
                AttackType.Melee, 2, 4, true, true, 0, races: new[] { UnitRace.Robot, UnitRace.Human });
            survivor.ApplyErasure();
            donor.IsDrained = true;
            state.PlayerBoard.Place(survivor.Position, survivor);
            state.PlayerBoard.Place(donor.Position, donor);
            RobotFusionRules.Resolve(state, PlayerId.Player, new[] { survivor.Position, donor.Position });
            Assert.That(state.PlayerBoard.GetOccupant(donor.Position), Is.Null);
            Assert.That(survivor.Races, Is.EqualTo(new[] { UnitRace.Robot }));
            Assert.That(survivor.Attack, Is.EqualTo(5));
            Assert.That(survivor.CurrentHp, Is.EqualTo(10));
        }

        [TestCase(JsonCardDefinitionKind.Building)]
        [TestCase(JsonCardDefinitionKind.DamageSpell)]
        public void NonUnits_RejectEvenAnEmptyRaceField(JsonCardDefinitionKind kind)
        {
            var record = new JsonCardDefinitionRecord
            {
                Id = "invalid-race", DisplayName = "Invalid", DefinitionType = kind,
                Races = Array.Empty<UnitRace>()
            };
            Assert.Throws<InvalidOperationException>(() => record.ToDefinition());
            var validation = CardDatabaseValidator.Validate(new JsonCardDefinitionDatabase
                { Cards = new System.Collections.Generic.List<JsonCardDefinitionRecord> { record } });
            Assert.That(validation.Errors, Has.Some.Contains("must not define races"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyRobotCardField_IsRejected(bool legacyFlag)
        {
            var data = JObject.Parse(File.ReadAllText(DataPath));
            data["cards"].First["hasRobot"] = legacyFlag;
            Assert.That(CardDatabaseValidator.ValidateJson(data.ToString()).Errors,
                Has.Some.Contains("removed hasRobot field"));
        }

        [Test]
        public void InvalidAndDuplicateRaceValues_AreReportedByDataValidator()
        {
            var record = new JsonCardDefinitionRecord
            {
                Id = "invalid-race", DisplayName = "Invalid", DefinitionType = JsonCardDefinitionKind.Unit,
                Races = new[] { UnitRace.Robot, UnitRace.Robot, (UnitRace)999 }
            };
            var validation = CardDatabaseValidator.Validate(new JsonCardDefinitionDatabase
                { Cards = new System.Collections.Generic.List<JsonCardDefinitionRecord> { record } });
            Assert.That(validation.Errors, Has.Some.Contains("repeats race"));
            Assert.That(validation.Errors, Has.Some.Contains("unknown race"));
            Assert.Throws<ArgumentOutOfRangeException>(() => record.ToDefinition());
        }

        private static UnitState Unit(UnitRace[] races, PlayerId owner = PlayerId.Player) =>
            new UnitState("race-unit", "race-unit", owner, new TileCoord(0, 0),
                AttackType.Melee, 3, 6, true, true, 1, races: races);

        private static BattleState State()
        {
            var deck = Enumerable.Repeat("Goblin", 20).ToArray();
            return new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.Player));
        }
    }
}
