using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class OccupantSpecialEffectTooltipCatalogTests
    {
        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 2)]
        [TestCase(4, 2)]
        [TestCase(5, 3)]
        [TestCase(6, 3)]
        public void GetLeftColumnCount_UsesRequestedDistribution(int total, int expectedLeft)
        {
            Assert.That(
                OccupantSpecialEffectTooltipCatalog.GetLeftColumnCount(total),
                Is.EqualTo(expectedLeft));
        }

        [Test]
        public void Build_ListsCardTraitsAndStatusesInLockedOrderWithoutRobotTooltip()
        {
            var definition = new UnitCardDefinition(
                "tooltip_unit",
                "툴팁 유닛",
                new ResourceSet(0, 0, 1, 0),
                AttackType.Melee,
                10,
                30,
                true,
                true,
                2,
                maxAttacksPerTurn: 2,
                hitsPerAttack: 3,
                hasBerserker: true,
                hasEndure: true,
                hasShielder: true,
                hasLifeSteal: true,
                hasRobot: true,
                hasRush: true,
                hasReplicate: true,
                sealboundOwnerTurnStarts: 2,
                hasHiding: true,
                hasFlying: true,
                spellPower: 4,
                invincibleDuration: InvincibleDurationType.Always,
                hasPiercing: true);
            var occupant = new UnitState(
                "tooltip-runtime",
                definition.CardId,
                PlayerId.Player,
                new TileCoord(0, 0),
                definition.AttackType,
                definition.Attack,
                definition.Health,
                definition.CanMove,
                definition.IsScience,
                definition.SciencePowerUpkeep,
                maxAttacksPerTurn: definition.MaxAttacksPerTurn,
                hitsPerAttack: definition.HitsPerAttack,
                hasBerserker: definition.HasBerserker,
                hasEndure: definition.HasEndure,
                hasShielder: definition.HasShielder,
                hasLifeSteal: definition.HasLifeSteal,
                hasRobot: definition.HasRobot,
                hasRush: definition.HasRush,
                hasHiding: definition.HasHiding,
                hasFlying: definition.HasFlying,
                spellPower: definition.SpellPower,
                hasPiercing: definition.HasPiercing);
            occupant.IsDrained = true;
            occupant.AddInvincibleEffect(InvincibleDurationType.Always);

            var entries = OccupantSpecialEffectTooltipCatalog.Build(definition, occupant);
            var titles = entries.Select(entry => entry.Title).ToArray();

            Assert.That(titles, Is.EqualTo(new[]
            {
                "전력 -2",
                "버서커",
                "3연타",
                "관통",
                "추가 공격 1",
                "불굴",
                "쉴더",
                "흡혈",
                "속공",
                "복제",
                "방전",
                "봉인 2",
                "은신",
                "비행",
                "주문력 +4",
                "무적",
            }));
            Assert.That(titles, Does.Not.Contain("로봇"));
            Assert.That(
                entries.Single(entry => entry.Title == "관통").Description,
                Is.EqualTo("대상을 일반공격 시 같은 열의 반대편 대상에게도 피해를 입힙니다."));
            Assert.That(
                entries.Single(entry => entry.Title == "불굴").Description,
                Is.EqualTo("처음 받는 치명적인 피해를 버티고 HP 1로 생존합니다."));
        }

        [Test]
        public void Build_ShowsErasureOnlyWhileCurrentOccupantHasErasure()
        {
            var occupant = new UnitState(
                "erasure-runtime",
                "plain_unit",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Melee,
                3,
                3,
                true,
                false,
                0);

            Assert.That(
                OccupantSpecialEffectTooltipCatalog.Build(null, occupant).Select(entry => entry.Title),
                Does.Not.Contain("망각"));

            occupant.ApplyErasure();

            Assert.That(
                OccupantSpecialEffectTooltipCatalog.Build(null, occupant).Select(entry => entry.Title),
                Does.Contain("망각"));
        }

        [Test]
        public void Build_DemonKingPendingRevival_ShowsReturnTooltipInsteadOfGenericSealboundTooltip()
        {
            var occupant = new UnitState(
                "demon-king-runtime",
                DemonKingRules.CardId,
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Melee,
                DemonKingRules.BaseAttack,
                DemonKingRules.BaseHealth,
                true,
                false,
                0,
                damageType: DamageType.Magic,
                physicalDefense: DemonKingRules.PhysicalDefense,
                magicDefense: DemonKingRules.MagicDefense);
            Assert.That(occupant.TryEnterDemonKingRevivalSeal(PlayerId.AI, 4), Is.True);
            Assert.That(occupant.ResolveDemonKingRevivalTurnStart(PlayerId.AI, 6), Is.False);

            var entries = OccupantSpecialEffectTooltipCatalog.Build(null, occupant);
            var returnTooltip = entries.Single(entry => entry.Title == "마왕의 귀환");

            Assert.That(returnTooltip.Description, Does.Contain("ATK/HP +33"));
            Assert.That(returnTooltip.Description, Does.Contain("남은 턴 시작: 2"));
            Assert.That(entries.Select(entry => entry.Title), Has.None.StartsWith("봉인 "));
        }

        [Test]
        public void Build_Hero_ShowsGrowthAndRemainingGlobalTurnEnds()
        {
            var occupant = new UnitState(
                "hero-runtime",
                HeroRules.CardId,
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Melee,
                HeroRules.BaseAttack,
                HeroRules.BaseHealth,
                true,
                false,
                0,
                damageType: DamageType.Fixed);
            occupant.AddInvincibleEffect(
                InvincibleDurationType.GlobalTurnEnds,
                HeroRules.InvincibleTurnEnds);

            var entries = OccupantSpecialEffectTooltipCatalog.Build(null, occupant);

            Assert.That(
                entries.Single(entry => entry.Title == "용사의 성장").Description,
                Does.Contain("ATK +13 또는 HP +13"));
            Assert.That(
                entries.Single(entry => entry.Title == "무적").Description,
                Does.Contain("남은 턴 종료: 3"));
        }
    }
}
