using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Domain.Cards
{
    public sealed class BuildingState : OccupantState
    {
        public BuildingState(
            string runtimeId,
            string cardId,
            PlayerId ownerId,
            TileCoord position,
            bool canAttack,
            int attack,
            int maxHp,
            ResourceSet turnStartResourceGain = null,
            int hitsPerAttack = 1,
            DamageType damageType = DamageType.Physical,
            int physicalDefense = 0,
            int magicDefense = 0,
            int sciencePowerUpkeep = 0,
            bool hasFlying = false,
            int spellPower = 0,
            bool hasPiercing = false)
            : base(
                runtimeId,
                cardId,
                ownerId,
                OccupantKind.Building,
                position,
                AttackType.Ranged,
                attack,
                maxHp,
                false,
                turnStartResourceGain ?? new ResourceSet(),
                canAttack ? 1 : 0,
                hitsPerAttack,
                damageType: damageType,
                physicalDefense: physicalDefense,
                magicDefense: magicDefense,
                hasFlying: hasFlying,
                spellPower: spellPower,
                hasPiercing: hasPiercing)
        {
            CanAttackAsBuilding = canAttack;
            SciencePowerUpkeep = sciencePowerUpkeep;
        }

        public bool CanAttackAsBuilding { get; }
        public int SciencePowerUpkeep { get; }
    }
}
