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
                hasGuard: true,
                hasLifeSteal: true,
                hasRobot: true,
                hasRush: true,
                hasReplicate: true,
                sealboundOwnerTurnStarts: 2,
                hasHiding: true,
                hasFlying: true,
                spellPower: 4,
                invincibleDuration: InvincibleDurationType.Always);
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
                hasGuard: definition.HasGuard,
                hasLifeSteal: definition.HasLifeSteal,
                hasRobot: definition.HasRobot,
                hasRush: definition.HasRush,
                hasHiding: definition.HasHiding,
                hasFlying: definition.HasFlying,
                spellPower: definition.SpellPower);
            occupant.IsDrained = true;
            occupant.AddInvincibleEffect(InvincibleDurationType.Always);

            var entries = OccupantSpecialEffectTooltipCatalog.Build(definition, occupant);
            var titles = entries.Select(entry => entry.Title).ToArray();

            Assert.That(titles, Is.EqualTo(new[]
            {
                "전력 -2",
                "버서커",
                "3연타",
                "추가 공격 1",
                "불굴",
                "가드",
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
    }
}
