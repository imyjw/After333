using Npgsql;
using Project333.PvpServer.Auth;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Persistence.Db;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.PvpServer.Cards;

public sealed class CardUpgradeService
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly JsonCardDefinitionDatabase _cardDatabase;

    public CardUpgradeService(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _cardDatabase = PrototypeCardDefinitions.LoadCardDefinitionDatabase();
    }

    public bool IsDatabaseConfigured => _connectionFactory.IsConfigured;

    public async Task<UpgradeCardResponse> UpgradeCardAsync(
        AuthenticatedAccount account,
        UpgradeCardRequest? request,
        CancellationToken cancellationToken)
    {
        if (account == null)
        {
            throw new ArgumentNullException(nameof(account));
        }

        var cardDefinition = ResolveCanonicalCardDefinition(request?.CardId);
        var cardId = cardDefinition.CardId;
        if (!CardUpgradeRules.IsCardUpgradeable(cardDefinition))
        {
            throw new CardUpgradeServiceException(
                "card_not_upgradeable",
                $"Card '{cardId}' cannot be upgraded.");
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var ownedCard = await LoadOwnedCardForUpdateAsync(
            connection,
            transaction,
            account.Account.Id,
            cardId,
            cancellationToken);
        if (ownedCard == null)
        {
            throw new CardUpgradeServiceException(
                "card_not_owned",
                $"Card '{cardId}' is not owned by this account.");
        }

        if (!CardUpgradeRules.TryGetNextUpgradeCost(ownedCard.UpgradeLevel, out _))
        {
            throw new CardUpgradeServiceException(
                "max_level_reached",
                $"Card '{cardId}' is already at max level {CardUpgradeRules.MaxLevel}.");
        }

        var cost = await LoadUpgradeCostAsync(
            connection,
            transaction,
            ownedCard.UpgradeLevel,
            ownedCard.UpgradeLevel + 1,
            cancellationToken);

        if (ownedCard.CopyCount < cost.RequiredCopyCount)
        {
            throw new CardUpgradeServiceException(
                "insufficient_card_copies",
                $"Card '{cardId}' needs {cost.RequiredCopyCount} copies to upgrade to Lv.{cost.LevelTo}.");
        }

        var wallet = await SpendResourceGoldAsync(
            connection,
            transaction,
            account.Account.Id,
            cost.RequiredResourceGold,
            cancellationToken);
        var upgradedCard = await ApplyCardUpgradeAsync(
            connection,
            transaction,
            account.Account.Id,
            cardId,
            ownedCard,
            cost,
            cancellationToken);
        await InsertUpgradeTransactionAsync(
            connection,
            transaction,
            account.Account.Id,
            cardId,
            cost,
            cancellationToken);
        var collection = await LoadCollectionSummaryAsync(
            connection,
            transaction,
            account.Account.Id,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new UpgradeCardResponse(
            account.Account,
            wallet,
            collection,
            upgradedCard,
            cost);
    }

    private CardDefinition ResolveCanonicalCardDefinition(string? requestedCardId)
    {
        var normalizedCardId = requestedCardId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCardId))
        {
            throw new CardUpgradeServiceException("invalid_card_id", "CardId is required.");
        }

        var card = _cardDatabase.Cards.FirstOrDefault(card =>
            card != null &&
            string.Equals(card.Id, normalizedCardId, StringComparison.OrdinalIgnoreCase));
        if (card == null || string.IsNullOrWhiteSpace(card.Id))
        {
            throw new CardUpgradeServiceException(
                "card_definition_not_found",
                $"Card definition '{normalizedCardId}' was not found.");
        }

        return card.ToDefinition();
    }

    private static async Task<OwnedCardDto?> LoadOwnedCardForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string cardId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select card_id, copy_count, upgrade_level
            from user_card_collection
            where account_id = @accountId
              and lower(card_id) = lower(@cardId)
            for update;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("cardId", cardId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new OwnedCardDto(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetInt32(2));
    }

    private static async Task<UpgradeCardCostDto> LoadUpgradeCostAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int levelFrom,
        int levelTo,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select required_copy_count, required_resource_gold
            from card_upgrade_costs
            where level_from = @levelFrom
              and level_to = @levelTo;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("levelFrom", levelFrom);
        command.Parameters.AddWithValue("levelTo", levelTo);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new CardUpgradeServiceException(
                "upgrade_cost_not_found",
                $"Upgrade cost Lv.{levelFrom} -> Lv.{levelTo} was not found.");
        }

        return new UpgradeCardCostDto(
            levelFrom,
            levelTo,
            reader.GetInt32(0),
            reader.GetInt64(1));
    }

    private static async Task<WalletDto> SpendResourceGoldAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        long resourceGoldCost,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update user_wallets
            set
                resource_gold = resource_gold - @resourceGoldCost,
                updated_at = now()
            where account_id = @accountId
              and resource_gold >= @resourceGoldCost
            returning resource_gold, tickets;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("resourceGoldCost", resourceGoldCost);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new CardUpgradeServiceException(
                "insufficient_resource_gold",
                $"Not enough Resource Gold. Upgrade requires {resourceGoldCost} Resource Gold.");
        }

        return new WalletDto(
            reader.GetInt64(0),
            reader.GetInt32(1));
    }

    private static async Task<OwnedCardDto> ApplyCardUpgradeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string cardId,
        OwnedCardDto ownedCard,
        UpgradeCardCostDto cost,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update user_card_collection
            set
                copy_count = copy_count - @requiredCopyCount,
                upgrade_level = @levelTo,
                updated_at = now()
            where account_id = @accountId
              and lower(card_id) = lower(@cardId)
              and copy_count >= @requiredCopyCount
              and upgrade_level = @levelFrom
            returning card_id, copy_count, upgrade_level;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("cardId", cardId);
        command.Parameters.AddWithValue("requiredCopyCount", cost.RequiredCopyCount);
        command.Parameters.AddWithValue("levelFrom", cost.LevelFrom);
        command.Parameters.AddWithValue("levelTo", cost.LevelTo);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new CardUpgradeServiceException(
                "card_upgrade_conflict",
                $"Card '{ownedCard.CardId}' could not be upgraded because collection state changed.");
        }

        return new OwnedCardDto(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetInt32(2));
    }

    private static async Task InsertUpgradeTransactionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string cardId,
        UpgradeCardCostDto cost,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into account_transactions(
                account_id,
                transaction_type,
                resource_gold_delta,
                card_id,
                card_count_delta,
                upgrade_level_before,
                upgrade_level_after,
                source_table
            )
            values (
                @accountId,
                'upgrade',
                @resourceGoldDelta,
                @cardId,
                @cardCountDelta,
                @levelFrom,
                @levelTo,
                'user_card_collection'
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("resourceGoldDelta", -cost.RequiredResourceGold);
        command.Parameters.AddWithValue("cardId", cardId);
        command.Parameters.AddWithValue("cardCountDelta", -cost.RequiredCopyCount);
        command.Parameters.AddWithValue("levelFrom", cost.LevelFrom);
        command.Parameters.AddWithValue("levelTo", cost.LevelTo);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<CollectionSummaryDto> LoadCollectionSummaryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select card_id, copy_count, upgrade_level
            from user_card_collection
            where account_id = @accountId
              and (copy_count > 0 or upgrade_level > 0)
            order by card_id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var ownedCards = new List<OwnedCardDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            ownedCards.Add(new OwnedCardDto(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetInt32(2)));
        }

        return new CollectionSummaryDto(ownedCards.Count, ownedCards);
    }
}
