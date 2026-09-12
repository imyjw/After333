using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class ScriptedSpellEffectTests
    {
        [Test]
        public void CastScriptedSpell_Daehwandan_BuffsMasterAndAddsPersistentQiGain()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new ScriptedSpellCardDefinition(
                    cardId: "Daehwandan",
                    displayName: "대환단",
                    cost: new ResourceSet(mana: 0, qi: 5, power: 0, gold: 0),
                    effectId: "daehwandan"),
            });

            battleState.Player.Hand.Add("Daehwandan");
            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 5, power: 0, gold: 0));

            spellService.CastScriptedSpell(battleState, PlayerId.Player, "Daehwandan");

            Assert.That(battleState.Player.Hand.Contains("Daehwandan"), Is.False);
            Assert.That(battleState.Player.Discard.CardIds, Does.Contain("Daehwandan"));
            Assert.That(battleState.Player.Resources.Qi, Is.Zero);
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(3));
            Assert.That(battleState.Player.Master.Attack, Is.EqualTo(33));
            Assert.That(battleState.PersistentEffects.Count, Is.EqualTo(1));
            Assert.That(battleState.PersistentEffects[0].TurnStartResourceGain.Qi, Is.EqualTo(3));
            Assert.That(battleState.PersistentEffects[0].OwnerTurnStartsRemaining, Is.EqualTo(3));
        }

        [Test]
        public void ResolveTurnStart_PersistentResourceEffectRecordsItsSourceCard()
        {
            var battleState = CreateBattleState();
            battleState.PersistentEffects.Add(new Project333.Runtime.Domain.Effects.PersistentEffectState(
                sourceCardId: "Daehwandan",
                ownerId: PlayerId.Player,
                effectId: "daehwandan",
                appliedTurn: battleState.TurnNumber,
                endConditionText: "Next 3 owner turn starts.",
                turnStartResourceGain: new ResourceSet(mana: 0, qi: 3, power: 0, gold: 0),
                ownerTurnStartsRemaining: 3));
            battleState.RestoreRuntimeState(2, PlayerId.Player, PhaseType.TurnStart);

            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(battleState.ResourceChangeEvents, Has.Count.EqualTo(1));
            Assert.That(battleState.ResourceChangeEvents[0].SourceCardId, Is.EqualTo("Daehwandan"));
            Assert.That(battleState.ResourceChangeEvents[0].Gained.Qi, Is.EqualTo(3));
            Assert.That(battleState.ResourceChangeEvents[0].Spent.Qi, Is.Zero);
        }

        [Test]
        public void CastScriptedSpell_PowerBank_SpendsGoldAndImmediatelyGainsPower()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new ScriptedSpellCardDefinition(
                    cardId: PowerBankRules.CardId,
                    displayName: "보조배터리",
                    cost: new ResourceSet(
                        mana: 0,
                        qi: 0,
                        power: 0,
                        gold: PowerBankRules.GoldCost),
                    effectId: PowerBankRules.EffectId,
                    damage: 0,
                    damageType: DamageType.None),
            });

            battleState.Player.Hand.Add(PowerBankRules.CardId);

            spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                PowerBankRules.CardId);

            Assert.That(battleState.Player.Hand.Contains(PowerBankRules.CardId), Is.False);
            Assert.That(battleState.Player.Discard.CardIds, Does.Contain(PowerBankRules.CardId));
            Assert.That(battleState.Player.Resources.Gold, Is.Zero);
            Assert.That(battleState.Player.Resources.Power, Is.EqualTo(PowerBankRules.PowerGain));
            Assert.That(battleState.PersistentEffects, Is.Empty);
            Assert.That(battleState.ResourceChangeEvents, Has.Count.EqualTo(1));
            Assert.That(battleState.ResourceChangeEvents[0].SourceCardId, Is.EqualTo(PowerBankRules.CardId));
            Assert.That(battleState.ResourceChangeEvents[0].Gained.Power, Is.EqualTo(PowerBankRules.PowerGain));
            Assert.That(battleState.ResourceChangeEvents[0].Spent.Gold, Is.Zero);
        }

        [Test]
        public void CastScriptedSpell_ManaStone_SpendsGoldAndImmediatelyGainsMana()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new ScriptedSpellCardDefinition(
                    cardId: ManaStoneRules.CardId,
                    displayName: "마정석",
                    cost: new ResourceSet(
                        mana: 0,
                        qi: 0,
                        power: 0,
                        gold: ManaStoneRules.GoldCost),
                    effectId: ManaStoneRules.EffectId,
                    damage: 0,
                    damageType: DamageType.None),
            });

            battleState.Player.Hand.Add(ManaStoneRules.CardId);

            spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                ManaStoneRules.CardId);

            Assert.That(battleState.Player.Hand.Contains(ManaStoneRules.CardId), Is.False);
            Assert.That(battleState.Player.Discard.CardIds, Does.Contain(ManaStoneRules.CardId));
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(1));
            Assert.That(battleState.Player.Resources.Mana, Is.EqualTo(ManaStoneRules.ManaGain));
            Assert.That(battleState.PersistentEffects, Is.Empty);
            Assert.That(battleState.ResourceChangeEvents, Has.Count.EqualTo(1));
            Assert.That(battleState.ResourceChangeEvents[0].SourceCardId, Is.EqualTo(ManaStoneRules.CardId));
            Assert.That(battleState.ResourceChangeEvents[0].Gained.Mana, Is.EqualTo(ManaStoneRules.ManaGain));
        }

        [Test]
        public void CastScriptedSpell_ManaStoneBundle_SpendsGoldAndImmediatelyGainsMana()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new ScriptedSpellCardDefinition(
                    cardId: ManaStoneBundleRules.CardId,
                    displayName: "마정석 꾸러미",
                    cost: new ResourceSet(
                        mana: 0,
                        qi: 0,
                        power: 0,
                        gold: ManaStoneBundleRules.GoldCost),
                    effectId: ManaStoneBundleRules.EffectId,
                    damage: 0,
                    damageType: DamageType.None),
            });

            battleState.Player.Hand.Add(ManaStoneBundleRules.CardId);
            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 0, gold: 2));

            spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                ManaStoneBundleRules.CardId);

            Assert.That(battleState.Player.Hand.Contains(ManaStoneBundleRules.CardId), Is.False);
            Assert.That(battleState.Player.Discard.CardIds, Does.Contain(ManaStoneBundleRules.CardId));
            Assert.That(battleState.Player.Resources.Gold, Is.Zero);
            Assert.That(battleState.Player.Resources.Mana, Is.EqualTo(ManaStoneBundleRules.ManaGain));
            Assert.That(battleState.PersistentEffects, Is.Empty);
            Assert.That(battleState.ResourceChangeEvents, Has.Count.EqualTo(1));
            Assert.That(battleState.ResourceChangeEvents[0].SourceCardId, Is.EqualTo(ManaStoneBundleRules.CardId));
            Assert.That(battleState.ResourceChangeEvents[0].Gained.Mana, Is.EqualTo(ManaStoneBundleRules.ManaGain));
        }

        [Test]
        public void ResolveTurnStart_CheonraJimang_DestroysMarkedUnitBeforeResourceGain()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                new ScriptedSpellCardDefinition(
                    cardId: "CheonraJimang",
                    displayName: "천라지망",
                    cost: new ResourceSet(mana: 0, qi: 5, power: 0, gold: 0),
                    effectId: "cheonra_jimang"),
            });
            var turnStartService = new TurnStartService();
            var markedUnit = new UnitState(
                runtimeId: "marked-unit",
                cardId: "GoldMiner",
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 3,
                maxHp: 15,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                turnStartResourceGain: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 1));

            battleState.PlayerBoard.Place(new TileCoord(0, 0), markedUnit);
            battleState.Player.Hand.Add("CheonraJimang");
            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 5, power: 0, gold: 0));
            var startingGold = battleState.Player.Resources.Gold;

            spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                "CheonraJimang",
                PlayerId.Player,
                new TileCoord(0, 0));

            battleState.StartNextTurn(PlayerId.Player);
            turnStartService.ResolveTurnStart(battleState);

            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(0, 0)), Is.Null);
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(startingGold + 1));
            Assert.That(battleState.PersistentEffects[0].IsExpired, Is.True);
        }

        [Test]
        public void CastScriptedSpell_Firewall_AllowsEmptyEnemyRowAndStoresFixedRow()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[] { CreateFirewallDefinition() });
            battleState.Player.Hand.Add("Firewall");
            battleState.Player.Resources.Add(new ResourceSet(mana: 3, qi: 0, power: 0, gold: 0));

            spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                "Firewall",
                PlayerId.AI,
                new TileCoord(4, 0));

            Assert.That(battleState.Player.Hand.Contains("Firewall"), Is.False);
            Assert.That(battleState.Player.Discard.CardIds, Does.Contain("Firewall"));
            Assert.That(battleState.Player.Resources.Mana, Is.Zero);
            Assert.That(battleState.PersistentEffects.Count, Is.EqualTo(1));
            Assert.That(battleState.PersistentEffects[0].TargetRow, Is.Zero);
            Assert.That(battleState.PersistentEffects[0].RemainingTriggers, Is.EqualTo(2));
            Assert.That(battleState.PersistentEffects[0].EffectDamage, Is.EqualTo(33));
            Assert.That(battleState.PersistentEffects[0].EffectDamageType, Is.EqualTo(DamageType.Magic));
            Assert.That(battleState.PersistentEffects[0].TargetsOwnerBoard, Is.False);
        }

        [Test]
        public void ResolveTurnStart_FirewallCanDamageOwnersSelectedRow()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[] { CreateFirewallDefinition() });
            var friendlyTarget = CreateUnit(
                "friendly-target",
                new TileCoord(0, 0),
                maxHp: 100,
                ownerId: PlayerId.Player);
            battleState.PlayerBoard.Place(friendlyTarget.Position, friendlyTarget);
            battleState.Player.Hand.Add("Firewall");
            battleState.Player.Resources.Add(new ResourceSet(mana: 3, qi: 0, power: 0, gold: 0));

            spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                "Firewall",
                PlayerId.Player,
                new TileCoord(4, 0));

            Assert.That(battleState.PersistentEffects[0].TargetsOwnerBoard, Is.True);

            battleState.StartNextTurn(PlayerId.AI);
            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(friendlyTarget.CurrentHp, Is.EqualTo(67));
            Assert.That(battleState.PersistentEffects[0].RemainingTriggers, Is.EqualTo(1));
        }

        [Test]
        public void ResolveTurnStart_Firewall_RunsAfterUpkeepAndHitsCurrentRowOccupantsTwice()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[] { CreateFirewallDefinition() });
            var originalTarget = CreateUnit(
                runtimeId: "original-target",
                position: new TileCoord(0, 0),
                maxHp: 100,
                sciencePowerUpkeep: 1,
                magicDefense: 3);
            var otherRowTarget = CreateUnit(
                runtimeId: "other-row-target",
                position: new TileCoord(0, 1),
                maxHp: 100);
            originalTarget.IsDrained = true;
            battleState.AIBoard.Place(originalTarget.Position, originalTarget);
            battleState.AIBoard.Place(otherRowTarget.Position, otherRowTarget);
            battleState.AI.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 1, gold: 0));
            battleState.Player.Hand.Add("Firewall");
            battleState.Player.Resources.Add(new ResourceSet(mana: 3, qi: 0, power: 0, gold: 0));

            spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                "Firewall",
                PlayerId.AI,
                new TileCoord(3, 0));

            battleState.StartNextTurn(PlayerId.AI);
            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(originalTarget.IsDrained, Is.False, "Power upkeep must resolve before Firewall damage.");
            Assert.That(originalTarget.CurrentHp, Is.EqualTo(70));
            Assert.That(otherRowTarget.CurrentHp, Is.EqualTo(100));
            Assert.That(battleState.PersistentEffects[0].RemainingTriggers, Is.EqualTo(1));
            Assert.That(battleState.PersistentEffects[0].IsExpired, Is.False);

            var laterTarget = CreateUnit(
                runtimeId: "later-target",
                position: new TileCoord(1, 0),
                maxHp: 100);
            battleState.AIBoard.Place(laterTarget.Position, laterTarget);
            battleState.StartNextTurn(PlayerId.Player);
            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(originalTarget.CurrentHp, Is.EqualTo(40));
            Assert.That(laterTarget.CurrentHp, Is.EqualTo(67));
            Assert.That(otherRowTarget.CurrentHp, Is.EqualTo(100));
            Assert.That(battleState.PersistentEffects[0].RemainingTriggers, Is.Zero);
            Assert.That(battleState.PersistentEffects[0].IsExpired, Is.True);
        }

        [Test]
        public void ResolveTurnStart_StackedFirewalls_ResolveIndependently()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[] { CreateFirewallDefinition() });
            var target = CreateUnit("stack-target", new TileCoord(0, 0), maxHp: 100);
            battleState.AIBoard.Place(target.Position, target);
            battleState.Player.Hand.Add("Firewall");
            battleState.Player.Hand.Add("Firewall");
            battleState.Player.Resources.Add(new ResourceSet(mana: 6, qi: 0, power: 0, gold: 0));

            spellService.CastScriptedSpell(battleState, PlayerId.Player, "Firewall", PlayerId.AI, new TileCoord(0, 0));
            spellService.CastScriptedSpell(battleState, PlayerId.Player, "Firewall", PlayerId.AI, new TileCoord(4, 0));
            battleState.StartNextTurn(PlayerId.AI);
            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(target.CurrentHp, Is.EqualTo(34));
            Assert.That(battleState.PersistentEffects.Count, Is.EqualTo(2));
            Assert.That(battleState.PersistentEffects[0].RemainingTriggers, Is.EqualTo(1));
            Assert.That(battleState.PersistentEffects[1].RemainingTriggers, Is.EqualTo(1));
        }

        [Test]
        public void CastDamageSpells_AppliesOneDamagePerUpgradeLevel()
        {
            var battleState = CreateBattleState();
            var upgrades = new InMemoryCardUpgradeLevelProvider();
            upgrades.SetUpgradeLevel(PlayerId.Player, "Firewall", 13);
            upgrades.SetUpgradeLevel(PlayerId.Player, "firebolt", 5);
            var spellService = CreateSpellService(
                new CardDefinition[]
                {
                    CreateFirewallDefinition(),
                    new DamageSpellCardDefinition(
                        "firebolt",
                        "파이어볼",
                        new ResourceSet(mana: 1, qi: 0, power: 0, gold: 0),
                        10,
                        DamageType.Magic),
                },
                upgrades);
            battleState.Player.Hand.Add("Firewall");
            battleState.Player.Hand.Add("firebolt");
            battleState.Player.Resources.Add(new ResourceSet(mana: 4, qi: 0, power: 0, gold: 0));

            spellService.CastScriptedSpell(battleState, PlayerId.Player, "Firewall", PlayerId.AI, new TileCoord(0, 0));
            var masterHpBefore = battleState.AI.Master.CurrentHp;
            spellService.CastDamageSpell(
                battleState,
                PlayerId.Player,
                "firebolt",
                PlayerId.AI,
                battleState.AI.Master.Position);

            Assert.That(battleState.PersistentEffects[0].EffectDamage, Is.EqualTo(46));
            Assert.That(battleState.AI.Master.CurrentHp, Is.EqualTo(masterHpBefore - 15));
        }

        [Test]
        public void CastScriptedSpell_TimedBombStoresUpgradeDamageAndThreeTurnCountdown()
        {
            var battleState = CreateBattleState();
            var upgrades = new InMemoryCardUpgradeLevelProvider();
            upgrades.SetUpgradeLevel(PlayerId.Player, TimedBombRules.CardId, 5);
            var spellService = CreateSpellService(
                new CardDefinition[] { CreateTimedBombDefinition() },
                upgrades);
            battleState.Player.Hand.Add(TimedBombRules.CardId);
            battleState.Player.Resources.Spend(new ResourceSet(mana: 0, qi: 0, power: 0, gold: 3));
            battleState.Player.Resources.Add(new ResourceSet(
                mana: 0,
                qi: 0,
                power: TimedBombRules.PowerCost,
                gold: TimedBombRules.GoldCost));

            spellService.CastScriptedSpell(battleState, PlayerId.Player, TimedBombRules.CardId);

            Assert.That(battleState.Player.Hand.Contains(TimedBombRules.CardId), Is.False);
            Assert.That(battleState.Player.Discard.CardIds, Does.Contain(TimedBombRules.CardId));
            Assert.That(battleState.Player.Resources.Power, Is.Zero);
            Assert.That(battleState.Player.Resources.Gold, Is.Zero);
            Assert.That(battleState.PersistentEffects.Count, Is.EqualTo(1));
            Assert.That(battleState.PersistentEffects[0].RemainingTriggers,
                Is.EqualTo(TimedBombRules.TurnStartsUntilDetonation));
            Assert.That(battleState.PersistentEffects[0].EffectDamage,
                Is.EqualTo(TimedBombRules.BaseDamage + 5));
            Assert.That(battleState.PersistentEffects[0].EffectDamageType, Is.EqualTo(DamageType.Physical));
        }

        [Test]
        public void CastScriptedSpell_TimedBombAllowsGoldToReplaceMissingPower()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[] { CreateTimedBombDefinition() });
            battleState.Player.Hand.Add(TimedBombRules.CardId);

            spellService.CastScriptedSpell(battleState, PlayerId.Player, TimedBombRules.CardId);

            Assert.That(battleState.Player.Resources.Power, Is.Zero);
            Assert.That(battleState.Player.Resources.Gold, Is.Zero,
                "Three gold replaces the missing power; there is no additional printed gold cost.");
            Assert.That(battleState.PersistentEffects.Count, Is.EqualTo(1));
        }

        [Test]
        public void ResolveTurnStart_TimedBombDetonatesOnlyAtThirdTurnStart()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[] { CreateTimedBombDefinition() });
            var target = new UnitState(
                runtimeId: "timed-bomb-target",
                cardId: "timed-bomb-target",
                ownerId: PlayerId.AI,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 1,
                maxHp: 100,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                damageType: DamageType.Physical,
                physicalDefense: 3);
            battleState.AIBoard.Place(target.Position, target);
            battleState.Player.Hand.Add(TimedBombRules.CardId);
            battleState.Player.Resources.Add(new ResourceSet(
                mana: 0,
                qi: 0,
                power: TimedBombRules.PowerCost,
                gold: TimedBombRules.GoldCost));
            spellService.CastScriptedSpell(battleState, PlayerId.Player, TimedBombRules.CardId);
            var masterHpBefore = battleState.AI.Master.CurrentHp;
            var turnStartService = new TurnStartService();

            battleState.StartNextTurn(PlayerId.AI);
            turnStartService.ResolveTurnStart(battleState);
            Assert.That(target.CurrentHp, Is.EqualTo(100));
            Assert.That(battleState.PersistentEffects[0].RemainingTriggers, Is.EqualTo(2));

            battleState.StartNextTurn(PlayerId.Player);
            turnStartService.ResolveTurnStart(battleState);
            Assert.That(target.CurrentHp, Is.EqualTo(100));
            Assert.That(battleState.PersistentEffects[0].RemainingTriggers, Is.EqualTo(1));

            battleState.StartNextTurn(PlayerId.AI);
            turnStartService.ResolveTurnStart(battleState);

            Assert.That(target.CurrentHp, Is.EqualTo(70), "Physical defense must reduce the 33 damage to 30.");
            Assert.That(battleState.AI.Master.CurrentHp, Is.EqualTo(masterHpBefore - TimedBombRules.BaseDamage));
            Assert.That(battleState.PersistentEffects[0].RemainingTriggers, Is.Zero);
            Assert.That(battleState.PersistentEffects[0].IsExpired, Is.True);
        }

        [Test]
        public void CastScriptedSpell_BiochemicalBombStoresUpgradedFixedArea()
        {
            var battleState = CreateBattleState();
            var upgrades = new InMemoryCardUpgradeLevelProvider();
            upgrades.SetUpgradeLevel(PlayerId.Player, BiochemicalBombRules.CardId, 3);
            var spellService = CreateSpellService(
                new CardDefinition[] { CreateBiochemicalBombDefinition() },
                upgrades);
            battleState.Player.Hand.Add(BiochemicalBombRules.CardId);
            battleState.Player.Resources.Add(new ResourceSet(
                mana: 0,
                qi: 0,
                power: BiochemicalBombRules.PowerCost,
                gold: BiochemicalBombRules.GoldCost));

            spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                BiochemicalBombRules.CardId,
                PlayerId.AI,
                new TileCoord(BiochemicalBombRules.RightAreaStartColumn, 0));

            var effect = battleState.PersistentEffects[0];
            Assert.That(effect.TargetStartColumn, Is.EqualTo(BiochemicalBombRules.RightAreaStartColumn));
            Assert.That(effect.RemainingTriggers, Is.EqualTo(BiochemicalBombRules.TriggerCount));
            Assert.That(effect.EffectDamage, Is.EqualTo(28));
            Assert.That(effect.EffectDamageType, Is.EqualTo(DamageType.Fixed));
            Assert.That(battleState.Player.Hand.Contains(BiochemicalBombRules.CardId), Is.False);
            Assert.That(battleState.Player.Discard.CardIds, Does.Contain(BiochemicalBombRules.CardId));
        }

        [Test]
        public void CastScriptedSpell_BiochemicalBombRejectsFriendlyBoard()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[] { CreateBiochemicalBombDefinition() });
            battleState.Player.Hand.Add(BiochemicalBombRules.CardId);
            battleState.Player.Resources.Add(new ResourceSet(
                mana: 0,
                qi: 0,
                power: BiochemicalBombRules.PowerCost,
                gold: BiochemicalBombRules.GoldCost));

            Assert.Throws<InvalidOperationException>(() => spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                BiochemicalBombRules.CardId,
                PlayerId.Player,
                new TileCoord(BiochemicalBombRules.LeftAreaStartColumn, 0)));
            Assert.That(battleState.Player.Hand.Contains(BiochemicalBombRules.CardId), Is.True);
            Assert.That(battleState.PersistentEffects, Is.Empty);
        }

        [Test]
        public void ResolveTurnStart_BiochemicalBombHitsFixedAreaFourTimesIncludingNewOccupants()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[] { CreateBiochemicalBombDefinition() });
            var outsideTarget = CreateUnit("outside-target", new TileCoord(0, 0), maxHp: 250);
            var insideTarget = new UnitState(
                runtimeId: "inside-target",
                cardId: "inside-target",
                ownerId: PlayerId.AI,
                position: new TileCoord(1, 0),
                attackType: AttackType.Melee,
                attack: 1,
                maxHp: 250,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                damageType: DamageType.Physical,
                physicalDefense: 15,
                magicDefense: 4);
            battleState.AIBoard.Place(outsideTarget.Position, outsideTarget);
            battleState.AIBoard.Place(insideTarget.Position, insideTarget);
            battleState.Player.Hand.Add(BiochemicalBombRules.CardId);
            battleState.Player.Resources.Add(new ResourceSet(
                mana: 0,
                qi: 0,
                power: BiochemicalBombRules.PowerCost,
                gold: BiochemicalBombRules.GoldCost));
            spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                BiochemicalBombRules.CardId,
                PlayerId.AI,
                new TileCoord(BiochemicalBombRules.RightAreaStartColumn, 0));
            var turnStartService = new TurnStartService();

            battleState.StartNextTurn(PlayerId.AI);
            turnStartService.ResolveTurnStart(battleState);
            Assert.That(outsideTarget.CurrentHp, Is.EqualTo(250));
            Assert.That(insideTarget.CurrentHp, Is.EqualTo(225));

            var laterTarget = CreateUnit("later-target", new TileCoord(4, 0), maxHp: 250);
            battleState.AIBoard.Place(laterTarget.Position, laterTarget);
            for (var trigger = 2; trigger <= BiochemicalBombRules.TriggerCount; trigger++)
            {
                var nextPlayer = trigger % 2 == 0 ? PlayerId.Player : PlayerId.AI;
                battleState.StartNextTurn(nextPlayer);
                turnStartService.ResolveTurnStart(battleState);
            }

            Assert.That(outsideTarget.CurrentHp, Is.EqualTo(250));
            Assert.That(insideTarget.CurrentHp, Is.EqualTo(150));
            Assert.That(laterTarget.CurrentHp, Is.EqualTo(175));
            Assert.That(battleState.PersistentEffects[0].RemainingTriggers, Is.Zero);
            Assert.That(battleState.PersistentEffects[0].IsExpired, Is.True);
        }

        [Test]
        public void ResolveTurnStart_FirewallAndBiochemicalBombUseCastOrder()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                CreateFirewallDefinition(),
                CreateBiochemicalBombDefinition(),
            });
            var target = CreateUnit("ordered-area-target", new TileCoord(1, 0), maxHp: 250);
            battleState.AIBoard.Place(target.Position, target);
            battleState.Player.Hand.Add("Firewall");
            battleState.Player.Hand.Add(BiochemicalBombRules.CardId);
            battleState.Player.Resources.Add(new ResourceSet(
                mana: 3,
                qi: 0,
                power: BiochemicalBombRules.PowerCost,
                gold: BiochemicalBombRules.GoldCost));

            spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                "Firewall",
                PlayerId.AI,
                new TileCoord(0, 0));
            spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                BiochemicalBombRules.CardId,
                PlayerId.AI,
                new TileCoord(BiochemicalBombRules.LeftAreaStartColumn, 0));

            battleState.StartNextTurn(PlayerId.AI);
            new TurnStartService().ResolveTurnStart(battleState);

            var sourceOrder = battleState.ValuePopupEvents
                .Where(valueEvent => valueEvent.RuntimeId == target.RuntimeId)
                .Select(valueEvent => valueEvent.SourceCardId)
                .ToArray();
            Assert.That(sourceOrder, Is.EqualTo(new[] { "Firewall", BiochemicalBombRules.CardId }));
        }

        [Test]
        public void ResolveTurnStart_AreaSpellsRecordEveryAffectedTileForPresentation()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[]
            {
                CreateFirewallDefinition(),
                CreateBiochemicalBombDefinition(),
            });
            battleState.Player.Hand.Add("Firewall");
            battleState.Player.Hand.Add(BiochemicalBombRules.CardId);
            battleState.Player.Resources.Add(new ResourceSet(
                mana: 3,
                qi: 0,
                power: BiochemicalBombRules.PowerCost,
                gold: BiochemicalBombRules.GoldCost));

            spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                "Firewall",
                PlayerId.AI,
                new TileCoord(0, 0));
            spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                BiochemicalBombRules.CardId,
                PlayerId.AI,
                new TileCoord(BiochemicalBombRules.RightAreaStartColumn, 0));

            battleState.StartNextTurn(PlayerId.AI);
            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(battleState.AreaSpellEffectEvents, Has.Count.EqualTo(2));

            var firewallEvent = battleState.AreaSpellEffectEvents[0];
            Assert.That(firewallEvent.EffectId, Is.EqualTo("firewall"));
            Assert.That(firewallEvent.SourceCardId, Is.EqualTo("Firewall"));
            Assert.That(firewallEvent.SourceOwnerId, Is.EqualTo(PlayerId.Player));
            Assert.That(firewallEvent.TargetOwnerId, Is.EqualTo(PlayerId.AI));
            Assert.That(firewallEvent.TargetCoords, Is.EqualTo(new[]
            {
                new TileCoord(0, 0),
                new TileCoord(1, 0),
                new TileCoord(2, 0),
                new TileCoord(3, 0),
                new TileCoord(4, 0),
            }));

            var biochemicalBombEvent = battleState.AreaSpellEffectEvents[1];
            Assert.That(
                biochemicalBombEvent.EffectId,
                Is.EqualTo(BiochemicalBombRules.EffectId));
            Assert.That(
                biochemicalBombEvent.SourceCardId,
                Is.EqualTo(BiochemicalBombRules.CardId));
            Assert.That(biochemicalBombEvent.SourceOwnerId, Is.EqualTo(PlayerId.Player));
            Assert.That(biochemicalBombEvent.TargetOwnerId, Is.EqualTo(PlayerId.AI));
            Assert.That(biochemicalBombEvent.TargetCoords, Is.EqualTo(new[]
            {
                new TileCoord(1, 0),
                new TileCoord(1, 1),
                new TileCoord(2, 0),
                new TileCoord(2, 1),
                new TileCoord(3, 0),
                new TileCoord(3, 1),
                new TileCoord(4, 0),
                new TileCoord(4, 1),
            }));
        }

        [Test]
        public void ResolveTurnStart_FirewallKillsMaster_EndsBattleForCaster()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[] { CreateFirewallDefinition() });
            battleState.AI.Master.CurrentHp = 33;
            battleState.Player.Hand.Add("Firewall");
            battleState.Player.Resources.Add(new ResourceSet(mana: 3, qi: 0, power: 0, gold: 0));

            spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                "Firewall",
                PlayerId.AI,
                new TileCoord(0, battleState.AI.Master.Position.Row));
            battleState.StartNextTurn(PlayerId.AI);
            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(battleState.IsEnded, Is.True);
            Assert.That(battleState.Result.Winner, Is.EqualTo(PlayerId.Player));
        }

        [Test]
        public void ResolveTurnStart_FirewallKillsOwnersMaster_AwardsOpponentVictory()
        {
            var battleState = CreateBattleState();
            var spellService = CreateSpellService(new CardDefinition[] { CreateFirewallDefinition() });
            battleState.Player.Master.CurrentHp = 33;
            battleState.Player.Hand.Add("Firewall");
            battleState.Player.Resources.Add(new ResourceSet(mana: 3, qi: 0, power: 0, gold: 0));

            spellService.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                "Firewall",
                PlayerId.Player,
                new TileCoord(0, battleState.Player.Master.Position.Row));
            battleState.StartNextTurn(PlayerId.AI);
            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(battleState.IsEnded, Is.True);
            Assert.That(battleState.Result.Winner, Is.EqualTo(PlayerId.AI));
        }

        private static SpellService CreateSpellService(IEnumerable<CardDefinition> definitions)
        {
            return new SpellService(new InMemoryCardDefinitionProvider(definitions));
        }

        private static SpellService CreateSpellService(
            IEnumerable<CardDefinition> definitions,
            ICardUpgradeLevelProvider upgradeLevelProvider)
        {
            return new SpellService(
                new InMemoryCardDefinitionProvider(definitions),
                upgradeLevelProvider);
        }

        private static ScriptedSpellCardDefinition CreateFirewallDefinition()
        {
            return new ScriptedSpellCardDefinition(
                cardId: "Firewall",
                displayName: "파이어월",
                cost: new ResourceSet(mana: 3, qi: 0, power: 0, gold: 0),
                effectId: "firewall",
                damage: 33,
                damageType: DamageType.Magic,
                triggerCount: 2);
        }

        private static ScriptedSpellCardDefinition CreateTimedBombDefinition()
        {
            return new ScriptedSpellCardDefinition(
                cardId: TimedBombRules.CardId,
                displayName: "시한폭탄",
                cost: new ResourceSet(
                    mana: 0,
                    qi: 0,
                    power: TimedBombRules.PowerCost,
                    gold: TimedBombRules.GoldCost),
                effectId: TimedBombRules.EffectId,
                damage: TimedBombRules.BaseDamage,
                damageType: DamageType.Physical,
                triggerCount: TimedBombRules.TurnStartsUntilDetonation);
        }

        private static ScriptedSpellCardDefinition CreateBiochemicalBombDefinition()
        {
            return new ScriptedSpellCardDefinition(
                cardId: BiochemicalBombRules.CardId,
                displayName: "생화학폭탄",
                cost: new ResourceSet(
                    mana: 0,
                    qi: 0,
                    power: BiochemicalBombRules.PowerCost,
                    gold: BiochemicalBombRules.GoldCost),
                effectId: BiochemicalBombRules.EffectId,
                damage: BiochemicalBombRules.BaseDamage,
                damageType: DamageType.Fixed,
                triggerCount: BiochemicalBombRules.TriggerCount);
        }

        private static UnitState CreateUnit(
            string runtimeId,
            TileCoord position,
            int maxHp,
            int sciencePowerUpkeep = 0,
            int magicDefense = 0,
            PlayerId ownerId = PlayerId.AI)
        {
            return new UnitState(
                runtimeId: runtimeId,
                cardId: runtimeId,
                ownerId: ownerId,
                position: position,
                attackType: AttackType.Melee,
                attack: 1,
                maxHp: maxHp,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: sciencePowerUpkeep,
                damageType: DamageType.Physical,
                magicDefense: magicDefense);
        }

        private static BattleState CreateBattleState()
        {
            var setupService = new BattleSetupService();
            var mulliganService = new MulliganService();
            var battleState = setupService.CreateInitialState(new BattleSetupRequest(CreateDeck("P"), CreateDeck("A"), PlayerId.Player));
            mulliganService.PassMulligan(battleState, PlayerId.Player);
            battleState.SetPhase(PhaseType.Main);
            return battleState;
        }

        private static IReadOnlyList<string> CreateDeck(string prefix)
        {
            return new List<string>
            {
                prefix + "-00",
                prefix + "-01",
                prefix + "-02",
                prefix + "-03",
                prefix + "-04",
                prefix + "-05",
                prefix + "-06",
                prefix + "-07",
                prefix + "-08",
                prefix + "-09",
            };
        }
    }
}
