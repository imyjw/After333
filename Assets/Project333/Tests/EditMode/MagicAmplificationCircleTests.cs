using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class MagicAmplificationCircleTests
    {
        private const string CardId = MagicAmplificationCircleRules.CardId;
        private static ICardDefinitionProvider Provider() =>
            JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).CreateProvider();
        private static BattleState State(PlayerId owner = PlayerId.Player)
        {
            var deck=Enumerable.Repeat("Goblin",33).ToArray();
            var state=new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck,deck,owner));
            state.SetPhase(PhaseType.Main);
            foreach(var player in new[]{state.Player,state.AI})
            {
                foreach(var id in player.Hand.CardIds.ToArray())player.Hand.Remove(id);
                player.Resources.Spend(player.Resources.Clone());
            }
            return state;
        }
        private static void Cast(BattleState state)
        {
            var owner=state.ActivePlayerId;
            state.GetPlayer(owner).Hand.Add(CardId);
            state.GetPlayer(owner).Resources.Add(new ResourceSet(1,0,0,0));
            new SpellService(Provider()).CastScriptedSpell(state,owner,CardId);
        }
        private static UnitState Unit(BattleState state,PlayerId owner,int col=0,string id="Goblin",int spellPower=0)
        {
            var unit=new UnitState(Guid.NewGuid().ToString(),id,owner,new TileCoord(col,0),AttackType.Melee,
                5,100,true,false,0,damageType:DamageType.Magic,spellPower:spellPower);
            unit.HasSummoningSickness=false;
            state.GetBoard(owner).Place(unit.Position,unit);
            return unit;
        }

        [Test]
        public void Definition_MatchesConfirmedMetadataAndExclusions()
        {
            var json=File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json");
            Assert.That(CardDatabaseValidator.ValidateJson(json).Errors,Is.Empty);
            var card=JsonCardDefinitionDatabase.FromJson(json).Cards.Single(c=>c.Id==CardId);
            Assert.That(card.Rarity,Is.EqualTo(CardRarity.Common));
            Assert.That(card.Affiliation,Is.EqualTo(CardAffiliation.Fantasy));
            Assert.That(card.Cost.Mana,Is.EqualTo(1));
            Assert.That(card.IncludeInDraft||card.IncludeInRewards,Is.False);
            Assert.That(CardUpgradeRules.IsCardUpgradeable(Provider().GetRequired(CardId)),Is.False);
        }

        [Test]
        public void Cast_StacksSpendsAndDiscards_AddsToActiveOccupantsOnly()
        {
            var state=State();var source=Unit(state,PlayerId.Player,spellPower:20);
            Cast(state);Cast(state);
            Assert.That(SpellPowerRules.GetTotal(state,PlayerId.Player),Is.EqualTo(40));
            Assert.That(SpellPowerRules.GetTotal(state,PlayerId.AI),Is.Zero);
            Assert.That(state.Player.Resources.Mana,Is.Zero);
            Assert.That(state.Player.Hand.CardIds,Does.Not.Contain(CardId));
            Assert.That(state.Player.Discard.CardIds.Count(id=>id==CardId),Is.EqualTo(2));
            source.ApplyErasure();
            Assert.That(SpellPowerRules.GetTotal(state,PlayerId.Player),Is.EqualTo(20));
            state.PlayerBoard.Remove(source.Position);
            Assert.That(SpellPowerRules.GetTotal(state,PlayerId.Player),Is.EqualTo(20));
        }

        [TestCase(DamageType.Magic,23)][TestCase(DamageType.Physical,3)][TestCase(DamageType.Fixed,3)]
        public void Bonus_OnlyAmplifiesMagic(DamageType type,int expected)
        {
            var state=State();Cast(state);Cast(state);
            Assert.That(SpellPowerRules.ApplyCurrent(state,PlayerId.Player,3,type),Is.EqualTo(expected));
        }

        [Test]
        public void AttackAndCounterattack_UseRespectiveOwnersCurrentBonus()
        {
            var state=State();
            // Cast on each owner's main turn without ending it, to exercise both damage sources.
            Cast(state);
            state.RestoreRuntimeState(state.TurnNumber,PlayerId.AI,PhaseType.Main);Cast(state);
            state.RestoreRuntimeState(state.TurnNumber,PlayerId.Player,PhaseType.Main);
            var attacker=Unit(state,PlayerId.Player);var defender=Unit(state,PlayerId.AI);
            new AttackService(new TargetingService()).Attack(state,PlayerId.Player,attacker.Position,defender.Position);
            Assert.That(attacker.CurrentHp,Is.EqualTo(85));
            Assert.That(defender.CurrentHp,Is.EqualTo(85));
        }

        [Test]
        public void EndTurn_AppliesToRedDragonThenExpiresBeforeFirewallNextTurn()
        {
            var state=State();Cast(state);
            Unit(state,PlayerId.Player,id:"RedDragon");
            var target=Unit(state,PlayerId.AI);
            state.Player.Hand.Add("Firewall");state.Player.Resources.Add(new ResourceSet(3,0,0,0));
            new SpellService(Provider()).CastScriptedSpell(state,PlayerId.Player,"Firewall",PlayerId.AI,target.Position);
            new EndTurnService().EndTurn(state);
            Assert.That(target.CurrentHp,Is.EqualTo(57)); // 33 + 10
            Assert.That(SpellPowerRules.GetTotal(state,PlayerId.Player),Is.Zero);
            new TurnStartService().ResolveTurnStart(state);
            Assert.That(target.CurrentHp,Is.EqualTo(24)); // only 33 on next turn
        }

        [Test]
        public void ImmediateSpell_UsesStackedBonusBeforeDefense()
        {
            var state=State();Cast(state);Cast(state);
            var target=Unit(state,PlayerId.AI);
            state.Player.Hand.Add("firebolt");state.Player.Resources.Add(new ResourceSet(1,0,0,0));
            new SpellService(Provider()).CastDamageSpell(state,PlayerId.Player,"firebolt",PlayerId.AI,target.Position);
            Assert.That(target.CurrentHp,Is.EqualTo(70));
        }

        [TestCase("resources")][TestCase("wrong-turn")][TestCase("target")]
        public void InvalidCast_DoesNotConsumeOrGrantEffect(string reason)
        {
            var state=State();state.Player.Hand.Add(CardId);
            if(reason!="resources")state.Player.Resources.Add(new ResourceSet(1,0,0,0));
            if(reason=="wrong-turn")state.RestoreRuntimeState(state.TurnNumber,PlayerId.AI,PhaseType.Main);
            var before=AiBattleStateCopy.PositionKey(state);
            var service=new SpellService(Provider());
            Assert.Throws<InvalidOperationException>(()=>{
                if(reason=="target")service.CastScriptedSpell(state,PlayerId.Player,CardId,PlayerId.Player,state.Player.Master.Position);
                else service.CastScriptedSpell(state,PlayerId.Player,CardId);
            });
            Assert.That(AiBattleStateCopy.PositionKey(state),Is.EqualTo(before));
        }

        [TestCase(PlayerId.Player)][TestCase(PlayerId.AI)]
        public void ProjectionAndAiClone_PreserveStacksAndOwnerTurnExpiry(PlayerId viewer)
        {
            var state=State(PlayerId.AI);Cast(state);Cast(state);
            var key=AiBattleStateCopy.PositionKey(state);
            var copy=AiBattleStateCopy.Create(state);
            Assert.That(AiBattleStateCopy.PositionKey(copy),Is.EqualTo(key));
            var projected=BattleStateViewProjector.CreateLocalPerspectiveState(new BattleStateViewFactory().CreateForPlayer(state,"magic",viewer));
            var projectedOwner=viewer==PlayerId.AI?PlayerId.Player:PlayerId.AI;
            Assert.That(SpellPowerRules.GetTotal(projected,projectedOwner),Is.EqualTo(20));
            new EndTurnService().EndTurn(projected);
            Assert.That(SpellPowerRules.GetTotal(projected,projectedOwner),Is.Zero);
            Assert.That(SpellPowerRules.GetTotal(state,PlayerId.AI),Is.EqualTo(20));
        }

        [Test]
        public void Ai_CanBuffBeforeLethalSpell()
        {
            var state=State(PlayerId.AI);
            state.Player.Master.CurrentHp=20;
            state.AI.Hand.Add(CardId);state.AI.Hand.Add("firebolt");
            state.AI.Resources.Add(new ResourceSet(2,0,0,0));
            var options=new AiSearchOptions {MaxMilliseconds=1000};
            var ai=new AiDecisionService(Provider(),new TargetingService(),ZeroCardUpgradeLevelProvider.Instance,options);
            var command=ai.GetNextCommand(state) as CastScriptedSpellCommand;
            Assert.That(command,Is.Not.Null);
            Assert.That(command.CardId,Is.EqualTo(CardId));
        }
    }
}
