using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;

namespace Project333.Tests.EditMode
{
    public sealed class ErasureServiceTests
    {
        [Test]
        public void Apply_ExpiresAttachedEffectsButKeepsUnrelatedPersistentEffects()
        {
            var battleState = CreateBattleState();
            var target = new UnitState(
                "target-runtime",
                "target-card",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Melee,
                attack: 10,
                maxHp: 20,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0);
            battleState.PlayerBoard.Place(target.Position, target);
            var attachedDebuff = new PersistentEffectState(
                "CheonraJimang",
                PlayerId.AI,
                "cheonra_jimang",
                appliedTurn: 1,
                endConditionText: "destroy target",
                turnStartResourceGain: new ResourceSet(),
                targetRuntimeId: target.RuntimeId);
            var unrelatedEffect = new PersistentEffectState(
                "Daehwandan",
                PlayerId.Player,
                "daehwandan",
                appliedTurn: 1,
                endConditionText: "gain qi",
                turnStartResourceGain: new ResourceSet(mana: 0, qi: 3, power: 0, gold: 0),
                ownerTurnStartsRemaining: 3);
            battleState.PersistentEffects.Add(attachedDebuff);
            battleState.PersistentEffects.Add(unrelatedEffect);

            new ErasureService().Apply(battleState, target);

            Assert.That(target.IsErasure, Is.True);
            Assert.That(attachedDebuff.IsExpired, Is.True);
            Assert.That(unrelatedEffect.IsExpired, Is.False);
        }

        private static BattleState CreateBattleState()
        {
            return new BattleSetupService().CreateInitialState(new BattleSetupRequest(
                new[] { "p0", "p1", "p2", "p3", "p4", "p5", "p6", "p7", "p8", "p9" },
                new[] { "a0", "a1", "a2", "a3", "a4", "a5", "a6", "a7", "a8", "a9" },
                PlayerId.Player));
        }
    }
}
