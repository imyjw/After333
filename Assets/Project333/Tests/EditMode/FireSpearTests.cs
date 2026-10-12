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
    public sealed class FireSpearTests
    {
        private static string Json() => File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json");
        private static ICardDefinitionProvider Provider() => JsonCardDefinitionDatabase.FromJson(Json()).CreateProvider();

        private static BattleState State(PlayerId owner = PlayerId.Player)
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner));
            state.SetPhase(PhaseType.Main);
            // Isolate payment tests from the initial battle resource grant.
            state.Player.Resources.Spend(state.Player.Resources.Clone());
            state.AI.Resources.Spend(state.AI.Resources.Clone());
            return state;
        }

        private static UnitState Unit(BattleState state, PlayerId owner, int column = 0, int row = 0,
            int hp = 500, int defense = 0, bool hiding = false, bool shielder = false,
            bool endure = false, bool flying = false, int spellPower = 0, string cardId = "Goblin")
        {
            var unit = new UnitState(Guid.NewGuid().ToString(), cardId, owner, new TileCoord(column, row),
                AttackType.Melee, 1, hp, true, false, 0, hasEndure: endure, magicDefense: defense,
                physicalDefense: 999, hasHiding: hiding, hasShielder: shielder,
                hasFlying: flying, spellPower: spellPower);
            state.GetBoard(owner).Place(unit.Position, unit);
            return unit;
        }

        private static BuildingState Building(BattleState state, PlayerId owner, int column = 0,
            int hp = 500, int defense = 0, string cardId = "ManaPond", int spellPower = 0, int row = 0)
        {
            var building = new BuildingState(Guid.NewGuid().ToString(), cardId, owner,
                new TileCoord(column, row), false, 0, hp, physicalDefense: 999,
                magicDefense: defense, spellPower: spellPower);
            state.GetBoard(owner).Place(building.Position, building);
            return building;
        }

        private static IEnumerable<TestCaseData> AnchorKinds()
        {
            foreach (var caster in new[] { PlayerId.Player, PlayerId.AI })
                foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
                    foreach (var kind in new[] { "unit", "building", "master" })
                        yield return new TestCaseData(caster, owner, kind);
        }

        [TestCaseSource(nameof(AnchorKinds))]
        public void LegalAnchorIncludesLivingUnitsBuildingsAndMastersOnEitherBoard(PlayerId caster, PlayerId owner, string kind)
        {
            var state = State(caster);
            OccupantState target = kind == "master" ? state.GetPlayer(owner).Master :
                kind == "building" ? (OccupantState)Building(state, owner) : Unit(state, owner);
            Assert.That(FireSpearRules.IsLegalTarget(state, caster, owner, target.Position), Is.True);
        }

        [TestCase(PlayerId.Player, true)][TestCase(PlayerId.AI, false)]
        public void HiddenFriendlyAnchorIsLegalButHiddenEnemyAnchorIsNot(PlayerId owner, bool expected)
        {
            var state = State(); var target = Unit(state, owner, hiding: true);
            Assert.That(target.IsHiding, Is.True);
            Assert.That(FireSpearRules.IsLegalTarget(state, PlayerId.Player, owner, target.Position), Is.EqualTo(expected));
        }

        [TestCase("empty")][TestCase("dead")][TestCase("sealed")]
        [TestCase("column-low")][TestCase("column-high")][TestCase("row-low")][TestCase("row-high")]
        [TestCase("owner")][TestCase("caster")][TestCase("state")]
        public void IllegalAnchorCannotSelectMissingDeadSealedOrOutsideOccupants(string reason)
        {
            var state = State(); var owner = PlayerId.AI; var caster = PlayerId.Player;
            var coord = new TileCoord(0, 0);
            if (reason != "empty")
            {
                var target = Unit(state, owner);
                if (reason == "dead") target.CurrentHp = 0;
                if (reason == "sealed") target.EnterSealbound(2);
            }
            switch (reason)
            {
                case "column-low": coord = new TileCoord(-1, 0); break;
                case "column-high": coord = new TileCoord(5, 0); break;
                case "row-low": coord = new TileCoord(0, -1); break;
                case "row-high": coord = new TileCoord(0, 2); break;
                case "owner": owner = (PlayerId)99; break;
                case "caster": caster = (PlayerId)99; break;
                case "state": state = null!; break;
            }
            Assert.That(FireSpearRules.IsLegalTarget(state, caster, owner, coord), Is.False);
        }

        private static void Cast(BattleState state, PlayerId caster, PlayerId targetOwner, TileCoord coord,
            int level = 0, string runtimeId = null!, bool fund = true)
        {
            var levels = new InMemoryCardUpgradeLevelProvider();
            levels.SetUpgradeLevel(caster, FireSpearRules.CardId, level);
            levels.SetUpgradeLevel(state.GetOpponent(caster).Id, FireSpearRules.CardId, level == 13 ? 0 : 13);
            if (runtimeId == null) runtimeId = state.GetPlayer(caster).Hand.Add(FireSpearRules.CardId).RuntimeId;
            if (fund) state.GetPlayer(caster).Resources.Add(new ResourceSet(3, 0, 0, 0));
            new SpellService(Provider(), levels).CastScriptedSpell(state, caster, FireSpearRules.CardId,
                targetOwner, coord, runtimeId);
        }

        private static IEnumerable<TestCaseData> CastLevels()
        {
            foreach (var caster in new[] { PlayerId.Player, PlayerId.AI })
                foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
                    for (var level = 0; level <= 13; level++) yield return new TestCaseData(caster, owner, level);
        }

        [TestCaseSource(nameof(CastLevels))]
        public void AllLevelsBothSeatsHitOnlyChosenOwnersTwoCellsWithCurrentSpellPower(PlayerId caster, PlayerId owner, int level)
        {
            var state = State(caster);
            var front = Unit(state, owner, defense: 4);
            var back = Building(state, owner, defense: 4, row: 1);
            var otherBoard = Unit(state, state.GetOpponent(owner).Id);
            var otherColumn = Unit(state, owner, column: 1);
            Unit(state, caster, column: 4, spellPower: 7);
            Unit(state, state.GetOpponent(caster).Id, column: 3, spellPower: 300);
            var unused = state.GetPlayer(caster).Hand.Add(FireSpearRules.CardId);
            var selected = state.GetPlayer(caster).Hand.AddTemporaryReplicate(FireSpearRules.CardId);
            state.GetPlayer(caster).Resources.Add(new ResourceSet(3, 0, 0, 0));
            var mana = state.GetPlayer(caster).Resources.Mana;
            var gold = state.GetPlayer(caster).Resources.Gold;
            var discarded = state.GetPlayer(caster).Discard.Count;
            Cast(state, caster, owner, front.Position, level, selected.RuntimeId, fund: false);
            var damage = 30 + level + 7;
            Assert.That(front.CurrentHp, Is.EqualTo(500 - (damage - 4)));
            Assert.That(back.CurrentHp, Is.EqualTo(500 - (damage - 4)));
            Assert.That(otherBoard.CurrentHp, Is.EqualTo(500), "A column belongs to only the chosen owner's board.");
            Assert.That(otherColumn.CurrentHp, Is.EqualTo(500));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(333));
            var events = state.ValuePopupEvents.Where(e => e.SourceCardId == FireSpearRules.CardId).ToArray();
            Assert.That(events.Length, Is.EqualTo(2));
            Assert.That(events.All(e => e.Cause == BattleValueChangeCause.Spell &&
                e.DamageType == DamageType.Magic && e.SourceOwnerId == caster), Is.True);
            Assert.That(state.GetPlayer(caster).Resources.Mana, Is.EqualTo(mana - 3));
            Assert.That(state.GetPlayer(caster).Resources.Gold, Is.EqualTo(gold));
            Assert.That(state.GetPlayer(caster).Hand.Contains(FireSpearRules.CardId, selected.RuntimeId), Is.False);
            Assert.That(state.GetPlayer(caster).Hand.Contains(FireSpearRules.CardId, unused.RuntimeId), Is.True);
            Assert.That(state.GetPlayer(caster).Discard.Count, Is.EqualTo(discarded + 1));
            Assert.That(state.PersistentEffects, Is.Empty);
            Assert.That(state.PlayerBoard.FalloutStacks + state.AIBoard.FalloutStacks, Is.Zero);
        }

        [TestCase(PlayerId.Player, PlayerId.Player, 0)][TestCase(PlayerId.Player, PlayerId.Player, 1)]
        [TestCase(PlayerId.Player, PlayerId.AI, 0)][TestCase(PlayerId.Player, PlayerId.AI, 1)]
        [TestCase(PlayerId.AI, PlayerId.Player, 0)][TestCase(PlayerId.AI, PlayerId.Player, 1)]
        [TestCase(PlayerId.AI, PlayerId.AI, 0)][TestCase(PlayerId.AI, PlayerId.AI, 1)]
        public void EitherRowAnchorHitsFrontAndMasterRearButNotOtherBoardMaster(PlayerId caster, PlayerId owner, int row)
        {
            var state = State(caster); var front = Unit(state, owner, column: 2);
            Cast(state, caster, owner, new TileCoord(2, row));
            Assert.That(front.CurrentHp, Is.EqualTo(470));
            Assert.That(state.GetPlayer(owner).Master.CurrentHp, Is.EqualTo(303));
            Assert.That(state.GetOpponent(owner).Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.ValuePopupEvents.Count(e => e.SourceCardId == FireSpearRules.CardId), Is.EqualTo(2));
        }

        [TestCase(PlayerId.Player)][TestCase(PlayerId.AI)]
        public void CollateralHiddenFlyingRearAndShielderEachReceiveOneFullAreaPacket(PlayerId caster)
        {
            var state = State(caster); var enemy = state.GetOpponent(caster).Id;
            var guard = Unit(state, enemy, shielder: true);
            var hidden = Unit(state, enemy, row: 1, hiding: true, flying: true);
            Cast(state, caster, enemy, guard.Position);
            Assert.That(guard.CurrentHp, Is.EqualTo(470));
            Assert.That(hidden.CurrentHp, Is.EqualTo(470));
            Assert.That(hidden.IsHiding, Is.False);
            Assert.That(state.ValuePopupEvents.Count(e => e.RuntimeId == guard.RuntimeId), Is.EqualTo(1));
            Assert.That(state.ValuePopupEvents.Count(e => e.RuntimeId == hidden.RuntimeId), Is.EqualTo(1));
        }

        [TestCase("normal", 473)][TestCase("drained", 410)][TestCase("erasure", 473)]
        [TestCase("sealed", 500)][TestCase("invincible", 500)][TestCase("stunned", 473)][TestCase("frozen", 473)]
        public void CollateralBuildingUsesMatchingDefenseAndExistingProtectionOnly(string status, int expectedHp)
        {
            var state = State(); var anchor = Unit(state, PlayerId.AI);
            var back = Building(state, PlayerId.AI, defense: 3, row: 1);
            if (status == "drained") back.IsDrained = true;
            if (status == "erasure") back.ApplyErasure();
            if (status == "sealed") back.EnterSealbound(3);
            if (status == "invincible") back.AddInvincibleEffect(InvincibleDurationType.Always);
            if (status == "stunned") back.ApplyStun(3);
            if (status == "frozen") back.ApplyFreeze(3);
            Cast(state, PlayerId.Player, PlayerId.AI, anchor.Position);
            Assert.That(back.CurrentHp, Is.EqualTo(expectedHp));
            if (status == "sealed") Assert.That(state.ValuePopupEvents.Any(e => e.RuntimeId == back.RuntimeId), Is.False);
            if (status == "invincible") Assert.That(state.ValuePopupEvents.Single(e => e.RuntimeId == back.RuntimeId).IsInvinciblePrevented, Is.True);
        }

        [TestCase("normal", true)][TestCase("drained", false)][TestCase("erasure", false)]
        public void EndureAndDemonKingReturnRetainSharedDestructionRules(string status, bool revival)
        {
            var state = State(); var guard = Unit(state, PlayerId.AI, hp: 8, endure: true);
            var king = Unit(state, PlayerId.AI, row: 1, hp: 8, cardId: "DemonKing");
            if (status == "drained") king.IsDrained = true;
            if (status == "erasure") king.ApplyErasure();
            Cast(state, PlayerId.Player, PlayerId.AI, guard.Position);
            Assert.That(guard.CurrentHp, Is.EqualTo(1));
            Assert.That(guard.EndureUsed, Is.True);
            Assert.That(king.IsDemonKingRevivalPending, Is.EqualTo(revival));
            Assert.That(state.AIBoard.GetOccupant(king.Position), revival ? Is.SameAs(king) : Is.Null);
        }

        [Test]
        public void SameOccupantReferenceInBothRowsReceivesOnlyOneAreaPacket()
        {
            var state = State(); var target = Building(state, PlayerId.AI);
            state.AIBoard.Place(new TileCoord(0, 1), target);
            Cast(state, PlayerId.Player, PlayerId.AI, new TileCoord(0, 0));
            Assert.That(target.CurrentHp, Is.EqualTo(470));
            Assert.That(state.ValuePopupEvents.Count(e => e.RuntimeId == target.RuntimeId), Is.EqualTo(1));
        }

        [TestCase(PlayerId.Player)][TestCase(PlayerId.AI)]
        public void ManaShortfallUsesExistingOneToOneGoldReplacementOnce(PlayerId caster)
        {
            var state = State(caster); var enemy = state.GetOpponent(caster).Id;
            var target = Unit(state, enemy);
            var card = state.GetPlayer(caster).Hand.Add(FireSpearRules.CardId);
            state.GetPlayer(caster).Resources.Add(new ResourceSet(1, 0, 0, 2));
            Cast(state, caster, enemy, target.Position, runtimeId: card.RuntimeId, fund: false);
            Assert.That(state.GetPlayer(caster).Resources.Mana, Is.Zero);
            Assert.That(state.GetPlayer(caster).Resources.Gold, Is.Zero);
            Assert.That(target.CurrentHp, Is.EqualTo(470));
        }

        [TestCase("empty")][TestCase("hidden")][TestCase("sealed")][TestCase("dead")]
        [TestCase("outside-column")][TestCase("outside-row")][TestCase("owner")]
        [TestCase("no-target")][TestCase("missing-card")][TestCase("resources")]
        [TestCase("opponent-turn")][TestCase("phase")]
        public void RejectedCastDoesNotConsumeAnyResourceHandOrDamage(string failure)
        {
            var state = State(); var target = Unit(state, PlayerId.AI, hiding: failure == "hidden");
            var owner = PlayerId.AI; var coord = target.Position;
            if (failure == "empty") coord = new TileCoord(1, 0);
            if (failure == "dead") target.CurrentHp = 0;
            if (failure == "sealed") target.EnterSealbound(2);
            if (failure == "outside-column") coord = new TileCoord(5, 0);
            if (failure == "outside-row") coord = new TileCoord(0, 2);
            if (failure == "owner") owner = (PlayerId)99;
            if (failure == "opponent-turn") state.RestoreRuntimeState(1, PlayerId.AI, PhaseType.Main);
            if (failure == "phase") state.SetPhase(PhaseType.TurnEnd);
            var card = state.Player.Hand.Add(FireSpearRules.CardId);
            if (failure != "resources") state.Player.Resources.Add(new ResourceSet(3, 0, 0, 0));
            var mana = state.Player.Resources.Mana; var gold = state.Player.Resources.Gold;
            var hp = target.CurrentHp; var discard = state.Player.Discard.Count;
            var service = new SpellService(Provider());
            Assert.Throws<InvalidOperationException>(() =>
            {
                if (failure == "no-target") service.CastScriptedSpell(state, PlayerId.Player, FireSpearRules.CardId, card.RuntimeId);
                else service.CastScriptedSpell(state, PlayerId.Player, FireSpearRules.CardId, owner, coord,
                    failure == "missing-card" ? "wrong-runtime" : card.RuntimeId);
            });
            Assert.That(target.CurrentHp, Is.EqualTo(hp));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.Player.Resources.Mana, Is.EqualTo(mana));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(gold));
            Assert.That(state.Player.Hand.Contains(FireSpearRules.CardId, card.RuntimeId), Is.True);
            Assert.That(state.Player.Discard.Count, Is.EqualTo(discard));
            Assert.That(state.ValuePopupEvents, Is.Empty);
            Assert.That(state.PersistentEffects, Is.Empty);
        }

        [Test]
        public void AllDirectAreaPacketsPrecedeReactorDestructionChain()
        {
            var state = State(); var reactor = Building(state, PlayerId.AI, hp: 8, cardId: "NuclearPowerPlant");
            var rear = Unit(state, PlayerId.AI, row: 1);
            Cast(state, PlayerId.Player, PlayerId.AI, reactor.Position);
            Assert.That(rear.CurrentHp, Is.EqualTo(470));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(303));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(303));
            Assert.That(state.AIBoard.GetOccupant(reactor.Position), Is.Null);
            var packets = state.ValuePopupEvents.ToList();
            Assert.That(packets.Count(e => e.SourceCardId == FireSpearRules.CardId), Is.EqualTo(2));
            Assert.That(packets.FindLastIndex(e => e.SourceCardId == FireSpearRules.CardId),
                Is.LessThan(packets.FindIndex(e => e.Cause == BattleValueChangeCause.NuclearPowerPlant)));
        }

        [Test]
        public void AiChoosesColumnWinAndPlanningLeavesAuthoritativeStateUnchanged()
        {
            var state = State(PlayerId.AI); state.Player.Master.CurrentHp = 15;
            Unit(state, PlayerId.Player, column: 2);
            foreach (var card in state.AI.Hand.Cards.ToArray()) state.AI.Hand.Remove(card.CardId, card.RuntimeId);
            state.AI.Hand.Add(FireSpearRules.CardId);
            state.AI.Resources.Add(new ResourceSet(3, 0, 0, 0));
            state.AI.Master.RemainingAttacksThisTurn = 0;
            var provider = Provider();
            var planner = new AiDecisionService(provider, new TargetingService(), ZeroCardUpgradeLevelProvider.Instance,
                new AiSearchOptions { MaxDepth = 1, MaxMilliseconds = 1000 });
            var before = AiBattleStateCopy.PositionKey(state);
            var command = planner.GetNextCommand(state);
            Assert.That(command, Is.TypeOf<CastScriptedSpellCommand>());
            var cast = (CastScriptedSpellCommand)command;
            Assert.That(cast.CardId, Is.EqualTo(FireSpearRules.CardId));
            Assert.That(cast.HasTarget, Is.True);
            Assert.That(cast.TargetOwnerId, Is.EqualTo(PlayerId.Player));
            Assert.That(cast.TargetCoord.Column, Is.EqualTo(2));
            Assert.That(FireSpearRules.IsLegalTarget(state, PlayerId.AI, cast.TargetOwnerId, cast.TargetCoord), Is.True);
            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(before));
            var flow = new BattleFlowController(new BattleSetupService(), new MulliganService(), new TurnStartService(provider),
                new BattleCommandProcessor(new PlayCardService(provider, new SummonService()), new SpellService(provider),
                    new MoveService(), new AttackService(), new EndTurnService()));
            flow.RestoreBattleState(state);
            flow.ExecuteCommand(PlayerId.AI, command);
            Assert.That(state.Result.Winner, Is.EqualTo(PlayerId.AI));
        }

        [Test]
        public void DefinitionMatchesConfirmedSpecsAndUpgradeReleasePools()
        {
            Assert.That(CardDatabaseValidator.ValidateJson(Json()).Errors, Is.Empty);
            var record = JsonCardDefinitionDatabase.FromJson(Json()).Cards.Single(c => c.Id == FireSpearRules.CardId);
            Assert.That(record.DisplayName, Is.EqualTo("파이어스피어"));
            Assert.That(record.DefinitionType, Is.EqualTo(JsonCardDefinitionKind.ScriptedSpell));
            Assert.That(record.Rarity, Is.EqualTo(CardRarity.Common));
            Assert.That(record.Affiliation, Is.EqualTo(CardAffiliation.Fantasy));
            Assert.That(record.Cost.Mana, Is.EqualTo(3));
            Assert.That(record.Cost.Gold + record.Cost.Qi + record.Cost.Power, Is.Zero);
            Assert.That(record.Damage, Is.EqualTo(30));
            Assert.That(record.DamageType, Is.EqualTo(DamageType.Magic));
            Assert.That(record.EffectId, Is.EqualTo("fire_spear"));
            Assert.That(record.EffectText, Is.EqualTo(FireSpearRules.EffectDescription));
            Assert.That(record.SpecialEffectText, Is.Empty);
            Assert.That(record.TriggerCount, Is.Zero);
            Assert.That(record.ChargeTileFootprint, Is.EqualTo(ChargeTileFootprint.None));
            Assert.That(record.IncludeInDraft && record.IncludeInRewards, Is.True);
            Assert.That(CardUpgradeRules.IsCardUpgradeable(record.ToDefinition()), Is.True);
        }

        [TestCase("cost")][TestCase("damage")][TestCase("damageType")][TestCase("rarity")]
        [TestCase("affiliation")][TestCase("draft")][TestCase("rewards")][TestCase("special")]
        [TestCase("effect")][TestCase("footprint")][TestCase("triggerCount")][TestCase("name")]
        public void ValidatorRejectsIncorrectFireSpearAuthoring(string field)
        {
            var database = JsonCardDefinitionDatabase.FromJson(Json());
            var record = database.Cards.Single(c => c.Id == FireSpearRules.CardId);
            switch (field)
            {
                case "cost": record.Cost.Mana = 2; break;
                case "damage": record.Damage = 14; break;
                case "damageType": record.DamageType = DamageType.Physical; break;
                case "rarity": record.Rarity = CardRarity.Uncommon; break;
                case "affiliation": record.Affiliation = CardAffiliation.Neutral; break;
                case "draft": record.IncludeInDraft = false; break;
                case "rewards": record.IncludeInRewards = false; break;
                case "special": record.SpecialEffectText = "관통"; break;
                case "effect": record.EffectText = "상대 전장 전체 피해"; break;
                case "footprint": record.ChargeTileFootprint = ChargeTileFootprint.OneByOne; break;
                case "triggerCount": record.TriggerCount = 1; break;
                case "name": record.DisplayName = "다른 이름"; break;
            }
            Assert.That(CardDatabaseValidator.Validate(database).Errors, Has.Some.Contains("FireSpear must be"));
        }

        private static IEnumerable<TestCaseData> AllLevels()
        {
            for (var level = 0; level <= 13; level++) yield return new TestCaseData(level);
        }

        [TestCaseSource(nameof(AllLevels))]
        public void AllUpgradeLevelsDisplayMagicDamageAndKeepCostFixed(int level)
        {
            var definition = (ScriptedSpellCardDefinition)Provider().GetRequired(FireSpearRules.CardId);
            Assert.That(CardStatDisplay.TryCreate(definition, level, out var stats, 7), Is.True);
            Assert.That(stats.Attack, Is.EqualTo(30 + level + 7));
            Assert.That(stats.HasHp, Is.False);
            Assert.That(CardStatDisplay.GetEffectText(definition, FireSpearRules.EffectDescription, level),
                Is.EqualTo(FireSpearRules.EffectDescription), "The user's generic effect wording contains no damage number.");
            Assert.That(CardStatDisplay.GetEffectText(definition, "마법 데미지 " + definition.Damage, level),
                Is.EqualTo("마법 데미지 " + (30 + level)));
            Assert.That(definition.Damage, Is.EqualTo(30));
            Assert.That(definition.Cost.Mana, Is.EqualTo(3));
        }
    }
}
