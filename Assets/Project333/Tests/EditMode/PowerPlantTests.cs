using System;
using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class PowerPlantTests
    {
        private static BattleState State(PlayerId actor = PlayerId.Player)
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, actor));
            state.SetPhase(PhaseType.Main);
            return state;
        }
        private static BuildingState Plant(BattleState state, PlayerId owner = PlayerId.Player, int col = 0)
        {
            var plant = new BuildingState("plant-" + col, "PowerPlant", owner, new TileCoord(col,0), false, 0, 30);
            state.GetBoard(owner).Place(plant.Position, plant);
            return plant;
        }
        [Test]
        public void PlayerButton_LogsSuccessfulEffectButNotInsufficientGoldOrDuplicateActivation()
        {
            var state = State(); var plant = Plant(state);
            state.Player.Resources.Spend(state.Player.Resources.Clone());
            var provider = new InMemoryCardDefinitionProvider(new CardDefinition[] {
                new BuildingCardDefinition("PowerPlant", "발전소", new ResourceSet(0, 0, 1, 0), false, 0, 30)
            });
            var flow = new BattleFlowController(new BattleSetupService(), new MulliganService(), new TurnStartService(),
                new BattleCommandProcessor(new PlayCardService(provider), new SpellService(provider),
                    new MoveService(), new AttackService(), new EndTurnService()));
            flow.RestoreBattleState(state);
            var go = new UnityEngine.GameObject("active-log-test");
            go.SetActive(false);
            try
            {
                var bootstrapper = go.AddComponent<Project333.Runtime.Presentation.Battle.BattleBootstrapper>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(Project333.Runtime.Presentation.Battle.BattleBootstrapper).GetField("_battleGateway", flags)
                    .SetValue(bootstrapper, new Project333.Runtime.Application.Gateways.LocalBattleGateway(flow));
                typeof(Project333.Runtime.Presentation.Battle.BattleBootstrapper).GetField("_cardDefinitionProvider", flags)
                    .SetValue(bootstrapper, provider);
                typeof(Project333.Runtime.Presentation.Battle.BattleBootstrapper).GetField("_sendPlayerActionsToOnlineGateway", flags)
                    .SetValue(bootstrapper, false);
                typeof(Project333.Runtime.Presentation.Battle.BattleBootstrapper).GetField("_startOnAwake", flags)
                    .SetValue(bootstrapper, false);
                typeof(Project333.Runtime.Presentation.Battle.BattleBootstrapper).GetField("_startWithDraftBeforeBattle", flags)
                    .SetValue(bootstrapper, false);
                go.SetActive(true);
                Assert.That(bootstrapper.TryActivatePlayerEffect(plant.Position, plant.RuntimeId, out _), Is.False);
                Assert.That(bootstrapper.CombatLogText, Does.Not.Contain("액티브 발동"));
                state.Player.Resources.Add(new ResourceSet(0, 0, 0, 1));
                Assert.That(bootstrapper.TryActivatePlayerEffect(plant.Position, plant.RuntimeId, out var message), Is.True, message);
                Assert.That(message, Is.EqualTo("플레이어의 발전소(0,0) 액티브 발동: 골드 1 소모, 전력 2 획득"));
                Assert.That(bootstrapper.CombatLogText, Does.Contain(message));
                Assert.That(state.Player.Resources.Power, Is.EqualTo(2));
                Assert.That(bootstrapper.TryActivatePlayerEffect(plant.Position, plant.RuntimeId, out _), Is.False);
                Assert.That(bootstrapper.CombatLogText.Split('\n').Count(line => line.Contains("액티브 발동")), Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void Activate(BattleState state, BuildingState plant) =>
            ActiveEffectService.Activate(state, plant.OwnerId, plant.Position, plant.RuntimeId);

        [Test]
        public void SummonTurn_ConvertsOnlyOnCommand_OncePerPlantPerTurn()
        {
            var state = State(); var plant = Plant(state);
            var gold = state.Player.Resources.Gold;
            Assert.That(plant.HasSummoningSickness, Is.True);
            Assert.That(ActiveEffectService.CanActivate(state, PlayerId.Player, plant.Position), Is.True);
            Activate(state, plant);
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(gold - 1));
            Assert.That(state.Player.Resources.Power, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => Activate(state, plant));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(gold - 1));
            Assert.That(state.Player.Resources.Power, Is.EqualTo(2));
            var second = Plant(state, col:1);
            Activate(state, second);
            Assert.That(state.Player.Resources.Power, Is.EqualTo(4));
            state.RestoreRuntimeState(state.TurnNumber + 2, PlayerId.Player, PhaseType.Main);
            Activate(state, plant);
            Assert.That(state.Player.Resources.Power, Is.EqualTo(6));
        }

        [Test]
        public void TurnStart_DoesNotConvertGold_OrSupplyAutomaticUpkeep()
        {
            var state = State(); Plant(state);
            state.PlayerBoard.Place(new TileCoord(1,0), new UnitState("robot","A-111",PlayerId.Player,
                new TileCoord(1,0),AttackType.Melee,13,13,true,true,1));
            var before = state.Player.Resources.Gold;
            state.RestoreRuntimeState(3, PlayerId.Player, PhaseType.TurnStart);
            new TurnStartService().ResolveTurnStart(state);
            Assert.That(state.Player.Resources.Power, Is.Zero);
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(before + 2)); // Base gold +3, then upkeep pays gold 1 directly.
            Assert.That(state.ResourceChangeEvents.Any(e => e.SourceCardId == "PowerPlant"), Is.False);
        }

        [TestCase("gold")][TestCase("erasure")][TestCase("drained")][TestCase("sealed")]
        [TestCase("dead")][TestCase("enemy-turn")][TestCase("phase")][TestCase("pending-fusion")]
        public void InvalidActivation_DoesNotSpendOrMarkUsed(string reason)
        {
            var state = State(); var plant = Plant(state);
            switch(reason)
            {
                case "gold": state.Player.Resources.Spend(state.Player.Resources.Clone()); break;
                case "erasure": plant.ApplyErasure(); break;
                case "drained": plant.IsDrained = true; break;
                case "sealed": plant.EnterSealbound(2); break;
                case "dead": plant.CurrentHp = 0; break;
                case "enemy-turn": state.RestoreRuntimeState(2,PlayerId.AI,PhaseType.Main); break;
                case "phase": state.RestoreRuntimeState(3, PlayerId.Player, PhaseType.TurnStart); break;
                case "pending-fusion": state.BeginRobotFusion(PlayerId.Player,"RobotFusion"); break;
            }
            var gold = state.Player.Resources.Gold;
            Assert.Throws<InvalidOperationException>(() => Activate(state,plant));
            Assert.That(state.Player.Resources.Gold,Is.EqualTo(gold));
            Assert.That(plant.LastActiveEffectTurnNumber,Is.EqualTo(-1));
        }

        [TestCase(PlayerId.Player)][TestCase(PlayerId.AI)]
        public void NoGold_OffersButtonButRejectsActivationWithoutConsumingUsage(PlayerId owner)
        {
            var state = State(owner); var plant = Plant(state, owner);
            var resources = state.GetPlayer(owner).Resources;
            resources.Spend(resources.Clone());
            Assert.That(ActiveEffectService.CanOfferActivation(state, owner, plant.Position), Is.True);
            Assert.That(ActiveEffectService.CanActivate(state, owner, plant.Position), Is.False);
            Assert.That(Assert.Throws<InvalidOperationException>(() => Activate(state, plant)).Message, Is.EqualTo("자원이 부족합니다."));
            Assert.That(resources.Gold, Is.Zero);
            Assert.That(resources.Power, Is.Zero);
            Assert.That(plant.LastActiveEffectTurnNumber, Is.EqualTo(-1));
            Assert.That(state.ResourceChangeEvents, Is.Empty);
            resources.Add(new ResourceSet(0, 0, 0, 1));
            Activate(state, plant);
            Assert.That(resources.Power, Is.EqualTo(2));
            Assert.That(ActiveEffectService.CanOfferActivation(state, owner, plant.Position), Is.False);
        }

        [Test]
        public void WrongIdentityOrEnemyTile_DoesNotActivate()
        {
            var state = State(); var plant = Plant(state);
            Assert.Throws<InvalidOperationException>(() =>
                ActiveEffectService.Activate(state,PlayerId.Player,plant.Position,"stale-runtime-id"));
            Assert.Throws<InvalidOperationException>(() =>
                ActiveEffectService.Activate(state,PlayerId.AI,plant.Position,plant.RuntimeId));
            Assert.That(plant.LastActiveEffectTurnNumber, Is.EqualTo(-1));
        }

        [TestCase(PlayerId.Player)][TestCase(PlayerId.AI)]
        public void ReconnectAndAiCopy_KeepUsage(PlayerId owner)
        {
            var state = State(owner); var plant = Plant(state,owner);
            Activate(state,plant);
            var view = new BattleStateViewFactory().CreateForPlayer(state,"active",owner);
            var roundTrip = JsonConvert.DeserializeObject<BattleStateViewDto>(JsonConvert.SerializeObject(view));
            var restored = BattleStateViewProjector.CreateLocalPerspectiveState(roundTrip);
            Assert.That(ActiveEffectService.CanActivate(restored,PlayerId.Player,plant.Position),Is.False);
            var copy = AiBattleStateCopy.Create(state);
            Assert.That(copy.GetBoard(owner).GetOccupant(plant.Position).LastActiveEffectTurnNumber,
                Is.EqualTo(state.TurnNumber));
        }

        [Test]
        public void ActiveTooltipAndCardAsset_UseConfirmedDescription()
        {
            var asset=UnityEditor.AssetDatabase.LoadAssetAtPath<BuildingCardDefinitionAsset>(
                "Assets/Project333/ScriptableObjects/StarterTen/Cards/PowerPlant.asset");
            Assert.That(asset.SpecialEffectText,Is.EqualTo("액티브: "+ActiveEffectService.PowerPlantDescription));
            var state=State(); var plant=Plant(state);
            var tooltip=Project333.Runtime.Presentation.Battle.OccupantSpecialEffectTooltipCatalog.Build(asset.ToDefinition(),plant);
            Assert.That(tooltip.Any(t=>t.Title=="액티브" && t.Description=="소환물의 액티브 버튼을 눌러 액티브 효과를 발동시킬 수 있다."),Is.True);
        }

        [Test]
        public void Ai_CanActivateToAffordWinningSpell()
        {
            var state = State(PlayerId.AI); var plant = Plant(state,PlayerId.AI);
            state.AI.Resources.Spend(state.AI.Resources.Clone());
            state.AI.Resources.Add(new ResourceSet(0,0,0,1));
            foreach(var id in state.AI.Hand.CardIds.ToArray()) state.AI.Hand.Remove(id);
            state.AI.Hand.Add("finisher");
            state.AI.Master.RemainingAttacksThisTurn=0;
            state.Player.Master.CurrentHp=1;
            var provider = new InMemoryCardDefinitionProvider(new CardDefinition[] {
                new DamageSpellCardDefinition("finisher","Finish",new ResourceSet(0,0,2,0),99)
            });
            var ai = new AiDecisionService(provider,new TargetingService(),ZeroCardUpgradeLevelProvider.Instance,
                new AiSearchOptions { MaxMilliseconds=10000, MaxSimulations=16000 });
            var command=ai.GetNextCommand(state);
            Assert.That(command,Is.TypeOf<ActivateEffectCommand>());
            var processor=new BattleCommandProcessor(new PlayCardService(provider),new SpellService(provider),
                new MoveService(),new AttackService(),new EndTurnService());
            var flow = new BattleFlowController(new BattleSetupService(), new MulliganService(),
                new TurnStartService(), processor);
            flow.RestoreBattleState(state);
            var logs = new System.Collections.Generic.List<string>();
            new AiTurnRunner(ai).RunAiTurn(flow, logs.Add, id => id == "PowerPlant" ? "발전소" : id);
            Assert.That(logs.Count(line => line == "상대방의 발전소(0,0) 액티브 발동: 골드 1 소모, 전력 2 획득"), Is.EqualTo(1));
            Assert.That(state.IsEnded,Is.True);
            Assert.That(state.Result.Winner,Is.EqualTo(PlayerId.AI));
        }
    }
}
