using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class A212Tests
    {
        private static JsonCardDefinitionDatabase Database() => JsonCardDefinitionDatabase.FromJson(
            File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json"));

        [Test]
        public void Definition_HasRequestedStatsAndMetadata()
        {
            var db = Database();
            var record = db.Cards.Single(c => c.Id == "A-212");
            var d = (UnitCardDefinition)db.CreateProvider().GetRequired("A-212");
            Assert.That(record.Rarity, Is.EqualTo(CardRarity.Rare));
            Assert.That(record.Affiliation, Is.EqualTo(CardAffiliation.ScienceCivilization));
            Assert.That(record.IncludeInDraft && record.IncludeInRewards, Is.True);
            Assert.That(d.Attack, Is.EqualTo(6));
            Assert.That(d.Health, Is.EqualTo(22));
            Assert.That(d.Cost.Power, Is.EqualTo(4));
            Assert.That(d.Cost.Mana + d.Cost.Qi + d.Cost.Gold, Is.Zero);
            Assert.That(d.HitsPerAttack, Is.EqualTo(4));
            Assert.That(d.HasRobot, Is.True);
            Assert.That(d.SciencePowerUpkeep, Is.EqualTo(2));
            Assert.That(d.AttackType, Is.EqualTo(AttackType.Melee));
            Assert.That(d.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(d.HasRush || d.HasFlying || d.HasReplicate, Is.False);
            Assert.That(CardUpgradeRules.IsCardUpgradeable(d), Is.True);
            Assert.That(CardDatabaseValidator.ValidateJson(File.ReadAllText(
                "Assets/Project333/Resources/Project333/Data/cards.json")).Errors, Is.Empty);
        }

        [TestCase(0, 0, 0)]
        [TestCase(1, 0, 1)]
        [TestCase(3, 0, 3)]
        [TestCase(6, 0, 6)]
        [TestCase(9, 0, 9)]
        [TestCase(12, 0, 12)]
        [TestCase(13, 1, 12)]
        public void Upgrade_OnlyLevelThirteenGrantsAttack(int level, int atk, int hp)
        {
            var bonus = CardLevelStatRules.CalculateBonus("A-212", level);
            Assert.That(bonus.AttackBonus, Is.EqualTo(atk));
            Assert.That(bonus.HpBonus, Is.EqualTo(hp));
        }

        [TestCase(0, 6, 22)]
        [TestCase(12, 6, 34)]
        [TestCase(13, 7, 34)]
        public void SummonAndAttack_ApplyLevelAndFourSeparatePhysicalHits(int level, int atk, int hp)
        {
            var provider = Database().CreateProvider();
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.Player));
            state.SetPhase(PhaseType.Main);
            state.Player.Resources.Add(new ResourceSet(0, 0, 4, 0));
            state.Player.Hand.Add("A-212");
            var levels = new InMemoryCardUpgradeLevelProvider();
            levels.SetUpgradeLevel(PlayerId.Player, "A-212", level);
            var unit = new PlayCardService(provider, new SummonService(), levels)
                .PlayUnitCard(state, PlayerId.Player, "A-212", new TileCoord(0, 0));
            Assert.That(unit.CurrentHp, Is.EqualTo(hp));
            Assert.That(unit.HasSummoningSickness, Is.True);
            Assert.That(state.Player.Resources.Power, Is.Zero);
            var titles = OccupantSpecialEffectTooltipCatalog.Build(provider.GetRequired("A-212"), unit)
                .Select(t => t.Title).ToArray();
            Assert.That(titles, Does.Contain("4연타"));
            Assert.That(titles, Does.Contain("전력 -2"));
            Assert.That(titles, Does.Not.Contain("로봇"));

            var target = new UnitState("target", "target", PlayerId.AI, new TileCoord(0, 0),
                AttackType.Ranged, 0, 100, true, false, 0, physicalDefense: 2);
            state.AIBoard.Place(target.Position, target);
            unit.HasSummoningSickness = false;
            unit.WasSummonedThisTurn = false;
            unit.RemainingAttacksThisTurn = 1;
            new AttackService().Attack(state, PlayerId.Player, unit.Position, target.Position);
            Assert.That(target.CurrentHp, Is.EqualTo(100 - (atk - 2) * 4));
            Assert.That(unit.RemainingAttacksThisTurn, Is.Zero);
        }
    }
}
