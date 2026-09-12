using System.Text.Json;
using Npgsql;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Auth;
using Project333.PvpServer.Persistence.Db;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.PvpServer.Runs;

public sealed class RunStartService
{
    private const int DraftDeckSize = 33;
    private const int RandomCardRewardsPerWin = 3;

    private static readonly (CardRarity Rarity, int Weight)[] RunRewardRarityWeights =
    {
        (CardRarity.Common, 40),
        (CardRarity.Uncommon, 30),
        (CardRarity.Rare, 18),
        (CardRarity.Unique, 9),
        (CardRarity.Legendary, 3),
    };

    private readonly DbConnectionFactory _connectionFactory;
    private readonly IConfiguration _configuration;
    private readonly ServerDraftOfferGenerator _draftOfferGenerator;

    public RunStartService(
        DbConnectionFactory connectionFactory,
        IConfiguration configuration)
        : this(connectionFactory, configuration, PrototypeCardDefinitions.LoadCardDefinitionDatabase()) { }

    public RunStartService(DbConnectionFactory connectionFactory, IConfiguration configuration, JsonCardDefinitionDatabase database)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _draftOfferGenerator = new ServerDraftOfferGenerator(database);
    }

    public bool IsDatabaseConfigured => _connectionFactory.IsConfigured;

    public async Task<StartRunResponse> StartDraftRunAsync(
        AuthenticatedAccount account,
        StartRunRequest? request,
        CancellationToken cancellationToken)
    {
        if (account == null)
        {
            throw new ArgumentNullException(nameof(account));
        }

        var mode = NormalizeMode(request?.Mode);
        var ticketCost = ResolveTicketCost();
        var draftSeed = Random.Shared.Next();
        var openingOfferCardIds = _draftOfferGenerator.CreateOffer(
            draftSeed,
            Array.Empty<string>());

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);

        // Serialize starts for this account before reading any active run. The next
        // READ COMMITTED statement sees the preceding starter's committed run.
        await LockRunStartWalletAsync(connection, transaction, account.Account.Id, cancellationToken);

        await EnsureCanStartNewDraftRunAsync(
            connection,
            transaction,
            account.Account.Id,
            cancellationToken);

        var wallet = await SpendTicketsAsync(
            connection,
            transaction,
            account.Account.Id,
            ticketCost,
            cancellationToken);
        RunSummaryDto run;
        try
        {
            run = await InsertDraftRunAsync(connection, transaction, account.Account.Id,
                mode, draftSeed, ticketCost, cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation &&
                                           ex.ConstraintName == "draft_runs_one_active_per_account_idx")
        {
            // The transaction (including its ticket debit) rolls back on disposal.
            throw new RunServiceException("active_run_exists", "A draft run is already active. Resume it before starting a new run.");
        }
        await UpdateDraftRunCurrentOfferAsync(
            connection,
            transaction,
            run.Id,
            openingOfferCardIds,
            cancellationToken);
        await InsertTicketSpendTransactionAsync(
            connection,
            transaction,
            account.Account.Id,
            run.Id,
            ticketCost,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new StartRunResponse(
            account.Account,
            wallet,
            run,
            Array.Empty<string>(),
            openingOfferCardIds);
    }

    public async Task<CompleteDraftResponse> CompleteDraftAsync(
        AuthenticatedAccount account,
        CompleteDraftRequest? request,
        CancellationToken cancellationToken)
    {
        if (account == null)
        {
            throw new ArgumentNullException(nameof(account));
        }

        var runId = ParseRunId(request?.RunId);
        var cardIds = NormalizeDeckCardIds(request?.CardIds);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await ValidateDraftRunForCompletionAsync(
            connection,
            transaction,
            account.Account.Id,
            runId,
            cancellationToken);

        var deck = await InsertCompletedDeckAsync(
            connection,
            transaction,
            account.Account.Id,
            runId,
            cardIds,
            cancellationToken);
        await InsertDeckCardsAsync(
            connection,
            transaction,
            deck.Id,
            cardIds,
            cancellationToken);
        await ReplaceDraftRunPicksAsync(
            connection,
            transaction,
            runId,
            Array.Empty<string>(),
            cancellationToken);
        await UpdateDraftRunCurrentOfferAsync(
            connection,
            transaction,
            runId,
            Array.Empty<string>(),
            cancellationToken);
        var run = await MarkDraftRunReadyAsync(
            connection,
            transaction,
            runId,
            deck.Id,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new CompleteDraftResponse(
            account.Account,
            account.Wallet,
            run,
            deck);
    }

    public async Task<SaveDraftPicksResponse> SaveDraftPicksAsync(
        AuthenticatedAccount account,
        SaveDraftPicksRequest? request,
        CancellationToken cancellationToken)
    {
        if (account == null)
        {
            throw new ArgumentNullException(nameof(account));
        }

        var runId = ParseRunId(request?.RunId);
        var cardIds = NormalizeDraftPickCardIds(request?.CardIds);
        var currentOfferCardIds = NormalizeCurrentOfferCardIds(request?.CurrentOfferCardIds);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var run = await ValidateDraftRunForPickSaveAsync(
            connection,
            transaction,
            account.Account.Id,
            runId,
            cancellationToken);

        var existingPickCount = await CountDraftRunPicksAsync(
            connection,
            transaction,
            runId,
            cancellationToken);
        if (existingPickCount > cardIds.Count)
        {
            throw new RunServiceException(
                "stale_draft_picks",
                $"Draft pick save is stale. Server already has {existingPickCount} picks.");
        }

        await ReplaceDraftRunPicksAsync(
            connection,
            transaction,
            runId,
            cardIds,
            cancellationToken);
        await UpdateDraftRunCurrentOfferAsync(
            connection,
            transaction,
            runId,
            currentOfferCardIds,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new SaveDraftPicksResponse(
            account.Account,
            account.Wallet,
            run,
            cardIds,
            currentOfferCardIds);
    }

    public async Task<DraftStateResponse> GetAuthoritativeDraftStateAsync(
        AuthenticatedAccount account,
        DraftStateRequest? request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        var runId = ParseRunId(request?.RunId);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var lockedRun = await LoadAuthoritativeDraftRunForUpdateAsync(
            connection,
            transaction,
            account.Account.Id,
            runId,
            cancellationToken);
        var selectedCardIds = await LoadDraftRunPickCardIdsAsync(
            connection,
            transaction,
            runId,
            cancellationToken);
        var currentOfferCardIds = await GetOrCreateSavedOfferAsync(connection, transaction, runId, lockedRun, selectedCardIds, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new DraftStateResponse(
            account.Account,
            account.Wallet,
            lockedRun.Run,
            selectedCardIds,
            currentOfferCardIds);
    }

    public async Task<SelectDraftCardResponse> SelectAuthoritativeDraftCardAsync(
        AuthenticatedAccount account,
        SelectDraftCardRequest? request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        var runId = ParseRunId(request?.RunId);
        if (request == null || request.PickIndex < 0 || request.PickIndex >= DraftDeckSize)
        {
            throw new RunServiceException(
                "invalid_draft_pick_index",
                $"Draft pick index must be between 0 and {DraftDeckSize - 1}.");
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var lockedRun = await LoadAuthoritativeDraftRunForUpdateAsync(
            connection,
            transaction,
            account.Account.Id,
            runId,
            cancellationToken);
        var selectedCardIds = (await LoadDraftRunPickCardIdsAsync(
                connection,
                transaction,
                runId,
                cancellationToken))
            .ToList();
        if (selectedCardIds.Count != request.PickIndex)
        {
            throw new RunServiceException(
                "stale_draft_pick_index",
                $"Draft pick request expected index {request.PickIndex}, but the server is at index {selectedCardIds.Count}.");
        }

        var authoritativeOfferCardIds = await GetOrCreateSavedOfferAsync(connection, transaction, runId, lockedRun, selectedCardIds, cancellationToken);
        var selectedCardId = _draftOfferGenerator.ResolveSelectedCard(
            authoritativeOfferCardIds,
            request.CardId);

        await AppendAuthoritativeDraftPickAsync(
            connection,
            transaction,
            runId,
            selectedCardIds.Count,
            authoritativeOfferCardIds,
            selectedCardId,
            cancellationToken);
        selectedCardIds.Add(selectedCardId);

        if (selectedCardIds.Count == DraftDeckSize)
        {
            var completedCardIds = _draftOfferGenerator.ValidateSavedCompletedDeck(selectedCardIds);
            var deck = await InsertCompletedDeckAsync(
                connection,
                transaction,
                account.Account.Id,
                runId,
                completedCardIds,
                cancellationToken);
            await InsertDeckCardsAsync(
                connection,
                transaction,
                deck.Id,
                completedCardIds,
                cancellationToken);
            await UpdateDraftRunCurrentOfferAsync(
                connection,
                transaction,
                runId,
                Array.Empty<string>(),
                cancellationToken);
            var completedRun = await MarkDraftRunReadyAsync(
                connection,
                transaction,
                runId,
                deck.Id,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new SelectDraftCardResponse(
                account.Account,
                account.Wallet,
                completedRun,
                true,
                completedCardIds,
                Array.Empty<string>(),
                deck);
        }

        var nextOfferCardIds = _draftOfferGenerator.CreateOfferFromSavedPicks(
            lockedRun.DraftSeed,
            selectedCardIds);
        await UpdateDraftRunCurrentOfferAsync(
            connection,
            transaction,
            runId,
            nextOfferCardIds,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SelectDraftCardResponse(
            account.Account,
            account.Wallet,
            lockedRun.Run,
            false,
            selectedCardIds,
            nextOfferCardIds,
            null);
    }

    public async Task<ClaimRunRewardsResponse> ClaimRunRewardsAsync(
        AuthenticatedAccount account,
        ClaimRunRewardsRequest? request,
        CancellationToken cancellationToken)
    {
        if (account == null)
        {
            throw new ArgumentNullException(nameof(account));
        }

        var runId = ParseRunId(request?.RunId);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var lockedRun = await LoadCompletedRunForRewardClaimAsync(
            connection,
            transaction,
            account.Account.Id,
            runId,
            cancellationToken);
        var rewardSpec = ResolveRunEndReward(lockedRun);
        var reward = await InsertRewardGrantAsync(
            connection,
            transaction,
            account.Account.Id,
            lockedRun.Id,
            rewardSpec,
            cancellationToken);
        var wallet = await ApplyWalletRewardAsync(
            connection,
            transaction,
            account.Account.Id,
            rewardSpec.ResourceGoldDelta,
            rewardSpec.TicketDelta,
            cancellationToken);

        await ApplyCardRewardsAsync(
            connection,
            transaction,
            account.Account.Id,
            reward.Id,
            rewardSpec.CardRewards,
            cancellationToken);
        await ApplyPackRewardsAsync(
            connection,
            transaction,
            account.Account.Id,
            reward.Id,
            rewardSpec.PackRewards,
            cancellationToken);
        await InsertWalletRewardTransactionAsync(
            connection,
            transaction,
            account.Account.Id,
            reward.Id,
            rewardSpec.ResourceGoldDelta,
            rewardSpec.TicketDelta,
            cancellationToken);
        var claimedRun = await MarkRunRewardsClaimedAsync(
            connection,
            transaction,
            lockedRun.Id,
            reward.CreatedAt,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new ClaimRunRewardsResponse(
            account.Account,
            wallet,
            claimedRun,
            reward);
    }

    public async Task<ActiveRunStateDto> GetLatestRunStateForAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var activeRunState = await LoadLatestRunStateAsync(
            connection,
            accountId,
            resumableOnly: true,
            cancellationToken);
        var latestRunState = await LoadLatestRunStateAsync(
            connection,
            accountId,
            resumableOnly: false,
            cancellationToken);

        return new ActiveRunStateDto(
            activeRunState.Run,
            activeRunState.Deck,
            activeRunState.DeckCardIds,
            activeRunState.DraftPickCardIds,
            activeRunState.CurrentOfferCardIds,
            latestRunState.Run,
            latestRunState.Deck,
            latestRunState.DeckCardIds,
            latestRunState.DraftPickCardIds,
            latestRunState.CurrentOfferCardIds);
    }

    private static async Task<RunStateQueryResult> LoadLatestRunStateAsync(
        NpgsqlConnection connection,
        Guid accountId,
        bool resumableOnly,
        CancellationToken cancellationToken)
    {
        var sql = resumableOnly
            ? """
              select
                  r.id,
                  r.status,
                  r.mode,
                  r.wins,
                  r.losses,
                  r.ticket_cost_paid,
                  r.started_at,
                  r.reward_claimed_at,
                  d.id,
                  d.name,
                  d.deck_type,
                  d.source_run_id,
                  d.card_definition_version,
                  (
                      select count(*)
                      from deck_cards dc
                      where dc.deck_id = d.id
                  ) as deck_card_count,
                  r.current_offer_card_ids
              from draft_runs r
              left join decks d on d.id = r.completed_deck_id
              where r.account_id = @accountId
                and r.status in ('drafting', 'ready', 'in_progress')
              order by r.started_at desc
              limit 1;
              """
            : """
            select
                r.id,
                r.status,
                r.mode,
                r.wins,
                r.losses,
                r.ticket_cost_paid,
                r.started_at,
                r.reward_claimed_at,
                d.id,
                d.name,
                d.deck_type,
                d.source_run_id,
                d.card_definition_version,
                (
                    select count(*)
                    from deck_cards dc
                    where dc.deck_id = d.id
                ) as deck_card_count,
                r.current_offer_card_ids
            from draft_runs r
            left join decks d on d.id = r.completed_deck_id
            where r.account_id = @accountId
            order by r.started_at desc
            limit 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("accountId", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new RunStateQueryResult(null, null, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        }

        var run = ReadRunSummary(reader);
        DeckSummaryDto? deck = null;
        Guid? deckId = null;
        if (!reader.IsDBNull(8))
        {
            deckId = reader.GetGuid(8);
            deck = new DeckSummaryDto(
                deckId.Value,
                reader.GetString(9),
                reader.GetString(10),
                reader.IsDBNull(11) ? run.Id : reader.GetGuid(11),
                Convert.ToInt32(reader.GetInt64(13)),
                reader.GetString(12));
        }

        var currentOfferCardIds = string.Equals(run.Status, "drafting", StringComparison.OrdinalIgnoreCase) && !reader.IsDBNull(14)
            ? DeserializeCardIdList(reader.GetString(14))
            : Array.Empty<string>();
        await reader.CloseAsync();
        var deckCardIds = deckId.HasValue
            ? await LoadDeckCardIdsAsync(connection, deckId.Value, cancellationToken)
            : Array.Empty<string>();
        var draftPickCardIds = string.Equals(run.Status, "drafting", StringComparison.OrdinalIgnoreCase)
            ? await LoadDraftPickCardIdsAsync(connection, run.Id, cancellationToken)
            : Array.Empty<string>();

        return new RunStateQueryResult(run, deck, deckCardIds, draftPickCardIds, currentOfferCardIds);
    }

    private sealed record RunStateQueryResult(
        RunSummaryDto? Run,
        DeckSummaryDto? Deck,
        IReadOnlyList<string> DeckCardIds,
        IReadOnlyList<string> DraftPickCardIds,
        IReadOnlyList<string> CurrentOfferCardIds);

    public async Task<IReadOnlyList<string>> LoadBattleDeckCardIdsAsync(
        AuthenticatedAccount account,
        string? runId,
        string? deckId,
        CancellationToken cancellationToken)
    {
        if (account == null)
        {
            throw new ArgumentNullException(nameof(account));
        }

        var parsedRunId = ParseRunId(runId);
        var parsedDeckId = ParseDeckId(deckId);

        const string sql = """
            select 1
            from draft_runs r
            join decks d on d.id = r.completed_deck_id
            where r.id = @runId
              and r.account_id = @accountId
              and r.completed_deck_id = @deckId
              and d.id = @deckId
              and d.account_id = @accountId
              and r.status in ('ready', 'in_progress')
            limit 1;
            """;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using (var command = new NpgsqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("runId", parsedRunId);
            command.Parameters.AddWithValue("deckId", parsedDeckId);
            command.Parameters.AddWithValue("accountId", account.Account.Id);

            var exists = await command.ExecuteScalarAsync(cancellationToken);
            if (exists == null)
            {
                throw new RunServiceException(
                    "battle_deck_not_found",
                    "The selected battle deck was not found for this account or is not ready.");
            }
        }

        var cardIds = await LoadDeckCardIdsAsync(connection, parsedDeckId, cancellationToken);
        if (cardIds.Count != DraftDeckSize)
        {
            throw new RunServiceException(
                "invalid_battle_deck_size",
                $"Battle deck must contain exactly {DraftDeckSize} cards.");
        }

        return cardIds;
    }

    private static async Task<IReadOnlyList<string>> LoadDeckCardIdsAsync(
        NpgsqlConnection connection,
        Guid deckId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select card_id
            from deck_cards
            where deck_id = @deckId
            order by slot_index;
            """;

        var cardIds = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("deckId", deckId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            cardIds.Add(reader.GetString(0));
        }

        return cardIds;
    }

    private static async Task<IReadOnlyList<string>> LoadDraftPickCardIdsAsync(
        NpgsqlConnection connection,
        Guid runId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select selected_card_id
            from draft_run_picks
            where run_id = @runId
            order by pick_index;
            """;

        var cardIds = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("runId", runId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            cardIds.Add(reader.GetString(0));
        }

        return cardIds;
    }

    private static async Task<RunSummaryDto> LoadCompletedRunForRewardClaimAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select id, status, mode, wins, losses, ticket_cost_paid, started_at, reward_claimed_at
            from draft_runs
            where id = @runId
              and account_id = @accountId
            for update;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("runId", runId);
        command.Parameters.AddWithValue("accountId", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new RunServiceException("run_not_found", "Draft run was not found for this account.");
        }

        var run = ReadRunSummary(reader);
        if (!string.Equals(run.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            throw new RunServiceException(
                "run_not_completed",
                "Run rewards can only be claimed after the run is completed.");
        }

        if (run.RewardClaimedAt.HasValue)
        {
            throw new RunServiceException(
                "reward_already_claimed",
                "Rewards for this run have already been claimed.");
        }

        return run;
    }

    private RunRewardSpec ResolveRunEndReward(RunSummaryDto run)
    {
        var resourceGold = ResolveLong("PROJECT333_RUN_REWARD_RESOURCE_GOLD_BASE", 0) +
                           ResolveLong("PROJECT333_RUN_REWARD_RESOURCE_GOLD_PER_WIN", 3) * run.Wins +
                           ResolveLong("PROJECT333_RUN_REWARD_RESOURCE_GOLD_PER_LOSS", 0) * run.Losses;
        var tickets = ResolveInt("PROJECT333_RUN_REWARD_TICKETS_BASE", 0) +
                      ResolveInt("PROJECT333_RUN_REWARD_TICKETS_PER_WIN", 0) * run.Wins +
                      ResolveInt("PROJECT333_RUN_REWARD_TICKETS_PER_LOSS", 0) * run.Losses;

        return new RunRewardSpec(
            Math.Max(0, resourceGold),
            Math.Max(0, tickets),
            ResolveRunEndCardRewards(run),
            ParseRewardMap(_configuration["PROJECT333_RUN_REWARD_PACK_REWARDS"]));
    }

    private IReadOnlyDictionary<string, int> ResolveRunEndCardRewards(RunSummaryDto run)
    {
        var rewards = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var database = PrototypeCardDefinitions.LoadCardDefinitionDatabase();
        AddRewardMap(rewards, ParseRewardMap(_configuration["PROJECT333_RUN_REWARD_CARD_REWARDS"]));
        AddRewardMap(rewards, CreateRandomRunCardRewards(run, database));
        RemoveNonRewardableCardRewards(rewards, database);
        return rewards;
    }

    private static IReadOnlyDictionary<string, int> CreateRandomRunCardRewards(
        RunSummaryDto run,
        JsonCardDefinitionDatabase database)
    {
        var rewardCount = Math.Max(0, run.Wins) * RandomCardRewardsPerWin;
        if (rewardCount <= 0)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        var cardPools = (database?.Cards ?? new List<JsonCardDefinitionRecord>())
            .Where(IsRewardableCard)
            .GroupBy(card => card.Rarity)
            .ToDictionary(
                group => group.Key,
                group => group.ToList());

        var rewards = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < rewardCount; i++)
        {
            if (!TryChooseRewardRarity(cardPools, out var rarity) ||
                !cardPools.TryGetValue(rarity, out var pool) ||
                pool.Count == 0)
            {
                break;
            }

            var selectedCard = pool[Random.Shared.Next(pool.Count)];
            rewards[selectedCard.Id] = rewards.TryGetValue(selectedCard.Id, out var existingCount)
                ? existingCount + 1
                : 1;
        }

        return rewards;
    }

    private static bool TryChooseRewardRarity(
        IReadOnlyDictionary<CardRarity, List<JsonCardDefinitionRecord>> cardPools,
        out CardRarity rarity)
    {
        var availableWeights = RunRewardRarityWeights
            .Where(weight => weight.Weight > 0 &&
                             cardPools.TryGetValue(weight.Rarity, out var pool) &&
                             pool.Count > 0)
            .ToArray();
        var totalWeight = availableWeights.Sum(weight => weight.Weight);
        if (totalWeight <= 0)
        {
            rarity = CardRarity.Common;
            return false;
        }

        var roll = Random.Shared.Next(totalWeight);
        foreach (var weightedRarity in availableWeights)
        {
            if (roll < weightedRarity.Weight)
            {
                rarity = weightedRarity.Rarity;
                return true;
            }

            roll -= weightedRarity.Weight;
        }

        rarity = availableWeights[^1].Rarity;
        return true;
    }

    private static bool IsExcludedFromRandomCardRewards(string cardId)
    {
        return string.Equals(cardId, "Master", StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveNonRewardableCardRewards(
        IDictionary<string, int> rewards,
        JsonCardDefinitionDatabase database)
    {
        if (rewards == null || rewards.Count == 0)
        {
            return;
        }

        var cards = database?.Cards ?? new List<JsonCardDefinitionRecord>();
        foreach (var cardId in rewards.Keys.ToArray())
        {
            var card = cards.FirstOrDefault(candidate =>
                candidate != null &&
                string.Equals(candidate.Id, cardId, StringComparison.OrdinalIgnoreCase));
            if (!IsRewardableCard(card))
            {
                rewards.Remove(cardId);
            }
        }
    }

    private static bool IsRewardableCard(JsonCardDefinitionRecord? card)
    {
        if (card == null ||
            string.IsNullOrWhiteSpace(card.Id) ||
            !card.IncludeInRewards ||
            IsExcludedFromRandomCardRewards(card.Id))
        {
            return false;
        }

        try
        {
            return CardUpgradeRules.IsCardUpgradeable(card.ToDefinition());
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void AddRewardMap(
        IDictionary<string, int> target,
        IReadOnlyDictionary<string, int> rewards)
    {
        if (rewards == null || rewards.Count == 0)
        {
            return;
        }

        foreach (var reward in rewards)
        {
            if (string.IsNullOrWhiteSpace(reward.Key) || reward.Value <= 0)
            {
                continue;
            }

            target[reward.Key] = target.TryGetValue(reward.Key, out var existingCount)
                ? existingCount + reward.Value
                : reward.Value;
        }
    }

    private static async Task<RewardGrantDto> InsertRewardGrantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid runId,
        RunRewardSpec reward,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into reward_grants(
                account_id,
                source_type,
                source_id,
                resource_gold_delta,
                ticket_delta,
                pack_rewards,
                card_rewards
            )
            values (
                @accountId,
                'run_end',
                @runId,
                @resourceGoldDelta,
                @ticketDelta,
                cast(@packRewards as jsonb),
                cast(@cardRewards as jsonb)
            )
            returning id, created_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("runId", runId);
        command.Parameters.AddWithValue("resourceGoldDelta", reward.ResourceGoldDelta);
        command.Parameters.AddWithValue("ticketDelta", reward.TicketDelta);
        command.Parameters.AddWithValue("packRewards", JsonSerializer.Serialize(reward.PackRewards));
        command.Parameters.AddWithValue("cardRewards", JsonSerializer.Serialize(reward.CardRewards));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new RunServiceException("reward_create_failed", "Failed to create a run-end reward grant.");
        }

        return new RewardGrantDto(
            reader.GetGuid(0),
            "run_end",
            runId,
            reward.ResourceGoldDelta,
            reward.TicketDelta,
            reward.CardRewards,
            reward.PackRewards,
            ToRewardItems(reward.CardRewards),
            ToRewardItems(reward.PackRewards),
            reader.GetFieldValue<DateTimeOffset>(1));
    }

    private static IReadOnlyList<RewardItemDto> ToRewardItems(IReadOnlyDictionary<string, int> rewards)
    {
        if (rewards == null || rewards.Count == 0)
        {
            return Array.Empty<RewardItemDto>();
        }

        return rewards
            .Where(reward => !string.IsNullOrWhiteSpace(reward.Key) && reward.Value > 0)
            .OrderBy(reward => reward.Key, StringComparer.OrdinalIgnoreCase)
            .Select(reward => new RewardItemDto(reward.Key, reward.Value))
            .ToArray();
    }

    private static async Task<WalletDto> ApplyWalletRewardAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        long resourceGoldDelta,
        int ticketDelta,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update user_wallets
            set
                resource_gold = resource_gold + @resourceGoldDelta,
                tickets = tickets + @ticketDelta,
                updated_at = now()
            where account_id = @accountId
            returning resource_gold, tickets;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("resourceGoldDelta", resourceGoldDelta);
        command.Parameters.AddWithValue("ticketDelta", ticketDelta);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new RunServiceException("wallet_not_found", "Wallet was not found for this account.");
        }

        return new WalletDto(
            reader.GetInt64(0),
            reader.GetInt32(1));
    }

    private static async Task ApplyCardRewardsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid rewardGrantId,
        IReadOnlyDictionary<string, int> cardRewards,
        CancellationToken cancellationToken)
    {
        const string collectionSql = """
            insert into user_card_collection(
                account_id,
                card_id,
                copy_count,
                upgrade_level
            )
            values (
                @accountId,
                @cardId,
                @copyCount,
                0
            )
            on conflict (account_id, card_id)
            do update set
                copy_count = user_card_collection.copy_count + excluded.copy_count,
                updated_at = now();
            """;

        foreach (var reward in cardRewards)
        {
            if (reward.Value <= 0)
            {
                continue;
            }

            await using (var command = new NpgsqlCommand(collectionSql, connection, transaction))
            {
                command.Parameters.AddWithValue("accountId", accountId);
                command.Parameters.AddWithValue("cardId", reward.Key);
                command.Parameters.AddWithValue("copyCount", reward.Value);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await InsertCardRewardTransactionAsync(
                connection,
                transaction,
                accountId,
                rewardGrantId,
                reward.Key,
                reward.Value,
                cancellationToken);
        }
    }

    private static async Task ApplyPackRewardsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid rewardGrantId,
        IReadOnlyDictionary<string, int> packRewards,
        CancellationToken cancellationToken)
    {
        const string inventorySql = """
            insert into user_pack_inventory(
                account_id,
                pack_id,
                quantity
            )
            values (
                @accountId,
                @packId,
                @quantity
            )
            on conflict (account_id, pack_id)
            do update set
                quantity = user_pack_inventory.quantity + excluded.quantity,
                updated_at = now();
            """;

        foreach (var reward in packRewards)
        {
            if (reward.Value <= 0)
            {
                continue;
            }

            await using (var command = new NpgsqlCommand(inventorySql, connection, transaction))
            {
                command.Parameters.AddWithValue("accountId", accountId);
                command.Parameters.AddWithValue("packId", reward.Key);
                command.Parameters.AddWithValue("quantity", reward.Value);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await InsertPackRewardTransactionAsync(
                connection,
                transaction,
                accountId,
                rewardGrantId,
                reward.Key,
                reward.Value,
                cancellationToken);
        }
    }

    private static async Task InsertWalletRewardTransactionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid rewardGrantId,
        long resourceGoldDelta,
        int ticketDelta,
        CancellationToken cancellationToken)
    {
        if (resourceGoldDelta == 0 && ticketDelta == 0)
        {
            return;
        }

        const string sql = """
            insert into account_transactions(
                account_id,
                transaction_type,
                resource_gold_delta,
                ticket_delta,
                source_table,
                source_id
            )
            values (
                @accountId,
                'reward',
                @resourceGoldDelta,
                @ticketDelta,
                'reward_grants',
                @rewardGrantId
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("resourceGoldDelta", resourceGoldDelta);
        command.Parameters.AddWithValue("ticketDelta", ticketDelta);
        command.Parameters.AddWithValue("rewardGrantId", rewardGrantId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertCardRewardTransactionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid rewardGrantId,
        string cardId,
        int copyCount,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into account_transactions(
                account_id,
                transaction_type,
                card_id,
                card_count_delta,
                source_table,
                source_id
            )
            values (
                @accountId,
                'reward',
                @cardId,
                @copyCount,
                'reward_grants',
                @rewardGrantId
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("cardId", cardId);
        command.Parameters.AddWithValue("copyCount", copyCount);
        command.Parameters.AddWithValue("rewardGrantId", rewardGrantId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertPackRewardTransactionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid rewardGrantId,
        string packId,
        int packCount,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into account_transactions(
                account_id,
                transaction_type,
                pack_id,
                pack_count_delta,
                source_table,
                source_id
            )
            values (
                @accountId,
                'reward',
                @packId,
                @packCount,
                'reward_grants',
                @rewardGrantId
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("packId", packId);
        command.Parameters.AddWithValue("packCount", packCount);
        command.Parameters.AddWithValue("rewardGrantId", rewardGrantId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<RunSummaryDto> MarkRunRewardsClaimedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid runId,
        DateTimeOffset claimedAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update draft_runs
            set
                reward_claimed_at = @claimedAt,
                updated_at = now()
            where id = @runId
            returning id, status, mode, wins, losses, ticket_cost_paid, started_at, reward_claimed_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("runId", runId);
        command.Parameters.AddWithValue("claimedAt", claimedAt);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new RunServiceException("run_update_failed", "Failed to mark the run rewards as claimed.");
        }

        return ReadRunSummary(reader);
    }

    private async Task<WalletDto> SpendTicketsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        int ticketCost,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update user_wallets
            set
                tickets = tickets - @ticketCost,
                updated_at = now()
            where account_id = @accountId
              and tickets >= @ticketCost
            returning resource_gold, tickets;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("ticketCost", ticketCost);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new RunServiceException(
                "insufficient_tickets",
                $"Not enough server tickets. Starting a draft run requires {ticketCost} tickets.");
        }

        return new WalletDto(
            reader.GetInt64(0),
            reader.GetInt32(1));
    }

    private static async Task LockRunStartWalletAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid accountId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select account_id from user_wallets where account_id = @accountId for update;", connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        if (await command.ExecuteScalarAsync(cancellationToken) == null)
            throw new RunServiceException("insufficient_tickets", "The account wallet is unavailable.");
    }
    private static async Task EnsureCanStartNewDraftRunAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select id, status, wins, losses, reward_claimed_at
            from draft_runs
            where account_id = @accountId
              and (status in ('drafting', 'ready', 'in_progress')
                   or (status = 'completed' and reward_claimed_at is null))
            order by case when status in ('drafting', 'ready', 'in_progress') then 0 else 1 end,
                     started_at desc, id
            limit 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return;
        }

        var latestRunId = reader.GetGuid(0);
        var latestRunStatus = reader.GetString(1);
        var latestRunWins = reader.GetInt32(2);
        var latestRunLosses = reader.GetInt32(3);
        var hasClaimedRewards = !reader.IsDBNull(4);

        if (IsResumableRunStatus(latestRunStatus))
        {
            throw new RunServiceException(
                "active_run_exists",
                $"A draft run is already active. Resume or finish it before starting a new run. run={latestRunId} status={latestRunStatus}");
        }

        if (string.Equals(latestRunStatus, "completed", StringComparison.OrdinalIgnoreCase) &&
            !hasClaimedRewards)
        {
            throw new RunServiceException(
                "unclaimed_run_rewards",
                $"Claim the previous run rewards before starting a new run. run={latestRunId} record={latestRunWins}-{latestRunLosses}");
        }
    }

    private static bool IsResumableRunStatus(string status)
    {
        return string.Equals(status, "drafting", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "ready", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "in_progress", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<RunSummaryDto> InsertDraftRunAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string mode,
        int draftSeed,
        int ticketCost,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into draft_runs(
                account_id,
                status,
                mode,
                draft_seed,
                ticket_cost_paid
            )
            values (
                @accountId,
                'drafting',
                @mode,
                @draftSeed,
                @ticketCost
            )
            returning id, status, mode, wins, losses, ticket_cost_paid, started_at, reward_claimed_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("mode", mode);
        command.Parameters.AddWithValue("draftSeed", draftSeed);
        command.Parameters.AddWithValue("ticketCost", ticketCost);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new RunServiceException("run_create_failed", "Failed to create a draft run.");
        }

        return ReadRunSummary(reader);
    }

    private static async Task ValidateDraftRunForCompletionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select status
            from draft_runs
            where id = @runId
              and account_id = @accountId
            for update;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("runId", runId);
        command.Parameters.AddWithValue("accountId", accountId);
        var status = await command.ExecuteScalarAsync(cancellationToken) as string;
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new RunServiceException("run_not_found", "Draft run was not found for this account.");
        }

        if (!string.Equals(status, "drafting", StringComparison.OrdinalIgnoreCase))
        {
            throw new RunServiceException(
                "run_not_drafting",
                $"Draft run cannot be completed while its status is '{status}'.");
        }
    }

    private async Task<IReadOnlyList<string>> GetOrCreateSavedOfferAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId,
        LockedAuthoritativeDraftRun run, IReadOnlyList<string> picks, CancellationToken ct)
    {
        List<string>? saved;
        try { saved = string.IsNullOrWhiteSpace(run.SavedOfferJson) ? null : JsonSerializer.Deserialize<List<string>>(run.SavedOfferJson); }
        catch (JsonException) { throw new RunServiceException("invalid_server_draft_offer", "Saved server offer is malformed; it was not replaced."); }
        if (saved is { Count: > 0 })
        {
            if (saved.Count != ServerDraftOfferGenerator.OfferSize || saved.Any(string.IsNullOrWhiteSpace) ||
                saved.Distinct(StringComparer.OrdinalIgnoreCase).Count() != saved.Count)
                throw new RunServiceException("invalid_server_draft_offer", "Saved server offer must contain 3 distinct card IDs.");
            return saved; // Preserve identity and order, never regenerate an already issued offer.
        }
        // Compatibility for old runs with no saved offer. The run row is already locked.
        var generated = _draftOfferGenerator.CreateOfferFromSavedPicks(run.DraftSeed, picks);
        await UpdateDraftRunCurrentOfferAsync(connection, transaction, runId, generated, ct);
        return generated;
    }
    private static async Task<LockedAuthoritativeDraftRun> LoadAuthoritativeDraftRunForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select
                id,
                status,
                mode,
                wins,
                losses,
                ticket_cost_paid,
                started_at,
                reward_claimed_at,
                draft_seed,
                current_offer_card_ids::text
            from draft_runs
            where id = @runId
              and account_id = @accountId
            for update;
            """;

        RunSummaryDto run;
        int? draftSeed;
        string? savedOfferJson;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("runId", runId);
            command.Parameters.AddWithValue("accountId", accountId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new RunServiceException("run_not_found", "Draft run was not found for this account.");
            }

            run = ReadRunSummary(reader);
            draftSeed = reader.IsDBNull(8) ? null : reader.GetInt32(8);
            savedOfferJson = reader.IsDBNull(9) ? null : reader.GetString(9);
        }

        if (!string.Equals(run.Status, "drafting", StringComparison.OrdinalIgnoreCase))
        {
            throw new RunServiceException(
                "run_not_drafting",
                $"Authoritative draft actions are not allowed while the run status is '{run.Status}'.");
        }

        if (!draftSeed.HasValue)
        {
            draftSeed = Random.Shared.Next();
            const string updateSeedSql = """
                update draft_runs
                set draft_seed = @draftSeed,
                    updated_at = now()
                where id = @runId;
                """;
            await using var updateSeedCommand = new NpgsqlCommand(updateSeedSql, connection, transaction);
            updateSeedCommand.Parameters.AddWithValue("runId", runId);
            updateSeedCommand.Parameters.AddWithValue("draftSeed", draftSeed.Value);
            await updateSeedCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        return new LockedAuthoritativeDraftRun(run, draftSeed.Value, savedOfferJson);
    }

    private static async Task<IReadOnlyList<string>> LoadDraftRunPickCardIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid runId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select selected_card_id
            from draft_run_picks
            where run_id = @runId
            order by pick_index;
            """;

        var cardIds = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("runId", runId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            cardIds.Add(reader.GetString(0));
        }

        return cardIds;
    }

    private static async Task AppendAuthoritativeDraftPickAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid runId,
        int pickIndex,
        IReadOnlyList<string> offeredCardIds,
        string selectedCardId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into draft_run_picks(
                run_id,
                pick_index,
                offered_card_ids,
                selected_card_id
            )
            values (
                @runId,
                @pickIndex,
                cast(@offeredCardIds as jsonb),
                @selectedCardId
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("runId", runId);
        command.Parameters.AddWithValue("pickIndex", pickIndex);
        command.Parameters.AddWithValue("offeredCardIds", JsonSerializer.Serialize(offeredCardIds));
        command.Parameters.AddWithValue("selectedCardId", selectedCardId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<RunSummaryDto> ValidateDraftRunForPickSaveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select id, status, mode, wins, losses, ticket_cost_paid, started_at, reward_claimed_at
            from draft_runs
            where id = @runId
              and account_id = @accountId
            for update;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("runId", runId);
        command.Parameters.AddWithValue("accountId", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new RunServiceException("run_not_found", "Draft run was not found for this account.");
        }

        var run = ReadRunSummary(reader);
        if (!string.Equals(run.Status, "drafting", StringComparison.OrdinalIgnoreCase))
        {
            throw new RunServiceException(
                "run_not_drafting",
                $"Draft picks cannot be saved while the run status is '{run.Status}'.");
        }

        return run;
    }

    private static async Task<int> CountDraftRunPicksAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid runId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select count(*)
            from draft_run_picks
            where run_id = @runId;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("runId", runId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private static async Task ReplaceDraftRunPicksAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid runId,
        IReadOnlyList<string> cardIds,
        CancellationToken cancellationToken)
    {
        const string deleteSql = """
            delete from draft_run_picks
            where run_id = @runId;
            """;

        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("runId", runId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = """
            insert into draft_run_picks(
                run_id,
                pick_index,
                offered_card_ids,
                selected_card_id
            )
            values (
                @runId,
                @pickIndex,
                '[]'::jsonb,
                @cardId
            );
            """;

        for (var i = 0; i < cardIds.Count; i++)
        {
            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("runId", runId);
            insertCommand.Parameters.AddWithValue("pickIndex", i);
            insertCommand.Parameters.AddWithValue("cardId", cardIds[i]);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task UpdateDraftRunCurrentOfferAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid runId,
        IReadOnlyList<string> currentOfferCardIds,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update draft_runs
            set
                current_offer_card_ids = cast(@currentOfferCardIds as jsonb),
                updated_at = now()
            where id = @runId;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("runId", runId);
        command.Parameters.AddWithValue("currentOfferCardIds", JsonSerializer.Serialize(currentOfferCardIds ?? Array.Empty<string>()));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<DeckSummaryDto> InsertCompletedDeckAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid runId,
        IReadOnlyList<string> cardIds,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into decks(
                account_id,
                name,
                deck_type,
                source_run_id,
                card_definition_version
            )
            values (
                @accountId,
                @name,
                'draft_run',
                @runId,
                @cardDefinitionVersion
            )
            returning id, name, deck_type, source_run_id, card_definition_version;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("name", $"Draft Run {DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}");
        command.Parameters.AddWithValue("runId", runId);
        command.Parameters.AddWithValue("cardDefinitionVersion", ResolveCardDefinitionVersion());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new RunServiceException("deck_create_failed", "Failed to create the completed draft deck.");
        }

        return new DeckSummaryDto(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetGuid(3),
            cardIds.Count,
            reader.GetString(4));
    }

    private static async Task InsertDeckCardsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid deckId,
        IReadOnlyList<string> cardIds,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into deck_cards(
                deck_id,
                slot_index,
                card_id
            )
            values (
                @deckId,
                @slotIndex,
                @cardId
            );
            """;

        for (var i = 0; i < cardIds.Count; i++)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("deckId", deckId);
            command.Parameters.AddWithValue("slotIndex", i);
            command.Parameters.AddWithValue("cardId", cardIds[i]);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<RunSummaryDto> MarkDraftRunReadyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid runId,
        Guid deckId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update draft_runs
            set
                status = 'ready',
                completed_deck_id = @deckId,
                updated_at = now()
            where id = @runId
            returning id, status, mode, wins, losses, ticket_cost_paid, started_at, reward_claimed_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("runId", runId);
        command.Parameters.AddWithValue("deckId", deckId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new RunServiceException("run_update_failed", "Failed to mark the draft run as ready.");
        }

        return ReadRunSummary(reader);
    }

    private static async Task InsertTicketSpendTransactionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid runId,
        int ticketCost,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into account_transactions(
                account_id,
                transaction_type,
                ticket_delta,
                source_table,
                source_id
            )
            values (
                @accountId,
                'ticket_spend',
                @ticketDelta,
                'draft_runs',
                @runId
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("ticketDelta", -ticketCost);
        command.Parameters.AddWithValue("runId", runId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private int ResolveTicketCost()
    {
        var ticketCost = ResolveInt("PROJECT333_DRAFT_RUN_TICKET_COST", 3);
        return Math.Max(0, ticketCost);
    }

    private int ResolveInt(string key, int fallback)
    {
        return int.TryParse(_configuration[key], out var value) ? value : fallback;
    }

    private long ResolveLong(string key, long fallback)
    {
        return long.TryParse(_configuration[key], out var value) ? value : fallback;
    }

    private string ResolveCardDefinitionVersion()
    {
        var value = _configuration["PROJECT333_CARD_DEFINITION_VERSION"];
        return string.IsNullOrWhiteSpace(value) ? "cards.json" : value;
    }

    private static Guid ParseRunId(string? runId)
    {
        if (Guid.TryParse(runId, out var parsedRunId))
        {
            return parsedRunId;
        }

        throw new RunServiceException("invalid_run_id", "A valid draft run id is required.");
    }

    private static Guid ParseDeckId(string? deckId)
    {
        if (Guid.TryParse(deckId, out var parsedDeckId))
        {
            return parsedDeckId;
        }

        throw new RunServiceException("invalid_deck_id", "A valid draft deck id is required.");
    }

    private static IReadOnlyList<string> NormalizeDeckCardIds(IReadOnlyList<string>? cardIds)
    {
        if (cardIds == null || cardIds.Count != DraftDeckSize)
        {
            throw new RunServiceException(
                "invalid_deck_size",
                $"Completed draft deck must contain exactly {DraftDeckSize} cards.");
        }

        var normalized = new string[cardIds.Count];
        for (var i = 0; i < cardIds.Count; i++)
        {
            var cardId = cardIds[i]?.Trim();
            if (string.IsNullOrWhiteSpace(cardId))
            {
                throw new RunServiceException("invalid_deck_card", $"Deck card at slot {i} is empty.");
            }

            normalized[i] = cardId;
        }

        return normalized;
    }

    private static IReadOnlyList<string> NormalizeDraftPickCardIds(IReadOnlyList<string>? cardIds)
    {
        if (cardIds == null)
        {
            throw new RunServiceException("invalid_draft_picks", "Draft pick card ids are required.");
        }

        if (cardIds.Count >= DraftDeckSize)
        {
            throw new RunServiceException(
                "invalid_draft_pick_count",
                $"In-progress draft picks must contain fewer than {DraftDeckSize} cards.");
        }

        var normalized = new string[cardIds.Count];
        for (var i = 0; i < cardIds.Count; i++)
        {
            var cardId = cardIds[i]?.Trim();
            if (string.IsNullOrWhiteSpace(cardId))
            {
                throw new RunServiceException("invalid_draft_pick_card", $"Draft pick at slot {i} is empty.");
            }

            normalized[i] = cardId;
        }

        return normalized;
    }

    private static IReadOnlyList<string> NormalizeCurrentOfferCardIds(IReadOnlyList<string>? cardIds)
    {
        if (cardIds == null || cardIds.Count == 0)
        {
            return Array.Empty<string>();
        }

        if (cardIds.Count != 3)
        {
            throw new RunServiceException(
                "invalid_draft_offer_count",
                "Current draft offer must contain exactly 3 cards.");
        }

        var normalized = new string[cardIds.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < cardIds.Count; i++)
        {
            var cardId = cardIds[i]?.Trim();
            if (string.IsNullOrWhiteSpace(cardId))
            {
                throw new RunServiceException("invalid_draft_offer_card", $"Draft offer card at slot {i} is empty.");
            }

            if (!seen.Add(cardId))
            {
                throw new RunServiceException("duplicate_draft_offer_card", $"Draft offer contains duplicate card '{cardId}'.");
            }

            normalized[i] = cardId;
        }

        return normalized;
    }

    private static IReadOnlyList<string> DeserializeCardIdList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            var cardIds = JsonSerializer.Deserialize<List<string>>(json);
            if (cardIds == null || cardIds.Count == 0)
            {
                return Array.Empty<string>();
            }

            return cardIds
                .Where(cardId => !string.IsNullOrWhiteSpace(cardId))
                .Select(cardId => cardId.Trim())
                .ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static RunSummaryDto ReadRunSummary(NpgsqlDataReader reader)
    {
        return new RunSummaryDto(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7));
    }

    private static string NormalizeMode(string? mode)
    {
        if (string.Equals(mode, "pvp", StringComparison.OrdinalIgnoreCase))
        {
            return "pvp";
        }

        return "pve";
    }

    private static IReadOnlyDictionary<string, int> ParseRewardMap(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return new Dictionary<string, int>();
        }

        var rewards = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var entries = rawValue.Split(
            new[] { ',', ';' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entry in entries)
        {
            var pair = entry.Split(
                ':',
                2,
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (pair.Length != 2 ||
                string.IsNullOrWhiteSpace(pair[0]) ||
                !int.TryParse(pair[1], out var count) ||
                count <= 0)
            {
                continue;
            }

            rewards[pair[0]] = rewards.TryGetValue(pair[0], out var existingCount)
                ? existingCount + count
                : count;
        }

        return rewards;
    }

    private sealed record RunRewardSpec(
        long ResourceGoldDelta,
        int TicketDelta,
        IReadOnlyDictionary<string, int> CardRewards,
        IReadOnlyDictionary<string, int> PackRewards);

    private sealed record LockedAuthoritativeDraftRun(
        RunSummaryDto Run,
        int DraftSeed,
        string? SavedOfferJson);
}
