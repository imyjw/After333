using Project333.PvpServer.Auth;

namespace Project333.PvpServer.Shop;

public sealed record PurchaseCardPackRequest(string? RequestId);

// CardIds retains the original draw order, including repeated cards.
public sealed record PurchaseCardPackResponse(
    string RequestId,
    bool Replayed,
    long ResourceGoldCost,
    AuthAccountDto Account,
    WalletDto Wallet,
    IReadOnlyList<string> CardIds);

public sealed class CardPackServiceException(string code, string message, int statusCode = 409) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
