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
using Project333.Runtime.Presentation.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class NuclearTests
    {
        private static ICardDefinitionProvider Provider() =>
            JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).CreateProvider();

        private static BattleState State(PlayerId actor = PlayerId.Player)
        {
            var deck=Enumerable.Repeat("Goblin",33).ToArray();
            var state=new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck,deck,actor));
            state.SetPhase(PhaseType.Main);
            return state;
        }
        private static UnitState Unit(BattleState state,PlayerId owner,int col=0,int row=0,int hp=100,bool endure=false,string id="Goblin")
        {
            var u=new UnitState(Guid.NewGuid().ToString(),id,owner,new TileCoord(col,row),AttackType.Melee,1,hp,true,false,0,
                hasEndure:endure,physicalDefense:99,magicDefense:99);
            state.GetBoard(owner).Place(u.Position,u);
            return u;
        }
        private static void Cast(BattleState state,PlayerId caster)
        {
            state.RestoreRuntimeState(state.TurnNumber,caster,PhaseType.Main);
            state.GetPlayer(caster).Hand.Add("Nuclear");
            state.GetPlayer(caster).Resources.Add(new ResourceSet(0,0,NuclearRules.PowerCost,0));
            new SpellService(Provider()).CastScriptedSpell(state,caster,"Nuclear");
        }
        private static void Start(BattleState state,PlayerId owner)
        {
            state.RestoreRuntimeState(state.TurnNumber+1,owner,PhaseType.TurnStart);
            new TurnStartService().ResolveTurnStart(state);
        }

        [Test]
        public void Definition_IsLegendarySciencePowerThirteen_UpgradeableFixedDamage_InDraftAndRewards()
        {
            var json=File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json");
            Assert.That(CardDatabaseValidator.ValidateJson(json).Errors,Is.Empty);
            var record=JsonCardDefinitionDatabase.FromJson(json).Cards.Single(c=>c.Id=="Nuclear");
            Assert.That(record.Rarity,Is.EqualTo(CardRarity.Legendary));
            Assert.That(record.Affiliation,Is.EqualTo(CardAffiliation.ScienceCivilization));
            Assert.That(record.IncludeInDraft,Is.True);
            Assert.That(record.IncludeInRewards,Is.True);
            Assert.That(record.Cost.Power,Is.EqualTo(13));
            Assert.That(record.Damage,Is.EqualTo(NuclearRules.BaseDamage));
            Assert.That(record.DamageType,Is.EqualTo(DamageType.Fixed));
            Assert.That(CardUpgradeRules.IsCardUpgradeable(Provider().GetRequired("Nuclear")),Is.True);
        }

        [Test]
        public void Cast_DamagesEnemyBoardAndMaster_HonorsProtectionAndPreservesAllies()
        {
            var state=State();
            var ally=Unit(state,PlayerId.Player);
            var invincible=Unit(state,PlayerId.AI);
            invincible.AddInvincibleEffect(InvincibleDurationType.Always,0);
            var endure=Unit(state,PlayerId.AI,1,endure:true);
            var sealedUnit=Unit(state,PlayerId.AI,3);sealedUnit.EnterSealbound(3);
            var survivor=Unit(state,PlayerId.AI,4,hp:250);
            Cast(state,PlayerId.Player);
            Assert.That(state.PlayerBoard.GetOccupant(ally.Position),Is.SameAs(ally));
            Assert.That(ally.CurrentHp,Is.EqualTo(100));
            Assert.That(state.AIBoard.GetOccupant(invincible.Position),Is.SameAs(invincible));
            Assert.That(invincible.CurrentHp,Is.EqualTo(100));
            Assert.That(invincible.FalloutStacks,Is.EqualTo(1));
            Assert.That(state.AIBoard.GetOccupant(endure.Position),Is.SameAs(endure));
            Assert.That(endure.CurrentHp,Is.EqualTo(1));Assert.That(endure.EndureUsed,Is.True);
            Assert.That(survivor.CurrentHp,Is.EqualTo(150),"Fixed damage ignores both 99-point defenses.");
            Assert.That(survivor.FalloutStacks,Is.EqualTo(1));
            Assert.That(state.AIBoard.GetOccupant(sealedUnit.Position),Is.SameAs(sealedUnit));
            Assert.That(sealedUnit.CurrentHp,Is.EqualTo(100));
            Assert.That(sealedUnit.FalloutStacks,Is.Zero);
            Assert.That(state.Player.Master.CurrentHp,Is.EqualTo(state.Player.Master.MaxHp));
            Assert.That(state.AI.Master.CurrentHp,Is.EqualTo(state.AI.Master.MaxHp-100));
            Assert.That(state.AI.Master.FalloutStacks,Is.EqualTo(1));
            Assert.That(state.AIBoard.FalloutStacks,Is.EqualTo(1));
            Assert.That(state.PlayerBoard.FalloutStacks,Is.Zero);
            Assert.That(state.Player.Hand.CardIds,Does.Not.Contain("Nuclear"));
            Assert.That(state.Player.Discard.CardIds,Does.Contain("Nuclear"));
            Assert.That(state.Player.Resources.Power,Is.Zero);
        }

        [Test]
        public void MultipleEnemyPowerPlants_ExplodeOnceEachAfterAllEnemyDamage_WithAlliedPlantSurviving()
        {
            var state=State();
            var alliedPlant=new BuildingState("allied-plant","NuclearPowerPlant",PlayerId.Player,new TileCoord(0,0),false,0,100);
            state.PlayerBoard.Place(alliedPlant.Position,alliedPlant);
            foreach(var column in new[]{0,1})
                state.AIBoard.Place(new TileCoord(column,0),new BuildingState("enemy-plant-"+column,"NuclearPowerPlant",PlayerId.AI,new TileCoord(column,0),false,0,100));
            var sealedUnit=Unit(state,PlayerId.AI,3);sealedUnit.EnterSealbound(2);
            var before=state.Player.Master.CurrentHp;
            Cast(state,PlayerId.Player);
            Assert.That(state.Player.Master.CurrentHp,Is.EqualTo(before-60));
            Assert.That(state.AI.Master.CurrentHp,Is.EqualTo(before-100-60));
            Assert.That(state.PlayerBoard.GetOccupant(alliedPlant.Position),Is.SameAs(alliedPlant));
            Assert.That(alliedPlant.CurrentHp,Is.EqualTo(40));
            Assert.That(sealedUnit.CurrentHp,Is.EqualTo(100));
            Assert.That(state.ValuePopupEvents.Count(e=>e.Cause==BattleValueChangeCause.NuclearPowerPlant&&e.RuntimeId==state.Player.Master.RuntimeId),Is.EqualTo(2));
            var events=state.ValuePopupEvents.ToList();
            var lastNuclearDamage=events.FindLastIndex(e=>e.SourceCardId=="Nuclear"&&e.Cause==BattleValueChangeCause.Spell);
            var firstExplosion=events.FindIndex(e=>e.Cause==BattleValueChangeCause.NuclearPowerPlant);
            Assert.That(lastNuclearDamage,Is.LessThan(firstExplosion),"Every immediate AoE packet precedes queued reactor explosions.");
        }

        [TestCase(PlayerId.Player,0)][TestCase(PlayerId.Player,1)][TestCase(PlayerId.Player,3)][TestCase(PlayerId.Player,13)]
        [TestCase(PlayerId.AI,0)][TestCase(PlayerId.AI,1)][TestCase(PlayerId.AI,3)][TestCase(PlayerId.AI,13)]
        public void Cast_UsesCasterUpgradeOnceForSelectedTemporaryRuntime_AndNeverAddsSpellPower(PlayerId caster,int level)
        {
            var state=State(caster);var enemy=state.GetOpponent(caster).Id;
            var target=Unit(state,enemy,hp:500);
            var mage=new UnitState("spell-power","ManaWeaver",caster,new TileCoord(0,0),AttackType.Melee,0,500,true,false,0,spellPower:100);
            state.GetBoard(caster).Place(mage.Position,mage);
            var hand=state.GetPlayer(caster).Hand;
            var permanent=hand.Add("Nuclear");var selected=hand.AddTemporaryReplicate("Nuclear");
            state.GetPlayer(caster).Resources.Add(new ResourceSet(0,0,NuclearRules.PowerCost,0));
            var levels=new InMemoryCardUpgradeLevelProvider();levels.SetUpgradeLevel(caster,"Nuclear",level);
            levels.SetUpgradeLevel(enemy,"Nuclear",13-level);
            var provider=Provider();var baseDefinition=(ScriptedSpellCardDefinition)provider.GetRequired("Nuclear");
            new SpellService(provider,levels).CastScriptedSpell(state,caster,"Nuclear",selected.RuntimeId);
            Assert.That(target.CurrentHp,Is.EqualTo(500-100-level));
            Assert.That(state.GetPlayer(enemy).Master.CurrentHp,Is.EqualTo(333-100-level));
            Assert.That(mage.CurrentHp,Is.EqualTo(500));
            Assert.That(hand.Contains("Nuclear",selected.RuntimeId),Is.False);
            Assert.That(hand.Contains("Nuclear",permanent.RuntimeId),Is.True);
            Assert.That(baseDefinition.Damage,Is.EqualTo(100),"Shared base definitions are not modified or pre-upgraded.");
            var damage=state.ValuePopupEvents.Single(e=>e.RuntimeId==target.RuntimeId&&e.SourceCardId=="Nuclear");
            Assert.That(damage.Amount,Is.EqualTo(100+level));Assert.That(damage.DamageType,Is.EqualTo(DamageType.Fixed));
            Assert.That(damage.SourceOwnerId,Is.EqualTo(caster));Assert.That(damage.Cause,Is.EqualTo(BattleValueChangeCause.Spell));
            Assert.That(target.FalloutStacks,Is.EqualTo(1));
        }

        [TestCase("normal",300)][TestCase("erasure",300)][TestCase("drained",100)][TestCase("invincible",400)][TestCase("sealed",400)]
        public void ImmediateDamage_UsesStandardFixedProtectionAndSuppression(string status,int expected)
        {
            var state=State();var target=Unit(state,PlayerId.AI,hp:400);
            if(status=="erasure")target.ApplyErasure();
            if(status=="drained")target.IsDrained=true;
            if(status=="invincible")target.AddInvincibleEffect(InvincibleDurationType.Always,0);
            if(status=="sealed")target.EnterSealbound(2);
            Cast(state,PlayerId.Player);
            Assert.That(target.CurrentHp,Is.EqualTo(expected));
            Assert.That(target.FalloutStacks,Is.EqualTo(status=="sealed"?0:1));
            Assert.That(state.AI.Master.CurrentHp,Is.EqualTo(233));
        }

        [TestCase(PlayerId.Player)][TestCase(PlayerId.AI)]
        public void ImmediateDamage_CanDefeatEnemyMasterWithoutDamagingCaster(PlayerId caster)
        {
            var state=State(caster);var enemy=state.GetOpponent(caster);
            enemy.Master.CurrentHp=99;
            Cast(state,caster);
            Assert.That(enemy.Master.CurrentHp,Is.EqualTo(-1));
            Assert.That(state.GetPlayer(caster).Master.CurrentHp,Is.EqualTo(333));
            Assert.That(state.IsEnded,Is.True);Assert.That(state.Result.Winner,Is.EqualTo(caster));
            Assert.That(state.GetBoard(enemy.Id).FalloutStacks,Is.EqualTo(1));
        }

        [Test]
        public void ImmediateDamage_MasterInvinciblePreventsDamageButEnemyBoardStillGetsFallout()
        {
            var state=State();state.AI.Master.AddInvincibleEffect(InvincibleDurationType.Always,0);
            Cast(state,PlayerId.Player);
            Assert.That(state.AI.Master.CurrentHp,Is.EqualTo(333));Assert.That(state.IsEnded,Is.False);
            Assert.That(state.AIBoard.FalloutStacks,Is.EqualTo(1));Assert.That(state.AI.Master.FalloutStacks,Is.EqualTo(1));
            var prevented=state.ValuePopupEvents.Single(e=>e.RuntimeId==state.AI.Master.RuntimeId&&e.SourceCardId=="Nuclear");
            Assert.That(prevented.IsInvinciblePrevented,Is.True);Assert.That(prevented.Amount,Is.Zero);
        }

        [Test]
        public void ImmediateDamage_ResolvesReactorChainBeforeComparingBothDefeatedMasters()
        {
            var state=State();state.Player.Master.CurrentHp=20;state.AI.Master.CurrentHp=120;
            state.AIBoard.Place(new TileCoord(0,0),new BuildingState("reactor","NuclearPowerPlant",PlayerId.AI,new TileCoord(0,0),false,0,100));
            Cast(state,PlayerId.Player);
            Assert.That(state.Player.Master.CurrentHp,Is.EqualTo(-10));Assert.That(state.AI.Master.CurrentHp,Is.EqualTo(-10));
            Assert.That(state.IsEnded,Is.True);Assert.That(state.Result.IsDraw,Is.True);
        }

        [Test]
        public void DemonKing_EntersRevivalSealInsteadOfBeingPermanentlyRemoved()
        {
            var state=State();var king=Unit(state,PlayerId.AI,id:"DemonKing");
            Cast(state,PlayerId.Player);
            Assert.That(state.AIBoard.GetOccupant(king.Position),Is.SameAs(king));
            Assert.That(king.IsDemonKingRevivalPending,Is.True);
            Assert.That(king.FalloutStacks,Is.EqualTo(1));
            for(var i=0;i<4;i++) { Start(state,PlayerId.AI); if(!king.IsDemonKingRevivalPending)break; Start(state,PlayerId.Player); }
            Assert.That(king.IsDemonKingRevivalPending,Is.False);
            Assert.That(king.FalloutStacks,Is.EqualTo(1));
        }

        [Test]
        public void RepeatedCast_StacksCurrentMasterAndUnits_FutureSummonsAndBuildingsNeverTick()
        {
            var state=State();var unit=Unit(state,PlayerId.AI,hp:300);
            Cast(state,PlayerId.Player);Cast(state,PlayerId.Player);
            var future=Unit(state,PlayerId.AI,3);
            Assert.That(future.FalloutStacks,Is.Zero);
            var building=new BuildingState("plant","PowerPlant",PlayerId.AI,new TileCoord(1,0),false,0,100);
            state.AIBoard.Place(building.Position,building);
            var ally=Unit(state,PlayerId.Player);
            Assert.That(unit.FalloutStacks,Is.EqualTo(2));
            Assert.That(state.AI.Master.FalloutStacks,Is.EqualTo(2));
            Assert.That(building.FalloutStacks,Is.Zero);
            Start(state,PlayerId.Player);
            Assert.That(unit.CurrentHp,Is.EqualTo(80));Assert.That(building.CurrentHp,Is.EqualTo(100));
            Assert.That(state.AI.Master.CurrentHp,Is.EqualTo(113));
            Start(state,PlayerId.AI);
            Assert.That(unit.CurrentHp,Is.EqualTo(60));Assert.That(building.CurrentHp,Is.EqualTo(100));
            Assert.That(state.AI.Master.CurrentHp,Is.EqualTo(93));
            Assert.That(ally.CurrentHp,Is.EqualTo(100));
            Assert.That(future.CurrentHp,Is.EqualTo(100));
            Assert.That(state.AI.Master.FalloutStacks,Is.EqualTo(2));
        }

        [TestCase("normal",90)][TestCase("erasure",90)][TestCase("drained",70)][TestCase("invincible",100)][TestCase("sealed",100)]
        public void Tick_AppliesExistingFixedDamageRules(string status,int expected)
        {
            var state=State();var target=Unit(state,PlayerId.AI,hp:200);Cast(state,PlayerId.Player);
            if(status=="erasure")target.ApplyErasure();
            if(status=="drained")target.IsDrained=true;
            if(status=="invincible")target.AddInvincibleEffect(InvincibleDurationType.Always,0);
            if(status=="sealed")target.EnterSealbound(2);
            var spellPower=new UnitState("mage","ManaWeaver",PlayerId.Player,new TileCoord(0,0),AttackType.Melee,0,100,true,false,0,spellPower:100);
            state.PlayerBoard.Place(spellPower.Position,spellPower);
            Start(state,PlayerId.Player);
            Assert.That(target.CurrentHp,Is.EqualTo(expected));
            Assert.That(target.FalloutStacks,Is.EqualTo(1));
        }

        [Test]
        public void StackedTick_IsOnePacket_EndureSurvivesOnceThenDies()
        {
            var state=State();var target=Unit(state,PlayerId.AI,hp:215,endure:true);
            Cast(state,PlayerId.Player);Cast(state,PlayerId.Player);
            Start(state,PlayerId.Player);
            Assert.That(target.CurrentHp,Is.EqualTo(1));
            Assert.That(target.EndureUsed,Is.True);
            Start(state,PlayerId.AI);
            Assert.That(state.AIBoard.GetOccupant(target.Position),Is.Null);
        }

        [Test]
        public void Sealbound_ImmuneAtCast_DoesNotAcquireFalloutOnRelease()
        {
            var state=State();var target=Unit(state,PlayerId.AI);target.EnterSealbound(1);
            Cast(state,PlayerId.Player);Cast(state,PlayerId.Player);
            Assert.That(target.FalloutStacks,Is.Zero);
            Start(state,PlayerId.AI);
            Assert.That(target.CurrentHp,Is.EqualTo(100));
            Assert.That(target.IsSealbound,Is.False);
            Assert.That(target.FalloutStacks,Is.Zero);
            Start(state,PlayerId.Player);
            Assert.That(target.CurrentHp,Is.EqualTo(100));
        }

        [Test]
        public void GuAndErasure_PreserveDebuff_ReturningToSameBoardDoesNotDuplicate()
        {
            var state=State();var target=Unit(state,PlayerId.AI,hp:200);Cast(state,PlayerId.Player);
            state.Player.Hand.Add("Gu");state.Player.Resources.Add(new ResourceSet(0,10,0,0));
            new SpellService(Provider()).CastScriptedSpell(state,PlayerId.Player,"Gu",PlayerId.AI,target.Position);
            Assert.That(target.OwnerId,Is.EqualTo(PlayerId.Player));
            Assert.That(target.FalloutStacks,Is.EqualTo(1));
            target.ApplyErasure();
            Start(state,PlayerId.AI);
            Assert.That(target.CurrentHp,Is.EqualTo(90));
            state.PlayerBoard.Remove(target.Position);target.TransferOwnership(PlayerId.AI);
            state.AIBoard.Place(new TileCoord(0,0),target);
            Assert.That(target.FalloutStacks,Is.EqualTo(1));
        }

        [TestCase(PlayerId.Player)][TestCase(PlayerId.AI)]
        public void ViewAndAiCopy_PreserveBoardAndTransferredExposures(PlayerId viewer)
        {
            var state=State();
            var target=Unit(state,PlayerId.AI);
            state.AIBoard.AddFalloutStack();state.AIBoard.AddFalloutStack();
            state.AIBoard.Remove(target.Position);target.TransferOwnership(PlayerId.Player);state.PlayerBoard.Place(new TileCoord(0,0),target);
            state.PlayerBoard.AddFalloutStack();
            var key=AiBattleStateCopy.PositionKey(state);
            var copy=AiBattleStateCopy.Create(state);
            Assert.That(AiBattleStateCopy.PositionKey(copy),Is.EqualTo(key));
            var view=new BattleStateViewFactory().CreateForPlayer(state,"nuclear",viewer);
            var restored=BattleStateViewProjector.CreateLocalPerspectiveState(view);
            var board=viewer==PlayerId.Player?restored.PlayerBoard:restored.AIBoard;
            var unit=board.GetOccupant(new TileCoord(0,0));
            Assert.That(unit.FalloutStacks,Is.EqualTo(3));
            Assert.That(unit.FalloutPlayerStacks,Is.EqualTo(viewer==PlayerId.Player?1:2));
            Assert.That(restored.PlayerBoard.FalloutStacks,Is.EqualTo(viewer==PlayerId.Player?1:2));
            board.Remove(unit.Position);board.Place(unit.Position,unit);
            Assert.That(unit.FalloutStacks,Is.EqualTo(3));
        }

        [Test]
        public void InsufficientResources_LeavesCardBoardAndExposureUnchanged()
        {
            var state=State();var unit=Unit(state,PlayerId.AI);
            state.Player.Resources.Spend(state.Player.Resources.Clone());state.Player.Hand.Add("Nuclear");
            Assert.Throws<InvalidOperationException>(()=>new SpellService(Provider()).CastScriptedSpell(state,PlayerId.Player,"Nuclear"));
            Assert.That(state.AIBoard.FalloutStacks,Is.Zero);
            Assert.That(state.AIBoard.GetOccupant(unit.Position),Is.SameAs(unit));
            Assert.That(state.Player.Hand.CardIds,Does.Contain("Nuclear"));
        }

        [Test]
        public void Tooltip_ShowsCurrentFalloutTitleWithoutDuplicateDescription()
        {
            var state=State();var target=Unit(state,PlayerId.AI);state.AIBoard.AddFalloutStack();state.AIBoard.AddFalloutStack();
            target.ApplyErasure();
            var entry=OccupantStatusTooltipCatalog.BuildAttached(null,target,state)
                .Single(e=>e.Title=="낙진: 양쪽 턴 시작마다 10의 고정 피해를 입는다.");
            Assert.That(entry.Description,Is.Empty);
        }

        [TestCase(false)][TestCase(true)]
        public void SummoningSealedDefinition_AfterNuclearNeverAcquiresFalloutAndKeepsActionsDisabled(bool building)
        {
            var state=State();state.PlayerBoard.AddFalloutStack();
            CardDefinition definition=building
                ? (CardDefinition)new BuildingCardDefinition("sealed","sealed",new ResourceSet(),false,0,100,sealboundOwnerTurnStarts:1)
                : new UnitCardDefinition("sealed","sealed",new ResourceSet(),AttackType.Melee,1,100,true,false,0,hasRush:true,sealboundOwnerTurnStarts:1);
            state.Player.Hand.Add("sealed");
            var play=new PlayCardService(new InMemoryCardDefinitionProvider(new[]{definition}));
            OccupantState target=building
                ? (OccupantState)play.PlayBuildingCard(state,PlayerId.Player,"sealed",new TileCoord(0,0))
                : play.PlayUnitCard(state,PlayerId.Player,"sealed",new TileCoord(0,0));
            Assert.That(target.FalloutStacks,Is.Zero);
            Assert.That(target.RemainingAttacksThisTurn,Is.Zero);
            Start(state,PlayerId.Player);
            Assert.That(target.IsSealbound,Is.False);
            Assert.That(target.CurrentHp,Is.EqualTo(100));
            Assert.That(target.FalloutStacks,Is.Zero);
            Start(state,PlayerId.AI);
            Assert.That(target.CurrentHp,Is.EqualTo(100));
        }

        [Test]
        public void Ai_UsesNuclearImmediateEnemyDamageToWinWithoutAssumingHighHealthReactorDestruction()
        {
            var state=State(PlayerId.AI);
            state.Player.Master.CurrentHp=20;
            var reactor=new BuildingState("reactor","NuclearPowerPlant",PlayerId.Player,new TileCoord(0,0),false,0,250);
            state.PlayerBoard.Place(reactor.Position,reactor);
            state.AI.Hand.Add("Nuclear");state.AI.Resources.Add(new ResourceSet(0,0,NuclearRules.PowerCost,0));
            var before=AiBattleStateCopy.PositionKey(state);
            // This verifies the winning action, independently of a cold Editor/JIT time budget.
            var planner=new AiDecisionService(Provider(),new TargetingService(),
                ZeroCardUpgradeLevelProvider.Instance,new AiSearchOptions { MaxMilliseconds=5000 });
            var command=planner.GetNextCommand(state);
            var action=command as Project333.Runtime.Application.Commands.CastScriptedSpellCommand;
            Assert.That(action,Is.Not.Null,$"Chosen action: {command?.GetType().Name}; budget exhausted: {planner.LastBudgetExhausted}");
            Assert.That(action.CardId,Is.EqualTo("Nuclear"));
            Assert.That(AiBattleStateCopy.PositionKey(state),Is.EqualTo(before));
        }

        [Test]
        public void FalloutScope_ImmediateDamageStillHitsBuildings_ButOnlyUnitsAndMasterTick()
        {
            var state=State();var unit=Unit(state,PlayerId.AI,hp:200);
            var building=new BuildingState("building","PowerPlant",PlayerId.AI,new TileCoord(1,0),false,0,200);
            state.AIBoard.Place(building.Position,building);
            Cast(state,PlayerId.Player);
            Assert.That(unit.CurrentHp,Is.EqualTo(100));Assert.That(building.CurrentHp,Is.EqualTo(100));
            Assert.That(unit.FalloutStacks,Is.EqualTo(1));Assert.That(building.FalloutStacks,Is.Zero);
            Assert.That(state.AI.Master.FalloutStacks,Is.EqualTo(1));
            Start(state,PlayerId.Player);
            Assert.That(unit.CurrentHp,Is.EqualTo(90));Assert.That(building.CurrentHp,Is.EqualTo(100));
            Assert.That(state.AI.Master.CurrentHp,Is.EqualTo(223));Assert.That(state.Player.Master.CurrentHp,Is.EqualTo(333));
        }

        [Test]
        public void FalloutScope_EachCastAddsOnlyOneStackToItsCurrentUnits_BuildingHistoryIsDiscarded()
        {
            var state=State();var unit=Unit(state,PlayerId.AI,hp:500);
            var building=new BuildingState("existing-building","PowerPlant",PlayerId.AI,new TileCoord(1,0),false,0,500);
            state.AIBoard.Place(building.Position,building);building.RestoreFallout(6,7);
            state.AIBoard.AddFalloutStack();
            var futureUnit=Unit(state,PlayerId.AI,3,hp:500);
            var futureBuilding=new BuildingState("future-building","PowerPlant",PlayerId.AI,new TileCoord(4,0),false,0,500);
            state.AIBoard.Place(futureBuilding.Position,futureBuilding);state.AIBoard.AddFalloutStack();
            Assert.That(unit.FalloutStacks,Is.EqualTo(2));Assert.That(futureUnit.FalloutStacks,Is.EqualTo(1));
            Assert.That(state.AI.Master.FalloutStacks,Is.EqualTo(2));
            Assert.That(building.FalloutPlayerStacks,Is.Zero);Assert.That(building.FalloutAiStacks,Is.Zero);
            Assert.That(futureBuilding.FalloutStacks,Is.Zero);
            Start(state,PlayerId.Player);
            Assert.That(unit.CurrentHp,Is.EqualTo(480));Assert.That(futureUnit.CurrentHp,Is.EqualTo(490));
            Assert.That(building.CurrentHp,Is.EqualTo(500));Assert.That(futureBuilding.CurrentHp,Is.EqualTo(500));
            Assert.That(state.AI.Master.CurrentHp,Is.EqualTo(313));
        }

        [TestCase(PlayerId.Player)][TestCase(PlayerId.AI)]
        public void FalloutScope_ViewAndAiCopyKeepMasterExposure_IgnoreLegacyBuildingExposure(PlayerId viewer)
        {
            var state=State();state.AIBoard.AddFalloutStack();state.AIBoard.AddFalloutStack();
            var building=new BuildingState("legacy-building","PowerPlant",PlayerId.AI,new TileCoord(0,0),false,0,500);
            state.AIBoard.Place(building.Position,building);building.RestoreFallout(5,7);
            var copy=AiBattleStateCopy.Create(state);
            Assert.That(AiBattleStateCopy.PositionKey(copy),Is.EqualTo(AiBattleStateCopy.PositionKey(state)));
            Assert.That(copy.AI.Master.FalloutStacks,Is.EqualTo(2));
            Assert.That(copy.AIBoard.GetOccupant(building.Position).FalloutStacks,Is.Zero);
            var view=new BattleStateViewFactory().CreateForPlayer(state,"fallout-scope",viewer);
            var oldBuilding=view.Occupants.Single(o=>o.RuntimeId==building.RuntimeId);
            oldBuilding.FalloutPlayerStacks=5;oldBuilding.FalloutAiStacks=7;
            var restored=BattleStateViewProjector.CreateLocalPerspectiveState(view);
            var localOwner=viewer==PlayerId.Player?PlayerId.AI:PlayerId.Player;
            var master=restored.GetPlayer(localOwner).Master;
            Assert.That(master.FalloutStacks,Is.EqualTo(2));
            Assert.That(restored.GetBoard(localOwner).GetOccupant(master.Position),Is.SameAs(master));
            Assert.That(restored.GetBoard(localOwner).GetOccupant(building.Position).FalloutStacks,Is.Zero);
            Start(restored,PlayerId.Player);
            Assert.That(master.CurrentHp,Is.EqualTo(313));
            Assert.That(restored.GetBoard(localOwner).GetOccupant(building.Position).CurrentHp,Is.EqualTo(500));
        }

        [Test]
        public void FalloutScope_NewAndRestoredMastersNeverInheritLegacyBoardExposure()
        {
            var board=new BoardState();board.RestoreFalloutStacks(4);
            var master=new MasterState("restored-master",PlayerId.AI,new TileCoord(2,1),3,333);
            master.RestoreFallout(2,3);board.Place(master.Position,master);
            Assert.That(master.FalloutPlayerStacks,Is.EqualTo(2));Assert.That(master.FalloutAiStacks,Is.EqualTo(3));
            board.Move(master.Position,new TileCoord(4,1));board.Remove(master.Position);board.Place(master.Position,master);
            Assert.That(master.FalloutStacks,Is.EqualTo(5));
            Assert.Throws<InvalidOperationException>(()=>master.EnterSealbound(1));
            board.Remove(master.Position);
            var futureMaster=new MasterState("future-master",PlayerId.AI,new TileCoord(2,1),3,333);
            board.Place(futureMaster.Position,futureMaster);
            Assert.That(futureMaster.FalloutStacks,Is.Zero);
        }

        [TestCase(PlayerId.Player)][TestCase(PlayerId.AI)]
        public void FalloutScope_ViewAndCopyNeverInventMissingOccupantStacksFromLegacyBoardExposure(PlayerId viewer)
        {
            var state=State();state.AIBoard.AddFalloutStack();state.AIBoard.AddFalloutStack();
            state.AI.Master.RestoreFallout(0,0);
            Assert.That(AiBattleStateCopy.Create(state).AI.Master.FalloutStacks,Is.Zero);
            var view=new BattleStateViewFactory().CreateForPlayer(state,"legacy-master",viewer);
            var restored=BattleStateViewProjector.CreateLocalPerspectiveState(view);
            var localOwner=viewer==PlayerId.Player?PlayerId.AI:PlayerId.Player;
            Assert.That(restored.GetPlayer(localOwner).Master.FalloutStacks,Is.Zero);
            Assert.That(state.AI.Master.FalloutStacks,Is.Zero,"Projection/copy must not mutate legacy source state.");
        }

        [TestCase(PlayerId.Player,"normal",293)][TestCase(PlayerId.AI,"normal",293)]
        [TestCase(PlayerId.Player,"erasure",293)][TestCase(PlayerId.AI,"erasure",293)]
        [TestCase(PlayerId.Player,"drained",213)][TestCase(PlayerId.AI,"drained",213)]
        [TestCase(PlayerId.Player,"invincible",333)][TestCase(PlayerId.AI,"invincible",333)]
        public void FalloutScope_MastersTickOnBothTurnsUsingFixedProtectionRules(PlayerId owner,string status,int expected)
        {
            var state=State();var master=state.GetPlayer(owner).Master;
            master.IncreasePhysicalDefense(999);master.IncreaseMagicDefense(999);
            state.GetBoard(owner).AddFalloutStack();state.GetBoard(owner).AddFalloutStack();
            if(status=="erasure")master.ApplyErasure();
            if(status=="drained")master.IsDrained=true;
            if(status=="invincible")master.AddInvincibleEffect(InvincibleDurationType.Always,0);
            Start(state,PlayerId.Player);Start(state,PlayerId.AI);
            Assert.That(master.CurrentHp,Is.EqualTo(expected));Assert.That(master.FalloutStacks,Is.EqualTo(2));
        }

        [Test]
        public void FalloutScope_AllMasterTicksResolveBeforeSimultaneousDefeatComparison()
        {
            var state=State();state.PlayerBoard.AddFalloutStack();state.AIBoard.AddFalloutStack();
            state.Player.Master.CurrentHp=5;state.AI.Master.CurrentHp=5;
            Start(state,PlayerId.Player);
            Assert.That(state.Player.Master.CurrentHp,Is.EqualTo(-5));Assert.That(state.AI.Master.CurrentHp,Is.EqualTo(-5));
            Assert.That(state.IsEnded,Is.True);Assert.That(state.Result.IsDraw,Is.True);
            Assert.That(state.ValuePopupEvents.Count(e=>e.Cause==BattleValueChangeCause.Fallout),Is.EqualTo(2));
        }

        [Test]
        public void FalloutScope_LegacyReactorExposureCannotCauseDamageOrExplosion()
        {
            var state=State();state.AIBoard.AddFalloutStack();
            var reactor=new BuildingState("legacy-reactor","NuclearPowerPlant",PlayerId.AI,new TileCoord(0,0),false,0,5);
            state.AIBoard.Place(reactor.Position,reactor);reactor.RestoreFallout(9,9);
            Start(state,PlayerId.Player);
            Assert.That(state.AIBoard.GetOccupant(reactor.Position),Is.SameAs(reactor));Assert.That(reactor.CurrentHp,Is.EqualTo(5));
            Assert.That(state.ValuePopupEvents.Any(e=>e.Cause==BattleValueChangeCause.NuclearPowerPlant),Is.False);
            Assert.That(state.Player.Master.CurrentHp,Is.EqualTo(333));Assert.That(state.AI.Master.CurrentHp,Is.EqualTo(323));
        }

        [TestCase(PlayerId.Player)][TestCase(PlayerId.AI)]
        public void Cast_FuturePlayedUnitsStayClean_AfterAiCopyAndBothViewerProjections(PlayerId caster)
        {
            var state=State(caster);var enemy=state.GetOpponent(caster).Id;
            var oldUnit=Unit(state,enemy,hp:500);
            Cast(state,caster);Cast(state,caster);
            Assert.That(oldUnit.FalloutStacks,Is.EqualTo(2));
            var states=new[] { state, AiBattleStateCopy.Create(state),
                BattleStateViewProjector.CreateLocalPerspectiveState(
                    new BattleStateViewFactory().CreateForPlayer(state,"current-only",caster)),
                BattleStateViewProjector.CreateLocalPerspectiveState(
                    new BattleStateViewFactory().CreateForPlayer(state,"current-only",enemy)) };
            for(var i=0;i<states.Length;i++)
            {
                var candidate=states[i];
                var localEnemy=i==2?PlayerId.AI:i==3?PlayerId.Player:enemy;
                candidate.RestoreRuntimeState(3,localEnemy,PhaseType.Main);
                candidate.GetPlayer(localEnemy).Hand.Add("Goblin");
                candidate.GetPlayer(localEnemy).Resources.Add(new ResourceSet(10,0,0,0));
                var summoned=new PlayCardService(Provider()).PlayUnitCard(
                    candidate,localEnemy,"Goblin",new TileCoord(4,0));
                Assert.That(summoned.FalloutStacks,Is.Zero,"State copy "+i);
                var hp=summoned.CurrentHp;
                Start(candidate,candidate.GetOpponent(localEnemy).Id);
                Assert.That(summoned.CurrentHp,Is.EqualTo(hp),"No inherited Fallout damage in state copy "+i);
            }
        }

        [Test]
        public void CardDescription_JsonAndNativeAssetBothDescribeCastTimeUnitsOnly()
        {
            var description=JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).Cards.Single(c=>c.Id=="Nuclear").EffectText;
            var asset=UnityEditor.AssetDatabase.LoadAssetAtPath<
                Project333.Runtime.Infrastructure.Data.ScriptedSpellCardDefinitionAsset>(
                "Assets/Project333/ScriptableObjects/StarterTen/Cards/Nuclear.asset");
            Assert.That(asset,Is.Not.Null);
            var serialized=new UnityEditor.SerializedObject(asset);
            var nativeDescription=serialized.FindProperty("_effectText").stringValue;
            // Native card art may use shorter wording; the shared Fallout tooltip supplies the tick rule.
            foreach(var text in new[]{description,nativeDescription})
            {
                Assert.That(text,Does.Contain("고정 데미지 100"));
                Assert.That(text,Does.Contain("사용 당시 상대 전장에 있는 유닛(마스터 포함)"));
                Assert.That(text,Does.Contain("낙진"));
                Assert.That(text,Does.Not.Contain("이후"));
            }
            Assert.That(description,Does.Contain("양쪽 턴 시작마다 중첩당 고정 피해 10"));
        }

        [Test]
        public void NewBattle_HasNoFallout()
        {
            var state=State();Cast(state,PlayerId.Player);
            var fresh=State();Assert.That(fresh.AIBoard.FalloutStacks,Is.Zero);
            Assert.That(Unit(fresh,PlayerId.AI).FalloutStacks,Is.Zero);
        }
    }
}
