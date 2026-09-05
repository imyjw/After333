using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Application.Services
{
    public sealed class ZeroCardUpgradeLevelProvider : ICardUpgradeLevelProvider
    {
        public static readonly ZeroCardUpgradeLevelProvider Instance = new ZeroCardUpgradeLevelProvider();

        private ZeroCardUpgradeLevelProvider()
        {
        }

        public int GetUpgradeLevel(PlayerId ownerId, string cardId)
        {
            return 0;
        }
    }
}
