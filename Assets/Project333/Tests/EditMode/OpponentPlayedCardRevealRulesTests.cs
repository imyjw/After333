using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class OpponentPlayedCardRevealRulesTests
    {
        [TestCase(BattleEventType.CardPlayed)]
        [TestCase(BattleEventType.SpellCast)]
        public void ShouldReveal_OpponentCardUse_ReturnsTrue(BattleEventType eventType)
        {
            var battleEvent = new BattleEventDto
            {
                EventType = eventType,
                SourceOwnerId = PlayerId.AI,
                CardId = "Goblin"
            };

            Assert.That(OpponentPlayedCardRevealRules.ShouldReveal(battleEvent), Is.True);
        }

        [Test]
        public void ShouldReveal_LocalCardUse_ReturnsFalse()
        {
            var battleEvent = new BattleEventDto
            {
                EventType = BattleEventType.CardPlayed,
                SourceOwnerId = PlayerId.Player,
                CardId = "Goblin"
            };

            Assert.That(OpponentPlayedCardRevealRules.ShouldReveal(battleEvent), Is.False);
        }

        [Test]
        public void ShouldReveal_ReconnectEvent_ReturnsFalse()
        {
            var battleEvent = new BattleEventDto
            {
                EventType = BattleEventType.ReconnectGraceCancelled,
                SourceOwnerId = PlayerId.AI,
                CardId = "Goblin"
            };

            Assert.That(OpponentPlayedCardRevealRules.ShouldReveal(battleEvent), Is.False);
        }

        [TestCase(BattleEventType.CardPlayed, true)]
        [TestCase(BattleEventType.SpellCast, false)]
        public void IsBoardCardPlay_DistinguishesSummonsFromSpells(
            BattleEventType eventType,
            bool expected)
        {
            var battleEvent = new BattleEventDto
            {
                EventType = eventType,
                SourceOwnerId = PlayerId.AI,
                CardId = "Goblin"
            };

            Assert.That(OpponentPlayedCardRevealRules.IsBoardCardPlay(battleEvent), Is.EqualTo(expected));
        }

        [Test]
        public void ResolveDisplayDuration_UsesAdaptiveQueueDuration()
        {
            Assert.That(
                OpponentPlayedCardRevealRules.ResolveDisplayDuration(false, 3f, 2f),
                Is.EqualTo(3f));
            Assert.That(
                OpponentPlayedCardRevealRules.ResolveDisplayDuration(true, 3f, 2f),
                Is.EqualTo(2f));
        }
    }
}
