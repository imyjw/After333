namespace Project333.Runtime.Application.Online
{
    public interface IOnlineBattleMessageSender
    {
        void Send(ClientBattleCommandMessage message);
    }
}
