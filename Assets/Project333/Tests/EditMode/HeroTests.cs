using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class HeroTests
    {
        private static BattleState State(PlayerId actor = PlayerId.Player)
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, actor));
            state.SetPhase(PhaseType.Main);
            return state;
        }

        private static UnitState Hero(BattleState state, PlayerId owner = PlayerId.Player, int hits = 1, bool piercing = false, bool flying = false)
        {
            var hero = new UnitState("hero", "Hero", owner, new TileCoord(0,0), AttackType.Melee,
                33, 33, true, false, 0, hitsPerAttack: hits, damageType: DamageType.Fixed,
                hasRush: true, hasPiercing: piercing, hasFlying: flying);
            hero.HasSummoningSickness = false;
            state.GetBoard(owner).Place(hero.Position, hero);
            return hero;
        }

        private static UnitState Enemy(BattleState state, int hp = 20, int attack = 0, int row = 0, bool shielder = false, bool endure = false)
        {
            var enemy = new UnitState("enemy-"+row, "Goblin", PlayerId.AI, new TileCoord(0,row),
                AttackType.Melee, attack, hp, true, false, 0, hasShielder: shielder, hasEndure: endure);
            state.AIBoard.Place(enemy.Position, enemy);
            return enemy;
        }

        private static void Attack(BattleState state, UnitState hero, OccupantState target, int roll)
        {
            new AttackService(new TargetingService(), n => { Assert.That(n, Is.EqualTo(2)); return roll; })
                .Attack(state, hero.OwnerId, hero.Position, target.Position);
        }

        [TestCase(0)][TestCase(1)]
        public void NormalAttackKill_GrantsExactlyOneStatAndPreservesDamage(int roll)
        {
            var state = State(); var hero = Hero(state); hero.CurrentHp = 20;
            var victim = Enemy(state, attack: 2);
            Attack(state, hero, victim, roll);
            Assert.That(state.AIBoard.GetOccupant(victim.Position), Is.Null);
            Assert.That(hero.BaseAttack, Is.EqualTo(roll == 0 ? 66 : 33));
            Assert.That(hero.MaxHp, Is.EqualTo(roll == 1 ? 66 : 33));
            Assert.That(hero.CurrentHp, Is.EqualTo(roll == 1 ? 51 : 18));
            Assert.That(hero.RemainingAttacksThisTurn, Is.Zero);
        }

        [Test]
        public void BuildingKill_AlsoGrantsBonus()
        {
            var state = State(); var hero = Hero(state);
            var victim = new BuildingState("building","PowerPlant",PlayerId.AI,new TileCoord(0,0),false,0,30);
            state.AIBoard.Place(victim.Position,victim);
            Attack(state,hero,victim,0);
            Assert.That(hero.BaseAttack,Is.EqualTo(66));
        }

        [TestCase(false)][TestCase(true)]
        public void SurvivingOrEnduringTarget_DoesNotGrantBonus(bool endure)
        {
            var state=State(); var hero=Hero(state); var target=Enemy(state,endure?20:100,endure:endure);
            new AttackService(new TargetingService(), _=>throw new Exception("Unexpected bonus roll"))
                .Attack(state,hero.OwnerId,hero.Position,target.Position);
            Assert.That(target.IsAlive,Is.True);
            Assert.That(hero.BaseAttack,Is.EqualTo(33));
            Assert.That(hero.MaxHp,Is.EqualTo(33));
        }

        [Test]
        public void MultipleHits_CountSameVictimOnlyOnce()
        {
            var state=State(); var hero=Hero(state,hits:3); var target=Enemy(state,hp:40);
            var rolls=0;
            new AttackService(new TargetingService(), _=>{rolls++;return 0;}).Attack(state,hero.OwnerId,hero.Position,target.Position);
            Assert.That(rolls,Is.EqualTo(1));
            Assert.That(hero.BaseAttack,Is.EqualTo(66));
        }

        [Test]
        public void Piercing_KillsTwoOccupantsAndRollsForEach()
        {
            var state=State(); var hero=Hero(state,piercing:true); var front=Enemy(state); Enemy(state,row:1);
            var rolls=0;
            new AttackService(new TargetingService(), _=>rolls++%2).Attack(state,hero.OwnerId,hero.Position,front.Position);
            Assert.That(rolls,Is.EqualTo(2));
            Assert.That(hero.BaseAttack,Is.EqualTo(66));
            Assert.That(hero.MaxHp,Is.EqualTo(66));
        }

        [Test]
        public void ShielderAndOverflow_KillsCountOnceEach()
        {
            var state=State();var hero=Hero(state,flying:true);
            Enemy(state,hp:10,shielder:true);var target=Enemy(state,hp:10,row:1);
            var rolls=0;
            new AttackService(new TargetingService(), _=>{rolls++;return 0;}).Attack(state,hero.OwnerId,hero.Position,target.Position);
            Assert.That(rolls,Is.EqualTo(2));
            Assert.That(hero.BaseAttack,Is.EqualTo(99));
        }

        [Test]
        public void SimultaneousDeath_DoesNotResurrectHero()
        {
            var state=State();var hero=Hero(state);var target=Enemy(state,attack:40);
            new AttackService(new TargetingService(), _=>throw new Exception("Dead Hero must not roll")).Attack(state,hero.OwnerId,hero.Position,target.Position);
            Assert.That(hero.IsAlive,Is.False);
            Assert.That(state.PlayerBoard.GetOccupant(hero.Position),Is.Null);
            Assert.That(hero.MaxHp,Is.EqualTo(33));
        }

        [Test]
        public void CounterattackKill_DoesNotGrantBonus()
        {
            var state=State(PlayerId.AI);var hero=Hero(state);var enemy=Enemy(state,attack:1);
            enemy.HasSummoningSickness=false;
            new AttackService(new TargetingService(), _=>throw new Exception("Counterattack must not roll")).Attack(state,PlayerId.AI,enemy.Position,hero.Position);
            Assert.That(enemy.IsAlive,Is.False);
            Assert.That(hero.BaseAttack,Is.EqualTo(33));
        }

        [Test]
        public void Erasure_PreventsKillBonus()
        {
            var state=State();var hero=Hero(state);hero.ApplyErasure();hero.HasSummoningSickness=false;
            var target=Enemy(state);
            new AttackService(new TargetingService(), _=>throw new Exception("Erased Hero must not roll")).Attack(state,hero.OwnerId,hero.Position,target.Position);
            Assert.That(target.IsAlive,Is.False);
            Assert.That(hero.BaseAttack,Is.EqualTo(33));
        }

        [Test]
        public void MasterKill_DoesNotGrantBonus()
        {
            var state=State();var hero=Hero(state);state.AI.Master.CurrentHp=1;
            new AttackService(new TargetingService(), _=>throw new Exception("Master must not count")).Attack(state,hero.OwnerId,hero.Position,state.AI.Master.Position);
            Assert.That(state.IsEnded,Is.True);
            Assert.That(hero.BaseAttack,Is.EqualTo(33));
        }

        [Test]
        public void NuclearDestructionCollateral_DoesNotGrantExtraBonus()
        {
            var state=State();var hero=Hero(state);var collateral=Enemy(state,hp:10,row:1);
            var reactor=new BuildingState("reactor","NuclearPowerPlant",PlayerId.AI,new TileCoord(0,0),false,0,5);
            state.AIBoard.Place(reactor.Position,reactor);
            var rolls=0;
            new AttackService(new TargetingService(), _=>{rolls++;return 0;}).Attack(state,hero.OwnerId,hero.Position,reactor.Position);
            Assert.That(collateral.IsAlive,Is.False);
            Assert.That(rolls,Is.EqualTo(1));
            Assert.That(hero.BaseAttack,Is.EqualTo(66));
        }

        [Test]
        public void HuanShuFriendlyKill_DoesNotGrantBonus()
        {
            var state=State();var hero=Hero(state);var target=Enemy(state);
            var ally=new BuildingState("ally","PowerPlant",PlayerId.Player,new TileCoord(1,0),false,0,10);
            state.PlayerBoard.Place(ally.Position,ally);hero.ApplyHuanShu();
            var candidates=state.PlayerBoard.EnumerateOccupants().Concat(state.AIBoard.EnumerateOccupants())
                .Where(o=>!ReferenceEquals(o,hero)&&o.IsAlive&&o.CanBeAffected&&!(o.OwnerId!=hero.OwnerId&&o.IsHiding)).ToArray();
            var index=Array.IndexOf(candidates,ally);var rolls=0;
            new AttackService(new TargetingService(), _=>{rolls++;return index;}).Attack(state,hero.OwnerId,hero.Position,target.Position);
            Assert.That(ally.IsAlive,Is.False);
            Assert.That(hero.BaseAttack,Is.EqualTo(33));
            Assert.That(rolls,Is.EqualTo(1));
        }

        [Test]
        public void EndTurn_DoesNotGrantAnyStatBonus()
        {
            var state=State();var hero=Hero(state);Hero(state,PlayerId.AI);
            new EndTurnService().EndTurn(state);
            Assert.That(hero.BaseAttack,Is.EqualTo(33));
            Assert.That(hero.MaxHp,Is.EqualTo(33));
            Assert.That(state.AIBoard.GetOccupant(new TileCoord(0,0)).MaxHp,Is.EqualTo(33));
        }

        [Test]
        public void SummonFromCurrentData_HasRushAndNoInvincible_UpgradeTotalsUnchanged()
        {
            var json=File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json");
            Assert.That(CardDatabaseValidator.ValidateJson(json).Errors,Is.Empty);
            var provider=JsonCardDefinitionDatabase.FromJson(json).CreateProvider();
            var state=State();state.Player.Hand.Add("Hero");state.Player.Resources.Add(new ResourceSet(3,0,0,0));
            var hero=new PlayCardService(provider).PlayUnitCard(state,PlayerId.Player,"Hero",new TileCoord(0,0));
            Assert.That(hero.BaseAttack,Is.EqualTo(33));
            Assert.That(hero.MaxHp,Is.EqualTo(33));
            Assert.That(hero.HasSummoningSickness,Is.False);
            Assert.That(hero.HasRush,Is.True);
            Assert.That(hero.InvincibleEffects,Is.Empty);
            var bonus=CardLevelStatRules.CalculateBonus("Hero",13);
            Assert.That(33+bonus.AttackBonus,Is.EqualTo(39));
            Assert.That(33+bonus.HpBonus,Is.EqualTo(42));
        }

        [Test]
        public void StateViewAndAiCopy_PreserveKillBonusWithoutReroll()
        {
            var state=State();var hero=Hero(state);Attack(state,hero,Enemy(state),1);
            var view=new BattleStateViewFactory().CreateForPlayer(state,"hero-match",PlayerId.Player);
            foreach(var restored in new[]{BattleStateViewProjector.CreateLocalPerspectiveState(view),AiBattleStateCopy.Create(state)})
            {
                var copy=restored.PlayerBoard.GetOccupant(hero.Position);
                Assert.That(copy.BaseAttack,Is.EqualTo(33));
                Assert.That(copy.MaxHp,Is.EqualTo(66));
                Assert.That(copy.HasRush,Is.True);
                Assert.That(copy.InvincibleEffects,Is.Empty);
            }
        }
    }
}
