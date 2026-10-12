using Project333.Runtime.Infrastructure.Data;

namespace Project333.PvpServer.Cards;

// Shared by run rewards and shop packs. Do not expand the explicit TrapReturn exception.
public static class RewardCardEligibility
{
    public static bool IsRewardableCard(JsonCardDefinitionRecord? card)
    {
        if (card == null || string.IsNullOrWhiteSpace(card.Id) || !card.IncludeInRewards ||
            string.Equals(card.Id, "Master", StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            var definition = card.ToDefinition();
            return definition is TrapSpellCardDefinition && definition.CardId == TrapReturnCardRules.CardId ||
                   CardUpgradeRules.IsCardUpgradeable(definition);
        }
        catch (Exception) { return false; }
    }
}
