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
        public void Build_ListsCardTraitsWithoutAttachedStatuses()
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
                races: new[] { UnitRace.Robot },
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
                races: definition.Races,
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
                "전력소모: 2",
                "방전",
                "버서커",
                "3연타",
                "관통",
                "추가 공격 1",
                "불굴",
                "쉴더",
                "흡혈",
                "속공",
                "복제",
                "봉인 2",
                "은신",
                "비행",
                "주문력 +4",
                "무적",
            }));
            Assert.That(entries.Single(entry => entry.Title == "은신").Description,
                Is.EqualTo("상대의 일반 공격과 단일 대상 마법의 대상이 되지 않으며 전열을 막지 않는다. 은신 상태에서 일반 공격 시 해당 공격의 모든 타격이 치명타가 되고 은신이 해제된다. 피해를 받으면 은신이 해제된다."));
            Assert.That(titles, Does.Not.Contain("로봇"));
            Assert.That(entries.Single(entry => entry.Title == "주문력 +4").Description,
                Is.EqualTo("마법 데미지를 입힐 때, 주문력만큼의 추가 마법 데미지를 준다."));
            Assert.That(
                entries.Single(entry => entry.Title == "3연타").Description,
                Is.EqualTo("공격·반격 시 3번 데미지를 가한다."));
            Assert.That(
                entries.Single(entry => entry.Title == "관통").Description,
                Is.EqualTo("일반공격 시 대상과 같은 열의 모두에게 피해를 입힌다."));
            Assert.That(
                entries.Single(entry => entry.Title == "불굴").Description,
                Is.EqualTo("처음 받는 치명적인 피해를 버티고 HP 1로 생존한다."));
            Assert.That(
                entries.Single(entry => entry.Title == "비행").Description,
                Is.EqualTo("비행이 없는 근접 유닛의 일반 공격 데미지를 절반만 받는다. 전열 차단을 무시하며, 전열 차단을 하지 않는다."));
        }

        [TestCase(false, 1)]
        [TestCase(false, 3)]
        [TestCase(true, 1)]
        [TestCase(true, 3)]
        public void Build_CardDefinition_ListsPowerConsumptionAndDrainedRulesWithoutApplyingAStatus(bool building, int amount)
        {
            CardDefinition definition = building
                ? (CardDefinition)new BuildingCardDefinition("preview_power_building", "건물", new ResourceSet(),
                    false, 0, 10, sciencePowerUpkeep: amount)
                : new UnitCardDefinition("preview_power_unit", "유닛", new ResourceSet(),
                    AttackType.Melee, 1, 10, true, false, amount);

            var entries = OccupantSpecialEffectTooltipCatalog.Build(definition);

            AssertPowerConsumption(entries, amount);
            Assert.That(entries.Select(entry => entry.Title), Is.EqualTo(new[] { $"전력소모: {amount}", "방전" }));
            Assert.That(entries.All(entry => entry.Kind == OccupantTooltipKind.Ability), Is.True,
                "A related rule explanation must not pretend the unplayed card has a debuff.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Build_RuntimePowerConsumption_AlwaysExplainsDrainedAndKeepsItsActualStatusSeparate(bool drained)
        {
            var occupant = new UnitState("power-runtime", "power-unit", PlayerId.Player,
                new TileCoord(0, 0), AttackType.Melee, 3, 10, true, false, 4);
            occupant.IsDrained = drained;

            var abilities = OccupantSpecialEffectTooltipCatalog.Build(null, occupant);
            var attached = OccupantStatusTooltipCatalog.BuildAttached(null, occupant, null);

            AssertPowerConsumption(abilities, 4);
            Assert.That(abilities.Count(entry => entry.Title == "방전"), Is.EqualTo(1));
            Assert.That(attached.Count(entry => entry.Title == "방전"), Is.EqualTo(drained ? 1 : 0));
            Assert.That(attached.All(entry => string.IsNullOrEmpty(entry.Description)), Is.True);
            Assert.That(occupant.IsDrained, Is.EqualTo(drained), "Tooltip projection must not mutate battle truth.");
            Assert.That(occupant.SciencePowerUpkeep, Is.EqualTo(4));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Build_NoPowerConsumption_DoesNotAddRelatedDrainedRules(bool drained)
        {
            var occupant = new UnitState("plain-runtime", "plain", PlayerId.Player,
                new TileCoord(0, 0), AttackType.Melee, 3, 10, true, false, 0);
            occupant.IsDrained = drained;

            var abilities = OccupantSpecialEffectTooltipCatalog.Build(null, occupant);

            Assert.That(abilities.Any(entry => entry.Title.StartsWith("전력소모:") || entry.Title == "방전"), Is.False);
            Assert.That(OccupantStatusTooltipCatalog.BuildAttached(null, occupant, null)
                .Count(entry => entry.Title == "방전"), Is.EqualTo(drained ? 1 : 0));
        }

        [Test]
        public void Build_SuppressedPowerConsumptionStillShowsItsInnateRules()
        {
            var occupant = new BuildingState("erased-building", "power-building", PlayerId.Player,
                new TileCoord(0, 0), false, 0, 10, sciencePowerUpkeep: 2);
            occupant.ApplyErasure();

            AssertPowerConsumption(OccupantSpecialEffectTooltipCatalog.Build(null, occupant), 2);
            Assert.That(occupant.IsErasure, Is.True);
            Assert.That(occupant.IsDrained, Is.False);
        }

        private static void AssertPowerConsumption(
            System.Collections.Generic.IReadOnlyList<OccupantSpecialEffectTooltip> entries, int amount)
        {
            Assert.That(entries.Single(entry => entry.Title == $"전력소모: {amount}").Description,
                Is.EqualTo($"내 턴 시작 시 {amount}의 전력을 지불해야한다. 지불하지 못하면 '방전' 디버프가 부여된다."));
            var drainedRule = entries.Single(entry => entry.Title == "방전");
            Assert.That(drainedRule.Description, Does.Contain("공격·반격·액티브 발동·이동·전열 차단")
                .And.Contain("물리·마법 방어력을 0").And.Contain("3배"));
            Assert.That(drainedRule.Kind, Is.EqualTo(OccupantTooltipKind.Ability));
        }
        [Test]
        public void Build_LeavesErasureToAttachedCatalog()
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
                Does.Not.Contain("망각"));
            Assert.That(OccupantStatusTooltipCatalog.BuildAttached(null, occupant, null).Select(entry => entry.Title), Does.Contain("망각"));
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
            Assert.That(returnTooltip.Description, Does.Not.Contain("남은 턴 시작"));
            Assert.That(OccupantStatusTooltipCatalog.BuildAttached(null, occupant, null).Single(entry => entry.Title == "마왕 부활 대기").Description, Is.Empty);
            Assert.That(entries.Select(entry => entry.Title), Has.None.StartsWith("봉인 "));
        }

        [Test]
        public void Build_Hero_ShowsRushWithoutRemovedSpecialEffects()
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
                damageType: DamageType.Fixed, hasRush: true);

            var entries = OccupantSpecialEffectTooltipCatalog.Build(null, occupant);

            Assert.That(entries.Any(entry => entry.Title == "속공"), Is.True);
            Assert.That(entries.Any(entry => entry.Title == "용사의 성장" || entry.Title == "무적"), Is.False);
        }
    }
}
