using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class EarthquakeTests
    {
        private static string Json() => File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json");
        private static ICardDefinitionProvider Provider() => JsonCardDefinitionDatabase.FromJson(Json()).CreateProvider();
        private static BattleState State(PlayerId owner = PlayerId.Player)
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner));
            state.SetPhase(PhaseType.Main);
            return state;
        }
        private static UnitState Unit(BattleState state, PlayerId owner, int column, int hp = 500,
            int defense = 0, bool endure = false, bool hiding = false, bool flying = false,
            bool shielder = false, string cardId = "Goblin", int row = 0, int spellPower = 0)
        {
            var unit = new UnitState(Guid.NewGuid().ToString(), cardId, owner, new TileCoord(column, row),
                AttackType.Melee, 1, hp, true, false, 0, hasEndure: endure,
                physicalDefense: defense, magicDefense: 99, hasHiding: hiding,
                hasFlying: flying, hasShielder: shielder, spellPower: spellPower);
            state.GetBoard(owner).Place(unit.Position, unit);
            return unit;
        }
        private static BuildingState Building(BattleState state, PlayerId owner, int column,
            int hp = 500, int defense = 0, string cardId = "ManaPond")
        {
            var building = new BuildingState(Guid.NewGuid().ToString(), cardId, owner,
                new TileCoord(column, 0), false, 0, hp, physicalDefense: defense, magicDefense: 99);
            state.GetBoard(owner).Place(building.Position, building);
            return building;
        }
        private static void Cast(BattleState state, PlayerId owner, int level = 0, string runtimeId = null)
        {
            var upgrades = new InMemoryCardUpgradeLevelProvider();
            upgrades.SetUpgradeLevel(owner, EarthquakeRules.CardId, level);
            upgrades.SetUpgradeLevel(state.GetOpponent(owner).Id, EarthquakeRules.CardId, level == 13 ? 0 : 13);
            if (runtimeId == null) runtimeId = state.GetPlayer(owner).Hand.Add(EarthquakeRules.CardId).RuntimeId;
            state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 0, 3));
            new SpellService(Provider(), upgrades).CastScriptedSpell(state, owner, EarthquakeRules.CardId, runtimeId);
        }
        private static IEnumerable<TestCaseData> AllLevels()
        {
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
                for (var level = 0; level <= 13; level++) yield return new TestCaseData(owner, level);
        }

        [Test]
        public void Definition_MatchesConfirmedCardAndReleasePools()
        {
            Assert.That(CardDatabaseValidator.ValidateJson(Json()).Errors, Is.Empty);
            var record = JsonCardDefinitionDatabase.FromJson(Json()).Cards.Single(c => c.Id == EarthquakeRules.CardId);
            Assert.That(record.DisplayName, Is.EqualTo("지진"));
            Assert.That(record.DefinitionType, Is.EqualTo(JsonCardDefinitionKind.ScriptedSpell));
            Assert.That(record.Rarity, Is.EqualTo(CardRarity.Uncommon));
            Assert.That(record.Affiliation, Is.EqualTo(CardAffiliation.Neutral));
            Assert.That(record.Cost.Gold, Is.EqualTo(3));
            Assert.That(record.Cost.Mana + record.Cost.Qi + record.Cost.Power, Is.Zero);
            Assert.That(record.Damage, Is.EqualTo(10));
            Assert.That(record.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(record.EffectText, Is.EqualTo(EarthquakeRules.EffectDescription));
            Assert.That(record.SpecialEffectText, Is.Empty);
            Assert.That(record.IncludeInDraft && record.IncludeInRewards, Is.True);
            Assert.That(CardUpgradeRules.IsCardUpgradeable(record.ToDefinition()), Is.True);
        }

        [TestCase("cost")][TestCase("damage")][TestCase("type")][TestCase("rarity")]
        [TestCase("affiliation")][TestCase("draft")][TestCase("rewards")][TestCase("special")]
        [TestCase("effect")][TestCase("footprint")]
        public void ValidatorRejectsIncorrectEarthquakeAuthoring(string field)
        {
            var database = JsonCardDefinitionDatabase.FromJson(Json());
            var record = database.Cards.Single(c => c.Id == EarthquakeRules.CardId);
            switch (field)
            {
                case "cost": record.Cost.Gold = 4; break;
                case "damage": record.Damage = 9; break;
                case "type": record.DamageType = DamageType.Magic; break;
                case "rarity": record.Rarity = CardRarity.Rare; break;
                case "affiliation": record.Affiliation = CardAffiliation.Fantasy; break;
                case "draft": record.IncludeInDraft = false; break;
                case "rewards": record.IncludeInRewards = false; break;
                case "special": record.SpecialEffectText = "비행"; break;
                case "effect": record.EffectText = "한 타일만 피해"; break;
                case "footprint": record.ChargeTileFootprint = ChargeTileFootprint.OneByOne; break;
            }
            Assert.That(CardDatabaseValidator.Validate(database).Errors, Has.Some.Contains("Earthquake must be"));
        }

        [TestCaseSource(nameof(AllLevels))]
        public void AllLevels_BothSeats_WholeEnemyBoardAndMaster_BuildingMultiplierBeforeDefense(PlayerId owner, int level)
        {
            var state = State(owner); var enemy = state.GetOpponent(owner).Id;
            var unit = Unit(state, enemy, 0, defense: 4, hiding: true, flying: true);
            var building = Building(state, enemy, 1, defense: 4);
            var ally = Unit(state, owner, 0, defense: 4);
            Unit(state, owner, 1, spellPower: 70);
            var unused = state.GetPlayer(owner).Hand.Add(EarthquakeRules.CardId);
            var selected = state.GetPlayer(owner).Hand.Add(EarthquakeRules.CardId);
            var discard = state.GetPlayer(owner).Discard.Count;
            var beforeGold = state.GetPlayer(owner).Resources.Gold;
            Cast(state, owner, level, selected.RuntimeId);
            var damage = 10 + level;
            Assert.That(unit.CurrentHp, Is.EqualTo(500 - (damage - 4)));
            Assert.That(building.CurrentHp, Is.EqualTo(500 - (damage * 3 - 4)), "Multiply before defense, not after.");
            Assert.That(state.GetPlayer(enemy).Master.CurrentHp, Is.EqualTo(333 - damage));
            Assert.That(ally.CurrentHp, Is.EqualTo(500));
            Assert.That(state.GetPlayer(owner).Master.CurrentHp, Is.EqualTo(333));
            Assert.That(unit.IsHiding, Is.False, "Actual area HP loss reveals Hiding.");
            Assert.That(state.ValuePopupEvents.Count(e => e.SourceCardId == EarthquakeRules.CardId), Is.EqualTo(3));
            Assert.That(state.ValuePopupEvents.Where(e => e.SourceCardId == EarthquakeRules.CardId)
                .All(e => e.Cause == BattleValueChangeCause.Spell && e.DamageType == DamageType.Physical && e.SourceOwnerId == owner), Is.True);
            Assert.That(state.GetPlayer(owner).Hand.Contains(EarthquakeRules.CardId, selected.RuntimeId), Is.False);
            Assert.That(state.GetPlayer(owner).Hand.Contains(EarthquakeRules.CardId, unused.RuntimeId), Is.True);
            Assert.That(state.GetPlayer(owner).Resources.Gold, Is.EqualTo(beforeGold));
            Assert.That(state.GetPlayer(owner).Discard.Count, Is.EqualTo(discard + 1));
            Assert.That(state.PersistentEffects, Is.Empty);
            Assert.That(state.PlayerBoard.FalloutStacks + state.AIBoard.FalloutStacks, Is.Zero);
            var definition = Provider().GetRequired(EarthquakeRules.CardId);
            Assert.That(((ScriptedSpellCardDefinition)definition).Damage, Is.EqualTo(10));
            Assert.That(CardStatDisplay.TryCreate(definition, level, out var stats, 999), Is.True);
            Assert.That(stats.Attack, Is.EqualTo(damage));
            Assert.That(stats.HasHp, Is.False);
            Assert.That(CardStatDisplay.GetEffectText(definition, EarthquakeRules.EffectDescription, level),
                Does.Contain("물리 데미지 " + damage + "을 입힌다.").And.Contain("건물에는 3배"));
        }

        [TestCase(false, "normal", 493)][TestCase(true, "normal", 473)]
        [TestCase(false, "drained", 470)][TestCase(true, "drained", 410)]
        [TestCase(false, "invincible", 500)][TestCase(true, "invincible", 500)]
        [TestCase(false, "sealed", 500)][TestCase(true, "sealed", 500)]
        [TestCase(false, "erasure", 493)][TestCase(true, "erasure", 473)]
        public void PhysicalDamage_HonorsProtectionAndSuppression(bool isBuilding, string status, int hp)
        {
            var state = State();
            OccupantState target = isBuilding ? Building(state, PlayerId.AI, 0, defense: 3) : Unit(state, PlayerId.AI, 0, defense: 3);
            if (status == "drained") target.IsDrained = true;
            if (status == "invincible") target.AddInvincibleEffect(InvincibleDurationType.Always);
            if (status == "sealed") target.EnterSealbound(3);
            if (status == "erasure") target.ApplyErasure();
            Cast(state, PlayerId.Player);
            Assert.That(target.CurrentHp, Is.EqualTo(hp));
            if (status == "sealed") Assert.That(state.ValuePopupEvents.Any(e => e.RuntimeId == target.RuntimeId), Is.False);
            if (status == "invincible") Assert.That(state.ValuePopupEvents.Single(e => e.RuntimeId == target.RuntimeId).IsInvinciblePrevented, Is.True);
        }

        [Test]
        public void AreaDamage_HitsHiddenAndShielderOnce_AndDoesNotTransferOrHalveFlying()
        {
            var state = State();
            var rear = Unit(state, PlayerId.AI, 0, hiding: true, flying: true, row: 1);
            var guard = Unit(state, PlayerId.AI, 0, shielder: true);
            Cast(state, PlayerId.Player);
            Assert.That(rear.CurrentHp, Is.EqualTo(490));
            Assert.That(guard.CurrentHp, Is.EqualTo(490));
            Assert.That(state.ValuePopupEvents.Count(e => e.RuntimeId == guard.RuntimeId), Is.EqualTo(1));
        }

        [Test]
        public void EndureAndDemonKing_UseSharedDestructionRules()
        {
            var state = State(); var endure = Unit(state, PlayerId.AI, 0, hp: 8, endure: true);
            var king = Unit(state, PlayerId.AI, 1, hp: 8, cardId: "DemonKing");
            var lethal = Unit(state, PlayerId.AI, 3, hp: 8);
            Cast(state, PlayerId.Player);
            Assert.That(endure.CurrentHp, Is.EqualTo(1)); Assert.That(endure.EndureUsed, Is.True);
            Assert.That(state.AIBoard.GetOccupant(king.Position), Is.SameAs(king));
            Assert.That(king.IsDemonKingRevivalPending, Is.True);
            Assert.That(state.AIBoard.GetOccupant(lethal.Position), Is.Null);
        }

        [Test]
        public void SharedOccupantReferenceOnTwoTiles_ReceivesOnePacket()
        {
            var state = State(); var building = Building(state, PlayerId.AI, 0);
            state.AIBoard.Place(new TileCoord(1, 0), building);
            Cast(state, PlayerId.Player);
            Assert.That(building.CurrentHp, Is.EqualTo(470));
            Assert.That(state.ValuePopupEvents.Count(e => e.RuntimeId == building.RuntimeId), Is.EqualTo(1));
        }

        [TestCase(PlayerId.Player)][TestCase(PlayerId.AI)]
        public void EmptyEnemyTiles_RemainValid_OnlyMasterIsDamaged_NoPersistentEffect(PlayerId owner)
        {
            var state = State(owner); Cast(state, owner);
            Assert.That(state.GetOpponent(owner).Master.CurrentHp, Is.EqualTo(323));
            Assert.That(state.ValuePopupEvents.Count(e => e.SourceCardId == EarthquakeRules.CardId), Is.EqualTo(1));
            Assert.That(state.PersistentEffects, Is.Empty);
        }

        [Test]
        public void AllAreaPacketsPrecedeReactorChains_AndBothMasterDefeatsResolveTogether()
        {
            var state = State(); state.Player.Master.CurrentHp = 20; state.AI.Master.CurrentHp = 30;
            Building(state, PlayerId.AI, 0, hp: 25, cardId: "NuclearPowerPlant");
            Building(state, PlayerId.AI, 1, hp: 25, cardId: "NuclearPowerPlant");
            var ally = Building(state, PlayerId.Player, 0, hp: 100, cardId: "NuclearPowerPlant");
            Cast(state, PlayerId.Player);
            Assert.That(ally.CurrentHp, Is.EqualTo(40));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(-40));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(-40));
            Assert.That(state.Result.IsDraw, Is.True);
            var packets = state.ValuePopupEvents.ToList();
            Assert.That(packets.FindLastIndex(e => e.SourceCardId == EarthquakeRules.CardId),
                Is.LessThan(packets.FindIndex(e => e.Cause == BattleValueChangeCause.NuclearPowerPlant)));
            Assert.That(packets.Count(e => e.RuntimeId == ally.RuntimeId && e.Cause == BattleValueChangeCause.NuclearPowerPlant), Is.EqualTo(2), "Both plants hit the living ally; no extra popup is recorded after a Master has zero remaining HP.");
        }

        [TestCase("missing")][TestCase("resources")][TestCase("targeted")]
        [TestCase("opponentTurn")][TestCase("phase")]
        public void InvalidCast_DoesNotSpendOrDamage(string failure)
        {
            var state = State(); var card = state.Player.Hand.Add(EarthquakeRules.CardId);
            if (failure == "resources") state.Player.Resources.Spend(state.Player.Resources.Clone());
            else state.Player.Resources.Add(new ResourceSet(0, 0, 0, 3));
            if (failure == "opponentTurn") state.RestoreRuntimeState(1, PlayerId.AI, PhaseType.Main);
            if (failure == "phase") state.SetPhase(PhaseType.TurnEnd);
            var gold = state.Player.Resources.Gold;
            var service = new SpellService(Provider());
            Assert.Throws<InvalidOperationException>(() =>
            {
                if (failure == "targeted") service.CastScriptedSpell(state, PlayerId.Player, EarthquakeRules.CardId, PlayerId.AI, new TileCoord(0, 0), card.RuntimeId);
                else service.CastScriptedSpell(state, PlayerId.Player, EarthquakeRules.CardId, failure == "missing" ? "wrong-runtime" : card.RuntimeId);
            });
            Assert.That(state.Player.Hand.Contains(EarthquakeRules.CardId, card.RuntimeId), Is.True);
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(gold));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.ValuePopupEvents, Is.Empty);
        }

        [Test]
        public void AiUsesUntargetedEarthquakeToWin_WithoutMutatingLiveStateDuringPlanning()
        {
            var state = State(PlayerId.AI); state.Player.Master.CurrentHp = 10;
            state.AI.Hand.Cards.ToList().ForEach(c => state.AI.Hand.Remove(c.CardId, c.RuntimeId));
            state.AI.Hand.Add(EarthquakeRules.CardId); state.AI.Resources.Add(new ResourceSet(0, 0, 0, 3));
            var planner = new AiDecisionService(Provider(), new TargetingService(), ZeroCardUpgradeLevelProvider.Instance,
                new AiSearchOptions { MaxDepth = 1, MaxMilliseconds = 1000 });
            var before = state.Player.Master.CurrentHp;
            var command = planner.GetNextCommand(state);
            Assert.That(command, Is.TypeOf<CastScriptedSpellCommand>());
            var cast = (CastScriptedSpellCommand)command;
            Assert.That(cast.CardId, Is.EqualTo(EarthquakeRules.CardId)); Assert.That(cast.HasTarget, Is.False);
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(before));
            Assert.That(state.AI.Hand.Contains(EarthquakeRules.CardId), Is.True);
            var provider = Provider();
            var flow = new BattleFlowController(new BattleSetupService(), new MulliganService(), new TurnStartService(provider),
                new BattleCommandProcessor(new PlayCardService(provider, new SummonService()), new SpellService(provider),
                    new MoveService(), new AttackService(), new EndTurnService()));
            flow.RestoreBattleState(state);
            flow.ExecuteCommand(PlayerId.AI, command);
            Assert.That(state.Result.Winner, Is.EqualTo(PlayerId.AI));
        }
    }
}