using System;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Presentation.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class OccupantStatusTooltipCatalogTests
    {
        private static BattleState State()
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            return new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.Player));
        }

        private static UnitState Unit(BattleState state, PlayerId owner = PlayerId.Player, int column = 0,
            string cardId = "Goblin", int spellPower = 0, bool berserker = false)
        {
            var unit = new UnitState(Guid.NewGuid().ToString(), cardId, owner, new TileCoord(column, 0),
                AttackType.Melee, 10, 100, true, false, 0, spellPower: spellPower, hasBerserker: berserker);
            state.GetBoard(owner).Place(unit.Position, unit);
            return unit;
        }

        private static PersistentEffectState Effect(BattleState state, string id, PlayerId owner = PlayerId.AI,
            int remaining = 2, int damage = 20, DamageType type = DamageType.Magic, int row = 0,
            int column = -1, bool ownBoard = false, string target = null)
        {
            var effect = new PersistentEffectState(id, owner, id, 0, string.Empty, new ResourceSet(),
                targetRuntimeId: target, targetRow: row, remainingTriggers: remaining,
                effectDamage: damage, effectDamageType: type, targetsOwnerBoard: ownBoard,
                targetStartColumn: column);
            state.PersistentEffects.Add(effect);
            return effect;
        }

        [Test]
        public void Attached_SourceBuffsRemainSeparateAndSameSourceStacksWithoutCountingLostHp()
        {
            var state = State(); var unit = Unit(state);
            unit.ApplyCombatStatBuff("Hero", attackBonus: 33);
            unit.ApplyCombatStatBuff("Hero", maxHpBonus: 33);
            unit.ApplyCombatStatBuff("RobotFusion", maxHpBonus: 20, magicDefenseBonus: 5);
            unit.CurrentHp -= 70;
            var entries = OccupantStatusTooltipCatalog.BuildAttached(null, unit, state);
            Assert.That(entries.Count, Is.EqualTo(2));
            Assert.That(entries.Select(e => e.Title), Is.EquivalentTo(new[]
            {
                "용사: 공격력 +33, 체력 +33", "로봇 합체: 체력 +20, 마법 방어력 +5"
            }));
            Assert.That(entries.All(e => e.Description == string.Empty), Is.True);
            Assert.That(entries.All(e => e.Kind == OccupantTooltipKind.Buff), Is.True);
        }

        [Test]
        public void Attached_LegacyFallbackContainsOnlyUnattributedBaselineDifference()
        {
            var state = State(); var unit = Unit(state);
            unit.IncreaseBaseAttack(7);
            unit.IncreaseMaxHpAndCurrentHp(9);
            unit.ApplyCombatStatBuff("Daehwandan", attackBonus: 100);
            unit.CurrentHp -= 40;
            var fallback = OccupantStatusTooltipCatalog.BuildAttached(null, unit, state)
                .Single(e => e.Title.StartsWith("능력치 변화:"));
            Assert.That(fallback.Title, Is.EqualTo("능력치 변화: 공격력 +7, 체력 +9"));
            Assert.That(fallback.Description, Is.Empty);
        }

        [Test]
        public void Attached_ErasureRemovesOldAttributionButKeepsLaterBuffAndFallout()
        {
            var state = State(); var unit = Unit(state);
            unit.ApplyCombatStatBuff("Hero", attackBonus: 33); unit.ApplyErasure();
            unit.ApplyCombatStatBuff("Daehwandan", attackBonus: 100); unit.RestoreFallout(2, 0);
            var entries = OccupantStatusTooltipCatalog.BuildAttached(null, unit, state);
            Assert.That(entries.Select(e => e.Title),
                Is.EquivalentTo(new[] { "대환단: 공격력 +100", "망각", "낙진" }));
            Assert.That(entries.All(e => e.Description == string.Empty), Is.True);
        }

        [Test]
        public void Attached_BerserkerAndPackBonusesAreDynamicAndDisappearWhenSuppressed()
        {
            var state = State(); var unit = Unit(state, cardId: "Werewolf", berserker: true);
            Unit(state, column: 1, cardId: "Werewolf");
            unit.CurrentHp -= 12;
            var entries = OccupantStatusTooltipCatalog.BuildAttached(null, unit, state);
            Assert.That(entries.Select(e => e.Title),
                Is.EquivalentTo(new[] { "버서커: 공격력 +12", "웨어울프: 공격력 +10" }));
            Assert.That(entries.All(e => e.Description == string.Empty), Is.True);
            unit.IsDrained = true;
            entries = OccupantStatusTooltipCatalog.BuildAttached(null, unit, state);
            Assert.That(entries.Single().Title, Is.EqualTo("방전"));
            Assert.That(entries.Single().Description, Is.Empty);
        }

        [Test]
        public void Attached_CheonraJimangShowsOnlyNameAndIgnoresExpired()
        {
            var state = State(); var unit = Unit(state);
            Effect(state, "cheonra_jimang", PlayerId.AI, target: unit.RuntimeId);
            Effect(state, "cheonra_jimang", PlayerId.Player, target: unit.RuntimeId).Expire();
            var entry = OccupantStatusTooltipCatalog.BuildAttached(null, unit, state).Single();
            Assert.That(entry.Title, Is.EqualTo("천라지망"));
            Assert.That(entry.Description, Is.Empty);
        }

        [Test]
        public void Attached_DemonKingPendingShowsOnlyNameWithoutDuplicateSeal()
        {
            var state = State(); var unit = Unit(state, cardId: "DemonKing");
            unit.TryEnterDemonKingRevivalSeal(PlayerId.AI, 3);
            unit.ResolveDemonKingRevivalTurnStart(PlayerId.AI, 5);
            var entries = OccupantStatusTooltipCatalog.BuildAttached(null, unit, state);
            Assert.That(entries.Single(e => e.Title == "마왕 부활 대기").Description, Is.Empty);
            Assert.That(entries.Any(e => e.Title == "봉인"), Is.False);
        }

        [Test]
        public void Attached_SealboundShowsOnlyName()
        {
            var state = State(); var unit = Unit(state, PlayerId.AI);
            unit.EnterSealbound(2);
            var entry = OccupantStatusTooltipCatalog.BuildAttached(null, unit, state).Single();
            Assert.That(entry.Title, Is.EqualTo("봉인"));
            Assert.That(entry.Description, Is.Empty);
        }

        [Test]
        public void Attached_InvincibilityKeepsNameWhenInactiveOrSuppressed()
        {
            var state = State(); var unit = Unit(state, PlayerId.AI);
            unit.AddInvincibleEffect(InvincibleDurationType.OwnerTurnOnly);
            var entry = OccupantStatusTooltipCatalog.BuildAttached(null, unit, state).Single();
            Assert.That(entry.Title, Is.EqualTo("무적"));
            Assert.That(entry.Description, Is.Empty);
            unit.ApplyErasure();
            entry = OccupantStatusTooltipCatalog.BuildAttached(null, unit, state).Single(e => e.Title == "무적");
            Assert.That(entry.Description, Is.Empty);
        }

        [Test]
        public void Attached_InvincibleShowsOnlyNameUntilEffectExpires()
        {
            var state = State(); var unit = Unit(state);
            unit.AddInvincibleEffect(InvincibleDurationType.GlobalTurnEnds, 2);
            unit.ResolveInvincibleTurnEnd(PlayerId.AI);
            var entry = OccupantStatusTooltipCatalog.BuildAttached(null, unit, state).Single();
            Assert.That(entry.Title, Is.EqualTo("무적"));
            Assert.That(entry.Description, Is.Empty);
            unit.ResolveInvincibleTurnEnd(PlayerId.Player);
            Assert.That(OccupantStatusTooltipCatalog.BuildAttached(null, unit, state), Is.Empty);
        }

        [Test]
        public void Influences_SpellPowerAndAmplificationAreSeparateTotalsAndSuppressionIsLive()
        {
            var state = State(); var unit = Unit(state, spellPower: 10);
            var ally = Unit(state, column: 1, spellPower: 20);
            Effect(state, MagicAmplificationCircleRules.EffectId, PlayerId.Player);
            Effect(state, MagicAmplificationCircleRules.EffectId, PlayerId.Player);
            Effect(state, MagicAmplificationCircleRules.EffectId, PlayerId.AI);
            var entries = OccupantStatusTooltipCatalog.BuildInfluences(null, unit, state);
            Assert.That(entries.Select(e => e.Title), Is.EquivalentTo(new[] { "주문력 +30", "마법 증폭진: 주문력 +20" }));
            Assert.That(entries.All(e => e.Kind == OccupantTooltipKind.Influence), Is.True);
            Assert.That(entries.All(e => e.Description == string.Empty), Is.True);
            ally.ApplyErasure();
            entries = OccupantStatusTooltipCatalog.BuildInfluences(null, unit, state);
            Assert.That(entries.Any(e => e.Title == "주문력 +10"), Is.True);
            Assert.That(entries.Any(e => e.Title == "마법 증폭진: 주문력 +20"), Is.True);
        }

        [Test]
        public void Influences_AreaSelectionFollowsCurrentPositionAndUsesLiveCasterPower()
        {
            var state = State(); var unit = Unit(state);
            Unit(state, PlayerId.AI, spellPower: 20);
            Effect(state, "firewall", damage: 10);
            Effect(state, "firewall", row: 1);
            Effect(state, "firewall", ownBoard: true);
            Effect(state, BiochemicalBombRules.EffectId, column: 0, damage: 25, type: DamageType.Fixed);
            Effect(state, BiochemicalBombRules.EffectId, column: BiochemicalBombRules.RightAreaStartColumn);
            Effect(state, "firewall").Expire();
            var entries = OccupantStatusTooltipCatalog.BuildInfluences(null, unit, state);
            Assert.That(entries.Count, Is.EqualTo(2));
            Assert.That(entries.Select(e => e.Title),
                Is.EquivalentTo(new[] { "파이어월: 마법 피해 30", "생화학폭탄: 고정 피해 25" }));
            Assert.That(entries.All(e => e.Description == string.Empty), Is.True);
            state.PlayerBoard.Remove(unit.Position);
            unit.Position = new TileCoord(4, 1); state.PlayerBoard.Place(unit.Position, unit);
            entries = OccupantStatusTooltipCatalog.BuildInfluences(null, unit, state);
            Assert.That(entries.Any(e => e.Title == "파이어월: 마법 피해 40"), Is.True);
        }

        [Test]
        public void Influences_DuplicateAreasGroupDamageWithoutShowingCountdowns()
        {
            var state = State(); var unit = Unit(state);
            Effect(state, "firewall", remaining: 1, damage: 10);
            Effect(state, "firewall", remaining: 1, damage: 10);
            Effect(state, "firewall", remaining: 2, damage: 30);
            var entry = OccupantStatusTooltipCatalog.BuildInfluences(null, unit, state).Single();
            Assert.That(entry.Title, Is.EqualTo("파이어월: 마법 피해 10 ×2, 마법 피해 30"));
            Assert.That(entry.Description, Is.Empty);
        }

        [Test]
        public void Influences_TimedBombOnlyAffectsOpponentsAndShowsDamage()
        {
            var state = State(); var unit = Unit(state);
            Effect(state, TimedBombRules.EffectId, remaining: 3, type: DamageType.Fixed);
            Effect(state, TimedBombRules.EffectId, PlayerId.Player, remaining: 1);
            var entry = OccupantStatusTooltipCatalog.BuildInfluences(null, unit, state).Single();
            Assert.That(entry.Title, Is.EqualTo("시한폭탄: 고정 피해 20"));
            Assert.That(entry.Description, Is.Empty);
        }

        [Test]
        public void Influences_HistoricalNuclearCounterDoesNotCreateFieldInfluence()
        {
            var state = State(); var unit = Unit(state);
            state.PlayerBoard.AddFalloutStack();
            Effect(state, "daehwandan", PlayerId.Player);
            Assert.That(OccupantStatusTooltipCatalog.BuildInfluences(null, unit, state),Is.Empty);
            Assert.That(OccupantStatusTooltipCatalog.BuildAttached(null, unit, state).Single().Title, Is.EqualTo("낙진"));
            Assert.That(OccupantStatusTooltipCatalog.BuildInfluences(null, state.Player.Master, state),Is.Empty);
            Assert.That(OccupantStatusTooltipCatalog.BuildAttached(null, state.Player.Master, state).Single().Title, Is.EqualTo("낙진"));
        }

        [TestCase(PlayerId.Player)][TestCase(PlayerId.AI)]
        public void Fallout_UnitAndMasterShowOnlyAttachedDebuff_BuildingsIgnoreLegacyStacks(PlayerId owner)
        {
            var state=State();var unit=Unit(state,owner);
            var building=new BuildingState("legacy-building","PowerPlant",owner,new TileCoord(1,0),false,0,100);
            state.GetBoard(owner).Place(building.Position,building);building.RestoreFallout(4,5);
            state.GetBoard(owner).AddFalloutStack();
            foreach(var occupant in new OccupantState[]{unit,state.GetPlayer(owner).Master})
            {
                Assert.That(OccupantStatusTooltipCatalog.BuildAttached(null,occupant,state).Single().Title,Is.EqualTo("낙진"));
                Assert.That(OccupantStatusTooltipCatalog.BuildInfluences(null,occupant,state),Is.Empty);
            }
            Assert.That(OccupantStatusTooltipCatalog.BuildAttached(null,building,state),Is.Empty);
            Assert.That(OccupantStatusTooltipCatalog.BuildInfluences(null,building,state),Is.Empty);
        }

        [Test]
        public void ViewerProjection_UsesLocalPerspectiveForOpponentOwnedBuffsAndDelayedEffects()
        {
            var state = State(); var unit = Unit(state, PlayerId.AI, spellPower: 10);
            Effect(state, MagicAmplificationCircleRules.EffectId, PlayerId.AI);
            Effect(state, "cheonra_jimang", PlayerId.Player, target: unit.RuntimeId);
            var view = new BattleStateViewFactory().CreateForPlayer(state, "tooltips", PlayerId.AI);
            var projected = BattleStateViewProjector.CreateLocalPerspectiveState(view);
            var local = projected.PlayerBoard.GetOccupant(unit.Position);
            var influences = OccupantStatusTooltipCatalog.BuildInfluences(null, local, projected);
            Assert.That(influences.Select(e => e.Title),
                Is.EquivalentTo(new[] { "주문력 +10", "마법 증폭진: 주문력 +10" }));
            Assert.That(influences.All(e => e.Description == string.Empty), Is.True);
            var attached = OccupantStatusTooltipCatalog.BuildAttached(null, local, projected).Single();
            Assert.That(attached.Title, Is.EqualTo("천라지망"));
            Assert.That(attached.Description, Is.Empty);
        }

        [Test]
        public void EmptyContext_LeavesOnlyAttachedStateAndNullOccupantIsRejected()
        {
            var unit = Unit(State()); unit.ApplyHuanShu();
            Assert.That(OccupantStatusTooltipCatalog.BuildAttached(null, unit, null).Single().Title, Is.EqualTo("환술"));
            Assert.That(OccupantStatusTooltipCatalog.BuildInfluences(null, unit, null), Is.Empty);
            Assert.Throws<ArgumentNullException>(() => OccupantStatusTooltipCatalog.BuildAttached(null, null, null));
        }
    }
}
