using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Presentation.Battle
{
    public static class BattleStateSummaryFormatter
    {
        public static string Format(BattleState battleState)
        {
            if (battleState == null)
            {
                return "No active battle.";
            }

            return string.Join(
                Environment.NewLine,
                $"Turn: {battleState.TurnNumber}",
                $"Phase: {battleState.Phase}",
                $"Active Player: {BattleUiFormatter.FormatSide(battleState.ActivePlayerId)}",
                $"Player Master HP: {battleState.Player.Master.CurrentHp}/{battleState.Player.Master.MaxHp}",
                $"Opponent Master HP: {battleState.AI.Master.CurrentHp}/{battleState.AI.Master.MaxHp}",
                $"Player Resources: {FormatResources(battleState.Player.Resources)}",
                $"Opponent Resources: {FormatResources(battleState.AI.Resources)}",
                $"Player Hand/Deck/Discard: {battleState.Player.Hand.Count}/{battleState.Player.Deck.Count}/{battleState.Player.Discard.Count}",
                $"Opponent Hand/Deck/Discard: {battleState.AI.Hand.Count}/{battleState.AI.Deck.Count}/{battleState.AI.Discard.Count}",
                $"Persistent Effects: {battleState.PersistentEffects.Count}",
                $"Battle Ended: {battleState.IsEnded}");
        }

        private static string FormatResources(ResourceSet resourceSet)
        {
            return $"M:{resourceSet.Mana} Q:{resourceSet.Qi} P:{resourceSet.Power} G:{resourceSet.Gold}";
        }
    }
}
