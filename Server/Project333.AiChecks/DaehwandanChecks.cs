using System.Reflection;
using System.Text.Json;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Cards;
using Project333.PvpServer.Messages;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using LogType = Project333.Runtime.Application.Online.BattleCombatLogEntryType;

internal static class DaehwandanChecks
{
    public static void Run()
    {
        var database = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "cards.json")));
        var provider = database.CreateProvider();
        var definition = (ScriptedSpellCardDefinition)provider.GetRequired("Daehwandan");
        Require(definition.EffectAmount == 30 && definition.Damage == 0 && definition.Cost.Qi == 5, "Canonical magnitude/type/cost changed incorrectly.");
        Require(CardUpgradeRules.IsCardUpgradeable(definition), "Daehwandan must be upgradeable.");
        Require(RewardCardEligibility.IsRewardableCard(database.Cards.Single(c => c.Id == "Daehwandan")), "Upgradeable reward-enabled card must enter reward/pack pool.");
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
        for (var level = 0; level <= 13; level++)
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner));
            state.SetPhase(PhaseType.Main);
            var caster = state.GetPlayer(owner);
            caster.Resources.Spend(caster.Resources.Clone());
            caster.Resources.Add(level % 2 == 0 ? new ResourceSet(0,5,0,0) : new ResourceSet(0,0,0,5));
            var hand = caster.Hand.Add("Daehwandan");
            var levels = new InMemoryCardUpgradeLevelProvider(); levels.SetUpgradeLevel(owner, "Daehwandan", level);
            var flow = PrototypeCardDefinitions.CreateBattleFlowController(provider, levels); flow.RestoreBattleState(state);
            var session = new BattleSession("daehwandan-check", false);
            typeof(BattleSession).GetField("_battleFlowController", hidden)!.SetValue(session, flow);
            typeof(BattleSession).GetField("_cardDefinitionProvider", hidden)!.SetValue(session, provider);
            session.ApplyCommand(new ClientBattleCommandMessage {
                ActorId = owner == PlayerId.Player ? PlayerIdDto.Player : PlayerIdDto.AI,
                CommandType = OnlineBattleCommandType.CastScriptedSpell, CardId = "Daehwandan", HandCardRuntimeId = hand.RuntimeId
            });
            Require(caster.Master.BaseAttack == 33 + level && caster.Resources.Qi == 0 && caster.Resources.Gold == 0, "Server cast must grant leveled attack and charge Qi/Gold once.");
            Require(state.PersistentEffects.Count == 0, "Cast must not create future Qi production.");
            var saved = JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(JsonSerializer.Serialize(session.CreatePersistenceSnapshot("daehwandan-check")))!;
            var log = saved.CombatLogEntries!.Single(entry => entry.EntryType == LogType.AttackBuffApplied);
            Require(log.AttackBonus == 30 + level && log.SourceOwnerId == owner && log.TargetCardId == "Master", "Combat log must report actual applied bonus for both seats.");
            var restore = typeof(BattleSession).GetMethod("RestoreBattleState", BindingFlags.NonPublic | BindingFlags.Static)!;
            var restored = (BattleState)restore.Invoke(null, new object[] { saved.DomainState!, provider })!;
            Require(restored.GetPlayer(owner).Master.CombatStatBuffs.Single().AttackBonus == 30 + level && restored.PersistentEffects.Count == 0, "JSON restore must retain only actual stat buff.");
        }
        Console.WriteLine("PASS Daehwandan: both seats Lv.0-13, Qi/Gold costs, no future production, exact combat logs, JSON buffs and reward/pack admission.");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
