namespace Project333.Runtime.Application.Online
{
    public enum OnlineBattleMessageType
    {
        Unknown = 0,
        ClientCommand = 1,
        StateView = 2,
        BattleEvents = 3,
        Error = 4,
        KeepAlive = 5,
        JoinMatch = 6
    }
}
