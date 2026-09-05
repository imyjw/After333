using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Tests.EditMode
{
    public sealed class HeroTests
    {
        [Test]
        public void EndTurn_EachActiveHeroIndependentlyGainsAttackOrHp()
        {
            var battleState = CreateBattleStateInMainPhase();
            var playerHero = CreateHero("player-hero", PlayerId.Player, new TileCoord(0, 0));
            var aiHero = CreateHero("ai-hero", PlayerId.AI, new TileCoord(0, 0));
            battleState.PlayerBoard.Place(playerHero.Position, playerHero);
            battleState.AIBoard.Place(aiHero.Position, aiHero);

            new EndTurnService(new SequenceRandom(0, 1)).EndTurn(battleState);

            Assert.That(playerHero.BaseAttack, Is.EqualTo(HeroRules.BaseAttack + HeroRules.GrowthAmount));
            Assert.That(playerHero.MaxHp, Is.EqualTo(HeroRules.BaseHealth));
            Assert.That(aiHero.BaseAttack, Is.EqualTo(HeroRules.BaseAttack));
            Assert.That(aiHero.MaxHp, Is.EqualTo(HeroRules.BaseHealth + HeroRules.GrowthAmount));
            Assert.That(aiHero.CurrentHp, Is.EqualTo(HeroRules.BaseHealth + HeroRules.GrowthAmount));
        }

        [Test]
        public void EndTurn_SuppressedHeroDoesNotGrowButInvincibleCountdownContinues()
        {
            var battleState = CreateBattleStateInMainPhase();
            var hero = CreateHero("drained-hero", PlayerId.Player, new TileCoord(0, 0));
            hero.IsDrained = true;
            hero.AddInvincibleEffect(
                InvincibleDurationType.GlobalTurnEnds,
                HeroRules.InvincibleTurnEnds,
                battleState.TurnNumber,
                battleState.ActivePlayerId);
            battleState.PlayerBoard.Place(hero.Position, hero);

            new EndTurnService(new SequenceRandom(0)).EndTurn(battleState);

            Assert.That(hero.BaseAttack, Is.EqualTo(HeroRules.BaseAttack));
            Assert.That(hero.MaxHp, Is.EqualTo(HeroRules.BaseHealth));
            Assert.That(hero.InvincibleEffects, Has.Count.EqualTo(1));
            Assert.That(hero.InvincibleEffects[0].OwnerTurnsRemaining, Is.EqualTo(2));
        }

        [Test]
        public void GlobalTurnEndsInvincible_ExpiresAfterThreeTurnEndingsIncludingSummonTurn()
        {
            var battleState = CreateBattleStateInMainPhase();
            var hero = CreateHero("invincible-hero", PlayerId.Player, new TileCoord(0, 0));
            hero.AddInvincibleEffect(
                InvincibleDurationType.GlobalTurnEnds,
                HeroRules.InvincibleTurnEnds,
                battleState.TurnNumber,
                battleState.ActivePlayerId);
            battleState.PlayerBoard.Place(hero.Position, hero);
            var endTurnService = new EndTurnService(new SequenceRandom(0, 0, 0));
            var turnStartService = new TurnStartService();

            endTurnService.EndTurn(battleState);
            Assert.That(hero.InvincibleEffects[0].OwnerTurnsRemaining, Is.EqualTo(2));
            turnStartService.ResolveTurnStart(battleState);

            endTurnService.EndTurn(battleState);
            Assert.That(hero.InvincibleEffects[0].OwnerTurnsRemaining, Is.EqualTo(1));
            turnStartService.ResolveTurnStart(battleState);

            endTurnService.EndTurn(battleState);

            Assert.That(hero.InvincibleEffects, Is.Empty);
            Assert.That(hero.IsInvincible, Is.False);
        }

        [Test]
        public void StateViewRoundTrip_PreservesHeroStatsAndGlobalInvincibleCountdown()
        {
            var battleState = CreateBattleStateInMainPhase();
            var hero = CreateHero("view-hero", PlayerId.Player, new TileCoord(0, 0));
            hero.IncreaseBaseAttack(HeroRules.GrowthAmount);
            hero.IncreaseMaxHpAndCurrentHp(HeroRules.GrowthAmount);
            hero.AddInvincibleEffect(
                InvincibleDurationType.GlobalTurnEnds,
                2,
                battleState.TurnNumber,
                battleState.ActivePlayerId);
            battleState.PlayerBoard.Place(hero.Position, hero);

            var view = new BattleStateViewFactory().CreateForPlayer(
                battleState,
                "hero-match",
                PlayerId.Player);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(view);
            var restored = projected.PlayerBoard.GetOccupant(hero.Position);

            Assert.That(restored.CardId, Is.EqualTo(HeroRules.CardId));
            Assert.That(restored.BaseAttack, Is.EqualTo(HeroRules.BaseAttack + HeroRules.GrowthAmount));
            Assert.That(restored.MaxHp, Is.EqualTo(HeroRules.BaseHealth + HeroRules.GrowthAmount));
            Assert.That(restored.InvincibleEffects, Has.Count.EqualTo(1));
            Assert.That(restored.InvincibleEffects[0].Duration, Is.EqualTo(InvincibleDurationType.GlobalTurnEnds));
            Assert.That(restored.InvincibleEffects[0].OwnerTurnsRemaining, Is.EqualTo(2));
        }

        private static UnitState CreateHero(string runtimeId, PlayerId ownerId, TileCoord coord)
        {
            return new UnitState(
                runtimeId,
                HeroRules.CardId,
                ownerId,
                coord,
                AttackType.Melee,
                HeroRules.BaseAttack,
                HeroRules.BaseHealth,
                true,
                false,
                0,
                damageType: DamageType.Fixed,
                physicalDefense: HeroRules.PhysicalDefense,
                magicDefense: HeroRules.MagicDefense);
        }

        private static BattleState CreateBattleStateInMainPhase()
        {
            var battleState = new BattleSetupService().CreateInitialState(
                new BattleSetupRequest(CreateDeck("P"), CreateDeck("A"), PlayerId.Player));
            new MulliganService().PassMulligan(battleState, PlayerId.Player);
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

        private sealed class SequenceRandom : Random
        {
            private readonly Queue<int> _values;

            public SequenceRandom(params int[] values)
            {
                _values = new Queue<int>(values ?? Array.Empty<int>());
            }

            public override int Next(int maxValue)
            {
                if (_values.Count == 0)
                {
                    return 0;
                }

                return Math.Abs(_values.Dequeue()) % maxValue;
            }
        }
    }
}
