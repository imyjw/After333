using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using UnityEditor;

namespace Project333.Tests.EditMode
{
    public sealed class LegacyTests
    {
        [TestCase("Zombie", PlayerId.Player)] [TestCase("Zombie", PlayerId.AI)]
        [TestCase("Skeleton", PlayerId.Player)] [TestCase("Skeleton", PlayerId.AI)]
        [TestCase("SkeletonArcher", PlayerId.Player)] [TestCase("SkeletonArcher", PlayerId.AI)]
        public void HpDeath_DrawsForActualOwnerExactlyOnce(string id, PlayerId owner)
        {
            var state = State(); var unit = Add(state, id, owner, 0);
            var player = state.GetPlayer(owner); var hand = player.Hand.Count; var deck = player.Deck.Count;
            DamageResolutionRules.ApplyEffectDamage(unit, 100, DamageType.Fixed);
            OccupantDestructionService.ResolveDefeatedOccupants(state);
            OccupantDestructionService.ResolveDefeatedOccupants(state);
            OccupantDestructionService.DestroyOccupant(state, owner, unit);
            Assert.That(state.GetBoard(owner).GetOccupant(unit.Position), Is.Null);
            Assert.That(player.Hand.Count, Is.EqualTo(hand + 1));
            Assert.That(player.Deck.Count, Is.EqualTo(deck - 1));
            Assert.That(state.CardDrawEvents.Count, Is.EqualTo(1));
            Assert.That(state.CardDrawEvents[0].OwnerId, Is.EqualTo(owner));
            Assert.That(state.CardDrawEvents[0].SourceCardId, Is.EqualTo(id));
        }

        [TestCase("Zombie")] [TestCase("Skeleton")] [TestCase("SkeletonArcher")]
        public void DirectDestruction_TriggersEvenWithPositiveHp(string id)
        {
            var state = State(); var unit = Add(state, id, PlayerId.Player, 0);
            Assert.That(unit.CurrentHp, Is.GreaterThan(0));
            OccupantDestructionService.DestroyOccupant(state, PlayerId.Player, unit);
            Assert.That(state.CardDrawEvents.Single().SourceCardId, Is.EqualTo(id));
        }

        [TestCase("erasure")] [TestCase("drained")] [TestCase("sealed")]
        public void SuppressedLegacy_DoesNotDraw(string suppression)
        {
            var state = State(); var unit = Add(state, "SkeletonArcher", PlayerId.AI, 0);
            if (suppression == "erasure") unit.ApplyErasure();
            else if (suppression == "drained") unit.IsDrained = true;
            else unit.EnterSealbound(2);
            Assert.That(OccupantDestructionService.HasActiveLegacyEffect(unit), Is.False);
            OccupantDestructionService.DestroyOccupant(state, PlayerId.AI, unit);
            Assert.That(state.CardDrawEvents, Is.Empty);
        }

        [Test]
        public void NuclearExplosion_KillsLegacyUnitsOnBothBoards_AndEachDrawsOnce()
        {
            var state = State();
            var plant = new BuildingState("plant", "NuclearPowerPlant", PlayerId.Player,
                new TileCoord(0, 0), false, 0, 30);
            state.PlayerBoard.Place(plant.Position, plant);
            Add(state, "Skeleton", PlayerId.Player, 1);
            Add(state, "Zombie", PlayerId.AI, 0);
            Add(state, "SkeletonArcher", PlayerId.AI, 1);
            OccupantDestructionService.DestroyOccupant(state, PlayerId.Player, plant);
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(303));
            Assert.That(state.AI.Master.CurrentHp, Is.EqualTo(303));
            Assert.That(state.CardDrawEvents.Count(x => x.OwnerId == PlayerId.Player), Is.EqualTo(1));
            Assert.That(state.CardDrawEvents.Count(x => x.OwnerId == PlayerId.AI), Is.EqualTo(2));
            OccupantDestructionService.ResolveDefeatedOccupants(state);
            Assert.That(state.CardDrawEvents.Count, Is.EqualTo(3));
        }

        [Test]
        public void MassDestruction_RemovesAllBeforeLegacy_AndQueuesEverySourceOnce()
        {
            var state = State();
            Add(state, "Skeleton", PlayerId.Player, 0); Add(state, "Zombie", PlayerId.Player, 1);
            Add(state, "SkeletonArcher", PlayerId.AI, 0);
            OccupantDestructionService.DestroyUnsealedSummons(state);
            Assert.That(state.CardDrawEvents.Count, Is.EqualTo(3));
            Assert.That(state.PlayerBoard.EnumerateOccupants().Count(), Is.EqualTo(1));
            Assert.That(state.AIBoard.EnumerateOccupants().Count(), Is.EqualTo(1));
        }

        [Test]
        public void OwnershipTransfer_DrawsForNewOwner()
        {
            var state = State(); var unit = Add(state, "Zombie", PlayerId.Player, 0);
            state.PlayerBoard.Remove(unit.Position); unit.TransferOwnership(PlayerId.AI);
            state.AIBoard.Place(unit.Position, unit);
            OccupantDestructionService.DestroyOccupant(state, PlayerId.AI, unit);
            Assert.That(state.CardDrawEvents.Single().OwnerId, Is.EqualTo(PlayerId.AI));
        }

        [Test]
        public void TrapReturn_IsNotDeathAndDoesNotTriggerLegacy()
        {
            var state = State(); var unit = Add(state, "Zombie", PlayerId.Player, 0);
            state.AI.Resources.Add(new ResourceSet(0, 0, 0, 10));
            state.ArmedTraps.Add(new ArmedTrapState("return", PlayerId.AI, "TrapReturn",
                TrapTriggerType.EnemyUnitAttacksMaster, TrapEffectType.ReturnAttackerToHand,
                new ResourceSet(0, 0, 0, 4), 1));
            var hand = state.Player.Hand.Count;
            Assert.That(TrapService.TryResolveAcceptedAttack(state, unit, PlayerId.AI,
                state.AI.Master.Position, state.AI.Master), Is.True);
            Assert.That(state.Player.Hand.Count, Is.EqualTo(hand + 1), "Only the returned original card is added.");
            Assert.That(state.CardDrawEvents, Is.Empty);
        }

        [Test]
        public void FullHand_BurnsDrawAndConsumesDeckWithoutAddingAnExtraCard()
        {
            var state = State(); var unit = Add(state, "Skeleton", PlayerId.Player, 0);
            while (state.Player.Hand.Count < state.Player.MaxHandSize) state.Player.Hand.Add("filled");
            var deck = state.Player.Deck.Count;
            OccupantDestructionService.DestroyOccupant(state, PlayerId.Player, unit);
            Assert.That(state.Player.Hand.Count, Is.EqualTo(state.Player.MaxHandSize));
            Assert.That(state.Player.Deck.Count, Is.EqualTo(deck - 1));
            Assert.That(state.CardDrawEvents, Is.Empty);
        }

        [Test]
        public void EmptyDeck_AppliesExistingFatigue_AndEndsBattleAfterLegacyBatch()
        {
            var state = State(); var unit = Add(state, "Zombie", PlayerId.Player, 0);
            while (state.Player.Deck.TryDraw(out _)) { }
            OccupantDestructionService.DestroyOccupant(state, PlayerId.Player, unit);
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(330));
            Assert.That(state.Player.FailedDrawCount, Is.EqualTo(1));
            Assert.That(state.IsEnded, Is.False);
        }

        [Test]
        public void SimultaneousLegacyFatigue_ResolvesBothMastersBeforeChoosingWinner()
        {
            var state = State(); Add(state, "Skeleton", PlayerId.Player, 0); Add(state, "Skeleton", PlayerId.AI, 0);
            while (state.Player.Deck.TryDraw(out _)) { }
            while (state.AI.Deck.TryDraw(out _)) { }
            DamageResolutionRules.ApplyEffectDamage(state.Player.Master, 330, DamageType.Fixed);
            DamageResolutionRules.ApplyEffectDamage(state.AI.Master, 330, DamageType.Fixed);
            OccupantDestructionService.DestroyUnsealedSummons(state);
            Assert.That(state.Player.Master.CurrentHp, Is.Zero);
            Assert.That(state.AI.Master.CurrentHp, Is.Zero);
            Assert.That(state.Result.IsDraw, Is.True, "Board iteration order must not decide a simultaneous lethal fatigue batch.");
        }

        [Test]
        public void AiCopyAndOnlineProjection_RetainLegacyWithoutNewWireFields()
        {
            var state = State(); var original = Add(state, "Skeleton", PlayerId.Player, 0);
            var copy = AiBattleStateCopy.Create(state);
            var copied = copy.PlayerBoard.GetOccupant(original.Position);
            Assert.That(copied.HasLegacy, Is.True);
            OccupantDestructionService.DestroyOccupant(copy, PlayerId.Player, copied);
            Assert.That(copy.CardDrawEvents.Count, Is.EqualTo(1));
            Assert.That(state.CardDrawEvents, Is.Empty);
            var dto = new BattleStateViewFactory().CreateForPlayer(state, "legacy", PlayerId.Player);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(dto);
            Assert.That(projected.PlayerBoard.GetOccupant(original.Position).HasLegacy, Is.True);
            original.ApplyErasure();
            projected = BattleStateViewProjector.CreateLocalPerspectiveState(new BattleStateViewFactory().CreateForPlayer(state, "legacy", PlayerId.Player));
            Assert.That(projected.PlayerBoard.GetOccupant(original.Position).HasLegacy, Is.False);
        }

        [TestCase("Zombie")] [TestCase("Skeleton")] [TestCase("SkeletonArcher")] [TestCase("NuclearPowerPlant")]
        public void JsonNativeAndPreview_ExposeOneLegacyWithExactGenericTooltip(string id)
        {
            var database = JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json"));
            var definition = database.CreateProvider().GetRequired(id);
            var native = AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>("Assets/Project333/ScriptableObjects/StarterTen/Cards/" + id + ".asset");
            Assert.That(native, Is.Not.Null);
            Assert.That(native.ToDefinition().HasLegacy && definition.HasLegacy, Is.True);
            var record = database.Cards.Single(x => x.Id == id);
            Assert.That(native.EffectText, Is.EqualTo(record.EffectText));
            Assert.That(native.SpecialEffectText, Is.EqualTo(record.SpecialEffectText));
            var entries = OccupantSpecialEffectTooltipCatalog.Build(definition);
            var legacy = entries.Single(x => x.Title == "유산");
            Assert.That(legacy.Description, Is.EqualTo("사망/파괴 시 효과를 발동한다."));
        }

        private static UnitState Add(BattleState state, string card, PlayerId owner, int column)
        {
            var unit = new UnitState(Guid.NewGuid().ToString("N"), card, owner, new TileCoord(column, 0),
                AttackType.Melee, 3, 10, true, false, 0);
            state.GetBoard(owner).Place(unit.Position, unit);
            return unit;
        }
        private static BattleState State()
        {
            var deck = Enumerable.Range(0, 33).Select(x => "draw-" + x).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.Player));
            new MulliganService().PassMulligan(state, PlayerId.Player);
            state.RestoreRuntimeState(1, PlayerId.Player, PhaseType.Main);
            state.ClearCardDrawEvents();
            return state;
        }
    }
}
