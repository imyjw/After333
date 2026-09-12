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
        public void Firewall_CapturesSpellPowerAtCastAndUsesItForEveryTargetAndTrigger()
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

            Assert.That(battleState.PersistentEffects[0].CapturedSpellPower, Is.EqualTo(5));
            battleState.PlayerBoard.Remove(source.Position);
            AddSpellPowerSource(battleState, PlayerId.Player, 20);

            ResolveTurnStart(battleState, PlayerId.AI);
            Assert.That(first.CurrentHp, Is.EqualTo(27));
            Assert.That(second.CurrentHp, Is.EqualTo(27));

            ResolveTurnStart(battleState, PlayerId.Player);
            Assert.That(first.CurrentHp, Is.EqualTo(14));
            Assert.That(second.CurrentHp, Is.EqualTo(14));
            Assert.That(battleState.PersistentEffects[0].IsExpired, Is.True);
        }

        [Test]
        public void StateViewRoundTrip_PreservesOccupantAndCapturedSpellPower()
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
            Assert.That(projected.PersistentEffects[0].CapturedSpellPower, Is.EqualTo(7));
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
