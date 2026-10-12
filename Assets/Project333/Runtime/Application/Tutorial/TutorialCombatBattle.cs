using System;
using System.Linq;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Tutorial
{
    // Authored practice staging only. Player actions still use the shared battle services.
    public static class TutorialCombatBattle
    {
        public static readonly TileCoord ArcherTile = new TileCoord(2, 0);
        public static readonly TileCoord MovedArcherTile = new TileCoord(0, 1);
        public static readonly TileCoord FrontlineTile = new TileCoord(2, 0);
        public static readonly TileCoord EnemyGoblinTile = new TileCoord(1, 0);
        public static readonly TileCoord GolemTile = new TileCoord(3, 0);
        public static readonly TileCoord BuildingMoveTile = new TileCoord(4, 1);
        public static LocalBattleGateway Create(ICardDefinitionProvider provider)
        {
            var gateway = TutorialResourceBattle.Create(provider);
            gateway.ApplyMulligan(PlayerId.Player, new[] { "firebolt" }, new TutorialResourceBattle.MulliganDeck());
            gateway.ResolveTurnStart();
            TutorialResourceBattle.GrantPracticeResources(gateway.CurrentBattleState);
            gateway.ExecuteCommand(PlayerId.Player, new PlayUnitCardCommand("Goblin", TutorialResourceBattle.GoblinTile));
            gateway.ExecuteCommand(PlayerId.Player, new PlayBuildingCardCommand("ManaPond", TutorialResourceBattle.PondTile));
            gateway.EndTurn(); gateway.ResolveTurnStart();
            gateway.ExecuteCommand(PlayerId.AI, new PlayUnitCardCommand("SkeletonArcher", TutorialResourceBattle.OpponentArcherTile));
            gateway.EndTurn(); gateway.ResolveTurnStart();
            return gateway;
        }
        public static void PrepareFrontline(BattleState battle, ICardDefinitionProvider provider)
        {
            if (battle.AIBoard.GetOccupant(FrontlineTile)?.CardId == "Goblin") return;
            if (battle.AIBoard.GetOccupant(FrontlineTile) != null)
                battle.AIBoard.Move(FrontlineTile, new TileCoord(0, 0));
            var d = (UnitCardDefinition)provider.GetRequired("Goblin");
            battle.AIBoard.Place(FrontlineTile, new UnitState("tutorial-frontline-goblin", d.CardId, PlayerId.AI,
                FrontlineTile, d.AttackType, d.Attack, d.Health, d.CanMove, d.IsScience, d.SciencePowerUpkeep,
                damageType: d.DamageType, physicalDefense: d.PhysicalDefense, magicDefense: d.MagicDefense, races: d.Races));
        }
        public static PlayUnitCardCommand OpponentSummon(BattleState battle, bool golem)
        {
            var id = golem ? "Golem" : "Goblin";
            if (!battle.AI.Hand.CardIds.Contains(id)) battle.AI.Hand.Add(id);
            return new PlayUnitCardCommand(id, golem ? GolemTile : EnemyGoblinTile);
        }
        public static void EmptyPracticeDeck(BattleState battle)
        { while (battle.Player.Deck.TryDraw(out _)) { } }
        public static void PrepareVictory(BattleState battle)
        {
            if (battle.IsEnded) throw new InvalidOperationException("Practice victory has already resolved.");
            battle.AIBoard.GetOccupant(TutorialResourceBattle.MasterTile).CurrentHp = 10;
        }
        public static TileCoord FindPlayerArcher(BattleState battle) =>
            battle.PlayerBoard.EnumerateOccupants().First(x => x.CardId == "SkeletonArcher").Position;
    }
}
