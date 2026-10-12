using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public sealed class ManaShieldTests
    {
        private static string Json() => File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json");
        private static ICardDefinitionProvider Provider() => JsonCardDefinitionDatabase.FromJson(Json()).CreateProvider();
        private static BattleState State(PlayerId caster = PlayerId.Player)
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, caster));
            state.SetPhase(PhaseType.Main);
            state.Player.Resources.Spend(state.Player.Resources.Clone());
            state.AI.Resources.Spend(state.AI.Resources.Clone());
            return state;
        }
        private static UnitState Unit(BattleState state, PlayerId owner, int column = 0, int row = 0,
            int defense = 3, bool hiding = false, bool shielder = false, int spellPower = 0,
            int attack = 10, int hp = 100, AttackType attackType = AttackType.Melee,
            DamageType damageType = DamageType.Magic, string cardId = "Goblin")
        {
            var unit = new UnitState(Guid.NewGuid().ToString(), cardId, owner, new TileCoord(column, row),
                attackType, attack, hp, true, false, 0, damageType: damageType, magicDefense: defense, physicalDefense: 4,
                hasHiding: hiding, hasShielder: shielder, spellPower: spellPower);
            state.GetBoard(owner).Place(unit.Position, unit); return unit;
        }
        private static BuildingState Building(BattleState state, PlayerId owner, int column = 0, int row = 0)
        {
            var building = new BuildingState(Guid.NewGuid().ToString(), "ManaPond", owner,
                new TileCoord(column, row), false, 0, 100, magicDefense: 3, physicalDefense: 4);
            state.GetBoard(owner).Place(building.Position, building); return building;
        }
        private static void Cast(BattleState state, PlayerId caster, OccupantState target, int level = 0,
            string runtimeId = null!, bool fund = true)
        {
            var levels = new InMemoryCardUpgradeLevelProvider();
            levels.SetUpgradeLevel(caster, ManaShieldRules.CardId, level);
            levels.SetUpgradeLevel(state.GetOpponent(caster).Id, ManaShieldRules.CardId, level == 13 ? 0 : 13);
            var player = state.GetPlayer(caster);
            if (runtimeId == null) runtimeId = player.Hand.Add(ManaShieldRules.CardId).RuntimeId;
            if (fund) player.Resources.Add(new ResourceSet(1, 0, 0, 0));
            new SpellService(Provider(), levels).CastScriptedSpell(state, caster, ManaShieldRules.CardId,
                target.OwnerId, target.Position, runtimeId);
        }
        private static IEnumerable<TestCaseData> CastCases()
        {
            foreach (var caster in new[] { PlayerId.Player, PlayerId.AI })
                foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
                    foreach (var kind in new[] { "unit", "building" })
                        for (var level = 0; level <= 13; level++) yield return new TestCaseData(caster, owner, kind, level);
        }
        [TestCaseSource(nameof(CastCases))]
        public void OldStoredLevelsBothSeatsStillGrantBaseDefenseWithoutAnyDamage(PlayerId caster, PlayerId owner, string kind, int level)
        {
            var state = State(caster);
            OccupantState target = kind == "unit" ? (OccupantState)Unit(state, owner) : Building(state, owner);
            var neighbor = Unit(state, owner, column: 1);
            var opposite = Unit(state, state.GetOpponent(owner).Id);
            Unit(state, caster, column: 4, spellPower: 300);
            var chosen = state.GetPlayer(caster).Hand.AddTemporaryReplicate(ManaShieldRules.CardId);
            var unused = state.GetPlayer(caster).Hand.Add(ManaShieldRules.CardId);
            var discard = state.GetPlayer(caster).Discard.Count;
            Cast(state, caster, target, level, chosen.RuntimeId);
            Assert.That(target.MagicDefense, Is.EqualTo(13));
            Assert.That(target.OriginalMagicDefense, Is.EqualTo(3));
            Assert.That(target.PhysicalDefense, Is.EqualTo(4));
            Assert.That(target.CurrentHp, Is.EqualTo(100));
            Assert.That(target.CombatStatBuffs.Count, Is.EqualTo(1));
            var buff = target.CombatStatBuffs.Single();
            Assert.That(buff.SourceCardId, Is.EqualTo(ManaShieldRules.CardId));
            Assert.That(buff.SourceRuntimeId, Is.Empty);
            Assert.That(buff.MagicDefenseBonus, Is.EqualTo(10));
            Assert.That(buff.AttackBonus + buff.MaxHpBonus + buff.PhysicalDefenseBonus, Is.Zero);
            Assert.That(neighbor.MagicDefense, Is.EqualTo(3));
            Assert.That(opposite.MagicDefense, Is.EqualTo(3));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.GetPlayer(caster).Resources.Mana, Is.Zero);
            Assert.That(state.GetPlayer(caster).Hand.Contains(ManaShieldRules.CardId, chosen.RuntimeId), Is.False);
            Assert.That(state.GetPlayer(caster).Hand.Contains(ManaShieldRules.CardId, unused.RuntimeId), Is.True);
            Assert.That(state.GetPlayer(caster).Discard.Count, Is.EqualTo(discard + 1));
            Assert.That(state.PersistentEffects, Is.Empty);
            Assert.That(state.ValuePopupEvents, Is.Empty);
        }
        [TestCase("empty")][TestCase("dead")][TestCase("master")][TestCase("hidden")][TestCase("sealed")]
        [TestCase("outside-column")][TestCase("outside-row")][TestCase("owner")][TestCase("no-target")]
        [TestCase("missing-card")][TestCase("resources")][TestCase("opponent-turn")][TestCase("phase")]
        public void InvalidCastLeavesDefenseResourcesHandAndEventsUnchanged(string failure)
        {
            var state = State(); var target = Unit(state, PlayerId.AI, hiding: failure == "hidden");
            OccupantState requested = failure == "master" ? state.AI.Master : target;
            if (failure == "dead") target.CurrentHp = 0;
            if (failure == "sealed") target.EnterSealbound(2);
            var coord = requested.Position;
            if (failure == "empty") coord = new TileCoord(1, 0);
            if (failure == "outside-column") coord = new TileCoord(5, 0);
            if (failure == "outside-row") coord = new TileCoord(0, 2);
            if (failure == "opponent-turn") state.RestoreRuntimeState(1, PlayerId.AI, PhaseType.Main);
            if (failure == "phase") state.SetPhase(PhaseType.TurnEnd);
            var chosen = state.Player.Hand.Add(ManaShieldRules.CardId);
            if (failure != "resources") state.Player.Resources.Add(new ResourceSet(1, 0, 0, 0));
            var before = AiBattleStateCopy.PositionKey(state);
            var service = new SpellService(Provider());
            Assert.Throws<InvalidOperationException>(() =>
            {
                if (failure == "no-target") service.CastScriptedSpell(state, PlayerId.Player, ManaShieldRules.CardId, chosen.RuntimeId);
                else service.CastScriptedSpell(state, PlayerId.Player, ManaShieldRules.CardId,
                    failure == "owner" ? (PlayerId)99 : PlayerId.AI, coord,
                    failure == "missing-card" ? "invalid-runtime" : chosen.RuntimeId);
            });
            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(before));
            Assert.That(state.Player.Hand.Contains(ManaShieldRules.CardId, chosen.RuntimeId), Is.True);
            Assert.That(target.CombatStatBuffs, Is.Empty);
            Assert.That(state.ValuePopupEvents, Is.Empty);
        }
        [Test]
        public void FriendlyHidingAndRearBehindShielderReceiveBuffWithoutRevealingOrRedirecting()
        {
            var state = State(); var guard = Unit(state, PlayerId.Player, shielder: true);
            var target = Unit(state, PlayerId.Player, row: 1, hiding: true);
            Cast(state, PlayerId.Player, target);
            Assert.That(target.MagicDefense, Is.EqualTo(13));
            Assert.That(guard.MagicDefense, Is.EqualTo(3));
            Assert.That(target.IsHiding, Is.True);
        }
        [Test]
        public void PermanentStacksSurviveBothTurnEndsAndReduceOnlyMagicDamage()
        {
            var state = State(); var target = Unit(state, PlayerId.Player);
            Cast(state, PlayerId.Player, target, 0); Cast(state, PlayerId.Player, target, 13);
            Assert.That(target.MagicDefense, Is.EqualTo(23));
            Assert.That(target.CombatStatBuffs.Single().MagicDefenseBonus, Is.EqualTo(20));
            Assert.That(DamageResolutionRules.ResolveIncomingDamage(target, 40, DamageType.Magic), Is.EqualTo(17));
            Assert.That(DamageResolutionRules.ResolveIncomingDamage(target, 40, DamageType.Physical), Is.EqualTo(36));
            Assert.That(DamageResolutionRules.ResolveIncomingDamage(target, 40, DamageType.Fixed), Is.EqualTo(40));
            new EndTurnService().EndTurn(state); state.SetPhase(PhaseType.Main);
            new EndTurnService().EndTurn(state);
            Assert.That(target.MagicDefense, Is.EqualTo(23));
            Assert.That(target.CombatStatBuffs.Single().MagicDefenseBonus, Is.EqualTo(20));
        }
        [Test]
        public void DrainedStoresShieldButSuppressesArmorUntilRestoredErasureRemovesExistingAndAcceptsNew()
        {
            var state = State(); var target = Unit(state, PlayerId.Player); target.IsDrained = true;
            Cast(state, PlayerId.Player, target);
            Assert.That(target.MagicDefense, Is.EqualTo(13)); Assert.That(target.EffectiveMagicDefense, Is.Zero);
            Assert.That(DamageResolutionRules.ResolveIncomingDamage(target, 40, DamageType.Magic), Is.EqualTo(120));
            target.IsDrained = false;
            Assert.That(DamageResolutionRules.ResolveIncomingDamage(target, 40, DamageType.Magic), Is.EqualTo(27));
            target.ApplyErasure(); Assert.That(target.MagicDefense, Is.EqualTo(3)); Assert.That(target.CombatStatBuffs, Is.Empty);
            Cast(state, PlayerId.Player, target, 3);
            Assert.That(target.IsErasure, Is.True); Assert.That(target.MagicDefense, Is.EqualTo(13));
            Assert.That(target.CombatStatBuffs.Single().MagicDefenseBonus, Is.EqualTo(10));
        }
        [Test]
        public void ExistingShieldPersistsAcrossSealCloneAndGuOwnershipTransfer()
        {
            var state = State(); var target = Unit(state, PlayerId.AI);
            Cast(state, PlayerId.Player, target, 4); target.EnterSealbound(1);
            var copy = AiBattleStateCopy.Create(state).AIBoard.GetOccupant(target.Position);
            Assert.That(copy.RuntimeId, Is.EqualTo(target.RuntimeId));
            Assert.That(copy.MagicDefense, Is.EqualTo(13));
            Assert.That(copy.CombatStatBuffs.Single().MagicDefenseBonus, Is.EqualTo(10));
            Assert.That(copy.CombatStatBuffs.Single(), Is.Not.SameAs(target.CombatStatBuffs.Single()));
            target.ResolveSealboundOwnerTurnStart();
            state.Player.Hand.Add(GuRules.CardId); state.Player.Resources.Add(new ResourceSet(0, 10, 0, 0));
            new SpellService(Provider()).CastScriptedSpell(state, PlayerId.Player, GuRules.CardId, PlayerId.AI, target.Position);
            Assert.That(target.OwnerId, Is.EqualTo(PlayerId.Player));
            Assert.That(target.MagicDefense, Is.EqualTo(13));
            Assert.That(target.CombatStatBuffs.Single().SourceCardId, Is.EqualTo(ManaShieldRules.CardId));
        }
        [Test]
        public void GoldPaysManaDeficitOnlyAndResourcesBelongToCaster()
        {
            var state = State(); var target = Unit(state, PlayerId.AI); var chosen = state.Player.Hand.Add(ManaShieldRules.CardId);
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 1)); state.AI.Resources.Add(new ResourceSet(9, 0, 0, 9));
            Cast(state, PlayerId.Player, target, runtimeId: chosen.RuntimeId, fund: false);
            Assert.That(state.Player.Resources.Gold + state.Player.Resources.Mana, Is.Zero);
            Assert.That(state.AI.Resources.Mana, Is.EqualTo(9)); Assert.That(state.AI.Resources.Gold, Is.EqualTo(9));
        }
        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)][TestCase(6)]
        [TestCase(7)][TestCase(8)][TestCase(9)][TestCase(10)][TestCase(11)][TestCase(12)][TestCase(13)]
        public void AllLevelsDisplayDefenseDescriptionAndNeverDamageBadge(int level)
        {
            var definition = (ScriptedSpellCardDefinition)Provider().GetRequired(ManaShieldRules.CardId);
            Assert.That(CardStatDisplay.TryCreate(definition, level, out _, 500), Is.False);
            Assert.That(CardStatDisplay.TryGetSpellDamage(definition, level, 500, out _), Is.False);
            Assert.That(CardStatDisplay.GetEffectText(definition, ManaShieldRules.EffectDescription, level),
                Does.Contain("영구적으로 10 증가"));
            Assert.That(definition.EffectAmount, Is.EqualTo(10)); Assert.That(definition.Cost.Mana, Is.EqualTo(1));
        }
        [Test]
        public void CanonicalSpecsAndOldSpellMagnitudeDefaultsRemainCompatible()
        {
            var db = JsonCardDefinitionDatabase.FromJson(Json());
            Assert.That(CardDatabaseValidator.Validate(db).Errors, Is.Empty);
            var record = db.Cards.Single(c => c.Id == ManaShieldRules.CardId);
            Assert.That(record.Affiliation, Is.EqualTo(CardAffiliation.Fantasy));
            Assert.That(record.Rarity, Is.EqualTo(CardRarity.Common));
            Assert.That(record.IncludeInDraft, Is.True);
            Assert.That(record.IncludeInRewards, Is.False);
            Assert.That(record.ChargeTileFootprint, Is.EqualTo(ChargeTileFootprint.None));
            Assert.That(record.Cost.Power + record.Cost.Qi + record.Cost.Gold, Is.Zero);
            Assert.That(record.EffectAmount, Is.EqualTo(10)); Assert.That(record.Damage, Is.Zero);
            Assert.That(record.DamageType, Is.EqualTo(DamageType.None));
            Assert.That(CardUpgradeRules.IsCardUpgradeable(record.ToDefinition()), Is.False);
            Assert.That(db.Cards.Where(c => c.Id != ManaShieldRules.CardId && c.Id != PlasmaShieldRules.CardId).All(c => c.EffectAmount == 0), Is.True);
            Assert.That(new ScriptedSpellCardDefinition("legacy", "Legacy", new ResourceSet(), "test").EffectAmount, Is.Zero);
        }
        [TestCase("amount")][TestCase("negative")][TestCase("damage")][TestCase("damageType")]
        [TestCase("cost")][TestCase("affiliation")][TestCase("rarity")][TestCase("draft")][TestCase("rewards")]
        public void ValidatorRejectsWrongShieldSpecs(string field)
        {
            var db = JsonCardDefinitionDatabase.FromJson(Json()); var c = db.Cards.Single(x => x.Id == ManaShieldRules.CardId);
            switch (field)
            {
                case "amount": c.EffectAmount = 9; break; case "negative": c.EffectAmount = -1; break;
                case "damage": c.Damage = 10; break; case "damageType": c.DamageType = DamageType.Magic; break;
                case "cost": c.Cost.Mana = 2; break; case "affiliation": c.Affiliation = CardAffiliation.ScienceCivilization; break;
                case "rarity": c.Rarity = CardRarity.Rare; break; case "draft": c.IncludeInDraft = false; break;
                case "rewards": c.IncludeInRewards = true; break;
            }
            Assert.That(CardDatabaseValidator.Validate(db).Errors, Has.Some.Contains("ManaShield must be"));
        }
        private static double Score(BattleState state)
        {
            var type = typeof(AiDecisionService).Assembly.GetType("Project333.Runtime.Application.Services.AiPositionEvaluator")!;
            return (double)type.GetMethod("Evaluate")!.Invoke(Activator.CreateInstance(type, true), new object[] { state })!;
        }
        [TestCase(PlayerId.AI, DamageType.Magic, true)][TestCase(PlayerId.Player, DamageType.Magic, true)]
        [TestCase(PlayerId.AI, DamageType.Physical, false)][TestCase(PlayerId.AI, DamageType.Fixed, false)]
        public void AiValuesOnlyRealMagicPreventionWithCorrectOwnershipSign(PlayerId owner, DamageType type, bool improves)
        {
            var state = State(PlayerId.AI); var recipient = Unit(state, owner, defense: 0, attackType: AttackType.Ranged);
            Unit(state, state.GetOpponent(owner).Id, column: 1, attack: 10, attackType: AttackType.Ranged, damageType: type);
            if (!improves) state.GetOpponent(owner).Master.ApplyStun(13);
            var before = Score(state); recipient.ApplyCombatStatBuff(ManaShieldRules.CardId, magicDefenseBonus: 10);
            var delta = Score(state) - before;
            if (improves) Assert.That(delta * (owner == PlayerId.AI ? 1 : -1), Is.GreaterThan(0));
            else Assert.That(delta, Is.EqualTo(0).Within(0.0001));
        }
        [TestCase("blocked")][TestCase("drained")][TestCase("sealed")][TestCase("hidden-enemy")][TestCase("invincible")]
        public void AiDoesNotRewardArmorWithoutApplicableThreatAndRedundantStacks(string reason)
        {
            var state = State(PlayerId.AI); var target = Unit(state, PlayerId.AI, defense: reason == "blocked" ? 50 : 0);
            if (reason == "hidden-enemy") state.Player.Master.ApplyStun(13);
            Unit(state, PlayerId.Player, column: 1, attack: 10, attackType: AttackType.Ranged, hiding: reason == "hidden-enemy");
            if (reason == "drained") target.IsDrained = true;
            if (reason == "sealed") target.EnterSealbound(2);
            if (reason == "invincible") target.AddInvincibleEffect(InvincibleDurationType.Always);
            var before = Score(state); target.ApplyCombatStatBuff(ManaShieldRules.CardId, magicDefenseBonus: 10);
            Assert.That(Score(state), Is.EqualTo(before).Within(0.0001));
        }
        [Test]
        public void RedundantSecondShieldHasZeroMarginalValueAndAiKeepsTheCard()
        {
            var state = State(PlayerId.AI); var target = Unit(state, PlayerId.AI, defense: 0, attack: 30, attackType: AttackType.Ranged);
            target.RemainingAttacksThisTurn = 0; state.AI.Master.RemainingAttacksThisTurn = 0;
            Unit(state, PlayerId.Player, column: 1, attack: 10, attackType: AttackType.Ranged);
            Unit(state, PlayerId.Player, column: 3, attack: 10, attackType: AttackType.Ranged);
            target.ApplyCombatStatBuff(ManaShieldRules.CardId, magicDefenseBonus: 10);
            var before = Score(state);
            var copy = AiBattleStateCopy.Create(state); copy.AIBoard.GetOccupant(target.Position)
                .ApplyCombatStatBuff(ManaShieldRules.CardId, magicDefenseBonus: 10);
            Assert.That(Score(copy), Is.EqualTo(before).Within(0.0001));
            foreach (var card in state.AI.Hand.Cards.ToArray()) state.AI.Hand.Remove(card.CardId, card.RuntimeId);
            var saved = state.AI.Hand.Add(ManaShieldRules.CardId); state.AI.Resources.Add(new ResourceSet(1, 0, 0, 0));
            var planner = new AiDecisionService(Provider(), new TargetingService(), ZeroCardUpgradeLevelProvider.Instance,
                new AiSearchOptions { MaxDepth = 1, MaxMilliseconds = 1000 });
            Assert.That(planner.GetNextCommand(state), Is.TypeOf<EndTurnCommand>());
            Assert.That(state.AI.Hand.Contains(ManaShieldRules.CardId, saved.RuntimeId), Is.True);
            Assert.That(state.AI.Resources.Mana, Is.EqualTo(1));
        }
        [Test]
        public void IntrinsicDefenseWithoutShieldLeavesOldAiScoreUnchanged()
        {
            var state = State(PlayerId.AI); var target = Unit(state, PlayerId.AI, defense: 0);
            Unit(state, PlayerId.Player, column: 1, attack: 20, attackType: AttackType.Ranged);
            var before = Score(state); target.IncreaseMagicDefense(10);
            Assert.That(Score(state), Is.EqualTo(before).Within(0.0001));
        }
        [Test]
        public void MixedShieldSourcesStackSeparatelyAndErasureClearsBoth()
        {
            var state = State(); var target = Unit(state, PlayerId.Player);
            var plasma = state.Player.Hand.Add(PlasmaShieldRules.CardId);
            state.Player.Resources.Add(new ResourceSet(0, 0, 1, 0));
            new SpellService(Provider()).CastScriptedSpell(state, PlayerId.Player, PlasmaShieldRules.CardId,
                PlayerId.Player, target.Position, plasma.RuntimeId);
            Cast(state, PlayerId.Player, target, 2); Cast(state, PlayerId.Player, target, 13);
            Assert.That(target.PhysicalDefense, Is.EqualTo(14));
            Assert.That(target.MagicDefense, Is.EqualTo(23));
            Assert.That(target.CombatStatBuffs.Count, Is.EqualTo(2));
            Assert.That(target.CombatStatBuffs.Single(x => x.SourceCardId == PlasmaShieldRules.CardId).PhysicalDefenseBonus, Is.EqualTo(10));
            Assert.That(target.CombatStatBuffs.Single(x => x.SourceCardId == ManaShieldRules.CardId).MagicDefenseBonus, Is.EqualTo(20));
            Assert.That(DamageResolutionRules.ResolveIncomingDamage(target, 40, DamageType.Physical), Is.EqualTo(26));
            Assert.That(DamageResolutionRules.ResolveIncomingDamage(target, 40, DamageType.Magic), Is.EqualTo(17));
            target.ApplyErasure();
            Assert.That(target.PhysicalDefense, Is.EqualTo(4)); Assert.That(target.MagicDefense, Is.EqualTo(3));
            Assert.That(target.CombatStatBuffs, Is.Empty);
        }
        [Test]
        public void AiCacheDistinguishesAttributedManaShieldFromSameUnattributedDefense()
        {
            var state = State(PlayerId.AI); var target = Unit(state, PlayerId.AI, defense: 0, attackType: AttackType.Ranged);
            Unit(state, PlayerId.Player, column: 1, attack: 10, attackType: AttackType.Ranged);
            var other = AiBattleStateCopy.Create(state);
            target.ApplyCombatStatBuff(ManaShieldRules.CardId, magicDefenseBonus: 10);
            other.AIBoard.GetOccupant(target.Position).IncreaseMagicDefense(10);
            Assert.That(other.AIBoard.GetOccupant(target.Position).MagicDefense, Is.EqualTo(target.MagicDefense));
            Assert.That(AiBattleStateCopy.PositionKey(other), Is.Not.EqualTo(AiBattleStateCopy.PositionKey(state)));
            Assert.That(Score(state), Is.GreaterThan(Score(other)));
        }
        [Test]
        public void AiMagicPreventionUsesCurrentSpellPowerAndCannotRewardUnpaidCannonAttack()
        {
            var state = State(PlayerId.AI); var target = Unit(state, PlayerId.AI, defense: 0, attackType: AttackType.Ranged);
            Unit(state, PlayerId.Player, column: 1, attack: 0, attackType: AttackType.Ranged,
                cardId: ManaCannonRules.CardId, spellPower: 0);
            // A real magic cannon threat exists only when its current owner can pay.
            var cannon = state.PlayerBoard.GetOccupant(new TileCoord(1, 0));
            cannon.IncreaseBaseAttack(10);
            var type = typeof(AiDecisionService).Assembly.GetType("Project333.Runtime.Application.Services.AiPositionEvaluator")!;
            var evaluator = Activator.CreateInstance(type, true)!;
            var evaluate = type.GetMethod("ManaShieldProtectionValue", BindingFlags.Instance | BindingFlags.NonPublic)!;
            target.ApplyCombatStatBuff(ManaShieldRules.CardId, magicDefenseBonus: 10);
            Assert.That((double)evaluate.Invoke(evaluator, new object[] { state, target })!, Is.Zero);
            state.Player.Resources.Add(new ResourceSet(1, 0, 0, 0));
            Assert.That((double)evaluate.Invoke(evaluator, new object[] { state, target })!, Is.EqualTo(8.5).Within(0.0001));
            // With armor above printed attack, live allied SpellPower still creates preventable damage.
            target.IncreaseMagicDefense(10);
            Assert.That((double)evaluate.Invoke(evaluator, new object[] { state, target })!, Is.Zero);
            Unit(state, PlayerId.Player, column: 3, attack: 0, spellPower: 10);
            Assert.That((double)evaluate.Invoke(evaluator, new object[] { state, target })!, Is.EqualTo(8.5).Within(0.0001));
        }
        [Test]
        public void AiSpendsShieldOnExposedFriendlyAndPlanningDoesNotMutateTruth()
        {
            var state = State(PlayerId.AI); var target = Unit(state, PlayerId.AI, defense: 0, attack: 30, attackType: AttackType.Ranged);
            target.RemainingAttacksThisTurn = 0; state.AI.Master.RemainingAttacksThisTurn = 0;
            Unit(state, PlayerId.Player, column: 1, attack: 10, attackType: AttackType.Ranged);
            Unit(state, PlayerId.Player, column: 3, attack: 10, attackType: AttackType.Ranged);
            foreach (var card in state.AI.Hand.Cards.ToArray()) state.AI.Hand.Remove(card.CardId, card.RuntimeId);
            state.AI.Hand.Add(ManaShieldRules.CardId); state.AI.Resources.Add(new ResourceSet(1, 0, 0, 0));
            var before = AiBattleStateCopy.PositionKey(state);
            var planner = new AiDecisionService(Provider(), new TargetingService(), ZeroCardUpgradeLevelProvider.Instance,
                new AiSearchOptions { MaxDepth = 1, MaxMilliseconds = 1000 });
            var chosen = planner.GetNextCommand(state);
            Assert.That(chosen, Is.TypeOf<CastScriptedSpellCommand>());
            var cast = (CastScriptedSpellCommand)chosen;
            Assert.That(cast.CardId, Is.EqualTo(ManaShieldRules.CardId));
            Assert.That(cast.TargetOwnerId, Is.EqualTo(PlayerId.AI)); Assert.That(cast.TargetCoord, Is.EqualTo(target.Position));
            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(before));
        }
    }
}