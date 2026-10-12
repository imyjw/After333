using System;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Tests.EditMode
{
    public sealed class AttackTargetRejectionTests
    {
        private static readonly TileCoord Source = new TileCoord(0,0), Target = new TileCoord(1,1), Front = new TileCoord(1,0);
        private static BattleState Battle(PlayerId actor)
        {
            var deck=Enumerable.Repeat("Goblin",33).ToArray();
            var b=new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck,deck,actor));
            b.SetActivePlayer(actor);b.SetPhase(PhaseType.Main);return b;
        }
        private static UnitState Unit(string id, PlayerId owner, TileCoord coord, AttackType type=AttackType.Melee, bool hiding=false, bool flying=false, int attack=3)
        {
            var unit=new UnitState(id,"Goblin",owner,coord,type,attack,30,true,false,0,hasHiding:hiding,hasFlying:flying);
            unit.HasSummoningSickness=false;unit.RemainingAttacksThisTurn=1;return unit;
        }
        [TestCase(PlayerId.Player,"hidden")][TestCase(PlayerId.AI,"hidden")]
        [TestCase(PlayerId.Player,"sealed")][TestCase(PlayerId.AI,"sealed")]
        [TestCase(PlayerId.Player,"protected")][TestCase(PlayerId.AI,"protected")]
        public void SharedAttackRejectsWithExactReasonWithoutDamageOrSpendingAnAttack(PlayerId actor,string reason)
        {
            var b=Battle(actor);var opponent=b.GetOpponent(actor).Id;var attacker=Unit("attacker",actor,Source);
            var target=Unit("target",opponent,Target,hiding:reason=="hidden");if(reason=="sealed")target.EnterSealbound(3);
            b.GetBoard(actor).Place(Source,attacker);b.GetBoard(opponent).Place(Target,target);
            // Direct target state must take priority even when the rear target also has cover.
            b.GetBoard(opponent).Place(Front,Unit("front",opponent,Front));
            var expected=reason=="hidden"?"은신한 유닛은 지정할 수 없습니다.":reason=="sealed"?"봉인된 대상은 지정할 수 없습니다.":"대상은 전열의 소환물에 의해 보호받고 있습니다.";
            var resources=b.GetPlayer(actor).Resources.Clone();
            var error=Assert.Throws<InvalidOperationException>(()=>new AttackService().Attack(b,actor,Source,Target));
            Assert.That(error.Message,Is.EqualTo(expected));Assert.That(target.CurrentHp,Is.EqualTo(30));Assert.That(attacker.CurrentHp,Is.EqualTo(30));
            Assert.That(attacker.RemainingAttacksThisTurn,Is.EqualTo(1));Assert.That(b.GetPlayer(actor).Resources.Gold,Is.EqualTo(resources.Gold));Assert.That(b.ValuePopupEvents,Is.Empty);Assert.That(b.LastAttackResolution,Is.Null);
        }
        [TestCase(PlayerId.Player)][TestCase(PlayerId.AI)]
        public void SealboundSuppressesHidingAndReportsSealbound(PlayerId actor)
        {
            var b=Battle(actor);var opponent=b.GetOpponent(actor).Id;var target=Unit("target",opponent,Target,hiding:true);target.EnterSealbound(2);
            b.GetBoard(actor).Place(Source,Unit("source",actor,Source));b.GetBoard(opponent).Place(Target,target);b.GetBoard(opponent).Place(Front,Unit("front",opponent,Front));
            Assert.That(target.IsHiding,Is.False);Assert.That(new TargetingService().GetTargetRejectionMessage(b,actor,Source,Target),Is.EqualTo("봉인된 대상은 지정할 수 없습니다."));
        }
        [TestCase("empty")][TestCase("ranged")][TestCase("flying-attacker")][TestCase("drained-front")]
        [TestCase("frozen-front")][TestCase("sealed-front")][TestCase("hidden-front")][TestCase("flying-front")]
        public void LegalBypassesAndNonBlockersStillAllowActualAttack(string rule)
        {
            foreach(var actor in new[]{PlayerId.Player,PlayerId.AI})
            {
                var b=Battle(actor);var opponent=b.GetOpponent(actor).Id;
                var attacker=Unit("attacker",actor,Source,rule=="ranged"?AttackType.Ranged:AttackType.Melee,flying:rule=="flying-attacker");
                b.GetBoard(actor).Place(Source,attacker);b.GetBoard(opponent).Place(Target,Unit("target",opponent,Target));
                if(rule!="empty")
                {
                    var front=Unit("front",opponent,Front,hiding:rule=="hidden-front",flying:rule=="flying-front");
                    if(rule=="drained-front")front.IsDrained=true;if(rule=="frozen-front")front.ApplyFreeze(2);if(rule=="sealed-front")front.EnterSealbound(2);
                    b.GetBoard(opponent).Place(Front,front);
                }
                Assert.That(new TargetingService().GetTargetRejectionMessage(b,actor,Source,Target),Is.Empty);
                Assert.DoesNotThrow(()=>new AttackService().Attack(b,actor,Source,Target));Assert.That(attacker.RemainingAttacksThisTurn,Is.Zero);
            }
        }
        [Test]
        public void GenericFallbackAndEarlierAttackValidationKeepTheirOwnMessages()
        {
            var b=Battle(PlayerId.Player);var attacker=Unit("attacker",PlayerId.Player,Source,attack:0);b.PlayerBoard.Place(Source,attacker);
            b.AIBoard.Place(Target,Unit("target",PlayerId.AI,Target));
            Assert.That(new TargetingService().GetTargetRejectionMessage(b,PlayerId.Player,Source,Target),Is.EqualTo("대상은 지정 불가 상태입니다."));
            Assert.That(Assert.Throws<InvalidOperationException>(()=>new AttackService().Attack(b,PlayerId.Player,Source,Target)).Message,Is.EqualTo("공격력이 0이하인 유닛은 공격할 수 없습니다."));
            Assert.That(Assert.Throws<InvalidOperationException>(()=>new AttackService().Attack(b,PlayerId.Player,Source,new TileCoord(4,1))).Message,Is.EqualTo("공격 대상 타일이 비어있습니다."));
        }
    }
}
