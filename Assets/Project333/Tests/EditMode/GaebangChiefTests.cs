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
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class GaebangChiefTests
    {
        private const string CardId = GaebangChiefRules.CardId;
        private static JsonCardDefinitionDatabase Database() => JsonCardDefinitionDatabase.FromJson(File.ReadAllText(
            Path.Combine(Application.dataPath, "Project333/Resources/Project333/Data/cards.json")));

        [Test]
        public void JsonProvider_RegistersConfirmedHumanWithoutInnateTraitsOrStaticGoldBonus()
        {
            var database = Database(); var record = database.Cards.Single(card => card.Id == CardId);
            var definition = (UnitCardDefinition)database.CreateProvider().GetRequired(CardId);
            Assert.That(CardDatabaseValidator.Validate(database).Errors, Is.Empty);
            Assert.That(record.DisplayName, Is.EqualTo("개방 방주")); Assert.That(record.Rarity, Is.EqualTo(CardRarity.Unique));
            Assert.That(record.Affiliation, Is.EqualTo(CardAffiliation.Murim)); Assert.That(record.DefinitionType, Is.EqualTo(JsonCardDefinitionKind.Unit));
            Assert.That(record.IncludeInDraft && record.IncludeInRewards, Is.True); Assert.That(record.EffectText, Is.EqualTo(GaebangChiefRules.EffectDescription));
            Assert.That(record.SpecialEffectText, Is.Empty); Assert.That(record.EffectId, Is.Empty);
            Assert.That(definition.Cost.Qi, Is.EqualTo(7)); Assert.That(definition.Cost.Mana + definition.Cost.Power + definition.Cost.Gold, Is.Zero);
            Assert.That(definition.Attack, Is.EqualTo(10)); Assert.That(definition.Health, Is.EqualTo(60));
            Assert.That(definition.AttackType, Is.EqualTo(AttackType.Melee)); Assert.That(definition.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(definition.PhysicalDefense + definition.MagicDefense, Is.Zero); Assert.That(definition.CanMove, Is.True);
            Assert.That(definition.Races, Is.EqualTo(new[] { UnitRace.Human })); Assert.That(definition.HitsPerAttack, Is.EqualTo(1));
            Assert.That(definition.MaxAttacksPerTurn, Is.EqualTo(1)); Assert.That(CardUpgradeRules.IsCardUpgradeable(definition), Is.True);
            Assert.That(definition.HasRush || definition.HasReplicate || definition.HasBerserker || definition.HasEndure ||
                definition.HasShielder || definition.HasLifeSteal || definition.HasHiding || definition.HasFlying || definition.HasPiercing, Is.False);
        }

        [TestCase(0, 10, 60)]
        [TestCase(1, 10, 61)]
        [TestCase(2, 10, 62)]
        [TestCase(3, 11, 62)]
        [TestCase(4, 11, 63)]
        [TestCase(5, 11, 64)]
        [TestCase(6, 12, 64)]
        [TestCase(7, 12, 65)]
        [TestCase(8, 12, 66)]
        [TestCase(9, 13, 66)]
        [TestCase(10, 13, 67)]
        [TestCase(11, 13, 68)]
        [TestCase(12, 13, 69)]
        [TestCase(13, 14, 69)]
        public void ActualSummon_ForBothOwnersAndGold0_1_3_KeepsPrintedUpgradeSeparate(int level, int attack, int hp)
        {
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            foreach (var gold in new[] { 0, 1, 3 })
            {
                var unit = Summon(level, owner, gold, out var state);
                Assert.That(unit.BaseAttack, Is.EqualTo(attack)); Assert.That(unit.OriginalAttack, Is.EqualTo(attack));
                Assert.That(unit.Attack, Is.EqualTo(attack + gold * 10));
                Assert.That(unit.MaxHp, Is.EqualTo(hp)); Assert.That(unit.OriginalMaxHp, Is.EqualTo(hp)); Assert.That(unit.CurrentHp, Is.EqualTo(hp));
                Assert.That(unit.HasRace(UnitRace.Human), Is.True); Assert.That(unit.CombatStatBuffs, Is.Empty);
                Assert.That(unit.HasSummoningSickness, Is.True); Assert.That(unit.RemainingAttacksThisTurn, Is.EqualTo(1));
                Assert.That(state.GetPlayer(owner).Resources.Qi, Is.Zero); Assert.That(state.GetPlayer(owner).Resources.Gold, Is.EqualTo(gold));
                Assert.That(state.GetPlayer(owner).Hand.Contains(CardId, "chief-selected"), Is.False);
                Assert.That(state.GetPlayer(owner).Hand.Contains(CardId, "chief-reserve"), Is.True);
                var definition = Database().CreateProvider().GetRequired(CardId);
                Assert.That(CardStatDisplay.TryCreate(definition, level, out var printed), Is.True);
                Assert.That(printed.Attack, Is.EqualTo(attack)); Assert.That(printed.Hp, Is.EqualTo(hp));
            }
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void GoldGainSpendAndPermanentBuffs_UpdateImmediatelyWithoutAccumulatingOrConsumingGold(PlayerId owner)
        {
            var unit = Summon(13, owner, 3, out var state); var resources = state.GetPlayer(owner).Resources;
            unit.ApplyCombatStatBuff("fixture-buff", attackBonus: 4, maxHpBonus: 2);
            Assert.That(unit.Attack, Is.EqualTo(48)); Assert.That(unit.BaseAttack, Is.EqualTo(18));
            resources.Add(new ResourceSet(0, 0, 0, 2)); Assert.That(unit.Attack, Is.EqualTo(68));
            resources.Spend(new ResourceSet(0, 0, 0, 4)); Assert.That(unit.Attack, Is.EqualTo(28));
            for (var i = 0; i < 10; i++) Assert.That(unit.Attack, Is.EqualTo(28));
            Assert.That(resources.Gold, Is.EqualTo(1)); Assert.That(unit.CombatStatBuffs.Count, Is.EqualTo(1));
            state.GetOpponent(owner).Resources.Add(new ResourceSet(0, 0, 0, 20)); Assert.That(unit.Attack, Is.EqualTo(28));
            resources.Spend(new ResourceSet(0, 0, 0, 1)); Assert.That(unit.Attack, Is.EqualTo(18));
            Assert.That(unit.OriginalAttack, Is.EqualTo(14));
        }

        [TestCase("Drained")]
        [TestCase("Sealbound")]
        [TestCase("Erasure")]
        [TestCase("Death")]
        public void ExistingSuppressionPolicy_DisablesOnlyGoldEffectAndRestoresTemporarySuppression(string status)
        {
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            {
                var unit = Summon(13, owner, 3, out var state);
                unit.ApplyCombatStatBuff("fixture-buff", attackBonus: 2, maxHpBonus: 4); unit.CurrentHp = 65;
                switch (status)
                {
                    case "Drained": unit.IsDrained = true; break;
                    case "Sealbound": unit.EnterSealbound(1); break;
                    case "Erasure": new ErasureService().Apply(state, unit); break;
                    case "Death": unit.CurrentHp = 0; break;
                }
                Assert.That(unit.Attack, Is.EqualTo(status == "Erasure" ? 14 : 16));
                Assert.That(unit.OriginalAttack, Is.EqualTo(14)); Assert.That(unit.HasRace(UnitRace.Human), Is.True);
                state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 0, 0, 1));
                Assert.That(unit.Attack, Is.EqualTo(status == "Erasure" ? 14 : 16));
                if (status == "Drained") unit.IsDrained = false;
                else if (status == "Sealbound") Assert.That(unit.ResolveSealboundOwnerTurnStart(), Is.True);
                else if (status == "Death") continue;
                else
                {
                    Assert.That(unit.CombatStatBuffs, Is.Empty); Assert.That(unit.MaxHp, Is.EqualTo(69)); Assert.That(unit.CurrentHp, Is.EqualTo(65));
                    continue;
                }
                Assert.That(unit.Attack, Is.EqualTo(56)); Assert.That(unit.BaseAttack, Is.EqualTo(16));
            }
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void ActualGu_UsesNewOwnersCurrentGoldWithoutChangingUpgradeOrCombatBuffs(PlayerId originalOwner)
        {
            var chief = Summon(13, originalOwner, 3, out var state); var controller = state.GetOpponent(originalOwner).Id;
            chief.ApplyCombatStatBuff("fixture-buff", attackBonus: 2, maxHpBonus: 4); chief.CurrentHp = 65;
            SetGold(state.GetPlayer(controller).Resources, 1); state.GetPlayer(controller).Resources.Add(new ResourceSet(0, 10, 0, 0));
            state.SetActivePlayer(controller); state.GetPlayer(controller).Hand.Add(GuRules.CardId, "gu-selected", false);
            new SpellService(Database().CreateProvider()).CastScriptedSpell(state, controller, GuRules.CardId, originalOwner, chief.Position, "gu-selected");
            Assert.That(chief.OwnerId, Is.EqualTo(controller)); Assert.That(chief.Attack, Is.EqualTo(26));
            Assert.That(chief.BaseAttack, Is.EqualTo(16)); Assert.That(chief.OriginalAttack, Is.EqualTo(14));
            Assert.That(chief.CurrentHp, Is.EqualTo(65)); Assert.That(chief.CombatStatBuffs.Count, Is.EqualTo(1));
            Assert.That(state.GetBoard(controller).GetOccupant(chief.Position), Is.SameAs(chief));
            Assert.That(state.GetPlayer(controller).Resources.Gold, Is.EqualTo(1)); Assert.That(state.GetPlayer(originalOwner).Resources.Gold, Is.EqualTo(3));
            state.GetPlayer(originalOwner).Resources.Add(new ResourceSet(0, 0, 0, 10)); Assert.That(chief.Attack, Is.EqualTo(26));
            state.GetPlayer(controller).Resources.Add(new ResourceSet(0, 0, 0, 1)); Assert.That(chief.Attack, Is.EqualTo(36));
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void AiCopyAndOppositeViewerProjection_RebindIndependentOwnerResourcesAndDoNotDoubleBonus(PlayerId owner)
        {
            var chief = Summon(13, owner, 3, out var state); var clone = AiBattleStateCopy.Create(state);
            var copied = clone.GetBoard(owner).GetOccupant(chief.Position);
            Assert.That(copied.Attack, Is.EqualTo(44)); Assert.That(copied.BaseAttack, Is.EqualTo(14));
            clone.GetPlayer(owner).Resources.Spend(new ResourceSet(0, 0, 0, 2)); Assert.That(copied.Attack, Is.EqualTo(24));
            Assert.That(chief.Attack, Is.EqualTo(44)); Assert.That(AiBattleStateCopy.PositionKey(clone), Is.Not.EqualTo(AiBattleStateCopy.PositionKey(state)));
            var enemy = state.GetOpponent(owner).Id; var view = new BattleStateViewFactory().CreateForPlayer(state, "chief", enemy);
            var wire = view.Occupants.Single(occupant => occupant.RuntimeId == chief.RuntimeId);
            Assert.That(wire.Attack, Is.EqualTo(44)); Assert.That(wire.BaseAttack, Is.EqualTo(14));
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(view); var received = projected.AIBoard.GetOccupant(chief.Position);
            Assert.That(received.RuntimeId, Is.EqualTo(chief.RuntimeId)); Assert.That(received.OwnerId, Is.EqualTo(PlayerId.AI));
            Assert.That(received.Attack, Is.EqualTo(44)); Assert.That(received.BaseAttack, Is.EqualTo(14));
            projected.AI.Resources.Spend(new ResourceSet(0, 0, 0, 3)); Assert.That(received.Attack, Is.EqualTo(14));
            Assert.That(chief.Attack, Is.EqualTo(44));
        }

        [TestCase(PlayerId.Player, 0)]
        [TestCase(PlayerId.Player, 1)]
        [TestCase(PlayerId.Player, 3)]
        [TestCase(PlayerId.AI, 0)]
        [TestCase(PlayerId.AI, 1)]
        [TestCase(PlayerId.AI, 3)]
        public void ActualMeleePhysicalAttack_UsesCurrentGoldAndOrdinaryCounterWithoutSpendingGold(PlayerId owner, int gold)
        {
            var chief = Summon(13, owner, gold, out var state); chief.HasSummoningSickness = false;
            var target = new UnitState("chief-victim", "fixture", state.GetOpponent(owner).Id, new TileCoord(0, 0), AttackType.Melee,
                5, 100, true, false, 0, physicalDefense: 2); target.HasSummoningSickness = false;
            state.GetOpponentBoard(owner).Place(target.Position, target);
            new AttackService().Attack(state, owner, chief.Position, target.Position);
            Assert.That(target.CurrentHp, Is.EqualTo(100 - (14 + gold * 10 - 2))); Assert.That(chief.CurrentHp, Is.EqualTo(64));
            Assert.That(state.GetPlayer(owner).Resources.Gold, Is.EqualTo(gold)); Assert.That(chief.CombatStatBuffs, Is.Empty);
            Assert.That(state.ValuePopupEvents.Single(evt => evt.Cause == BattleValueChangeCause.NormalAttack).Amount, Is.EqualTo(12 + gold * 10));
            Assert.That(state.ValuePopupEvents.Single(evt => evt.Cause == BattleValueChangeCause.Counterattack).Amount, Is.EqualTo(5));
        }

        [Test]
        public void VeryLargeResourceValues_DoNotWrapDerivedAttackNegativeOrMutateGold()
        {
            var chief = Summon(13, PlayerId.Player, int.MaxValue, out var state);
            Assert.That(chief.Attack, Is.EqualTo(int.MaxValue)); Assert.That(chief.BaseAttack, Is.EqualTo(14));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(int.MaxValue));
        }

        [TestCase("cost")]
        [TestCase("race")]
        [TestCase("attackType")]
        [TestCase("damageType")]
        [TestCase("effect")]
        [TestCase("trait")]
        [TestCase("stats")]
        [TestCase("rewards")]
        public void Validator_RejectsDivergenceFromTheConfirmedCard(string change)
        {
            var database = Database(); var record = database.Cards.Single(card => card.Id == CardId);
            switch (change)
            {
                case "cost": record.Cost.Qi = 6; break;
                case "race": record.Races = new[] { UnitRace.Orc }; break;
                case "attackType": record.AttackType = AttackType.Ranged; break;
                case "damageType": record.DamageType = DamageType.Magic; break;
                case "effect": record.EffectText = "unconfirmed effect"; break;
                case "trait": record.HasBerserker = true; break;
                case "stats": record.Attack = 11; break;
                case "rewards": record.IncludeInRewards = false; break;
            }
            Assert.That(CardDatabaseValidator.Validate(database).Errors, Has.Some.Contains("GaebangChief must"));
        }

        private static UnitState Summon(int level, PlayerId owner, int gold, out BattleState state)
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner)); state.SetPhase(PhaseType.Main);
            SetGold(state.GetPlayer(owner).Resources, gold); state.GetPlayer(owner).Resources.Add(new ResourceSet(0, 7, 0, 0));
            state.GetPlayer(owner).Hand.Add(CardId, "chief-selected", false); state.GetPlayer(owner).Hand.Add(CardId, "chief-reserve", false);
            var levels = new InMemoryCardUpgradeLevelProvider(); levels.SetUpgradeLevel(owner, CardId, level);
            return new PlayCardService(Database().CreateProvider(), new SummonService(), levels).PlayUnitCard(
                state, owner, CardId, new TileCoord(0, 0), "chief-selected");
        }

        private static void SetGold(ResourceSet resources, int gold)
        {
            resources.Spend(new ResourceSet(0, 0, 0, resources.Gold)); resources.Add(new ResourceSet(0, 0, 0, gold));
        }
    }
}
