namespace Project333.Runtime.Application.Online
{
    public sealed class OnlineBattleErrorDto
    {
        public string Code { get; set; } = string.Empty;

        public long? CommandSequence { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}
