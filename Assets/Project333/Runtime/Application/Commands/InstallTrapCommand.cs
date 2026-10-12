#nullable enable
namespace Project333.Runtime.Application.Commands
{
    public sealed class InstallTrapCommand : IHandCardCommand
    {
        public InstallTrapCommand(string cardId, string? handCardRuntimeId = null)
        {
            CardId = cardId;
            HandCardRuntimeId = handCardRuntimeId ?? string.Empty;
        }

        public string CardId { get; }
        public string HandCardRuntimeId { get; }
    }
}
