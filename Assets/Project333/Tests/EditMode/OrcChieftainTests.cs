using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class OrcChieftainTests
    {
        private static JsonCardDefinitionDatabase Database() => JsonCardDefinitionDatabase.FromJson(File.ReadAllText(
            Path.Combine(Application.dataPath, "Project333/Resources/Project333/Data/cards.json")));

        [Test]
        public void RegisteredDefinition_MatchesTheConfirmedUnitWithoutExtraTraits()
        {
            var database = Database();
            var record = database.Cards.Single(card => card.Id == OrcChieftainRules.CardId);
            var definition = (UnitCardDefinition)database.CreateProvider().GetRequired(record.Id);
            Assert.That(CardDatabaseValidator.Validate(database).Errors, Is.Empty);
            Assert.That(record.DisplayName, Is.EqualTo("오크 족장"));
            Assert.That(record.Rarity, Is.EqualTo(CardRarity.Rare));
            Assert.That(record.Affiliation, Is.EqualTo(CardAffiliation.Fantasy));
            Assert.That(record.IncludeInDraft && record.IncludeInRewards, Is.True);
            Assert.That(record.SpecialEffectText, Is.Empty);
            Assert.That(record.Races, Is.EqualTo(new[] { UnitRace.Orc }));
            Assert.That(definition.Cost.Mana, Is.EqualTo(7));
            Assert.That(definition.Cost.Qi + definition.Cost.Power + definition.Cost.Gold, Is.Zero);
            Assert.That(definition.Attack, Is.EqualTo(50));
            Assert.That(definition.Health, Is.EqualTo(70));
            Assert.That(definition.AttackType, Is.EqualTo(AttackType.Melee));
            Assert.That(definition.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(definition.PhysicalDefense + definition.MagicDefense, Is.Zero);
            Assert.That(definition.CanMove, Is.True);
            Assert.That(definition.HasBerserker || definition.HasRush || definition.HasReplicate || definition.HasEndure ||
                definition.HasShielder || definition.HasLifeSteal || definition.HasFlying || definition.HasHiding || definition.HasPiercing, Is.False);
            Assert.That(CardUpgradeRules.IsCardUpgradeable(definition), Is.True);
        }

        [TestCase(0, 50, 70)]
        [TestCase(1, 50, 71)]
        [TestCase(2, 50, 72)]
        [TestCase(3, 51, 72)]
        [TestCase(4, 51, 73)]
        [TestCase(5, 51, 74)]
        [TestCase(6, 52, 74)]
        [TestCase(7, 52, 75)]
        [TestCase(8, 52, 76)]
        [TestCase(9, 53, 76)]
        [TestCase(10, 53, 77)]
        [TestCase(11, 53, 78)]
        [TestCase(12, 53, 79)]
        [TestCase(13, 54, 79)]
        public void SharedProviderSummon_AllLevelsAndBothSeats_SeparateAccountBaselineFromSelfAura(int level, int attack, int hp)
        {
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            {
                var state = State(owner);
                var player = state.GetPlayer(owner);
                player.Resources.Add(new ResourceSet(7, 0, 0, 0));
                player.Hand.Add(OrcChieftainRules.CardId, "chief-selected", false);
                player.Hand.Add(OrcChieftainRules.CardId, "chief-reserve", false);
                var upgrades = new InMemoryCardUpgradeLevelProvider();
                upgrades.SetUpgradeLevel(owner, OrcChieftainRules.CardId, level);
                var chief = new PlayCardService(Database().CreateProvider(), new SummonService(), upgrades).PlayUnitCard(
                    state, owner, OrcChieftainRules.CardId, new TileCoord(0, 0), "chief-selected");
                Assert.That(chief.OriginalAttack, Is.EqualTo(attack));
                Assert.That(chief.OriginalMaxHp, Is.EqualTo(hp));
                AssertStats(chief, attack + 10, hp + 10, hp + 10);
                Assert.That(chief.HasSummoningSickness, Is.True);
                Assert.That(chief.CombatStatBuffs.Single().SourceRuntimeId, Is.EqualTo(chief.RuntimeId));
                Assert.That(chief.CombatStatBuffs.Single().AttackBonus, Is.EqualTo(10));
                Assert.That(player.Resources.Mana, Is.Zero);
                Assert.That(player.Hand.Cards.Any(card => card.RuntimeId == "chief-selected"), Is.False);
                Assert.That(player.Hand.Cards.Any(card => card.RuntimeId == "chief-reserve"), Is.True);
            }
        }

        [Test]
        public void CurrentAndFutureAlliedOrcs_StackByLiveSource_IncludingBothChieftains()
        {
            var state = State();
            var board = state.PlayerBoard;
            var orc = Orc("existing", 0); board.Place(orc.Position, orc);
            var first = Chief("first", 3); board.Place(first.Position, first);
            var second = Chief("second", 4); board.Place(second.Position, second);
            var mixedRace = Orc("future", 1, races: new[] { UnitRace.Orc, UnitRace.Robot }); board.Place(mixedRace.Position, mixedRace);
            var human = Orc("human", 2, races: new[] { UnitRace.Human }); board.Place(human.Position, human);
            var enemy = Orc("enemy", 0, PlayerId.AI); state.AIBoard.Place(enemy.Position, enemy);
            var building = new BuildingState("building", "fixture", PlayerId.Player, new TileCoord(0, 1), false, 0, 30);
            board.Place(building.Position, building);
            AssertStats(orc, 40, 50, 50); AssertStats(mixedRace, 40, 50, 50);
            AssertStats(first, 70, 90, 90); AssertStats(second, 70, 90, 90);
            AssertStats(human, 20, 30, 30); AssertStats(enemy, 20, 30, 30);
            AssertStats(building, 0, 30, 30); AssertStats(state.Player.Master, 3, 333, 333);
            Assert.That(orc.CombatStatBuffs.Select(buff => buff.SourceRuntimeId), Is.EquivalentTo(new[] { "first", "second" }));
            orc.ApplyCombatStatBuff("Hero", attackBonus: 3, maxHpBonus: 4);
            DamageResolutionRules.ApplyEffectDamage(orc, 15, DamageType.Fixed);
            board.Remove(first.Position);
            AssertStats(orc, 33, 44, 29);
            Assert.That(orc.CombatStatBuffs.Select(buff => buff.SourceCardId), Is.EquivalentTo(new[] { "Hero", OrcChieftainRules.CardId }));
            Assert.That(orc.CombatStatBuffs.Single(buff => buff.SourceCardId == OrcChieftainRules.CardId).SourceRuntimeId, Is.EqualTo("second"));
        }

        [Test]
        public void RepeatedRefreshMoveAndSwap_DoNotHealDamageOrDuplicateSourceRecords()
        {
            var state = State(); var board = state.PlayerBoard;
            var chief = Chief("chief", 4); board.Place(chief.Position, chief);
            var orc = Orc("orc", 0); board.Place(orc.Position, orc);
            var other = Orc("other", 1); board.Place(other.Position, other);
            DamageResolutionRules.ApplyEffectDamage(orc, 17, DamageType.Fixed);
            board.Move(orc.Position, new TileCoord(0, 1)); board.Swap(orc.Position, other.Position);
            for (var i = 0; i < 8; i++) board.RefreshOrcChieftainAuras();
            AssertStats(orc, 30, 40, 23);
            Assert.That(orc.CombatStatBuffs.Count, Is.EqualTo(1));
            Assert.That(orc.CombatStatBuffs.Single().SourceRuntimeId, Is.EqualTo("chief"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LeavingOrExplicitDestruction_WithdrawsHpExactly_WithoutDamagePrevention(bool explicitDestroy)
        {
            var state = State(); var board = state.PlayerBoard;
            var chief = Chief("chief", 4); board.Place(chief.Position, chief);
            var orc = Orc("protected", 0, endure: true); board.Place(orc.Position, orc);
            orc.AddInvincibleEffect(InvincibleDurationType.Always);
            orc.CurrentHp = 8;
            if (explicitDestroy) OccupantDestructionService.DestroyOccupant(state, PlayerId.Player, chief);
            else { board.Remove(chief.Position); OccupantDestructionService.ResolveDefeatedOccupants(state); }
            Assert.That(board.GetOccupant(orc.Position), Is.Null);
            AssertStats(orc, 20, 30, -2);
            Assert.That(orc.EndureUsed, Is.False, "Withdrawing a stat bonus is not a damage packet.");
            Assert.That(state.ValuePopupEvents, Is.Empty);
            Assert.That(chief.CombatStatBuffs, Is.Empty);
        }

        [Test]
        public void HpWithdrawal_ResolvesEarlierTilesAndSecondChieftainDeathsToAFixedPoint()
        {
            var state = State(); var board = state.PlayerBoard;
            var orc = Orc("early", 0); board.Place(orc.Position, orc);
            var second = Chief("second", 2); board.Place(second.Position, second);
            var first = Chief("first", 4); board.Place(first.Position, first);
            orc.CurrentHp = 15; second.CurrentHp = 8; first.CurrentHp = 0;
            OccupantDestructionService.ResolveDefeatedOccupants(state);
            Assert.That(board.EnumerateOccupants().Where(occupant => occupant.Kind != OccupantKind.Master), Is.Empty);
            Assert.That(orc.CurrentHp, Is.EqualTo(-5));
            Assert.That(state.IsEnded, Is.False);
        }

        [Test]
        public void ReactorExplosion_WithdrawsKilledChieftainAura_BeforeOutcomeAndSubsequentActions()
        {
            var state = State(); var board = state.PlayerBoard;
            var orc = Orc("armored", 0, physicalDefense: 40); board.Place(orc.Position, orc);
            var chief = Chief("chief", 4); board.Place(chief.Position, chief);
            orc.CurrentHp = 8; chief.CurrentHp = 25;
            var reactor = new BuildingState("reactor", NuclearPowerPlantRules.CardId, PlayerId.AI, new TileCoord(0, 0), false, 0, 30);
            state.AIBoard.Place(reactor.Position, reactor); reactor.CurrentHp = 0;
            OccupantDestructionService.ResolveDefeatedOccupants(state);
            Assert.That(board.GetOccupant(chief.Position), Is.Null);
            Assert.That(board.GetOccupant(orc.Position), Is.Null);
            Assert.That(orc.CurrentHp, Is.EqualTo(-2));
            Assert.That(state.ValuePopupEvents.Any(popup => popup.RuntimeId == orc.RuntimeId), Is.False,
                "The armored Orc took no blast damage; only its expired +10 HP caused defeat.");
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(303));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(303));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DrainedSource_StopsAndResumesEmission_WhileDrainedRecipientsFollowOrdinaryBuffRules(bool targetDrained)
        {
            var state = State(); var board = state.PlayerBoard;
            var chief = Chief("chief", 4); board.Place(chief.Position, chief);
            var orc = Orc("orc", 0); orc.IsDrained = targetDrained; board.Place(orc.Position, orc);
            AssertStats(orc, 30, 40, 40);
            chief.IsDrained = true;
            AssertStats(orc, 20, 30, 30); AssertStats(chief, 50, 70, 70);
            chief.IsDrained = false;
            AssertStats(orc, 30, 40, 40); AssertStats(chief, 60, 80, 80);
            Assert.That(orc.IsDrained, Is.EqualTo(targetDrained));
        }

        [Test]
        public void ErasedSource_StopsItsAura_ButReceivesANewChieftainsBuffNormally()
        {
            var state = State(); var board = state.PlayerBoard;
            var first = Chief("first", 4); board.Place(first.Position, first);
            var orc = Orc("orc", 0); board.Place(orc.Position, orc);
            new ErasureService().Apply(state, first);
            AssertStats(first, 50, 70, 70); AssertStats(orc, 20, 30, 30);
            var second = Chief("second", 3); board.Place(second.Position, second);
            AssertStats(first, 60, 80, 80); AssertStats(orc, 30, 40, 40);
            Assert.That(first.CombatStatBuffs.Single().SourceRuntimeId, Is.EqualTo("second"));
        }

        [Test]
        public void RecipientErasure_UsesExistingHpClamp_BlocksOldSourcesAndAcceptsOnlyNewSources()
        {
            var state = State(); var board = state.PlayerBoard;
            var first = Chief("first", 4); board.Place(first.Position, first);
            var orc = Orc("orc", 0); board.Place(orc.Position, orc);
            DamageResolutionRules.ApplyEffectDamage(orc, 15, DamageType.Fixed);
            new ErasureService().Apply(state, orc);
            AssertStats(orc, 20, 30, 25);
            for (var i = 0; i < 3; i++) board.RefreshOrcChieftainAuras();
            AssertStats(orc, 20, 30, 25);
            Assert.That(orc.ErasedOrcChieftainSourceRuntimeIds, Is.EquivalentTo(new[] { "first" }));
            var second = Chief("second", 3); board.Place(second.Position, second);
            AssertStats(orc, 30, 40, 35);
            new ErasureService().Apply(state, orc);
            AssertStats(orc, 20, 30, 30);
            Assert.That(orc.ErasedOrcChieftainSourceRuntimeIds, Is.EquivalentTo(new[] { "first", "second" }));
            board.Remove(first.Position); board.Remove(second.Position);
            AssertStats(orc, 20, 30, 30);
        }

        [Test]
        public void SealedRecipient_KeepsExistingLiveSource_LosesExpiredSource_AndReceivesNewSourceAfterRelease()
        {
            var state = State(); var board = state.PlayerBoard;
            var first = Chief("first", 4); board.Place(first.Position, first);
            var orc = Orc("orc", 0); board.Place(orc.Position, orc); orc.EnterSealbound(1);
            var second = Chief("second", 3); board.Place(second.Position, second);
            AssertStats(orc, 30, 40, 40);
            Assert.That(orc.CombatStatBuffs.Single().SourceRuntimeId, Is.EqualTo("first"));
            new ErasureService().Apply(state, orc);
            Assert.That(orc.IsErasure, Is.False);
            board.Remove(first.Position); AssertStats(orc, 20, 30, 30);
            Assert.That(orc.ResolveSealboundOwnerTurnStart(), Is.True);
            AssertStats(orc, 30, 40, 40);
            Assert.That(orc.CombatStatBuffs.Single().SourceRuntimeId, Is.EqualTo("second"));
        }

        [Test]
        public void SealedChieftain_StopsOnlyItsOwnEmission_AndRetainsOtherExistingSources()
        {
            var state = State(); var board = state.PlayerBoard;
            var first = Chief("first", 4); board.Place(first.Position, first);
            var second = Chief("second", 3); board.Place(second.Position, second);
            first.EnterSealbound(1);
            AssertStats(first, 60, 80, 80); AssertStats(second, 60, 80, 80);
            Assert.That(first.CombatStatBuffs.Single().SourceRuntimeId, Is.EqualTo("second"));
            Assert.That(first.ResolveSealboundOwnerTurnStart(), Is.True);
            AssertStats(first, 70, 90, 90); AssertStats(second, 70, 90, 90);
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void GuLowHpChieftain_AtomicallyReplacesOtherSources_PreservesSelfSourceAndTriggersOldAllyDefeat(PlayerId caster)
        {
            var state = State(caster); var enemy = state.GetOpponent(caster).Id;
            var oldBoard = state.GetBoard(enemy); var newBoard = state.GetBoard(caster);
            var target = Chief("controlled", 4, enemy); oldBoard.Place(target.Position, target);
            var oldChief = Chief("old-source", 3, enemy); oldBoard.Place(oldChief.Position, oldChief);
            var oldAlly = Orc("old-ally", 0, enemy); oldBoard.Place(oldAlly.Position, oldAlly);
            var newChief = Chief("new-source", 3, caster); newBoard.Place(newChief.Position, newChief);
            var newAlly = Orc("new-ally", 1, caster); newBoard.Place(newAlly.Position, newAlly);
            target.CurrentHp = 5; oldAlly.CurrentHp = 8;
            CastGu(state, caster, enemy, target.Position);
            Assert.That(target.OwnerId, Is.EqualTo(caster));
            Assert.That(target.CurrentHp, Is.EqualTo(5), "Removing and adding different equal contributions is one net delta.");
            AssertStats(target, 70, 90, 5);
            Assert.That(target.CombatStatBuffs.Select(buff => buff.SourceRuntimeId), Is.EquivalentTo(new[] { "controlled", "new-source" }));
            Assert.That(oldBoard.GetOccupant(oldAlly.Position), Is.Null);
            Assert.That(oldAlly.CurrentHp, Is.EqualTo(-12), "The defeated ally also leaves its remaining old chief's aura.");
            AssertStats(newAlly, 40, 50, 50);
            Assert.That(newBoard.GetOccupant(target.Position), Is.SameAs(target));
            Assert.That(target.HasSummoningSickness, Is.True);
            Assert.That(target.RemainingAttacksThisTurn, Is.Zero);
            Assert.That(state.ValuePopupEvents, Is.Empty);
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void GuLowHpRecipient_ReplacesEqualContributionsWithoutTransientDeathOrHealing(PlayerId caster)
        {
            var state = State(caster); var enemy = state.GetOpponent(caster).Id;
            var oldBoard = state.GetBoard(enemy); var newBoard = state.GetBoard(caster);
            var oldChief = Chief("old", 4, enemy); oldBoard.Place(oldChief.Position, oldChief);
            var target = Orc("recipient", 0, enemy); oldBoard.Place(target.Position, target); target.CurrentHp = 5;
            var newChief = Chief("new", 4, caster); newBoard.Place(newChief.Position, newChief);
            CastGu(state, caster, enemy, target.Position);
            AssertStats(target, 30, 40, 5);
            Assert.That(newBoard.GetOccupant(target.Position), Is.SameAs(target));
            Assert.That(target.CombatStatBuffs.Single().SourceRuntimeId, Is.EqualTo("new"));
        }

        [Test]
        public void AiClone_PreservesSourceIdentityErasureHistoryAndDamagedStats_WithIndependentMutations()
        {
            var state = State(); var board = state.PlayerBoard;
            var first = Chief("first", 4); board.Place(first.Position, first);
            var orc = Orc("orc", 0); board.Place(orc.Position, orc);
            DamageResolutionRules.ApplyEffectDamage(orc, 15, DamageType.Fixed);
            new ErasureService().Apply(state, orc);
            var second = Chief("second", 3); board.Place(second.Position, second);
            var clone = AiBattleStateCopy.Create(state);
            var clonedOrc = clone.PlayerBoard.GetOccupant(orc.Position);
            AssertStats(clonedOrc, 30, 40, 35);
            Assert.That(clonedOrc.ErasedOrcChieftainSourceRuntimeIds, Is.EquivalentTo(new[] { "first" }));
            Assert.That(clonedOrc.CombatStatBuffs.Single().SourceRuntimeId, Is.EqualTo("second"));
            var key = AiBattleStateCopy.PositionKey(clone);
            clonedOrc.RestoreErasedOrcChieftainSources(new[] { "first", "another-old-source" });
            Assert.That(AiBattleStateCopy.PositionKey(clone), Is.Not.EqualTo(key));
            Assert.That(orc.ErasedOrcChieftainSourceRuntimeIds, Is.EquivalentTo(new[] { "first" }));
            clone.PlayerBoard.Remove(second.Position);
            AssertStats(clonedOrc, 20, 30, 25); AssertStats(orc, 30, 40, 35);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BulkRestorePlacement_AnySourceOrder_ReconcilesWithoutReapplyingHp(bool reverse)
        {
            var state = State(); var board = state.PlayerBoard;
            var orc = Orc("orc", 0); board.Place(orc.Position, orc);
            var first = Chief("first", 4); board.Place(first.Position, first);
            var second = Chief("second", 3); board.Place(second.Position, second);
            DamageResolutionRules.ApplyEffectDamage(orc, 37, DamageType.Fixed);
            var restored = new BoardState();
            var originals = board.EnumerateOccupants().ToArray();
            foreach (var original in reverse ? originals.Reverse() : originals)
                restored.Place(original.Position, original.CloneDetached(), synchronizeAuras: false);
            restored.RefreshOrcChieftainAuras(); restored.RefreshOrcChieftainAuras();
            AssertStats(restored.GetOccupant(orc.Position), 40, 50, 13);
            Assert.That(restored.GetOccupant(orc.Position).CombatStatBuffs.Count, Is.EqualTo(2));
        }

        [Test]
        public void LethalMeleeExchange_CapturesBothAttacks_BeforeAnySourceHpWithdrawal()
        {
            var state = State();
            var chief = Chief("attacker", 0); state.PlayerBoard.Place(chief.Position, chief); chief.CurrentHp = 30;
            var enemyChief = Chief("enemy-chief", 4, PlayerId.AI); state.AIBoard.Place(enemyChief.Position, enemyChief);
            var defender = Orc("defender", 0, PlayerId.AI, berserker: true, attack: 30, hp: 50);
            state.AIBoard.Place(defender.Position, defender); defender.CurrentHp = 25;
            chief.HasSummoningSickness = false; defender.HasSummoningSickness = false;
            Assert.That(chief.Attack, Is.EqualTo(60)); Assert.That(defender.Attack, Is.EqualTo(75));
            new AttackService().Attack(state, PlayerId.Player, chief.Position, defender.Position);
            Assert.That(chief.CurrentHp, Is.EqualTo(-55));
            Assert.That(defender.CurrentHp, Is.EqualTo(-45));
            Assert.That(state.PlayerBoard.GetOccupant(chief.Position), Is.Null);
            Assert.That(state.AIBoard.GetOccupant(defender.Position), Is.Null);
            Assert.That(state.ValuePopupEvents.Select(popup => popup.Cause),
                Is.EquivalentTo(new[] { BattleValueChangeCause.NormalAttack, BattleValueChangeCause.Counterattack }));
        }

        [TestCase("cost")]
        [TestCase("race")]
        [TestCase("stats")]
        [TestCase("trait")]
        [TestCase("draft")]
        [TestCase("rewards")]
        public void StrictValidator_RejectsCardContractDivergence(string change)
        {
            var database = Database(); var record = database.Cards.Single(card => card.Id == OrcChieftainRules.CardId);
            switch (change)
            {
                case "cost": record.Cost.Mana = 6; break;
                case "race": record.Races = new[] { UnitRace.Human }; break;
                case "stats": record.Attack = 51; break;
                case "trait": record.HasBerserker = true; break;
                case "draft": record.IncludeInDraft = false; break;
                case "rewards": record.IncludeInRewards = false; break;
            }
            Assert.That(CardDatabaseValidator.Validate(database).Errors, Has.Some.Contains("OrcChieftain must"));
        }

        private static BattleState State(PlayerId first = PlayerId.Player)
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, first));
            state.SetPhase(PhaseType.Main);
            return state;
        }

        private static UnitState Chief(string runtimeId, int column, PlayerId owner = PlayerId.Player) =>
            new UnitState(runtimeId, OrcChieftainRules.CardId, owner, new TileCoord(column, 0), AttackType.Melee,
                OrcChieftainRules.BaseAttack, OrcChieftainRules.BaseHealth, true, false, 0, races: new[] { UnitRace.Orc });

        private static UnitState Orc(string runtimeId, int column, PlayerId owner = PlayerId.Player,
            UnitRace[] races = null, bool endure = false, int physicalDefense = 0, bool berserker = false, int attack = 20, int hp = 30) =>
            new UnitState(runtimeId, "fixture-orc", owner, new TileCoord(column, 0), AttackType.Melee, attack, hp, true, false, 0,
                hasBerserker: berserker, hasEndure: endure, physicalDefense: physicalDefense, races: races ?? new[] { UnitRace.Orc });

        private static void AssertStats(OccupantState occupant, int attack, int maxHp, int currentHp)
        {
            Assert.That(occupant.Attack, Is.EqualTo(attack), occupant.RuntimeId + " ATK");
            Assert.That(occupant.MaxHp, Is.EqualTo(maxHp), occupant.RuntimeId + " max HP");
            Assert.That(occupant.CurrentHp, Is.EqualTo(currentHp), occupant.RuntimeId + " current HP");
        }

        private static void CastGu(BattleState state, PlayerId caster, PlayerId enemy, TileCoord target)
        {
            state.GetPlayer(caster).Resources.Add(new ResourceSet(0, 10, 0, 0));
            state.GetPlayer(caster).Hand.Add(GuRules.CardId, "gu-selected", false);
            new SpellService(Database().CreateProvider()).CastScriptedSpell(state, caster, GuRules.CardId, enemy, target, "gu-selected");
        }
    }
}
