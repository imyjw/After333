using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using Project333.PvpServer.AccountOperations;
using Project333.PvpServer.Auth;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Cards;
using Project333.PvpServer.Persistence.Db;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.PvpServer.Shop;

public sealed class CardPackService
{
    public const long ResourceGoldCost = 33;
    public const int CardsPerPack = 10;
    public const string OperationType = "card_pack_purchase";
    private const string Fingerprint = "{\"ProductId\":\"CardPack10\"}";
    private readonly DbConnectionFactory _connectionFactory;
    private readonly JsonCardDefinitionDatabase _database;
    private static readonly CardRarity[] Rarities =
        { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare, CardRarity.Unique, CardRarity.Legendary };

    public CardPackService(DbConnectionFactory connectionFactory)
        : this(connectionFactory, PrototypeCardDefinitions.LoadCardDefinitionDatabase()) { }

    public CardPackService(DbConnectionFactory connectionFactory, JsonCardDefinitionDatabase database)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public bool IsDatabaseConfigured => _connectionFactory.IsConfigured;

    public async Task<PurchaseCardPackResponse> PurchaseAsync(AuthenticatedAccount account,
        PurchaseCardPackRequest? request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(account);
        var requestId = AccountOperationReceipt.ParseRequestId(request?.RequestId);
        await using var connection = await _connectionFactory.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        // Same lock/order as purchase, upgrade and durable operation resolution.
        var wallet = await AccountOperationReceipt.LockWalletAsync(connection, transaction, account.Account.Id, ct);
        var original = await LoadReceiptAsync(connection, transaction, account.Account.Id, requestId, ct);
        if (original != null)
        {
            await transaction.CommitAsync(ct);
            return original with { Replayed = true, Account = account.Account, Wallet = wallet };
        }
        if (wallet.ResourceGold < ResourceGoldCost)
            throw new CardPackServiceException("insufficient_resource_gold", "카드팩 구매에는 33골드가 필요합니다.");

        var cardIds = DrawCardIds(_database);
        var grants = cardIds.GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.Ordinal).ToArray();
        await using (var spend = new NpgsqlCommand("""
            update user_wallets set resource_gold=resource_gold-@cost, updated_at=now()
            where account_id=@id and resource_gold>=@cost returning resource_gold,tickets
            """, connection, transaction))
        {
            spend.Parameters.AddWithValue("id", account.Account.Id);
            spend.Parameters.AddWithValue("cost", ResourceGoldCost);
            await using var reader = await spend.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                throw new CardPackServiceException("insufficient_resource_gold", "카드팩 구매에는 33골드가 필요합니다.");
            wallet = new WalletDto(reader.GetInt64(0), reader.GetInt32(1));
        }
        await WriteLedgerAsync(connection, transaction, account.Account.Id, requestId, null, 0, -ResourceGoldCost, ct);
        foreach (var group in grants)
        {
            await using var grant = new NpgsqlCommand("""
                insert into user_card_collection(account_id,card_id,copy_count,upgrade_level)
                values(@id,@card,@count,0)
                on conflict(account_id,card_id) do update
                set copy_count=user_card_collection.copy_count+excluded.copy_count,updated_at=now()
                """, connection, transaction);
            grant.Parameters.AddWithValue("id", account.Account.Id);
            grant.Parameters.AddWithValue("card", group.Key);
            grant.Parameters.AddWithValue("count", group.Count());
            await grant.ExecuteNonQueryAsync(ct);
            await WriteLedgerAsync(connection, transaction, account.Account.Id, requestId, group.Key, group.Count(), 0, ct);
        }
        var response = new PurchaseCardPackResponse(requestId.ToString("D"), false, ResourceGoldCost, account.Account, wallet, cardIds);
        // Original ordered cards, charge, collection increments and ledger commit together.
        await AccountOperationReceipt.SaveAsync(connection, transaction, account.Account.Id, requestId,
            OperationType, Fingerprint, response, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<PurchaseCardPackResponse> GetPurchaseAsync(AuthenticatedAccount account,
        string? rawRequestId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(account);
        var requestId = AccountOperationReceipt.ParseRequestId(rawRequestId);
        await using var connection = await _connectionFactory.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        var wallet = await AccountOperationReceipt.LockWalletAsync(connection, transaction, account.Account.Id, ct);
        var original = await LoadReceiptAsync(connection, transaction, account.Account.Id, requestId, ct)
            ?? throw new CardPackServiceException("card_pack_purchase_not_found", "카드팩 구매 내역이 없습니다.", 404);
        await transaction.CommitAsync(ct);
        return original with { Replayed = true, Account = account.Account, Wallet = wallet };
    }

    public static CardRarity ResolveRarity(int roll)
    {
        if (roll < 0 || roll >= 100) throw new ArgumentOutOfRangeException(nameof(roll));
        return roll < 40 ? CardRarity.Common : roll < 70 ? CardRarity.Uncommon :
            roll < 88 ? CardRarity.Rare : roll < 97 ? CardRarity.Unique : CardRarity.Legendary;
    }

    public static IReadOnlyList<string> DrawCardIds(JsonCardDefinitionDatabase database, Func<int, int>? next = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        next ??= RandomNumberGenerator.GetInt32;
        var eligible = (database.Cards ?? new List<JsonCardDefinitionRecord>())
            .Where(RewardCardEligibility.IsRewardableCard).ToArray();
        if (eligible.Select(card => card.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != eligible.Length)
            throw new CardPackServiceException("card_pack_pool_unavailable", "카드팩을 잠시 구매할 수 없습니다.", 503);
        var pools = eligible.GroupBy(card => card.Rarity).ToDictionary(group => group.Key, group => group.ToArray());
        // Never silently normalize away a promised rarity. Reject before any charge.
        if (Rarities.Any(rarity => !pools.TryGetValue(rarity, out var pool) || pool.Length == 0))
            throw new CardPackServiceException("card_pack_pool_unavailable", "카드팩을 잠시 구매할 수 없습니다.", 503);
        var result = new string[CardsPerPack];
        for (var index = 0; index < result.Length; index++)
        {
            var pool = pools[ResolveRarity(next(100))];
            var selected = next(pool.Length);
            if (selected < 0 || selected >= pool.Length) throw new ArgumentOutOfRangeException(nameof(next));
            result[index] = pool[selected].Id;
        }
        return result;
    }

    private static async Task<PurchaseCardPackResponse?> LoadReceiptAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid accountId, Guid requestId, CancellationToken ct)
    {
        PurchaseCardPackResponse? receipt;
        try
        {
            receipt = await AccountOperationReceipt.FindAsync<PurchaseCardPackResponse>(connection, transaction,
                accountId, requestId, OperationType, Fingerprint, ct);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or NotSupportedException)
        { throw ReceiptUnavailable(); }
        if (receipt != null &&
            (!Guid.TryParseExact(receipt.RequestId, "D", out var storedId) || storedId != requestId ||
             receipt.Account == null || receipt.Account.Id != accountId || receipt.Wallet == null ||
             receipt.ResourceGoldCost != ResourceGoldCost || receipt.Wallet.ResourceGold < 0 || receipt.Wallet.Tickets < 0 ||
             receipt.CardIds == null || receipt.CardIds.Count != CardsPerPack ||
             receipt.CardIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 128 || id != id.Trim())))
            throw ReceiptUnavailable();
        await VerifyReceiptLedgerAsync(connection, transaction, accountId, requestId, receipt, ct);
        // Current eligibility is deliberately not checked: a committed original draw must survive pool revisions.
        return receipt;
    }

    private static async Task VerifyReceiptLedgerAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid accountId, Guid requestId, PurchaseCardPackResponse? receipt, CancellationToken ct)
    {
        await using var query = new NpgsqlCommand("""
            select card_id,card_count_delta,resource_gold_delta from account_transactions
            where account_id=@id and source_table='account_operation_receipts' and source_id=@request
            """, connection, transaction);
        query.Parameters.AddWithValue("id", accountId);
        query.Parameters.AddWithValue("request", requestId);
        await using var rows = await query.ExecuteReaderAsync(ct);
        var grants = new Dictionary<string, int>(StringComparer.Ordinal);
        var chargeCount = 0;
        while (await rows.ReadAsync(ct))
        {
            // A payment whose receipt was removed must never become a new charge.
            if (receipt == null || rows.IsDBNull(1)) throw ReceiptUnavailable();
            var count = rows.GetInt32(1);
            var gold = rows.GetInt64(2);
            if (rows.IsDBNull(0))
            {
                if (count != 0 || gold != -ResourceGoldCost) throw ReceiptUnavailable();
                chargeCount++;
            }
            else
            {
                var id = rows.GetString(0);
                if (string.IsNullOrWhiteSpace(id) || count <= 0 || gold != 0 || !grants.TryAdd(id, count))
                    throw ReceiptUnavailable();
            }
        }
        if (receipt == null) return;
        var expected = receipt.CardIds.GroupBy(id => id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        if (chargeCount != 1 || grants.Count != expected.Count ||
            expected.Any(group => !grants.TryGetValue(group.Key, out var count) || count != group.Value))
            throw ReceiptUnavailable();
    }

    private static CardPackServiceException ReceiptUnavailable() => new(
        "card_pack_receipt_unavailable", "카드팩 구매 내역을 확인할 수 없습니다.");

    private static async Task WriteLedgerAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid accountId, Guid requestId, string? cardId, int cardCount, long goldDelta, CancellationToken ct)
    {
        await using var ledger = new NpgsqlCommand("""
            insert into account_transactions(account_id,transaction_type,resource_gold_delta,card_id,card_count_delta,source_table,source_id)
            values(@id,'purchase',@gold,@card,@count,'account_operation_receipts',@request)
            """, connection, transaction);
        ledger.Parameters.AddWithValue("id", accountId);
        ledger.Parameters.AddWithValue("gold", goldDelta);
        ledger.Parameters.AddWithValue("card", NpgsqlTypes.NpgsqlDbType.Text, cardId == null ? DBNull.Value : cardId);
        ledger.Parameters.AddWithValue("count", cardCount);
        ledger.Parameters.AddWithValue("request", requestId);
        await ledger.ExecuteNonQueryAsync(ct);
    }
}
