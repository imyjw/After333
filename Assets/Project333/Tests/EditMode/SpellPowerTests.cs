using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
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
    public sealed class SpellPowerTests
    {
        [Test]
        public void GetTotal_CountsOnlyLivingUnsuppressedAlliedOccupants()
        {
            var battleState = CreateBattleState(PlayerId.Player, playerMasterSpellPower: 1);
            var active = CreateUnit("active", PlayerId.Player, new TileCoord(0, 0), spellPower: 2,
                hasHiding: true, hasFlying: true);
            var building = new BuildingState(
                "building", "building", PlayerId.Player, new TileCoord(1, 0),
                canAttack: false, attack: 0, maxHp: 10, spellPower: 3);
            var sealbound = CreateUnit("sealed", PlayerId.Player, new TileCoord(3, 0), spellPower: 4);
            var defeated = CreateUnit("defeated", PlayerId.Player, new TileCoord(4, 0), spellPower: 5);
            sealbound.EnterSealbound(1);
            defeated.CurrentHp = 0;
            battleState.PlayerBoard.Place(active.Position, active);
            battleState.PlayerBoard.Place(building.Position, building);
            battleState.PlayerBoard.Place(sealbound.Position, sealbound);
            battleState.PlayerBoard.Place(defeated.Position, defeated);

            Assert.That(SpellPowerRules.GetTotal(battleState, PlayerId.Player), Is.EqualTo(6));

            active.IsDrained = true;
            Assert.That(SpellPowerRules.GetTotal(battleState, PlayerId.Player), Is.EqualTo(4));

            building.ApplyErasure();
            Assert.That(SpellPowerRules.GetTotal(battleState, PlayerId.Player), Is.EqualTo(1));
        }

        [Test]
        public void CastDamageSpell_MagicAddsSpellPowerBeforeMagicDefense()
        {
            var battleState = CreateBattleState();
            AddSpellPowerSource(battleState, PlayerId.Player, 5);
            var target = CreateUnit(
                "target", PlayerId.AI, new TileCoord(0, 0), maxHp: 30, magicDefense: 4);
            battleState.AIBoard.Place(target.Position, target);
            battleState.Player.Hand.Add("magic-spell");
            var service = CreateDamageSpellService("magic-spell", 10, DamageType.Magic);

            service.CastDamageSpell(
                battleState, PlayerId.Player, "magic-spell", PlayerId.AI, target.Position);

            Assert.That(target.CurrentHp, Is.EqualTo(19));
        }

        [Test]
        public void CastDamageSpell_NonMagicDamageDoesNotAddSpellPower()
        {
            AssertNonMagicSpellDamage(DamageType.Physical, expectedHp: 23);
            AssertNonMagicSpellDamage(DamageType.Fixed, expectedHp: 20);
        }

        private static void AssertNonMagicSpellDamage(DamageType damageType, int expectedHp)
        {
            var battleState = CreateBattleState();
            AddSpellPowerSource(battleState, PlayerId.Player, 5);
            var target = CreateUnit(
                "target", PlayerId.AI, new TileCoord(0, 0), maxHp: 30, physicalDefense: 3);
            battleState.AIBoard.Place(target.Position, target);
            battleState.Player.Hand.Add("spell");
            var service = CreateDamageSpellService("spell", 10, damageType);

            service.CastDamageSpell(
                battleState, PlayerId.Player, "spell", PlayerId.AI, target.Position);

            Assert.That(target.CurrentHp, Is.EqualTo(expectedHp));
        }

        [Test]
        public void CastDamageSpell_OwnTargetStillReceivesSpellPowerBonus()
        {
            var battleState = CreateBattleState();
            var sourceAndTarget = CreateUnit(
                "source", PlayerId.Player, new TileCoord(0, 0), maxHp: 20,
                magicDefense: 1, spellPower: 4);
            battleState.PlayerBoard.Place(sourceAndTarget.Position, sourceAndTarget);
            battleState.Player.Hand.Add("magic-spell");
            var service = CreateDamageSpellService("magic-spell", 2, DamageType.Magic);

            service.CastDamageSpell(
                battleState,
                PlayerId.Player,
                "magic-spell",
                PlayerId.Player,
                sourceAndTarget.Position);

            Assert.That(sourceAndTarget.CurrentHp, Is.EqualTo(15));
        }

        [TestCase(0)]
        [TestCase(5)]
        public void BiochemicalBomb_IgnoresSpellPowerAndDefensesForAllFourTriggers(int spellPowerAtCast)
        {
            var battleState = CreateBattleState();
            var source = AddSpellPowerSource(battleState, PlayerId.Player, spellPowerAtCast);
            var target = CreateUnit("target", PlayerId.AI, new TileCoord(0, 0),
                maxHp: 300, physicalDefense: 20, magicDefense: 2);
            battleState.AIBoard.Place(target.Position, target);
            var upgrades = new InMemoryCardUpgradeLevelProvider();
            upgrades.SetUpgradeLevel(PlayerId.Player, BiochemicalBombRules.CardId, 3);
            var definition = new ScriptedSpellCardDefinition(
                BiochemicalBombRules.CardId, "Biochemical Bomb", new ResourceSet(),
                BiochemicalBombRules.EffectId, damage: 25, damageType: DamageType.Fixed, triggerCount: 4);
            var service = new SpellService(
                new InMemoryCardDefinitionProvider(new CardDefinition[] { definition }),
                cardUpgradeLevelProvider: upgrades);
            battleState.Player.Hand.Add(BiochemicalBombRules.CardId);

            service.CastScriptedSpell(battleState, PlayerId.Player, BiochemicalBombRules.CardId,
                PlayerId.AI, target.Position);

            var effect = battleState.PersistentEffects[0];
            Assert.That(effect.EffectDamage, Is.EqualTo(28));
            Assert.That(effect.EffectDamageType, Is.EqualTo(DamageType.Fixed));
            Assert.That(effect.CapturedSpellPower, Is.Zero);
            battleState.PlayerBoard.Remove(source.Position);
            AddSpellPowerSource(battleState, PlayerId.Player, 20);
            for (var trigger = 1; trigger <= 4; trigger++)
            {
                ResolveTurnStart(battleState, trigger % 2 == 1 ? PlayerId.AI : PlayerId.Player);
                Assert.That(target.CurrentHp, Is.EqualTo(300 - trigger * 28));
            }
            Assert.That(effect.IsExpired, Is.True);
        }

        [Test]
        public void Firewall_RecalculatesSpellPowerForEveryTrigger()
        {
            var battleState = CreateBattleState();
            var source = AddSpellPowerSource(battleState, PlayerId.Player, 5);
            var first = CreateUnit(
                "first", PlayerId.AI, new TileCoord(0, 0), maxHp: 40, magicDefense: 2);
            var second = CreateUnit(
                "second", PlayerId.AI, new TileCoord(1, 0), maxHp: 40, magicDefense: 2);
            battleState.AIBoard.Place(first.Position, first);
            battleState.AIBoard.Place(second.Position, second);
            battleState.Player.Hand.Add("Firewall");
            var definition = new ScriptedSpellCardDefinition(
                "Firewall", "Firewall", new ResourceSet(), "firewall",
                damage: 10, damageType: DamageType.Magic, triggerCount: 2);
            var service = new SpellService(
                new InMemoryCardDefinitionProvider(new CardDefinition[] { definition }));

            service.CastScriptedSpell(
                battleState,
                PlayerId.Player,
                "Firewall",
                PlayerId.AI,
                new TileCoord(4, 0));

            Assert.That(battleState.PersistentEffects[0].CapturedSpellPower, Is.Zero);
            battleState.PlayerBoard.Remove(source.Position);
            AddSpellPowerSource(battleState, PlayerId.Player, 20);

            ResolveTurnStart(battleState, PlayerId.AI);
            Assert.That(first.CurrentHp, Is.EqualTo(12));
            Assert.That(second.CurrentHp, Is.EqualTo(12));

            battleState.PlayerBoard.Remove(source.Position);
            ResolveTurnStart(battleState, PlayerId.Player);
            Assert.That(first.CurrentHp, Is.EqualTo(4));
            Assert.That(second.CurrentHp, Is.EqualTo(4));
            Assert.That(battleState.PersistentEffects[0].IsExpired, Is.True);
        }

        [Test]
        public void StateViewRoundTrip_UsesCurrentSourceAndIgnoresLegacyCapturedSpellPower()
        {
            var battleState = CreateBattleState();
            var source = AddSpellPowerSource(battleState, PlayerId.Player, 3);
            battleState.PersistentEffects.Add(new PersistentEffectState(
                "Firewall",
                PlayerId.Player,
                "firewall",
                1,
                "two triggers",
                new ResourceSet(),
                targetRow: 0,
                remainingTriggers: 2,
                effectDamage: 10,
                effectDamageType: DamageType.Magic,
                capturedSpellPower: 7));

            var view = new BattleStateViewFactory().CreateForPlayer(
                battleState, "match", PlayerId.Player);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(view);

            Assert.That(
                projected.PlayerBoard.GetOccupant(source.Position).SpellPower,
                Is.EqualTo(3));
            Assert.That(projected.PersistentEffects[0].CapturedSpellPower, Is.EqualTo(7)); // Legacy wire compatibility.
            var target = CreateUnit("legacy-target", PlayerId.AI, new TileCoord(0, 0), maxHp: 100);
            projected.AIBoard.Place(target.Position, target);
            ResolveTurnStart(projected, PlayerId.AI);
            Assert.That(target.CurrentHp, Is.EqualTo(87), "10 base + 3 live power, ignoring stored 7");
        }

        [Test]
        public void AiKillSpellPrediction_IncludesActiveSpellPower()
        {
            var battleState = CreateBattleState(PlayerId.AI);
            battleState.AI.Master.IsDrained = true;
            var source = AddSpellPowerSource(battleState, PlayerId.AI, 3);
            source.RemainingAttacksThisTurn = 0;
            var target = CreateUnit(
                "target", PlayerId.Player, new TileCoord(0, 0), maxHp: 7, magicDefense: 1);
            battleState.PlayerBoard.Place(target.Position, target);
            battleState.AI.Hand.Add("ai-spell");
            var definition = new DamageSpellCardDefinition(
                "ai-spell", "AI Spell", new ResourceSet(), 5, DamageType.Magic);
            var service = new AiDecisionService(
                new InMemoryCardDefinitionProvider(new CardDefinition[] { definition }));

            var command = service.GetNextCommand(battleState) as CastDamageSpellCommand;

            Assert.That(command, Is.Not.Null);
            Assert.That(command.CardId, Is.EqualTo("ai-spell"));
            Assert.That(command.TargetCoord, Is.EqualTo(target.Position));
        }

        [Test]
        public void CardDatabaseValidator_SpellPowerSchemaAndTextStayConsistent()
        {
            var valid = CreateUnitRecord("spell-power-unit");
            valid.SpellPower = 2;
            valid.SpecialEffectText = "주문력 +2";
            var validDatabase = CreateDraftCompatibleDatabase(valid);

            var result = CardDatabaseValidator.Validate(validDatabase);
            var definition = valid.ToDefinition() as UnitCardDefinition;

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            Assert.That(definition.SpellPower, Is.EqualTo(2));

            var negative = CreateUnitRecord("negative-spell-power");
            negative.SpellPower = -1;
            Assert.That(
                CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(negative)).Errors,
                Has.Some.Contains("negative spellPower"));

            var spell = new JsonCardDefinitionRecord
            {
                Id = "invalid-spell-power-spell",
                DisplayName = "Invalid",
                DefinitionType = JsonCardDefinitionKind.DamageSpell,
                ChargeTileFootprint = ChargeTileFootprint.None,
                Damage = 1,
                DamageType = DamageType.Magic,
                SpellPower = 1,
                SpecialEffectText = "SpellPower +1",
            };
            Assert.That(
                CardDatabaseValidator.Validate(CreateDraftCompatibleDatabase(spell)).Errors,
                Has.Some.Contains("spellPower can only be used by Unit or Building cards"));
        }


        [Test]
        public void Firewall_CastWithoutPowerThenSummonTenAndRemoveBeforeSecondTick()
        {
            var state = CreateBattleState();
            var target = CreateUnit("target", PlayerId.AI, new TileCoord(1, 0), maxHp: 100);
            state.AIBoard.Place(target.Position, target);
            var spell = new ScriptedSpellCardDefinition("Firewall", "Firewall", new ResourceSet(),
                "firewall", damage: 33, damageType: DamageType.Magic, triggerCount: 2);
            var summon = new UnitCardDefinition("power", "Power", new ResourceSet(), AttackType.Melee,
                0, 10, true, false, 0, spellPower: 10);
            var provider = new InMemoryCardDefinitionProvider(new CardDefinition[] { spell, summon });
            state.Player.Hand.Add("Firewall");
            state.Player.Hand.Add("power");
            new SpellService(provider).CastScriptedSpell(state, PlayerId.Player, "Firewall", PlayerId.AI, target.Position);
            var source = new PlayCardService(provider).PlayUnitCard(state, PlayerId.Player, "power", new TileCoord(0, 0));
            new EndTurnService().EndTurn(state);
            new TurnStartService().ResolveTurnStart(state);
            Assert.That(target.CurrentHp, Is.EqualTo(57));
            source.CurrentHp = 0;
            OccupantDestructionService.ResolveDefeatedOccupants(state);
            new EndTurnService().EndTurn(state);
            new TurnStartService().ResolveTurnStart(state);
            Assert.That(target.CurrentHp, Is.EqualTo(24));
        }

        [TestCase(DamageType.Magic, 89)]
        [TestCase(DamageType.Physical, 97)]
        [TestCase(DamageType.Fixed, 97)]
        public void NormalAttack_OnlyMagicUsesOwnersCurrentPower(DamageType damageType, int expectedHp)
        {
            var state = CreateBattleState();
            var attacker = new UnitState("attacker", "attacker", PlayerId.Player, new TileCoord(1, 0),
                AttackType.Ranged, 3, 100, true, false, 0, damageType: damageType, spellPower: 10);
            attacker.HasSummoningSickness = false;
            state.PlayerBoard.Place(attacker.Position, attacker);
            var target = CreateUnit("target", PlayerId.AI, new TileCoord(1, 0), maxHp: 100, magicDefense: 2, spellPower: 50);
            state.AIBoard.Place(target.Position, target);
            new AttackService().Attack(state, PlayerId.Player, attacker.Position, target.Position);
            Assert.That(target.CurrentHp, Is.EqualTo(expectedHp));
        }

        [TestCase("unit")]
        [TestCase("building")]
        [TestCase("master")]
        public void EveryOccupantKind_MagicAttackReceivesPower(string kind)
        {
            var state = CreateBattleState();
            AddSpellPowerSource(state, PlayerId.Player, 7);
            OccupantState attacker;
            if (kind == "building")
                attacker = new BuildingState("a", "a", PlayerId.Player, new TileCoord(1, 0), true, 10, 100, damageType: DamageType.Magic);
            else if (kind == "master")
                attacker = new MasterState("a", PlayerId.Player, new TileCoord(1, 0), 10, 100);
            else
                attacker = new UnitState("a", "a", PlayerId.Player, new TileCoord(1, 0), AttackType.Melee, 10, 100, true, false, 0, damageType: DamageType.Magic);
            // Synthetic magic Master exercises the common occupant path; real Master remains physical.
            if (kind == "master") typeof(OccupantState).GetProperty("DamageType").SetValue(attacker, DamageType.Magic);
            attacker.HasSummoningSickness = false;
            state.PlayerBoard.Place(attacker.Position, attacker);
            var target = CreateUnit("target", PlayerId.AI, new TileCoord(1, 0), maxHp: 100, magicDefense: 2);
            state.AIBoard.Place(target.Position, target);
            new AttackService().Attack(state, PlayerId.Player, attacker.Position, target.Position);
            Assert.That(target.CurrentHp, Is.EqualTo(85));
        }

        [Test]
        public void MultiHitMagicCounterattack_UsesDefendersPowerBeforeSimultaneousLethalHit()
        {
            var state = CreateBattleState();
            AddSpellPowerSource(state, PlayerId.Player, 70);
            var attacker = new UnitState("attacker", "attacker", PlayerId.Player, new TileCoord(1, 0),
                AttackType.Melee, 100, 100, true, false, 0, damageType: DamageType.Physical);
            var defender = new UnitState("defender", "defender", PlayerId.AI, new TileCoord(1, 0),
                AttackType.Melee, 3, 5, true, false, 0, hitsPerAttack: 3, damageType: DamageType.Magic, spellPower: 10);
            attacker.HasSummoningSickness = false;
            state.PlayerBoard.Place(attacker.Position, attacker);
            state.AIBoard.Place(defender.Position, defender);
            new AttackService().Attack(state, PlayerId.Player, attacker.Position, defender.Position);
            Assert.That(attacker.CurrentHp, Is.EqualTo(61));
            Assert.That(state.AIBoard.GetOccupant(defender.Position), Is.Null);
        }

        [Test]
        public void PiercingMultiHitMagicAttack_AddsPowerOncePerHitAndTarget()
        {
            var state = CreateBattleState();
            var attacker = new UnitState("attacker", "attacker", PlayerId.Player, new TileCoord(1, 0),
                AttackType.Ranged, 3, 100, true, false, 0, hitsPerAttack: 3, damageType: DamageType.Magic, spellPower: 10, hasPiercing: true);
            attacker.HasSummoningSickness = false;
            state.PlayerBoard.Place(attacker.Position, attacker);
            var front = CreateUnit("front", PlayerId.AI, new TileCoord(1, 0), maxHp: 100, magicDefense: 2);
            var back = CreateUnit("back", PlayerId.AI, new TileCoord(1, 1), maxHp: 100, magicDefense: 2);
            state.AIBoard.Place(front.Position, front);
            state.AIBoard.Place(back.Position, back);
            new AttackService().Attack(state, PlayerId.Player, attacker.Position, front.Position);
            Assert.That(front.CurrentHp, Is.EqualTo(67));
            Assert.That(back.CurrentHp, Is.EqualTo(67));
        }

        [TestCase(false, 70)]
        [TestCase(true, 80)]
        public void RedDragonEffect_UsesLiveUnsuppressedPower(bool suppressed, int expectedHp)
        {
            var state = CreateBattleState();
            var source = AddSpellPowerSource(state, PlayerId.Player, 10);
            if (suppressed) source.ApplyErasure();
            var dragon = new UnitState("dragon", "RedDragon", PlayerId.Player, new TileCoord(1, 0), AttackType.Ranged,
                10, 100, true, false, 0, damageType: DamageType.Magic);
            state.PlayerBoard.Place(dragon.Position, dragon);
            var target = CreateUnit("target", PlayerId.AI, new TileCoord(1, 0), maxHp: 100);
            state.AIBoard.Place(target.Position, target);
            new EndTurnService().EndTurn(state);
            Assert.That(target.CurrentHp, Is.EqualTo(expectedHp));
        }

        [Test]
        public void AreaDamage_UsesOneCurrentValueForSimultaneousTargetsIncludingOwnPowerSource()
        {
            var state = CreateBattleState();
            var source = AddSpellPowerSource(state, PlayerId.Player, 10);
            var target = CreateUnit("target", PlayerId.Player, new TileCoord(1, 0), maxHp: 100);
            state.PlayerBoard.Place(target.Position, target);
            state.PersistentEffects.Add(new PersistentEffectState("Firewall", PlayerId.Player, "firewall", 1, "one trigger",
                new ResourceSet(), targetRow: 0, remainingTriggers: 1, effectDamage: 33, effectDamageType: DamageType.Magic, targetsOwnerBoard: true));
            ResolveTurnStart(state, PlayerId.AI);
            Assert.That(state.PlayerBoard.GetOccupant(source.Position), Is.Null);
            Assert.That(target.CurrentHp, Is.EqualTo(57));
        }


        [Test]
        public void ShielderRedirectAndOverflow_DoNotAddSpellPowerTwice()
        {
            var state = CreateBattleState();
            var attacker = new UnitState("attacker", "attacker", PlayerId.Player, new TileCoord(1, 0),
                AttackType.Ranged, 10, 100, true, false, 0, damageType: DamageType.Magic, spellPower: 7);
            attacker.HasSummoningSickness = false;
            state.PlayerBoard.Place(attacker.Position, attacker);
            var shield = new UnitState("shield", "shield", PlayerId.AI, new TileCoord(1, 0),
                AttackType.Melee, 0, 5, true, false, 0, hasShielder: true);
            var target = CreateUnit("target", PlayerId.AI, new TileCoord(1, 1), maxHp: 100);
            state.AIBoard.Place(shield.Position, shield);
            state.AIBoard.Place(target.Position, target);
            new AttackService().Attack(state, PlayerId.Player, attacker.Position, target.Position);
            Assert.That(state.AIBoard.GetOccupant(shield.Position), Is.Null);
            Assert.That(target.CurrentHp, Is.EqualTo(88), "17 raw - 5 absorbed = 12 overflow");
        }


        [TestCase(0)]
        [TestCase(13)]
        public void RealManaPond_PaysSixManaAndImmediatelyGrantsTwentyPower(int level)
        {
            var state = CreateBattleState();
            var database = JsonCardDefinitionDatabase.FromJson(System.IO.File.ReadAllText(
                System.IO.Path.Combine(UnityEngine.Application.dataPath, "Project333/Resources/Project333/Data/cards.json")));
            var provider = database.CreateProvider();
            var upgrades = new InMemoryCardUpgradeLevelProvider();
            upgrades.SetUpgradeLevel(PlayerId.Player, "ManaPond", level);
            state.Player.Hand.Add("ManaPond");
            var manaBefore = state.Player.Resources.Mana;
            var pond = new PlayCardService(provider, new SummonService(), upgrades)
                .PlayBuildingCard(state, PlayerId.Player, "ManaPond", new TileCoord(0, 0));
            Assert.That(state.Player.Resources.Mana, Is.EqualTo(manaBefore - 6));
            Assert.That(pond.CurrentHp, Is.EqualTo(20 + level));
            Assert.That(pond.Attack, Is.Zero);
            Assert.That(pond.SpellPower, Is.EqualTo(20));
            Assert.That(SpellPowerRules.GetTotal(state, PlayerId.Player), Is.EqualTo(20));
            var target = CreateUnit("target", PlayerId.AI, new TileCoord(1, 0), maxHp: 100);
            state.AIBoard.Place(target.Position, target);
            state.Player.Hand.Add("firebolt");
            new SpellService(provider).CastDamageSpell(state, PlayerId.Player, "firebolt", PlayerId.AI, target.Position);
            Assert.That(target.CurrentHp, Is.EqualTo(70), "Firebolt 10 + ManaPond 20");
        }

        private static SpellService CreateDamageSpellService(
            string cardId,
            int damage,
            DamageType damageType)
        {
            var definition = new DamageSpellCardDefinition(
                cardId, cardId, new ResourceSet(), damage, damageType);
            return new SpellService(
                new InMemoryCardDefinitionProvider(new CardDefinition[] { definition }));
        }

        private static UnitState AddSpellPowerSource(
            BattleState battleState,
            PlayerId ownerId,
            int spellPower)
        {
            var board = battleState.GetBoard(ownerId);
            var coord = new TileCoord(0, board.GetOccupant(new TileCoord(0, 0)) == null ? 0 : 1);
            var source = CreateUnit(
                $"source-{ownerId}-{spellPower}", ownerId, coord, spellPower: spellPower);
            board.Place(coord, source);
            return source;
        }

        private static void ResolveTurnStart(BattleState battleState, PlayerId playerId)
        {
            battleState.StartNextTurn(playerId);
            new TurnStartService().ResolveTurnStart(battleState);
        }

        private static UnitState CreateUnit(
            string id,
            PlayerId ownerId,
            TileCoord coord,
            int maxHp = 20,
            int physicalDefense = 0,
            int magicDefense = 0,
            int spellPower = 0,
            bool hasHiding = false,
            bool hasFlying = false)
        {
            return new UnitState(
                id,
                id,
                ownerId,
                coord,
                AttackType.Melee,
                attack: 0,
                maxHp: maxHp,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                damageType: DamageType.Physical,
                physicalDefense: physicalDefense,
                magicDefense: magicDefense,
                hasHiding: hasHiding,
                hasFlying: hasFlying,
                spellPower: spellPower);
        }

        private static BattleState CreateBattleState(
            PlayerId activePlayerId = PlayerId.Player,
            int playerMasterSpellPower = 0)
        {
            var playerBoard = new BoardState();
            var aiBoard = new BoardState();
            var playerMaster = new MasterState(
                "player-master", PlayerId.Player, new TileCoord(2, 1), 3, 333,
                spellPower: playerMasterSpellPower);
            var aiMaster = new MasterState(
                "ai-master", PlayerId.AI, new TileCoord(2, 1), 3, 333);
            playerBoard.Place(playerMaster.Position, playerMaster);
            aiBoard.Place(aiMaster.Position, aiMaster);
            var resources = new ResourceSet(20, 20, 20, 20);
            var player = new PlayerState(
                PlayerId.Player,
                resources.Clone(),
                new DeckState(new[] { "p1", "p2", "p3", "p4" }),
                new HandState(),
                new DiscardState(),
                playerMaster);
            var ai = new PlayerState(
                PlayerId.AI,
                resources.Clone(),
                new DeckState(new[] { "a1", "a2", "a3", "a4" }),
                new HandState(),
                new DiscardState(),
                aiMaster);
            var battleState = new BattleState(player, ai, playerBoard, aiBoard);
            battleState.RestoreRuntimeState(1, activePlayerId, PhaseType.Main);
            return battleState;
        }

        private static JsonCardDefinitionRecord CreateUnitRecord(string id)
        {
            return new JsonCardDefinitionRecord
            {
                Id = id,
                DisplayName = id,
                DefinitionType = JsonCardDefinitionKind.Unit,
                ChargeTileFootprint = ChargeTileFootprint.OneByOne,
                Health = 1,
                Attack = 0,
                MaxAttacksPerTurn = 1,
                HitsPerAttack = 1,
            };
        }

        private static JsonCardDefinitionDatabase CreateDraftCompatibleDatabase(
            JsonCardDefinitionRecord testedCard)
        {
            var database = new JsonCardDefinitionDatabase();
            database.Cards.Add(testedCard);
            for (var index = 0; index < 3; index += 1)
            {
                var legendary = CreateUnitRecord($"legendary-{index}");
                legendary.Rarity = CardRarity.Legendary;
                database.Cards.Add(legendary);
            }

            for (var index = 0; database.Cards.Count < 16; index += 1)
            {
                database.Cards.Add(CreateUnitRecord($"filler-{index}"));
            }

            return database;
        }
    }
}
