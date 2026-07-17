using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class BattleMulliganOverlayPresenterTests
    {
        [TestCase(true, "선공\n시작 카드를 선택하세요")]
        [TestCase(false, "후공\n시작 카드를 선택하세요")]
        public void BuildTurnOrderTitle_UsesLocalPlayerPerspective(
            bool isLocalFirstPlayer,
            string expected)
        {
            var result = BattleMulliganOverlayPresenter.BuildTurnOrderTitle(
                isLocalFirstPlayer,
                "시작 카드를 선택하세요");

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void BuildConfirmedCardLayout_ReplacesSelectedSlotAndIgnoresTurnStartDraw()
        {
            var result = BattleMulliganOverlayPresenter.BuildConfirmedCardLayout(
                new[] { "A", "B", "C" },
                new HashSet<int> { 1 },
                new[] { "A", "C", "D", "TURN_DRAW" });

            Assert.That(result, Is.EqualTo(new[] { "A", "D", "C" }));
        }

        [Test]
        public void BuildConfirmedCardLayout_WithDuplicateCards_PreservesUnselectedCopy()
        {
            var result = BattleMulliganOverlayPresenter.BuildConfirmedCardLayout(
                new[] { "A", "A", "B", "C" },
                new HashSet<int> { 0, 2 },
                new[] { "A", "C", "X", "Y", "TURN_DRAW" });

            Assert.That(result, Is.EqualTo(new[] { "X", "A", "Y", "C" }));
        }

        [Test]
        public void BuildConfirmedCardLayout_WithNoSelectedCards_KeepsOpeningLayout()
        {
            var result = BattleMulliganOverlayPresenter.BuildConfirmedCardLayout(
                new[] { "A", "B", "C" },
                new HashSet<int>(),
                new[] { "A", "B", "C", "TURN_DRAW" });

            Assert.That(result, Is.EqualTo(new[] { "A", "B", "C" }));
        }

        [Test]
        public void TryBuildCardStatValues_UnitUsesOwnedCardUpgradeBonuses()
        {
            var definition = new UnitCardDefinition(
                cardId: "unit",
                displayName: "Unit",
                cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 0),
                attackType: AttackType.Melee,
                attack: 20,
                health: 30,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0);

            var hasStats = BattleMulliganOverlayPresenter.TryBuildCardStatValues(
                definition,
                upgradeLevel: 5,
                out var attack,
                out var hp);

            Assert.That(hasStats, Is.True);
            Assert.That(attack, Is.EqualTo(21));
            Assert.That(hp, Is.EqualTo(34));
        }

        [Test]
        public void TryBuildCardStatValues_SpellDoesNotExposeAttackOrHp()
        {
            var definition = new DamageSpellCardDefinition(
                cardId: "spell",
                displayName: "Spell",
                cost: new ResourceSet(mana: 1, qi: 0, power: 0, gold: 0),
                damage: 10);

            var hasStats = BattleMulliganOverlayPresenter.TryBuildCardStatValues(
                definition,
                upgradeLevel: 0,
                out _,
                out _);

            Assert.That(hasStats, Is.False);
        }
    }
}
