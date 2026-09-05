using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Presentation.Draft
{
    public interface IDraftOverlayHost
    {
        void SelectDraftCard(string cardId);

        void ReturnFromDraftOverlay();

        bool TryGetCardDefinitionAsset(string cardId, out CardDefinitionAsset cardAsset);
    }
}
