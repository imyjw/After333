namespace Project333.PvpServer.Messages;

public sealed class OnlineBattleErrorDto
{
    public string Code { get; set; } = string.Empty;

        public long? CommandSequence { get; set; }

    public string Message { get; set; } = string.Empty;
}
