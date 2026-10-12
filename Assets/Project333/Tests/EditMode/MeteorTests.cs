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
    public sealed class MeteorTests
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
            bool shielder = false, string cardId = "Goblin", int row = 0, int spellPower = 0, int physicalDefense = 0)
        {
            var unit = new UnitState(Guid.NewGuid().ToString(), cardId, owner, new TileCoord(column, row),
                AttackType.Melee, 1, hp, true, false, 0, hasEndure: endure,
                physicalDefense: physicalDefense, magicDefense: defense, hasHiding: hiding,
                hasFlying: flying, hasShielder: shielder, spellPower: spellPower);
            state.GetBoard(owner).Place(unit.Position, unit);
            return unit;
        }
        private static BuildingState Building(BattleState state, PlayerId owner, int column,
            int hp = 500, int defense = 0, string cardId = "ManaPond", int spellPower = 0, int physicalDefense = 0, bool flying = false)
        {
            var building = new BuildingState(Guid.NewGuid().ToString(), cardId, owner,
                new TileCoord(column, 0), false, 0, hp, physicalDefense: physicalDefense,
                magicDefense: defense, spellPower: spellPower, hasFlying: flying);
            state.GetBoard(owner).Place(building.Position, building);
            return building;
        }
        private static void Cast(BattleState state, PlayerId owner, int level = 0, string runtimeId = null)
        {
            var upgrades = new InMemoryCardUpgradeLevelProvider();
            upgrades.SetUpgradeLevel(owner, MeteorRules.CardId, level);
            upgrades.SetUpgradeLevel(state.GetOpponent(owner).Id, MeteorRules.CardId, level == 13 ? 0 : 13);
            if (runtimeId == null) runtimeId = state.GetPlayer(owner).Hand.Add(MeteorRules.CardId).RuntimeId;
            state.GetPlayer(owner).Resources.Add(new ResourceSet(7, 0, 0, 0));
            new SpellService(Provider(), upgrades).CastScriptedSpell(state, owner, MeteorRules.CardId, runtimeId);
        }
        private static void Amplify(BattleState state, PlayerId owner)
        {
            var card = state.GetPlayer(owner).Hand.Add(MagicAmplificationCircleRules.CardId);
            state.GetPlayer(owner).Resources.Add(new ResourceSet(1, 0, 0, 0));
            new SpellService(Provider()).CastScriptedSpell(state, owner, card.CardId, card.RuntimeId);
        }
        private static IEnumerable<TestCaseData> AllLevels()
        {
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
                for (var level = 0; level <= 13; level++) yield return new TestCaseData(owner, level);
        }

        [Test]
        public void Definition_MatchesConfirmedCardUpgradeAndReleasePools()
        {
            Assert.That(CardDatabaseValidator.ValidateJson(Json()).Errors, Is.Empty);
            var record = JsonCardDefinitionDatabase.FromJson(Json()).Cards.Single(c => c.Id == MeteorRules.CardId);
            Assert.That(record.DisplayName, Is.EqualTo("메테오"));
            Assert.That(record.DefinitionType, Is.EqualTo(JsonCardDefinitionKind.ScriptedSpell));
            Assert.That(record.Rarity, Is.EqualTo(CardRarity.Unique));
            Assert.That(record.Affiliation, Is.EqualTo(CardAffiliation.Fantasy));
            Assert.That(record.Cost.Mana, Is.EqualTo(7));
            Assert.That(record.Cost.Gold + record.Cost.Qi + record.Cost.Power, Is.Zero);
            Assert.That(record.Damage, Is.EqualTo(40));
            Assert.That(record.DamageType, Is.EqualTo(DamageType.Magic));
            Assert.That(record.EffectText, Is.EqualTo(MeteorRules.EffectDescription));
            Assert.That(record.SpecialEffectText, Is.Empty);
            Assert.That(record.IncludeInDraft && record.IncludeInRewards, Is.True);
            Assert.That(CardUpgradeRules.IsCardUpgradeable(record.ToDefinition()), Is.True);
        }

        [TestCase("cost")][TestCase("damage")][TestCase("type")][TestCase("rarity")]
        [TestCase("affiliation")][TestCase("draft")][TestCase("rewards")][TestCase("special")]
        [TestCase("effect")][TestCase("footprint")]
        public void ValidatorRejectsIncorrectMeteorAuthoring(string field)
        {
            var database = JsonCardDefinitionDatabase.FromJson(Json());
            var record = database.Cards.Single(c => c.Id == MeteorRules.CardId);
            switch (field)
            {
                case "cost": record.Cost.Mana = 6; break;
                case "damage": record.Damage = 39; break;
                case "type": record.DamageType = DamageType.Physical; break;
                case "rarity": record.Rarity = CardRarity.Uncommon; break;
                case "affiliation": record.Affiliation = CardAffiliation.Neutral; break;
                case "draft": record.IncludeInDraft = false; break;
                case "rewards": record.IncludeInRewards = false; break;
                case "special": record.SpecialEffectText = "비행"; break;
                case "effect": record.EffectText = "한 타일만 피해"; break;
                case "footprint": record.ChargeTileFootprint = ChargeTileFootprint.OneByOne; break;
            }
            Assert.That(CardDatabaseValidator.Validate(database).Errors, Has.Some.Contains("Meteor must be"));
        }

        [TestCaseSource(nameof(AllLevels))]
        public void AllLevels_BothSeats_WholeEnemyBoard_UsesOwnLiveSpellPowerOnceAndMagicDefense(PlayerId owner, int level)
        {
            var state = State(owner); var enemy = state.GetOpponent(owner).Id;
            var unit = Unit(state, enemy, 0, defense: 4, hiding: true, flying: true, physicalDefense: 999);
            var building = Building(state, enemy, 1, defense: 4, physicalDefense: 999);
            var ally = Unit(state, owner, 0, spellPower: 70);
            Unit(state, enemy, 3, spellPower: 100);
            Amplify(state, owner);
            var unused = state.GetPlayer(owner).Hand.Add(MeteorRules.CardId);
            var selected = state.GetPlayer(owner).Hand.AddTemporaryReplicate(MeteorRules.CardId);
            var discard = state.GetPlayer(owner).Discard.Count;
            var beforeMana = state.GetPlayer(owner).Resources.Mana;
            var beforeGold = state.GetPlayer(owner).Resources.Gold;
            Cast(state, owner, level, selected.RuntimeId);
            var damage = 40 + level + 80;
            Assert.That(unit.CurrentHp, Is.EqualTo(500 - (damage - 4)));
            Assert.That(building.CurrentHp, Is.EqualTo(500 - (damage - 4)), "Buildings receive the same Magic packet, not Earthquake's triple packet.");
            Assert.That(state.GetPlayer(enemy).Master.CurrentHp, Is.EqualTo(333 - damage));
            Assert.That(ally.CurrentHp, Is.EqualTo(500));
            Assert.That(state.GetPlayer(owner).Master.CurrentHp, Is.EqualTo(333));
            Assert.That(unit.IsHiding, Is.False);
            Assert.That(state.ValuePopupEvents.Count(e => e.SourceCardId == MeteorRules.CardId), Is.EqualTo(4));
            Assert.That(state.ValuePopupEvents.Where(e => e.SourceCardId == MeteorRules.CardId)
                .All(e => e.Cause == BattleValueChangeCause.Spell && e.DamageType == DamageType.Magic && e.SourceOwnerId == owner), Is.True);
            Assert.That(state.GetPlayer(owner).Hand.Contains(MeteorRules.CardId, selected.RuntimeId), Is.False);
            Assert.That(state.GetPlayer(owner).Hand.Contains(MeteorRules.CardId, unused.RuntimeId), Is.True);
            Assert.That(state.GetPlayer(owner).Resources.Mana, Is.EqualTo(beforeMana));
            Assert.That(state.GetPlayer(owner).Resources.Gold, Is.EqualTo(beforeGold));
            Assert.That(state.GetPlayer(owner).Discard.Count, Is.EqualTo(discard + 1));
            Assert.That(state.PersistentEffects.Count, Is.EqualTo(1), "Only the preexisting amplification remains.");
            Assert.That(state.PlayerBoard.FalloutStacks + state.AIBoard.FalloutStacks, Is.Zero);
            var definition = Provider().GetRequired(MeteorRules.CardId);
            Assert.That(((ScriptedSpellCardDefinition)definition).Damage, Is.EqualTo(40));
            Assert.That(CardStatDisplay.TryCreate(definition, level, out var stats, 80), Is.True);
            Assert.That(stats.Attack, Is.EqualTo(damage));
            Assert.That(stats.HasHp, Is.False);
            Assert.That(CardStatDisplay.GetEffectText(definition, MeteorRules.EffectDescription, level),
                Is.EqualTo("상대 전장 전체에 마법 데미지 " + (40 + level) + "을 입힌다."));
        }

        [TestCase(false, "normal", 463)][TestCase(true, "normal", 463)]
        [TestCase(false, "drained", 380)][TestCase(true, "drained", 380)]
        [TestCase(false, "invincible", 500)][TestCase(true, "invincible", 500)]
        [TestCase(false, "sealed", 500)][TestCase(true, "sealed", 500)]
        [TestCase(false, "erasure", 463)][TestCase(true, "erasure", 463)]
        public void MagicDamage_HonorsProtectionAndSuppression(bool isBuilding, string status, int hp)
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

        [TestCase(false, "normal", 60)][TestCase(true, "normal", 60)]
        [TestCase(false, "drained", 40)][TestCase(true, "drained", 40)]
        [TestCase(false, "erasure", 40)][TestCase(true, "erasure", 40)]
        [TestCase(false, "sealed", 40)][TestCase(true, "sealed", 40)]
        [TestCase(false, "flying", 60)][TestCase(true, "flying", 60)]
        public void SpellPower_OnlyActiveOwnSourcesContribute(bool isBuilding, string status, int expected)
        {
            var state = State();
            OccupantState source = isBuilding ? Building(state, PlayerId.Player, 0, spellPower: 20, flying: status == "flying")
                : Unit(state, PlayerId.Player, 0, spellPower: 20, flying: status == "flying");
            if (status == "drained") source.IsDrained = true;
            if (status == "erasure") source.ApplyErasure();
            if (status == "sealed") source.EnterSealbound(3);
            var before = state.AI.Master.CurrentHp;
            Cast(state, PlayerId.Player);
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(before - expected));
        }

        [TestCase(PlayerId.Player)][TestCase(PlayerId.AI)]
        public void AreaDamage_HitsHiddenAndShielderOnce_WithoutTransferOrFlyingReduction(PlayerId owner)
        {
            var state = State(owner); var enemy = state.GetOpponent(owner).Id;
            var rear = Unit(state, enemy, 0, hiding: true, flying: true, row: 1);
            var guard = Unit(state, enemy, 0, shielder: true);
            Cast(state, owner);
            Assert.That(rear.CurrentHp, Is.EqualTo(460));
            Assert.That(guard.CurrentHp, Is.EqualTo(460));
            Assert.That(state.ValuePopupEvents.Count(e => e.RuntimeId == rear.RuntimeId), Is.EqualTo(1));
            Assert.That(state.ValuePopupEvents.Count(e => e.RuntimeId == guard.RuntimeId), Is.EqualTo(1));
        }

        [TestCase("normal", true)][TestCase("drained", false)][TestCase("erasure", false)]
        public void DefeatedDemonKing_RevivesOnlyWhenNotSuppressed(string status, bool revival)
        {
            var state = State();
            var king = Unit(state, PlayerId.AI, 0, hp: 8, cardId: "DemonKing");
            if (status == "drained") king.IsDrained = true;
            if (status == "erasure") king.ApplyErasure();
            var endure = Unit(state, PlayerId.AI, 1, hp: 8, endure: true);
            Cast(state, PlayerId.Player);
            Assert.That(king.IsDemonKingRevivalPending, Is.EqualTo(revival));
            if (revival) Assert.That(state.AIBoard.GetOccupant(king.Position), Is.SameAs(king));
            else Assert.That(state.AIBoard.GetOccupant(king.Position), Is.Null);
            Assert.That(endure.CurrentHp, Is.EqualTo(1));
            Assert.That(endure.EndureUsed, Is.True);
        }

        [Test]
        public void SharedOccupantReferenceOnTwoTiles_ReceivesOnePacket()
        {
            var state = State(); var building = Building(state, PlayerId.AI, 0);
            state.AIBoard.Place(new TileCoord(1, 0), building);
            Cast(state, PlayerId.Player);
            Assert.That(building.CurrentHp, Is.EqualTo(460));
            Assert.That(state.ValuePopupEvents.Count(e => e.RuntimeId == building.RuntimeId), Is.EqualTo(1));
        }

        [TestCase(PlayerId.Player)][TestCase(PlayerId.AI)]
        public void EmptyEnemyTiles_RemainValid_OnlyMasterIsDamaged_NoPersistentEffect(PlayerId owner)
        {
            var state = State(owner); Cast(state, owner);
            Assert.That(state.GetOpponent(owner).Master.CurrentHp, Is.EqualTo(293));
            Assert.That(state.ValuePopupEvents.Count(e => e.SourceCardId == MeteorRules.CardId), Is.EqualTo(1));
            Assert.That(state.PersistentEffects, Is.Empty);
        }

        [TestCase(PlayerId.Player, 60)][TestCase(PlayerId.AI, 60)]
        [TestCase(PlayerId.Player, 70)][TestCase(PlayerId.AI, 70)]
        [TestCase(PlayerId.Player, 50)][TestCase(PlayerId.AI, 50)]
        public void AllAreaPacketsPrecedeReactorChains_AndBothMasterDefeatsCompareFinalHp(PlayerId owner, int enemyHp)
        {
            var state = State(owner); var enemy = state.GetOpponent(owner).Id;
            state.GetPlayer(owner).Master.CurrentHp = 20; state.GetPlayer(enemy).Master.CurrentHp = enemyHp;
            Building(state, enemy, 0, hp: 25, cardId: "NuclearPowerPlant");
            Building(state, enemy, 1, hp: 25, cardId: "NuclearPowerPlant");
            var ally = Building(state, owner, 0, hp: 100, cardId: "NuclearPowerPlant");
            Cast(state, owner);
            Assert.That(ally.CurrentHp, Is.EqualTo(40));
            Assert.That(state.GetPlayer(owner).Master.CurrentHp, Is.EqualTo(-40));
            Assert.That(state.GetPlayer(enemy).Master.CurrentHp, Is.EqualTo(enemyHp - 100));
            Assert.That(state.Result.IsDraw, Is.EqualTo(enemyHp == 60));
            if (enemyHp != 60) Assert.That(state.Result.Winner, Is.EqualTo(enemyHp > 60 ? enemy : owner));
            var packets = state.ValuePopupEvents.ToList();
            Assert.That(packets.FindLastIndex(e => e.SourceCardId == MeteorRules.CardId),
                Is.LessThan(packets.FindIndex(e => e.Cause == BattleValueChangeCause.NuclearPowerPlant)));
            Assert.That(packets.Count(e => e.RuntimeId == ally.RuntimeId && e.Cause == BattleValueChangeCause.NuclearPowerPlant), Is.EqualTo(2));
        }

        [TestCase("missing")][TestCase("resources")][TestCase("targeted")]
        [TestCase("opponentTurn")][TestCase("phase")]
        public void InvalidCast_DoesNotSpendOrDamage(string failure)
        {
            var state = State(); var card = state.Player.Hand.Add(MeteorRules.CardId);
            if (failure != "resources") state.Player.Resources.Add(new ResourceSet(7, 0, 0, 0));
            if (failure == "opponentTurn") state.RestoreRuntimeState(1, PlayerId.AI, PhaseType.Main);
            if (failure == "phase") state.SetPhase(PhaseType.TurnEnd);
            var mana = state.Player.Resources.Mana;
            var service = new SpellService(Provider());
            Assert.Throws<InvalidOperationException>(() =>
            {
                if (failure == "targeted") service.CastScriptedSpell(state, PlayerId.Player, MeteorRules.CardId, PlayerId.AI, new TileCoord(0, 0), card.RuntimeId);
                else service.CastScriptedSpell(state, PlayerId.Player, MeteorRules.CardId, failure == "missing" ? "wrong-runtime" : card.RuntimeId);
            });
            Assert.That(state.Player.Hand.Contains(MeteorRules.CardId, card.RuntimeId), Is.True);
            Assert.That(state.Player.Resources.Mana, Is.EqualTo(mana));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.ValuePopupEvents, Is.Empty);
        }

        [Test]
        public void AiUsesUntargetedMeteorWithSpellPowerToWin_WithoutMutatingLiveStateDuringPlanning()
        {
            var state = State(PlayerId.AI); state.Player.Master.CurrentHp = 50;
            state.AI.Hand.Cards.ToList().ForEach(c => state.AI.Hand.Remove(c.CardId, c.RuntimeId));
            state.AI.Hand.Add(MeteorRules.CardId); state.AI.Resources.Add(new ResourceSet(7, 0, 0, 0));
            Unit(state, PlayerId.AI, 0, spellPower: 10);
            var planner = new AiDecisionService(Provider(), new TargetingService(), ZeroCardUpgradeLevelProvider.Instance,
                new AiSearchOptions { MaxDepth = 1, MaxMilliseconds = 1000 });
            var before = AiBattleStateCopy.PositionKey(state);
            var command = planner.GetNextCommand(state);
            Assert.That(command, Is.TypeOf<CastScriptedSpellCommand>());
            var cast = (CastScriptedSpellCommand)command;
            Assert.That(cast.CardId, Is.EqualTo(MeteorRules.CardId)); Assert.That(cast.HasTarget, Is.False);
            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(before));
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
