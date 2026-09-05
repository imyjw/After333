namespace Project333.Runtime.Application.Commands
{
    public interface IBattleCommand
    {
    }

    public interface IHandCardCommand : IBattleCommand
    {
        string CardId { get; }

        string HandCardRuntimeId { get; }
    }
}
