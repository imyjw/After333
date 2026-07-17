using System.Collections.Generic;
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
                    cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 3),
                    effectId: "daehwandan"),
            });

            battleState.Player.Hand.Add("Daehwandan");

            spellService.CastScriptedSpell(battleState, PlayerId.Player, "Daehwandan");

            Assert.That(battleState.Player.Hand.Contains("Daehwandan"), Is.False);
            Assert.That(battleState.Player.Discard.CardIds, Does.Contain("Daehwandan"));
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(0));
            Assert.That(battleState.Player.Master.Attack, Is.EqualTo(33));
            Assert.That(battleState.PersistentEffects.Count, Is.EqualTo(1));
            Assert.That(battleState.PersistentEffects[0].TurnStartResourceGain.Qi, Is.EqualTo(3));
            Assert.That(battleState.PersistentEffects[0].OwnerTurnStartsRemaining, Is.EqualTo(3));
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
            battleState.Player.Resources.Add(new ResourceSet(
                mana: 0,
                qi: 0,
                power: TimedBombRules.PowerCost,
                gold: TimedBombRules.GoldCost));

            spellService.CastScriptedSpell(battleState, PlayerId.Player, TimedBombRules.CardId);

            Assert.That(battleState.Player.Hand.Contains(TimedBombRules.CardId), Is.False);
            Assert.That(battleState.Player.Discard.CardIds, Does.Contain(TimedBombRules.CardId));
            Assert.That(battleState.Player.Resources.Power, Is.Zero);
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(3));
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
            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 0, gold: 1));

            spellService.CastScriptedSpell(battleState, PlayerId.Player, TimedBombRules.CardId);

            Assert.That(battleState.Player.Resources.Power, Is.Zero);
            Assert.That(battleState.Player.Resources.Gold, Is.Zero,
                "Three gold replaces the missing power and one additional gold pays the printed gold cost.");
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
