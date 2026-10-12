using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class ManaCannonTests
    {
        private const string CardId = ManaCannonRules.CardId;
        private static JsonCardDefinitionDatabase Database() => JsonCardDefinitionDatabase.FromJson(File.ReadAllText(
            Path.Combine(Application.dataPath, "Project333/Resources/Project333/Data/cards.json")));

        [Test]
        public void JsonProvider_RegistersConfirmedRacelessRangedMagicUnitAndAttackCost()
        {
            var database = Database(); var record = database.Cards.Single(card => card.Id == CardId);
            var definition = (UnitCardDefinition)database.CreateProvider().GetRequired(CardId);
            Assert.That(CardDatabaseValidator.Validate(database).Errors, Is.Empty);
            Assert.That(record.DisplayName, Is.EqualTo("마나 대포")); Assert.That(record.Rarity, Is.EqualTo(CardRarity.Uncommon));
            Assert.That(record.Affiliation, Is.EqualTo(CardAffiliation.Fantasy)); Assert.That(record.DefinitionType, Is.EqualTo(JsonCardDefinitionKind.Unit));
            Assert.That(record.IncludeInDraft && record.IncludeInRewards, Is.True); Assert.That(record.EffectText, Is.EqualTo(ManaCannonRules.EffectDescription));
            Assert.That(record.SpecialEffectText, Is.Empty); Assert.That(record.EffectId, Is.Empty); Assert.That(definition.Races, Is.Empty);
            Assert.That(definition.Cost.Mana, Is.EqualTo(2)); Assert.That(definition.Cost.Qi + definition.Cost.Power + definition.Cost.Gold, Is.Zero);
            Assert.That(definition.Attack, Is.EqualTo(40)); Assert.That(definition.Health, Is.EqualTo(10));
            Assert.That(definition.AttackType, Is.EqualTo(AttackType.Ranged)); Assert.That(definition.DamageType, Is.EqualTo(DamageType.Magic));
            Assert.That(definition.PhysicalDefense + definition.MagicDefense, Is.Zero); Assert.That(definition.CanMove, Is.True);
            Assert.That(definition.HitsPerAttack, Is.EqualTo(1)); Assert.That(definition.MaxAttacksPerTurn, Is.EqualTo(1));
            Assert.That(CardUpgradeRules.IsCardUpgradeable(definition), Is.True);
            Assert.That(definition.HasRush || definition.HasReplicate || definition.HasBerserker || definition.HasEndure ||
                definition.HasShielder || definition.HasLifeSteal || definition.HasHiding || definition.HasFlying || definition.HasPiercing, Is.False);
        }

        [TestCase(0, 40, 10)]
        [TestCase(1, 40, 11)]
        [TestCase(2, 40, 12)]
        [TestCase(3, 41, 12)]
        [TestCase(4, 41, 13)]
        [TestCase(5, 41, 14)]
        [TestCase(6, 42, 14)]
        [TestCase(7, 42, 15)]
        [TestCase(8, 42, 16)]
        [TestCase(9, 43, 16)]
        [TestCase(10, 43, 17)]
        [TestCase(11, 43, 18)]
        [TestCase(12, 43, 19)]
        [TestCase(13, 44, 19)]
        public void ActualSummonAndRangedMagicAttack_ForBothOwnersAndManaOrGoldPayments(int level, int attack, int hp)
        {
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            foreach (var goldPayment in new[] { false, true })
            {
                var cannon = Summon(level, owner, out var state);
                Assert.That(cannon.Attack, Is.EqualTo(attack)); Assert.That(cannon.OriginalAttack, Is.EqualTo(attack));
                Assert.That(cannon.MaxHp, Is.EqualTo(hp)); Assert.That(cannon.OriginalMaxHp, Is.EqualTo(hp)); Assert.That(cannon.CurrentHp, Is.EqualTo(hp));
                Assert.That(cannon.Races, Is.Empty); Assert.That(cannon.HasSummoningSickness, Is.True);
                Assert.That(state.GetPlayer(owner).Resources.Mana + state.GetPlayer(owner).Resources.Gold, Is.Zero);
                Assert.That(state.GetPlayer(owner).Hand.Contains(CardId, "cannon-selected"), Is.False);
                Assert.That(state.GetPlayer(owner).Hand.Contains(CardId, "cannon-reserve"), Is.True);
                Assert.That(CardStatDisplay.TryCreate(Database().CreateProvider().GetRequired(CardId), level, out var displayed), Is.True);
                Assert.That(displayed.Attack, Is.EqualTo(attack)); Assert.That(displayed.Hp, Is.EqualTo(hp));
                cannon.HasSummoningSickness = false;
                state.GetPlayer(owner).Resources.Add(goldPayment ? new ResourceSet(0, 0, 0, 1) : new ResourceSet(1, 0, 0, 0));
                var target = Victim(state, owner, magicDefense: 3, physicalDefense: 40);
                new AttackService().Attack(state, owner, cannon.Position, target.Position);
                Assert.That(target.CurrentHp, Is.EqualTo(100 - attack + 3)); Assert.That(cannon.CurrentHp, Is.EqualTo(hp));
                Assert.That(cannon.RemainingAttacksThisTurn, Is.Zero); Assert.That(state.GetPlayer(owner).Resources.Mana + state.GetPlayer(owner).Resources.Gold, Is.Zero);
                Assert.That(state.ValuePopupEvents.Count, Is.EqualTo(1));
                Assert.That(state.ValuePopupEvents.Single().Cause, Is.EqualTo(BattleValueChangeCause.NormalAttack));
                Assert.That(state.ValuePopupEvents.Single().Amount, Is.EqualTo(attack - 3));
                Assert.That(state.ValuePopupEvents.Single().DamageType, Is.EqualTo(DamageType.Magic));
            }
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void ManaIsUsedBeforeGold_AndEverySeparateAttackPaysOnce(PlayerId owner)
        {
            var cannon = Summon(0, owner, out var state); cannon.HasSummoningSickness = false;
            state.GetPlayer(owner).Resources.Add(new ResourceSet(2, 0, 0, 3)); var target = Victim(state, owner, hp: 500);
            var service = new AttackService();
            for (var i = 0; i < 5; i++)
            {
                cannon.RemainingAttacksThisTurn = 1; service.Attack(state, owner, cannon.Position, target.Position);
                Assert.That(state.GetPlayer(owner).Resources.Mana, Is.EqualTo(Math.Max(0, 1 - i)));
                Assert.That(state.GetPlayer(owner).Resources.Gold, Is.EqualTo(Math.Min(3, 4 - i)));
            }
            cannon.RemainingAttacksThisTurn = 1;
            Assert.That(() => service.Attack(state, owner, cannon.Position, target.Position), Throws.InvalidOperationException.With.Message.EqualTo("자원이 부족합니다."));
            Assert.That(target.CurrentHp, Is.EqualTo(300)); Assert.That(cannon.RemainingAttacksThisTurn, Is.EqualTo(1));
        }

        [TestCase("mana")]
        [TestCase("empty")]
        [TestCase("sealed-target")]
        [TestCase("hidden-target")]
        [TestCase("sickness")]
        [TestCase("used")]
        [TestCase("drained")]
        [TestCase("sealed")]
        [TestCase("phase")]
        [TestCase("turn")]
        public void RejectedAttack_KeepsResourcesUsageHidingHpAndAttackHistoryUntouched(string reason)
        {
            var cannon = Summon(0, PlayerId.Player, out var state); cannon.HasSummoningSickness = false;
            var target = Victim(state, PlayerId.Player); var coord = target.Position;
            if (reason != "mana") state.Player.Resources.Add(new ResourceSet(1, 2, 3, 4));
            switch (reason)
            {
                case "empty": coord = new TileCoord(4, 0); break;
                case "sealed-target": target.EnterSealbound(2); break;
                case "hidden-target":
                    state.AIBoard.Remove(target.Position);
                    target = new UnitState("hidden", "fixture", PlayerId.AI, coord, AttackType.Melee, 7, 100, true, false, 0, hasHiding: true);
                    state.AIBoard.Place(coord, target); break;
                case "sickness": cannon.HasSummoningSickness = true; break;
                case "used": cannon.RemainingAttacksThisTurn = 0; break;
                case "drained": cannon.IsDrained = true; break;
                case "sealed": cannon.EnterSealbound(2); break;
                case "phase": state.SetPhase(PhaseType.TurnStart); break;
                case "turn": state.SetActivePlayer(PlayerId.AI); break;
            }
            var before = AiBattleStateCopy.PositionKey(state); var attacks = cannon.RemainingAttacksThisTurn;
            var mana = state.Player.Resources.Mana; var gold = state.Player.Resources.Gold;
            Assert.That(() => new AttackService().Attack(state, PlayerId.Player, cannon.Position, coord), Throws.InvalidOperationException);
            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(before)); Assert.That(cannon.RemainingAttacksThisTurn, Is.EqualTo(attacks));
            Assert.That(state.Player.Resources.Mana, Is.EqualTo(mana)); Assert.That(state.Player.Resources.Gold, Is.EqualTo(gold));
            Assert.That(target.CurrentHp, Is.EqualTo(100)); Assert.That(state.LastAttackResolution, Is.Null); Assert.That(state.ValuePopupEvents, Is.Empty);
        }

        [Test]
        public void UnaffordableHuanShuAttack_DoesNotChooseRandomTargetOrMutateAnything()
        {
            var cannon = Summon(0, PlayerId.Player, out var state); cannon.HasSummoningSickness = false; cannon.ApplyHuanShu();
            var target = Victim(state, PlayerId.Player); var calls = 0;
            var service = new AttackService(new TargetingService(), count => { calls++; return 0; });
            var before = AiBattleStateCopy.PositionKey(state);
            Assert.That(() => service.Attack(state, PlayerId.Player, cannon.Position, target.Position), Throws.InvalidOperationException.With.Message.EqualTo("자원이 부족합니다."));
            Assert.That(calls, Is.Zero); Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(before));
            Assert.That(state.LastAttackResolution, Is.Null); Assert.That(state.ValuePopupEvents, Is.Empty);
        }

        [Test]
        public void InvalidHuanShuRandomResult_DoesNotChargeAfterDeclaredTargetValidation()
        {
            var cannon = Summon(0, PlayerId.Player, out var state); cannon.HasSummoningSickness = false; cannon.ApplyHuanShu();
            state.Player.Resources.Add(new ResourceSet(1, 0, 0, 1)); var target = Victim(state, PlayerId.Player);
            var before = AiBattleStateCopy.PositionKey(state);
            Assert.That(() => new AttackService(new TargetingService(), count => count).Attack(state, PlayerId.Player, cannon.Position, target.Position), Throws.InvalidOperationException);
            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(before)); Assert.That(state.LastAttackResolution, Is.Null);
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void HuanShuRedirectedAttack_ChargesCurrentAttackerOwnerOnly(PlayerId owner)
        {
            var cannon = Summon(0, owner, out var state); cannon.HasSummoningSickness = false; cannon.ApplyHuanShu();
            state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 0, 1));
            var target = Victim(state, owner); var other = state.GetOpponent(owner);
            var beforeOtherMana = other.Resources.Mana; var beforeOtherGold = other.Resources.Gold;
            new AttackService(new TargetingService(), count => 0).Attack(state, owner, cannon.Position, target.Position);
            Assert.That(state.GetPlayer(owner).Resources.Gold, Is.Zero);
            Assert.That(other.Resources.Mana, Is.EqualTo(beforeOtherMana)); Assert.That(other.Resources.Gold, Is.EqualTo(beforeOtherGold));
            Assert.That(state.LastAttackResolution, Is.Not.Null); Assert.That(cannon.RemainingAttacksThisTurn, Is.Zero);
        }

        [Test]
        public void MultiHitAttack_ChargesOneManaForWholeAction_AndEachMagicHitUsesSpellPower()
        {
            var state = State(PlayerId.Player); SetResources(state.Player.Resources, 1, 0);
            var cannon = new UnitState("multi", CardId, PlayerId.Player, new TileCoord(0, 0), AttackType.Ranged,
                50, 10, true, false, 0, hitsPerAttack: 3, damageType: DamageType.Magic);
            cannon.HasSummoningSickness = false; state.PlayerBoard.Place(cannon.Position, cannon);
            var support = new UnitState("spell-support", "fixture", PlayerId.Player, new TileCoord(1, 0), AttackType.Melee,
                1, 10, true, false, 0, spellPower: 4); state.PlayerBoard.Place(support.Position, support);
            var target = Victim(state, PlayerId.Player, hp: 500, magicDefense: 3, physicalDefense: 40);
            new AttackService().Attack(state, PlayerId.Player, cannon.Position, target.Position);
            Assert.That(state.Player.Resources.Mana, Is.Zero); Assert.That(state.Player.Resources.Gold, Is.Zero);
            Assert.That(target.CurrentHp, Is.EqualTo(347)); Assert.That(cannon.CurrentHp, Is.EqualTo(10));
            Assert.That(state.ValuePopupEvents.Count(evt => evt.Cause == BattleValueChangeCause.NormalAttack), Is.EqualTo(3));
            Assert.That(state.ValuePopupEvents.All(evt => evt.Amount == 51), Is.True);
        }

        [Test]
        public void ValidAttackAgainstInvincibleTarget_StillPaysOneManaWithoutDamageOrCounter()
        {
            var cannon = Summon(0, PlayerId.Player, out var state); cannon.HasSummoningSickness = false;
            state.Player.Resources.Add(new ResourceSet(1, 0, 0, 0)); var target = Victim(state, PlayerId.Player);
            target.AddInvincibleEffect(InvincibleDurationType.OwnerTurns, 2);
            new AttackService().Attack(state, PlayerId.Player, cannon.Position, target.Position);
            Assert.That(state.Player.Resources.Mana, Is.Zero); Assert.That(cannon.RemainingAttacksThisTurn, Is.Zero);
            Assert.That(target.CurrentHp, Is.EqualTo(100)); Assert.That(cannon.CurrentHp, Is.EqualTo(10));
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void Erasure_SuppressesAttackCostButPreservesPrintedCombatAndNoRace(PlayerId owner)
        {
            var cannon = Summon(13, owner, out var state); cannon.HasSummoningSickness = false;
            cannon.ApplyCombatStatBuff("fixture", attackBonus: 7, maxHpBonus: 3); cannon.ApplyErasure(); cannon.HasSummoningSickness = false;
            Assert.That(ManaCannonRules.RequiresAttackPayment(cannon), Is.False);
            var target = Victim(state, owner); new AttackService().Attack(state, owner, cannon.Position, target.Position);
            Assert.That(target.CurrentHp, Is.EqualTo(56)); Assert.That(cannon.Attack, Is.EqualTo(44)); Assert.That(cannon.MaxHp, Is.EqualTo(19));
            Assert.That(cannon.Races, Is.Empty); Assert.That(state.GetPlayer(owner).Resources.Mana + state.GetPlayer(owner).Resources.Gold, Is.Zero);
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void GuTransfer_UsesNewOwnersResourcesAndOriginalUpgradedBaseline(PlayerId owner)
        {
            var cannon = Summon(13, owner, out var state); var controller = state.GetOpponent(owner).Id;
            state.GetPlayer(owner).Resources.Add(new ResourceSet(3, 0, 0, 2));
            SetResources(state.GetPlayer(controller).Resources, 1, 0); state.GetPlayer(controller).Resources.Add(new ResourceSet(0, 10, 0, 0));
            state.GetPlayer(controller).Hand.Add(GuRules.CardId, "gu-selected", false); state.SetActivePlayer(controller);
            new SpellService(Database().CreateProvider()).CastScriptedSpell(state, controller, GuRules.CardId, owner, cannon.Position, "gu-selected");
            // Gu grants no immediate attack without Rush; observe a later eligible attack window.
            cannon.HasSummoningSickness = false; cannon.RemainingAttacksThisTurn = 1; var target = Victim(state, controller);
            new AttackService().Attack(state, controller, cannon.Position, target.Position);
            Assert.That(cannon.OwnerId, Is.EqualTo(controller)); Assert.That(cannon.Attack, Is.EqualTo(44)); Assert.That(cannon.MaxHp, Is.EqualTo(19));
            Assert.That(cannon.Races, Is.Empty); Assert.That(state.GetPlayer(controller).Resources.Mana, Is.Zero);
            Assert.That(state.GetPlayer(owner).Resources.Mana, Is.EqualTo(3)); Assert.That(state.GetPlayer(owner).Resources.Gold, Is.EqualTo(2));
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void AiCopyAndOppositeViewerProjection_PreserveResourcesCostAndIndependentExecution(PlayerId owner)
        {
            var cannon = Summon(13, owner, out var state); cannon.HasSummoningSickness = false;
            state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 0, 1)); var target = Victim(state, owner);
            var clone = AiBattleStateCopy.Create(state); var copied = clone.GetBoard(owner).GetOccupant(cannon.Position);
            new AttackService().Attack(clone, owner, copied.Position, target.Position);
            Assert.That(clone.GetPlayer(owner).Resources.Gold, Is.Zero); Assert.That(state.GetPlayer(owner).Resources.Gold, Is.EqualTo(1));
            Assert.That(target.CurrentHp, Is.EqualTo(100)); Assert.That(cannon.RemainingAttacksThisTurn, Is.EqualTo(1));
            var view = new BattleStateViewFactory().CreateForPlayer(state, "cannon", state.GetOpponent(owner).Id);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(view); projected.SetActivePlayer(PlayerId.AI);
            var received = projected.AIBoard.GetOccupant(cannon.Position);
            Assert.That(received.RuntimeId, Is.EqualTo(cannon.RuntimeId)); Assert.That(received.Attack, Is.EqualTo(44));
            Assert.That(((UnitState)received).Races, Is.Empty); Assert.That(projected.AI.Resources.Gold, Is.EqualTo(1));
            new AttackService().Attack(projected, PlayerId.AI, received.Position, target.Position);
            Assert.That(projected.AI.Resources.Gold, Is.Zero); Assert.That(state.GetPlayer(owner).Resources.Gold, Is.EqualTo(1));
        }

        [Test]
        public void AiCandidatesAndLookahead_ShareAttackAffordabilityAndDoNotReuseSpentMana()
        {
            var cannon = Summon(0, PlayerId.AI, out var state); cannon.HasSummoningSickness = false;
            var second = new UnitState("second-cannon", CardId, PlayerId.AI, new TileCoord(1, 0), AttackType.Ranged, 50, 10, true, false, 0, damageType: DamageType.Magic);
            second.HasSummoningSickness = false; state.AIBoard.Place(second.Position, second); state.AI.Master.RemainingAttacksThisTurn = 0;
            while (state.AI.Hand.Count > 0) state.AI.Hand.Remove(state.AI.Hand.CardIds[0]);
            var planner = new AiDecisionService(Database().CreateProvider());
            var generator = typeof(AiDecisionService).GetField("_generator", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(planner);
            var generate = generator.GetType().GetMethod("Generate");
            Func<BattleState, object[]> attacks = current => ((IEnumerable)generate.Invoke(generator, new object[] { current })).Cast<object>()
                .Where(action => ((IBattleCommand[])action.GetType().GetField("Commands").GetValue(action))[0] is AttackCommand).ToArray();
            Assert.That(attacks(state), Is.Empty);
            SetResources(state.AI.Resources, 1, 0); var available = attacks(state);
            var first = available.Single(action => ((AttackCommand)((IBattleCommand[])action.GetType().GetField("Commands").GetValue(action))[0]).AttackerCoord == cannon.Position);
            var next = available.Single(action => ((AttackCommand)((IBattleCommand[])action.GetType().GetField("Commands").GetValue(action))[0]).AttackerCoord == second.Position);
            var simulation = typeof(AiDecisionService).GetField("_simulation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(planner);
            var apply = simulation.GetType().GetMethod("Apply"); var before = AiBattleStateCopy.PositionKey(state);
            var after = (BattleState)apply.Invoke(simulation, new[] { (object)state, first, 0 });
            Assert.That(after, Is.Not.Null); Assert.That(after.AI.Resources.Mana, Is.Zero); Assert.That(attacks(after), Is.Empty);
            Assert.That(apply.Invoke(simulation, new[] { (object)after, next, 0 }), Is.Null);
            Assert.That(AiBattleStateCopy.PositionKey(state), Is.EqualTo(before)); Assert.That(state.AI.Resources.Mana, Is.EqualTo(1));
            SetResources(state.AI.Resources, 0, 1); Assert.That(attacks(state), Is.Not.Empty);
            SetResources(state.AI.Resources, 0, 0); cannon.ApplyErasure(); cannon.HasSummoningSickness = false;
            Assert.That(attacks(state).Length, Is.EqualTo(1));
        }

        [Test]
        public void ShielderAndOverflow_RemainOneAttackPaymentWithOrdinaryDamageRouting()
        {
            var cannon = Summon(0, PlayerId.Player, out var state); cannon.HasSummoningSickness = false;
            state.Player.Resources.Add(new ResourceSet(1, 0, 0, 0));
            var target = new UnitState("protected", "fixture", PlayerId.AI, new TileCoord(0, 1), AttackType.Melee, 7, 100, true, false, 0);
            var shielder = new UnitState("shielder", "fixture", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 0, 30, true, false, 0, hasShielder: true);
            state.AIBoard.Place(target.Position, target); state.AIBoard.Place(shielder.Position, shielder);
            new AttackService().Attack(state, PlayerId.Player, cannon.Position, target.Position);
            Assert.That(state.Player.Resources.Mana, Is.Zero); Assert.That(state.Player.Resources.Gold, Is.Zero);
            Assert.That(target.CurrentHp, Is.EqualTo(90)); Assert.That(state.AIBoard.GetOccupant(shielder.Position), Is.Null);
            Assert.That(state.ValuePopupEvents.Count(evt => evt.Cause == BattleValueChangeCause.NormalAttack), Is.EqualTo(2));
        }

        [Test]
        public void OtherCards_DoNotAcquireAnAttackCost()
        {
            var state = State(PlayerId.Player); SetResources(state.Player.Resources, 0, 0);
            var attacker = new UnitState("ordinary", "Goblin", PlayerId.Player, new TileCoord(0, 0), AttackType.Ranged, 5, 10, true, false, 0);
            attacker.HasSummoningSickness = false; state.PlayerBoard.Place(attacker.Position, attacker); var target = Victim(state, PlayerId.Player);
            new AttackService().Attack(state, PlayerId.Player, attacker.Position, target.Position);
            Assert.That(target.CurrentHp, Is.EqualTo(95)); Assert.That(state.Player.Resources.Mana + state.Player.Resources.Gold, Is.Zero);
        }

        [TestCase("cost")]
        [TestCase("race")]
        [TestCase("attackType")]
        [TestCase("damageType")]
        [TestCase("effect")]
        [TestCase("trait")]
        [TestCase("stats")]
        [TestCase("rewards")]
        public void Validator_RejectsDivergenceFromConfirmedCard(string change)
        {
            var database = Database(); var record = database.Cards.Single(card => card.Id == CardId);
            switch (change)
            {
                case "cost": record.Cost.Mana = 3; break;
                case "race": record.Races = new[] { UnitRace.Robot }; break;
                case "attackType": record.AttackType = AttackType.Melee; break;
                case "damageType": record.DamageType = DamageType.Physical; break;
                case "effect": record.EffectText = "unconfirmed effect"; break;
                case "trait": record.HasBerserker = true; break;
                case "stats": record.Attack = 51; break;
                case "rewards": record.IncludeInRewards = false; break;
            }
            Assert.That(CardDatabaseValidator.Validate(database).Errors, Has.Some.Contains("ManaCannon must"));
        }

        private static UnitState Summon(int level, PlayerId owner, out BattleState state)
        {
            state = State(owner); SetResources(state.GetPlayer(owner).Resources, 2, 0);
            state.GetPlayer(owner).Hand.Add(CardId, "cannon-selected", false); state.GetPlayer(owner).Hand.Add(CardId, "cannon-reserve", false);
            var levels = new InMemoryCardUpgradeLevelProvider(); levels.SetUpgradeLevel(owner, CardId, level);
            return new PlayCardService(Database().CreateProvider(), new SummonService(), levels).PlayUnitCard(state, owner, CardId, new TileCoord(0, 0), "cannon-selected");
        }

        private static BattleState State(PlayerId owner)
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner)); state.SetPhase(PhaseType.Main); return state;
        }

        private static UnitState Victim(BattleState state, PlayerId attacker, int hp = 100, int magicDefense = 0, int physicalDefense = 0)
        {
            var target = new UnitState("cannon-victim", "fixture", state.GetOpponent(attacker).Id, new TileCoord(0, 0),
                AttackType.Melee, 7, hp, true, false, 0, magicDefense: magicDefense, physicalDefense: physicalDefense);
            target.HasSummoningSickness = false; state.GetOpponentBoard(attacker).Place(target.Position, target); return target;
        }

        private static void SetResources(ResourceSet resources, int mana, int gold)
        {
            resources.Spend(new ResourceSet(resources.Mana, resources.Qi, resources.Power, resources.Gold));
            resources.Add(new ResourceSet(mana, 0, 0, gold));
        }
    }
}
