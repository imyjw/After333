using System;
using System.Collections.Generic;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Tutorial
{
    public static class TutorialResourceBattle
    {
        public static readonly TileCoord GoblinTile = new TileCoord(1, 0);
        public static readonly TileCoord PondTile = new TileCoord(3, 1);
        public static readonly TileCoord OpponentArcherTile = new TileCoord(2, 0);
        public static readonly TileCoord MasterTile = new TileCoord(2, 1);
        public static LocalBattleGateway Create(ICardDefinitionProvider provider)
        {
            var processor = new BattleCommandProcessor(new PlayCardService(provider), new SpellService(provider),
                new MoveService(), new AttackService(), new EndTurnService());
            var flow = new BattleFlowController(new BattleSetupService(new OrderedDeck()), new MulliganService(),
                new TurnStartService(provider), processor);
            var gateway = new LocalBattleGateway(flow);
            var deck = CreateDeck();
            gateway.StartBattle(new BattleSetupRequest(deck, deck, PlayerId.Player));
            return gateway;
        }
        public static IReadOnlyList<string> CreateDeck()
        {
            // DeckState draws from the end. This order is independent of real deck/upgrades.
            var drawOrder = new List<string> { "Goblin", "ManaPond", "firebolt", "SkeletonArcher", "PowerPlant", "firebolt" };
            var pool = new[] { "Goblin", "ManaPond", "SkeletonArcher", "PowerPlant", "ManaShield", "PlasmaShield", "GaebangBranch", "ManaCannon", "OrcBerserker", "ElfLongbowScout", "A-301" };
            var copies = new Dictionary<string, int>();
            foreach (var id in drawOrder) copies[id] = copies.TryGetValue(id, out var count) ? count + 1 : 1;
            while (drawOrder.Count < 33)
                foreach (var id in pool)
                {
                    if (drawOrder.Count == 33) break;
                    if (copies.TryGetValue(id, out var count) && count >= 3) continue;
                    copies[id] = copies.TryGetValue(id, out count) ? count + 1 : 1;
                    drawOrder.Add(id);
                }
            drawOrder.Reverse();
            return drawOrder.AsReadOnly();
        }
        public static ResourceSet GrantPracticeResources(BattleState battle)
        {
            // Authored practice grant: demonstrate typed payment, then Gold substitution.
            var grant = new ResourceSet(mana: 3, qi: 0, power: 0, gold: 5);
            battle.Player.Resources.Add(grant);
            return grant;
        }
        public sealed class OrderedDeck : IDeckShuffler { public void Shuffle(DeckState deck) { } }
        public sealed class MulliganDeck : IDeckShuffler
        {
            public void Shuffle(DeckState deck)
            {
                // Shared mulligan returns the selected copy to the top after drawing.
                // Place that exact returned copy at the bottom, preserving authored next draws.
                if (!deck.TryDraw(out var returned)) throw new InvalidOperationException("Missing returned practice card.");
                deck.AddToBottom(returned);
            }
        }
    }
}
