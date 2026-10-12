using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Presentation.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class StunFreezeTooltipTests
    {
        [TestCase(BattleCombatLogEntryType.StunApplied, "상대방의 고블린이 스턴 상태가 됨")]
        [TestCase(BattleCombatLogEntryType.StunCleared, "상대방의 고블린 스턴 해제")]
        [TestCase(BattleCombatLogEntryType.FreezeApplied, "상대방의 고블린이 빙결 상태가 됨")]
        [TestCase(BattleCombatLogEntryType.FreezeCleared, "상대방의 고블린 빙결 해제")]
        public void History_UsesKoreanStatusNames(BattleCombatLogEntryType type, string expected)
        {
            var entry = new Project333.Runtime.Application.Online.BattleCombatLogEntryDto
            {
                EntryType = type, TargetOwnerId = PlayerId.AI, TargetCardId = "Goblin"
            };
            Assert.That(BattleCombatLogEntryFormatter.Format(entry, id => id == "Goblin" ? "고블린" : id),
                Is.EqualTo(expected));
        }

        private static UnitState Unit() => new UnitState("status-tooltip-unit", "Goblin",
            PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 10, 100,
            true, false, 0, hasBerserker: true, hasShielder: true, spellPower: 20);

        [TestCase(true, "스턴")]
        [TestCase(false, "빙결")]
        public void Attached_UsesDebuffNameWithoutDurationOrDescription(bool stun, string title)
        {
            var unit = Unit();
            if (stun) unit.ApplyStun(3); else unit.ApplyFreeze(3);
            var entries = OccupantStatusTooltipCatalog.BuildAttached(null, unit, null);
            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].Title, Is.EqualTo(title));
            Assert.That(entries[0].Description, Is.Empty);
            Assert.That(entries[0].Kind, Is.EqualTo(OccupantTooltipKind.Debuff));
        }

        [Test]
        public void Attached_ListsBothStatesAndRemovesOnlyExpiredOne()
        {
            var unit = Unit(); unit.ApplyStun(1); unit.ApplyFreeze(2);
            Assert.That(OccupantStatusTooltipCatalog.BuildAttached(null, unit, null)
                .Select(e => e.Title), Is.EquivalentTo(new[] { "스턴", "빙결" }));
            unit.ResolveActionDebuffsTurnEnd();
            Assert.That(OccupantStatusTooltipCatalog.BuildAttached(null, unit, null)
                .Select(e => e.Title), Is.EqualTo(new[] { "빙결" }));
            unit.ResolveActionDebuffsTurnEnd();
            Assert.That(OccupantStatusTooltipCatalog.BuildAttached(null, unit, null), Is.Empty);
        }

        [Test]
        public void Formatter_ShowsEachStateAndRemainingTurnEnds()
        {
            var unit = Unit(); unit.ApplyStun(2); unit.ApplyFreeze(4);
            var text = BattleUiFormatter.FormatTileOccupant(unit);
            Assert.That(text, Does.Contain("스턴 2"));
            Assert.That(text, Does.Contain("빙결 4"));
            unit.ClearActionDebuffs();
            text = BattleUiFormatter.FormatTileOccupant(unit);
            Assert.That(text, Does.Not.Contain("스턴"));
            Assert.That(text, Does.Not.Contain("빙결"));
        }

        [Test]
        public void AbilityTooltips_StayAvailableWhenActionsAreBlocked()
        {
            var unit = Unit();
            var before = OccupantSpecialEffectTooltipCatalog.Build(null, unit)
                .Select(e => e.Title + ":" + e.Description).ToArray();
            unit.ApplyStun(3); unit.ApplyFreeze(3);
            var after = OccupantSpecialEffectTooltipCatalog.Build(null, unit)
                .Select(e => e.Title + ":" + e.Description).ToArray();
            Assert.That(after, Is.EqualTo(before));
            Assert.That(unit.HasActiveShielder, Is.True);
            Assert.That(unit.EffectiveSpellPower, Is.EqualTo(20));
        }
    }
}
