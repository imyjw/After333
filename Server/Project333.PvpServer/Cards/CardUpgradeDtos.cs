using Project333.PvpServer.Auth;

namespace Project333.PvpServer.Cards;

public sealed record UpgradeCardRequest(
    string? CardId);

public sealed record UpgradeCardCostDto(
    int LevelFrom,
    int LevelTo,
    int RequiredCopyCount,
    long RequiredResourceGold);

public sealed record UpgradeCardResponse(
    AuthAccountDto Account,
    WalletDto Wallet,
    CollectionSummaryDto CollectionSummary,
    OwnedCardDto UpgradedCard,
    UpgradeCardCostDto UpgradeCost);
