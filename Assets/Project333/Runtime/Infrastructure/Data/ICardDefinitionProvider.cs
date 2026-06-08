namespace Project333.Runtime.Infrastructure.Data
{
    public interface ICardDefinitionProvider
    {
        CardDefinition GetRequired(string cardId);
    }
}
