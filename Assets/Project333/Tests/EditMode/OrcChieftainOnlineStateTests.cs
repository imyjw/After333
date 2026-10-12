using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Tests.EditMode
{
    public sealed class OrcChieftainOnlineStateTests
    {
        [TestCase(PlayerId.Player, false)]
        [TestCase(PlayerId.Player, true)]
        [TestCase(PlayerId.AI, false)]
        [TestCase(PlayerId.AI, true)]
        public void ActualJsonViewProjection_PreservesTwoSourcesForEitherSeatAndAnyWireOrder(PlayerId owner, bool reverse)
        {
            var state = State();
            var first = Put(state, owner, "chief-one", "OrcChieftain", 50, 70, 0);
            var second = Put(state, owner, "chief-two", "OrcChieftain", 50, 70, 1);
            var orc = Put(state, owner, "orc", "OrcWarrior", 25, 40, 3);
            orc.CurrentHp -= 7;
            Assert.That(orc.Attack, Is.EqualTo(45));
            Assert.That(orc.MaxHp, Is.EqualTo(60));
            Assert.That(orc.CurrentHp, Is.EqualTo(53));
            var wire = new BattleStateViewFactory().CreateForPlayer(state, "chief-wire", owner);
            if (reverse) wire.Occupants.Reverse();
            for (var iteration = 0; iteration < 3; iteration++)
            {
                var received = JsonConvert.DeserializeObject<BattleStateViewDto>(JsonConvert.SerializeObject(wire));
                var projected = BattleStateViewProjector.CreateLocalPerspectiveState(received);
                var restored = Find(projected, "orc");
                Assert.That(restored.OwnerId, Is.EqualTo(PlayerId.Player), "The viewing seat always maps to the local player.");
                Assert.That(restored.Attack, Is.EqualTo(45));
                Assert.That(restored.MaxHp, Is.EqualTo(60));
                Assert.That(restored.CurrentHp, Is.EqualTo(53), "Rebuilding a view must neither heal nor duplicate buffs.");
                Assert.That(restored.CombatStatBuffs.Select(buff => buff.SourceRuntimeId), Is.EquivalentTo(new[] { "chief-one", "chief-two" }));
                var removed = Find(projected, "chief-one");
                OccupantDestructionService.DestroyOccupant(projected, PlayerId.Player, removed);
                Assert.That(restored.Attack, Is.EqualTo(35));
                Assert.That(restored.MaxHp, Is.EqualTo(50));
                Assert.That(restored.CurrentHp, Is.EqualTo(43));
                Assert.That(restored.CombatStatBuffs.Single().SourceRuntimeId, Is.EqualTo("chief-two"));
            }
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void ErasedOldSourcesAndNewSource_SurviveJsonAndCannotRegrantAnErasedContribution(PlayerId owner)
        {
            var state = State();
            Put(state, owner, "old-one", "OrcChieftain", 50, 70, 0);
            Put(state, owner, "old-two", "OrcChieftain", 50, 70, 1);
            var orc = Put(state, owner, "erased-orc", "OrcWarrior", 25, 40, 3);
            orc.ApplyErasure();
            Put(state, owner, "new-chief", "OrcChieftain", 50, 70, 4);
            Assert.That(orc.Attack, Is.EqualTo(35));
            Assert.That(orc.MaxHp, Is.EqualTo(50));
            Assert.That(orc.CurrentHp, Is.EqualTo(50));
            var original = new BattleStateViewFactory().CreateForPlayer(state, "erased-sources", owner);
            original.Occupants.Reverse();
            var wire = JsonConvert.DeserializeObject<BattleStateViewDto>(JsonConvert.SerializeObject(original));
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(wire);
            var restored = Find(projected, "erased-orc");
            Assert.That(restored.IsErasure, Is.True);
            Assert.That(restored.ErasedOrcChieftainSourceRuntimeIds, Is.EquivalentTo(new[] { "old-one", "old-two" }));
            Assert.That(restored.Attack, Is.EqualTo(35));
            Assert.That(restored.MaxHp, Is.EqualTo(50));
            Assert.That(restored.CurrentHp, Is.EqualTo(50));
            Assert.That(restored.CombatStatBuffs.Single().SourceRuntimeId, Is.EqualTo("new-chief"));
            projected.PlayerBoard.RefreshOrcChieftainAuras();
            Assert.That(restored.Attack, Is.EqualTo(35));
            OccupantDestructionService.DestroyOccupant(projected, PlayerId.Player, Find(projected, "old-one"));
            Assert.That(restored.Attack, Is.EqualTo(35), "A contribution already erased is not subtracted again.");
            OccupantDestructionService.DestroyOccupant(projected, PlayerId.Player, Find(projected, "new-chief"));
            Assert.That(restored.Attack, Is.EqualTo(25));
            Assert.That(restored.MaxHp, Is.EqualTo(40));
            Assert.That(restored.CurrentHp, Is.EqualTo(40));
            Assert.That(((UnitState)restored).HasRace(UnitRace.Orc), Is.True);
        }

        [Test]
        public void OldViewWithoutNewFields_KeepsExistingPermanentBuffStatsAndEmptySourceMetadata()
        {
            var state = State();
            var orc = Put(state, PlayerId.Player, "legacy-orc", "OrcWarrior", 25, 40, 0);
            orc.ApplyCombatStatBuff("Hero", attackBonus: 5, maxHpBonus: 7);
            orc.CurrentHp -= 3;
            var wire = new BattleStateViewFactory().CreateForPlayer(state, "legacy-view", PlayerId.Player);
            var json = Newtonsoft.Json.Linq.JObject.Parse(JsonConvert.SerializeObject(wire));
            foreach (var occupant in json["Occupants"].Cast<Newtonsoft.Json.Linq.JObject>())
            {
                occupant.Remove("ErasedOrcChieftainSourceRuntimeIds");
                foreach (var buff in occupant["CombatStatBuffs"].Cast<Newtonsoft.Json.Linq.JObject>()) buff.Remove("SourceRuntimeId");
            }
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(json.ToObject<BattleStateViewDto>());
            var restored = Find(projected, "legacy-orc");
            Assert.That(restored.Attack, Is.EqualTo(30));
            Assert.That(restored.MaxHp, Is.EqualTo(47));
            Assert.That(restored.CurrentHp, Is.EqualTo(44));
            Assert.That(restored.CombatStatBuffs.Single().SourceRuntimeId, Is.Empty);
            Assert.That(restored.ErasedOrcChieftainSourceRuntimeIds, Is.Empty);
        }

        private static BattleState State()
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.Player));
            state.SetPhase(PhaseType.Main);
            return state;
        }
        private static UnitState Put(BattleState state, PlayerId owner, string runtimeId, string cardId, int atk, int hp, int column)
        {
            var unit = new UnitState(runtimeId, cardId, owner, new TileCoord(column, 0), AttackType.Melee,
                atk, hp, true, false, 0, races: new[] { UnitRace.Orc });
            state.GetBoard(owner).Place(unit.Position, unit);
            return unit;
        }
        private static OccupantState Find(BattleState state, string runtimeId) =>
            state.PlayerBoard.EnumerateOccupants().Concat(state.AIBoard.EnumerateOccupants()).Single(unit => unit.RuntimeId == runtimeId);
    }
}
