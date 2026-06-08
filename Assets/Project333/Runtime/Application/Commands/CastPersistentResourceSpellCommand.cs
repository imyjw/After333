namespace Project333.Runtime.Application.Commands
{
    public sealed class CastPersistentResourceSpellCommand : IBattleCommand
    {
        public CastPersistentResourceSpellCommand(string cardId)
        {
            CardId = cardId;
        }

        public string CardId { get; }
    }
}
