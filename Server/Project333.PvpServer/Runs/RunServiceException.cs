namespace Project333.PvpServer.Runs;

public sealed class RunServiceException : Exception
{
    public RunServiceException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
