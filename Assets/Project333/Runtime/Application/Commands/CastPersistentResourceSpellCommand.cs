namespace Project333.Runtime.Application.Commands
{
    public sealed class CastPersistentResourceSpellCommand : IHandCardCommand
    {
        public CastPersistentResourceSpellCommand(string cardId, string handCardRuntimeId = null)
        {
            CardId = cardId;
            HandCardRuntimeId = handCardRuntimeId ?? string.Empty;
        }

        public string CardId { get; }

        public string HandCardRuntimeId { get; }
    }
}
