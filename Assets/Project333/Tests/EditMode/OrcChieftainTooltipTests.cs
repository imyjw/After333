using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Presentation.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class OrcChieftainTooltipTests
    {
        [Test]
        public void ChieftainSourcesShowOneCombinedKoreanBuffAndRefreshWhenOneSourceLeaves()
        {
            var first = new CombatStatBuffState("OrcChieftain", attackBonus: 10, maxHpBonus: 10, sourceRuntimeId: "chief-a");
            var second = new CombatStatBuffState("OrcChieftain", attackBonus: 10, maxHpBonus: 10, sourceRuntimeId: "chief-b");
            var permanent = new CombatStatBuffState("Hero", attackBonus: 33);
            AssertTitlesAreReadOnly(Snapshot(first, second, permanent),
                "오크 족장 ×2: 공격력 +20, 체력 +20", "용사: 공격력 +33");
            // Viewer snapshots after removing one live source retain only that source's survivor.
            AssertTitlesAreReadOnly(Snapshot(second, permanent),
                "오크 족장: 공격력 +10, 체력 +10", "용사: 공격력 +33");
            AssertTitlesAreReadOnly(Snapshot(permanent), "용사: 공격력 +33");
        }

        private static UnitState Snapshot(params CombatStatBuffState[] records)
        {
            var unit = new UnitState("orc-recipient", "OrcWarrior", PlayerId.Player, new TileCoord(0, 0),
                AttackType.Melee, 25, 40, true, false, 0, races: new[] { UnitRace.Orc });
            foreach (var record in records)
                unit.ApplyCombatStatBuff(record.SourceCardId, record.AttackBonus, record.MaxHpBonus,
                    record.PhysicalDefenseBonus, record.MagicDefenseBonus);
            unit.RestoreCombatStatBuffs(records);
            unit.CurrentHp -= 17; // Ordinary damage must not be misreported as an aura/HP debuff.
            return unit;
        }

        private static void AssertTitlesAreReadOnly(UnitState unit, params string[] expected)
        {
            var beforeStats = new[] { unit.BaseAttack, unit.CurrentHp, unit.MaxHp, unit.PhysicalDefense, unit.MagicDefense };
            var beforeSources = unit.CombatStatBuffs.ToArray();
            var entries = OccupantStatusTooltipCatalog.BuildAttached(null, unit, null);
            Assert.That(entries.Select(entry => entry.Title), Is.EquivalentTo(expected));
            Assert.That(entries.All(entry => entry.Kind == OccupantTooltipKind.Buff && entry.Description == string.Empty), Is.True);
            Assert.That(new[] { unit.BaseAttack, unit.CurrentHp, unit.MaxHp, unit.PhysicalDefense, unit.MagicDefense }, Is.EqualTo(beforeStats));
            Assert.That(unit.CombatStatBuffs, Is.EqualTo(beforeSources));
        }
    }
}
