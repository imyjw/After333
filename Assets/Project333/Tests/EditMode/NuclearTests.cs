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
            state.GetPlayer(caster).Resources.Add(new ResourceSet(0,0,10,0));
            new SpellService(Provider()).CastScriptedSpell(state,caster,"Nuclear");
        }
        private static void Start(BattleState state,PlayerId owner)
        {
            state.RestoreRuntimeState(state.TurnNumber+1,owner,PhaseType.TurnStart);
            new TurnStartService().ResolveTurnStart(state);
        }

        [Test]
        public void Definition_IsLegendarySciencePowerTen_NoUpgradeDraftOrRewards()
        {
            var json=File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json");
            Assert.That(CardDatabaseValidator.ValidateJson(json).Errors,Is.Empty);
            var record=JsonCardDefinitionDatabase.FromJson(json).Cards.Single(c=>c.Id=="Nuclear");
            Assert.That(record.Rarity,Is.EqualTo(CardRarity.Legendary));
            Assert.That(record.Affiliation,Is.EqualTo(CardAffiliation.ScienceCivilization));
            Assert.That(record.IncludeInDraft||record.IncludeInRewards,Is.False);
            Assert.That(record.Cost.Power,Is.EqualTo(10));
            Assert.That(CardUpgradeRules.IsCardUpgradeable(Provider().GetRequired("Nuclear")),Is.False);
        }

        [Test]
        public void Cast_DestroysBothBoardsBypassingInvincibleAndEndure_ButPreservesMastersAndSealbound()
        {
            var state=State();
            var ally=Unit(state,PlayerId.Player);
            var invincible=Unit(state,PlayerId.AI);
            invincible.AddInvincibleEffect(InvincibleDurationType.Always,0);
            var endure=Unit(state,PlayerId.AI,1,endure:true);
            var sealedUnit=Unit(state,PlayerId.AI,3);sealedUnit.EnterSealbound(3);
            Cast(state,PlayerId.Player);
            Assert.That(state.PlayerBoard.GetOccupant(ally.Position),Is.Null);
            Assert.That(state.AIBoard.GetOccupant(invincible.Position),Is.Null);
            Assert.That(state.AIBoard.GetOccupant(endure.Position),Is.Null);
            Assert.That(state.AIBoard.GetOccupant(sealedUnit.Position),Is.SameAs(sealedUnit));
            Assert.That(sealedUnit.FalloutStacks,Is.Zero);
            Assert.That(state.Player.Master.CurrentHp,Is.EqualTo(state.Player.Master.MaxHp));
            Assert.That(state.AI.Master.CurrentHp,Is.EqualTo(state.AI.Master.MaxHp));
            Assert.That(state.AIBoard.FalloutStacks,Is.EqualTo(1));
            Assert.That(state.PlayerBoard.FalloutStacks,Is.Zero);
            Assert.That(state.Player.Hand.CardIds,Does.Not.Contain("Nuclear"));
            Assert.That(state.Player.Discard.CardIds,Does.Contain("Nuclear"));
            Assert.That(state.Player.Resources.Power,Is.Zero);
        }

        [Test]
        public void MultiplePowerPlants_ExplodeOnceEachAfterWholeBoardRemoval()
        {
            var state=State();
            foreach(var owner in new[]{PlayerId.Player,PlayerId.AI})
                state.GetBoard(owner).Place(new TileCoord(0,0),new BuildingState(owner.ToString(),"NuclearPowerPlant",owner,new TileCoord(0,0),false,0,100));
            var sealedUnit=Unit(state,PlayerId.AI,1);sealedUnit.EnterSealbound(2);
            var before=state.Player.Master.CurrentHp;
            Cast(state,PlayerId.Player);
            Assert.That(state.Player.Master.CurrentHp,Is.EqualTo(before-60));
            Assert.That(sealedUnit.CurrentHp,Is.EqualTo(100));
            Assert.That(state.ValuePopupEvents.Count(e=>e.Cause==BattleValueChangeCause.NuclearPowerPlant&&e.RuntimeId==state.Player.Master.RuntimeId),Is.EqualTo(2));
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
        public void RepeatedCast_StacksAndFutureUnitsAndBuildingsTickOnBothTurnStarts()
        {
            var state=State();Cast(state,PlayerId.Player);Cast(state,PlayerId.Player);
            var unit=Unit(state,PlayerId.AI);
            var building=new BuildingState("plant","PowerPlant",PlayerId.AI,new TileCoord(1,0),false,0,100);
            state.AIBoard.Place(building.Position,building);
            var ally=Unit(state,PlayerId.Player);
            Assert.That(unit.FalloutStacks,Is.EqualTo(2));
            Start(state,PlayerId.Player);
            Assert.That(unit.CurrentHp,Is.EqualTo(80));Assert.That(building.CurrentHp,Is.EqualTo(80));
            Start(state,PlayerId.AI);
            Assert.That(unit.CurrentHp,Is.EqualTo(60));Assert.That(building.CurrentHp,Is.EqualTo(60));
            Assert.That(ally.CurrentHp,Is.EqualTo(100));
            Assert.That(state.AI.Master.FalloutStacks,Is.Zero);
        }

        [TestCase("normal",90)][TestCase("erasure",90)][TestCase("drained",70)][TestCase("invincible",100)][TestCase("sealed",100)]
        public void Tick_AppliesExistingFixedDamageRules(string status,int expected)
        {
            var state=State();Cast(state,PlayerId.Player);var target=Unit(state,PlayerId.AI);
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
            var state=State();Cast(state,PlayerId.Player);Cast(state,PlayerId.Player);
            var target=Unit(state,PlayerId.AI,hp:15,endure:true);
            Start(state,PlayerId.Player);
            Assert.That(target.CurrentHp,Is.EqualTo(1));
            Assert.That(target.EndureUsed,Is.True);
            Start(state,PlayerId.AI);
            Assert.That(state.AIBoard.GetOccupant(target.Position),Is.Null);
        }

        [Test]
        public void Sealbound_DefersNewExposureUntilRelease()
        {
            var state=State();var target=Unit(state,PlayerId.AI);target.EnterSealbound(1);
            Cast(state,PlayerId.Player);Cast(state,PlayerId.Player);
            Assert.That(target.FalloutStacks,Is.Zero);
            Start(state,PlayerId.AI);
            Assert.That(target.CurrentHp,Is.EqualTo(100));
            Assert.That(target.IsSealbound,Is.False);
            Assert.That(target.FalloutStacks,Is.EqualTo(2));
            Start(state,PlayerId.Player);
            Assert.That(target.CurrentHp,Is.EqualTo(80));
        }

        [Test]
        public void GuAndErasure_PreserveDebuff_ReturningToSameBoardDoesNotDuplicate()
        {
            var state=State();Cast(state,PlayerId.Player);var target=Unit(state,PlayerId.AI);
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
            state.PlayerBoard.AddFalloutStack();state.AIBoard.AddFalloutStack();state.AIBoard.AddFalloutStack();
            var target=Unit(state,PlayerId.AI);
            state.AIBoard.Remove(target.Position);target.TransferOwnership(PlayerId.Player);state.PlayerBoard.Place(new TileCoord(0,0),target);
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
        public void Tooltip_ShowsFalloutNameWithoutDamageDescription()
        {
            var state=State();state.AIBoard.AddFalloutStack();state.AIBoard.AddFalloutStack();var target=Unit(state,PlayerId.AI);
            target.ApplyErasure();
            var entry=OccupantStatusTooltipCatalog.BuildAttached(null,target,state).Single(e=>e.Title=="낙진");
            Assert.That(entry.Description,Is.Empty);
        }

        [TestCase(false)][TestCase(true)]
        public void SummoningSealedDefinition_DefersExposureAndKeepsActionsDisabled(bool building)
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
            Assert.That(target.FalloutStacks,Is.EqualTo(1));
            Start(state,PlayerId.AI);
            Assert.That(target.CurrentHp,Is.EqualTo(90));
        }

        [Test]
        public void Ai_UsesNuclearToWinThroughReactorExplosion()
        {
            var state=State(PlayerId.AI);
            state.Player.Master.CurrentHp=20;
            var reactor=new BuildingState("reactor","NuclearPowerPlant",PlayerId.Player,new TileCoord(0,0),false,0,100);
            state.PlayerBoard.Place(reactor.Position,reactor);
            state.AI.Hand.Add("Nuclear");state.AI.Resources.Add(new ResourceSet(0,0,10,0));
            var before=AiBattleStateCopy.PositionKey(state);
            var action=new AiDecisionService(Provider()).GetNextCommand(state)
                as Project333.Runtime.Application.Commands.CastScriptedSpellCommand;
            Assert.That(action,Is.Not.Null);
            Assert.That(action.CardId,Is.EqualTo("Nuclear"));
            Assert.That(AiBattleStateCopy.PositionKey(state),Is.EqualTo(before));
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
