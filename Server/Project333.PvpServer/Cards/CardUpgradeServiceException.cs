namespace Project333.PvpServer.Cards;

public sealed class CardUpgradeServiceException : Exception
{
    public CardUpgradeServiceException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
