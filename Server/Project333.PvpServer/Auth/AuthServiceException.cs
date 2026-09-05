namespace Project333.PvpServer.Auth;

public sealed class AuthServiceException : Exception
{
    public AuthServiceException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
