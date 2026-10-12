using System;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class MirrorTrapCombatTests
    {
        private static readonly TileCoord MasterCoord = new TileCoord(2, 1);
        private static readonly TileCoord AttackerCoord = new TileCoord(0, 1);
        private static readonly TileCoord UnitCoord = new TileCoord(1, 0);

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void FreeInstallKeepsExactSecretIdentityAndCapturedManaCostWithoutUpgrade(PlayerId owner)
        {
            var state = State(owner);
            var definition = TrapMirrorFieldCardRules.CreateDefinition();
            var levels = new InMemoryCardUpgradeLevelProvider();
            levels.SetUpgradeLevel(owner, definition.CardId, 13);
            state.GetPlayer(owner).Hand.Add(definition.CardId, "first-mirror", false);
            state.GetPlayer(owner).Hand.Add(definition.CardId, "second-mirror", false);
            var service = new TrapService(new InMemoryCardDefinitionProvider(new[] { definition }), levels);
            var installed = service.Install(state, owner, definition.CardId, "first-mirror");
            Assert.That(installed.RuntimeId, Is.EqualTo("first-mirror"));
            Assert.That(installed.ActivationCost.Mana, Is.EqualTo(2));
            Assert.That(installed.UpgradeLevel, Is.Zero);
            Assert.That(state.GetPlayer(owner).Resources.Mana, Is.Zero);
            Assert.That(state.GetPlayer(owner).Discard.CardIds, Is.Empty);
            Assert.That(state.GetPlayer(owner).Hand.Cards.Single().RuntimeId, Is.EqualTo("second-mirror"));
            Assert.That(state.TrapEvents.Single().CardId, Is.Empty);
            Assert.That(state.TrapEvents.Single().RuntimeId, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => service.Install(state, owner, definition.CardId, "second-mirror"));
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
        }

        [TestCase(PlayerId.Player, false, AttackType.Melee)]
        [TestCase(PlayerId.Player, true, AttackType.Melee)]
        [TestCase(PlayerId.Player, false, AttackType.Ranged)]
        [TestCase(PlayerId.Player, true, AttackType.Ranged)]
        [TestCase(PlayerId.AI, false, AttackType.Melee)]
        [TestCase(PlayerId.AI, true, AttackType.Melee)]
        [TestCase(PlayerId.AI, false, AttackType.Ranged)]
        [TestCase(PlayerId.AI, true, AttackType.Ranged)]
        public void AcceptedAttackAtOwnUnitOrMasterIsCanceledAndOneMagicPacketHitsAttacker(
            PlayerId owner, bool masterTarget, AttackType attackType)
        {
            var state = State(owner);
            Arm(state, owner);
            state.GetPlayer(owner).Resources.Add(new ResourceSet(2, 0, 0, 0));
            var attacker = Attacker(state, Opponent(owner), attackType: attackType, magicDefense: 3);
            var target = Target(state, owner, masterTarget);
            var targetHp = target.CurrentHp;
            Attack(state, attacker, target);
            Assert.That(target.CurrentHp, Is.EqualTo(targetHp));
            Assert.That(attacker.CurrentHp, Is.EqualTo(83));
            Assert.That(attacker.RemainingAttacksThisTurn, Is.Zero);
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
            Assert.That(state.ArmedTraps, Is.Empty);
            Assert.That(state.GetPlayer(owner).Resources.Mana, Is.Zero);
            Assert.That(state.GetPlayer(owner).Discard.CardIds, Is.EqualTo(new[] { TrapMirrorFieldCardRules.CardId }));
            Assert.That(state.GetPlayer(attacker.OwnerId).Hand.Count, Is.Zero);
            var popup = state.ValuePopupEvents.Single();
            Assert.That(popup.Amount, Is.EqualTo(17));
            Assert.That(popup.DamageType, Is.EqualTo(DamageType.Magic));
            Assert.That(popup.Cause, Is.EqualTo(BattleValueChangeCause.Spell));
            Assert.That(popup.SourceOwnerId, Is.EqualTo(owner));
            Assert.That(popup.SourceCardId, Is.EqualTo(TrapMirrorFieldCardRules.CardId));
            Assert.That(popup.RuntimeId, Is.EqualTo(attacker.RuntimeId));
            var trigger = state.TrapEvents.Single();
            Assert.That(trigger.CardId, Is.EqualTo(TrapMirrorFieldCardRules.CardId));
            Assert.That(trigger.TargetRuntimeId, Is.EqualTo(attacker.RuntimeId));
            Assert.That(trigger.TargetCoord, Is.EqualTo(attacker.Position));
            Assert.That(trigger.AddedToHand, Is.False);
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void OpposingMasterAttackCanTriggerReflectionAndLoseTheBattle(PlayerId owner)
        {
            var state = State(owner);
            Arm(state, owner);
            state.GetPlayer(owner).Resources.Add(new ResourceSet(2, 0, 0, 0));
            var attacker = state.GetPlayer(Opponent(owner)).Master;
            attacker.CurrentHp = 3;
            attacker.RemainingAttacksThisTurn = 1;
            Attack(state, attacker, state.GetPlayer(owner).Master);
            Assert.That(state.GetPlayer(owner).Master.CurrentHp, Is.EqualTo(333));
            Assert.That(attacker.CurrentHp, Is.Zero);
            Assert.That(state.IsEnded, Is.True);
            Assert.That(state.Result.Winner, Is.EqualTo(owner));
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
            Assert.That(state.ValuePopupEvents.Single().Amount, Is.EqualTo(3));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BuildingsAreExcludedAsAttackersAndProtectedTargets(bool buildingAttacker)
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(2, 0, 0, 0));
            OccupantState attacker;
            OccupantState target;
            if (buildingAttacker)
            {
                attacker = new BuildingState("tower", "Tower", PlayerId.AI, AttackerCoord, true, 20, 100);
                state.AIBoard.Place(attacker.Position, attacker);
                target = state.Player.Master;
            }
            else
            {
                attacker = Attacker(state, PlayerId.AI);
                target = new BuildingState("building-target", "Tower", PlayerId.Player, UnitCoord, false, 0, 100);
                state.PlayerBoard.Place(target.Position, target);
            }
            attacker.HasSummoningSickness = false;
            attacker.RemainingAttacksThisTurn = 1;
            var targetHp = target.CurrentHp;
            Attack(state, attacker, target);
            Assert.That(target.CurrentHp, Is.EqualTo(targetHp - 20));
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.False);
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(state.Player.Resources.Mana, Is.EqualTo(2));
            Assert.That(state.TrapEvents, Is.Empty);
        }

        [TestCase(2, 5, 0, 5)]
        [TestCase(1, 2, 0, 1)]
        [TestCase(0, 2, 0, 0)]
        public void ActivationPaysManaFirstWithOneToOneGoldSubstitution(int mana, int gold, int expectedMana, int expectedGold)
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(mana, 0, 0, gold));
            var attacker = Attacker(state, PlayerId.AI);
            Attack(state, attacker, state.Player.Master);
            Assert.That(state.Player.Resources.Mana, Is.EqualTo(expectedMana));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(expectedGold));
            Assert.That(state.TrapEvents.Count, Is.EqualTo(1));
            Assert.That(attacker.CurrentHp, Is.EqualTo(80));
        }

        [Test]
        public void UnaffordableTrapRemainsHiddenUntilALaterValidAttack()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(1, 0, 0, 0));
            var attacker = Attacker(state, PlayerId.AI);
            Attack(state, attacker, state.Player.Master);
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(313));
            Assert.That(state.Player.Resources.Mana, Is.EqualTo(1));
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(state.TrapEvents, Is.Empty);
            state.Player.Resources.Add(new ResourceSet(0, 0, 0, 1));
            attacker.RemainingAttacksThisTurn = 1;
            Attack(state, attacker, state.Player.Master);
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(313));
            Assert.That(attacker.CurrentHp, Is.EqualTo(80));
            Assert.That(state.ArmedTraps, Is.Empty);
            Assert.That(state.Player.Resources.Mana, Is.Zero);
            Assert.That(state.Player.Resources.Gold, Is.Zero);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MirrorAndReturnFollowInstallationOrderAndOnlyConsumeTheFirstMatch(bool mirrorFirst)
        {
            var state = State(PlayerId.Player);
            var mirror = TrapMirrorFieldCardRules.CreateDefinition();
            var returning = TrapReturnCardRules.CreateDefinition();
            Arm(state, PlayerId.Player, mirrorFirst ? mirror : returning);
            Arm(state, PlayerId.Player, mirrorFirst ? returning : mirror);
            state.Player.Resources.Add(new ResourceSet(2, 0, 0, 4));
            var attacker = Attacker(state, PlayerId.AI);
            Attack(state, attacker, state.Player.Master);
            Assert.That(state.TrapEvents.Single().CardId, Is.EqualTo(mirrorFirst ? mirror.CardId : returning.CardId));
            Assert.That(state.ArmedTraps.Single().CardId, Is.EqualTo(mirrorFirst ? returning.CardId : mirror.CardId));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(333));
            if (mirrorFirst) Assert.That(state.AIBoard.GetOccupant(AttackerCoord), Is.SameAs(attacker));
            else Assert.That(state.AIBoard.GetOccupant(AttackerCoord), Is.Null);
            Assert.That(attacker.CurrentHp, Is.EqualTo(mirrorFirst ? 80 : 100));
            Assert.That(state.AI.Hand.Count, Is.EqualTo(mirrorFirst ? 0 : 1));
            Assert.That(state.Player.Resources.Mana, Is.EqualTo(mirrorFirst ? 0 : 2));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(mirrorFirst ? 4 : 0));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void EarlierReturnThatIsUnaffordableOrDoesNotMatchStaysArmedWhileMirrorTriggers(bool masterTarget)
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player, TrapReturnCardRules.CreateDefinition());
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(2, 0, 0, masterTarget ? 0 : 4));
            var attacker = Attacker(state, PlayerId.AI);
            Attack(state, attacker, Target(state, PlayerId.Player, masterTarget));
            Assert.That(state.ArmedTraps.Single().CardId, Is.EqualTo(TrapReturnCardRules.CardId));
            Assert.That(state.TrapEvents.Single().CardId, Is.EqualTo(TrapMirrorFieldCardRules.CardId));
            Assert.That(attacker.CurrentHp, Is.EqualTo(80));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(masterTarget ? 0 : 4));
        }

        [Test]
        public void ReflectionUsesCurrentBuffedBerserkerAttackOnceBeforeHpLoss()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(2, 0, 0, 0));
            var attacker = Attacker(state, PlayerId.AI, berserker: true);
            attacker.ApplyCombatStatBuff("attack-buff", attackBonus: 5);
            attacker.CurrentHp = 80;
            Assert.That(attacker.Attack, Is.EqualTo(45));
            Attack(state, attacker, state.Player.Master);
            Assert.That(attacker.CurrentHp, Is.EqualTo(35));
            Assert.That(attacker.Attack, Is.EqualTo(90));
            Assert.That(state.ValuePopupEvents.Single().Amount, Is.EqualTo(45));
        }

        [TestCase("active", 7)]
        [TestCase("drained", 0)]
        [TestCase("erasure", 0)]
        [TestCase("seal", 0)]
        public void ReflectionReadsTheTrapOwnersLiveSpellPowerAtTriggerNotInstallationOrAttackerPower(string sourceStatus, int bonus)
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            var source = new UnitState("power-source", "mage", PlayerId.Player, new TileCoord(4, 0),
                AttackType.Ranged, 1, 100, true, false, 0, spellPower: 7);
            state.PlayerBoard.Place(source.Position, source);
            if (sourceStatus == "drained") source.IsDrained = true;
            else if (sourceStatus == "erasure") source.ApplyErasure();
            else if (sourceStatus == "seal") source.EnterSealbound(1);
            state.Player.Resources.Add(new ResourceSet(2, 0, 0, 0));
            var attacker = Attacker(state, PlayerId.AI, magicDefense: 3, spellPower: 11, damageType: DamageType.Magic);
            Attack(state, attacker, state.Player.Master);
            Assert.That(attacker.CurrentHp, Is.EqualTo(100 - (20 + bonus - 3)));
            Assert.That(state.ValuePopupEvents.Single().Amount, Is.EqualTo(20 + bonus - 3));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(333));
        }

        [Test]
        public void CanceledMultiHitPiercingCriticalAttackHasNoCounterattackOrLifeStealAndReflectionDoesNotHalveForFlying()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(2, 0, 0, 0));
            var attacker = Attacker(state, PlayerId.AI, AttackType.Melee, hits: 3,
                lifeSteal: true, hiding: true, flying: true, piercing: true);
            attacker.CurrentHp = 50;
            var target = Target(state, PlayerId.Player, false);
            var rear = new UnitState("rear", "Golem", PlayerId.Player, new TileCoord(1, 1),
                AttackType.Melee, 50, 100, true, false, 0);
            state.PlayerBoard.Place(rear.Position, rear);
            Attack(state, attacker, target);
            Assert.That(target.CurrentHp, Is.EqualTo(100));
            Assert.That(rear.CurrentHp, Is.EqualTo(100));
            Assert.That(attacker.CurrentHp, Is.EqualTo(30));
            Assert.That(attacker.IsHiding, Is.False, "Real reflected HP loss breaks hiding.");
            Assert.That(state.ValuePopupEvents.Count, Is.EqualTo(1));
            Assert.That(state.ValuePopupEvents.Single().Amount, Is.EqualTo(20));
        }

        [TestCase("invincible", 100, false)]
        [TestCase("defense", 100, false)]
        [TestCase("endure", 1, true)]
        public void ReflectionPreservesMagicDefenseInvincibleAndEndureAndStillConsumesTheTrap(
            string protection, int expectedHp, bool expectedEndure)
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(2, 0, 0, 0));
            var attacker = Attacker(state, PlayerId.AI,
                attack: protection == "endure" ? 150 : 20,
                magicDefense: protection == "defense" ? 25 : 0,
                endure: protection == "endure");
            if (protection == "invincible") attacker.AddInvincibleEffect(InvincibleDurationType.Always);
            Attack(state, attacker, state.Player.Master);
            Assert.That(attacker.CurrentHp, Is.EqualTo(expectedHp));
            Assert.That(attacker.EndureUsed, Is.EqualTo(expectedEndure));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.True);
            Assert.That(state.ArmedTraps, Is.Empty);
            Assert.That(state.Player.Resources.Mana, Is.Zero);
            if (protection == "defense") Assert.That(state.ValuePopupEvents, Is.Empty);
            else if (protection == "invincible") Assert.That(state.ValuePopupEvents.Single().IsInvinciblePrevented, Is.True);
            else Assert.That(state.ValuePopupEvents.Single().Amount, Is.EqualTo(99));
        }

        [TestCase("drained")]
        [TestCase("seal")]
        [TestCase("stun")]
        [TestCase("freeze")]
        public void InvalidActionLockedAttackCannotSpendRevealOrConsumeTheTrap(string status)
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(2, 0, 0, 0));
            var attacker = Attacker(state, PlayerId.AI);
            if (status == "drained") attacker.IsDrained = true;
            else if (status == "seal") attacker.EnterSealbound(1);
            else if (status == "stun") attacker.ApplyStun(1);
            else attacker.ApplyFreeze(1);
            PrepareAttack(state, attacker.OwnerId);
            Assert.Throws<InvalidOperationException>(() =>
                new AttackService().Attack(state, attacker.OwnerId, attacker.Position, MasterCoord));
            Assert.That(state.Player.Resources.Mana, Is.EqualTo(2));
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(1));
            Assert.That(state.TrapEvents, Is.Empty);
            Assert.That(state.LastAttackResolution, Is.Null);
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(333));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ManaCannonCostIsPaidExactlyOnceBeforeItsActionIsReflected(bool goldOnly)
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(2, 0, 0, 0));
            state.AI.Resources.Add(goldOnly ? new ResourceSet(0, 0, 0, 1) : new ResourceSet(1, 0, 0, 0));
            var attacker = Attacker(state, PlayerId.AI, cardId: ManaCannonRules.CardId, hits: 3);
            Attack(state, attacker, state.Player.Master);
            Assert.That(state.AI.Resources.Mana, Is.Zero);
            Assert.That(state.AI.Resources.Gold, Is.Zero);
            Assert.That(state.Player.Resources.Mana, Is.Zero);
            Assert.That(attacker.CurrentHp, Is.EqualTo(80));
            Assert.That(state.TrapEvents.Count, Is.EqualTo(1));
            Assert.That(state.ValuePopupEvents.Count, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void HuanShuUsesItsActualTargetAndAnAlliedRedirectDoesNotTriggerTheOpponentTrap(int selected)
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(2, 0, 0, 0));
            var target = Target(state, PlayerId.Player, false);
            var attacker = Attacker(state, PlayerId.AI);
            attacker.ApplyHuanShu();
            PrepareAttack(state, PlayerId.AI);
            var calls = 0;
            new AttackService(new TargetingService(), count =>
            {
                Assert.That(count, Is.EqualTo(3));
                calls++;
                return selected;
            }).Attack(state, PlayerId.AI, attacker.Position, MasterCoord);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(state.LastAttackResolution.WasHuanShuRedirected, Is.EqualTo(selected != 1));
            Assert.That(state.LastAttackResolution.WasCancelledByTrap, Is.EqualTo(selected != 2));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(target.CurrentHp, Is.EqualTo(100));
            Assert.That(attacker.CurrentHp, Is.EqualTo(selected == 2 ? 100 : 80));
            Assert.That(state.ArmedTraps.Count, Is.EqualTo(selected == 2 ? 1 : 0));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(selected == 2 ? 313 : 333));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LethalReflectionUsesOrdinaryDestructionOrDemonKingRevivalRatherThanReturningACard(bool demonKing)
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(2, 0, 0, 0));
            var attacker = Attacker(state, PlayerId.AI, cardId: demonKing ? DemonKingRules.CardId : "Golem");
            attacker.CurrentHp = 10;
            Attack(state, attacker, state.Player.Master);
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(state.AI.Hand.Count, Is.Zero);
            Assert.That(state.TrapEvents.Single().AddedToHand, Is.False);
            Assert.That(state.ValuePopupEvents.Single().Amount, Is.EqualTo(10));
            if (demonKing)
            {
                Assert.That(state.AIBoard.GetOccupant(attacker.Position), Is.SameAs(attacker));
                Assert.That(attacker.IsDemonKingRevivalPending, Is.True);
                Assert.That(attacker.IsSealbound, Is.True);
            }
            else Assert.That(state.AIBoard.GetOccupant(attacker.Position), Is.Null);
        }

        [Test]
        public void ReflectionCancelsBeforeProtectedTargetTransferAndHitsAttackerItselfWithoutItsShielder()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(2, 0, 0, 0));
            var protectedShield = new UnitState("protected-shield", "Shieldbearer", PlayerId.Player,
                new TileCoord(2, 0), AttackType.Melee, 50, 100, true, false, 0, hasShielder: true);
            var attackingShield = new UnitState("attacking-shield", "Shieldbearer", PlayerId.AI,
                new TileCoord(0, 0), AttackType.Melee, 50, 100, true, false, 0, hasShielder: true);
            state.PlayerBoard.Place(protectedShield.Position, protectedShield);
            state.AIBoard.Place(attackingShield.Position, attackingShield);
            var attacker = Attacker(state, PlayerId.AI);
            Attack(state, attacker, state.Player.Master);
            Assert.That(protectedShield.CurrentHp, Is.EqualTo(100));
            Assert.That(attackingShield.CurrentHp, Is.EqualTo(100));
            Assert.That(attacker.CurrentHp, Is.EqualTo(80));
            Assert.That(state.ValuePopupEvents.Single().RuntimeId, Is.EqualTo(attacker.RuntimeId));
        }

        [Test]
        public void TransferredUnitIsReflectedUsingItsCurrentOwnerAndCurrentGoldBasedAttack()
        {
            var state = State(PlayerId.Player);
            Arm(state, PlayerId.Player);
            state.Player.Resources.Add(new ResourceSet(2, 0, 0, 4));
            state.AI.Resources.Add(new ResourceSet(0, 0, 0, 3));
            var attacker = Attacker(state, PlayerId.Player, cardId: GaebangChiefRules.CardId, attack: 10);
            state.PlayerBoard.Remove(attacker.Position);
            attacker.TransferOwnership(PlayerId.AI);
            state.AIBoard.Place(attacker.Position, attacker);
            Assert.That(attacker.Attack, Is.EqualTo(40));
            Attack(state, attacker, state.Player.Master);
            Assert.That(attacker.CurrentHp, Is.EqualTo(60));
            Assert.That(state.TrapEvents.Single().TargetOwnerId, Is.EqualTo(PlayerId.AI));
            Assert.That(state.ValuePopupEvents.Single().Amount, Is.EqualTo(40));
            Assert.That(state.AI.Resources.Gold, Is.EqualTo(3));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(4));
        }

        private static BattleState State(PlayerId owner)
        {
            var playerMaster = new MasterState("master-player", PlayerId.Player, MasterCoord, 3, 333);
            var aiMaster = new MasterState("master-ai", PlayerId.AI, MasterCoord, 3, 333);
            var player = new PlayerState(PlayerId.Player, new ResourceSet(), new DeckState(Array.Empty<string>()),
                new HandState(), new DiscardState(), playerMaster);
            var ai = new PlayerState(PlayerId.AI, new ResourceSet(), new DeckState(Array.Empty<string>()),
                new HandState(), new DiscardState(), aiMaster);
            var playerBoard = new BoardState(); playerBoard.Place(MasterCoord, playerMaster);
            var aiBoard = new BoardState(); aiBoard.Place(MasterCoord, aiMaster);
            var state = new BattleState(player, ai, playerBoard, aiBoard);
            state.RestoreRuntimeState(5, owner, PhaseType.Main);
            return state;
        }

        private static void Arm(BattleState state, PlayerId owner, TrapSpellCardDefinition definition = null)
        {
            definition = definition ?? TrapMirrorFieldCardRules.CreateDefinition();
            state.SetActivePlayer(owner);
            state.GetPlayer(owner).Hand.Add(definition.CardId, definition.CardId + "-armed", false);
            new TrapService(new InMemoryCardDefinitionProvider(new[] { definition }))
                .Install(state, owner, definition.CardId, definition.CardId + "-armed");
        }

        private static UnitState Attacker(BattleState state, PlayerId owner, AttackType attackType = AttackType.Ranged,
            int magicDefense = 0, int attack = 20, string cardId = "Golem", int hits = 1, bool berserker = false,
            bool endure = false, bool lifeSteal = false, bool hiding = false, bool flying = false, bool piercing = false,
            int spellPower = 0, DamageType damageType = DamageType.Physical)
        {
            var result = new UnitState("attacker", cardId, owner, AttackerCoord, attackType, attack, 100,
                true, false, 0, hitsPerAttack: hits, hasBerserker: berserker, hasEndure: endure,
                hasLifeSteal: lifeSteal, damageType: damageType, magicDefense: magicDefense,
                hasHiding: hiding, hasFlying: flying, spellPower: spellPower, hasPiercing: piercing);
            result.HasSummoningSickness = false;
            result.RemainingAttacksThisTurn = 1;
            state.GetBoard(owner).Place(result.Position, result);
            return result;
        }

        private static OccupantState Target(BattleState state, PlayerId owner, bool master)
        {
            if (master) return state.GetPlayer(owner).Master;
            var result = new UnitState("target", "Golem", owner, UnitCoord, AttackType.Melee, 50, 100, true, false, 0);
            state.GetBoard(owner).Place(result.Position, result);
            return result;
        }

        private static void PrepareAttack(BattleState state, PlayerId attackerOwner)
        {
            state.SetActivePlayer(attackerOwner);
            state.ClearTrapEvents();
            state.ClearValuePopupEvents();
        }

        private static void Attack(BattleState state, OccupantState attacker, OccupantState target)
        {
            PrepareAttack(state, attacker.OwnerId);
            new AttackService().Attack(state, attacker.OwnerId, attacker.Position, target.Position);
        }

        private static PlayerId Opponent(PlayerId owner) => owner == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
    }
}
