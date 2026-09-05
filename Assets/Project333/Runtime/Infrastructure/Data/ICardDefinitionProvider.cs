namespace Project333.Runtime.Infrastructure.Data
{
    public interface ICardDefinitionProvider
    {
        CardDefinition GetRequired(string cardId);

        System.Collections.Generic.IReadOnlyList<CardDefinition> GetAll();
    }
}
