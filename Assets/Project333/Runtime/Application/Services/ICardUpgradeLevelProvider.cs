using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Application.Services
{
    public interface ICardUpgradeLevelProvider
    {
        int GetUpgradeLevel(PlayerId ownerId, string cardId);
    }
}
