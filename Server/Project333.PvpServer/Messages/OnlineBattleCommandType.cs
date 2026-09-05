namespace Project333.PvpServer.Messages;

public enum OnlineBattleCommandType
{
    Unknown = 0,
    StartBattle = 1,
    ApplyMulligan = 2,
    PassMulligan = 3,
    ResolveTurnStart = 4,
    PlayUnitCard = 5,
    PlayBuildingCard = 6,
    CastDamageSpell = 7,
    CastPersistentResourceSpell = 8,
    CastScriptedSpell = 9,
    MoveOccupant = 10,
    Attack = 11,
    EndTurn = 12,
    Surrender = 13
}
