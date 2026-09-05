using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class InvincibleTests
    {
        [Test]
        public void DamageResolution_InvinciblePreventsEveryDamageTypeAndDoesNotConsumeEndure()
        {
            var target = CreateUnit("invincible", PlayerId.Player, new TileCoord(0, 0), hasEndure: true);
            target.AddInvincibleEffect(InvincibleDurationType.Always);

            Assert.That(DamageResolutionRules.ApplyAttackDamage(target, 20, DamageType.Physical), Is.Zero);
            Assert.That(DamageResolutionRules.ApplyEffectDamage(target, 20, DamageType.Magic), Is.Zero);
            Assert.That(DamageResolutionRules.ApplyEffectDamage(target, 20, DamageType.Fixed), Is.Zero);
            Assert.That(target.CurrentHp, Is.EqualTo(20));
            Assert.That(target.EndureUsed, Is.False);
        }

        [Test]
        public void Attack_MultiHitInvincibleTarget_ConsumesAttackButDealsNoDamageOrLifeSteal()
        {
            var battleState = CreateBattleStateInMainPhase();
            var attacker = CreateUnit(
                "lifesteal-attacker",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Ranged,
                attack: 7,
                hitsPerAttack: 3,
                hasLifeSteal: true);
            attacker.CurrentHp = 5;
            attacker.HasSummoningSickness = false;
            var defender = CreateUnit(
                "invincible-defender",
                PlayerId.AI,
                new TileCoord(0, 0),
                hasEndure: true);
            defender.AddInvincibleEffect(
                InvincibleDurationType.Always,
                appliedTurnNumber: battleState.TurnNumber,
                appliedActivePlayerId: battleState.ActivePlayerId);
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(defender.Position, defender);

            new AttackService().Attack(
                battleState,
                PlayerId.Player,
                attacker.Position,
                defender.Position);

            Assert.That(defender.CurrentHp, Is.EqualTo(20));
            Assert.That(defender.EndureUsed, Is.False);
            Assert.That(attacker.CurrentHp, Is.EqualTo(5));
            Assert.That(attacker.RemainingAttacksThisTurn, Is.Zero);
            Assert.That(battleState.ValuePopupEvents, Has.Count.EqualTo(3));
            Assert.That(battleState.ValuePopupEvents, Has.All.Property("IsInvinciblePrevented").True);
        }

        [Test]
        public void Attack_InvincibleShielder_BlocksHitWithoutOverflow()
        {
            var battleState = CreateBattleStateInMainPhase();
            var attacker = CreateUnit(
                "attacker",
                PlayerId.Player,
                new TileCoord(0, 0),
                AttackType.Ranged,
                attack: 20);
            attacker.HasSummoningSickness = false;
            var shielder = CreateUnit(
                "shielder",
                PlayerId.AI,
                new TileCoord(0, 0),
                attack: 0,
                maxHp: 3,
                hasShielder: true);
            shielder.AddInvincibleEffect(
                InvincibleDurationType.Always,
                appliedTurnNumber: battleState.TurnNumber,
                appliedActivePlayerId: battleState.ActivePlayerId);
            var protectedUnit = CreateUnit(
                "protected",
                PlayerId.AI,
                new TileCoord(0, 1),
                maxHp: 6);
            battleState.PlayerBoard.Place(attacker.Position, attacker);
            battleState.AIBoard.Place(shielder.Position, shielder);
            battleState.AIBoard.Place(protectedUnit.Position, protectedUnit);

            new AttackService().Attack(
                battleState,
                PlayerId.Player,
                attacker.Position,
                protectedUnit.Position);

            Assert.That(shielder.CurrentHp, Is.EqualTo(3));
            Assert.That(protectedUnit.CurrentHp, Is.EqualTo(6));
            Assert.That(battleState.ValuePopupEvents, Has.Count.EqualTo(1));
            Assert.That(battleState.ValuePopupEvents[0].IsInvinciblePrevented, Is.True);
        }

        [Test]
        public void Invincible_DrainedAndErasureSuppressItWithoutDeletingDuration()
        {
            var target = CreateUnit("suppressed", PlayerId.Player, new TileCoord(0, 0));
            target.AddInvincibleEffect(InvincibleDurationType.Always);

            target.IsDrained = true;
            Assert.That(target.IsInvincible, Is.False);
            Assert.That(DamageResolutionRules.ApplyEffectDamage(target, 2, DamageType.Fixed), Is.EqualTo(6));

            target.IsDrained = false;
            Assert.That(target.IsInvincible, Is.True);
            target.ApplyErasure();
            Assert.That(target.IsInvincible, Is.False);
            Assert.That(target.InvincibleEffects, Has.Count.EqualTo(1));
            Assert.That(DamageResolutionRules.ApplyEffectDamage(target, 2, DamageType.Fixed), Is.EqualTo(2));
        }

        [Test]
        public void InvincibleDuration_TurnConditionsAndSeparateEffectsResolveIndependently()
        {
            var target = CreateUnit("durations", PlayerId.Player, new TileCoord(0, 0));
            target.AddInvincibleEffect(InvincibleDurationType.Always);
            target.AddInvincibleEffect(InvincibleDurationType.SummonTurn);
            target.AddInvincibleEffect(InvincibleDurationType.UntilTurnEnd);
            target.AddInvincibleEffect(InvincibleDurationType.OwnerTurns, ownerTurns: 2);

            target.ResolveInvincibleTurnEnd(PlayerId.AI);
            Assert.That(target.InvincibleEffects, Has.Count.EqualTo(2));
            Assert.That(target.InvincibleEffects[1].OwnerTurnsRemaining, Is.EqualTo(2));

            target.ResolveInvincibleTurnEnd(PlayerId.Player);
            Assert.That(target.InvincibleEffects, Has.Count.EqualTo(2));
            Assert.That(target.InvincibleEffects[1].OwnerTurnsRemaining, Is.EqualTo(1));

            target.ResolveInvincibleTurnEnd(PlayerId.Player);
            Assert.That(target.InvincibleEffects, Has.Count.EqualTo(1));
            Assert.That(target.InvincibleEffects[0].Duration, Is.EqualTo(InvincibleDurationType.Always));

            var conditional = CreateUnit("conditional", PlayerId.Player, new TileCoord(1, 0));
            conditional.AddInvincibleEffect(InvincibleDurationType.OwnerTurnOnly);
            conditional.SetBattleTurnContext(PlayerId.Player, 1);
            Assert.That(conditional.IsInvincible, Is.True);
            conditional.SetBattleTurnContext(PlayerId.AI, 2);
            Assert.That(conditional.IsInvincible, Is.False);

            conditional.RestoreInvincibleEffects(new[]
            {
                new InvincibleEffectState(
                    InvincibleDurationType.OpponentTurnOnly,
                    0,
                    2,
                    PlayerId.AI),
            });
            Assert.That(conditional.IsInvincible, Is.True);
        }

        [Test]
        public void EndTurn_AdvancesOwnerTurnsInvincibleEvenWhileSealbound()
        {
            var battleState = CreateBattleStateInMainPhase();
            var target = CreateUnit("sealed-invincible", PlayerId.Player, new TileCoord(0, 0));
            target.AddInvincibleEffect(
                InvincibleDurationType.OwnerTurns,
                ownerTurns: 1,
                appliedTurnNumber: battleState.TurnNumber,
                appliedActivePlayerId: battleState.ActivePlayerId);
            target.EnterSealbound(2);
            battleState.PlayerBoard.Place(target.Position, target);

            new EndTurnService().EndTurn(battleState);

            Assert.That(target.InvincibleEffects, Is.Empty);
            Assert.That(target.IsSealbound, Is.True);
            Assert.That(DamageResolutionRules.ApplyEffectDamage(target, 100, DamageType.Fixed), Is.Zero);
        }

        [Test]
        public void PlayCard_InvincibleDefinitionCreatesRuntimeEffect()
        {
            var definition = new UnitCardDefinition(
                cardId: "invincible-card",
                displayName: "Invincible Card",
                cost: new ResourceSet(),
                attackType: AttackType.Melee,
                attack: 1,
                health: 10,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                invincibleDuration: InvincibleDurationType.OwnerTurns,
                invincibleOwnerTurns: 2);
            var battleState = CreateBattleStateInMainPhase();
            battleState.Player.Hand.Add(definition.CardId);
            var service = new PlayCardService(
                new InMemoryCardDefinitionProvider(new CardDefinition[] { definition }));

            var unit = service.PlayUnitCard(
                battleState,
                PlayerId.Player,
                definition.CardId,
                new TileCoord(0, 0));

            Assert.That(unit.IsInvincible, Is.True);
            Assert.That(unit.InvincibleEffects, Has.Count.EqualTo(1));
            Assert.That(unit.InvincibleEffects[0].OwnerTurnsRemaining, Is.EqualTo(2));
        }

        [Test]
        public void StateViewRoundTrip_PreservesEveryInvincibleEffectField()
        {
            var battleState = CreateBattleStateInMainPhase();
            var target = CreateUnit("view-invincible", PlayerId.Player, new TileCoord(0, 0));
            target.AddInvincibleEffect(
                InvincibleDurationType.OwnerTurns,
                ownerTurns: 2,
                appliedTurnNumber: 7,
                appliedActivePlayerId: PlayerId.AI);
            target.AddInvincibleEffect(
                InvincibleDurationType.OwnerTurnOnly,
                appliedTurnNumber: 8,
                appliedActivePlayerId: PlayerId.Player);
            battleState.PlayerBoard.Place(target.Position, target);

            var view = new BattleStateViewFactory().CreateForPlayer(
                battleState,
                "invincible-match",
                PlayerId.Player);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(view);
            var restored = projected.PlayerBoard.GetOccupant(target.Position);

            Assert.That(restored.InvincibleEffects, Has.Count.EqualTo(2));
            Assert.That(restored.InvincibleEffects[0].Duration, Is.EqualTo(InvincibleDurationType.OwnerTurns));
            Assert.That(restored.InvincibleEffects[0].OwnerTurnsRemaining, Is.EqualTo(2));
            Assert.That(restored.InvincibleEffects[0].AppliedTurnNumber, Is.EqualTo(7));
            Assert.That(restored.InvincibleEffects[0].AppliedActivePlayerId, Is.EqualTo(PlayerId.AI));
            Assert.That(restored.InvincibleEffects[1].Duration, Is.EqualTo(InvincibleDurationType.OwnerTurnOnly));
        }

        [Test]
        public void NonDamageRemoval_InvincibleDoesNotPreventCheonraJimang()
        {
            var battleState = CreateBattleStateAtTurnStart();
            var target = CreateUnit("marked-invincible", PlayerId.AI, new TileCoord(0, 0));
            target.AddInvincibleEffect(
                InvincibleDurationType.Always,
                appliedTurnNumber: battleState.TurnNumber,
                appliedActivePlayerId: battleState.ActivePlayerId);
            battleState.AIBoard.Place(target.Position, target);
            battleState.PersistentEffects.Add(new PersistentEffectState(
                sourceCardId: "CheonraJimang",
                ownerId: PlayerId.Player,
                effectId: "cheonra_jimang",
                appliedTurn: battleState.TurnNumber,
                endConditionText: "next owner turn",
                turnStartResourceGain: new ResourceSet(),
                targetRuntimeId: target.RuntimeId));

            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(battleState.AIBoard.GetOccupant(target.Position), Is.Null);
        }

        [Test]
        public void Master_CanReceiveInvincibleAndPreventDeckExhaustionStyleFixedDamage()
        {
            var battleState = CreateBattleStateInMainPhase();
            battleState.Player.Master.AddInvincibleEffect(
                InvincibleDurationType.Always,
                appliedTurnNumber: battleState.TurnNumber,
                appliedActivePlayerId: battleState.ActivePlayerId);

            var actualDamage = DamageResolutionRules.ApplyEffectDamage(
                battleState.Player.Master,
                333,
                DamageType.Fixed);

            Assert.That(actualDamage, Is.Zero);
            Assert.That(battleState.Player.Master.CurrentHp, Is.EqualTo(333));
        }

        private static UnitState CreateUnit(
            string id,
            PlayerId ownerId,
            TileCoord coord,
            AttackType attackType = AttackType.Melee,
            int attack = 5,
            int maxHp = 20,
            int hitsPerAttack = 1,
            bool hasEndure = false,
            bool hasShielder = false,
            bool hasLifeSteal = false)
        {
            return new UnitState(
                runtimeId: id,
                cardId: id,
                ownerId: ownerId,
                position: coord,
                attackType: attackType,
                attack: attack,
                maxHp: maxHp,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                hitsPerAttack: hitsPerAttack,
                hasEndure: hasEndure,
                hasShielder: hasShielder,
                hasLifeSteal: hasLifeSteal);
        }

        private static BattleState CreateBattleStateAtTurnStart()
        {
            var battleState = new BattleSetupService().CreateInitialState(
                new BattleSetupRequest(CreateDeck("P"), CreateDeck("A"), PlayerId.Player));
            new MulliganService().PassMulligan(battleState, PlayerId.Player);
            return battleState;
        }

        private static BattleState CreateBattleStateInMainPhase()
        {
            var battleState = CreateBattleStateAtTurnStart();
            new TurnStartService().ResolveTurnStart(battleState);
            return battleState;
        }

        private static IReadOnlyList<string> CreateDeck(string prefix)
        {
            var cards = new List<string>();
            for (var index = 0; index < 10; index++)
            {
                cards.Add($"{prefix}-{index:00}");
            }

            return cards;
        }
    }
}
