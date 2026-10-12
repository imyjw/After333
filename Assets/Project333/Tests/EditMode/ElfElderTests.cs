using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class ElfElderTests
    {
        private const string CardId = "ElfElder";
        private static JsonCardDefinitionDatabase Database() => JsonCardDefinitionDatabase.FromJson(File.ReadAllText(
            Path.Combine(Application.dataPath, "Project333/Resources/Project333/Data/cards.json")));

        [Test]
        public void JsonProvider_UsesCanonicalIdAndConfirmedTwoTraits()
        {
            var database = Database(); var provider = database.CreateProvider();
            var record = database.Cards.Single(card => card.Id == CardId);
            var definition = (UnitCardDefinition)provider.GetRequired(CardId);
            Assert.That(CardDatabaseValidator.Validate(database).Errors, Is.Empty);
            Assert.That(definition.CardId, Is.EqualTo("ElfElder"));
            Assert.That(provider.GetAll().Select(card => card.CardId), Does.Not.Contain("Elf Elder"));
            Assert.That(record.DisplayName, Is.EqualTo("엘프 장로"));
            Assert.That(record.DefinitionType, Is.EqualTo(JsonCardDefinitionKind.Unit));
            Assert.That(record.Rarity, Is.EqualTo(CardRarity.Rare)); Assert.That(record.Affiliation, Is.EqualTo(CardAffiliation.Fantasy));
            Assert.That(record.ChargeTileFootprint, Is.EqualTo(ChargeTileFootprint.OneByOne));
            Assert.That(record.IncludeInDraft && record.IncludeInRewards, Is.True);
            Assert.That(record.EffectText, Is.Empty); Assert.That(record.SpecialEffectText, Is.EqualTo("관통\n속공"));
            Assert.That(definition.Cost.Mana, Is.EqualTo(5));
            Assert.That(definition.Cost.Qi + definition.Cost.Power + definition.Cost.Gold, Is.Zero);
            Assert.That(definition.Attack, Is.EqualTo(30)); Assert.That(definition.Health, Is.EqualTo(25));
            Assert.That(definition.AttackType, Is.EqualTo(AttackType.Ranged)); Assert.That(definition.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(definition.PhysicalDefense + definition.MagicDefense, Is.Zero);
            Assert.That(definition.Races, Is.EqualTo(new[] { UnitRace.Elf })); Assert.That(definition.CanMove, Is.True);
            Assert.That(definition.HasRush && definition.HasPiercing, Is.True);
            Assert.That(definition.HasReplicate || definition.HasLifeSteal || definition.HasBerserker || definition.HasEndure ||
                definition.HasShielder || definition.HasHiding || definition.HasFlying, Is.False);
            Assert.That(definition.MaxAttacksPerTurn, Is.EqualTo(1)); Assert.That(definition.HitsPerAttack, Is.EqualTo(1));
            Assert.That(CardUpgradeRules.IsCardUpgradeable(definition), Is.True);
            Assert.That(provider.GetAll().Where(card => card.IncludeInDraft).Select(card => card.CardId), Contains.Item(CardId));
        }

        [TestCase(0, 30, 25)]
        [TestCase(1, 30, 26)]
        [TestCase(2, 30, 27)]
        [TestCase(3, 31, 27)]
        [TestCase(4, 31, 28)]
        [TestCase(5, 31, 29)]
        [TestCase(6, 32, 29)]
        [TestCase(7, 32, 30)]
        [TestCase(8, 32, 31)]
        [TestCase(9, 33, 31)]
        [TestCase(10, 33, 32)]
        [TestCase(11, 33, 33)]
        [TestCase(12, 33, 34)]
        [TestCase(13, 34, 34)]
        public void SharedProviderAndSummon_ApplyEveryConfirmedLevelAndRushForBothOwners(int level, int attack, int hp)
        {
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            {
                var unit = Summon(level, owner, out var state);
                var bonus = CardLevelStatRules.CalculateBonus(CardId, level);
                Assert.That(bonus.AttackBonus, Is.EqualTo(attack - 30)); Assert.That(bonus.HpBonus, Is.EqualTo(hp - 25));
                Assert.That(unit.Attack, Is.EqualTo(attack)); Assert.That(unit.OriginalAttack, Is.EqualTo(attack));
                Assert.That(unit.MaxHp, Is.EqualTo(hp)); Assert.That(unit.OriginalMaxHp, Is.EqualTo(hp)); Assert.That(unit.CurrentHp, Is.EqualTo(hp));
                Assert.That(unit.Races, Is.EqualTo(new[] { UnitRace.Elf })); Assert.That(unit.HasActivePiercing, Is.True);
                Assert.That(unit.HasRush && unit.WasSummonedThisTurn, Is.True); Assert.That(unit.HasSummoningSickness, Is.False);
                Assert.That(unit.RemainingAttacksThisTurn, Is.EqualTo(1)); Assert.That(unit.CombatStatBuffs, Is.Empty);
                Assert.That(state.GetPlayer(owner).Resources.Mana, Is.Zero);
                Assert.That(state.GetPlayer(owner).Hand.Contains(CardId, "elder-selected"), Is.False);
                Assert.That(state.GetPlayer(owner).Hand.Contains(CardId, "elder-reserve"), Is.True);
                PlaceVictims(state, state.GetOpponent(owner).Id, out var front, out var rear);
                new AttackService().Attack(state, owner, unit.Position, front.Position);
                Assert.That(front.CurrentHp, Is.EqualTo(100 - (attack - 2)));
                Assert.That(rear.CurrentHp, Is.EqualTo(100 - (attack - 5)));
                Assert.That(unit.CurrentHp, Is.EqualTo(hp), "The summon-turn ranged attack and Piercing must not receive a counter.");
                Assert.That(unit.RemainingAttacksThisTurn, Is.Zero);
                Assert.That(state.ValuePopupEvents.Count, Is.EqualTo(2));
            }
        }

        [TestCase(PlayerId.Player, 0)]
        [TestCase(PlayerId.Player, 1)]
        [TestCase(PlayerId.AI, 0)]
        [TestCase(PlayerId.AI, 1)]
        public void ImmediateRangedAttack_FromEitherRow_AppliesSeparatePhysicalPacketsWithoutCounter(PlayerId owner, int targetRow)
        {
            var elder = Summon(13, owner, out var state);
            PlaceVictims(state, state.GetOpponent(owner).Id, out var front, out var rear);
            var direct = targetRow == 0 ? front : rear; var pierced = targetRow == 0 ? rear : front;
            new AttackService().Attack(state, owner, elder.Position, direct.Position);
            Assert.That(front.CurrentHp, Is.EqualTo(68)); Assert.That(rear.CurrentHp, Is.EqualTo(71));
            Assert.That(elder.CurrentHp, Is.EqualTo(34)); Assert.That(elder.RemainingAttacksThisTurn, Is.Zero);
            Assert.That(state.ValuePopupEvents.Count, Is.EqualTo(2));
            var hit = state.ValuePopupEvents.Single(value => value.Cause == BattleValueChangeCause.NormalAttack);
            var piercing = state.ValuePopupEvents.Single(value => value.Cause == BattleValueChangeCause.Piercing);
            Assert.That(hit.RuntimeId, Is.EqualTo(direct.RuntimeId)); Assert.That(piercing.RuntimeId, Is.EqualTo(pierced.RuntimeId));
            Assert.That(hit.SourceRuntimeId, Is.EqualTo(elder.RuntimeId)); Assert.That(piercing.SourceRuntimeId, Is.EqualTo(elder.RuntimeId));
            Assert.That(hit.Amount, Is.EqualTo(34 - direct.PhysicalDefense));
            Assert.That(piercing.Amount, Is.EqualTo(34 - pierced.PhysicalDefense));
        }

        [TestCase(0, 30, 25)]
        [TestCase(13, 34, 34)]
        public void Erasure_SuppressesRushAndPiercing_PreservingUpgradeRaceAndDamagedHp(int level, int attack, int hp)
        {
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            {
                var elder = Summon(level, owner, out var state);
                elder.ApplyCombatStatBuff("fixture-buff", attackBonus: 4, maxHpBonus: 7); elder.CurrentHp = hp - 1;
                new ErasureService().Apply(state, elder);
                Assert.That(elder.Attack, Is.EqualTo(attack)); Assert.That(elder.MaxHp, Is.EqualTo(hp));
                Assert.That(elder.CurrentHp, Is.EqualTo(hp - 1)); Assert.That(elder.CombatStatBuffs, Is.Empty);
                Assert.That(elder.OriginalAttack, Is.EqualTo(attack)); Assert.That(elder.OriginalMaxHp, Is.EqualTo(hp));
                Assert.That(elder.HasRush && elder.HasPiercing, Is.True, "Innate traits remain authored, but Erasure suppresses them.");
                Assert.That(elder.HasActivePiercing, Is.False); Assert.That(elder.HasSummoningSickness, Is.True);
                Assert.That(elder.HasRace(UnitRace.Elf) && elder.CanMove, Is.True);
                PlaceVictims(state, state.GetOpponent(owner).Id, out var front, out var rear);
                Assert.Throws<InvalidOperationException>(() => new AttackService().Attack(state, owner, elder.Position, front.Position));
                Assert.That(front.CurrentHp, Is.EqualTo(100)); Assert.That(rear.CurrentHp, Is.EqualTo(100));
                elder.WasSummonedThisTurn = false; elder.HasSummoningSickness = false;
                new AttackService().Attack(state, owner, elder.Position, front.Position);
                Assert.That(front.CurrentHp, Is.EqualTo(100 - (attack - 2))); Assert.That(rear.CurrentHp, Is.EqualTo(100));
                Assert.That(state.ValuePopupEvents.Single().Cause, Is.EqualTo(BattleValueChangeCause.NormalAttack));
                Assert.That(elder.CurrentHp, Is.EqualTo(hp - 1));
            }
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void AiCopyAndOppositeViewerProjection_PreserveSpacedIdUpgradeAndTraits(PlayerId owner)
        {
            var elder = Summon(13, owner, out var state); DamageResolutionRules.ApplyEffectDamage(elder, 2, DamageType.Fixed);
            var clone = AiBattleStateCopy.Create(state); var copied = clone.GetBoard(owner).GetOccupant(elder.Position);
            Assert.That(copied.RuntimeId, Is.EqualTo(elder.RuntimeId)); Assert.That(copied.CardId, Is.EqualTo(CardId));
            Assert.That(copied.CurrentHp, Is.EqualTo(32)); Assert.That(copied.Attack, Is.EqualTo(34));
            Assert.That(copied.HasRush && copied.HasActivePiercing, Is.True); Assert.That(copied.HasSummoningSickness, Is.False);
            var view = new BattleStateViewFactory().CreateForPlayer(state, "elf-elder", state.GetOpponent(owner).Id);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(view);
            var received = (UnitState)projected.AIBoard.GetOccupant(elder.Position);
            Assert.That(received.RuntimeId, Is.EqualTo(elder.RuntimeId)); Assert.That(received.OwnerId, Is.EqualTo(PlayerId.AI));
            Assert.That(received.CardId, Is.EqualTo(CardId)); Assert.That(received.Races, Is.EqualTo(new[] { UnitRace.Elf }));
            Assert.That(received.Attack, Is.EqualTo(34)); Assert.That(received.CurrentHp, Is.EqualTo(32));
            Assert.That(received.OriginalAttack, Is.EqualTo(34)); Assert.That(received.OriginalMaxHp, Is.EqualTo(34));
            Assert.That(received.HasRush && received.HasActivePiercing, Is.True); Assert.That(received.HasSummoningSickness, Is.False);
            copied.CurrentHp = 1; Assert.That(elder.CurrentHp, Is.EqualTo(32));
        }

        [TestCase("id")]
        [TestCase("cost")]
        [TestCase("race")]
        [TestCase("attackType")]
        [TestCase("damageType")]
        [TestCase("effect")]
        [TestCase("extraTrait")]
        [TestCase("missingRush")]
        [TestCase("missingPiercing")]
        [TestCase("traitText")]
        [TestCase("stats")]
        [TestCase("rewards")]
        public void Validator_RejectsDivergenceFromTheConfirmedCard(string change)
        {
            var database = Database(); var record = database.Cards.Single(card => card.Id == CardId);
            switch (change)
            {
                case "id": record.Id = "Elf Elder"; break;
                case "cost": record.Cost.Mana = 6; break;
                case "race": record.Races = new[] { UnitRace.Human }; break;
                case "attackType": record.AttackType = AttackType.Melee; break;
                case "damageType": record.DamageType = DamageType.Magic; break;
                case "effect": record.EffectText = "unconfirmed effect"; break;
                case "extraTrait": record.HasFlying = true; break;
                case "missingRush": record.HasRush = false; break;
                case "missingPiercing": record.HasPiercing = false; break;
                case "traitText": record.SpecialEffectText = "관통"; break;
                case "stats": record.Attack = 31; break;
                case "rewards": record.IncludeInRewards = false; break;
            }
            Assert.That(CardDatabaseValidator.Validate(database).Errors, Has.Some.Contains("ElfElder must"));
        }

        private static UnitState Summon(int level, PlayerId owner, out BattleState state)
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner)); state.SetPhase(PhaseType.Main);
            state.GetPlayer(owner).Resources.Add(new ResourceSet(5, 0, 0, 0));
            state.GetPlayer(owner).Hand.Add(CardId, "elder-selected", false); state.GetPlayer(owner).Hand.Add(CardId, "elder-reserve", false);
            var levels = new InMemoryCardUpgradeLevelProvider(); levels.SetUpgradeLevel(owner, CardId, level);
            return new PlayCardService(Database().CreateProvider(), new SummonService(), levels).PlayUnitCard(
                state, owner, CardId, new TileCoord(0, 0), "elder-selected");
        }

        private static void PlaceVictims(BattleState state, PlayerId owner, out UnitState front, out UnitState rear)
        {
            front = new UnitState("elder-front", "fixture", owner, new TileCoord(1, 0), AttackType.Melee, 200, 100, true, false, 0, physicalDefense: 2);
            rear = new UnitState("elder-rear", "fixture", owner, new TileCoord(1, 1), AttackType.Melee, 200, 100, true, false, 0, physicalDefense: 5);
            state.GetBoard(owner).Place(front.Position, front); state.GetBoard(owner).Place(rear.Position, rear);
        }
    }
}
