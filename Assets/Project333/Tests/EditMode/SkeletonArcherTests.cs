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
    public sealed class SkeletonArcherTests
    {
        private const string CardId = "SkeletonArcher";
        private static JsonCardDefinitionDatabase Database() => JsonCardDefinitionDatabase.FromJson(File.ReadAllText(
            Path.Combine(Application.dataPath, "Project333/Resources/Project333/Data/cards.json")));

        [Test]
        public void JsonProvider_RegistersConfirmedReleasedUnitWithoutEffectsOrTraits()
        {
            var database = Database();
            var record = database.Cards.Single(card => card.Id == CardId);
            var definition = (UnitCardDefinition)database.CreateProvider().GetRequired(CardId);
            Assert.That(CardDatabaseValidator.Validate(database).Errors, Is.Empty);
            Assert.That(record.DisplayName, Is.EqualTo("스켈레톤아처"));
            Assert.That(record.DefinitionType, Is.EqualTo(JsonCardDefinitionKind.Unit));
            Assert.That(record.Rarity, Is.EqualTo(CardRarity.Common));
            Assert.That(record.Affiliation, Is.EqualTo(CardAffiliation.Fantasy));
            Assert.That(record.ChargeTileFootprint, Is.EqualTo(ChargeTileFootprint.OneByOne));
            Assert.That(record.IncludeInDraft && record.IncludeInRewards, Is.True);
            Assert.That(record.EffectText, Is.Empty); Assert.That(record.SpecialEffectText, Is.Empty);
            Assert.That(definition.Cost.Mana, Is.EqualTo(1));
            Assert.That(definition.Cost.Qi + definition.Cost.Power + definition.Cost.Gold, Is.Zero);
            Assert.That(definition.Attack, Is.EqualTo(10)); Assert.That(definition.Health, Is.EqualTo(3));
            Assert.That(definition.AttackType, Is.EqualTo(AttackType.Ranged));
            Assert.That(definition.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(definition.PhysicalDefense + definition.MagicDefense, Is.Zero);
            Assert.That(definition.Races, Is.EqualTo(new[] { UnitRace.Undead }));
            Assert.That(definition.CanMove, Is.True);
            Assert.That(definition.HasRush || definition.HasReplicate || definition.HasLifeSteal || definition.HasBerserker ||
                definition.HasEndure || definition.HasShielder || definition.HasHiding || definition.HasFlying || definition.HasPiercing, Is.False);
            Assert.That(definition.MaxAttacksPerTurn, Is.EqualTo(1)); Assert.That(definition.HitsPerAttack, Is.EqualTo(1));
            Assert.That(CardUpgradeRules.IsCardUpgradeable(definition), Is.True);
            Assert.That(database.CreateProvider().GetAll().Where(card => card.IncludeInDraft).Select(card => card.CardId), Contains.Item(CardId));
        }

        [TestCase(0, 10, 3)]
        [TestCase(1, 10, 4)]
        [TestCase(2, 10, 5)]
        [TestCase(3, 11, 5)]
        [TestCase(4, 11, 6)]
        [TestCase(5, 11, 7)]
        [TestCase(6, 12, 7)]
        [TestCase(7, 12, 8)]
        [TestCase(8, 12, 9)]
        [TestCase(9, 13, 9)]
        [TestCase(10, 13, 10)]
        [TestCase(11, 13, 11)]
        [TestCase(12, 13, 12)]
        [TestCase(13, 14, 12)]
        public void SharedProviderAndSummon_ApplyEveryConfirmedLevelForBothOwners(int level, int attack, int hp)
        {
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            {
                var unit = Summon(level, owner, out var state);
                var bonus = CardLevelStatRules.CalculateBonus(CardId, level);
                Assert.That(bonus.AttackBonus, Is.EqualTo(attack - 10), "ATK increases only at Lv.3/6/9/13.");
                Assert.That(bonus.HpBonus, Is.EqualTo(hp - 3), "Lv.12 grants HP; Lv.13 grants ATK.");
                Assert.That(unit.Attack, Is.EqualTo(attack)); Assert.That(unit.OriginalAttack, Is.EqualTo(attack));
                Assert.That(unit.MaxHp, Is.EqualTo(hp)); Assert.That(unit.OriginalMaxHp, Is.EqualTo(hp));
                Assert.That(unit.CurrentHp, Is.EqualTo(hp));
                Assert.That(unit.Races, Is.EqualTo(new[] { UnitRace.Undead }));
                Assert.That(unit.AttackType, Is.EqualTo(AttackType.Ranged)); Assert.That(unit.DamageType, Is.EqualTo(DamageType.Physical));
                Assert.That(unit.HasSummoningSickness, Is.True); Assert.That(unit.CombatStatBuffs, Is.Empty);
                Assert.That(state.GetPlayer(owner).Resources.Mana, Is.Zero);
                Assert.That(state.GetPlayer(owner).Hand.Contains(CardId, "archer-selected"), Is.False);
                Assert.That(state.GetPlayer(owner).Hand.Contains(CardId, "archer-reserve"), Is.True);
            }
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void ActualRangedAttack_DamagesMeleeTargetWithoutReceivingACounterattack(PlayerId owner)
        {
            var archer = Summon(13, owner, out var state);
            var enemy = state.GetOpponent(owner).Id;
            var target = new UnitState("melee", "fixture", enemy, new TileCoord(0, 0), AttackType.Melee,
                200, 100, true, false, 0, physicalDefense: 2);
            state.GetBoard(enemy).Place(target.Position, target);
            archer.HasSummoningSickness = false;
            new AttackService().Attack(state, owner, archer.Position, target.Position);
            Assert.That(target.CurrentHp, Is.EqualTo(88), "The ranged Physical packet uses upgraded ATK 14 minus DEF 2.");
            Assert.That(archer.CurrentHp, Is.EqualTo(12), "A ranged attack receives no melee counterattack.");
            Assert.That(archer.RemainingAttacksThisTurn, Is.Zero);
            Assert.That(state.ValuePopupEvents.Single().Cause, Is.EqualTo(BattleValueChangeCause.NormalAttack));
            Assert.That(state.ValuePopupEvents.Single().SourceRuntimeId, Is.EqualTo(archer.RuntimeId));
        }

        [TestCase(0, 10, 3)]
        [TestCase(13, 14, 12)]
        public void Erasure_RemovesOnlyCombatModifiers_PreservingUpgradeRaceAndRangedRules(int level, int attack, int hp)
        {
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            {
                var archer = Summon(level, owner, out var state);
                archer.ApplyCombatStatBuff("fixture-buff", attackBonus: 4, maxHpBonus: 7);
                archer.CurrentHp = hp - 1;
                new ErasureService().Apply(state, archer);
                Assert.That(archer.Attack, Is.EqualTo(attack)); Assert.That(archer.MaxHp, Is.EqualTo(hp));
                Assert.That(archer.CurrentHp, Is.EqualTo(hp - 1)); Assert.That(archer.CombatStatBuffs, Is.Empty);
                Assert.That(archer.OriginalAttack, Is.EqualTo(attack)); Assert.That(archer.OriginalMaxHp, Is.EqualTo(hp));
                Assert.That(archer.HasRace(UnitRace.Undead), Is.True);
                Assert.That(archer.AttackType, Is.EqualTo(AttackType.Ranged)); Assert.That(archer.CanMove, Is.True);
                DamageResolutionRules.ApplyEffectDamage(archer, 1, DamageType.Fixed);
                Assert.That(archer.Attack, Is.EqualTo(attack), "The card has no damage-triggered trait.");
            }
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void AiCopyAndOppositeViewerProjection_PreserveIdentityStatsAndUndeadRace(PlayerId owner)
        {
            var archer = Summon(13, owner, out var state);
            DamageResolutionRules.ApplyEffectDamage(archer, 2, DamageType.Fixed);
            var clone = AiBattleStateCopy.Create(state);
            var copied = clone.GetBoard(owner).GetOccupant(archer.Position);
            Assert.That(copied.RuntimeId, Is.EqualTo(archer.RuntimeId));
            Assert.That(copied.CurrentHp, Is.EqualTo(10)); Assert.That(copied.Attack, Is.EqualTo(14));
            var view = new BattleStateViewFactory().CreateForPlayer(state, "skeleton-archer", state.GetOpponent(owner).Id);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(view);
            var received = (UnitState)projected.AIBoard.GetOccupant(archer.Position);
            Assert.That(received.RuntimeId, Is.EqualTo(archer.RuntimeId)); Assert.That(received.OwnerId, Is.EqualTo(PlayerId.AI));
            Assert.That(received.Races, Is.EqualTo(new[] { UnitRace.Undead }));
            Assert.That(received.Attack, Is.EqualTo(14)); Assert.That(received.CurrentHp, Is.EqualTo(10));
            Assert.That(received.OriginalAttack, Is.EqualTo(14)); Assert.That(received.OriginalMaxHp, Is.EqualTo(12));
            copied.CurrentHp = 1;
            Assert.That(archer.CurrentHp, Is.EqualTo(10));
        }

        [TestCase("cost")]
        [TestCase("race")]
        [TestCase("attackType")]
        [TestCase("damageType")]
        [TestCase("effect")]
        [TestCase("trait")]
        [TestCase("stats")]
        [TestCase("rewards")]
        public void Validator_RejectsDivergenceFromTheConfirmedCard(string change)
        {
            var database = Database(); var record = database.Cards.Single(card => card.Id == CardId);
            switch (change)
            {
                case "cost": record.Cost.Mana = 2; break;
                case "race": record.Races = new[] { UnitRace.Human }; break;
                case "attackType": record.AttackType = AttackType.Melee; break;
                case "damageType": record.DamageType = DamageType.Magic; break;
                case "effect": record.EffectText = "unconfirmed effect"; break;
                case "trait": record.HasRush = true; break;
                case "stats": record.Attack = 11; break;
                case "rewards": record.IncludeInRewards = false; break;
            }
            Assert.That(CardDatabaseValidator.Validate(database).Errors, Has.Some.Contains("SkeletonArcher must"));
        }

        private static UnitState Summon(int level, PlayerId owner, out BattleState state)
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner));
            state.SetPhase(PhaseType.Main);
            state.GetPlayer(owner).Resources.Add(new ResourceSet(1, 0, 0, 0));
            state.GetPlayer(owner).Hand.Add(CardId, "archer-selected", false);
            state.GetPlayer(owner).Hand.Add(CardId, "archer-reserve", false);
            var levels = new InMemoryCardUpgradeLevelProvider(); levels.SetUpgradeLevel(owner, CardId, level);
            return new PlayCardService(Database().CreateProvider(), new SummonService(), levels).PlayUnitCard(
                state, owner, CardId, new TileCoord(0, 0), "archer-selected");
        }
    }
}
