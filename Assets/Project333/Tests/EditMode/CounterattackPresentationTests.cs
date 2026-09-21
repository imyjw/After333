using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Presentation.Battle;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class CounterattackPresentationTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly TileCoord MasterCoord = new TileCoord(2, 1);
        private GameObject _root;
        private BattleBootstrapper _bootstrapper;

        [SetUp]
        public void SetUp()
        {
            // Inactive hierarchy avoids scene startup, network access and actual game input.
            _root = new GameObject("CounterattackPresentationTest");
            _root.SetActive(false);
            _bootstrapper = _root.AddComponent<BattleBootstrapper>();
            var flow = new BattleFlowController();
            var gateway = new LocalBattleGateway(flow);
            var deck = Enumerable.Repeat("unknown", 20).ToArray();
            gateway.StartBattle(new BattleSetupRequest(deck, deck, PlayerId.Player));
            typeof(BattleBootstrapper).GetField("_battleGateway", PrivateInstance).SetValue(_bootstrapper, gateway);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        public void OnlineAttack_KeepsEveryCounterattackHitIncludingInvincibleFeedback(int hitCount, bool invincible)
        {
            var events = new List<BattleEventDto>
            {
                Damage(PlayerId.AI, BattleValueChangeCause.NormalAttack, 20, 17),
            };
            for (var hit = 0; hit < hitCount; hit++)
            {
                var counter = Damage(PlayerId.Player, BattleValueChangeCause.Counterattack, 30 - hit * 4, 26 - hit * 4);
                if (invincible)
                {
                    counter.EventType = BattleEventType.InvinciblePrevented;
                    counter.Amount = 0;
                    counter.HpBefore = 30;
                    counter.HpAfter = 30;
                }
                events.Add(counter);
            }

            var request = BuildRequest(events);
            var popups = Get<List<BattleValuePopupEvent>>(request, "ValuePopupEvents");
            var counters = popups.Where(popup => popup.Cause == BattleValueChangeCause.Counterattack).ToArray();

            Assert.That(Get<bool>(request, "DefenderCounterattacked"), Is.True);
            Assert.That(Get<bool>(request, "AttackerTookDamage"), Is.EqualTo(!invincible));
            Assert.That(popups.Count, Is.EqualTo(hitCount + 1));
            Assert.That(counters.Length, Is.EqualTo(hitCount));
            for (var hit = 0; hit < hitCount; hit++)
            {
                Assert.That(counters[hit].Amount, Is.EqualTo(invincible ? 0 : 4));
                Assert.That(counters[hit].IsInvinciblePrevented, Is.EqualTo(invincible));
                Assert.That(counters[hit].HpBefore, Is.EqualTo(invincible ? 30 : 30 - hit * 4));
                Assert.That(counters[hit].HpAfter, Is.EqualTo(invincible ? 30 : 26 - hit * 4));
            }
        }

        [Test]
        public void OnlineAttack_DestructionDamageToAttackerDoesNotAnimateACounterattack()
        {
            var request = BuildRequest(new[]
            {
                Damage(PlayerId.AI, BattleValueChangeCause.NormalAttack, 5, 0),
                Damage(PlayerId.Player, BattleValueChangeCause.NuclearPowerPlant, 33, 3),
            });

            Assert.That(Get<bool>(request, "DefenderCounterattacked"), Is.False);
            Assert.That(Get<bool>(request, "AttackerTookDamage"), Is.True);
            Assert.That(Get<List<BattleValuePopupEvent>>(request, "ValuePopupEvents").Count, Is.EqualTo(2));
        }

        [Test]
        public void OnlineAttack_CounterattackFeedbackForAnotherTargetDoesNotMarkThisAttacker()
        {
            var request = BuildRequest(new[]
            {
                Damage(PlayerId.AI, BattleValueChangeCause.Counterattack, 30, 26),
            });

            Assert.That(Get<bool>(request, "DefenderCounterattacked"), Is.False);
            Assert.That(Get<bool>(request, "AttackerTookDamage"), Is.False);
        }

        private object BuildRequest(IReadOnlyList<BattleEventDto> events)
        {
            var attack = new BattleEventDto
            {
                EventType = BattleEventType.AttackStarted,
                SourceOwnerId = PlayerId.Player,
                SourceCoord = TileCoordDto.FromDomain(MasterCoord),
                TargetOwnerId = PlayerId.AI,
                TargetCoord = TileCoordDto.FromDomain(MasterCoord),
            };
            var request = typeof(BattleBootstrapper).GetMethod("BuildOnlineAttackAnimationRequest", PrivateInstance)
                .Invoke(_bootstrapper, new object[] { attack, events });
            Assert.That(request, Is.Not.Null);
            return request;
        }

        private static BattleEventDto Damage(PlayerId targetOwnerId, BattleValueChangeCause cause, int hpBefore, int hpAfter)
        {
            return new BattleEventDto
            {
                EventType = BattleEventType.DamageApplied,
                SourceOwnerId = targetOwnerId == PlayerId.Player ? PlayerId.AI : PlayerId.Player,
                TargetOwnerId = targetOwnerId,
                TargetCoord = TileCoordDto.FromDomain(MasterCoord),
                RuntimeId = targetOwnerId + "-master",
                SourceRuntimeId = targetOwnerId == PlayerId.Player ? "ai-master" : "player-master",
                SourceCardId = "Master",
                ValueCause = cause,
                DamageType = DamageType.Physical,
                Amount = hpBefore - hpAfter,
                HpBefore = hpBefore,
                HpAfter = hpAfter,
            };
        }

        private static T Get<T>(object request, string propertyName)
        {
            return (T)request.GetType().GetProperty(propertyName).GetValue(request);
        }
    }
}
